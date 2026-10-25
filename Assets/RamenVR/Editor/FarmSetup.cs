using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using S = RamenVR.EditorTools.RamenShopSetup;

namespace RamenVR.EditorTools
{
    /// Menu: Ramen VR > Setup Selected Farm  (house5_farm model "RAI KAI TOD")
    public static class FarmSetup
    {
        public static readonly string[] Steps =
        {
            "Pull the lever to open the gate",
            "Press START on the order board",
            "Plant the seeds in the soil",
            "Fill the watering can at the pump",
            "Water the plot",
            "Pull a carrot, put it in the basket",
            "Pick a cabbage, put it in the basket",
            "Put an egg in the egg tray",
            "Put the basket and egg tray on the cart",
            "Press DELIVER"
        };

        static readonly string[] GrabItems = { "seeds", "can", "carrot", "cabbage", "egg", "basket", "eggtray" };

        static readonly string[] NoColliderWords = { "spot", "inside", "text", "grow" };

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        static readonly Color Leaf = new Color(0.3f, 0.65f, 0.3f);
        static readonly Color Green = new Color(0.3f, 0.8f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Ramen VR/Setup Selected Farm")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null) { EditorUtility.DisplayDialog("Farm", "Select the imported farm model first.", "OK"); return; }
            Debug.Log(Run(root));
        }

