using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    [Serializable]
    public class PlacementTargetRule
    {
        public string targetNameContains;
        public int houseNumber;
        public int actionNumber;
        public bool attachToTargetOnPlace = false;
        public string specialSoundType = ""; // "Sizzle", "Ratchet", "Chime"
    }

    /// <summary>
    /// Universal VR Grab + Placement/Stacking/Tool-Contact handler.
    /// Supports open-top hollow containers (Basket, Drawer) using 5 thin wall/floor BoxColliders
    /// so items can be physically dropped inside without hitting a convex hull lid.
    /// </summary>
    public class GrabAndPlaceItem : MonoBehaviour
    {
        [Header("Grab Action (Optional)")]
        public bool completeActionOnGrab = false;
        public int grabHouseNumber = 1;
        public int grabActionNumber = 1;

        [Header("Hollow Container Mode (e.g. Shopping Basket)")]
        public bool isHollowContainer = false;

        [Header("Placement / Stacking / Tool Rules")]
        public List<PlacementTargetRule> placementRules = new List<PlacementTargetRule>();
        [Header("Custom Sound Override")]
        [Tooltip("Drag & drop any custom AudioClip here (.wav, .mp3, .ogg) to override procedural sizzle/ratchet.")]
        public AudioClip customPlaceSound;

        private XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;
        private AudioSource _audioSource;
        private bool _isHeld = false;
        // Several items start out touching their target (pan on stove, stacked rims, basket on table);
        // only contacts after the player has picked the item up count as placing it.
        private bool _wasGrabbed = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (isHollowContainer ||
                gameObject.name.IndexOf("Basket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gameObject.name.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gameObject.name.IndexOf("Bin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isHollowContainer = true;
                BuildHollowOpenTopColliders(gameObject);

                Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
                _rb.centerOfMass = -localUp * 0.15f;
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col == null)
                {
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                        bc.center = mf.sharedMesh.bounds.center;
                        bc.size = mf.sharedMesh.bounds.size;
                    }
                    else
                    {
                        MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                        mc.convex = true;
                    }
                }
                else if (col is MeshCollider meshCol)
                {
                    meshCol.convex = true;
                }
            }

            _grabInteractable = GetComponent<XRGrabInteractable>();
            if (_grabInteractable == null)
                _grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

            _grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            _grabInteractable.throwOnDetach = true;
            _grabInteractable.interactionLayers = 1;
            _grabInteractable.retainTransformParent = false;

            SyncGrabColliders(gameObject);

            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnReleased);
            _grabInteractable.selectExited.AddListener(OnReleased);

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.7f;
            _audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Synchronizes the non-trigger colliders with the XRGrabInteractable's collider list.
        /// </summary>
        public static void SyncGrabColliders(GameObject containerGO)
        {
            if (containerGO == null) return;
            var grab = containerGO.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.colliders.Clear();
                foreach (Collider c in containerGO.GetComponents<Collider>())
                {
                    if (c != null && !c.isTrigger && c.enabled)
                    {
                        grab.colliders.Add(c);
                    }
                }
            }
        }

        /// <summary>
        /// Replaces any solid/convex collider on a container (Basket or Drawer) with 5 thin BoxColliders
        /// (Bottom floor + 4 side walls) and 1 interior Trigger BoxCollider so the top is completely open!
        /// </summary>
        public static void BuildHollowOpenTopColliders(GameObject containerGO)
        {
            MeshFilter mf = containerGO.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // If 5 or more BoxColliders are already present, hollow colliders are already built!
            BoxCollider[] existingBoxes = containerGO.GetComponents<BoxCollider>();
            if (existingBoxes != null && existingBoxes.Length >= 5)
            {
                SyncGrabColliders(containerGO);
                return;
            }

            foreach (Collider existing in containerGO.GetComponents<Collider>())
            {
                existing.enabled = false;
                if (Application.isPlaying) Destroy(existing);
                else DestroyImmediate(existing);
            }

            Bounds b = mf.sharedMesh.bounds;
            Vector3 c = b.center;
            Vector3 s = b.size;
            float wallThickX = Mathf.Max(s.x * 0.08f, 0.015f);
            float wallThickY = Mathf.Max(s.y * 0.08f, 0.015f);
            float wallThickZ = Mathf.Max(s.z * 0.08f, 0.015f);

            // Note: Blender meshes have localRotation (-90, 0, 0), so local Z is vertical (Up/Down in world)
            // or if rotation was baked, local Y is vertical. Detect which local axis is aligned with World Up!
            Vector3 localUp = containerGO.transform.InverseTransformDirection(Vector3.up);
            bool zIsUp = Mathf.Abs(localUp.z) > Mathf.Abs(localUp.y) && Mathf.Abs(localUp.z) > Mathf.Abs(localUp.x);

            if (zIsUp)
            {
                float signZ = Mathf.Sign(localUp.z);
                float bottomZ = c.z - signZ * (s.z * 0.5f - wallThickZ * 0.5f);

                // 1. Bottom Floor
                BoxCollider floor = containerGO.AddComponent<BoxCollider>();
                floor.center = new Vector3(c.x, c.y, bottomZ);
                floor.size = new Vector3(s.x, s.y, wallThickZ);

                // 2. Left Wall (-X)
                BoxCollider left = containerGO.AddComponent<BoxCollider>();
                left.center = new Vector3(c.x - s.x * 0.5f + wallThickX * 0.5f, c.y, c.z);
                left.size = new Vector3(wallThickX, s.y, s.z);

                // 3. Right Wall (+X)
                BoxCollider right = containerGO.AddComponent<BoxCollider>();
                right.center = new Vector3(c.x + s.x * 0.5f - wallThickX * 0.5f, c.y, c.z);
                right.size = new Vector3(wallThickX, s.y, s.z);

                // 4. Front Wall (-Y)
                BoxCollider front = containerGO.AddComponent<BoxCollider>();
                front.center = new Vector3(c.x, c.y - s.y * 0.5f + wallThickY * 0.5f, c.z);
                front.size = new Vector3(s.x, wallThickY, s.z);

                // 5. Back Wall (+Y)
                BoxCollider back = containerGO.AddComponent<BoxCollider>();
                back.center = new Vector3(c.x, c.y + s.y * 0.5f - wallThickY * 0.5f, c.z);
                back.size = new Vector3(s.x, wallThickY, s.z);

                // 6. Interior Trigger Volume
                BoxCollider trig = containerGO.AddComponent<BoxCollider>();
                trig.isTrigger = true;
                trig.center = c;
                trig.size = new Vector3(s.x * 0.85f, s.y * 0.85f, s.z * 0.85f);
            }
            else
            {
                float bottomY = c.y - (s.y * 0.5f - wallThickY * 0.5f);

                // 1. Bottom Floor
                BoxCollider floor = containerGO.AddComponent<BoxCollider>();
                floor.center = new Vector3(c.x, bottomY, c.z);
                floor.size = new Vector3(s.x, wallThickY, s.z);

                // 2. Left Wall (-X)
                BoxCollider left = containerGO.AddComponent<BoxCollider>();
                left.center = new Vector3(c.x - s.x * 0.5f + wallThickX * 0.5f, c.y, c.z);
                left.size = new Vector3(wallThickX, s.y, s.z);

                // 3. Right Wall (+X)
                BoxCollider right = containerGO.AddComponent<BoxCollider>();
                right.center = new Vector3(c.x + s.x * 0.5f - wallThickX * 0.5f, c.y, c.z);
                right.size = new Vector3(wallThickX, s.y, s.z);

                // 4. Front Wall (-Z)
                BoxCollider front = containerGO.AddComponent<BoxCollider>();
                front.center = new Vector3(c.x, c.y, c.z - s.z * 0.5f + wallThickZ * 0.5f);
                front.size = new Vector3(s.x, s.y, wallThickZ);

                // 5. Back Wall (+Z)
                BoxCollider back = containerGO.AddComponent<BoxCollider>();
                back.center = new Vector3(c.x, c.y, c.z + s.z * 0.5f - wallThickZ * 0.5f);
                back.size = new Vector3(s.x, s.y, wallThickZ);

                // 6. Interior Trigger Volume
                BoxCollider trig = containerGO.AddComponent<BoxCollider>();
                trig.isTrigger = true;
                trig.center = c;
                trig.size = new Vector3(s.x * 0.85f, s.y * 0.85f, s.z * 0.85f);
            }

            SyncGrabColliders(containerGO);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _isHeld = true;
            _wasGrabbed = true;
            if (transform.parent != null &&
                (transform.parent.name.IndexOf("Basket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 transform.parent.name.IndexOf("Drawer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 transform.parent.name.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 transform.parent.name.IndexOf("Bin", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                transform.SetParent(transform.parent.parent, true);
            }

            if (completeActionOnGrab && VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.CompleteAction(grabHouseNumber, grabActionNumber);
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _isHeld = false;
            CheckNearbyTargets();
        }

        private void OnCollisionEnter(Collision collision)
        {
            EvaluateContact(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            EvaluateContact(other.gameObject);
        }

        private void CheckNearbyTargets()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.45f);
            foreach (var hit in hits)
            {
                if (hit.gameObject != gameObject)
                {
                    EvaluateContact(hit.gameObject);
                }
            }
        }

        private void EvaluateContact(GameObject otherObj)
        {
            if (otherObj == null || otherObj == gameObject) return;

            string otherName = otherObj.name;
            string parentName = otherObj.transform.parent != null ? otherObj.transform.parent.name : "";

            // Allow placing items back inside any Drawer or Trash/Basket without clipping out
            if (!_isHeld && (otherName.IndexOf("Drawer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             otherName.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             otherName.IndexOf("Bin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             otherName.IndexOf("Basket", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                transform.SetParent(otherObj.transform, true);
            }

            if (!_wasGrabbed) return;

            foreach (var rule in placementRules)
            {
                if (string.IsNullOrEmpty(rule.targetNameContains)) continue;

                bool match = otherName.IndexOf(rule.targetNameContains, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             parentName.IndexOf(rule.targetNameContains, StringComparison.OrdinalIgnoreCase) >= 0;

                if (match)
                {
                    if (VillageActionManager.Instance != null)
                    {
                        bool alreadyDone = VillageActionManager.Instance.IsActionCompleted(rule.houseNumber, rule.actionNumber);
                        VillageActionManager.Instance.CompleteAction(rule.houseNumber, rule.actionNumber);

                        if (!alreadyDone || !string.IsNullOrEmpty(rule.specialSoundType))
                        {
                            PlaySpecialSound(rule.specialSoundType);
                        }
                    }

                    if (rule.attachToTargetOnPlace && !_isHeld)
                    {
                        transform.SetParent(otherObj.transform, true);
                    }
                }
            }
        }

        private void PlaySpecialSound(string soundType)
        {
            if (_audioSource == null) return;

            if (customPlaceSound != null)
            {
                _audioSource.PlayOneShot(customPlaceSound, 0.9f);
                return;
            }

            if (string.IsNullOrEmpty(soundType)) return;

            if (soundType.Equals("Sizzle", StringComparison.OrdinalIgnoreCase))
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateSizzleSound(2.5f), 0.85f);
            }
            else if (soundType.Equals("Ratchet", StringComparison.OrdinalIgnoreCase))
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateRatchetSound(0.6f), 0.85f);
            }
        }
    }
}
