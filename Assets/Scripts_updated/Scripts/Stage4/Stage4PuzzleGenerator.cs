using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Stage 4 - Final Control Room.
/// Generates ONE random, validated, solvable puzzle configuration per run.
/// Called once by Stage4Manager.StartStage4(). Never regenerates in Update.
///
/// FINAL CODE RECIPE (4 digits, all derived from earlier puzzles):
///   Digit 1 = answer of the Pattern puzzle (0-9)
///   Digit 2 = colour number of the LAST light in the Memory sequence
///             (Red 1, Blue 2, Green 3, Yellow 4)
///   Digit 3 = number of the Control Panel whose colour matches the FIRST light
///             (Panel A 1, B 2, C 3, D 4)
///   Digit 4 = number of the terminal that is wired LAST in the Wire puzzle
///             (the terminal with the highest voltage, numbered 1-6)
/// </summary>
public class Stage4PuzzleGenerator : MonoBehaviour
{
    public enum PatternType { Increasing, Decreasing, Alternating, Cycle }

    // Colour ids used everywhere in Stage 4 (spec: RED=0, BLUE=1, GREEN=2, YELLOW=3)
    public static readonly string[] ColorNames = { "RED", "BLUE", "GREEN", "YELLOW" };
    public const int ColorCount = 4;
    public const int PanelCount = 4;
    public const int PatternShownCount = 4; // visible terms; the 5th is the "?" answer

    [Header("Seed (testing only)")]
    [Tooltip("Keep OFF for normal play. ON = same puzzle every run (useful for debugging).")]
    [SerializeField] private bool useFixedSeed = false;
    [SerializeField] private int fixedSeed = 12345;

    [Header("Memory puzzle")]
    [SerializeField, Range(4, 6)] private int memoryLengthMin = 4;
    [SerializeField, Range(4, 6)] private int memoryLengthMax = 5;

    [Header("Wire puzzle")]
    [Tooltip("How many terminals the junction box has (4-6).")]
    [SerializeField, Range(4, 6)] private int wireCount = 5;

    [Header("Safety")]
    [SerializeField] private int maxGenerationAttempts = 200;

    // ---- generated data (created once per run) ----
    private bool hasPuzzle;
    private PatternType patternType;
    private int[] patternShown;       // 4 visible numbers
    private int patternAnswer;        // 0-9
    private int[] memorySequence;     // colour ids 0-3
    private int[] panelColors;        // panelColors[panelIndex] = colour id
    private int correctPanel;         // 0-3
    private int[] wireVoltages;       // voltage shown on each terminal (all different)
    private int[] wireOrder;          // terminal indices (0-based) from LOWEST to HIGHEST voltage
    private string finalCode;         // 4 digits

    private System.Random rng;
    private static string lastSignature = "";

    // Voltages the wire puzzle can show (volts). Plain, easy-to-compare numbers.
    private static readonly int[] VoltagePool = { 3, 5, 6, 9, 12, 15, 18, 24, 30, 36, 48 };

    // =====================================================================
    // PUBLIC API (spec section 11)
    // =====================================================================

    /// <summary>Creates a brand-new, validated puzzle configuration.</summary>
    public void GenerateNewPuzzle()
    {
        ClampSettings();
        hasPuzzle = false;

        int baseSeed = useFixedSeed
            ? fixedSeed
            : (Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode();
        rng = new System.Random(baseSeed);

        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            if (!BuildCandidate()) continue;   // discard invalid, generate again
            if (!ValidateAll()) continue;

            string signature = BuildSignature();
            if (!useFixedSeed && signature == lastSignature) continue; // must differ from last run

            lastSignature = signature;
            hasPuzzle = true;
            return;
        }

        // Extremely unlikely. Use a known-good configuration so the game never breaks.
        Debug.LogWarning("[Stage4Generator] Random generation failed; using fallback puzzle.");
        BuildFallback();
        if (ValidateAll())
        {
            hasPuzzle = true;
        }
        else
        {
            Debug.LogError("[Stage4Generator] Fallback puzzle is invalid. Check generator settings.");
        }
    }

    /// <summary>Correct answer of the Pattern puzzle as text ("7").</summary>
    public string GetPatternAnswer()
    {
        EnsureGenerated();
        return patternAnswer.ToString();
    }

