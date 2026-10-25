using UnityEngine;

namespace VRVillage
{
    /// <summary>Sound ids so steps can pick a synthesized clip from the inspector.</summary>
    public enum SynthSound
    {
        None,
        Ding,
        Buzzer,
        Click,
        Swish,
        CoinDrop,
        CanDrop,
        CanRoll,
        Gulp,
        Sigh,
        CardError,
        CardApproved,
        Crunch,
        Crumple,
        SnackDrop,
        ButtonBeep,
        CashRegister,
        BenchCreak,
        NeonBuzz,
        Grind,
        MetalClack,
        ChairThunk,
        EspressoPump,
        MilkPour,
        PlateClink,
        FanWhirr,
        ChangeOut,
        ClothFlop,
        QueueConfirm,
        MachineDoor,
        WashLoop,
        DryerHum,
        WashDone,
        CardDispense,
        ArcadeStart,
        ArcadeWin,
        StarBlip,
        NetSwish,
        BallBounce,
        MolePop,
        Bonk,
        GoalHorn,
        PuckClack,
        ClawWhirr,
    }

    /// <summary>Sakura Park sounds (mail, vending machine, drinking, snacks, card reader).</summary>
    public static partial class SynthAudio
    {
        private static AudioClip s_Swish, s_CoinDrop, s_CanDrop, s_CanRoll, s_Gulp, s_Sigh;
        private static AudioClip s_CardError, s_CardApproved, s_Crunch, s_Crumple, s_SnackDrop, s_ButtonBeep, s_BenchCreak;

        public static AudioClip Swish => s_Swish != null ? s_Swish : s_Swish = BuildSwish();
        public static AudioClip CoinDrop => s_CoinDrop != null ? s_CoinDrop : s_CoinDrop = BuildCoinDrop();
        public static AudioClip CanDrop => s_CanDrop != null ? s_CanDrop : s_CanDrop = BuildCanDrop();
        public static AudioClip CanRoll => s_CanRoll != null ? s_CanRoll : s_CanRoll = BuildCanRoll();
        public static AudioClip Gulp => s_Gulp != null ? s_Gulp : s_Gulp = BuildGulp();
        public static AudioClip Sigh => s_Sigh != null ? s_Sigh : s_Sigh = BuildSigh();
        public static AudioClip CardError => s_CardError != null ? s_CardError : s_CardError = BuildCardError();
        public static AudioClip CardApproved => s_CardApproved != null ? s_CardApproved : s_CardApproved = BuildCardApproved();
        public static AudioClip Crunch => s_Crunch != null ? s_Crunch : s_Crunch = BuildCrunch();
        public static AudioClip Crumple => s_Crumple != null ? s_Crumple : s_Crumple = BuildCrumple();
        public static AudioClip SnackDrop => s_SnackDrop != null ? s_SnackDrop : s_SnackDrop = BuildSnackDrop();
        public static AudioClip ButtonBeep => s_ButtonBeep != null ? s_ButtonBeep : s_ButtonBeep = BuildButtonBeep();
        /// <summary>Wooden slats taking your weight: a low creak and a soft settle.</summary>
        public static AudioClip BenchCreak => s_BenchCreak != null ? s_BenchCreak : s_BenchCreak = BuildBenchCreak();

