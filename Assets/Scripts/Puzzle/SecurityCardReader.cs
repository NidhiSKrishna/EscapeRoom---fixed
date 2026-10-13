using UnityEngine;
using UnityEngine.Events;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Security card reader mounted beside the exit door.
    /// Checks whether the player holds a specific item ID (the access card).
    /// On success: plays access-granted sound, unlocks the linked door, fires events.
    /// On failure: plays denial sound, shows "ACCESS CARD REQUIRED" message.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SecurityCardReader : MonoBehaviour, IInteractable
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Card Requirement")]
        [Tooltip("Item ID that must be in inventory to use the reader (e.g. 'access_card').")]
        [SerializeField] private string requiredItemId = "access_card";

        [Header("State")]
        [SerializeField] private bool isActivated = false;

        [Header("Linked Door")]
        [Tooltip("The security door that is unlocked when the card is accepted.")]
        [SerializeField] private DoorController linkedDoor;

        [Header("Reader Light Visual")]
        [Tooltip("Optional renderer whose material colour changes on activation (e.g. a light indicator).")]
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Color idleColor     = new Color(0.8f, 0.1f, 0.1f);
        [SerializeField] private Color activatedColor = new Color(0.1f, 0.8f, 0.2f);

        [Header("Prompts")]
        [SerializeField] private string noCardPrompt       = "Use Access Card";
        [SerializeField] private string hasCardPrompt      = "Use Access Card on reader";
        [SerializeField] private string activatedPrompt    = "ACCESS GRANTED";

        [Header("Events")]
        public UnityEvent onAccessGranted;
        public UnityEvent onAccessDenied;
        public static event System.Action OnAnyAccessGranted;

        // ── IInteractable ────────────────────────────────────────────────────
        public string InteractionPrompt
        {
            get
            {
                if (isActivated) return activatedPrompt;
                string keyName = EscapeRoom.Core.InputConfig.InteractKeyName;
                bool hasCard = InventorySystem.Instance != null &&
                               InventorySystem.Instance.HasItem(requiredItemId);
                return hasCard ? $"[{keyName}] Use Access Card on Reader" : $"[{keyName}] Inspect Card Reader (Requires Access Card)";
            }
        }
        public bool CanInteract => !isActivated && gameObject.activeInHierarchy;
        public bool IsActivated => isActivated;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Start()
        {
            ApplyIndicatorColor(idleColor);
        }

        // ── IInteractable ────────────────────────────────────────────────────
        public void Interact()
        {
            if (isActivated) return;

            var inventory = InventorySystem.Instance ??
                            FindAnyObjectByType<InventorySystem>();

            bool hasCard = inventory != null && inventory.HasItem(requiredItemId);

            if (!hasCard)
            {
                Debug.Log("[SecurityCardReader] ACCESS DENIED: no access card in inventory.");
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong);
                FeedbackHUD.ShowMessage("ACCESS CARD REQUIRED", new Color(0.95f, 0.40f, 0.40f));
                onAccessDenied?.Invoke();
                return;
            }

            // Card accepted!
            isActivated = true;
            Debug.Log("<color=#5cb85c><b>[SecurityCardReader]</b></color> ACCESS GRANTED. Unlocking door.");

            EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadCorrect);
            FeedbackHUD.ShowMessage("ACCESS GRANTED  ·  Security door unlocked!", new Color(0.40f, 0.95f, 0.45f));

            ApplyIndicatorColor(activatedColor);

            if (linkedDoor != null)
            {
                linkedDoor.Unlock();
            }

            onAccessGranted?.Invoke();
            OnAnyAccessGranted?.Invoke();
        }

        private void ApplyIndicatorColor(Color color)
        {
            if (indicatorRenderer == null) return;
            // Use MaterialPropertyBlock to avoid creating new material instances
            var mpb = new MaterialPropertyBlock();
            indicatorRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_EmissionColor", color * 2f);
            indicatorRenderer.SetPropertyBlock(mpb);
        }
    }
}