    public int GetPatternAnswerValue()
    {
        EnsureGenerated();
        return patternAnswer;
    }

    /// <summary>The 4 visible numbers of the pattern. The player must find the 5th.</summary>
    public int[] GetPatternSequence()
    {
        EnsureGenerated();
        return (int[])patternShown.Clone();
    }

    public string GetPatternTypeName()
    {
        EnsureGenerated();
        return patternType.ToString();
    }

    public int[] GetMemorySequence()
    {
        EnsureGenerated();
        return (int[])memorySequence.Clone();
    }

    /// <summary>For each control panel (A,B,C,D = index 0-3), which colour id it shows.</summary>
    public int[] GetControlPanelColors()
    {
        EnsureGenerated();
        return (int[])panelColors.Clone();
    }

    public int GetCorrectControlPanel()
    {
        EnsureGenerated();
        return correctPanel;
    }

    public int GetWireCount()
    {
        return wireCount;
    }

    /// <summary>Voltage printed on each terminal (index 0 = Terminal 1).</summary>
    public int[] GetWireVoltages()
    {
        EnsureGenerated();
        return (int[])wireVoltages.Clone();
    }

    /// <summary>Terminal indices (0-based) in the order they must be connected: lowest to highest voltage.</summary>
    public int[] GetWireOrder()
    {
        EnsureGenerated();
        return (int[])wireOrder.Clone();
    }

    public string GetFinalCode()
    {
        EnsureGenerated();
        return finalCode;
    }

    public bool HasPuzzle()
    {
        return hasPuzzle;
    }

    public void ResetGeneration()
    {
        hasPuzzle = false;
        patternShown = null;
        memorySequence = null;
        panelColors = null;
        wireVoltages = null;
        wireOrder = null;
        finalCode = null;
        patternAnswer = 0;
        correctPanel = 0;
    }

    /// <summary>Short text the Cipher can use as a fair hint about how the code is built.</summary>
    public string GetCodeRecipe()
    {
        return "Digit 1: the pattern answer. " +
               "Digit 2: colour number of the LAST light (Red 1, Blue 2, Green 3, Yellow 4). " +
               "Digit 3: number of the panel matching the FIRST light (A 1, B 2, C 3, D 4). " +
               "Digit 4: number of the terminal you wire LAST (the highest voltage).";
    }

    public string GetDebugSummary()
    {
        if (!hasPuzzle) return "STAGE 4 DEBUG\n(no puzzle generated yet)";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("STAGE 4 DEBUG");
        sb.AppendLine("Pattern (" + patternType + "): " + string.Join(",", patternShown) + ",? -> " + patternAnswer);
        sb.AppendLine("Memory: " + string.Join(",", memorySequence));
        sb.AppendLine("Panel colours A-D: " + string.Join(",", panelColors) + "  Correct panel: " + correctPanel);
        sb.AppendLine("Wire voltages T1-T" + wireCount + ": " + string.Join(",", wireVoltages) + "  Order (terminal #): " + string.Join(">", Array.ConvertAll(wireOrder, i => (i + 1).ToString())));
        sb.AppendLine("Final Code: " + finalCode);
        return sb.ToString();
    }

    // =====================================================================
    // BUILDING
    // =====================================================================

    private void EnsureGenerated()
    {
        if (!hasPuzzle) GenerateNewPuzzle();
    }

