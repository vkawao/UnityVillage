using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Hides objects when play starts (they stay visible in the editor so you can place them).
    /// Steps show them later, e.g. a can when it's dispensed.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class QuestHideAtStart : MonoBehaviour
    {
        [SerializeField] private GameObject[] m_Hide;

        private void Awake()
        {
            if (m_Hide == null) return;
            foreach (GameObject go in m_Hide)
            {
                if (go != null) go.SetActive(false);
            }
        }
    }
}
