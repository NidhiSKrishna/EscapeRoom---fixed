using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Four-symbol combination cabinet puzzle for Stage 2 (Storage Room).
    /// The player must select the correct symbol in each of the four slots to unlock the cabinet.
    /// On unlock: fires OnCabinetUnlocked, plays success sound, and reveals the contents.
    /// On wrong combination: plays error sound and shows denial message.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SymbolCombinationCabinet : MonoBehaviour, IInteractable
    {
        // ── Symbol catalogue ─────────────────────────────────────────────────
        public static readonly string[] Symbols = { "△", "○", "□", "★", "◇", "✦" };

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Combination Settings")]
        [Tooltip("Correct symbol indices (0-5) for each of the 4 slots. 0=△ 1=○ 2=□ 3=★ 4=◇ 5=✦")]
        [SerializeField] private int[] correctIndices = { 0, 1, 2, 3 }; // △ ○ □ ★

        [Header("Cabinet State")]
        [SerializeField] private bool isLocked   = true;
        [SerializeField] private bool isOpen     = false;

        [Header("Animation")]
        [Tooltip("Optional door/lid transform that swings open.")]
        [SerializeField] private Transform doorTransform;
        [Tooltip("Local euler rotation delta when open.")]
        [SerializeField] private Vector3 openEulerDelta = new Vector3(0f, -110f, 0f);
        [SerializeField] private float   openDuration   = 0.9f;

        [Header("Contents")]
        [Tooltip("Hidden object inside the cabinet revealed on opening (e.g. access card pickup).")]
        [SerializeField] private GameObject contentsObject;

        [Header("Prompts")]
        [SerializeField] private string lockedPrompt   = "Enter Combination";
        [SerializeField] private string unlockedPrompt = "Press E to open cabinet";
        [SerializeField] private string openedPrompt   = "Cabinet is open";

        [Header("Events")]
        public UnityEvent onCabinetUnlocked;
        public UnityEvent onCabinetOpened;
        public static event System.Action OnAnyCabinetUnlocked;
        public static event System.Action OnAnyCabinetOpened;

        // ── Runtime ──────────────────────────────────────────────────────────
        private int[] currentIndices = { 0, 0, 0, 0 };
        private bool  uiOpen         = false;
        private bool  wrongFlash     = false;
        private float wrongFlashTimer = 0f;
        private Quaternion closedDoorRot;
        private Coroutine openCoroutine;

        public int[] CorrectIndices
        {
            get
            {
                if (EscapeRoom.Core.PlaythroughGameState.Instance != null &&
                    EscapeRoom.Core.PlaythroughGameState.Instance.SymbolPattern != null &&
                    EscapeRoom.Core.PlaythroughGameState.Instance.SymbolPattern.Length == 4)
                {
                    return EscapeRoom.Core.PlaythroughGameState.Instance.SymbolPattern;
                }
                return correctIndices;
            }
        }

        // ── IInteractable ────────────────────────────────────────────────────
        public string InteractionPrompt
        {
            get
            {
                if (isOpen)   return openedPrompt;
                if (isLocked) return EscapeRoom.Core.InputConfig.FormatPrompt("Examine Symbol Combination Cabinet");
                return EscapeRoom.Core.InputConfig.FormatPrompt("Open Cabinet");
            }
        }
        public bool CanInteract => !isOpen && gameObject.activeInHierarchy;
        public bool IsLocked    => isLocked;
        public bool IsOpen      => isOpen;
        public bool IsUIOpen    => uiOpen;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (doorTransform != null)
                closedDoorRot = doorTransform.localRotation;

            if (contentsObject != null)
                contentsObject.SetActive(false);

            // Validate correct indices length
            if (correctIndices == null || correctIndices.Length != 4)
                correctIndices = new int[] { 0, 1, 2, 3 };
        }

        private void Update()
        {
            if (!uiOpen) return;

            if (wrongFlashTimer > 0f)
            {
                wrongFlashTimer -= Time.deltaTime;
                if (wrongFlashTimer <= 0f)
                {
                    wrongFlash = false;
                    wrongFlashTimer = 0f;
                }
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseUI();
            }
        }

        // ── IInteractable ────────────────────────────────────────────────────
        public void Interact()
        {
            if (isOpen) return;

            if (isLocked)
            {
                OpenUI();
            }
            else
            {
                OpenCabinet();
            }
        }

        // ── Combination UI ───────────────────────────────────────────────────
        public void OpenUI()
        {
            if (uiOpen) return;
            uiOpen = true;
            wrongFlash = false;
            wrongFlashTimer = 0f;

            // Unlock cursor
            var playerLook = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
            if (playerLook != null) playerLook.SetCursorLock(false);
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.5f);
            Debug.Log("[SymbolCombinationCabinet] Combination UI opened.");
        }

        public void CloseUI()
        {
            if (!uiOpen) return;
            uiOpen = false;
            wrongFlash = false;

            // Re-lock cursor
            var playerLook = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
            if (playerLook != null) playerLook.SetCursorLock(true);
            else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            Debug.Log("[SymbolCombinationCabinet] Combination UI closed.");
        }

        private void SubmitCombination()
        {
            bool correct = true;
            int[] targetSol = CorrectIndices;
            for (int i = 0; i < 4; i++)
            {
                if (currentIndices[i] != targetSol[i])
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
            {
                isLocked = false;
                Debug.Log("<color=#5cb85c><b>[SymbolCombinationCabinet]</b></color> Correct combination! Cabinet unlocked.");

                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadCorrect);
                FeedbackHUD.ShowMessage("CABINET UNLOCKED  ·  Press E to open", new Color(0.40f, 0.95f, 0.45f));

                CloseUI();
                onCabinetUnlocked?.Invoke();
                OnAnyCabinetUnlocked?.Invoke();
            }
            else
            {
                Debug.Log("<color=#d9534f>[SymbolCombinationCabinet]</color> Incorrect combination.");
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong);
                FeedbackHUD.ShowMessage("WRONG COMBINATION", new Color(0.95f, 0.35f, 0.35f));
                wrongFlash = true;
                wrongFlashTimer = 0.6f;
            }
        }

        // ── Open Cabinet ─────────────────────────────────────────────────────
        public void OpenCabinet()
        {
            if (isOpen || isLocked) return;
            isOpen = true;

            EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.LockboxOpen);
            FeedbackHUD.ShowMessage("Cabinet opened!", new Color(0.75f, 0.92f, 0.98f));

            if (contentsObject != null)
                contentsObject.SetActive(true);

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (doorTransform != null && gameObject.activeInHierarchy)
                openCoroutine = StartCoroutine(AnimateDoorOpen());

            Debug.Log("<color=#5cb85c><b>[SymbolCombinationCabinet]</b></color> Cabinet opened.");
            onCabinetOpened?.Invoke();
            OnAnyCabinetOpened?.Invoke();
        }

        private IEnumerator AnimateDoorOpen()
        {
            Quaternion startRot   = doorTransform.localRotation;
            Quaternion targetRot  = closedDoorRot * Quaternion.Euler(openEulerDelta);
            float      elapsed    = 0f;

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openDuration));
                doorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            doorTransform.localRotation = targetRot;
        }

        // ── ImGUI Combination Panel ──────────────────────────────────────────
        private void OnGUI()
        {
            if (!uiOpen) return;
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // Dim overlay
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

            float pw = 420f, ph = 360f;
            float px = (sw - pw) * 0.5f, py = (sh - ph) * 0.5f;

            // Panel background
            GUI.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);

            // Gold border
            GUI.color = new Color(0.85f, 0.70f, 0.28f, 0.90f);
            GUI.DrawTexture(new Rect(px + 3, py + 3, pw - 6, ph - 6), Texture2D.whiteTexture);

            // Inner background
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(px + 5, py + 5, pw - 10, ph - 10), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 10, py + 14, pw - 20, 28), "COMBINATION CABINET", titleStyle);

            // Divider
            GUI.color = new Color(0.85f, 0.70f, 0.28f, 0.40f);
            GUI.DrawTexture(new Rect(px + 30, py + 46, pw - 60, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(px + 10, py + 52, pw - 20, 20), "Use  ◄  ►  arrows to change each symbol, then confirm", hintStyle);

            // Symbol slots
            float slotW = 80f, slotH = 80f;
            float totalSlotWidth = 4 * slotW + 3 * 10f; // 4 slots + 3 gaps of 10px
            float slotStartX = px + (pw - totalSlotWidth) * 0.5f;
            float slotY = py + 82f;

            GUIStyle symbolStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 32, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = wrongFlash ? new Color(1f, 0.3f, 0.3f) : Color.white }
            };

            GUIStyle arrowStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            for (int i = 0; i < 4; i++)
            {
                float slotX = slotStartX + i * (slotW + 10f);

                // Slot background
                GUI.color = wrongFlash
                    ? new Color(0.35f, 0.08f, 0.08f, 1f)
                    : new Color(0.06f, 0.08f, 0.11f, 1f);
                GUI.DrawTexture(new Rect(slotX, slotY, slotW, slotH), Texture2D.whiteTexture);
                GUI.color = wrongFlash
                    ? new Color(0.90f, 0.25f, 0.25f, 0.80f)
                    : new Color(0.30f, 0.65f, 1.0f, 0.70f);
                GUI.DrawTexture(new Rect(slotX + 2, slotY + 2, slotW - 4, slotH - 4), Texture2D.whiteTexture);
                GUI.color = wrongFlash
                    ? new Color(0.28f, 0.06f, 0.06f, 1f)
                    : new Color(0.06f, 0.08f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(slotX + 4, slotY + 4, slotW - 8, slotH - 8), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Symbol text
                GUI.Label(new Rect(slotX, slotY, slotW, slotH), Symbols[currentIndices[i]], symbolStyle);

                // Arrow buttons
                GUI.backgroundColor = new Color(0.28f, 0.32f, 0.38f);
                int captured = i; // capture loop variable
                if (GUI.Button(new Rect(slotX + 2, slotY + slotH + 4, 36f, 28f), "◄", arrowStyle))
                {
                    currentIndices[captured] = (currentIndices[captured] - 1 + Symbols.Length) % Symbols.Length;
                    EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.5f);
                }
                if (GUI.Button(new Rect(slotX + slotW - 38f, slotY + slotH + 4, 36f, 28f), "►", arrowStyle))
                {
                    currentIndices[captured] = (currentIndices[captured] + 1) % Symbols.Length;
                    EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.5f);
                }
                GUI.backgroundColor = Color.white;
            }

            // Confirm button
            float btnY = slotY + slotH + 48f;
            GUI.backgroundColor = new Color(0.15f, 0.65f, 0.30f);
            GUIStyle confirmStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            if (GUI.Button(new Rect(px + 40, btnY, pw * 0.5f - 50f, 38f), "CONFIRM", confirmStyle))
            {
                SubmitCombination();
            }

            // Close button
            GUI.backgroundColor = new Color(0.40f, 0.15f, 0.15f);
            GUIStyle closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            if (GUI.Button(new Rect(px + pw * 0.5f + 10f, btnY, pw * 0.5f - 50f, 38f), "CLOSE (ESC)", closeStyle))
            {
                CloseUI();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (uiOpen) CloseUI();
        }
    }
}
