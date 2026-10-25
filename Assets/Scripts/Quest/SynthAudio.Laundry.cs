using UnityEngine;

namespace VRVillage
{
    /// <summary>Laundromat sounds (ceiling fans, change machine, laundry, queue machine).</summary>
    public static partial class SynthAudio
    {
        private static AudioClip s_FanWhirr, s_ChangeOut, s_ClothFlop, s_QueueConfirm;
        private static AudioClip s_MachineDoor, s_WashLoop, s_DryerHum, s_WashDone;

        /// <summary>Ceiling fan: soft motor hum and air swooshing with the blades. Loops seamlessly (1 s).</summary>
        public static AudioClip FanWhirr => s_FanWhirr != null ? s_FanWhirr : s_FanWhirr = BuildFanWhirr();
        /// <summary>A handful of coins clattering down into a metal tray.</summary>
        public static AudioClip ChangeOut => s_ChangeOut != null ? s_ChangeOut : s_ChangeOut = BuildChangeOut();
        /// <summary>Soft whump of a shirt landing on a pile of clothes.</summary>
        public static AudioClip ClothFlop => s_ClothFlop != null ? s_ClothFlop : s_ClothFlop = BuildClothFlop();
        /// <summary>Booking confirmed: rising two-tone beep and a little ticket-printer buzz.</summary>
        public static AudioClip QueueConfirm => s_QueueConfirm != null ? s_QueueConfirm : s_QueueConfirm = BuildQueueConfirm();

        /// <summary>Washer/dryer door: plastic latch clack and a soft bump.</summary>
        public static AudioClip MachineDoor => s_MachineDoor != null ? s_MachineDoor : s_MachineDoor = BuildMachineDoor();
        /// <summary>Washer running: motor hum, water sloshing with each turn of the drum. Loops seamlessly (2 s).</summary>
        public static AudioClip WashLoop => s_WashLoop != null ? s_WashLoop : s_WashLoop = BuildWashLoop();
        /// <summary>Dryer running: warm low hum, fan rush and clothes tumbling. Loops seamlessly (2 s).</summary>
        public static AudioClip DryerHum => s_DryerHum != null ? s_DryerHum : s_DryerHum = BuildDryerHum();
        /// <summary>End of the wash: a short, bright bell tune (our own, in the style of a washer's end chime).</summary>
        public static AudioClip WashDone => s_WashDone != null ? s_WashDone : s_WashDone = BuildWashDone();

        private static AudioClip BuildFanWhirr()
        {
            int n = Rate; // whole cycles of 100 Hz and of the 6 Hz blade swoosh, so it loops without a click
            float[] buf = new float[n];
            var rng = new System.Random(61);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.06f * (noise - lp);
                float swoosh = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                float motor = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.05f;
                buf[i] = lp * 2.2f * swoosh + motor;
            }
            LowPass(buf, 1500f);
            return Make("FanWhirr", buf, 0.4f);
        }

        private static AudioClip BuildChangeOut()
        {
            float[] buf = new float[(int)(Rate * 1.2f)];
            var rng = new System.Random(67);
            // Coins tumbling out of the hopper: quick clinks, then a few settling in the tray.
            float t = 0f;
            for (int k = 0; k < 14; k++)
            {
                t += 0.025f + (float)rng.NextDouble() * 0.05f;
                float g = k < 10 ? 0.7f + 0.3f * (float)rng.NextDouble() : 0.45f * Mathf.Pow(0.75f, k - 10);
                AddClink(buf, t, 2600f + (float)rng.NextDouble() * 1800f, g);
            }
            AddThud(buf, 0.05f, 150f, 0.3f, 30f); // the hopper motor kicking
            AddMetal(buf, t * 0.6f, 0.25f);        // the metal tray ringing
            LowPass(buf, 7000f);
            return Make("ChangeOut", buf, 0.7f);
        }

        private static AudioClip BuildClothFlop()
        {
            float[] buf = new float[(int)(Rate * 0.35f)];
            var rng = new System.Random(71);
            float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.08f * (noise - lp);
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 16f);
                buf[i] = lp * env * 2f;
            }
            AddThud(buf, 0.005f, 90f, 0.35f, 28f);
            LowPass(buf, 1800f);
            return Make("ClothFlop", buf, 0.6f);
        }

        private static AudioClip BuildMachineDoor()
        {
            float[] buf = new float[(int)(Rate * 0.45f)];
            var rng = new System.Random(73);
            AddLatch(buf, 0f, 0.6f, rng);
            AddThud(buf, 0.01f, 140f, 0.45f, 30f);
            AddClink(buf, 0.02f, 1500f, 0.25f);
            LowPass(buf, 5000f);
            return Make("MachineDoor", buf, 0.65f);
        }

        private static AudioClip BuildWashLoop()
        {
            int n = Rate * 2; // whole cycles of 50 Hz and of the 1 Hz slosh
            float[] buf = new float[n];
            var rng = new System.Random(79);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.05f * (noise - lp);
                lp2 += 0.2f * (noise - lp2);
                // Water: a swell each drum turn, with bubbly detail on top.
                float slosh = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 1f * t), 2f);
                float water = lp * 2.5f * (0.35f + slosh) + lp2 * 0.25f * slosh;
                float motor = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.1f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.06f;
                buf[i] = water + motor;
            }
            LowPass(buf, 2500f);
            return Make("WashLoop", buf, 0.5f);
        }

        private static AudioClip BuildDryerHum()
        {
            int n = Rate * 2;
            float[] buf = new float[n];
            var rng = new System.Random(83);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.04f * (noise - lp);
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.1f;
                buf[i] = hum + lp * 1.8f;
            }
            // Clothes flopping over in the drum, twice a turn.
            for (float k = 0.2f; k < 2f; k += 0.5f) AddThud(buf, k, 75f, 0.18f, 18f);
            LowPass(buf, 1800f);
            return Make("DryerHum", buf, 0.45f);
        }

        private static AudioClip BuildWashDone()
        {
            // A rising, happy little tune on soft bells: C6 E6 G6 C7, then A6 F6 G6 C7 held.
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 1760f, 1396.9f, 1568f, 2093f };
            float[] times = { 0f, 0.16f, 0.32f, 0.48f, 0.8f, 0.96f, 1.12f, 1.28f };
            float[] buf = new float[(int)(Rate * 2.6f)];
            for (int k = 0; k < notes.Length; k++)
                AddBell(buf, times[k], notes[k] * 0.5f, k == notes.Length - 1 ? 0.6f : 0.45f);
            return Make("WashDone", buf, 0.7f);
        }

        private static AudioClip BuildQueueConfirm()
        {
            float[] buf = new float[(int)(Rate * 0.9f)];
            AddBeep(buf, 0f, 0.11f, 880f, false);
            AddBeep(buf, 0.13f, 0.16f, 1318.5f, false);
            // Ticket printer: a short buzzing chatter.
            int s0 = (int)(0.35f * Rate), len = (int)(0.4f * Rate);
            for (int i = 0; i < len && s0 + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float chatter = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 32f * t)) * 0.5f + 0.5f;
                buf[s0 + i] += Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.25f * chatter * Mathf.Clamp01((0.4f - t) / 0.05f);
            }
            return Make("QueueConfirm", buf, 0.6f);
        }
    }
}
