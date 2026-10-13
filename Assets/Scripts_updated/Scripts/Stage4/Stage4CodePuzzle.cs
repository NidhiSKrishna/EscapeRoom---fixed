using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - Puzzle 5: FINAL CODE (spec sections 23, 24, 25).
///
/// Flow: The player enters a 4-digit security code derived from earlier puzzles:
///   Digit 1: Pattern puzzle answer (0-9)
///   Digit 2: Last light color number in Memory sequence (1-4)
///   Digit 3: Control panel number matching the first light (1-4)
///   Digit 4: Number of the terminal wired LAST in the Wire puzzle (1-6)
///
/// Required methods (spec section 25):
///   - public void Initialize(Stage4PuzzleGenerator generator)
///   - public void SubmitDigit(int digit)
///   - public void Backspace()
///   - public void ClearInput()
///   - public void SubmitCode()
///   - public bool IsSolved()
///   - public void ResetPuzzle()
///
/// Correct   -> success sound, green UI, Stage4Manager.CompleteCodePuzzle()
/// Incorrect -> error sound, small time penalty, clear input, allow retry
/// </summary>
[RequireComponent(typeof(Stage4Interactable))]
public class Stage4CodePuzzle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip keyClickClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    [Header("Prompts")]
    [SerializeField] private string readyPrompt = "Press E to access Security Keypad";
    [SerializeField] private string solvedPrompt = "Security Keypad: AUTHORIZED";

    private Stage4Interactable interactable;
    private Stage4PuzzleGenerator generator;

    private string expectedCode = "";
    private StringBuilder currentInput = new StringBuilder();
    private bool initialized;
    private bool solved;
    private bool panelOpen;
    private string statusMessage = "";
    private float closeAtTime = -1f;
    private float clearAtTime = -1f;   // wrong code stays visible for a moment, then clears

    private GUIStyle boxStyle, titleStyle, codeStyle, msgStyle, keyStyle, ctrlStyle, slotLabelStyle;

    private void Awake()
    {
        interactable = GetComponent<Stage4Interactable>();
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        SetupAudio();

        interactable.SetPrompt(readyPrompt);
        interactable.SetInteractable(false);
    }

    private void OnEnable()
    {
        interactable.OnInteracted += OpenKeypad;
        if (manager != null)
        {
            manager.OnStageStarted += HandleStageStarted;
            manager.OnStageReset += ResetPuzzle;
            manager.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        interactable.OnInteracted -= OpenKeypad;
        if (manager != null)
        {
            manager.OnStageStarted -= HandleStageStarted;
            manager.OnStageReset -= ResetPuzzle;
            manager.OnStateChanged -= HandleStateChanged;
        }
        CloseKeypad();
    }

    private void Start()
    {
        if (!initialized && manager != null && manager.GetCurrentState() != Stage4State.NotStarted)
        {
            Initialize(manager.Generator);
        }
    }

    private void Update()
    {
        if (!panelOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasReleasedThisFrame)
            {
                CloseKeypad();
                return;
            }

            // Keyboard input support
            for (int d = 0; d <= 9; d++)
            {
                Key k = (Key)((int)Key.Digit0 + d);
                Key nk = (Key)((int)Key.Numpad0 + d);
                if (keyboard[k].wasPressedThisFrame || keyboard[nk].wasPressedThisFrame)
                {
                    SubmitDigit(d);
                    break;
                }
            }

            if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SubmitCode();
        }

        if (clearAtTime > 0f && Time.unscaledTime >= clearAtTime)
        {
            ResetInputAfterReject();
        }

        if (closeAtTime > 0f && Time.unscaledTime >= closeAtTime)
        {
            closeAtTime = -1f;
            CloseKeypad();
        }
    }

    private void HandleStageStarted()
    {
        Initialize(manager.Generator);
    }

    private void HandleStateChanged(Stage4State state)
    {
        bool active = state == Stage4State.CodePuzzle;
        if (interactable != null && !solved) interactable.SetInteractable(active);
    }

    // =====================================================================
    // REQUIRED METHODS (spec section 25)
    // =====================================================================

    public void Initialize(Stage4PuzzleGenerator newGenerator)
    {
        generator = newGenerator;
        if (generator == null) return;

        expectedCode = generator.GetFinalCode();
        currentInput.Clear();
        solved = false;
        statusMessage = "";
        closeAtTime = -1f;
        initialized = true;

        interactable.SetPrompt(readyPrompt);
        bool active = manager != null && manager.GetCurrentState() == Stage4State.CodePuzzle;
        interactable.SetInteractable(active);
    }

    public void SubmitDigit(int digit)
    {
        if (!initialized || solved) return;
        if (digit < 0 || digit > 9) return;

        // A rejected code is still on screen: typing a new digit starts a fresh code.
        if (clearAtTime > 0f) ResetInputAfterReject();

        if (currentInput.Length >= 4) return;

        currentInput.Append(digit);
        PlayClick();

        // The code is NOT sent automatically any more. The player sees all 4 digits,
        // then presses ENTER (key or button) to confirm. This also allows fixing a typo for free.
        if (currentInput.Length == 4)
        {
            statusMessage = "PRESS ENTER TO CONFIRM";
        }
    }

    private void ResetInputAfterReject()
    {
        currentInput.Clear();
        clearAtTime = -1f;
        statusMessage = "ENTER AUTHORIZATION CODE";
    }

    public void Backspace()
    {
        if (!initialized || solved || currentInput.Length == 0) return;
        if (clearAtTime > 0f) { ResetInputAfterReject(); return; }
        currentInput.Remove(currentInput.Length - 1, 1);
        statusMessage = "ENTER AUTHORIZATION CODE";
        PlayClick();
    }

    public void ClearInput()
    {
        if (!initialized || solved) return;
        currentInput.Clear();
        clearAtTime = -1f;
        statusMessage = "ENTER AUTHORIZATION CODE";
        PlayClick();
    }

    public void SubmitCode()
    {
        if (!initialized || solved) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.CodePuzzle) return;
        if (clearAtTime > 0f) return; // a rejected code is still showing

        string entered = currentInput.ToString();
        if (entered.Length < 4)
        {
            statusMessage = "ENTER 4 DIGITS";
            PlayClip(errorClip, 180f, 0.25f);
            return;
        }

        if (entered == expectedCode)
        {
            solved = true;
            statusMessage = "ACCESS GRANTED - CODE VERIFIED";
            PlayClip(successClip, 659f, 0.5f);

            interactable.SetPrompt(solvedPrompt);
            interactable.SetInteractable(false);
            closeAtTime = Time.unscaledTime + 1.4f;

            if (manager != null) manager.CompleteCodePuzzle();
        }
        else
        {
            statusMessage = "ACCESS DENIED - CODE REJECTED";
            PlayClip(errorClip, 130f, 0.4f);
            clearAtTime = Time.unscaledTime + 1.2f;   // keep the wrong code visible for a moment
            if (manager != null) manager.ApplyTimePenalty();
        }
    }

    public bool IsSolved()
    {
        return solved;
    }

    public void ResetPuzzle()
    {
        CloseKeypad();
        expectedCode = "";
        currentInput.Clear();
        solved = false;
        initialized = false;
        statusMessage = "";
        closeAtTime = -1f;
        clearAtTime = -1f;

        interactable.SetPrompt(readyPrompt);
        interactable.SetInteractable(false);
    }

    // =====================================================================
    // UI & KEYPAD
    // =====================================================================

    private void OpenKeypad()
    {
        if (!initialized || solved) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.CodePuzzle) return;

        panelOpen = true;
        if (clearAtTime > 0f) { currentInput.Clear(); clearAtTime = -1f; }
        statusMessage = currentInput.Length == 4 ? "PRESS ENTER TO CONFIRM" : "ENTER AUTHORIZATION CODE";
        Stage4TestPlayer.InputLocked = true;
    }

    private void CloseKeypad()
    {
        if (!panelOpen) return;
        panelOpen = false;
        Stage4TestPlayer.InputLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayClick()
    {
        if (keyClickClip != null) audioSource.PlayOneShot(keyClickClip);
        else PlayClip(null, 880f, 0.05f);
    }

    private void PlayClip(AudioClip clip, float toneFreq, float duration)
    {
        if (clip != null) audioSource.PlayOneShot(clip);
        else audioSource.PlayOneShot(MakeTone("KeypadTone_" + toneFreq, toneFreq, duration));
    }

    private static AudioClip MakeTone(string clipName, float freq, float seconds)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float env = Mathf.Sin((float)i / samples * Mathf.PI);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * env * 0.35f;
        }
        AudioClip c = AudioClip.Create(clipName, samples, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    private void EnsureStyles()
    {
        if (boxStyle != null) return;

        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.96f));
        bg.Apply();

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bg;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(1f, 0.82f, 0.25f);

        codeStyle = new GUIStyle(GUI.skin.box);
        codeStyle.fontSize = 36;
        codeStyle.fontStyle = FontStyle.Bold;
        codeStyle.alignment = TextAnchor.MiddleCenter;
        codeStyle.normal.textColor = Color.white;

        msgStyle = new GUIStyle(GUI.skin.label);
        msgStyle.fontSize = 16;
        msgStyle.fontStyle = FontStyle.Bold;
        msgStyle.alignment = TextAnchor.MiddleCenter;

        keyStyle = new GUIStyle(GUI.skin.button);
        keyStyle.fontSize = 24;
        keyStyle.fontStyle = FontStyle.Bold;

        slotLabelStyle = new GUIStyle(GUI.skin.label);
        slotLabelStyle.fontSize = 12;
        slotLabelStyle.fontStyle = FontStyle.Bold;
        slotLabelStyle.alignment = TextAnchor.MiddleCenter;
        slotLabelStyle.normal.textColor = new Color(0.65f, 0.8f, 0.95f);

        ctrlStyle = new GUIStyle(GUI.skin.button);
        ctrlStyle.fontSize = 16;
        ctrlStyle.fontStyle = FontStyle.Bold;
    }

    private static readonly string[] SlotLabels = { "1 PATTERN", "2 LAST LIGHT", "3 PANEL NO.", "4 LAST WIRE" };

    private void OnGUI()
    {
        if (!panelOpen) return;
        EnsureStyles();

        float w = 440f;
        float h = 590f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none, boxStyle);
        GUI.Label(new Rect(r.x, r.y + 14f, w, 32f), "SECURITY OVERRIDE KEYPAD", titleStyle);

        // Four code slots, each with a label saying where that digit comes from.
        float slotW = 86f;
        float slotGap = 10f;
        float slotsX = r.x + (w - (4 * slotW + 3 * slotGap)) * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            string ch = i < currentInput.Length ? currentInput[i].ToString() : "_";
            float sx = slotsX + i * (slotW + slotGap);
            GUI.Box(new Rect(sx, r.y + 54f, slotW, 62f), ch, codeStyle);
            GUI.Label(new Rect(sx - 4f, r.y + 118f, slotW + 8f, 20f), SlotLabels[i], slotLabelStyle);
        }

        // Status message
        if (!string.IsNullOrEmpty(statusMessage))
        {
            bool bad = statusMessage.StartsWith("ACCESS DENIED");
            msgStyle.normal.textColor = solved ? new Color(0.3f, 1f, 0.45f)
                : (bad ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.85f, 0.35f));
            GUI.Label(new Rect(r.x, r.y + 144f, w, 28f), statusMessage, msgStyle);
        }

        GUI.enabled = !solved;

        // Keypad buttons (1-9, Clear, 0, Backspace)
        float btnW = 90f;
        float btnH = 58f;
        float pad = 14f;
        float startX = r.x + (w - (3 * btnW + 2 * pad)) * 0.5f;
        float startY = r.y + 180f;

        for (int i = 1; i <= 9; i++)
        {
            int row = (i - 1) / 3;
            int col = (i - 1) % 3;
            Rect b = new Rect(startX + col * (btnW + pad), startY + row * (btnH + pad), btnW, btnH);
            if (GUI.Button(b, i.ToString(), keyStyle)) SubmitDigit(i);
        }

        // Bottom row: Clear, 0, Backspace
        float bY = startY + 3 * (btnH + pad);
        if (GUI.Button(new Rect(startX, bY, btnW, btnH), "CLR", ctrlStyle)) ClearInput();
        if (GUI.Button(new Rect(startX + (btnW + pad), bY, btnW, btnH), "0", keyStyle)) SubmitDigit(0);
        if (GUI.Button(new Rect(startX + 2 * (btnW + pad), bY, btnW, btnH), "DEL", ctrlStyle)) Backspace();

        // ENTER button (the code is only checked when this is pressed)
        float eY = bY + btnH + pad;
        if (GUI.Button(new Rect(startX, eY, 3 * btnW + 2 * pad, 50f), "ENTER", keyStyle)) SubmitCode();

        GUI.enabled = true;

        if (GUI.Button(new Rect(r.x + w * 0.5f - 80f, r.y + h - 40f, 160f, 30f), "Close (ESC)"))
        {
            CloseKeypad();
        }
    }
}
