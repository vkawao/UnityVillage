using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: tune every listed guitar. Each GuitarTuner becomes active when this
    /// step starts; the step completes when all of them are in tune.
    /// </summary>
    public class TuneGuitarsStep : QuestStep
    {
        [SerializeField] private GuitarTuner[] m_Guitars;

        [Tooltip("Hint shown above the tuned counter.")]
        [SerializeField] private string m_Hint = "Pick up or shake each guitar until it sounds right";

        protected override void OnBegin()
        {
            if (m_Guitars == null || m_Guitars.Length == 0)
            {
                Debug.LogWarning("[Quest] TuneGuitarsStep has no guitars assigned.", this);
                return;
            }

            foreach (GuitarTuner g in m_Guitars)
            {
                if (g == null) continue;
                g.Tuned += OnGuitarTuned;
                g.SetTuningActive(true);
            }

            Refresh();
        }

        protected override void OnEnd()
        {
            if (m_Guitars == null) return;
            foreach (GuitarTuner g in m_Guitars)
            {
                if (g == null) continue;
                g.Tuned -= OnGuitarTuned;
                g.SetTuningActive(false);
            }
        }

        private void OnGuitarTuned(GuitarTuner _) => Refresh();

        private void Refresh()
        {
            int total = 0, tuned = 0;
            foreach (GuitarTuner g in m_Guitars)
            {
                if (g == null) continue;
                total++;
                if (g.IsTuned) tuned++;
            }

            SetProgress($"{m_Hint}\nTuned {tuned}/{total}");
            if (tuned == total) Complete();
        }
    }
}
