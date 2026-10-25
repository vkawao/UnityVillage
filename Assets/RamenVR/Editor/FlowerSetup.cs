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
    /// Menu: Ramen VR > Setup Selected Flower Shop  (house3_flower model "DOK MAI SUAY")
    public static class FlowerSetup
    {
        public static readonly string[] Steps =
        {
            "Press the doorbell to open the door",
            "Press START on the order board",
            "Pull the handle to open the flower fridge",
            "Put the vase on the table",
            "Put a red flower in the vase",
            "Put a yellow flower in the vase",
            "Put the vase on the window shelf",
            "Put the pot plant on the wall shelf",
            "Press the light switch",
            "Hang the OPEN sign on the hook by the door"
        };

        static readonly string[] GrabItems = { "jaekan", "dokdaeng", "dokluang", "krathang", "opensign" };
        static readonly string[] NoColliderWords = { "spot", "text", "petal", "leaf", "stem", "middle", "glow" };

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        static readonly Color Pink = new Color(0.9f, 0.45f, 0.6f);
        static readonly Color Green = new Color(0.3f, 0.8f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Ramen VR/Setup Selected Flower Shop")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null) { EditorUtility.DisplayDialog("Flower shop", "Select the imported flower shop model first.", "OK"); return; }
            Debug.Log(Run(root));
        }

        public static string Run(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // safe to run again: remove the spots this script made last time
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "redspot" || t.name == "yellowspot").ToArray())
                Object.DestroyImmediate(t.gameObject);
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t && t.name == n);
            var missing = new List<string>();
            var dyn = new HashSet<Transform>();

            var floor = Find("floor");
            Vector3 shopC = floor ? S.Center(floor) : S.Center(root.transform);
            var step = Find("step");
            Vector3 doorC = step ? S.Center(step) : shopC + Vector3.forward * 2.6f;
            doorC.y = root.transform.position.y;
            Vector3 outDir = doorC - shopC; outDir.y = 0;
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
                rb.isKinematic = true;   // waits where it is until grabbed
                var grab = S.GetOrAdd<XRGrabInteractable>(t.gameObject);
                grab.colliders.Clear(); grab.colliders.Add(box);
                grab.useDynamicAttach = false;
                grab.trackRotation = false;   // stays upright while carried
                var at = t.Find("GrabAttach");
                if (!at) { at = new GameObject("GrabAttach").transform; at.SetParent(t, false); }
                at.position = new Vector3(b.center.x, b.min.y, b.center.z); at.rotation = t.rotation;
                grab.attachTransform = at;
                S.GetOrAdd<SnapAssist>(t.gameObject);
                S.GetOrAdd<DropOnRelease>(t.gameObject);
            }

            // two spots inside the vase (they ride along with it)
            var vase = Find("jaekan");
            Transform redSpot = null, yellowSpot = null;
            if (vase)
            {
                var vb = S.Bounds(vase);
                redSpot = new GameObject("redspot").transform; redSpot.SetParent(vase, false);
                yellowSpot = new GameObject("yellowspot").transform; yellowSpot.SetParent(vase, false);
                S.PlaceInside(redSpot, vase, 0.7f, -rightDir * vb.extents.x * 0.35f);
                S.PlaceInside(yellowSpot, vase, 0.7f, rightDir * vb.extents.x * 0.35f);
            }

            // 2. sockets: (spot, item, step, required, radius, lock)
            var sockets = new (string spot, string item, int step, int[] req, float radius, bool lockIt)[]
            {
                ("vasespot",   "jaekan",   3, new[] { 1 },    0.3f,  false),
                ("redspot",    "dokdaeng", 4, new[] { 2, 3 }, 0.2f,  true),
                ("yellowspot", "dokluang", 5, new[] { 2, 3 }, 0.2f,  true),
                ("showspot",   "jaekan",   6, new[] { 4, 5 }, 0.35f, true),
                ("shelfspot",  "krathang", 7, new[] { 1 },    0.3f,  true),
                ("hookspot",   "opensign", 9, new[] { 8 },    0.3f,  true),
            };
            foreach (var d in sockets)
            {
                var t = d.spot == "redspot" ? redSpot : d.spot == "yellowspot" ? yellowSpot : Find(d.spot);
                if (t == null) { missing.Add(d.spot); continue; }
                var sc = S.GetOrAdd<SphereCollider>(t.gameObject);
                sc.isTrigger = true; sc.radius = d.radius / Mathf.Max(0.0001f, t.lossyScale.x);
                var s = S.GetOrAdd<RamenSocket>(t.gameObject);
                s.acceptedName = d.item; s.stepIndex = d.step; s.requiredSteps = d.req;
                s.hoverSocketSnapping = true; s.socketSnappingRadius = d.radius;
                s.lockOnPlace = d.lockIt;
            }

            // 3. moving things: front door, fridge door, lamp, open sign
            HingeDoor MakeDoor(string n, bool swingIn)
            {
                var d = Find(n);
                if (!d) { missing.Add(n); return null; }
                dyn.Add(d);
                var h = S.GetOrAdd<HingeDoor>(d.gameObject);
                Vector3 arm = S.Center(d) - d.position; arm.y = 0;
                Vector3 swung = Quaternion.AngleAxis(95f, Vector3.up) * arm;
                Vector3 want = swingIn ? -outDir : outDir;
                h.openAngle = Vector3.Dot(swung - arm, want) > 0 ? 95f : -95f;
                h.duration = 1.2f;
                var rb = S.GetOrAdd<Rigidbody>(d.gameObject); rb.isKinematic = true;
                foreach (var mf in d.GetComponentsInChildren<MeshFilter>()) S.GetOrAdd<BoxCollider>(mf.gameObject);
                return h;
            }
            var frontDoor = MakeDoor("door", true);
            var fridgeDoor = MakeDoor("fridgedoor", false);   // opens towards the shop

            var lamp = Find("lamp");
            var glow = Find("glow");
            if (lamp) dyn.Add(lamp); else missing.Add("lamp");
            if (glow) glow.gameObject.SetActive(false);   // light is off until the switch is pressed

            var bell = Find("bellbutton");
            var handle = Find("fridgehandle");
            var lightBtn = Find("lightbutton");
            
            // 4. static colliders for everything else
            int statics = 0;
            foreach (var t in new[] { bell, lightBtn }) if (t) dyn.Add(t);
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
            foreach (var n in new[] { "floor", "grass", "path", "step" }) { var t = Find(n); if (t) S.GetOrAdd<TeleportationArea>(t.gameObject); }
            if (Find("Ground") == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Ground";
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(shopC.x, root.transform.position.y - 0.03f, shopC.z);
                g.transform.localScale = new Vector3(4, 1, 4);
                g.GetComponent<Renderer>().sharedMaterial = S.MakeMat("Ground_Grey", new Color(0.35f, 0.36f, 0.35f), false);
                g.AddComponent<TeleportationArea>();
            }

            // 6. game manager + UI + buttons
            var gm = root.GetComponentInChildren<RamenGameManager>(true);
            if (gm == null)
            {
                var gmGo = new GameObject("Flower_GameManager");
                gmGo.transform.SetParent(root.transform, false);
                gm = gmGo.AddComponent<RamenGameManager>();
            }
            gm.shopRoot = root.transform;
            gm.stepLabels = Steps;
            gm.showDistance = 4f;

            BuildUI(root.transform, gm, Find, shopC, doorC, outDir);

            MakeButton(bell, 0, null, "", dyn, missing, b =>
            {
                if (frontDoor) b.openDoors = new[] { frontDoor };
                b.showOrderPanel = true; b.duration = 0.4f;
            });
            MakeButton(handle, 2, new[] { 1 }, "Press START first!", dyn, missing, b =>
            {
                if (fridgeDoor) b.openDoors = new[] { fridgeDoor };
                b.duration = 0.4f;
            });
            MakeButton(lightBtn, 8, new[] { 6, 7 }, "Put the vase and the pot plant on the shelves first!", dyn, missing, b =>
            {
                if (lamp)
                {
                    var shade = Find("lampshade");
                    Vector3 armDir = (shade ? S.Center(shade) : lamp.position + Vector3.up) - lamp.position;
                    Vector3 to = outDir; // swing the lamp out over the door
                    Vector3 axis = Vector3.Cross(armDir, to);
                    b.rotateTarget = lamp;
                    b.rotateAxis = axis.sqrMagnitude > 1e-6f ? axis.normalized : rightDir;
                    b.rotateAngle = Vector3.Angle(armDir, to);
                }
                if (glow) b.showObjects = new[] { glow.gameObject };
                b.duration = 0.8f;
            });
            S.EnsureEventSystem();

            // 7. player starts outside, facing the door
            if (Object.FindObjectsByType<RamenGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)
            {
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Vector3 spawn = doorC + outDir * 2.6f;
                if (origin) { origin.transform.position = spawn; origin.transform.rotation = Quaternion.LookRotation(-outDir); }
            }
            var xr = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xr) S.LongRays(xr.gameObject, 25f);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            string msg = $"Flower shop setup done. Static colliders: {statics}.";
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
            sb.onPressed = new UnityEngine.Events.UnityEvent();
            extra?.Invoke(sb);
        }

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 f = to - from; f.y = 0;
            return Quaternion.LookRotation(f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward);
        }

        static void BuildUI(Transform root, RamenGameManager gm, System.Func<string, Transform> F, Vector3 shopC, Vector3 doorC, Vector3 outDir)
        {
            var old = root.Find("Flower_UI");
            if (old) Object.DestroyImmediate(old.gameObject);
            var uiRoot = new GameObject("Flower_UI");
            uiRoot.transform.SetParent(root, false);
            Vector3 viewer = doorC - outDir * 1.0f + Vector3.up * 1.6f;   // just inside the door

            // ---- checklist board on the back wall, above the flower box ----
            var planter = F("planter");
            Vector3 boardPos = planter ? S.Center(planter) - outDir * 0.36f : shopC - outDir * 2f;
            boardPos.y = root.position.y + 1.85f;
            Quaternion rot = Quaternion.LookRotation(-outDir);

            var steps = gm.Steps; int n = steps.Length;
            var board = S.MakeCanvas("Checklist_Board", uiRoot.transform, boardPos, rot, new Vector2(760, 900), 0.0009f);
            S.MakePanel(board.transform, "BG", Bg, Vector2.zero, new Vector2(760, 900));
            S.MakePanel(board.transform, "TopBar", Pink, new Vector2(0, 420), new Vector2(760, 60));
            S.MakeText(board.transform, "Title", "DOK TONG", 40, Color.white, new Vector2(0, 420), new Vector2(740, 60), true);
            S.MakeText(board.transform, "Sub", "Get the flower shop ready to open!", 24, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 360), new Vector2(740, 40));
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
            S.MakePanel(done, "Glow", new Color(0.9f, 0.45f, 0.6f, 0.1f), new Vector2(0, 140), new Vector2(760, 360));
            S.MakePanel(done, "Banner", Pink, new Vector2(0, 385), new Vector2(760, 130));
            S.MakeText(done, "Title", "SHOP OPEN!", 68, Color.white, new Vector2(0, 385), new Vector2(740, 120), true);
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 90 : 64;
                var d = S.MakePanel(done, "Star" + i, Gold, new Vector2((i - 1) * 130, i == 1 ? 225 : 205), new Vector2(s, s));
                d.localRotation = Quaternion.Euler(0, 0, 45);
                var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.5f, 0.3f, 0f); o.effectDistance = new Vector2(4, -4);
            }
            S.MakeText(done, "Thanks", "Khob khun ka!", 80, Gold, new Vector2(0, 90), new Vector2(740, 110), true).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            S.MakeText(done, "Sub", "The shop window looks beautiful!", 26, new Color(0.85f, 0.85f, 0.9f), new Vector2(0, 15), new Vector2(720, 50));
            S.MakePanel(done, "Divider", new Color(1, 1, 1, 0.12f), new Vector2(0, -30), new Vector2(620, 3));
            var statsBg = S.MakePanel(done, "StatsBG", new Color(1, 1, 1, 0.06f), new Vector2(0, -95), new Vector2(620, 80));
            gm.doneStatsText = S.MakeText(statsBg, "Stats", "10 / 10 steps", 34, Color.white, Vector2.zero, new Vector2(600, 80), true);
            S.MakeText(done, "Visit", "Thank you for visiting\nDOK TONG", 32, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, -195), new Vector2(700, 90));
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
            S.MakePanel(hud.transform, "Accent", Pink, new Vector2(-305, 0), new Vector2(10, 70));
            gm.hudText = S.MakeText(hud.transform, "Text", "", 32, Color.white, new Vector2(5, 0), new Vector2(590, 70));
            hud.gameObject.AddComponent<LazyHud>();

            // ---- order board above the work table ----
            var table = F("table");
            Vector3 orderPos = table ? S.Center(table) + Vector3.up * 0.95f : shopC + Vector3.up * 1.6f;
            var order = S.MakeCanvas("Order_Panel", uiRoot.transform, orderPos, Facing(viewer, orderPos), new Vector2(520, 330), 0.0013f);
            S.MakePanel(order.transform, "BG", Bg, Vector2.zero, new Vector2(520, 330));
            S.MakePanel(order.transform, "TopBar", Pink, new Vector2(0, 140), new Vector2(520, 50));
            S.MakeText(order.transform, "Head", "TODAY'S JOB", 32, Color.white, new Vector2(0, 140), new Vector2(500, 50), true);
            S.MakeText(order.transform, "Text", "Fill the shop window:\nflower vase + pot plant", 30, new Color(0.9f, 0.9f, 0.92f), new Vector2(0, 50), new Vector2(500, 110));
            var btn = S.MakePanel(order.transform, "Start_Button", Green, new Vector2(0, -90), new Vector2(380, 100));
            S.MakeText(btn, "Label", "START", 42, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var button = btn.gameObject.AddComponent<Button>(); button.targetGraphic = btn.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, gm.StartOrder);
            var bc = btn.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(380, 100, 40);
            btn.gameObject.AddComponent<XRSimpleInteractable>();
            btn.gameObject.AddComponent<StartOrderButton>();
            gm.orderPanel = order.gameObject;

            // ---- welcome sign above the door (outside) ----
            {
                Vector3 p = doorC + Vector3.up * 2.45f + outDir * 0.3f;
                var sign = S.MakeCanvas("Welcome_Sign", uiRoot.transform, p, Quaternion.LookRotation(-outDir), new Vector2(440, 120), 0.0016f);
                S.MakePanel(sign.transform, "BG", Bg, Vector2.zero, new Vector2(440, 120));
                S.MakeText(sign.transform, "Text", "WELCOME!\nPress the bell to open", 34, Color.white, Vector2.zero, new Vector2(420, 110));
            }

            // ---- guide arrows ----
            gm.pickArrow = S.MakeArrow(uiRoot.transform, "Arrow_Pick", "THIS", new Color(1f, 0.42f, 0.25f));
            gm.placeArrow = S.MakeArrow(uiRoot.transform, "Arrow_Place", "PUT HERE", Green);
            string[] pick = { "bellbutton", null, "fridgehandle", "jaekan", "dokdaeng", "dokluang", "jaekan", "krathang", "lightbutton", "opensign" };
            string[] place = { null, null, null, "table", "jaekan", "jaekan", "showshelf", "wallshelf", null, "hook" };
            gm.pickTargets = pick.Select(x => x == null ? null : F(x) ?? root.Find(x)).ToArray();
            gm.placeTargets = place.Select(x => x == null ? null : F(x) ?? root.Find(x)).ToArray();
            gm.pickTargetsAlt = new Transform[10]; gm.placeTargetsAlt = new Transform[10];
            foreach (var img in uiRoot.GetComponentsInChildren<Image>(true)) img.raycastTarget = img.GetComponent<Button>() != null;
        }
    }
}
