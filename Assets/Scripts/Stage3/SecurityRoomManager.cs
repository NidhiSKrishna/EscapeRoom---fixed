using System;
using System.Collections;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.UI;
using EscapeRoom.Stage3.Puzzles;
using EscapeRoom.Stage3.UI;
using EscapeRoom.Puzzle;

namespace EscapeRoom.Stage3
{
    /// <summary>
    /// Master orchestrator for Stage 3 Security Control Room.
    ///
    /// This manager:
    /// - Creates Stage 3 objectives
    /// - Connects the existing puzzle completion events
    /// - Controls the order of Stage 3
    /// - Starts the lockdown after the color puzzle
    /// - Activates the path puzzle after lockdown
    /// - Updates Cipher hints
    /// - Handles final symbol completion
    /// - Controls the Stage 3 timer
    ///
    /// Puzzle failure penalties are handled by the individual puzzle
    /// scripts, so this manager does not require separate failure events.
    /// </summary>
    [DisallowMultipleComponent]
    public class SecurityRoomManager : MonoBehaviour
    {
        private static SecurityRoomManager instance;

        public static SecurityRoomManager Instance
        {
            get { return instance; }
        }

        // ============================================================
        // PUZZLE REFERENCES
        // ============================================================

        [Header("Puzzle References")]

        [SerializeField]
        private ColorMemoryPuzzle colorMemoryPuzzle;

        [SerializeField]
        private SecurityLockdownEvent lockdownEvent;

        [SerializeField]
        private MemoryPathPuzzle pathPuzzle;

        [SerializeField]
        private SymbolMappingPuzzle symbolPuzzle;

        [SerializeField]
        private Stage3ExitTrigger exitTrigger;

        [SerializeField]
        [Tooltip("The physical exit door that opens automatically when all 4 puzzles are completed.")]
        private DoorController exitDoor;

        // ============================================================
        // STAGE SETTINGS
        // ============================================================

        [Header("Stage Settings")]

        [SerializeField]
        private float initialTimeLimit = 480f;

        // ============================================================
        // OBJECTIVE STEPS
        // ============================================================

        [Header("Runtime Objectives")]

        private ObjectiveStep stepColorMemory;
        private ObjectiveStep stepLockdown;
        private ObjectiveStep stepPath;
        private ObjectiveStep stepSymbol;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            UnwirePuzzleEvents();

            if (Stage3Timer.Instance != null)
            {
                Stage3Timer.Instance.OnTimerExpired -=
                    OnStageTimerExpired;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            StartCoroutine(InitializeStageRoutine());
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================

        private IEnumerator InitializeStageRoutine()
        {
            // Wait until the scene has finished initializing.
            yield return new WaitForEndOfFrame();

            // Create objectives.
            SetupObjectiveSteps();

            // Find and connect puzzles.
            WireUpPuzzleEvents();

            // --------------------------------------------------------
            // START STAGE TIMER
            // --------------------------------------------------------

            if (Stage3Timer.Instance != null)
            {
                Stage3Timer.Instance.ResetTimer(initialTimeLimit);

                Stage3Timer.Instance.OnTimerExpired +=
                    OnStageTimerExpired;

                Stage3Timer.Instance.StartTimer();
            }

            // --------------------------------------------------------
            // INTRODUCTORY BANNER
            // --------------------------------------------------------

            if (BannerUI.Instance != null)
            {
                BannerUI.Instance.ShowBanner(
                    "SECURITY CONTROL ROOM",
                    "Override facility lockdown and clear the security perimeter."
                );
            }

            // --------------------------------------------------------
            // INITIAL CIPHER FOCUS
            // --------------------------------------------------------

            if (Stage3CipherHelper.Instance != null)
            {
                Stage3CipherHelper.Instance.SetCurrentPuzzle("COLOR");
            }
        }

        // ============================================================
        // OBJECTIVE SETUP
        // ============================================================

        private void SetupObjectiveSteps()
        {
            stepColorMemory =
                ScriptableObject.CreateInstance<ObjectiveStep>();

            stepColorMemory.Configure(
                "Color Terminal",
                "Solve the color memory sequence at the primary panel.",
                "STAGE3_COLOR_MEMORY_SOLVED",
                "Security Control"
            );

            stepLockdown =
                ScriptableObject.CreateInstance<ObjectiveStep>();

            stepLockdown.Configure(
                "Emergency Overload",
                "Deactivate the security lockdown emergency switch before overload!",
                "STAGE3_LOCKDOWN_RESOLVED",
                "Security Control"
            );

            stepPath =
                ScriptableObject.CreateInstance<ObjectiveStep>();

            stepPath.Configure(
                "Security Grid",
                "Memorize and navigate the illuminated floor grid path.",
                "STAGE3_PATH_SOLVED",
                "Security Grid"
            );

            stepSymbol =
                ScriptableObject.CreateInstance<ObjectiveStep>();

            stepSymbol.Configure(
                "Symbol Terminal",
                "Deduce symbol values from monitors and input master code.",
                "STAGE3_SYMBOL_SOLVED",
                "Control Center"
            );

            ObjectiveStep[] steps =
            {
                stepColorMemory,
                stepLockdown,
                stepPath,
                stepSymbol
            };

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetSteps(steps);
                ObjectiveManager.Instance.CompletionInstruction =
                    "All security systems cleared! Exit unlocked.";
                ObjectiveManager.Instance.FinalEscapedMessage =
                    "STAGE 3 COMPLETE — FACILITY ESCAPED!";
            }
        }

