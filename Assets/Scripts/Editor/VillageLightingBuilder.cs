using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using L = VRVillage.Editor.VillageLayout;

namespace VRVillage.Editor
{
    /// <summary>
    /// Interior lighting for every indoor house, under VillageLighting/Lights_&lt;House&gt;.
    /// A house with no group yet gets 1-4 soft point lights on a grid over its footprint (a building receives at
    /// most 4 per-object lights in URP), short range so little leaks onto the street, colour per house.
    /// Existing groups are the user's: they are never rebuilt, only re-anchored to their building (centre + yaw),
    /// so Arrange Village Layout carries hand-placed lights along with the house. Delete a group to regenerate it.
    /// Narin's own 16-18 m interior lights are switched off (they lit the neighbours); houses that bring small
    /// lights of their own (Ramen, Fish shop pendants) and outdoor ones (parks, farm) are left alone.
    /// </summary>
    public static class VillageLightingBuilder
    {
        const string RootName = "VillageLighting";
        const float Intensity = 1.4f;
        const float Height = 2.6f;      // above the building's floor
        const float Cell = 7f;          // one light per ~7 m

        static readonly Dictionary<string, Color> s_Indoor = new Dictionary<string, Color>
        {
            { "GuitarCenter", new Color(1f, 0.86f, 0.68f) },
            { "Arcade", new Color(0.86f, 0.66f, 1f) },
            { "Cafe", new Color(1f, 0.82f, 0.6f) },
            { "Laundromat", new Color(0.92f, 0.96f, 1f) },
            { "4_DokTong", new Color(1f, 0.9f, 0.86f) },
            { "House3_ConvenienceShop", new Color(0.94f, 0.97f, 1f) },
            { "House2_ResidentialHouse", new Color(1f, 0.88f, 0.72f) },
            { "House4_MechanicsShop", new Color(1f, 0.97f, 0.9f) },
            { "House1_StorageFacility", new Color(1f, 0.95f, 0.85f) },
        };

        [MenuItem("VR Village/Rebuild House Lights", false, 102)]
        static void Menu()
        {
            Undo.SetCurrentGroupName("Rebuild House Lights");
            int group = Undo.GetCurrentGroup();
            var log = new List<string>();
            Build(log);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Village] " + string.Join("\n  ", log));
        }

        public static void Build(List<string> log)
        {
            List<Transform> all = L.SceneTransforms();
            Transform root = null;
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                if (top.name == RootName) root = top.transform;
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Rebuild House Lights");
            }

            // Narin's oversized interior lights
            int off = 0;
            foreach (Transform t in all)
            {
                if (t.name != "Interior_PointLight" || !t.TryGetComponent(out Light nl) || !nl.enabled) continue;
                Undo.RecordObject(nl, "Rebuild House Lights");
                nl.enabled = false;
                off++;
            }

            int created = 0, kept = 0;
            foreach (L.House h in L.AllHouses())
            {
                if (!s_Indoor.TryGetValue(h.BoundsFrom, out Color color)) continue;
                Transform building = L.FindNamed(all, h.BoundsFrom);
                if (building == null) continue;
                Bounds b = L.WorldBounds(building, true);
                Vector3 anchorPos = new Vector3(b.center.x, b.min.y, b.center.z);
                Quaternion anchorRot = Quaternion.Euler(0f, building.eulerAngles.y, 0f);

                Transform g = root.Find("Lights_" + h.Display);
                if (g != null)
                {
                    Anchor(g, anchorPos, anchorRot);
                    kept++;
                    continue;
                }

                g = new GameObject("Lights_" + h.Display).transform;
                Undo.RegisterCreatedObjectUndo(g.gameObject, "Rebuild House Lights");
                g.SetParent(root, false);
                g.SetPositionAndRotation(anchorPos, anchorRot);

                int nx = Mathf.Clamp(Mathf.RoundToInt(b.size.x / Cell), 1, 2);
                int nz = Mathf.Clamp(Mathf.RoundToInt(b.size.z / Cell), 1, 2);
                float cellX = b.size.x / nx, cellZ = b.size.z / nz;
                float range = Mathf.Max(cellX, cellZ) * 0.75f + 1.5f;
                float y = b.min.y + Mathf.Min(Height, b.size.y * 0.75f);
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        var go = new GameObject("PointLight");
                        go.transform.SetParent(g, false);
                        go.transform.position = new Vector3(b.min.x + cellX * (ix + 0.5f), y, b.min.z + cellZ * (iz + 0.5f));
                        var l = go.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = color;
                        l.intensity = Intensity;
                        l.range = range;
                        l.shadows = LightShadows.None;
                        l.lightmapBakeType = LightmapBakeType.Realtime;
                    }
                created++;
            }
            log.Add($"House lights: {created} houses lit, {kept} existing groups kept (moved with their house); " +
                    $"{off} of Narin's 16-18 m interior lights switched off.");
        }

        /// <summary>
        /// Pins every existing group to its building where the house stands now, without moving any light.
        /// Arrange runs this before moving houses, so groups made before anchoring existed travel with them.
        /// </summary>
        public static void AnchorExisting()
        {
            List<Transform> all = L.SceneTransforms();
            Transform root = null;
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                if (top.name == RootName) root = top.transform;
            if (root == null) return;
            foreach (L.House h in L.AllHouses())
            {
                Transform g = root.Find("Lights_" + h.Display);
                Transform building = L.FindNamed(all, h.BoundsFrom);
                if (g == null || building == null) continue;
                if (g.position != Vector3.zero || g.rotation != Quaternion.identity) continue;   // already pinned
                Bounds b = L.WorldBounds(building, true);
                Anchor(g, new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.Euler(0f, building.eulerAngles.y, 0f));
            }
        }

        /// <summary>
        /// Puts the group on its building's anchor. A group still at the world origin (made before anchoring
        /// existed, possibly hand-edited) is re-anchored without moving its lights; an anchored group simply
        /// follows the building, carrying its lights along.
        /// </summary>
        static void Anchor(Transform g, Vector3 pos, Quaternion rot)
        {
            Undo.RecordObject(g, "Rebuild House Lights");
            if (g.position == Vector3.zero && g.rotation == Quaternion.identity)
            {
                var children = new List<Transform>();
                var poses = new List<Pose>();
                foreach (Transform c in g)
                {
                    children.Add(c);
                    poses.Add(new Pose(c.position, c.rotation));
                    Undo.RecordObject(c, "Rebuild House Lights");
                }
                g.SetPositionAndRotation(pos, rot);
                for (int i = 0; i < children.Count; i++)
                    children[i].SetPositionAndRotation(poses[i].position, poses[i].rotation);
            }
            else
            {
                g.SetPositionAndRotation(pos, rot);
            }
        }
    }
}
