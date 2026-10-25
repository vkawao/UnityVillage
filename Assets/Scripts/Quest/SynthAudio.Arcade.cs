using UnityEngine;

namespace VRVillage
{
    /// <summary>Arcade sounds (card dispenser, game start/win jingles, rhythm game notes).</summary>
    public static partial class SynthAudio
    {
        private static AudioClip s_CardDispense, s_ArcadeStart, s_ArcadeWin;
        private static AudioClip s_StarBlip, s_NetSwish, s_BallBounce, s_MolePop, s_Bonk;
        private static AudioClip s_GoalHorn, s_PuckClack, s_ClawWhirr;
        private static AudioClip[] s_RhythmNotes;

        /// <summary>Air hockey goal: a short, buzzy two-tone arena horn.</summary>
        public static AudioClip GoalHorn => s_GoalHorn != null ? s_GoalHorn : s_GoalHorn = BuildGoalHorn();
        /// <summary>Puck hitting a pusher or the rail: a sharp plastic clack.</summary>
        public static AudioClip PuckClack => s_PuckClack != null ? s_PuckClack : s_PuckClack = BuildPuckClack();
        /// <summary>Claw machine motor moving the claw (short whirr).</summary>
        public static AudioClip ClawWhirr => s_ClawWhirr != null ? s_ClawWhirr : s_ClawWhirr = BuildClawWhirr();

        /// <summary>Cabinet game: a star caught (quick rising chip-tune blip).</summary>
        public static AudioClip StarBlip => s_StarBlip != null ? s_StarBlip : s_StarBlip = BuildStarBlip();
        /// <summary>Ball through the net: a soft "swish" of rope.</summary>
        public static AudioClip NetSwish => s_NetSwish != null ? s_NetSwish : s_NetSwish = BuildNetSwish();
        /// <summary>Basketball bouncing a few times as it rolls back onto the ramp.</summary>
        public static AudioClip BallBounce => s_BallBounce != null ? s_BallBounce : s_BallBounce = BuildBallBounce();
        /// <summary>A mole popping up out of its hole.</summary>
        public static AudioClip MolePop => s_MolePop != null ? s_MolePop : s_MolePop = BuildMolePop();
        /// <summary>Hammer on a mole: a hollow plastic bonk and a little squeak.</summary>
        public static AudioClip Bonk => s_Bonk != null ? s_Bonk : s_Bonk = BuildBonk();

        /// <summary>Card printer: a short motor whirr pushing the card out, then a click.</summary>
        public static AudioClip CardDispense => s_CardDispense != null ? s_CardDispense : s_CardDispense = BuildCardDispense();
        /// <summary>Game start: chip-tune "ready... go!" beeps.</summary>
        public static AudioClip ArcadeStart => s_ArcadeStart != null ? s_ArcadeStart : s_ArcadeStart = BuildArcadeStart();
        /// <summary>Game won: a fast chip-tune arpeggio and a held chord (our own "stage clear").</summary>
        public static AudioClip ArcadeWin => s_ArcadeWin != null ? s_ArcadeWin : s_ArcadeWin = BuildArcadeWin();

        /// <summary>Rhythm-game hit, step <paramref name="index"/> up the major scale from C5 (each hit sounds higher).</summary>
        public static AudioClip RhythmNote(int index)
        {
            if (s_RhythmNotes == null) s_RhythmNotes = new AudioClip[16];
            index = Mathf.Clamp(index, 0, s_RhythmNotes.Length - 1);
            if (s_RhythmNotes[index] == null) s_RhythmNotes[index] = BuildRhythmNote(index);
            return s_RhythmNotes[index];
        }

        // Major scale steps in semitones.
        private static readonly int[] s_Major = { 0, 2, 4, 5, 7, 9, 11 };

        private static float ScaleFreq(float root, int degree)
        {
            int octave = degree / s_Major.Length;
            int semis = s_Major[degree % s_Major.Length] + 12 * octave;
            return root * Mathf.Pow(2f, semis / 12f);
        }

