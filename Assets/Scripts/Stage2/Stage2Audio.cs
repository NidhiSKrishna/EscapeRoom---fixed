using System;
using UnityEngine;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Self-contained procedural audio synthesizer for Stage 2 (Storage Room).
    /// Generates lightweight sound effects at runtime without any external audio asset dependencies.
    /// Provides feedback for UI buttons, pattern selections, puzzle outcomes, locks, keycard pickups,
    /// alarms, and stage completion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class Stage2Audio : MonoBehaviour
    {
        private static Stage2Audio instance;
        public static Stage2Audio Instance => instance;

        private AudioSource audioSource;
        private AudioSource alarmAudioSource;

        private AudioClip buttonClickClip;
        private AudioClip patternSelectClip;
        private AudioClip correctClip;
        private AudioClip wrongClip;
        private AudioClip unlockClip;
        private AudioClip pickupClip;
        private AudioClip alarmSirenClip;
        private AudioClip disarmClip;
        private AudioClip completeClip;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D UI sound

            // Separate looping audio source for the security siren
            GameObject alarmChild = new GameObject("AlarmAudioSource");
            alarmChild.transform.SetParent(transform, false);
            alarmAudioSource = alarmChild.AddComponent<AudioSource>();
            alarmAudioSource.playOnAwake = false;
            alarmAudioSource.spatialBlend = 0f;
            alarmAudioSource.loop = true;

            GenerateClips();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void GenerateClips()
        {
            const int sampleRate = 44100;

            // 1. Button Click (Short crisp click)
            buttonClickClip = CreateAudioClip("S2_ButtonClick", sampleRate, 0.05f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 60f);
                return env * Mathf.Sin(2f * Mathf.PI * 1100f * t) * 0.4f;
            });

            // 2. Pattern Select (Melodic blip)
            patternSelectClip = CreateAudioClip("S2_PatternSelect", sampleRate, 0.08f, (t, dur) =>
            {
                float freq = Mathf.Lerp(520f, 780f, t / dur);
                float env = Mathf.Exp(-t * 25f);
                return env * Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
            });

            // 3. Correct Answer (Pleasing major arpeggio / harmonic chord)
            correctClip = CreateAudioClip("S2_CorrectAnswer", sampleRate, 0.40f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 7f);
                float note1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t); // C5
                float note2 = (t > 0.08f) ? Mathf.Sin(2f * Mathf.PI * 659.25f * (t - 0.08f)) : 0f; // E5
                float note3 = (t > 0.16f) ? Mathf.Sin(2f * Mathf.PI * 783.99f * (t - 0.16f)) : 0f; // G5
                float note4 = (t > 0.24f) ? Mathf.Sin(2f * Mathf.PI * 1046.50f * (t - 0.24f)) : 0f; // C6
                return env * (note1 * 0.3f + note2 * 0.3f + note3 * 0.3f + note4 * 0.4f);
            });

            // 4. Wrong Answer (Low error buzz)
            wrongClip = CreateAudioClip("S2_WrongAnswer", sampleRate, 0.30f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 8f);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 140f * t)) * 0.35f; // square/buzz
                return env * wave;
            });

            // 5. Container Unlock (Mechanical clunk + sweet harmonic chime)
            unlockClip = CreateAudioClip("S2_ContainerUnlock", sampleRate, 0.50f, (t, dur) =>
            {
                float clunk = Mathf.Exp(-t * 40f) * Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.6f;
                float chime = 0f;
                if (t > 0.12f)
                {
                    float chimeT = t - 0.12f;
                    chime = Mathf.Exp(-chimeT * 8f) * (Mathf.Sin(2f * Mathf.PI * 880f * chimeT) + Mathf.Sin(2f * Mathf.PI * 1320f * chimeT)) * 0.25f;
                }
                return clunk + chime;
            });

            // 6. Item Pickup (Bright twin chime)
            pickupClip = CreateAudioClip("S2_ItemPickup", sampleRate, 0.28f, (t, dur) =>
            {
                float n1 = Mathf.Exp(-t * 16f) * Mathf.Sin(2f * Mathf.PI * 987.77f * t); // B5
                float n2 = 0f;
                if (t > 0.09f)
                {
                    float t2 = t - 0.09f;
                    n2 = Mathf.Exp(-t2 * 14f) * Mathf.Sin(2f * Mathf.PI * 1318.51f * t2); // E6
                }
                return (n1 * 0.4f + n2 * 0.5f);
            });

            // 7. Security Alarm Siren (Wailing siren cycle)
            alarmSirenClip = CreateAudioClip("S2_AlarmSiren", sampleRate, 1.20f, (t, dur) =>
            {
                // Triangle wave modulation between 550Hz and 880Hz
                float cycle = Mathf.PingPong(t * 2.2f, 1f);
                float freq = Mathf.Lerp(500f, 900f, cycle);
                float sample = Mathf.Sin(2f * Mathf.PI * freq * t);
                return sample * 0.45f;
            });

            // 8. Disarm Sound (Descending electronic shutdown)
            disarmClip = CreateAudioClip("S2_Disarm", sampleRate, 0.45f, (t, dur) =>
            {
                float freq = Mathf.Lerp(900f, 250f, t / dur);
                float env = Mathf.Exp(-t * 5f);
                return env * Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f;
            });

            // 9. Stage Complete (Triumphant fanfare arpeggio)
            completeClip = CreateAudioClip("S2_StageComplete", sampleRate, 0.85f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 3.5f);
                float n1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t); // C5
                float n2 = (t > 0.12f) ? Mathf.Sin(2f * Mathf.PI * 659.25f * (t - 0.12f)) : 0f; // E5
                float n3 = (t > 0.24f) ? Mathf.Sin(2f * Mathf.PI * 783.99f * (t - 0.24f)) : 0f; // G5
                float n4 = (t > 0.36f) ? Mathf.Sin(2f * Mathf.PI * 1046.50f * (t - 0.36f)) : 0f; // C6
                return env * (n1 * 0.25f + n2 * 0.25f + n3 * 0.25f + n4 * 0.5f);
            });
        }

        private AudioClip CreateAudioClip(string name, int sampleRate, float duration, Func<float, float, float> generator)
        {
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Clamp(generator(t, duration), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayButtonPress()
        {
            if (audioSource != null && buttonClickClip != null)
                audioSource.PlayOneShot(buttonClickClip, 0.6f);
        }

        public void PlayPatternSelect()
        {
            if (audioSource != null && patternSelectClip != null)
                audioSource.PlayOneShot(patternSelectClip, 0.65f);
        }

        public void PlayCorrectAnswer()
        {
            if (audioSource != null && correctClip != null)
                audioSource.PlayOneShot(correctClip, 0.85f);
        }

        public void PlayWrongAnswer()
        {
            if (audioSource != null && wrongClip != null)
                audioSource.PlayOneShot(wrongClip, 0.75f);
        }

        public void PlayContainerUnlock()
        {
            if (audioSource != null && unlockClip != null)
                audioSource.PlayOneShot(unlockClip, 0.85f);
        }

        public void PlayItemPickup()
        {
            if (audioSource != null && pickupClip != null)
                audioSource.PlayOneShot(pickupClip, 0.85f);
        }

        public void StartSecurityAlarm()
        {
            if (alarmAudioSource != null && alarmSirenClip != null && !alarmAudioSource.isPlaying)
            {
                alarmAudioSource.clip = alarmSirenClip;
                alarmAudioSource.volume = 0.55f;
                alarmAudioSource.Play();
            }
        }

        public void StopSecurityAlarm()
        {
            if (alarmAudioSource != null && alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();
            }
            if (audioSource != null && disarmClip != null)
            {
                audioSource.PlayOneShot(disarmClip, 0.8f);
            }
        }

        public void PlayStageComplete()
        {
            StopSecurityAlarm();
            if (audioSource != null && completeClip != null)
            {
                audioSource.PlayOneShot(completeClip, 0.95f);
            }
        }
    }
}
