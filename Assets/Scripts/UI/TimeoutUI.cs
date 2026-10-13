using UnityEngine;
using EscapeRoom.Core;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displayed when the stage timer reaches 00:00.
    /// Shows "TIME EXPIRED" with Restart / Return to Menu options.
    /// Does NOT kill the player or play horror effects.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeoutUI : MonoBehaviour
    {
        private static TimeoutUI instance;
        public static TimeoutUI Instance => instance;

        private bool isOpen = false;
        private float alpha = 0f;
        private GUIStyle titleStyle, bodyStyle, btnStyle;
        private bool stylesBuilt = false;

        public bool IsOpen => isOpen;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            StageTimer.OnAnyTimerExpired += HandleTimerExpired;
        }

        private void OnDisable()
        {
            StageTimer.OnAnyTimerExpired -= HandleTimerExpired;
        }

        private void Update()
        {
            if (!isOpen) return;
            alpha = Mathf.MoveTowards(alpha, 1f, Time.unscaledDeltaTime * 2.5f);
        }

        // ── API ───────────────────────────────────────────────────────────────
        public void ShowTimeout()
        {
            if (isOpen) return;
            isOpen = true;
            alpha  = 0f;

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            var look = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
            if (look != null) look.SetCursorLock(false);

            Debug.Log("[TimeoutUI] Time expired — showing timeout screen.");
        }

        private void HandleTimerExpired()
        {
            ShowTimeout();
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void RestartLevel()
        {
            isOpen = false;
            if (GameManager.Instance != null)
                GameManager.Instance.RestartGame();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void ReturnToMenu()
        {
            isOpen = false;
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMainMenu();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!isOpen || alpha <= 0.01f) return;
            if (Event.current.type != EventType.Repaint &&
                Event.current.type != EventType.MouseDown &&
                Event.current.type != EventType.MouseUp) return;

            if (!stylesBuilt) BuildStyles();

            GUI.depth = -25;
            Color old = GUI.color;

            // Dark overlay
            GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.88f * alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = Mathf.Min(520f, Screen.width * 0.9f);
            float ph = 290f;
            float px = (Screen.width  - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // Panel
            GUI.color = new Color(0.08f, 0.10f, 0.13f, 0.97f * alpha);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);

            // Red top accent
            GUI.color = new Color(0.85f, 0.25f, 0.25f, 0.90f * alpha);
            GUI.DrawTexture(new Rect(px, py, pw, 4f), Texture2D.whiteTexture);

            // Inner background
            GUI.color = new Color(0.11f, 0.13f, 0.17f, 1f * alpha);
            GUI.DrawTexture(new Rect(px + 4, py + 4, pw - 8, ph - 8), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Title
            titleStyle.normal.textColor = new Color(0.90f, 0.30f, 0.30f, alpha);
            GUI.Label(new Rect(px + 10, py + 18, pw - 20, 46), "TIME EXPIRED", titleStyle);

            // Cipher body
            bodyStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f, alpha);
            GUI.Label(new Rect(px + 30, py + 74, pw - 60, 24), "CIPHER:", bodyStyle);
            GUI.Label(new Rect(px + 30, py + 98, pw - 60, 24), "\"We ran out of time. Let's try that again.\"", bodyStyle);

            // Divider
            GUI.color = new Color(0.35f, 0.40f, 0.45f, 0.50f * alpha);
            GUI.DrawTexture(new Rect(px + 30, py + 140, pw - 60, 1), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);

            float btnW = 200f, btnH = 42f;
            float btnX = px + (pw - btnW) * 0.5f;

            GUI.backgroundColor = new Color(0.20f, 0.60f, 0.30f, alpha);
            if (GUI.Button(new Rect(btnX, py + 160, btnW, btnH), "RESTART LEVEL", btnStyle))
                RestartLevel();

            GUI.backgroundColor = new Color(0.32f, 0.35f, 0.42f, alpha);
            if (GUI.Button(new Rect(btnX, py + 215f, btnW, 36f), "RETURN TO MENU", btnStyle))
                ReturnToMenu();

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 30, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft, wordWrap = true
            };
            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            stylesBuilt = true;
        }
    }
}
