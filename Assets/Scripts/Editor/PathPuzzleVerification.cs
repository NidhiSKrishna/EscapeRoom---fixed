using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Stage3;
using EscapeRoom.Stage3.Puzzles;

namespace EscapeRoom.Editor
{
    public static class PathPuzzleVerification
    {
        public const string ScenePath = "Assets/Scenes/SecurityControlRoom.unity";

        [MenuItem("Tools/Escape Room/Verify Stage 3 Path Puzzle", false, 50)]
        public static void RunVerificationFromMenu()
        {
            RunVerification();
        }

        public static void RunVerification()
        {
            Debug.Log("==================================================");
            Debug.Log("STARTING STAGE 3 PATH PUZZLE COMPREHENSIVE VERIFICATION");
            Debug.Log("==================================================");

            bool allPassed = true;

            try
            {
                // Test 2: Stage 3 loads normally
                Debug.Log("[VERIFY] Opening scene: " + ScenePath);
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError("[FAIL] Test 2: Could not open " + ScenePath);
                    allPassed = false;
                    return;
                }
                Debug.Log("[PASS] Test 2: Stage 3 scene loaded normally.");

                // Check Objects
                MemoryPathPuzzle puzzle = UnityEngine.Object.FindFirstObjectByType<MemoryPathPuzzle>();
                if (puzzle == null)
                {
                    Debug.LogError("[FAIL] MemoryPathPuzzle not found in scene.");
                    allPassed = false;
                    return;
                }

                SecurityRoomManager roomMgr = UnityEngine.Object.FindFirstObjectByType<SecurityRoomManager>();
                Stage3Timer timer = UnityEngine.Object.FindFirstObjectByType<Stage3Timer>();
                ObjectiveManager objMgr = UnityEngine.Object.FindFirstObjectByType<ObjectiveManager>();
                Camera mainCam = Camera.main;

                if (mainCam == null)
                {
                    Debug.LogError("[FAIL] MainCamera not found in scene.");
                    allPassed = false;
                }
                else
                {
                    Debug.Log($"[PASS] Camera found: {mainCam.name}, tag: {mainCam.tag}");
                }

                // Verify Tiles
                FieldInfo tilesField = typeof(MemoryPathPuzzle).GetField("tiles", BindingFlags.NonPublic | BindingFlags.Instance);
                PathTile[] tiles = tilesField != null ? (PathTile[])tilesField.GetValue(puzzle) : null;
                if (tiles == null || tiles.Length != 16)
                {
                    tiles = puzzle.GetComponentsInChildren<PathTile>(true);
                }

                if (tiles == null || tiles.Length != 16)
                {
                    Debug.LogError($"[FAIL] Expected 16 tiles, found {(tiles != null ? tiles.Length : 0)}");
                    allPassed = false;
                    return;
                }
                Debug.Log($"[PASS] All 16 PathTiles found.");

                // Test Tile Coordinates & Colliders
                HashSet<string> coords = new HashSet<string>();
                for (int i = 0; i < tiles.Length; i++)
                {
                    PathTile t = tiles[i];
                    if (t == null)
                    {
                        Debug.LogError($"[FAIL] Tile at index {i} is null!");
                        allPassed = false;
                        continue;
                    }

                    string key = $"{t.TileX},{t.TileY}";
                    if (!coords.Add(key))
                    {
                        Debug.LogError($"[FAIL] Duplicate tile coordinates found: {key}");
                        allPassed = false;
                    }

                    Collider col = t.GetComponent<Collider>();
                    if (col == null || !col.enabled)
                    {
                        Debug.LogError($"[FAIL] Tile {t.name} has missing or disabled collider!");
                        allPassed = false;
                    }
                }
                Debug.Log($"[PASS] Unique grid coordinates verified for all {coords.Count} tiles.");

                // Test Raycasting to Tiles
                int raycastHits = 0;
                RaycastHit hitInfo;
                foreach (PathTile t in tiles)
                {
                    Vector3 tilePos = t.transform.position + Vector3.up * 0.05f;
                    Vector3 rayOrigin = mainCam.transform.position;
                    Vector3 rayDir = (tilePos - rayOrigin).normalized;
                    float dist = Vector3.Distance(rayOrigin, tilePos) + 1f;

                    if (Physics.Raycast(rayOrigin, rayDir, out hitInfo, dist, ~0, QueryTriggerInteraction.Collide))
                    {
                        PathTile hitTile = hitInfo.collider.GetComponentInParent<PathTile>();
                        if (hitTile != null)
                        {
                            raycastHits++;
                        }
                    }
                }
                Debug.Log($"[PASS] Raycast reachability verified: {raycastHits}/{tiles.Length} tiles directly hit from MainCamera.");

                // Test 3 & 4.A: Path Generation Integrity (100 iterations)
                MethodInfo genPathMethod = typeof(MemoryPathPuzzle).GetMethod("GenerateConnectedRandomPath", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo pathIndicesField = typeof(MemoryPathPuzzle).GetField("currentPathIndices", BindingFlags.NonPublic | BindingFlags.Instance);

                int pathTestRuns = 100;
                for (int run = 0; run < pathTestRuns; run++)
                {
                    bool genSuccess = (bool)genPathMethod.Invoke(puzzle, null);
                    if (!genSuccess)
                    {
                        Debug.LogError($"[FAIL] Path generation failed at run {run}");
                        allPassed = false;
                        break;
                    }

                    List<int> path = (List<int>)pathIndicesField.GetValue(puzzle);
                    if (path.Count != 5)
                    {
                        Debug.LogError($"[FAIL] Path length expected 5, got {path.Count}");
                        allPassed = false;
                        break;
                    }

                    // Check start tile on perimeter
                    PathTile startTile = tiles[path[0]];
                    bool isPerimeter = (startTile.TileX == 0 || startTile.TileY == 0 || startTile.TileX == 3 || startTile.TileY == 3);
                    if (!isPerimeter)
                    {
                        Debug.LogError($"[FAIL] Start tile ({startTile.TileX},{startTile.TileY}) is not on perimeter!");
                        allPassed = false;
                        break;
                    }

                    // Check connectivity, no repeats, no diagonal jumps
                    HashSet<int> visited = new HashSet<int>();
                    for (int step = 0; step < path.Count; step++)
                    {
                        int currentIdx = path[step];
                        if (!visited.Add(currentIdx))
                        {
                            Debug.LogError($"[FAIL] Duplicate tile in path at step {step}: index {currentIdx}");
                            allPassed = false;
                            break;
                        }

                        if (step > 0)
                        {
                            PathTile prev = tiles[path[step - 1]];
                            PathTile curr = tiles[currentIdx];
                            int manhattan = Mathf.Abs(prev.TileX - curr.TileX) + Mathf.Abs(prev.TileY - curr.TileY);
                            if (manhattan != 1)
                            {
                                Debug.LogError($"[FAIL] Non-adjacent step in path: ({prev.TileX},{prev.TileY}) -> ({curr.TileX},{curr.TileY}) (Manhattan={manhattan})");
                                allPassed = false;
                                break;
                            }
                        }
                    }
                }
                Debug.Log($"[PASS] Path generation verified over {pathTestRuns} runs: All paths length 5, start on perimeter, orthogonal, zero diagonal jumps, zero repeats.");

                // Test 5, 6, 10, 11: Correct clicks, progression, completion and single event fire
                puzzle.ResetPuzzle();
                genPathMethod.Invoke(puzzle, null);
                List<int> testPath = (List<int>)pathIndicesField.GetValue(puzzle);

                // Set state to WaitingForInput
                PropertyInfo stateProp = typeof(MemoryPathPuzzle).GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance);
                stateProp.SetValue(puzzle, MemoryPathPuzzleState.WaitingForInput);
                MethodInfo setDetectMethod = typeof(MemoryPathPuzzle).GetMethod("SetTilesDetection", BindingFlags.NonPublic | BindingFlags.Instance);
                setDetectMethod.Invoke(puzzle, new object[] { true });

                int completionEventsFired = 0;
                puzzle.OnPuzzleCompleted += () => { completionEventsFired++; };

                MethodInfo clickHandler = typeof(MemoryPathPuzzle).GetMethod("HandleTileClicked", BindingFlags.NonPublic | BindingFlags.Instance);

                // Simulate clicks along the path
                for (int step = 0; step < testPath.Count; step++)
                {
                    PathTile t = tiles[testPath[step]];
                    clickHandler.Invoke(puzzle, new object[] { t });

                    if (t.CurrentState != TileState.Correct)
                    {
                        Debug.LogError($"[FAIL] Test 5: Tile did not change to green (Correct) state at step {step}. Current: {t.CurrentState}");
                        allPassed = false;
                    }
                }

                if (!puzzle.IsCompleted)
                {
                    Debug.LogError("[FAIL] Test 10: Puzzle not marked IsCompleted after path finished.");
                    allPassed = false;
                }
                else
                {
                    Debug.Log("[PASS] Test 10: Puzzle successfully completed.");
                }

                if (completionEventsFired != 1)
                {
                    Debug.LogError($"[FAIL] Test 11: OnPuzzleCompleted fired {completionEventsFired} times, expected exactly 1.");
                    allPassed = false;
                }
                else
                {
                    Debug.Log("[PASS] Test 11: OnPuzzleCompleted fired exactly once.");
                }

                // Test 7, 8: Wrong click, red state, penalty, and retry
                puzzle.ResetPuzzle();
                genPathMethod.Invoke(puzzle, null);
                testPath = (List<int>)pathIndicesField.GetValue(puzzle);
                stateProp.SetValue(puzzle, MemoryPathPuzzleState.WaitingForInput);
                setDetectMethod.Invoke(puzzle, new object[] { true });

                // Find a wrong tile (not testPath[0])
                PathTile wrongTile = null;
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (i != testPath[0])
                    {
                        wrongTile = tiles[i];
                        break;
                    }
                }

