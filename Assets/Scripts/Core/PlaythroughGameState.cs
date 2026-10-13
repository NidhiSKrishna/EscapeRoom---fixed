using System;
using UnityEngine;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Centralized game state tracking single-playthrough randomized values across all 3 stages.
    ///
    /// Level 1 (Attic):
    ///   • Level 1 Passcode ("4719" or randomized)
    /// Level 2 (Archive):
    ///   • Framed Pattern Solution ("30")
    ///   • Simon Code ("518")
    ///   • Lockdown Duration (45s)
    /// Level 3 (Vault):
    ///   • 5x4 Safe Tile Matrix
    ///   • Word Cipher ("IUHH" -> "FREE")
    ///   • Vault Lockdown Duration (40s)
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaythroughGameState : MonoBehaviour
    {
        private static PlaythroughGameState instance;
        public static PlaythroughGameState Instance => instance;

        // ── Available Symbol Catalogue ────────────────────────────────────────
        public static readonly string[] SymbolCatalogue = { "△", "○", "□", "★", "◇", "✦" };

        // ── State ─────────────────────────────────────────────────────────────
        [Header("Level 1 (Attic)")]
        [SerializeField] private string level1Passcode = "4719";

        [Header("Level 2 (Archive)")]
        [SerializeField] private int framedPatternSolution = 30; // 2, 6, 12, 20, 30
        [SerializeField] private string level2Passcode = "518";
        [SerializeField] private int[] symbolPattern = new int[4] { 0, 1, 2, 3 };

        [Header("Level 3 (Vault)")]
        [SerializeField] private bool[,] safeTileGrid = new bool[5, 4];
        [SerializeField] private string wordCipherEncrypted = "IUHH";
        [SerializeField] private string wordCipherDecrypted = "FREE";

        [Header("Status")]
        [SerializeField] private bool isInitialized = false;

        public string Passcode => level1Passcode;
        public string Level1Passcode => level1Passcode;
        public int FramedPatternSolution => framedPatternSolution;
        public string Level2Passcode => level2Passcode;
        public int[] SymbolPattern => symbolPattern;
        public bool[,] SafeTileGrid => safeTileGrid;
        public string WordCipherEncrypted => wordCipherEncrypted;
        public string WordCipherDecrypted => wordCipherDecrypted;
        public bool IsInitialized => isInitialized;

        public event Action OnPlaythroughStateInitialized;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void EnsureInitialized()
        {
            if (!isInitialized || string.IsNullOrEmpty(level1Passcode))
            {
                GenerateNewPlaythroughValues();
            }
        }

        /// <summary>
        /// Generates fresh session values for a completely new playthrough.
        /// </summary>
        public void GenerateNewPlaythroughValues()
        {
            // Level 1 Passcode (fixed to 8431 per Stage 1 specification)
            level1Passcode = "8431";

            // Level 2 Symbol & Passcode
            int code2 = UnityEngine.Random.Range(100, 1000);
            level2Passcode = code2.ToString();
            framedPatternSolution = 30;

            var indices = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4, 5 };
            symbolPattern = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int r = UnityEngine.Random.Range(0, indices.Count);
                symbolPattern[i] = indices[r];
                indices.RemoveAt(r);
            }

            // Level 3 Tile Matrix (5 rows x 4 cols, guaranteed safe path)
            safeTileGrid = new bool[5, 4];
            int currentCol = UnityEngine.Random.Range(1, 3); // start at col 1 or 2
            safeTileGrid[0, 1] = safeTileGrid[0, 2] = true;

            for (int r = 0; r < 5; r++)
            {
                safeTileGrid[r, currentCol] = true;
                if (r < 4)
                {
                    int nextCol = UnityEngine.Random.Range(0, 4);
                    int minC = Mathf.Min(currentCol, nextCol);
                    int maxC = Mathf.Max(currentCol, nextCol);
                    for (int c = minC; c <= maxC; c++)
                    {
                        safeTileGrid[r, c] = true;
                    }
                    currentCol = nextCol;
                }
            }

            wordCipherEncrypted = "IUHH";
            wordCipherDecrypted = "FREE";

            isInitialized = true;
            Debug.Log($"<color=#5cb85c><b>[PlaythroughGameState]</b></color> Playthrough Session Initialized!\n" +
                      $"  Level 1 Code: {level1Passcode}\n" +
                      $"  Level 2 Code: {level2Passcode}\n" +
                      $"  Level 3 Cipher: {wordCipherEncrypted} -> {wordCipherDecrypted}");

            OnPlaythroughStateInitialized?.Invoke();
        }

        public bool IsTileSafe(int row, int col)
        {
            EnsureInitialized();
            if (row < 0 || row >= 5 || col < 0 || col >= 4) return false;
            return safeTileGrid[row, col];
        }

        public string GetFormattedSymbolSequence()
        {
            EnsureInitialized();
            string[] syms = new string[symbolPattern.Length];
            for (int i = 0; i < symbolPattern.Length; i++)
            {
                int idx = Mathf.Clamp(symbolPattern[i], 0, SymbolCatalogue.Length - 1);
                syms[i] = SymbolCatalogue[idx];
            }
            return string.Join("   ", syms);
        }
    }
}
