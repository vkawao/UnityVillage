using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: drink or eat something by holding it up to your mouth.
    ///   Drink: keep it at your mouth for a few seconds (gulps), then a satisfied sigh.
    ///   Eat:   a crunch every moment it's at your mouth; after a few munches the item
    ///          flattens (e.g. an empty snack bag) with a crumple.
    /// </summary>
    public class ConsumeStep : QuestStep
    {
        public enum Mode { Drink, Eat }

        [Tooltip("The item. Leave empty to use the item chosen by Item Source.")]
        [SerializeField] private XRGrabInteractable m_Item;
        [Tooltip("Optional: a step that decides the item (implements IQuestItemSource).")]
        [SerializeField] private MonoBehaviour m_ItemSource;
        [SerializeField] private Mode m_Mode = Mode.Drink;
        [Tooltip("How close (m) the item must be to the mouth.")]
        [SerializeField] private float m_MouthRadius = 0.22f;

        [Header("Drink")]
        [SerializeField] private float m_DrinkSeconds = 2.5f;
        [SerializeField] private float m_GulpInterval = 0.55f;

        [Header("Eat")]
        [SerializeField] private int m_Munches = 4;
        [SerializeField] private float m_MunchInterval = 0.65f;
        [Tooltip("Thickness left after eating (fraction of the original).")]
        [SerializeField] private float m_FlattenTo = 0.2f;

        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.9f;

        private Transform m_Head;
        private float m_Progress;
        private float m_NextSound;
        private int m_Count;

        private XRGrabInteractable Item => QuestItems.Resolve(m_Item, m_ItemSource);

        /// <summary>The item until it's in your hand (then nothing to point at but the panel).</summary>
        public override Transform GuideTarget
        {
            get
            {
                XRGrabInteractable item = Item;
                if (item == null || GrabUtil.IsHeldByHand(item) || !item.gameObject.activeInHierarchy) return base.GuideTarget;
                return item.transform;
            }
        }

        protected override void OnBegin()
        {
            m_Progress = 0f;
            m_Count = 0;
            m_NextSound = 0f;
            UpdateProgress(false);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (m_Head == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                m_Head = cam.transform;
            }

            XRGrabInteractable item = Item;
            if (item == null || !item.gameObject.activeInHierarchy) return;
            bool atMouth = GrabUtil.IsHeldByHand(item) &&
                           Vector3.Distance(Center(item), QuestItems.Mouth(m_Head)) < m_MouthRadius;

            if (m_Mode == Mode.Drink) UpdateDrink(item, atMouth);
            else UpdateEat(item, atMouth);
        }

        private void UpdateDrink(XRGrabInteractable item, bool atMouth)
        {
            if (atMouth)
            {
                m_Progress += Time.deltaTime;
                if (Time.time >= m_NextSound)
                {
                    m_NextSound = Time.time + m_GulpInterval;
                    SynthAudio.PlayAt(SynthSound.Gulp, m_Head.position, m_Volume);
                }
                if (m_Progress >= m_DrinkSeconds)
                {
                    SynthAudio.PlayAt(SynthSound.Sigh, m_Head.position, m_Volume);
                    SetProgress("<color=#6EE787>Ahh, refreshing! The can is empty.</color>");
                    Complete();
                    return;
                }
            }
            UpdateProgress(atMouth);
        }

        private void UpdateEat(XRGrabInteractable item, bool atMouth)
        {
            if (atMouth && Time.time >= m_NextSound)
            {
                m_NextSound = Time.time + m_MunchInterval;
                m_Count++;
                SynthAudio.PlayAt(SynthSound.Crunch, m_Head.position, m_Volume);
                if (m_Count >= m_Munches)
                {
                    SynthAudio.PlayAt(SynthSound.Crumple, item.transform.position, m_Volume);
                    StartCoroutine(Flatten(item.gameObject));
                    SetProgress("<color=#6EE787>All gone! The bag is empty.</color>");
                    Complete();
                    return;
                }
            }
            UpdateProgress(atMouth);
        }

        private void UpdateProgress(bool atMouth)
        {
            if (m_Mode == Mode.Drink)
            {
                int pct = Mathf.RoundToInt(100f * Mathf.Clamp01(m_Progress / m_DrinkSeconds));
                SetProgress(atMouth || pct > 0
                    ? $"Drinking... <color=#E6E6EA>{pct}%</color>"
                    : "Hold the can up to your mouth");
            }
            else
            {
                SetProgress(m_Count > 0
                    ? $"Munch! <color=#E6E6EA>{m_Count}/{m_Munches}</color>"
                    : "Hold the bag up to your mouth");
            }
        }

        private IEnumerator Flatten(GameObject go)
        {
            // While held, XRI re-applies the scale the item had when grabbed every frame;
            // stop it tracking scale so the item stays flat (also after being grabbed again).
            var grab = go.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.trackScale = false;

            Vector3 from = go.transform.localScale;
            Vector3 to = QuestItems.FlattenedScale(go, m_FlattenTo);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                if (go == null) yield break;
                go.transform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.35f));
                yield return null;
            }
            if (go != null) go.transform.localScale = to;
        }

        private static Vector3 Center(XRGrabInteractable item)
        {
            Renderer r = item.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : item.transform.position;
        }
    }
}