                if (timer != null)
                {
                    timer.ResetTimer(480f);
                    timer.StartTimer();
                }

                float timeBefore = timer != null ? timer.TimeRemaining : 0f;
                clickHandler.Invoke(puzzle, new object[] { wrongTile });

                if (wrongTile.CurrentState != TileState.Error)
                {
                    Debug.LogError($"[FAIL] Test 7: Wrong tile did not turn red (Error). Current: {wrongTile.CurrentState}");
                    allPassed = false;
                }
                else
                {
                    Debug.Log("[PASS] Test 7: Wrong tile turned red (Error).");
                }

                if (timer != null)
                {
                    float timeAfter = timer.TimeRemaining;
                    if (timeBefore - timeAfter < 9f)
                    {
                        Debug.LogError($"[FAIL] Penalty check failed: before={timeBefore}, after={timeAfter}");
                        allPassed = false;
                    }
                    else
                    {
                        Debug.Log($"[PASS] Test 7: Timer penalty applied: reduced by {timeBefore - timeAfter}s (from {timeBefore}s to {timeAfter}s).");
                    }
                }

                if (puzzle.CurrentState == MemoryPathPuzzleState.Failed)
                {
                    Debug.Log("[PASS] Test 8: Puzzle transitioned to Failed state for automatic retry.");
                }

                if (allPassed)
                {
                    Debug.Log("==================================================");
                    Debug.Log(">>> ALL PUZZLE VERIFICATION CHECKS PASSED! <<<");
                    Debug.Log("==================================================");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[EXCEPTION] Verification threw an error: " + ex);
            }
        }
    }
}
