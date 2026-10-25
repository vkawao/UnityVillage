using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VRVillage.Editor
{
    /// <summary>
    /// The shared village layout (metres, north = +z), built round the presentation:
    ///   Centre: a presentation plaza with the 5 demo houses facing into it - Sakura Park + Arcade (south side),
    ///           Ramen (north), chicken farm (west), the Backrooms house (east) - and the spawn in the middle.
    ///   Around it: a ring road; the other 10 houses face the road from outside - Guitar, Cafe, Laundromat (south),
    ///           mini-mart, residential house, mechanics shop (north), storage + park (west), fish + flower shop (east).
    /// Wikorn's houses only ever slide (building + quest root together); the others are turned to face their side,
    /// using each house's known entrance direction (LocalFront).
    /// </summary>
    public static class VillageLayout
    {
        public const float RoadWidth = 6f, SidewalkWidth = 2f;
        // Road ring (outer edges of the asphalt) and the inner block it goes around
        public const float RingMinX = -23f, RingMaxX = 56f, RingMinZ = -32f, RingMaxZ = 28f;
        public const float BlockMinX = RingMinX + RoadWidth, BlockMaxX = RingMaxX - RoadWidth;   // -17 .. 50
        public const float BlockMinZ = RingMinZ + RoadWidth, BlockMaxZ = RingMaxZ - RoadWidth;   // -26 .. 22

        // The presentation plaza inside the block; demo houses' fronts sit on its edges
        public static readonly Rect Plaza = Rect.MinMaxRect(2f, -11.5f, 26f, 8.5f);
        // Paved walkways from the plaza to the block's sidewalk (between the demo houses)
        public static readonly Rect[] Walkways =
        {
            Rect.MinMaxRect(3f, 8.5f, 7f, BlockMaxZ - SidewalkWidth),            // north-west, under the welcome gate
            Rect.MinMaxRect(20.5f, 8.5f, 24.5f, BlockMaxZ - SidewalkWidth),      // north-east
            Rect.MinMaxRect(BlockMinX + SidewalkWidth, -11f, 2f, -9f),           // west, past the farm
            Rect.MinMaxRect(13f, BlockMinZ + SidewalkWidth, 15.2f, -11.5f),      // south, between Sakura and Arcade
        };

        public static readonly Vector3 Spawn = new Vector3(14f, 0f, -1.5f);   // plaza centre, facing Sakura + Arcade
        public const float SpawnYaw = 180f;

        public enum Face { North, South, East, West }

        public class House
        {
            public string Label, Display, BoundsFrom;
            public string[] Move;       // transforms that move together (building + its quest root)
            public Vector3 LocalFront;  // entrance in the building's local space
            public bool NeverRotate;    // Wikorn's quests are built around their orientation
            public bool Presented;      // one of the 5 demo houses round the plaza
            public Face Face;
        }

        static House Ours(string display, string building, params string[] move) => new House
        { Label = display, Display = display, BoundsFrom = building, Move = move, LocalFront = Vector3.forward, NeverRotate = true };
        static House Phumiphat(string display, string prefab, Vector3 localFront) => new House
        { Label = prefab, Display = display, BoundsFrom = prefab, Move = new[] { prefab }, LocalFront = localFront };
        // Entrances read from Narin's build script (each house root's rotation is commented there): storage, house and
        // the Backrooms house enter on local -Z, the mini-mart and mechanic on local -X. Guessing from door positions
        // turned three of them the wrong way (a corner door, one of two garage doors, hidden Backrooms kit parts).
        static House Narin(string display, string root, Vector3 localFront) => new House
        { Label = root, Display = display, BoundsFrom = root, Move = new[] { root }, LocalFront = localFront };
        static House Demo(House h) { h.Presented = true; return h; }

        public class Row
        {
            public Face Face; public float Line, Start, Gap; public House[] Houses;
            public Row(Face face, float line, float start, float gap, params House[] houses)
            { Face = face; Line = line; Start = start; Gap = gap; Houses = houses; foreach (House h in houses) h.Face = face; }
        }

        // Line = where the entrance side sits; Start = first house's left edge, or the centre for a single house
        public static readonly Row[] Rows =
        {
            // the plaza
            new Row(Face.North, Plaza.yMin, Plaza.xMin, 2.5f,
                Demo(Ours("Sakura Park", "VendingMachinePark", "Wikorn - Vending Machine")),
                Demo(Ours("Arcade", "Arcade", "Arcade", "ArcadeQuest"))),
            new Row(Face.South, Plaza.yMax, Plaza.center.x, 0f,
                Demo(Phumiphat("Ramen", "1_RiceShowerRamen", Vector3.back))),
            new Row(Face.East, Plaza.xMin, Plaza.center.y, 0f,
                Demo(Phumiphat("Chicken Farm", "3_RaiKaiTod", Vector3.forward))),
            new Row(Face.West, Plaza.xMax, Plaza.center.y, 0f,
                Demo(Narin("???", "House5_Backrooms_VillageExterior", Vector3.back))),
            // outside the ring, facing the road
            new Row(Face.North, RingMinZ - SidewalkWidth, RingMinX + 2f, 2.5f,
                Ours("Guitar Shop", "GuitarCenter", "Wikorn - Guitar Center"),
                Ours("Cafe", "Cafe", "Cafe", "CafeQuest"),
                Ours("Laundromat", "Laundromat", "Laundromat", "LaundromatQuest")),
            new Row(Face.South, RingMaxZ + SidewalkWidth, RingMinX + 2f, 2.5f,
                Narin("Mini-mart", "House3_ConvenienceShop", Vector3.left),
                Narin("House", "House2_ResidentialHouse", Vector3.back),
                Narin("Mechanic", "House4_MechanicsShop", Vector3.left)),
            new Row(Face.East, RingMinX - SidewalkWidth, -12f, 0f, Narin("Storage", "House1_StorageFacility", Vector3.back)),
            new Row(Face.East, RingMinX - SidewalkWidth, 10f, 0f, Phumiphat("Park", "5_SuanTonMaiKongPoa", Vector3.forward)),
            new Row(Face.West, RingMaxX + SidewalkWidth, -12f, 0f, Phumiphat("Fish Shop", "2_KaiPlaNaJa", Vector3.forward)),
            new Row(Face.West, RingMaxX + SidewalkWidth, 8f, 0f, Phumiphat("Flower Shop", "4_DokTong", Vector3.forward)),
        };

        public static IEnumerable<House> AllHouses()
        {
            foreach (Row r in Rows) foreach (House h in r.Houses) yield return h;
        }

        public static Vector3 FrontDir(Face f) =>
            f == Face.North ? Vector3.forward : f == Face.South ? Vector3.back : f == Face.East ? Vector3.right : Vector3.left;

        public static List<Transform> SceneTransforms()
        {
            var all = new List<Transform>();
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                all.AddRange(top.GetComponentsInChildren<Transform>(true));
            return all;
        }

        /// <summary>Prefers a scene root with that name (several houses share common child names).</summary>
        public static Transform FindNamed(List<Transform> all, string name)
        {
            Transform deep = null;
            foreach (Transform t in all)
            {
                if (t.name != name) continue;
                if (t.parent == null) return t;
                if (deep == null) deep = t;
            }
            return deep;
        }

        /// <summary>
        /// Renderer bounds of what is visible (hidden parts skipped: Narin keeps the whole Backrooms kit hidden inside
        /// its house). <paramref name="includeHidden"/> is the older measure the light groups were pinned with.
        /// </summary>
        public static Bounds WorldBounds(Transform t, bool includeHidden = false)
        {
            bool any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(includeHidden))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!includeHidden && !r.enabled) continue;
                if (r.bounds.size == Vector3.zero) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }

    /// <summary>Menu: places all 15 houses per <see cref="VillageLayout"/>, then rebuilds the decor and lights.</summary>
    public static class VillageLayoutArranger
    {
        // Narin's debug/progress board: on the east road, out of the presentation's way
        static readonly Vector3 NarinBoardPosition = new Vector3(VillageLayout.RingMaxX + 1f, 0f, -22f);

        [MenuItem("VR Village/Arrange Village Layout", false, 100)]
        static void Arrange()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<Transform> all = VillageLayout.SceneTransforms();

            Undo.SetCurrentGroupName("Arrange Village Layout");
            int undoGroup = Undo.GetCurrentGroup();
            var log = new List<string>();

            VillageLightingBuilder.AnchorExisting();   // before anything moves, so hand-placed lights travel along

            foreach (VillageLayout.Row row in VillageLayout.Rows)
            {
                float cursor = row.Start;
                bool single = row.Houses.Length == 1;
                foreach (VillageLayout.House h in row.Houses)
                {
                    if (!Turn(all, h, out List<Transform> group, out Transform building)) { log.Add($"{h.Display}: NOT FOUND"); continue; }
                    Bounds b = VillageLayout.WorldBounds(building);
                    bool alongX = row.Face == VillageLayout.Face.North || row.Face == VillageLayout.Face.South;
                    float width = alongX ? b.size.x : b.size.z;
                    float along = single ? row.Start : cursor + width * 0.5f;
                    Slide(group, building, row.Face, along, row.Line);
                    cursor += width + row.Gap;
                    Bounds nb = VillageLayout.WorldBounds(building);
                    log.Add($"{h.Display}: centre ({nb.center.x:F1}, {nb.center.z:F1}), size {nb.size.x:F1} x {nb.size.z:F1}, faces {row.Face}");
                }
            }

            PlaceNarinExtras(all, log);
            PlaceSpawn(log);
            SettleGround(all, log);
            foreach (GameObject top in scene.GetRootGameObjects())
                if (top.name == "VillageRoads") Undo.DestroyObjectImmediate(top);   // first layout's roads
            VillageDecorBuilder.Build(log);
            VillageLightingBuilder.Build(log);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Village] Layout arranged (Ctrl+Z undoes it; save the scene to keep it):\n  " + string.Join("\n  ", log));
        }

        /// <summary>Finds the house and turns it so its entrance faces its side (Wikorn's houses never turn).</summary>
        static bool Turn(List<Transform> all, VillageLayout.House h, out List<Transform> group, out Transform building)
        {
            group = new List<Transform>();
            building = VillageLayout.FindNamed(all, h.BoundsFrom);
            foreach (string name in h.Move)
            {
                Transform t = VillageLayout.FindNamed(all, name);
                if (t != null && !group.Contains(t)) group.Add(t);
            }
            if (building == null || group.Count == 0) return false;
            foreach (Transform t in group) Undo.RecordObject(t, "Arrange Village Layout");
            if (h.NeverRotate) return true;

            Vector3 front = Snap(building.TransformDirection(h.LocalFront));
            float angle = Vector3.SignedAngle(front, VillageLayout.FrontDir(h.Face), Vector3.up);
            if (Mathf.Abs(angle) < 1f) return true;

            Vector3 pivot = VillageLayout.WorldBounds(building).center;
            Quaternion turn = Quaternion.AngleAxis(Mathf.Round(angle / 90f) * 90f, Vector3.up);
            foreach (Transform t in group)
                t.SetPositionAndRotation(pivot + turn * (t.position - pivot), turn * t.rotation);
            return true;
        }

        /// <summary>Centre at <paramref name="along"/> on its side's axis, entrance side on the front line.</summary>
        static void Slide(List<Transform> group, Transform building, VillageLayout.Face face, float along, float line)
        {
            Bounds b = VillageLayout.WorldBounds(building);
            Vector3 delta = Vector3.zero;
            switch (face)
            {
                case VillageLayout.Face.North: delta.x = along - b.center.x; delta.z = line - b.max.z; break;
                case VillageLayout.Face.South: delta.x = along - b.center.x; delta.z = line - b.min.z; break;
                case VillageLayout.Face.East: delta.z = along - b.center.z; delta.x = line - b.max.x; break;
                case VillageLayout.Face.West: delta.z = along - b.center.z; delta.x = line - b.min.x; break;
            }
            foreach (Transform t in group) t.position += delta;
        }

        /// <summary>Narin's progress board out of the way, and escaping the Backrooms lands back in the plaza.</summary>
        static void PlaceNarinExtras(List<Transform> all, List<string> log)
        {
            Transform board = VillageLayout.FindNamed(all, "Village_Master_Control_Board");
            if (board != null)
            {
                Undo.RecordObject(board, "Arrange Village Layout");
                board.SetPositionAndRotation(new Vector3(NarinBoardPosition.x, board.position.y, NarinBoardPosition.z),
                    Quaternion.Euler(0f, 90f, 0f)); // readable from the east road
            }

            Transform spawn = VillageLayout.FindNamed(all, "Village_SpawnPoint");
            if (spawn != null)
            {
                Undo.RecordObject(spawn, "Arrange Village Layout");
                spawn.SetPositionAndRotation(
                    new Vector3(VillageLayout.Spawn.x + 2f, spawn.position.y, VillageLayout.Spawn.z),
                    Quaternion.Euler(0f, VillageLayout.SpawnYaw, 0f));
                log.Add($"Backrooms return spot: {spawn.position:F1} (plaza)");
            }
        }

        static void PlaceSpawn(List<string> log)
        {
            XROrigin origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null) { log.Add("XR Origin: NOT FOUND"); return; }
            Undo.RecordObject(origin.transform, "Arrange Village Layout");
            origin.transform.SetPositionAndRotation(
                new Vector3(VillageLayout.Spawn.x, origin.transform.position.y, VillageLayout.Spawn.z),
                Quaternion.Euler(0f, VillageLayout.SpawnYaw, 0f));
            log.Add($"Spawn (XR Origin): {origin.transform.position:F1}, plaza centre facing Sakura Park + Arcade");
        }

        /// <summary>
        /// Lifts the shared ground (Other/Plane) so its top meets the houses' bases (median of all 15), instead of
        /// lowering 15 houses and their quest roots. It sat 0.42 m below them, so everything looked like it floated.
        /// </summary>
        static void SettleGround(List<Transform> all, List<string> log)
        {
            Transform plane = all.Find(t => t.name == "Plane" && t.parent != null && t.parent.name == "Other");
            if (plane == null || !plane.TryGetComponent(out Renderer pr)) { log.Add("Ground plane Other/Plane: NOT FOUND"); return; }
            var bases = new List<float>();
            foreach (VillageLayout.House h in VillageLayout.AllHouses())
            {
                Transform b = VillageLayout.FindNamed(all, h.BoundsFrom);
                if (b != null) bases.Add(VillageLayout.WorldBounds(b).min.y);
            }
            if (bases.Count == 0) return;
            bases.Sort();
            float target = bases[bases.Count / 2];
            float dy = target - pr.bounds.max.y;
            if (Mathf.Abs(dy) < 0.01f) return;
            Undo.RecordObject(plane, "Arrange Village Layout");
            plane.position += Vector3.up * dy;
            log.Add($"Ground plane moved {dy:+0.00;-0.00} m so its top meets the houses (y {target:F2}).");
        }

        static Vector3 Snap(Vector3 d) =>
            Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
    }
}