        private static AudioClip BuildCardDispense()
        {
            float[] buf = new float[(int)(Rate * 0.9f)];
            var rng = new System.Random(89);
            float lp = 0f;
            // Motor: a buzzy whine rising a little, with rollers rubbing (filtered noise).
            for (int i = 0; i < (int)(Rate * 0.65f); i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(180f, 240f, t / 0.65f);
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((0.65f - t) / 0.05f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.15f * (noise - lp);
                float saw = 2f * ((t * f) % 1f) - 1f;
                buf[i] += (saw * 0.25f + lp * 0.6f) * env;
            }
            AddClink(buf, 0.68f, 2400f, 0.6f);  // the card clicking out
            AddThud(buf, 0.68f, 180f, 0.25f, 40f);
            LowPass(buf, 4500f);
            return Make("CardDispense", buf, 0.6f);
        }

        private static AudioClip BuildArcadeStart()
        {
            float[] buf = new float[(int)(Rate * 1.1f)];
            AddBeep(buf, 0f, 0.12f, 659.3f, true);
            AddBeep(buf, 0.25f, 0.12f, 659.3f, true);
            AddBeep(buf, 0.5f, 0.35f, 1318.5f, true); // "go!" an octave up
            return Make("ArcadeStart", buf, 0.5f);
        }

        private static AudioClip BuildArcadeWin()
        {
            float[] buf = new float[(int)(Rate * 2.2f)];
            // Quick climb through C major, then a held C-E-G-C chord.
            float[] run = { 523.3f, 659.3f, 784f, 1046.5f, 784f, 1046.5f, 1318.5f, 1568f };
            for (int k = 0; k < run.Length; k++) AddBeep(buf, k * 0.075f, 0.07f, run[k], true);
            float chord = run.Length * 0.075f + 0.05f;
            foreach (float f in new[] { 523.3f, 659.3f, 784f, 1046.5f })
                AddBeep(buf, chord, 1.1f, f, true);
            for (int i = (int)(chord * Rate); i < buf.Length; i++)
            {
                float t = (float)i / Rate - chord;
                buf[i] *= Mathf.Exp(-t * 1.6f); // the chord rings out
            }
            LowPass(buf, 6000f);
            return Make("ArcadeWin", buf, 0.6f);
        }

        private static AudioClip BuildStarBlip()
        {
            float[] buf = new float[(int)(Rate * 0.3f)];
            AddBeep(buf, 0f, 0.06f, 987.8f, true);
            AddBeep(buf, 0.06f, 0.06f, 1318.5f, true);
            AddBeep(buf, 0.12f, 0.12f, 1975.5f, true);
            return Make("StarBlip", buf, 0.45f);
        }

        private static AudioClip BuildNetSwish()
        {
            float[] buf = new float[(int)(Rate * 0.45f)];
            var rng = new System.Random(97);
            float lp = 0f, prev = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (noise - lp);
                float hp = lp - prev; // a thin, airy rustle
                prev = lp;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 7f);
                buf[i] = (hp * 3f + lp * 0.4f) * env;
            }
            return Make("NetSwish", buf, 0.6f);
        }

        private static AudioClip BuildBallBounce()
        {
            float[] buf = new float[(int)(Rate * 1.1f)];
            // Each bounce comes sooner and softer, then it rolls to a stop.
            float t = 0f, gap = 0.3f, gain = 0.8f;
            for (int k = 0; k < 5; k++)
            {
                AddThud(buf, t, 95f, gain, 22f);
                AddClink(buf, t, 520f, gain * 0.15f); // the hollow "pank" of the ball
                t += gap;
                gap *= 0.65f;
                gain *= 0.6f;
            }
            LowPass(buf, 2500f);
            return Make("BallBounce", buf, 0.6f);
        }

        private static AudioClip BuildMolePop()
        {
            float[] buf = new float[(int)(Rate * 0.25f)];
            // A quick upward "bloop".
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(250f, 700f, Mathf.Clamp01(t / 0.08f));
                phase += 2f * Mathf.PI * f / Rate;
                buf[i] = Mathf.Sin(phase) * Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-t * 18f);
            }
            return Make("MolePop", buf, 0.5f);
        }

        private static AudioClip BuildBonk()
        {
            float[] buf = new float[(int)(Rate * 0.6f)];
            AddThud(buf, 0f, 220f, 0.9f, 25f);  // hollow plastic head
            AddClink(buf, 0f, 900f, 0.35f);
            // Squeak: a short pitch drop.
            float phase = 0f;
            int s0 = (int)(0.08f * Rate);
            for (int i = s0; i < buf.Length; i++)
            {
                float t = (float)(i - s0) / Rate;
                if (t > 0.22f) break;
                float f = Mathf.Lerp(1900f, 1200f, t / 0.22f);
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.22f - t) / 0.05f);
                buf[i] += Mathf.Sin(phase) * env * 0.3f;
            }
            return Make("Bonk", buf, 0.7f);
        }

        private static AudioClip BuildGoalHorn()
        {
            float[] buf = new float[(int)(Rate * 1.2f)];
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float f = t < 0.45f ? 233f : 311f; // two blasts, the second higher
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((1.2f - t) / 0.15f) * (t > 0.4f && t < 0.5f ? 0.3f : 1f);
                float saw = 2f * ((t * f) % 1f) - 1f;
                float saw2 = 2f * ((t * f * 1.005f) % 1f) - 1f;
                buf[i] = (saw + saw2) * 0.5f * env;
            }
            LowPass(buf, 2200f);
            return Make("GoalHorn", buf, 0.6f);
        }

        private static AudioClip BuildPuckClack()
        {
            float[] buf = new float[(int)(Rate * 0.15f)];
            AddClink(buf, 0f, 1800f, 0.8f);
            AddThud(buf, 0f, 400f, 0.4f, 60f);
            return Make("PuckClack", buf, 0.6f);
        }

        private static AudioClip BuildClawWhirr()
        {
            float[] buf = new float[(int)(Rate * 0.5f)];
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((0.5f - t) / 0.08f);
                float saw = 2f * ((t * 140f) % 1f) - 1f;
                buf[i] = (saw * 0.4f + Mathf.Sin(2f * Mathf.PI * 280f * t) * 0.3f) * env;
            }
            LowPass(buf, 1500f);
            return Make("ClawWhirr", buf, 0.45f);
        }

        private static AudioClip BuildRhythmNote(int index)
        {
            float[] buf = new float[(int)(Rate * 0.5f)];
            float f = ScaleFreq(523.3f, index);
            AddBell(buf, 0f, f, 0.7f);
            AddBeep(buf, 0f, 0.06f, f * 2f, true); // a bright chip-tune attack on top
            return Make("RhythmNote" + index, buf, 0.6f);
        }
    }
}
