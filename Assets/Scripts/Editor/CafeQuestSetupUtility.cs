using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static VRVillage.Editor.QuestBuildKit;

namespace VRVillage.Editor
{
    /// <summary>
    /// Builds the Cafe (barista shift) quest in the Village scene:
    ///   CafeQuest (QuestManager)
    ///     QuestPanel, Anchors/, Steps/, FX/, Guide/ (as in the other houses)
    ///     Chairs/   Spot_* (where chairs belong, with a green floor marker) and Mess_* (where they start)
    ///     Bar/      MachineSocket and GrinderSocket for the portafilter
    /// The cafe model (Cafe.fbx) stays a linked prefab instance, so Blender re-exports keep flowing in;
    /// components are added onto its objects (door, sign, chairs, portafilter, crank).
    /// Safe to re-run: anchors, spots, mess poses and sockets you've moved keep their placement.
    /// </summary>
    public static class CafeQuestSetupUtility
    {
        private const string RootName = "CafeQuest";
        private const string CafeName = "Cafe";

        /// <summary>How many of the indoor chairs get messed up each play (picked at random).</summary>
        private const int MessyChairCount = 4;

        private static readonly string[] s_ShortTexts =
        {
            "Open the cafe door",
            "Touch the OPEN sign to turn it on",
            "Put the chairs back on the green squares",
            "Portafilter under the grinder, then turn the crank",
            "Lock the portafilter into the coffee machine",
            "Put a cup under the coffee machine",
            "Pick a drink on the machine's white screen",
            "Take the cup out and pour milk from the pitcher",
            "Cup on a plate, cake on another plate",
            "Serve the coffee and cake at the counter",
        };

        private static readonly string[] s_DrinkNames = { "Espresso", "Latte", "Cappuccino" };
        private static readonly Color[] s_DrinkButtons =
        {
            new Color(0.3f, 0.17f, 0.08f), new Color(0.62f, 0.42f, 0.24f), new Color(0.47f, 0.3f, 0.17f),
        };
        private static readonly Color s_Milk = new Color(0.97f, 0.96f, 0.92f);
        private static readonly Color s_Coffee = new Color(0.2f, 0.11f, 0.05f);

        private static readonly Color s_NeonOff = new Color(0.32f, 0.27f, 0.2f);
        private static readonly Color s_Grounds = new Color(0.22f, 0.13f, 0.07f);
        private static readonly Color s_SpotGreen = new Color(0.3f, 1f, 0.45f, 0.4f);
        private static readonly Color s_GhostGreen = new Color(0.3f, 1f, 0.45f, 0.35f);
        private static readonly Color s_ChairGlow = new Color(1f, 0.78f, 0.24f, 0.5f);

        [MenuItem("VR Village/Setup Cafe Quest")]
        public static void Setup()
        {
            if (EditorSceneManager.GetActiveScene().name != "Village")
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/Village.unity", OpenSceneMode.Single);
            }

            GameObject cafe = FindInScene(CafeName);
            if (cafe == null)
            {
                Debug.LogError($"[Cafe Setup] '{CafeName}' not found in the scene. Drag Cafe.fbx into the scene first.");
                return;
            }

            // The portafilter's basket is located from mesh data, so the model must be readable.
            if (EnsureReadable(Need<Renderer>(cafe.transform, "CafeBuilding")))
                cafe = FindInScene(CafeName);

            Transform c = cafe.transform;
            Renderer building = Need<Renderer>(c, "CafeBuilding");
            Renderer door = Need<Renderer>(c, "CafeDoor");
            Renderer neon = Need<Renderer>(c, "OpenNeon");
            Renderer grinder = Need<Renderer>(c, "Grinder");
            Renderer crank = Need<Renderer>(c, "GrinderCrank");
            Renderer portafilter = Need<Renderer>(c, "Portafilter");
            Renderer machine = Need<Renderer>(c, "EspressoMachine");
            Renderer backBar = Need<Renderer>(c, "BackBar");
            Renderer frontCounter = Need<Renderer>(c, "FrontCounter");
            Renderer pitcher = Need<Renderer>(c, "MilkPitcher");
            List<Renderer> cups = NeedNumbered(c, "Cup");
            List<Renderer> saucers = NeedNumbered(c, "Saucer");
            List<Renderer> cakes = NeedNumbered(c, "Cake");
            if (building == null || door == null || neon == null || grinder == null || crank == null || portafilter == null ||
                machine == null || backBar == null || frontCounter == null || pitcher == null ||
                cups.Count == 0 || saucers.Count < 2 || cakes.Count == 0)
            {
                Debug.LogError("[Cafe Setup] Some cafe objects are missing (see the errors above). Nothing was changed.");
                return;
            }

            EnsureEventSystem();

            // ---------- Colliders on scenery (so things can be set down and land) ----------
            foreach (string n in new[] { "CafeBuilding", "BackBar", "FrontCounter", "Table", "Table.001",
                         "Table.002", "Table.003", "Table.004" })
            {
                Transform t = FindDeepChild(c, n);
                if (t != null) EnsureStaticCollider(t.gameObject);
            }
            // No collider on the pastry case: hands reach through its glass for the cakes, which rest on
            // their shelves until picked up (PlateItem "start resting"). An earlier run added one.
            Transform pastryCase = FindDeepChild(c, "PastryCase");
            if (pastryCase != null)
            {
                foreach (MeshCollider mc in pastryCase.GetComponents<MeshCollider>()) Undo.DestroyObjectImmediate(mc);
            }

