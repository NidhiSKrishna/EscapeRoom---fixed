using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Automated setup tool for the guided, story-driven escape room flow:
    /// - Generates the 7 ObjectiveStep ScriptableObject assets
    /// - Creates and configures Assets/Scenes/MainMenu.unity
    /// - Updates Build Settings (0: MainMenu, 1: EscapeRoom_Main, 2: SampleScene)
    /// - Populates EscapeRoom_Main with ObjectiveManager, ProgressionAdapter, ObjectiveHUD, BannerUI, AtticBriefingUI, and FlashlightController
    /// </summary>
    [InitializeOnLoad]
    public static class GuidedEscapeRoomSetup
    {
        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string MainScenePath = "Assets/Scenes/EscapeRoom_Main.unity";
        public const string StorageScenePath = "Assets/Scenes/StorageRoom.unity";
        public const string ObjectivesFolder = "Assets/UI/Objectives";

        static GuidedEscapeRoomSetup()
        {
            // Ensure Unity Play Mode always starts from MainMenu.unity
            EditorApplication.delayCall += () =>
            {
                var mainMenuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
                if (mainMenuAsset != null && EditorSceneManager.playModeStartScene != mainMenuAsset)
                {
                    EditorSceneManager.playModeStartScene = mainMenuAsset;
                    Debug.Log($"[GuidedEscapeRoomSetup] Set Play Mode Start Scene to '{MainMenuScenePath}'.");
                }
            };
        }

        /// <summary>
        /// Number of objective steps currently playable.
        /// Steps beyond this index exist as assets for future rooms but are NOT
        /// loaded into ObjectiveManager until those rooms are implemented.
        /// Increase to 7 when Storage Room + Cellar gameplay is complete.
        /// </summary>
        public const int ActiveStepCount = 4;

        /// <summary>
        /// Returns a slice of allSteps containing only the currently active steps (first ActiveStepCount).
        /// Pass this to ObjectiveManager.SetSteps(), not the full 7-step array.
        /// </summary>
        public static ObjectiveStep[] GetActiveSteps(ObjectiveStep[] allSteps = null)
        {
            if (allSteps == null) allSteps = EnsureObjectiveAssets();
            int count = Mathf.Min(ActiveStepCount, allSteps.Length);
            var active = new ObjectiveStep[count];
            System.Array.Copy(allSteps, active, count);
            return active;
        }

        [MenuItem("Tools/Escape Room/Setup Guided Flow & Objectives", false, 30)]
        public static void RunFullSetup()
        {
            Debug.Log("<b><color=#337ab7>[GuidedEscapeRoomSetup]</color> Starting full guided flow setup...</b>");

            // 1. Create ObjectiveStep ScriptableObject assets
            ObjectiveStep[] steps = EnsureObjectiveAssets();

            // 2. Setup MainMenu scene
            EnsureMainMenuScene();

            // 3. Update Build Settings
            UpdateBuildSettings();

            // 4. Setup EscapeRoom_Main scene
            EnsureEscapeRoomMainSetup(steps);

            // 5. Configure playModeStartScene
            var mainMenuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            if (mainMenuAsset != null)
            {
                EditorSceneManager.playModeStartScene = mainMenuAsset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<b><color=#5cb85c>[GuidedEscapeRoomSetup] Guided flow and objectives setup complete!</color></b>");
        }

        public static ObjectiveStep[] EnsureObjectiveAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }
            if (!AssetDatabase.IsValidFolder(ObjectivesFolder))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Objectives");
            }

            var stepDefs = new (string filename, string title, string instruction, string flag, string room)[]
            {
                // ── Currently active Attic steps (shown in HUD as STEP 1-4 / 4) ──
                ("Step1_LockboxKey",    "Lockbox Key",    "Find the lockbox key",          "LOCKBOX_KEY_ACQUIRED", "Attic"),
                ("Step2_OpenLockbox",   "Open Lockbox",   "Unlock and open the lockbox",   "LOCKBOX_OPENED",       "Attic"),
                ("Step3_ExitNote",      "Exit Code Note", "Read the Exit Code Note",       "EXIT_NOTE_READ",       "Attic"),
                ("Step4_KeypadCode",    "Exit Keypad",    "Enter the code on the keypad",  "EXIT_CODE_ACCEPTED",   "Attic"),
                // ── Future room steps (assets preserved, NOT active yet) ──────────
                ("Step5_StorageKeycard","Storage Keycard","Search the storage room and find the keycard.",          "KEYCARD_ACQUIRED",    "Storage Room"),
                ("Step6_SecurityReader","Security Reader","Use the keycard on the security reader to reach the cellar.", "CELLAR_UNLOCKED","Cellar"),
                ("Step7_FinalExit",     "Final Exit",     "Find the key in the crate and unlock the final exit.",  "FINAL_EXIT_UNLOCKED", "Cellar")
            };

            ObjectiveStep[] result = new ObjectiveStep[stepDefs.Length];

            for (int i = 0; i < stepDefs.Length; i++)
            {
                var def = stepDefs[i];
                string assetPath = $"{ObjectivesFolder}/{def.filename}.asset";

                ObjectiveStep step = AssetDatabase.LoadAssetAtPath<ObjectiveStep>(assetPath);
                if (step == null)
                {
                    step = ScriptableObject.CreateInstance<ObjectiveStep>();
                    step.Configure(def.title, def.instruction, def.flag, def.room);
                    AssetDatabase.CreateAsset(step, assetPath);
                    Debug.Log($"[GuidedEscapeRoomSetup] Created ObjectiveStep asset: '{assetPath}'.");
                }
                else
                {
                    step.Configure(def.title, def.instruction, def.flag, def.room);
                    EditorUtility.SetDirty(step);
                }

                result[i] = step;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        public static void EnsureMainMenuScene()
        {
            if (File.Exists(MainMenuScenePath))
            {
                Debug.Log($"[GuidedEscapeRoomSetup] MainMenu scene already exists at '{MainMenuScenePath}'.");
                return;
            }

            // Create new scene
            Scene currentActive = EditorSceneManager.GetActiveScene();
            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create Main Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1f, -10f);

            // Create Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.5f;
            light.color = new Color(0.85f, 0.90f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Create MainMenu GameObject
            GameObject menuGo = new GameObject("MainMenu");
            PhoneIntroUI phoneIntro = menuGo.AddComponent<PhoneIntroUI>();
            MainMenuUI menuUI = menuGo.AddComponent<MainMenuUI>();

            // Link serialized reference
            SerializedObject so = new SerializedObject(menuUI);
            so.FindProperty("phoneIntroUI").objectReferenceValue = phoneIntro;
            so.ApplyModifiedProperties();

            // Save Scene
            EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
            Debug.Log($"<color=#5cb85c><b>[GuidedEscapeRoomSetup]</b></color> Created '{MainMenuScenePath}' with MainMenuUI and PhoneIntroUI.");

            // Restore previous active scene if valid
            if (!string.IsNullOrEmpty(currentActive.path) && File.Exists(currentActive.path))
            {
                EditorSceneManager.OpenScene(currentActive.path);
            }
        }

        public const string VaultScenePath = "Assets/Scenes/VaultCellar.unity";
        public const string SecurityControlRoomScenePath = "Assets/Scenes/SecurityControlRoom.unity";
        public const string FinalControlRoomScenePath = "Assets/Scenes/FinalControlRoom.unity";

        [MenuItem("Tools/Escape Room/Build & Setup All Scenes", false, 1)]
        public static void SetupAll()
        {
            Debug.Log("<b><color=#337ab7>[GuidedEscapeRoomSetup]</color> Building all scenes and setting up guided flow...</b>");
            StorageRoomSceneBuilder.BuildStorageRoomScene();
            VaultSceneBuilder.BuildVaultScene();
            RunFullSetup();
        }

        public static void UpdateBuildSettings()
        {
            var scenesList = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            if (File.Exists(StorageScenePath))
            {
                scenesList.Add(new EditorBuildSettingsScene(StorageScenePath, true));
            }

            if (File.Exists(SecurityControlRoomScenePath))
            {
                scenesList.Add(new EditorBuildSettingsScene(SecurityControlRoomScenePath, true));
            }

            if (File.Exists(VaultScenePath))
            {
                scenesList.Add(new EditorBuildSettingsScene(VaultScenePath, true));
            }

            if (File.Exists(FinalControlRoomScenePath))
            {
                scenesList.Add(new EditorBuildSettingsScene(FinalControlRoomScenePath, true));
            }

            if (File.Exists(SceneHousekeeper.SampleScenePath))
            {
                scenesList.Add(new EditorBuildSettingsScene(SceneHousekeeper.SampleScenePath, false));
            }

            EditorBuildSettings.scenes = scenesList.ToArray();
            Debug.Log("[GuidedEscapeRoomSetup] Build Settings updated: MainMenu, EscapeRoom_Main, StorageRoom, SecurityControlRoom, VaultCellar, FinalControlRoom.");
        }

        public static void EnsureEscapeRoomMainSetup(ObjectiveStep[] steps = null)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool wasMainOpen = (activeScene.path == MainScenePath);

            if (!wasMainOpen)
            {
                if (File.Exists(MainScenePath))
                {
                    activeScene = EditorSceneManager.OpenScene(MainScenePath);
                }
                else
                {
                    Debug.LogWarning("[GuidedEscapeRoomSetup] EscapeRoom_Main does not exist yet.");
                    return;
                }
            }

            if (steps == null || steps.Length == 0)
            {
                steps = EnsureObjectiveAssets();
            }

            bool sceneModified = false;

            // 1. Setup GameManagers GameObject
            GameObject managersGo = GameObject.Find(PuzzleBuilder.GameManagersName);
            if (managersGo == null)
            {
                managersGo = new GameObject(PuzzleBuilder.GameManagersName);
                Undo.RegisterCreatedObjectUndo(managersGo, "Create GameManagers");
                sceneModified = true;
            }

            // Ensure Core Managers
            if (managersGo.GetComponent<GameManager>() == null)
            {
                managersGo.AddComponent<GameManager>();
                sceneModified = true;
            }
            if (managersGo.GetComponent<PauseManager>() == null)
            {
                managersGo.AddComponent<PauseManager>();
                sceneModified = true;
            }
            if (managersGo.GetComponent<EscapeUI>() == null)
            {
                managersGo.AddComponent<EscapeUI>();
                sceneModified = true;
            }
            if (managersGo.GetComponent<PauseUI>() == null)
            {
                managersGo.AddComponent<PauseUI>();
                sceneModified = true;
            }
            if (managersGo.GetComponent<FeedbackHUD>() == null)
            {
                managersGo.AddComponent<FeedbackHUD>();
                sceneModified = true;
            }

            // PlaythroughGameState — centralized single-playthrough randomizer
            if (managersGo.GetComponent<PlaythroughGameState>() == null)
            {
                managersGo.AddComponent<PlaythroughGameState>();
                sceneModified = true;
            }

            // InputConfig — centralized interaction key configuration
            if (managersGo.GetComponent<InputConfig>() == null)
            {
                managersGo.AddComponent<InputConfig>();
                sceneModified = true;
            }

            // EscapeRoomAudio — central procedural audio manager
            if (managersGo.GetComponent<EscapeRoom.Audio.EscapeRoomAudio>() == null)
            {
                managersGo.AddComponent<EscapeRoom.Audio.EscapeRoomAudio>();
                sceneModified = true;
            }

            // CipherHostUI — digital guide host
            if (managersGo.GetComponent<CipherHostUI>() == null)
            {
                managersGo.AddComponent<CipherHostUI>();
                sceneModified = true;
            }

            // StageTimer — stage countdown timer
            if (managersGo.GetComponent<StageTimer>() == null)
            {
                managersGo.AddComponent<StageTimer>();
                sceneModified = true;
            }

            // TimeoutUI — time expired modal
            if (managersGo.GetComponent<TimeoutUI>() == null)
            {
                managersGo.AddComponent<TimeoutUI>();
                sceneModified = true;
            }

            // ObjectiveManager — loaded with ACTIVE steps only (first ActiveStepCount)
            var om = managersGo.GetComponent<ObjectiveManager>();
            if (om == null)
            {
                om = managersGo.AddComponent<ObjectiveManager>();
                sceneModified = true;
            }
            om.SetSteps(GetActiveSteps(steps)); // ← Only 4 steps active; future steps preserved as assets
            EditorUtility.SetDirty(om);

            // ObjectiveProgressionAdapter
            if (managersGo.GetComponent<ObjectiveProgressionAdapter>() == null)
            {
                managersGo.AddComponent<ObjectiveProgressionAdapter>();
                sceneModified = true;
            }

            // ObjectiveHUD
            if (managersGo.GetComponent<ObjectiveHUD>() == null)
            {
                managersGo.AddComponent<ObjectiveHUD>();
                sceneModified = true;
            }

            // BannerUI
            if (managersGo.GetComponent<BannerUI>() == null)
            {
                managersGo.AddComponent<BannerUI>();
                sceneModified = true;
            }

            // AtticBriefingUI
            if (managersGo.GetComponent<AtticBriefingUI>() == null)
            {
                managersGo.AddComponent<AtticBriefingUI>();
                sceneModified = true;
            }

            // 2. Setup Player FlashlightController
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                var flashlight = playerGo.GetComponent<FlashlightController>();
                if (flashlight == null)
                {
                    flashlight = playerGo.AddComponent<FlashlightController>();
                    sceneModified = true;
                }
                flashlight.EnsureSpotlightSetup();
                EditorUtility.SetDirty(playerGo);
            }

            // 3. Room Banner Triggers
            GameObject triggersParent = GameObject.Find("RoomBannerTriggers");
            if (triggersParent == null)
            {
                triggersParent = new GameObject("RoomBannerTriggers");
                sceneModified = true;
            }

            // Attic Banner Trigger
            Transform atticTrigTrans = triggersParent.transform.Find("Trigger_Attic");
            if (atticTrigTrans == null)
            {
                GameObject atticTrigGo = new GameObject("Trigger_Attic");
                atticTrigGo.transform.SetParent(triggersParent.transform, false);
                atticTrigGo.transform.position = new Vector3(0f, 1.2f, -4f);

                BoxCollider col = atticTrigGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(8f, 3f, 8f);

                var rbt = atticTrigGo.AddComponent<RoomBannerTrigger>();
                rbt.RoomTitle = "ATTIC";
                rbt.TipText = "Search carefully. Something useful is hidden here.";
                sceneModified = true;
            }

            // Storage Room Banner Trigger (Infrastructure placeholder beyond exit door)
            Transform storageTrigTrans = triggersParent.transform.Find("Trigger_StorageRoom");
            if (storageTrigTrans == null)
            {
                GameObject storageTrigGo = new GameObject("Trigger_StorageRoom");
                storageTrigGo.transform.SetParent(triggersParent.transform, false);
                storageTrigGo.transform.position = new Vector3(0f, 1.2f, 10f);

                BoxCollider col = storageTrigGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(6f, 3f, 6f);

                var rbt = storageTrigGo.AddComponent<RoomBannerTrigger>();
                rbt.RoomTitle = "STORAGE ROOM";
                rbt.TipText = "Keep your eyes open. You're not alone.";
                sceneModified = true;
            }

            // Cellar Banner Trigger (Infrastructure placeholder)
            Transform cellarTrigTrans = triggersParent.transform.Find("Trigger_Cellar");
            if (cellarTrigTrans == null)
            {
                GameObject cellarTrigGo = new GameObject("Trigger_Cellar");
                cellarTrigGo.transform.SetParent(triggersParent.transform, false);
                cellarTrigGo.transform.position = new Vector3(0f, 1.2f, 20f);

                BoxCollider col = cellarTrigGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(6f, 3f, 6f);

                var rbt = cellarTrigGo.AddComponent<RoomBannerTrigger>();
                rbt.RoomTitle = "THE CELLAR";
                rbt.TipText = "Find the key. This is your final way out.";
                sceneModified = true;
            }

            // 4. Ensure BlurLight_Anomaly (guide ball) GameObject is removed if present
            GameObject blurLightGo = GameObject.Find("BlurLight_Anomaly");
            if (blurLightGo != null)
            {
                Undo.DestroyObjectImmediate(blurLightGo);
                sceneModified = true;
            }

            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[GuidedEscapeRoomSetup] EscapeRoom_Main updated with all guided flow systems and saved.");
            }
        }
    }
}
