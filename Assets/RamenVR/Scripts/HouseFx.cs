using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RamenVR
{
    /// Small sounds and sparkles for one shop: a tick when you grab, a click on buttons,
    /// a pop and a sparkle when an item is placed. All sounds are made in code (no audio files).
    /// The game manager adds this by itself, nothing to set up.
    public class HouseFx : MonoBehaviour
    {
        const int Rate = 44100;
        static AudioClip tick, click, pop, ding, fanfare;

        public static AudioClip Ding => ding ? ding : ding = Chime(new[] { 1318.5f, 1975.5f }, 0.10f, 0.6f, 0.30f, "ding");
        public static AudioClip Fanfare => fanfare ? fanfare : fanfare = Chime(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 1.3f, 0.32f, "fanfare");
        static AudioClip Tick => tick ? tick : tick = Make("tick", 0.04f, t => Mathf.Sin(6.2832f * 2400f * t) * Mathf.Exp(-t * 140f) * 0.6f + Noise() * Mathf.Exp(-t * 300f) * 0.25f);
        static AudioClip Click => click ? click : click = Make("click", 0.06f, t => Mathf.Sin(6.2832f * 1500f * t) * Mathf.Exp(-t * 120f) * 0.6f + Mathf.Sin(6.2832f * 180f * t) * Mathf.Exp(-t * 60f) * 0.5f);
        static AudioClip Pop => pop ? pop : pop = MakePop();

        RamenGameManager gm;

        void Awake() { gm = GetComponent<RamenGameManager>(); }

        void Start()
        {
            var root = gm && gm.shopRoot ? gm.shopRoot : transform;
            foreach (var g in root.GetComponentsInChildren<XRGrabInteractable>(true))
                g.selectEntered.AddListener(a =>
                {
                    if (a.interactorObject is XRSocketInteractor) return;
                    Play(Tick, a.interactableObject.transform.position, 0.35f, Random.Range(0.92f, 1.08f));
                });
        }

        public static void ButtonClick(Component c, Vector3 pos) => Play(Click, pos, 0.5f, 1f);

        public static void Placed(Component c, Vector3 pos)
        {
            Play(Pop, pos, 0.6f, Random.Range(0.95f, 1.05f));
            var m = RamenGameManager.For(c);
            if (m && m.confettiMaterial) Sparkle(pos, m.confettiMaterial);
        }

        static void Play(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            if (!clip) return;
            var go = new GameObject("FxSound");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.pitch = pitch;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f; s.maxDistance = 10f; s.dopplerLevel = 0f;
            s.Play();
            Destroy(go, clip.length / pitch + 0.1f);
        }

        static void Sparkle(Vector3 pos, Material mat)
        {
            var go = new GameObject("Sparkle");
            go.transform.position = pos + Vector3.up * 0.05f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f; main.loop = false; main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f), Color.white);
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.06f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime; col.enabled = true; col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
            Destroy(go, 1.5f);
        }

        // ---- sound makers ----
        static System.Random rnd = new System.Random(7);
        static float Noise() => (float)(rnd.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = (int)(Rate * seconds);
            var d = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = f(t) * Mathf.Min(1f, t / 0.002f); }
            return ToClip(name, d);
        }

        static AudioClip MakePop()
        {
            int n = (int)(Rate * 0.14f);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = 300f + 600f * Mathf.Exp(-t * 35f);
                phase += 2.0 * Mathf.PI * freq / Rate;
                d[i] = (float)System.Math.Sin(phase) * Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 28f) * 0.7f;
            }
            return ToClip("pop", d);
        }

        /// soft bell notes one after another (no clicks: short fade-in, smooth decay)
        static AudioClip Chime(float[] notes, float gap, float tail, float volume, string name)
        {
            int n = (int)(Rate * (gap * (notes.Length - 1) + tail));
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * gap * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate, w = 6.2832f * notes[k] * t;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 5f);
                    d[i] += (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w) * Mathf.Exp(-t * 8f) + 0.1f * Mathf.Sin(3f * w) * Mathf.Exp(-t * 14f)) * env;
                }
            }
            float peak = 0.0001f;
            foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] = d[i] / peak * volume * Mathf.Min(1f, (n - i) / (Rate * 0.05f));
            return ToClip(name, d);
        }

        static AudioClip ToClip(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
