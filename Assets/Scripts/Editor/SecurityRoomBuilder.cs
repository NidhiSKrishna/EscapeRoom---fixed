using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.Interaction;
using EscapeRoom.UI;
using EscapeRoom.Stage3;
using EscapeRoom.Stage3.Puzzles;
using EscapeRoom.Stage3.UI;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Automated scene builder for Stage 3 Security Control Room.
    /// Creates Assets/Scenes/SecurityControlRoom.unity, builds room geometry, furniture, lights,
    /// materials, player spawn, systems, and puzzle GameObjects.
    /// Menu item: Tools > Escape Room > Build Security Control Room
    /// </summary>
    public static class SecurityRoomBuilder
    {
        public const string Stage3ScenePath =
            "Assets/Scenes/SecurityControlRoom.unity";

        public const string Stage3MaterialsFolder =
            "Assets/Art/Materials/Stage3";

        [MenuItem(
            "Tools/Escape Room/Build Security Control Room",
            false,
            40)]
        public static void BuildSecurityControlRoomMenu()
        {
            Debug.Log(
                "<b><color=#337ab7>[SecurityRoomBuilder]</color> " +
                "Building Stage 3 Security Control Room...</b>");

            // 1. Ensure folders exist
            EnsureDirectories();

            // 2. Create and open scene
            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            // 3. Create materials
            MaterialSet materials = CreateMaterials();

            // 4. Create root hierarchy
            GameObject stage3Root =
                new GameObject("[Stage 3 Security Room]");

            // 5. Build room architecture
            BuildArchitecture(
                stage3Root.transform,
                materials);

            // 6. Build furniture and consoles
            BuildFurniture(
                stage3Root.transform,
                materials);

            // 7. Setup lights
            SetupLighting(
                stage3Root.transform);

            // 8. Build Stage 3 managers
            GameObject managersGo =
                BuildCoreManagers(
                    stage3Root.transform);

            // 9. Build puzzles
            BuildPuzzles(
                stage3Root.transform,
                materials,
                managersGo);

            // 10. Build player and UI
            BuildPlayerAndUI(
                stage3Root.transform);

            // 11. Save scene
            EditorSceneManager.SaveScene(
                scene,
                Stage3ScenePath);

            AddSceneToBuildSettings(
                Stage3ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "<b><color=#5cb85c>" +
                "[SecurityRoomBuilder] Stage 3 Security Control Room " +
                "built and saved successfully!</color></b>");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "Scenes");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Art"))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "Art");
            }

            if (!AssetDatabase.IsValidFolder(
                "Assets/Art/Materials"))
            {
                AssetDatabase.CreateFolder(
                    "Assets/Art",
                    "Materials");
            }

            if (!AssetDatabase.IsValidFolder(
                Stage3MaterialsFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/Art/Materials",
                    "Stage3");
            }
        }

        // =========================================================
        // MATERIAL SET
        // =========================================================

        private class MaterialSet
        {
            public Material Floor;
            public Material Wall;
            public Material Ceiling;
            public Material Console;
            public Material Server;
            public Material Monitor;
            public Material Panel;
            public Material Door;

            public Material RedButton;
            public Material BlueButton;
            public Material GreenButton;
            public Material YellowButton;

            public Material TileNeutral;
            public Material TileActive;
            public Material TileCorrect;
            public Material TileError;
        }

        private static MaterialSet CreateMaterials()
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            var set = new MaterialSet
            {
                Floor = CreateOrLoadMat(
                    "M_S3_Floor",
                    shader,
                    new Color(
                        0.12f,
                        0.14f,
                        0.18f),
                    0.7f,
                    0.4f),

                Wall = CreateOrLoadMat(
                    "M_S3_Wall",
                    shader,
                    new Color(
                        0.20f,
                        0.22f,
                        0.26f),
                    0.3f,
                    0.6f),

                Ceiling = CreateOrLoadMat(
                    "M_S3_Ceiling",
                    shader,
                    new Color(
                        0.08f,
                        0.09f,
                        0.12f),
                    0.1f,
                    0.8f),

                Console = CreateOrLoadMat(
                    "M_S3_Console",
                    shader,
                    new Color(
                        0.15f,
                        0.18f,
                        0.24f),
                    0.8f,
                    0.3f),

                Server = CreateOrLoadMat(
                    "M_S3_Server",
                    shader,
                    new Color(
                        0.10f,
                        0.10f,
                        0.12f),
                    0.9f,
                    0.2f),

                Monitor = CreateOrLoadMat(
                    "M_S3_Monitor",
                    shader,
                    new Color(
                        0.05f,
                        0.15f,
                        0.20f),
                    0.5f,
                    0.2f,
                    new Color(
                        0.1f,
                        0.4f,
                        0.5f) * 1.5f),

                Panel = CreateOrLoadMat(
                    "M_S3_Panel",
                    shader,
                    new Color(
                        0.18f,
                        0.20f,
                        0.22f),
                    0.5f,
                    0.5f),

                Door = CreateOrLoadMat(
                    "M_S3_Door",
                    shader,
                    new Color(
                        0.25f,
                        0.28f,
                        0.32f),
                    0.8f,
                    0.3f),

                RedButton = CreateOrLoadMat(
                    "M_S3_Red",
                    shader,
                    new Color(
                        0.8f,
                        0.15f,
                        0.15f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.4f,
                        0.05f,
                        0.05f)),

                BlueButton = CreateOrLoadMat(
                    "M_S3_Blue",
                    shader,
                    new Color(
                        0.15f,
                        0.4f,
                        0.9f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.05f,
                        0.1f,
                        0.4f)),

                GreenButton = CreateOrLoadMat(
                    "M_S3_Green",
                    shader,
                    new Color(
                        0.15f,
                        0.8f,
                        0.25f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.05f,
                        0.4f,
                        0.1f)),

                YellowButton = CreateOrLoadMat(
                    "M_S3_Yellow",
                    shader,
                    new Color(
                        0.9f,
                        0.8f,
                        0.15f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.4f,
                        0.35f,
                        0.05f)),

                TileNeutral = CreateOrLoadMat(
                    "M_S3_TileNeutral",
                    shader,
                    new Color(
                        0.22f,
                        0.25f,
                        0.30f),
                    0.6f,
                    0.4f),

                TileActive = CreateOrLoadMat(
                    "M_S3_TileActive",
                    shader,
                    new Color(
                        0.95f,
                        0.85f,
                        0.35f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.95f,
                        0.85f,
                        0.35f) * 2f),

                TileCorrect = CreateOrLoadMat(
                    "M_S3_TileCorrect",
                    shader,
                    new Color(
                        0.35f,
                        0.95f,
                        0.35f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.35f,
                        0.95f,
                        0.35f) * 2f),

                TileError = CreateOrLoadMat(
                    "M_S3_TileError",
                    shader,
                    new Color(
                        0.95f,
                        0.25f,
                        0.25f),
                    0.5f,
                    0.3f,
                    new Color(
                        0.95f,
                        0.25f,
                        0.25f) * 2f)
            };

            return set;
        }

        private static Material CreateOrLoadMat(
            string name,
            Shader shader,
            Color albedo,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            string path =
                $"{Stage3MaterialsFolder}/{name}.mat";

            Material mat =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);

            if (mat == null)
            {
                mat = new Material(shader);

                AssetDatabase.CreateAsset(
                    mat,
                    path);
            }

            mat.color = albedo;

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat(
                    "_Metallic",
                    metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat(
                    "_Smoothness",
                    smoothness);
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor(
                        "_EmissionColor",
                        emission.Value);
                }
            }

            return mat;
        }

        // =========================================================
        // ARCHITECTURE
        // =========================================================

        private static void BuildArchitecture(
            Transform parent,
            MaterialSet mats)
        {
            GameObject roomGo =
                new GameObject("RoomArchitecture");

            roomGo.transform.SetParent(
                parent,
                false);

            // Floor
            GameObject floor =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            floor.name = "Floor";

            floor.transform.SetParent(
                roomGo.transform,
                false);

            floor.transform.position =
                new Vector3(
                    0f,
                    -0.25f,
                    0f);

            floor.transform.localScale =
                new Vector3(
                    10f,
                    0.5f,
                    8f);

            floor.GetComponent<MeshRenderer>().material =
                mats.Floor;

            // Ceiling
            GameObject ceiling =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            ceiling.name = "Ceiling";

            ceiling.transform.SetParent(
                roomGo.transform,
                false);

            ceiling.transform.position =
                new Vector3(
                    0f,
                    3.75f,
                    0f);

            ceiling.transform.localScale =
                new Vector3(
                    10f,
                    0.5f,
                    8f);

            ceiling.GetComponent<MeshRenderer>().material =
                mats.Ceiling;

            // North Wall
            GameObject nWall =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            nWall.name = "NorthWall";

            nWall.transform.SetParent(
                roomGo.transform,
                false);

            nWall.transform.position =
                new Vector3(
                    0f,
                    1.75f,
                    4.25f);

            nWall.transform.localScale =
                new Vector3(
                    10.5f,
                    3.5f,
                    0.5f);

            nWall.GetComponent<MeshRenderer>().material =
                mats.Wall;

            // South Wall
            GameObject sWall =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            sWall.name = "SouthWall";

            sWall.transform.SetParent(
                roomGo.transform,
                false);

            sWall.transform.position =
                new Vector3(
                    0f,
                    1.75f,
                    -4.25f);

            sWall.transform.localScale =
                new Vector3(
                    10.5f,
                    3.5f,
                    0.5f);

            sWall.GetComponent<MeshRenderer>().material =
                mats.Wall;

            // West Wall
            GameObject wWall =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            wWall.name = "WestWall";

            wWall.transform.SetParent(
                roomGo.transform,
                false);

            wWall.transform.position =
                new Vector3(
                    -5.25f,
                    1.75f,
                    0f);

            wWall.transform.localScale =
                new Vector3(
                    0.5f,
                    3.5f,
                    8.5f);

            wWall.GetComponent<MeshRenderer>().material =
                mats.Wall;

            // East Wall - North
            GameObject eWallN =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            eWallN.name = "EastWall_North";

            eWallN.transform.SetParent(
                roomGo.transform,
                false);

            eWallN.transform.position =
                new Vector3(
                    5.25f,
                    1.75f,
                    2.75f);

            eWallN.transform.localScale =
                new Vector3(
                    0.5f,
                    3.5f,
                    3.0f);

            eWallN.GetComponent<MeshRenderer>().material =
                mats.Wall;

            // East Wall - South
            GameObject eWallS =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            eWallS.name = "EastWall_South";

            eWallS.transform.SetParent(
                roomGo.transform,
                false);

            eWallS.transform.position =
                new Vector3(
                    5.25f,
                    1.75f,
                    -2.75f);

            eWallS.transform.localScale =
                new Vector3(
                    0.5f,
                    3.5f,
                    3.0f);

            eWallS.GetComponent<MeshRenderer>().material =
                mats.Wall;

            // East Wall Lintel
            GameObject eWallL =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            eWallL.name = "EastWall_Lintel";

            eWallL.transform.SetParent(
                roomGo.transform,
                false);

            eWallL.transform.position =
                new Vector3(
                    5.25f,
                    3.0f,
                    0f);

            eWallL.transform.localScale =
                new Vector3(
                    0.5f,
                    1.0f,
                    2.5f);

            eWallL.GetComponent<MeshRenderer>().material =
                mats.Wall;
        }

        // =========================================================
        // FURNITURE
        // =========================================================

        private static void BuildFurniture(
            Transform parent,
            MaterialSet mats)
        {
            GameObject furnGo =
                new GameObject("FurnitureConsoles");

            furnGo.transform.SetParent(
                parent,
                false);

            // Main Desk Console
            GameObject desk =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            desk.name = "MainControlConsole";

            desk.transform.SetParent(
                furnGo.transform,
                false);

            desk.transform.position =
                new Vector3(
                    0f,
                    0.45f,
                    2.8f);

            desk.transform.localScale =
                new Vector3(
                    5f,
                    0.9f,
                    1.2f);

            desk.GetComponent<MeshRenderer>().material =
                mats.Console;

            // Server Rack 1
            GameObject server1 =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            server1.name = "ServerRack_1";

            server1.transform.SetParent(
                furnGo.transform,
                false);

            server1.transform.position =
                new Vector3(
                    -4.5f,
                    1.4f,
                    2.5f);

            server1.transform.localScale =
                new Vector3(
                    0.8f,
                    2.8f,
                    1.2f);

            server1.GetComponent<MeshRenderer>().material =
                mats.Server;

            // Server Rack 2
            GameObject server2 =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            server2.name = "ServerRack_2";

            server2.transform.SetParent(
                furnGo.transform,
                false);

            server2.transform.position =
                new Vector3(
                    -4.5f,
                    1.4f,
                    -2.5f);

            server2.transform.localScale =
                new Vector3(
                    0.8f,
                    2.8f,
                    1.2f);

            server2.GetComponent<MeshRenderer>().material =
                mats.Server;

            // Wall Monitors
            for (int i = -1; i <= 1; i++)
            {
                GameObject mon =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                mon.name =
                    $"MonitorScreen_{i + 2}";

                mon.transform.SetParent(
                    furnGo.transform,
                    false);

                mon.transform.position =
                    new Vector3(
                        i * 1.8f,
                        2.0f,
                        3.95f);

                mon.transform.localScale =
                    new Vector3(
                        1.4f,
                        0.9f,
                        0.1f);

                mon.GetComponent<MeshRenderer>().material =
                    mats.Monitor;

                var monUI =
                    mon.AddComponent<MonitorDisplayUI>();

                // ConfigureMonitor removed because the current
                // MonitorDisplayUI does not expose that method.
                // The monitor remains as a visual security display.
            }
        }

        // =========================================================
        // LIGHTING
        // =========================================================

        private static void SetupLighting(
            Transform parent)
        {
            GameObject lightGo =
                new GameObject(
                    "EnvironmentLighting");

            lightGo.transform.SetParent(
                parent,
                false);

            // Cyan ambient light
            GameObject cyanLight =
                new GameObject(
                    "OverheadCyanLight");

            cyanLight.transform.SetParent(
                lightGo.transform,
                false);

            cyanLight.transform.position =
                new Vector3(
                    0f,
                    3.0f,
                    0f);

            Light l1 =
                cyanLight.AddComponent<Light>();

            l1.type = LightType.Point;
            l1.color =
                new Color(
                    0.2f,
                    0.7f,
                    1.0f);

            l1.intensity = 2.5f;
            l1.range = 14f;

            // Emergency red light
            GameObject alarmLight =
                new GameObject(
                    "EmergencyAlarmLight");

            alarmLight.transform.SetParent(
                lightGo.transform,
                false);

            alarmLight.transform.position =
                new Vector3(
                    3.5f,
                    2.8f,
                    3.5f);

            Light l2 =
                alarmLight.AddComponent<Light>();

            l2.type = LightType.Point;

            l2.color =
                new Color(
                    1.0f,
                    0.2f,
                    0.2f);

            l2.intensity = 0f;
            l2.range = 12f;

            // Grid light
            GameObject gridLight =
                new GameObject(
                    "GridSpotlight");

            gridLight.transform.SetParent(
                lightGo.transform,
                false);

            gridLight.transform.position =
                new Vector3(
                    0f,
                    3.2f,
                    -1.0f);

            Light l3 =
                gridLight.AddComponent<Light>();

            l3.type = LightType.Point;

            l3.color =
                new Color(
                    0.8f,
                    0.9f,
                    1.0f);

            l3.intensity = 1.8f;
            l3.range = 10f;
        }

        // =========================================================
        // CORE MANAGERS
        // =========================================================

        private static GameObject BuildCoreManagers(
            Transform parent)
        {
            GameObject managersGo =
                new GameObject(
                    "[Stage 3 Managers]");

            managersGo.transform.SetParent(
                parent,
                false);

            // Game Manager
            if (GameManager.Instance == null)
            {
                var gmGo =
                    new GameObject(
                        "GameManager");

                gmGo.transform.SetParent(
                    managersGo.transform,
                    false);

                gmGo.AddComponent<GameManager>();
            }

            // Pause Manager
            if (PauseManager.Instance == null)
            {
                var pmGo =
                    new GameObject(
                        "PauseManager");

                pmGo.transform.SetParent(
                    managersGo.transform,
                    false);

                pmGo.AddComponent<PauseManager>();
            }

            // Objective Manager
            if (ObjectiveManager.Instance == null)
            {
                var omGo =
                    new GameObject(
                        "ObjectiveManager");

                omGo.transform.SetParent(
                    managersGo.transform,
                    false);

                omGo.AddComponent<ObjectiveManager>();
            }

            // Stage 3 Audio Manager
            var audioGo =
                new GameObject(
                    "Stage3AudioManager");

            audioGo.transform.SetParent(
                managersGo.transform,
                false);

            audioGo.AddComponent<Stage3AudioManager>();

            // Stage 3 Timer
            var timerGo =
                new GameObject(
                    "Stage3Timer");

            timerGo.transform.SetParent(
                managersGo.transform,
                false);

            timerGo.AddComponent<Stage3Timer>();

            // Stage 3 Cipher Helper
            var cipherGo =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            cipherGo.name =
                "Stage3CipherTerminal";

            cipherGo.transform.SetParent(
                parent,
                false);

            cipherGo.transform.position =
                new Vector3(
                    -2.2f,
                    1.1f,
                    2.3f);

            cipherGo.transform.localScale =
                new Vector3(
                    0.6f,
                    0.4f,
                    0.4f);

            cipherGo.AddComponent<Stage3CipherHelper>();

            // Stage 3 HUD
            var hudGo =
                new GameObject(
                    "Stage3HUD");

            hudGo.transform.SetParent(
                managersGo.transform,
                false);

            hudGo.AddComponent<Stage3HUD>();

            // Security Room Manager
            var roomMgrGo =
                new GameObject(
                    "SecurityRoomManager");

            roomMgrGo.transform.SetParent(
                managersGo.transform,
                false);

            roomMgrGo.AddComponent<SecurityRoomManager>();

            return managersGo;
        }

        // =========================================================
        // PUZZLES
        // =========================================================

        private static void BuildPuzzles(
            Transform parent,
            MaterialSet mats,
            GameObject managersGo)
        {
            GameObject puzzleRoot =
                new GameObject("[Puzzles]");

            puzzleRoot.transform.SetParent(
                parent,
                false);

            // =====================================================
            // 1. COLOR MEMORY PUZZLE
            // =====================================================

            GameObject colorPuzGo =
                new GameObject(
                    "ColorMemoryPuzzle");

            colorPuzGo.transform.SetParent(
                puzzleRoot.transform,
                false);

            var colorPuz =
                colorPuzGo.AddComponent<ColorMemoryPuzzle>();

            // Backing Panel
            GameObject colorPanel =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            colorPanel.name =
                "ColorPanelBacking";

            colorPanel.transform.SetParent(
                colorPuzGo.transform,
                false);

            colorPanel.transform.position =
                new Vector3(
                    -4.95f,
                    1.6f,
                    0f);

            colorPanel.transform.localScale =
                new Vector3(
                    0.1f,
                    1.4f,
                    1.4f);

            colorPanel.GetComponent<MeshRenderer>().material =
                mats.Panel;

            // IMPORTANT:
            // ColorType is declared at namespace level.
            ColorType[] cTypes =
                new ColorType[]
                {
                    ColorType.Red,
                    ColorType.Blue,
                    ColorType.Green,
                    ColorType.Yellow
                };

            Material[] cMats =
                new Material[]
                {
                    mats.RedButton,
                    mats.BlueButton,
                    mats.GreenButton,
                    mats.YellowButton
                };

            Vector3[] cPos =
                new Vector3[]
                {
                    new Vector3(
                        -4.88f,
                        1.9f,
                        -0.3f),

                    new Vector3(
                        -4.88f,
                        1.9f,
                        0.3f),

                    new Vector3(
                        -4.88f,
                        1.3f,
                        -0.3f),

                    new Vector3(
                        -4.88f,
                        1.3f,
                        0.3f)
                };

            ColorButton[] btnRefs =
                new ColorButton[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject btnGo =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                btnGo.name =
                    $"ColorButton_{cTypes[i]}";

                btnGo.transform.SetParent(
                    colorPuzGo.transform,
                    false);

                btnGo.transform.position =
                    cPos[i];

                btnGo.transform.localScale =
                    new Vector3(
                        0.15f,
                        0.45f,
                        0.45f);

                btnGo.GetComponent<MeshRenderer>().material =
                    cMats[i];

                ColorButton btnScript =
                    btnGo.AddComponent<ColorButton>();

                btnScript.SetColorType(
                    cTypes[i]);

                btnRefs[i] =
                    btnScript;
            }

            colorPuz.ConfigureButtons(
                btnRefs);


            // =====================================================
            // 2. SECURITY LOCKDOWN PANEL
            // =====================================================

            GameObject lockPanelGo =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            lockPanelGo.name =
                "SecurityLockdownEmergencySwitch";

            lockPanelGo.transform.SetParent(
                puzzleRoot.transform,
                false);

            lockPanelGo.transform.position =
                new Vector3(
                    3.5f,
                    1.4f,
                    3.9f);

            lockPanelGo.transform.localScale =
                new Vector3(
                    0.6f,
                    0.6f,
                    0.2f);

            lockPanelGo.GetComponent<MeshRenderer>().material =
                mats.RedButton;

            lockPanelGo.AddComponent<
                SecurityLockdownEvent>();


            // =====================================================
            // 3. MEMORY PATH PUZZLE
            // =====================================================

            GameObject pathPuzGo =
                new GameObject(
                    "MemoryPathPuzzle");

            pathPuzGo.transform.SetParent(
                puzzleRoot.transform,
                false);

            var pathPuz =
                pathPuzGo.AddComponent<
                    MemoryPathPuzzle>();

            GameObject gridRoot =
                new GameObject(
                    "PathGrid");

            gridRoot.transform.SetParent(
                pathPuzGo.transform,
                false);

            // Local 2D array for grid organization.
            PathTile[,] tileGrid =
                new PathTile[4, 4];

            // MemoryPathPuzzle expects a 1D array.
            PathTile[] tileArray =
                new PathTile[16];

            float tileSize = 0.9f;
            float spacing = 0.1f;

            Vector3 gridOrigin =
                new Vector3(
                    -1.65f,
                    0.01f,
                    -1.65f);

            int tileIndex = 0;

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    GameObject tileGo =
                        GameObject.CreatePrimitive(
                            PrimitiveType.Cube);

                    tileGo.name =
                        $"PathTile_{r}_{c}";

                    tileGo.transform.SetParent(
                        gridRoot.transform,
                        false);

                    tileGo.transform.position =
                        gridOrigin +
                        new Vector3(
                            c * (tileSize + spacing),
                            0f,
                            r * (tileSize + spacing));

                    tileGo.transform.localScale =
                        new Vector3(
                            tileSize,
                            0.05f,
                            tileSize);

                    tileGo.GetComponent<MeshRenderer>().material =
                        mats.TileNeutral;

                    PathTile tileScript =
                        tileGo.AddComponent<
                            PathTile>();

                    // Current PathTile API:
                    // ConfigureTile(int x, int y)
                    tileScript.ConfigureTile(
                        r,
                        c);

                    tileGrid[r, c] =
                        tileScript;

                    tileArray[tileIndex] =
                        tileScript;

                    tileIndex++;
                }
            }

            // Current MemoryPathPuzzle API:
            // ConfigureGrid(PathTile[])
            pathPuz.ConfigureGrid(
                tileArray);


            // =====================================================
            // 4. SYMBOL MAPPING PUZZLE
            // =====================================================

            GameObject symPuzGo =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            symPuzGo.name =
                "SymbolMappingTerminal";

            symPuzGo.transform.SetParent(
                puzzleRoot.transform,
                false);

            symPuzGo.transform.position =
                new Vector3(
                    0f,
                    1.1f,
                    2.3f);

            symPuzGo.transform.localScale =
                new Vector3(
                    0.8f,
                    0.4f,
                    0.5f);

            symPuzGo.GetComponent<MeshRenderer>().material =
                mats.Console;

            symPuzGo.AddComponent<
                SymbolMappingPuzzle>();

            // Symbol puzzle display
            GameObject symTextGo =
                new GameObject(
                    "ScreenDisplayMesh");

            symTextGo.transform.SetParent(
                symPuzGo.transform,
                false);

            symTextGo.transform.localPosition =
                new Vector3(
                    0f,
                    0.6f,
                    0f);

            symTextGo.transform.localRotation =
                Quaternion.Euler(
                    30f,
                    0f,
                    0f);

            var tm =
                symTextGo.AddComponent<
                    TextMesh>();

            tm.fontSize = 24;
            tm.alignment =
                TextAlignment.Center;

            tm.anchor =
                TextAnchor.MiddleCenter;

            symTextGo.AddComponent<
                SymbolPuzzleUI>();


            // =====================================================
            // 5. SECURITY EXIT TRIGGER
            // =====================================================

            GameObject exitGo =
                new GameObject(
                    "Stage3ExitTrigger");

            exitGo.transform.SetParent(
                puzzleRoot.transform,
                false);

            exitGo.transform.position =
                new Vector3(
                    5.0f,
                    1.0f,
                    0.0f);

            var boxCol =
                exitGo.AddComponent<
                    BoxCollider>();

            boxCol.isTrigger = true;

            boxCol.size =
                new Vector3(
                    1.5f,
                    2.5f,
                    2.0f);

            exitGo.AddComponent<
                Stage3ExitTrigger>();
        }

        // =========================================================
        // PLAYER + UI
        // =========================================================

        private static void BuildPlayerAndUI(
            Transform parent)
        {
            // Feedback HUD
            if (FeedbackHUD.Instance == null)
            {
                var fbGo =
                    new GameObject(
                        "FeedbackHUD");

                fbGo.AddComponent<
                    FeedbackHUD>();
            }

            // Banner UI
            if (BannerUI.Instance == null)
            {
                var bnGo =
                    new GameObject(
                        "BannerUI");

                bnGo.AddComponent<
                    BannerUI>();
            }

            // Objective HUD
            if (ObjectiveHUD.Instance == null)
            {
                var ohGo =
                    new GameObject(
                        "ObjectiveHUD");

                ohGo.AddComponent<
                    ObjectiveHUD>();
            }

            // Escape UI
            if (EscapeUI.Instance == null)
            {
                var escGo =
                    new GameObject(
                        "EscapeUI");

                escGo.AddComponent<
                    EscapeUI>();
            }

            // Player spawn
            Vector3 spawnPos =
                new Vector3(
                    3.5f,
                    0.05f,
                    -2.5f);

            Quaternion spawnRot =
                Quaternion.Euler(
                    0f,
                    -90f,
                    0f);

            PlayerBuilder.BuildPlayer(
                spawnPos,
                spawnRot,
                true);
        }

        // =========================================================
        // BUILD SETTINGS
        // =========================================================

        private static void AddSceneToBuildSettings(
            string scenePath)
        {
            var scenes =
                EditorBuildSettings.scenes;

            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                    return;
            }

            var newScenes =
                new EditorBuildSettingsScene[
                    scenes.Length + 1];

            System.Array.Copy(
                scenes,
                newScenes,
                scenes.Length);

            newScenes[scenes.Length] =
                new EditorBuildSettingsScene(
                    scenePath,
                    true);

            EditorBuildSettings.scenes =
                newScenes;
        }
    }
}