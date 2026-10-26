using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Player;
using EscapeRoom.Puzzle;

namespace EscapeRoom.UI
{
    /// <summary>
    /// On-screen keypad terminal.
    /// Supports mouse clicks through Unity IMGUI and keyboard input
    /// through the New Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeypadUI : MonoBehaviour
    {
        private static KeypadUI instance;
        public static KeypadUI Instance => instance;

        private KeypadController activeKeypad;
        private string currentInput = "";
        private bool isOpen = false;

        private string statusMessage = "ENTER PASSCODE";
        private Color statusColor = Color.cyan;
        private float statusResetTimer = 0f;

        private PlayerLook cachedPlayerLook;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            cachedPlayerLook = FindAnyObjectByType<PlayerLook>();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!isOpen)
                return;

            if (statusResetTimer > 0f)
            {
                statusResetTimer -= Time.deltaTime;

                if (statusResetTimer <= 0f)
                {
                    statusMessage = "ENTER PASSCODE";
                    statusColor = Color.cyan;
                }
            }

            HandleKeyboardInput();
        }

        public void Open(KeypadController keypad)
        {
            if (keypad == null)
            {
                Debug.LogError("[KeypadUI] Cannot open: keypad reference is null.");
                return;
            }

            activeKeypad = keypad;
            currentInput = "";
            isOpen = true;

            statusMessage = "ENTER PASSCODE";
            statusColor = Color.cyan;
            statusResetTimer = 0f;

            if (cachedPlayerLook == null)
                cachedPlayerLook = FindAnyObjectByType<PlayerLook>();

            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Debug.Log(
                $"[KeypadUI] Opened keypad for '{keypad.gameObject.name}'. " +
                "Mouse cursor unlocked."
            );
        }

        public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            activeKeypad = null;
            currentInput = "";

            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Debug.Log("[KeypadUI] Closed keypad.");
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                PressEnter();
                return;
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                PressClear();
                return;
            }

            if (keyboard.digit0Key.wasPressedThisFrame ||
                keyboard.numpad0Key.wasPressedThisFrame)
                PressDigit("0");
            else if (keyboard.digit1Key.wasPressedThisFrame ||
                     keyboard.numpad1Key.wasPressedThisFrame)
                PressDigit("1");
            else if (keyboard.digit2Key.wasPressedThisFrame ||
                     keyboard.numpad2Key.wasPressedThisFrame)
                PressDigit("2");
            else if (keyboard.digit3Key.wasPressedThisFrame ||
                     keyboard.numpad3Key.wasPressedThisFrame)
                PressDigit("3");
            else if (keyboard.digit4Key.wasPressedThisFrame ||
                     keyboard.numpad4Key.wasPressedThisFrame)
                PressDigit("4");
            else if (keyboard.digit5Key.wasPressedThisFrame ||
                     keyboard.numpad5Key.wasPressedThisFrame)
                PressDigit("5");
            else if (keyboard.digit6Key.wasPressedThisFrame ||
                     keyboard.numpad6Key.wasPressedThisFrame)
                PressDigit("6");
            else if (keyboard.digit7Key.wasPressedThisFrame ||
                     keyboard.numpad7Key.wasPressedThisFrame)
                PressDigit("7");
            else if (keyboard.digit8Key.wasPressedThisFrame ||
                     keyboard.numpad8Key.wasPressedThisFrame)
                PressDigit("8");
            else if (keyboard.digit9Key.wasPressedThisFrame ||
                     keyboard.numpad9Key.wasPressedThisFrame)
                PressDigit("9");
        }

        public void PressDigit(string digit)
        {
            if (!isOpen || activeKeypad == null)
                return;

            if (string.IsNullOrEmpty(digit))
                return;

            int maxLength = Mathf.Max(1, activeKeypad.MaxCodeLength);

            if (currentInput.Length < maxLength)
            {
                currentInput += digit;
                EscapeRoom.Audio.EscapeRoomAudio.Play(
                    EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.8f);
                Debug.Log($"[KeypadUI] Digit entered. Length: {currentInput.Length}");
            }
        }

        public void PressClear()
        {
            if (!isOpen)
                return;

            currentInput = "";
            Debug.Log("[KeypadUI] Input cleared.");
        }

        public void PressEnter()
        {
            if (!isOpen || activeKeypad == null)
                return;

            if (string.IsNullOrEmpty(currentInput))
            {
                statusMessage = "CODE EMPTY";
                statusColor = Color.yellow;
                statusResetTimer = 1.5f;
                return;
            }

            bool accepted = activeKeypad.SubmitCode(currentInput);

            if (accepted)
            {
                statusMessage = "ACCESS GRANTED";
                statusColor = new Color(0.3f, 1f, 0.4f);
                statusResetTimer = 2f;

                Close();
            }
            else
            {
                statusMessage = "ACCESS DENIED";
                statusColor = new Color(1f, 0.3f, 0.3f);
                statusResetTimer = 1.5f;

                currentInput = "";
            }
        }

        private void OnGUI()
        {
            if (!isOpen)
                return;

            GUI.depth = -100;

            float panelWidth = 320f;
            float panelHeight = 440f;

            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            Color oldColor = GUI.color;
            Color oldBackgroundColor = GUI.backgroundColor;

            // Dim the game behind the keypad.
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture
            );

            // Keypad outer case.
            GUI.color = new Color(0.12f, 0.13f, 0.15f, 0.98f);
            GUI.Box(
                new Rect(panelX, panelY, panelWidth, panelHeight),
                GUIContent.none
            );

            // Display frame.
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            GUI.Box(
                new Rect(panelX + 20f, panelY + 20f, panelWidth - 40f, 75f),
                GUIContent.none
            );

            // Status text.
            GUI.color = Color.white;

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(
                new Rect(panelX + 25f, panelY + 24f, panelWidth - 50f, 20f),
                statusMessage,
                statusStyle
            );

            // Digit display.
            GUI.color = Color.white;

            string displayDigits = currentInput;
            int maxLength = activeKeypad != null
                ? Mathf.Max(1, activeKeypad.MaxCodeLength)
                : 4;

            while (displayDigits.Length < maxLength)
                displayDigits += "_ ";

            GUIStyle digitStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(
                new Rect(panelX + 25f, panelY + 44f, panelWidth - 50f, 45f),
                displayDigits,
                digitStyle
            );

            // Keypad buttons.
            string[] buttons =
            {
                "1", "2", "3",
                "4", "5", "6",
                "7", "8", "9",
                "CLR", "0", "ENT"
            };

            float startX = panelX + 24f;
            float startY = panelY + 110f;

            float buttonWidth = 82f;
            float buttonHeight = 55f;
            float gapX = 12f;
            float gapY = 10f;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            for (int i = 0; i < buttons.Length; i++)
            {
                int row = i / 3;
                int column = i % 3;

                float buttonX = startX + column * (buttonWidth + gapX);
                float buttonY = startY + row * (buttonHeight + gapY);

                string label = buttons[i];

                if (label == "ENT")
                    GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
                else if (label == "CLR")
                    GUI.backgroundColor = new Color(0.8f, 0.3f, 0.2f);
                else
                    GUI.backgroundColor = new Color(0.35f, 0.38f, 0.42f);

                Rect buttonRect = new Rect(
                    buttonX,
                    buttonY,
                    buttonWidth,
                    buttonHeight
                );

                if (GUI.Button(buttonRect, label, buttonStyle))
                {
                    Debug.Log($"[KeypadUI] Mouse clicked button: {label}");

                    if (label == "CLR")
                        PressClear();
                    else if (label == "ENT")
                        PressEnter();
                    else
                        PressDigit(label);
                }
            }

            // Close button.
            GUI.backgroundColor = new Color(0.25f, 0.25f, 0.28f);

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            if (GUI.Button(
                new Rect(
                    panelX + 35f,
                    panelY + panelHeight - 48f,
                    panelWidth - 70f,
                    32f
                ),
                "Close (ESC)",
                closeStyle))
            {
                Close();
            }

            // Restore GUI settings for other OnGUI code.
            GUI.backgroundColor = oldBackgroundColor;
            GUI.color = oldColor;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}