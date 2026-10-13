using UnityEngine;

namespace EscapeRoom.Stage3
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class Stage3AudioManager : MonoBehaviour
    {
        public static Stage3AudioManager Instance { get; private set; }

        private AudioSource audioSource;
        private const int SampleRate = 44100;

        private AudioClip[] colorTones = new AudioClip[4];
        private AudioClip correctSound;
        private AudioClip errorSound;
        private AudioClip alarmSound;
        private AudioClip countdownTick;
        private AudioClip completionFanfare;
        private AudioClip pathCorrect;
        private AudioClip pathError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            
            audioSource.playOnAwake = false;

            GenerateAudioClips();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void GenerateAudioClips()
        {
            // 1. Color button tones (Red=C4, Blue=E4, Green=G4, Yellow=A4)
            colorTones[0] = GenerateSineWave("ColorRed", 261.63f, 0.3f, true);
            colorTones[1] = GenerateSineWave("ColorBlue", 329.63f, 0.3f, true);
            colorTones[2] = GenerateSineWave("ColorGreen", 392.00f, 0.3f, true);
            colorTones[3] = GenerateSineWave("ColorYellow", 440.00f, 0.3f, true);

            // 2. Correct sound: Arpeggio C5-E5-G5 (0.4s)
            correctSound = GenerateArpeggio("CorrectSound", new[] { 523.25f, 659.25f, 783.99f }, 0.4f);

            // 3. Error sound: Low dissonant buzz (150Hz + 157Hz beating), 0.5s, harsh
            errorSound = GenerateDissonantBuzz("ErrorSound", 150f, 157f, 0.5f);

            // 4. Security alarm: Alternating 800Hz/600Hz square wave, 1.0s, urgent siren
            alarmSound = GenerateAlternatingSquareWave("AlarmSound", 800f, 600f, 1.0f);

            // 5. Countdown tick: Sharp 1000Hz click, 0.05s
            countdownTick = GenerateSineWave("CountdownTick", 1000f, 0.05f, true);

            // 6. Completion fanfare: Major chord (C4+E4+G4+C5 simultaneously), 0.8s slow decay
            completionFanfare = GenerateChord("CompletionFanfare", new[] { 261.63f, 329.63f, 392.00f, 523.25f }, 0.8f);

            // 7. Path step correct: Short 660Hz blip, 0.15s
            pathCorrect = GenerateSineWave("PathCorrect", 660f, 0.15f, true);

            // 8. Path step error: Low 200Hz thud, 0.3s
            pathError = GenerateSineWave("PathError", 200f, 0.3f, true);
        }

        public void PlayColorTone(int colorIndex)
        {
            if (colorIndex >= 0 && colorIndex < colorTones.Length)
            {
                audioSource.PlayOneShot(colorTones[colorIndex]);
            }
        }

        public void PlayCorrectSound() => audioSource.PlayOneShot(correctSound);
        public void PlayErrorSound() => audioSource.PlayOneShot(errorSound);
        public void PlayAlarmSound() => audioSource.PlayOneShot(alarmSound);
        public void PlayLockdownAlarm() => audioSource.PlayOneShot(alarmSound);
        public void PlayCountdownTick() => audioSource.PlayOneShot(countdownTick);
        public void PlayCompletionFanfare() => audioSource.PlayOneShot(completionFanfare);
        public void PlayPathCorrect() => audioSource.PlayOneShot(pathCorrect);
        public void PlayPathError() => audioSource.PlayOneShot(pathError);

        private AudioClip GenerateSineWave(string name, float frequency, float duration, bool exponentialDecay)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = 1f;
                if (exponentialDecay)
                {
                    envelope = Mathf.Exp(-5f * time / duration);
                }

                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateArpeggio(string name, float[] frequencies, float duration)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];
            int noteCount = frequencies.Length;
            int samplesPerNote = sampleCount / noteCount;

            for (int i = 0; i < sampleCount; i++)
            {
                int noteIndex = Mathf.Min(i / samplesPerNote, noteCount - 1);
                float frequency = frequencies[noteIndex];
                float time = (i % samplesPerNote) / (float)SampleRate;
                
                // Slight decay per note for a pleasant chime
                float envelope = Mathf.Exp(-3f * time / (duration / noteCount));
                
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateDissonantBuzz(string name, float freq1, float freq2, float duration)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                // Combine square waves for harshness
                float wave1 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * freq1 * time));
                float wave2 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * freq2 * time));
                
                float envelope = 1f - (time / duration); // Linear fade
                samples[i] = (wave1 + wave2) * 0.5f * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateAlternatingSquareWave(string name, float freq1, float freq2, float duration)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float interval = 0.25f; // Alternate every 0.25s

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                bool isFreq1 = (time % (interval * 2)) < interval;
                float currentFreq = isFreq1 ? freq1 : freq2;

                samples[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * currentFreq * time)) * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateChord(string name, float[] frequencies, float duration)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float mix = 0f;

                foreach (float freq in frequencies)
                {
                    mix += Mathf.Sin(2 * Mathf.PI * freq * time);
                }

                mix /= frequencies.Length;
                
                // Slow decay
                float envelope = Mathf.Exp(-2f * time / duration);
                samples[i] = mix * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
