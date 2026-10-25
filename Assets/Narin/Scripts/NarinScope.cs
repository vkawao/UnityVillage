using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace NarinVillage
{
    /// <summary>
    /// Keeps Narin's houses to their own objects in the shared village: every scene-wide search
    /// (by name, "Trash"/"Bin", canvases, buttons) only looks under the NarinHouses root.
    /// </summary>
    public static class NarinScope
    {
        public const string RootName = "NarinHouses";

        private static Transform s_Root;

        public static Transform Root
        {
            get
            {
                if (s_Root == null)
                {
                    if (VillageActionManager.Instance != null)
                    {
                        s_Root = VillageActionManager.Instance.transform.root;
                    }
                    else
                    {
                        GameObject go = GameObject.Find(RootName);
                        if (go != null) s_Root = go.transform;
                    }
                }
                return s_Root;
            }
        }

        /// <summary>All objects under the root, inactive ones included.</summary>
        public static IEnumerable<GameObject> AllObjects()
        {
            Transform root = Root;
            if (root == null) yield break;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                yield return t.gameObject;
        }

        /// <summary>All components of a type under the root, inactive ones included.</summary>
        public static T[] Components<T>() where T : Component
        {
            Transform root = Root;
            return root != null ? root.GetComponentsInChildren<T>(true) : new T[0];
        }

        /// <summary>Like GameObject.Find(name), but only under the root (active objects only).</summary>
        public static GameObject Find(string name)
        {
            Transform root = Root;
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// The player's rig for anything under it (camera, controllers): its XROrigin, not transform.root.
        /// In the shared village the rig is parented under a group object, and moving the root moved that whole group.
        /// </summary>
        public static Transform PlayerRig(Transform t)
        {
            if (t == null) return null;
            XROrigin origin = t.GetComponentInParent<XROrigin>();
            return origin != null ? origin.transform : t.root;
        }

        public static bool IsMine(GameObject go)
        {
            Transform root = Root;
            return go != null && root != null && go.transform.IsChildOf(root);
        }
    }
}