            // ---------- Hierarchy ----------
            GameObject root = FindOrCreate(null, RootName);
            Transform anchors = FindOrCreate(root.transform, "Anchors").transform;
            Transform steps = FindOrCreate(root.transform, "Steps").transform;
            Transform fx = FindOrCreate(root.transform, "FX").transform;
            Transform chairRoot = FindOrCreate(root.transform, "Chairs").transform;
            Transform barRoot = FindOrCreate(root.transform, "Bar").transform;

            QuestManager manager = GetOrAdd<QuestManager>(root);
            QuestPanel panel = BuildPanel(root.transform);
            panel.transform.localScale = Vector3.one * 0.00075f; // ~1.2 m wide: a cafe is cosy

            // ---------- Geometry ----------
            var frame = new BuildingFrame(c);
            Bounds doorB = door.bounds;
            Bounds barB = backBar.bounds;
            Vector3 outward = frame.Snap(doorB.center - barB.center);           // from the cafe out to the terrace
            Vector3 barFront = frame.Snap(frontCounter.bounds.center - barB.center); // from the back bar toward the barista
            // Panels read from their -Z side, so +Z points away from where the player stands.
            Quaternion facesOutside = Quaternion.LookRotation(-outward, Vector3.up); // read from the terrace / the doorway
            Quaternion facesBarista = Quaternion.LookRotation(-barFront, Vector3.up);
            float floorY = doorB.min.y;

            // ---------- 1. Door ----------
            DoorMover doorMover = SetupDoor(door, outward);

            // ---------- 2. OPEN sign ----------
            NeonSign sign = GetOrAdd<NeonSign>(neon.gameObject);
            XRSimpleInteractable signTouch = SimpleInteractable(neon.gameObject, 0.08f);
            var soSign = new SerializedObject(sign);
            soSign.FindProperty("m_Renderer").objectReferenceValue = neon;
            if (!(soSign.FindProperty("m_OnMaterial").objectReferenceValue is Material))
                soSign.FindProperty("m_OnMaterial").objectReferenceValue = neon.sharedMaterial; // the model's glowing neon
            soSign.FindProperty("m_OffMaterial").objectReferenceValue =
                GetOrCreateMaterial("Assets/Materials/CafeNeonOff.mat", "Universal Render Pipeline/Lit", s_NeonOff, 0.8f);
            soSign.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 3. Chairs ----------
            // Every chair inside the cafe can be picked for the mess (chosen at random each play).
            var chairs = new List<Transform>();
            foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
            {
                if ((t.name == "Chair" || t.name.StartsWith("Chair.")) && Vector3.Dot(t.position - doorB.center, outward) < 0f)
                    chairs.Add(t);
            }
            chairs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (chairs.Count == 0)
            {
                Debug.LogError("[Cafe Setup] No chairs found inside the cafe.");
                return;
            }
            foreach (Transform old in new List<Transform>(chairRoot.GetComponentsInChildren<Transform>(true)))
            {
                if (old != null && old.name.StartsWith("Mess_")) Undo.DestroyObjectImmediate(old.gameObject); // fixed poses from before
            }

            var chairGrabs = new List<XRGrabInteractable>();
            var spots = new List<Transform>();
            var markers = new List<GameObject>();
            Material spotMat = GetOrCreateTransparentUnlit("Assets/Materials/CafeChairSpot.mat", s_SpotGreen);
            Material ghostMat = GetOrCreateTransparentUnlit("Assets/Materials/CafeChairGhost.mat", s_GhostGreen);
            Material glowMat = GetOrCreateTransparentUnlit("Assets/Materials/CafeChairHighlight.mat", s_ChairGlow);
            foreach (Transform chair in chairs)
            {
                Bounds cb = RendererBounds(chair);
                // Where it belongs = where it was modelled. Created once, so a later run keeps your tweaks.
                Transform spot = FindOrCreatePose(chairRoot, "Spot_" + chair.name, chair.position, chair.rotation);
                spots.Add(spot);
                markers.Add(BuildSpotMarker(spot, chair, new Vector2(cb.size.x, cb.size.z) + Vector2.one * 0.08f,
                    floorY + 0.006f, spotMat, ghostMat));
                chairGrabs.Add(SetupGrabbable(chair.gameObject, 4f));
            }

            // "Move me!" markers that the step places above out-of-place chairs at runtime.
            Transform moveRoot = FindOrCreate(chairRoot, "MoveMarkers").transform;
            var moveMarkers = new List<Transform>();
            for (int i = 0; i < MessyChairCount + 2; i++) // a couple spare, for chairs the player moves out
            {
                Transform m = FindOrCreate(moveRoot, "MoveMarker_" + i).transform;
                m.localScale = Vector3.one * 0.6f;
                BuildHereMarker(m, "Move me!");
                moveMarkers.Add(m);
            }
            Vector3 chairsCenter = Vector3.zero;
            foreach (Transform s in spots) chairsCenter += s.position;
            chairsCenter /= spots.Count;

