using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// Button on the finish screen.
    /// Only one shop in the scene: PLAY AGAIN reloads the scene.
    /// Several shops (the village): it becomes CLOSE, so the other houses keep their progress.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class RestartButton : MonoBehaviour
    {
        bool busy;
        float last = -10f;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Restart());
            if (!TryGetComponent<HoverTriggerClick>(out var click)) click = gameObject.AddComponent<HoverTriggerClick>();
            click.onClick.AddListener(Restart);
        }

        void Start()
        {
            if (RamenGameManager.Count > 1)
                foreach (var t in GetComponentsInChildren<Text>(true)) t.text = "CLOSE";
        }

        public void Restart()
        {
            if (busy || Time.time - last < 0.5f) return;
            last = Time.time;
            if (RamenGameManager.Count > 1)
            {
                RamenGameManager.For(this)?.CloseDonePanel();
                return;
            }
            busy = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
