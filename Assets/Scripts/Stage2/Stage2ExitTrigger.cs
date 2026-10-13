using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Trigger volume located at the Stage 2 (Storage Room) exit blast door.
    /// When the player enters after completing the prerequisites, ends Stage 2,
    /// stops the timer, records completion metrics, unlocks Stage 3, and displays the Mission Debriefing modal.
    /// Fully responsive to mouse clicks (GUI.Button + direct MouseDown) and keyboard hotkeys.
    /// Hotkeys: [Enter]/[Space]/[C] = Continue, [R] = Replay Stage 2.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class Stage2ExitTrigger : MonoBehaviour
    {
        private static Stage2ExitTrigger instance;
        public static Stage2ExitTrigger Instance => instance;

        public static bool IsStage2Complete => instance != null && instance.isStage2Complete;

        [Header("State")]
        [SerializeField] private bool isStage2Complete = false;
        [SerializeField] private float completionTime = 0f;
        [SerializeField] private string completionRating = "S";

        [Header("Scene Transition")]
        [SerializeField] private string nextSceneName = "SecurityControlRoom";

        public event Action OnStage2Completed;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            isStage2Complete = false;

            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (!isStage2Complete) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            bool continuePressed = false;
            bool replayPressed = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.spaceKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
                {
                    continuePressed = true;
                }
                else if (kb.rKey.wasPressedThisFrame)
                {
                    replayPressed = true;
                }
            }


            if (continuePressed)
            {
                Stage2Audio.Instance?.PlayButtonPress();
                CompleteStage2();
            }
            else if (replayPressed)
            {
                Stage2Audio.Instance?.PlayButtonPress();
                ReplayStage();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isStage2Complete) return;

            // Only player triggers exit
            if (other.GetComponent<PlayerController>() != null || other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            {
                TriggerStage2Complete();
            }
        }

        public void TriggerStage2Complete()
        {
            if (isStage2Complete) return;

            isStage2Complete = true;

            // Complete final objective step
            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentStep != null)
            {
                ObjectiveManager.Instance.CompleteFlag(ObjectiveManager.Instance.CurrentStep.CompletionFlag);
            }

            // Stop stage timer and capture time
            if (Stage2Timer.Instance != null)
            {
                completionTime = Stage2Timer.Instance.ElapsedTime;
                Stage2Timer.Instance.StopTimer();
            }
            else
            {
                completionTime = 120f;
            }

            // Save progression: unlock Stage 3
            GameProgress.SetStageUnlocked(3);

            // Compute rating
            if (completionTime < 90f)
                completionRating = "EXEMPLARY [S-RANK]";
            else if (completionTime < 180f)
                completionRating = "PROFICIENT [A-RANK]";
            else
                completionRating = "QUALIFIED [B-RANK]";

            // Disarm siren if still active
            Stage2SecurityAlarmEvent.Instance?.DisarmAlarm();

            // Play completion audio
            Stage2Audio.Instance?.PlayStageComplete();

            // Unlock mouse cursor for completion UI and disable player controls
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(false);
                look.enabled = false;
            }

            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            OnStage2Completed?.Invoke();
            Debug.Log($"<color=#5cb85c><b>[Stage2ExitTrigger]</b></color> STAGE 2 COMPLETE! Time: {completionTime:F1}s | Rating: {completionRating}");
        }

        public void ReplayStage()
        {
            Time.timeScale = 1f;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.enabled = true;
            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) controller.enabled = true;

            var active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex);
            else
                SceneManager.LoadScene(active.name);
        }

        /// <summary>
        /// Transition method for integration into the main project.
        /// Loads nextSceneName if configured, otherwise restarts stage or notifies GameManager.
        /// </summary>
        public void CompleteStage2()
        {
            Debug.Log($"[Stage2ExitTrigger] CompleteStage2() invoked. Next scene target: '{nextSceneName}'.");
            Time.timeScale = 1f;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.enabled = true;
            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) controller.enabled = true;

            string target = !string.IsNullOrEmpty(nextSceneName) ? nextSceneName : GameProgress.GetStageSceneName(3);
            if (Application.CanStreamedLevelBeLoaded(target))
            {
                SceneManager.LoadScene(target);
            }
            else if (Application.CanStreamedLevelBeLoaded("SecurityControlRoom"))
            {
                SceneManager.LoadScene("SecurityControlRoom");
            }
            else if (Application.CanStreamedLevelBeLoaded("VaultCellar"))
            {
                SceneManager.LoadScene("VaultCellar");
            }
            else
            {
                // Fallback: search build settings for Stage 3 / SecurityControlRoom / VaultCellar
                bool loaded = false;
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(i);
                    if (path.IndexOf("SecurityControlRoom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("VaultCellar", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SceneManager.LoadScene(i);
                        loaded = true;
                        break;
                    }
                }

                if (!loaded)
                {
                    if (Application.CanStreamedLevelBeLoaded("MainMenu"))
                    {
                        SceneManager.LoadScene("MainMenu");
                    }
                    else
                    {
                        ReplayStage();
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!isStage2Complete) return;

            GUI.depth = -100;

            float winW = Mathf.Min(560f, Screen.width * 0.90f);
            float winH = Mathf.Min(420f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            Color oldColor = GUI.color;

            // Chassis
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Green/Cyan Border
            GUI.color = new Color(0.25f, 0.85f, 0.45f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Banner
            GUI.color = new Color(0.10f, 0.22f, 0.14f, 1f);
            GUI.Box(new Rect(winX + 6, winY + 6, winW - 12, 54f), GUIContent.none);

            // Title (White #FFFFFF)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 10, winY + 12, winW - 20, 26f), "✔ STAGE 2: STORAGE ROOM COMPLETE", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.95f, 0.80f) }
            };
            GUI.Label(new Rect(winX + 10, winY + 38, winW - 20, 20f), "SECTOR ACCESS BADGE ACQUIRED // EVACUATION PASSAGE OPEN", subStyle);

            float curY = winY + 75f;

            // Stats Card
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);
            GUI.Box(new Rect(winX + 24, curY, winW - 48, 175f), GUIContent.none);

            var statHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 30, curY + 12, winW - 60, 22f), "MISSION DEBRIEFING", statHeaderStyle);

            int minutes = Mathf.FloorToInt(completionTime / 60f);
            int seconds = Mathf.FloorToInt(completionTime % 60f);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(winX + 45, curY + 44, winW - 90, 22f), $"• Clearance Time: {minutes:D2}:{seconds:D2}", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 70, winW - 90, 22f), "• Pattern Logic Sequence: Verified", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 96, winW - 90, 22f), "• Storage Container Vault: Unlocked", bodyStyle);
            GUI.Label(new Rect(winX + 45, curY + 122, winW - 90, 22f), "• Security Alarm: Disarmed at Reader", bodyStyle);

            var rankStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.98f, 0.90f, 0.35f) }
            };
            GUI.Label(new Rect(winX + 45, curY + 144, winW - 90, 24f), $"EVALUATION: {completionRating}", rankStyle);

            curY += 195f;

            // Action Buttons
            float btnW = (winW - 60f) * 0.5f;
            float btnH = 44f;
            Rect replayBtnRect = new Rect(winX + 24, curY, btnW, btnH);
            Rect nextBtnRect = new Rect(winX + 36 + btnW, curY, btnW, btnH);

            var actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.20f, 0.60f, 0.80f);
            if (GUI.Button(replayBtnRect, "REPLAY STAGE 2 [R]", actionBtnStyle))
            {
                Stage2Audio.Instance?.PlayButtonPress();
                ReplayStage();
            }

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            string nextLabel = string.IsNullOrEmpty(nextSceneName) ? "CONTINUE [ENTER / C]" : "CONTINUE [ENTER / C]";
            if (GUI.Button(nextBtnRect, nextLabel, actionBtnStyle))
            {
                Stage2Audio.Instance?.PlayButtonPress();
                CompleteStage2();
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
                else if (nextBtnRect.Contains(e.mousePosition))
                {
                    Stage2Audio.Instance?.PlayButtonPress();
                    CompleteStage2();
                    e.Use();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
