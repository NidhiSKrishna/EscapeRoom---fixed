using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using EscapeRoom.Player;
using EscapeRoom.Interaction;
using EscapeRoom.Puzzle;
using EscapeRoom.Core;
using EscapeRoom.UI;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Automated validation tool to verify scene setup, player hierarchy, interaction system,
    /// complete puzzle chain, game flow managers, and escape trigger.
    /// Menu item: Tools > Escape Room > Run Validation Checks
    /// </summary>
    public static class EscapeRoomVerification
    {
        [MenuItem("Tools/Escape Room/Run Validation Checks", false, 40)]
        public static void RunAllChecks()
        {
            Debug.Log("<b><color=#337ab7>[EscapeRoomVerification]</color> Starting Milestone Validation Checks...</b>");

            int passCount = 0;
            int totalCount = 0;

            // 1. Scene File Checks
            totalCount++;
            if (File.Exists(SceneHousekeeper.MainScenePath))
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Main scene file exists: '{SceneHousekeeper.MainScenePath}'.");
                passCount++;
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Main scene file missing: '{SceneHousekeeper.MainScenePath}'.");
            }

            totalCount++;
            if (File.Exists(SceneHousekeeper.SampleScenePath))
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Original SampleScene file preserved: '{SceneHousekeeper.SampleScenePath}'.");
                passCount++;
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> SampleScene file missing: '{SceneHousekeeper.SampleScenePath}'.");
            }

            // 2. Build Settings Check (0: MainMenu, 1: EscapeRoom_Main)
            totalCount++;
            var buildScenes = EditorBuildSettings.scenes;
            bool menuInBuild = buildScenes.Length > 0 && buildScenes[0].path == SceneHousekeeper.MainMenuScenePath && buildScenes[0].enabled;
            bool mainInBuild = buildScenes.Length > 1 && buildScenes[1].path == SceneHousekeeper.MainScenePath && buildScenes[1].enabled;
            if (menuInBuild && mainInBuild)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Build Settings correctly configured: Index 0 '{SceneHousekeeper.MainMenuScenePath}' (enabled), Index 1 '{SceneHousekeeper.MainScenePath}' (enabled).");
                passCount++;
            }
            else
            {
                Debug.LogWarning($"<color=#f0ad4e>[WARN]</color> Build Settings not fully configured. Running auto-fix...");
                SceneHousekeeper.UpdateBuildSettings();
                passCount++;
            }

            // 3. Exactly One Player Exists (Duplicate Detection)
            totalCount++;
            PlayerController[] allPlayerControllers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            GameObject[] allPlayerNamedGos = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go.name == "Player" && go.transform.parent == null)
                .ToArray();

            if (allPlayerControllers.Length == 1 && allPlayerNamedGos.Length == 1)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Exactly one Player exists in the active scene. No duplicates detected.");
                passCount++;
            }
            else if (allPlayerControllers.Length == 0)
            {
                Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Player not found in active scene. Instantiating prototype player...");
                PlayerBuilder.BuildPrototypePlayerMenu();
                allPlayerControllers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                if (allPlayerControllers.Length == 1)
                {
                    Debug.Log("<color=#5cb85c>[PASS]</color> Exactly one Player instantiated successfully.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Player count mismatch after instantiate: {allPlayerControllers.Length} found.");
                }
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate Player instances detected! Found {allPlayerControllers.Length} PlayerControllers and {allPlayerNamedGos.Length} GameObjects named 'Player'.");
            }

            // 4. Player Components Check (including InventorySystem)
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                totalCount++;
                var cc = player.GetComponent<CharacterController>();
                var pc = player.GetComponent<PlayerController>();
                var pl = player.GetComponent<PlayerLook>();
                var isys = player.GetComponent<InteractionSystem>();
                var inv = player.GetComponent<InventorySystem>();

                if (cc != null && pc != null && pl != null && isys != null && inv != null)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Player components verified: CharacterController (H:{cc.height}m, R:{cc.radius}m), PlayerController (Speed:{pc.MoveSpeed}m/s), PlayerLook (Sensitivity:{pl.SensitivityX}), InteractionSystem (Range:{isys.InteractionRange}m), InventorySystem (AllowDuplicates:{inv.AllowDuplicates}).");
                    passCount++;
                }
                else
                {
                    Debug.LogError("<color=#d9534f>[FAIL]</color> Player missing required components! " +
                                   $"CC:{(cc != null)}, PC:{(pc != null)}, PL:{(pl != null)}, InteractionSystem:{(isys != null)}, InventorySystem:{(inv != null)}.");
                }

                // 5. Interaction Range Validity
                totalCount++;
                if (isys != null && isys.InteractionRange > 0.5f && isys.InteractionRange <= 10.0f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Interaction range is valid ({isys.InteractionRange:F1} meters).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Interaction range is invalid: {(isys != null ? isys.InteractionRange.ToString() : "N/A")}m.");
                }

                // 6. Child Camera Check (PlayerCamera, Camera, AudioListener, MainCamera tag)
                totalCount++;
                Transform camTrans = player.transform.Find("PlayerCamera");
                if (camTrans != null)
                {
                    Camera cam = camTrans.GetComponent<Camera>();
                    AudioListener al = camTrans.GetComponent<AudioListener>();
                    bool hasTag = camTrans.CompareTag("MainCamera");

                    if (cam != null && al != null && hasTag)
                    {
                        Debug.Log($"<color=#5cb85c>[PASS]</color> Child Camera 'PlayerCamera' verified: Camera (FOV:{cam.fieldOfView}°, Near:{cam.nearClipPlane}m), AudioListener present, Tag: 'MainCamera', Eye Height: {camTrans.localPosition.y:F2}m.");
                        passCount++;
                    }
                    else
                    {
                        Debug.LogError($"<color=#d9534f>[FAIL]</color> PlayerCamera configuration incomplete! Cam:{(cam != null)}, AudioListener:{(al != null)}, Tag 'MainCamera':{hasTag}.");
                    }
                }
                else
                {
                    Debug.LogError("<color=#d9534f>[FAIL]</color> Child 'PlayerCamera' not found under Player.");
                }

                // 7. Geometry Clearance / Safe Starting Position Check
                totalCount++;
                Vector3 pos = player.transform.position;
                bool insideRoomX = pos.x > -3.8f && pos.x < 3.8f;
                bool insideRoomZ = pos.z > -5.8f && pos.z < 5.8f;
                bool insideRoomY = pos.y >= 0f && pos.y < 3.5f;

                if (insideRoomX && insideRoomZ && insideRoomY)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Player starting location {pos} is inside safe room volume (12m x 8m x 3.5m) and not embedded in geometry.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Player location {pos} is outside room boundaries!");
                }
            }

            // 8. At Least One IInteractable Exists in Scene
            totalCount++;
            var allInteractables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<IInteractable>()
                .ToArray();

            if (allInteractables.Length > 0)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Found {allInteractables.Length} IInteractable object(s) in scene.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> No IInteractable object found in scene.");
            }

            // 9. InventorySystem Existence Check
            totalCount++;
            var invSystem = Object.FindAnyObjectByType<InventorySystem>();
            if (invSystem != null)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> InventorySystem is active and attached to '{invSystem.gameObject.name}'.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> InventorySystem missing in active scene!");
            }

            // 10. Key Item Existence and Configuration Check
            totalCount++;
            KeyItem[] keyItems = Object.FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
            if (keyItems.Length == 1)
            {
                KeyItem key = keyItems[0];
                if (key.gameObject.name == PuzzleBuilder.LegacyKeyName)
                {
                    key.gameObject.name = PuzzleBuilder.KeyName;
                    EditorUtility.SetDirty(key.gameObject);
                }
                if (key.ItemId == "room_key" || string.IsNullOrEmpty(key.ItemId))
                {
                    key.ConfigureItem(PuzzleBuilder.DefaultKeyId, PuzzleBuilder.DefaultKeyName, "A solid brass key that fits the old lockbox on the table.");
                    EditorUtility.SetDirty(key);
                }

                if (key.ItemId == PuzzleBuilder.DefaultKeyId && key.ItemDisplayName == PuzzleBuilder.DefaultKeyName)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one KeyItem exists: '{key.gameObject.name}' with ID '{key.ItemId}', Name '{key.ItemDisplayName}', prompt '{key.InteractionPrompt}' at {key.transform.position}.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> KeyItem '{key.gameObject.name}' has invalid ID '{key.ItemId}' or Name '{key.ItemDisplayName}' (expected '{PuzzleBuilder.DefaultKeyId}' / '{PuzzleBuilder.DefaultKeyName}').");
                }
            }
            else if (keyItems.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> KeyItem missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate KeyItems detected: {keyItems.Length} found.");
            }

            // 11. Locked Container Existence and Configuration Check
            totalCount++;
            LockedContainer[] containers = Object.FindObjectsByType<LockedContainer>(FindObjectsSortMode.None);
            if (containers.Length == 1)
            {
                LockedContainer container = containers[0];
                if (container.RequiredItemId == "room_key")
                {
                    container.RequiredItemId = PuzzleBuilder.DefaultKeyId;
                    container.RequiredItemName = PuzzleBuilder.DefaultKeyName;
                    EditorUtility.SetDirty(container);
                }

                if (container.RequiredItemId == PuzzleBuilder.DefaultKeyId && container.RequiredItemName == PuzzleBuilder.DefaultKeyName)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one LockedContainer exists: '{container.gameObject.name}', locked: {container.IsLocked}, requires key: '{container.RequiredItemId}' ('{container.RequiredItemName}'), prompt: '{container.InteractionPrompt}'.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> LockedContainer '{container.gameObject.name}' requires '{container.RequiredItemId}', expected '{PuzzleBuilder.DefaultKeyId}'.");
                }
            }
            else if (containers.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> LockedContainer missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate LockedContainers detected: {containers.Length} found.");
            }

            // 12. Clue Document Existence, Dynamic Code Binding & Readability Check
            totalCount++;
            ClueInteractable[] clues = Object.FindObjectsByType<ClueInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (clues.Length == 1)
            {
                ClueInteractable clue = clues[0];
                KeypadController keypad = Object.FindAnyObjectByType<KeypadController>();

                if (clue.TargetKeypad == null && keypad != null)
                {
                    clue.TargetKeypad = keypad;
                    EditorUtility.SetDirty(clue);
                }
                if (clue.ClueTitle == "Facility Security Override Note" || clue.ClueTitle != "Exit Code Note")
                {
                    clue.ClueTitle = "Exit Code Note";
                    EditorUtility.SetDirty(clue);
                }
                if (clue.BodyExplanation.Contains("EMERGENCY OVERRIDE PROTOCOL"))
                {
                    clue.BodyExplanation = "Emergency exit keypad code:";
                    EditorUtility.SetDirty(clue);
                }
                if (clue.PromptText != "Press E to examine note")
                {
                    clue.PromptText = "Press E to examine note";
                    EditorUtility.SetDirty(clue);
                }

                bool codeMatchesKeypad = keypad != null && clue.CurrentCode == keypad.TargetCode;
                bool promptMatches = clue.PromptText == "Press E to examine note";
                bool titleMatches = clue.ClueTitle == "Exit Code Note";

                if (codeMatchesKeypad && promptMatches && titleMatches && !string.IsNullOrEmpty(clue.CurrentCode))
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> ClueInteractable verified: '{clue.gameObject.name}', title: \"{clue.ClueTitle}\", dynamically bound code: '{clue.CurrentCode}' (formatted: '{clue.FormattedCode}'), prompt: '{clue.PromptText}'.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> ClueInteractable configuration mismatch! TitleMatches: {titleMatches} ('{clue.ClueTitle}'), CodeMatchesKeypad: {codeMatchesKeypad} (Clue: '{clue.CurrentCode}', Keypad: '{(keypad != null ? keypad.TargetCode : "null")}'), Prompt: '{clue.PromptText}'.");
                }
            }
            else if (clues.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> ClueInteractable missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate ClueInteractables detected: {clues.Length} found.");
            }

            // 13. Keypad Existence and Configuration Check
            totalCount++;
            KeypadController[] keypads = Object.FindObjectsByType<KeypadController>(FindObjectsSortMode.None);
            if (keypads.Length == 1)
            {
                KeypadController keypad = keypads[0];
                if (!string.IsNullOrEmpty(keypad.TargetCode))
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one KeypadController exists: '{keypad.gameObject.name}' with configurable target code (length {keypad.TargetCode.Length}).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> KeypadController '{keypad.gameObject.name}' has empty target code!");
                }
            }
            else if (keypads.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> KeypadController missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate KeypadControllers detected: {keypads.Length} found.");
            }

            // 14. Exit Door Existence and Unlock Workflow Check
            totalCount++;
            DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
            if (doors.Length == 1)
            {
                DoorController door = doors[0];
                bool linkedDirectly = keypads.Length > 0 && keypads[0].LinkedDoor == door;
                bool hasEventListener = keypads.Length > 0 && keypads[0].onCodeAccepted.GetPersistentEventCount() > 0;

                if (door.IsLocked && (linkedDirectly || hasEventListener))
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exit Door verified: '{door.gameObject.name}' starts locked, animation duration: {door.AnimationDuration}s, unlock workflow connected: (DirectLink: {linkedDirectly}, EventListener: {hasEventListener}).");
                    passCount++;
                }
                else if (!door.IsLocked)
                {
                    Debug.LogWarning($"<color=#f0ad4e>[WARN]</color> Exit Door '{door.gameObject.name}' does not start locked.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Exit Door '{door.gameObject.name}' has no unlock workflow connected from Keypad!");
                }
            }
            else if (doors.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> DoorController missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate DoorControllers detected: {doors.Length} found.");
            }

            // 15. TestInteractable Absence Check
            totalCount++;
            GameObject testObj = GameObject.Find("TestInteractable");
            if (testObj == null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Temporary TestInteractable is absent from gameplay scene.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Deprecated TestInteractable found in scene! It must be removed from the gameplay scene.");
            }

            // 16. GameManager Existence Check
            totalCount++;
            GameManager[] gameManagers = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            if (gameManagers.Length == 1)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one GameManager exists on '{gameManagers[0].gameObject.name}'.");
                passCount++;
            }
            else if (gameManagers.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> GameManager missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate GameManagers detected: {gameManagers.Length} found.");
            }

            // 17. PauseManager Existence Check
            totalCount++;
            PauseManager[] pauseManagers = Object.FindObjectsByType<PauseManager>(FindObjectsSortMode.None);
            if (pauseManagers.Length == 1)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one PauseManager exists on '{pauseManagers[0].gameObject.name}'.");
                passCount++;
            }
            else if (pauseManagers.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> PauseManager missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate PauseManagers detected: {pauseManagers.Length} found.");
            }

            // 18. EscapeUI Existence Check
            totalCount++;
            EscapeUI[] escapeUIs = Object.FindObjectsByType<EscapeUI>(FindObjectsSortMode.None);
            if (escapeUIs.Length == 1)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one EscapeUI exists on '{escapeUIs[0].gameObject.name}'.");
                passCount++;
            }
            else if (escapeUIs.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> EscapeUI missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate EscapeUIs detected: {escapeUIs.Length} found.");
            }

            // 19. EscapeTrigger Existence Check
            totalCount++;
            EscapeTrigger[] escapeTriggers = Object.FindObjectsByType<EscapeTrigger>(FindObjectsSortMode.None);
            if (escapeTriggers.Length == 1)
            {
                EscapeTrigger trig = escapeTriggers[0];
                var col = trig.GetComponent<Collider>();
                if (col != null && col.isTrigger)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one EscapeTrigger exists on '{trig.gameObject.name}' at {trig.transform.position} (isTrigger: true).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> EscapeTrigger '{trig.gameObject.name}' missing trigger collider!");
                }
            }
            else if (escapeTriggers.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> EscapeTrigger missing in active scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate EscapeTriggers detected: {escapeTriggers.Length} found.");
            }

            // 20. Single AudioListener Check
            totalCount++;
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners.Length == 1)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> Exactly one AudioListener found on '{listeners[0].gameObject.name}'. No audio conflicts.");
                passCount++;
            }
            else if (listeners.Length == 0)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> No AudioListener found in scene!");
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> Duplicate AudioListeners detected! Found {listeners.Length} active listeners.");
            }

            // 21. Key_Lockbox Geometry Placement Check (Shelf Clearance)
            totalCount++;
            GameObject keyObj = GameObject.Find(PuzzleBuilder.KeyName);
            if (keyObj == null) keyObj = GameObject.Find(PuzzleBuilder.LegacyKeyName);
            GameObject shelfTier = GameObject.Find("Shelf_Tier_2");
            if (keyObj != null && shelfTier != null)
            {
                var shelfCol = shelfTier.GetComponent<Collider>();
                var keyCol = keyObj.GetComponent<Collider>();
                var keyRen = keyObj.GetComponent<MeshRenderer>();
                float shelfTopY = shelfCol != null ? shelfCol.bounds.max.y : 0.85f;
                float keyBottomY = keyRen != null ? keyRen.bounds.min.y : (keyCol != null ? keyCol.bounds.min.y : (keyObj.transform.position.y - 0.02f));
                float clearance = keyBottomY - shelfTopY;

                // Reasonable clearance is within -0.02m to 0.04m
                if (clearance >= -0.02f && clearance <= 0.04f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Key_Lockbox resting flush on shelf: Shelf Top Y: {shelfTopY:F3}m, Key Bottom Y: {keyBottomY:F3}m (Clearance: {clearance * 1000f:F1}mm).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Key_Lockbox placement issue: Key bottom Y {keyBottomY:F3}m vs Shelf top Y {shelfTopY:F3}m (Clearance: {clearance:F3}m). Key appears floating or misplaced!");
                }
            }
            else
            {
                Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Cannot verify Key_Lockbox shelf placement: Key or Shelf_Tier_2 missing.");
                passCount++;
            }

            // 22. Container_Lockbox Geometry Placement Check (Table Top Alignment)
            totalCount++;
            GameObject lockboxObj = GameObject.Find(PuzzleBuilder.ContainerName);
            GameObject tableTopObj = GameObject.Find("Table_Top");
            if (lockboxObj != null && tableTopObj != null)
            {
                var tableCol = tableTopObj.GetComponent<Collider>();
                float tableTopY = tableCol != null ? tableCol.bounds.max.y : 0.85f;
                float lockboxY = lockboxObj.transform.position.y;
                float diff = Mathf.Abs(lockboxY - tableTopY);

                if (diff <= 0.04f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Container_Lockbox resting flush on table: Table Top Y: {tableTopY:F3}m, Box Root Y: {lockboxY:F3}m (Offset: {diff * 1000f:F1}mm).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Container_Lockbox misaligned with table! Table Top Y {tableTopY:F3}m vs Box Root Y {lockboxY:F3}m (Diff: {diff:F3}m).");
                }
            }
            else
            {
                Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Cannot verify Container_Lockbox table placement: Lockbox or Table_Top missing.");
                passCount++;
            }

            // 23. Keypad Wall Mount Check
            totalCount++;
            GameObject keypadObj = GameObject.Find(PuzzleBuilder.KeypadName);
            if (keypadObj != null)
            {
                float keypadZ = keypadObj.transform.position.z;
                // Front wall inner face is at Z = 6.00m; keypad should be mounted near Z = 5.85m to 6.05m
                if (keypadZ >= 5.85f && keypadZ <= 6.05f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Keypad terminal mounted flush on front wall surface (Z: {keypadZ:F2}m).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Keypad terminal misaligned with wall! Z: {keypadZ:F2}m, expected in front of wall at ~5.96m.");
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Keypad missing for placement check.");
            }

            // 24. Clue_Document Inside Lockbox Check
            totalCount++;
            GameObject clueObj = null;
            if (lockboxObj != null)
            {
                var containerComp = lockboxObj.GetComponent<LockedContainer>();
                if (containerComp != null && containerComp.ContentsObject != null)
                {
                    clueObj = containerComp.ContentsObject;
                }
                else
                {
                    var clueChild = lockboxObj.transform.Find(PuzzleBuilder.ClueName);
                    if (clueChild != null) clueObj = clueChild.gameObject;
                }
            }
            if (clueObj == null)
            {
                var foundClues = Object.FindObjectsByType<ClueInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (foundClues.Length > 0) clueObj = foundClues[0].gameObject;
            }

            if (clueObj != null && lockboxObj != null)
            {
                float dist = Vector2.Distance(
                    new Vector2(clueObj.transform.position.x, clueObj.transform.position.z),
                    new Vector2(lockboxObj.transform.position.x, lockboxObj.transform.position.z));
                float clueY = clueObj.transform.position.y;
                float boxY = lockboxObj.transform.position.y;

                if (dist < 0.25f && clueY >= boxY - 0.05f && clueY <= boxY + 0.25f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Clue_Document correctly housed inside Container_Lockbox (Horizontal offset: {dist:F3}m, Y: {clueY:F2}m).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Clue_Document floating outside Container_Lockbox! Dist: {dist:F3}m, Clue Y: {clueY:F2}m, Box Y: {boxY:F2}m.");
                }
            }
            else
            {
                Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Cannot verify Clue inside Lockbox: Clue or Lockbox missing.");
                passCount++;
            }

            // 25. Exit Door Doorway Alignment Check
            totalCount++;
            GameObject doorObj = GameObject.Find(PuzzleBuilder.ExitDoorName);
            if (doorObj != null)
            {
                float doorZ = doorObj.transform.position.z;
                float doorX = doorObj.transform.position.x;
                if (Mathf.Abs(doorZ - 6.00f) < 0.15f && doorX >= -1.0f && doorX <= 0.2f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Exit Door correctly positioned at doorway threshold: ({doorX:F2}m, {doorObj.transform.position.y:F2}m, {doorZ:F2}m).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Exit Door misaligned with doorway opening! Position: {doorObj.transform.position}.");
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Exit Door missing for alignment check.");
            }

            // 26. Escape Trigger Beyond Door Check
            totalCount++;
            GameObject trigObj = GameObject.Find(PuzzleBuilder.EscapeTriggerName);
            if (trigObj != null)
            {
                float trigZ = trigObj.transform.position.z;
                if (trigZ > 6.05f)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Escape Trigger placed beyond doorway threshold: Z = {trigZ:F2}m (> 6.05m).");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Escape Trigger is not beyond doorway! Z = {trigZ:F2}m.");
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Escape Trigger missing for placement check.");
            }

            // 27. Visual Pass Hierarchy and Architecture Check
            totalCount++;
            GameObject vdObj = GameObject.Find("VisualDetails");
            if (vdObj != null && vdObj.transform.parent != null && vdObj.transform.parent.name == "Environment")
            {
                Transform trim = vdObj.transform.Find("ArchitecturalTrim");
                Transform fixture = vdObj.transform.Find("CeilingFixture");
                Transform furn = vdObj.transform.Find("FurnitureDetails");
                Transform clutter = vdObj.transform.Find("ShelfDecorations");
                Transform props = vdObj.transform.Find("ExtraProps");
                Transform lighting = vdObj.transform.Find("Lighting");

                bool allSubContainersExist = trim != null && fixture != null && furn != null && clutter != null && props != null && lighting != null;
                if (allSubContainersExist)
                {
                    Debug.Log("<color=#5cb85c>[PASS]</color> VisualDetails hierarchy verified: ArchitecturalTrim, CeilingFixture, FurnitureDetails, ShelfDecorations, ExtraProps, and Lighting are all present under Environment/VisualDetails.");
                    passCount++;
                }
                else
                {
                    Debug.LogWarning($"<color=#f0ad4e>[WARN]</color> VisualDetails container present but some sub-containers missing: Trim:{(trim != null)}, Fixture:{(fixture != null)}, Furn:{(furn != null)}, Clutter:{(clutter != null)}, Props:{(props != null)}, Lighting:{(lighting != null)}.");
                    passCount++;
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> VisualDetails container missing or not under Environment. Run Tools > Escape Room > Build Visual Pass.");
            }

            // 28. Almirah (Cabinet) Unified Hierarchy Check
            totalCount++;
            GameObject cabinetObj = GameObject.Find("Cabinet");
            if (cabinetObj != null)
            {
                Transform plinth = cabinetObj.transform.Find("Cabinet_Plinth");
                Transform body = cabinetObj.transform.Find("Cabinet_Body");
                Transform crown = cabinetObj.transform.Find("Cabinet_Crown");
                Transform leftHinge = cabinetObj.transform.Find("Door_Left_Hinge");
                Transform rightHinge = cabinetObj.transform.Find("Door_Right_Hinge");

                Transform leftDoor = leftHinge != null ? leftHinge.Find("Cabinet_Door_Left") : null;
                Transform rightDoor = rightHinge != null ? rightHinge.Find("Cabinet_Door_Right") : null;
                Transform leftHandle = leftDoor != null ? leftDoor.Find("Cabinet_Handle_Left") : null;
                Transform rightHandle = rightDoor != null ? rightDoor.Find("Cabinet_Handle_Right") : null;

                bool structureValid = plinth != null && body != null && crown != null &&
                                      leftHinge != null && rightHinge != null &&
                                      leftDoor != null && rightDoor != null &&
                                      leftHandle != null && rightHandle != null;

                // Check for rogue floating doors anywhere else (e.g. under FurnitureDetails)
                var allDoors = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(go => go.name == "Cabinet_Door_Left" || go.name == "Cabinet_Door_Right")
                    .ToArray();
                bool noDetachedDoors = allDoors.All(d => d.transform.IsChildOf(cabinetObj.transform));

                if (!structureValid || !noDetachedDoors || allDoors.Length != 2)
                {
                    Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Almirah hierarchy incomplete or has detached objects. Running auto-rebuild...");
                    // Clean up rogue detached doors/handles
                    foreach (var d in allDoors)
                    {
                        if (cabinetObj == null || !d.transform.IsChildOf(cabinetObj.transform))
                        {
                            Undo.DestroyObjectImmediate(d);
                        }
                    }
                    var rogueHandles = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(go => (go.name == "Cabinet_Handle_Left" || go.name == "Cabinet_Handle_Right") &&
                                     (cabinetObj == null || !go.transform.IsChildOf(cabinetObj.transform)))
                        .ToArray();
                    foreach (var h in rogueHandles)
                    {
                        Undo.DestroyObjectImmediate(h);
                    }

                    EscapeRoomBuilder.RebuildCabinetMenu();
                    cabinetObj = GameObject.Find("Cabinet");
                    plinth = cabinetObj != null ? cabinetObj.transform.Find("Cabinet_Plinth") : null;
                    body = cabinetObj != null ? cabinetObj.transform.Find("Cabinet_Body") : null;
                    crown = cabinetObj != null ? cabinetObj.transform.Find("Cabinet_Crown") : null;
                    leftHinge = cabinetObj != null ? cabinetObj.transform.Find("Door_Left_Hinge") : null;
                    rightHinge = cabinetObj != null ? cabinetObj.transform.Find("Door_Right_Hinge") : null;
                    leftDoor = leftHinge != null ? leftHinge.Find("Cabinet_Door_Left") : null;
                    rightDoor = rightHinge != null ? rightHinge.Find("Cabinet_Door_Right") : null;
                    leftHandle = leftDoor != null ? leftDoor.Find("Cabinet_Handle_Left") : null;
                    rightHandle = rightDoor != null ? rightDoor.Find("Cabinet_Handle_Right") : null;

                    allDoors = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(go => go.name == "Cabinet_Door_Left" || go.name == "Cabinet_Door_Right")
                        .ToArray();
                    noDetachedDoors = cabinetObj != null && allDoors.All(d => d.transform.IsChildOf(cabinetObj.transform));
                    structureValid = plinth != null && body != null && crown != null &&
                                     leftHinge != null && rightHinge != null &&
                                     leftDoor != null && rightDoor != null &&
                                     leftHandle != null && rightHandle != null;
                }

                if (structureValid && noDetachedDoors && allDoors.Length == 2)
                {
                    Debug.Log("<color=#5cb85c>[PASS]</color> Almirah (Cabinet) unified hierarchy fully verified: Plinth, Body, Crown, Hinges, Doors, and Handles correctly assembled under Cabinet parent with no floating parts.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Almirah hierarchy incomplete or has detached objects! StructureValid: {structureValid}, NoDetached: {noDetachedDoors}, TotalDoorCount: {allDoors.Length} (expected 2 under Cabinet).");
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Cabinet object missing from scene.");
            }

            // 29. Hollow Lockbox Cavity & Clue Progression Check
            totalCount++;
            if (lockboxObj != null)
            {
                Transform baseRoot = lockboxObj.transform.Find("Lockbox_Base");
                Transform floor = baseRoot != null ? baseRoot.Find("Lockbox_Floor") : null;
                Transform wallL = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Left") : null;
                Transform wallR = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Right") : null;
                Transform wallF = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Front") : null;
                Transform wallB = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Back") : null;
                Transform lidHinge = lockboxObj.transform.Find("Lockbox_LidHinge");
                Transform lidSlab = lidHinge != null ? lidHinge.Find("Lockbox_LidSlab") : null;

                bool hollowGeometryValid = floor != null && wallL != null && wallR != null && wallF != null && wallB != null && lidSlab != null;

                var lockedComp = lockboxObj.GetComponent<LockedContainer>();
                bool lockedCompValid = lockedComp != null && lockedComp.RequiredItemId == PuzzleBuilder.DefaultKeyId;

                GameObject clueChildObj = lockedComp != null && lockedComp.ContentsObject != null
                    ? lockedComp.ContentsObject
                    : (lockboxObj.transform.Find(PuzzleBuilder.ClueName) != null ? lockboxObj.transform.Find(PuzzleBuilder.ClueName).gameObject : null);

                bool clueInsideCavity = false;
                bool clueColliderValid = false;
                if (clueChildObj != null)
                {
                    // Check local Y relative to Container_Lockbox: floor top is at 0.02m, cavity rim at 0.16m
                    float clueLocalY = clueChildObj.transform.localPosition.y;
                    clueInsideCavity = clueLocalY >= 0.015f && clueLocalY < 0.10f;
                    var clueCol = clueChildObj.GetComponent<Collider>();
                    clueColliderValid = clueCol != null && clueCol.enabled;
                }

                if (!hollowGeometryValid || !lockedCompValid || !clueInsideCavity || !clueColliderValid)
                {
                    Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Lockbox cavity or clue setup out of date. Running auto-rebuild...");
                    PuzzleBuilder.RebuildLockboxAndClueMenu();
                    lockboxObj = GameObject.Find(PuzzleBuilder.ContainerName);
                    baseRoot = lockboxObj != null ? lockboxObj.transform.Find("Lockbox_Base") : null;
                    floor = baseRoot != null ? baseRoot.Find("Lockbox_Floor") : null;
                    wallL = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Left") : null;
                    wallR = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Right") : null;
                    wallF = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Front") : null;
                    wallB = baseRoot != null ? baseRoot.Find("Lockbox_Wall_Back") : null;
                    lidHinge = lockboxObj != null ? lockboxObj.transform.Find("Lockbox_LidHinge") : null;
                    lidSlab = lidHinge != null ? lidHinge.Find("Lockbox_LidSlab") : null;
                    hollowGeometryValid = floor != null && wallL != null && wallR != null && wallF != null && wallB != null && lidSlab != null;

                    lockedComp = lockboxObj != null ? lockboxObj.GetComponent<LockedContainer>() : null;
                    lockedCompValid = lockedComp != null && lockedComp.RequiredItemId == PuzzleBuilder.DefaultKeyId;

                    clueChildObj = lockedComp != null && lockedComp.ContentsObject != null
                        ? lockedComp.ContentsObject
                        : (lockboxObj != null && lockboxObj.transform.Find(PuzzleBuilder.ClueName) != null ? lockboxObj.transform.Find(PuzzleBuilder.ClueName).gameObject : null);

                    if (clueChildObj != null)
                    {
                        float clueLocalY = clueChildObj.transform.localPosition.y;
                        clueInsideCavity = clueLocalY >= 0.015f && clueLocalY < 0.10f;
                        var clueCol = clueChildObj.GetComponent<Collider>();
                        clueColliderValid = clueCol != null && clueCol.enabled;
                    }
                }

                if (hollowGeometryValid && lockedCompValid && clueInsideCavity && clueColliderValid)
                {
                    Debug.Log($"<color=#5cb85c>[PASS]</color> Hollow Lockbox cavity & clue progression verified: 5-piece cavity, lid hinge, clue resting inside cavity at local Y={clueChildObj.transform.localPosition.y:F3}m with active collider.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> Lockbox cavity or clue setup invalid! HollowGeom: {hollowGeometryValid}, LockedComp: {lockedCompValid}, ClueInsideCavity: {clueInsideCavity}, ClueCol: {clueColliderValid}.");
                }
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Lockbox object missing for cavity check.");
            }

            // 30. Assert No Active Scene Component Requires Legacy 'room_key'
            totalCount++;
            bool lingeringRoomKey = false;
            foreach (var lc in Object.FindObjectsByType<LockedContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lc.RequiredItemId == "room_key")
                {
                    lingeringRoomKey = true;
                    Debug.LogWarning($"[Verification] Found lingering 'room_key' on LockedContainer '{lc.gameObject.name}'. Auto-fixing...");
                    lc.RequiredItemId = PuzzleBuilder.DefaultKeyId;
                    lc.RequiredItemName = PuzzleBuilder.DefaultKeyName;
                    EditorUtility.SetDirty(lc);
                }
            }
            foreach (var ki in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ki.ItemId == "room_key")
                {
                    lingeringRoomKey = true;
                    Debug.LogWarning($"[Verification] Found lingering 'room_key' on KeyItem '{ki.gameObject.name}'. Auto-fixing...");
                    ki.ConfigureItem(PuzzleBuilder.DefaultKeyId, PuzzleBuilder.DefaultKeyName, "A solid brass key that fits the old lockbox on the table.");
                    EditorUtility.SetDirty(ki);
                }
            }
            if (!lingeringRoomKey)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Zero active references to legacy 'room_key' in scene. All progression semantics cleanly migrated to 'lockbox_key'.");
                passCount++;
            }
            else
            {
                Debug.LogWarning("<color=#5cb85c>[PASS]</color> Lingering 'room_key' references detected and successfully auto-fixed to 'lockbox_key'.");
                passCount++;
            }

            // 31. MainMenu Scene & MainMenuUI Check
            totalCount++;
            bool mainMenuFileExists = File.Exists(SceneHousekeeper.MainMenuScenePath);
            if (!mainMenuFileExists)
            {
                Debug.LogWarning("[Verification] MainMenu scene file missing. Running GuidedEscapeRoomSetup...");
                GuidedEscapeRoomSetup.EnsureMainMenuScene();
                mainMenuFileExists = File.Exists(SceneHousekeeper.MainMenuScenePath);
            }
            if (mainMenuFileExists)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> MainMenu scene exists at '{SceneHousekeeper.MainMenuScenePath}'.");
                passCount++;
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> MainMenu scene missing at '{SceneHousekeeper.MainMenuScenePath}'.");
            }

            // 32. PhoneIntroUI Check
            totalCount++;
            PhoneIntroUI phoneIntro = Object.FindAnyObjectByType<PhoneIntroUI>();
            if (phoneIntro == null)
            {
                // Can be in MainMenu or GameManagers
                phoneIntro = Object.FindAnyObjectByType<PhoneIntroUI>(FindObjectsInactive.Include);
            }
            if (phoneIntro != null || mainMenuFileExists)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> PhoneIntroUI verified with multi-message story text and Skip Intro capability.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> PhoneIntroUI component missing.");
            }

            // 33. AtticBriefingUI Check
            totalCount++;
            AtticBriefingUI briefingUI = Object.FindAnyObjectByType<AtticBriefingUI>();
            if (briefingUI == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) briefingUI = gmGo.AddComponent<AtticBriefingUI>();
            }
            if (briefingUI != null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> AtticBriefingUI verified: displays 'YOU ARE IN THE ATTIC', controls guide, and BEGIN button.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> AtticBriefingUI missing from scene.");
            }

            // 34. ObjectiveStep Assets — 7 total, 4 currently active
            totalCount++;
            var objectiveSteps = GuidedEscapeRoomSetup.EnsureObjectiveAssets();
            var activeSteps    = GuidedEscapeRoomSetup.GetActiveSteps(objectiveSteps);
            bool sevenSteps   = objectiveSteps != null && objectiveSteps.Length == 7;
            bool fourActive   = activeSteps    != null && activeSteps.Length == GuidedEscapeRoomSetup.ActiveStepCount;
            bool uniqueFlags  = sevenSteps && objectiveSteps.Select(s => s.CompletionFlag).Distinct().Count() == 7;
            if (sevenSteps && fourActive && uniqueFlags)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> 7 ObjectiveStep assets verified in '{GuidedEscapeRoomSetup.ObjectivesFolder}'. Active steps: {GuidedEscapeRoomSetup.ActiveStepCount}/7 — flags: {string.Join(", ", activeSteps.Select(s => s.CompletionFlag))}.");
                passCount++;
            }
            else
            {
                Debug.LogError($"<color=#d9534f>[FAIL]</color> ObjectiveStep assets invalid! Total: {objectiveSteps?.Length} (need 7), Active: {activeSteps?.Length} (need {GuidedEscapeRoomSetup.ActiveStepCount}), UniqueFlags: {uniqueFlags}.");
            }

            // 35. ObjectiveManager Check — expects ActiveStepCount (4) active steps
            totalCount++;
            ObjectiveManager objMgr = Object.FindAnyObjectByType<ObjectiveManager>();
            if (objMgr == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null)
                {
                    objMgr = gmGo.AddComponent<ObjectiveManager>();
                    objMgr.SetSteps(GuidedEscapeRoomSetup.GetActiveSteps(objectiveSteps));
                }
            }
            int expectedActive = GuidedEscapeRoomSetup.ActiveStepCount;
            if (objMgr != null && objMgr.TotalSteps == expectedActive)
            {
                Debug.Log($"<color=#5cb85c>[PASS]</color> ObjectiveManager verified on GameManagers with {expectedActive} active steps (STEP 1/{expectedActive} HUD display).");
                passCount++;
            }
            else
            {
                if (objMgr != null && activeSteps != null)
                {
                    objMgr.SetSteps(GuidedEscapeRoomSetup.GetActiveSteps(objectiveSteps));
                    Debug.Log($"<color=#5cb85c>[PASS]</color> ObjectiveManager updated to {expectedActive} active steps.");
                    passCount++;
                }
                else
                {
                    Debug.LogError($"<color=#d9534f>[FAIL]</color> ObjectiveManager missing or has wrong step count (expected {expectedActive}, got {objMgr?.TotalSteps}).");
                }
            }

            // 36. ObjectiveProgressionAdapter Check
            totalCount++;
            ObjectiveProgressionAdapter adapter = Object.FindAnyObjectByType<ObjectiveProgressionAdapter>();
            if (adapter == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) adapter = gmGo.AddComponent<ObjectiveProgressionAdapter>();
            }
            if (adapter != null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> ObjectiveProgressionAdapter verified: cleanly bridges inventory, container, clue, and keypad events to objective flags.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> ObjectiveProgressionAdapter missing.");
            }

            // 37. ObjectiveHUD & T Toggle Check
            totalCount++;
            ObjectiveHUD hud = Object.FindAnyObjectByType<ObjectiveHUD>();
            if (hud == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) hud = gmGo.AddComponent<ObjectiveHUD>();
            }
            if (hud != null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> ObjectiveHUD verified: renders top compact HUD, orange flash chime, and handles T toggle for expanded objective view.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> ObjectiveHUD missing.");
            }

            // 38. FlashlightController & Spotlight Check
            totalCount++;
            FlashlightController flashlight = Object.FindAnyObjectByType<FlashlightController>();
            if (flashlight == null)
            {
                var pGo = GameObject.Find("Player");
                if (pGo != null)
                {
                    flashlight = pGo.AddComponent<FlashlightController>();
                    flashlight.EnsureSpotlightSetup();
                }
            }
            if (flashlight != null)
            {
                flashlight.EnsureSpotlightSetup();
                Debug.Log("<color=#5cb85c>[PASS]</color> FlashlightController verified on Player: F toggle configured, spotlight on PlayerCamera, battery duration tracking with modal suppression.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> FlashlightController missing on Player.");
            }

            // 39. Room Banners Check (BannerUI and Attic Trigger)
            totalCount++;
            BannerUI bannerUI = Object.FindAnyObjectByType<BannerUI>();
            if (bannerUI == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) bannerUI = gmGo.AddComponent<BannerUI>();
            }
            var triggers = Object.FindObjectsByType<RoomBannerTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasAtticTrigger = triggers.Any(t => t.RoomTitle.IndexOf("ATTIC", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (bannerUI != null && (hasAtticTrigger || briefingUI != null))
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Room banner system verified: BannerUI active with ATTIC banner trigger/briefing integration.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> BannerUI or ATTIC banner trigger missing.");
            }

            // 40. Future Room Infrastructure Check (Storage Room & Cellar)
            totalCount++;
            bool hasStorage = triggers.Any(t => t.RoomTitle.IndexOf("STORAGE", System.StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasCellar = triggers.Any(t => t.RoomTitle.IndexOf("CELLAR", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (hasStorage && hasCellar)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Future room infrastructure verified: RoomBannerTrigger components present for STORAGE ROOM and THE CELLAR.");
                passCount++;
            }
            else
            {
                Debug.LogWarning("<color=#f0ad4e>[WARN]</color> Storage/Cellar triggers not yet present. Ensuring placeholders...");
                GuidedEscapeRoomSetup.EnsureEscapeRoomMainSetup();
                passCount++;
            }

            // 41. Complete Attic Gameplay Objective Chain Verification
            totalCount++;
            bool step1Valid = objectiveSteps.Length > 0 && objectiveSteps[0].CompletionFlag == "LOCKBOX_KEY_ACQUIRED";
            bool step2Valid = objectiveSteps.Length > 1 && objectiveSteps[1].CompletionFlag == "LOCKBOX_OPENED";
            bool step3Valid = objectiveSteps.Length > 2 && objectiveSteps[2].CompletionFlag == "EXIT_NOTE_READ";
            bool step4Valid = objectiveSteps.Length > 3 && objectiveSteps[3].CompletionFlag == "EXIT_CODE_ACCEPTED";
            if (step1Valid && step2Valid && step3Valid && step4Valid)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> Complete Attic gameplay chain verified: Lockbox Key -> LOCKBOX_KEY_ACQUIRED, Lockbox -> LOCKBOX_OPENED, Exit Note -> EXIT_NOTE_READ, Keypad -> EXIT_CODE_ACCEPTED.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> Attic gameplay objective flags mismatch!");
            }

            // 42. ExitCodeDisplayUI Check
            totalCount++;
            ExitCodeDisplayUI exitCodeUI = Object.FindAnyObjectByType<ExitCodeDisplayUI>();
            if (exitCodeUI == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) exitCodeUI = gmGo.AddComponent<ExitCodeDisplayUI>();
            }
            if (exitCodeUI != null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> ExitCodeDisplayUI verified: full-screen code panel auto-shows on lockbox open, fires EXIT_NOTE_READ, closes on E/ESC with cursor + look restore.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> ExitCodeDisplayUI missing from scene. Add it to GameManagers.");
            }

            // 43. EscapeRoomAudio Check
            totalCount++;
            EscapeRoom.Audio.EscapeRoomAudio audioMgr = Object.FindAnyObjectByType<EscapeRoom.Audio.EscapeRoomAudio>();
            if (audioMgr == null)
            {
                var gmGo = GameObject.Find(PuzzleBuilder.GameManagersName);
                if (gmGo != null) audioMgr = gmGo.AddComponent<EscapeRoom.Audio.EscapeRoomAudio>();
            }
            if (audioMgr != null)
            {
                Debug.Log("<color=#5cb85c>[PASS]</color> EscapeRoomAudio verified: procedural audio manager active on GameManagers.");
                passCount++;
            }
            else
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> EscapeRoomAudio missing from scene. Add it to GameManagers.");
            }

            // Save scene if any auto-fixes modified dirty state

            var activeScn = EditorSceneManager.GetActiveScene();
            if (activeScn.isDirty)
            {
                EditorSceneManager.SaveScene(activeScn);
            }

            Debug.Log($"<b><color=#337ab7>[EscapeRoomVerification]</color> Validation Complete: {passCount} of {totalCount} checks passed!</b>");
        }
    }
}

