using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using EscapeRoom.Interaction;
using EscapeRoom.Core;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays a "STAGE 2 COMPLETE" screen after the security door is opened.
    /// Triggered by subscribing to DoorController.onDoorOpened or by calling ShowStage2Complete().
    /// After a brief delay (or on player input), either loads Stage 3 or shows a "Stay & Explore" message.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2CompleteUI : MonoBehaviour
    {
        private static Stage2CompleteUI instance;
        public static Stage2CompleteUI Instance => instance;

        [Header("Timing")]
        [SerializeField] private float autoCloseDuration = 6f;

        [Header("Next Scene")]
        [Tooltip("Name of the Stage 3 scene to load. Leave empty if Stage 3 doesn't exist yet.")]
        [SerializeField] private string stage3SceneName = "SecurityControlRoom";

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isOpen    = false;
        private float showTimer = 0f;
        private float alpha     = 0f;

        public bool IsOpen => isOpen;

        // ── Lifecycle ────────────────────────────────────────────────────────
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

            // Fade in
            alpha = Mathf.MoveTowards(alpha, 1f, Time.deltaTime * 3f);

            showTimer -= Time.deltaTime;
            if (showTimer <= 0f)
            {
                showTimer = 0f;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    TryProceed();
                }
            }
        }

        public void ShowStage2Complete()
        {
            if (isOpen) return;
            isOpen    = true;
            showTimer = autoCloseDuration;
            alpha     = 0f;
            Debug.Log("<color=#5cb85c><b>[Stage2CompleteUI]</b></color> Stage 2 Complete screen shown.");
        }

        private void TryProceed()
        {
            // Check if Stage 3 scene exists in build settings
            bool stage3Exists = false;
            if (!string.IsNullOrEmpty(stage3SceneName))
            {
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(i);
                    if (path.IndexOf(stage3SceneName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        stage3Exists = true;
                        break;
                    }
                }
            }

            if (stage3Exists)
            {
                Debug.Log($"[Stage2CompleteUI] Loading Stage 3: {stage3SceneName}");
                SceneManager.LoadScene(stage3SceneName);
            }
            else
            {
                Debug.Log("[Stage2CompleteUI] Stage 3 not yet built. Staying in Stage 2.");
                FeedbackHUD.ShowMessage("Stage 3 coming soon...", new Color(0.80f, 0.85f, 1.0f));
                isOpen = false; // dismiss the completion screen
            }
        }

        // ── ImGUI ────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!isOpen || alpha <= 0f) return;
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            GUI.depth = -30;
            Color old = GUI.color;

            // Semi-transparent overlay
            GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.86f * alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = Mathf.Min(560f, Screen.width * 0.92f);
            float ph = 280f;
            float px = (Screen.width  - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // Card background
            GUI.color = new Color(0.08f, 0.10f, 0.13f, 0.96f * alpha);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);

            // Cyan border
            GUI.color = new Color(0.20f, 0.85f, 0.90f, 0.90f * alpha);
            GUI.DrawTexture(new Rect(px + 3, py + 3, pw - 6, ph - 6), Texture2D.whiteTexture);

            // Dark fill
            GUI.color = new Color(0.10f, 0.12f, 0.16f, 1.0f * alpha);
            GUI.DrawTexture(new Rect(px + 5, py + 5, pw - 10, ph - 10), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Main title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 34, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 24, pw - 40, 50), "STAGE 2 COMPLETE", titleStyle);

            // Divider
            GUI.color = new Color(0.20f, 0.85f, 0.90f, 0.45f * alpha);
            GUI.DrawTexture(new Rect(px + 40, py + 80, pw - 80, 2), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Subtitle
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 92, pw - 40, 50),
                      "THE SECURITY CORRIDOR IS NOW OPEN.", subStyle);

            // Countdown / hint
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

            bool stage3InBuild = !string.IsNullOrEmpty(stage3SceneName);
            string hintText = stage3InBuild
                ? "Press ENTER to proceed to Stage 3  ·  ESC to dismiss"
                : "Stage 3 not yet available.  Press ESC to close.";
            GUI.Label(new Rect(px + 20, py + 150, pw - 40, 24), hintText, hintStyle);

            // Proceed button
            float btnW = 240f, btnH = 42f;
            float btnX = px + (pw - btnW) * 0.5f;
            float btnY = py + ph - 62f;

            GUI.backgroundColor = stage3InBuild
                ? new Color(0.15f, 0.65f, 0.80f, alpha)
                : new Color(0.35f, 0.38f, 0.42f, alpha);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            string btnLabel = stage3InBuild ? "PROCEED TO STAGE 3" : "CLOSE";
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), btnLabel, btnStyle))
            {
                TryProceed();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }
    }
}
