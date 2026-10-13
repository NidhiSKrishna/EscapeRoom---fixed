using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Definition of a single pattern recognition puzzle instance.
    /// </summary>
    [System.Serializable]
    public class PatternPuzzleData
    {
        public string puzzleTitle;
        public string category;
        public string sequenceDisplay;      // e.g., "3  ->  7  ->  11  ->  15  ->  [ ? ]"
        public string ruleExplanation;      // e.g., "Each step adds 4."
        public string correctAnswer;        // e.g., "19"
        public string[] answerOptions;      // 4 choices shuffled
        public string clueHint;             // Hint if player requests or examines panel
    }

    /// <summary>
    /// Definition of a physical search spot in the storage room.
    /// </summary>
    [System.Serializable]
    public class SearchSpotData
    {
        public string spotId;
        public string spotName;
        public string locationDescription;
        public string clueNote;
    }

    /// <summary>
    /// Independent runtime manager and generator for Stage 2 puzzles.
    /// Generates a randomized pattern puzzle, locked container combination, and search location
    /// per playthrough, guaranteeing consistent logic without guessing.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2PuzzleGenerator : MonoBehaviour
    {
        private static Stage2PuzzleGenerator instance;
        public static Stage2PuzzleGenerator Instance => instance;

        [Header("Runtime Puzzle State")]
        [SerializeField] private PatternPuzzleData activePattern;
        [SerializeField] private string containerCombination = "482";
        [SerializeField] private int validSearchSpotIndex = 0;
        [SerializeField] private bool isPatternSolved = false;
        [SerializeField] private bool isContainerUnlocked = false;
        [SerializeField] private bool isKeycardFound = false;
        [SerializeField] private bool isChipCollected = false;

        public PatternPuzzleData ActivePattern => activePattern;
        public string ContainerCombination => containerCombination;
        public int ValidSearchSpotIndex => validSearchSpotIndex;
        public bool IsPatternSolved => isPatternSolved;
        public bool IsContainerUnlocked => isContainerUnlocked;
        public bool IsKeycardFound => isKeycardFound;
        public bool IsChipCollected => isChipCollected;

        public static event Action OnPatternSolved;
        public static event Action OnContainerUnlocked;
        public static event Action OnKeycardAcquired;
        public static event Action OnChipCollected;

        private readonly SearchSpotData[] allSearchSpots = new SearchSpotData[]
        {
            new SearchSpotData
            {
                spotId = "desk_drawer",
                spotName = "Supervisor's Desk Drawer",
                locationDescription = "Inside the wooden desk drawer on the north wall.",
                clueNote = "FACILITY LOG: 'Shift supervisor locked the Terminal Security Chip inside the Supervisor's Desk Drawer on the north wall.'"
            },
            new SearchSpotData
            {
                spotId = "filing_cabinet",
                spotName = "Steel Filing Cabinet",
                locationDescription = "Inside the tall closed Steel Filing Cabinet on the west wall (next to the exit door - NOT the open shelving units).",
                clueNote = "FACILITY LOG: 'The Terminal Security Chip was filed in the tall Steel Filing Cabinet by the west wall exit door (NOT the open shelves).'"
            },
            new SearchSpotData
            {
                spotId = "metal_shelf",
                spotName = "Industrial Shelving Unit",
                locationDescription = "On the open metal shelf rack on the west wall (NOT the closed filing cabinet).",
                clueNote = "FACILITY LOG: 'The Terminal Security Chip was left on the open Industrial Shelving Unit rack along the west wall (NOT the closed cabinet).'"
            },
            new SearchSpotData
            {
                spotId = "supply_crate",
                spotName = "Supply Crate #04",
                locationDescription = "Inside reinforced wooden cargo Supply Crate #04 in the southwest corner.",
                clueNote = "FACILITY LOG: 'Cargo manifest lists the Terminal Security Chip inside wooden Supply Crate #04 in the southwest corner.'"
            },
            new SearchSpotData
            {
                spotId = "work_toolbox",
                spotName = "Work Table Tool Case",
                locationDescription = "Inside the red tool case on the central assembly work table.",
                clueNote = "FACILITY LOG: 'Technician placed the Terminal Security Chip inside the red Work Table Tool Case on the central table.'"
            }
        };

        public SearchSpotData[] AllSearchSpots => allSearchSpots;
        public SearchSpotData ActiveSearchSpot => allSearchSpots[Mathf.Clamp(validSearchSpotIndex, 0, allSearchSpots.Length - 1)];

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            InitializeRun();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Generates a randomized, consistent configuration for this session.
        /// </summary>
        public void InitializeRun()
        {
            isPatternSolved = false;
            isContainerUnlocked = false;
            isKeycardFound = false;
            isChipCollected = false;
            Stage2SearchSpot.ResetChipState();

            // 1. Generate 3-digit combination with distinct digits
            int d1 = UnityEngine.Random.Range(2, 9);
            int d2 = (d1 + UnityEngine.Random.Range(1, 8)) % 10;
            int d3 = (d2 + UnityEngine.Random.Range(1, 8)) % 10;
            containerCombination = $"{d1}{d2}{d3}";

            // 2. Select 1 of 5 search spots
            validSearchSpotIndex = UnityEngine.Random.Range(0, allSearchSpots.Length);

            // 3. Generate randomized pattern recognition puzzle
            activePattern = GenerateRandomPattern();

            Debug.Log($"<color=#5cb85c>[Stage2PuzzleGenerator]</color> Initialized Run: " +
                      $"Pattern Category='{activePattern.category}', TargetAnswer='{activePattern.correctAnswer}', " +
                      $"Container Combination={containerCombination}, ValidSearchSpot='{ActiveSearchSpot.spotName}' ({validSearchSpotIndex})");
        }

        private PatternPuzzleData GenerateRandomPattern()
        {
            int patternType = UnityEngine.Random.Range(0, 6);

            switch (patternType)
            {
                case 0:
                    // Arithmetic progression: a, a+d, a+2d, a+3d, ?
                    int startA = UnityEngine.Random.Range(2, 10);
                    int stepD = UnityEngine.Random.Range(3, 8);
                    int a1 = startA;
                    int a2 = a1 + stepD;
                    int a3 = a2 + stepD;
                    int a4 = a3 + stepD;
                    int aAns = a4 + stepD;
                    return BuildPattern(
                        "Arithmetic Progression Sequence",
                        "Number Progression",
                        $"{a1}   ➔   {a2}   ➔   {a3}   ➔   {a4}   ➔   [  ?  ]",
                        $"The sequence increases by +{stepD} at each step ({a4} + {stepD} = {aAns}).",
                        aAns.ToString(),
                        new string[] { (aAns - stepD).ToString(), (aAns + 1).ToString(), (aAns - 2).ToString() },
                        $"Notice the uniform increment between consecutive numbers: +{stepD}."
                    );

                case 1:
                    // Geometric / Multiplier progression
                    int baseG = UnityEngine.Random.Range(2, 4);
                    int multG = (baseG == 2) ? UnityEngine.Random.Range(2, 4) : 2;
                    int g1 = baseG;
                    int g2 = g1 * multG;
                    int g3 = g2 * multG;
                    int g4 = g3 * multG;
                    int gAns = g4 * multG;
                    return BuildPattern(
                        "Exponential Factor Sequence",
                        "Geometric Progression",
                        $"{g1}   ➔   {g2}   ➔   {g3}   ➔   {g4}   ➔   [  ?  ]",
                        $"Each number is multiplied by {multG} ({g4} × {multG} = {gAns}).",
                        gAns.ToString(),
                        new string[] { (g4 + 10).ToString(), (gAns - g3).ToString(), (gAns + multG).ToString() },
                        $"Check the ratio between consecutive numbers (x {multG})."
                    );

                case 2:
                    // Alternating Dual-Step Pattern (+A, -B)
                    int p1 = UnityEngine.Random.Range(4, 9);
                    int addVal = UnityEngine.Random.Range(5, 8);
                    int subVal = UnityEngine.Random.Range(2, 4);
                    int p2 = p1 + addVal;
                    int p3 = p2 - subVal;
                    int p4 = p3 + addVal;
                    int pAns = p4 - subVal;
                    return BuildPattern(
                        "Alternating Oscillation Sequence",
                        "Dual-Phase Arithmetic",
                        $"{p1}   ➔   {p2}   ➔   {p3}   ➔   {p4}   ➔   [  ?  ]",
                        $"The sequence alternates between adding {addVal} and subtracting {subVal}.",
                        pAns.ToString(),
                        new string[] { (p4 + addVal).ToString(), (pAns + 2).ToString(), (pAns - 3).ToString() },
                        $"Observe the alternating steps: +{addVal}, then -{subVal}."
                    );

                case 3:
                    // Additive Fibonacci-style (n[i] = n[i-1] + n[i-2])
                    int f1 = UnityEngine.Random.Range(1, 4);
                    int f2 = UnityEngine.Random.Range(3, 6);
                    int f3 = f1 + f2;
                    int f4 = f2 + f3;
                    int fAns = f3 + f4;
                    return BuildPattern(
                        "Cumulative Sum Sequence",
                        "Summation Progression",
                        $"{f1}   ➔   {f2}   ➔   {f3}   ➔   {f4}   ➔   [  ?  ]",
                        $"Each term is the sum of the preceding two numbers ({f3} + {f4} = {fAns}).",
                        fAns.ToString(),
                        new string[] { (f4 + f2).ToString(), (fAns - 1).ToString(), (fAns + 3).ToString() },
                        "Look at how the previous two numbers relate to the next."
                    );

                case 4:
                    // Directional Compass / Rotational Vector
                    string[] directions = new string[] { "NORTH [ ▲ ]", "EAST [ ▶ ]", "SOUTH [ ▼ ]", "WEST [ ◀ ]" };
                    int startDir = UnityEngine.Random.Range(0, 4);
                    int rotStep = (UnityEngine.Random.Range(0, 2) == 0) ? 1 : 3; // Clockwise or Counter-Clockwise
                    string dStepName = (rotStep == 1) ? "Clockwise 90°" : "Counter-Clockwise 90°";

                    string s1 = directions[startDir];
                    string s2 = directions[(startDir + rotStep) % 4];
                    string s3 = directions[(startDir + rotStep * 2) % 4];
                    string s4 = directions[(startDir + rotStep * 3) % 4];
                    string sAns = directions[(startDir + rotStep * 4) % 4]; // full circle back to start!

                    List<string> distractors = new List<string>();
                    for (int i = 0; i < 4; i++)
                    {
                        if (directions[i] != sAns)
                            distractors.Add(directions[i]);
                    }

                    return BuildPattern(
                        "Rotational Azimuth Sequence",
                        "Rotational Progression",
                        $"{s1}  ➔  {s2}  ➔  {s3}  ➔  {s4}  ➔  [  ?  ]",
                        $"The directional indicator rotates {dStepName} on each transition.",
                        sAns,
                        distractors.ToArray(),
                        $"Track the 90° {dStepName} rotation."
                    );

                default:
                    // Shape Vertex / Polygon Progression
                    string[] shapes = new string[]
                    {
                        "Triangle (3 Vertices)",
                        "Square (4 Vertices)",
                        "Pentagon (5 Vertices)",
                        "Hexagon (6 Vertices)",
                        "Heptagon (7 Vertices)",
                        "Octagon (8 Vertices)"
                    };
                    int shapeStart = UnityEngine.Random.Range(0, 2);
                    string sh1 = shapes[shapeStart];
                    string sh2 = shapes[shapeStart + 1];
                    string sh3 = shapes[shapeStart + 2];
                    string sh4 = shapes[shapeStart + 3];
                    string shAns = shapes[shapeStart + 4];

                    return BuildPattern(
                        "Geometric Polygon Progression",
                        "Morphological Vertex Count",
                        $"{sh1}  ➔  {sh2}  ➔  {sh3}  ➔  {sh4}  ➔  [  ?  ]",
                        "Each successive shape adds exactly 1 vertex / side.",
                        shAns,
                        new string[] { shapes[shapeStart], shapes[shapeStart + 1], "Nonagon (9 Vertices)" },
                        "Count the number of sides or vertices on each geometric figure."
                    );
            }
        }

        private PatternPuzzleData BuildPattern(string title, string category, string sequence, string rule, string correct, string[] rawDistractors, string hint)
        {
            List<string> options = new List<string> { correct };
            for (int i = 0; i < rawDistractors.Length && options.Count < 4; i++)
            {
                if (!options.Contains(rawDistractors[i]))
                {
                    options.Add(rawDistractors[i]);
                }
            }

            // Fill with safe fallback if not 4
            int extra = 99;
            while (options.Count < 4)
            {
                options.Add((extra++).ToString());
            }

            // Fisher-Yates shuffle options
            for (int i = options.Count - 1; i > 0; i--)
            {
                int r = UnityEngine.Random.Range(0, i + 1);
                string temp = options[i];
                options[i] = options[r];
                options[r] = temp;
            }

            return new PatternPuzzleData
            {
                puzzleTitle = title,
                category = category,
                sequenceDisplay = sequence,
                ruleExplanation = rule,
                correctAnswer = correct,
                answerOptions = options.ToArray(),
                clueHint = hint
            };
        }

        public bool ValidatePatternAnswer(string answer)
        {
            if (activePattern == null) return false;

            if (string.Equals(activePattern.correctAnswer.Trim(), answer.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                isPatternSolved = true;
                OnPatternSolved?.Invoke();
                EscapeRoom.Core.ObjectiveManager.Instance?.CompleteFlag("PATTERN_SOLVED");
                return true;
            }
            return false;
        }

        public bool ValidateContainerCode(string enteredCode)
        {
            if (string.Equals(containerCombination.Trim(), enteredCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                isContainerUnlocked = true;
                OnContainerUnlocked?.Invoke();
                EscapeRoom.Core.ObjectiveManager.Instance?.CompleteFlag("SAFE_UNLOCKED");
                return true;
            }
            return false;
        }

        public void RegisterChipFound()
        {
            isChipCollected = true;
            Stage2SearchSpot.SetChipCollected(true);
            OnChipCollected?.Invoke();
            Debug.Log("<color=#5cb85c>[Stage2PuzzleGenerator]</color> Security Chip registered. Pattern Terminal unlocked.");
        }

        public void RegisterKeycardFound()
        {
            isKeycardFound = true;
            OnKeycardAcquired?.Invoke();
        }
    }
}
