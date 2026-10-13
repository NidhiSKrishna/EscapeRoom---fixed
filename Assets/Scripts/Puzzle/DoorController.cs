using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Controls the physical exit door placed within the ProBuilder doorway frame.
    /// Implements IInteractable.
    /// Starts locked, can be unlocked via Keypad authorization, and animates open smoothly.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DoorController : MonoBehaviour, IInteractable
    {
        [Header("Door State")]
        [Tooltip("Whether the door is currently locked by the security system.")]
        [SerializeField] private bool isLocked = true;

        [Tooltip("Whether the door is currently in its open position.")]
        [SerializeField] private bool isOpen = false;

        [Tooltip("Whether the door should automatically swing open when unlocked.")]
        [SerializeField] private bool autoOpenOnUnlock = true;

        [Header("Animation & Hinge")]
        [Tooltip("The transform representing the door hinge/slab that rotates open. If null, this transform is used.")]
        [SerializeField] private Transform hingeTransform;

        [Tooltip("Relative local rotation applied when fully opened.")]
        [SerializeField] private Vector3 openEulerAngles = new Vector3(0f, -95f, 0f);

        [Tooltip("Duration of the door swing animation in seconds.")]
        [SerializeField] private float animationDuration = 1.4f;

        [Header("Prompts")]
        [SerializeField] private string lockedPrompt = "Exit Door (Locked)";
        [SerializeField] private string unlockPrompt = "Press E to open Exit Door";
        [SerializeField] private string openedPrompt = "Exit Door (Open)";

        [Header("Events")]
        public UnityEvent onDoorUnlocked;
        public UnityEvent onDoorOpened;

        private Quaternion closedRotation;
        private Coroutine swingCoroutine;

        public bool IsLocked
        {
            get => isLocked;
            set => isLocked = value;
        }

        public bool IsOpen => isOpen;

        public Transform HingeTransform
        {
            get => hingeTransform;
            set
            {
                hingeTransform = value;
                if (hingeTransform != null)
                {
                    closedRotation = hingeTransform.localRotation;
                }
            }
        }

        public Vector3 OpenEulerAngles
        {
            get => openEulerAngles;
            set => openEulerAngles = value;
        }

        public float AnimationDuration
        {
            get => animationDuration;
            set => animationDuration = Mathf.Max(0.1f, value);
        }

        public string InteractionPrompt
        {
            get
            {
                if (isOpen) return openedPrompt;
                if (isLocked) return "Press O to Open Exit Door (Locked)";
                return "Press O to Open Exit Door";
            }
        }

        public bool CanInteract => !isOpen;

        private void Awake()
        {
            if (hingeTransform == null)
            {
                hingeTransform = transform;
            }
            closedRotation = hingeTransform.localRotation;
        }

        public void Interact()
        {
            if (isOpen) return;

            if (isLocked)
            {
                Debug.Log("[DoorController] Exit door is locked. Clear all security systems to unlock.");
                FeedbackHUD.ShowMessage("Exit door is locked! Clear all security systems to unlock.", new Color(0.95f, 0.40f, 0.40f));
                return;
            }

            OpenDoor();
        }

        /// <summary>
        /// Unlocks the door, allowing it to be opened. If autoOpenOnUnlock is true, immediately swings open.
        /// </summary>
        public void Unlock()
        {
            if (!isLocked) return;

            isLocked = false;
            Debug.Log($"<color=#5cb85c><b>[DoorController]</b></color> '{gameObject.name}' unlocked!");

            // ── Door unlock sound ─────────────────────────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.DoorUnlock,
                transform.position);

            FeedbackHUD.ShowMessage("Exit Door Unlocked!", new Color(0.40f, 0.95f, 0.45f));
            onDoorUnlocked?.Invoke();

            if (autoOpenOnUnlock)
            {
                OpenDoor();
            }
        }

        /// <summary>
        /// Explicit method to unlock and open simultaneously.
        /// </summary>
        public void UnlockAndOpen()
        {
            isLocked = false;
            OpenDoor();
        }

        /// <summary>
        /// Animates the door open if unlocked.
        /// </summary>
        public void OpenDoor()
        {
            if (isOpen || isLocked) return;
            isOpen = true;

            if (swingCoroutine != null)
            {
                StopCoroutine(swingCoroutine);
            }

            // ── Door open sound ───────────────────────────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.DoorOpen,
                transform.position);

            if (gameObject.activeInHierarchy)
            {
                swingCoroutine = StartCoroutine(AnimateDoorSwing());
            }

            Debug.Log($"<color=#5cb85c><b>[DoorController]</b></color> '{gameObject.name}' opening...");
            FeedbackHUD.ShowMessage("Exit Door Opened - Proceed to Exit!", new Color(0.35f, 0.95f, 0.95f));
            onDoorOpened?.Invoke();
        }

        private IEnumerator AnimateDoorSwing()
        {
            Transform target = hingeTransform != null ? hingeTransform : transform;
            Quaternion startRot = target.localRotation;
            Quaternion targetRot = closedRotation * Quaternion.Euler(openEulerAngles);
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                target.localRotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                yield return null;
            }

            target.localRotation = targetRot;
        }
    }
}
