using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.Puzzle;
using EscapeRoom.UI;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor build tool to programmatically generate Level 3 (Vault Cellar) scene:
    /// Assets/Scenes/VaultCellar.unity
    ///
    /// Scene includes:
    ///   • Concrete architecture & industrial vault lighting
    ///   • Symbol matrix panel on entrance gate
    ///   • Memory console previewing safe path
    ///   • 5x4 pressure tile floor corridor with alarm reset & teleportation
    ///   • Torn note cipher on desk (IUHH -> FREE)
    ///   • Vault door D3 & vault lock terminal
    ///   • Exit trigger volume leading to final victory screen
    /// </summary>
    public static class VaultSceneBuilder
    {
        public const string VaultScenePath = "Assets/Scenes/VaultCellar.unity";

        [MenuItem("Tools/Escape Room/Build Vault Cellar Scene (Stage 3)", false, 42)]
        public static void BuildVaultScene()
        {
            Debug.Log("<b><color=#337ab7>[VaultSceneBuilder]</color> Building Level 3: Vault Cellar scene...</b>");

            // Ensure Scenes folder exists
            if (!Directory.Exists("Assets/Scenes"))
            {
                Directory.CreateDirectory("Assets/Scenes");
            }

            // Create new scene
            Scene currentScene = EditorSceneManager.GetActiveScene();
            Scene vaultScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Build Architecture & Room Volumes
            BuildArchitecture();

            // 2. Build Lighting
            BuildLighting();

            // 3. Build Player
            GameObject playerGo = BuildPlayer();

            // 4. Build Core Game Managers
            BuildManagers();

            // 5. Build Puzzles & Props
            BuildPuzzlesAndProps();

            // Save Scene
            EditorSceneManager.SaveScene(vaultScene, VaultScenePath);
            Debug.Log($"<color=#5cb85c><b>[VaultSceneBuilder]</b></color> Successfully created Level 3 scene: '{VaultScenePath}'");

            // Update Build Settings
            GuidedEscapeRoomSetup.UpdateBuildSettings();

            // Re-open previous scene if valid
            if (!string.IsNullOrEmpty(currentScene.path) && File.Exists(currentScene.path))
            {
                EditorSceneManager.OpenScene(currentScene.path);
            }
        }

        private static void BuildArchitecture()
        {
            GameObject archParent = new GameObject("_Architecture");

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor_Concrete";
            floor.transform.SetParent(archParent.transform, false);
            floor.transform.position = new Vector3(0f, -0.1f, -10f);
            floor.transform.localScale = new Vector3(8f, 0.2f, 22f);

            // Ceiling
            GameObject ceil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceil.name = "Ceiling_Concrete";
            ceil.transform.SetParent(archParent.transform, false);
            ceil.transform.position = new Vector3(0f, 3.1f, -10f);
            ceil.transform.localScale = new Vector3(8f, 0.2f, 22f);

            // Left Wall
            GameObject leftW = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftW.name = "Wall_Left";
            leftW.transform.SetParent(archParent.transform, false);
            leftW.transform.position = new Vector3(-4.1f, 1.5f, -10f);
            leftW.transform.localScale = new Vector3(0.2f, 3f, 22f);

            // Right Wall
            GameObject rightW = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightW.name = "Wall_Right";
            rightW.transform.SetParent(archParent.transform, false);
            rightW.transform.position = new Vector3(4.1f, 1.5f, -10f);
            rightW.transform.localScale = new Vector3(0.2f, 3f, 22f);

            // Entrance Wall
            GameObject backW = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backW.name = "Wall_Entrance";
            backW.transform.SetParent(archParent.transform, false);
            backW.transform.position = new Vector3(0f, 1.5f, 0.1f);
            backW.transform.localScale = new Vector3(8f, 3f, 0.2f);

            // Back Vault Wall
            GameObject vaultW = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vaultW.name = "Wall_VaultExit";
            vaultW.transform.SetParent(archParent.transform, false);
            vaultW.transform.position = new Vector3(0f, 1.5f, -20.1f);
            vaultW.transform.localScale = new Vector3(8f, 3f, 0.2f);
        }

        private static void BuildLighting()
        {
            GameObject lightParent = new GameObject("_Lighting");

            // Ambient Vault Light
            RenderSettings.ambientLight = new Color(0.10f, 0.14f, 0.20f);

            // Section Lights (Blue / Industrial Red)
            Vector3[] lightPositions = new[]
            {
                new Vector3(0f, 2.6f, -3f),
                new Vector3(0f, 2.6f, -8f),
                new Vector3(0f, 2.6f, -14f),
                new Vector3(0f, 2.6f, -19f)
            };

            for (int i = 0; i < lightPositions.Length; i++)
            {
                GameObject lGo = new GameObject($"PointLight_Sec{i + 1}");
                lGo.transform.SetParent(lightParent.transform, false);
                lGo.transform.position = lightPositions[i];

                Light pLight = lGo.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.range = 10f;
                pLight.intensity = (i == 3) ? 1.5f : 0.9f;
                pLight.color = (i == 3) ? new Color(1.0f, 0.3f, 0.2f) : new Color(0.35f, 0.70f, 1.0f);
            }
        }

        private static GameObject BuildPlayer()
        {
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo == null)
            {
                playerGo = new GameObject("Player");
                playerGo.tag = "Player";
                playerGo.transform.position = new Vector3(0f, 1.6f, -1.5f);

                CharacterController cc = playerGo.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.4f;

                playerGo.AddComponent<PlayerController>();
                playerGo.AddComponent<InteractionSystem>();

                // Camera
                GameObject camGo = new GameObject("PlayerCamera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(playerGo.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);

                Camera cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.AddComponent<PlayerLook>();

                var flashlight = playerGo.AddComponent<FlashlightController>();
                flashlight.EnsureSpotlightSetup();
            }
            return playerGo;
        }

        private static void BuildManagers()
        {
            GameObject managersGo = new GameObject(PuzzleBuilder.GameManagersName);

            managersGo.AddComponent<GameManager>();
            managersGo.AddComponent<PauseManager>();
            managersGo.AddComponent<PlaythroughGameState>();
            managersGo.AddComponent<InputConfig>();
            managersGo.AddComponent<EscapeRoom.Audio.EscapeRoomAudio>();
            managersGo.AddComponent<ObjectiveManager>();
            managersGo.AddComponent<ObjectiveProgressionAdapter>();
            managersGo.AddComponent<ObjectiveHUD>();
            managersGo.AddComponent<BannerUI>();
            managersGo.AddComponent<CipherHostUI>();
            managersGo.AddComponent<StageTimer>();
            managersGo.AddComponent<TimeoutUI>();
            managersGo.AddComponent<EscapeUI>();
            managersGo.AddComponent<PauseUI>();
            managersGo.AddComponent<FeedbackHUD>();
            managersGo.AddComponent<MemoryConsoleUI>();
            managersGo.AddComponent<WordCipherUI>();
        }

        private static void BuildPuzzlesAndProps()
        {
            GameObject propsParent = new GameObject("_PropsAndPuzzles");

            // 1. Entry Security Gate Door
            GameObject gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = "SecurityGate_Door";
            gateGo.transform.SetParent(propsParent.transform, false);
            gateGo.transform.position = new Vector3(0f, 1.2f, -5.5f);
            gateGo.transform.localScale = new Vector3(2.2f, 2.4f, 0.2f);
            DoorController gateDoor = gateGo.AddComponent<DoorController>();

            // 2. Memory Console Preview Terminal (Left Wall)
            GameObject consoleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            consoleGo.name = "MemoryConsole_Terminal";
            consoleGo.transform.SetParent(propsParent.transform, false);
            consoleGo.transform.position = new Vector3(-3.85f, 1.4f, -4.5f);
            consoleGo.transform.localScale = new Vector3(0.2f, 0.8f, 0.8f);

            var consoleTrigger = consoleGo.AddComponent<MemoryConsoleTrigger>();

            // 3. Pressure Tile Floor Corridor (5x4 Grid)
            GameObject tileGridGo = new GameObject("PressureTile_Corridor");
            tileGridGo.transform.SetParent(propsParent.transform, false);
            tileGridGo.transform.position = Vector3.zero;
            tileGridGo.AddComponent<PressureTileGrid>();

            // 4. Desk & Torn Note (Past Tiles)
            GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Vault_Desk";
            desk.transform.SetParent(propsParent.transform, false);
            desk.transform.position = new Vector3(2.8f, 0.45f, -13f);
            desk.transform.localScale = new Vector3(1.4f, 0.9f, 0.9f);

            GameObject noteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noteGo.name = "TornNote_Cipher";
            noteGo.transform.SetParent(desk.transform, false);
            noteGo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            noteGo.transform.localScale = new Vector3(0.35f, 0.02f, 0.28f);

            var noteClue = noteGo.AddComponent<ClueInteractable>();
            noteClue.ClueTitle = "Torn Note Cipher";
            noteClue.BodyExplanation = "Daniel written: 'IUHH' (Each letter pushed +3 levels. Pull them back to find the key word: FREE).";

            // 5. Vault Door D3 & Lock Terminal
            GameObject vaultDoorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vaultDoorGo.name = "Vault_ExitDoor_D3";
            vaultDoorGo.transform.SetParent(propsParent.transform, false);
            vaultDoorGo.transform.position = new Vector3(0f, 1.2f, -19.9f);
            vaultDoorGo.transform.localScale = new Vector3(2.4f, 2.4f, 0.25f);
            DoorController vaultDoor = vaultDoorGo.AddComponent<DoorController>();

            GameObject vaultLockGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vaultLockGo.name = "Vault_CipherTerminal";
            vaultLockGo.transform.SetParent(propsParent.transform, false);
            vaultLockGo.transform.position = new Vector3(1.6f, 1.3f, -19.75f);
            vaultLockGo.transform.localScale = new Vector3(0.35f, 0.45f, 0.12f);

            var cipherTerminal = vaultLockGo.AddComponent<VaultLockTerminal>();
            cipherTerminal.Configure(vaultDoor);

            // 6. Final Escape Trigger Volume
            GameObject exitTrig = new GameObject("EscapeTrigger_Vault");
            exitTrig.transform.SetParent(propsParent.transform, false);
            exitTrig.transform.position = new Vector3(0f, 1.2f, -21.2f);
            BoxCollider col = exitTrig.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(4f, 3f, 3f);
            exitTrig.AddComponent<EscapeTrigger>();
        }
    }

    /// <summary>
    /// Interactive trigger component for the Memory Console on the wall.
    /// </summary>
    public class MemoryConsoleTrigger : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => InputConfig.FormatPrompt("Inspect Memory Console");
        public bool CanInteract => gameObject.activeInHierarchy;

        public void Interact()
        {
            if (MemoryConsoleUI.Instance != null)
            {
                MemoryConsoleUI.Instance.Open();
            }
        }
    }

    /// <summary>
    /// Interactive trigger component for the Vault Lock Terminal.
    /// </summary>
    public class VaultLockTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorController linkedVaultDoor;

        public void Configure(DoorController door)
        {
            linkedVaultDoor = door;
        }

        public string InteractionPrompt => InputConfig.FormatPrompt("Use Vault Cipher Terminal");
        public bool CanInteract => gameObject.activeInHierarchy;

        public void Interact()
        {
            if (WordCipherUI.Instance != null)
            {
                WordCipherUI.Instance.Open(linkedVaultDoor);
            }
        }
    }
}
