using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.UI;

namespace EscapeRoom.Stage3.Puzzles
{
    public enum MemoryPathPuzzleState
    {
        Idle,
        ShowingPath,
        WaitingForInput,
        WaitingForPlayerToLeaveGrid,
        Completed,
        Failed
    }

    [DisallowMultipleComponent]
    public class MemoryPathPuzzle : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private PathTile[] tiles;
        [SerializeField] private int gridSize = 4;
        [SerializeField] private int pathLength = 5;

        [Header("Timing")]
        [SerializeField] private float tileShowDuration = 0.8f;
        [SerializeField] private float delayBetweenTiles = 0.25f;

        [Header("Penalty")]
        [SerializeField] private float failurePenalty = 10f;

        [Header("Path Generation")]
        [SerializeField] private int maximumGenerationAttempts = 100;

        [Header("Neighbour Detection")]
        [SerializeField] private float neighbourDistance = 1.6f;
        [SerializeField] private float rowColumnTolerance = 0.25f;

        [Header("Click Retry")]
        [SerializeField] private float retryDelay = 0.8f;

        [Header("Mouse Click Detection")]
        [Tooltip("Assign the camera that views the floor tiles. If null, MainCamera is used.")]
        [SerializeField] private Camera puzzleCamera;

        [Tooltip("Set to Everything initially. Tile colliders must be included.")]
        [SerializeField] private LayerMask tileClickLayers = ~0;

        [Tooltip("Maximum distance for the camera ray.")]
        [SerializeField] private float clickRayDistance = 1000f;

        public event Action OnPuzzleCompleted;

        public static MemoryPathPuzzle Instance { get; private set; }
        public bool IsCompleted { get; private set; }

        public MemoryPathPuzzleState CurrentState { get; private set; }
            = MemoryPathPuzzleState.Idle;

        private readonly List<int> currentPathIndices = new List<int>();
        private int playerPathIndex;

        private Coroutine pathCoroutine;
        private Coroutine recoveryCoroutine;

        private bool failurePenaltyApplied;