    private void ClampSettings()
    {
        if (memoryLengthMax < memoryLengthMin) memoryLengthMax = memoryLengthMin;
        if (wireCount < 4) wireCount = 4;
        if (wireCount > 6) wireCount = 6;                            // keeps digit 4 between 1 and 6
        if (maxGenerationAttempts < 1) maxGenerationAttempts = 1;
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private bool BuildCandidate()
    {
        BuildPattern();
        BuildMemory();
        BuildPanels();
        if (!BuildWires()) return false;
        BuildCode();
        return true;
    }

    private void BuildPattern()
    {
        patternType = (PatternType)rng.Next(0, 4);
        patternShown = new int[PatternShownCount];

        switch (patternType)
        {
            case PatternType.Increasing:
            {
                int step = rng.Next(1, 3);                 // 1 or 2
                int maxStart = 9 - PatternShownCount * step; // keep answer <= 9
                int start = rng.Next(0, maxStart + 1);
                for (int i = 0; i < PatternShownCount; i++) patternShown[i] = start + i * step;
                patternAnswer = start + PatternShownCount * step;
                break;
            }
            case PatternType.Decreasing:
            {
                int step = rng.Next(1, 3);
                int minStart = PatternShownCount * step;   // keep answer >= 0
                int start = rng.Next(minStart, 10);
                for (int i = 0; i < PatternShownCount; i++) patternShown[i] = start - i * step;
                patternAnswer = start - PatternShownCount * step;
                break;
            }
            case PatternType.Alternating:
            {
                int a = rng.Next(0, 10);
                int b;
                do { b = rng.Next(0, 10); } while (b == a);
                patternShown[0] = a; patternShown[1] = b; patternShown[2] = a; patternShown[3] = b;
                patternAnswer = a;
                break;
            }
            default: // Cycle of three: A B C A ?  -> B
            {
                int a = rng.Next(0, 10);
                int b;
                do { b = rng.Next(0, 10); } while (b == a);
                int c;
                do { c = rng.Next(0, 10); } while (c == a || c == b);
                patternShown[0] = a; patternShown[1] = b; patternShown[2] = c; patternShown[3] = a;
                patternAnswer = b;
                break;
            }
        }
    }

    private void BuildMemory()
    {
        int length = rng.Next(memoryLengthMin, memoryLengthMax + 1);
        memorySequence = new int[length];
        memorySequence[0] = rng.Next(0, ColorCount);
        for (int i = 1; i < length; i++)
        {
            int v;
            do { v = rng.Next(0, ColorCount); } while (v == memorySequence[i - 1]); // no back-to-back repeats
            memorySequence[i] = v;
        }
    }

    private void BuildPanels()
    {
        // Random one-to-one mapping panel -> colour.
        panelColors = new int[PanelCount];
        for (int i = 0; i < PanelCount; i++) panelColors[i] = i;
        for (int i = PanelCount - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int tmp = panelColors[i]; panelColors[i] = panelColors[j]; panelColors[j] = tmp;
        }

        // The correct panel is the one showing the FIRST colour of the memory sequence.
        correctPanel = Array.IndexOf(panelColors, memorySequence[0]);
    }

    private bool BuildWires()
    {
        // Pick wireCount DIFFERENT voltages from the pool and give them to terminals 1..N in random order.
        List<int> pool = new List<int>(VoltagePool);
        wireVoltages = new int[wireCount];
        for (int i = 0; i < wireCount; i++)
        {
            int pick = rng.Next(0, pool.Count);
            wireVoltages[i] = pool[pick];
            pool.RemoveAt(pick);
        }

        // Correct connection order = terminal indices sorted from lowest to highest voltage.
        wireOrder = new int[wireCount];
        for (int i = 0; i < wireCount; i++) wireOrder[i] = i;
        Array.Sort(wireOrder, (a, b) => wireVoltages[a].CompareTo(wireVoltages[b]));

        // Reject orders that are too easy: 1-2-3-4-5 (ascending) or 5-4-3-2-1 (descending).
        bool ascending = true, descending = true;
        for (int i = 0; i < wireCount; i++)
        {
            if (wireOrder[i] != i) ascending = false;
            if (wireOrder[i] != wireCount - 1 - i) descending = false;
        }
        return !(ascending || descending);
    }

    private void BuildCode()
    {
        int d1 = patternAnswer;                                   // 0-9
        int d2 = memorySequence[memorySequence.Length - 1] + 1;   // 1-4
        int d3 = correctPanel + 1;                                // 1-4
        int d4 = wireOrder[wireOrder.Length - 1] + 1;             // 1-6 (terminal wired last)
        finalCode = d1.ToString() + d2.ToString() + d3.ToString() + d4.ToString();
    }

    private void BuildFallback()
    {
        patternType = PatternType.Increasing;
        patternShown = new[] { 1, 3, 5, 7 };
        patternAnswer = 9;

        memorySequence = new[] { 0, 2, 1, 3 };
        panelColors = new[] { 2, 0, 3, 1 };
        correctPanel = Array.IndexOf(panelColors, memorySequence[0]);

        // Known-good wire layout (5 terminals): 12V, 5V, 24V, 3V, 9V -> order T4, T2, T5, T1, T3.
        wireCount = 5;
        wireVoltages = new[] { 12, 5, 24, 3, 9 };
        wireOrder = new[] { 3, 1, 4, 0, 2 };

        BuildCode();
    }

    // =====================================================================
    // VALIDATION  (spec section 55: every puzzle must be solvable)
    // =====================================================================

    private bool ValidateAll()
    {
        return ValidatePattern()
            && ValidateMemory()
            && ValidatePanels()
            && ValidateWires()
            && ValidateCode();
    }

    private bool ValidatePattern()
    {
        if (patternShown == null || patternShown.Length != PatternShownCount) return false;
        if (patternAnswer < 0 || patternAnswer > 9) return false;
        for (int i = 0; i < patternShown.Length; i++)
            if (patternShown[i] < 0 || patternShown[i] > 9) return false;

        int[] t = patternShown;
        if (t[0] == t[1] && t[1] == t[2] && t[2] == t[3]) return false; // all equal = ambiguous

        // Every simple rule that fits the visible numbers must predict the SAME answer.
        // 1) constant difference
        int d = t[1] - t[0];
        if (t[2] - t[1] == d && t[3] - t[2] == d)
        {
            int next = t[3] + d;
            if (next >= 0 && next <= 9 && next != patternAnswer) return false;
        }
        // 2) repeating every 2
        if (t[0] == t[2] && t[1] == t[3] && t[2] != patternAnswer) return false;
        // 3) repeating every 3
        if (t[3] == t[0] && t[1] != patternAnswer) return false;

        return true;
    }

    private bool ValidateMemory()
    {
        if (memorySequence == null) return false;
        if (memorySequence.Length < memoryLengthMin || memorySequence.Length > memoryLengthMax) return false;

        bool[] seen = new bool[ColorCount];
        int distinct = 0;
        for (int i = 0; i < memorySequence.Length; i++)
        {
            int v = memorySequence[i];
            if (v < 0 || v >= ColorCount) return false;
            if (i > 0 && v == memorySequence[i - 1]) return false;
            if (!seen[v]) { seen[v] = true; distinct++; }
        }
        return distinct >= 3; // avoids very guessable sequences
    }

    private bool ValidatePanels()
    {
        if (panelColors == null || panelColors.Length != PanelCount) return false;

        bool[] seen = new bool[ColorCount];
        for (int i = 0; i < PanelCount; i++)
        {
            int c = panelColors[i];
            if (c < 0 || c >= ColorCount || seen[c]) return false; // each colour exactly once
            seen[c] = true;
        }

        if (correctPanel < 0 || correctPanel >= PanelCount) return false;
        return panelColors[correctPanel] == memorySequence[0];
    }

    private bool ValidateWires()
    {
        if (wireVoltages == null || wireOrder == null) return false;
        if (wireVoltages.Length != wireCount || wireOrder.Length != wireCount) return false;

        HashSet<int> volts = new HashSet<int>();
        HashSet<int> terminals = new HashSet<int>();
        for (int i = 0; i < wireCount; i++)
        {
            if (wireVoltages[i] <= 0) return false;
            if (!volts.Add(wireVoltages[i])) return false;            // every voltage must be different
            int t = wireOrder[i];
            if (t < 0 || t >= wireCount) return false;
            if (!terminals.Add(t)) return false;                       // each terminal used exactly once
            if (i > 0 && wireVoltages[wireOrder[i - 1]] >= wireVoltages[t]) return false; // strictly lowest -> highest
        }
        return true;
    }

    private bool ValidateCode()
    {
        if (string.IsNullOrEmpty(finalCode) || finalCode.Length != 4) return false;
        for (int i = 0; i < finalCode.Length; i++)
            if (finalCode[i] < '0' || finalCode[i] > '9') return false;
        return true;
    }

    private string BuildSignature()
    {
        return string.Join(",", patternShown) + "|" + patternAnswer + "|" +
               string.Join(",", memorySequence) + "|" +
               string.Join(",", panelColors) + "|" +
               string.Join(",", wireVoltages) + "|" + finalCode;
    }
}
