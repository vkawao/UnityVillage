using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RamenVR
{
    /// Keeps track of the 10 ramen-shop actions, drives the checklist board,
    /// the floating HUD, the guide arrows and the finish screen.
    public class RamenGameManager : MonoBehaviour
    {
        public static RamenGameManager Instance { get; private set; }

        /// every shop in the scene registers here, so many shops can live in one village
        static readonly System.Collections.Generic.List<RamenGameManager> all = new System.Collections.Generic.List<RamenGameManager>();
        public static int Count => all.Count;

        /// The manager of the shop that object c belongs to.
        public static RamenGameManager For(Component c)
        {
            if (c == null) return all.Count == 1 ? all[0] : Instance;
            // items can be moved out of the shop's hierarchy by XRI when grabbed/placed,
            // so each manager remembers every object that started inside its shop
            for (var t = c.transform; t != null; t = t.parent)
                foreach (var m in all)
                    if (m && m.members.Contains(t)) return m;
            foreach (var m in all)
                if (m && m.shopRoot && c.transform.IsChildOf(m.shopRoot)) return m;
            return all.Count == 1 ? all[0] : Instance;
        }

        [Header("Shop")]
        [Tooltip("Root object of this shop (everything of the shop is under it)")]
        public Transform shopRoot;
        [Tooltip("The NEXT panel and arrows only show when the player is this close to the shop (metres)")]
        public float showDistance = 3f;

        readonly System.Collections.Generic.HashSet<Transform> members = new System.Collections.Generic.HashSet<Transform>();

        public static readonly string[] Labels =
        {
            "Ring the doorbell",
            "Press MENU, then START ORDER",
            "Put a bowl next to the pot",
            "Add the noodles",
            "Add the egg",
            "Add the pork (chashu)",
            "Add the spring onion",
            "Press the SOUP button",
            "Place the chopsticks",
            "Serve the bowl!"
        };

        [Header("Steps")]
        [Tooltip("The 10 step names of this shop (empty = the ramen steps)")]
        public string[] stepLabels;
        public string[] Steps => stepLabels != null && stepLabels.Length > 0 ? stepLabels : Labels;

        [Header("Checklist board")]
        public Image[] rowBg;
        public Image[] rowDot;
        public Text[] rowNum;
        public Text[] rowLabel;
        public RectTransform progressFill;
        public Text progressText;
        public Text hintText;

        [Header("Floating HUD")]
        public Text hudText;

        [Header("Panels")]
        public GameObject orderPanel;
        public GameObject donePanel;
        public Text doneStatsText;
        public Material confettiMaterial;

        [Header("Guide arrows")]
        public Transform pickArrow;     // orange "THIS"
        public Transform placeArrow;    // green "PUT HERE"
        [Tooltip("Used for a step once its first pick target is already placed (e.g. basket done, now the egg tray)")]
        public Transform[] pickTargetsAlt;
        public Transform[] placeTargetsAlt;
        public Transform[] pickTargets;
        public Transform[] placeTargets;

        static readonly Color Done = new Color(0.30f, 0.85f, 0.45f);
        static readonly Color Current = new Color(1.00f, 0.62f, 0.20f);
        static readonly Color Pending = new Color(0.35f, 0.35f, 0.38f);

        bool[] done;
        Coroutine hintRoutine;
        float startTime = -1f;
        AudioSource audioSrc;
        AudioClip ding, fanfare;

        void Awake()
        {
            Instance = this;
            if (!all.Contains(this)) all.Add(this);
            if (!shopRoot) shopRoot = transform.parent ? transform.parent : transform;
            foreach (var t in shopRoot.GetComponentsInChildren<Transform>(true)) members.Add(t);
            done = new bool[Steps.Length];
            if (orderPanel) orderPanel.SetActive(false);
            if (donePanel) donePanel.SetActive(false);
            if (hintText) hintText.text = "";
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.spatialBlend = 0f;
            ding = HouseFx.Ding; audioSrc.volume = 0.8f; if (!GetComponent<HouseFx>()) gameObject.AddComponent<HouseFx>();
            fanfare = HouseFx.Fanfare;
            Refresh();
        }

        void OnDestroy() { all.Remove(this); if (Instance == this) Instance = all.Count > 0 ? all[0] : null; }

        Bounds shopBounds; bool haveBounds; GameObject hudRoot;

        void Start()
        {
            foreach (var rd in shopRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (rd is ParticleSystemRenderer || rd.GetComponentInParent<Canvas>(true) || rd.name == "Ground") continue;   // the ground plane is not part of the shop
                if (!haveBounds) { shopBounds = rd.bounds; haveBounds = true; } else shopBounds.Encapsulate(rd.bounds);
            }
            if (hudText) { var lh = hudText.GetComponentInParent<LazyHud>(true); hudRoot = lh ? lh.gameObject : null; }
        }

        /// true while the player is inside the shop or standing near it
        public bool PlayerNear()
        {
            var cam = Camera.main;
            if (!cam || !haveBounds) return true;
            Vector3 p = cam.transform.position; p.y = shopBounds.center.y;
            return shopBounds.SqrDistance(p) <= showDistance * showDistance;
        }

        public bool IsDone(int index) => index >= 0 && index < done.Length && done[index];

        public bool AllDone(int[] indices)
        {
            if (indices == null) return true;
            foreach (var i in indices) if (!IsDone(i)) return false;
            return true;
        }

        public void Complete(int index)
        {
            if (index < 0 || index >= done.Length || done[index]) return;
            done[index] = true;
            if (index == 0) startTime = Time.time;
            bool all = true;
            foreach (var d in done) if (!d) all = false;
            if (all)
            {
                audioSrc.PlayOneShot(fanfare);
                StartCoroutine(Celebrate());
            }
            else
            {
                audioSrc.PlayOneShot(ding);
                Hint("Nice! Step " + (index + 1) + " done");
            }
            Refresh();
        }

        public void ShowOrderPanel()
        {
            if (orderPanel) orderPanel.SetActive(true);
        }

        public void StartOrder()
        {
            if (!IsDone(0)) return;
            if (orderPanel) orderPanel.SetActive(false);
            Complete(1);
        }

        public void Hint(string message)
        {
            if (!hintText) return;
            if (hintRoutine != null) StopCoroutine(hintRoutine);
            hintRoutine = StartCoroutine(ShowHint(message));
        }

        IEnumerator ShowHint(string message)
        {
            hintText.text = message;
            yield return new WaitForSeconds(2.5f);
            hintText.text = "";
        }

        int NextStep()
        {
            for (int i = 0; i < done.Length; i++) if (!done[i]) return i;
            return -1;
        }

        void Refresh()
        {
            int count = 0;
            foreach (var d in done) if (d) count++;
            int next = NextStep();

            for (int i = 0; i < Steps.Length; i++)
            {
                bool isDone = done[i], isCur = i == next;
                if (rowDot != null && i < rowDot.Length && rowDot[i]) rowDot[i].color = isDone ? Done : isCur ? Current : Pending;
                if (rowNum != null && i < rowNum.Length && rowNum[i]) rowNum[i].text = isDone ? "OK" : (i + 1).ToString();
                if (rowLabel != null && i < rowLabel.Length && rowLabel[i])
                {
                    rowLabel[i].color = isDone ? new Color(0.6f, 0.75f, 0.62f) : isCur ? Color.white : new Color(0.62f, 0.62f, 0.66f);
                    rowLabel[i].fontStyle = isCur ? FontStyle.Bold : FontStyle.Normal;
                }
                if (rowBg != null && i < rowBg.Length && rowBg[i]) rowBg[i].color = isCur ? new Color(1f, 0.62f, 0.2f, 0.22f) : new Color(1, 1, 1, 0.03f);
            }
            if (progressFill) progressFill.anchorMax = new Vector2(count / (float)Steps.Length, 1f);
            if (progressText) progressText.text = $"{count} / {Steps.Length}";
            if (hudText) hudText.text = next < 0 ? "ORDER COMPLETE!" : $"NEXT  >  {Steps[next]}";
        }

        // ---- 3D sound ping: you can hear where the next thing is ----
        AudioSource pingSrc;
        AudioClip pingClip;
        float nextPing;
        int lastPingStep = -2;

        void Update()
        {
            int next = NextStep();
            bool near = PlayerNear();
            if (hudRoot && hudRoot.activeSelf != near) hudRoot.SetActive(near);
            if (!near) next = -1;   // far away: hide this shop's arrows
            float bob = Mathf.Sin(Time.time * 4f) * 0.04f;
            bool orderOpen = next == 1 && orderPanel && orderPanel.activeSelf;   // the order panel itself is the guide
            Transform pick = !orderOpen && next >= 0 && pickTargets != null && next < pickTargets.Length ? pickTargets[next] : null;
            Transform place = next >= 0 && placeTargets != null && next < placeTargets.Length ? placeTargets[next] : null;
            if (pick != null && Placed(pick) && pickTargetsAlt != null && next < pickTargetsAlt.Length && pickTargetsAlt[next] != null)
            {
                pick = pickTargetsAlt[next];
                place = placeTargetsAlt != null && next < placeTargetsAlt.Length ? placeTargetsAlt[next] : null;
            }
            if (pick != null && Placed(pick)) { pick = null; place = null; }
            UpdateArrow(pickArrow, pick, bob);
            UpdateArrow(placeArrow, place, -bob);

            // (guide sound removed - arrows only)
        }

        // an item that was put in a locking spot loses its grab component
        static bool Placed(Transform t) => t.GetComponentInParent<RamenSocket>() != null && t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() == null;

        static void UpdateArrow(Transform arrow, Transform target, float bob)
        {
            if (!arrow) return;
            bool show = target != null;
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
            if (!show) return;
            Vector3 top = target.position;
            var rs = target.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) if (r.enabled && r.gameObject.activeInHierarchy) b.Encapsulate(r.bounds);
                top = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            Vector3 toCam = Camera.main ? Camera.main.transform.position - top : Vector3.zero; toCam.y = 0;
            if (toCam.sqrMagnitude > 0.0001f) toCam = toCam.normalized * 0.18f;
            arrow.position = top + toCam + Vector3.up * (0.25f + bob);
        }

        IEnumerator Celebrate()
        {
            float secs = startTime >= 0 ? Time.time - startTime : 0f;
            if (doneStatsText) doneStatsText.text = $"10 / 10 steps          Time  {(int)secs / 60}:{(int)secs % 60:00}";
            if (pickArrow) pickArrow.gameObject.SetActive(false);
            if (placeArrow) placeArrow.gameObject.SetActive(false);
            if (!donePanel) yield break;
            hiddenForDone.Clear();
            foreach (Transform sib in donePanel.transform.parent)
                if (sib != donePanel.transform && sib.gameObject.activeSelf) { sib.gameObject.SetActive(false); hiddenForDone.Add(sib.gameObject); }
            donePanel.transform.SetAsLastSibling();
            donePanel.SetActive(true);
            SpawnConfetti(donePanel.transform.position);
            var t = donePanel.transform;
            Vector3 full = t.localScale;
            for (float k = 0; k < 0.35f; k += Time.deltaTime)
            {
                t.localScale = full * Mathf.Max(0.01f, Mathf.Sin(k / 0.35f * Mathf.PI * 0.5f) * 1.08f);
                yield return null;
            }
            for (float k = 0; k < 0.15f; k += Time.deltaTime)
            {
                t.localScale = full * Mathf.Lerp(1.08f, 1f, k / 0.15f);
                yield return null;
            }
            t.localScale = full;
        }

        readonly System.Collections.Generic.List<GameObject> hiddenForDone = new System.Collections.Generic.List<GameObject>();

        /// Village mode: close the finish screen and show the (finished) checklist again.
        public void CloseDonePanel()
        {
            if (donePanel) donePanel.SetActive(false);
            foreach (var g in hiddenForDone) if (g) g.SetActive(true);
            hiddenForDone.Clear();
        }

        void SpawnConfetti(Vector3 pos)
        {
            var go = new GameObject("Confetti");
            go.transform.position = pos + Vector3.down * 0.2f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f; main.loop = false;
            main.startLifetime = 2.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.gravityModifier = 0.6f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.2f), new Color(1f, 0.85f, 0.3f));
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 180) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (confettiMaterial) r.sharedMaterial = confettiMaterial;
            ps.Play();
            Destroy(go, 4f);
        }

        static AudioClip MakeTone(float[] notes, float noteLen)
        {
            int rate = 44100, per = (int)(rate * noteLen);
            var data = new float[per * notes.Length];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)rate;
                    data[n * per + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * 18f) * 0.35f;
                }
            var clip = AudioClip.Create("tone", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
