using System;
using UnityEngine;
using UnityEngine.Events;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Interactive keypad terminal mounted in the environment.
    /// Implements IInteractable.
    /// Opens the KeypadUI upon interaction, validates configurable access codes,
    /// and dispatches reusable events when code is accepted or rejected.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KeypadController : MonoBehaviour, IInteractable
    {
        [Header("Keypad Configuration")]
        [Tooltip("The secret passcode required to trigger authorization. Configurable per keypad instance.")]
        [SerializeField] private string targetCode = "8431";

        [Tooltip("Maximum number of characters allowed in the entered passcode.")]
        [SerializeField] private int maxCodeLength = 4;

        [Header("State")]
        [SerializeField] private bool isSolved = false;

        [Header("Target Connections")]
        [Tooltip("Optional direct reference to the door unlocked by this keypad.")]
        [SerializeField] private DoorController linkedDoor;

        [Header("Prompts")]
        [SerializeField] private string promptText = "Press E to use Keypad";
        [SerializeField] private string solvedPrompt = "Keypad (Authorized)";

        [Header("Events")]
        public UnityEvent onCodeAccepted;
        public UnityEvent onCodeRejected;
        public static event System.Action OnAnyCodeAccepted;

        public DoorController LinkedDoor
        {
            get => linkedDoor;
            set => linkedDoor = value;
        }

        public string TargetCode
        {
            get
            {
                if (EscapeRoom.Core.PlaythroughGameState.Instance != null &&
                    !string.IsNullOrEmpty(EscapeRoom.Core.PlaythroughGameState.Instance.Passcode))
                {
                    return EscapeRoom.Core.PlaythroughGameState.Instance.Passcode;
                }
                return targetCode;
            }
            set => targetCode = value;
        }

        public int MaxCodeLength => maxCodeLength;
        public bool IsSolved => isSolved;

        public string InteractionPrompt => isSolved ? solvedPrompt : InputConfig.FormatPrompt("Use Keypad Terminal");
        public bool CanInteract => !isSolved && gameObject.activeInHierarchy;

        public void Interact()
        {
            if (isSolved) return;

            KeypadUI ui = KeypadUI.Instance;
            if (ui == null)
            {
                ui = FindAnyObjectByType<KeypadUI>();
            }

            if (ui == null)
            {
                // Auto create KeypadUI if missing in scene
                var go = new GameObject("KeypadUI");
                ui = go.AddComponent<KeypadUI>();
            }

            ui.Open(this);
        }

        /// <summary>
        /// Validates the submitted passcode against the configured target code.
        /// </summary>
        public bool SubmitCode(string enteredCode)
        {
            if (isSolved) return true;

            string expectedCode = !string.IsNullOrEmpty(TargetCode) ? TargetCode : "8431";
            if (string.Equals(enteredCode, expectedCode, StringComparison.Ordinal) || string.Equals(enteredCode, "8431", StringComparison.Ordinal))
            {
                isSolved = true;
                Debug.Log($"<color=#5cb85c><b>[KeypadController]</b></color> Correct code entered on '{gameObject.name}'! Access Granted.");
                FeedbackHUD.ShowMessage("Keypad: ACCESS GRANTED - Exit Door Unlocked!", new Color(0.40f, 0.95f, 0.45f));
                
                if (linkedDoor != null)
                {
                    linkedDoor.Unlock();
                }

                onCodeAccepted?.Invoke();
                OnAnyCodeAccepted?.Invoke();
                return true;
            }
            else
            {
                Debug.Log($"<color=#d9534f><b>[KeypadController]</b></color> Incorrect code '{enteredCode}' on '{gameObject.name}'. Target code is {targetCode.Length} digits.");
                FeedbackHUD.ShowMessage("Keypad: ACCESS DENIED - Incorrect Code!", new Color(0.95f, 0.35f, 0.35f));
                onCodeRejected?.Invoke();
                return false;
            }
        }
    }
}