        public static AudioClip Get(SynthSound sound)
        {
            switch (sound)
            {
                case SynthSound.Ding: return Ding;
                case SynthSound.Buzzer: return Buzzer;
                case SynthSound.Click: return Click;
                case SynthSound.Swish: return Swish;
                case SynthSound.CoinDrop: return CoinDrop;
                case SynthSound.CanDrop: return CanDrop;
                case SynthSound.CanRoll: return CanRoll;
                case SynthSound.Gulp: return Gulp;
                case SynthSound.Sigh: return Sigh;
                case SynthSound.CardError: return CardError;
                case SynthSound.CardApproved: return CardApproved;
                case SynthSound.Crunch: return Crunch;
                case SynthSound.Crumple: return Crumple;
                case SynthSound.SnackDrop: return SnackDrop;
                case SynthSound.ButtonBeep: return ButtonBeep;
                case SynthSound.BenchCreak: return BenchCreak;
                case SynthSound.CashRegister: return CashRegister;
                case SynthSound.NeonBuzz: return NeonBuzz;
                case SynthSound.Grind: return Grind;
                case SynthSound.MetalClack: return MetalClack;
                case SynthSound.ChairThunk: return ChairThunk;
                case SynthSound.EspressoPump: return EspressoPump;
                case SynthSound.MilkPour: return MilkPour;
                case SynthSound.PlateClink: return PlateClink;
                case SynthSound.FanWhirr: return FanWhirr;
                case SynthSound.ChangeOut: return ChangeOut;
                case SynthSound.ClothFlop: return ClothFlop;
                case SynthSound.QueueConfirm: return QueueConfirm;
                case SynthSound.MachineDoor: return MachineDoor;
                case SynthSound.WashLoop: return WashLoop;
                case SynthSound.DryerHum: return DryerHum;
                case SynthSound.WashDone: return WashDone;
                case SynthSound.CardDispense: return CardDispense;
                case SynthSound.ArcadeStart: return ArcadeStart;
                case SynthSound.ArcadeWin: return ArcadeWin;
                case SynthSound.StarBlip: return StarBlip;
                case SynthSound.NetSwish: return NetSwish;
                case SynthSound.BallBounce: return BallBounce;
                case SynthSound.MolePop: return MolePop;
                case SynthSound.Bonk: return Bonk;
                case SynthSound.GoalHorn: return GoalHorn;
                case SynthSound.PuckClack: return PuckClack;
                case SynthSound.ClawWhirr: return ClawWhirr;
                default: return null;
            }
        }