            // ---------- 4-5. Portafilter, sockets, crank ----------
            XRGrabInteractable portaGrab = SetupGrabbable(portafilter.gameObject, 0.5f);
            if (!MaterialBounds(portafilter, "Steel", frame, out Bounds basketL))
            {
                Debug.LogError("[Cafe Setup] Couldn't find the basket (material 'Steel') on Portafilter.");
                return;
            }
            Vector3 basketW = frame.ToWorld(basketL.center);
            Transform portaParts = RebuildParts(portafilter.transform);
            Transform attach = NewPart("Attach", portaParts, basketW, portafilter.transform.rotation);
            KeepPlacement(attach);
            portaGrab.attachTransform = attach;
            EditorUtility.SetDirty(portaGrab);
            Transform grounds = BuildGrounds(portaParts, frame.ToWorld(new Vector3(basketL.center.x, basketL.max.y - 0.008f, basketL.center.z)),
                Mathf.Min(basketL.size.x, basketL.size.z) * 0.75f);

            // The portafilter is modelled locked into the machine: that's the machine socket's pose.
            XRSocketInteractor machineSocket = BuildSocket(barRoot, "MachineSocket", basketW, portafilter.transform.rotation,
                new[] { portaGrab }, portaGrab, SynthSound.MetalClack);
            Bounds gB = grinder.bounds;
            float basketHalf = basketL.size.y * 0.5f;
            Vector3 grinderSpot = Flat(gB.center) + barFront * (Extent(frame.BoundsOf(grinder.transform), frame.LocalDir(barFront)) + 0.07f)
                                  + Vector3.up * (gB.min.y + basketHalf + 0.03f);
            XRSocketInteractor grinderSocket = BuildSocket(barRoot, "GrinderSocket", grinderSpot, portafilter.transform.rotation,
                new[] { portaGrab }, null, SynthSound.MetalClack);

            CrankHandle crankHandle = GetOrAdd<CrankHandle>(crank.gameObject);
            BoxCollider crankBox = ThickBox(crank.gameObject, 0.04f);
            var soCrank = new SerializedObject(crankHandle);
            SetArray(soCrank.FindProperty("m_Colliders"), new Object[] { crankBox });
            soCrank.FindProperty("m_Crank").objectReferenceValue = crank.transform;
            soCrank.FindProperty("m_LoopSound").enumValueIndex = (int)SynthSound.Grind;
            soCrank.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 6-9. Cups, plates, cakes, milk ----------
            Material coffeeMat = GetOrCreateMaterial("Assets/Materials/CafeCoffee.mat", "Universal Render Pipeline/Lit", s_Coffee, 0.85f);
            var cupGrabs = new List<XRGrabInteractable>();
            var cupItems = new List<PlateItem>();
            foreach (Renderer cup in cups)
            {
                XRGrabInteractable g = SetupGrabbable(cup.gameObject, 0.25f);
                cupGrabs.Add(g);
                Transform parts = RebuildParts(cup.transform);
                Bounds b = cup.bounds;
                Transform cupBase = NewPart("Base", parts, new Vector3(b.center.x, b.min.y, b.center.z), cup.transform.rotation);
                g.attachTransform = cupBase; // a socket stands the cup on its bottom
                EditorUtility.SetDirty(g);
                cupItems.Add(SetupPlateItem(cup.gameObject, cupBase, false));
                BuildCupFill(cup, parts, coffeeMat);
            }
            var cakeItems = new List<PlateItem>();
            foreach (Renderer cake in cakes)
            {
                SetupGrabbable(cake.gameObject, 0.15f);
                Transform parts = RebuildParts(cake.transform);
                Bounds b = cake.bounds;
                cakeItems.Add(SetupPlateItem(cake.gameObject,
                    NewPart("Base", parts, new Vector3(b.center.x, b.min.y, b.center.z), cake.transform.rotation), true));
            }
            var plateAccepts = new List<PlateItem>(cupItems);
            plateAccepts.AddRange(cakeItems);
            var plates = new List<PlateHolder>();
            foreach (Renderer saucer in saucers)
            {
                SetupGrabbable(saucer.gameObject, 0.3f);
                ThickBox(saucer.gameObject, 0.012f); // a saucer is too thin to grab otherwise
                Transform parts = RebuildParts(saucer.transform);
                Bounds b = saucer.bounds;
                Transform top = NewPart("Top", parts, new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.identity);
                PlateHolder holder = GetOrAdd<PlateHolder>(saucer.gameObject);
                var soP = new SerializedObject(holder);
                soP.FindProperty("m_Top").objectReferenceValue = top;
                SetArray(soP.FindProperty("m_Accepts"), plateAccepts);
                soP.ApplyModifiedPropertiesWithoutUndo();
                plates.Add(holder);
            }

