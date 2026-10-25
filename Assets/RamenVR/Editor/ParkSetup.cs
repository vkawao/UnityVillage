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
    /// Menu: Ramen VR > Setup Selected Park  (house4_park model "SUAN TON MAI KONG POA")
    public static class ParkSetup
    {
        public static readonly string[] Steps =
        {
            "Turn the red valve to start the fountain",
            "Press START on the park board",
            "Throw a coin into the fountain",
            "Put the can in the trash bin",
            "Put bread by the pond for the ducks",
            "Push the swing",
            "Shake dad's big tree",
            "Rake the fallen leaves",
            "Teleport to the bench and sit down",
            "Let the balloon fly away"
        };

        static readonly string[] GrabItems = { "rian10baht", "krapong", "kanompangping", "kraadkongpoa", "loogpongloy" };
        static readonly string[] NoColliderWords = { "spot", "text", "spray", "fallingleaves", "string", "fruit", "toongbaimai", "tie", "leafpeek" };

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        static readonly Color Sky = new Color(0.3f, 0.6f, 0.9f);
        static readonly Color Green = new Color(0.3f, 0.8f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Ramen VR/Setup Selected Park")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null) { EditorUtility.DisplayDialog("Park", "Select the imported park model first.", "OK"); return; }
            Debug.Log(Run(root));
        }

        public static string Run(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "swingdriver" || t.name == "shakedriver" || t.name == "duckdriver" || t.name == "shakespot").ToArray())
                Object.DestroyImmediate(t.gameObject);

            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t && t.name == n);
            var missing = new List<string>();
            var dyn = new HashSet<Transform>();

            var grass = Find("grass");
            Vector3 parkC = grass ? S.Center(grass) : S.Center(root.transform);
            var archBoard = Find("archboard");
            Vector3 entrC = archBoard ? S.Center(archBoard) : parkC + Vector3.forward * 6.3f;
            entrC.y = root.transform.position.y;
            Vector3 outDir = entrC - parkC; outDir.y = 0;
            outDir = Mathf.Abs(outDir.z) > Mathf.Abs(outDir.x) ? new Vector3(0, 0, Mathf.Sign(outDir.z)) : new Vector3(Mathf.Sign(outDir.x), 0, 0);
            Vector3 rightDir = Vector3.Cross(Vector3.up, outDir);

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
                rb.isKinematic = true;
                var grab = S.GetOrAdd<XRGrabInteractable>(t.gameObject);
                grab.colliders.Clear(); grab.colliders.Add(box);
                grab.useDynamicAttach = false;
                grab.trackRotation = false;
                var at = t.Find("GrabAttach");
                if (!at) { at = new GameObject("GrabAttach").transform; at.SetParent(t, false); }
                at.position = new Vector3(b.center.x, b.min.y, b.center.z); at.rotation = t.rotation;
                grab.attachTransform = at;
                if (n == "loogpongloy")
                {
                    var br = S.GetOrAdd<BalloonRelease>(t.gameObject);
                    br.stepIndex = 9; br.requiredSteps = new[] { 1 }; br.notReadyHint = "Press START first!";
                }
                else
                {
                    S.GetOrAdd<SnapAssist>(t.gameObject);
                    S.GetOrAdd<DropOnRelease>(t.gameObject);
                }
            }

            // things that appear later
            var spray = Find("spray"); var leaves = Find("fallingleaves"); var bag = Find("toongbaimai");
            foreach (var t in new[] { spray, leaves, bag }) if (t) t.gameObject.SetActive(false);

            // ducks swim to the bread
            var duckDriver = new GameObject("duckdriver"); duckDriver.transform.SetParent(root.transform, false);
            var mv = duckDriver.AddComponent<MoveOnEnable>();
            mv.targets = all.Where(t => t && t.name.StartsWith("pedibest") && t.parent == root.transform).ToArray();
            mv.destination = Find("breadspot"); mv.stopShort = 0.35f; mv.duration = 3f;
            duckDriver.SetActive(false);

            // 2. sockets: (spot, item, step, required, radius, lock)
            var sockets = new (string spot, string item, int step, int[] req, float radius)[]
            {
                ("coinspot",  "rian10baht",    2, new[] { 0, 1 }, 0.45f),
                ("binspot",   "krapong",       3, new[] { 1 },    0.35f),
                ("breadspot", "kanompangping", 4, new[] { 1 },    0.4f),
                ("leafspot",  "kraadkongpoa",  7, new[] { 6 },    0.6f),
            };
            var made = new Dictionary<string, RamenSocket>();
            foreach (var d in sockets)
            {
                var t = Find(d.spot);
                if (t == null) { missing.Add(d.spot); continue; }
                var sc = S.GetOrAdd<SphereCollider>(t.gameObject);
                sc.isTrigger = true; sc.radius = d.radius / Mathf.Max(0.0001f, t.lossyScale.x);
                var s = S.GetOrAdd<RamenSocket>(t.gameObject);
                s.acceptedName = d.item; s.stepIndex = d.step; s.requiredSteps = d.req;
                s.hoverSocketSnapping = true; s.socketSnappingRadius = d.radius;
                s.lockOnPlace = true;
                made[d.spot] = s;
            }
            if (made.TryGetValue("breadspot", out var bs)) bs.showOnPlace = new[] { duckDriver };
            if (made.TryGetValue("leafspot", out var ls))
            {
                if (leaves) ls.hideOnPlace = new[] { leaves.gameObject };
                if (bag) ls.showOnPlace = new[] { bag.gameObject };
            }

            // 3. moving things
            var valve = Find("valve");
            var fountain = Find("fountain");
            var seat = Find("chingcha");
            var frame = Find("swingframe");
            var tree = Find("tonmaipoa");
            var canopy = Find("canopy");
            var bench = Find("maanangkonhaeo");
            var benchSpot = Find("benchspot");

            GameObject swingDriver = null;
            if (seat)
            {
                swingDriver = new GameObject("swingdriver"); swingDriver.transform.SetParent(root.transform, false);
                var sw = swingDriver.AddComponent<Swing>();
                sw.target = seat;
                Vector3 bar = Vector3.right;
                if (frame) { var fb = S.Bounds(frame); bar = fb.size.x >= fb.size.z ? Vector3.right : Vector3.forward; }
                sw.localAxis = seat.parent.InverseTransformDirection(bar);
                sw.angle = 25f; sw.period = 2.2f; sw.duration = 0f;
                swingDriver.SetActive(false);
            }
            GameObject shakeDriver = null;
            if (canopy)
            {
                shakeDriver = new GameObject("shakedriver"); shakeDriver.transform.SetParent(root.transform, false);
                var sw = shakeDriver.AddComponent<Swing>();
                sw.target = canopy; sw.localAxis = canopy.parent.InverseTransformDirection(rightDir);
                sw.angle = 4f; sw.period = 0.35f; sw.duration = 2f;
                shakeDriver.SetActive(false);
            }

            // bench: a teleport anchor that sits you down facing the fountain, and the step finishes when you are there
            if (bench && benchSpot)
            {
                dyn.Add(bench);
                var b = S.Bounds(bench);
                var box = S.GetOrAdd<BoxCollider>(bench.gameObject);
                box.center = bench.InverseTransformPoint(b.center); box.size = AbsLocal(bench, b.size);
                Vector3 look = (fountain ? S.Center(fountain) : parkC) - benchSpot.position; look.y = 0;
                benchSpot.rotation = Quaternion.LookRotation(look.normalized);
                var anchor = S.GetOrAdd<TeleportationAnchor>(bench.gameObject);
                anchor.teleportAnchorTransform = benchSpot;
                anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
                var z = S.GetOrAdd<ZoneStep>(benchSpot.gameObject);
                z.stepIndex = 8; z.requiredSteps = new[] { 1 }; z.radius = 0.8f;
            }
            else missing.Add("bench");

            // 4. static colliders for everything else
            int statics = 0;
            foreach (var t in new[] { valve, seat, tree, canopy }) if (t) dyn.Add(t);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (S.IsUnder(mf.transform, dyn)) continue;
                if (NoColliderWords.Any(w => mf.name.ToLower().Contains(w))) continue;
                if (mf.GetComponent<Collider>()) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                mf.gameObject.isStatic = true;
                statics++;
            }
            if (canopy) foreach (var mf in canopy.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<Collider>() && !mf.name.Contains("fruit")) mf.gameObject.AddComponent<MeshCollider>();

            // 5. walkable ground
            foreach (var n in new[] { "grass", "path" }) { var t = Find(n); if (t) S.GetOrAdd<TeleportationArea>(t.gameObject); }
            if (Find("Ground") == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Ground";
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(parkC.x, root.transform.position.y - 0.05f, parkC.z);
                g.transform.localScale = new Vector3(2.4f, 1, 2.4f);
                g.GetComponent<Renderer>().sharedMaterial = S.MakeMat("Ground_Grey", new Color(0.35f, 0.36f, 0.35f), false);
                g.AddComponent<TeleportationArea>();
            }

            // 6. game manager + UI + buttons
            var gm = root.GetComponentInChildren<RamenGameManager>(true);
            if (gm == null)
            {
                var gmGo = new GameObject("Park_GameManager");
                gmGo.transform.SetParent(root.transform, false);
                gm = gmGo.AddComponent<RamenGameManager>();
            }
            gm.shopRoot = root.transform;
            gm.stepLabels = Steps;
            gm.showDistance = 7f;

            BuildUI(root.transform, gm, Find, parkC, entrC, outDir, rightDir);

            MakeButton(valve, 0, null, "", dyn, missing, b =>
            {
                Vector3 axis = valve.position - (fountain ? S.Center(fountain) : parkC); axis.y = 0;
                b.rotateTarget = valve; b.rotateAxis = axis.sqrMagnitude > 1e-6f ? axis.normalized : outDir; b.rotateAngle = 150f; b.duration = 1.0f;
                if (spray) b.showObjects = new[] { spray.gameObject };
                b.showOrderPanel = true;
            });
            MakeButton(seat, 5, new[] { 1 }, "Press START first!", dyn, missing, b =>
            {
                if (swingDriver) b.showObjects = new[] { swingDriver };
                b.duration = 0.2f;
            });
            MakeButton(tree, 6, new[] { 1 }, "Press START first!", dyn, missing, b =>
            {
                var list = new List<GameObject>();
                if (leaves) list.Add(leaves.gameObject);
                if (shakeDriver) list.Add(shakeDriver);
                b.showObjects = list.ToArray();
                if (leaves)
                {
                    var lb = S.Bounds(leaves);
                    b.moveTarget = leaves; b.moveOffset = Vector3.down * (lb.min.y - root.transform.position.y - 0.03f);
                }
                b.duration = 2.0f;
            });
            if (tree)
            {
                var tr = tree.GetComponent<Renderer>();
                var box = tree.GetComponent<BoxCollider>();
                if (tr && box)
                {
                    box.center = tree.InverseTransformPoint(tr.bounds.center);
                    box.size = AbsLocal(tree, tr.bounds.size);   // just the trunk, so the rake next to it can be grabbed
                }
                var ss = new GameObject("shakespot").transform; ss.SetParent(root.transform, false);
                Vector3 toPark = parkC - tree.position; toPark.y = 0;
                ss.position = tree.position + toPark.normalized * 0.55f + Vector3.up * 1.3f;
                if (gm.pickTargets != null && gm.pickTargets.Length > 6) gm.pickTargets[6] = ss;
            }
            S.EnsureEventSystem();

            // 7. player starts outside the entrance, facing in
            if (Object.FindObjectsByType<RamenGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)
            {
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Vector3 spawn = entrC + outDir * 2.6f;
                if (origin) { origin.transform.position = spawn; origin.transform.rotation = Quaternion.LookRotation(-outDir); }
            }
            var xr = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xr) S.LongRays(xr.gameObject, 25f);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            string msg = $"Park setup done. Static colliders: {statics}. Ducks: {mv.targets.Length}.";
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
            box.size = Vector3.Max(AbsLocal(t, b.size) * 1.3f, Vector3.one * 0.08f);
            S.GetOrAdd<XRSimpleInteractable>(t.gameObject);
            var sb = S.GetOrAdd<StepButton>(t.gameObject);
            sb.stepIndex = step; sb.requiredSteps = req; sb.notReadyHint = hint;
            sb.onPressed = new UnityEngine.Events.UnityEvent();
            extra?.Invoke(sb);
        }

        static void BuildUI(Transform root, RamenGameManager gm, System.Func<string, Transform> F, Vector3 parkC, Vector3 entrC, Vector3 outDir, Vector3 rightDir)
        {
            var old = root.Find("Park_UI");
            if (old) Object.DestroyImmediate(old.gameObject);
            var uiRoot = new GameObject("Park_UI");
            uiRoot.transform.SetParent(root, false);
            Quaternion rot = Quaternion.LookRotation(-outDir);   // signs read while walking in
            Quaternion boardRot = Quaternion.LookRotation(outDir);   // the checklist is read from inside the park

            // ---- checklist board on the map board by the entrance ----
            var map = F("mapboard");
            Vector3 boardPos = map ? S.Center(map) : entrC - outDir * 1.0f;
            boardPos.y = root.position.y + 2.25f;
            boardPos -= outDir * 0.05f;

            var steps = gm.Steps; int n = steps.Length;
            var board = S.MakeCanvas("Checklist_Board", uiRoot.transform, boardPos, boardRot, new Vector2(760, 900), 0.0009f);
            S.MakePanel(board.transform, "BG", Bg, Vector2.zero, new Vector2(760, 900));
            S.MakePanel(board.transform, "TopBar", Sky, new Vector2(0, 420), new Vector2(760, 60));
            S.MakeText(board.transform, "Title", "SUAN TON MAI KONG POA", 36, Color.white, new Vector2(0, 420), new Vector2(740, 60), true);
            S.MakeText(board.transform, "Sub", "Help dad look after his park!", 24, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 360), new Vector2(740, 40));
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
            S.MakePanel(done, "Glow", new Color(0.3f, 0.6f, 0.9f, 0.1f), new Vector2(0, 140), new Vector2(760, 360));
            S.MakePanel(done, "Banner", Sky, new Vector2(0, 385), new Vector2(760, 130));
            S.MakeText(done, "Title", "HAPPY PARK!", 68, Color.white, new Vector2(0, 385), new Vector2(740, 120), true);
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 90 : 64;
                var d = S.MakePanel(done, "Star" + i, Gold, new Vector2((i - 1) * 130, i == 1 ? 225 : 205), new Vector2(s, s));
                d.localRotation = Quaternion.Euler(0, 0, 45);
                var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.5f, 0.3f, 0f); o.effectDistance = new Vector2(4, -4);
            }
            S.MakeText(done, "Thanks", "Khob jai na, kong poa!", 64, Gold, new Vector2(0, 90), new Vector2(740, 110), true).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            S.MakeText(done, "Sub", "Dad's park is clean and full of fun!", 26, new Color(0.85f, 0.85f, 0.9f), new Vector2(0, 15), new Vector2(720, 50));
            S.MakePanel(done, "Divider", new Color(1, 1, 1, 0.12f), new Vector2(0, -30), new Vector2(620, 3));
            var statsBg = S.MakePanel(done, "StatsBG", new Color(1, 1, 1, 0.06f), new Vector2(0, -95), new Vector2(620, 80));
            gm.doneStatsText = S.MakeText(statsBg, "Stats", "10 / 10 steps", 34, Color.white, Vector2.zero, new Vector2(600, 80), true);
            S.MakeText(done, "Visit", "Thank you for visiting\nSUAN TON MAI KONG POA", 32, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, -195), new Vector2(700, 90));
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
            S.MakePanel(hud.transform, "Accent", Sky, new Vector2(-305, 0), new Vector2(10, 70));
            gm.hudText = S.MakeText(hud.transform, "Text", "", 32, Color.white, new Vector2(5, 0), new Vector2(590, 70));
            hud.gameObject.AddComponent<LazyHud>();

            // ---- order board next to the fountain valve ----
            var valve = F("valve");
            Vector3 orderPos = (valve ? valve.position : parkC) + rightDir * 1.3f; orderPos.y = root.position.y + 1.6f;
            var order = S.MakeCanvas("Order_Panel", uiRoot.transform, orderPos, rot, new Vector2(520, 330), 0.0013f);
            S.MakePanel(order.transform, "BG", Bg, Vector2.zero, new Vector2(520, 330));
            S.MakePanel(order.transform, "TopBar", Sky, new Vector2(0, 140), new Vector2(520, 50));
            S.MakeText(order.transform, "Head", "DAD'S LIST", 32, Color.white, new Vector2(0, 140), new Vector2(500, 50), true);
            S.MakeText(order.transform, "Text", "Clean up and have fun\nin dad's park!", 30, new Color(0.9f, 0.9f, 0.92f), new Vector2(0, 50), new Vector2(500, 110));
            var btn = S.MakePanel(order.transform, "Start_Button", Green, new Vector2(0, -90), new Vector2(380, 100));
            S.MakeText(btn, "Label", "START", 42, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var button = btn.gameObject.AddComponent<Button>(); button.targetGraphic = btn.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, gm.StartOrder);
            var bc = btn.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(380, 100, 40);
            btn.gameObject.AddComponent<XRSimpleInteractable>();
            btn.gameObject.AddComponent<StartOrderButton>();
            gm.orderPanel = order.gameObject;

            // ---- welcome sign under the entrance arch ----
            {
                Vector3 p = entrC + Vector3.up * 1.95f + outDir * 0.2f;
                var sign = S.MakeCanvas("Welcome_Sign", uiRoot.transform, p, rot, new Vector2(520, 120), 0.0016f);
                S.MakePanel(sign.transform, "BG", Bg, Vector2.zero, new Vector2(520, 120));
                S.MakeText(sign.transform, "Text", "WELCOME!\nTurn the red valve at the fountain", 32, Color.white, Vector2.zero, new Vector2(500, 110));
            }

            // ---- guide arrows ----
            gm.pickArrow = S.MakeArrow(uiRoot.transform, "Arrow_Pick", "THIS", new Color(1f, 0.42f, 0.25f));
            gm.placeArrow = S.MakeArrow(uiRoot.transform, "Arrow_Place", "PUT HERE", Green);
            string[] pick = { "valve", null, "rian10baht", "krapong", "kanompangping", "chingcha", "shakespot", "kraadkongpoa", "maanangkonhaeo", "loogpongloy" };
            string[] place = { null, null, "coinspot", "bin", "breadspot", null, null, "leafspot", null, null };
            gm.pickTargets = pick.Select(x => x == null ? null : F(x)).ToArray();
            gm.placeTargets = place.Select(x => x == null ? null : F(x)).ToArray();
            gm.pickTargetsAlt = new Transform[10]; gm.placeTargetsAlt = new Transform[10];
            foreach (var img in uiRoot.GetComponentsInChildren<Image>(true)) img.raycastTarget = img.GetComponent<Button>() != null;
        }
    }
}