        public static string Run(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // remove growtriggers left over from earlier runs
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                if (root.transform.GetChild(i).name == "growtrigger") Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            var missing = new List<string>();
            var dyn = new HashSet<Transform>();

            var grass = Find("grass");
            Vector3 farmC = grass ? S.Center(grass) : S.Center(root.transform);
            var gp0 = Find("gatepost"); var gp1 = Find("gatepost.001");
            Vector3 gateC = gp0 && gp1 ? (S.Center(gp0) + S.Center(gp1)) * 0.5f : farmC + Vector3.back * 4.5f;
            gateC.y = root.transform.position.y;
            Vector3 outDir = gateC - farmC; outDir.y = 0; outDir.Normalize();

            // 1. grabbable items
            foreach (var n in GrabItems)
            {
                var t = Find(n);
                if (t == null) { missing.Add(n); continue; }
                dyn.Add(t);
                var b = S.Bounds(t);
                var box = S.GetOrAdd<BoxCollider>(t.gameObject);
                box.center = t.InverseTransformPoint(b.center);
                box.size = AbsLocal(t, b.size);
                var rb = S.GetOrAdd<Rigidbody>(t.gameObject);
                rb.mass = 0.3f; rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.isKinematic = true;   // items stay where they are put, nothing rolls away
                var grab = S.GetOrAdd<XRGrabInteractable>(t.gameObject);
                grab.colliders.Clear(); grab.colliders.Add(box);
                grab.useDynamicAttach = false;
                grab.trackRotation = false;   // stays upright while carried, easy to put down
                var at = t.Find("GrabAttach");
                if (!at) { at = new GameObject("GrabAttach").transform; at.SetParent(t, false); }
                at.position = new Vector3(b.center.x, b.min.y, b.center.z); at.rotation = t.rotation;
                grab.attachTransform = at;
                S.GetOrAdd<SnapAssist>(t.gameObject);
                S.GetOrAdd<DropOnRelease>(t.gameObject);
            }

            // a second spot inside the basket for the cabbage
            var basket = Find("basket");
            var carrotIn = Find("basketinside");
            Transform cabbageIn = basket ? basket.Find("cabbagespot") : null;
            if (basket && carrotIn && !cabbageIn)
            {
                cabbageIn = new GameObject("cabbagespot").transform;
                cabbageIn.SetParent(basket, false);
                var bb = S.Bounds(basket);
                Vector3 side = Vector3.ProjectOnPlane(basket.right, Vector3.up).normalized;
                S.PlaceInside(carrotIn, basket, 0.35f, -side * bb.extents.x * 0.45f);
                S.PlaceInside(cabbageIn, basket, 0.35f, side * bb.extents.x * 0.45f);
            }

            S.PlaceInside(Find("trayinside"), Find("eggtray"), 0.6f);

            // the cart spots ride along with the cart
            var cart = Find("cart");
            foreach (var n in new[] { "basketspot", "trayspot" }) { var t = Find(n); if (t && cart) t.SetParent(cart, true); }

            // 2. sockets: (item, step, required, radius, lock)
            var sockets = new (string spot, string item, int step, int[] req, float radius, bool lockIt)[]
            {
                ("seedspot",     "seeds",   2, new[] { 1 },       0.35f, true),
                ("pumpspot",     "can",     3, new[] { 2 },       0.3f,  false),
                ("waterspot",    "can",     4, new[] { 3 },       0.4f,  true),
                ("basketinside", "carrot",  5, new[] { 4 },       0.22f, true),
                ("cabbagespot",  "cabbage", 6, new[] { 4 },       0.22f, true),
                ("trayinside",   "egg",     7, new[] { 1 },       0.15f, true),
                ("basketspot",   "basket",  8, new[] { 5, 6, 7 }, 0.3f,  true),
                ("trayspot",     "eggtray", 8, new[] { 5, 6, 7 }, 0.3f,  true),
            };
            var made = new Dictionary<string, RamenSocket>();
            foreach (var d in sockets)
            {
                var t = d.spot == "cabbagespot" ? cabbageIn : Find(d.spot);
                if (t == null) { missing.Add(d.spot); continue; }
                var sc = S.GetOrAdd<SphereCollider>(t.gameObject);
                sc.isTrigger = true; sc.radius = d.radius / Mathf.Max(0.0001f, t.lossyScale.x);
                var s = S.GetOrAdd<RamenSocket>(t.gameObject);
                s.acceptedName = d.item; s.stepIndex = d.step; s.requiredSteps = d.req;
                s.hoverSocketSnapping = true; s.socketSnappingRadius = d.radius;
                s.lockOnPlace = d.lockIt;
                made[d.spot] = s;
            }
            if (made.TryGetValue("basketspot", out var bs) && made.TryGetValue("trayspot", out var ts)) { bs.partner = ts; ts.partner = bs; bs.waitHint = "Now put the egg tray on the cart"; ts.waitHint = "Now put the basket on the cart"; }
            var seeds = Find("seeds");
            if (made.TryGetValue("seedspot", out var ss) && seeds) ss.hideOnPlace = new[] { seeds.gameObject };   // planted

            // watering: the can tips over the plot, then the vegetables grow
            var can = Find("can");
            if (made.TryGetValue("waterspot", out var ws) && can)
            {
                var spout = can.Find("spout");
                Vector3 v = spout ? can.InverseTransformPoint(S.Center(spout)) - can.InverseTransformPoint(S.Center(can)) : Vector3.right;
                v = Vector3.Scale(v, can.lossyScale); v.y = 0;
                if (v.sqrMagnitude < 1e-6f) v = Vector3.right;
                Vector3 axis = Vector3.Cross(Vector3.up, v.normalized);
                Quaternion q = Quaternion.AngleAxis(40f, axis);
                if ((q * v).y > 0) q = Quaternion.AngleAxis(-40f, axis);
                ws.transform.rotation = q;
            }
            var plants = all.Where(t => (t.name.StartsWith("carrot") || t.name.StartsWith("cabbage")) && t.parent == root.transform).ToArray();
            var growGo = new GameObject("growtrigger");
            growGo.transform.SetParent(root.transform, false);
            // safe to run again: one Grow only, and full size is always the model's own size (1)
            foreach (var old in growGo.GetComponents<Grow>()) Object.DestroyImmediate(old);
            var grow = growGo.AddComponent<Grow>();
            foreach (var pl in plants) pl.localScale = Vector3.one;
            grow.targets = plants; grow.fullScales = plants.Select(p => p.localScale).ToArray();
            foreach (var p in plants)
            {
                p.localScale *= 0.25f;   // tiny until watered
                if (p.TryGetComponent<XRGrabInteractable>(out var g)) g.enabled = false;
            }
            growGo.SetActive(false);
            if (ws) ws.showOnPlace = new[] { growGo };

            // 3. moving things: gate doors, lever, cart
            var doors = new List<HingeDoor>();
            foreach (var n in new[] { "gatedoor", "gatedoor.001" })
            {
                var d = Find(n);
                if (!d) { missing.Add(n); continue; }
                dyn.Add(d);
                var h = S.GetOrAdd<HingeDoor>(d.gameObject);
                Vector3 arm = S.Center(d) - d.position; arm.y = 0;
                Vector3 swung = Quaternion.AngleAxis(95f, Vector3.up) * arm;
                h.openAngle = Vector3.Dot(swung - arm, -outDir) > 0 ? 95f : -95f;   // swing into the farm
                h.duration = 1.2f;
                var rb = S.GetOrAdd<Rigidbody>(d.gameObject); rb.isKinematic = true;
                foreach (var mf in d.GetComponentsInChildren<MeshFilter>()) S.GetOrAdd<BoxCollider>(mf.gameObject);
                doors.Add(h);
            }
            var lever = Find("lever"); if (lever) dyn.Add(lever);
            if (cart)
            {
                dyn.Add(cart);
                var rb = S.GetOrAdd<Rigidbody>(cart.gameObject); rb.isKinematic = true;
                foreach (var mf in cart.GetComponentsInChildren<MeshFilter>()) S.GetOrAdd<BoxCollider>(mf.gameObject);
            }
            var deliver = Find("deliverbutton");

            // 4. static colliders for everything else
            int statics = 0;
            if (deliver) dyn.Add(deliver);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (S.IsUnder(mf.transform, dyn)) continue;
                if (NoColliderWords.Any(w => mf.name.ToLower().Contains(w))) continue;
                if (mf.GetComponent<Collider>()) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                mf.gameObject.isStatic = true;
                statics++;
            }

