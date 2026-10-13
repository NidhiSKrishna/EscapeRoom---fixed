using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Interactive puzzle modal for the Framed Number Pattern on the Archive wall.
    /// Displays sequence: "2 · 6 · 12 · 20 · ?"
    /// Prompt: "What number comes next?" Options: [24], [28], [30], [32].
    /// Correct answer (30): opens hidden drawer with Brass Key!
    /// Wrong answer: applies -10s penalty to stage clock.
    /// </summary>
    [DisallowMultipleComponent]
    public class FramedPatternPuzzleUI : MonoBehaviour
    {
        private static FramedPatternPuzzleUI instance;
        public static FramedPatternPuzzleUI Instance => instance;

        private bool isOpen = false;
        private bool isSolved = false;
        public bool IsOpen => isOpen;
        public bool IsSolved => isSolved;

        public static event System.Action OnPatternSolved;

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
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;

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

        private void SelectOption(int choice)
        {
            if (choice == 30)
            {
                isSolved = true;
                Debug.Log("<color=#5cb85c><b>[FramedPatternPuzzleUI]</b></color> Correct pattern choice (30)! Hidden drawer opened.");
                FeedbackHUD.ShowMessage("CORRECT! A hidden drawer pops open: Brass Key revealed!", new Color(0.40f, 0.95f, 0.45f));
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadCorrect);
                OnPatternSolved?.Invoke();
                Close();
            }
            else
            {
                Debug.Log("<color=#d9534f>[FramedPatternPuzzleUI]</color> Wrong pattern choice. Applying -10s penalty.");
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong);
                if (StageTimer.Instance != null)
                {
                    StageTimer.Instance.ApplyPenalty(10f);
                }
            }
        }

        private void OnGUI()
        {
            if (!isOpen || Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = 480f, ph = 320f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // Panel background
            GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            GUI.Box(new Rect(px, py, pw, ph), GUIContent.none);

            // Gold border
            GUI.color = new Color(0.85f, 0.70f, 0.28f, 0.90f);
            GUI.Box(new Rect(px + 3, py + 3, pw - 6, ph - 6), GUIContent.none);

            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 10, py + 16, pw - 20, 30), "NUMBER PATTERN PUZZLE", titleStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 54, pw - 40, 44),
                "Daniel scratched this pattern on his shelf:\n2  ·  6  ·  12  ·  20  ·  ?", bodyStyle);

            var promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 104, pw - 40, 22), "What number comes next in the sequence?", promptStyle);

            // 4 Option Buttons: 24, 28, 30, 32
            int[] choices = new[] { 24, 28, 30, 32 };
            float bw = 90f, bh = 42f, gap = 14f;
            float bx = px + (pw - (4 * bw + 3 * gap)) * 0.5f;
            float by = py + 140f;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            for (int i = 0; i < choices.Length; i++)
            {
                int val = choices[i];
                GUI.backgroundColor = new Color(0.20f, 0.40f, 0.65f);
                if (GUI.Button(new Rect(bx + i * (bw + gap), by, bw, bh), val.ToString(), btnStyle))
                {
                    SelectOption(val);
                }
            }

            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(px + (pw - 160f) * 0.5f, py + ph - 50f, 160f, 34f), "Close (ESC)", btnStyle))
            {
                Close();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }
    }
}
