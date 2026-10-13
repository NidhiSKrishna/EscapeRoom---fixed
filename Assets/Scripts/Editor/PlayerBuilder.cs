using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EscapeRoom.Player;
using EscapeRoom.Interaction;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor utility for constructing the prototype first-person Player hierarchy and test interactables.
    /// Menu items:
    /// - Tools > Escape Room > Build Prototype Player
    /// - Tools > Escape Room > Create Test Interactable
    /// Target player hierarchy:
    /// Player
    /// └── PlayerCamera
    /// </summary>
    public static class PlayerBuilder
    {
        public const string PlayerGameObjectName = "Player";
        public const string PlayerCameraGameObjectName = "PlayerCamera";
        public const string TestInteractableName = "TestInteractable";

        // Safe starting location inside the 12m (Z) x 8m (X) x 3.5m (Y) prototype room
        // Center along X (0m), slightly above floor (Y = 0.05m), toward the back (Z = -2.5m)
        // Facing forward (Quaternion.identity) directly toward the exit doorway at Z = +6.1m
        public static readonly Vector3 DefaultSpawnPosition = new Vector3(0f, 0.05f, -2.5f);

        [MenuItem("Tools/Escape Room/Build Prototype Player", false, 11)]
        public static GameObject BuildPrototypePlayerMenu()
        {
            return BuildPlayer(DefaultSpawnPosition, Quaternion.identity);
        }

        [MenuItem("Tools/Escape Room/Create Test Interactable", false, 12)]
        public static GameObject CreateTestInteractableMenu()
        {
            return BuildTestInteractable();
        }

        /// <summary>
        /// Instantiates or rebuilds the Player hierarchy at the designated spawn point.
        /// Prevents duplicates if a Player already exists in the active scene.
        /// </summary>
        public static GameObject BuildPlayer(Vector3 spawnPosition, Quaternion spawnRotation, bool forceRebuild = false)
        {
            // 1. Detect existing Player to prevent duplicates
            GameObject existingPlayer = GameObject.Find(PlayerGameObjectName);
            if (existingPlayer != null && !forceRebuild)
            {
                if (existingPlayer.GetComponent<InventorySystem>() == null)
                {
                    existingPlayer.AddComponent<InventorySystem>();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    Debug.Log("[PlayerBuilder] Attached missing InventorySystem to existing Player.");
                }

                Debug.LogWarning($"<color=#f0ad4e><b>[PlayerBuilder]</b></color> '{PlayerGameObjectName}' already exists in the active scene at {existingPlayer.transform.position}. Selecting existing Player to avoid duplicate creation.");
                Selection.activeGameObject = existingPlayer;
                EditorGUIUtility.PingObject(existingPlayer);
                return existingPlayer;
            }

            if (existingPlayer != null && forceRebuild)
            {
                Undo.DestroyObjectImmediate(existingPlayer);
                Debug.Log($"[PlayerBuilder] Replacing existing '{PlayerGameObjectName}' hierarchy.");
            }

            // 2. Create root Player GameObject
            GameObject playerGo = new GameObject(PlayerGameObjectName);
            playerGo.transform.position = spawnPosition;
            playerGo.transform.rotation = spawnRotation;
            Undo.RegisterCreatedObjectUndo(playerGo, "Build Prototype Player");

            // 3. Configure CharacterController (Standard human dimensions in meters)
            CharacterController cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.80f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 0.90f, 0f); // Pivot at player feet (Y = 0)
            cc.stepOffset = 0.30f;
            cc.slopeLimit = 45.0f;
            cc.minMoveDistance = 0.001f;

            // 4. Configure PlayerController
            PlayerController pc = playerGo.AddComponent<PlayerController>();
            pc.MoveSpeed = 4.0f;
            pc.Gravity = -19.62f;

            // 5. Create child PlayerCamera
            GameObject camGo = new GameObject(PlayerCameraGameObjectName);
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f); // 1.65m human eye level
            camGo.transform.localRotation = Quaternion.identity;

            Camera cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 70.0f;
            cam.nearClipPlane = 0.05f; // Prevent wall clipping up-close
            cam.farClipPlane = 100.0f;

            // Ensure AudioListener is attached to the player's eyes
            camGo.AddComponent<AudioListener>();

            // 6. Configure PlayerLook on Player
            PlayerLook pl = playerGo.AddComponent<PlayerLook>();
            pl.PlayerCamera = camGo.transform;
            pl.SensitivityX = 0.12f;
            pl.SensitivityY = 0.12f;
            pl.CameraHeight = 1.65f;

            // 7. Configure InteractionSystem on Player
            InteractionSystem isys = playerGo.AddComponent<InteractionSystem>();
            isys.PlayerCamera = cam;
            isys.InteractionRange = 3.0f;

            // 8. Configure InventorySystem on Player
            InventorySystem inv = playerGo.AddComponent<InventorySystem>();
            inv.AllowDuplicates = false;

            // 9. Disable any standalone Scene cameras to avoid rendering conflicts and duplicate AudioListeners
            DisableStandaloneSceneCameras(camGo);

            // 10. Register undo, mark scene dirty, and highlight new Player
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = playerGo;
            EditorGUIUtility.PingObject(playerGo);

            Debug.Log($"<color=#5cb85c><b>[PlayerBuilder]</b></color> Prototype Player created successfully at {spawnPosition}. " +
                      $"Camera height: 1.65m | CharacterController: 1.80m H x 0.38m R | Interaction Range: {isys.InteractionRange}m.");

            return playerGo;
        }

        /// <summary>
        /// Instantiates the prototype TestInteractable object inside the room on the table.
        /// </summary>
        public static GameObject BuildTestInteractable()
        {
            GameObject existing = GameObject.Find(TestInteractableName);
            if (existing != null)
            {
                Debug.LogWarning($"[PlayerBuilder] '{TestInteractableName}' already exists at {existing.transform.position}. Selecting existing object.");
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return existing;
            }

            // Positioned prominently on the table at X = 1.5, Z = -1.0, Y = 1.025 (table surface is at Y = 0.85)
            Vector3 pos = new Vector3(1.50f, 1.025f, -1.00f);
            Vector3 size = new Vector3(0.35f, 0.35f, 0.35f);

            GameObject propsContainer = GameObject.Find("Props");
            Transform parent = propsContainer != null ? propsContainer.transform : null;

            Material mat = EscapeRoomBuilder.GetOrCreateMaterial("M_Proto_Interactable", new Color(0.96f, 0.65f, 0.12f), 0.35f, 0.10f);

            var pb = EscapeRoomBuilder.CreateProBuilderCube(TestInteractableName, parent, pos, size, Quaternion.identity, mat);
            pb.gameObject.AddComponent<TestInteractable>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = pb.gameObject;
            EditorGUIUtility.PingObject(pb.gameObject);

            Debug.Log($"<color=#5cb85c><b>[PlayerBuilder]</b></color> Prototype '{TestInteractableName}' created on the table at {pos}.");
            return pb.gameObject;
        }

        private static void DisableStandaloneSceneCameras(GameObject activePlayerCam)
        {
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam.gameObject != activePlayerCam && !cam.transform.IsChildOf(activePlayerCam.transform.parent))
                {
                    Debug.Log($"[PlayerBuilder] Disabling standalone camera '{cam.name}' to prioritize '{PlayerCameraGameObjectName}'.");
                    Undo.RecordObject(cam.gameObject, "Disable Standalone Scene Camera");
                    cam.gameObject.SetActive(false);
                }
            }
        }
    }
}
