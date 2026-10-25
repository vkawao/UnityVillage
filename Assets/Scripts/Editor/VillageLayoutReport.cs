using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRVillage.Editor
{
    /// <summary>
    /// Read-only: writes every house's footprint (renderer bounds, metres) to Logs/VillageLayout.txt so the
    /// village layout can be planned to scale. Changes nothing in the scene.
    /// </summary>
    public static class VillageLayoutReport
    {
        // Scene objects to measure (searched anywhere in the open scene, inactive included)
        static readonly string[] SceneHouses =
        {
            "Wikorn - Guitar Center", "Wikorn - Vending Machine", "Cafe", "Laundromat", "Arcade",
            "House1_StorageFacility", "House2_ResidentialHouse", "House3_ConvenienceShop", "House4_MechanicsShop",
            "House5_Backrooms_VillageExterior", "NarinVillageArea"
        };

        const string PhumiphatPrefabs = "Assets/RamenVR/Prefabs";

        [MenuItem("VR Village/Report House Footprints", false, 200)]
        static void Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("name | centre x z | size x z (m) | rotation y | big children (> 25 m)");

            var all = new List<Transform>();
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                all.AddRange(top.GetComponentsInChildren<Transform>(true));

            foreach (string name in SceneHouses)
            {
                Transform t = all.Find(x => x.name == name);
                if (t == null) { sb.AppendLine($"{name} | NOT IN SCENE"); continue; }
                sb.AppendLine(Describe(name, t));
            }

            sb.AppendLine();
            sb.AppendLine("Scene roots:");
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                sb.AppendLine(Describe("  " + top.name, top.transform));

            sb.AppendLine();
            sb.AppendLine("Phumiphat prefabs (measured at the origin, as dragged in):");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PhumiphatPrefabs }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try { sb.AppendLine(Describe(Path.GetFileNameWithoutExtension(path), contents.transform)); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }

            string outPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "VillageLayout.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[Village] Footprints written to {outPath}\n{sb}");
        }

        static string Describe(string label, Transform t)
        {
            bool any = false;
            var b = new Bounds();
            var big = new List<string>();
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                Bounds rb = r.bounds;
                if (rb.size == Vector3.zero) continue;
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
                if (rb.size.x > 25f || rb.size.z > 25f)
                    big.Add($"{r.name} {rb.size.x:F0}x{rb.size.z:F0}");
            }
            if (!any) return $"{label} | no renderers";
            return $"{label} | {b.center.x:F1} {b.center.z:F1} | {b.size.x:F1} x {b.size.z:F1} | {t.eulerAngles.y:F0} | {string.Join(", ", big)}";
        }
    }
}
