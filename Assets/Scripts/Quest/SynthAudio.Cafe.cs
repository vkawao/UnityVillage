using UnityEngine;

namespace VRVillage
{
    /// <summary>Cafe sounds (neon sign, coffee grinder, portafilter, chairs).</summary>
    public static partial class SynthAudio
    {
        private static AudioClip s_NeonBuzz, s_Grind, s_MetalClack, s_ChairThunk, s_EspressoPump, s_MilkPour, s_PlateClink;

        /// <summary>Vibratory espresso pump: low buzz plus the hiss of hot water. Loops seamlessly (1 s).</summary>
        public static AudioClip EspressoPump => s_EspressoPump != null ? s_EspressoPump : s_EspressoPump = BuildEspressoPump();
        /// <summary>Liquid trickling into a cup. Loops seamlessly (1 s).</summary>
        public static AudioClip MilkPour => s_MilkPour != null ? s_MilkPour : s_MilkPour = BuildMilkPour();
        /// <summary>Ceramic clink: something set down on a saucer.</summary>
        public static AudioClip PlateClink => s_PlateClink != null ? s_PlateClink : s_PlateClink = BuildPlateClink();

        /// <summary>Neon transformer hum with a little crackle. Loops seamlessly (1 s).</summary>
        public static AudioClip NeonBuzz => s_NeonBuzz != null ? s_NeonBuzz : s_NeonBuzz = BuildNeonBuzz();
        /// <summary>Burr grinder crunching beans. Loops seamlessly (1 s).</summary>
        public static AudioClip Grind => s_Grind != null ? s_Grind : s_Grind = BuildGrind();
        /// <summary>Portafilter locking into place: metal knock plus a latch click.</summary>
        public static AudioClip MetalClack => s_MetalClack != null ? s_MetalClack : s_MetalClack = BuildMetalClack();
        /// <summary>Wooden chair set down on a wooden floor.</summary>
        public static AudioClip ChairThunk => s_ChairThunk != null ? s_ChairThunk : s_ChairThunk = BuildChairThunk();

        private static AudioClip BuildNeonBuzz()
        {
            int n = Rate; // whole number of 60 Hz cycles, so it loops without a click
            float[] buf = new float[n];
            var rng = new System.Random(41);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.6f
                          + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.3f
                          + Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.15f;
                // Gas discharge: a gritty edge that pulses with the mains.
                float grit = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 60f * t));
                buf[i] = hum + grit;
            }
            LowPass(buf, 3000f);
            return Make("NeonBuzz", buf, 0.35f);
        }

        private static AudioClip BuildGrind()
        {
            int n = Rate;
            float[] buf = new float[n];
            var rng = new System.Random(43);
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Beans cracking: dense random clicks on top of gritty noise.
                float crack = rng.NextDouble() < 0.012 ? (float)(rng.NextDouble() * 2.0 - 1.0) * 2.5f : 0f;
                lp += 0.35f * (noise + crack - lp);
                hp = 0.92f * (hp + lp - prev);
                prev = lp;
                // Burrs rumbling: 20 Hz wobble so it sounds mechanical (20 whole cycles per loop).
                float rumble = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.25f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 20f * t));
                buf[i] = hp * 0.8f + rumble;
            }
            return Make("Grind", buf, 0.55f);
        }

        private static AudioClip BuildMetalClack()
        {
            float[] buf = new float[(int)(Rate * 0.6f)];
            var rng = new System.Random(47);
            AddMetal(buf, 0f, 0.7f);
            AddThud(buf, 0f, 160f, 0.5f, 35f);
            AddLatch(buf, 0.12f, 0.5f, rng); // twisted into the lock
            AddClink(buf, 0.13f, 2600f, 0.4f);
            return Make("MetalClack", buf, 0.75f);
        }

        private static AudioClip BuildEspressoPump()
        {
            int n = Rate; // whole number of 50 Hz cycles
            float[] buf = new float[n];
            var rng = new System.Random(53);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 50f * t)) * 0.35f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.3f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.25f * (noise - lp);
                buf[i] = buzz * 0.6f + (noise - lp) * 0.35f; // hiss = noise minus its low end
            }
            LowPass(buf, 4000f);
            return Make("EspressoPump", buf, 0.45f);
        }

        private static AudioClip BuildMilkPour()
        {
            int n = Rate;
            float[] buf = new float[n];
            var rng = new System.Random(59);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.12f * (noise - lp);
                // Little bubbles popping: short tones at random pitches.
                float bubble = 0f;
                if (rng.NextDouble() < 0.0015) bubble = 1f;
                buf[i] = lp * 1.6f * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 7f * t)) + bubble;
            }
            LowPass(buf, 2200f);
            return Make("MilkPour", buf, 0.5f);
        }

        private static AudioClip BuildPlateClink()
        {
            float[] buf = new float[(int)(Rate * 0.5f)];
            AddClink(buf, 0f, 2900f, 0.8f);
            AddClink(buf, 0.06f, 3300f, 0.35f); // a little rattle as it settles
            AddThud(buf, 0f, 220f, 0.2f, 40f);
            return Make("PlateClink", buf, 0.6f);
        }

        private static AudioClip BuildChairThunk()
        {
            float[] buf = new float[(int)(Rate * 0.5f)];
            AddThud(buf, 0f, 120f, 0.9f, 22f);
            AddThud(buf, 0.035f, 180f, 0.45f, 30f); // second leg touching down
            LowPass(buf, 2500f);
            return Make("ChairThunk", buf, 0.7f);
        }
    }
}