            // 5. walkable ground
            if (grass) S.GetOrAdd<TeleportationArea>(grass.gameObject);
            var path = Find("path"); if (path) S.GetOrAdd<TeleportationArea>(path.gameObject);
            if (Find("Ground") == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Ground";
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(farmC.x, root.transform.position.y - 0.001f, farmC.z);
                g.transform.localScale = new Vector3(4, 1, 4);
                g.GetComponent<Renderer>().sharedMaterial = S.MakeMat("Ground_Grey", new Color(0.35f, 0.36f, 0.35f), false);
                g.AddComponent<TeleportationArea>();
            }

            // 6. game manager + UI + buttons
            var gm = root.GetComponentInChildren<RamenGameManager>(true);
            if (gm == null)
            {
                var gmGo = new GameObject("Farm_GameManager");
                gmGo.transform.SetParent(root.transform, false);
                gm = gmGo.AddComponent<RamenGameManager>();
            }
            gm.shopRoot = root.transform;
            gm.stepLabels = Steps;
            gm.showDistance = 4f;

            BuildUI(root.transform, gm, Find, farmC, gateC, outDir);

            MakeButton(lever, 0, null, "", dyn, missing, b =>
            {
                b.rotateTarget = lever; b.rotateAxis = Vector3.Cross(Vector3.up, outDir); b.rotateAngle = 60f;
                b.openDoors = doors.ToArray(); b.showOrderPanel = true; b.duration = 0.6f;
            });
            MakeButton(deliver, 9, new[] { 8 }, "Put the basket and the egg tray on the cart first!", dyn, missing, b =>
            {
                b.moveTarget = cart; b.moveOffset = outDir * 7f; b.duration = 3f;
            });
            S.EnsureEventSystem();

