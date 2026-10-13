using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor.ProBuilder;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.Interaction;
using EscapeRoom.Puzzle;
using EscapeRoom.UI;
using EscapeRoom.Editor;

namespace EscapeRoom.Stage2.Editor
{
    /// <summary>
    /// Editor tool responsible for generating the self-contained Stage 2 (Storage Room) scene.
    /// Builds high-quality ProBuilder architecture, industrial shelving, crates, desk,
    /// central work table, locked vault safe, security terminal, keycard, alarm sirens,
    /// security reader, exit door, and managers.
    /// </summary>
    public static class StorageRoomBuilder
    {
        public const string ScenePath = "Assets/Scenes/StorageRoom.unity";

        private const string RootGameObjectName = "Environment";
        private const string ArchitectureContainerName = "Architecture";
        private const string FurnitureContainerName = "Furniture";
        private const string PropsContainerName = "Props";
        private const string InteractablesContainerName = "Interactables";
        private const string LightingContainerName = "Lighting";
        private const string ManagersContainerName = "Stage2_Managers";

        [MenuItem("Tools/Escape Room/Stage 2/Open Storage Room Scene", false, 90)]
        public static void OpenStorageRoomMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            // Clear Play Mode Start Scene so clicking Play will stay in StorageRoom
            EditorSceneManager.playModeStartScene = null;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EnsureSceneInBuildSettings(ScenePath);
                Debug.Log($"<color=#5cb85c>[StorageRoomBuilder]</color> Successfully opened '{ScenePath}' and cleared Play Mode Start Scene.");
            }
        }

        [MenuItem("Tools/Escape Room/Stage 2/Disable Play Mode Start Scene (Play Storage Room Directly)", false, 95)]
        public static void DisablePlayModeStartSceneMenu()
        {
            EditorSceneManager.playModeStartScene = null;
            EnsureSceneInBuildSettings(ScenePath);
            Debug.Log("<color=#5cb85c><b>[Stage 2]</b> Play Mode Start Scene cleared!</color> Unity will now run whichever scene is open in the Hierarchy (StorageRoom).");
        }

        [MenuItem("Tools/Escape Room/Progression/Open Main Menu Scene", false, 110)]
        public static void OpenMainMenuMenu()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorSceneManager.playModeStartScene = null;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
                Debug.Log("<color=#5cb85c>[Progress Debug]</color> MainMenu scene opened.");
            }
        }

        [MenuItem("Tools/Escape Room/Progression/Set Saved Progress to Stage 2 (Storage Room)", false, 120)]
        public static void SetProgressStage2Menu()
        {
            EscapeRoom.Core.GameProgress.SetStageUnlocked(2);
            Debug.Log("<color=#5cb85c><b>[Progress Debug]</b> Saved progress set to Stage 2!</color> Open MainMenu and click Play to see 'CONTINUE [STAGE 2]'.");
        }

        [MenuItem("Tools/Escape Room/Progression/Reset Saved Progress to Stage 1 (New Game)", false, 130)]
        public static void ResetProgressMenu()
        {
            EscapeRoom.Core.GameProgress.ResetProgress();
            Debug.Log("<color=#f0ad4e><b>[Progress Debug]</b> Saved progress reset to Stage 1.</color>");
        }

        [MenuItem("Tools/Escape Room/Stage 2/Build Storage Room Scene", false, 100)]
        public static void BuildStorageRoomMenu()
        {
            BuildStorageRoomScene();
        }

        public static void BuildStorageRoomScene()
        {
            Debug.Log("<b><color=#00bcd4>[StorageRoomBuilder]</color> Starting Storage Room scene generation...</b>");

            // 1. Ensure scene file exists and open it
            Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[StorageRoomBuilder] Created new scene at '{ScenePath}'.");
            }

            // 2. Clear any existing Environment root
            GameObject existingEnv = GameObject.Find(RootGameObjectName);
            if (existingEnv != null)
            {
                Undo.DestroyObjectImmediate(existingEnv);
            }
            GameObject existingManagers = GameObject.Find(ManagersContainerName);
            if (existingManagers != null)
            {
                Undo.DestroyObjectImmediate(existingManagers);
            }

            // 3. Create root containers
            GameObject root = new GameObject(RootGameObjectName);
            root.transform.position = Vector3.zero;

            Transform archParent = CreateSubContainer(ArchitectureContainerName, root.transform).transform;
            Transform furnParent = CreateSubContainer(FurnitureContainerName, root.transform).transform;
            Transform propsParent = CreateSubContainer(PropsContainerName, root.transform).transform;
            Transform interactParent = CreateSubContainer(InteractablesContainerName, root.transform).transform;
            Transform lightParent = CreateSubContainer(LightingContainerName, root.transform).transform;

            // Load Materials
            Material floorMat = LoadOrCreateMaterial("M_Proto_Floor", new Color(0.35f, 0.38f, 0.40f), 0.25f);
            Material wallMat = LoadOrCreateMaterial("M_Proto_Wall", new Color(0.72f, 0.73f, 0.70f), 0.15f);
            Material ceilingMat = LoadOrCreateMaterial("M_Proto_Ceiling", new Color(0.20f, 0.22f, 0.24f), 0.10f);
            Material frameMat = LoadOrCreateMaterial("M_Proto_DoorFrame", new Color(0.18f, 0.19f, 0.20f), 0.35f, 0.20f);
            Material metalMat = LoadOrCreateMaterial("M_Proto_Metal", new Color(0.30f, 0.32f, 0.35f), 0.50f, 0.65f);
            Material darkWoodMat = LoadOrCreateMaterial("M_Proto_DarkWood", new Color(0.25f, 0.16f, 0.10f), 0.25f);
            Material woodMat = LoadOrCreateMaterial("M_Proto_Wood", new Color(0.48f, 0.32f, 0.18f), 0.30f);
            Material crateMat = LoadOrCreateMaterial("M_Proto_Crate", new Color(0.60f, 0.46f, 0.30f), 0.20f);
            Material lockboxMat = LoadOrCreateMaterial("M_Proto_Lockbox", new Color(0.22f, 0.24f, 0.26f), 0.60f, 0.70f);
            Material keypadMat = LoadOrCreateMaterial("M_Proto_Keypad", new Color(0.12f, 0.13f, 0.14f), 0.40f);
            Material doorMat = LoadOrCreateMaterial("M_Proto_Door", new Color(0.28f, 0.30f, 0.32f), 0.40f, 0.30f);
            Material clueMat = LoadOrCreateMaterial("M_Proto_Clue", new Color(0.92f, 0.88f, 0.78f), 0.10f);

            // 4. Build Architecture (10m W x 14m L x 3.6m H)
            float width = 10.0f;
            float length = 14.0f;
            float height = 3.6f;
            float wallThk = 0.20f;

            // Floor
            CreateProBuilderCube("Floor", archParent, new Vector3(0f, -wallThk * 0.5f, 0f), new Vector3(width + 2f * wallThk, wallThk, length + 2f * wallThk), floorMat);
            // Ceiling
            CreateProBuilderCube("Ceiling", archParent, new Vector3(0f, height + wallThk * 0.5f, 0f), new Vector3(width + 2f * wallThk, wallThk, length + 2f * wallThk), ceilingMat);
            // Left Wall (-X)
            CreateProBuilderCube("Wall_Left", archParent, new Vector3(-(width * 0.5f + wallThk * 0.5f), height * 0.5f, 0f), new Vector3(wallThk, height, length + 2f * wallThk), wallMat);
            // Right Wall (+X)
            CreateProBuilderCube("Wall_Right", archParent, new Vector3(width * 0.5f + wallThk * 0.5f, height * 0.5f, 0f), new Vector3(wallThk, height, length + 2f * wallThk), wallMat);
            // Back Wall (-Z) - Sealed Entrance from Attic
            CreateProBuilderCube("Wall_Back", archParent, new Vector3(0f, height * 0.5f, -(length * 0.5f + wallThk * 0.5f)), new Vector3(width, height, wallThk), wallMat);
            // Sealed Door Graphic on back wall
            CreateProBuilderCube("Sealed_Attic_Door", archParent, new Vector3(0f, 1.2f, -(length * 0.5f - 0.05f)), new Vector3(1.8f, 2.4f, 0.1f), doorMat);

            // Front Wall (+Z) with Exit Doorway opening (opening width = 1.6m, height = 2.4m)
            float frontZ = length * 0.5f + wallThk * 0.5f;
            float doorW = 1.6f;
            float doorH = 2.4f;
            float sideWallW = (width - doorW) * 0.5f;

            // Left Front Wall
            CreateProBuilderCube("Wall_Front_Left", archParent, new Vector3(-(doorW * 0.5f + sideWallW * 0.5f), height * 0.5f, frontZ), new Vector3(sideWallW, height, wallThk), wallMat);
            // Right Front Wall
            CreateProBuilderCube("Wall_Front_Right", archParent, new Vector3(doorW * 0.5f + sideWallW * 0.5f, height * 0.5f, frontZ), new Vector3(sideWallW, height, wallThk), wallMat);
            // Front Lintel
            float lintelH = height - doorH;
            CreateProBuilderCube("Wall_Front_Lintel", archParent, new Vector3(0f, doorH + lintelH * 0.5f, frontZ), new Vector3(doorW, lintelH, wallThk), wallMat);

            // Exit Doorway Frame
            float frameThk = 0.12f;
            float frameDepth = wallThk + 0.08f;
            CreateProBuilderCube("Frame_Post_Left", archParent, new Vector3(-(doorW * 0.5f - frameThk * 0.5f), doorH * 0.5f, frontZ), new Vector3(frameThk, doorH, frameDepth), frameMat);
            CreateProBuilderCube("Frame_Post_Right", archParent, new Vector3(doorW * 0.5f - frameThk * 0.5f, doorH * 0.5f, frontZ), new Vector3(frameThk, doorH, frameDepth), frameMat);
            CreateProBuilderCube("Frame_Lintel", archParent, new Vector3(0f, doorH - frameThk * 0.5f, frontZ), new Vector3(doorW, frameThk, frameDepth), frameMat);

            // Exit Door (Hinged)
            GameObject doorHinge = new GameObject("ExitDoor_Hinge");
            doorHinge.transform.SetParent(archParent, false);
            doorHinge.transform.position = new Vector3(-(doorW * 0.5f - frameThk), 0f, frontZ);

            GameObject doorSlab = new GameObject("ExitDoor_Slab");
            doorSlab.transform.SetParent(doorHinge.transform, false);
            doorSlab.transform.localPosition = new Vector3((doorW - 2f * frameThk) * 0.5f, doorH * 0.5f, 0f);

            var doorMesh = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(doorW - 2f * frameThk, doorH - frameThk, 0.08f));
            doorMesh.transform.SetParent(doorSlab.transform, false);
            doorMesh.transform.localPosition = Vector3.zero;
            doorMesh.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            doorMesh.ToMesh();
            doorMesh.Refresh();
            EditorMeshUtility.Optimize(doorMesh);

            BoxCollider doorCol = doorSlab.AddComponent<BoxCollider>();
            doorCol.size = new Vector3(doorW - 2f * frameThk, doorH - frameThk, 0.10f);
            doorCol.center = Vector3.zero;

            DoorController doorCtrl = doorSlab.AddComponent<DoorController>();
            doorCtrl.HingeTransform = doorHinge.transform;
            doorCtrl.OpenEulerAngles = new Vector3(0f, -95f, 0f);
            doorCtrl.AnimationDuration = 1.5f;

            // 5. Lighting Setup
            // 4 Ceiling Fluorescent lights
            CreateFluorescentLight("Light_Bay_1", lightParent, new Vector3(-2.5f, height - 0.2f, -3.5f));
            CreateFluorescentLight("Light_Bay_2", lightParent, new Vector3(2.5f, height - 0.2f, -3.5f));
            CreateFluorescentLight("Light_Bay_3", lightParent, new Vector3(-2.5f, height - 0.2f, 3.5f));
            CreateFluorescentLight("Light_Bay_4", lightParent, new Vector3(2.5f, height - 0.2f, 3.5f));

            // 4 Emergency Alarm Siren Beacons
            List<Light> alarmLights = new List<Light>();
            alarmLights.Add(CreateAlarmBeacon("Beacon_FL", lightParent, new Vector3(-4.6f, height - 0.35f, 6.6f)));
            alarmLights.Add(CreateAlarmBeacon("Beacon_FR", lightParent, new Vector3(4.6f, height - 0.35f, 6.6f)));
            alarmLights.Add(CreateAlarmBeacon("Beacon_BL", lightParent, new Vector3(-4.6f, height - 0.35f, -6.6f)));
            alarmLights.Add(CreateAlarmBeacon("Beacon_BR", lightParent, new Vector3(4.6f, height - 0.35f, -6.6f)));

            // 6. Furniture
            // Shelving Unit 1 (-X, Z = -2.5)
            BuildIndustrialShelving("Shelving_Unit_A", furnParent, new Vector3(-4.2f, 0f, -2.5f), metalMat);
            // Shelving Unit 2 (-X, Z = 2.0)
            BuildIndustrialShelving("Shelving_Unit_B", furnParent, new Vector3(-4.2f, 0f, 2.0f), metalMat);
            // Steel Filing Cabinet (-X, Z = 5.2)
            BuildFilingCabinet("FilingCabinet", furnParent, new Vector3(-4.2f, 0f, 5.2f), metalMat);

            // Supervisor Desk (+X, Z = -3.5)
            BuildSupervisorDesk("SupervisorDesk", furnParent, new Vector3(3.8f, 0f, -3.5f), darkWoodMat, metalMat);

            // Central Work Table (Assembly table at X = 0, Z = 0.8)
            BuildAssemblyTable("CentralWorkTable", furnParent, new Vector3(0f, 0f, 0.8f), woodMat, metalMat);

            // Vault Pedestal (Stand for locked container at X = -2.2, Z = 3.5)
            CreateProBuilderCube("VaultPedestal", furnParent, new Vector3(-2.2f, 0.40f, 3.5f), new Vector3(1.0f, 0.80f, 0.80f), metalMat);

            // 7. Props & Crates
            BuildCrateStack("CrateStack_A", propsParent, new Vector3(3.8f, 0f, 2.5f), crateMat);
            BuildCrateStack("CrateStack_B", propsParent, new Vector3(3.8f, 0f, 5.0f), crateMat);
            // Supply Crate #04 at (3.8, 0, 0.0)
            CreateProBuilderCube("Supply_Crate_04", propsParent, new Vector3(3.8f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), crateMat);

            // 8. Stage 2 Interactables & Puzzles
            // Notice Board / Clipboard near entrance (-Z wall)
            GameObject memoGo = CreateProBuilderCube("NoticeClipboard", interactParent, new Vector3(-2.0f, 1.6f, -(length * 0.5f - 0.06f)), new Vector3(0.85f, 1.10f, 0.05f), clueMat);
            BoxCollider memoCol = memoGo.GetComponent<BoxCollider>();
            if (memoCol == null) memoCol = memoGo.AddComponent<BoxCollider>();
            memoCol.size = new Vector3(1.30f, 1.50f, 0.60f);
            memoCol.center = new Vector3(0f, 0f, 0.25f);
            Stage2CipherAdapter cipherAdapter = memoGo.AddComponent<Stage2CipherAdapter>();

            // 5 Search Spots
            // Spot 1: Supervisor's Desk Drawer
            GameObject spot1Go = new GameObject("Spot_SupervisorDeskDrawer");
            spot1Go.transform.SetParent(interactParent, false);
            spot1Go.transform.position = new Vector3(3.8f, 0.70f, -3.5f);
            BoxCollider sc1 = spot1Go.AddComponent<BoxCollider>();
            sc1.size = new Vector3(0.6f, 0.35f, 0.6f);
            Stage2SearchSpot spot1 = spot1Go.AddComponent<Stage2SearchSpot>();
            spot1.ConfigureSpot(0, "Supervisor's Desk Drawer");

            // Spot 2: Steel Filing Cabinet
            GameObject spot2Go = new GameObject("Spot_FilingCabinet");
            spot2Go.transform.SetParent(interactParent, false);
            spot2Go.transform.position = new Vector3(-4.2f, 0.90f, 5.2f);
            BoxCollider sc2 = spot2Go.AddComponent<BoxCollider>();
            sc2.size = new Vector3(0.6f, 0.4f, 0.6f);
            Stage2SearchSpot spot2 = spot2Go.AddComponent<Stage2SearchSpot>();
            spot2.ConfigureSpot(1, "Steel Filing Cabinet");

            // Spot 3: Industrial Shelving Unit
            GameObject spot3Go = new GameObject("Spot_IndustrialShelving");
            spot3Go.transform.SetParent(interactParent, false);
            spot3Go.transform.position = new Vector3(-4.2f, 1.25f, 2.0f);
            BoxCollider sc3 = spot3Go.AddComponent<BoxCollider>();
            sc3.size = new Vector3(0.8f, 0.4f, 0.6f);
            Stage2SearchSpot spot3 = spot3Go.AddComponent<Stage2SearchSpot>();
            spot3.ConfigureSpot(2, "Industrial Shelving Unit");

            // Spot 4: Supply Crate #04
            GameObject spot4Go = new GameObject("Spot_SupplyCrate04");
            spot4Go.transform.SetParent(interactParent, false);
            spot4Go.transform.position = new Vector3(3.8f, 0.60f, 0.0f);
            BoxCollider sc4 = spot4Go.AddComponent<BoxCollider>();
            sc4.size = new Vector3(1.0f, 1.0f, 1.0f);
            Stage2SearchSpot spot4 = spot4Go.AddComponent<Stage2SearchSpot>();
            spot4.ConfigureSpot(3, "Supply Crate #04");

            // Spot 5: Work Table Tool Case
            Material redBoxMat = LoadOrCreateMaterial("M_SR_IndicRed", new Color(0.85f, 0.12f, 0.10f), 0.40f, 0.10f);
            GameObject spot5Go = CreateProBuilderCube("Spot_ToolCase", interactParent, new Vector3(-0.6f, 0.95f, 0.8f), new Vector3(0.40f, 0.20f, 0.30f), redBoxMat);
            spot5Go.name = "Spot_ToolCase";
            BoxCollider sc5 = spot5Go.GetComponent<BoxCollider>();
            if (sc5 == null) sc5 = spot5Go.AddComponent<BoxCollider>();
            Stage2SearchSpot spot5 = spot5Go.AddComponent<Stage2SearchSpot>();
            spot5.ConfigureSpot(4, "Work Table Tool Case");

            // Pattern Terminal on Work Table (X = 0.3, Z = 0.8)
            GameObject terminalRoot = new GameObject("PatternTerminal");
            terminalRoot.transform.SetParent(interactParent, false);
            terminalRoot.transform.position = new Vector3(0.3f, 0.85f, 0.8f);
            terminalRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Face towards player / entrance

            GameObject termBase = CreateProBuilderCube("TerminalBase", terminalRoot.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.50f, 0.10f, 0.40f), keypadMat);
            GameObject termScreen = CreateProBuilderCube("TerminalScreen", terminalRoot.transform, new Vector3(0f, 0.32f, 0.05f), new Vector3(0.45f, 0.38f, 0.20f), metalMat);

            // CRT Screen Glow light (makes the terminal unmistakable as an active computer monitor)
            Light screenGlow = termScreen.AddComponent<Light>();
            screenGlow.type = LightType.Point;
            screenGlow.color = new Color(0.25f, 0.95f, 0.55f); // Phosphor green glow
            screenGlow.intensity = 1.6f;
            screenGlow.range = 2.5f;

            BoxCollider termCol = terminalRoot.AddComponent<BoxCollider>();
            termCol.size = new Vector3(1.40f, 1.00f, 1.40f);
            termCol.center = new Vector3(0f, 0.30f, 0f);

            Stage2PatternTerminal patternTerminal = terminalRoot.AddComponent<Stage2PatternTerminal>();
            patternTerminal.RequireSecurityChip = true;

            // Storage Vault Container on Pedestal (X = -2.2, Z = 3.5, Y = 0.80)
            GameObject vaultRoot = new GameObject("StorageVaultContainer");
            vaultRoot.transform.SetParent(interactParent, false);
            vaultRoot.transform.position = new Vector3(-2.2f, 0.80f, 3.5f);

            GameObject vaultBody = CreateProBuilderCube("VaultBody", vaultRoot.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.70f, 0.50f, 0.60f), lockboxMat);

            GameObject vaultLidHinge = new GameObject("VaultLidHinge");
            vaultLidHinge.transform.SetParent(vaultRoot.transform, false);
            vaultLidHinge.transform.localPosition = new Vector3(0f, 0.50f, 0.30f);

            GameObject vaultLidSlab = CreateProBuilderCube("VaultLid", vaultLidHinge.transform, new Vector3(0f, 0.02f, -0.30f), new Vector3(0.72f, 0.04f, 0.62f), lockboxMat);

            GameObject vaultKeypad = CreateProBuilderCube("VaultKeypad", vaultRoot.transform, new Vector3(0f, 0.30f, -0.31f), new Vector3(0.20f, 0.25f, 0.04f), keypadMat);

            // Keycard inside the vault (elevated to y=0.56f so it floats clearly above vault rim)
            GameObject keycardGo = CreateProBuilderCube("MasterKeycard", vaultRoot.transform, new Vector3(0f, 0.56f, 0f), new Vector3(0.16f, 0.02f, 0.24f), metalMat);
            BoxCollider kcCol = keycardGo.GetComponent<BoxCollider>();
            if (kcCol == null) kcCol = keycardGo.AddComponent<BoxCollider>();
            kcCol.size = new Vector3(0.60f, 0.50f, 0.60f); // Generous grab collider

            Light kcGlow = keycardGo.AddComponent<Light>();
            kcGlow.type = LightType.Point;
            kcGlow.color = new Color(0.2f, 0.9f, 0.85f);
            kcGlow.intensity = 1.0f;
            kcGlow.range = 1.5f;

            Stage2KeycardPickup keycardPickup = keycardGo.AddComponent<Stage2KeycardPickup>();

            // Setup Storage Container script
            BoxCollider vaultCol = vaultRoot.AddComponent<BoxCollider>();
            vaultCol.size = new Vector3(0.85f, 0.65f, 0.75f);
            vaultCol.center = new Vector3(0f, 0.28f, 0f);

            Stage2StorageContainer storageContainer = vaultRoot.AddComponent<Stage2StorageContainer>();
            storageContainer.LidTransform = vaultLidHinge.transform;
            storageContainer.ContentsKeycard = keycardGo;

            // Security Reader mounted at exit wall (X = 1.1, Y = 1.35, Z = 6.95)
            GameObject readerGo = CreateProBuilderCube("SecurityReader", interactParent, new Vector3(1.1f, 1.35f, frontZ - 0.12f), new Vector3(0.24f, 0.32f, 0.10f), metalMat);
            BoxCollider readerCol = readerGo.GetComponent<BoxCollider>();
            if (readerCol == null) readerCol = readerGo.AddComponent<BoxCollider>();
            readerCol.size = new Vector3(0.50f, 0.50f, 0.40f);

            // Reader Status LED
            GameObject ledGo = new GameObject("ReaderLED");
            ledGo.transform.SetParent(readerGo.transform, false);
            ledGo.transform.localPosition = new Vector3(0f, 0.10f, -0.06f);
            Light readerLed = ledGo.AddComponent<Light>();
            readerLed.type = LightType.Point;
            readerLed.color = new Color(0.95f, 0.25f, 0.25f); // Red
            readerLed.range = 0.8f;
            readerLed.intensity = 1.5f;

            Stage2SecurityReader securityReader = readerGo.AddComponent<Stage2SecurityReader>();
            securityReader.ExitDoor = doorCtrl;
            securityReader.StatusLed = readerLed;

            // Exit Zone Trigger behind the doorway (Z = 8.5)
            GameObject exitTriggerGo = new GameObject("ExitTriggerZone");
            exitTriggerGo.transform.SetParent(interactParent, false);
            exitTriggerGo.transform.position = new Vector3(0f, 1.2f, frontZ + 1.2f);
            BoxCollider exitCol = exitTriggerGo.AddComponent<BoxCollider>();
            exitCol.isTrigger = true;
            exitCol.size = new Vector3(3.0f, 2.5f, 2.0f);
            exitTriggerGo.AddComponent<Stage2ExitTrigger>();

            // 9. Managers Setup
            GameObject managersGo = new GameObject(ManagersContainerName);
            managersGo.transform.position = Vector3.zero;

            Stage2Audio stage2Audio = managersGo.AddComponent<Stage2Audio>();
            Stage2PuzzleGenerator puzzleGen = managersGo.AddComponent<Stage2PuzzleGenerator>();
            Stage2SecurityAlarmEvent alarmEvent = managersGo.AddComponent<Stage2SecurityAlarmEvent>();
            alarmEvent.SirenLights = alarmLights.ToArray();

            Stage2Timer timer = managersGo.AddComponent<Stage2Timer>();
            Stage2IntroUI introUI = managersGo.AddComponent<Stage2IntroUI>();

            ObjectiveManager objManager = managersGo.AddComponent<ObjectiveManager>();
            var objectiveSteps = Stage2ObjectiveSetup.EnsureStage2ObjectiveAssets();
            objManager.SetSteps(objectiveSteps);

            ObjectiveProgressionAdapter adapter = managersGo.AddComponent<ObjectiveProgressionAdapter>();
            ObjectiveHUD objHUD = managersGo.AddComponent<ObjectiveHUD>();
            FeedbackHUD feedbackHUD = managersGo.AddComponent<FeedbackHUD>();
            GameManager gameManager = managersGo.AddComponent<GameManager>();
            PauseManager pauseManager = managersGo.AddComponent<PauseManager>();
            PauseUI pauseUI = managersGo.AddComponent<PauseUI>();
            EscapeUI escapeUI = managersGo.AddComponent<EscapeUI>();

            // 10. Instantiate Player
            Vector3 playerSpawn = new Vector3(0f, 0.05f, -5.0f);
            GameObject playerGo = PlayerBuilder.BuildPlayer(playerSpawn, Quaternion.identity, forceRebuild: true);

            // 11. Add scene to EditorBuildSettings
            EnsureSceneInBuildSettings(ScenePath);

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            ProBuilderEditor.Refresh(false);
            SceneView.RepaintAll();

            Debug.Log($"<b><color=#5cb85c>[StorageRoomBuilder] Stage 2 Storage Room successfully generated and saved to '{ScenePath}'!</color></b>");
        }

        #region Helpers & Architecture Building

        private static GameObject CreateSubContainer(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject CreateProBuilderCube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPos;
            pb.transform.localRotation = Quaternion.identity;

            MeshRenderer mr = pb.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }

            pb.ToMesh();
            pb.Refresh();
            EditorMeshUtility.Optimize(pb);

            // Ensure BoxCollider exists
            BoxCollider col = pb.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = pb.gameObject.AddComponent<BoxCollider>();
            }

            return pb.gameObject;
        }

        private static void CreateFluorescentLight(string name, Transform parent, Vector3 localPos)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPos;

            Light lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.88f, 0.92f, 0.96f);
            lt.intensity = 1.4f;
            lt.range = 9.5f;
            lt.shadows = LightShadows.Soft;
        }

        private static Light CreateAlarmBeacon(string name, Transform parent, Vector3 localPos)
        {
            GameObject beaconGo = new GameObject(name);
            beaconGo.transform.SetParent(parent, false);
            beaconGo.transform.localPosition = localPos;

            Light lt = beaconGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(1.0f, 0.15f, 0.15f);
            lt.intensity = 2.4f;
            lt.range = 12.0f;
            lt.enabled = false;

            return lt;
        }

        private static void BuildIndustrialShelving(string name, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject shelfRoot = CreateSubContainer(name, parent);
            shelfRoot.transform.localPosition = localPos;

            float shelfH = 2.8f;
            float shelfW = 2.4f;
            float shelfD = 0.8f;
            float postThk = 0.08f;

            // 4 Upright Corner Posts
            CreateProBuilderCube("Post_FL", shelfRoot.transform, new Vector3(shelfD * 0.5f, shelfH * 0.5f, shelfW * 0.5f), new Vector3(postThk, shelfH, postThk), mat);
            CreateProBuilderCube("Post_FR", shelfRoot.transform, new Vector3(shelfD * 0.5f, shelfH * 0.5f, -shelfW * 0.5f), new Vector3(postThk, shelfH, postThk), mat);
            CreateProBuilderCube("Post_BL", shelfRoot.transform, new Vector3(-shelfD * 0.5f, shelfH * 0.5f, shelfW * 0.5f), new Vector3(postThk, shelfH, postThk), mat);
            CreateProBuilderCube("Post_BR", shelfRoot.transform, new Vector3(-shelfD * 0.5f, shelfH * 0.5f, -shelfW * 0.5f), new Vector3(postThk, shelfH, postThk), mat);

            // 4 Horizontal Tiers
            for (int i = 0; i < 4; i++)
            {
                float y = 0.2f + i * 0.8f;
                CreateProBuilderCube($"Tier_{i + 1}", shelfRoot.transform, new Vector3(0f, y, 0f), new Vector3(shelfD, 0.05f, shelfW), mat);
            }
        }

        private static void BuildFilingCabinet(string name, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject cabRoot = CreateSubContainer(name, parent);
            cabRoot.transform.localPosition = localPos;

            CreateProBuilderCube("Cabinet_Body", cabRoot.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 0.6f), mat);
        }

        private static void BuildSupervisorDesk(string name, Transform parent, Vector3 localPos, Material woodMat, Material metalMat)
        {
            GameObject deskRoot = CreateSubContainer(name, parent);
            deskRoot.transform.localPosition = localPos;

            // Desk Top (2.0m L x 1.0m W x 0.08m H)
            CreateProBuilderCube("Desk_Top", deskRoot.transform, new Vector3(0f, 0.78f, 0f), new Vector3(1.0f, 0.08f, 2.0f), woodMat);
            // Desk Legs / Pedestals
            CreateProBuilderCube("Pedestal_Left", deskRoot.transform, new Vector3(0f, 0.37f, 0.75f), new Vector3(0.85f, 0.74f, 0.45f), metalMat);
            CreateProBuilderCube("Pedestal_Right", deskRoot.transform, new Vector3(0f, 0.37f, -0.75f), new Vector3(0.85f, 0.74f, 0.45f), metalMat);
        }

        private static void BuildAssemblyTable(string name, Transform parent, Vector3 localPos, Material woodMat, Material metalMat)
        {
            GameObject tableRoot = CreateSubContainer(name, parent);
            tableRoot.transform.localPosition = localPos;

            // Heavy Worktop (2.4m L x 1.2m W x 0.10m H at Y = 0.85m)
            CreateProBuilderCube("Worktable_Top", tableRoot.transform, new Vector3(0f, 0.80f, 0f), new Vector3(1.2f, 0.10f, 2.4f), woodMat);

            // 4 Steel Legs
            float legH = 0.75f;
            float legThk = 0.10f;
            CreateProBuilderCube("Leg_FL", tableRoot.transform, new Vector3(0.50f, legH * 0.5f, 1.05f), new Vector3(legThk, legH, legThk), metalMat);
            CreateProBuilderCube("Leg_FR", tableRoot.transform, new Vector3(0.50f, legH * 0.5f, -1.05f), new Vector3(legThk, legH, legThk), metalMat);
            CreateProBuilderCube("Leg_BL", tableRoot.transform, new Vector3(-0.50f, legH * 0.5f, 1.05f), new Vector3(legThk, legH, legThk), metalMat);
            CreateProBuilderCube("Leg_BR", tableRoot.transform, new Vector3(-0.50f, legH * 0.5f, -1.05f), new Vector3(legThk, legH, legThk), metalMat);
        }

        private static void BuildCrateStack(string name, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject stackRoot = CreateSubContainer(name, parent);
            stackRoot.transform.localPosition = localPos;

            CreateProBuilderCube("Crate_Base_1", stackRoot.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.0f, 1.0f, 1.0f), mat);
            CreateProBuilderCube("Crate_Top", stackRoot.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), mat);
        }

        private static Material LoadOrCreateMaterial(string matName, Color albedo, float smoothness = 0.2f, float metallic = 0.0f)
        {
            string path = $"Assets/Art/Materials/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = albedo;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path.Equals(scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[StorageRoomBuilder] Added '{scenePath}' to EditorBuildSettings.");
        }

        #endregion
    }
}