            XRGrabInteractable pitcherGrab = SetupGrabbable(pitcher.gameObject, 0.4f);
            Transform pitcherParts = RebuildParts(pitcher.transform);
            Bounds pB = pitcher.bounds;
            Transform rim = NewPart("Rim", pitcherParts, new Vector3(pB.center.x, pB.max.y, pB.center.z), Quaternion.identity);
            float rimRadius = Mathf.Min(pB.extents.x, pB.extents.z) * 0.85f;

            // Cup spot under the portafilter, standing on whatever is below it (drip tray or bar).
            float basketBottom = frame.ToWorld(basketL.center - Vector3.up * basketL.extents.y).y;
            float trayY = TrayHeight(machine, basketW, basketBottom, barB.max.y);
            XRSocketInteractor cupSocket = BuildSocket(barRoot, "CupSocket", new Vector3(basketW.x, trayY + 0.002f, basketW.z),
                cups[0].transform.rotation, cupGrabs, null, SynthSound.PlateClink);
            Transform spout = FindOrCreatePose(barRoot, "CoffeeSpout", new Vector3(basketW.x, basketBottom - 0.005f, basketW.z), Quaternion.identity);
            StreamFx coffeeStream = BuildStream(barRoot, "CoffeeStream", 0.007f);
            StreamFx milkStream = BuildStream(barRoot, "MilkStream", 0.012f);

            // Drink buttons on the machine's white panel.
            if (!FrontFaces(machine, "White", barFront, frame, out Bounds screenB))
            {
                Debug.LogError("[Cafe Setup] Couldn't find the screen (material 'White') on EspressoMachine's front.");
                return;
            }
            Vector3 frontL = frame.LocalDir(barFront);
            Transform screenAnchor = FindOrCreateAnchor(barRoot, "DrinkScreenAnchor",
                frame.ToWorld(screenB.center + frontL * 0.004f), facesBarista);
            Button[] drinkButtons = BuildDrinkButtons(screenAnchor, new Vector2(Across(screenB, frontL), screenB.size.y));

            // ---------- 10. Serving spot on the front counter ----------
            Bounds fcB = frontCounter.bounds;
            Transform counterRoot = FindOrCreate(root.transform, "Counter").transform;
            float counterDepth = Extent(frame.BoundsOf(frontCounter.transform), frontL) * 2f;
            ItemTrigger serveZone = KeepOrBuildTrigger(counterRoot, "ServeZone", frame,
                frame.ToLocal(new Vector3(fcB.center.x, fcB.max.y + 0.15f, fcB.center.z)),
                new Vector3(1.1f, 0.3f, counterDepth), -barFront);
            var soZone = new SerializedObject(serveZone);
            soZone.FindProperty("m_LogNearMisses").boolValue = false;
            soZone.ApplyModifiedPropertiesWithoutUndo();
            Transform hereAnchor = FindOrCreateAnchor(counterRoot, "HereMarkerAnchor",
                new Vector3(fcB.center.x, fcB.max.y + 0.5f, fcB.center.z), Quaternion.LookRotation(barFront, Vector3.up));
            GameObject hereMarker = BuildHereMarker(hereAnchor);

            // ---------- Panel anchors ----------
            Transform a1 = FindOrCreateAnchor(anchors, "QuestAnchor_1_Door",
                Flat(doorB.center) + outward * 0.3f + Vector3.up * (doorB.max.y + 0.35f), facesOutside);
            Bounds nB = neon.bounds;
            Transform a2 = FindOrCreateAnchor(anchors, "QuestAnchor_2_OpenSign",
                Flat(nB.center) + outward * 0.25f + Vector3.up * (nB.max.y + 0.32f), facesOutside);
            Transform a3 = FindOrCreateAnchor(anchors, "QuestAnchor_3_Chairs",
                Flat(doorB.center) - outward * 2.6f + Vector3.up * (floorY + 2.15f), facesOutside);
            Vector3 barTop = Vector3.up * (barB.max.y + 1.05f);
            Vector3 mB = machine.bounds.center;
            Transform a4 = FindOrCreateAnchor(anchors, "QuestAnchor_4_Grind",
                Flat((gB.center + mB) * 0.5f) + barFront * 0.35f + barTop, facesBarista);
            Transform a5 = FindOrCreateAnchor(anchors, "QuestAnchor_5_LockPortafilter",
                Flat(mB) + barFront * 0.35f + barTop, facesBarista);
            Transform a6 = FindOrCreateAnchor(anchors, "QuestAnchor_6_Cup",
                Flat(mB) + barFront * 0.35f + barTop, facesBarista);
            Transform a7 = FindOrCreateAnchor(anchors, "QuestAnchor_7_ChooseDrink",
                Flat(mB) + barFront * 0.35f + barTop, facesBarista);
            Transform a8 = FindOrCreateAnchor(anchors, "QuestAnchor_8_Milk",
                Flat(pB.center) + barFront * 0.35f + barTop, facesBarista);
            Vector3 saucersCenter = Vector3.zero;
            foreach (Renderer s in saucers) saucersCenter += s.bounds.center;
            saucersCenter /= saucers.Count;
            Transform a9 = FindOrCreateAnchor(anchors, "QuestAnchor_9_Plate",
                Flat(saucersCenter) + barFront * 0.35f + barTop, facesBarista);
            // Over the counter, read from the barista's side (the player faces +barFront to look at it).
            Transform a10 = FindOrCreateAnchor(anchors, "QuestAnchor_10_Serve",
                new Vector3(fcB.center.x, fcB.max.y + 0.95f, fcB.center.z) + barFront * 0.1f, Quaternion.LookRotation(barFront, Vector3.up));

