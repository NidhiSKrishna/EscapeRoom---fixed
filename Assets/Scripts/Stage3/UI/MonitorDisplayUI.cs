using System;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Interaction;
using EscapeRoom.Player;

namespace EscapeRoom.Stage3.UI
{
    /// <summary>
    /// Interactive monitor display for Stage 3 Security Control Room.
    /// Shows story text, symbol puzzle clues, and facility narrative.
    /// Implements IInteractable so the player can read monitors by pressing E.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MonitorDisplayUI : MonoBehaviour, IInteractable
    {
        [Header("Monitor Content")]
        [Tooltip("Title displayed at the top of the monitor.")]
        [SerializeField] private string monitorTitle = "SECURITY MONITOR";

        [TextArea(3, 6)]
        [Tooltip("Primary text content displayed on the monitor.")]
        [SerializeField] private string monitorText = "System status: NOMINAL";

        [TextArea(2, 4)]
        [Tooltip("Secondary text or clue displayed below the main content.")]
        [SerializeField] private string secondaryText = "";

        [Header("Monitor Style")]
        [Tooltip("Color of the monitor title text.")]
        [SerializeField] private Color titleColor = new Color(0.35f, 0.95f, 0.95f);

        [Tooltip("Color of the monitor body text.")]
        [SerializeField] private Color textColor = Color.white;

        [Header("State")]
        [SerializeField] private bool isOpen = false;
        private int openedFrame = -1;

        public static bool IsAnyMonitorOpen { get; private set; }

        public string MonitorTitle
        {
            get => monitorTitle;
            set => monitorTitle = value;
        }

        public string MonitorText
        {
            get => monitorText;
            set => monitorText = value;
        }

        public string SecondaryText
        {
            get => secondaryText;
            set => secondaryText = value;
        }

        public bool IsOpen => isOpen;

        // IInteractable
        public string InteractionPrompt => "Press E to read monitor";
        public bool CanInteract => !isOpen && !IsAnyMonitorOpen;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = false; // Solid collider for raycast detection
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                CloseMonitor();
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Only permit closing with E if it was NOT pressed on the exact frame the monitor opened
            bool eClosePressed = keyboard.eKey.wasPressedThisFrame && Time.frameCount > openedFrame;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || eClosePressed)
            {
                CloseMonitor();
            }
        }

        public void Interact()
        {
            if (isOpen || IsAnyMonitorOpen) return;
            OpenMonitor();
        }

        private void OpenMonitor()
        {
            openedFrame = Time.frameCount;
            RefreshClueContent();

            isOpen = true;
            IsAnyMonitorOpen = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(false);
        }

        /// <summary>
        /// Ensures the monitor displays the latest clue equations from SymbolMappingPuzzle.
        /// </summary>
        private void RefreshClueContent()
        {
            var symbolPuzzle = EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle.Instance ??
                               FindFirstObjectByType<EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle>();
            if (symbolPuzzle != null && symbolPuzzle.MonitorClues != null)
            {
                string goName = gameObject.name;
                if (goName == "MonitorScreen_1" && symbolPuzzle.MonitorClues.Length > 0)
                {
                    var lines = symbolPuzzle.MonitorClues[0].Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    monitorTitle = "SYSTEM CALIBRATION";
                    monitorText = lines.Length > 0 ? lines[0] : symbolPuzzle.MonitorClues[0];
                    secondaryText = lines.Length > 1 ? lines[1] : "";
                }
                else if (goName == "MonitorScreen_2" && symbolPuzzle.MonitorClues.Length > 1)
                {
                    monitorTitle = "DIAGNOSTIC LOG";
                    monitorText = symbolPuzzle.MonitorClues[1];
                    secondaryText = "Cross-reference with calibration data.";
                }
                else if (goName == "MonitorScreen_3" && symbolPuzzle.MonitorClues.Length > 2)
                {
                    var lines = symbolPuzzle.MonitorClues[2].Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    monitorTitle = "IDENTITY VERIFICATION";
                    monitorText = lines.Length > 0 ? lines[0] : symbolPuzzle.MonitorClues[2];
                    secondaryText = lines.Length > 1 ? lines[1] : "";
                }
            }
        }

        private void CloseMonitor()
        {
            isOpen = false;
            IsAnyMonitorOpen = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(true);
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            Color oldColor = GUI.color;

            // Screen dim
            GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float cardW = Mathf.Min(560f, Screen.width * 0.9f);
            float cardH = 390f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            // Monitor frame (dark with cyan accent — like a real monitor)
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none);

            // Cyan border (monitor screen edge)
            GUI.color = new Color(0.20f, 0.75f, 0.80f, 0.70f);
            GUI.Box(new Rect(cardX + 3, cardY + 3, cardW - 6, cardH - 6), GUIContent.none);

            // Screen background (very dark blue-black)
            GUI.color = new Color(0.04f, 0.06f, 0.10f, 1f);
            GUI.Box(new Rect(cardX + 5, cardY + 5, cardW - 10, cardH - 10), GUIContent.none);

            // Scanline effect (subtle)
            for (int i = 0; i < (int)cardH; i += 3)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.08f);
                GUI.DrawTexture(new Rect(cardX + 5, cardY + 5 + i, cardW - 10, 1), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;

            // Title
            var monTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = titleColor }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 18, cardW - 40, 30), monitorTitle, monTitleStyle);

            // Top divider (glowing line)
            GUI.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.50f);
            GUI.DrawTexture(new Rect(cardX + 25, cardY + 52, cardW - 50, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Main text body
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = textColor }
            };
            GUI.Label(new Rect(cardX + 30, cardY + 68, cardW - 60, 150), monitorText, bodyStyle);

            // Secondary text (clues, etc.)
            if (!string.IsNullOrEmpty(secondaryText))
            {
                GUI.color = new Color(0.20f, 0.75f, 0.80f, 0.35f);
                GUI.DrawTexture(new Rect(cardX + 25, cardY + 235, cardW - 50, 1), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var secStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    normal = { textColor = new Color(0.70f, 0.95f, 0.65f) }
                };
                GUI.Label(new Rect(cardX + 30, cardY + 245, cardW - 60, 85), secondaryText, secStyle);
            }

            // Close hint and button
            var closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.60f, 0.70f, 0.75f) }
            };
            GUI.Label(new Rect(cardX + 25, cardY + cardH - 36, cardW - 130, 24), "[ESC] / [E] / [SPACE] to close", closeStyle);

            if (GUI.Button(new Rect(cardX + cardW - 110, cardY + cardH - 38, 85, 26), "CLOSE"))
            {
                CloseMonitor();
            }

            GUI.color = oldColor;
        }
    }
}
