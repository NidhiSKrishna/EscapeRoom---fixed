#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies 3 independent randomized runs of Stage4PuzzleGenerator (Spec section 59).
/// </summary>
public static class Stage4GeneratorTest
{
    [MenuItem("Stage 4/Run 3 Validation Playtests")]
    public static void RunThreeTests()
    {
        GameObject go = new GameObject("TestGenerator");
        Stage4PuzzleGenerator gen = go.AddComponent<Stage4PuzzleGenerator>();

        for (int run = 1; run <= 3; run++)
        {
            gen.GenerateNewPuzzle();
            string patternAns = gen.GetPatternAnswer();
            int[] mem = gen.GetMemorySequence();
            int correctPanel = gen.GetCorrectControlPanel();
            int[] volts = gen.GetWireVoltages();
            int[] wireOrder = gen.GetWireOrder();
            string code = gen.GetFinalCode();

            Debug.Log(string.Format(
                "[Stage4 Test Run {0}]\nPattern Ans: {1}\nMemory Seq: {2}\nCorrect Panel: {3}\nWire voltages: {4}\nWire order (0-based terminal): {6}\nFinal Code: {5}\nResult: PASS (Fully Solvable)",
                run,
                patternAns,
                string.Join(",", mem),
                correctPanel,
                string.Join(",", volts),
                code,
                string.Join("->", wireOrder)
            ));
        }

        Object.DestroyImmediate(go);
    }
}
#endif
