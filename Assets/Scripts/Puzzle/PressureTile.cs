using System.Collections;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Player;

namespace EscapeRoom.Puzzle
{
    /// <summary>
    /// Individual pressure tile component in the Level 3 (Vault) corridor.
    /// Detects when the Player steps on it and notifies the parent PressureTileGrid.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PressureTile : MonoBehaviour
    {
        [Header("Grid Position")]
        [SerializeField] private int row = 0;
        [SerializeField] private int col = 0;

        [Header("References")]
        [SerializeField] private Renderer tileRenderer;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.22f);
        [SerializeField] private Color safeColor = new Color(0.18f, 0.75f, 0.35f);
        [SerializeField] private Color unsafeColor = new Color(0.85f, 0.20f, 0.20f);

        private PressureTileGrid parentGrid;
        private bool isTriggered = false;
        private Material cachedMaterial;

        public int Row => row;
        public int Col => col;

        public void Configure(int r, int c, PressureTileGrid grid)
        {
            row = r;
            col = c;
            parentGrid = grid;
        }

        private void Awake()
        {
            if (tileRenderer == null) tileRenderer = GetComponent<Renderer>();
            if (tileRenderer != null)
            {
                cachedMaterial = tileRenderer.material;
            }

            var collider = GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
        }

        public void ResetTile()
        {
            isTriggered = false;
            SetColor(normalColor);
        }

        public void SetColor(Color c)
        {
            if (cachedMaterial != null && cachedMaterial.HasProperty("_Color"))
            {
                cachedMaterial.color = c;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isTriggered) return;

            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponent<PlayerController>() != null ||
                            other.GetComponentInParent<PlayerController>() != null ||
                            other.GetComponent<CharacterController>() != null;

            if (!isPlayer) return;

            isTriggered = true;

            if (parentGrid != null)
            {
                parentGrid.OnTileStepped(this);
            }
        }

        public void SetSafeVisual()
        {
            SetColor(safeColor);
        }

        public void SetUnsafeVisual()
        {
            SetColor(unsafeColor);
        }
    }
}