        // ============================================================
        // FIND + CONNECT PUZZLES
        // ============================================================

        private void WireUpPuzzleEvents()
        {
            // --------------------------------------------------------
            // Automatically find missing references.
            // --------------------------------------------------------

            if (colorMemoryPuzzle == null)
            {
                colorMemoryPuzzle =
                    FindFirstObjectByType<ColorMemoryPuzzle>();
            }

            if (lockdownEvent == null)
            {
                lockdownEvent =
                    FindFirstObjectByType<SecurityLockdownEvent>();
            }

            if (pathPuzzle == null)
            {
                pathPuzzle =
                    FindFirstObjectByType<MemoryPathPuzzle>();
            }

            if (symbolPuzzle == null)
            {
                symbolPuzzle =
                    FindFirstObjectByType<SymbolMappingPuzzle>();
            }

            if (exitTrigger == null)
            {
                exitTrigger =
                    FindFirstObjectByType<Stage3ExitTrigger>();
            }

            if (exitDoor == null)
            {
                exitDoor =
                    FindFirstObjectByType<DoorController>();
            }

            // --------------------------------------------------------
            // COLOR MEMORY
            // --------------------------------------------------------

            if (colorMemoryPuzzle != null)
            {
                colorMemoryPuzzle.OnPuzzleCompleted +=
                    HandleColorPuzzleCompleted;
            }
            else
            {
                Debug.LogWarning(
                    "[SecurityRoomManager] " +
                    "ColorMemoryPuzzle was not found."
                );
            }

            // --------------------------------------------------------
            // LOCKDOWN
            // --------------------------------------------------------

            if (lockdownEvent != null)
            {
                lockdownEvent.OnLockdownDeactivated -=
                    HandleLockdownResolved;

                lockdownEvent.OnLockdownDeactivated +=
                    HandleLockdownResolved;
            }

            // --------------------------------------------------------
            // PATH PUZZLE
            // --------------------------------------------------------

            if (pathPuzzle != null)
            {
                pathPuzzle.OnPuzzleCompleted +=
                    HandlePathPuzzleCompleted;
            }
            else
            {
                Debug.LogWarning(
                    "[SecurityRoomManager] " +
                    "MemoryPathPuzzle was not found."
                );
            }

            // --------------------------------------------------------
            // SYMBOL PUZZLE
            // --------------------------------------------------------

            if (symbolPuzzle != null)
            {
                symbolPuzzle.OnPuzzleCompleted +=
                    HandleSymbolPuzzleCompleted;
            }
            else
            {
                Debug.LogWarning(
                    "[SecurityRoomManager] " +
                    "SymbolMappingPuzzle was not found."
                );
            }
        }

        // ============================================================
        // DISCONNECT EVENTS
        // ============================================================

        private void UnwirePuzzleEvents()
        {
            if (colorMemoryPuzzle != null)
            {
                colorMemoryPuzzle.OnPuzzleCompleted -=
                    HandleColorPuzzleCompleted;
            }

            if (lockdownEvent != null)
            {
                lockdownEvent.OnLockdownDeactivated -=
                    HandleLockdownResolved;
            }

            if (pathPuzzle != null)
            {
                pathPuzzle.OnPuzzleCompleted -=
                    HandlePathPuzzleCompleted;
            }

            if (symbolPuzzle != null)
            {
                symbolPuzzle.OnPuzzleCompleted -=
                    HandleSymbolPuzzleCompleted;
            }
        }

        // ============================================================
        // COLOR PUZZLE COMPLETED
        // ============================================================