            // 7. player starts outside the gate, facing the farm
            if (Object.FindObjectsByType<RamenGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)
            {
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Vector3 spawn = gateC + outDir * 2.6f;
                if (origin) { origin.transform.position = spawn; origin.transform.rotation = Quaternion.LookRotation(-outDir); }
            }
            var xr = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xr) S.LongRays(xr.gameObject, 25f);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            string msg = $"Farm setup done. Static colliders: {statics}. Plants: {plants.Length}.";
            if (missing.Count > 0) msg += " NOT FOUND: " + string.Join(", ", missing);
            return msg;
        }

        static Vector3 AbsLocal(Transform t, Vector3 worldSize)
        {
            Vector3 x = t.InverseTransformVector(new Vector3(worldSize.x, 0, 0));
            Vector3 y = t.InverseTransformVector(new Vector3(0, worldSize.y, 0));
            Vector3 z = t.InverseTransformVector(new Vector3(0, 0, worldSize.z));
            var s = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return Vector3.Max(s, Vector3.one * 0.03f);
        }

        static void MakeButton(Transform t, int step, int[] req, string hint, HashSet<Transform> dyn, List<string> missing, System.Action<StepButton> extra)
        {
            if (t == null) { missing.Add("button"); return; }
            dyn.Add(t);
            var b = S.Bounds(t);
            var box = S.GetOrAdd<BoxCollider>(t.gameObject);
            box.center = t.InverseTransformPoint(b.center);
            box.size = Vector3.Max(AbsLocal(t, b.size) * 1.6f, Vector3.one * 0.08f);
            S.GetOrAdd<XRSimpleInteractable>(t.gameObject);
            var sb = S.GetOrAdd<StepButton>(t.gameObject);
            sb.stepIndex = step; sb.requiredSteps = req; sb.notReadyHint = hint;
            extra?.Invoke(sb);
        }

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 f = to - from; f.y = 0;
            return Quaternion.LookRotation(f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward);
        }

        static void BuildUI(Transform root, RamenGameManager gm, System.Func<string, Transform> F, Vector3 farmC, Vector3 gateC, Vector3 outDir)
        {
            var old = root.Find("Farm_UI");
            if (old) Object.DestroyImmediate(old.gameObject);
            var uiRoot = new GameObject("Farm_UI");
            uiRoot.transform.SetParent(root, false);
            Vector3 viewer = gateC - outDir * 0.5f + Vector3.up * 1.6f;   // just inside the gate

            // ---- checklist board between the gate and the pump ----
            var pump = F("pump");
            Vector3 boardPos = pump ? Vector3.Lerp(gateC, S.Center(pump), 0.5f) : farmC;
            boardPos.y = root.position.y + 1.75f;
            Quaternion rot = Facing(viewer, boardPos);

            var steps = gm.Steps; int n = steps.Length;
            var board = S.MakeCanvas("Checklist_Board", uiRoot.transform, boardPos, rot, new Vector2(760, 900), 0.0009f);
            S.MakePanel(board.transform, "BG", Bg, Vector2.zero, new Vector2(760, 900));
            S.MakePanel(board.transform, "TopBar", Leaf, new Vector2(0, 420), new Vector2(760, 60));
            S.MakeText(board.transform, "Title", "RAI KAI TOD", 40, Color.white, new Vector2(0, 420), new Vector2(740, 60), true);
            S.MakeText(board.transform, "Sub", "Grow veggies, collect eggs, send them to market!", 24, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 360), new Vector2(740, 40));
            gm.rowBg = new Image[n]; gm.rowDot = new Image[n]; gm.rowNum = new Text[n]; gm.rowLabel = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float y = 290 - i * 62;
                var row = S.MakePanel(board.transform, "Row" + (i + 1), new Color(1, 1, 1, 0.03f), new Vector2(0, y), new Vector2(700, 54));
                gm.rowBg[i] = row.GetComponent<Image>();
                var dot = S.MakePanel(row, "Dot", new Color(0.35f, 0.35f, 0.38f), new Vector2(-315, 0), new Vector2(46, 46));
                gm.rowDot[i] = dot.GetComponent<Image>();
                gm.rowNum[i] = S.MakeText(dot, "Num", (i + 1).ToString(), 24, Color.white, Vector2.zero, new Vector2(46, 46), true);
                var lab = S.MakeText(row, "Label", steps[i], 28, Color.white, new Vector2(40, 0), new Vector2(600, 54));
                lab.alignment = TextAnchor.MiddleLeft;
                gm.rowLabel[i] = lab;
            }
            var barBg = S.MakePanel(board.transform, "ProgressBG", new Color(1, 1, 1, 0.1f), new Vector2(-40, -375), new Vector2(560, 26));
            var fill = S.MakePanel(barBg, "Fill", Green, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero; fill.pivot = new Vector2(0, 0.5f);
            gm.progressFill = fill;
            gm.progressText = S.MakeText(board.transform, "Progress", "0 / 10", 30, Color.white, new Vector2(300, -375), new Vector2(140, 40));
            gm.hintText = S.MakeText(board.transform, "Hint", "", 30, Gold, new Vector2(0, -420), new Vector2(740, 44));

            // ---- finish screen ----
            var done = S.MakePanel(board.transform, "Done_Panel", new Color(0.06f, 0.06f, 0.08f, 0.98f), Vector2.zero, new Vector2(760, 900));
            S.MakePanel(done, "Glow", new Color(0.3f, 0.65f, 0.3f, 0.1f), new Vector2(0, 140), new Vector2(760, 360));
            S.MakePanel(done, "Banner", Leaf, new Vector2(0, 385), new Vector2(760, 130));
            S.MakeText(done, "Title", "DELIVERED!", 68, Color.white, new Vector2(0, 385), new Vector2(740, 120), true);
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 90 : 64;
                var d = S.MakePanel(done, "Star" + i, Gold, new Vector2((i - 1) * 130, i == 1 ? 225 : 205), new Vector2(s, s));
                d.localRotation = Quaternion.Euler(0, 0, 45);
                var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.5f, 0.3f, 0f); o.effectDistance = new Vector2(4, -4);
            }
            S.MakeText(done, "Thanks", "Khob khun krub!", 80, Gold, new Vector2(0, 90), new Vector2(740, 110), true).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            S.MakeText(done, "Sub", "Fresh veggies and eggs are on the way to the market!", 26, new Color(0.85f, 0.85f, 0.9f), new Vector2(0, 15), new Vector2(720, 50));
            S.MakePanel(done, "Divider", new Color(1, 1, 1, 0.12f), new Vector2(0, -30), new Vector2(620, 3));
            var statsBg = S.MakePanel(done, "StatsBG", new Color(1, 1, 1, 0.06f), new Vector2(0, -95), new Vector2(620, 80));
            gm.doneStatsText = S.MakeText(statsBg, "Stats", "10 / 10 steps", 34, Color.white, Vector2.zero, new Vector2(600, 80), true);
            S.MakeText(done, "Visit", "Thank you for visiting\nRAI KAI TOD", 32, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, -195), new Vector2(700, 90));
            var again = S.MakePanel(done, "PlayAgain_Button", Green, new Vector2(0, -340), new Vector2(380, 100));
            S.MakeText(again, "Label", "PLAY AGAIN", 44, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var ab = again.gameObject.AddComponent<Button>(); ab.targetGraphic = again.GetComponent<Image>();
            var abc = again.gameObject.AddComponent<BoxCollider>(); abc.size = new Vector3(380, 100, 40);
            again.gameObject.AddComponent<XRSimpleInteractable>();
            var restart = again.gameObject.AddComponent<RestartButton>();
            UnityEventTools.AddPersistentListener(ab.onClick, restart.Restart);
            gm.donePanel = done.gameObject;
            gm.confettiMaterial = S.ParticleMat();

            // ---- floating NEXT HUD ----
            var hud = S.MakeCanvas("Next_HUD", uiRoot.transform, boardPos, rot, new Vector2(620, 70), 0.0008f);
            S.MakePanel(hud.transform, "BG", new Color(0.07f, 0.07f, 0.09f, 0.8f), Vector2.zero, new Vector2(620, 70));
            S.MakePanel(hud.transform, "Accent", Leaf, new Vector2(-305, 0), new Vector2(10, 70));
            gm.hudText = S.MakeText(hud.transform, "Text", "", 32, Color.white, new Vector2(5, 0), new Vector2(590, 70));
            hud.gameObject.AddComponent<LazyHud>();

            // ---- order board above the work table ----
            var table = F("table");
            Vector3 orderPos = table ? S.Center(table) + Vector3.up * 1.0f : farmC + Vector3.up * 1.6f;
            var order = S.MakeCanvas("Order_Panel", uiRoot.transform, orderPos, Facing(viewer, orderPos), new Vector2(520, 330), 0.0013f);
            S.MakePanel(order.transform, "BG", Bg, Vector2.zero, new Vector2(520, 330));
            S.MakePanel(order.transform, "TopBar", Leaf, new Vector2(0, 140), new Vector2(520, 50));
            S.MakeText(order.transform, "Head", "MARKET ORDER", 32, Color.white, new Vector2(0, 140), new Vector2(500, 50), true);
            S.MakeText(order.transform, "Text", "1 carrot - 1 cabbage - 1 egg\nby cart, today!", 30, new Color(0.9f, 0.9f, 0.92f), new Vector2(0, 50), new Vector2(500, 110));
            var btn = S.MakePanel(order.transform, "Start_Button", Green, new Vector2(0, -90), new Vector2(380, 100));
            S.MakeText(btn, "Label", "START", 42, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var button = btn.gameObject.AddComponent<Button>(); button.targetGraphic = btn.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, gm.StartOrder);
            var bc = btn.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(380, 100, 40);
            btn.gameObject.AddComponent<XRSimpleInteractable>();
            btn.gameObject.AddComponent<StartOrderButton>();
            gm.orderPanel = order.gameObject;

            // ---- welcome sign next to the lever (outside) ----
            var lb = F("leverbase");
            if (lb)
            {
                Vector3 p = S.Center(lb) + Vector3.up * 0.95f + outDir * 0.05f;
                var sign = S.MakeCanvas("Welcome_Sign", uiRoot.transform, p, Quaternion.LookRotation(-outDir), new Vector2(440, 120), 0.0016f);
                S.MakePanel(sign.transform, "BG", Bg, Vector2.zero, new Vector2(440, 120));
                S.MakeText(sign.transform, "Text", "WELCOME!\nPull the lever to open", 34, Color.white, Vector2.zero, new Vector2(420, 110));
            }

            // ---- guide arrows ----
            gm.pickArrow = S.MakeArrow(uiRoot.transform, "Arrow_Pick", "THIS", new Color(1f, 0.42f, 0.25f));
            gm.placeArrow = S.MakeArrow(uiRoot.transform, "Arrow_Place", "PUT HERE", Green);
            string[] pick = { "lever", null, "seeds", "can", "can", "carrot", "cabbage", "egg", "basket", "deliverbutton" };
            string[] place = { null, null, "plot", "pump", "plot", "basket", "basket", "eggtray", "basketspot", null };
            gm.pickTargets = pick.Select(x => x == null ? null : F(x)).ToArray();
            gm.placeTargets = place.Select(x => x == null ? null : F(x)).ToArray();
            gm.pickTargetsAlt = new Transform[10]; gm.placeTargetsAlt = new Transform[10];
            gm.pickTargetsAlt[8] = F("eggtray"); gm.placeTargetsAlt[8] = F("trayspot");
            foreach (var img in uiRoot.GetComponentsInChildren<Image>(true)) img.raycastTarget = img.GetComponent<Button>() != null;
        }
    }
}