        /// <summary>Play a synthesized sound at a world position (no-op for None).</summary>
        public static void PlayAt(SynthSound sound, Vector3 position, float volume = 1f)
        {
            AudioClip clip = Get(sound);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        // ------------------------------------------------------------------

        /// <summary>Paper sliding through a slot: band-passed noise sweeping down.</summary>
        private static AudioClip BuildSwish()
        {
            float len = 0.55f;
            float[] buf = new float[(int)(Rate * len)];
            var rng = new System.Random(21);
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / len;
                float cutoff = Mathf.Lerp(4500f, 700f, k);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (n - lp);
                hp = 0.97f * (hp + lp - prev); // remove rumble
                prev = lp;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.1f)) * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 18f * t));
                buf[i] = hp * env;
            }
            // Soft thud as it lands inside.
            AddThud(buf, 0.42f, 110f, 0.35f, 30f);
            return Make("Swish", buf, 0.7f);
        }

        /// <summary>Coin clinking down a chute inside the machine, then settling.</summary>
        private static AudioClip BuildCoinDrop()
        {
            float[] buf = new float[(int)(Rate * 1.1f)];
            float[] times = { 0f, 0.16f, 0.28f, 0.37f, 0.44f, 0.5f };
            for (int k = 0; k < times.Length; k++)
            {
                float g = Mathf.Pow(0.72f, k);
                AddClink(buf, times[k], 3200f + 400f * (k % 2), g);
            }
            AddThud(buf, 0.55f, 140f, 0.4f, 25f);
            LowPass(buf, 6000f); // it's inside a box
            return Make("CoinDrop", buf, 0.7f);
        }

        /// <summary>Can dropping into the tray: heavy clunk plus a small bounce.</summary>
        private static AudioClip BuildCanDrop()
        {
            float[] buf = new float[(int)(Rate * 0.9f)];
            AddThud(buf, 0f, 95f, 1f, 16f);
            AddMetal(buf, 0f, 0.6f);
            AddThud(buf, 0.18f, 120f, 0.4f, 22f);
            AddMetal(buf, 0.18f, 0.25f);
            return Make("CanDrop", buf, 0.85f);
        }

        /// <summary>Aluminium can rolling on gravel: rumbling, ticking noise that slows down.</summary>
        private static AudioClip BuildCanRoll()
        {
            float len = 2.6f;
            float[] buf = new float[(int)(Rate * len)];
            var rng = new System.Random(4);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / len;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.08f * (n - lp);
                float spin = Mathf.Lerp(9f, 3f, k); // revolutions per second
                phase += spin / Rate;
                float tick = Mathf.Exp(-(phase % 1f) * 30f);
                float env = Mathf.Clamp01(t / 0.05f) * (1f - k) * (1f - k);
                buf[i] = (lp * 2.2f + tick * 0.5f * n) * env;
            }
            return Make("CanRoll", buf, 0.5f);
        }

        /// <summary>One "glug": a quick downward-sweeping throat tone with a wet click.</summary>
        private static AudioClip BuildGulp()
        {
            float len = 0.32f;
            float[] buf = new float[(int)(Rate * len)];
            float phase = 0f;
            var rng = new System.Random(8);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(320f, 110f, Mathf.Clamp01(t / 0.18f));
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 11f);
                float wet = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f) * 0.3f;
                buf[i] = Mathf.Sin(phase) * env + wet;
            }
            return Make("Gulp", buf, 0.6f);
        }

        /// <summary>A content "ahhh": breathy voice through an "ah" vowel, pitch falling.</summary>
        private static AudioClip BuildSigh()
        {
            float len = 1.4f;
            float[] buf = new float[(int)(Rate * len)];
            var rng = new System.Random(13);
            var f1 = new Resonator(800f, 90f);
            var f2 = new Resonator(1200f, 110f);
            var f3 = new Resonator(2500f, 160f);
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float pitch = Mathf.Lerp(185f, 120f, t / len);
                phase += pitch / Rate;
                float saw = 2f * (phase % 1f) - 1f;
                float breath = (float)(rng.NextDouble() * 2.0 - 1.0);
                float voiced = Mathf.Lerp(0.15f, 0.55f, Mathf.Clamp01((t - 0.1f) * 3f)) * Mathf.Clamp01((len - t) * 1.5f);
                float src = saw * voiced + breath * 0.6f;
                float y = f1.Process(src) + 0.7f * f2.Process(src) + 0.25f * f3.Process(src);
                float env = Mathf.Clamp01(t / 0.12f) * Mathf.Pow(1f - t / len, 1.3f);
                buf[i] = y * env;
            }
            return Make("Sigh", buf, 0.65f);
        }

        /// <summary>Two low "denied" beeps.</summary>
        private static AudioClip BuildCardError()
        {
            float[] buf = new float[(int)(Rate * 0.55f)];
            AddBeep(buf, 0f, 0.16f, 330f, true);
            AddBeep(buf, 0.24f, 0.2f, 262f, true);
            return Make("CardError", buf, 0.55f);
        }

        /// <summary>Bright rising "approved" chirp.</summary>
        private static AudioClip BuildCardApproved()
        {
            float[] buf = new float[(int)(Rate * 0.6f)];
            AddBeep(buf, 0f, 0.1f, 1318.5f, false);
            AddBeep(buf, 0.12f, 0.22f, 1975.5f, false);
            return Make("CardApproved", buf, 0.6f);
        }

        /// <summary>A crunchy munch: dense crackle bursts, high-passed.</summary>
        private static AudioClip BuildCrunch()
        {
            float len = 0.35f;
            float[] buf = new float[(int)(Rate * len)];
            var rng = new System.Random(17);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                float crack = rng.NextDouble() < 0.02 ? (float)(rng.NextDouble() * 2.0 - 1.0) * 3f : 0f;
                float x = n * 0.5f + crack;
                hp = 0.9f * (hp + x - prev);
                prev = x;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 9f);
                buf[i] = hp * env;
            }
            AddThud(buf, 0f, 90f, 0.25f, 30f); // jaw
            return Make("Crunch", buf, 0.6f);
        }

        /// <summary>Crinkly plastic bag being squashed.</summary>
        private static AudioClip BuildCrumple()
        {
            float len = 0.7f;
            float[] buf = new float[(int)(Rate * len)];
            var rng = new System.Random(23);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float burst = rng.NextDouble() < 0.006 ? 1f : 0f;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0) * (0.25f + burst * 4f);
                hp = 0.85f * (hp + n - prev);
                prev = n;
                float env = Mathf.Sin(Mathf.PI * t / len);
                buf[i] = hp * env;
            }
            return Make("Crumple", buf, 0.55f);
        }

        /// <summary>Snack bag landing in the tray: soft thump plus crinkle.</summary>
        private static AudioClip BuildSnackDrop()
        {
            float[] buf = new float[(int)(Rate * 0.5f)];
            AddThud(buf, 0f, 85f, 0.8f, 20f);
            var rng = new System.Random(29);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                buf[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f * Mathf.Exp(-t * 14f);
            }
            return Make("SnackDrop", buf, 0.65f);
        }

        private static AudioClip BuildBenchCreak()
        {
            float[] buf = new float[(int)(Rate * 0.9f)];
            var rng = new System.Random(31);
            AddThud(buf, 0.05f, 75f, 0.5f, 14f);              // weight on the seat
            AddCreak(buf, 0.08f, 0.5f, 70f, 45f, 0.45f, rng);  // slow, low wooden creak
            return Make("BenchCreak", buf, 0.7f);
        }

        private static AudioClip BuildButtonBeep()
        {
            float[] buf = new float[(int)(Rate * 0.12f)];
            AddBeep(buf, 0f, 0.08f, 1046.5f, false);
            return Make("ButtonBeep", buf, 0.5f);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------
        private static void AddThud(float[] buf, float start, float freq, float gain, float decay)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < buf.Length; i++)
            {
                float t = (float)(i - s0) / Rate;
                float f = freq * (1f + 0.6f * Mathf.Exp(-t * 40f)); // pitch drops on impact
                buf[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * decay) * gain;
            }
        }

        private static void AddClink(float[] buf, float start, float freq, float gain)
        {
            float[] ratios = { 1f, 1.52f, 2.31f };
            int s0 = (int)(start * Rate);
            foreach (float r in ratios)
            {
                float f = freq * r;
                if (f > Rate * 0.45f) continue;
                for (int i = s0; i < buf.Length; i++)
                {
                    float t = (float)(i - s0) / Rate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 28f * r) * gain * 0.4f;
                }
            }
        }

        private static void AddMetal(float[] buf, float start, float gain)
        {
            float[] freqs = { 870f, 1390f, 2260f };
            int s0 = (int)(start * Rate);
            for (int p = 0; p < freqs.Length; p++)
            {
                for (int i = s0; i < buf.Length; i++)
                {
                    float t = (float)(i - s0) / Rate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * freqs[p] * t) * Mathf.Exp(-t * (18f + 8f * p)) * gain * 0.35f;
                }
            }
        }

        private static void AddBeep(float[] buf, float start, float dur, float freq, bool square)
        {
            int s0 = (int)(start * Rate);
            int len = (int)(dur * Rate);
            for (int i = 0; i < len && s0 + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (square) s = Mathf.Sign(s) * 0.6f;
                float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((dur - t) / 0.02f);
                buf[s0 + i] += s * env;
            }
        }

        /// <summary>Two-pole resonant band-pass (a vowel formant).</summary>
        private class Resonator
        {
            private readonly float m_A1, m_A2, m_Gain;
            private float m_Y1, m_Y2;

            public Resonator(float freq, float bandwidth)
            {
                float r = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
                m_A1 = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
                m_A2 = -r * r;
                m_Gain = 1f - r;
            }

            public float Process(float x)
            {
                float y = m_Gain * x + m_A1 * m_Y1 + m_A2 * m_Y2;
                m_Y2 = m_Y1;
                m_Y1 = y;
                return y;
            }
        }
    }
}
