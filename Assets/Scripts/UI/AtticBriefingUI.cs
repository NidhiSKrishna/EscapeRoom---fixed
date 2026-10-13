using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays the pre-game Attic Briefing Card with story instructions and player controls.
    /// Freezes Time.timeScale = 0, unlocks the cursor, disables player input, and prevents battery drain.
    /// Pressing BEGIN restores Time.timeScale = 1, locks cursor, triggers Attic banner, and activates objectives.
    /// </summary>
    [DisallowMultipleComponent]
    public class AtticBriefingUI : MonoBehaviour
    {
        private static AtticBriefingUI instance;
        public static AtticBriefingUI Instance => instance;

        [Header("Settings")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private string briefingTitle = "YOU ARE IN THE ATTIC";

        [TextArea(3, 6)]
        [SerializeField] private string briefingBody =
            "Your goal is to escape the building.\n" +
            "Search the room, follow the clues, and use\n" +
            "anything you discover.";

        [TextArea(5, 8)]
        [SerializeField] private string controlsText =
            "CONTROLS\n" +
            "WASD - Move\n" +
            "Mouse - Look\n" +
            "E - Interact\n" +
            "F - Flashlight\n" +
            "T - Objectives\n" +
            "ESC - Pause / close current panel";

        [Header("State")]
        [SerializeField] private bool isOpen = false;

        public bool IsOpen => isOpen;
        public static bool IsBriefingOpen => instance != null && instance.isOpen;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            // If PhoneIntroUI is present and active, let PhoneIntroUI trigger briefing upon completion
            if (PhoneIntroUI.Instance != null && PhoneIntroUI.Instance.IsOpen)
            {
                isOpen = false;
                return;
            }

            if (showOnStart)
            {
                ShowBriefing();
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                OnBeginClicked();
                return;
            }

        }

        public void ShowBriefing()
        {
            isOpen = true;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(false);
            }

            Debug.Log("[AtticBriefingUI] Attic briefing card displayed. Time.timeScale set to 0.");
        }

        public void OnBeginClicked()
        {
            isOpen = false;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(true);
            }

            // Activate objectives
            ObjectiveManager.Instance?.ActivateSystem();

            // Trigger room banner
            BannerUI.Instance?.ShowBanner("ATTIC", "Search carefully. Something useful is hidden here.");

            // ── Start the stage timer ──────────────────────────────────────────
            if (EscapeRoom.UI.StageTimer.Instance != null)
            {
                EscapeRoom.UI.StageTimer.Instance.StartTimer();
            }

            // ── Activate Cipher ────────────────────────────────────────────────
            if (EscapeRoom.UI.CipherHostUI.Instance != null)
            {
                EscapeRoom.UI.CipherHostUI.Instance.Activate();
            }

            Debug.Log("[AtticBriefingUI] BEGIN clicked. Gameplay started, cursor locked, objectives activated.");
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            GUI.depth = -100;

            // Full screen dim backdrop
            Color oldColor = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float cardWidth = Mathf.Min(560f, Screen.width * 0.92f);
            float cardHeight = Mathf.Min(470f, Screen.height * 0.92f);
            float cardX = (Screen.width - cardWidth) * 0.5f;
            float cardY = (Screen.height - cardHeight) * 0.5f;

            // Outer card frame
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
            GUI.Box(new Rect(cardX, cardY, cardWidth, cardHeight), GUIContent.none);

            // Border accent
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.90f);
            GUI.Box(new Rect(cardX + 3, cardY + 3, cardWidth - 6, cardHeight - 6), GUIContent.none);

            // Card body background
            GUI.color = new Color(0.14f, 0.16f, 0.22f, 1f);
            GUI.Box(new Rect(cardX + 5, cardY + 5, cardWidth - 10, cardHeight - 10), GUIContent.none);

            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.85f, 0.35f) }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 20, cardWidth - 40, 36), briefingTitle, titleStyle);

            // Divider line
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.40f);
            GUI.DrawTexture(new Rect(cardX + 40, cardY + 62, cardWidth - 80, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Body
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.90f, 0.92f, 0.95f) }
            };
            GUI.Label(new Rect(cardX + 30, cardY + 75, cardWidth - 60, 70), briefingBody, bodyStyle);

            // Controls Box
            float boxX = cardX + 35;
            float boxY = cardY + 155;
            float boxW = cardWidth - 70;
            float boxH = 180;

            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
            GUI.Box(new Rect(boxX, boxY, boxW, boxH), GUIContent.none);
            GUI.color = Color.white;

            var controlsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.80f, 0.88f, 0.95f) }
            };
            GUI.Label(new Rect(boxX + 20, boxY + 12, boxW - 40, boxH - 24), controlsText, controlsStyle);

            // BEGIN Button
            float btnW = 200f;
            float btnH = 46f;
            float btnX = cardX + (cardWidth - btnW) * 0.5f;
            float btnY = cardY + cardHeight - btnH - 22f;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black }
            };

            GUI.backgroundColor = new Color(0.98f, 0.82f, 0.25f, 1f);
            Rect beginRect = new Rect(btnX, btnY, btnW, btnH);
            if (GUI.Button(beginRect, "BEGIN [ENTER / SPACE]", btnStyle))
            {
                OnBeginClicked();
            }

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0)
            {
                if (beginRect.Contains(e.mousePosition))
                {
                    OnBeginClicked();
                    e.Use();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
