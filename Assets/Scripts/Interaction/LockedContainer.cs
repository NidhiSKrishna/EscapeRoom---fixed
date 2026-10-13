using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using EscapeRoom.UI;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Interactive container that starts locked and requires a specific item ID (e.g. 'lockbox_key') in inventory to open.
    /// Implements IInteractable.
    /// Two-step interaction:
    ///   1st press (locked + key present) → unlock the box, show "BOX UNLOCKED / OPEN THE BOX" messages.
    ///   2nd press (unlocked but not open) → open lid, reveal contents, show ExitCodeDisplayUI.
    /// Rotates a lid/cover smoothly and reveals contents upon opening.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LockedContainer : MonoBehaviour, IInteractable
    {
        [Header("Key Requirement")]
        [Tooltip("The item ID required in the player's inventory to unlock this container.")]
        [SerializeField] private string requiredItemId = "lockbox_key";

        [Tooltip("Friendly name of the required key for prompt display.")]
        [SerializeField] private string requiredItemName = "Lockbox Key";

        [Tooltip("Whether to consume/remove the key from the inventory upon opening.")]
        [SerializeField] private bool consumeKey = false;

        [Header("State")]
        [SerializeField] private bool isLocked = true;
        [SerializeField] private bool isOpen = false;

        [Header("Visual / Animation Elements")]
        [Tooltip("Optional child transform that hinges/rotates open.")]
        [SerializeField] private Transform lidTransform;

        [Tooltip("Target local euler rotation angles for open state.")]
        [SerializeField] private Vector3 openRotationOffset = new Vector3(-105f, 0f, 0f);

        [Tooltip("Duration of the lid opening animation in seconds.")]
        [SerializeField] private float openDuration = 0.8f;

        [Tooltip("Optional hidden contents object inside the container revealed on opening.")]
        [SerializeField] private GameObject contentsObject;

        [Header("Prompts")]
        [SerializeField] private string lockedMissingKeyPrompt = "Locked - requires {0}";
        [SerializeField] private string lockedHaveKeyPrompt = "Press E to unlock with {0}";
        [SerializeField] private string unlockedOpenPrompt = "Press E to open";
        [SerializeField] private string openedPrompt = "Container is already open";

        [Header("Events")]
        public UnityEvent onContainerUnlocked;
        public UnityEvent onContainerOpened;
        public static event System.Action OnAnyContainerOpened;

        private Quaternion closedRotation;
        private Coroutine openCoroutine;

        public string RequiredItemId
        {
            get => requiredItemId;
            set => requiredItemId = value;
        }

        public string RequiredItemName
        {
            get => requiredItemName;
            set => requiredItemName = value;
        }

        public bool IsLocked => isLocked;
        public bool IsOpen => isOpen;

        public Transform LidTransform
        {
            get => lidTransform;
            set => lidTransform = value;
        }

        public GameObject ContentsObject
        {
            get => contentsObject;
            set => contentsObject = value;
        }

        public string InteractionPrompt
        {
            get
            {
                if (isOpen)
                {
                    return openedPrompt;
                }

                if (isLocked)
                {
                    bool hasKey = InventorySystem.Instance != null &&
                        (InventorySystem.Instance.HasItem(requiredItemId) || (requiredItemId == "lockbox_key" && InventorySystem.Instance.HasItem("room_key")));
                    return hasKey
                        ? $"Press O to Open Lockbox"
                        : $"Press O to Open Lockbox (Requires {requiredItemName})";
                }

                // Unlocked but not yet open
                return $"Press O to Open Lockbox";
            }
        }

        /// <summary>
        /// Allow interaction while locked (to unlock) OR while unlocked-but-not-open (to open lid).
        /// Disallow once fully open.
        /// </summary>
        public bool CanInteract => !isOpen;


        private void Awake()
        {
            // Auto-migrate legacy 'room_key' or old prompt format
            if (requiredItemId == "room_key")
            {
                requiredItemId = "lockbox_key";
                requiredItemName = "Lockbox Key";
            }
            if (lockedMissingKeyPrompt == "Locked (Requires {0})")
            {
                lockedMissingKeyPrompt = "Locked - requires {0}";
            }

            if (lidTransform != null)
            {
                closedRotation = lidTransform.localRotation;
            }

            if (contentsObject == null)
            {
                var clueChild = GetComponentInChildren<ClueInteractable>(true);
                if (clueChild != null)
                {
                    contentsObject = clueChild.gameObject;
                }
            }

            if (isOpen)
            {
                Collider rootCol = GetComponent<Collider>();
                if (rootCol != null) rootCol.enabled = false;
                if (contentsObject != null) contentsObject.SetActive(true);
            }
            else if (contentsObject != null && isLocked)
            {
                contentsObject.SetActive(false);
            }
        }

        public void Interact()
        {
            if (isOpen) return;

            if (isLocked)
            {
                // ── Step 1: Try to unlock ────────────────────────────────────────
                InventorySystem inventory = InventorySystem.Instance;
                if (inventory == null)
                {
                    inventory = FindAnyObjectByType<InventorySystem>();
                }

                bool hasKey = inventory != null &&
                    (inventory.HasItem(requiredItemId) || (requiredItemId == "lockbox_key" && inventory.HasItem("room_key")));

                if (!hasKey)
                {
                    Debug.Log($"[LockedContainer] '{gameObject.name}' is locked. Requires '{requiredItemId}'.");
                    FeedbackHUD.ShowMessage($"Locked - requires {requiredItemName}", new Color(0.95f, 0.40f, 0.40f));
                    return;
                }

                // Player holds the required key — unlock only, do NOT open yet.
                isLocked = false;
                Debug.Log($"<color=#5cb85c><b>[LockedContainer]</b></color> '{gameObject.name}' unlocked using '{requiredItemId}'!");

                // ── Unlock sound ──────────────────────────────────────────────
                EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                    EscapeRoom.Audio.EscapeRoomAudio.SoundId.LockboxUnlock,
                    transform.position);

                // Show a clear two-part message so the player knows what to do next.
                FeedbackHUD.ShowMessage("BOX UNLOCKED  ·  Press E again to OPEN", new Color(0.40f, 0.90f, 0.45f));

                if (consumeKey && inventory != null)
                {
                    if (inventory.HasItem(requiredItemId))
                        inventory.RemoveItem(requiredItemId);
                    else if (inventory.HasItem("room_key"))
                        inventory.RemoveItem("room_key");
                }

                onContainerUnlocked?.Invoke();

                // Do NOT call OpenContainer() here — player must press E again.
                return;
            }

            // ── Step 2: Already unlocked — open the lid ──────────────────────
            OpenContainer();
        }


        public void OpenContainer()
        {
            if (isOpen) return;
            isOpen = true;

            if (openCoroutine != null)
            {
                StopCoroutine(openCoroutine);
            }

            if (lidTransform != null && gameObject.activeInHierarchy)
            {
                openCoroutine = StartCoroutine(AnimateLidOpen());
            }

            if (contentsObject == null)
            {
                var clueChild = GetComponentInChildren<ClueInteractable>(true);
                if (clueChild != null)
                {
                    contentsObject = clueChild.gameObject;
                }
            }

            if (contentsObject != null)
            {
                contentsObject.SetActive(true);
            }

            // Disable container interaction collider so player can directly look at and target contents
            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null)
            {
                rootCol.enabled = false;
            }

            Debug.Log($"<color=#5cb85c><b>[LockedContainer]</b></color> '{gameObject.name}' is now open.");

            // ── Lid-open sound ────────────────────────────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.LockboxOpen,
                transform.position);

            onContainerOpened?.Invoke();
            OnAnyContainerOpened?.Invoke();
        }

        private IEnumerator AnimateLidOpen()
        {
            Quaternion startRot = lidTransform.localRotation;
            Quaternion targetRot = closedRotation * Quaternion.Euler(openRotationOffset);
            float elapsed = 0f;

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                // Smooth step curve
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                lidTransform.localRotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                yield return null;
            }

            lidTransform.localRotation = targetRot;
        }
    }
}
