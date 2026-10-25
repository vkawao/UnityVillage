using System.Collections.Generic;
using UnityEngine;

namespace VRVillage
{
    /// <summary>Action: bring the amp found in the amp test to the counter.</summary>
    public class WorkingAmpToCounterStep : ItemToCounterStep
    {
        [SerializeField] private AmpTestStep m_AmpTest;

        protected override IEnumerable<CounterItem> Candidates
        {
            get
            {
                AmpUnit amp = m_AmpTest != null ? m_AmpTest.WorkingAmp : null;
                if (amp != null) yield return amp.GetComponent<CounterItem>();
            }
        }
    }
}
