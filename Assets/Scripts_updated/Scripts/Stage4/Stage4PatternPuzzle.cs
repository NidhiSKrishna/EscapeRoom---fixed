using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - Puzzle 1: PATTERN RECOGNITION (spec sections 13 and 14).
///
/// The player presses E on the Pattern panel, sees a number sequence with the last number hidden
/// (for example 3  5  7  9  ?), and picks the missing number (0-9).
/// The pattern comes from Stage4PuzzleGenerator and is always logically solvable (validated there).
///
/// Correct   -> SUCCESS, success sound, Stage4Manager.CompletePatternPuzzle()
/// Incorrect -> TRY AGAIN, error sound, small time penalty. The stage does NOT fail.
///
/// Put this script on the object the player looks at (it adds Stage4Interactable automatically).
/// The panel below is a simple temporary on-screen UI. It will be replaced by the Stage4Canvas UI
/// later; the puzzle logic (Initialize / SubmitAnswer / IsSolved / ResetPuzzle) stays the same.
/// </summary>
[RequireComponent(typeof(Stage4Interactable))]
public class Stage4PatternPuzzle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    [Header("Text")]
    [SerializeField] private string readyPrompt = "Press E to access Pattern Terminal";
    [SerializeField] private string solvedPrompt = "Pattern Terminal: SOLVED";

    private Stage4Interactable interactable;
    private Stage4PuzzleGenerator generator;

    private int[] shown;
    private int correctAnswer;
    private bool initialized;
    private bool solved;
    private bool panelOpen;
    private int attempts;
    private string message = "";
    private float closeAtTime = -1f;

    private GUIStyle boxStyle, titleStyle, textStyle, seqStyle, messageStyle, buttonStyle;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        interactable = GetComponent<Stage4Interactable>();
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        interactable.SetPrompt(readyPrompt);
        interactable.SetInteractable(false); // unlocked when the stage starts
    }

    private void OnEnable()
    {
        interactable.OnInteracted += OpenPanel;
        if (manager != null)
        {
            manager.OnStageStarted += HandleStageStarted;
            manager.OnStageReset += ResetPuzzle;
        }
    }

    private void OnDisable()
    {
        interactable.OnInteracted -= OpenPanel;
        if (manager != null)
        {
            manager.OnStageStarted -= HandleStageStarted;
            manager.OnStageReset -= ResetPuzzle;
        }
        ClosePanel();
    }

    private void Start()
    {
        // Safety: if the stage already started before this script was listening, catch up.
        if (!initialized && manager != null && manager.GetCurrentState() == Stage4State.PatternPuzzle)
        {
            Initialize(manager.Generator);
        }
    }

    private void Update()
    {
        if (!panelOpen) return;

        // Close on the RELEASE of Esc, so the player script does not also react to the same key press.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasReleasedThisFrame)
        {
            ClosePanel();
            return;
        }

        if (closeAtTime > 0f && Time.unscaledTime >= closeAtTime)
        {
            closeAtTime = -1f;
            ClosePanel();
        }
    }

    private void HandleStageStarted()
    {
        Initialize(manager.Generator);
    }

    // =====================================================================
    // REQUIRED METHODS (spec section 14)
    // =====================================================================

    public void Initialize(Stage4PuzzleGenerator newGenerator)
    {
        generator = newGenerator;
        if (generator == null) return;

        shown = generator.GetPatternSequence();
        correctAnswer = generator.GetPatternAnswerValue();

        solved = false;
        attempts = 0;
        message = "";
        closeAtTime = -1f;
        initialized = true;

        interactable.SetPrompt(readyPrompt);
        interactable.SetInteractable(true);
    }

    public void SubmitAnswer(int answer)
    {
        if (!initialized || solved) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.PatternPuzzle) return;

        if (answer == correctAnswer)
        {
            solved = true;
            message = "SUCCESS";
            PlaySound(successClip);

            interactable.SetPrompt(solvedPrompt);
            interactable.SetInteractable(false);
            closeAtTime = Time.unscaledTime + 1.2f;

            if (manager != null) manager.CompletePatternPuzzle();
        }
        else
        {
            attempts++;
            message = "TRY AGAIN";
            PlaySound(errorClip);
            if (manager != null) manager.ApplyTimePenalty(); // small penalty, never an instant fail
        }
    }

    public bool IsSolved()
    {
        return solved;
    }

    public void ResetPuzzle()
    {
        ClosePanel();
        solved = false;
        attempts = 0;
        message = "";
        closeAtTime = -1f;
        initialized = false;
        shown = null;

        interactable.SetPrompt(readyPrompt);
        interactable.SetInteractable(false); // locked until the stage starts again
    }

    // =====================================================================
    // PANEL
    // =====================================================================

    private void OpenPanel()
    {
        if (!initialized || solved) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.PatternPuzzle) return;

        panelOpen = true;
        message = "";
        Stage4TestPlayer.InputLocked = true; // main project: replace with the real player's "freeze input"
    }

    private void ClosePanel()
    {
        if (!panelOpen) return;

        panelOpen = false;
        Stage4TestPlayer.InputLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // =====================================================================
    // TEMPORARY ON-SCREEN UI
    // =====================================================================

    private void EnsureStyles()
    {
        if (boxStyle != null) return;

        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.14f, 0.96f));
        bg.Apply();

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bg;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(1f, 0.65f, 0.2f);

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 16;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);

        seqStyle = new GUIStyle(GUI.skin.label);
        seqStyle.fontSize = 44;
        seqStyle.fontStyle = FontStyle.Bold;
        seqStyle.alignment = TextAnchor.MiddleCenter;
        seqStyle.normal.textColor = Color.white;

        messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.fontSize = 22;
        messageStyle.fontStyle = FontStyle.Bold;
        messageStyle.alignment = TextAnchor.MiddleCenter;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 22;
        buttonStyle.fontStyle = FontStyle.Bold;
    }

    private void OnGUI()
    {
        if (!panelOpen || shown == null) return;
        EnsureStyles();

        float w = 560f;
        float h = 400f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none, boxStyle);
        GUI.Label(new Rect(r.x, r.y + 14f, w, 40f), "PATTERN ANALYSIS", titleStyle);
        GUI.Label(new Rect(r.x, r.y + 56f, w, 26f), "Find the next number in the sequence.", textStyle);
        GUI.Label(new Rect(r.x, r.y + 92f, w, 64f), string.Join("   ", shown) + "   ?", seqStyle);

        if (!string.IsNullOrEmpty(message))
        {
            messageStyle.normal.textColor = solved ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
            GUI.Label(new Rect(r.x, r.y + 160f, w, 30f), message, messageStyle);
        }

        GUI.enabled = !solved;
        for (int i = 0; i < 10; i++)
        {
            int row = i / 5;
            int col = i % 5;
            Rect b = new Rect(r.x + 40f + col * 100f, r.y + 200f + row * 62f, 90f, 52f);
            if (GUI.Button(b, i.ToString(), buttonStyle)) SubmitAnswer(i);
        }
        GUI.enabled = true;

        if (GUI.Button(new Rect(r.x + w * 0.5f - 85f, r.y + h - 54f, 170f, 38f), "Close (ESC)"))
        {
            ClosePanel();
        }
    }
}
