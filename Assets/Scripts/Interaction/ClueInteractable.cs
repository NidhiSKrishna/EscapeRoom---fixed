using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Player;
using EscapeRoom.Puzzle;
using EscapeRoom.UI;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Interactive note/document that displays readable clue text in a clean, high-contrast modal.
    /// Implements IInteractable.
    /// Dynamically displays the configured keypad code from the linked KeypadController.
    /// Can be dismissed with Escape, E, Space, or the on-screen Close button.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClueInteractable : MonoBehaviour, IInteractable
    {
        [Header("Keypad Reference")]
        [Tooltip("Reference to the KeypadController whose passcode is displayed on this clue.")]
        [SerializeField] private KeypadController targetKeypad;

        [Header("Clue Content")]
        [Tooltip("Header title displayed at the top of the reading panel.")]
        [SerializeField] private string clueTitle = "Exit Code Note";

        [TextArea(2, 5)]
        [Tooltip("Brief explanation that this note contains the emergency exit keypad code.")]
        [SerializeField] private string bodyExplanation = "Emergency exit keypad code:";

        [Tooltip("Fallback code if no KeypadController is linked.")]
        [SerializeField] private string fallbackCode = "8431";

        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Press E to examine note";

        private static ClueInteractable activeClue;
        public static bool IsAnyClueOpen => activeClue != null && activeClue.isReading;
        public static ClueInteractable ActiveClue => activeClue;
        public static event System.Action OnAnyClueOpened;

        private bool isReading = false;
        private PlayerLook cachedPlayerLook;

        public KeypadController TargetKeypad
        {
            get => targetKeypad;
            set => targetKeypad = value;
        }

        public string CurrentCode
        {
            get
            {
                if (EscapeRoom.Core.PlaythroughGameState.Instance != null &&
                    !string.IsNullOrEmpty(EscapeRoom.Core.PlaythroughGameState.Instance.Passcode))
                {
                    return EscapeRoom.Core.PlaythroughGameState.Instance.Passcode;
                }

                if (targetKeypad != null && !string.IsNullOrEmpty(targetKeypad.TargetCode))
                {
                    return targetKeypad.TargetCode;
                }

                // Auto-resolve KeypadController in scene if not directly linked
                var sceneKeypad = FindAnyObjectByType<KeypadController>();
                if (sceneKeypad != null && !string.IsNullOrEmpty(sceneKeypad.TargetCode))
                {
                    targetKeypad = sceneKeypad;
                    return sceneKeypad.TargetCode;
                }

                return fallbackCode;
            }
        }

        public string FormattedCode
        {
            get
            {
                string code = CurrentCode;
                return string.Join("   ", code.ToCharArray());
            }
        }

        public string ClueTitle
        {
            get => clueTitle;
            set => clueTitle = value;
        }

        public string BodyExplanation
        {
            get => bodyExplanation;
            set => bodyExplanation = value;
        }

        public string ClueText
        {
            get => $"Emergency exit keypad code: {CurrentCode}";
            set => bodyExplanation = value;
        }

        public string PromptText
        {
            get => promptText;
            set => promptText = value;
        }

        public string InteractionPrompt => EscapeRoom.Core.InputConfig.FormatPrompt("Examine Exit Code Note");
        public bool CanInteract => !isReading && gameObject.activeInHierarchy;
        public bool IsReading => isReading;

        private void Awake()
        {
            // Auto-resolve linked keypad if not assigned
            if (targetKeypad == null)
            {
                targetKeypad = FindAnyObjectByType<KeypadController>();
            }

            // Auto-migrate legacy title and body text
            if (string.IsNullOrEmpty(clueTitle) || clueTitle == "Facility Security Override Note")
            {
                clueTitle = "Exit Code Note";
            }
            if (string.IsNullOrEmpty(bodyExplanation) || bodyExplanation.Contains("EMERGENCY OVERRIDE PROTOCOL"))
            {
                bodyExplanation = "Emergency exit keypad code:";
            }

            // Ensure promptText matches requirement
            if (string.IsNullOrEmpty(promptText) || promptText.StartsWith("Press E to inspect"))
            {
                promptText = "Press E to examine note";
            }
        }

        private void Update()
        {
            if (!isReading) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame ||
                    keyboard.iKey.wasPressedThisFrame ||
                    keyboard.eKey.wasPressedThisFrame ||
                    keyboard.spaceKey.wasPressedThisFrame)
                {
                    CloseClue();
                }
            }
        }

        public void Interact()
        {
            if (isReading) return;

            OpenClue();
        }

        public void OpenClue()
        {
            isReading = true;
            activeClue = this;

            // Ensure keypad reference is linked
            if (targetKeypad == null)
            {
                targetKeypad = FindAnyObjectByType<KeypadController>();
            }

            // Unlock cursor for reading/interaction
            if (cachedPlayerLook == null)
            {
                cachedPlayerLook = FindAnyObjectByType<PlayerLook>();
            }

            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            FeedbackHUD.ShowMessage("Reading: " + clueTitle, new Color(0.90f, 0.90f, 0.70f));
            Debug.Log($"<color=#337ab7><b>[ClueInteractable]</b></color> Opened '{clueTitle}' displaying code: {CurrentCode}.");

            OnAnyClueOpened?.Invoke();
        }

        public void CloseClue()
        {
            if (!isReading) return;
            isReading = false;
            if (activeClue == this) activeClue = null;

            // Re-lock cursor
            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Debug.Log($"[ClueInteractable] Closed '{clueTitle}'.");
        }

        private void OnDestroy()
        {
            if (activeClue == this)
            {
                activeClue = null;
            }
        }

        private void OnGUI()
        {
            if (!isReading) return;

            GUI.depth = -20; // Draw in front of all crosshairs and HUD prompts

            Color oldColor = GUI.color;

            // 1. Solid darkening full-screen overlay to block distractions
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Responsive modal card sizing
            float panelWidth = Mathf.Clamp(Screen.width * 0.48f, 460f, 540f);
            float panelHeight = Mathf.Clamp(Screen.height * 0.48f, 300f, 360f);
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            // 2. Outer golden accent border frame
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 1.0f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), Texture2D.whiteTexture);

            // 3. Document card background (solid dark slate, high contrast)
            GUI.color = new Color(0.11f, 0.12f, 0.16f, 1.0f);
            GUI.DrawTexture(new Rect(panelX + 3, panelY + 3, panelWidth - 6, panelHeight - 6), Texture2D.whiteTexture);

            // 4. Header title: Exit Code Note
            GUI.color = Color.white;
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(panelX + 20, panelY + 18, panelWidth - 40, 30), clueTitle, titleStyle);

            // 5. Divider line
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.50f);
            GUI.DrawTexture(new Rect(panelX + 24, panelY + 52, panelWidth - 48, 2), Texture2D.whiteTexture);

            // 6. Label: Emergency exit keypad code:
            GUI.color = Color.white;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(panelX + 20, panelY + 68, panelWidth - 40, 28), "Emergency exit keypad code:", labelStyle);

            // 7. Prominent High-Contrast Keypad Code Display Box
            float codeBoxWidth = panelWidth - 64f;
            float codeBoxHeight = 90f;
            float codeBoxX = panelX + 32f;
            float codeBoxY = panelY + 104f;

            // Code box outer border (vibrant cyan accent)
            GUI.color = new Color(0.30f, 0.70f, 1.0f, 0.95f);
            GUI.DrawTexture(new Rect(codeBoxX, codeBoxY, codeBoxWidth, codeBoxHeight), Texture2D.whiteTexture);

            // Code box inner dark fill
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 1.0f);
            GUI.DrawTexture(new Rect(codeBoxX + 2, codeBoxY + 2, codeBoxWidth - 4, codeBoxHeight - 4), Texture2D.whiteTexture);

            // Code Box Big Digits: 8431 (Large, bold, white text)
            GUIStyle digitsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 50,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(codeBoxX + 6, codeBoxY + 8, codeBoxWidth - 12, codeBoxHeight - 16), CurrentCode, digitsStyle);

            // 8. Close Button
            float btnWidth = 180f;
            float btnHeight = 36f;
            float btnX = panelX + (panelWidth - btnWidth) * 0.5f;
            float btnY = panelY + panelHeight - 50f;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "Close Note (ESC / E)", btnStyle))
            {
                CloseClue();
            }

            GUI.color = oldColor;
        }
    }
}
