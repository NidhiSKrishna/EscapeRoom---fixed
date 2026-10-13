using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays the Memory Console preview for Level 3 (Vault Cellar).
    /// Shows a 5x4 grid representing the safe pressure tile path for 10 seconds.
    /// Bottom row = nearest tile to entry gate. Green = safe tile.
    /// </summary>
    [DisallowMultipleComponent]
    public class MemoryConsoleUI : MonoBehaviour
    {
        private static MemoryConsoleUI instance;
        public static MemoryConsoleUI Instance => instance;

        private bool isOpen = false;
        private float autoHideTimer = 0f;
        public bool IsOpen => isOpen;

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

            autoHideTimer -= Time.deltaTime;
            if (autoHideTimer <= 0f)
            {
                Close();
            }

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
            autoHideTimer = 10f;

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

        private void OnGUI()
        {
            if (!isOpen || Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.80f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = 420f, ph = 380f;
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
            GUI.Label(new Rect(px + 10, py + 14, pw - 20, 28), "MEMORY CONSOLE — SAFE PATH", titleStyle);

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(px + 10, py + 42, pw - 20, 22),
                $"Memorise the safe path ({Mathf.CeilToInt(autoHideTimer)}s). Bottom row = entrance.", hintStyle);

            // 5x4 Grid Preview (Row 4 down to Row 0)
            float cellW = 70f, cellH = 34f, gap = 8f;
            float startX = px + (pw - (4 * cellW + 3 * gap)) * 0.5f;
            float startY = py + 74f;

            for (int r = 4; r >= 0; r--)
            {
                int drawRow = 4 - r;
                float cy = startY + drawRow * (cellH + gap);

                for (int c = 0; c < 4; c++)
                {
                    float cx = startX + c * (cellW + gap);
                    bool safe = PlaythroughGameState.Instance != null && PlaythroughGameState.Instance.IsTileSafe(r, c);

                    GUI.color = safe ? new Color(0.20f, 0.80f, 0.35f, 0.95f) : new Color(0.20f, 0.22f, 0.26f, 0.85f);
                    GUI.DrawTexture(new Rect(cx, cy, cellW, cellH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

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
