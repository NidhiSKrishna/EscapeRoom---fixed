using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EscapeRoom.Puzzle;
using EscapeRoom.Interaction;
using EscapeRoom.Core;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor utility that configures Level 1 dynamic hinge transitions:
    /// 1. Door Hinge Swing:
    ///    - Ensures Door_Slab and Exit_Door are non-static (staticEditorFlags = 0) so Unity's static
    ///      batching does not freeze the rotating mesh in place.
    ///    - Configures DoorController to smoothly swing -95 degrees around the left hinge into the corridor.
    /// 2. Lockbox Lid Opening:
    ///    - Ensures Lockbox_LidSlab and Lockbox_LidHinge are non-static (staticEditorFlags = 0).
    ///    - Sets LockedContainer.openRotationOffset to +110 degrees on X (swings upward and backward).
    ///    - Ensures contents note (Clue_Document) is non-static and linked for smooth reveal.
    /// 3. Passcode Randomization:
    ///    - Ensures Level 1 4-digit passcode is randomized up to 9999.
    ///
    /// Runs automatically via [InitializeOnLoad] and via menu:
    /// Menu item: Tools > Escape Room > Apply Level 1 Door and Lid Transitions
    /// </summary>
    [InitializeOnLoad]
    public static class Level1TransitionsBuilder
    {
        private const string ScenePath = "Assets/Scenes/EscapeRoom_Main.unity";
        private const string SessionKey = "Level1Transitions_Applied_v2";

        static Level1TransitionsBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                ApplyTransitions();
            }
        }

        [MenuItem("Tools/Escape Room/Apply Level 1 Door and Lid Transitions", false, 19)]
        public static void ApplyTransitions()
        {
            Debug.Log("<b><color=#337ab7>[Level1TransitionsBuilder]</color> Configuring dynamic door hinge & lockbox lid transitions...</b>");

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (File.Exists(ScenePath))
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    activeScene = EditorSceneManager.GetActiveScene();
                }
                else
                {
                    Debug.LogWarning($"[Level1TransitionsBuilder] Scene not found at {ScenePath}");
                    return;
                }
            }

            int changesCount = 0;

            // ── 1. Exit Door & Door Slab ──────────────────────────────────────
            GameObject exitDoorGo = GameObject.Find("Exit_Door");
            if (exitDoorGo != null)
            {
                GameObjectUtility.SetStaticEditorFlags(exitDoorGo, 0);
                exitDoorGo.isStatic = false;

                var doorCtrl = exitDoorGo.GetComponent<DoorController>();
                if (doorCtrl != null)
                {
                    doorCtrl.HingeTransform = exitDoorGo.transform;
                    doorCtrl.OpenEulerAngles = new Vector3(0f, -95f, 0f);
                    doorCtrl.AnimationDuration = 1.4f;
                    EditorUtility.SetDirty(doorCtrl);
                    changesCount++;
                }

                Transform doorSlab = exitDoorGo.transform.Find("Door_Slab");
                if (doorSlab != null)
                {
                    GameObjectUtility.SetStaticEditorFlags(doorSlab.gameObject, 0);
                    doorSlab.gameObject.isStatic = false;
                    EditorUtility.SetDirty(doorSlab.gameObject);
                    changesCount++;
                    Debug.Log("<color=#5cb85c>[Level1TransitionsBuilder]</color> 'Door_Slab' set to non-static dynamic mesh for hinge rotation.");
                }
            }

            // ── 2. Lockbox Container, Lid Hinge & Lid Slab ───────────────────
            GameObject containerGo = GameObject.Find("Container_Lockbox");
            if (containerGo != null)
            {
                GameObjectUtility.SetStaticEditorFlags(containerGo, 0);
                containerGo.isStatic = false;

                var lockedCtrl = containerGo.GetComponent<LockedContainer>();
                if (lockedCtrl != null)
                {
                    Transform lidHinge = containerGo.transform.Find("Lockbox_LidHinge");
                    if (lidHinge != null)
                    {
                        GameObjectUtility.SetStaticEditorFlags(lidHinge.gameObject, 0);
                        lidHinge.gameObject.isStatic = false;
                        lockedCtrl.LidTransform = lidHinge;

                        Transform lidSlab = lidHinge.Find("Lockbox_LidSlab");
                        if (lidSlab != null)
                        {
                            GameObjectUtility.SetStaticEditorFlags(lidSlab.gameObject, 0);
                            lidSlab.gameObject.isStatic = false;
                            EditorUtility.SetDirty(lidSlab.gameObject);
                            changesCount++;
                            Debug.Log("<color=#5cb85c>[Level1TransitionsBuilder]</color> 'Lockbox_LidSlab' set to non-static dynamic mesh for lid rotation.");
                        }
                    }

                    lockedCtrl.OpenRotationOffset = new Vector3(110f, 0f, 0f);
                    EditorUtility.SetDirty(lockedCtrl);
                    changesCount++;
                }
            }

            // ── 3. Clue Document ─────────────────────────────────────────────
            GameObject clueGo = GameObject.Find("Clue_Document");
            if (clueGo != null)
            {
                GameObjectUtility.SetStaticEditorFlags(clueGo, 0);
                clueGo.isStatic = false;
                EditorUtility.SetDirty(clueGo);
                changesCount++;
            }

            // ── 4. Keypad Controller ─────────────────────────────────────────
            GameObject keypadGo = GameObject.Find("Terminal_Keypad");
            if (keypadGo != null)
            {
                var keypad = keypadGo.GetComponent<KeypadController>();
                if (keypad != null)
                {
                    keypad.EnsureRandomizedCode();
                    EditorUtility.SetDirty(keypad);
                    changesCount++;
                }
            }

            if (changesCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log($"<b><color=#5cb85c>[Level1TransitionsBuilder]</color> Successfully configured {changesCount} elements in {activeScene.name}!</b>");
            }
            else
            {
                Debug.Log("[Level1TransitionsBuilder] Scene already configured with dynamic transitions.");
            }

            EscapeRoomVerification.RunAllChecks();
        }
    }
}
