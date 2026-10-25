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
    /// Menu: Ramen VR > Setup Selected Fish Shop  (house2_fish model "KAI PLA NA JA")
    public static class FishShopSetup
    {
        public static readonly string[] Steps =
        {
            "Press the green button (OPEN)",
            "Press TAKE ORDER",
            "Scoop a fish with the net",
            "Put the fish on the scale",
            "Press the red WEIGH button",
            "Put the fish on the cutting board",
            "Cut the fish with the knife",
            "Put the cut fish in the bag",
            "Give the bag to the customer",
            "Press GET PAID at the till"
        };

        static readonly string[] GrabItems = { "net", "fish", "knife", "fishcut", "bag" };

        // socket -> (item, step, required steps, radius, lock)
        static readonly Dictionary<string, (string item, int step, int[] req, float radius, bool lockIt)> Sockets = new()
        {
            { "pondspot",     ("net",     2, new[] { 1 }, 0.45f, false) },
            { "scalespot",    ("fish",    3, new[] { 2 }, 0.22f, false) },
            { "boardspot",    ("fish",    5, new[] { 4 }, 0.22f, true) },
            { "knifespot",    ("knife",   6, new[] { 5 }, 0.22f, true) },
            { "baginside",    ("fishcut", 7, new[] { 6 }, 0.2f,  true) },
            { "customerspot", ("bag",     8, new[] { 7 }, 0.3f,  true) },
        };

        static readonly string[] NoColliderWords = { "spot", "inside", "text", "water", "open", "closed" };

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        static readonly Color Blue = new Color(0.2f, 0.45f, 0.85f);
        static readonly Color Green = new Color(0.3f, 0.8f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Ramen VR/Setup Selected Fish Shop")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null) { EditorUtility.DisplayDialog("Fish shop", "Select the imported fish shop model first.", "OK"); return; }
            Debug.Log(Run(root));
        }

        public static string Run(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            var missing = new List<string>();
            var dyn = new HashSet<Transform>();

            // 1. grabbable items
            foreach (var n in GrabItems)
            {
                var t = Find(n);
                if (t == null) { missing.Add(n); continue; }
                dyn.Add(t);
                var b = S.Bounds(t);
                var box = S.GetOrAdd<BoxCollider>(t.gameObject);
                var ls = t.lossyScale;
                box.size = Vector3.Max(new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z)), Vector3.one * 0.03f);
                box.center = t.InverseTransformPoint(b.center);
                // the bounds were measured in world space: undo the item's own rotation for the box size
                box.size = AbsLocal(t, b.size);
                var rb = S.GetOrAdd<Rigidbody>(t.gameObject);
                rb.mass = 0.3f; rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var grab = S.GetOrAdd<XRGrabInteractable>(t.gameObject);
                grab.colliders.Clear(); grab.colliders.Add(box);
                grab.useDynamicAttach = false;
                if (n == "bag") grab.trackRotation = false;   // the bag stays upright
                var at = t.Find("GrabAttach");
                if (!at) { at = new GameObject("GrabAttach").transform; at.SetParent(t, false); }
                at.position = new Vector3(b.center.x, b.min.y, b.center.z); at.rotation = t.rotation;
                grab.attachTransform = at;
                S.GetOrAdd<SnapAssist>(t.gameObject);
            }

            S.PlaceInside(Find("baginside"), Find("bag"), 0.3f);

            // 2. sockets
            foreach (var kv in Sockets)
            {
                var t = Find(kv.Key);
                if (t == null) { missing.Add(kv.Key); continue; }
                var sc = S.GetOrAdd<SphereCollider>(t.gameObject);
                sc.isTrigger = true; sc.radius = kv.Value.radius / Mathf.Max(0.0001f, t.lossyScale.x);
                var s = S.GetOrAdd<RamenSocket>(t.gameObject);
                s.acceptedName = kv.Value.item; s.stepIndex = kv.Value.step; s.requiredSteps = kv.Value.req;
                s.hoverSocketSnapping = true; s.socketSnappingRadius = kv.Value.radius;
                s.lockOnPlace = kv.Value.lockIt;
            }
            var fish = Find("fish"); var fishcut = Find("fishcut"); var coins = Find("coins");
            var pondspot = Find("pondspot")?.GetComponent<RamenSocket>();
            if (pondspot) { pondspot.revealItem = fish; pondspot.revealAt = Find("frame"); }
            var knifespot = Find("knifespot")?.GetComponent<RamenSocket>();
            if (knifespot) { knifespot.hideOnPlace = new[] { fish ? fish.gameObject : null }; knifespot.showOnPlace = new[] { fishcut ? fishcut.gameObject : null }; }
            var custspot = Find("customerspot")?.GetComponent<RamenSocket>();
            var bagin = Find("baginside")?.GetComponent<RamenSocket>();
            if (bagin) bagin.hideOnPlace = new[] { fishcut ? fishcut.gameObject : null };   // the fish goes inside the bag

            // 3. static colliders for everything else
            int statics = 0;
            var btnNames = new[] { "signbutton", "weighbutton" };
            foreach (var bn in btnNames) { var bt = Find(bn); if (bt) dyn.Add(bt); }
            var sign = Find("opensign"); if (sign) dyn.Add(sign);
            var cbox = Find("cashbox"); if (cbox) dyn.Add(cbox);   // its lid opens, so it can't be static
            if (cbox) foreach (var mf in cbox.GetComponentsInChildren<MeshFilter>()) S.GetOrAdd<BoxCollider>(mf.gameObject);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (S.IsUnder(mf.transform, dyn)) continue;
                if (NoColliderWords.Any(w => mf.name.ToLower().Contains(w))) continue;
                if (mf.GetComponent<Collider>()) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                mf.gameObject.isStatic = true;
                statics++;
            }

            // 4. floor teleport + ground outside
            var floor = Find("floor");
            if (floor) S.GetOrAdd<TeleportationArea>(floor.gameObject);
            if (Find("Ground") == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Ground";
                g.transform.SetParent(root.transform, false);
                var cc = S.Center(root.transform);
                g.transform.position = new Vector3(cc.x, root.transform.position.y - 0.001f, cc.z);
                g.transform.localScale = new Vector3(4, 1, 4);
                g.GetComponent<Renderer>().sharedMaterial = S.MakeMat("Ground_Grey", new Color(0.35f, 0.36f, 0.35f), false);
                g.AddComponent<TeleportationArea>();
            }

            // 5. see-through water + lamp lights
            var water = Find("water");
            if (water) water.GetComponent<Renderer>().sharedMaterial = S.MakeMat("FishWater", new Color(0.15f, 0.45f, 0.8f, 0.55f), true);
            foreach (var t in all.Where(t => t && t.name.StartsWith("lamp") && (t.parent == null || !t.parent.name.StartsWith("lamp"))))
            {
                if (t.Find("Pendant_PointLight")) continue;
                var lgo = new GameObject("Pendant_PointLight");
                lgo.transform.SetParent(t, false);
                var b = S.Bounds(t);
                lgo.transform.position = new Vector3(b.center.x, b.min.y - 0.15f, b.center.z);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 4.5f; l.intensity = 2f; l.color = new Color(1f, 0.88f, 0.7f);
            }

            // 6. game manager + UI + buttons
            var gm = root.GetComponentInChildren<RamenGameManager>(true);
            if (gm == null)
            {
                var gmGo = new GameObject("Fish_GameManager");
                gmGo.transform.SetParent(root.transform, false);
                gm = gmGo.AddComponent<RamenGameManager>();
            }
            gm.shopRoot = root.transform;
            gm.stepLabels = Steps;
            var order = S.GetOrAdd<FishOrder>(gm.gameObject);

            Vector3 shopC = floor ? S.Center(floor) : S.Center(root.transform);
            var stepT = Find("step");
            Vector3 outDir = stepT ? S.Center(stepT) - shopC : Vector3.back; outDir.y = 0; outDir.Normalize();

            var cashbox = Find("cashbox");
            if (coins && cashbox) { var cb = S.Bounds(cashbox); coins.position = new Vector3(cb.center.x, cb.min.y + 0.015f, cb.center.z); }   // coins wait inside the cash box
            var screen = BuildUI(root.transform, gm, Find, shopC, outDir, missing, order, out var payPanel, out var payBtn);
            if (custspot) custspot.showOnPlace = new[] { payPanel };

            MakeButton(Find("signbutton"), 0, null, "", dyn, missing, b =>
            {
                b.rotateTarget = sign; b.rotateAxis = Vector3.up; b.rotateAngle = 180f; b.showOrderPanel = true;
            });
            MakeButton(Find("weighbutton"), 4, new[] { 3 }, "Put the fish on the scale first!", dyn, missing, b =>
            {
                b.showObjects = screen ? new[] { screen } : new GameObject[0];
                b.duration = 0.4f;
                UnityEventTools.AddPersistentListener(b.onPressed, order.ShowNumbers);
            });
            if (payBtn)
            {
                S.GetOrAdd<XRSimpleInteractable>(payBtn.gameObject);
                var pb = S.GetOrAdd<StepButton>(payBtn.gameObject);
                pb.stepIndex = 9; pb.requiredSteps = new[] { 8 }; pb.duration = 0.6f;
                var lid = cashbox ? cashbox.Find("lid") : null;
                if (lid)
                {
                    // hinge on the lid's back edge (the side away from the customer), so it flips open
                    var lb = S.Bounds(lid);
                    Vector3 toCust = (stepT ? S.Center(stepT) : shopC + outDir) - lb.center; toCust.y = 0; toCust.Normalize();
                    toCust = Mathf.Abs(toCust.x) > Mathf.Abs(toCust.z) ? new Vector3(Mathf.Sign(toCust.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toCust.z));   // straight edge of the box
                    Vector3 backEdge = lb.center - toCust * Vector3.Scale(lb.extents, new Vector3(Mathf.Abs(toCust.x), 0, Mathf.Abs(toCust.z))).magnitude;
                    backEdge.y = lb.max.y;
                    var hinge = new GameObject("lidhinge").transform;
                    hinge.SetParent(cashbox, false); hinge.position = backEdge; hinge.rotation = Quaternion.identity;
                    lid.SetParent(hinge, true);
                    Vector3 axis = Vector3.Cross(Vector3.up, toCust).normalized;
                    // pick the direction that lifts the front of the lid up
                    Vector3 front = lb.center + toCust * 0.1f - backEdge;
                    float lidDir = (Quaternion.AngleAxis(100f, axis) * front).y > front.y ? 1f : -1f;
                    pb.rotateTarget = hinge; pb.rotateAxis = axis; pb.rotateAngle = 100f * lidDir;
                }
                pb.showObjects = coins ? new[] { coins.gameObject } : new GameObject[0];
                UnityEventTools.AddPersistentListener(pb.onPressed, order.ShowNumbers);
                gm.pickTargets[9] = payBtn;
                gm.placeTargets[9] = null;
            }
            S.EnsureEventSystem();

            // hidden until their step
            if (fish) fish.gameObject.SetActive(false);
            if (fishcut) fishcut.gameObject.SetActive(false);
            if (coins) coins.gameObject.SetActive(false);

            // 7. player starts outside, facing the shop (only when this is the only shop in the scene)
            if (Object.FindObjectsByType<RamenGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)
            {
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Vector3 spawn = (stepT ? S.Center(stepT) : shopC) + outDir * 2.6f; spawn.y = root.transform.position.y;
                if (origin) { origin.transform.position = spawn; origin.transform.rotation = Quaternion.LookRotation(-outDir); }
            }
            var xr = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xr) S.LongRays(xr.gameObject, 25f);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            string msg = $"Fish shop setup done. Static colliders: {statics}.";
            if (missing.Count > 0) msg += " NOT FOUND: " + string.Join(", ", missing);
            return msg;
        }

        static Vector3 AbsLocal(Transform t, Vector3 worldSize)
        {
            // size of a world AABB expressed along the item's own axes (good enough for boxy items)
            var ls = t.lossyScale;
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
            box.size = Vector3.Max(AbsLocal(t, b.size) * 1.6f, Vector3.one * 0.06f);
            S.GetOrAdd<XRSimpleInteractable>(t.gameObject);
            var sb = S.GetOrAdd<StepButton>(t.gameObject);
            sb.stepIndex = step; sb.requiredSteps = req; sb.notReadyHint = hint;
            extra?.Invoke(sb);
        }

        static void QtyButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction act)
        {
            var b = S.MakePanel(parent, name, new Color(0.25f, 0.5f, 0.9f), pos, new Vector2(100, 90));
            S.MakeText(b, "Label", label, 56, Color.white, Vector2.zero, new Vector2(100, 90), true);
            var ub = b.gameObject.AddComponent<Button>(); ub.targetGraphic = b.GetComponent<Image>();
            var bc = b.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(100, 90, 40);
            b.gameObject.AddComponent<XRSimpleInteractable>();
            var ca = b.gameObject.AddComponent<ClickAction>();
            UnityEventTools.AddPersistentListener(ca.onPress, act);
        }

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 f = to - from; f.y = 0;
            return Quaternion.LookRotation(f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward);
        }

        /// builds the checklist board, finish screen, HUD, order panel, welcome sign, scale screen and arrows.
        /// returns the scale screen (hidden until WEIGH is pressed)
        static GameObject BuildUI(Transform root, RamenGameManager gm, System.Func<string, Transform> F, Vector3 shopC, Vector3 outDir, List<string> missing,
                                  FishOrder fo, out GameObject payPanel, out Transform payBtn)
        {
            payPanel = null; payBtn = null;
            var old = root.Find("Fish_UI");
            if (old) Object.DestroyImmediate(old.gameObject);
            var uiRoot = new GameObject("Fish_UI");
            uiRoot.transform.SetParent(root, false);

            // ---- checklist board on the back wall, above the pond side ----
            var back = F("wall");
            var pond = F("pond");
            Vector3 wallC = back ? S.Center(back) : shopC - outDir * 3f;
            Vector3 inward = shopC - wallC; inward.y = 0; inward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, inward);
            Vector3 boardPos = wallC + inward * 0.12f;
            if (pond) boardPos += along * Vector3.Dot(S.Center(pond) - wallC, along);
            boardPos.y = root.position.y + 1.75f;
            Quaternion rot = Quaternion.LookRotation(-inward);

            var steps = gm.Steps; int n = steps.Length;
            var board = S.MakeCanvas("Checklist_Board", uiRoot.transform, boardPos, rot, new Vector2(760, 900), 0.0009f);
            S.MakePanel(board.transform, "BG", Bg, Vector2.zero, new Vector2(760, 900));
            S.MakePanel(board.transform, "TopBar", Blue, new Vector2(0, 420), new Vector2(760, 60));
            S.MakeText(board.transform, "Title", "KAI PLA NA JA", 40, Color.white, new Vector2(0, 420), new Vector2(740, 60), true);
            S.MakeText(board.transform, "Sub", "Sell a fresh fish to the customer!", 26, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 360), new Vector2(740, 40));
            gm.rowBg = new Image[n]; gm.rowDot = new Image[n]; gm.rowNum = new Text[n]; gm.rowLabel = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float y = 290 - i * 62;
                var row = S.MakePanel(board.transform, "Row" + (i + 1), new Color(1, 1, 1, 0.03f), new Vector2(0, y), new Vector2(700, 54));
                gm.rowBg[i] = row.GetComponent<Image>();
                var dot = S.MakePanel(row, "Dot", new Color(0.35f, 0.35f, 0.38f), new Vector2(-315, 0), new Vector2(46, 46));
                gm.rowDot[i] = dot.GetComponent<Image>();
                gm.rowNum[i] = S.MakeText(dot, "Num", (i + 1).ToString(), 24, Color.white, Vector2.zero, new Vector2(46, 46), true);
                var lab = S.MakeText(row, "Label", steps[i], 30, Color.white, new Vector2(40, 0), new Vector2(600, 54));
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
            S.MakePanel(done, "Glow", new Color(0.2f, 0.45f, 0.85f, 0.1f), new Vector2(0, 140), new Vector2(760, 360));
            S.MakePanel(done, "Banner", Blue, new Vector2(0, 385), new Vector2(760, 130));
            S.MakeText(done, "Title", "SOLD!", 72, Color.white, new Vector2(0, 385), new Vector2(740, 120), true);
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 90 : 64;
                var d = S.MakePanel(done, "Star" + i, Gold, new Vector2((i - 1) * 130, i == 1 ? 225 : 205), new Vector2(s, s));
                d.localRotation = Quaternion.Euler(0, 0, 45);
                var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.5f, 0.3f, 0f); o.effectDistance = new Vector2(4, -4);
            }
            S.MakeText(done, "Thanks", "Khob khun na ja!", 80, Gold, new Vector2(0, 90), new Vector2(740, 110), true).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            S.MakeText(done, "Sub", "The customer took home a fresh fish!", 28, new Color(0.85f, 0.85f, 0.9f), new Vector2(0, 15), new Vector2(700, 50));
            S.MakePanel(done, "Divider", new Color(1, 1, 1, 0.12f), new Vector2(0, -30), new Vector2(620, 3));
            var statsBg = S.MakePanel(done, "StatsBG", new Color(1, 1, 1, 0.06f), new Vector2(0, -95), new Vector2(620, 80));
            gm.doneStatsText = S.MakeText(statsBg, "Stats", "10 / 10 steps", 34, Color.white, Vector2.zero, new Vector2(600, 80), true);
            S.MakeText(done, "Visit", "Thank you for visiting\nKAI PLA NA JA", 32, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, -195), new Vector2(700, 90));
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
            S.MakePanel(hud.transform, "Accent", Blue, new Vector2(-305, 0), new Vector2(10, 70));
            gm.hudText = S.MakeText(hud.transform, "Text", "", 32, Color.white, new Vector2(5, 0), new Vector2(590, 70));
            hud.gameObject.AddComponent<LazyHud>();

            // ---- order panel above the customer counter, facing the entrance ----
            var front = F("counter.001");
            var stepT = F("step");
            Vector3 orderPos = front ? S.Center(front) + Vector3.up * 0.95f : shopC + Vector3.up * 1.5f;
            Vector3 viewer = stepT ? S.Center(stepT) : shopC + outDir * 3f;
            var order = S.MakeCanvas("Order_Panel", uiRoot.transform, orderPos, Facing(viewer, orderPos), new Vector2(520, 400), 0.0013f);
            S.MakePanel(order.transform, "BG", Bg, Vector2.zero, new Vector2(520, 400));
            S.MakePanel(order.transform, "TopBar", Blue, new Vector2(0, 175), new Vector2(520, 50));
            S.MakeText(order.transform, "Head", "HOW MANY FISH?", 32, Color.white, new Vector2(0, 175), new Vector2(500, 50), true);
            fo.orderText = S.MakeText(order.transform, "Count", "Fish:  1", 34, Color.white, new Vector2(0, 75), new Vector2(300, 110), true);
            QtyButton(order.transform, "Less_Button", "-", new Vector2(-190, 75), fo.Less);
            QtyButton(order.transform, "More_Button", "+", new Vector2(190, 75), fo.More);
            var btn = S.MakePanel(order.transform, "Start_Button", Green, new Vector2(0, -110), new Vector2(380, 100));
            S.MakeText(btn, "Label", "TAKE ORDER", 42, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var button = btn.gameObject.AddComponent<Button>(); button.targetGraphic = btn.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, gm.StartOrder);
            var bc = btn.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(380, 100, 40);
            btn.gameObject.AddComponent<XRSimpleInteractable>();
            btn.gameObject.AddComponent<StartOrderButton>();
            gm.orderPanel = order.gameObject;

            // ---- welcome sign under the green button (outside) ----
            var sbtn = F("signbutton");
            if (sbtn)
            {
                Vector3 p = S.Center(sbtn);
                var sign = S.MakeCanvas("Welcome_Sign", uiRoot.transform, new Vector3(p.x, p.y - 0.3f, p.z) + outDir * 0.06f, Quaternion.LookRotation(-outDir), new Vector2(440, 120), 0.0016f);
                S.MakePanel(sign.transform, "BG", Bg, Vector2.zero, new Vector2(440, 120));
                S.MakeText(sign.transform, "Text", "WELCOME!\nPress the green button", 34, Color.white, Vector2.zero, new Vector2(420, 110));
            }

            // ---- price on the scale screen (shown after WEIGH) ----
            GameObject screenGo = null;
            var scr = F("scalescreen");
            if (scr)
            {
                Vector3 c = S.Center(scr);
                Vector3 toRoom = shopC - c; toRoom.y = 0; toRoom.Normalize();
                // face straight out of the screen, a tilted canvas sinks into it on one side
                toRoom = Mathf.Abs(toRoom.x) > Mathf.Abs(toRoom.z) ? new Vector3(Mathf.Sign(toRoom.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toRoom.z));
                var sc = S.MakeCanvas("Scale_Price", uiRoot.transform, c + toRoom * 0.012f, Quaternion.LookRotation(-toRoom), new Vector2(300, 110), 0.0009f);
                var st = S.MakeText(sc.transform, "Text", "1 fish  2.0 kg\n240 B", 30, new Color(0.4f, 1f, 0.5f), Vector2.zero, new Vector2(300, 110), true);
                st.horizontalOverflow = HorizontalWrapMode.Overflow; st.resizeTextForBestFit = true; st.resizeTextMinSize = 14; st.resizeTextMaxSize = 30;
                fo.scaleText = st;
                screenGo = sc.gameObject;
                screenGo.SetActive(false);
            }
            else missing.Add("scalescreen");

            // ---- pay panel by the cash box (shown when the customer has the bag) ----
            var cash = F("cashbox");
            Vector3 payPos = cash ? S.Center(cash) + Vector3.up * 0.5f : orderPos;
            var pay = S.MakeCanvas("Pay_Panel", uiRoot.transform, payPos, Facing(viewer, payPos), new Vector2(480, 300), 0.0011f);
            S.MakePanel(pay.transform, "BG", Bg, Vector2.zero, new Vector2(480, 300));
            S.MakePanel(pay.transform, "TopBar", Gold, new Vector2(0, 125), new Vector2(480, 50));
            S.MakeText(pay.transform, "Head", "BILL", 32, new Color(0.1f, 0.1f, 0.1f), new Vector2(0, 125), new Vector2(460, 50), true);
            fo.payText = S.MakeText(pay.transform, "Total", "1 fish  2.0 kg\nTOTAL 240 B", 32, Color.white, new Vector2(0, 35), new Vector2(460, 110), true);
            var payB = S.MakePanel(pay.transform, "Pay_Button", Green, new Vector2(0, -85), new Vector2(340, 90));
            S.MakeText(payB, "Label", "GET PAID", 40, Color.white, Vector2.zero, new Vector2(340, 90), true);
            payB.gameObject.AddComponent<Button>().targetGraphic = payB.GetComponent<Image>();
            var pbc = payB.gameObject.AddComponent<BoxCollider>(); pbc.size = new Vector3(340, 90, 40);
            payPanel = pay.gameObject; payBtn = payB;
            payPanel.SetActive(false);

            // ---- guide arrows ----
            gm.pickArrow = S.MakeArrow(uiRoot.transform, "Arrow_Pick", "THIS", new Color(1f, 0.42f, 0.25f));
            gm.placeArrow = S.MakeArrow(uiRoot.transform, "Arrow_Place", "PUT HERE", Green);
            string[] pick = { "signbutton", null, "net", "fish", "weighbutton", "fish", "knife", "fishcut", "bag", "coins" };
            string[] place = { null, null, "water", "scale", null, "board", "board", "bag", "customerspot", "cashbox" };
            gm.pickTargets = pick.Select(x => x == null ? null : F(x)).ToArray();
            gm.placeTargets = place.Select(x => x == null ? null : F(x)).ToArray();
            foreach (var img in uiRoot.GetComponentsInChildren<Image>(true)) img.raycastTarget = img.GetComponent<Button>() != null;
            return screenGo;
        }
    }
}
