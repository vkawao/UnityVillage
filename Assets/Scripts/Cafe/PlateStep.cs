using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: plate up. The coffee goes on a saucer, and a cake from the pastry case on another one.
    /// Completes when both are sitting on plates.
    /// </summary>
    public class PlateStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [Tooltip("The step that brewed the coffee (gives the cup).")]
        [SerializeField] private BrewStep m_Brew;
        [SerializeField] private PlateItem[] m_Cakes;
        [SerializeField] private PlateHolder[] m_Plates;

        private int m_LastState = -1;

        public PlateItem Cup => m_Brew != null && m_Brew.ChosenItem != null ? m_Brew.ChosenItem.GetComponent<PlateItem>() : null;

        /// <summary>The cake that's on a plate (the first one found), or null.</summary>
        public PlateItem PlatedCake
        {
            get
            {
                if (m_Cakes == null) return null;
                foreach (PlateItem cake in m_Cakes)
                {
                    if (cake != null && cake.Plate != null) return cake;
                }
                return null;
            }
        }

        public override Transform GuideTarget
        {
            get
            {
                PlateItem cup = Cup;
                if (cup != null && cup.Plate == null) return GrabUtil.IsHeldByHand(cup.Grab) ? FreePlate(cup.transform.position) : cup.transform;
                if (PlatedCake == null && m_Cakes != null)
                {
                    foreach (PlateItem cake in m_Cakes)
                    {
                        if (cake != null && GrabUtil.IsHeldByHand(cake.Grab)) return FreePlate(cake.transform.position);
                    }
                    foreach (PlateItem cake in m_Cakes)
                    {
                        if (cake != null && cake.isActiveAndEnabled) return cake.transform;
                    }
                }
                return base.GuideTarget;
            }
        }

        protected override void OnBegin()
        {
            m_LastState = -1;
            Check();
        }

        private void Update()
        {
            if (IsActive) Check();
        }

        private void Check()
        {
            PlateItem cup = Cup;
            bool cupDone = cup != null && cup.Plate != null;
            bool cakeDone = PlatedCake != null;
            if (cupDone && cakeDone)
            {
                SetProgress($"<color={OnColor}>Looks delicious!</color>");
                Complete();
                return;
            }

            int state = (cupDone ? 1 : 0) + (cakeDone ? 2 : 0);
            if (state == m_LastState) return;
            m_LastState = state;
            string hint = !cupDone
                ? "Set your cup down on one of the little white plates"
                : "Take a cake from the glass case and set it on another plate";
            SetProgress(
                $"<color={(cupDone ? OnColor : OffColor)}><b>CUP</b> {(cupDone ? "ON A PLATE" : "NOT YET")}</color>" +
                $"<space=1.2em><color={(cakeDone ? OnColor : OffColor)}><b>CAKE</b> {(cakeDone ? "ON A PLATE" : "NOT YET")}</color>\n" +
                $"<color={TextColor}><i>{hint}</i></color>");
        }

        private Transform FreePlate(Vector3 near)
        {
            Transform best = base.GuideTarget;
            float bestDist = float.MaxValue;
            if (m_Plates == null) return best;
            foreach (PlateHolder plate in m_Plates)
            {
                if (plate == null || plate.Item != null) continue;
                float d = Vector3.Distance(plate.transform.position, near);
                if (d < bestDist) { bestDist = d; best = plate.transform; }
            }
            return best;
        }
    }
}
