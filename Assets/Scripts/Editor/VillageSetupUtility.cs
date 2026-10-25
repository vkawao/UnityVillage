using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage.Editor
{
    [InitializeOnLoad]
    public static class VillageSetupUtility
    {
        private const string SESSION_KEY = "VRVillage_AutoSetup_Ran_v3";

        static VillageSetupUtility()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(SESSION_KEY, false))
                {
                    SessionState.SetBool(SESSION_KEY, true);
                    SetupInteractions();
                }
            };
        }

        [MenuItem("VR Village/Setup Scene Interactions (Door, Guitar, Rack)")]
        public static void SetupInteractions()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Village")
            {
                var villageScene = EditorSceneManager.OpenScene("Assets/Village.unity", OpenSceneMode.Single);
                if (!villageScene.IsValid())
                {
                    Debug.LogError("[VR Village] Assets/Village.unity could not be opened!");
                    return;
                }
            }

            GameObject gc = GameObject.Find("GuitarCenter");
            if (gc == null)
            {
                Debug.LogError("[VR Village] 'GuitarCenter' root object not found in Village scene!");
                return;
            }

            // Unpack prefab if it is still a prefab instance to allow structural reparenting
            if (PrefabUtility.IsPartOfPrefabInstance(gc))
            {
                PrefabUtility.UnpackPrefabInstance(gc, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log("[VR Village] Unpacked GuitarCenter prefab instance for hierarchy configuration.");
            }

            Undo.RegisterFullObjectHierarchyUndo(gc, "Configure Village VR Interactions");

            // 1. Setup Environment & Floor Colliders
            SetupEnvironmentColliders(gc);

            // 2. Setup Storefront Door (Hinge + DoorMover + XR Interactable)
            SetupStorefrontDoor(gc);

            // 3. Setup Guitars (Grab Interactable + Rigidbody + Collider + Strummer)
            SetupGuitars(gc);

            // 4. Setup Wall Guitar Racks (Sockets + Snap Hangers)
            SetupWallGuitarRacks(gc);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[VR Village] Successfully configured Door, Guitars, and Wall Racks in Village scene!</color>");
        }

        private static void SetupEnvironmentColliders(GameObject gc)
        {
            string[] solidObjects = new string[] { "Floor", "Structure", "Back Wall", "Left Wall", "Right Wall", "Front Left Wall", "Front Right Wall", "Front Top Wall", "Counter" };
            foreach (var name in solidObjects)
            {
                Transform t = FindDeepChild(gc.transform, name);
                if (t != null)
                {
                    if (t.GetComponent<Collider>() == null)
                    {
                        var mc = t.gameObject.AddComponent<MeshCollider>();
                        mc.convex = false; // Static environment can be non-convex
                    }
                }
            }
        }

        private static void SetupStorefrontDoor(GameObject gc)
        {
            Transform doorGlass = FindDeepChild(gc.transform, "Door Glass");
            Transform doorHandle = FindDeepChild(gc.transform, "Door Handle");
            Transform doorTrim = FindDeepChild(gc.transform, "Door Bottom Trim");

            if (doorGlass == null)
            {
                Debug.LogWarning("[VR Village] 'Door Glass' not found!");
                return;
            }

            Renderer glassRenderer = doorGlass.GetComponent<Renderer>();
            Bounds bounds = glassRenderer != null ? glassRenderer.bounds : new Bounds(doorGlass.position, Vector3.one);

            // Determine hinge position: opposite side of the door handle
            float hingeX = bounds.min.x;
            if (doorHandle != null && doorHandle.position.x < bounds.center.x)
            {
                hingeX = bounds.max.x;
            }

            Vector3 hingeWorldPos = new Vector3(hingeX, bounds.min.y, bounds.center.z);

            // Find or create Door_Hinge parent
            Transform existingHinge = FindDeepChild(gc.transform, "Door_Hinge");
            GameObject doorHingeObj;
            if (existingHinge == null)
            {
                doorHingeObj = new GameObject("Door_Hinge");
                doorHingeObj.transform.position = hingeWorldPos;
                doorHingeObj.transform.rotation = gc.transform.rotation;
                doorHingeObj.transform.SetParent(gc.transform, true);

                // Reparent door components under hinge
                doorGlass.SetParent(doorHingeObj.transform, true);
                if (doorHandle != null) doorHandle.SetParent(doorHingeObj.transform, true);
                if (doorTrim != null) doorTrim.SetParent(doorHingeObj.transform, true);
            }
            else
            {
                doorHingeObj = existingHinge.gameObject;
            }

            // Ensure colliders
            if (doorHandle != null && doorHandle.GetComponent<Collider>() == null)
            {
                var handleCol = doorHandle.gameObject.AddComponent<BoxCollider>();
                Renderer hr = doorHandle.GetComponent<Renderer>();
                if (hr != null) handleCol.size = hr.localBounds.size * 1.5f; // Generous grab zone for VR
            }

            if (doorGlass.GetComponent<Collider>() == null)
            {
                doorGlass.gameObject.AddComponent<BoxCollider>();
            }

            // Setup XR Interactable & DoorMover on the handle and/or hinge
            GameObject interactTarget = doorHandle != null ? doorHandle.gameObject : doorHingeObj;
            
            var simpleInteractable = interactTarget.GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
            {
                simpleInteractable = interactTarget.AddComponent<XRSimpleInteractable>();
            }

            var mover = doorHingeObj.GetComponent<DoorMover>();
            if (mover == null)
            {
                mover = doorHingeObj.AddComponent<DoorMover>();
            }

            // Configure DoorMover properties via SerializedObject
            SerializedObject moverSO = new SerializedObject(mover);
            moverSO.FindProperty("m_HingeTransform").objectReferenceValue = doorHingeObj.transform;
            moverSO.FindProperty("m_ClosedRotation").vector3Value = Vector3.zero;
            // Outward 90-degree swing
            moverSO.FindProperty("m_OpenRotation").vector3Value = new Vector3(0f, -90f, 0f);
            moverSO.FindProperty("m_SwingDuration").floatValue = 0.85f;
            moverSO.ApplyModifiedProperties();

            Debug.Log("[VR Village] Storefront Door assembly configured with DoorMover and XRSimpleInteractable.");
        }

        private static void SetupGuitars(GameObject gc)
        {
            string[] guitarNames = new string[] { "RedStrat", "LesPaul", "YamahaGuitar", "TaylorGuitar", "LavaMeGuitar" };

            foreach (var gName in guitarNames)
            {
                Transform gTransform = FindDeepChild(gc.transform, gName);
                if (gTransform == null) continue;

                GameObject guitar = gTransform.gameObject;

                // Ensure Rigidbody
                Rigidbody rb = guitar.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = guitar.AddComponent<Rigidbody>();
                }
                rb.mass = 3.2f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Ensure BoxCollider fitted to guitar bounds
                BoxCollider col = guitar.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = guitar.AddComponent<BoxCollider>();
                }

                Renderer[] renderers = guitar.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
                    bool hasBounds = false;
                    foreach (var r in renderers)
                    {
                        if (!hasBounds)
                        {
                            localBounds = r.localBounds;
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(r.localBounds);
                        }
                    }
                    col.center = localBounds.center;
                    col.size = localBounds.size;
                }

                // Ensure XRGrabInteractable
                XRGrabInteractable grab = guitar.GetComponent<XRGrabInteractable>();
                if (grab == null)
                {
                    grab = guitar.AddComponent<XRGrabInteractable>();
                }
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
                grab.smoothPosition = true;
                grab.smoothRotation = true;

                // Attach point for natural grip on guitar neck/body
                Transform attachPoint = gTransform.Find("AttachPoint");
                if (attachPoint == null)
                {
                    GameObject apObj = new GameObject("AttachPoint");
                    apObj.transform.SetParent(gTransform, false);
                    // Position at neck joint
                    apObj.transform.localPosition = new Vector3(0f, col.center.y * 0.5f, 0.05f);
                    apObj.transform.localRotation = Quaternion.identity;
                    attachPoint = apObj.transform;
                }
                grab.attachTransform = attachPoint;

                // Guitar Strummer audio component
                if (guitar.GetComponent<GuitarStrummer>() == null)
                {
                    guitar.AddComponent<GuitarStrummer>();
                }

                Debug.Log($"[VR Village] Configured XRGrabInteractable & Strummer on {gName}.");
            }
        }

        private static void SetupWallGuitarRacks(GameObject gc)
        {
            string[] rackNames = new string[] { "Wall Guitar Rack", "Wall Guitar Rack.001" };

            foreach (var rName in rackNames)
            {
                Transform rackTransform = FindDeepChild(gc.transform, rName);
                if (rackTransform == null) continue;

                GameObject rackObj = rackTransform.gameObject;

                // Static collider for the backboard
                if (rackObj.GetComponent<Collider>() == null)
                {
                    rackObj.AddComponent<BoxCollider>();
                }

                Renderer rRenderer = rackObj.GetComponent<Renderer>();
                Bounds bounds = rRenderer != null ? rRenderer.localBounds : new Bounds(Vector3.zero, new Vector3(1.2f, 0.2f, 0.2f));

                // Each rack has 3 hanger positions (-X, Center, +X)
                float spanX = bounds.size.x * 0.35f;
                float[] xOffsets = new float[] { -spanX, 0f, spanX };

                for (int i = 0; i < xOffsets.Length; i++)
                {
                    string socketName = $"RackSocket_{i + 1}";
                    Transform socketT = rackTransform.Find(socketName);
                    GameObject socketObj;
                    if (socketT == null)
                    {
                        socketObj = new GameObject(socketName);
                        socketObj.transform.SetParent(rackTransform, false);
                        // Project slightly forward from the wall
                        socketObj.transform.localPosition = new Vector3(xOffsets[i], bounds.center.y, bounds.max.z + 0.12f);
                        socketObj.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        socketObj = socketT.gameObject;
                    }

                    // Sphere trigger collider for socket proximity
                    SphereCollider trigger = socketObj.GetComponent<SphereCollider>();
                    if (trigger == null)
                    {
                        trigger = socketObj.AddComponent<SphereCollider>();
                    }
                    trigger.isTrigger = true;
                    trigger.radius = 0.25f;

                    // XRSocketInteractor
                    XRSocketInteractor socket = socketObj.GetComponent<XRSocketInteractor>();
                    if (socket == null)
                    {
                        socket = socketObj.AddComponent<XRSocketInteractor>();
                    }
                    socket.showInteractableHoverMeshes = true;

                    // Attach transform: guitar snaps VERTICAL + facing OUTWARD.
                    //
                    // Verified by rotation math (Unity ZXY order):
                    //   Guitar long axis  = local X
                    //   Guitar face       = local Y
                    //   Wall rack outward = world +Z
                    //
                    //   Euler(0, 90, 90):
                    //     localX → Rz(+90°) → world Y  (vertical ✓)
                    //     localY → Rz(+90°) → world -X → Ry(+90°) → world +Z (outward ✓)
                    //
                    Transform socketAttach = socketObj.transform.Find("SocketAttach");
                    if (socketAttach != null)
                    {
                        // Always rebuild so rotation change is applied on re-run.
                        UnityEngine.Object.DestroyImmediate(socketAttach.gameObject);
                        socketAttach = null;
                    }

                    GameObject saObj = new GameObject("SocketAttach");
                    saObj.transform.SetParent(socketObj.transform, false);
                    saObj.transform.localPosition = Vector3.zero;
                    saObj.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                    socketAttach = saObj.transform;
                    socket.attachTransform = socketAttach;

                    // Audio feedback component
                    if (socketObj.GetComponent<WallRackSocket>() == null)
                    {
                        socketObj.AddComponent<WallRackSocket>();
                    }
                }

                Debug.Log($"[VR Village] Configured 3 guitar docking sockets on {rName}.");
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
