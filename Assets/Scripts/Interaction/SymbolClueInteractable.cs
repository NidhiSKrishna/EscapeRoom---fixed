using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Player;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Symbol clue note for Stage 2 (Storage Room).
    /// Displays a four-symbol combination clue in a readable modal panel.
    /// Implements IInteractable.
    /// Fires OnAnySymbolClueOpened so the StorageRoomProgressionAdapter can complete the objective.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SymbolClueInteractable : MonoBehaviour, IInteractable
    {
        // ── Static / shared ──────────────────────────────────────────────────
        private static SymbolClueInteractable activeClue;
        public static bool   IsAnyClueOpen => activeClue != null && activeClue.isReading;
        public static event System.Action OnAnySymbolClueOpened;

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Clue Content")]
        [Tooltip("Title displayed at top of the reading panel.")]
        [SerializeField] private string clueTitle = "Storage Room Clue";

        [TextArea(2, 4)]
        [Tooltip("Flavour text displayed above the symbols.")]
        [SerializeField] private string bodyText = "A scrap of paper tucked behind the shelf:";

        [Tooltip("Four symbol strings to display as the combination clue.")]
        [SerializeField] private string[] symbols = { "△", "○", "□", "★" };

        [Header("Interaction")]
        [SerializeField] private string promptText = "Inspect Clue";

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isReading = false;
        private PlayerLook cachedPlayerLook;
        private bool clueEventFired = false;

        // ── IInteractable ────────────────────────────────────────────────────
        public string InteractionPrompt => EscapeRoom.Core.InputConfig.FormatPrompt("Inspect Storage Room Clue");
        public bool   CanInteract       => !isReading && gameObject.activeInHierarchy;
        public bool   IsReading         => isReading;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Update()
        {
            if (!isReading) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame ||
                kb.eKey.wasPressedThisFrame      ||
                kb.spaceKey.wasPressedThisFrame)
            {
                CloseClue();
            }
        }

        private void OnDestroy()
        {
            if (activeClue == this) activeClue = null;
        }

        // ── IInteractable ────────────────────────────────────────────────────
        public void Interact()
        {
            if (isReading) return;
            OpenClue();
        }

        public void OpenClue()
        {
            isReading  = true;
            activeClue = this;

            if (cachedPlayerLook == null)
                cachedPlayerLook = FindAnyObjectByType<PlayerLook>();

            if (cachedPlayerLook != null) cachedPlayerLook.SetCursorLock(false);
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            EscapeRoom.UI.FeedbackHUD.ShowMessage("Reading: " + clueTitle, new Color(0.90f, 0.90f, 0.70f));

            if (!clueEventFired)
            {
                clueEventFired = true;
                OnAnySymbolClueOpened?.Invoke();
            }

            Debug.Log($"<color=#337ab7><b>[SymbolClueInteractable]</b></color> Opened clue '{clueTitle}'.");
        }

        public void CloseClue()
        {
            if (!isReading) return;
            isReading = false;
            if (activeClue == this) activeClue = null;

            if (cachedPlayerLook != null) cachedPlayerLook.SetCursorLock(true);
            else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            Debug.Log($"[SymbolClueInteractable] Closed '{clueTitle}'.");
        }

        // ── ImGUI ────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!isReading) return;
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            GUI.depth = -20;
            Color old = GUI.color;

            // Overlay
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float pw = Mathf.Clamp(Screen.width * 0.50f, 480f, 560f);
            float ph = Mathf.Clamp(Screen.height * 0.54f, 320f, 400f);
            float px = (Screen.width  - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // Gold border
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 1.0f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);

            // Dark card
            GUI.color = new Color(0.10f, 0.11f, 0.15f, 1.0f);
            GUI.DrawTexture(new Rect(px + 3, py + 3, pw - 6, ph - 6), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 18, pw - 40, 30), clueTitle, titleStyle);

            // Divider
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.50f);
            GUI.DrawTexture(new Rect(px + 24, py + 52, pw - 48, 2), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Body text
            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14, fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, py + 60, pw - 40, 32), bodyText, bodyStyle);

            // Symbols display box
            float boxW = pw - 60f, boxH = 100f;
            float boxX = px + 30f, boxY = py + 100f;

            // Box border
            GUI.color = new Color(0.30f, 0.70f, 1.0f, 0.90f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            GUI.color = new Color(0.04f, 0.06f, 0.10f, 1.0f);
            GUI.DrawTexture(new Rect(boxX + 2, boxY + 2, boxW - 4, boxH - 4), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Symbols text — e.g. "△   ○   □   ★"
            string symbolLine = EscapeRoom.Core.PlaythroughGameState.Instance != null
                ? EscapeRoom.Core.PlaythroughGameState.Instance.GetFormattedSymbolSequence()
                : (symbols != null && symbols.Length > 0 ? string.Join("   ", symbols) : "△   ○   □   ★");

            GUIStyle symbolStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 42, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(boxX + 6, boxY + 8, boxW - 12, boxH - 16), symbolLine, symbolStyle);

            // Hint
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 20, boxY + boxH + 6, pw - 40, 22),
                      "This is the cabinet combination order.", hintStyle);

            // Close button
            float btnW = 200f, btnH = 36f;
            float btnX = px + (pw - btnW) * 0.5f;
            float btnY = py + ph - 52f;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.backgroundColor = new Color(0.25f, 0.28f, 0.35f);
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Close (ESC / E)", btnStyle))
                CloseClue();

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }
    }
}
