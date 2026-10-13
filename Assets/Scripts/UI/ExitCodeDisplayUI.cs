using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Puzzle;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Full-screen modal that clearly displays the EXIT KEYPAD CODE after the lockbox is opened.
    /// Uses OnGUI (ImGUI) consistent with all other UI systems in this project.
    /// Triggered by LockedContainer.OnAnyContainerOpened.
    /// Fires ExitCodeDisplayUI.OnCodeDisplayed when shown so ObjectiveProgressionAdapter
    /// can mark EXIT_NOTE_READ without reading the physical clue note.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExitCodeDisplayUI : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static ExitCodeDisplayUI instance;
        public static ExitCodeDisplayUI Instance => instance;

        // ── Public state ─────────────────────────────────────────────────────
        /// <summary>True while the code panel is on screen. Used to suppress player input.</summary>
        public static bool IsDisplayOpen => instance != null && instance.isOpen;

        // ── Static event ─────────────────────────────────────────────────────
        /// <summary>Fired once when the code panel becomes visible.</summary>
        public static event System.Action OnCodeDisplayed;
        /// <summary>Fired when the player finishes reading and closes the code note panel.</summary>
        public static event System.Action OnCodeClosed;

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Appearance")]
        [SerializeField] private float panelWidth  = 480f;
        [SerializeField] private float panelHeight = 310f;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration  = 0.35f;
        [SerializeField] private float fadeOutDuration = 0.25f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool   isOpen     = false;
        private string codeText   = "";
        private float  alpha      = 0f;
        private Coroutine fadeRoutine;

        // ── Styles (created once on first OnGUI call) ─────────────────────────
        private GUIStyle titleStyle;
        private GUIStyle codeStyle;
        private GUIStyle promptStyle;
        private bool     stylesInitialised = false;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnEnable()
        {
            EscapeRoom.Interaction.LockedContainer.OnAnyContainerOpened += HandleContainerOpened;
        }

        private void OnDisable()
        {
            EscapeRoom.Interaction.LockedContainer.OnAnyContainerOpened -= HandleContainerOpened;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ── Event handlers ───────────────────────────────────────────────────
        private void HandleContainerOpened()
        {
            // Resolve keypad code at the moment the container opens
            KeypadController keypad = FindAnyObjectByType<KeypadController>();
            string code = (keypad != null && !string.IsNullOrEmpty(keypad.TargetCode))
                ? keypad.TargetCode
                : "8431";

            ShowCode(code);
        }

        // ── Public API ───────────────────────────────────────────────────────
        /// <summary>Opens the code display panel with the given code string.</summary>
        public void ShowCode(string code)
        {
            if (isOpen) return;

            codeText = code;
            isOpen   = true;
            stylesInitialised = false; // force style rebuild

            // ── Code reveal sound ─────────────────────────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.Play(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.CodeNoteReveal);

            // Unlock cursor so player can see the panel
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            var playerLook = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
            if (playerLook != null) playerLook.SetCursorLock(false);

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn());

            OnCodeDisplayed?.Invoke();
            Debug.Log($"<color=#5cb85c><b>[ExitCodeDisplayUI]</b></color> Code panel shown: {codeText}");
        }

        /// <summary>Closes the code display panel and restores first-person controls.</summary>
        public void CloseDisplay()
        {
            if (!isOpen) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOutThenClose());
        }

        // ── Input ────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame ||
                keyboard.eKey.wasPressedThisFrame      ||
                keyboard.spaceKey.wasPressedThisFrame  ||
                keyboard.enterKey.wasPressedThisFrame)
            {
                CloseDisplay();
            }
        }

        // ── Fade coroutines ───────────────────────────────────────────────────
        private IEnumerator FadeIn()
        {
            alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                alpha    = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            alpha = 1f;
        }

        private IEnumerator FadeOutThenClose()
        {
            float startAlpha = alpha;
            float elapsed    = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                alpha    = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            alpha  = 0f;
            isOpen = false;
            OnCodeClosed?.Invoke();

            // Re-lock cursor into gameplay
            var playerLook = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
            Debug.Log("[ExitCodeDisplayUI] Panel closed; first-person controls restored.");
        }

        // ── Rendering ─────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!isOpen || alpha <= 0f) return;
            if (Event.current.type != EventType.Repaint) return;

            if (!stylesInitialised) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Dim overlay
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture);

            // Panel
            float px = (sw - panelWidth)  * 0.5f;
            float py = (sh - panelHeight) * 0.5f;

            GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.96f * alpha);
            GUI.DrawTexture(new Rect(px, py, panelWidth, panelHeight), Texture2D.whiteTexture);

            // Thin accent line at top of panel
            GUI.color = new Color(1f, 0.85f, 0.20f, 0.90f * alpha);
            GUI.DrawTexture(new Rect(px, py, panelWidth, 3f), Texture2D.whiteTexture);

            GUI.color = prevColor;

            // Title
            titleStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(px, py + 28f, panelWidth, 40f), "EXIT KEYPAD CODE", titleStyle);

            // Separator line
            GUI.color = new Color(0.35f, 0.35f, 0.35f, 0.80f * alpha);
            GUI.DrawTexture(new Rect(px + 40f, py + 78f, panelWidth - 80f, 1f), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Code
            codeStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(px, py + 90f, panelWidth, 130f), codeText, codeStyle);

            // Dismiss prompt
            promptStyle.normal.textColor = new Color(1f, 1f, 1f, alpha * 0.85f);
            GUI.Label(new Rect(px, py + panelHeight - 46f, panelWidth, 30f),
                      "Press  E  /  ESC  to close", promptStyle);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = false
            };

            codeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 92,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = false
            };

            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = false
            };

            stylesInitialised = true;
        }
    }
}
