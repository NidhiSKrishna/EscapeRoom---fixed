using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 - Puzzle 3: CONTROL PANEL (spec sections 18 and 19).
///
/// Flow: The player must find and activate the correct control console among 4 panels (A, B, C, D).
/// The correct panel corresponds to the FIRST color revealed in the memory sequence (Puzzle 2).
///
/// Required methods (spec section 19):
///   - public void Initialize(Stage4PuzzleGenerator generator)
///   - public void SelectPanel(int panelIndex)
///   - public bool IsCorrectPanel(int panelIndex)
///   - public void ResetPanel()
///
/// Correct   -> green indicator, success sound, Stage4Manager.CompleteControlPanel()
/// Incorrect -> warning, error sound, time penalty (never permanently locks out player)
/// </summary>
public class Stage4ControlPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Panels (leave empty + keep Auto Build ticked to generate test consoles)")]
    [SerializeField] private bool autoBuildPanels = true;
    [Tooltip("4 consoles/buttons for Panels A, B, C, D (indices 0, 1, 2, 3)")]
    [SerializeField] private Stage4Interactable[] panelButtons = new Stage4Interactable[4];

    [Header("Audio (optional - synthetic tones generated if empty)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    private static readonly string[] PanelNames = { "PANEL A", "PANEL B", "PANEL C", "PANEL D" };
    private static readonly Color[] DisplayColors =
    {
        new Color(0.95f, 0.2f, 0.2f),   // RED = 0
        new Color(0.2f, 0.5f, 1.0f),    // BLUE = 1
        new Color(0.2f, 0.9f, 0.35f),   // GREEN = 2
        new Color(1.0f, 0.85f, 0.2f)    // YELLOW = 3
    };

    private Renderer[] panelRenderers;
    private Action[] buttonHandlers;
    private MaterialPropertyBlock block;
    private Coroutine flashRoutine;

    private int[] panelColors;
    private int correctPanelIndex;
    private bool initialized;
    private bool solved;

    private string statusMessage = "";
    private float messageUntil;
    private GUIStyle messageStyle;
    private GUIStyle labelStyle;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        block = new MaterialPropertyBlock();

        if (NeedsPanels())
        {
            if (autoBuildPanels) BuildTestPanels();
            else Debug.LogError("[Stage4ControlPanel] Assign 4 panel buttons or enable Auto Build.");
        }

        CacheRenderers();
        SetupAudio();
        SetupPrompts();
        DimAll();
        SetAllInteractable(false);
    }

    private void OnEnable()
    {
        if (panelButtons != null)
        {
            buttonHandlers = new Action[panelButtons.Length];
            for (int i = 0; i < panelButtons.Length; i++)
            {
                if (panelButtons[i] == null) continue;
                int index = i;
                buttonHandlers[i] = () => SelectPanel(index);
                panelButtons[i].OnInteracted += buttonHandlers[i];
            }
        }

        if (manager != null)
        {
            manager.OnStageStarted += HandleStageStarted;
            manager.OnStageReset += ResetPanel;
            manager.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (panelButtons != null && buttonHandlers != null)
        {
            for (int i = 0; i < panelButtons.Length && i < buttonHandlers.Length; i++)
            {
                if (panelButtons[i] != null && buttonHandlers[i] != null)
                    panelButtons[i].OnInteracted -= buttonHandlers[i];
            }
        }

        if (manager != null)
        {
            manager.OnStageStarted -= HandleStageStarted;
            manager.OnStageReset -= ResetPanel;
            manager.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Start()
    {
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
    // REQUIRED METHODS (spec section 19)
    // =====================================================================

    public void Initialize(Stage4PuzzleGenerator generator)
    {
        if (generator == null) return;

        panelColors = generator.GetControlPanelColors();
        correctPanelIndex = generator.GetCorrectControlPanel();
        solved = false;
        statusMessage = "";
        initialized = true;

        UpdateColors();
        SetupPrompts();
        UpdateInteractables();
    }

    public void SelectPanel(int panelIndex)
    {
        if (!initialized || solved) return;
        if (panelIndex < 0 || panelIndex >= 4) return;
        if (manager != null && manager.GetCurrentState() != Stage4State.ControlPanel) return;

        if (IsCorrectPanel(panelIndex))
        {
            solved = true;
            PlayClip(successClip, 587f, 0.4f);
            ShowMessage("PANEL " + (char)('A' + panelIndex) + " VERIFIED - SUCCESS", 3f);
            FlashIndicator(panelIndex, new Color(0.2f, 1f, 0.4f), true);

            UpdateInteractables();
            if (manager != null) manager.CompleteControlPanel();
        }
        else
        {
            PlayClip(errorClip, 150f, 0.35f);
            ShowMessage("WARNING: PANEL " + (char)('A' + panelIndex) + " REJECTED", 2.5f);
            FlashIndicator(panelIndex, new Color(1f, 0.15f, 0.15f), false);

            if (manager != null) manager.ApplyTimePenalty();
        }
    }

    public bool IsCorrectPanel(int panelIndex)
    {
        return panelIndex == correctPanelIndex;
    }

    public void ResetPanel()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        solved = false;
        initialized = false;
        panelColors = null;
        correctPanelIndex = 0;
        statusMessage = "";

        DimAll();
        SetAllInteractable(false);
    }

    // =====================================================================
    // FEEDBACK & VISUALS
    // =====================================================================

    private void UpdateColors()
    {
        if (panelColors == null || panelRenderers == null) return;
        for (int i = 0; i < 4 && i < panelRenderers.Length; i++)
        {
            int colorIdx = panelColors[i];
            Color c = (colorIdx >= 0 && colorIdx < DisplayColors.Length)
                ? DisplayColors[colorIdx] * 0.4f
                : Color.gray * 0.3f;
            SetRendererColor(panelRenderers[i], c);
        }
    }

    private void DimAll()
    {
        if (panelRenderers == null) return;
        for (int i = 0; i < panelRenderers.Length; i++)
        {
            SetRendererColor(panelRenderers[i], new Color(0.1f, 0.12f, 0.18f));
        }
    }

    private void FlashIndicator(int panelIndex, Color flashColor, bool stayLit)
    {
        if (panelRenderers == null || panelIndex < 0 || panelIndex >= panelRenderers.Length) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(panelIndex, flashColor, stayLit));
    }

    private IEnumerator FlashRoutine(int panelIndex, Color flashColor, bool stayLit)
    {
        Renderer r = panelRenderers[panelIndex];
        SetRendererColor(r, flashColor);
        yield return new WaitForSeconds(0.8f);

        if (stayLit)
        {
            SetRendererColor(r, new Color(0.2f, 1f, 0.4f));
        }
        else
        {
            UpdateColors();
        }
        flashRoutine = null;
    }

    private void SetRendererColor(Renderer r, Color color)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color * 0.6f);
        r.SetPropertyBlock(block);
    }

    private void UpdateInteractables()
    {
        bool active = initialized && !solved && manager != null &&
                      manager.GetCurrentState() == Stage4State.ControlPanel;
        SetAllInteractable(active);
    }

    private void SetAllInteractable(bool value)
    {
        if (panelButtons == null) return;
        for (int i = 0; i < panelButtons.Length; i++)
        {
            if (panelButtons[i] != null) panelButtons[i].SetInteractable(value);
        }
    }

    private void SetupPrompts()
    {
        if (panelButtons == null) return;
        for (int i = 0; i < panelButtons.Length && i < 4; i++)
        {
            if (panelButtons[i] == null) continue;
            string colorLabel = "";
            if (panelColors != null && i < panelColors.Length)
            {
                int c = panelColors[i];
                if (c >= 0 && c < Stage4PuzzleGenerator.ColorNames.Length)
                    colorLabel = " [" + Stage4PuzzleGenerator.ColorNames[c] + "]";
            }
            panelButtons[i].SetPrompt("Press E to authorize " + PanelNames[i] + colorLabel);
        }
    }

    private void CacheRenderers()
    {
        panelRenderers = new Renderer[4];
        for (int i = 0; i < 4 && i < panelButtons.Length; i++)
        {
            if (panelButtons[i] != null)
                panelRenderers[i] = panelButtons[i].GetComponentInChildren<Renderer>();
        }
    }

    private bool NeedsPanels()
    {
        if (panelButtons == null || panelButtons.Length != 4) return true;
        for (int i = 0; i < panelButtons.Length; i++)
            if (panelButtons[i] == null) return true;
        return false;
    }

    private void BuildTestPanels()
    {
        panelButtons = new Stage4Interactable[4];
        float[] xPos = { -2.1f, -0.7f, 0.7f, 2.1f };

        for (int i = 0; i < 4; i++)
        {
            GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
            console.name = "Console_" + (char)('A' + i);
            console.transform.SetParent(transform, false);
            console.transform.localPosition = new Vector3(xPos[i], 0.6f, 0f);
            console.transform.localScale = new Vector3(0.9f, 1.2f, 0.5f);

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "ScreenIndicator";
            screen.transform.SetParent(console.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.35f, -0.26f);
            screen.transform.localScale = new Vector3(0.7f, 0.4f, 0.05f);

            panelButtons[i] = console.AddComponent<Stage4Interactable>();
        }
    }

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayClip(AudioClip clip, float toneFreq, float duration)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioClip syn = MakeTone("PanelTone_" + toneFreq, toneFreq, duration);
            audioSource.PlayOneShot(syn);
        }
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

    private void ShowMessage(string text, float duration)
    {
        statusMessage = text;
        messageUntil = Time.unscaledTime + duration;
    }

    // Floating "PANEL A / No. 1" labels above each console, so the player can tell which console is which.
    private void DrawConsoleLabels()
    {
        Camera cam = Camera.main;
        if (cam == null || panelButtons == null) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box);
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.white;
        }

        for (int i = 0; i < panelButtons.Length && i < 4; i++)
        {
            if (panelButtons[i] == null) continue;
            Vector3 world = panelButtons[i].transform.position + Vector3.up * 0.95f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.5f) continue; // behind the camera

            float y = Screen.height - sp.y;
            Rect rect = new Rect(sp.x - 55f, y - 24f, 110f, 48f);
            GUI.Box(rect, PanelNames[i] + "\nNo. " + (i + 1), labelStyle);
        }
    }

    private void OnGUI()
    {
        if (manager == null || manager.GetCurrentState() != Stage4State.ControlPanel) return;

        if (initialized && !solved) DrawConsoleLabels();

        string display = "";
        if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < messageUntil)
            display = statusMessage;
        else if (initialized && !solved)
            display = "Find the console showing the FIRST light colour. Its number is DIGIT 3 of the code";

        if (string.IsNullOrEmpty(display)) return;

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.box);
            messageStyle.fontSize = 18;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(Screen.width * 0.5f - 320f, 50f, 640f, 38f), display, messageStyle);
    }
}
