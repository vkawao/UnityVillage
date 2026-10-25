using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RamenVR.EditorTools
{
    /// Menu: Ramen VR > Setup Selected Ramen Shop
    /// Select the imported house1_ramen model in the Hierarchy, then run this once.
    public static class RamenShopSetup
    {
        static readonly string[] GrabItems = { "bowl", "noodles", "egg", "pork", "springonion", "chopsticks" };

        // socket -> (accepted item, step, required steps, trigger radius)
        static readonly Dictionary<string, (string item, int step, int[] req, float radius)> Sockets = new()
        {
            { "bowlspot",       ("bowl", 2, new[] { 1 },                0.25f) },
            { "noodlespot", ("noodles",    3, new[] { 2 },                0.15f) },
            { "eggspot",     ("egg",        4, new[] { 2 },                0.15f) },
            { "porkspot",  ("pork",     5, new[] { 2 },                0.15f) },
            { "springonionspot",   ("springonion", 6, new[] { 2 },                0.15f) },
            { "chopstickspot",     ("chopsticks", 8, null,                       0.2f) },
            { "servespot",          ("bowl", 9, new[] { 2, 3, 4, 5, 6, 7 }, 0.25f) },
        };

        static readonly string[] NoColliderWords = { "noren", "signtext", "spot", "pourtarget", "glass" };

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        static readonly Color Orange = new Color(1f, 0.42f, 0.25f);
        static readonly Color Green = new Color(0.3f, 0.8f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Ramen VR/Setup Selected Ramen Shop")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Ramen VR", "Select the imported ramen shop model in the Hierarchy first.", "OK");
                return;
            }
            Debug.Log(Run(root));
        }

        public static string Run(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            var missing = new List<string>();
            var dynamicRoots = new HashSet<Transform>();

            // 1. grabbable items: fixed grab point at the bottom, easy snapping
            foreach (var n in GrabItems)
            {
                var t = Find(n);
                if (t == null) { missing.Add(n); continue; }
                dynamicRoots.Add(t);
                var b = Bounds(t);
                var box = GetOrAdd<BoxCollider>(t.gameObject);
                var ls = t.lossyScale;
                Vector3 size = new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z));
                Vector3 c = t.InverseTransformPoint(b.center);
                if (n == "bowl")   // low collider so toppings can drop inside
                {
                    size.y *= 0.45f; c.y = t.InverseTransformPoint(new Vector3(0, b.min.y + b.size.y * 0.225f, 0)).y;
                }
                box.size = Vector3.Max(size, Vector3.one * 0.03f); box.center = c;

                var rb = GetOrAdd<Rigidbody>(t.gameObject);
                rb.mass = 0.3f; rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var grab = GetOrAdd<XRGrabInteractable>(t.gameObject);
                grab.colliders.Clear(); grab.colliders.Add(box);
                grab.useDynamicAttach = false;
                var at = t.Find("GrabAttach");
                if (!at) { at = new GameObject("GrabAttach").transform; at.SetParent(t, false); }
                at.position = new Vector3(b.center.x, b.min.y, b.center.z); at.rotation = t.rotation;
                grab.attachTransform = at;
                GetOrAdd<SnapAssist>(t.gameObject);
            }

            // 2. sockets
            foreach (var kv in Sockets)
            {
                var t = Find(kv.Key);
                if (t == null) { missing.Add(kv.Key); continue; }
                var sc = GetOrAdd<SphereCollider>(t.gameObject);
                sc.isTrigger = true; sc.radius = kv.Value.radius;
                var s = GetOrAdd<RamenSocket>(t.gameObject);
                s.acceptedName = kv.Value.item; s.stepIndex = kv.Value.step; s.requiredSteps = kv.Value.req;
                s.hoverSocketSnapping = true; s.socketSnappingRadius = kv.Value.radius;
                s.lockOnPlace = kv.Key != "bowlspot";
            }

            // 3. hinged door (opens into the shop)
            var door = Find("door");
            var doorBtn = Find("bell");
            var step = Find("step");
            HingeDoor hinge = null;
            if (door && doorBtn)
            {
                dynamicRoots.Add(door);
                hinge = GetOrAdd<HingeDoor>(door.gameObject);
                Vector3 outDir = step ? Center(step) - Center(door) : -door.forward; outDir.y = 0; outDir.Normalize();
                Vector3 arm = Center(door) - door.position; arm.y = 0;
                Vector3 swung = Quaternion.AngleAxis(95f, Vector3.up) * arm;
                hinge.openAngle = Vector3.Dot(swung - arm, -outDir) > 0 ? 95f : -95f;
                var rb = GetOrAdd<Rigidbody>(door.gameObject); rb.isKinematic = true;
                foreach (var mf in door.GetComponentsInChildren<MeshFilter>())
                    if (!mf.name.ToLower().Contains("glass")) GetOrAdd<BoxCollider>(mf.gameObject);
            }
            else missing.Add("Shop_Door / Door_Button");

            // 4. soup pot
            var pot = Find("pot");
            var bowl = Find("bowl");
            var prep = Find("bowlspot");
            PotTilt tilt = null;
            if (pot)
            {
                dynamicRoots.Add(pot);
                tilt = GetOrAdd<PotTilt>(pot.gameObject);
                var toBowl = (prep ? prep.position : pot.position + Vector3.right) - pot.position; toBowl.y = 0;
                var axis = Vector3.Cross(Vector3.up, toBowl.normalized);
                tilt.tiltAxis = axis.sqrMagnitude < 0.01f ? Vector3.forward : axis;
                foreach (var mf in pot.GetComponentsInChildren<MeshFilter>()) GetOrAdd<MeshCollider>(mf.gameObject);
                if (bowl) tilt.soupInBowl = MakeSoup(bowl, pot);
            }
            else missing.Add("SoupPot");

            // 5. buttons
            MakeButton(doorBtn, RamenButton.Action.OpenDoor, null, "", hinge, null, dynamicRoots, missing, "Door_Button");
            MakeButton(Find("menubutton"), RamenButton.Action.ShowMenu, new[] { 0 }, "Open the door first!", null, null, dynamicRoots, missing, "Menu_Button");
            MakeButton(Find("soupbutton"), RamenButton.Action.PourSoup, new[] { 2 }, "Put the bowl on the orange mat first!", null, tilt, dynamicRoots, missing, "Pour_Button");

            // 6. static colliders for everything else
            int statics = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (IsUnder(mf.transform, dynamicRoots)) continue;
                if (NoColliderWords.Any(w => mf.name.ToLower().Contains(w))) continue;
                if (mf.GetComponent<Collider>()) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                mf.gameObject.isStatic = true;
                statics++;
            }

            // 7. floor teleport + ground
            var floor = Find("floor");
            if (floor) GetOrAdd<TeleportationArea>(floor.gameObject);
            if (Find("Ground") == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Ground";
                g.transform.SetParent(root.transform, false);
                var cc = Center(root.transform);
                g.transform.position = new Vector3(cc.x, root.transform.position.y, cc.z);
                g.transform.localScale = new Vector3(4, 1, 4);
                g.GetComponent<Renderer>().sharedMaterial = MakeMat("Ground_Grey", new Color(0.35f, 0.36f, 0.35f), false);
                g.AddComponent<TeleportationArea>();
            }

            // 8. glass + pendant lights
            var glass = MakeMat("Glass_Transparent", new Color(0.8f, 0.9f, 0.95f, 0.1f), true);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterials.Any(m => m && m.name.ToLower().Contains("glass")))
                    r.sharedMaterials = r.sharedMaterials.Select(m => m && m.name.ToLower().Contains("glass") ? glass : m).ToArray();
            foreach (var t in all.Where(t => t && t.name.StartsWith("lamp") && (t.parent == null || !t.parent.name.StartsWith("lamp"))))
            {
                if (t.Find("Pendant_PointLight")) continue;
                var lgo = new GameObject("Pendant_PointLight");
                lgo.transform.SetParent(t, false);
                var b = Bounds(t);
                lgo.transform.position = new Vector3(b.center.x, b.min.y - 0.15f, b.center.z);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 4f; l.intensity = 2f; l.color = new Color(1f, 0.82f, 0.6f);
            }

            // 9. game manager + UI
            var gm = root.GetComponentInChildren<RamenGameManager>(true);
            if (gm == null)
            {
                var gmGo = new GameObject("Ramen_GameManager");
                gmGo.transform.SetParent(root.transform, false);
                gm = gmGo.AddComponent<RamenGameManager>();
            }
            gm.shopRoot = root.transform;
            BuildUI(root.transform, gm, Find("frontcounter"), Find("kitchencounter"), Find("menubutton"), door, doorBtn, step);
            EnsureEventSystem();

            // 10. player starts outside, facing the door
            if (door && Object.FindObjectsByType<RamenGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)   // in the village the player keeps the village start point
            {
                Vector3 outDir = step ? Center(step) - Center(door) : -door.forward; outDir.y = 0; outDir.Normalize();
                Vector3 spawn = Center(door) + outDir * 3.2f; spawn.y = 0f;
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin) { origin.transform.position = spawn; origin.transform.rotation = Quaternion.LookRotation(-outDir); }
            }
            var xr = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xr) LongRays(xr.gameObject, 25f);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            string msg = $"Ramen setup done. Static colliders: {statics}.";
            if (missing.Count > 0) msg += " NOT FOUND: " + string.Join(", ", missing);
            return msg;
        }

        // =====================================================================
        static void MakeButton(Transform t, RamenButton.Action action, int[] req, string hint, HingeDoor door, PotTilt pot,
                               HashSet<Transform> dyn, List<string> missing, string label)
        {
            if (t == null) { missing.Add(label); return; }
            dyn.Add(t);
            var b = Bounds(t);
            var box = GetOrAdd<BoxCollider>(t.gameObject);
            var ls = t.lossyScale;
            box.center = t.InverseTransformPoint(b.center);
            box.size = new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z)) * 1.6f;
            box.size = Vector3.Max(box.size, Vector3.one * 0.06f);
            GetOrAdd<XRSimpleInteractable>(t.gameObject);
            var rb = GetOrAdd<RamenButton>(t.gameObject);
            rb.action = action; rb.requiredSteps = req; rb.notReadyHint = hint; rb.door = door; rb.pot = pot;
        }

        static GameObject MakeSoup(Transform bowl, Transform pot)
        {
            var old = bowl.Find("SoupInBowl");
            if (old) return old.gameObject;
            var soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soup.name = "SoupInBowl";
            Object.DestroyImmediate(soup.GetComponent<Collider>());
            var b = Bounds(bowl);
            soup.transform.SetParent(bowl, true);
            soup.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.6f, b.center.z);
            soup.transform.rotation = Quaternion.identity;
            float d = Mathf.Min(b.size.x, b.size.z) * 0.72f;
            var ls = bowl.lossyScale;
            soup.transform.localScale = new Vector3(d / ls.x, 0.004f / ls.y, d / ls.z);
            var soupMat = pot.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name.ToLower().Contains("soup"));
            soup.GetComponent<Renderer>().sharedMaterial = soupMat ? soupMat : MakeMat("Soup_Fallback", new Color(0.55f, 0.3f, 0.1f), false);
            soup.SetActive(false);
            return soup;
        }

        static void BuildUI(Transform root, RamenGameManager gm, Transform counterMain, Transform counterKitchen,
                            Transform menuBtn, Transform door, Transform doorBtn, Transform step)
        {
            var old = root.Find("Ramen_UI");
            if (old) Object.DestroyImmediate(old.gameObject);
            var uiRoot = new GameObject("Ramen_UI");
            uiRoot.transform.SetParent(root, false);
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform F(string n) => all.FirstOrDefault(t => t.name == n);

            // ---- checklist board above the front counter, between two lamps, facing the cook ----
            Vector3 boardPos = counterMain ? Bounds(counterMain).center + Vector3.left * 0.7f : root.position + Vector3.up * 1.6f;
            if (counterMain) boardPos.y = Bounds(counterMain).max.y + 0.65f;
            Vector3 viewer = counterKitchen && counterMain ? (Bounds(counterMain).center + Bounds(counterKitchen).center) * 0.5f : boardPos - Vector3.forward;
            Vector3 face = boardPos - viewer; face.y = 0; face.x = 0;   // face straight across the counter, no tilt
            Quaternion rot = Quaternion.LookRotation(face.sqrMagnitude > 0.001f ? face.normalized : Vector3.forward);

            int n = RamenGameManager.Labels.Length;
            var board = MakeCanvas("Checklist_Board", uiRoot.transform, boardPos, rot, new Vector2(760, 900), 0.0009f);
            MakePanel(board.transform, "BG", Bg, Vector2.zero, new Vector2(760, 900));
            MakePanel(board.transform, "TopBar", Orange, new Vector2(0, 420), new Vector2(760, 60));
            MakeText(board.transform, "Title", "RICE SHOWER RAMEN", 40, Color.white, new Vector2(0, 420), new Vector2(740, 60), true);
            MakeText(board.transform, "Sub", "Make a bowl of ramen for the customer!", 26, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 360), new Vector2(740, 40));
            gm.rowBg = new Image[n]; gm.rowDot = new Image[n]; gm.rowNum = new Text[n]; gm.rowLabel = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float y = 290 - i * 62;
                var row = MakePanel(board.transform, "Row" + (i + 1), new Color(1, 1, 1, 0.03f), new Vector2(0, y), new Vector2(700, 54));
                gm.rowBg[i] = row.GetComponent<Image>();
                var dot = MakePanel(row, "Dot", new Color(0.35f, 0.35f, 0.38f), new Vector2(-315, 0), new Vector2(46, 46));
                gm.rowDot[i] = dot.GetComponent<Image>();
                gm.rowNum[i] = MakeText(dot, "Num", (i + 1).ToString(), 24, Color.white, Vector2.zero, new Vector2(46, 46), true);
                var lab = MakeText(row, "Label", RamenGameManager.Labels[i], 30, Color.white, new Vector2(40, 0), new Vector2(600, 54));
                lab.alignment = TextAnchor.MiddleLeft;
                gm.rowLabel[i] = lab;
            }
            var barBg = MakePanel(board.transform, "ProgressBG", new Color(1, 1, 1, 0.1f), new Vector2(-40, -375), new Vector2(560, 26));
            var fill = MakePanel(barBg, "Fill", Green, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero; fill.pivot = new Vector2(0, 0.5f);
            gm.progressFill = fill;
            gm.progressText = MakeText(board.transform, "Progress", "0 / 10", 30, Color.white, new Vector2(300, -375), new Vector2(140, 40));
            gm.hintText = MakeText(board.transform, "Hint", "", 30, Gold, new Vector2(0, -420), new Vector2(740, 44));

            // ---- finish screen (covers the board) ----
            var done = MakePanel(board.transform, "Done_Panel", new Color(0.06f, 0.06f, 0.08f, 0.98f), Vector2.zero, new Vector2(760, 900));
            MakePanel(done, "Glow", new Color(1f, 0.42f, 0.25f, 0.08f), new Vector2(0, 140), new Vector2(760, 360));
            MakePanel(done, "Banner", Orange, new Vector2(0, 385), new Vector2(760, 130));
            MakeText(done, "Title", "ORDER COMPLETE!", 64, Color.white, new Vector2(0, 385), new Vector2(740, 120), true);
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 90 : 64;
                var d = MakePanel(done, "Star" + i, Gold, new Vector2((i - 1) * 130, i == 1 ? 225 : 205), new Vector2(s, s));
                d.localRotation = Quaternion.Euler(0, 0, 45);
                var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.5f, 0.3f, 0f); o.effectDistance = new Vector2(4, -4);
            }
            MakeText(done, "Itadakimasu", "Itadakimasu!", 84, Gold, new Vector2(0, 90), new Vector2(740, 110), true).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            MakeText(done, "Sub", "Your shoyu ramen was served. The customer loved it!", 28, new Color(0.85f, 0.85f, 0.9f), new Vector2(0, 15), new Vector2(700, 50));
            MakePanel(done, "Divider", new Color(1, 1, 1, 0.12f), new Vector2(0, -30), new Vector2(620, 3));
            var statsBg = MakePanel(done, "StatsBG", new Color(1, 1, 1, 0.06f), new Vector2(0, -95), new Vector2(620, 80));
            gm.doneStatsText = MakeText(statsBg, "Stats", "10 / 10 steps", 34, Color.white, Vector2.zero, new Vector2(600, 80), true);
            MakeText(done, "Thanks", "Thank you for visiting\nRICE SHOWER RAMEN", 32, new Color(0.8f, 0.8f, 0.85f), new Vector2(0, -195), new Vector2(700, 90));
            var again = MakePanel(done, "PlayAgain_Button", Green, new Vector2(0, -340), new Vector2(380, 100));
            MakeText(again, "Label", "PLAY AGAIN", 44, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var ab = again.gameObject.AddComponent<Button>(); ab.targetGraphic = again.GetComponent<Image>();
            var abc = again.gameObject.AddComponent<BoxCollider>(); abc.size = new Vector3(380, 100, 40);
            again.gameObject.AddComponent<XRSimpleInteractable>();
            var restart = again.gameObject.AddComponent<RestartButton>();
            UnityEventTools.AddPersistentListener(ab.onClick, restart.Restart);
            gm.donePanel = done.gameObject;
            gm.confettiMaterial = ParticleMat();

            // ---- floating HUD ----
            var hud = MakeCanvas("Next_HUD", uiRoot.transform, boardPos, rot, new Vector2(620, 70), 0.0008f);
            MakePanel(hud.transform, "BG", new Color(0.07f, 0.07f, 0.09f, 0.8f), Vector2.zero, new Vector2(620, 70));
            MakePanel(hud.transform, "Accent", Orange, new Vector2(-305, 0), new Vector2(10, 70));
            gm.hudText = MakeText(hud.transform, "Text", "", 32, Color.white, new Vector2(5, 0), new Vector2(590, 70));
            hud.gameObject.AddComponent<LazyHud>();

            // ---- order panel ----
            Vector3 orderPos = menuBtn ? Bounds(menuBtn).center + Vector3.up * 0.4f : boardPos + Vector3.down * 0.5f;
            orderPos -= (rot * Vector3.forward) * 0.15f;
            var order = MakeCanvas("Order_Panel", uiRoot.transform, orderPos, rot, new Vector2(520, 330), 0.0013f);
            MakePanel(order.transform, "BG", Bg, Vector2.zero, new Vector2(520, 330));
            MakePanel(order.transform, "TopBar", Orange, new Vector2(0, 140), new Vector2(520, 50));
            MakeText(order.transform, "Head", "NEW ORDER", 32, Color.white, new Vector2(0, 140), new Vector2(500, 50), true);
            MakeText(order.transform, "Text", "Shoyu Ramen x1\negg - pork - spring onion", 30, new Color(0.9f, 0.9f, 0.92f), new Vector2(0, 50), new Vector2(500, 110));
            var btn = MakePanel(order.transform, "Start_Button", Green, new Vector2(0, -90), new Vector2(380, 100));
            MakeText(btn, "Label", "START ORDER", 42, Color.white, Vector2.zero, new Vector2(380, 100), true);
            var button = btn.gameObject.AddComponent<Button>(); button.targetGraphic = btn.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, gm.StartOrder);
            var bc = btn.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(380, 100, 40);
            btn.gameObject.AddComponent<XRSimpleInteractable>();
            btn.gameObject.AddComponent<StartOrderButton>();
            gm.orderPanel = order.gameObject;

            // ---- door sign ----
            if (door && doorBtn)
            {
                Vector3 outDir = step ? Center(step) - Center(door) : -door.forward; outDir.y = 0; outDir.Normalize();
                Vector3 p = Center(doorBtn);
                var sign = MakeCanvas("Door_Sign", uiRoot.transform, new Vector3(p.x + 0.05f, p.y - 0.32f, p.z) + outDir * 0.15f, Quaternion.LookRotation(-outDir), new Vector2(420, 120), 0.0016f);
                MakePanel(sign.transform, "BG", Bg, Vector2.zero, new Vector2(420, 120));
                MakeText(sign.transform, "Text", "WELCOME!\nPress the bell to open", 34, Color.white, Vector2.zero, new Vector2(400, 110));
            }

            // ---- guide arrows ----
            gm.pickArrow = MakeArrow(uiRoot.transform, "Arrow_Pick", "THIS", Orange);
            gm.placeArrow = MakeArrow(uiRoot.transform, "Arrow_Place", "PUT HERE", Green);
            string[] pick = { "bell", "menubutton", "bowl", "noodles", "egg", "pork", "springonion", "soupbutton", "chopsticks", "bowl" };
            string[] place = { null, null, "bowlmat", "bowl", "bowl", "bowl", "bowl", null, "chopstickspot", "tray" };
            gm.pickTargets = pick.Select(x => x == null ? null : F(x)).ToArray();
            gm.placeTargets = place.Select(x => x == null ? null : F(x)).ToArray();
            // only real buttons catch the laser; panels and signs let it pass through
            foreach (var img in uiRoot.GetComponentsInChildren<Image>(true)) img.raycastTarget = img.GetComponent<Button>() != null;
        }

        public static Transform MakeArrow(Transform parent, string name, string text, Color col)
        {
            var c = MakeCanvas(name, parent, parent.position, Quaternion.identity, new Vector2(300, 200), 0.0016f);
            Object.DestroyImmediate(c.GetComponent<TrackedDeviceGraphicRaycaster>());
            var t = MakeText(c.transform, "Text", text, 60, col, new Vector2(0, 50), new Vector2(300, 100), true);
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = Color.black; o.effectDistance = new Vector2(3, -3);
            var tip = MakePanel(c.transform, "Tip", col, new Vector2(0, -35), new Vector2(70, 70));
            tip.localRotation = Quaternion.Euler(0, 0, 45);
            var to = tip.gameObject.AddComponent<Outline>(); to.effectColor = Color.black; to.effectDistance = new Vector2(3, -3);
            c.gameObject.AddComponent<FaceCamera>();
            return c.transform;
        }

        public static Canvas MakeCanvas(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size; rt.position = pos; rt.rotation = rot;
            rt.localScale = Vector3.one * scale;
            return c;
        }

        public static RectTransform MakePanel(Transform parent, string name, Color col, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Text MakeText(Transform parent, string name, string text, int size, Color col, Vector2 pos, Vector2 box, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyle.Bold;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            return t;
        }

        public static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null) { new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule)); return; }
            if (!es.GetComponent<XRUIInputModule>())
            {
                foreach (var m in es.GetComponents<BaseInputModule>()) m.enabled = false;
                es.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        // make the far rays reach further (any caster/ray component with a distance field)
        public static void LongRays(GameObject origin, float dist)
        {
            foreach (var c in origin.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                bool changed = false;
                foreach (var name in new[] { "m_CastDistance", "m_MaxRaycastDistance" })
                {
                    var prop = so.FindProperty(name);
                    if (prop != null && prop.propertyType == SerializedPropertyType.Float && prop.floatValue < dist) { prop.floatValue = dist; changed = true; }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        public static bool IsUnder(Transform t, HashSet<Transform> roots)
        {
            for (var p = t; p != null; p = p.parent) if (roots.Contains(p)) return true;
            return false;
        }

        public static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "SoupInBowl").ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.1f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static Vector3 Center(Transform t) => Bounds(t).center;

        /// FBX loses the offset of empties parented to a mesh, so put an 'inside' spot back into its container
        public static void PlaceInside(Transform spot, Transform container, float height01 = 0.35f, Vector3 sideOffset = default)
        {
            if (!spot || !container) return;
            var b = Bounds(container);
            spot.position = new Vector3(b.center.x, b.min.y + b.size.y * height01, b.center.z) + sideOffset;
            spot.rotation = Quaternion.identity;
        }

        public static Material ParticleMat()
        {
            const string path = "Assets/RamenVR/Materials/Confetti.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            EnsureMatFolder();
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            m = new Material(sh); AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureMatFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RamenVR/Materials")) AssetDatabase.CreateFolder("Assets/RamenVR", "Materials");
        }

        public static Material MakeMat(string name, Color col, bool transparent)
        {
            EnsureMatFolder();
            string path = $"Assets/RamenVR/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var m = new Material(Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard")) { name = name };
            if (urp) m.SetColor("_BaseColor", col); else m.color = col;
            if (transparent)
            {
                if (urp)
                {
                    m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else
                {
                    m.SetFloat("_Mode", 3);
                    m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHABLEND_ON");
                }
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
