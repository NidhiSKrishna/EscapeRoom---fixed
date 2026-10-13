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
        public static EscapeRoomAudio Instance => instance;

        // ── Sound catalogue ───────────────────────────────────────────────────
        public enum SoundId
        {
            KeyPickup,          // Metallic chime — key acquired
            LockboxUnlock,      // Lock click / latch release
            LockboxOpen,        // Lid creak / hinge
            CodeNoteReveal,     // Soft discovery chime (2-tone rise)
            KeypadDigit,        // Short beep per digit
            KeypadWrong,        // Buzzer / error
            KeypadCorrect,      // Access-granted two-tone
            DoorUnlock,         // Heavy clunk
            DoorOpen,           // Slow creak sweep
            ObjectBump,         // Subtle thud
        }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("2D UI Source Pool")]
        [SerializeField] private int uiSourcePoolSize = 4;

        [Header("Volume Multiplier")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.72f;

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
            clips[(int)SoundId.LockboxOpen]      = CreateLockboxOpen();
            clips[(int)SoundId.CodeNoteReveal]   = CreateCodeNoteReveal();
            clips[(int)SoundId.KeypadDigit]      = CreateKeypadDigit();
            clips[(int)SoundId.KeypadWrong]      = CreateKeypadWrong();
            clips[(int)SoundId.KeypadCorrect]    = CreateKeypadCorrect();
            clips[(int)SoundId.DoorUnlock]       = CreateDoorUnlock();
            clips[(int)SoundId.DoorOpen]         = CreateDoorOpen();
            clips[(int)SoundId.ObjectBump]       = CreateObjectBump();
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Play a fully 2D (UI) sound.</summary>
        public static void Play(SoundId id, float volumeScale = 1f)
        {
            if (instance == null) return;
            var clip = instance.GetClip(id);
            if (clip == null) return;

            var src = instance.NextUISource();
            src.PlayOneShot(clip, instance.masterVolume * volumeScale);
        }

        /// <summary>Play a spatialised 3D sound at a world position.</summary>
        public static void PlayAt(SoundId id, Vector3 worldPos, float volumeScale = 1f)
        {
            if (instance == null) return;
            var clip = instance.GetClip(id);
            if (clip == null) return;

            // AudioSource.PlayClipAtPoint is the simplest 3D one-shot mechanism
            AudioSource.PlayClipAtPoint(clip, worldPos, instance.masterVolume * volumeScale);
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

        // 1. Key Pickup — bright metallic chime (two descending tones)
        private static AudioClip CreateKeyPickup()
        {
            var t1 = SineDecay(1046.5f, 0.20f, 10f, 0.40f); // C6
            var t2 = SineDecay(783.99f, 0.22f, 10f, 0.30f); // G5, slightly offset
            // Stagger: t2 starts 30ms after t1
            int offset = Mathf.FloorToInt(SR * 0.03f);
            int total  = t1.Length + offset;
            float[] out_ = new float[total];
            for (int i = 0; i < t1.Length && i < total; i++) out_[i] += t1[i];
            for (int i = 0; i < t2.Length && (i + offset) < total; i++) out_[i + offset] += t2[i];
            return Clip("KeyPickup", out_);
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

        // 3. Lockbox Open — slow low creak then settle
        private static AudioClip CreateLockboxOpen()
        {
            float duration = 0.55f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            // Creak: frequency sweeps 180→90Hz with noise modulation
            float freqStart = 180f, freqEnd = 90f;
            for (int i = 0; i < n; i++)
            {
                float t  = (float)i / SR;
                float env = Mathf.Exp(-t * 3.5f) * 0.38f;
                float freq = Mathf.Lerp(freqStart, freqEnd, t / duration);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.12f;
                s[i] = env * (Mathf.Sin(2f * Mathf.PI * freq * t) + noise);
            }
            return Clip("LockboxOpen", s);
        }

        // 4. Code Note Reveal — soft rising two-tone chime
        private static AudioClip CreateCodeNoteReveal()
        {
            var t1 = SineDecay(523.25f, 0.28f, 7f, 0.32f); // C5
            // t2 starts 0.12 s later
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
            float duration = 0.25f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SR;
                float env = Mathf.Exp(-t * 22f) * 0.60f;
                float noise = (UnityEngine.Random.value * 2f - 1f);
                // Low thud + 130Hz resonance
                s[i] = env * (noise * 0.55f + Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.45f);
            }
            return Clip("DoorUnlock", s);
        }

        // 9. Door Open — slow creak sweeping 100→60Hz over 0.8s
        private static AudioClip CreateDoorOpen()
        {
            float duration = 0.80f;
            int n = Mathf.FloorToInt(SR * duration);
            float[] s = new float[n];
            float freqS = 100f, freqE = 60f;
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SR;
                float env = Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-t * 2.8f) * 0.42f;
                float freq = Mathf.Lerp(freqS, freqE, t / duration);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;
                s[i] = env * (Mathf.Sin(2f * Mathf.PI * freq * t) + noise);
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
    }
}
