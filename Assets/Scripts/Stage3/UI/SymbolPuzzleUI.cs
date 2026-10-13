using System;
using UnityEngine;
using EscapeRoom.Stage3.Puzzles;

namespace EscapeRoom.Stage3.UI
{
    /// <summary>
    /// Supplementary UI controller for symbol puzzle visualization on in-world screens.
    /// Manages equation display rendering and status feedback.
    /// </summary>
    public class SymbolPuzzleUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SymbolMappingPuzzle puzzle;
        [SerializeField] private TextMesh screenText;

        private void Update()
        {
            if (puzzle == null)
            {
                puzzle = SymbolMappingPuzzle.Instance;
            }

            if (screenText != null && puzzle != null)
            {
                if (puzzle.IsCompleted)
                {
                    screenText.text = "TERMINAL OVERRIDE:\n[ACCESS GRANTED]";
                    screenText.color = new Color(0.35f, 0.95f, 0.35f);
                }
                else
                {
                    screenText.text = $"SECURITY EQUATION:\n{puzzle.EquationDisplay}";
                    screenText.color = new Color(0.95f, 0.85f, 0.35f);
                }
            }
        }
    }
}
