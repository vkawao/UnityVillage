using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Sounds for actions 7-10, synthesized at runtime (no audio files needed).
    /// Each clip is built once on first use and cached.
    /// </summary>
    public static partial class SynthAudio
    {
        private const int Rate = 22050;

        private static AudioClip s_Buzz, s_Hum, s_Buzzer, s_CashRegister, s_Click, s_Ding, s_DoorOpen, s_DoorClose;
        private static AudioClip s_Unplugged, s_Clean, s_Lead;

        /// <summary>Looping harsh mains buzz with crackle (faulty amp).</summary>
        public static AudioClip AmpBuzz => s_Buzz != null ? s_Buzz : s_Buzz = BuildBuzz();
        /// <summary>Looping soft low hum (working amp).</summary>
        public static AudioClip AmpHum => s_Hum != null ? s_Hum : s_Hum = BuildHum();
        /// <summary>Short "wrong answer" buzzer.</summary>
        public static AudioClip Buzzer => s_Buzzer != null ? s_Buzzer : s_Buzzer = BuildBuzzer();
        /// <summary>Drawer thunk + "ching!" bell.</summary>
        public static AudioClip CashRegister => s_CashRegister != null ? s_CashRegister : s_CashRegister = BuildCashRegister();
        /// <summary>Footswitch click.</summary>
        public static AudioClip Click => s_Click != null ? s_Click : s_Click = BuildClick();
        /// <summary>Latch click, wooden creak and a little shop-door bell jingle.</summary>
        public static AudioClip DoorOpen => s_DoorOpen != null ? s_DoorOpen : s_DoorOpen = BuildDoorOpen();
        /// <summary>Short creak, soft thud and latch click.</summary>
        public static AudioClip DoorClose => s_DoorClose != null ? s_DoorClose : s_DoorClose = BuildDoorClose();
        /// <summary>Soft two-note bell: "a new task is over here".</summary>
        public static AudioClip Ding => s_Ding != null ? s_Ding : s_Ding = BuildDing();
        /// <summary>Thin, quiet strum of an unplugged electric guitar.</summary>
        public static AudioClip UnpluggedStrum => s_Unplugged != null ? s_Unplugged : s_Unplugged = BuildUnplugged();
        /// <summary>Clean amped E major strum.</summary>
        public static AudioClip CleanStrum => s_Clean != null ? s_Clean : s_Clean = BuildClean();
        /// <summary>Distorted lead lick with a bend.</summary>
        public static AudioClip LeadLick => s_Lead != null ? s_Lead : s_Lead = BuildLead();

        // Open E major: E2 B2 E3 G#3 B3 E4
        private static readonly float[] s_EMajor = { 82.41f, 123.47f, 164.81f, 207.65f, 246.94f, 329.63f };

        // ------------------------------------------------------------------
        // Amp noises
        // ------------------------------------------------------------------
        private static AudioClip BuildBuzz()
        {
            int n = Rate; // 1 s loop: whole number of 60 Hz cycles, so it loops seamlessly
            float[] buf = new float[n];
            var rng = new System.Random(7);
            float crackle = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float saw = 2f * ((t * 120f) % 1f) - 1f;
                float v = Mathf.Clamp(saw * 1.8f, -1f, 1f) * 0.5f + 0.3f * Mathf.Sin(2f * Mathf.PI * 60f * t);
                if (rng.NextDouble() < 0.0015) crackle = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.2f;
                crackle *= 0.93f;
                v += crackle + (float)(rng.NextDouble() - 0.5) * 0.08f;
                buf[i] = v;
            }
            return Make("AmpBuzz", buf, 0.6f);
        }

        private static AudioClip BuildHum()
        {
            int n = Rate;
            float[] buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                buf[i] = 0.7f * Mathf.Sin(2f * Mathf.PI * 60f * t)
                       + 0.25f * Mathf.Sin(2f * Mathf.PI * 120f * t)
                       + 0.06f * Mathf.Sin(2f * Mathf.PI * 180f * t);
            }
            return Make("AmpHum", buf, 0.45f);
        }

        // ------------------------------------------------------------------
        // UI / shop sounds
        // ------------------------------------------------------------------
        private static AudioClip BuildBuzzer()
        {
            int n = (int)(Rate * 0.55f);
            float[] buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.55f - t) / 0.06f);
                float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t));
                float b = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 116.5f * t));
                buf[i] = (a + b) * 0.5f * env;
            }
            return Make("Buzzer", buf, 0.6f);
        }

        private static AudioClip BuildCashRegister()
        {
            int n = (int)(Rate * 1.6f);
            float[] buf = new float[n];
            var rng = new System.Random(3);

            // Mechanical rattle (0 - 0.15 s)
            for (int i = 0; i < (int)(Rate * 0.15f); i++)
            {
                float t = (float)i / Rate;
                float tick = Mathf.Exp(-((t * 40f) % 1f) * 8f);
                buf[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * tick * 0.5f;
            }
            // Drawer thunk
            int thunk = (int)(Rate * 0.12f);
            for (int i = thunk; i < n; i++)
            {
                float t = (float)(i - thunk) / Rate;
                buf[i] += Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 18f) * 0.9f;
            }
            // "Ching!" bell
            int ching = (int)(Rate * 0.16f);
            float[] freqs = { 2093f, 2637f, 4186f, 5274f };
            float[] amps = { 1f, 0.7f, 0.45f, 0.25f };
            for (int p = 0; p < freqs.Length; p++)
            {
                for (int i = ching; i < n; i++)
                {
                    float t = (float)(i - ching) / Rate;
                    buf[i] += amps[p] * 0.5f * Mathf.Sin(2f * Mathf.PI * freqs[p] * t) * Mathf.Exp(-t * (2.5f + p));
                }
            }
            return Make("CashRegister", buf, 0.8f);
        }

        private static AudioClip BuildDoorOpen()
        {
            float[] buf = new float[(int)(Rate * 1.8f)];
            var rng = new System.Random(5);
            AddLatch(buf, 0f, 0.7f, rng);
            AddCreak(buf, 0.06f, 0.75f, 95f, 150f, 0.5f, rng);

            // Shop bell: two small bells shaken a few times, each strike a little softer.
            float[] strikes = { 0.10f, 0.19f, 0.30f, 0.43f, 0.58f };
            for (int k = 0; k < strikes.Length; k++)
            {
                float pitch = k % 2 == 0 ? 2650f : 3150f;
                AddBell(buf, strikes[k], pitch, 0.55f * Mathf.Pow(0.75f, k));
            }
            return Make("DoorOpen", buf, 0.8f);
        }

        private static AudioClip BuildDoorClose()
        {
            float[] buf = new float[(int)(Rate * 1.0f)];
            var rng = new System.Random(9);
            AddCreak(buf, 0f, 0.4f, 140f, 100f, 0.35f, rng);
            // Door meeting the frame: low thud.
            int s0 = (int)(0.42f * Rate);
            for (int i = s0; i < buf.Length; i++)
            {
                float t = (float)(i - s0) / Rate;
                buf[i] += (Mathf.Sin(2f * Mathf.PI * 70f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 140f * t)) * Mathf.Exp(-t * 22f);
            }
            AddLatch(buf, 0.44f, 0.6f, rng);
            AddBell(buf, 0.45f, 2650f, 0.2f);
            return Make("DoorClose", buf, 0.75f);
        }

        /// <summary>Metallic latch: a short noise burst plus a tiny ring.</summary>
        private static void AddLatch(float[] buf, float start, float gain, System.Random rng)
        {
            int s0 = (int)(start * Rate);
            int len = (int)(0.05f * Rate);
            for (int i = 0; i < len && s0 + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buf[s0 + i] += (noise * 0.7f + Mathf.Sin(2f * Mathf.PI * 1900f * t)) * Mathf.Exp(-t * 90f) * gain;
            }
        }

        /// <summary>
        /// Wooden hinge creak: a stick-slip train of tiny clicks whose rate glides from
        /// rateFrom to rateTo, each click ringing two wood-like resonances.
        /// </summary>
        private static void AddCreak(float[] buf, float start, float dur, float rateFrom, float rateTo, float gain, System.Random rng)
        {
            float t = 0f;
            while (t < dur)
            {
                float k = t / dur;
                float env = Mathf.Sin(Mathf.PI * k);                 // swell in and out
                float rate = Mathf.Lerp(rateFrom, rateTo, Mathf.SmoothStep(0f, 1f, k));
                rate *= 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t); // wobble
                int s0 = (int)((start + t) * Rate);
                float amp = env * gain * (0.7f + 0.3f * (float)rng.NextDouble());
                int len = (int)(0.012f * Rate);
                for (int i = 0; i < len && s0 + i < buf.Length; i++)
                {
                    float tt = (float)i / Rate;
                    float ring = Mathf.Sin(2f * Mathf.PI * 620f * tt) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1450f * tt);
                    buf[s0 + i] += ring * Mathf.Exp(-tt * 380f) * amp;
                }
                t += 1f / rate * (0.85f + 0.3f * (float)rng.NextDouble());
            }
        }

        /// <summary>Small bell: inharmonic partials, the higher ones dying faster.</summary>
        private static void AddBell(float[] buf, float start, float freq, float gain)
        {
            float[] ratios = { 1f, 2.76f, 5.4f };
            float[] amps = { 1f, 0.45f, 0.25f };
            int s0 = (int)(start * Rate);
            for (int p = 0; p < ratios.Length; p++)
            {
                float f = freq * ratios[p];
                if (f > Rate * 0.45f) break;
                for (int i = s0; i < buf.Length; i++)
                {
                    float t = (float)(i - s0) / Rate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * (4f + 3f * p)) * amps[p] * gain;
                }
            }
        }

        private static AudioClip BuildDing()
        {
            int n = (int)(Rate * 1.4f);
            float[] buf = new float[n];
            float[] notes = { 1318.5f, 1760f }; // E6 -> A6
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * 0.14f * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 3.5f);
                    buf[i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * t)) * env;
                }
            }
            return Make("Ding", buf, 0.6f);
        }

        private static AudioClip BuildClick()
        {
            int n = (int)(Rate * 0.06f);
            float[] buf = new float[n];
            var rng = new System.Random(11);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                buf[i] = ((float)(rng.NextDouble() * 2.0 - 1.0) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1800f * t)) * Mathf.Exp(-t * 120f);
            }
            return Make("Click", buf, 0.6f);
        }

        // ------------------------------------------------------------------
        // Electric guitar
        // ------------------------------------------------------------------
        private static AudioClip BuildUnplugged()
        {
            float len = 0.9f;
            float[] buf = new float[(int)(Rate * len)];
            for (int s = 0; s < s_EMajor.Length; s++)
            {
                // Plucked hard and close to the bridge, dies fast, almost no body.
                AddNote(buf, s * 0.018f, len, s_EMajor[s], s_EMajor[s], 0f, 0f, 0.8f,
                    pluckPos: 0.12f, decayBase: 6f, decayPerHarmonic: 4f, fundamentalGain: 0.25f);
            }
            return Make("UnpluggedStrum", buf, 0.35f);
        }

        private static AudioClip BuildClean()
        {
            float len = 3.0f;
            float[] buf = new float[(int)(Rate * len)];
            for (int s = 0; s < s_EMajor.Length; s++)
            {
                AddNote(buf, s * 0.022f, len, s_EMajor[s], s_EMajor[s], 0f, 0f, s < 3 ? 1f : 0.8f,
                    pluckPos: 0.2f, decayBase: 0.9f, decayPerHarmonic: 0.7f, fundamentalGain: 1f);
            }
            // A touch of amp warmth.
            for (int i = 0; i < buf.Length; i++) buf[i] = Tanh(buf[i] * 1.2f);
            LowPass(buf, 3200f);
            return Make("CleanStrum", buf, 0.8f);
        }

        private static AudioClip BuildLead()
        {
            float len = 2.6f;
            float[] buf = new float[(int)(Rate * len)];
            // A minor pentatonic lick: A4 C5 D5, then bend D5 -> E5 and hold with vibrato.
            AddNote(buf, 0.00f, 0.20f, 440.00f, 440.00f, 0f, 0f, 1f, 0.2f, 0.6f, 0.35f, 1f);
            AddNote(buf, 0.20f, 0.20f, 523.25f, 523.25f, 0f, 0f, 1f, 0.2f, 0.6f, 0.35f, 1f);
            AddNote(buf, 0.40f, 0.22f, 587.33f, 587.33f, 0f, 0f, 1f, 0.2f, 0.6f, 0.35f, 1f);
            AddNote(buf, 0.62f, 1.98f, 587.33f, 659.25f, 0.12f, 0.12f, 1.1f, 0.2f, 0.45f, 0.3f, 1f, vibratoFrom: 0.5f);
            // Power chord underneath the start (A5: A2 E3 A3).
            AddNote(buf, 0.00f, 0.62f, 110.00f, 110.00f, 0f, 0f, 0.7f, 0.2f, 0.8f, 0.4f, 1f);
            AddNote(buf, 0.01f, 0.62f, 164.81f, 164.81f, 0f, 0f, 0.6f, 0.2f, 0.8f, 0.4f, 1f);

            // High-gain distortion + cabinet roll-off.
            for (int i = 0; i < buf.Length; i++) buf[i] = Tanh(buf[i] * 9f);
            LowPass(buf, 3800f);
            LowPass(buf, 5000f);
            return Make("LeadLick", buf, 0.7f);
        }

        /// <summary>
        /// Plucked string made of decaying harmonics. Supports a pitch bend
        /// (from freq to bendTo, starting bendStart s into the note over bendTime s)
        /// and vibrato after vibratoFrom s. The note is cut with a short release at
        /// start + dur.
        /// </summary>
        private static void AddNote(float[] buf, float start, float dur, float freq, float bendTo,
            float bendStart, float bendTime, float gain, float pluckPos, float decayBase,
            float decayPerHarmonic, float fundamentalGain, float vibratoFrom = -1f)
        {
            const int harmonics = 8;
            int s0 = (int)(start * Rate);
            int len = Mathf.Min(buf.Length - s0, (int)((dur + 0.03f) * Rate));
            if (len <= 0) return;

            for (int h = 1; h <= harmonics; h++)
            {
                if (Mathf.Max(freq, bendTo) * h > Rate * 0.45f) break;
                float amp = Mathf.Abs(Mathf.Sin(h * Mathf.PI * pluckPos)) / h * (h == 1 ? fundamentalGain : 1f);
                float decay = decayBase + decayPerHarmonic * h;
                float phase = 0f;
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / Rate;
                    float f = freq;
                    if (bendTime > 0f && t > bendStart)
                        f = Mathf.Lerp(freq, bendTo, Mathf.SmoothStep(0f, 1f, (t - bendStart) / bendTime));
                    if (vibratoFrom >= 0f && t > vibratoFrom)
                        f *= 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 5.5f * (t - vibratoFrom)) * Mathf.Clamp01((t - vibratoFrom) * 2f);
                    phase += 2f * Mathf.PI * f * h / Rate;
                    if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;

                    float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(i / 40f);
                    if (t > dur) env *= Mathf.Clamp01(1f - (t - dur) / 0.03f);
                    buf[s0 + i] += Mathf.Sin(phase) * amp * env * gain;
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * Mathf.Clamp(x, -10f, 10f));
            return (e - 1f) / (e + 1f);
        }

        private static void LowPass(float[] buf, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / Rate;
            float a = dt / (rc + dt);
            float y = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                y += a * (buf[i] - y);
                buf[i] = y;
            }
        }

        private static AudioClip Make(string name, float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max > 1e-5f)
            {
                float k = peak / max;
                for (int i = 0; i < buf.Length; i++) buf[i] *= k;
            }
            AudioClip clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
