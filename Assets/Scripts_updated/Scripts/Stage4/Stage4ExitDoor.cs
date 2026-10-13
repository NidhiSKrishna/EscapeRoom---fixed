using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 - FINAL EXIT DOOR (spec sections 32 and 33).
///
/// Must remain locked until:
///   1. All puzzles complete
///   2. Final Override executed
///   3. Lockdown emergency actions satisfied
///
/// Required methods (spec section 33):
///   - public void UnlockDoor()
///   - public void OpenDoor()
///   - public bool IsUnlocked()
///   - public bool IsOpen()
///   - public void ResetDoor()
///
/// Stepping through the exit trigger triggers Stage4Manager.CompleteEscape().
/// </summary>
public class Stage4ExitDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Door Components")]
    [SerializeField] private bool autoBuildDoor = true;
    [SerializeField] private Transform doorMeshTransform;
    [SerializeField] private Stage4Interactable doorInteractable;
    [SerializeField] private Collider exitTriggerCollider;

    [Header("Animation")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 2.5f, 0f); // slide upward
    [SerializeField] private float openDurationSeconds = 1.8f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip unlockClip;
    [SerializeField] private AudioClip openClip;

    [Header("Prompts")]
    [SerializeField] private string lockedPrompt = "FINAL EXIT: LOCKED - Lockdown Override Required";
    [SerializeField] private string unlockedPrompt = "Press E to Open Final Exit Door";
    [SerializeField] private string openPrompt = "FINAL EXIT: OPEN - ESCAPE NOW";

    private static readonly Color LockedLampColor = new Color(1f, 0.15f, 0.15f);
    private static readonly Color OpenLampColor = new Color(0.2f, 1f, 0.4f);

    private Renderer statusLight;
    private Light openGlow;
    private MaterialPropertyBlock block;
    private Transform playerTransform;
    private bool escapeSent;

    private bool isUnlocked;
    private bool isOpen;
    private Vector3 closedLocalPosition;
    private Coroutine openRoutine;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        block = new MaterialPropertyBlock();

        // Older versions of this script built a solid "DoorFrame" block that stayed in the doorway forever.
        Transform legacyFrame = transform.Find("DoorFrame");
        if (legacyFrame != null) Destroy(legacyFrame.gameObject);

        if (NeedsDoorSetup())
        {
            if (autoBuildDoor) BuildTestDoor();
            else Debug.LogError("[Stage4ExitDoor] Assign door components or tick Auto Build Door.");
        }

        if (doorMeshTransform != null) closedLocalPosition = doorMeshTransform.localPosition;
        SetupAudio();
        ResetDoor();
    }

    private void OnEnable()
    {
        if (doorInteractable != null) doorInteractable.OnInteracted += HandleDoorInteract;
        if (manager != null)
        {
            manager.OnStageReset += ResetDoor;
        }
    }

    private void OnDisable()
    {
        if (doorInteractable != null) doorInteractable.OnInteracted -= HandleDoorInteract;
        if (manager != null)
        {
            manager.OnStageReset -= ResetDoor;
        }
    }

    private void HandleDoorInteract()
    {
        if (!isUnlocked)
        {
            PlayClip(null, 150f, 0.25f);
            return;
        }

        if (!isOpen)
        {
            OpenDoor();
        }
    }

    // =====================================================================
    // REQUIRED METHODS (spec section 33)
    // =====================================================================

    public void UnlockDoor()
    {
        if (isUnlocked) return;
        isUnlocked = true;
        Debug.Log("[Stage4Door] Door unlocked - opening now.");
        SetLamp(statusLight, OpenLampColor);

        PlayClip(unlockClip, 523f, 0.4f);

        if (doorInteractable != null)
        {
            doorInteractable.SetPrompt(unlockedPrompt);
            doorInteractable.SetInteractable(true);
        }

        // Auto-open upon unlock to maintain adrenaline flow
        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;

        PlayClip(openClip, 260f, 0.6f);

        if (doorInteractable != null)
        {
            doorInteractable.SetPrompt(openPrompt);
            doorInteractable.SetInteractable(false);
        }

        if (doorMeshTransform != null)
        {
            // Make the doorway passable straight away, even before the slide animation has finished.
            SetDoorCollidersEnabled(false);

            if (openRoutine != null) StopCoroutine(openRoutine);
            openRoutine = StartCoroutine(OpenAnimationRoutine());
        }

        if (openGlow != null) openGlow.enabled = true;

        if (exitTriggerCollider != null)
        {
            exitTriggerCollider.enabled = true;
        }
    }

    public bool IsUnlocked()
    {
        return isUnlocked;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public void ResetDoor()
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        isUnlocked = false;
        isOpen = false;
        escapeSent = false;
        SetLamp(statusLight, LockedLampColor);

        if (doorMeshTransform != null)
        {
            doorMeshTransform.localPosition = closedLocalPosition;
            SetDoorCollidersEnabled(true);
        }
        if (openGlow != null) openGlow.enabled = false;

        if (doorInteractable != null)
        {
            doorInteractable.SetPrompt(lockedPrompt);
            doorInteractable.SetInteractable(true);
        }

        if (exitTriggerCollider != null)
        {
            exitTriggerCollider.enabled = false;
        }
    }

    // =====================================================================
    // ANIMATION & AUDIO
    // =====================================================================

    private IEnumerator OpenAnimationRoutine()
    {
        Vector3 startPos = doorMeshTransform.localPosition;
        Vector3 endPos = closedLocalPosition + openOffset;
        float elapsed = 0f;

        while (elapsed < openDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDurationSeconds);
            doorMeshTransform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        doorMeshTransform.localPosition = endPos;
        openRoutine = null;
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
        if (clip != null) audioSource.PlayOneShot(clip);
        else audioSource.PlayOneShot(MakeTone("DoorTone_" + toneFreq, toneFreq, duration));
    }

    private static AudioClip MakeTone(string clipName, float freq, float seconds)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float env = Mathf.Sin((float)i / samples * Mathf.PI);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * env * 0.4f;
        }
        AudioClip c = AudioClip.Create(clipName, samples, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    private bool NeedsDoorSetup()
    {
        return doorMeshTransform == null || doorInteractable == null;
    }

    private void BuildTestDoor()
    {
        // The doorway cut into the north wall is 4 m wide and 3.6 m high, so the door panel fills it completely.
        // The panel sits INSIDE the wall thickness and slides sideways into the wall when it opens,
        // so nothing is left in the doorway for the player to bump into.
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "DoorPanel";
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(0f, 1.8f, 0.15f);
        panel.transform.localScale = new Vector3(4.0f, 3.6f, 0.2f);
        doorMeshTransform = panel.transform;
        openOffset = new Vector3(-4.2f, 0f, 0f);

        Renderer r = panel.GetComponent<Renderer>();
        SetLamp(r, new Color(0.24f, 0.28f, 0.36f));

        doorInteractable = panel.AddComponent<Stage4Interactable>();

        // Status light on the wall above the door: RED = locked, GREEN = open.
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lamp.name = "DoorStatusLight";
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(0f, 4.1f, -0.2f);
        lamp.transform.localScale = new Vector3(1.0f, 0.22f, 0.1f);
        Collider lampCol = lamp.GetComponent<Collider>();
        if (lampCol != null) Destroy(lampCol);
        statusLight = lamp.GetComponent<Renderer>();
        SetLamp(statusLight, LockedLampColor);

        // Green glow in the doorway once the door is open, so the player can see where to go.
        GameObject glowObj = new GameObject("DoorOpenGlow");
        glowObj.transform.SetParent(transform, false);
        glowObj.transform.localPosition = new Vector3(0f, 2.0f, -1.2f);
        openGlow = glowObj.AddComponent<Light>();
        openGlow.type = LightType.Point;
        openGlow.color = new Color(0.3f, 1f, 0.5f);
        openGlow.range = 9f;
        openGlow.intensity = 3f;
        openGlow.enabled = false;

        // Backup trigger just past the doorway. (Update() below also checks the player's position.)
        GameObject triggerObj = new GameObject("ExitTriggerZone");
        triggerObj.transform.SetParent(transform, false);
        triggerObj.transform.localPosition = new Vector3(0f, 1.8f, 0.7f);
        BoxCollider box = triggerObj.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3.6f, 3.6f, 1.4f);
        exitTriggerCollider = box;

        ExitZoneSensor sensor = triggerObj.AddComponent<ExitZoneSensor>();
        sensor.OnPlayerEnteredExit += HandlePlayerReachedExit;
    }

    private void SetDoorCollidersEnabled(bool value)
    {
        if (doorMeshTransform == null) return;
        Collider[] cols = doorMeshTransform.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = value;
    }

    private void SetLamp(Renderer r, Color color)
    {
        if (r == null || block == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color * 0.5f);
        r.SetPropertyBlock(block);
    }

    // Reliable exit check: once the door is open, walking into the doorway ends the stage.
    private void Update()
    {
        if (!isOpen || escapeSent || manager == null) return;
        if (manager.GetCurrentState() != Stage4State.Lockdown) return;

        if (playerTransform == null)
        {
            CharacterController cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) playerTransform = cc.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }
        if (playerTransform == null) return;

        Vector3 local = transform.InverseTransformPoint(playerTransform.position);
        if (Mathf.Abs(local.x) < 1.8f && local.z > 0.1f && local.z < 3f && local.y > -1f && local.y < 4f)
        {
            HandlePlayerReachedExit();
        }
    }

    private void HandlePlayerReachedExit()
    {
        if (!isOpen || escapeSent || manager == null) return;
        if (manager.GetCurrentState() != Stage4State.Lockdown) return;

        escapeSent = true;
        manager.CompleteEscape();
    }
}

/// <summary>
/// Trigger sensor placed past the final exit door to confirm escape.
/// </summary>
public class ExitZoneSensor : MonoBehaviour
{
    public Action OnPlayerEnteredExit;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CharacterController>() != null ||
            other.GetComponentInParent<CharacterController>() != null ||
            other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            if (OnPlayerEnteredExit != null) OnPlayerEnteredExit();
        }
    }
}
