using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor.ProBuilder;
using EscapeRoom.Core;
using EscapeRoom.Puzzle;
using EscapeRoom.UI;


namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor tool that creates and populates Assets/Scenes/StorageRoom.unity (Stage 2).
    /// Run from Tools > Escape Room > Build Storage Room Scene.
    ///
    /// Layout overview (top-down):
    ///   Back wall (−Z): shelves + locked combination cabinet
    ///   Right wall (+X): old filing cabinet, clue note on desk
    ///   Front wall (+Z): entry area + security door (right side)
    ///   Security card reader: beside the security door
    ///
    /// Systems placed:
    ///   Player, ObjectiveManager (Stage 2 steps), ObjectiveHUD, FeedbackHUD,
    ///   InventorySystem, EscapeRoomAudio, InteractionSystem, StorageRoomProgressionAdapter,
    ///   SymbolClueInteractable, SymbolCombinationCabinet, PickupItem (access card),
    ///   SecurityCardReader, DoorController (security door), Stage2CompleteUI,
    ///   RoomBannerTrigger, BannerUI, PauseManager
    /// </summary>
    public static class StorageRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StorageRoom.unity";

        [MenuItem("Tools/Escape Room/Build Storage Room Scene (Stage 2)", false, 15)]
        public static void BuildStorageRoomScene()
        {
            // ── Prompt user to save current scene ───────────────────────────
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // ── Create / open blank scene ────────────────────────────────────
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Ensure Art/Materials directory exists ───────────────────────
            EnsureDirectory("Assets/Art/Materials");
            EnsureDirectory("Assets/Scenes");

            // ── Materials ────────────────────────────────────────────────────
            Material floorMat    = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Floor",    new Color(0.25f, 0.23f, 0.20f), 0.15f);
            Material wallMat     = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Wall",     new Color(0.32f, 0.30f, 0.28f), 0.10f);
            Material ceilMat     = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Ceiling",  new Color(0.18f, 0.17f, 0.16f), 0.05f);
            Material woodMat     = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Wood",     new Color(0.38f, 0.27f, 0.15f), 0.18f);
            Material metalMat    = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Metal",    new Color(0.40f, 0.42f, 0.44f), 0.60f, 0.70f);
            Material crateMat    = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Crate",    new Color(0.58f, 0.46f, 0.30f), 0.12f);
            Material cardMat     = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Card",     new Color(0.20f, 0.55f, 0.90f), 0.50f);
            Material readerMat   = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Reader",   new Color(0.12f, 0.14f, 0.16f), 0.70f, 0.80f);
            Material doorMat     = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Door",     new Color(0.22f, 0.24f, 0.26f), 0.55f, 0.50f);
            Material lightMat    = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_LightPanel", new Color(0.80f, 0.85f, 0.90f), 0.30f);
            Material indicGreenMat = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_IndicRed", new Color(0.8f, 0.1f, 0.1f), 0.60f);

            // ── Room dimensions ──────────────────────────────────────────────
            //   10 m long (Z), 8 m wide (X), 3.2 m high
            float rL = 10f, rW = 8f, rH = 3.2f, rT = 0.25f;

            // ── Environment root ─────────────────────────────────────────────
            GameObject envRoot = new GameObject("Environment");
            GameObject archGo  = CreateChild("Architecture", envRoot.transform);
            GameObject furnGo  = CreateChild("Furniture",    envRoot.transform);
            GameObject propsGo = CreateChild("Props",        envRoot.transform);

            // ── Architecture ─────────────────────────────────────────────────
            BuildArchitecture(archGo.transform, rL, rW, rH, rT, floorMat, wallMat, ceilMat, doorMat, metalMat);

            // ── Furniture ────────────────────────────────────────────────────
            BuildFurniture(furnGo.transform, woodMat, metalMat, lightMat);

            // ── Props ────────────────────────────────────────────────────────
            BuildProps(propsGo.transform, crateMat, woodMat);

            // ── Lighting ─────────────────────────────────────────────────────
            BuildLighting(archGo.transform, rW, rH);

            // ── Player ───────────────────────────────────────────────────────
            GameObject playerGo = BuildPlayer(rL, rW);

            // ── UI / Systems ─────────────────────────────────────────────────
            BuildSystems(envRoot.transform, playerGo, rL, rW, cardMat, readerMat, metalMat, indicGreenMat, doorMat);

            // ── Room banner trigger ──────────────────────────────────────────
            BuildRoomBannerTrigger(envRoot.transform, rL, rW);

            // ── Finalize & save ──────────────────────────────────────────────
            if (!Application.isBatchMode)
            {
                ProBuilderEditor.Refresh(false);
                SceneView.RepaintAll();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            // ── Add to build settings if missing ────────────────────────────
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("<color=#5cb85c><b>[StorageRoomSceneBuilder]</b></color> " +
                      "StorageRoom.unity successfully created at " + ScenePath);
        }

        // ────────────────────────────────────────────────────────────────────
        #region Architecture

        private static void BuildArchitecture(
            Transform parent,
            float rL, float rW, float rH, float rT,
            Material floor, Material wall, Material ceil, Material door, Material metal)
        {
            // Floor
            Cube("Floor", parent, new Vector3(0, -rT * 0.5f, 0),
                 new Vector3(rW + 2 * rT, rT, rL + 2 * rT), floor);

            // Ceiling
            Cube("Ceiling", parent, new Vector3(0, rH + rT * 0.5f, 0),
                 new Vector3(rW + 2 * rT, rT, rL + 2 * rT), ceil);

            // Left wall (−X)
            Cube("Wall_Left", parent, new Vector3(-(rW * .5f + rT * .5f), rH * .5f, 0),
                 new Vector3(rT, rH, rL + 2 * rT), wall);

            // Right wall (+X)
            Cube("Wall_Right", parent, new Vector3(rW * .5f + rT * .5f, rH * .5f, 0),
                 new Vector3(rT, rH, rL + 2 * rT), wall);

            // Back wall (−Z)
            Cube("Wall_Back", parent, new Vector3(0, rH * .5f, -(rL * .5f + rT * .5f)),
                 new Vector3(rW, rH, rT), wall);

            // Front wall (+Z) — with security doorway on right side
            // Door opening: 1.1 m wide, 2.5 m tall, centred at X = +2.0
            float dW = 1.1f, dH = 2.5f, dX = 2.2f;
            float frontZ = rL * .5f + rT * .5f;
            float wallHalfW = rW * 0.5f;

            // Left segment (solid): from -wallHalfW to dX - dW/2
            float leftSegW = wallHalfW + dX - dW * .5f;
            float leftSegX = -wallHalfW + leftSegW * .5f;
            Cube("Wall_Front_Left", parent,
                 new Vector3(leftSegX, rH * .5f, frontZ),
                 new Vector3(leftSegW, rH, rT), wall);

            // Right segment: from dX + dW/2 to +wallHalfW
            float rightSegW = wallHalfW - dX - dW * .5f;
            float rightSegX = dX + dW * .5f + rightSegW * .5f;
            Cube("Wall_Front_Right", parent,
                 new Vector3(rightSegX, rH * .5f, frontZ),
                 new Vector3(rightSegW, rH, rT), wall);

            // Header above door
            float headerH = rH - dH;
            Cube("Wall_Front_Header", parent,
                 new Vector3(dX, dH + headerH * .5f, frontZ),
                 new Vector3(dW, headerH, rT), wall);

            // Door frame
            float fT = 0.06f, fD = 0.10f;
            Cube("DoorFrame_Left",   parent, new Vector3(dX - dW*.5f + fT*.5f, dH*.5f, frontZ), new Vector3(fT, dH, fD), metal);
            Cube("DoorFrame_Right",  parent, new Vector3(dX + dW*.5f - fT*.5f, dH*.5f, frontZ), new Vector3(fT, dH, fD), metal);
            Cube("DoorFrame_Header", parent, new Vector3(dX, dH + fT*.5f, frontZ),               new Vector3(dW, fT, fD), metal);
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Furniture

        private static void BuildFurniture(Transform parent, Material wood, Material metal, Material lightPanel)
        {
            // ── Shelf Unit A (back-left wall) ────────────────────────────────
            BuildShelfUnit("ShelfA", parent, new Vector3(-2.8f, 0, -4.5f), wood, 4);
            // ── Shelf Unit B (left wall, middle) ────────────────────────────
            BuildShelfUnit("ShelfB", parent, new Vector3(-3.6f, 0, -1.0f), wood, 3);

            // ── Old Desk (right area, near clue) ────────────────────────────
            BuildDesk("Desk", parent, new Vector3(2.8f, 0, -1.5f), wood, metal);

            // ── Filing cabinet (back-right) ──────────────────────────────────
            BuildFilingCabinet("FilingCabinet", parent, new Vector3(3.0f, 0, -4.0f), metal);

            // ── Combination Cabinet (back-right area) ───────────────────────
            // This is the puzzle cabinet — tagged for SymbolCombinationCabinet script
            BuildCombinationCabinetGeometry("CombinationCabinet", parent, new Vector3(1.5f, 0, -4.6f), wood, metal);
        }

        private static void BuildShelfUnit(string name, Transform parent, Vector3 pos, Material mat, int tiers)
        {
            GameObject root = CreateChild(name, parent);
            root.transform.localPosition = pos;
            float sD = 0.45f, sH = 2.2f, sW = 1.4f, tT = 0.04f, sideT = 0.05f;

            Cube("Side_L",    root.transform, new Vector3(0, sH*.5f, -sW*.5f + sideT*.5f), new Vector3(sD, sH, sideT), mat);
            Cube("Side_R",    root.transform, new Vector3(0, sH*.5f,  sW*.5f - sideT*.5f), new Vector3(sD, sH, sideT), mat);
            Cube("Back",      root.transform, new Vector3(-sD*.5f + .02f, sH*.5f, 0),      new Vector3(.03f, sH, sW),  mat);

            float spacing = (sH - .1f) / (tiers - 1);
            for (int i = 0; i < tiers; i++)
            {
                float y = .05f + i * spacing;
                Cube($"Tier_{i+1}", root.transform, new Vector3(0, y, 0), new Vector3(sD - .03f, tT, sW - 2*sideT), mat);
            }
        }

        private static void BuildDesk(string name, Transform parent, Vector3 pos, Material wood, Material metal)
        {
            GameObject root = CreateChild(name, parent);
            root.transform.localPosition = pos;
            float dW = 1.2f, dH = 0.82f, dL = 0.70f, topT = 0.04f, legT = 0.05f;

            Cube("Top",    root.transform, new Vector3(0, dH - topT*.5f, 0), new Vector3(dL, topT, dW), wood);
            float legH = dH - topT, ly = legH*.5f;
            float lx = dL*.5f - legT*.6f, lz = dW*.5f - legT*.6f;
            Cube("Leg_FL", root.transform, new Vector3(-lx, ly, lz),  new Vector3(legT, legH, legT), metal);
            Cube("Leg_FR", root.transform, new Vector3( lx, ly, lz),  new Vector3(legT, legH, legT), metal);
            Cube("Leg_BL", root.transform, new Vector3(-lx, ly, -lz), new Vector3(legT, legH, legT), metal);
            Cube("Leg_BR", root.transform, new Vector3( lx, ly, -lz), new Vector3(legT, legH, legT), metal);
        }

        private static void BuildFilingCabinet(string name, Transform parent, Vector3 pos, Material metal)
        {
            GameObject root = CreateChild(name, parent);
            root.transform.localPosition = pos;
            Cube("Body",   root.transform, new Vector3(0, 0.65f, 0), new Vector3(0.55f, 1.30f, 0.62f), metal);
            Cube("Top",    root.transform, new Vector3(0, 1.32f, 0), new Vector3(0.57f, 0.04f, 0.64f), metal);
            // 2 drawer handles
            Cube("Handle1", root.transform, new Vector3(-0.28f, 0.45f, 0), new Vector3(0.02f, 0.04f, 0.20f), metal);
            Cube("Handle2", root.transform, new Vector3(-0.28f, 0.90f, 0), new Vector3(0.02f, 0.04f, 0.20f), metal);
        }

        private static void BuildCombinationCabinetGeometry(string name, Transform parent, Vector3 pos, Material wood, Material metal)
        {
            // Note: The actual SymbolCombinationCabinet component + BoxCollider will be added
            // programmatically on the "Body" child (or root) during system setup below.
            GameObject root = CreateChild(name, parent);
            root.transform.localPosition = pos;

            float cW = 0.65f, cH = 1.80f, cD = 0.50f;
            Cube("Body",      root.transform, new Vector3(0, cH*.5f, 0), new Vector3(cD, cH, cW), wood);
            Cube("Plinth",    root.transform, new Vector3(0, .04f, 0),   new Vector3(cD+.02f, .08f, cW+.02f), wood);
            Cube("Crown",     root.transform, new Vector3(0, cH+.03f, 0), new Vector3(cD+.04f, .06f, cW+.04f), wood);

            // Door (hinge pivot at left edge)
            GameObject hingeGo = CreateChild("Door_Hinge", root.transform);
            hingeGo.transform.localPosition = new Vector3(-cD*.5f - .005f, cH*.5f, -cW*.5f + .02f);

            var doorGo = Cube("Door", hingeGo.transform,
                new Vector3(0f, 0f, cW*.5f - .01f),
                new Vector3(.025f, cH - .10f, cW - .04f), wood);

            // Metal latch handle
            Cube("Handle", doorGo.transform,
                new Vector3(-.02f, 0, cW*.5f - .10f),
                new Vector3(.02f, .12f, .02f), metal);

            // Combination dial plate (small panel on door)
            Cube("DialPlate", doorGo.transform,
                new Vector3(-.02f, .25f, 0),
                new Vector3(.015f, .16f, .30f), metal);
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Props

        private static void BuildProps(Transform parent, Material crate, Material wood)
        {
            // Crates cluster near left wall
            CrateAt("Crate_A", parent, new Vector3(-2.8f, 0.25f, 0.8f), new Vector3(.55f, .50f, .55f), crate);
            CrateAt("Crate_B", parent, new Vector3(-2.3f, 0.25f, 0.5f), new Vector3(.48f, .48f, .48f), crate);
            CrateAt("Crate_C", parent, new Vector3(-2.8f, 0.76f, 0.8f), new Vector3(.50f, .48f, .50f), crate, 12f);
            CrateAt("Crate_D", parent, new Vector3(-3.0f, 0.25f, 2.0f), new Vector3(.60f, .52f, .60f), crate, -8f);

            // Boxes on shelves (non-interactive decor)
            CrateAt("Box_S1", parent, new Vector3(-2.65f, 0.52f, -4.20f), new Vector3(.30f, .25f, .28f), crate);
            CrateAt("Box_S2", parent, new Vector3(-2.65f, 0.52f, -4.60f), new Vector3(.28f, .25f, .32f), crate);
            CrateAt("Box_S3", parent, new Vector3(-2.65f, 1.08f, -4.35f), new Vector3(.35f, .25f, .30f), crate, 5f);

            // Small scrap on desk (flavour)
            CrateAt("PaperStack", parent, new Vector3(2.85f, 0.85f, -1.35f), new Vector3(.22f, .04f, .16f), wood);
        }

        private static ProBuilderMesh CrateAt(string n, Transform p, Vector3 pos, Vector3 size, Material mat, float rotY = 0f)
        {
            var pb = EscapeRoomBuilder.CreateProBuilderCube(n, p, pos, size, Quaternion.Euler(0, rotY, 0), mat);
            // Make crates NOT interactable (no IInteractable)
            GameObjectUtility.SetStaticEditorFlags(pb.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            return pb;
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Lighting

        private static void BuildLighting(Transform parent, float rW, float rH)
        {
            // Main ambient fill - very dim
            var ambGo = new GameObject("AmbientLight");
            ambGo.transform.SetParent(parent, false);
            var ambLight = ambGo.AddComponent<Light>();
            ambLight.type = LightType.Directional;
            ambLight.intensity = 0.12f;
            ambLight.color = new Color(0.6f, 0.65f, 0.7f);
            ambLight.shadows = LightShadows.None;

            // Flickering industrial lights (two point lights at ceiling)
            AddIndustrialLight("Light_Main_L", parent, new Vector3(-rW * .22f, rH - .2f, -1.5f), intensity: 1.4f);
            AddIndustrialLight("Light_Main_R", parent, new Vector3( rW * .22f, rH - .2f, -1.5f), intensity: 1.2f);

            // Accent light near security door
            AddIndustrialLight("Light_Door",   parent, new Vector3(2.2f, rH - .3f, 4.5f),  intensity: 1.0f, color: new Color(0.7f, 0.75f, 0.8f), range: 5f);

            // Weak light near back wall (cabinet/shelf area)
            AddIndustrialLight("Light_Back",   parent, new Vector3(0f, rH - .3f, -4.0f), intensity: 0.8f, range: 5f);
        }

        private static void AddIndustrialLight(string name, Transform parent, Vector3 pos,
            float intensity = 1f, Color? color = null, float range = 7f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color ?? new Color(0.82f, 0.80f, 0.72f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Player

        private static GameObject BuildPlayer(float rL, float rW)
        {
            // Player spawns near front entry (south-center)
            Vector3 spawnPos = new Vector3(0f, 1f, rL * .5f - 1.5f);

            GameObject player = new GameObject("Player");
            player.transform.position = spawnPos;
            player.tag = "Player";

            // CharacterController
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 0.9f, 0);

            // Player components
            player.AddComponent<EscapeRoom.Player.PlayerController>();
            player.AddComponent<EscapeRoom.Player.PlayerLook>();
            player.AddComponent<EscapeRoom.Interaction.InventorySystem>();

            // Camera
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0.75f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 80f;
            cam.fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();

            // Interaction system on player
            var interSys = player.AddComponent<EscapeRoom.Interaction.InteractionSystem>();

            // FeedbackHUD on player root
            player.AddComponent<EscapeRoom.UI.FeedbackHUD>();

            return player;
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Systems

        private static void BuildSystems(
            Transform envRoot, GameObject playerGo,
            float rL, float rW,
            Material cardMat, Material readerMat, Material metalMat, Material indicMat,
            Material doorMat)
        {
            // ── Core systems ─────────────────────────────────────────────────
            var systemsGo = CreateChild("_Systems", envRoot);

            // GameManager
            systemsGo.AddComponent<EscapeRoom.Core.GameManager>();

            // PauseManager
            systemsGo.AddComponent<EscapeRoom.Core.PauseManager>();

            // EscapeRoomAudio
            systemsGo.AddComponent<EscapeRoom.Audio.EscapeRoomAudio>();

            // ObjectiveManager (Stage 2 steps)
            var objMgr = systemsGo.AddComponent<EscapeRoom.Core.ObjectiveManager>();

            // Load Stage 2 objective steps and assign via serialized field
            var step1 = LoadStep("S2_Step1_FindClue");
            var step2 = LoadStep("S2_Step2_DiscoverCombo");
            var step3 = LoadStep("S2_Step3_UnlockCabinet");
            var step4 = LoadStep("S2_Step4_CollectCard");
            var step5 = LoadStep("S2_Step5_UnlockCorridor");

            // Use SetSteps via serialized object to set the steps array
            var so = new SerializedObject(objMgr);
            var stepsProp = so.FindProperty("steps");
            stepsProp.arraySize = 5;
            stepsProp.GetArrayElementAtIndex(0).objectReferenceValue = step1;
            stepsProp.GetArrayElementAtIndex(1).objectReferenceValue = step2;
            stepsProp.GetArrayElementAtIndex(2).objectReferenceValue = step3;
            stepsProp.GetArrayElementAtIndex(3).objectReferenceValue = step4;
            stepsProp.GetArrayElementAtIndex(4).objectReferenceValue = step5;
            so.ApplyModifiedProperties();

            // ObjectiveHUD (on player for audio access)
            var hudGo = CreateChild("ObjectiveHUD", envRoot);
            hudGo.AddComponent<EscapeRoom.UI.ObjectiveHUD>();

            // Stage2CompleteUI
            var s2uiGo = CreateChild("Stage2CompleteUI", envRoot);
            s2uiGo.AddComponent<EscapeRoom.UI.Stage2CompleteUI>();

            // StorageRoomProgressionAdapter (Stage 2 only)
            systemsGo.AddComponent<EscapeRoom.Interaction.StorageRoomProgressionAdapter>();

            // KeypadUI (needed as dependency of InteractionSystem / ObjectiveHUD checks)
            var keypadUIGo = CreateChild("KeypadUI", envRoot);
            keypadUIGo.AddComponent<EscapeRoom.UI.KeypadUI>();

            // BannerUI
            var bannerUIGo = CreateChild("BannerUI", envRoot);
            bannerUIGo.AddComponent<EscapeRoom.UI.BannerUI>();

            // ── Puzzle objects ───────────────────────────────────────────────

            // 1. Symbol Clue Note (on desk surface)
            BuildSymbolClue(envRoot);

            // 2. Combination Cabinet (back-right)
            BuildCombinationCabinetPuzzle(envRoot, cardMat);

            // 3. Security door + card reader + Stage2 completion hook
            BuildSecurityDoorSystem(envRoot, rL, rW, readerMat, metalMat, indicMat, doorMat, s2uiGo);
        }

        private static EscapeRoom.Core.ObjectiveStep LoadStep(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:ObjectiveStep");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<EscapeRoom.Core.ObjectiveStep>(path);
            }
            Debug.LogWarning($"[StorageRoomSceneBuilder] Could not find ObjectiveStep asset: {assetName}");
            return null;
        }

        // ── Symbol Clue ──────────────────────────────────────────────────────
        private static void BuildSymbolClue(Transform envRoot)
        {
            // A folded paper / note on the desk surface
            var clueGo = new GameObject("SymbolClueNote");
            clueGo.transform.SetParent(envRoot, false);
            clueGo.transform.position = new Vector3(2.75f, 0.86f, -1.60f);
            clueGo.transform.rotation = Quaternion.Euler(0, 15f, 0);

            // Visual (flat paper)
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "ClueNote_Visual";
            vis.transform.SetParent(clueGo.transform, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localScale = new Vector3(0.22f, 0.01f, 0.18f);

            Material paperMat = EscapeRoomBuilder.GetOrCreateMaterial("M_SR_Paper", new Color(0.90f, 0.87f, 0.80f), 0.05f);
            vis.GetComponent<Renderer>().sharedMaterial = paperMat;
            UnityEngine.Object.DestroyImmediate(vis.GetComponent<BoxCollider>());

            // Interaction collider on root
            var col = clueGo.AddComponent<BoxCollider>();
            col.size   = new Vector3(0.25f, 0.15f, 0.22f);
            col.center = new Vector3(0, 0.07f, 0);

            // SymbolClueInteractable component
            var clue = clueGo.AddComponent<EscapeRoom.Interaction.SymbolClueInteractable>();

            // Configure via SerializedObject
            var so = new SerializedObject(clue);
            so.FindProperty("clueTitle").stringValue      = "Storage Room Note";
            so.FindProperty("bodyText").stringValue       = "A scrap of paper pinned to the shelf wall:";
            so.FindProperty("promptText").stringValue     = "Inspect Clue";

            var symProp = so.FindProperty("symbols");
            symProp.arraySize = 4;
            symProp.GetArrayElementAtIndex(0).stringValue = "△";
            symProp.GetArrayElementAtIndex(1).stringValue = "○";
            symProp.GetArrayElementAtIndex(2).stringValue = "□";
            symProp.GetArrayElementAtIndex(3).stringValue = "★";
            so.ApplyModifiedProperties();
        }

        // ── Combination Cabinet Puzzle ────────────────────────────────────────
        private static void BuildCombinationCabinetPuzzle(Transform envRoot, Material cardMat)
        {
            // Find the geometry object already created
            // We'll search by name in the scene
            var cabinetBodyGo = GameObject.Find("CombinationCabinet");
            if (cabinetBodyGo == null)
            {
                Debug.LogWarning("[StorageRoomSceneBuilder] CombinationCabinet geometry not found, creating fallback.");
                cabinetBodyGo = new GameObject("CombinationCabinet");
                cabinetBodyGo.transform.position = new Vector3(1.5f, 0, -4.6f);
            }

            // Ensure BoxCollider on root for interaction
            var existingCol = cabinetBodyGo.GetComponent<BoxCollider>();
            if (existingCol == null)
            {
                var c = cabinetBodyGo.AddComponent<BoxCollider>();
                c.size   = new Vector3(0.55f, 1.80f, 0.70f);
                c.center = new Vector3(-0.02f, 0.90f, 0f);
            }

            // SymbolCombinationCabinet script
            var cabinet = cabinetBodyGo.AddComponent<EscapeRoom.Puzzle.SymbolCombinationCabinet>();

            // Find door hinge child to assign as doorTransform
            var hingeT = cabinetBodyGo.transform.Find("Door_Hinge");

            // Configure via SerializedObject
            var so = new SerializedObject(cabinet);
            var ci = so.FindProperty("correctIndices");
            ci.arraySize = 4;
            ci.GetArrayElementAtIndex(0).intValue = 0; // △
            ci.GetArrayElementAtIndex(1).intValue = 1; // ○
            ci.GetArrayElementAtIndex(2).intValue = 2; // □
            ci.GetArrayElementAtIndex(3).intValue = 3; // ★

            so.FindProperty("lockedPrompt").stringValue   = "Enter Combination";
            so.FindProperty("unlockedPrompt").stringValue = "Press E to open cabinet";
            so.FindProperty("openedPrompt").stringValue   = "Cabinet is open";
            so.FindProperty("openEulerDelta").vector3Value = new Vector3(0, -105f, 0);

            if (hingeT != null)
                so.FindProperty("doorTransform").objectReferenceValue = hingeT;

            so.ApplyModifiedProperties();

            // ── Access Card (inside cabinet, hidden until opened) ─────────────
            var accessCardGo = new GameObject("AccessCard");
            accessCardGo.transform.SetParent(cabinetBodyGo.transform, false);
            accessCardGo.transform.localPosition = new Vector3(0f, 0.95f, 0.1f);

            // Visual
            var cardVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cardVis.name = "Card_Visual";
            cardVis.transform.SetParent(accessCardGo.transform, false);
            cardVis.transform.localScale = new Vector3(0.09f, 0.005f, 0.055f);
            cardVis.GetComponent<Renderer>().sharedMaterial = cardMat;
            UnityEngine.Object.DestroyImmediate(cardVis.GetComponent<BoxCollider>());

            // Collider
            var cardCol = accessCardGo.AddComponent<BoxCollider>();
            cardCol.size   = new Vector3(0.12f, 0.04f, 0.08f);
            cardCol.center = Vector3.zero;

            // PickupItem component
            var pickup = accessCardGo.AddComponent<EscapeRoom.Interaction.PickupItem>();
            var soCard = new SerializedObject(pickup);
            soCard.FindProperty("fallbackItemId").stringValue      = "access_card";
            soCard.FindProperty("fallbackDisplayName").stringValue = "Security Access Card";
            soCard.FindProperty("fallbackDescription").stringValue = "A card granting access to the security corridor.";
            soCard.FindProperty("promptFormat").stringValue        = "Pick Up Access Card";
            soCard.FindProperty("destroyOnPickup").boolValue       = false;
            soCard.ApplyModifiedProperties();

            // Start hidden — SymbolCombinationCabinet reveals it on open
            var cabinetSO = new SerializedObject(cabinet);
            cabinetSO.FindProperty("contentsObject").objectReferenceValue = accessCardGo;
            cabinetSO.ApplyModifiedProperties();

            accessCardGo.SetActive(false); // hidden until cabinet opens
        }

        // ── Security Door System ──────────────────────────────────────────────
        private static void BuildSecurityDoorSystem(
            Transform envRoot, float rL, float rW,
            Material readerMat, Material metalMat, Material indicMat,
            Material doorMat, GameObject stage2UIGo)
        {
            // Security door in the doorway opening (dX = 2.2, front wall Z = rL/2 = 5)
            float doorX = 2.2f, doorZ = rL * .5f, doorH = 2.5f, doorW = 1.05f;

            // Door geometry
            var doorGo = new GameObject("SecurityDoor");
            doorGo.transform.SetParent(envRoot, false);
            doorGo.transform.position = new Vector3(doorX, 0, doorZ);

            // Hinge pivot at left edge of door
            var hingeGo = new GameObject("Door_Hinge");
            hingeGo.transform.SetParent(doorGo.transform, false);
            hingeGo.transform.localPosition = new Vector3(-doorW * .5f, doorH * .5f, 0);

            // Door slab
            var doorSlab = EscapeRoomBuilder.CreateProBuilderCube(
                "DoorSlab", hingeGo.transform,
                new Vector3(doorW * .5f, 0, 0),
                new Vector3(doorW, doorH, 0.06f),
                Quaternion.identity, doorMat);
            // Remove auto-added BoxCollider size mismatch fix
            var slabCol = doorSlab.GetComponent<BoxCollider>();
            if (slabCol != null) { slabCol.size = new Vector3(doorW, doorH, 0.06f); slabCol.center = Vector3.zero; }

            // Reinforcement bars
            EscapeRoomBuilder.CreateProBuilderCube("Bar_H1", hingeGo.transform, new Vector3(doorW*.5f, doorH*.35f, .04f), new Vector3(doorW-0.04f, .04f, .025f), Quaternion.identity, metalMat);
            EscapeRoomBuilder.CreateProBuilderCube("Bar_H2", hingeGo.transform, new Vector3(doorW*.5f, doorH*.72f, .04f), new Vector3(doorW-0.04f, .04f, .025f), Quaternion.identity, metalMat);

            // DoorController component on root (requires a Collider on doorGo)
            var doorRootCol = doorGo.AddComponent<BoxCollider>();
            doorRootCol.size = new Vector3(doorW + 0.2f, doorH + 0.2f, 0.4f);
            doorRootCol.center = new Vector3(doorW * 0.5f, doorH * 0.5f, 0f);
            doorRootCol.isTrigger = false;

            var doorCtrl = doorGo.AddComponent<EscapeRoom.Puzzle.DoorController>();
            var doorSO = new SerializedObject(doorCtrl);
            doorSO.FindProperty("isLocked").boolValue           = true;
            doorSO.FindProperty("autoOpenOnUnlock").boolValue   = false;
            doorSO.FindProperty("openEulerAngles").vector3Value = new Vector3(0, -90f, 0);
            doorSO.FindProperty("animationDuration").floatValue = 1.6f;
            doorSO.FindProperty("lockedPrompt").stringValue     = "Security Door (Locked)";
            doorSO.FindProperty("unlockPrompt").stringValue     = "Open Security Door";
            doorSO.FindProperty("openedPrompt").stringValue     = "Security Corridor Open";
            doorSO.FindProperty("hingeTransform").objectReferenceValue = hingeGo.transform;
            doorSO.ApplyModifiedProperties();

            // Make door open trigger Stage2CompleteUI via UnityEvent
            // We wire it up below after stage2UI is available
            var stage2UI = stage2UIGo != null ? stage2UIGo.GetComponent<EscapeRoom.UI.Stage2CompleteUI>() : null;

            // ── Security Card Reader ─────────────────────────────────────────
            // Mounted on the wall to the left of the security door
            var readerGo = new GameObject("SecurityCardReader");
            readerGo.transform.SetParent(envRoot, false);
            readerGo.transform.position = new Vector3(doorX - doorW * .5f - 0.35f, 1.2f, doorZ + 0.05f);

            // Reader body
            var readerBody = EscapeRoomBuilder.CreateProBuilderCube(
                "ReaderBody", readerGo.transform, Vector3.zero,
                new Vector3(0.15f, 0.22f, 0.06f), Quaternion.identity, readerMat);
            readerBody.GetComponent<BoxCollider>().size = new Vector3(0.22f, 0.30f, 0.12f);

            // Indicator LED (small coloured cube)
            var ledGo = EscapeRoomBuilder.CreateProBuilderCube(
                "LED_Indicator", readerGo.transform,
                new Vector3(0f, 0.07f, .04f),
                new Vector3(0.04f, 0.04f, 0.015f), Quaternion.identity, indicMat);
            UnityEngine.Object.DestroyImmediate(ledGo.GetComponent<BoxCollider>());

            // SecurityCardReader component (requires a Collider)
            var rCol = readerGo.AddComponent<BoxCollider>();
            rCol.size = new Vector3(0.3f, 0.4f, 0.3f);
            rCol.isTrigger = true;

            var reader = readerGo.AddComponent<EscapeRoom.Puzzle.SecurityCardReader>();
            var readerSO = new SerializedObject(reader);
            readerSO.FindProperty("requiredItemId").stringValue    = "access_card";
            readerSO.FindProperty("noCardPrompt").stringValue      = "Use Access Card";
            readerSO.FindProperty("hasCardPrompt").stringValue     = "Use Access Card on reader";
            readerSO.FindProperty("activatedPrompt").stringValue   = "ACCESS GRANTED";
            readerSO.FindProperty("linkedDoor").objectReferenceValue = doorCtrl;
            readerSO.FindProperty("indicatorRenderer").objectReferenceValue = ledGo.GetComponent<Renderer>();
            readerSO.FindProperty("idleColor").colorValue          = new Color(0.8f, 0.1f, 0.1f);
            readerSO.FindProperty("activatedColor").colorValue     = new Color(0.1f, 0.8f, 0.2f);
            readerSO.ApplyModifiedProperties();

            // Hook Stage2CompleteUI to door's onDoorOpened event
            if (stage2UI != null)
            {
                var doorCtrlSO = new SerializedObject(doorCtrl);
                var openedEvent = doorCtrlSO.FindProperty("onDoorOpened");
                // Note: We can't easily wire UnityEvents via SerializedObject from editor scripts
                // without reflection. We'll use a helper MonoBehaviour instead.
                doorCtrlSO.ApplyModifiedProperties();

                // Add a simple stage2 trigger helper
                var triggerHelper = doorGo.AddComponent<Stage2DoorOpenTrigger>();
                var helperSO = new SerializedObject(triggerHelper);
                helperSO.FindProperty("stage2UI").objectReferenceValue = stage2UI;
                helperSO.ApplyModifiedProperties();
            }

            // ── Escape zone beyond door ──────────────────────────────────────
            var escapeTrigger = new GameObject("Stage2_ExitTrigger");
            escapeTrigger.transform.SetParent(envRoot, false);
            escapeTrigger.transform.position = new Vector3(doorX, 1f, doorZ + 2.0f);
            var escapeTCol = escapeTrigger.AddComponent<BoxCollider>();
            escapeTCol.isTrigger = true;
            escapeTCol.size = new Vector3(doorW + 0.8f, 2.0f, 1.5f);
            escapeTrigger.AddComponent<EscapeRoom.Core.RoomBannerTrigger>();
            var bannerTSO = new SerializedObject(escapeTrigger.GetComponent<EscapeRoom.Core.RoomBannerTrigger>());
            bannerTSO.FindProperty("roomTitle").stringValue = "SECURITY CORRIDOR";
            bannerTSO.FindProperty("tipText").stringValue   = "The corridor is open. Stage 3 awaits.";
            bannerTSO.ApplyModifiedProperties();
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Room Banner Trigger

        private static void BuildRoomBannerTrigger(Transform envRoot, float rL, float rW)
        {
            var trigGo = new GameObject("StorageRoom_EntryTrigger");
            trigGo.transform.SetParent(envRoot, false);
            trigGo.transform.position = new Vector3(0f, 1f, rL * .5f - 0.6f);
            var col = trigGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(rW - 0.5f, 2.0f, 1.2f);
            var banner = trigGo.AddComponent<EscapeRoom.Core.RoomBannerTrigger>();
            var so = new SerializedObject(banner);
            so.FindProperty("roomTitle").stringValue = "STORAGE ROOM";
            so.FindProperty("tipText").stringValue   = "An old facility storage area. Something is locked away here.";
            so.FindProperty("triggerOnce").boolValue = true;
            so.ApplyModifiedProperties();
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Utilities

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static ProBuilderMesh Cube(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            return EscapeRoomBuilder.CreateProBuilderCube(name, parent, pos, size, Quaternion.identity, mat);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            bool exists = scenes.Exists(s => s.path == scenePath);
            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[StorageRoomSceneBuilder] Added '{scenePath}' to Build Settings.");
            }
        }

        #endregion
    }
}
