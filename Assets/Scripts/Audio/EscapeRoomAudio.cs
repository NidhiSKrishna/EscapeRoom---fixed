using System.Collections;
using UnityEngine;

namespace EscapeRoom.Audio
{
    /// <summary>
    /// Central audio manager for the Escape Room.
    /// All sounds are procedurally generated — no external audio files required.
    /// Uses a pool of 2D AudioSources for UI sounds and plays one-shot 3D sounds at a world position.
    ///
    /// Public static API:
    ///   EscapeRoomAudio.Play(SoundId)                 — 2D UI sound
    ///   EscapeRoomAudio.PlayAt(SoundId, position)     — 3D spatial sound at world position
    /// </summary>
    [DisallowMultipleComponent]
    public class EscapeRoomAudio : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static EscapeRoomAudio instance;
        public static EscapeRoomAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindAnyObjectByType<EscapeRoomAudio>();
                    if (instance == null)
                    {
                        var go = new GameObject("EscapeRoomAudio");
                        instance = go.AddComponent<EscapeRoomAudio>();
                    }
                }
                return instance;
            }
        }

        // ── Sound catalogue ───────────────────────────────────────────────────
        public enum SoundId
        {
            KeyPickup,          // Metallic chime & brass key jingle — key acquired
            LockboxUnlock,      // Lock click / latch release
            LockboxOpen,        // Mechanical chest opening & lid hinge squeak
            CodeNoteReveal,     // Soft discovery chime (2-tone rise)
            KeypadDigit,        // Short beep per digit
            KeypadWrong,        // Buzzer / error
            KeypadCorrect,      // Access-granted two-tone
            DoorUnlock,         // Heavy bolt clunk
            DoorOpen,           // Heavy door swing, creaking hinge, and thud
            ObjectBump,         // Subtle thud
            LevelExit,          // Triumphant level clear victory fanfare & transition swell
        }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("2D UI Source Pool")]
        [SerializeField] private int uiSourcePoolSize = 6;

        [Header("Volume Multiplier")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.85f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private AudioSource[] uiPool;
        private int uiPoolIndex = 0;

        private AudioClip[] clips;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            BuildPool();
            BuildClips();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ── Pool ──────────────────────────────────────────────────────────────
        private void BuildPool()
        {
            uiPool = new AudioSource[uiSourcePoolSize];
            for (int i = 0; i < uiSourcePoolSize; i++)
            {
                var go = new GameObject($"UIAudioSource_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake  = false;
                src.loop         = false;
                src.spatialBlend = 0f; // fully 2D
                uiPool[i] = src;
            }
        }

        // ── Clip factory ──────────────────────────────────────────────────────
        private void BuildClips()
        {
            int count = System.Enum.GetValues(typeof(SoundId)).Length;
            clips = new AudioClip[count];

            clips[(int)SoundId.KeyPickup]       = CreateKeyPickup();
            clips[(int)SoundId.LockboxUnlock]   = CreateLockboxUnlock();
            clips[(int)SoundId.LockboxOpen]     = CreateLockboxOpen();
            clips[(int)SoundId.CodeNoteReveal]  = CreateCodeNoteReveal();
            clips[(int)SoundId.KeypadDigit]     = CreateKeypadDigit();
            clips[(int)SoundId.KeypadWrong]     = CreateKeypadWrong();
            clips[(int)SoundId.KeypadCorrect]   = CreateKeypadCorrect();
            clips[(int)SoundId.DoorUnlock]      = CreateDoorUnlock();
            clips[(int)SoundId.DoorOpen]        = CreateDoorOpen();
            clips[(int)SoundId.ObjectBump]      = CreateObjectBump();
            clips[(int)SoundId.LevelExit]       = CreateLevelExit();
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Play a fully 2D (UI) sound.</summary>
        public static void Play(SoundId id, float volumeScale = 1f)
        {
            var audioMgr = Instance;
            if (audioMgr == null) return;
            var clip = audioMgr.GetClip(id);
            if (clip == null) return;

            var src = audioMgr.NextUISource();
            src.PlayOneShot(clip, audioMgr.masterVolume * volumeScale);
        }

        /// <summary>Play a spatialised 3D sound at a world position, reinforced by 2D audio so it is never lost.</summary>
        public static void PlayAt(SoundId id, Vector3 worldPos, float volumeScale = 1f)
        {
            var audioMgr = Instance;
            if (audioMgr == null) return;
            var clip = audioMgr.GetClip(id);
            if (clip == null) return;

            // AudioSource.PlayClipAtPoint plays in 3D space
            AudioSource.PlayClipAtPoint(clip, worldPos, audioMgr.masterVolume * volumeScale);

            // Reinforce with 2D audio at modest volume so distance never makes key game actions silent
            var src = audioMgr.NextUISource();
            src.PlayOneShot(clip, audioMgr.masterVolume * volumeScale * 0.75f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private AudioClip GetClip(SoundId id) => clips[(int)id];

        private AudioSource NextUISource()
        {
            var src = uiPool[uiPoolIndex];
            uiPoolIndex = (uiPoolIndex + 1) % uiPool.Length;
            return src;
        }

        // ── Procedural clip generators ────────────────────────────────────────
        private const int SR = 44100; // sample rate

        // Utility: pure sine with exponential decay
        private static float[] SineDecay(float freq, float duration, float decayRate, float amp = 0.5f)
        {
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                s[i] = amp * Mathf.Exp(-t * decayRate) * Mathf.Sin(2f * Mathf.PI * freq * t);
            }
            return s;
        }

        // Mix two sample arrays (same length) — result clamped
        private static float[] Mix(float[] a, float[] b)
        {
            int n = Mathf.Min(a.Length, b.Length);
            float[] out_ = new float[n];
            for (int i = 0; i < n; i++) out_[i] = Mathf.Clamp(a[i] + b[i], -1f, 1f);
            return out_;
        }

        // Append two arrays
        private static float[] Append(float[] a, float[] b)
        {
            float[] out_ = new float[a.Length + b.Length];
            a.CopyTo(out_, 0);
            b.CopyTo(out_, a.Length);
            return out_;
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var c = AudioClip.Create(name, samples.Length, 1, SR, false);
            c.SetData(samples, 0);
            return c;
        }

        // 1. Key Pickup — bright crystalline chime arpeggio + metallic brass key jingle
        private static AudioClip CreateKeyPickup()
        {
            float duration = 0.65f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];

            // 4-note ascending bell arpeggio: C6 (1046.5Hz), E6 (1318.5Hz), G6 (1567.98Hz), C7 (2093Hz)
            float[] freqs = { 1046.5f, 1318.51f, 1567.98f, 2093.00f };
            float[] startTimes = { 0.00f, 0.07f, 0.14f, 0.22f };

            for (int note = 0; note < freqs.Length; note++)
            {
                float f = freqs[note];
                int startIdx = Mathf.FloorToInt(SR * startTimes[note]);
                for (int i = startIdx; i < n; i++)
                {
                    float t = (float)(i - startIdx) / SR;
                    float env = Mathf.Exp(-t * 9.0f) * 0.35f;
                    float wave = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
                    s[i] += env * wave;
                }
            }

            // Subtle initial metallic brass clink transient
            int clickLen = Mathf.Min(n, Mathf.FloorToInt(SR * 0.035f));
            for (int i = 0; i < clickLen; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 120f) * 0.30f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.20f;
                s[i] += env * (Mathf.Sin(2f * Mathf.PI * 2800f * t) + noise);
            }

            for (int i = 0; i < n; i++) s[i] = Mathf.Clamp(s[i], -1f, 1f);
            return Clip("KeyPickup", s);
        }

        // 2. Lockbox Unlock — click then metallic ring
        private static AudioClip CreateLockboxUnlock()
        {
            // Short click: narrow pulse
            int clickLen = Mathf.FloorToInt(SR * 0.008f);
            float[] click = new float[clickLen];
            for (int i = 0; i < clickLen; i++) click[i] = Mathf.Exp(-i * 0.003f) * 0.55f * (i % 2 == 0 ? 1f : -1f);

            // Ring: metallic 1200Hz
            var ring = SineDecay(1200f, 0.18f, 16f, 0.30f);

            return Clip("LockboxUnlock", Append(click, ring));
        }

        // 3. Lockbox Open — mechanical latch pop + metallic lid hinge squeak + settle
        private static AudioClip CreateLockboxOpen()
        {
            float duration = 0.90f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float sample = 0f;

                // Phase 1 (0.00 - 0.15s): Latch release click & mechanical snap
                if (t < 0.15f)
                {
                    float clickEnv = Mathf.Exp(-t * 45f) * 0.65f;
                    float clickTone = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.5f;
                    float noise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                    sample += clickEnv * (clickTone + noise);
                }

                // Phase 2 (0.10 - 0.75s): Metallic lid hinge squeak & friction
                if (t >= 0.10f && t < 0.75f)
                {
                    float tHinge = t - 0.10f;
                    float hingeDur = 0.65f;
                    float hingeProg = tHinge / hingeDur;
                    float hingeEnv = Mathf.Sin(hingeProg * Mathf.PI) * 0.50f;

                    // Fundamental frequency sweeps upward with subtle vibration
                    float freq = Mathf.Lerp(550f, 950f, hingeProg) + Mathf.Sin(2f * Mathf.PI * 22f * tHinge) * 35f;
                    float tone1 = Mathf.Sin(2f * Mathf.PI * freq * tHinge);
                    float tone2 = Mathf.Sin(2f * Mathf.PI * (freq * 1.85f) * tHinge) * 0.35f;
                    float friction = (UnityEngine.Random.value * 2f - 1f) * 0.12f;

                    sample += hingeEnv * (tone1 + tone2 + friction);
                }

                // Phase 3 (0.70 - 0.90s): Soft lid settle tap against hinge stop
                if (t >= 0.70f)
                {
                    float tSettle = t - 0.70f;
                    float settleEnv = Mathf.Exp(-tSettle * 28f) * 0.40f;
                    float settleTone = Mathf.Sin(2f * Mathf.PI * 620f * tSettle) * 0.5f + Mathf.Sin(2f * Mathf.PI * 180f * tSettle) * 0.4f;
                    sample += settleEnv * settleTone;
                }

                s[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            return Clip("LockboxOpen", s);
        }

        // 4. Code Note Reveal — soft rising two-tone chime
        private static AudioClip CreateCodeNoteReveal()
        {
            var t1 = SineDecay(523.25f, 0.28f, 7f, 0.32f); // C5
            int gap = Mathf.FloorToInt(SR * 0.12f);
            var t2 = SineDecay(783.99f, 0.30f, 7f, 0.30f); // G5
            int total = t1.Length + gap + t2.Length;
            float[] out_ = new float[total];
            for (int i = 0; i < t1.Length; i++) out_[i] += t1[i];
            int start2 = t1.Length - gap;
            if (start2 < 0) start2 = 0;
            for (int i = 0; i < t2.Length && (i + start2) < total; i++) out_[i + start2] += t2[i];
            return Clip("CodeNoteReveal", out_);
        }

        // 5. Keypad Digit — short 800Hz beep
        private static AudioClip CreateKeypadDigit()
        {
            return Clip("KeypadDigit", SineDecay(800f, 0.07f, 30f, 0.35f));
        }

        // 6. Keypad Wrong — two descending buzzes
        private static AudioClip CreateKeypadWrong()
        {
            var b1 = SineDecay(330f, 0.12f, 18f, 0.45f);
            int gap = Mathf.FloorToInt(SR * 0.06f);
            var b2 = SineDecay(260f, 0.14f, 16f, 0.40f);
            float[] silence = new float[gap];
            return Clip("KeypadWrong", Append(Append(b1, silence), b2));
        }

        // 7. Keypad Correct — ascending two-tone confirmation
        private static AudioClip CreateKeypadCorrect()
        {
            var t1 = SineDecay(659.25f, 0.18f, 8f, 0.38f); // E5
            int gap = Mathf.FloorToInt(SR * 0.10f);
            var t2 = SineDecay(987.77f, 0.22f, 7f, 0.35f); // B5
            float[] silence = new float[gap];
            return Clip("KeypadCorrect", Append(Append(t1, silence), t2));
        }

        // 8. Door Unlock — heavy mechanical clunk
        private static AudioClip CreateDoorUnlock()
        {
            float duration = 0.28f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SR;
                float env = Mathf.Exp(-t * 18f) * 0.70f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.40f;
                // Heavy bolt click (420Hz) + low mechanical thud (110Hz)
                s[i] = env * (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.4f + noise);
            }
            return Clip("DoorUnlock", s);
        }

        // 9. Door Open — full 1.4s door swing: bolt release, rich resonant hinge creak, air whoosh & stop thud
        private static AudioClip CreateDoorOpen()
        {
            float duration = 1.40f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float sample = 0f;

                // Phase 1 (0.00 - 0.22s): Initial bolt unlatch and mechanical clack
                if (t < 0.22f)
                {
                    float env = Mathf.Exp(-t * 22f) * 0.60f;
                    float clunk = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 460f * t) * 0.4f;
                    float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
                    sample += env * (clunk + noise);
                }

                // Phase 2 (0.18 - 1.15s): Slow heavy door creak (rich harmonics & friction)
                if (t >= 0.18f && t < 1.15f)
                {
                    float tCreak = t - 0.18f;
                    float creakDur = 0.97f;
                    float creakProg = tCreak / creakDur;
                    float creakEnv = Mathf.Sin(creakProg * Mathf.PI) * 0.55f;

                    // Wood/iron friction harmonics with rhythmic creak modulation
                    float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 7.5f * tCreak);
                    float f1 = (Mathf.Lerp(280f, 410f, creakProg) + Mathf.Sin(2f * Mathf.PI * 14f * tCreak) * 20f) * wobble;
                    float f2 = f1 * 1.55f;
                    float f3 = f1 * 2.1f;

                    float tone = Mathf.Sin(2f * Mathf.PI * f1 * tCreak) * 0.40f
                               + Mathf.Sin(2f * Mathf.PI * f2 * tCreak) * 0.28f
                               + Mathf.Sin(2f * Mathf.PI * f3 * tCreak) * 0.18f;
                    float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;

                    sample += creakEnv * (tone + noise);
                }

                // Phase 3 (1.10 - 1.40s): Door reaches fully open stop with a satisfying settle thump
                if (t >= 1.10f)
                {
                    float tStop = t - 1.10f;
                    float stopEnv = Mathf.Exp(-tStop * 18f) * 0.55f;
                    float stopThump = Mathf.Sin(2f * Mathf.PI * 90f * tStop) * 0.7f + Mathf.Sin(2f * Mathf.PI * 220f * tStop) * 0.3f;
                    sample += stopEnv * stopThump;
                }

                s[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            return Clip("DoorOpen", s);
        }

        // 10. Object Bump — muted thud
        private static AudioClip CreateObjectBump()
        {
            float duration = 0.14f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SR;
                float env = Mathf.Exp(-t * 35f) * 0.45f;
                float noise = (UnityEngine.Random.value * 2f - 1f);
                s[i] = env * (noise * 0.6f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.4f);
            }
            return Clip("ObjectBump", s);
        }

        // 11. Level Exit — triumphant victory fanfare & transition swell
        private static AudioClip CreateLevelExit()
        {
            float duration = 1.80f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];

            // Triumphant 4-phase chord progression:
            // Phase 1: C5 (523.25Hz) + G5 (783.99Hz)
            // Phase 2: E5 (659.25Hz) + C6 (1046.50Hz)
            // Phase 3: G5 (783.99Hz) + E6 (1318.51Hz)
            // Phase 4: Full chord: C6 (1046.5Hz) + G6 (1567.98Hz) + C7 (2093.0Hz)
            float[][] chordFreqs = new float[][]
            {
                new float[] { 523.25f, 783.99f },
                new float[] { 659.25f, 1046.50f },
                new float[] { 783.99f, 1318.51f },
                new float[] { 1046.50f, 1567.98f, 2093.00f }
            };
            float[] startTimes = { 0.00f, 0.16f, 0.32f, 0.50f };

            for (int c = 0; c < chordFreqs.Length; c++)
            {
                float[] freqs = chordFreqs[c];
                int startIdx = Mathf.FloorToInt(SR * startTimes[c]);
                float chordDuration = duration - startTimes[c];

                for (int i = startIdx; i < n; i++)
                {
                    float t = (float)(i - startIdx) / SR;
                    float env = Mathf.Exp(-t * (c == chordFreqs.Length - 1 ? 2.2f : 4.5f)) * 0.32f;

                    for (int f = 0; f < freqs.Length; f++)
                    {
                        float tone = Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
                        s[i] += env * tone / freqs.Length;
                    }
                }
            }

            // Low warm resonant swell and air transition whoosh across whole duration
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float swellProg = t / duration;
                float swellEnv = Mathf.Sin(swellProg * Mathf.PI) * 0.35f;
                float sweepFreq = Mathf.Lerp(160f, 480f, swellProg);
                float swellTone = Mathf.Sin(2f * Mathf.PI * sweepFreq * t);
                float airNoise = (UnityEngine.Random.value * 2f - 1f) * 0.10f;
                s[i] += swellEnv * (swellTone + airNoise);
                s[i] = Mathf.Clamp(s[i], -1f, 1f);
            }

            return Clip("LevelExit", s);
        }
    }
}
