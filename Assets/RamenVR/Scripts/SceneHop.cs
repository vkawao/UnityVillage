using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// Press (ray + trigger) to walk over to the next house (loads its scene).
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SceneHop : MonoBehaviour
    {
        public string sceneName;
        bool going;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Go());
        }

        public void Go()
        {
            if (going || Time.timeSinceLevelLoad < 1f || string.IsNullOrEmpty(sceneName)) return;
            going = true;
            SceneManager.LoadScene(sceneName);
        }
    }
}
