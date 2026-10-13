using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Interactive Simon Says 4-colour memory puzzle panel for Level 2 (Archive).
    /// Plays a sequence of 4 coloured lights (Red, Green, Blue, Yellow).
    /// Player repeats the sequence.
    /// Correct sequence: reveals the 3-digit passcode ("518").
    /// Wrong sequence: flashes error, applies -5s penalty, and replays sequence.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimonColourPanelUI : MonoBehaviour
    {
        private static SimonColourPanelUI instance;
        public static SimonColourPanelUI Instance => instance;

        private bool isOpen = false;
        private bool isSolved = false;
        private bool isShowingSequence = false;
        private List<int> generatedSequence = new List<int>();
        private List<int> playerSequence = new List<int>();
        private int litColorIndex = -1;

        public bool IsOpen => isOpen;
        public bool IsSolved => isSolved;

        public static event System.Action OnSimonSolved;

        private static readonly Color[] PanelColors = new[]
        {
            new Color(0.85f, 0.20f, 0.20f), // 0: Red
            new Color(0.20f, 0.85f, 0.35f), // 1: Green
            new Color(0.20f, 0.50f, 0.95f), // 2: Blue
            new Color(0.95f, 0.80f, 0.20f)  // 3: Yellow
        };

        private static readonly float[] AudioPitches = new[] { 330f, 392f, 440f, 523f };

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            GenerateSequence();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void GenerateSequence()
        {
            generatedSequence.Clear();
            for (int i = 0; i < 4; i++)
            {
                generatedSequence.Add(Random.Range(0, 4));
            }
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
            playerSequence.Clear();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(false);
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            StartCoroutine(PlaySequenceRoutine());
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            StopAllCoroutines();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(true);
            else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        private IEnumerator PlaySequenceRoutine()
        {
            isShowingSequence = true;
            playerSequence.Clear();
            yield return new WaitForSecondsRealtime(0.5f);

            for (int i = 0; i < generatedSequence.Count; i++)
            {
                int colorIdx = generatedSequence[i];
                litColorIndex = colorIdx;
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.8f);
                yield return new WaitForSecondsRealtime(0.5f);
                litColorIndex = -1;
                yield return new WaitForSecondsRealtime(0.25f);
            }

            isShowingSequence = false;
        }

        private void PressColor(int colorIndex)
        {
            if (isShowingSequence || isSolved) return;

            litColorIndex = colorIndex;
            EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.8f);

            StartCoroutine(ResetLitColorAfterDelay());

            playerSequence.Add(colorIndex);
            int step = playerSequence.Count - 1;

            if (playerSequence[step] != generatedSequence[step])
            {
                // Wrong step
                Debug.Log("<color=#d9534f>[SimonColourPanelUI]</color> Incorrect Simon step. Penalty -5s.");
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong);
                if (StageTimer.Instance != null) StageTimer.Instance.ApplyPenalty(5f);

                StartCoroutine(PlaySequenceRoutine());
                return;
            }

            if (playerSequence.Count == generatedSequence.Count)
            {
                // Solved!
                isSolved = true;
                string code = PlaythroughGameState.Instance != null ? PlaythroughGameState.Instance.Level2Passcode : "518";
                Debug.Log($"<color=#5cb85c><b>[SimonColourPanelUI]</b></color> Simon puzzle solved! Passcode revealed: {code}");
                FeedbackHUD.ShowMessage($"COLOUR PANEL SOLVED! Passcode revealed: {code}", new Color(0.40f, 0.95f, 0.45f));
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadCorrect);
                OnSimonSolved?.Invoke();
                Close();
            }
        }

        private IEnumerator ResetLitColorAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            litColorIndex = -1;
        }

        private void OnGUI()
        {
            if (!isOpen || Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = 380f, ph = 360f;
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
            GUI.Label(new Rect(px + 10, py + 14, pw - 20, 28), "COLOUR PANEL PUZZLE", titleStyle);

            string statusText = isShowingSequence ? "Watch the pattern..." : "Repeat the pattern!";
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isShowingSequence ? new Color(0.95f, 0.85f, 0.25f) : new Color(0.35f, 0.95f, 0.45f) }
            };
            GUI.Label(new Rect(px + 10, py + 44, pw - 20, 22), statusText, statusStyle);

            // 2x2 Grid of Color Buttons
            float bw = 130f, bh = 110f, gap = 16f;
            float startX = px + (pw - (2 * bw + gap)) * 0.5f;
            float startY = py + 76f;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

            for (int i = 0; i < 4; i++)
            {
                int r = i / 2, c = i % 2;
                float bx = startX + c * (bw + gap);
                float by = startY + r * (bh + gap);

                Color baseCol = PanelColors[i];
                bool isLit = (litColorIndex == i);
                GUI.backgroundColor = isLit ? baseCol * 1.5f : baseCol * 0.65f;

                if (GUI.Button(new Rect(bx, by, bw, bh), "", btnStyle))
                {
                    PressColor(i);
                }
            }

            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(px + (pw - 140f) * 0.5f, py + ph - 42f, 140f, 30f), "Close (ESC)", btnStyle))
            {
                Close();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }
    }
}
