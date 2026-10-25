using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using L = VRVillage.Editor.VillageLayout;

namespace VRVillage.Editor
{
    /// <summary>
    /// Builds the shared street decor under one root, VillageDecor, around wherever the houses stand now:
    /// roads + sidewalks + markings, zebra crossings at the walkways, the paved presentation plaza and its walkways,
    /// grass on the rest of the block (fountain, benches, stone lanterns, a sakura grove), a lantern canopy over the
    /// plaza, street lamps, power poles + wires, a welcome gate on the north-west walkway, signposts, a name sign +
    /// flower pot per house, a bus stop with bikes, the farm fence and trees all round (dead ones by the Backrooms).
    /// Primitives only, no real lights (emissive bulbs), static-batched, so it stays cheap on Quest.
    /// Anything that would land inside a house is skipped. Rebuilt from scratch on every run.
    /// </summary>
    public static class VillageDecorBuilder
    {
        const string RootName = "VillageDecor";
        const string MatFolder = "Assets/Materials/Decor";
        const string FontPath = "Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF.asset";
        public const string WelcomeText = "MAI RUU MUU BAAN";   // the village's name (Group 15)

        struct HouseSpot { public L.House House; public Bounds Bounds; }

        static Transform s_Root;
        static float Y0;
        static System.Random s_Rng;
        static List<HouseSpot> s_Houses;
        static readonly List<Vector3> s_Crossings = new List<Vector3>();
        static readonly List<Bounds> s_Reserved = new List<Bounds>();   // decor that later decor must avoid
        static readonly List<Rect> s_Paved = new List<Rect>();          // plaza + walkways (no grass, no trees)
        static TMP_FontAsset s_Font;
        static readonly Dictionary<string, Material> s_Mats = new Dictionary<string, Material>();

        [MenuItem("VR Village/Rebuild Village Decor", false, 101)]
        static void Menu()
        {
            Undo.SetCurrentGroupName("Rebuild Village Decor");
            int group = Undo.GetCurrentGroup();
            var log = new List<string>();
            Build(log);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Village] " + string.Join("\n  ", log));
        }

