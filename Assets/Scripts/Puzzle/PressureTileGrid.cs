using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Manages the 5x4 pressure tile floor corridor in Level 3 (Vault Cellar).
    /// Validates player tile steps against PlaythroughGameState.Instance.IsTileSafe(row, col).
    /// Stepping on a safe tile: illuminates tile green.
    /// Stepping on an unsafe tile: triggers alarm audio, applies -10s penalty to stage timer,
    /// flashes red screen, and teleports player back to start of tile grid!
    /// </summary>
    [DisallowMultipleComponent]
    public class PressureTileGrid : MonoBehaviour
    {
        private static PressureTileGrid instance;
        public static PressureTileGrid Instance => instance;

        [Header("Grid Setup")]
        [SerializeField] private Vector3 startPosition = new Vector3(-2.25f, 0.05f, -6.5f);
        [SerializeField] private Vector3 startTeleportPosition = new Vector3(0f, 1.6f, -4.6f);
        [SerializeField] private float tileSpacingX = 1.5f;
        [SerializeField] private float tileSpacingZ = 1.0f;
        [SerializeField] private float tileSizeX = 1.4f;
        [SerializeField] private float tileSizeZ = 0.9f;

        [Header("State")]
        [SerializeField] private bool isResetting = false;

        private List<PressureTile> allTiles = new List<PressureTile>();

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            EnsureGridGenerated();
        }

        public void EnsureGridGenerated()
        {
            if (allTiles.Count > 0) return;

            // Generate 5 rows x 4 cols if not built in inspector
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Vector3 pos = new Vector3(
                        startPosition.x + c * tileSpacingX,
                        startPosition.y,
                        startPosition.z - r * tileSpacingZ);

                    GameObject tileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tileGo.name = $"PressureTile_R{r}_C{c}";
                    tileGo.transform.SetParent(transform, false);
                    tileGo.transform.position = pos;
                    tileGo.transform.localScale = new Vector3(tileSizeX, 0.08f, tileSizeZ);

                    var tileComp = tileGo.AddComponent<PressureTile>();
                    tileComp.Configure(r, c, this);
                    allTiles.Add(tileComp);
                }
            }
        }

        public void OnTileStepped(PressureTile tile)
        {
            if (isResetting) return;

            bool isSafe = PlaythroughGameState.Instance != null
                ? PlaythroughGameState.Instance.IsTileSafe(tile.Row, tile.Col)
                : (tile.Col == 1 || tile.Col == 2);

            if (isSafe)
            {
                tile.SetSafeVisual();
                EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadDigit, 0.5f);
                Debug.Log($"<color=#5cb85c><b>[PressureTileGrid]</b></color> Safe tile stepped: R{tile.Row}_C{tile.Col}");
            }
            else
            {
                tile.SetUnsafeVisual();
                StartCoroutine(TriggerAlarmAndReset(tile));
            }
        }

        private IEnumerator TriggerAlarmAndReset(PressureTile failedTile)
        {
            isResetting = true;
            Debug.Log($"<color=#d9534f><b>[PressureTileGrid]</b></color> Unsafe tile stepped at R{failedTile.Row}_C{failedTile.Col}! Triggering alarm reset.");

            // Alarm sound
            EscapeRoom.Audio.EscapeRoomAudio.Play(EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeypadWrong, 1.0f);

            // Time penalty (-10s)
            if (EscapeRoom.UI.StageTimer.Instance != null)
            {
                EscapeRoom.UI.StageTimer.Instance.ApplyPenalty(10f);
            }

            EscapeRoom.UI.FeedbackHUD.ShowMessage("WRONG TILE! -10s Penalty  ·  Teleported Back!", new Color(0.95f, 0.35f, 0.35f));

            yield return new WaitForSecondsRealtime(0.5f);

            // Teleport player back to start of tile section
            var player = GameObject.FindWithTag("Player");
            if (player == null) player = FindAnyObjectByType<PlayerController>()?.gameObject;

            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = startTeleportPosition;
                if (cc != null) cc.enabled = true;
            }

            // Reset all tile visuals
            foreach (var tile in allTiles)
            {
                tile.ResetTile();
            }

            yield return new WaitForSecondsRealtime(0.4f);
            isResetting = false;
        }
    }
}
