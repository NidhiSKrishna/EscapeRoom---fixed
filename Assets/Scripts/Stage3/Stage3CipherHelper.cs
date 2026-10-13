using UnityEngine;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.Core;
using System.Collections.Generic;

namespace EscapeRoom.Stage3
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Stage3CipherHelper : MonoBehaviour, IInteractable
    {
        private static Stage3CipherHelper instance;
        public static Stage3CipherHelper Instance => instance;

        public static bool IsOpen { get; private set; }
        public int CurrentHintLevel { get; private set; }

        private string currentPuzzle = "ColorMemory";
        
        private string colorHint3 = "The sequence begins with...";
        private string pathHint3 = "The first step direction...";
        private string symbolHint3 = "One mapping is...";

        private string[] narrativeLines = new string[]
        {
            "FACILITY SHUTDOWN PROTOCOL",
            "I didn't know this file still existed.",
            "Someone restricted my access.",
            "The control room was sealed for a reason."
        };

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

        public string InteractionPrompt => "Access Terminal";
        public bool CanInteract => !IsOpen;

        public void Interact()
        {
            if (!IsOpen)
            {
                OpenTerminal();
            }
        }

        private void OpenTerminal()
        {
            IsOpen = true;
            CurrentHintLevel++;
            if (CurrentHintLevel > 3) CurrentHintLevel = 3;

            var playerLook = FindFirstObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.GetType().GetMethod("SetCursorLock")?.Invoke(playerLook, new object[] { false });
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseTerminal()
        {
            IsOpen = false;
            var playerLook = FindFirstObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.GetType().GetMethod("SetCursorLock")?.Invoke(playerLook, new object[] { true });
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseTerminal();
            }
        }

        public void SetCurrentPuzzle(string puzzleName)
        {
            currentPuzzle = puzzleName;
            ResetHintLevel();
        }

        public void SetColorSequenceHints(int[] sequence)
        {
            if (sequence != null && sequence.Length > 0)
            {
                string colorName = sequence[0] switch
                {
                    0 => "RED",
                    1 => "BLUE",
                    2 => "GREEN",
                    3 => "YELLOW",
                    _ => "UNKNOWN"
                };
                colorHint3 = $"The sequence begins with {colorName}.";
            }
        }

        public void SetPathHints(string firstStepDirection)
        {
            pathHint3 = $"The first step direction is {firstStepDirection}.";
        }

        public void SetSymbolHints(string revealedMapping)
        {
            symbolHint3 = $"One mapping is {revealedMapping}.";
        }

        public void ResetHintLevel()
        {
            CurrentHintLevel = 0;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            GUI.depth = -100;
            
            float width = 600f;
            float height = 400f;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;
            Rect windowRect = new Rect(x, y, width, height);

            GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = Texture2D.blackTexture;
            windowStyle.normal.textColor = new Color(0.98f, 0.85f, 0.25f);
            windowStyle.fontSize = 18;
            windowStyle.fontStyle = FontStyle.Bold;

            GUI.Box(windowRect, "TERMINAL ACCESS", windowStyle);

            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.normal.textColor = Color.white;
            textStyle.wordWrap = true;
            textStyle.fontSize = 14;
            textStyle.padding = new RectOffset(20, 20, 40, 20);

            GUILayout.BeginArea(windowRect);
            GUILayout.Space(40);

            string hintText = GetHintText();
            GUILayout.Label(hintText, textStyle);
            
            GUILayout.FlexibleSpace();

            GUIStyle narrativeStyle = new GUIStyle(GUI.skin.label);
            narrativeStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            narrativeStyle.wordWrap = true;
            narrativeStyle.fontSize = 12;
            narrativeStyle.alignment = TextAnchor.LowerCenter;

            foreach (var line in narrativeLines)
            {
                GUILayout.Label(line, narrativeStyle);
            }

            GUILayout.Space(20);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = new Color(0.95f, 0.35f, 0.35f);
            buttonStyle.fontSize = 14;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.fixedHeight = 30;

            if (GUILayout.Button("CLOSE CONNECTION [ESC]", buttonStyle))
            {
                CloseTerminal();
            }

            GUILayout.EndArea();
        }

        private string GetHintText()
        {
            string hint = "";
            if (currentPuzzle == "ColorMemory" || currentPuzzle == "COLOR")
            {
                if (CurrentHintLevel >= 1) hint += "- Watch carefully. The pattern holds the key.\n\n";
                if (CurrentHintLevel >= 2) hint += "- The order matters more than the colors themselves.\n\n";
                if (CurrentHintLevel >= 3) hint += $"- {colorHint3}\n\n";
            }
            else if (currentPuzzle == "PathPuzzle" || currentPuzzle == "PATH")
            {
                if (CurrentHintLevel >= 1) hint += "- The floor remembers the way.\n\n";
                if (CurrentHintLevel >= 2) hint += "- Step only where the light has been.\n\n";
                if (CurrentHintLevel >= 3) hint += $"- {pathHint3}\n\n";
            }
            else if (currentPuzzle == "SymbolPuzzle" || currentPuzzle == "SYMBOL")
            {
                if (CurrentHintLevel >= 1) hint += "- Symbols have values. Find the pattern.\n\n";
                if (CurrentHintLevel >= 2) hint += "- The monitors hold the equations you need.\n\n";
                if (CurrentHintLevel >= 3) hint += $"- {symbolHint3}\n\n";
            }
            else
            {
                hint = "No connection to current systems.";
            }
            return hint;
        }
    }
}
