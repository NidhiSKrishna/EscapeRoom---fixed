using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - Puzzle 4: WIRE HACKING (replaces the old Memorized Path puzzle).
///
/// Flow: the player presses E on the Junction Box. A panel opens showing N terminals (usually 5),
/// each marked with a voltage. The rule is printed on the panel:
///     "Reconnect the terminals from LOWEST to HIGHEST voltage."
/// The player clicks (or presses the number key of) each terminal in that order.
///
/// Correct terminal -> it turns green, the next one is expected.
/// Wrong terminal   -> short circuit: error sound, small time penalty, all wires reset.
/// Last terminal    -> success sound, Stage4Manager.CompletePathPuzzle().
///
/// FINAL CODE LINK: digit 4 of the keypad code is the NUMBER of the terminal that was connected LAST
/// (the highest voltage). The player must remember it. See Stage4PuzzleGenerator.
///
/// NOTE: the state, event and method names inside Stage4Manager still say "Path"
/// (PathPuzzle, OnPathCompleted, CompletePathPuzzle) on purpose, so the integration with the main
/// project keeps working. Only this puzzle itself changed.
///
/// Required methods: Initialize, SelectTerminal, IsComplete, ResetWires.
///
/// EASIEST SETUP: put this script on an EMPTY object. With "Auto Build Box" ticked it creates a
/// junction box with one indicator light per terminal. Tones are generated in code.
/// </summary>
public class Stage4WirePuzzle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Junction box (leave empty + keep Auto Build ticked for a ready-made test box)")]
    [SerializeField] private bool autoBuildBox = true;
    [Tooltip("Press E on this to open the wiring panel.")]
    [SerializeField] private Stage4Interactable boxInteractable;
    [Tooltip("Optional: one renderer per terminal (indicator lights). Filled automatically by Auto Build.")]
    [SerializeField] private Renderer[] terminalIndicators;

    [Header("Audio (optional - synthetic tones generated if empty)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip connectClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    [Header("Text")]
    [SerializeField] private string readyPrompt = "Press E to open the Wiring Panel";
    [SerializeField] private string solvedPrompt = "Wiring Panel: POWER RESTORED";

    private static readonly Color IdleColor = new Color(0.45f, 0.5f, 0.6f);
    private static readonly Color ConnectedColor = new Color(0.25f, 0.95f, 0.45f);
    private static readonly Color ErrorColor = new Color(1f, 0.18f, 0.18f);

    // Wire colours are NOT red/blue/green/yellow, so they are never confused with the Memory puzzle colours.
    private static readonly Color[] WireColors =
    {
        new Color(1.0f, 0.55f, 0.1f),   // orange
        new Color(0.2f, 0.85f, 0.95f),  // cyan
        new Color(0.9f, 0.3f, 0.85f),   // magenta
        new Color(0.95f, 0.95f, 0.95f), // white
        new Color(0.65f, 0.9f, 0.2f),   // lime
        new Color(0.6f, 0.45f, 1.0f)    // violet
    };

    private Stage4PuzzleGenerator generator;
    private MaterialPropertyBlock block;
    private Coroutine flashRoutine;
    private Action boxHandler;

    private int wireCount = 5;
    private int[] voltages;
    private int[] order;
    private int connectedCount;
    private bool initialized;
    private bool completed;
    private bool panelOpen;
    private float closeAtTime = -1f;

    private string message = "";
    private float messageUntil;
    private GUIStyle boxStyle, titleStyle, textStyle, messageStyle, terminalStyle, smallStyle;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        block = new MaterialPropertyBlock();

        if (NeedsBox())
        {
            if (autoBuildBox) BuildTestBox();
            else Debug.LogError("[Stage4Wire] Assign a Box Interactable or enable Auto Build Box.");
        }

        SetupAudio();
        if (boxInteractable != null)
        {
            boxInteractable.SetPrompt(readyPrompt);
            boxInteractable.SetInteractable(false);
        }
        DimAll();
    }

    private void OnEnable()
    {
        if (boxInteractable != null)
        {
            boxHandler = OpenPanel;
            boxInteractable.OnInteracted += boxHandler;
        }

        if (manager != null)
        {
            manager.OnStageStarted += HandleStageStarted;
            manager.OnStageReset += ResetWires;
            manager.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (boxInteractable != null && boxHandler != null)
            boxInteractable.OnInteracted -= boxHandler;

        if (manager != null)
        {
            manager.OnStageStarted -= HandleStageStarted;
            manager.OnStageReset -= ResetWires;
            manager.OnStateChanged -= HandleStateChanged;
        }
        ClosePanel();
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
            // Close on the RELEASE of Esc so the player script does not also react to the same key press.
            if (keyboard.escapeKey.wasReleasedThisFrame)
            {
                ClosePanel();
                return;
            }

            // Number keys 1-6 connect the matching terminal.
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < wireCount && i < keys.Length; i++)
            {
                if (keyboard[keys[i]].wasPressedThisFrame) SelectTerminal(i);
            }
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

    private void HandleStateChanged(Stage4State state)
    {
        bool active = state == Stage4State.PathPuzzle && initialized && !completed;
        if (boxInteractable != null) boxInteractable.SetInteractable(active);
    }

    // =====================================================================
    // REQUIRED METHODS
    // =====================================================================

    public void Initialize(Stage4PuzzleGenerator newGenerator)
    {
        generator = newGenerator;
        if (generator == null) return;

        StopFlash();
        wireCount = generator.GetWireCount();
        voltages = generator.GetWireVoltages();
        order = generator.GetWireOrder();
        connectedCount = 0;
        completed = false;
        message = "";
        closeAtTime = -1f;
        initialized = true;

        DimAll();

        bool active = manager != null && manager.GetCurrentState() == Stage4State.PathPuzzle;
        if (boxInteractable != null)
        {
            boxInteractable.SetPrompt(readyPrompt);
            boxInteractable.SetInteractable(active);
        }
    }

    /// <summary>Connect a terminal. index is 0-based (0 = Terminal 1).</summary>
    public void SelectTerminal(int index)
    {
        if (!initialized || completed) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.PathPuzzle) return;
        if (order == null || index < 0 || index >= wireCount) return;
        if (IsConnected(index)) return; // already wired, ignore

        if (index == order[connectedCount])
        {
            connectedCount++;
            SetIndicator(index, ConnectedColor);
            PlayConnectTone(connectedCount);

            if (connectedCount >= wireCount)
            {
                completed = true;
                ShowMessage("POWER RESTORED - ACCESS GRANTED", 3f);
                PlaySuccessTone();
                closeAtTime = Time.unscaledTime + 1.4f;

                if (boxInteractable != null)
                {
                    boxInteractable.SetPrompt(solvedPrompt);
                    boxInteractable.SetInteractable(false);
                }

                if (manager != null) manager.CompletePathPuzzle();
            }
            else
            {
                ShowMessage("Connected " + connectedCount + " / " + wireCount, 2f);
            }
        }
        else
        {
            ShowMessage("SHORT CIRCUIT - REWIRE FROM THE START", 2.5f);
            PlayErrorTone();
            if (manager != null) manager.ApplyTimePenalty();

            connectedCount = 0;
            StartFlash(index);
        }
    }

    public bool IsComplete()
    {
        return completed;
    }

    public void ResetWires()
    {
        ClosePanel();
        StopFlash();
        voltages = null;
        order = null;
        connectedCount = 0;
        completed = false;
        initialized = false;
        message = "";
        closeAtTime = -1f;

        DimAll();
        if (boxInteractable != null)
        {
            boxInteractable.SetPrompt(readyPrompt);
            boxInteractable.SetInteractable(false);
        }
    }

    private bool IsConnected(int terminal)
    {
        for (int i = 0; i < connectedCount; i++)
            if (order[i] == terminal) return true;
        return false;
    }

    // =====================================================================
    // PANEL OPEN / CLOSE
    // =====================================================================

    private void OpenPanel()
    {
        if (!initialized || completed) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.PathPuzzle) return;

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

    // =====================================================================
    // INDICATOR LIGHTS (3D box)
    // =====================================================================

    private void DimAll()
    {
        if (terminalIndicators == null) return;
        for (int i = 0; i < terminalIndicators.Length; i++) SetIndicator(i, IdleColor);
    }

    private void SetIndicator(int index, Color color)
    {
        if (terminalIndicators == null || index < 0 || index >= terminalIndicators.Length) return;
        Renderer r = terminalIndicators[index];
        if (r == null) return;

        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color * 0.7f);
        r.SetPropertyBlock(block);
    }

    private void StartFlash(int wrongIndex)
    {
        StopFlash();
        flashRoutine = StartCoroutine(ErrorFlashRoutine(wrongIndex));
    }

    private void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    private IEnumerator ErrorFlashRoutine(int wrongIndex)
    {
        DimAll();
        SetIndicator(wrongIndex, ErrorColor);
        yield return new WaitForSeconds(0.6f);
        DimAll();
        flashRoutine = null;
    }

    private bool NeedsBox()
    {
        return boxInteractable == null;
    }

    private void BuildTestBox()
    {
        int count = 6; // build 6 lights; the puzzle only uses the first wireCount ones
        terminalIndicators = new Renderer[count];

        // Standing junction box, facing the player spawn (front = -Z).
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "WireJunctionBox";
        box.transform.SetParent(transform, false);
        box.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        box.transform.localScale = new Vector3(1.8f, 1.4f, 0.6f);
        boxInteractable = box.AddComponent<Stage4Interactable>();

        for (int i = 0; i < count; i++)
        {
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            light.name = "TerminalLight_" + (i + 1);
            light.transform.SetParent(transform, false);
            float x = (i - (count - 1) * 0.5f) * 0.26f;
            light.transform.localPosition = new Vector3(x, 1.05f, -0.33f);
            light.transform.localScale = new Vector3(0.16f, 0.16f, 0.06f);

            // Indicators are decoration only: no collider, so the player's E-ray hits the box behind them.
            Collider c = light.GetComponent<Collider>();
            if (c != null) Destroy(c);

            terminalIndicators[i] = light.GetComponent<Renderer>();
        }
    }

    // =====================================================================
    // AUDIO
    // =====================================================================

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayConnectTone(int step)
    {
        if (connectClip != null) { audioSource.PlayOneShot(connectClip); return; }
        audioSource.PlayOneShot(MakeTone("WireTone_" + step, 330f + step * 60f, 0.18f));
    }

    private void PlaySuccessTone()
    {
        if (successClip != null) { audioSource.PlayOneShot(successClip); return; }
        audioSource.PlayOneShot(MakeTone("WireSuccess", 659f, 0.5f));
    }

    private void PlayErrorTone()
    {
        if (errorClip != null) { audioSource.PlayOneShot(errorClip); return; }
        audioSource.PlayOneShot(MakeTone("WireError", 140f, 0.35f));
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

    // =====================================================================
    // TEMPORARY ON-SCREEN UI
    // =====================================================================

    private void ShowMessage(string text, float duration)
    {
        message = text;
        messageUntil = Time.unscaledTime + duration;
    }

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
        textStyle.fontSize = 17;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);

        messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.fontSize = 22;
        messageStyle.fontStyle = FontStyle.Bold;
        messageStyle.alignment = TextAnchor.MiddleCenter;

        terminalStyle = new GUIStyle(GUI.skin.button);
        terminalStyle.fontSize = 30;
        terminalStyle.fontStyle = FontStyle.Bold;
        terminalStyle.alignment = TextAnchor.MiddleCenter;

        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 15;
        smallStyle.fontStyle = FontStyle.Bold;
        smallStyle.alignment = TextAnchor.MiddleCenter;
        smallStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (!panelOpen || voltages == null) return;
        EnsureStyles();

        float w = Mathf.Max(640f, wireCount * 130f + 60f);
        float h = 470f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none, boxStyle);
        GUI.Label(new Rect(r.x, r.y + 14f, w, 40f), "WIRE HACK - POWER RESTORATION", titleStyle);
        GUI.Label(new Rect(r.x, r.y + 58f, w, 26f),
            "Reconnect the terminals from LOWEST to HIGHEST voltage.", textStyle);
        GUI.Label(new Rect(r.x, r.y + 84f, w, 24f),
            "Click a terminal (or press its number key). A wrong terminal shorts the circuit.", textStyle);

        // Terminals
        float bw = 110f;
        float gap = 20f;
        float totalW = wireCount * bw + (wireCount - 1) * gap;
        float startX = r.x + (w - totalW) * 0.5f;
        float topY = r.y + 130f;
        float btnH = 120f;
        float railY = r.y + 330f;

        Color oldColor = GUI.color;
        Color oldBg = GUI.backgroundColor;

        // power rail
        GUI.color = new Color(0.35f, 0.4f, 0.5f);
        GUI.DrawTexture(new Rect(startX - 10f, railY, totalW + 20f, 10f), Texture2D.whiteTexture);

        for (int i = 0; i < wireCount; i++)
        {
            float x = startX + i * (bw + gap);
            bool connected = IsConnected(i);
            Color wire = WireColors[i % WireColors.Length];

            // wire from the terminal down to the rail (green once connected)
            GUI.color = connected ? ConnectedColor : new Color(wire.r, wire.g, wire.b, 0.55f);
            GUI.DrawTexture(new Rect(x + bw * 0.5f - 4f, topY + btnH, 8f, railY - (topY + btnH)), Texture2D.whiteTexture);

            // coloured strip on top of the terminal
            GUI.color = wire;
            GUI.DrawTexture(new Rect(x, topY - 10f, bw, 8f), Texture2D.whiteTexture);

            GUI.color = oldColor;
            GUI.backgroundColor = connected ? new Color(0.3f, 1f, 0.5f) : Color.white;

            GUI.enabled = !completed && !connected;
            if (GUI.Button(new Rect(x, topY, bw, btnH), voltages[i] + " V", terminalStyle))
                SelectTerminal(i);
            GUI.enabled = true;

            GUI.Label(new Rect(x, topY + btnH + 4f, bw, 22f), "TERMINAL " + (i + 1), smallStyle);
        }

        GUI.color = oldColor;
        GUI.backgroundColor = oldBg;

        // status
        string progress = "Connected: " + connectedCount + " / " + wireCount;
        GUI.Label(new Rect(r.x, r.y + 362f, w, 26f), progress, textStyle);

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
        {
            messageStyle.normal.textColor = completed ? new Color(0.3f, 1f, 0.5f)
                : (message.StartsWith("SHORT") ? new Color(1f, 0.35f, 0.35f) : new Color(0.7f, 0.9f, 1f));
            GUI.Label(new Rect(r.x, r.y + 390f, w, 30f), message, messageStyle);
        }

        if (GUI.Button(new Rect(r.x + w * 0.5f - 85f, r.y + h - 44f, 170f, 34f), "Close (ESC)"))
        {
            ClosePanel();
        }
    }
}