            QuestCelebration chairsFanfare = BuildCelebration(fx, "Celebration_Chairs", chairsCenter + Vector3.up * 1.2f);
            QuestCelebration barSparkle = BuildCelebration(fx, "Celebration_Bar", gB.center + Vector3.up * 0.3f);
            QuestCelebration serveConfetti = BuildCelebration(fx, "Celebration_Serve",
                new Vector3(fcB.center.x, fcB.max.y + 0.45f, fcB.center.z), CelebrationStyle.ConfettiStyle());

            // ---------- Steps ----------
            DoorOpenStep step1 = GetOrAdd<DoorOpenStep>(FindOrCreate(steps, "Step1_OpenDoor"));
            var so1 = StepSO(step1, "Open the door to start your barista shift", a1);
            so1.FindProperty("m_Door").objectReferenceValue = doorMover;
            so1.ApplyModifiedPropertiesWithoutUndo();

            TouchSignStep step2 = GetOrAdd<TouchSignStep>(FindOrCreate(steps, "Step2_OpenSign"));
            var so2 = StepSO(step2, "Turn on the OPEN sign.", a2);
            so2.FindProperty("m_Sign").objectReferenceValue = sign;
            so2.FindProperty("m_Touch").objectReferenceValue = signTouch;
            so2.ApplyModifiedPropertiesWithoutUndo();

            ArrangeChairsStep step3 = GetOrAdd<ArrangeChairsStep>(FindOrCreate(steps, "Step3_Chairs"));
            var so3 = StepSO(step3, "The chairs in here are a mess! Organize them.", a3, chairsFanfare);
            SetArray(so3.FindProperty("m_Chairs"), chairGrabs);
            SetArray(so3.FindProperty("m_Spots"), spots);
            SetArray(so3.FindProperty("m_SpotMarkers"), markers);
            so3.FindProperty("m_MessCount").intValue = MessyChairCount;
            so3.FindProperty("m_HighlightMaterial").objectReferenceValue = glowMat;
            SetArray(so3.FindProperty("m_MoveMarkers"), moveMarkers);
            so3.ApplyModifiedPropertiesWithoutUndo();

            GrindStep step4 = GetOrAdd<GrindStep>(FindOrCreate(steps, "Step4_GrindBeans"));
            var so4 = StepSO(step4, "Grab the portafilter and grind the beans.", a4);
            so4.FindProperty("m_Portafilter").objectReferenceValue = portaGrab;
            so4.FindProperty("m_GrinderSocket").objectReferenceValue = grinderSocket;
            so4.FindProperty("m_Crank").objectReferenceValue = crankHandle;
            so4.FindProperty("m_Grounds").objectReferenceValue = grounds;
            so4.FindProperty("m_Sparkle").objectReferenceValue = barSparkle;
            so4.ApplyModifiedPropertiesWithoutUndo();

            SocketItemStep step5 = GetOrAdd<SocketItemStep>(FindOrCreate(steps, "Step5_LockPortafilter"));
            var so5 = StepSO(step5, "Lock the portafilter into the coffee machine.", a5);
            so5.FindProperty("m_Socket").objectReferenceValue = machineSocket;
            SetArray(so5.FindProperty("m_Items"), new[] { portaGrab });
            so5.FindProperty("m_Hint").stringValue = "Take it from the grinder and click it back into the machine";
            so5.FindProperty("m_DoneText").stringValue = "Click! Locked in.";
            so5.ApplyModifiedPropertiesWithoutUndo();

            SocketItemStep step6 = GetOrAdd<SocketItemStep>(FindOrCreate(steps, "Step6_CupUnderMachine"));
            var so6 = StepSO(step6, "Place a cup under the coffee machine.", a6);
            so6.FindProperty("m_Socket").objectReferenceValue = cupSocket;
            SetArray(so6.FindProperty("m_Items"), cupGrabs);
            so6.FindProperty("m_Hint").stringValue = "Grab a white cup from the bar and put it under the portafilter";
            so6.FindProperty("m_DoneText").stringValue = "Cup ready!";
            so6.ApplyModifiedPropertiesWithoutUndo();

            BrewStep step7 = GetOrAdd<BrewStep>(FindOrCreate(steps, "Step7_ChooseDrink"));
            var so7 = StepSO(step7, "Choose a drink on the machine's screen.", a7);
            SetArray(so7.FindProperty("m_Buttons"), drinkButtons);
            so7.FindProperty("m_CupSocket").objectReferenceValue = cupSocket;
            so7.FindProperty("m_PortafilterSocket").objectReferenceValue = machineSocket;
            so7.FindProperty("m_Spout").objectReferenceValue = spout;
            so7.FindProperty("m_Stream").objectReferenceValue = coffeeStream;
            so7.FindProperty("m_CoffeeColor").colorValue = s_Coffee;
            so7.ApplyModifiedPropertiesWithoutUndo();

