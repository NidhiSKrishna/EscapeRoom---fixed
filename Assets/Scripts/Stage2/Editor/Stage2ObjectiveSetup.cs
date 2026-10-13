using System.IO;
using UnityEngine;
using UnityEditor;
using EscapeRoom.Core;

namespace EscapeRoom.Stage2.Editor
{
    /// <summary>
    /// Generates and manages the 7 Stage 2 (Storage Room) ObjectiveStep ScriptableObject assets.
    /// Safe and self-contained; does not alter Stage 1 assets.
    /// </summary>
    public static class Stage2ObjectiveSetup
    {
        public const string Stage2ObjectivesFolder = "Assets/UI/Objectives/Stage2";

        [MenuItem("Tools/Escape Room/Stage 2/Create Stage 2 Objective Assets", false, 110)]
        public static ObjectiveStep[] EnsureStage2ObjectiveAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }
            if (!AssetDatabase.IsValidFolder("Assets/UI/Objectives"))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Objectives");
            }
            if (!AssetDatabase.IsValidFolder(Stage2ObjectivesFolder))
            {
                AssetDatabase.CreateFolder("Assets/UI/Objectives", "Stage2");
            }

            var stepDefs = new (string filename, string title, string instruction, string flag, string room)[]
            {
                ("Step1_ReadMemo", "Facility Memo", "Read the Facility Shift Log on the clipboard near the entrance.", "MEMO_READ", "Storage Room"),
                ("Step2_SearchChip", "Search Sector", "Search the designated storage location to find the security terminal key chip.", "ITEM_FOUND", "Storage Room"),
                ("Step3_SolvePattern", "Pattern Terminal", "Access the Pattern Terminal on the work table and solve the sequence to reveal the safe code.", "PATTERN_SOLVED", "Storage Room"),
                ("Step4_UnlockSafe", "Storage Vault", "Enter the decrypted 3-digit combination into the storage vault keypad.", "SAFE_UNLOCKED", "Storage Room"),
                ("Step5_KeycardAcquired", "Master Keycard", "Retrieve the Master Keycard from inside the opened storage vault.", "KEYCARD_ACQUIRED", "Storage Room"),
                ("Step6_UnlockCellar", "Security Reader", "Swipe the Master Keycard at the exit door security terminal before lockdown!", "CELLAR_UNLOCKED", "Storage Room"),
                ("Step7_Stage2Exit", "Cellar Passage", "Proceed through the unlocked doorway into the subterranean cellar corridor.", "STAGE2_COMPLETE", "Storage Room")
            };

            ObjectiveStep[] result = new ObjectiveStep[stepDefs.Length];

            for (int i = 0; i < stepDefs.Length; i++)
            {
                var def = stepDefs[i];
                string assetPath = $"{Stage2ObjectivesFolder}/{def.filename}.asset";

                ObjectiveStep step = AssetDatabase.LoadAssetAtPath<ObjectiveStep>(assetPath);
                if (step == null)
                {
                    step = ScriptableObject.CreateInstance<ObjectiveStep>();
                    step.Configure(def.title, def.instruction, def.flag, def.room);
                    AssetDatabase.CreateAsset(step, assetPath);
                    Debug.Log($"[Stage2ObjectiveSetup] Created ObjectiveStep asset: '{assetPath}'.");
                }
                else
                {
                    step.Configure(def.title, def.instruction, def.flag, def.room);
                    EditorUtility.SetDirty(step);
                }

                result[i] = step;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }
    }
}
