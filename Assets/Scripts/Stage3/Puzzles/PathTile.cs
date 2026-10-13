using System;
using UnityEngine;

namespace EscapeRoom.Stage3.Puzzles
{
    public enum TileState
    {
        Neutral,
        Highlighted,
        Correct,
        Error
    }

    [DisallowMultipleComponent]
    public class PathTile : MonoBehaviour
    {
        // Used by MemoryPathPuzzle and Stage 3 systems
        public event Action<PathTile> OnTileClicked;
        public event Action<PathTile> OnPlayerStepped;

        [Header("Grid Coordinates")]
        [SerializeField] private int tileX;
        [SerializeField] private int tileY;

        [Header("Colors")]
        [SerializeField] private Color neutralColor = new Color(0.15f, 0.18f, 0.22f, 1f);
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color correctColor = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("State")]
        public TileState CurrentState { get; private set; } = TileState.Neutral;

        [Header("Interaction")]
        public bool isActive = false;

        public int TileX => tileX;
        public int TileY => tileY;

        private Renderer tileRenderer;
        private Material tileMaterial;

        private void Awake()
        {
            tileRenderer = GetComponent<Renderer>();

            if (tileRenderer == null)
                tileRenderer = GetComponentInChildren<Renderer>();

            if (tileRenderer != null)
                tileMaterial = tileRenderer.material;

            EnsureCollider();
            ApplyNeutralVisual();
        }

        // ---------------------------------------------------------
        // GRID CONFIGURATION
        // ---------------------------------------------------------

        public void ConfigureTile(int x, int y)
        {
            tileX = x;
            tileY = y;
        }

        // ---------------------------------------------------------
        // CLICK EVENT
        // MemoryPathPuzzle uses camera raycasting and calls this
        // method after finding the clicked tile.
        // ---------------------------------------------------------

        public void NotifyClicked()
        {
            if (!isActive)
                return;

            Debug.Log($"[PathTile] Clicked tile ({TileX},{TileY}).");
            OnTileClicked?.Invoke(this);
        }

        // Retained for compatibility with scripts that track player stepping.
        public void NotifyPlayerStepped()
        {
            if (!isActive)
                return;

            OnPlayerStepped?.Invoke(this);
        }

        // ---------------------------------------------------------
        // DETECTION CONTROL
        // ---------------------------------------------------------

        public void SetDetection(bool enabled)
        {
            isActive = enabled;
        }

        // ---------------------------------------------------------
        // STATE CONTROL
        // ---------------------------------------------------------

        public void SetState(TileState newState)
        {
            CancelInvoke(nameof(ApplyNeutralVisual));

            CurrentState = newState;

            switch (newState)
            {
                case TileState.Neutral:
                    ApplyNeutralVisual();
                    break;

                case TileState.Highlighted:
                    ApplyHighlightedVisual();
                    break;

                case TileState.Correct:
                    ApplyCorrectVisual();
                    break;

                case TileState.Error:
                    ApplyErrorVisual();
                    break;
            }
        }

        public void SetHighlighted()
        {
            SetState(TileState.Highlighted);
        }

        public void SetCorrect()
        {
            SetState(TileState.Correct);
        }

        public void SetError()
        {
            SetState(TileState.Error);
        }

        // ---------------------------------------------------------
        // VISUAL EFFECTS
        // ---------------------------------------------------------

        public void FlashHighlight(float duration = 0.25f)
        {
            SetState(TileState.Highlighted);
            Invoke(nameof(ApplyNeutralVisual), duration);
        }

        public void FlashCorrect(float duration = 0.35f)
        {
            SetState(TileState.Correct);
            Invoke(nameof(ApplyNeutralVisual), duration);
        }

        public void FlashError(float duration = 0.5f)
        {
            SetState(TileState.Error);
            Invoke(nameof(ApplyNeutralVisual), duration);
        }

        // ---------------------------------------------------------
        // RESET
        // ---------------------------------------------------------

        public void ResetTile()
        {
            CancelInvoke(nameof(ApplyNeutralVisual));
            ApplyNeutralVisual();
        }

        // ---------------------------------------------------------
        // VISUAL IMPLEMENTATION
        // ---------------------------------------------------------

        private void SetTileVisual(Color color, bool isEmissive)
        {
            if (tileMaterial == null)
                return;

            // Support both URP Lit (_BaseColor) and Standard/Legacy (_Color)
            if (tileMaterial.HasProperty("_BaseColor"))
                tileMaterial.SetColor("_BaseColor", color);

            if (tileMaterial.HasProperty("_Color"))
                tileMaterial.color = color;

            if (isEmissive)
            {
                tileMaterial.EnableKeyword("_EMISSION");
                if (tileMaterial.HasProperty("_EmissionColor"))
                    tileMaterial.SetColor("_EmissionColor", color * 1.5f);
            }
            else
            {
                if (tileMaterial.HasProperty("_EmissionColor"))
                    tileMaterial.SetColor("_EmissionColor", color * 0.15f);
            }
        }

        private void ApplyNeutralVisual()
        {
            CurrentState = TileState.Neutral;
            SetTileVisual(neutralColor, false);
        }

        private void ApplyHighlightedVisual()
        {
            CurrentState = TileState.Highlighted;
            SetTileVisual(highlightColor, true);
        }

        private void ApplyCorrectVisual()
        {
            CurrentState = TileState.Correct;
            SetTileVisual(correctColor, true);
        }

        private void ApplyErrorVisual()
        {
            CurrentState = TileState.Error;
            SetTileVisual(errorColor, true);
        }

        // ---------------------------------------------------------
        // COLLIDER
        // ---------------------------------------------------------

        private void EnsureCollider()
        {
            Collider existingCollider = GetComponent<Collider>();

            if (existingCollider != null)
                return;

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(0f, 0.05f, 0f);
            box.size = new Vector3(1f, 0.5f, 1f);
        }
    }
}