using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Manages the ordered list of ObjectiveStep assets, tracks progression,
    /// verifies completion flags, and raises events on step changes.
    /// Resets progression on scene load / restart.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveManager : MonoBehaviour
    {
        private static ObjectiveManager instance;
        public static ObjectiveManager Instance => instance;

        [Header("Objective Steps (Ordered)")]
        [Tooltip("Ordered sequence of objective steps defining game progression.")]
        [SerializeField] private ObjectiveStep[] steps = new ObjectiveStep[0];

        [Header("Runtime State")]
        [SerializeField] private int currentStepIndex = 0;
        [SerializeField] private bool isSystemActive = true;
        [SerializeField] private bool isAllCompleted = false;
        [SerializeField] private bool isFinalEscaped = false;
        [SerializeField] private string completionInstruction = "All security systems cleared! Exit unlocked.";
        [SerializeField] private string finalEscapedMessage = "STAGE 3 COMPLETE — FACILITY ESCAPED!";

        public event Action<int, ObjectiveStep> OnStepChanged;
        public event Action<ObjectiveStep> OnObjectiveCompleted;
        public event Action OnAllObjectivesCompleted;
        public event Action OnFinalEscaped;

        public ObjectiveStep[] Steps => steps;
        public int CurrentStepIndex => currentStepIndex;
        public int TotalSteps => steps != null ? steps.Length : 0;
        public bool IsSystemActive
        {
            get => isSystemActive;
            set => isSystemActive = value;
        }

        public bool IsAllCompleted => isAllCompleted || (steps != null && steps.Length > 0 && currentStepIndex >= steps.Length);
        public bool IsFinalEscaped => isFinalEscaped;

        public string CompletionInstruction
        {
            get => completionInstruction;
            set => completionInstruction = value;
        }

        public string FinalEscapedMessage
        {
            get => finalEscapedMessage;
            set => finalEscapedMessage = value;
        }

        public ObjectiveStep CurrentStep
        {
            get
            {
                if (steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Length)
                {
                    return steps[currentStepIndex];
                }
                return null;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            // Initial notification for HUD
            NotifyStepChanged();
        }

        /// <summary>
        /// Attempts to complete the current objective step using the provided completion flag.
        /// Only advances if the flag matches the current step's completionFlag.
        /// When the final step is completed, it stays at the final numbered step (e.g. 4/4)
        /// and marks completion rather than rolling over to an invalid step number.
        /// </summary>
        public bool CompleteFlag(string flag)
        {
            if (!isSystemActive) return false;
            if (string.IsNullOrEmpty(flag)) return false;
            if (IsAllCompleted) return false;

            var current = CurrentStep;
            if (current == null) return false;

            if (string.Equals(current.CompletionFlag, flag, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"<color=#5cb85c><b>[ObjectiveManager]</b></color> Objective Completed: Step {currentStepIndex + 1}/{TotalSteps} ('{current.Instruction}') [Flag: {flag}]");

                OnObjectiveCompleted?.Invoke(current);

                // If completing the final step in the sequence:
                if (currentStepIndex + 1 >= TotalSteps)
                {
                    isAllCompleted = true;
                    // Do not increment past TotalSteps - 1 so the counter remains at 4/4
                    Debug.Log("<b><color=#5cb85c>[ObjectiveManager] All Objectives Successfully Completed!</color></b>");
                    OnAllObjectivesCompleted?.Invoke();
                }
                else
                {
                    currentStepIndex++;
                }

                NotifyStepChanged();
                return true;
            }

            return false;
        }

        public void SetFinalEscaped(string message = null)
        {
            isFinalEscaped = true;
            if (!string.IsNullOrEmpty(message))
            {
                finalEscapedMessage = message;
            }
            OnFinalEscaped?.Invoke();
            NotifyStepChanged();
        }

        public void SetSteps(ObjectiveStep[] newSteps)
        {
            steps = newSteps;
            currentStepIndex = 0;
            isAllCompleted = false;
            isFinalEscaped = false;
            NotifyStepChanged();
        }

        public void ResetObjectives()
        {
            currentStepIndex = 0;
            isAllCompleted = false;
            isFinalEscaped = false;
            isSystemActive = true;
            Debug.Log("[ObjectiveManager] Objectives reset to Step 1.");
            NotifyStepChanged();
        }

        public void ActivateSystem()
        {
            isSystemActive = true;
            NotifyStepChanged();
        }

        public void NotifyStepChanged()
        {
            OnStepChanged?.Invoke(currentStepIndex, CurrentStep);
        }
    }
}
