using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 - Puzzle 2: MEMORY SEQUENCE (spec sections 15 to 17).
///
/// Flow: the player presses E on the panel -> the four coloured buttons light up one by one
/// (with a unique tone each) -> the lights go dark -> the player repeats the sequence by pressing
/// E on the coloured buttons.
///
/// Colour ids (same as the generator):  RED = 0, BLUE = 1, GREEN = 2, YELLOW = 3.
///
/// Correct   -> success sound, SUCCESS message, Stage4Manager.CompleteMemoryPuzzle()
/// Incorrect -> error sound, the pressed button flashes red, small time penalty, input resets
///              (the stage never fails; the player may replay the sequence for free).
///
/// EASIEST SETUP: put this script on an EMPTY object. With "Auto Build Panel" ticked it creates a
/// simple test panel (a backing plate and four coloured buttons) by itself. Later, level designers
/// can assign their own objects to "Start Interactable" and "Color Buttons" and untick Auto Build.
/// Tones are generated in code, so no audio files are needed (assign clips to replace them).
/// </summary>
public class Stage4MemoryPuzzle : MonoBehaviour
{
    private enum Phase { Idle, Showing, WaitingInput, Solved }

    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Panel (leave empty + keep Auto Build ticked for a ready-made test panel)")]
    [SerializeField] private bool autoBuildPanel = true;
    [Tooltip("Press E on this to (re)play the sequence.")]
    [SerializeField] private Stage4Interactable startInteractable;
    [Tooltip("Exactly 4, in the order RED, BLUE, GREEN, YELLOW.")]
    [SerializeField] private Stage4Interactable[] colorButtons = new Stage4Interactable[4];

    [Header("Timing")]
    [SerializeField] private float flashSeconds = 0.6f;
    [SerializeField] private float gapSeconds = 0.25f;

