using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Stage3.Puzzles;

namespace EscapeRoom.Stage3.UI
{
    [DisallowMultipleComponent]
    public class Stage3HUD : MonoBehaviour
    {
        private static Stage3HUD instance;
        public static Stage3HUD Instance => instance;

        [Header("Display Settings")]
        [Tooltip("Whether to show the puzzle status panel.")]
        [SerializeField] private bool showPuzzleStatus = true;

        private ColorMemoryPuzzle colorMemoryPuzzle;
        private MemoryPathPuzzle memoryPathPuzzle;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Find the puzzles in the current Stage 3 scene.
            colorMemoryPuzzle = FindFirstObjectByType<ColorMemoryPuzzle>();
            memoryPathPuzzle = FindFirstObjectByType<MemoryPathPuzzle>();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnGUI()
        {
            if (!showPuzzleStatus)
                return;

            // Don't show during paused or escaped states.
            if (PauseManager.Instance != null &&
                PauseManager.Instance.IsPaused)
                return;

            if (GameManager.Instance != null &&
                GameManager.Instance.HasEscaped)
                return;

            // Don't show during Symbol Mapping UI.
            if (SymbolMappingPuzzle.Instance != null &&
                SymbolMappingPuzzle.Instance.IsUIOpen)
                return;

            // Don't show while Cipher UI is open.
            if (Stage3CipherHelper.Instance != null &&
                Stage3CipherHelper.IsOpen)
                return;

            // Don't show while a monitor is open.
            if (MonitorDisplayUI.IsAnyMonitorOpen)
                return;

            DrawPuzzleStatusPanel();
        }

        private void DrawPuzzleStatusPanel()
        {
            Color oldColor = GUI.color;

            float panelW = 200f;
            float panelH = 110f;
            float panelX = 12f;
            float panelY = Screen.height - panelH - 12f;

            // Background
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.75f);

            GUI.Box(
                new Rect(panelX, panelY, panelW, panelH),
                GUIContent.none
            );

            // Border
            GUI.color = new Color(0.25f, 0.30f, 0.38f, 0.60f);

            GUI.Box(
                new Rect(panelX + 1, panelY + 1, panelW - 2, panelH - 2),
                GUIContent.none
            );

            // Inner fill
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.85f);

            GUI.Box(
                new Rect(panelX + 2, panelY + 2, panelW - 4, panelH - 4),
                GUIContent.none
            );

            GUI.color = Color.white;

            // Header
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            headerStyle.normal.textColor =
                new Color(0.65f, 0.70f, 0.75f);

            GUI.Label(
                new Rect(panelX + 10, panelY + 6, panelW - 20, 16),
                "SECURITY SYSTEMS",
                headerStyle
            );

            // Divider
            GUI.color = new Color(0.35f, 0.40f, 0.50f, 0.40f);

            GUI.DrawTexture(
                new Rect(panelX + 8, panelY + 24, panelW - 16, 1),
                Texture2D.whiteTexture
            );

            GUI.color = Color.white;

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            float rowY = panelY + 30f;
            float rowH = 18f;

            // Memory Puzzle
            bool memComplete =
                colorMemoryPuzzle != null &&
                colorMemoryPuzzle.IsCompleted;

            DrawStatusRow(
                panelX + 10,
                rowY,
                panelW - 20,
                rowH,
                "Memory Lock",
                memComplete,
                statusStyle
            );

            // Path Puzzle
            rowY += rowH + 2f;

            bool pathComplete =
                memoryPathPuzzle != null &&
                memoryPathPuzzle.IsCompleted;

            DrawStatusRow(
                panelX + 10,
                rowY,
                panelW - 20,
                rowH,
                "Path Lock",
                pathComplete,
                statusStyle
            );

            // Symbol Puzzle
            rowY += rowH + 2f;

            bool symComplete =
                SymbolMappingPuzzle.Instance != null &&
                SymbolMappingPuzzle.Instance.IsCompleted;

            DrawStatusRow(
                panelX + 10,
                rowY,
                panelW - 20,
                rowH,
                "Symbol Lock",
                symComplete,
                statusStyle
            );

            // Lockdown
            rowY += rowH + 2f;

            bool lockdownCleared =
                SecurityLockdownEvent.Instance != null &&
                SecurityLockdownEvent.Instance.IsDeactivated;

            bool lockdownActive =
                SecurityLockdownEvent.Instance != null &&
                SecurityLockdownEvent.Instance.IsLockdownActive;

            if (lockdownActive)
            {
                statusStyle.normal.textColor =
                    new Color(0.95f, 0.35f, 0.35f);

                GUI.Label(
                    new Rect(
                        panelX + 10,
                        rowY,
                        panelW - 20,
                        rowH
                    ),
                    "◉ Lockdown ACTIVE",
                    statusStyle
                );
            }
            else
            {
                DrawStatusRow(
                    panelX + 10,
                    rowY,
                    panelW - 20,
                    rowH,
                    "Lockdown",
                    lockdownCleared,
                    statusStyle
                );
            }

            GUI.color = oldColor;
        }

        private void DrawStatusRow(
            float x,
            float y,
            float w,
            float h,
            string label,
            bool completed,
            GUIStyle style)
        {
            string prefix = completed ? "✓" : "○";

            Color color = completed
                ? new Color(0.40f, 0.90f, 0.45f)
                : new Color(0.55f, 0.60f, 0.65f);

            style.normal.textColor = color;

            GUI.Label(
                new Rect(x, y, w, h),
                $"{prefix} {label}",
                style
            );
        }
    }
}