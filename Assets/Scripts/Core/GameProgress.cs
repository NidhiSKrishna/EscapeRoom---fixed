using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Manages persistent player progression across all Escape Room stages using PlayerPrefs.
    /// Tracks the highest unlocked stage, allows resuming gameplay directly from the Main Menu,
    /// and supports full progression reset for new playthroughs.
    /// </summary>
    public static class GameProgress
    {
        private const string StageKey = "EscapeRoom_HighestUnlockedStage";

        /// <summary>
        /// Gets the highest unlocked stage (1-indexed: 1 = Attic, 2 = Storage Room, 3 = Cellar/Future).
        /// Default is 1.
        /// </summary>
        public static int GetHighestUnlockedStage()
        {
            return PlayerPrefs.GetInt(StageKey, 1);
        }

        /// <summary>
        /// Unlocks a stage if it is higher than the current saved progress and persists to PlayerPrefs.
        /// </summary>
        public static void SetStageUnlocked(int stage)
        {
            int current = GetHighestUnlockedStage();
            if (stage > current)
            {
                PlayerPrefs.SetInt(StageKey, stage);
                PlayerPrefs.Save();
                Debug.Log($"<color=#5cb85c><b>[GameProgress]</b></color> Progress saved! Unlocked Stage {stage} ({GetStageDisplayName(stage)}).");
            }
        }

        /// <summary>
        /// Resets all saved progress back to Stage 1.
        /// </summary>
        public static void ResetProgress()
        {
            PlayerPrefs.SetInt(StageKey, 1);
            PlayerPrefs.Save();
            Debug.Log("<color=#f0ad4e><b>[GameProgress]</b></color> Game progress reset to Stage 1.");
        }

        /// <summary>
        /// Returns the scene file name corresponding to a given stage index.
        /// </summary>
        public static string GetStageSceneName(int stage)
        {
            switch (stage)
            {
                case 1:
                    return "EscapeRoom_Main";
                case 2:
                    return "StorageRoom";
                case 3:
                    return "SecurityControlRoom";
                case 4:
                    return "FinalControlRoom";
                default:
                    return "StorageRoom";
            }
        }

        /// <summary>
        /// Returns a human-friendly display title for the stage.
        /// </summary>
        public static string GetStageDisplayName(int stage)
        {
            switch (stage)
            {
                case 1:
                    return "Stage 1: The Attic";
                case 2:
                    return "Stage 2: Storage Room";
                case 3:
                    return "Stage 3: Security Control Room";
                case 4:
                    return "Stage 4: Final Control Room";
                default:
                    return $"Stage {stage}";
            }
        }
    }
}