    [Header("Audio (optional - tones are generated if left empty)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] colorClips;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    private static readonly Color[] ButtonColors =
    {
        new Color(0.95f, 0.15f, 0.15f), // RED
        new Color(0.15f, 0.45f, 1.00f), // BLUE
        new Color(0.15f, 0.85f, 0.30f), // GREEN
        new Color(1.00f, 0.85f, 0.15f)  // YELLOW
    };
    private static readonly float[] ToneFrequencies = { 262f, 330f, 392f, 523f };
    private const float DimFactor = 0.25f;

    private Renderer[] buttonRenderers;
    private Action[] buttonHandlers;
    private AudioClip[] generatedClips;
    private AudioClip generatedSuccess;
    private AudioClip generatedError;
    private Coroutine[] flashRoutines;
    private MaterialPropertyBlock block;

    private int[] sequence;
    private int inputIndex;
    private bool initialized;
    private bool solved;
    private Phase phase = Phase.Idle;

    private string message = "";
    private float messageUntil;
    private GUIStyle messageStyle;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        block = new MaterialPropertyBlock();
        flashRoutines = new Coroutine[4];

        if (NeedsPanel())
        {
            if (autoBuildPanel) BuildTestPanel();
            else Debug.LogError("[Stage4Memory] Assign Start Interactable and 4 Color Buttons, or tick Auto Build Panel.");
        }

        CacheRenderers();
        SetupAudio();
        SetupPrompts();
        DimAll();
        SetAllInteractable(false);
    }

    private void OnEnable()
    {
        if (startInteractable != null) startInteractable.OnInteracted += PlaySequence;

        if (colorButtons != null)
        {
            buttonHandlers = new Action[colorButtons.Length];
            for (int i = 0; i < colorButtons.Length; i++)
            {
                if (colorButtons[i] == null) continue;
                int index = i; // each button remembers its own colour id
                buttonHandlers[i] = () => SubmitColor(index);
                colorButtons[i].OnInteracted += buttonHandlers[i];
            }
        }

        if (manager != null)
        {
            manager.OnStageStarted += HandleStageStarted;
            manager.OnStageReset += ResetPuzzle;
            manager.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (startInteractable != null) startInteractable.OnInteracted -= PlaySequence;

        if (colorButtons != null && buttonHandlers != null)
        {
            for (int i = 0; i < colorButtons.Length && i < buttonHandlers.Length; i++)
            {
                if (colorButtons[i] != null && buttonHandlers[i] != null) colorButtons[i].OnInteracted -= buttonHandlers[i];
            }
        }

        if (manager != null)
        {
            manager.OnStageStarted -= HandleStageStarted;
            manager.OnStageReset -= ResetPuzzle;
            manager.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Start()
    {
        // Safety: catch up if the stage started before this script was listening.
        if (!initialized && manager != null && manager.GetCurrentState() != Stage4State.NotStarted)
        {
            Initialize(manager.Generator);
        }
    }

    private void HandleStageStarted()
    {
        Initialize(manager.Generator);
    }

    private void HandleStateChanged(Stage4State state)
    {
        UpdateInteractables();
    }

    // =====================================================================
    // REQUIRED METHODS (spec section 17)
    // =====================================================================

    public void Initialize(Stage4PuzzleGenerator generator)
    {
        if (generator == null) return;

        StopAllCoroutines();
        sequence = generator.GetMemorySequence();
        inputIndex = 0;
        solved = false;
        phase = Phase.Idle;
        message = "";
        initialized = true;

        DimAll();
        UpdateInteractables();
    }

    /// <summary>Shows the colour sequence (lights + tones), then lets the player repeat it.</summary>
    public void PlaySequence()
    {
        if (!initialized || solved || phase == Phase.Showing) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.MemoryPuzzle) return;

        StopAllCoroutines();
        for (int i = 0; i < flashRoutines.Length; i++) flashRoutines[i] = null;
        DimAll();
        StartCoroutine(PlaySequenceRoutine());
    }

    public void SubmitColor(int colorIndex)
    {
        if (!initialized || solved) return;
        if (colorIndex < 0 || colorIndex >= 4) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.MemoryPuzzle) return;
        if (phase == Phase.Showing) return; // ignore presses while the sequence plays

        if (phase == Phase.Idle)
        {
            // Player pressed a button while idle -> immediately start the sequence!
            PlaySequence();
            return;
        }

        if (colorIndex == sequence[inputIndex])
        {
            PlayColorTone(colorIndex);
            Flash(colorIndex, 0.25f, ButtonColors[colorIndex]);
            inputIndex++;

            if (inputIndex >= sequence.Length)
            {
                solved = true;
                phase = Phase.Solved;
                PlayClip(successClip != null ? successClip : generatedSuccess);
                ShowMessage("SUCCESS - MEMORY SEQUENCE VERIFIED", 2.5f);
                SetPromptsSolved();
                StartCoroutine(SuccessRoutine());

                if (manager != null) manager.CompleteMemoryPuzzle();
            }
            else
            {
                ShowMessage("Progress: " + inputIndex + " / " + sequence.Length, 1.5f);
            }
        }
        else
        {
            PlayClip(errorClip != null ? errorClip : generatedError);
            Flash(colorIndex, 0.45f, new Color(1f, 0f, 0f));
            ShowMessage("WRONG - Press E to replay sequence", 2.5f);
            ResetInput();
            phase = Phase.Idle;
            SetPromptsIdle();
            if (manager != null) manager.ApplyTimePenalty();
        }
    }

    /// <summary>Clears what the player has entered so far. The sequence stays the same.</summary>
    public void ResetInput()
    {
        inputIndex = 0;
    }

    public bool IsSolved()
    {
        return solved;
    }

    public void ResetPuzzle()
    {
        StopAllCoroutines();
        for (int i = 0; i < flashRoutines.Length; i++) flashRoutines[i] = null;

        sequence = null;
        inputIndex = 0;
        solved = false;
        initialized = false;
        phase = Phase.Idle;
        message = "";

        DimAll();
        SetAllInteractable(false);
    }

    // =====================================================================
    // ROUTINES
    // =====================================================================

    private IEnumerator PlaySequenceRoutine()
    {
        phase = Phase.Showing;
        inputIndex = 0;
        SetPromptsShowing();
        ShowMessage("WATCH THE SEQUENCE", 30f);

        yield return new WaitForSeconds(0.7f);

        for (int i = 0; i < sequence.Length; i++)
        {
            int c = sequence[i];
            SetColor(c, ButtonColors[c]);
            PlayColorTone(c);
            yield return new WaitForSeconds(flashSeconds);

            SetColor(c, ButtonColors[c] * DimFactor);
            yield return new WaitForSeconds(gapSeconds);
        }

        phase = Phase.WaitingInput;
        inputIndex = 0;
        SetPromptsWaiting();
        ShowMessage("YOUR TURN: repeat the sequence", 3f);
    }

    private IEnumerator SuccessRoutine()
    {
        for (int i = 0; i < 4; i++) SetColor(i, ButtonColors[i]);
        yield return new WaitForSeconds(1f);
        DimAll();
    }

    private IEnumerator FlashRoutine(int index, float seconds, Color color)
    {
        SetColor(index, color);
        yield return new WaitForSeconds(seconds);
        SetColor(index, ButtonColors[index] * DimFactor);
        flashRoutines[index] = null;
    }

    private void Flash(int index, float seconds, Color color)
    {
        if (flashRoutines[index] != null) StopCoroutine(flashRoutines[index]);
        flashRoutines[index] = StartCoroutine(FlashRoutine(index, seconds, color));
    }

    // =====================================================================
    // INTERACTABLES
    // =====================================================================

    private void UpdateInteractables()
    {
        bool active = initialized && !solved && manager != null &&
                      manager.GetCurrentState() == Stage4State.MemoryPuzzle;
        SetAllInteractable(active);
        if (active && phase == Phase.Idle) SetPromptsIdle();
    }

    private void SetAllInteractable(bool value)
    {
        if (startInteractable != null) startInteractable.SetInteractable(value);
        if (colorButtons == null) return;
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] != null) colorButtons[i].SetInteractable(value);
        }
    }

    private void SetupPrompts()
    {
        SetPromptsIdle();
    }

    private void SetPromptsIdle()
    {
        if (startInteractable != null) startInteractable.SetPrompt("Press E to play Memory Sequence");
        if (colorButtons == null) return;
        for (int i = 0; i < colorButtons.Length && i < 4; i++)
        {
            if (colorButtons[i] != null)
                colorButtons[i].SetPrompt("Press E to play Memory Sequence");
        }
    }

    private void SetPromptsShowing()
    {
        if (startInteractable != null) startInteractable.SetPrompt("Watching sequence...");
        if (colorButtons == null) return;
        for (int i = 0; i < colorButtons.Length && i < 4; i++)
        {
            if (colorButtons[i] != null)
                colorButtons[i].SetPrompt("Watching sequence...");
        }
    }

    private void SetPromptsWaiting()
    {
        if (startInteractable != null) startInteractable.SetPrompt("Press E to replay Memory Sequence");
        if (colorButtons == null) return;
        for (int i = 0; i < colorButtons.Length && i < 4; i++)
        {
            if (colorButtons[i] != null)
                colorButtons[i].SetPrompt("Press E: " + Stage4PuzzleGenerator.ColorNames[i]);
        }
    }

    private void SetPromptsSolved()
    {
        if (startInteractable != null)
        {
            startInteractable.SetPrompt("Memory Terminal: VERIFIED");
            startInteractable.SetInteractable(false);
        }
        if (colorButtons == null) return;
        for (int i = 0; i < colorButtons.Length && i < 4; i++)
        {
            if (colorButtons[i] != null)
            {
                colorButtons[i].SetPrompt("Memory Terminal: VERIFIED");
                colorButtons[i].SetInteractable(false);
            }
        }
    }

    // =====================================================================
    // LIGHTS (button colours)
    // =====================================================================

    private void CacheRenderers()
    {
        buttonRenderers = new Renderer[4];
        for (int i = 0; i < 4 && colorButtons != null && i < colorButtons.Length; i++)
        {
            if (colorButtons[i] != null) buttonRenderers[i] = colorButtons[i].GetComponentInChildren<Renderer>();
        }
    }

    private void DimAll()
    {
        for (int i = 0; i < 4; i++) SetColor(i, ButtonColors[i] * DimFactor);
    }

    private void SetColor(int index, Color color)
    {
        if (buttonRenderers == null || index < 0 || index >= buttonRenderers.Length) return;
        Renderer r = buttonRenderers[index];
        if (r == null) return;
        color.a = 1f;
        ApplyColor(r, color);
    }

    // Works with both the Universal Render Pipeline (_BaseColor) and the Built-in pipeline (_Color).
    private void ApplyColor(Renderer r, Color color)
    {
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        r.SetPropertyBlock(block);
    }

    // =====================================================================
    // TEST PANEL (built at runtime when nothing is assigned)
    // =====================================================================

    private bool NeedsPanel()
    {
        if (startInteractable == null) return true;
        if (colorButtons == null || colorButtons.Length != 4) return true;
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] == null) return true;
        }
        return false;
    }

    private void BuildTestPanel()
    {
        GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = "MemoryBacking";
        backing.transform.SetParent(transform, false);
        backing.transform.localPosition = Vector3.zero;
        backing.transform.localScale = new Vector3(1.7f, 1.7f, 0.1f);
        ApplyColor(backing.GetComponent<Renderer>(), new Color(0.08f, 0.12f, 0.20f));
        startInteractable = backing.AddComponent<Stage4Interactable>();

        // Layout seen from the player (the player looks along +Z): RED, BLUE on top; GREEN, YELLOW below.
        float[] xs = { -0.4f, 0.4f, -0.4f, 0.4f };
        float[] ys = { 0.4f, 0.4f, -0.4f, -0.4f };

        colorButtons = new Stage4Interactable[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Button_" + Stage4PuzzleGenerator.ColorNames[i];
            button.transform.SetParent(transform, false);
            button.transform.localPosition = new Vector3(xs[i], ys[i], -0.1f);
            button.transform.localScale = new Vector3(0.7f, 0.7f, 0.1f);
            colorButtons[i] = button.AddComponent<Stage4Interactable>();
        }
    }

    // =====================================================================
    // AUDIO (tones are generated, no files needed)
    // =====================================================================

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // plain 2D sound

        generatedClips = new AudioClip[4];
        for (int i = 0; i < 4; i++)
        {
            generatedClips[i] = MakeTone("Stage4Memory_" + Stage4PuzzleGenerator.ColorNames[i],
                ToneFrequencies[i], ToneFrequencies[i], 0.45f, false);
        }
        generatedSuccess = MakeTone("Stage4Memory_Success", 523f, 784f, 0.5f, false);
        generatedError = MakeTone("Stage4Memory_Error", 140f, 140f, 0.35f, true);
    }

    private void PlayColorTone(int index)
    {
        AudioClip clip = (colorClips != null && index < colorClips.Length && colorClips[index] != null)
            ? colorClips[index]
            : generatedClips[index];
        PlayClip(clip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private static AudioClip MakeTone(string clipName, float freqStart, float freqEnd, float seconds, bool square)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(seconds * sampleRate);
        float[] data = new float[samples];
        float phaseValue = 0f;

        for (int i = 0; i < samples; i++)
        {
            float freq = i < samples / 2 ? freqStart : freqEnd;
            phaseValue += 2f * Mathf.PI * freq / sampleRate;

            float attack = Mathf.Min(1f, i / (0.01f * sampleRate));
            float release = Mathf.Min(1f, (samples - i) / (0.08f * sampleRate));
            float wave = Mathf.Sin(phaseValue);
            if (square) wave = Mathf.Sign(wave) * 0.6f;

            data[i] = wave * attack * release * 0.35f;
        }

        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // =====================================================================
    // MESSAGE (temporary on-screen text)
    // =====================================================================

    private void ShowMessage(string text, float seconds)
    {
        message = text;
        messageUntil = Time.unscaledTime + seconds;
    }

    private void OnGUI()
    {
        if (manager == null || manager.GetCurrentState() != Stage4State.MemoryPuzzle) return;

        string text = "";
        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) text = message;
        else if (phase == Phase.WaitingInput && sequence != null) text = "Memory: " + inputIndex + " / " + sequence.Length;
        if (string.IsNullOrEmpty(text)) return;

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.box);
            messageStyle.fontSize = 20;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(Screen.width * 0.5f - 220f, 50f, 440f, 40f), text, messageStyle);
    }
}
