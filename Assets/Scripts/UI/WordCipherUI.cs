using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.Puzzle;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Interactive 4-letter word cipher terminal for Level 3 (Vault Cellar).
    /// Displays encrypted hint ("IUHH" - Each letter was pushed forward by 3 levels. Pull them back).
    /// Decoded solution: "FREE".
    /// Correct entry: unlocks Vault Door D3!
    /// Wrong entry: applies -10s penalty.
    /// </summary>
    [DisallowMultipleComponent]
    public class WordCipherUI : MonoBehaviour
    {
        private static WordCipherUI instance;
        public static WordCipherUI Instance => instance;

        private DoorController linkedDoor;
        private bool isOpen = false;
        private bool isSolved = false;
        private string inputWord = "";
        private string statusMessage = "ENTER 4-LETTER CIPHER WORD";
        private Color statusColor = Color.white;

        public bool IsOpen => isOpen;
        public bool IsSolved => isSolved;

        public static event System.Action OnCipherSolved;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!isOpen) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }

                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    Submit();
                    return;
                }

                if (kb.backspaceKey.wasPressedThisFrame)
                {
                    if (inputWord.Length > 0)
                        inputWord = inputWord.Substring(0, inputWord.Length - 1);
                    return;
                }

                // Handle A-Z typing
                for (Key k = Key.A; k <= Key.Z; k++)
                {
                    if (kb[k].wasPressedThisFrame && inputWord.Length < 4)
                    {
                        inputWord += k.ToString();
                        EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.8f);
                    }
                }
            }
        }

        public void Open(DoorController door = null)
        {
            if (isOpen) return;
            linkedDoor = door;
            isOpen = true;
            inputWord = "";
            statusMessage = "ENTER 4-LETTER CIPHER WORD";
            statusColor = Color.white;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(false);
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(true);
            else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        public void Submit()
        {
            string target = PlaythroughGameState.Instance != null ? PlaythroughGameState.Instance.WordCipherDecrypted : "FREE";

            if (string.Equals(inputWord.Trim(), target, System.StringComparison.OrdinalIgnoreCase))
            {
                isSolved = true;
                Debug.Log($"<color=#5cb85c><b>[WordCipherUI]</b></color> Correct cipher word entered ('{inputWord}')! Vault Door Unlocked.");
                FeedbackHUD.ShowMessage("VAULT LOCK UNLOCKED! Vault Door Opened!", new Color(0.40f, 0.95f, 0.45f));
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadCorrect);

                if (linkedDoor != null)
                {
                    linkedDoor.Unlock();
                    linkedDoor.OpenDoor();
                }

                OnCipherSolved?.Invoke();
                Close();
            }
            else
            {
                Debug.Log($"<color=#d9534f>[WordCipherUI]</color> Incorrect word '{inputWord}'. Target solution is '{target}'.");
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong);
                if (StageTimer.Instance != null) StageTimer.Instance.ApplyPenalty(10f);

                statusMessage = "WRONG WORD  ·  -10s PENALTY";
                statusColor = new Color(0.95f, 0.35f, 0.35f);
                inputWord = "";
            }
        }

        private void OnGUI()
        {
            if (!isOpen || Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.80f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = 440f, ph = 320f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // Box background
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            GUI.Box(new Rect(px, py, pw, ph), GUIContent.none);

            GUI.color = new Color(0.20f, 0.85f, 0.90f, 0.90f);
            GUI.Box(new Rect(px + 3, py + 3, pw - 6, ph - 6), GUIContent.none);

            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 10, py + 14, pw - 20, 28), "VAULT CIPHER TERMINAL", titleStyle);

            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = statusColor }
            };
            GUI.Label(new Rect(px + 10, py + 42, pw - 20, 22), statusMessage, statusStyle);

            // Display Word Box (4 letters)
            string display = inputWord.PadRight(4, '_');
            string formattedDisplay = string.Join("  ", display.ToCharArray());

            var codeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.color = new Color(0.04f, 0.06f, 0.10f, 1.0f);
            GUI.DrawTexture(new Rect(px + 40, py + 74, pw - 80, 60), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 40, py + 74, pw - 80, 60), formattedDisplay, codeStyle);

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(px + 20, py + 144, pw - 40, 44),
                "Hint: Torn Note Cipher = IUHH\n(Each letter shifted +3 levels. Type answer & press ENTER)", hintStyle);

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.20f, 0.70f, 0.35f);
            if (GUI.Button(new Rect(px + 40, py + ph - 50f, 170f, 34f), "SUBMIT", btnStyle))
            {
                Submit();
            }

            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(px + pw - 210f, py + ph - 50f, 170f, 34f), "CANCEL (ESC)", btnStyle))
            {
                Close();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }
    }
}