            PourMilkStep step8 = GetOrAdd<PourMilkStep>(FindOrCreate(steps, "Step8_PourMilk"));
            var so8 = StepSO(step8, "Take your coffee out and pour some milk in.", a8);
            so8.FindProperty("m_Brew").objectReferenceValue = step7;
            so8.FindProperty("m_CupSocket").objectReferenceValue = cupSocket;
            so8.FindProperty("m_Pitcher").objectReferenceValue = pitcherGrab;
            so8.FindProperty("m_Rim").objectReferenceValue = rim;
            so8.FindProperty("m_RimRadius").floatValue = rimRadius;
            so8.FindProperty("m_Stream").objectReferenceValue = milkStream;
            so8.FindProperty("m_MilkColor").colorValue = s_Milk;
            so8.ApplyModifiedPropertiesWithoutUndo();

            PlateStep step9 = GetOrAdd<PlateStep>(FindOrCreate(steps, "Step9_PlateUp"));
            var so9 = StepSO(step9, "Put the cup on a plate, then put a cake on a plate.", a9);
            so9.FindProperty("m_Brew").objectReferenceValue = step7;
            SetArray(so9.FindProperty("m_Cakes"), cakeItems);
            SetArray(so9.FindProperty("m_Plates"), plates);
            so9.ApplyModifiedPropertiesWithoutUndo();

            ServeStep step10 = GetOrAdd<ServeStep>(FindOrCreate(steps, "Step10_Serve"));
            var so10 = StepSO(step10, "Serve everything at the counter!", a10, serveConfetti, new[] { hereMarker });
            so10.FindProperty("m_Plating").objectReferenceValue = step9;
            so10.FindProperty("m_Zone").objectReferenceValue = serveZone;
            so10.ApplyModifiedPropertiesWithoutUndo();

            var allSteps = new List<Object> { step1, step2, step3, step4, step5, step6, step7, step8, step9, step10 };
            SetGuideTexts(allSteps, s_ShortTexts, new Dictionary<Object, Transform>
            {
                { step1, door.transform },
                { step2, neon.transform },
            });

            // ---------- Guide ----------
            Bounds floorL = frame.BoundsOf(building.transform);
            BuildGuide(root.transform, frame, floorL, GuideMarginAvoidingOthers(floorL, frame, root.transform));

            var soM = new SerializedObject(manager);
            soM.FindProperty("m_Panel").objectReferenceValue = panel;
            SetArray(soM.FindProperty("m_Steps"), allSteps);
            soM.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("<color=green>[Cafe Setup] Cafe quest ready: actions 1-10.</color>");
        }

        // ------------------------------------------------------------------
        // Finding things
        // ------------------------------------------------------------------
        private static T Need<T>(Transform root, string name) where T : Component
        {
            Transform t = FindDeepChild(root, name);
            T comp = t != null ? t.GetComponent<T>() : null;
            if (comp == null) Debug.LogError($"[Cafe Setup] '{name}' not found under {root.name}.");
            return comp;
        }

        /// <summary>An empty marking a pose. Created once; afterwards your placement is kept.</summary>
        private static Transform FindOrCreatePose(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            GameObject go = FindOrCreate(parent, name);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.transform;
        }

        // ------------------------------------------------------------------
        // Door
        // ------------------------------------------------------------------
        /// <summary>
        /// The door swings on its own pivot (the user modelled its origin on the hinge edge),
        /// outward onto the terrace so it never sweeps through the room.
        /// </summary>
        private static DoorMover SetupDoor(Renderer door, Vector3 outward)
        {
            Transform t = door.transform;
            BoxCollider box = GetOrAdd<BoxCollider>(t.gameObject);
            Bounds local = LocalBounds(t);
            box.center = local.center;
            box.size = local.size;
            XRSimpleInteractable interactable = GetOrAdd<XRSimpleInteractable>(t.gameObject);
            var soI = new SerializedObject(interactable);
            SetArray(soI.FindProperty("m_Colliders"), new Object[] { box });
            soI.ApplyModifiedPropertiesWithoutUndo();

            Vector3 arm = Flat(door.bounds.center - t.position);
            float angle = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * arm, outward) > 0f ? 90f : -90f;
            Quaternion parentRot = t.parent != null ? t.parent.rotation : Quaternion.identity;
            Quaternion openLocal = Quaternion.Inverse(parentRot) * Quaternion.AngleAxis(angle, Vector3.up) * t.rotation;

