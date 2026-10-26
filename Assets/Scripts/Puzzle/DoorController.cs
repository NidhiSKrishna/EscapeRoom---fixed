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

        [Header("Transition Lighting")]
        [Tooltip("Optional light that illuminates the doorway opening transition.")]
        [SerializeField] private Light doorwayGlowLight;

        public void Interact()
        {
            if (isOpen) return;

            if (isLocked)
            {
                Debug.Log("[DoorController] Exit door is locked. Clear all security systems to unlock.");
                FeedbackHUD.ShowMessage("Exit door is locked! Clear all security systems to unlock.", Color.white);
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

            // ── Door unlock sound: subtle spatial audio ─────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.DoorUnlock,
                transform.position,
                0.50f);

            FeedbackHUD.ShowMessage("Exit Door Unlocked!", Color.white);
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
        /// Animates the door open if unlocked with audio and lighting transition.
        /// </summary>
        public void OpenDoor()
        {
            if (isOpen || isLocked) return;
            isOpen = true;

            if (swingCoroutine != null)
            {
                StopCoroutine(swingCoroutine);
            }

            // ── Door open sound: subtle spatial audio ──────────────────────────
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.DoorOpen,
                transform.position,
                0.55f);

            // Ensure doorway transition light
            EnsureDoorwayLight();

            if (gameObject.activeInHierarchy)
            {
                swingCoroutine = StartCoroutine(AnimateDoorSwing());
            }

            // Disable root interaction collider so doorway passage is completely clear
            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null)
            {
                rootCol.enabled = false;
            }

            Debug.Log($"<color=#5cb85c><b>[DoorController]</b></color> '{gameObject.name}' opening...");
            FeedbackHUD.ShowMessage("Exit Door Opened - Proceed to Exit!", Color.white);
            onDoorOpened?.Invoke();
        }

        private void EnsureDoorwayLight()
        {
            if (doorwayGlowLight == null)
            {
                var lightChild = transform.Find("DoorwayGlowLight");
                if (lightChild != null)
                {
                    doorwayGlowLight = lightChild.GetComponent<Light>();
                }
                else
                {
                    GameObject glowGo = new GameObject("DoorwayGlowLight");
                    glowGo.transform.SetParent(transform, false);
                    glowGo.transform.localPosition = new Vector3(0.52f, 1.4f, 0.40f);
                    doorwayGlowLight = glowGo.AddComponent<Light>();
                    doorwayGlowLight.type = LightType.Point;
                    doorwayGlowLight.color = new Color(0.98f, 0.92f, 0.78f);
                    doorwayGlowLight.range = 6.0f;
                    doorwayGlowLight.intensity = 0f;
                }
            }
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

                if (doorwayGlowLight != null)
                {
                    doorwayGlowLight.intensity = Mathf.Lerp(0f, 2.2f, smoothT);
                }

                yield return null;
            }

            target.localRotation = targetRot;
            if (doorwayGlowLight != null)
            {
                doorwayGlowLight.intensity = 2.2f;
            }
        }
    }
}
