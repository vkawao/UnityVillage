using UnityEngine;
using Unity.XR.CoreUtils;

namespace NarinVillage
{
    /// <summary>
    /// Enforces CharacterController wall/floor/door collision and gravity even when moving
    /// the HMD (Main Camera) via WASD in the XR Interaction Simulator, and keeps Main Camera
    /// horizontally aligned with XROrigin so teleportation is always 100% accurate.
    /// </summary>
    public class VRPlayerCollisionEnforcer : MonoBehaviour
    {
        [Tooltip("Adjusts WASD movement speed in XR Interaction Simulator (default 1.25)")]
        public float movementSpeedMultiplier = 1.25f;

        private XROrigin _xrOrigin;
        private CharacterController _cc;
        private Vector3 _lastCamLocalPos;
        private bool _hasLastCamPos = false;

        private void Awake()
        {
            _xrOrigin = GetComponentInChildren<XROrigin>();
            if (_xrOrigin == null)
            {
                _xrOrigin = FindFirstObjectByType<XROrigin>();
            }

            _cc = GetComponent<CharacterController>();
            if (_cc == null && _xrOrigin != null)
            {
                _cc = _xrOrigin.GetComponent<CharacterController>();
            }
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.radius = 0.28f;
                _cc.height = 1.7f;
                _cc.center = new Vector3(0f, 0.85f, 0f);
                _cc.stepOffset = 0.35f;
                _cc.slopeLimit = 50f;
            }
        }

        public void ResetVerticalVelocity()
        {
            if (_xrOrigin != null && _xrOrigin.Camera != null)
            {
                Transform camT = _xrOrigin.Camera.transform;
                _lastCamLocalPos = camT.localPosition;
                _hasLastCamPos = true;
                if (camT.parent != null && camT.parent != transform)
                {
                    camT.parent.localPosition = new Vector3(-_lastCamLocalPos.x, camT.parent.localPosition.y, -_lastCamLocalPos.z);
                }
            }
        }

        private void LateUpdate()
        {
            if (_xrOrigin == null || _xrOrigin.Camera == null || _cc == null || !_cc.enabled)
                return;

            // Safety guard: never allow the player to fall below ground level (Y = 0) into the void!
            if (transform.position.y < -0.35f)
            {
                _cc.enabled = false;
                transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
                Physics.SyncTransforms();
                _cc.enabled = true;
                return;
            }

            Transform camT = _xrOrigin.Camera.transform;
            Vector3 localPos = camT.localPosition;

            if (!_hasLastCamPos)
            {
                _lastCamLocalPos = localPos;
                _hasLastCamPos = true;
            }

            // Compute ONLY the frame-to-frame delta from XRDeviceSimulator / TrackedPoseDriver
            // so releasing WASD stops movement immediately (zero ice/sliding!)
            Vector3 deltaLocal = new Vector3(localPos.x - _lastCamLocalPos.x, 0f, localPos.z - _lastCamLocalPos.z);
            _lastCamLocalPos = localPos;

            Transform parentT = camT.parent != null ? camT.parent : transform;

            // Keep Camera Offset counter-shifted so Main Camera stays horizontally centered over XROrigin's CharacterController
            if (parentT != transform)
            {
                parentT.localPosition = new Vector3(-localPos.x, parentT.localPosition.y, -localPos.z);
            }

            // Only call CharacterController.Move when moving horizontally so standing still never bounces vertically!
            if (deltaLocal.sqrMagnitude > 0.0000001f)
            {
                deltaLocal = Vector3.ClampMagnitude(deltaLocal, 0.35f) * movementSpeedMultiplier;
                Vector3 worldMove = transform.TransformVector(deltaLocal);
                worldMove.y = 0f;
                _cc.Move(worldMove);
            }
        }
    }
}