            DoorMover mover = GetOrAdd<DoorMover>(t.gameObject);
            var so = new SerializedObject(mover);
            so.FindProperty("m_HingeTransform").objectReferenceValue = t;
            so.FindProperty("m_ClosedRotation").vector3Value = t.localEulerAngles;
            so.FindProperty("m_OpenRotation").vector3Value = openLocal.eulerAngles;
            so.FindProperty("m_SwingDuration").floatValue = 0.85f;
            so.FindProperty("m_UseBuiltInSounds").boolValue = true; // latch + creak + bell for this cafe only
            so.ApplyModifiedPropertiesWithoutUndo();
            return mover;
        }

        // ------------------------------------------------------------------
        // Interactables
        // ------------------------------------------------------------------
        /// <summary>Box collider around the object's mesh, at least <paramref name="minThickness"/> m on every axis.</summary>
        private static BoxCollider ThickBox(GameObject go, float minThickness)
        {
            BoxCollider box = GetOrAdd<BoxCollider>(go);
            Bounds local = LocalBounds(go.transform);
            Vector3 size = local.size;
            Vector3 lossy = go.transform.lossyScale;
            for (int i = 0; i < 3; i++)
            {
                float min = minThickness / Mathf.Max(Mathf.Abs(lossy[i]), 1e-4f);
                if (size[i] < min) size[i] = min;
            }
            box.center = local.center;
            box.size = size;
            return box;
        }

        private static XRSimpleInteractable SimpleInteractable(GameObject go, float minThickness)
        {
            BoxCollider box = ThickBox(go, minThickness);
            XRSimpleInteractable interactable = GetOrAdd<XRSimpleInteractable>(go);
            var so = new SerializedObject(interactable);
            SetArray(so.FindProperty("m_Colliders"), new Object[] { box });
            so.ApplyModifiedPropertiesWithoutUndo();
            return interactable;
        }

        /// <summary>
        /// A socket that only takes the listed items. Its pose is where an item's attach point ends up
        /// (the portafilter's basket centre, a cup's bottom), with the item turned as it was modelled.
        /// </summary>
        private static XRSocketInteractor BuildSocket(Transform parent, string name, Vector3 position, Quaternion rotation,
            IList<XRGrabInteractable> accepts, XRGrabInteractable startsHolding, SynthSound sound)
        {
            Transform t = FindOrCreatePose(parent, name, position, rotation);
            SphereCollider sphere = GetOrAdd<SphereCollider>(t.gameObject);
            sphere.isTrigger = true;
            sphere.radius = 0.1f;
            HolsterSocket socket = GetOrAdd<HolsterSocket>(t.gameObject);
            socket.attachTransform = t;
            socket.showInteractableHoverMeshes = true;
            var so = new SerializedObject(socket);
            SetArray(so.FindProperty("m_Accepts"), accepts);
            so.FindProperty("m_StartingSelectedInteractable").objectReferenceValue = startsHolding;
            so.ApplyModifiedPropertiesWithoutUndo();
            var soSound = new SerializedObject(GetOrAdd<SocketSound>(t.gameObject));
            soSound.FindProperty("m_Sound").enumValueIndex = (int)sound;
            soSound.ApplyModifiedPropertiesWithoutUndo();
            return socket;
        }

        /// <summary>A trigger you've moved keeps its place; a new one is built where it should go.</summary>
        private static ItemTrigger KeepOrBuildTrigger(Transform parent, string name, BuildingFrame frame, Vector3 centerL,
            Vector3 size, Vector3 frontW)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.GetComponent<ItemTrigger>() != null) return existing.GetComponent<ItemTrigger>();
            return BuildItemTrigger(parent, name, frame, centerL, size, frontW);
        }

        /// <summary>
        /// Height of whatever is under the portafilter (the machine's drip tray, or the bar if the machine
        /// has nothing there): a ray down against a temporary collider on the machine.
        /// </summary>
        private static float TrayHeight(Renderer machine, Vector3 basketW, float basketBottom, float barTop)
        {
            MeshCollider temp = machine.gameObject.AddComponent<MeshCollider>();
            Physics.SyncTransforms();
            var ray = new Ray(new Vector3(basketW.x, basketBottom - 0.01f, basketW.z), Vector3.down);
            float y = barTop;
            if (temp.Raycast(ray, out RaycastHit hit, basketBottom - barTop + 0.05f)) y = Mathf.Max(hit.point.y, barTop);
            Object.DestroyImmediate(temp);
            return y;
        }

        private static List<Renderer> NeedNumbered(Transform root, string baseName)
        {
            var list = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == baseName || (r.name.StartsWith(baseName + ".") && int.TryParse(r.name.Substring(baseName.Length + 1), out _)))
                    list.Add(r);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (list.Count == 0) Debug.LogError($"[Cafe Setup] No '{baseName}' objects found under {root.name}.");
            return list;
        }

        private static PlateItem SetupPlateItem(GameObject go, Transform itemBase, bool startResting)
        {
            PlateItem item = GetOrAdd<PlateItem>(go);
            var so = new SerializedObject(item);
            so.FindProperty("m_Base").objectReferenceValue = itemBase;
            so.FindProperty("m_Upright").quaternionValue = go.transform.rotation;
            so.FindProperty("m_StartResting").boolValue = startResting;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        /// <summary>The drink inside a cup: a flat disc from just above the bottom (empty) to just under the rim (full).</summary>
        private static void BuildCupFill(Renderer cup, Transform parts, Material mat)
        {
            Bounds b = cup.bounds;
            float diameter = Mathf.Min(b.size.x, b.size.z) * 0.78f; // the narrow side skips the handle
            GameObject disc = Primitive(PrimitiveType.Cylinder, "Drink", parts, Vector3.zero, Vector3.one, mat);
            Vector3 full = new Vector3(b.center.x, b.max.y - 0.012f, b.center.z);
            Vector3 empty = new Vector3(b.center.x, b.min.y + 0.012f, b.center.z);
            disc.transform.SetPositionAndRotation(full, Quaternion.identity);
            float s = Mathf.Max(Mathf.Abs(parts.lossyScale.x), 1e-4f);
            disc.transform.localScale = new Vector3(diameter, 0.002f, diameter) / s;
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            CupFill fill = GetOrAdd<CupFill>(cup.gameObject);
            var so = new SerializedObject(fill);
            so.FindProperty("m_Surface").objectReferenceValue = disc.transform;
            so.FindProperty("m_FullLocal").vector3Value = parts.InverseTransformPoint(full);
            so.FindProperty("m_EmptyLocal").vector3Value = parts.InverseTransformPoint(empty);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A pouring stream (rebuilt each run): StreamFx plus the thin cylinder it stretches.</summary>
        private static StreamFx BuildStream(Transform parent, string name, float width)
        {
            Transform old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            Material mat = GetOrCreateMaterial("Assets/Materials/CafeStream.mat", "Universal Render Pipeline/Unlit", Color.white);
            GameObject cyl = Primitive(PrimitiveType.Cylinder, "Cylinder", go.transform, Vector3.zero, Vector3.one, mat);
            cyl.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StreamFx fx = go.AddComponent<StreamFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("m_Cylinder").objectReferenceValue = cyl.transform;
            so.FindProperty("m_Width").floatValue = width;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        /// <summary>Espresso / Latte / Cappuccino side by side, filling the machine's white panel.</summary>
        private static Button[] BuildDrinkButtons(Transform anchor, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "DrinkButtons", px, true);
            var buttons = new Button[s_DrinkNames.Length];
            float gap = 0.03f, w = (1f - gap * (buttons.Length + 1)) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                float left = gap + i * (w + gap);
                RectTransform slot = NewUIChild("Slot_" + s_DrinkNames[i], canvas);
                slot.anchorMin = new Vector2(left, 0.1f);
                slot.anchorMax = new Vector2(left + w, 0.9f);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                buttons[i] = AddButton(slot, s_DrinkNames[i], s_DrinkButtons[i], Mathf.Min(px.y * 0.3f, px.x * w * 0.16f));
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = px.y * 0.3f;
                }
            }
            return buttons;
        }

        // ------------------------------------------------------------------
        // Generated parts
        // ------------------------------------------------------------------
        private static Transform NewPart(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go.transform;
        }

        /// <summary>Flat disc of coffee grounds in the basket (hidden until the grinding starts).</summary>
        private static Transform BuildGrounds(Transform parts, Vector3 worldPos, float diameter)
        {
            Material mat = GetOrCreateMaterial("Assets/Materials/CafeGrounds.mat", "Universal Render Pipeline/Lit", s_Grounds, 0.1f);
            GameObject go = Primitive(PrimitiveType.Cylinder, "Grounds", parts, Vector3.zero, Vector3.one, mat);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            float s = Mathf.Max(Mathf.Abs(parts.lossyScale.x), 1e-4f);
            go.transform.localScale = new Vector3(diameter, 0.006f, diameter) / s; // cylinder is 2 units tall
            KeepPlacement(go.transform);
            return go.transform;
        }

        /// <summary>
        /// What an empty chair spot shows: a see-through green copy of the chair standing exactly where it
        /// belongs, over a green square on the floor. Rebuilt each run, follows the spot.
        /// </summary>
        private static GameObject BuildSpotMarker(Transform spot, Transform chair, Vector2 size, float floorY,
            Material floorMat, Material ghostMat)
        {
            Transform old = spot.Find("Marker");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var marker = new GameObject("Marker");
            Undo.RegisterCreatedObjectUndo(marker, "Create Marker");
            marker.transform.SetParent(spot, false);

            GameObject floor = Primitive(PrimitiveType.Quad, "Floor", marker.transform, Vector3.zero, Vector3.one, floorMat);
            float yaw = Quaternion.LookRotation(FlatDir(spot.forward), Vector3.up).eulerAngles.y;
            floor.transform.SetPositionAndRotation(new Vector3(spot.position.x, floorY, spot.position.z), Quaternion.Euler(90f, yaw, 0f));
            float s = Mathf.Max(Mathf.Abs(spot.lossyScale.x), 1e-4f);
            floor.transform.localScale = new Vector3(size.x, size.y, 1f) / s;
            floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            MeshFilter source = chair.GetComponent<MeshFilter>();
            if (source != null && source.sharedMesh != null)
            {
                var ghost = new GameObject("GhostChair");
                ghost.transform.SetParent(marker.transform, false);
                ghost.transform.SetPositionAndRotation(spot.position, spot.rotation);
                ghost.transform.localScale = chair.lossyScale / s;
                ghost.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var mr = ghost.AddComponent<MeshRenderer>();
                var mats = new Material[source.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return marker;
        }

        private static Material GetOrCreateTransparentUnlit(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return GetOrCreateMaterial(path, "Universal Render Pipeline/Lit", color);
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
