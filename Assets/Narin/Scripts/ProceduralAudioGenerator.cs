using UnityEngine;

namespace NarinVillage
{
    /// <summary>
    /// Generates runtime AudioClips procedurally so all 5 houses have immediate,
    /// zero-dependency sound effects (doorbell, radio tracks, sizzling bacon, toilet flush,
    /// car horn, engine rev, tools, fluorescent hum, and monster jumpscare).
    /// </summary>
    public static class ProceduralAudioGenerator
    {
        private const int SampleRate = 44100;

        public static AudioClip CreateChime(float freq1 = 660f, float freq2 = 880f, float duration = 0.6f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 5f);
                float freq = t < duration * 0.4f ? freq1 : freq2;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            return ToClip("Chime", data);
        }

        public static AudioClip CreateSlideSound(float duration = 0.7f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 1337;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float noise = NextRandom(ref seed) * 0.15f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.2f;
                data[i] = (noise + rumble) * env;
            }
            return ToClip("SlideSound", data);
        }

        public static AudioClip CreateRadioLoop(int trackIndex)
        {
            float duration = 4.0f;
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            float[] notes = trackIndex == 0
                ? new float[] { 261.63f, 329.63f, 392.00f, 523.25f, 392.00f, 329.63f, 293.66f, 349.23f }
                : new float[] { 220.00f, 261.63f, 329.63f, 440.00f, 392.00f, 329.63f, 261.63f, 196.00f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int noteIdx = Mathf.FloorToInt(t * 2f) % notes.Length;
                float noteTime = (t * 2f) % 1f;
                float env = Mathf.Exp(-noteTime * 3.5f);
                float freq = notes[noteIdx];
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.25f;
            }
            return ToClip("RadioLoop_" + trackIndex, data);
        }

        public static AudioClip CreateSizzleSound(float duration = 2.5f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 4242;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float crackle = NextRandom(ref seed);
                if (Mathf.Abs(crackle) > 0.85f) crackle *= 1.8f;
                else crackle *= 0.3f;
                float env = Mathf.Clamp01(1f - (t / duration) * 0.3f);
                data[i] = Mathf.Clamp(crackle * env * 0.35f, -0.9f, 0.9f);
            }
            return ToClip("BaconSizzle", data);
        }

        public static AudioClip CreateFlushSound(float duration = 2.0f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 9999;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float swirl = Mathf.Sin(2f * Mathf.PI * (180f - t * 60f) * t) * 0.2f;
                float water = NextRandom(ref seed) * 0.35f;
                data[i] = (swirl + water) * env;
            }
            return ToClip("ToiletFlush", data);
        }

        public static AudioClip CreateRatchetSound(float duration = 0.6f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float clickPhase = (t * 16f) % 1f;
                float env = Mathf.Exp(-clickPhase * 18f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * env * 0.45f;
            }
            return ToClip("ToolRatchet", data);
        }

        public static AudioClip CreateCarHorn(float duration = 0.65f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // Classic dual-tone car horn (F4 + A4)
                float w1 = Mathf.Sin(2f * Mathf.PI * 349.23f * t);
                float w2 = Mathf.Sin(2f * Mathf.PI * 440.00f * t);
                float harmonic = 0.3f * Mathf.Sin(2f * Mathf.PI * 698.46f * t);
                data[i] = Mathf.Clamp((w1 + w2 + harmonic) * 0.35f, -0.8f, 0.8f);
            }
            return ToClip("CarHorn", data);
        }

        public static AudioClip CreateEngineRev(float duration = 1.8f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 777;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float rpmCurve = 55f + Mathf.Sin(Mathf.PI * (t / duration)) * 95f;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float pulse = Mathf.Sin(2f * Mathf.PI * rpmCurve * t) + 0.5f * Mathf.Sin(4f * Mathf.PI * rpmCurve * t);
                float exhaust = NextRandom(ref seed) * 0.15f;
                data[i] = (pulse * 0.3f + exhaust) * env;
            }
            return ToClip("EngineRev", data);
        }

        public static AudioClip CreateFluorescentBuzz(float duration = 3.0f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float hum60 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.15f;
                float hum120 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.12f;
                float hum240 = Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.06f;
                data[i] = hum60 + hum120 + hum240;
            }
            return ToClip("FluorescentBuzz", data);
        }

        public static AudioClip CreateJumpscareScream(float duration = 1.1f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 666;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 2.2f);
                float screech = Mathf.Sin(2f * Mathf.PI * (820f + Mathf.Sin(t * 45f) * 260f) * t);
                float sub = Mathf.Sin(2f * Mathf.PI * 65f * t);
                float noise = NextRandom(ref seed) * 0.5f;
                data[i] = Mathf.Clamp((screech * 0.45f + sub * 0.4f + noise) * env, -0.95f, 0.95f);
            }
            return ToClip("JumpscareScream", data);
        }

        public static AudioClip CreateCashRegisterSound(float duration = 0.85f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 4242;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // Mechanical latch / drawer slide click in the first 0.10s
                float clickEnv = t < 0.10f ? Mathf.Sin(Mathf.PI * (t / 0.10f)) : 0f;
                float clickNoise = NextRandom(ref seed) * 0.25f;
                float clickRumble = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f;
                float click = (clickNoise + clickRumble) * clickEnv;

                // Bright mechanical bell chime (Ka-Ching!) starting slightly after latch
                float bellT = Mathf.Max(0f, t - 0.08f);
                float bellEnv = Mathf.Exp(-bellT * 4.5f);
                float bellTone = (Mathf.Sin(2f * Mathf.PI * 1760f * bellT) * 0.4f +
                                  Mathf.Sin(2f * Mathf.PI * 2637f * bellT) * 0.25f +
                                  Mathf.Sin(2f * Mathf.PI * 3520f * bellT) * 0.15f) * bellEnv;

                data[i] = Mathf.Clamp(click + bellTone, -1f, 1f);
            }
            return ToClip("CashRegisterKaChing", data);
        }

        public static AudioClip CreateLockedRattleSound(float duration = 0.45f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 8888;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // Double jiggle / rattle clatter (two metal impacts at 0.0s and 0.12s)
                float rattle1 = Mathf.Max(0f, t);
                float rattle2 = Mathf.Max(0f, t - 0.12f);
                float env1 = Mathf.Exp(-rattle1 * 28f);
                float env2 = Mathf.Exp(-rattle2 * 32f);
                float tone1 = (Mathf.Sin(2f * Mathf.PI * 840f * rattle1) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1420f * rattle1)) * env1;
                float tone2 = (Mathf.Sin(2f * Mathf.PI * 720f * rattle2) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1280f * rattle2)) * env2;
                float noise = NextRandom(ref seed) * 0.18f * (env1 + env2);
                data[i] = Mathf.Clamp(tone1 + tone2 + noise, -0.9f, 0.9f);
            }
            return ToClip("LockedDoorRattle", data);
        }

        public static AudioClip CreateLockLatchSound(float duration = 0.5f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 7777;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // Heavy metallic bolt slide (first 0.18s) followed by sharp bolt drop / latch click
                float slideEnv = t < 0.18f ? Mathf.Sin(Mathf.PI * (t / 0.18f)) : 0f;
                float slideNoise = NextRandom(ref seed) * 0.2f * slideEnv;
                float latchT = Mathf.Max(0f, t - 0.16f);
                float latchEnv = Mathf.Exp(-latchT * 24f);
                float latchClick = (Mathf.Sin(2f * Mathf.PI * 480f * latchT) * 0.7f +
                                    Mathf.Sin(2f * Mathf.PI * 960f * latchT) * 0.4f +
                                    NextRandom(ref seed) * 0.3f) * latchEnv;
                data[i] = Mathf.Clamp(slideNoise + latchClick, -0.95f, 0.95f);
            }
            return ToClip("LockLatchSound", data);
        }

        public static AudioClip CreateFootstepThud(float duration = 0.18f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 31415;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 26f);
                float thud = Mathf.Sin(2f * Mathf.PI * (75f - t * 40f) * t) * 0.7f;
                float scuff = NextRandom(ref seed) * 0.25f * Mathf.Exp(-t * 38f);
                data[i] = Mathf.Clamp((thud + scuff) * env, -0.95f, 0.95f);
            }
            return ToClip("FootstepThud", data);
        }

        public static AudioClip CreatePlayerFoundSound(PlayerFoundSoundStyle style = PlayerFoundSoundStyle.DramaticHorrorStinger)
        {
            switch (style)
            {
                case PlayerFoundSoundStyle.DissonantScreechChord:
                    return CreateDissonantScreechChord();
                case PlayerFoundSoundStyle.StealthAlertSting:
                    return CreateStealthAlertSting();
                case PlayerFoundSoundStyle.DeepOminousDrone:
                    return CreateDeepOminousDrone();
                case PlayerFoundSoundStyle.DramaticHorrorStinger:
                default:
                    return CreateDramaticHorrorStinger();
            }
        }

        public static AudioClip CreateDramaticHorrorStinger(float duration = 1.35f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 99991;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.012f);
                float decay = Mathf.Exp(-t * 2.6f);
                float env = attack * decay;

                // Sub bass punch dropping from 85Hz down to 42Hz
                float subFreq = Mathf.Lerp(85f, 42f, t / duration);
                float sub = Mathf.Sin(2f * Mathf.PI * subFreq * t) * 0.55f * Mathf.Exp(-t * 3.8f);

                // Dissonant horror cluster chord (D5, Eb5, Ab5, D6)
                float f1 = 587.33f;
                float f2 = 622.25f;
                float f3 = 830.61f;
                float f4 = 1174.66f;
                float chord = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f +
                               Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f +
                               Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.30f +
                               Mathf.Sin(2f * Mathf.PI * f4 * t) * 0.20f);

                // Tremolo modulation for spine-chilling shiver
                float trem = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 28f * t);

                // Sudden noise transient bite at the initial sting
                float bite = NextRandom(ref seed) * 0.30f * Mathf.Exp(-t * 22f);

                data[i] = Mathf.Clamp((sub + chord * trem + bite) * env, -0.95f, 0.95f);
            }
            return ToClip("PlayerFound_DramaticStinger", data);
        }

        public static AudioClip CreateDissonantScreechChord(float duration = 1.15f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 88812;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.008f);
                float decay = Mathf.Exp(-t * 3.2f);
                float env = attack * decay;

                // Piercing clashing frequencies with screech modulation
                float screechMod = Mathf.Sin(2f * Mathf.PI * 45f * t) * 80f;
                float s1 = Mathf.Sin(2f * Mathf.PI * (1046.5f + screechMod) * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * (1108.7f - screechMod * 0.7f) * t);
                float s3 = Mathf.Sin(2f * Mathf.PI * 1568.0f * t);
                float s4 = Mathf.Sin(2f * Mathf.PI * 2217.5f * t);

                float screech = (s1 * 0.35f + s2 * 0.35f + s3 * 0.20f + s4 * 0.15f);
                float noise = NextRandom(ref seed) * 0.40f * Mathf.Exp(-t * 8f);

                data[i] = Mathf.Clamp((screech + noise) * env, -0.95f, 0.95f);
            }
            return ToClip("PlayerFound_ScreechChord", data);
        }

        public static AudioClip CreateStealthAlertSting(float duration = 0.75f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float val = 0f;
                if (t < 0.07f)
                {
                    // Low alert punch (330Hz)
                    float att = Mathf.Clamp01(t / 0.005f);
                    val = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.8f * att;
                }
                else
                {
                    // High alert sting (660Hz + 990Hz + 1320Hz saw overtone)
                    float tRel = t - 0.07f;
                    float att = Mathf.Clamp01(tRel / 0.005f);
                    float dec = Mathf.Exp(-tRel * 6.5f);
                    float p1 = Mathf.Sin(2f * Mathf.PI * 660f * t);
                    float p2 = Mathf.Sin(2f * Mathf.PI * 990f * t) * 0.5f;
                    float p3 = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f;
                    val = (p1 + p2 + p3) * att * dec;
                }
                data[i] = Mathf.Clamp(val, -0.95f, 0.95f);
            }
            return ToClip("PlayerFound_StealthAlert", data);
        }

        public static AudioClip CreateDeepOminousDrone(float duration = 1.85f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 77731;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.06f);
                float decay = Mathf.Exp(-t * 1.5f);
                float env = attack * decay;

                // Heavy sub bass swell + distorted brass saw
                float sub = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.6f;
                float saw = (2f * ((90f * t) - Mathf.Floor(90f * t + 0.5f))) * 0.35f;
                float saw2 = Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.25f;
                float rumble = NextRandom(ref seed) * 0.15f * Mathf.Exp(-t * 2f);

                data[i] = Mathf.Clamp((sub + saw + saw2 + rumble) * env, -0.95f, 0.95f);
            }
            return ToClip("PlayerFound_OminousDrone", data);
        }

        public static AudioClip CreateMonsterAwakenRoar(float duration = 2.4f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint seed = 91823;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.15f);
                float decay = Mathf.Exp(-t * 1.1f);
                float env = attack * decay;

                // Guttural sub-bass sweep down (110Hz down to 55Hz)
                float freq = Mathf.Lerp(110f, 55f, t / duration);
                float phase = 2f * Mathf.PI * freq * t;
                float growl = Mathf.Sin(phase) * 0.55f;

                // Resonant rasp saw harmonic
                float raspFreq = freq * 2.85f;
                float saw = (2f * ((raspFreq * t) - Mathf.Floor(raspFreq * t + 0.5f))) * 0.35f;

                // Screeching undertone
                float screech = Mathf.Sin(2f * Mathf.PI * 440f * t) * Mathf.Exp(-t * 3.5f) * 0.25f;

                // Low rumble noise
                float rumble = NextRandom(ref seed) * 0.25f * Mathf.Exp(-t * 2.0f);

                data[i] = Mathf.Clamp((growl + saw + screech + rumble) * env, -0.95f, 0.95f);
            }
            return ToClip("Monster_Awaken_Roar", data);
        }

        private static float NextRandom(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return ((state & 0xFFFF) / 32768f) - 1f;
        }

        private static AudioClip ToClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
