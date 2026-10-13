using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Independent 4.5-minute room timer for Stage 2 (Storage Room).
    /// Displays a sleek countdown timer on the HUD, pauses during modal/pause screens,
    /// turns warning amber/red as time wanes.
    /// When time expires, displays a 'YOU HAVE BEEN CAUGHT' containment failure screen
    /// with interactive REPLAY and MAIN MENU options (clickable and key-bindable via R / M).
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2Timer : MonoBehaviour
    {
        private static Stage2Timer instance;
        public static Stage2Timer Instance => instance;

        [Header("Timer Parameters")]
        [Tooltip("Total stage time limit in seconds (4.5 minutes = 270 seconds).")]
        [SerializeField] private float totalTimeSeconds = 270.0f; // 4.5 minutes (270 seconds)

        [Header("State")]
        [SerializeField] private float remainingTime;
        [SerializeField] private bool isRunning = true;
        [SerializeField] private bool isExpired = false;

        public float RemainingTime => remainingTime;
        public float TotalTimeSeconds => totalTimeSeconds;
        public float ElapsedTime => Mathf.Max(0f, totalTimeSeconds - remainingTime);
        public bool IsRunning => isRunning;
        public bool IsExpired => isExpired;

        public static event Action OnTimerExpired;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            totalTimeSeconds = 270.0f; // Explicitly enforce 4.5 minutes (270s) for Stage 2
            ResetTimer();
        }

        private void Start()
        {
            if (remainingTime <= 0f && !isExpired)
            {
                ResetTimer();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            Time.timeScale = 1f;
        }

        public void ResetTimer()
        {
            totalTimeSeconds = 270.0f; // 4.5 minutes
            remainingTime = totalTimeSeconds;
            isRunning = true;
            isExpired = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            totalTimeSeconds = 270.0f;
        }
#endif

        public void StopTimer()
        {
            isRunning = false;
        }

        private void Update()
        {
            if (isExpired)
            {
                // Ensure PlayerLook remains disabled so it cannot capture/eat mouse clicks
                var playerLook = FindAnyObjectByType<PlayerLook>();
                if (playerLook != null && playerLook.enabled)
                {
                    playerLook.enabled = false;
                }

                UnlockCursor();

                // Keyboard shortcuts for convenience (R = Replay, M or Esc = Main Menu)
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                    {
                        Stage2Audio.Instance?.PlayButtonPress();
                        ReplayStage();
                        return;
                    }
                    if (keyboard.mKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                    {
                        Stage2Audio.Instance?.PlayButtonPress();
                        ReturnToMainMenu();
                        return;
                    }
                }

                return;
            }

            if (!isRunning) return;

            // Pause if game is paused or modals are active
            if (Time.timeScale <= 0f) return;
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            if (Stage2ExitTrigger.IsStage2Complete || Stage2IntroUI.IsIntroOpen) return;

            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                HandleTimerExpired();
            }
        }

        private void HandleTimerExpired()
        {
            isExpired = true;
            isRunning = false;

            Stage2Audio.Instance?.PlayWrongAnswer();
            Stage2SecurityAlarmEvent.Instance?.TriggerAlarm();
            OnTimerExpired?.Invoke();

            // Disable PlayerLook so it cannot re-lock the mouse cursor on click!
            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.enabled = false;
            }

            // Also disable PlayerController movement
            var playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // Freeze game time and unlock cursor for user to choose Replay or Main Menu
            Time.timeScale = 0f;
            UnlockCursor();

            Debug.Log("<color=#d9534f><b>[Stage2Timer]</b></color> Time expired! Displaying 'YOU HAVE BEEN CAUGHT' failure screen.");
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(false);
            }
        }

        public void ReplayStage()
        {
            Time.timeScale = 1f;
            isExpired = false;

            // Re-enable Player components before reloading
            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null) playerLook.enabled = true;

            var playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null) playerController.enabled = true;

            var active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex);
            else
                SceneManager.LoadScene(active.name);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            isExpired = false;

            // Re-enable Player components
            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null) playerLook.enabled = true;

            var playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null) playerController.enabled = true;

            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenu();
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        private void OnGUI()
        {
            if (Stage2ExitTrigger.IsStage2Complete || Stage2IntroUI.IsIntroOpen) return;

            // When time runs out, display the Caught / Failure modal
            if (isExpired)
            {
                DrawFailureScreen();
                return;
            }

            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

            // High-visibility timer HUD widget at top-right
            float panelW = 140f;
            float panelH = 50f;
            float panelX = Screen.width - panelW - 16f;
            float panelY = 14f;

            Color oldColor = GUI.color;

            // Determine text color based on urgency
            Color timeColor;
            if (remainingTime <= 30f)
            {
                timeColor = new Color(0.95f, 0.30f, 0.30f); // Red (< 30s)
            }
            else if (remainingTime <= 90f)
            {
                timeColor = new Color(0.98f, 0.75f, 0.25f); // Amber (< 1.5 min)
            }
            else
            {
                timeColor = Color.white;
            }

            // Dark sleek backdrop
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Left accent line
            GUI.color = timeColor * new Color(1f, 1f, 1f, 0.90f);
            GUI.DrawTexture(new Rect(panelX, panelY, 3f, panelH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.65f, 0.75f, 0.85f) }
            };
            GUI.Label(new Rect(panelX + 10, panelY + 4, panelW - 14, 16), "TIME REMAINING", subtitleStyle);

            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);

            var timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = timeColor }
            };
            GUI.Label(new Rect(panelX + 10, panelY + 18, panelW - 14, 28), $"{minutes:D2}:{seconds:D2}", timerStyle);

            GUI.color = oldColor;
        }

        private void DrawFailureScreen()
        {
            GUI.depth = -100; // Render on top of all other GUI elements and receive clicks first

            Color oldColor = GUI.color;

            // 1. Fullscreen dark vignette with red warning tint
            GUI.color = new Color(0.10f, 0.02f, 0.02f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // 2. Central Failure Card
            float winW = Mathf.Min(560f, Screen.width * 0.90f);
            float winH = Mathf.Min(410f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            // Card Chassis background
            GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Red Emergency Warning Border
            GUI.color = new Color(0.95f, 0.25f, 0.25f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Top Header Banner
            GUI.color = new Color(0.30f, 0.08f, 0.08f, 1f);
            GUI.Box(new Rect(winX + 6, winY + 6, winW - 12, 56f), GUIContent.none);

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.95f, 0.95f) }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 10, winY + 12, winW - 20, 26f), "✖ YOU HAVE BEEN CAUGHT", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.55f, 0.55f) }
            };
            GUI.Label(new Rect(winX + 10, winY + 38, winW - 20, 18f), "TIME LIMIT EXPIRED // FACILITY CONTAINMENT PROTOCOL ENGAGED", subStyle);

            float curY = winY + 74f;

            // Incident Briefing Box
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.95f);
            GUI.Box(new Rect(winX + 24, curY, winW - 48, 180f), GUIContent.none);

            var statHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.40f) }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 30, curY + 10, winW - 60, 20f), "SECURITY INCIDENT REPORT", statHeaderStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(winX + 45, curY + 38, winW - 90, 22f), "• Sector: Sector B (Automated Storage)", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 64, winW - 90, 22f), "• Time Allotted: 04:30 [EXCEEDED]", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 90, winW - 90, 22f), "• Perimeter Status: Blast doors mag-locked under containment", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 116, winW - 90, 22f), "• Outcome: Security forces have intercepted the sector", bodyStyle);

            var alertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.35f, 0.35f) }
            };
            GUI.Label(new Rect(winX + 30, curY + 146, winW - 60, 22f), "MISSION FAILED: You failed to escape before time expired.", alertStyle);

            curY += 196f;

            // Action Buttons
            float btnW = (winW - 64f) * 0.5f;
            float btnH = 46f;
            Rect replayBtnRect = new Rect(winX + 24, curY, btnW, btnH);
            Rect menuBtnRect = new Rect(winX + 40 + btnW, curY, btnW, btnH);

            var actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            // REPLAY Button (Cyan / Emerald accent)
            GUI.backgroundColor = new Color(0.18f, 0.65f, 0.85f, 1f);
            if (GUI.Button(replayBtnRect, "⟲ REPLAY STAGE 2 [R]", actionBtnStyle))
            {
                Stage2Audio.Instance?.PlayButtonPress();
                ReplayStage();
            }

            // MAIN MENU Button (Slate Gray accent)
            GUI.backgroundColor = new Color(0.30f, 0.34f, 0.42f, 1f);
            if (GUI.Button(menuBtnRect, "⌂ MAIN MENU [M]", actionBtnStyle))
            {
                Stage2Audio.Instance?.PlayButtonPress();
                ReturnToMainMenu();
            }

            // Direct Mouse Click Fallback
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0)
            {
                if (replayBtnRect.Contains(e.mousePosition))
                {
                    Stage2Audio.Instance?.PlayButtonPress();
                    ReplayStage();
                    e.Use();
                }
                else if (menuBtnRect.Contains(e.mousePosition))
                {
                    Stage2Audio.Instance?.PlayButtonPress();
                    ReturnToMainMenu();
                    e.Use();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
