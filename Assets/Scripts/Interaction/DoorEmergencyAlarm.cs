using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using EscapeRoom.UI;
using EscapeRoom.Puzzle;
using EscapeRoom.Core;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Level 1 Emergency Alarm:
    /// Once the player obtains/reveals the exit keypad code, activates an emergency 1-minute (60s)
    /// door lockdown countdown, an alarming red strobe light at the gate/keypad, and a looped alarm siren.
    /// Disarms when the correct code is entered at the keypad.
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorEmergencyAlarm : MonoBehaviour
    {
        private static DoorEmergencyAlarm instance;
        public static DoorEmergencyAlarm Instance => instance;

        [Header("Alarm Audio & Visuals")]
        [SerializeField] private Vector3 gatePosition = new Vector3(0.9f, 2.3f, 5.85f);
        [SerializeField] private Color alarmColor = new Color(1f, 0.05f, 0.05f);
        [SerializeField] private float strobeFrequency = 3.5f;

        private Light alarmLight;
        private AudioSource alarmSource;
        private AudioSource countdownAudioSource;
        private bool isAlarmActive = false;
        private bool isDisarmed = false;
        private Coroutine strobeCoroutine;
        private Coroutine countdownSfxCoroutine;
        private Coroutine delayedNoteTriggerCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "EscapeRoom_Main")
            {
                if (FindFirstObjectByType<DoorEmergencyAlarm>() == null)
                {
                    GameObject go = new GameObject("[DoorEmergencyAlarm]");
                    go.AddComponent<DoorEmergencyAlarm>();
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnEnable()
        {
            ExitCodeDisplayUI.OnCodeClosed += HandleNoteFinished;
            ExitCodeDisplayUI.OnCodeDisplayed += HandleNoteOpened;
            LockedContainer.OnAnyContainerOpened += HandleLockboxOpened;
            KeypadController.OnAnyCodeAccepted += DisarmAlarm;
        }

        private void OnDisable()
        {
            ExitCodeDisplayUI.OnCodeClosed -= HandleNoteFinished;
            ExitCodeDisplayUI.OnCodeDisplayed -= HandleNoteOpened;
            LockedContainer.OnAnyContainerOpened -= HandleLockboxOpened;
            KeypadController.OnAnyCodeAccepted -= DisarmAlarm;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void HandleLockboxOpened()
        {
            // Lockbox opened; player is about to read or is reading the code
            if (delayedNoteTriggerCoroutine != null) StopCoroutine(delayedNoteTriggerCoroutine);
            delayedNoteTriggerCoroutine = StartCoroutine(DelayedAutoTriggerIfNoteIgnored(8f));
        }

        private void HandleNoteOpened()
        {
            // When note opens, give the player 4 seconds of reading before initiating countdown if they don't close it earlier
            if (delayedNoteTriggerCoroutine != null) StopCoroutine(delayedNoteTriggerCoroutine);
            delayedNoteTriggerCoroutine = StartCoroutine(DelayedAutoTriggerIfNoteIgnored(4.5f));
        }

        private void HandleNoteFinished()
        {
            // Player closed the note after reading it -> trigger immediate 10s emergency countdown!
            if (delayedNoteTriggerCoroutine != null)
            {
                StopCoroutine(delayedNoteTriggerCoroutine);
                delayedNoteTriggerCoroutine = null;
            }
            TriggerEmergencyAlarm();
        }

        private IEnumerator DelayedAutoTriggerIfNoteIgnored(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerEmergencyAlarm();
        }

        public void TriggerEmergencyAlarm()
        {
            if (isAlarmActive || isDisarmed) return;
            isAlarmActive = true;

            // 1. Set 10-second emergency timer on door / stage
            if (StageTimer.Instance != null)
            {
                StageTimer.Instance.TriggerEmergencyCountdown(10f);
            }

            // 2. Locate gate / keypad anchor if available
            GameObject keypadObj = GameObject.Find("Terminal_Keypad");
            Vector3 pos = keypadObj != null ? keypadObj.transform.position + Vector3.up * 0.85f : gatePosition;

            // 3. Create alarm light at gate
            CreateAlarmLight(pos);

            // 4. Create and start alarm siren sound at gate
            StartAlarmSound(pos);

            // 5. Start accelerating countdown sound effects (10-second ticker)
            StartCountdownAudio();

            // 6. Visual notification banner
            FeedbackHUD.ShowMessage("CRITICAL ALERT: CODE ACQUIRED! GATE LOCKS IN 10s — RUN TO EXIT!", Color.red);
            Debug.Log("<color=#d9534f><b>[DoorEmergencyAlarm]</b></color> 10-second gate emergency countdown & alarming sounds initiated!");
        }

        public void DisarmAlarm()
        {
            if (isDisarmed) return;
            isDisarmed = true;
            isAlarmActive = false;

            if (delayedNoteTriggerCoroutine != null)
            {
                StopCoroutine(delayedNoteTriggerCoroutine);
                delayedNoteTriggerCoroutine = null;
            }

            if (strobeCoroutine != null)
            {
                StopCoroutine(strobeCoroutine);
                strobeCoroutine = null;
            }

            if (countdownSfxCoroutine != null)
            {
                StopCoroutine(countdownSfxCoroutine);
                countdownSfxCoroutine = null;
            }

            if (alarmLight != null)
            {
                alarmLight.enabled = false;
                alarmLight.color = Color.green;
            }

            if (alarmSource != null && alarmSource.isPlaying)
            {
                alarmSource.Stop();
            }

            if (countdownAudioSource != null && countdownAudioSource.isPlaying)
            {
                countdownAudioSource.Stop();
            }

            FeedbackHUD.ShowMessage("GATE LOCKDOWN OVERRIDDEN — ACCESS GRANTED", Color.green);
            Debug.Log("<color=#5cb85c><b>[DoorEmergencyAlarm]</b></color> Alarm disarmed!");
        }

        private void CreateAlarmLight(Vector3 pos)
        {
            if (alarmLight == null)
            {
                GameObject lightGo = new GameObject("GateAlarmLight");
                lightGo.transform.SetParent(transform);
                lightGo.transform.position = pos;

                alarmLight = lightGo.AddComponent<Light>();
                alarmLight.type = LightType.Point;
                alarmLight.color = alarmColor;
                alarmLight.range = 8.5f;
                alarmLight.intensity = 2.5f;
                alarmLight.shadows = LightShadows.None;
            }

            alarmLight.enabled = true;
            if (strobeCoroutine != null) StopCoroutine(strobeCoroutine);
            strobeCoroutine = StartCoroutine(StrobeLightRoutine());
        }

        private IEnumerator StrobeLightRoutine()
        {
            while (isAlarmActive)
            {
                if (alarmLight != null)
                {
                    float sine = Mathf.PingPong(Time.time * strobeFrequency * 2f, 1f);
                    alarmLight.intensity = Mathf.Lerp(0.3f, 4.0f, sine);
                }
                yield return null;
            }
        }

        private void StartAlarmSound(Vector3 pos)
        {
            if (alarmSource == null)
            {
                GameObject sndGo = new GameObject("GateAlarmSiren");
                sndGo.transform.SetParent(transform);
                sndGo.transform.position = pos;

                alarmSource = sndGo.AddComponent<AudioSource>();
                alarmSource.spatialBlend = 0.65f; // 3D spatialized near gate, audible everywhere
                alarmSource.minDistance = 3f;
                alarmSource.maxDistance = 25f;
                alarmSource.loop = true;
                alarmSource.volume = 0.85f;
                alarmSource.clip = CreateProceduralAlarmSiren();
            }

            if (!alarmSource.isPlaying)
            {
                alarmSource.Play();
            }
        }

        private AudioClip CreateProceduralAlarmSiren()
        {
            int sampleRate = 44100;
            float duration = 1.0f; // 1-second loop
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Dual-tone siren: 880Hz for 0.5s, 580Hz for 0.5s
                float freq = (t < 0.5f) ? 880f : 587f;
                phase += 2f * Mathf.PI * freq / sampleRate;
                if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;

                // Square-ish wave harmonic with sine for buzzer/siren tone
                float val = Mathf.Sin(phase) * 0.7f + (Mathf.Sin(phase * 3f) / 3f) * 0.3f;
                samples[i] = Mathf.Clamp(val * 0.45f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("GateEmergencySiren", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void StartCountdownAudio()
        {
            if (countdownAudioSource == null)
            {
                GameObject countGo = new GameObject("CountdownAudioPlayer");
                countGo.transform.SetParent(transform);
                countdownAudioSource = countGo.AddComponent<AudioSource>();
                countdownAudioSource.spatialBlend = 0f; // 2D direct into ears
                countdownAudioSource.volume = 0.9f;
            }

            if (countdownSfxCoroutine != null) StopCoroutine(countdownSfxCoroutine);
            countdownSfxCoroutine = StartCoroutine(CountdownAudioRoutine());
        }

        private IEnumerator CountdownAudioRoutine()
        {
            AudioClip normalBeep = CreateProceduralBeep(880f, 0.12f);
            AudioClip urgentBeep = CreateProceduralBeep(1320f, 0.08f);

            // 10 seconds total:
            // First 7 seconds: one beep per second
            for (int i = 10; i >= 4; i--)
            {
                if (!isAlarmActive) yield break;
                if (countdownAudioSource != null) countdownAudioSource.PlayOneShot(normalBeep, 0.85f);
                yield return new WaitForSeconds(1.0f);
            }

            // Final 3 seconds: urgent fast beeps (every 0.5s)
            for (int i = 6; i >= 1; i--)
            {
                if (!isAlarmActive) yield break;
                if (countdownAudioSource != null) countdownAudioSource.PlayOneShot(urgentBeep, 1.0f);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private AudioClip CreateProceduralBeep(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Envelope decay to prevent clicks
                float envelope = 1f - (t / duration);
                float val = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
                samples[i] = Mathf.Clamp(val * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create($"Beep_{frequency}Hz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
