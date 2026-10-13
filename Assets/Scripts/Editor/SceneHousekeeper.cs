using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeRoom.Interaction;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Handles scene housekeeping, scene transitions, and build settings registration for EscapeRoom_Main.
    /// Preserves existing generated ProBuilder environment and sets EscapeRoom_Main as primary gameplay scene.
    /// Milestone: complete escape flow and gameplay state
    /// </summary>
    [InitializeOnLoad]
    public static class SceneHousekeeper
    {
        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string MainScenePath = "Assets/Scenes/EscapeRoom_Main.unity";
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        static SceneHousekeeper()
        {
            EditorApplication.delayCall += EnsureMainSceneSetup;
        }

        [MenuItem("Tools/Escape Room/Housekeeping - Setup EscapeRoom_Main", false, 5)]
        public static void SetupEscapeRoomMainSceneMenu()
        {
            PerformSceneHousekeeping(forceRecreate: true);
        }

        [MenuItem("Tools/Escape Room/Open EscapeRoom_Main Scene", false, 6)]
        public static void OpenEscapeRoomMainSceneMenu()
        {
            if (File.Exists(MainScenePath))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(MainScenePath);
                    Debug.Log($"[SceneHousekeeper] Opened '{MainScenePath}'.");
                }
            }
            else
            {
                PerformSceneHousekeeping(forceRecreate: true);
            }
        }

        public static void EnsureMainSceneSetup()
        {
            UpdateBuildSettings();

            Scene activeScene = EditorSceneManager.GetActiveScene();

            // Case 1: Active scene is SampleScene with the generated Environment in memory
            if (activeScene.path == SampleScenePath || activeScene.name == "SampleScene")
            {
                if (GameObject.Find("Environment") != null)
                {
                    // Save active scene so SampleScene has environment persisted
                    EditorSceneManager.SaveScene(activeScene);
                    Debug.Log("[SceneHousekeeper] Saved active SampleScene with existing ProBuilder environment.");

                    // Save as EscapeRoom_Main
                    EditorSceneManager.SaveScene(activeScene, MainScenePath, saveAsCopy: false);
                    Debug.Log($"<color=#5cb85c><b>[SceneHousekeeper]</b></color> Successfully created and switched to '{MainScenePath}' with preserved environment.");
                }
                else
                {
                    // Environment not yet generated in SampleScene; build it and save as EscapeRoom_Main
                    EscapeRoomBuilder.BuildRoom(RoomConfiguration.CreatePrototypeDefault());
                    EditorSceneManager.SaveScene(activeScene, MainScenePath, saveAsCopy: false);
                    Debug.Log($"<color=#5cb85c><b>[SceneHousekeeper]</b></color> Generated prototype environment and saved to '{MainScenePath}'.");
                }

                AssetDatabase.Refresh();
                UpdateBuildSettings();
                return;
            }

            // Case 2: Active scene is EscapeRoom_Main
            if (activeScene.path == MainScenePath || activeScene.name == "EscapeRoom_Main")
            {
                bool sceneModified = false;
                if (GameObject.Find("Environment") == null)
                {
                    Debug.Log("[SceneHousekeeper] Environment missing in EscapeRoom_Main. Generating prototype environment...");
                    EscapeRoomBuilder.BuildRoom(RoomConfiguration.CreatePrototypeDefault());
                    sceneModified = true;
                }

                if (GameObject.Find("Player") == null)
                {
                    Debug.Log("[SceneHousekeeper] Player missing in EscapeRoom_Main. Building prototype player...");
                    PlayerBuilder.BuildPrototypePlayerMenu();
                    sceneModified = true;
                }

                // Remove deprecated TestInteractable from scene
                var testInteractable = GameObject.Find("TestInteractable");
                if (testInteractable != null)
                {
                    Object.DestroyImmediate(testInteractable);
                    sceneModified = true;
                    Debug.Log("[SceneHousekeeper] Removed deprecated TestInteractable from scene.");
                }

                var playerGo = GameObject.Find("Player");
                if (playerGo != null && playerGo.GetComponent<InventorySystem>() == null)
                {
                    playerGo.AddComponent<InventorySystem>();
                    sceneModified = true;
                    Debug.Log("[SceneHousekeeper] Added InventorySystem to Player.");
                }

                // Rebuild puzzle geometry to ensure geometry-aware surface alignment and clean single instances
                Debug.Log("[SceneHousekeeper] Rebuilding prototype puzzle with geometry-aware alignment...");
                PuzzleBuilder.BuildPuzzle(forceRebuild: true);
                sceneModified = true;

                if (sceneModified)
                {
                    EditorSceneManager.SaveScene(activeScene);
                }

                // Run verification to log validation status
                EscapeRoomVerification.RunAllChecks();
            }
        }

        /// <summary>
        /// Explicitly copies or migrates current environment into EscapeRoom_Main.unity and updates Build Settings.
        /// </summary>
        public static void PerformSceneHousekeeping(bool forceRecreate)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();

            // Persist current scene if dirty and has environment
            if (GameObject.Find("Environment") != null && activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[SceneHousekeeper] Saved active scene '{activeScene.path}' to persist environment.");
            }

            // Ensure Environment exists
            if (GameObject.Find("Environment") == null)
            {
                Debug.Log("[SceneHousekeeper] Generating prototype environment...");
                EscapeRoomBuilder.BuildRoom(RoomConfiguration.CreatePrototypeDefault());
            }

            // Save active scene as EscapeRoom_Main
            bool saved = EditorSceneManager.SaveScene(activeScene, MainScenePath, saveAsCopy: false);
            if (saved)
            {
                Debug.Log($"<color=#5cb85c><b>[SceneHousekeeper]</b></color> Successfully saved '{MainScenePath}' as primary gameplay scene.");
                AssetDatabase.Refresh();
                UpdateBuildSettings();
            }
            else
            {
                Debug.LogError($"[SceneHousekeeper] Failed to save scene as '{MainScenePath}'.");
            }
        }

        /// <summary>
        /// Registers MainMenu at index 0 (enabled), EscapeRoom_Main at index 1 (enabled), and SampleScene at index 2 (disabled).
        /// </summary>
        public static void UpdateBuildSettings()
        {
            var scenesList = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/StorageRoom.unity", true)
            };

            if (System.IO.File.Exists("Assets/Scenes/SecurityControlRoom.unity"))
                scenesList.Add(new EditorBuildSettingsScene("Assets/Scenes/SecurityControlRoom.unity", true));

            if (System.IO.File.Exists("Assets/Scenes/VaultCellar.unity"))
                scenesList.Add(new EditorBuildSettingsScene("Assets/Scenes/VaultCellar.unity", true));

            if (System.IO.File.Exists("Assets/Scenes/FinalControlRoom.unity"))
                scenesList.Add(new EditorBuildSettingsScene("Assets/Scenes/FinalControlRoom.unity", true));

            if (System.IO.File.Exists(SampleScenePath))
                scenesList.Add(new EditorBuildSettingsScene(SampleScenePath, false));

            EditorBuildSettings.scenes = scenesList.ToArray();
        }
    }
}