        // ---------------------------------------------------------
        // INITIALIZATION
        // ---------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            FindTilesIfNeeded();
            ConfigureTileCoordinatesIfNeeded();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            FindTilesIfNeeded();
            SubscribeToTiles();
        }

        private void Start()
        {
            FindTilesIfNeeded();
            ConfigureTileCoordinatesIfNeeded();
            ResolveCamera();

            if (tiles == null || tiles.Length == 0)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] No PathTile objects found. " +
                    "Assign the tiles in the Inspector."
                );
                return;
            }

            Debug.Log(
                $"[MemoryPathPuzzle] Initialized with {tiles.Length} tiles."
            );

            ResetPuzzle();
        }

        private void OnDisable()
        {
            UnsubscribeFromTiles();
            StopRunningCoroutines();
        }

        private void FindTilesIfNeeded()
        {
            if (tiles == null || tiles.Length == 0)
            {
                tiles = GetComponentsInChildren<PathTile>(true);
            }
        }

        private Camera ResolveCamera()
        {
            if (puzzleCamera != null)
                return puzzleCamera;

            puzzleCamera = Camera.main;

            if (puzzleCamera == null)
                puzzleCamera = FindFirstObjectByType<Camera>();

            if (puzzleCamera == null)
            {
                Debug.LogWarning(
                    "[MemoryPathPuzzle] No camera found in scene. " +
                    "Assign Puzzle Camera in the Inspector."
                );
            }

            return puzzleCamera;
        }

        public void ConfigureGrid(PathTile[] configuredTiles)
        {
            UnsubscribeFromTiles();

            tiles = configuredTiles;

            ConfigureTileCoordinatesIfNeeded();
            SubscribeToTiles();

            if (tiles == null || tiles.Length == 0)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] ConfigureGrid received no tiles."
                );
                return;
            }

            ResetPuzzle();
        }

        // ---------------------------------------------------------
        // TILE COORDINATES
        // ---------------------------------------------------------

        private void ConfigureTileCoordinatesIfNeeded()
        {
            if (tiles == null || tiles.Length == 0)
                return;

            List<PathTile> validTiles = new List<PathTile>();
            foreach (PathTile tile in tiles)
            {
                if (tile != null)
                    validTiles.Add(tile);
            }

            if (validTiles.Count == 0)
                return;

            // Check if existing coordinates are complete, non-negative, and unique
            HashSet<string> coordinates = new HashSet<string>();
            bool coordinatesValid = validTiles.Count == (gridSize * gridSize);

            if (coordinatesValid)
            {
                foreach (PathTile tile in validTiles)
                {
                    string key = $"{tile.TileX},{tile.TileY}";
                    if (!coordinates.Add(key)
                        || tile.TileX < 0
                        || tile.TileY < 0
                        || tile.TileX >= gridSize
                        || tile.TileY >= gridSize)
                    {
                        coordinatesValid = false;
                        break;
                    }
                }
            }

            if (coordinatesValid)
                return;

            // Reassign grid coordinates from physical world positions.
            // Z represents rows (min Z = row 0, max Z = row gridSize - 1)
            // X represents columns (min X = col 0, max X = col gridSize - 1)
            validTiles.Sort(
                (a, b) => a.transform.position.z.CompareTo(b.transform.position.z)
            );

            List<List<PathTile>> rows = new List<List<PathTile>>();
            foreach (PathTile tile in validTiles)
            {
                float z = tile.transform.position.z;
                int rowIndex = rows.FindIndex(
                    row => Mathf.Abs(row[0].transform.position.z - z) <= rowColumnTolerance
                );

                if (rowIndex < 0)
                    rows.Add(new List<PathTile> { tile });
                else
                    rows[rowIndex].Add(tile);
            }

            rows.Sort(
                (a, b) => a[0].transform.position.z.CompareTo(b[0].transform.position.z)
            );

            for (int r = 0; r < rows.Count; r++)
            {
                rows[r].Sort(
                    (a, b) => a.transform.position.x.CompareTo(b.transform.position.x)
                );

                for (int c = 0; c < rows[r].Count; c++)
                {
                    rows[r][c].ConfigureTile(r, c);
                }
            }

            Debug.Log(
                "[MemoryPathPuzzle] Assigned grid coordinates from physical positions."
            );
        }

        // ---------------------------------------------------------
        // TILE EVENT SUBSCRIPTIONS
        // ---------------------------------------------------------

        private void SubscribeToTiles()
        {
            if (tiles == null)
                return;

            foreach (PathTile tile in tiles)
            {
                if (tile == null)
                    continue;

                tile.OnTileClicked -= HandleTileClicked;
                tile.OnTileClicked += HandleTileClicked;
            }
        }

        private void UnsubscribeFromTiles()
        {
            if (tiles == null)
                return;

            foreach (PathTile tile in tiles)
            {
                if (tile != null)
                    tile.OnTileClicked -= HandleTileClicked;
            }
        }

        // ---------------------------------------------------------
        // CAMERA RAYCAST INPUT (NEW INPUT SYSTEM)
        // ---------------------------------------------------------

        private void Update()
        {
            if (CurrentState != MemoryPathPuzzleState.WaitingForInput)
                return;

            if (IsCompleted)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            // Detect left mouse click using the Unity Input System package
            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            Camera cam = ResolveCamera();
            if (cam == null)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] No camera found. " +
                    "Assign Puzzle Camera in the Inspector."
                );
                return;
            }

            // If the cursor is locked (first-person look), raycast from center of screen.
            // If the cursor is free, raycast from the mouse cursor position.
            Vector3 screenPos = (Cursor.lockState == CursorLockMode.Locked)
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : (Vector3)mouse.position.ReadValue();

            Ray ray = cam.ScreenPointToRay(screenPos);

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                clickRayDistance,
                tileClickLayers,
                QueryTriggerInteraction.Collide
            );

            PathTile clickedTile = null;
            float nearestTileDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                PathTile tile = hit.collider.GetComponentInParent<PathTile>();

                if (tile == null || !tile.isActive)
                    continue;

                if (hit.distance < nearestTileDistance)
                {
                    nearestTileDistance = hit.distance;
                    clickedTile = tile;
                }
            }

            if (clickedTile == null)
            {
                Debug.Log(
                    "[MemoryPathPuzzle] Mouse click detected, " +
                    "but no active tile collider was hit."
                );
                return;
            }

            Debug.Log(
                $"[MemoryPathPuzzle] Raycast selected tile " +
                $"({clickedTile.TileX},{clickedTile.TileY})."
            );

            clickedTile.NotifyClicked();
        }

        // ---------------------------------------------------------
        // START PUZZLE
        // ---------------------------------------------------------

        public void StartPuzzle()
        {
            if (IsCompleted)
                return;

            FindTilesIfNeeded();
            ConfigureTileCoordinatesIfNeeded();

            if (tiles == null || tiles.Length == 0)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] Cannot start: no PathTiles."
                );
                return;
            }

            StopRunningCoroutines();
            SubscribeToTiles();

            SetTilesDetection(false);
            ResetAllTiles();

            if (!GenerateConnectedRandomPath())
            {
                CurrentState = MemoryPathPuzzleState.Failed;
                Debug.LogError(
                    "[MemoryPathPuzzle] Could not generate a connected path."
                );
                return;
            }

            playerPathIndex = 0;
            failurePenaltyApplied = false;

            Debug.Log(
                "[MemoryPathPuzzle] GENERATED PATH: " +
                GetPathDescription()
            );

            pathCoroutine = StartCoroutine(ShowPathSequence());
        }

        private void StopRunningCoroutines()
        {
            if (pathCoroutine != null)
            {
                StopCoroutine(pathCoroutine);
                pathCoroutine = null;
            }

            if (recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = null;
            }
        }

        // ---------------------------------------------------------
        // PERIMETER CHECK
        // ---------------------------------------------------------

        private bool IsPerimeterTile(PathTile tile)
        {
            if (tile == null)
                return false;

            int last = gridSize - 1;

            return tile.TileX == 0
                || tile.TileY == 0
                || tile.TileX == last
                || tile.TileY == last;
        }

        // ---------------------------------------------------------
        // RANDOM CONNECTED PATH GENERATION
        // ---------------------------------------------------------

        private bool GenerateConnectedRandomPath()
        {
            currentPathIndices.Clear();

            if (tiles == null || tiles.Length == 0)
                return false;

            int targetLength = Mathf.Clamp(
                pathLength,
                1,
                tiles.Length
            );

            List<int> perimeterStarts = new List<int>();
            int validCount = 0;

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                    continue;

                validCount++;

                if (IsPerimeterTile(tiles[i]))
                    perimeterStarts.Add(i);
            }

            if (validCount < targetLength || perimeterStarts.Count == 0)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] Invalid grid or no perimeter tiles. " +
                    "Check Grid Size and tile coordinates."
                );
                return false;
            }

            for (int attempt = 0;
                attempt < Mathf.Max(1, maximumGenerationAttempts);
                attempt++)
            {
                currentPathIndices.Clear();

                int startIndex = perimeterStarts[
                    UnityEngine.Random.Range(0, perimeterStarts.Count)
                ];

                currentPathIndices.Add(startIndex);

                if (BuildConnectedPath(startIndex, targetLength)
                    && currentPathIndices.Count == targetLength)
                {
                    return true;
                }
            }

            currentPathIndices.Clear();
            return false;
        }

        private bool BuildConnectedPath(int currentIndex, int targetLength)
        {
            if (currentPathIndices.Count >= targetLength)
                return true;

            List<int> neighbours = GetPhysicalNeighbours(currentIndex);
            ShuffleIntegerList(neighbours);

            foreach (int nextIndex in neighbours)
            {
                if (currentPathIndices.Contains(nextIndex))
                    continue;

                currentPathIndices.Add(nextIndex);

                if (BuildConnectedPath(nextIndex, targetLength))
                    return true;

                currentPathIndices.RemoveAt(currentPathIndices.Count - 1);
            }

            return false;
        }

        private List<int> GetPhysicalNeighbours(int index)
        {
            List<int> neighbours = new List<int>();

            if (tiles == null
                || index < 0
                || index >= tiles.Length
                || tiles[index] == null)
            {
                return neighbours;
            }

            Vector3 current = tiles[index].transform.position;

            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == index || tiles[i] == null)
                    continue;

                if (currentPathIndices.Contains(i))
                    continue;

                Vector3 other = tiles[i].transform.position;

                float dx = Mathf.Abs(other.x - current.x);
                float dz = Mathf.Abs(other.z - current.z);

                // Physical adjacency: strictly orthogonal, no diagonals
                bool physicalAdjacent =
                    (dz <= rowColumnTolerance && dx > 0.01f && dx <= neighbourDistance)
                    || (dx <= rowColumnTolerance && dz > 0.01f && dz <= neighbourDistance);

                // Grid coordinate adjacency: Manhattan distance of 1
                bool coordAdjacent =
                    (Mathf.Abs(tiles[i].TileX - tiles[index].TileX) +
                     Mathf.Abs(tiles[i].TileY - tiles[index].TileY)) == 1;

                if (physicalAdjacent || coordAdjacent)
                {
                    // Strict prevention of diagonal jumps
                    bool isDiagonal =
                        (dx > rowColumnTolerance && dz > rowColumnTolerance)
                        && (Mathf.Abs(tiles[i].TileX - tiles[index].TileX) >= 1 &&
                            Mathf.Abs(tiles[i].TileY - tiles[index].TileY) >= 1);

                    if (!isDiagonal)
                        neighbours.Add(i);
                }
            }

            return neighbours;
        }

        private void ShuffleIntegerList(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                int temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        // ---------------------------------------------------------
        // DISPLAY PATH
        // ---------------------------------------------------------

        private IEnumerator ShowPathSequence()
        {
            CurrentState = MemoryPathPuzzleState.ShowingPath;
            SetTilesDetection(false);
            ResetAllTiles();

            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < currentPathIndices.Count; i++)
            {
                int index = currentPathIndices[i];

                if (index < 0
                    || index >= tiles.Length
                    || tiles[index] == null)
                {
                    continue;
                }

                PathTile tile = tiles[index];
                tile.SetState(TileState.Highlighted);

                yield return new WaitForSeconds(tileShowDuration);

                tile.SetState(TileState.Neutral);

                yield return new WaitForSeconds(delayBetweenTiles);
            }

            ResetAllTiles();

            playerPathIndex = 0;
            CurrentState = MemoryPathPuzzleState.WaitingForInput;

            SetTilesDetection(true);

            FeedbackHUD.ShowMessage(
                "PATH LOCKED — Click the tiles in order.",
                Color.white
            );

            Debug.Log(
                "[MemoryPathPuzzle] Ready for mouse input. " +
                $"Expected first tile: {currentPathIndices[0]}"
            );

            pathCoroutine = null;
        }

        private void SetTilesDetection(bool enabled)
        {
            if (tiles == null)
                return;

            foreach (PathTile tile in tiles)
            {
                if (tile != null)
                    tile.SetDetection(enabled);
            }
        }

        // ---------------------------------------------------------
        // HANDLE TILE CLICK
        // ---------------------------------------------------------

        private void HandleTileClicked(PathTile tile)
        {
            if (tile == null || IsCompleted)
                return;

            if (SecurityLockdownEvent.Instance != null && !SecurityLockdownEvent.Instance.IsDeactivated)
            {
                FeedbackHUD.ShowMessage("GRID OFFLINE — Deactivate lockdown alarm at red control box first!", Color.red);
                return;
            }

            if (CurrentState != MemoryPathPuzzleState.WaitingForInput)
                return;

            if (playerPathIndex < 0 || playerPathIndex >= currentPathIndices.Count)
                return;

            int expectedIndex = currentPathIndices[playerPathIndex];

            if (expectedIndex < 0
                || expectedIndex >= tiles.Length
                || tiles[expectedIndex] == null)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] Expected path tile is missing."
                );
                return;
            }

            PathTile expectedTile = tiles[expectedIndex];

            if (tile == expectedTile)
            {
                tile.SetState(TileState.Correct);
                playerPathIndex++;

                Stage3AudioManager.Instance?.PlayPathCorrect();

                Debug.Log(
                    $"[MemoryPathPuzzle] Correct click " +
                    $"{playerPathIndex}/{currentPathIndices.Count}"
                );

                if (playerPathIndex >= currentPathIndices.Count)
                    CompletePuzzle();

                return;
            }

            Debug.LogWarning(
                $"[MemoryPathPuzzle] Wrong click at " +
                $"({tile.TileX},{tile.TileY})."
            );

            tile.FlashError();
            Stage3AudioManager.Instance?.PlayPathError();
            BeginWrongPathRecovery();
        }

        // ---------------------------------------------------------
        // WRONG CLICK AND AUTOMATIC RETRY
        // ---------------------------------------------------------

        private void BeginWrongPathRecovery()
        {
            if (CurrentState == MemoryPathPuzzleState.Failed
                || CurrentState == MemoryPathPuzzleState.Completed)
            {
                return;
            }

            CurrentState = MemoryPathPuzzleState.Failed;
            SetTilesDetection(false);

            if (!failurePenaltyApplied)
            {
                Stage3Timer timer = Stage3Timer.Instance != null
                    ? Stage3Timer.Instance
                    : FindFirstObjectByType<Stage3Timer>();

                if (timer != null)
                {
                    timer.ApplyPenalty(failurePenalty);
                }
                else
                {
                    Debug.LogWarning(
                        "[MemoryPathPuzzle] Stage3Timer not found; " +
                        "the failure penalty could not be applied."
                    );
                }

                failurePenaltyApplied = true;
            }

            FeedbackHUD.ShowMessage(
                "WRONG TILE — TRY AGAIN",
                Color.red
            );

            if (recoveryCoroutine != null)
                StopCoroutine(recoveryCoroutine);

            recoveryCoroutine = StartCoroutine(RetryWithNewPath());
        }

        private IEnumerator RetryWithNewPath()
        {
            yield return new WaitForSeconds(retryDelay);

            ResetAllTiles();

            if (IsCompleted)
            {
                recoveryCoroutine = null;
                yield break;
            }

            if (!GenerateConnectedRandomPath())
            {
                CurrentState = MemoryPathPuzzleState.Failed;
                recoveryCoroutine = null;

                Debug.LogError(
                    "[MemoryPathPuzzle] Could not generate a retry path."
                );
                yield break;
            }

            playerPathIndex = 0;
            failurePenaltyApplied = false;

            recoveryCoroutine = null;
            pathCoroutine = StartCoroutine(ShowPathSequence());
        }

        // ---------------------------------------------------------
        // COMPLETE PUZZLE
        // ---------------------------------------------------------

        private void CompletePuzzle()
        {
            if (IsCompleted)
                return;

            if (playerPathIndex != currentPathIndices.Count)
                return;

            int expectedLength = Mathf.Clamp(
                pathLength,
                1,
                tiles.Length
            );

            if (currentPathIndices.Count != expectedLength)
            {
                Debug.LogError(
                    "[MemoryPathPuzzle] Completion blocked: " +
                    "invalid path length."
                );
                return;
            }

            IsCompleted = true;
            CurrentState = MemoryPathPuzzleState.Completed;

            SetTilesDetection(false);
            StopRunningCoroutines();

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag("PATH_PUZZLE_COMPLETE");
                ObjectiveManager.Instance.CompleteFlag("STAGE3_PATH_SOLVED");
            }

            FeedbackHUD.ShowMessage(
                "PATH PUZZLE COMPLETE",
                Color.green
            );

            Stage3AudioManager.Instance?.PlayCompletionFanfare();

            Debug.Log(
                "[MemoryPathPuzzle] PATH PUZZLE COMPLETE!"
            );

            OnPuzzleCompleted?.Invoke();
        }

        // ---------------------------------------------------------
        // RESET
        // ---------------------------------------------------------

        public void ResetPuzzle()
        {
            StopRunningCoroutines();

            IsCompleted = false;
            playerPathIndex = 0;
            failurePenaltyApplied = false;

            CurrentState = MemoryPathPuzzleState.Idle;

            currentPathIndices.Clear();

            SetTilesDetection(false);
            ResetAllTiles();
        }

        private void ResetAllTiles()
        {
            if (tiles == null)
                return;

            foreach (PathTile tile in tiles)
            {
                if (tile != null)
                    tile.ResetTile();
            }
        }

        // ---------------------------------------------------------
        // DEBUG HELPERS
        // ---------------------------------------------------------

        public string GetPathDescription()
        {
            if (currentPathIndices.Count == 0)
                return "No path generated.";

            List<string> parts = new List<string>();

            foreach (int index in currentPathIndices)
            {
                if (index >= 0
                    && index < tiles.Length
                    && tiles[index] != null)
                {
                    PathTile tile = tiles[index];
                    parts.Add($"[{index}]({tile.TileX},{tile.TileY})");
                }
                else
                {
                    parts.Add($"[{index}]NULL");
                }
            }

            return string.Join(" -> ", parts);
        }

        public int GetPathLength()
        {
            return currentPathIndices.Count;
        }

        public bool IsWaitingForInput()
        {
            return CurrentState == MemoryPathPuzzleState.WaitingForInput;
        }
    }
}