        private void HandleColorPuzzleCompleted()
        {
            Debug.Log(
                "[SecurityRoomManager] " +
                "Color memory puzzle completed."
            );

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag(
                    "STAGE3_COLOR_MEMORY_SOLVED"
                );
            }

            FeedbackHUD.ShowMessage(
                "COLOR SEQUENCE ACCEPTED - ALERT: LOCKDOWN ENGAGED!",
                new Color(
                    0.95f,
                    0.45f,
                    0.25f
                )
            );

            // --------------------------------------------------------
            // START LOCKDOWN
            // --------------------------------------------------------

            if (lockdownEvent != null)
            {
                lockdownEvent.TriggerLockdown();
            }
            else
            {
                Debug.LogWarning(
                    "[SecurityRoomManager] " +
                    "SecurityLockdownEvent was not found."
                );
            }
        }

        // ============================================================
        // LOCKDOWN RESOLVED
        // ============================================================

        public void HandleLockdownResolved()
        {
            Debug.Log(
                "[SecurityRoomManager] " +
                "Security lockdown resolved."
            );

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag(
                    "STAGE3_LOCKDOWN_RESOLVED"
                );
            }

            FeedbackHUD.ShowMessage(
                "EMERGENCY OVERRIDE SUCCESSFUL - Floor Grid Initializing...",
                new Color(
                    0.35f,
                    0.95f,
                    0.35f
                )
            );

            if (Stage3CipherHelper.Instance != null)
            {
                Stage3CipherHelper.Instance.SetCurrentPuzzle(
                    "PATH"
                );
            }

            // Activate path puzzle and start sequence.
            if (pathPuzzle != null)
            {
                pathPuzzle.gameObject.SetActive(true);
                pathPuzzle.StartPuzzle();
            }
        }

        // ============================================================
        // PATH PUZZLE COMPLETED
        // ============================================================

        private void HandlePathPuzzleCompleted()
        {
            Debug.Log(
                "[SecurityRoomManager] " +
                "Memory path puzzle completed."
            );

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag(
                    "STAGE3_PATH_SOLVED"
                );
            }

            FeedbackHUD.ShowMessage(
                "SECURITY GRID PASSED - Access Symbol Terminal to release door locks.",
                new Color(
                    0.35f,
                    0.95f,
                    0.95f
                )
            );

            if (Stage3CipherHelper.Instance != null)
            {
                Stage3CipherHelper.Instance.SetCurrentPuzzle(
                    "SYMBOL"
                );
            }
        }

        // ============================================================
        // SYMBOL PUZZLE COMPLETED
        // ============================================================

        private void HandleSymbolPuzzleCompleted()
        {
            Debug.Log(
                "[SecurityRoomManager] " +
                "Symbol mapping puzzle completed. All 4 Stage 3 puzzles cleared."
            );

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag(
                    "STAGE3_SYMBOL_SOLVED"
                );
            }

            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayCompletionFanfare();
            }

            if (BannerUI.Instance != null)
            {
                BannerUI.Instance.ShowBanner(
                    "ALL SECURITY SYSTEMS CLEARED",
                    "All security systems cleared! Exit unlocked."
                );
            }

            FeedbackHUD.ShowMessage(
                "All security systems cleared! Exit unlocked.",
                new Color(
                    0.40f,
                    0.95f,
                    0.45f
                )
            );

            if (Stage3Timer.Instance != null)
            {
                Stage3Timer.Instance.StopTimer();
            }

            // Automatically and immediately open the physical exit door
            if (exitDoor != null)
            {
                exitDoor.UnlockAndOpen();
                Debug.Log(
                    "<color=#5cb85c><b>[SecurityRoomManager]</b></color> " +
                    "Exit door automatically unlocked and opened."
                );
            }
            else
            {
                Debug.LogWarning(
                    "[SecurityRoomManager] " +
                    "exitDoor reference is missing!"
                );
            }

            // Unlock the exit trigger so walking through completes the stage
            if (exitTrigger != null)
            {
                exitTrigger.UnlockExit();
            }
        }

        // ============================================================
        // TIMER EXPIRED
        // ============================================================

        private void OnStageTimerExpired()
        {
            FeedbackHUD.ShowMessage(
                "SECURITY LOCKOUT ENGAGED - Time limit exceeded!",
                new Color(
                    0.95f,
                    0.25f,
                    0.25f
                )
            );

            Debug.LogWarning(
                "[SecurityRoomManager] " +
                "Stage 3 timer expired."
            );
        }
    }
}