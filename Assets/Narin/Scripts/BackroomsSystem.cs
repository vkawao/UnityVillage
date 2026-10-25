using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Automatically turns the Flashlight spotlight ON when picked up (selectEntered)
    /// and OFF when dropped/put down (selectExited), and configures an attachTransform
    /// on XRGrabInteractable so the flashlight barrel and spotlight always point FORWARD away from the player!
    /// </summary>
    public class FlashlightAutoLightController : MonoBehaviour
    {
        public Light spotlightBeam;
        public Text statusLabel;
        private XRGrabInteractable _grab;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab != null)
            {
                Transform attachT = transform.Find("Flashlight_HandAttach");
                if (attachT == null)
                {
                    GameObject attachGO = new GameObject("Flashlight_HandAttach");
                    attachGO.transform.SetParent(transform, false);
                    attachT = attachGO.transform;
                }
                attachT.localPosition = new Vector3(0f, 0f, 0.16f);
                attachT.localRotation = Quaternion.Euler(0f, 180f, 0f);

                _grab.useDynamicAttach = false;
                _grab.matchAttachPosition = true;
                _grab.matchAttachRotation = true;
                _grab.attachTransform = attachT;
            }
        }

        private void Start()
        {
            if (spotlightBeam == null)
                spotlightBeam = GetComponentInChildren<Light>(true);

            if (spotlightBeam != null)
                spotlightBeam.enabled = false;
            UpdateStatusText();

            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            if (_grab != null)
            {
                _grab.selectEntered.AddListener(OnPickedUp);
                _grab.selectExited.AddListener(OnPutDown);
                _grab.activated.AddListener(_ => ToggleLight());
            }
        }

        private void OnPickedUp(SelectEnterEventArgs args)
        {
            SetLightState(true);
            if (VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.CompleteAction(5, 3);
                VillageActionManager.Instance.CompleteAction(5, 4);
            }
        }

        private void OnPutDown(SelectExitEventArgs args)
        {
            SetLightState(false);
        }

        public void ToggleLight()
        {
            if (spotlightBeam != null)
                SetLightState(!spotlightBeam.enabled);
        }

        public void SetLightState(bool on)
        {
            if (spotlightBeam != null)
                spotlightBeam.enabled = on;
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (statusLabel != null && spotlightBeam != null)
            {
                statusLabel.text = spotlightBeam.enabled
                    ? "BACKROOMS TERMINAL | LIGHT: ON"
                    : "BACKROOMS TERMINAL | LIGHT: OFF";
            }
        }
    }
}
