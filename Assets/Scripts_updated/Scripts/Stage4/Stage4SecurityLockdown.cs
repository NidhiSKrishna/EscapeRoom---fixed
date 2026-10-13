using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 - SECURITY LOCKDOWN (spec sections 28 to 31).
///
/// The 30-second adrenaline climax event triggered after the Final Override sequence.
///
/// Flow:
///   Emergency red lights ON
///   Alarm audio loops
///   30-second countdown runs
///   Final door remains locked
///   Player must complete 3 emergency actions on the emergency control stations:
///     1. Activate Emergency Power (action 0)
///     2. Disable Security Matrix (action 1)
///     3. Final Evacuation Authorization (action 2)
///
/// When all complete -> unlocks FinalExitDoor.
/// If timer expires -> LOCKDOWN FAILED, safely resets to allow retry without player death.
///
/// Required methods (spec section 30):
///   - public void StartLockdown()
///   - public void StopLockdown()
///   - public void ResetLockdown()
///   - public float GetRemainingTime()
///   - public bool IsActive()
///   - public void CompleteEmergencyAction(int actionIndex)
///   - public bool AreAllEmergencyActionsComplete()
/// </summary>
public class Stage4SecurityLockdown : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;
    [SerializeField] private Stage4ExitDoor exitDoor;

    [Header("Settings")]
    [SerializeField] private float lockdownDurationSeconds = 30f;

    [Header("Emergency Stations")]
    [SerializeField] private bool autoBuildEmergencyStations = true;
    [Tooltip("3 Emergency action interactables (Power, Matrix, Auth)")]
    [SerializeField] private Stage4Interactable[] emergencyStations = new Stage4Interactable[3];

    [Header("Lighting")]
    [SerializeField] private Light[] normalLights;
    [SerializeField] private Light[] emergencyLights;
    [SerializeField] private Color emergencyAmbientColor = new Color(0.4f, 0.05f, 0.05f);

    [Header("Audio")]
    [SerializeField] private AudioSource alarmAudioSource;
    [SerializeField] private AudioClip alarmClip;
    [SerializeField] private AudioClip actionCompleteClip;

    private static readonly string[] ActionNames =
    {
        "Activate Emergency Power",
        "Disable Security Matrix",
        "Final Evacuation Authorization"
    };

    private bool isActive;
    private float remainingTime;
    private bool[] actionCompleted = new bool[3];
    private Action[] stationHandlers;
    private Coroutine alarmRoutine;

    private Color cachedAmbient;
    private GUIStyle bannerStyle, timerStyle;

    private Color[] stationOriginalColors = new Color[3];
    private bool[] stationColorSaved = new bool[3];
    private string noticeText = "";
    private Color noticeColor = Color.white;
    private float noticeUntil;
    private GUIStyle noticeStyle;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        if (exitDoor == null) exitDoor = FindFirstObjectByType<Stage4ExitDoor>();

        if (NeedsStations())
        {
            if (autoBuildEmergencyStations) BuildTestStations();
            else Debug.LogError("[Stage4Lockdown] Assign 3 emergency stations or tick Auto Build.");
        }

        SetupAudio();
        SetupPrompts();
        SetStationsInteractable(false);
        SetEmergencyLighting(false);
    }

    private void OnEnable()
    {
        WireStationEvents();

        if (manager != null)
        {
            manager.OnLockdownStarted += HandleLockdownStarted;
            manager.OnStageReset += ResetLockdown;
            manager.OnStageFailed += StopLockdown;
            manager.OnEscapeCompleted += HandleEscapeCompleted;
        }
    }

    private void OnDisable()
    {
        UnwireStationEvents();

        if (manager != null)
        {
            manager.OnLockdownStarted -= HandleLockdownStarted;
            manager.OnStageReset -= ResetLockdown;
            manager.OnStageFailed -= StopLockdown;
            manager.OnEscapeCompleted -= HandleEscapeCompleted;
        }
        StopLockdown();
    }

    private void Update()
    {
        if (!isActive) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            HandleLockdownFailure();
        }
    }

    private void HandleLockdownStarted()
    {
        StartLockdown();
    }

    private void HandleEscapeCompleted()
    {
        StopLockdown();
        SetEmergencyLighting(false);
    }

    // =====================================================================
    // REQUIRED METHODS (spec section 30)
    // =====================================================================

    public void StartLockdown()
    {
        isActive = true;
        remainingTime = lockdownDurationSeconds;
        for (int i = 0; i < actionCompleted.Length; i++) actionCompleted[i] = false;

        SetEmergencyLighting(true);
        PlayAlarmSound();
        SetStationsInteractable(true);
        SetupPrompts();
    }

    public void StopLockdown()
    {
        isActive = false;
        StopAlarmSound();
        SetStationsInteractable(false);
    }

    public void ResetLockdown()
    {
        StopLockdown();
        remainingTime = lockdownDurationSeconds;
        for (int i = 0; i < actionCompleted.Length; i++) actionCompleted[i] = false;
        RestoreStationVisuals();
        SetEmergencyLighting(false);
        SetupPrompts();
    }

    private void RestoreStationVisuals()
    {
        if (emergencyStations == null) return;
        for (int i = 0; i < emergencyStations.Length && i < 3; i++)
        {
            if (emergencyStations[i] == null) continue;

            Renderer r = emergencyStations[i].GetComponent<Renderer>();
            if (r != null && stationColorSaved[i]) r.material.color = stationOriginalColors[i];
            stationColorSaved[i] = false;

            Transform beacon = emergencyStations[i].transform.Find("AlertBeacon");
            if (beacon != null)
            {
                Renderer br = beacon.GetComponent<Renderer>();
                if (br != null) br.material.color = new Color(1f, 0.2f, 0.2f);
            }
        }
    }

    private void ShowNotice(string text, Color color, float seconds)
    {
        noticeText = text;
        noticeColor = color;
        noticeUntil = Time.unscaledTime + seconds;
    }

    public float GetRemainingTime()
    {
        return Mathf.Max(0f, remainingTime);
    }

    public bool IsActive()
    {
        return isActive;
    }

    public void CompleteEmergencyAction(int actionIndex)
    {
        if (!isActive || actionIndex < 0 || actionIndex >= actionCompleted.Length) return;
        if (actionCompleted[actionIndex]) return;

        actionCompleted[actionIndex] = true;
        PlayActionCompleteSound(actionIndex);

        if (emergencyStations != null && actionIndex < emergencyStations.Length && emergencyStations[actionIndex] != null)
        {
            emergencyStations[actionIndex].SetPrompt(ActionNames[actionIndex] + ": ENGAGED");
            emergencyStations[actionIndex].SetInteractable(false);

            Renderer r = emergencyStations[actionIndex].GetComponent<Renderer>();
            if (r != null)
            {
                if (!stationColorSaved[actionIndex])
                {
                    stationOriginalColors[actionIndex] = r.material.color;
                    stationColorSaved[actionIndex] = true;
                }
                r.material.color = new Color(0.2f, 1f, 0.4f);
            }

            Transform beacon = emergencyStations[actionIndex].transform.Find("AlertBeacon");
            if (beacon != null)
            {
                Renderer br = beacon.GetComponent<Renderer>();
                if (br != null) br.material.color = new Color(0.2f, 1f, 0.4f);
            }
        }

        if (AreAllEmergencyActionsComplete())
        {
            // All emergency requirements met! The countdown has done its job: stop it, so it can never
            // time out while the player is running to the open door.
            isActive = false;
            StopAlarmSound();
            ShowNotice("ALL STATIONS ENGAGED - EXIT DOOR OPEN! RUN TO THE DOOR!", new Color(0.3f, 1f, 0.45f), 10f);
            if (exitDoor != null) exitDoor.UnlockDoor();
            else Debug.LogError("[Stage4Lockdown] No Stage4ExitDoor found, so the door cannot open.");
        }
    }

    public bool AreAllEmergencyActionsComplete()
    {
        for (int i = 0; i < actionCompleted.Length; i++)
        {
            if (!actionCompleted[i]) return false;
        }
        return true;
    }

    // =====================================================================
    // FAILURE & RETRY (spec section 31)
    // =====================================================================

    private void HandleLockdownFailure()
    {
        StopLockdown();
        // Do not destroy player! Reset lockdown and allow retry of override sequence
        Debug.LogWarning("[Stage4Lockdown] Lockdown countdown expired! Resetting for retry.");
        ShowNotice("LOCKDOWN FAILED - go back to the Central Terminal and run the override again", new Color(1f, 0.4f, 0.3f), 6f);
        StartCoroutine(FailureRetryRoutine());
    }

    private IEnumerator FailureRetryRoutine()
    {
        yield return new WaitForSeconds(2f);
        ResetLockdown();
        if (manager != null)
        {
            // Go back to FinalOverride so the Central Terminal works again (the old call was ignored
            // because the state was still Lockdown, which left the player stuck).
            manager.RestartFinalOverride();
        }
    }

    // =====================================================================
    // STATIONS SETUP
    // =====================================================================

    private void WireStationEvents()
    {
        if (emergencyStations == null) return;
        stationHandlers = new Action[emergencyStations.Length];

        for (int i = 0; i < emergencyStations.Length; i++)
        {
            if (emergencyStations[i] == null) continue;
            int idx = i;
            stationHandlers[i] = () => CompleteEmergencyAction(idx);
            emergencyStations[i].OnInteracted += stationHandlers[i];
        }
    }

    private void UnwireStationEvents()
    {
        if (emergencyStations == null || stationHandlers == null) return;
        for (int i = 0; i < emergencyStations.Length && i < stationHandlers.Length; i++)
        {
            if (emergencyStations[i] != null && stationHandlers[i] != null)
                emergencyStations[i].OnInteracted -= stationHandlers[i];
        }
    }

    private void SetStationsInteractable(bool value)
    {
        if (emergencyStations == null) return;
        for (int i = 0; i < emergencyStations.Length; i++)
        {
            if (emergencyStations[i] != null && (!actionCompleted[i] || !value))
                emergencyStations[i].SetInteractable(value);
        }
    }

    private void SetupPrompts()
    {
        if (emergencyStations == null) return;
        for (int i = 0; i < emergencyStations.Length && i < ActionNames.Length; i++)
        {
            if (emergencyStations[i] != null)
            {
                string status = actionCompleted[i] ? " [ENGAGED]" : "";
                emergencyStations[i].SetPrompt("Press E to " + ActionNames[i] + status);
            }
        }
    }

    private bool NeedsStations()
    {
        if (emergencyStations == null || emergencyStations.Length != 3) return true;
        for (int i = 0; i < emergencyStations.Length; i++)
            if (emergencyStations[i] == null) return true;
        return false;
    }

    private void BuildTestStations()
    {
        emergencyStations = new Stage4Interactable[3];
        float[] xPos = { -1.8f, 0f, 1.8f };

        for (int i = 0; i < 3; i++)
        {
            GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
            station.name = "EmergencyStation_" + (i + 1);
            station.transform.SetParent(transform, false);
            station.transform.localPosition = new Vector3(xPos[i], 0.7f, 0f);
            station.transform.localScale = new Vector3(0.8f, 1.3f, 0.4f);

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "AlertBeacon";
            indicator.transform.SetParent(station.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            indicator.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            Renderer r = indicator.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1f, 0.2f, 0.2f);

            emergencyStations[i] = station.AddComponent<Stage4Interactable>();
        }
    }

    // =====================================================================
    // LIGHTING & AUDIO
    // =====================================================================

    private void SetEmergencyLighting(bool isEmergency)
    {
        if (isEmergency)
        {
            cachedAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = emergencyAmbientColor;
        }
        else if (cachedAmbient != default)
        {
            RenderSettings.ambientLight = cachedAmbient;
        }

        if (normalLights != null)
        {
            for (int i = 0; i < normalLights.Length; i++)
                if (normalLights[i] != null) normalLights[i].enabled = !isEmergency;
        }

        if (emergencyLights != null)
        {
            for (int i = 0; i < emergencyLights.Length; i++)
                if (emergencyLights[i] != null) emergencyLights[i].enabled = isEmergency;
        }
    }

    private void SetupAudio()
    {
        if (alarmAudioSource == null) alarmAudioSource = GetComponent<AudioSource>();
        if (alarmAudioSource == null) alarmAudioSource = gameObject.AddComponent<AudioSource>();
        alarmAudioSource.playOnAwake = false;
        alarmAudioSource.loop = true;
        alarmAudioSource.spatialBlend = 0f;
    }

    private void PlayAlarmSound()
    {
        if (alarmClip != null)
        {
            alarmAudioSource.clip = alarmClip;
            alarmAudioSource.Play();
        }
        else
        {
            if (alarmRoutine != null) StopCoroutine(alarmRoutine);
            alarmRoutine = StartCoroutine(PulsingAlarmRoutine());
        }
    }

    private void StopAlarmSound()
    {
        if (alarmAudioSource != null) alarmAudioSource.Stop();
        if (alarmRoutine != null)
        {
            StopCoroutine(alarmRoutine);
            alarmRoutine = null;
        }
    }

    private IEnumerator PulsingAlarmRoutine()
    {
        AudioClip high = MakeTone("AlarmHigh", 800f, 0.28f);
        AudioClip low = MakeTone("AlarmLow", 550f, 0.28f);

        while (isActive)
        {
            alarmAudioSource.PlayOneShot(high);
            yield return new WaitForSeconds(0.32f);
            if (!isActive) break;
            alarmAudioSource.PlayOneShot(low);
            yield return new WaitForSeconds(0.32f);
        }
        alarmRoutine = null;
    }

    private void PlayActionCompleteSound(int index)
    {
        if (actionCompleteClip != null) alarmAudioSource.PlayOneShot(actionCompleteClip);
        else alarmAudioSource.PlayOneShot(MakeTone("ActionDone_" + index, 600f + index * 100f, 0.35f));
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
    // HUD & COUNTDOWN
    // =====================================================================

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(noticeText) && Time.unscaledTime < noticeUntil)
        {
            if (noticeStyle == null)
            {
                noticeStyle = new GUIStyle(GUI.skin.box);
                noticeStyle.fontSize = 22;
                noticeStyle.fontStyle = FontStyle.Bold;
                noticeStyle.alignment = TextAnchor.MiddleCenter;
                noticeStyle.wordWrap = true;
            }
            noticeStyle.normal.textColor = noticeColor;
            float nw = Mathf.Min(760f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - nw) * 0.5f, Screen.height * 0.5f - 160f, nw, 70f), noticeText, noticeStyle);
        }

        if (!isActive) return;

        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.box);
            bannerStyle.fontSize = 20;
            bannerStyle.fontStyle = FontStyle.Bold;
            bannerStyle.alignment = TextAnchor.MiddleCenter;
            bannerStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);

            timerStyle = new GUIStyle(GUI.skin.label);
            timerStyle.fontSize = 32;
            timerStyle.fontStyle = FontStyle.Bold;
            timerStyle.alignment = TextAnchor.MiddleCenter;
            timerStyle.normal.textColor = Color.white;
        }

        float w = 520f;
        float h = 90f;
        Rect r = new Rect((Screen.width - w) * 0.5f, 20f, w, h);

        GUI.Box(r, GUIContent.none, bannerStyle);
        GUI.Label(new Rect(r.x, r.y + 8f, w, 30f), "CRITICAL: SECURITY LOCKDOWN IN EFFECT", bannerStyle);

        int secs = Mathf.CeilToInt(remainingTime);
        GUI.Label(new Rect(r.x, r.y + 40f, w, 40f), "00:" + secs.ToString("00"), timerStyle);
    }
}