        public static void Build(List<string> log)
        {
            foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
                if (top.name == RootName) Undo.DestroyObjectImmediate(top);

            List<Transform> all = L.SceneTransforms();
            s_Houses = new List<HouseSpot>();
            foreach (L.House h in L.AllHouses())
            {
                Transform b = L.FindNamed(all, h.BoundsFrom);
                if (b != null) s_Houses.Add(new HouseSpot { House = h, Bounds = L.WorldBounds(b) });
            }
            Transform plane = all.Find(t => t.name == "Plane" && t.parent != null && t.parent.name == "Other");
            Y0 = plane != null && plane.TryGetComponent(out Renderer pr) ? pr.bounds.max.y : 0f;
            s_Rng = new System.Random(20261025);
            s_Crossings.Clear();
            s_Reserved.Clear();
            s_Paved.Clear();
            s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Rebuild Village Decor");
            s_Root = root.transform;

            Streets();
            FarmFence();
            Block();
            Lamps();
            PlazaCanopy();
            PowerLines();
            WelcomeGate();
            Signposts();
            HouseSigns();
            BusStop();
            Trees();

            int pieces = 0;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                pieces++;
                if (r.GetComponent<TextMeshPro>() == null)
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }
            log.Add($"Decor rebuilt: {pieces} pieces under {RootName} (ground y {Y0:F2}).");
        }

        // ------------------------------------------------------------------ streets

        static void Streets()
        {
            Transform g = Group("Streets");
            float roadY = Y0 + 0.01f, walkY = Y0 + 0.03f;
            float cx = (L.RingMinX + L.RingMaxX) * 0.5f, cz = (L.RingMinZ + L.RingMaxZ) * 0.5f;
            float lenX = L.RingMaxX - L.RingMinX, lenZ = L.BlockMaxZ - L.BlockMinZ;
            Material asphalt = Mat("Asphalt", new Color(0.22f, 0.22f, 0.24f), 0.1f);
            Material walk = Mat("Sidewalk", new Color(0.64f, 0.62f, 0.58f), 0.1f);

            Box(g, new Vector3(cx, roadY, L.RingMinZ + L.RoadWidth * 0.5f), new Vector3(lenX, 0.02f, L.RoadWidth), asphalt, "SouthStreet");
            Box(g, new Vector3(cx, roadY, L.RingMaxZ - L.RoadWidth * 0.5f), new Vector3(lenX, 0.02f, L.RoadWidth), asphalt, "NorthStreet");
            Box(g, new Vector3(L.RingMinX + L.RoadWidth * 0.5f, roadY, cz), new Vector3(L.RoadWidth, 0.02f, lenZ), asphalt, "WestStreet");
            Box(g, new Vector3(L.RingMaxX - L.RoadWidth * 0.5f, roadY, cz), new Vector3(L.RoadWidth, 0.02f, lenZ), asphalt, "EastStreet");

            float s = L.SidewalkWidth;
            // outer ring of sidewalk
            Box(g, new Vector3(cx, walkY, L.RingMinZ - s * 0.5f), new Vector3(lenX + 2 * s, 0.06f, s), walk, "Sidewalk");
            Box(g, new Vector3(cx, walkY, L.RingMaxZ + s * 0.5f), new Vector3(lenX + 2 * s, 0.06f, s), walk, "Sidewalk");
            Box(g, new Vector3(L.RingMinX - s * 0.5f, walkY, cz), new Vector3(s, 0.06f, L.RingMaxZ - L.RingMinZ), walk, "Sidewalk");
            Box(g, new Vector3(L.RingMaxX + s * 0.5f, walkY, cz), new Vector3(s, 0.06f, L.RingMaxZ - L.RingMinZ), walk, "Sidewalk");
            // inner ring of sidewalk round the block
            float bx = (L.BlockMinX + L.BlockMaxX) * 0.5f, blenX = L.BlockMaxX - L.BlockMinX;
            Box(g, new Vector3(bx, walkY, L.BlockMinZ + s * 0.5f), new Vector3(blenX, 0.06f, s), walk, "Sidewalk");
            Box(g, new Vector3(bx, walkY, L.BlockMaxZ - s * 0.5f), new Vector3(blenX, 0.06f, s), walk, "Sidewalk");
            Box(g, new Vector3(L.BlockMinX + s * 0.5f, walkY, cz), new Vector3(s, 0.06f, lenZ - 2 * s), walk, "Sidewalk");
            Box(g, new Vector3(L.BlockMaxX - s * 0.5f, walkY, cz), new Vector3(s, 0.06f, lenZ - 2 * s), walk, "Sidewalk");

            // zebra crossings where the walkways meet the ring
            Material paint = Mat("RoadPaint", new Color(0.93f, 0.93f, 0.9f), 0.2f);
            foreach (Rect w in L.Walkways)
            {
                bool northSouth = w.height > w.width;
                if (northSouth)
                {
                    bool north = w.center.y > L.Plaza.center.y;
                    float z = north ? L.RingMaxZ - L.RoadWidth * 0.5f : L.RingMinZ + L.RoadWidth * 0.5f;
                    Crossing(g, new Vector3(w.center.x, 0f, z), true, paint);
                }
                else
                {
                    bool west = w.center.x < L.Plaza.center.x;
                    float x = west ? L.RingMinX + L.RoadWidth * 0.5f : L.RingMaxX - L.RoadWidth * 0.5f;
                    Crossing(g, new Vector3(x, 0f, w.center.y), false, paint);
                }
            }

            // dashed centre lines, broken at crossings
            float dashY = Y0 + 0.022f;
            for (float x = L.BlockMinX + 1f; x < L.BlockMaxX - 1f; x += 5f)
            {
                Dash(g, new Vector3(x, dashY, L.RingMinZ + L.RoadWidth * 0.5f), true, paint);
                Dash(g, new Vector3(x, dashY, L.RingMaxZ - L.RoadWidth * 0.5f), true, paint);
            }
            for (float z = L.BlockMinZ + 1f; z < L.BlockMaxZ - 1f; z += 5f)
            {
                Dash(g, new Vector3(L.RingMinX + L.RoadWidth * 0.5f, dashY, z), false, paint);
                Dash(g, new Vector3(L.RingMaxX - L.RoadWidth * 0.5f, dashY, z), false, paint);
            }
        }

        static void Crossing(Transform g, Vector3 centre, bool roadAlongX, Material paint)
        {
            s_Crossings.Add(centre);
            float y = Y0 + 0.024f;
            for (int i = 0; i < 6; i++)
            {
                float off = -L.RoadWidth * 0.5f + 0.5f + i;
                Vector3 p = roadAlongX ? new Vector3(centre.x, y, centre.z + off) : new Vector3(centre.x + off, y, centre.z);
                Vector3 size = roadAlongX ? new Vector3(3f, 0.004f, 0.5f) : new Vector3(0.5f, 0.004f, 3f);
                Box(g, p, size, paint, "Zebra");
            }
        }

        static void Dash(Transform g, Vector3 p, bool alongX, Material paint)
        {
            if (NearCrossing(p, 3f)) return;
            Box(g, p, alongX ? new Vector3(2f, 0.004f, 0.12f) : new Vector3(0.12f, 0.004f, 2f), paint, "Dash");
        }

        // ------------------------------------------------------------------ the block: plaza, walkways, green

        static void Block()
        {
            Transform g = Group("Block");
            Material paving = Mat("Paving", new Color(0.78f, 0.72f, 0.62f), 0.15f);
            Material grass = Mat("Grass", new Color(0.36f, 0.58f, 0.27f), 0.05f);
            float s = L.SidewalkWidth;

            // paved plaza + walkways, cut round the houses
            var paved = new List<Rect> { L.Plaza };
            paved.AddRange(L.Walkways);
            foreach (Rect area in paved)
                foreach (Rect r in FreeRects(area.xMin, area.xMax, area.yMin, area.yMax, 0.05f, null))
                {
                    Box(g, new Vector3(r.center.x, Y0 + 0.012f, r.center.y), new Vector3(r.width, 0.024f, r.height), paving, "Paving");
                    s_Paved.Add(r);
                }

            // grass on the rest of the block
            foreach (Rect r in FreeRects(L.BlockMinX + s, L.BlockMaxX - s, L.BlockMinZ + s, L.BlockMaxZ - s, 0.3f, paved))
                Box(g, new Vector3(r.center.x, Y0 + 0.008f, r.center.y), new Vector3(r.width, 0.016f, r.height), grass, "Grass");

            // fountain + benches in the north-west corner of the block (beside the gate walkway)
            var fc = new Vector3((L.BlockMinX + s + L.Walkways[0].xMin) * 0.5f, Y0, (L.Plaza.yMax + L.BlockMaxZ - s) * 0.5f + 1.5f);
            if (!Blocked(fc, 3f))
            {
                Fountain(g, fc);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + 90f * i) * Mathf.Deg2Rad;
                    Vector3 p = fc + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.4f;
                    if (!Blocked(p, 1f) && !Paved(p, 0.8f)) Bench(g, p, fc - p);
                }
            }

            // stone lanterns at both sides of each walkway's street end
            foreach (Rect w in L.Walkways)
            {
                bool northSouth = w.height > w.width;
                Vector3 a, b;
                if (northSouth)
                {
                    float z = w.center.y > L.Plaza.center.y ? w.yMax - 0.8f : w.yMin + 0.8f;
                    a = new Vector3(w.xMin - 0.7f, Y0, z); b = new Vector3(w.xMax + 0.7f, Y0, z);
                }
                else
                {
                    float x = w.center.x < L.Plaza.center.x ? w.xMin + 0.8f : w.xMax - 0.8f;
                    a = new Vector3(x, Y0, w.yMin - 0.7f); b = new Vector3(x, Y0, w.yMax + 0.7f);
                }
                foreach (Vector3 p in new[] { a, b })
                    if (!Blocked(p, 0.6f)) StoneLantern(g, p);
            }
        }

        static void Fountain(Transform g, Vector3 c)
        {
            Transform f = Node(g, "Fountain", c, 0f);
            Material stone = Mat("Stone", new Color(0.7f, 0.69f, 0.66f), 0.3f);
            Material water = Mat("Water", new Color(0.3f, 0.6f, 0.85f), 0.95f);
            Cyl(f, new Vector3(0f, 0.28f, 0f), 2.4f, 0.55f, stone);
            Cyl(f, new Vector3(0f, 0.5f, 0f), 2.15f, 0.06f, water);
            Cyl(f, new Vector3(0f, 1.0f, 0f), 0.3f, 1.3f, stone);
            Cyl(f, new Vector3(0f, 1.7f, 0f), 0.95f, 0.2f, stone);
            Cyl(f, new Vector3(0f, 1.81f, 0f), 0.8f, 0.04f, water);
            Cyl(f, new Vector3(0f, 2.0f, 0f), 0.14f, 0.45f, stone);
            var col = f.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(4.6f, 0.6f, 4.6f);
            Reserve(c, 3.2f);

            var spray = new GameObject("Spray");
            spray.transform.SetParent(f, false);
            spray.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = spray.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.1f; main.startSpeed = 2.4f; main.startSize = 0.08f;
            main.gravityModifier = 0.9f; main.maxParticles = 150;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.8f);
            var em = ps.emission; em.rateOverTime = 70f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.05f;
            spray.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                Mat("Spray", new Color(0.85f, 0.93f, 1f, 0.8f), 0f, null, "Universal Render Pipeline/Particles/Unlit");
        }

        static void Bench(Transform g, Vector3 p, Vector3 facing)
        {
            facing.y = 0f;
            Transform b = Node(g, "Bench", new Vector3(p.x, Y0, p.z), Quaternion.LookRotation(facing.normalized).eulerAngles.y);
            Material wood = Mat("BenchWood", new Color(0.55f, 0.36f, 0.2f), 0.2f);
            Material iron = Mat("DarkMetal", new Color(0.16f, 0.16f, 0.18f), 0.5f);
            Box(b, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.07f, 0.45f), wood);
            Box(b, new Vector3(0f, 0.78f, -0.22f), new Vector3(1.7f, 0.32f, 0.05f), wood);
            foreach (float x in new[] { -0.7f, 0.7f })
                Box(b, new Vector3(x, 0.22f, -0.02f), new Vector3(0.06f, 0.44f, 0.45f), iron);
            Reserve(p, 1.2f);
        }

        static void StoneLantern(Transform g, Vector3 p)
        {
            Transform t = Node(g, "StoneLantern", new Vector3(p.x, Y0, p.z), 0f);
            Material stone = Mat("Stone", new Color(0.7f, 0.69f, 0.66f), 0.3f);
            Cyl(t, new Vector3(0f, 0.1f, 0f), 0.6f, 0.2f, stone);
            Box(t, new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 0.8f, 0.22f), stone);
            Box(t, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.1f, 0.5f), stone);
            Box(t, new Vector3(0f, 1.25f, 0f), new Vector3(0.32f, 0.3f, 0.32f), Glow());
            Box(t, new Vector3(0f, 1.47f, 0f), new Vector3(0.7f, 0.14f, 0.7f), stone, null, Quaternion.Euler(0f, 45f, 0f));
            Ball(t, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.18f, stone);
            Reserve(p, 0.8f);
        }

        // ------------------------------------------------------------------ lights and wires

        /// <summary>Street lamps on both sidewalks of every road, staggered, arms out over the road.</summary>
        static void Lamps()
        {
            Transform g = Group("StreetLamps");
            float edge = 0.4f;
            for (float x = L.BlockMinX + 3f; x < L.BlockMaxX; x += 12f)
            {
                Lamp(g, new Vector3(x, 0f, L.BlockMaxZ - edge), Vector3.forward);
                Lamp(g, new Vector3(x + 6f, 0f, L.RingMaxZ + edge), Vector3.back);
                Lamp(g, new Vector3(x + 6f, 0f, L.BlockMinZ + edge), Vector3.back);
                Lamp(g, new Vector3(x, 0f, L.RingMinZ - edge), Vector3.forward);
            }
            for (float z = L.BlockMinZ + 4f; z < L.BlockMaxZ; z += 12f)
            {
                Lamp(g, new Vector3(L.RingMinX - edge, 0f, z), Vector3.right);
                Lamp(g, new Vector3(L.BlockMinX + edge, 0f, z + 6f), Vector3.left);
                Lamp(g, new Vector3(L.BlockMaxX - edge, 0f, z), Vector3.right);
                Lamp(g, new Vector3(L.RingMaxX + edge, 0f, z + 6f), Vector3.left);
            }
        }

        static void Lamp(Transform g, Vector3 p, Vector3 arm)
        {
            if (Blocked(p, 0.3f) || NearCrossing(p, 4f) || Paved(p, 0.3f)) return;   // keeps walkway mouths clear
            Transform l = Node(g, "Lamp", new Vector3(p.x, Y0, p.z), Quaternion.LookRotation(arm).eulerAngles.y);
            Material pole = Mat("DarkMetal", new Color(0.16f, 0.16f, 0.18f), 0.5f);
            Cyl(l, new Vector3(0f, 0.15f, 0f), 0.26f, 0.3f, pole);
            Cyl(l, new Vector3(0f, 1.9f, 0f), 0.12f, 3.8f, pole);
            Box(l, new Vector3(0f, 3.75f, 0.45f), new Vector3(0.08f, 0.08f, 0.9f), pole);
            Box(l, new Vector3(0f, 3.62f, 0.85f), new Vector3(0.36f, 0.08f, 0.36f), pole);
            Box(l, new Vector3(0f, 3.45f, 0.85f), new Vector3(0.26f, 0.28f, 0.26f), Glow());
        }

        /// <summary>Festival lanterns strung over the presentation plaza from red poles at its corners.</summary>
        static void PlazaCanopy()
        {
            Transform g = Group("PlazaCanopy");
            Material red = Mat("LanternRed", new Color(0.92f, 0.2f, 0.14f), 0.3f, new Color(1.1f, 0.22f, 0.12f));
            Material orange = Mat("LanternOrange", new Color(1f, 0.62f, 0.2f), 0.3f, new Color(1.2f, 0.6f, 0.15f));
            Material pole = Mat("GateRed", new Color(0.78f, 0.14f, 0.1f), 0.3f);
            Material wire = LineMat("Wire", new Color(0.05f, 0.05f, 0.05f));
            const float top = 5.2f, inset = 0.8f;
            Rect p = L.Plaza;
            var corners = new[]
            {
                new Vector3(p.xMin + inset, 0f, p.yMax - inset), new Vector3(p.xMax - inset, 0f, p.yMax - inset),
                new Vector3(p.xMax - inset, 0f, p.yMin + inset), new Vector3(p.xMin + inset, 0f, p.yMin + inset),
            };
            var ok = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                ok[i] = !Blocked(corners[i], 0.3f);
                if (ok[i]) Cyl(g, new Vector3(corners[i].x, Y0 + top * 0.5f, corners[i].z), 0.12f, top, pole, "CanopyPole");
            }
            // two diagonals over the spawn plus the north and south edges
            foreach ((int a, int b) in new[] { (0, 2), (1, 3), (0, 1), (3, 2) })
            {
                if (!ok[a] || !ok[b]) continue;
                Vector3 pa = corners[a] + Vector3.up * (Y0 + top - 0.1f), pb = corners[b] + Vector3.up * (Y0 + top - 0.1f);
                Vector3[] curve = Sag(pa, pb, 0.9f, 22);
                Line(g, curve, 0.02f, wire);
                for (int i = 2; i < curve.Length - 2; i += 2)
                    Ball(g, curve[i] + Vector3.down * 0.22f, new Vector3(0.32f, 0.4f, 0.32f), i % 4 == 0 ? red : orange, "Lantern");
            }
        }

        /// <summary>Wooden power poles along the south road with three sagging wires.</summary>
        static void PowerLines()
        {
            Transform g = Group("PowerLines");
            Material wood = Mat("PoleWood", new Color(0.42f, 0.32f, 0.22f), 0.15f);
            Material wire = LineMat("Wire", new Color(0.05f, 0.05f, 0.05f));
            var tops = new List<Vector3>();
            float z = L.RingMinZ - L.SidewalkWidth + 0.35f;
            for (float x = L.RingMinX + 1f; x <= L.RingMaxX; x += 17f)
            {
                var p = new Vector3(x, 0f, z);
                if (Blocked(p, 0.3f) || NearCrossing(p, 2.5f)) continue;
                Cyl(g, new Vector3(x, Y0 + 3.5f, z), 0.24f, 7f, wood, "PowerPole");
                Box(g, new Vector3(x, Y0 + 6.6f, z), new Vector3(0.12f, 0.12f, 1.8f), wood, "CrossArm");
                tops.Add(new Vector3(x, Y0 + 6.7f, z));
            }
            for (int i = 0; i + 1 < tops.Count; i++)
                foreach (float off in new[] { -0.75f, 0f, 0.75f })
                    Line(g, Sag(tops[i] + new Vector3(0f, 0f, off), tops[i + 1] + new Vector3(0f, 0f, off), 0.6f, 12), 0.025f, wire);
        }

        // ------------------------------------------------------------------ gate and signs

        /// <summary>A red torii-style gate over the north-west walkway where it meets the street.</summary>
        static void WelcomeGate()
        {
            Rect w = L.Walkways[0];
            var c = new Vector3(w.center.x, Y0, L.BlockMaxZ - L.SidewalkWidth * 0.5f);
            Transform g = Node(Group("WelcomeGate"), "Gate", c, 90f);   // local z = across the walkway (world x)
            Material red = Mat("GateRed", new Color(0.78f, 0.14f, 0.1f), 0.3f);
            Material black = Mat("GateBlack", new Color(0.08f, 0.08f, 0.08f), 0.3f);
            float half = w.width * 0.5f + 0.9f;
            foreach (float z in new[] { -half, half })
            {
                Cyl(g, new Vector3(0f, 2.75f, z), 0.44f, 5.5f, red, "Post").AddComponent<CapsuleCollider>();
                Cyl(g, new Vector3(0f, 0.2f, z), 0.6f, 0.4f, black);
            }
            Box(g, new Vector3(0f, 5.65f, 0f), new Vector3(0.5f, 0.35f, 2f * half + 1.6f), black, "Kasagi");
            Box(g, new Vector3(0f, 5.35f, 0f), new Vector3(0.45f, 0.3f, 2f * half + 1.0f), red);
            Box(g, new Vector3(0f, 4.55f, 0f), new Vector3(0.3f, 0.28f, 2f * half + 0.6f), red, "Nuki");
            Box(g, new Vector3(0f, 4.95f, 0f), new Vector3(0.14f, 0.55f, 2f * half - 1.2f), black, "Plaque");
            // readable from the street and from the plaza
            Text3D(g, WelcomeText, new Vector3(-0.08f, 4.95f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(2f * half - 1.4f, 0.42f), new Color(1f, 0.85f, 0.4f));
            Text3D(g, WelcomeText, new Vector3(0.08f, 4.95f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(2f * half - 1.4f, 0.42f), new Color(1f, 0.85f, 0.4f));
            Material red2 = Mat("LanternRed", new Color(0.92f, 0.2f, 0.14f), 0.3f, new Color(1.1f, 0.22f, 0.12f));
            foreach (float z in new[] { -half + 0.6f, half - 0.6f })
            {
                Box(g, new Vector3(0f, 4.15f, z), new Vector3(0.02f, 0.55f, 0.02f), black);
                Ball(g, new Vector3(0f, 3.65f, z), new Vector3(0.5f, 0.65f, 0.5f), red2, "Lantern");
            }
            Reserve(c, half + 0.5f);
        }

        /// <summary>Signposts at the four outer corners, plus "Plaza" pointers where the walkways leave the ring.</summary>
        static void Signposts()
        {
            Transform g = Group("Signposts");
            float o = L.SidewalkWidth * 0.5f;
            const string north = "Mini-mart  ·  House  ·  Mechanic", south = "Guitar  ·  Cafe  ·  Laundromat";
            const string west = "Storage  ·  Bus  ·  Park", east = "Fish  ·  Flower";
            Signpost(g, new Vector3(L.RingMinX - o, 0f, L.RingMaxZ + o), (north, Vector3.right), (west, Vector3.back));
            Signpost(g, new Vector3(L.RingMaxX + o, 0f, L.RingMaxZ + o), (north, Vector3.left), (east, Vector3.back));
            Signpost(g, new Vector3(L.RingMaxX + o, 0f, L.RingMinZ - o), (south, Vector3.left), (east, Vector3.forward));
            Signpost(g, new Vector3(L.RingMinX - o, 0f, L.RingMinZ - o), (south, Vector3.right), (west, Vector3.forward));

            // across the road from each walkway: "Plaza" pointing into it (the gate walkway has the gate)
            for (int i = 1; i < L.Walkways.Length; i++)
            {
                Rect w = L.Walkways[i];
                bool northSouth = w.height > w.width;
                Vector3 p, dir;
                if (northSouth)
                {
                    bool n = w.center.y > L.Plaza.center.y;
                    p = new Vector3(w.xMax + 1.2f, 0f, n ? L.RingMaxZ + o : L.RingMinZ - o);
                    dir = n ? Vector3.back : Vector3.forward;
                }
                else
                {
                    bool wst = w.center.x < L.Plaza.center.x;
                    p = new Vector3(wst ? L.RingMinX - o : L.RingMaxX + o, 0f, w.yMax + 1.2f);
                    dir = wst ? Vector3.right : Vector3.left;
                }
                Signpost(g, p, ("Plaza", dir));
            }
        }

        static void Signpost(Transform g, Vector3 p, params (string text, Vector3 dir)[] boards)
        {
            if (Blocked(p, 0.3f)) return;
            Transform s = Node(g, "Signpost", new Vector3(p.x, Y0, p.z), 0f);
            Material wood = Mat("SignWood", new Color(0.5f, 0.34f, 0.2f), 0.2f);
            Material board = Mat("SignBoard", new Color(0.96f, 0.93f, 0.84f), 0.2f);
            Cyl(s, new Vector3(0f, 1.5f, 0f), 0.12f, 3f, wood);
            for (int i = 0; i < boards.Length; i++)
            {
                Vector3 d = boards[i].dir;
                var b = new GameObject("Board").transform;
                b.SetParent(s, false);
                b.localPosition = new Vector3(0f, 2.6f - 0.5f * i, 0f) + d * 1.05f;
                b.localRotation = Quaternion.FromToRotation(Vector3.right, d);
                Box(b, Vector3.zero, new Vector3(1.9f, 0.36f, 0.05f), board);
                Box(b, new Vector3(0.95f, 0f, 0f), new Vector3(0.26f, 0.26f, 0.05f), board, null, Quaternion.Euler(0f, 0f, 45f));
                var ink = new Color(0.25f, 0.16f, 0.08f);
                Text3D(b, boards[i].text + "  >", new Vector3(0f, 0f, -0.03f), Quaternion.identity, new Vector2(1.8f, 0.26f), ink);
                Text3D(b, "<  " + boards[i].text, new Vector3(0f, 0f, 0.03f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.8f, 0.26f), ink);
            }
            Reserve(p, 0.6f);
        }

        /// <summary>A name sign at each house's front-left corner and a flower pot at its front-right.</summary>
        static void HouseSigns()
        {
            Transform g = Group("HouseSigns");
            Material wood = Mat("SignWood", new Color(0.5f, 0.34f, 0.2f), 0.2f);
            Material board = Mat("SignBoard", new Color(0.96f, 0.93f, 0.84f), 0.2f);
            Material pot = Mat("Terracotta", new Color(0.72f, 0.38f, 0.25f), 0.2f);
            Material bush = Mat("Bush", new Color(0.24f, 0.52f, 0.22f), 0.05f);
            Material bloom = Mat("Blossom", new Color(0.95f, 0.45f, 0.6f), 0.2f);
            foreach (HouseSpot hs in s_Houses)
            {
                Vector3 front = L.FrontDir(hs.House.Face);
                Vector3 right = Vector3.Cross(Vector3.up, -front);   // as seen from in front
                Bounds b = hs.Bounds;
                float depthHalf = Mathf.Abs(Vector3.Dot(b.extents, front));
                float widthHalf = Mathf.Abs(Vector3.Dot(b.extents, right));
                Vector3 frontMid = new Vector3(b.center.x, 0f, b.center.z) + front * depthHalf;

                Vector3 sp = frontMid - right * Mathf.Max(0.5f, widthHalf - 0.9f) + front * 0.9f;
                Transform s = Node(g, "Sign_" + hs.House.Display, new Vector3(sp.x, Y0, sp.z), Quaternion.LookRotation(-front).eulerAngles.y);
                foreach (float x in new[] { -0.65f, 0.65f }) Box(s, new Vector3(x, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), wood);
                Box(s, new Vector3(0f, 1.55f, 0f), new Vector3(1.6f, 0.5f, 0.06f), board);
                Text3D(s, hs.House.Display, new Vector3(0f, 1.55f, -0.04f), Quaternion.identity, new Vector2(1.45f, 0.38f), new Color(0.25f, 0.16f, 0.08f));
                Reserve(sp, 0.9f);

                Vector3 pp = frontMid + right * Mathf.Max(0.5f, widthHalf - 0.7f) + front * 0.6f;
                if (Blocked(pp, 0.3f)) continue;
                Transform p = Node(g, "FlowerPot", new Vector3(pp.x, Y0, pp.z), 0f);
                Cyl(p, new Vector3(0f, 0.22f, 0f), 0.6f, 0.45f, pot);
                Ball(p, new Vector3(0f, 0.65f, 0f), new Vector3(0.75f, 0.6f, 0.75f), bush);
                Ball(p, new Vector3(0.12f, 0.88f, 0.05f), Vector3.one * 0.22f, bloom);
                Ball(p, new Vector3(-0.15f, 0.82f, -0.1f), Vector3.one * 0.2f, bloom);
                Reserve(pp, 0.6f);
            }
        }

        // ------------------------------------------------------------------ bus stop, farm, trees

        static void BusStop()
        {
            Transform g = Group("BusStop");
            float x = L.RingMinX - L.SidewalkWidth, z = -0.5f;   // west road, between the storage and the park
            var at = new Vector3(x - 1.4f, 0f, z);
            if (Blocked(at, 1.6f)) return;
            Transform s = Node(g, "Shelter", new Vector3(at.x, Y0, z), 90f);   // opens toward the road (+x)
            Material blue = Mat("BusBlue", new Color(0.2f, 0.42f, 0.72f), 0.4f);
            Material metal = Mat("DarkMetal", new Color(0.16f, 0.16f, 0.18f), 0.5f);
            Material glass = Mat("ShelterGlass", new Color(0.6f, 0.75f, 0.85f), 0.9f);
            Material wood = Mat("BenchWood", new Color(0.55f, 0.36f, 0.2f), 0.2f);
            Box(s, new Vector3(0f, 2.5f, 0f), new Vector3(4f, 0.12f, 2.4f), blue, "Roof");
            Box(s, new Vector3(0f, 1.25f, -1.1f), new Vector3(3.8f, 2.4f, 0.06f), glass, "BackPanel");
            foreach (float px in new[] { -1.9f, 1.9f })
            {
                Box(s, new Vector3(px, 1.25f, 1.05f), new Vector3(0.08f, 2.5f, 0.08f), metal);
                Box(s, new Vector3(px, 1.25f, 0f), new Vector3(0.04f, 2.2f, 2.0f), glass);
            }
            Box(s, new Vector3(0f, 0.45f, -0.75f), new Vector3(3f, 0.07f, 0.45f), wood);
            Box(s, new Vector3(0f, 2.75f, 1.0f), new Vector3(2.2f, 0.36f, 0.08f), blue);
            Text3D(s, "BUS STOP", new Vector3(0f, 2.75f, 1.05f), Quaternion.Euler(0f, 180f, 0f), new Vector2(2f, 0.3f), Color.white);
            Reserve(at, 2.6f);
        }

        static void FarmFence()
        {
            if (!TryHouse("3_RaiKaiTod", out Bounds farm)) return;
            Transform g = Group("FarmFence");
            Material wood = Mat("FenceWood", new Color(0.62f, 0.47f, 0.31f), 0.15f);
            float minX = farm.min.x - 1.5f, maxX = farm.max.x + 1.5f, minZ = farm.min.z - 1.5f, maxZ = farm.max.z + 1.5f;
            FenceRun(g, new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), wood, float.NaN, true);
            FenceRun(g, new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ), wood, float.NaN, true);
            FenceRun(g, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), wood, float.NaN, false);
            FenceRun(g, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), wood, farm.center.z, false);   // gate toward the plaza
            s_Reserved.Add(new Bounds(new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX + 1f, 10f, maxZ - minZ + 1f)));
        }

        static void FenceRun(Transform g, Vector3 a, Vector3 b, Material wood, float gapAt, bool alongX)
        {
            Vector3 d = b - a;
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 2f));
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = a + d * (i / (float)n), p1 = a + d * ((i + 1) / (float)n);
                Vector3 mid = (p0 + p1) * 0.5f;
                float along = alongX ? mid.x : mid.z;
                if (!float.IsNaN(gapAt) && Mathf.Abs(along - gapAt) < 1.6f) continue;
                Box(g, new Vector3(p0.x, Y0 + 0.5f, p0.z), new Vector3(0.12f, 1f, 0.12f), wood, "FencePost");
                float len = (p1 - p0).magnitude;
                Quaternion rot = Quaternion.LookRotation(p1 - p0);
                Box(g, new Vector3(mid.x, Y0 + 0.75f, mid.z), new Vector3(0.05f, 0.08f, len), wood, "Rail", rot);
                Box(g, new Vector3(mid.x, Y0 + 0.4f, mid.z), new Vector3(0.05f, 0.08f, len), wood, "Rail", rot);
            }
        }

        /// <summary>Trees all round the town (outside the streets) and a grove on the block's grass.</summary>
        static void Trees()
        {
            Transform g = Group("Trees");
            TryHouse("VendingMachinePark", out Bounds sakura);
            bool haveBackrooms = TryHouse("House5_Backrooms_VillageExterior", out Bounds backrooms);
            var spawn = new Vector3(L.Spawn.x, 0f, L.Spawn.z);

            for (float x = -35f; x <= 86f; x += 7f)
                for (float z = -60f; z <= 66f; z += 7f)
                {
                    var p = new Vector3(x + Jitter(2.2f), 0f, z + Jitter(2.2f));
                    if (p.x < -37f || p.x > 87f) continue;
                    bool inTown = p.x > L.RingMinX - L.SidewalkWidth - 1.5f && p.x < L.RingMaxX + L.SidewalkWidth + 1.5f
                               && p.z > L.RingMinZ - L.SidewalkWidth - 1.5f && p.z < L.RingMaxZ + L.SidewalkWidth + 1.5f;
                    if (inTown || Blocked(p, 2.2f)) continue;
                    float scale = 0.85f + (float)s_Rng.NextDouble() * 0.45f;
                    if (s_Rng.NextDouble() < 0.2) SakuraTree(g, p, scale);
                    else RoundTree(g, p, scale);
                }

            // the block's grass: sakura near Sakura Park, dead trees by the Backrooms house, round trees elsewhere
            float s = L.SidewalkWidth;
            for (float x = L.BlockMinX + s + 2f; x < L.BlockMaxX - s - 1f; x += 4.5f)
                for (float z = L.BlockMinZ + s + 2f; z < L.BlockMaxZ - s - 1f; z += 4.5f)
                {
                    var p = new Vector3(x + Jitter(1f), 0f, z + Jitter(1f));
                    if (Blocked(p, 1.8f) || Paved(p, 1.6f) || Flat(p - spawn).magnitude < 6f) continue;
                    float scale = 0.8f + (float)s_Rng.NextDouble() * 0.3f;
                    if (haveBackrooms && Distance(backrooms, p) < 7f) DeadTree(g, p, scale);
                    else if (Distance(sakura, p) < 14f || s_Rng.NextDouble() < 0.5) SakuraTree(g, p, scale);
                    else RoundTree(g, p, scale);
                    Reserve(p, 1.2f);
                }
        }

        static void RoundTree(Transform g, Vector3 p, float s)
        {
            Transform t = Node(g, "Tree", new Vector3(p.x, Y0, p.z), (float)s_Rng.NextDouble() * 360f, s);
            Cyl(t, new Vector3(0f, 1.1f, 0f), 0.36f, 2.2f, Mat("Bark", new Color(0.36f, 0.24f, 0.15f), 0.1f));
            Ball(t, new Vector3(0f, 3.0f, 0f), Vector3.one * 2.7f, Mat("Leaves", new Color(0.24f, 0.5f, 0.2f), 0.05f));
            Ball(t, new Vector3(0.5f, 3.7f, 0.3f), Vector3.one * 1.8f, Mat("LeavesLight", new Color(0.32f, 0.6f, 0.25f), 0.05f));
        }

        static void SakuraTree(Transform g, Vector3 p, float s)
        {
            Transform t = Node(g, "SakuraTree", new Vector3(p.x, Y0, p.z), (float)s_Rng.NextDouble() * 360f, s);
            Material pink = Mat("Sakura", new Color(0.97f, 0.73f, 0.83f), 0.1f);
            Material deep = Mat("SakuraDeep", new Color(0.93f, 0.6f, 0.75f), 0.1f);
            Cyl(t, new Vector3(0f, 0.9f, 0f), 0.3f, 1.8f, Mat("DarkBark", new Color(0.28f, 0.2f, 0.17f), 0.1f));
            Ball(t, new Vector3(0f, 2.6f, 0f), new Vector3(2.6f, 1.8f, 2.6f), pink);
            Ball(t, new Vector3(0.8f, 2.9f, 0.3f), new Vector3(1.6f, 1.3f, 1.6f), deep);
            Ball(t, new Vector3(-0.7f, 3.0f, -0.4f), new Vector3(1.5f, 1.2f, 1.5f), pink);
        }

        static void DeadTree(Transform g, Vector3 p, float s)
        {
            Transform t = Node(g, "DeadTree", new Vector3(p.x, Y0, p.z), (float)s_Rng.NextDouble() * 360f, s);
            Material bark = Mat("DeadBark", new Color(0.2f, 0.18f, 0.17f), 0.1f);
            Cyl(t, new Vector3(0f, 1.5f, 0f), 0.28f, 3f, bark);
            Box(t, new Vector3(0.45f, 2.5f, 0f), new Vector3(0.1f, 1.3f, 0.1f), bark, null, Quaternion.Euler(0f, 0f, -40f));
            Box(t, new Vector3(-0.4f, 2.8f, 0.1f), new Vector3(0.09f, 1.1f, 0.09f), bark, null, Quaternion.Euler(10f, 0f, 35f));
            Box(t, new Vector3(0.1f, 3.1f, -0.35f), new Vector3(0.08f, 0.9f, 0.08f), bark, null, Quaternion.Euler(-35f, 0f, 0f));
        }

        // ------------------------------------------------------------------ helpers

        static bool TryHouse(string boundsName, out Bounds b)
        {
            foreach (HouseSpot hs in s_Houses)
                if (hs.House.BoundsFrom == boundsName) { b = hs.Bounds; return true; }
            b = default;
            return false;
        }

        /// <summary>Inside (or within <paramref name="margin"/> of) any house, or near decor already placed.</summary>
        static bool Blocked(Vector3 p, float margin)
        {
            foreach (HouseSpot hs in s_Houses)
                if (Distance(hs.Bounds, p) < margin) return true;
            foreach (Bounds r in s_Reserved)
                if (Distance(r, p) < margin) return true;
            return false;
        }

        static bool Paved(Vector3 p, float margin)
        {
            foreach (Rect r in s_Paved)
                if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.z > r.yMin - margin && p.z < r.yMax + margin) return true;
            return false;
        }

        /// <summary>Flat (xz) distance from a point to a box; 0 inside.</summary>
        static float Distance(Bounds b, Vector3 p)
        {
            if (b.size == Vector3.zero) return float.MaxValue;
            float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
            float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool NearCrossing(Vector3 p, float r)
        {
            foreach (Vector3 c in s_Crossings)
                if (Flat(p - c).magnitude < r) return true;
            return false;
        }

        static void Reserve(Vector3 p, float r) => s_Reserved.Add(new Bounds(new Vector3(p.x, 0f, p.z), new Vector3(2f * r, 10f, 2f * r)));

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Jitter(float a) => ((float)s_Rng.NextDouble() * 2f - 1f) * a;

        /// <summary>Rectangles of the area not covered by any house or <paramref name="extra"/> rect (column sweep, merged).</summary>
        static List<Rect> FreeRects(float minX, float maxX, float minZ, float maxZ, float margin, List<Rect> extra)
        {
            var obstacles = new List<Rect>();
            foreach (HouseSpot hs in s_Houses)
                obstacles.Add(Rect.MinMaxRect(hs.Bounds.min.x - margin, hs.Bounds.min.z - margin, hs.Bounds.max.x + margin, hs.Bounds.max.z + margin));
            if (extra != null) obstacles.AddRange(extra);

            var rects = new List<Rect>();
            const float step = 0.5f;
            List<Vector2> prev = null; float prevStart = minX;
            for (float x = minX; x < maxX - 0.001f; x += step)
            {
                float x1 = Mathf.Min(x + step, maxX);
                var free = new List<Vector2> { new Vector2(minZ, maxZ) };
                foreach (Rect o in obstacles)
                {
                    if (o.xMax <= x || o.xMin >= x1) continue;
                    var next = new List<Vector2>();
                    foreach (Vector2 iv in free)
                    {
                        if (o.yMax <= iv.x || o.yMin >= iv.y) { next.Add(iv); continue; }
                        if (o.yMin > iv.x) next.Add(new Vector2(iv.x, o.yMin));
                        if (o.yMax < iv.y) next.Add(new Vector2(o.yMax, iv.y));
                    }
                    free = next;
                }
                if (prev != null && !SameIntervals(prev, free))
                {
                    foreach (Vector2 iv in prev) rects.Add(Rect.MinMaxRect(prevStart, iv.x, x, iv.y));
                    prevStart = x;
                }
                if (prev == null) prevStart = x;
                prev = free;
                if (x1 >= maxX - 0.001f)
                    foreach (Vector2 iv in free) rects.Add(Rect.MinMaxRect(prevStart, iv.x, x1, iv.y));
            }
            rects.RemoveAll(r => r.width < 0.4f || r.height < 0.4f);
            return rects;
        }

        static bool SameIntervals(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (Mathf.Abs(a[i].x - b[i].x) > 0.01f || Mathf.Abs(a[i].y - b[i].y) > 0.01f) return false;
            return true;
        }

        static Vector3[] Sag(Vector3 a, Vector3 b, float sag, int n)
        {
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                pts[i] = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            return pts;
        }

        static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(s_Root, false);
            return go.transform;
        }

        static Transform Node(Transform parent, string name, Vector3 worldPos, float yaw, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go.transform;
        }

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material m, string name, Quaternion? rot)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, string name = null, Quaternion? rot = null) =>
            Shape(PrimitiveType.Cube, p, pos, size, m, name, rot);

        /// <summary>Cylinder by diameter and height (Unity's is 1 wide, 2 tall).</summary>
        static GameObject Cyl(Transform p, Vector3 pos, float diameter, float height, Material m, string name = null, Quaternion? rot = null) =>
            Shape(PrimitiveType.Cylinder, p, pos, new Vector3(diameter, height * 0.5f, diameter), m, name, rot);

        static GameObject Ball(Transform p, Vector3 pos, Vector3 size, Material m, string name = null) =>
            Shape(PrimitiveType.Sphere, p, pos, size, m, name, null);

        static void Line(Transform parent, Vector3[] pts, float width, Material m)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.widthMultiplier = width;
            lr.sharedMaterial = m;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.numCapVertices = 0;
        }

        static void Text3D(Transform parent, string text, Vector3 localPos, Quaternion localRot, Vector2 box, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var t = go.AddComponent<TextMeshPro>();
            if (s_Font != null) t.font = s_Font;
            t.text = text;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.1f;
            t.fontSizeMax = 6f;
            t.rectTransform.sizeDelta = box;
            t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Glow() => Mat("LampGlow", new Color(1f, 0.88f, 0.6f), 0.2f, new Color(1.8f, 1.4f, 0.8f));

        /// <summary>Shared decor material (instanced); an emission colour makes it glow without a real light.</summary>
        static Material Mat(string name, Color color, float smoothness, Color? emission = null, string shader = "Universal Render Pipeline/Lit")
        {
            if (s_Mats.TryGetValue(name, out Material cached) && cached != null) return cached;
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Decor");
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            s_Mats[name] = m;
            return m;
        }

        static Material LineMat(string name, Color color) => Mat(name, color, 0f, null, "Universal Render Pipeline/Unlit");
    }
}
