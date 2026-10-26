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
            DoorOpen,           // Subtle, smooth door swing and soft latch release
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

                // 8. Door Unlock - subtle metallic bolt slide
        private static AudioClip CreateDoorUnlock()
        {
            float duration = 0.22f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            float lpf = 0f;
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SR;
                float env = Mathf.Exp(-t * 28f) * 0.35f;
                float white = UnityEngine.Random.value * 2f - 1f;
                lpf += 0.08f * (white - lpf);
                // Subtle mechanical click and slide
                float tone = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 320f * t) * 0.4f;
                s[i] = env * (tone + lpf * 0.30f);
            }
            return Clip("DoorUnlock", s);
        }

        // 9. Door Open - subtle, smooth door opening: gentle latch release, warm low hinge whisper and soft air glide
        private static AudioClip CreateDoorOpen()
        {
            float duration = 0.90f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            float lpf = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float sample = 0f;

                // Low-pass filtered noise for smooth, velvet-like air and friction (no harsh hiss)
                float white = UnityEngine.Random.value * 2f - 1f;
                lpf += 0.06f * (white - lpf); // ~420Hz one-pole LPF

                // Phase 1 (0.00 - 0.12s): Very soft latch disengagement click
                if (t < 0.12f)
                {
                    float latchEnv = Mathf.Exp(-t * 36f) * 0.28f;
                    float click = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.4f;
                    sample += latchEnv * (click + lpf * 0.35f);
                }

                // Phase 2 (0.06 - 0.78s): Subtle, warm wooden/brass hinge glide and soft air displacement
                if (t >= 0.06f && t < 0.78f)
                {
                    float tGlide = t - 0.06f;
                    float glideDur = 0.72f;
                    float prog = tGlide / glideDur;
                    float glideEnv = Mathf.Sin(prog * Mathf.PI);

                    // Low warm fundamental (125Hz -> 155Hz), very gentle and mellow
                    float freq = Mathf.Lerp(125f, 155f, prog);
                    float warmTone = Mathf.Sin(2f * Mathf.PI * freq * tGlide) * 0.14f
                                   + Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * tGlide) * 0.06f;

                    // Soft velvety air whoosh
                    float airWhoosh = lpf * 0.10f;

                    sample += glideEnv * (warmTone + airWhoosh);
                }

                // Phase 3 (0.70 - 0.90s): Gentle, damped soft stop settle
                if (t >= 0.70f)
                {
                    float tStop = t - 0.70f;
                    float stopEnv = Mathf.Exp(-tStop * 22f) * 0.16f;
                    float stopThud = Mathf.Sin(2f * Mathf.PI * 85f * tStop);
                    sample += stopEnv * stopThud;
                }

                // Edge smoothing at start and end to guarantee zero clicks
                float edge = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((duration - t) / 0.015f);
                s[i] = Mathf.Clamp(sample * edge, -1f, 1f);
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
