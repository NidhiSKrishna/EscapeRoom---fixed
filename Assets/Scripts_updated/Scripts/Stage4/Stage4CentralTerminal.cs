using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - CENTRAL CONTROL TERMINAL & FINAL OVERRIDE (spec sections 26 and 27).
///
/// Displays:
///   FACILITY CONTROL SYSTEM
///   ACCESS LEVEL: FINAL
///   OVERRIDE STATUS: LOCKED (before puzzles) -> READY / AUTHORIZED (after Code puzzle)
///
/// When activated in FinalOverride state:
///   Executes 5-step override sequence:
///     STEP 1: Confirm override
///     STEP 2: Activate auxiliary power
///     STEP 3: Disable security matrix
///     STEP 4: Confirm emergency evacuation
///     STEP 5: Execute final override
///   Then displays:
///     "EXECUTING FINAL OVERRIDE... \n WARNING: SECURITY SYSTEM ACTIVATED"
///   And calls Stage4Manager.BeginSecurityLockdown().
/// </summary>
[RequireComponent(typeof(Stage4Interactable))]
public class Stage4CentralTerminal : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stepClip;
    [SerializeField] private AudioClip overrideAlertClip;

    [Header("Prompts")]
    [SerializeField] private string lockedPrompt = "Central Terminal: OVERRIDE STATUS LOCKED";
    [SerializeField] private string readyPrompt = "Press E to Initiate Final Override Sequence";
    [SerializeField] private string activePrompt = "Central Terminal: OVERRIDE IN PROGRESS";

    private Stage4Interactable interactable;
    private int currentOverrideStep = 0;
    private bool terminalOpen = false;
    private bool executingOverride = false;

    private static readonly string[] StepTitles =
    {
        "STEP 1: CONFIRM FACILITY OVERRIDE",
        "STEP 2: ACTIVATE AUXILIARY POWER",
        "STEP 3: DISABLE MAIN SECURITY MATRIX",
        "STEP 4: CONFIRM EVACUATION PROTOCOL",
        "STEP 5: EXECUTE FINAL SYSTEM OVERRIDE"
    };

    private GUIStyle boxStyle, titleStyle, headerStyle, statusStyle, btnStyle, stepStyle;

    private void Awake()
    {
        interactable = GetComponent<Stage4Interactable>();
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        SetupAudio();

        interactable.SetPrompt(lockedPrompt);
        interactable.SetInteractable(true);
    }

    private void OnEnable()
    {
        interactable.OnInteracted += HandleTerminalInteract;
        if (manager != null)
        {
            manager.OnStateChanged += HandleStateChanged;
            manager.OnStageReset += ResetTerminal;
        }
    }

    private void OnDisable()
    {
        interactable.OnInteracted -= HandleTerminalInteract;
        if (manager != null)
        {
            manager.OnStateChanged -= HandleStateChanged;
            manager.OnStageReset -= ResetTerminal;
        }
        CloseTerminal();
    }

    private void Update()
    {
        if (!terminalOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasReleasedThisFrame)
        {
            CloseTerminal();
        }
    }

    private void HandleStateChanged(Stage4State state)
    {
        if (state == Stage4State.FinalOverride)
        {
            interactable.SetPrompt(readyPrompt);
            interactable.SetInteractable(true);
        }
        else if (state == Stage4State.Lockdown || state == Stage4State.Escaped)
        {
            interactable.SetPrompt("Central Terminal: OFFLINE");
            interactable.SetInteractable(false);
            CloseTerminal();
        }
        else
        {
            interactable.SetPrompt(lockedPrompt);
            interactable.SetInteractable(true);
        }
    }

    private void HandleTerminalInteract()
    {
        OpenTerminal();
    }

    private void OpenTerminal()
    {
        terminalOpen = true;
        Stage4TestPlayer.InputLocked = true;
    }

    private void CloseTerminal()
    {
        if (!terminalOpen) return;
        terminalOpen = false;
        Stage4TestPlayer.InputLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void AdvanceOverrideStep()
    {
        if (executingOverride) return;

        currentOverrideStep++;
        PlayStepSound(currentOverrideStep);

        if (currentOverrideStep >= StepTitles.Length)
        {
            StartCoroutine(ExecuteFinalOverrideRoutine());
        }
    }

    private IEnumerator ExecuteFinalOverrideRoutine()
    {
        executingOverride = true;
        PlayAlertSound();

        yield return new WaitForSeconds(1.6f);

        CloseTerminal();
        executingOverride = false;
        currentOverrideStep = 0;

        if (manager != null)
        {
            manager.BeginSecurityLockdown();
        }
    }

    public void ResetTerminal()
    {
        currentOverrideStep = 0;
        executingOverride = false;
        CloseTerminal();
        interactable.SetPrompt(lockedPrompt);
        interactable.SetInteractable(true);
    }

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayStepSound(int step)
    {
        if (stepClip != null) audioSource.PlayOneShot(stepClip);
        else audioSource.PlayOneShot(MakeTone("TerminalStep", 440f + step * 80f, 0.2f));
    }

    private void PlayAlertSound()
    {
        if (overrideAlertClip != null) audioSource.PlayOneShot(overrideAlertClip);
        else audioSource.PlayOneShot(MakeTone("OverrideAlert", 750f, 0.6f));
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
        bg.SetPixel(0, 0, new Color(0.03f, 0.06f, 0.1f, 0.96f));
        bg.Apply();

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bg;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.3f, 0.75f, 1f);

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.normal.textColor = Color.white;

        statusStyle = new GUIStyle(GUI.skin.label);
        statusStyle.fontSize = 18;
        statusStyle.fontStyle = FontStyle.Bold;
        statusStyle.alignment = TextAnchor.MiddleCenter;

        stepStyle = new GUIStyle(GUI.skin.box);
        stepStyle.fontSize = 16;
        stepStyle.fontStyle = FontStyle.Bold;
        stepStyle.alignment = TextAnchor.MiddleLeft;
        stepStyle.padding = new RectOffset(15, 10, 10, 10);

        btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 18;
        btnStyle.fontStyle = FontStyle.Bold;
    }

    private void OnGUI()
    {
        if (!terminalOpen) return;
        EnsureStyles();

        float w = 540f;
        float h = 480f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none, boxStyle);
        GUI.Label(new Rect(r.x, r.y + 15f, w, 32f), "FACILITY CONTROL SYSTEM", titleStyle);
        GUI.Label(new Rect(r.x, r.y + 48f, w, 24f), "ACCESS LEVEL: FINAL", headerStyle);

        Stage4State st = manager != null ? manager.GetCurrentState() : Stage4State.NotStarted;
        bool isReady = st == Stage4State.FinalOverride;

        if (!isReady && !executingOverride)
        {
            statusStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(r.x, r.y + 80f, w, 30f), "OVERRIDE STATUS: LOCKED", statusStyle);

            GUI.Label(new Rect(r.x + 30f, r.y + 150f, w - 60f, 80f),
                "SYSTEM LOCK ACTIVE.\nSolve all security puzzle terminals to authorize final facility override.",
                headerStyle);
        }
        else if (executingOverride)
        {
            statusStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
            GUI.Label(new Rect(r.x, r.y + 140f, w, 35f), "EXECUTING FINAL OVERRIDE...", statusStyle);
            GUI.Label(new Rect(r.x, r.y + 190f, w, 35f), "WARNING: SECURITY LOCKDOWN IMMINENT", statusStyle);
        }
        else
        {
            statusStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);
            GUI.Label(new Rect(r.x, r.y + 75f, w, 30f), "OVERRIDE STATUS: AUTHORIZED & READY", statusStyle);

            // Show 5 steps
            for (int i = 0; i < StepTitles.Length; i++)
            {
                Rect stepR = new Rect(r.x + 40f, r.y + 115f + i * 44f, w - 80f, 38f);
                string mark = i < currentOverrideStep ? "[OK]  " : "[...]  ";
                Color c = i < currentOverrideStep ? new Color(0.3f, 1f, 0.4f) :
                          (i == currentOverrideStep ? new Color(1f, 0.85f, 0.2f) : Color.gray);
                stepStyle.normal.textColor = c;
                GUI.Box(stepR, mark + StepTitles[i], stepStyle);
            }

            Rect actionBtn = new Rect(r.x + 50f, r.y + 350f, w - 100f, 50f);
            string btnText = currentOverrideStep < StepTitles.Length
                ? "CONFIRM " + StepTitles[currentOverrideStep]
                : "EXECUTE OVERRIDE";

            if (GUI.Button(actionBtn, btnText, btnStyle))
            {
                AdvanceOverrideStep();
            }
        }

        if (GUI.Button(new Rect(r.x + w * 0.5f - 80f, r.y + h - 45f, 160f, 32f), "Close (ESC)"))
        {
            CloseTerminal();
        }
    }
}
