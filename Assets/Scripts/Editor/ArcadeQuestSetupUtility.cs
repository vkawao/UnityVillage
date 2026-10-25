using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static VRVillage.Editor.QuestBuildKit;

namespace VRVillage.Editor
{
    /// <summary>
    /// Builds the Arcade quest in the Village scene:
    ///   ArcadeQuest (QuestManager, QuestHideAtStart)
    ///     QuestPanel, Anchors/, Steps/, FX/, Guide/ (as in the other houses)
    ///     HipHolster   hip pocket for the arcade card between games
    ///     Items/       the arcade card (a recoloured copy of Sakura Park's credit card)
    ///     Counter/     register screen ("Get card"), the card's pose in the grey slot
    ///     Machines/    game screens (score) and card readers
    /// The arcade model (Arcade.fbx) stays a linked prefab instance, so Blender re-exports keep
    /// flowing in; components are added onto its objects (door, neon, game buttons).
    /// Safe to re-run: anchors, screens, readers and the card pose you've moved keep their placement.
    /// </summary>
    public static class ArcadeQuestSetupUtility
    {
        private const string RootName = "ArcadeQuest";
        private const string ModelName = "Arcade";
        private const string ParkName = "VendingMachinePark";

        private static readonly string[] s_ShortTexts =
        {
            "Open the arcade door",
            "Get your card at the register behind the counter",
            "Tap your card on the rhythm game, then hit the glowing buttons",
            "Play the arcade cabinet",
            "Shoot some hoops",
            "Whack the moles",
            "Score a goal at air hockey",
            "Win a plush at the claw machine",
            "Bring your plush to the counter",
            "Return your card at the register",
        };

        private static readonly Color s_ScreenDark = new Color(0.04f, 0.04f, 0.12f, 0.97f);
        private static readonly Color s_CardPink = new Color(1f, 0.31f, 0.85f);
        private static readonly Color s_NeonOff = new Color(0.12f, 0.12f, 0.15f);
        private static readonly Color s_ButtonGlow = new Color(1f, 0.97f, 0.6f, 0.85f);

        [MenuItem("VR Village/Setup Arcade Quest")]
        public static void Setup()
        {
            if (EditorSceneManager.GetActiveScene().name != "Village")
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/Village.unity", OpenSceneMode.Single);
            }

            GameObject model = FindInScene(ModelName);
            if (model == null)
            {
                Debug.LogError($"[Arcade Setup] '{ModelName}' not found in the scene. Drag Arcade.fbx into the scene first.");
                return;
            }

            // Screens, buttons, slots and readers are located from mesh data, so the model must be readable.
            if (EnsureReadable(Need<Renderer>(model.transform, "Building")))
                model = FindInScene(ModelName);

            Transform m = model.transform;
            Renderer building = Need<Renderer>(m, "Building");
            Renderer door = Need<Renderer>(m, "ArcadeDoor");
            Renderer counter = Need<Renderer>(m, "PrizeCounter");
            Renderer maimai = Need<Renderer>(m, "MaimaiMachine");
            Renderer cabinet = Need<Renderer>(m, "ArcadeCabinet");
            Renderer joystick = Need<Renderer>(m, "Joystick");
            Renderer hoops = Need<Renderer>(m, "BasketballGame");
            List<Renderer> balls = NeedNumbered(m, "Basketball");
            Renderer whack = Need<Renderer>(m, "WhackAMole");
            Renderer hammer = Need<Renderer>(m, "Hammer");
            List<Renderer> moles = NeedNumbered(m, "Mole");
            Renderer table = Need<Renderer>(m, "AirHockeyTable");
            Renderer puck = Need<Renderer>(m, "Puck");
            Renderer pusher = Need<Renderer>(m, "Pusher");
            Renderer farPusher = Need<Renderer>(m, "Pusher.001");
            Renderer clawMachine = Need<Renderer>(m, "ClawMachine");
            Renderer claw = Need<Renderer>(m, "Claw");
            List<Renderer> plushes = NeedNumbered(m, "Plush");
            if (building == null || door == null || counter == null || maimai == null || cabinet == null || joystick == null ||
                hoops == null || balls.Count == 0 || whack == null || hammer == null || moles.Count == 0 ||
                table == null || puck == null || pusher == null || farPusher == null || clawMachine == null || claw == null || plushes.Count == 0)
            {
                Debug.LogError("[Arcade Setup] Some arcade objects are missing (see the errors above). Nothing was changed.");
                return;
            }

            EnsureEventSystem();

            // ---------- Colliders on scenery (so things can be set down and land) ----------
            foreach (string n in new[] { "Building", "PrizeCounter", "MaimaiMachine", "ArcadeCabinet", "ArcadeCabinetRed", "ClawMachine",
                         "BasketballGame", "WhackAMole" })
            {
                Transform t = FindDeepChild(m, n);
                if (t != null) EnsureStaticCollider(t.gameObject);
            }

            // ---------- Hierarchy ----------
            GameObject root = FindOrCreate(null, RootName);
            Transform anchors = FindOrCreate(root.transform, "Anchors").transform;
            Transform steps = FindOrCreate(root.transform, "Steps").transform;
            Transform fx = FindOrCreate(root.transform, "FX").transform;
            Transform items = FindOrCreate(root.transform, "Items").transform;
            Transform counterRoot = FindOrCreate(root.transform, "Counter").transform;
            Transform machines = FindOrCreate(root.transform, "Machines").transform;

            QuestManager manager = GetOrAdd<QuestManager>(root);
            QuestPanel panel = BuildPanel(root.transform);
            panel.transform.localScale = Vector3.one * 0.00075f; // ~1.2 m wide

            // ---------- Geometry ----------
            var frame = new BuildingFrame(m);
            if (!MaterialBounds(building, "Carpet", frame, out Bounds floorL))
            {
                Debug.LogWarning("[Arcade Setup] No floor (material 'Carpet') on Building; using the whole building.");
                floorL = frame.BoundsOf(building.transform);
            }
            Vector3 floorCenterW = frame.ToWorld(floorL.center);
            Bounds doorB = door.bounds;
            Vector3 outward = frame.Snap(doorB.center - floorCenterW);                 // from the arcade out to the street
            Vector3 vendorSide = frame.Snap(counter.bounds.center - floorCenterW);    // from the counter toward the wall behind it
            Vector3 maimaiFront = frame.Snap(floorCenterW - maimai.bounds.center);    // from the back wall into the room

            // ---------- 1. Door + neon ----------
            DoorMover doorMover = SetupDoor(door, outward);
            NeonFlicker neon = GetOrAdd<NeonFlicker>(building.gameObject);
            var soNeon = new SerializedObject(neon);
            soNeon.FindProperty("m_Renderer").objectReferenceValue = building;
            var neonSlots = new List<int>();
            Material[] buildingMats = building.sharedMaterials;
            for (int i = 0; i < buildingMats.Length; i++)
            {
                Material mat = buildingMats[i];
                if (mat != null && (MaterialNameIs(mat, "NeonPink") || MaterialNameIs(mat, "NeonCyan") || MaterialNameIs(mat, "NeonGreen")))
                    neonSlots.Add(i);
            }
            SerializedProperty slotsProp = soNeon.FindProperty("m_Slots");
            slotsProp.arraySize = neonSlots.Count;
            for (int i = 0; i < neonSlots.Count; i++) slotsProp.GetArrayElementAtIndex(i).intValue = neonSlots[i];
            soNeon.FindProperty("m_OffColor").colorValue = s_NeonOff;
            soNeon.FindProperty("m_Door").objectReferenceValue = doorMover;
            soNeon.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 2. Register screen, card slot, card ----------
            if (!PlaneOf(Triangles(counter, "Screen"), vendorSide, out Vector3 regCenter, out Vector3 regNormal, out Vector2 regSize))
            {
                Debug.LogError("[Arcade Setup] Couldn't find the register screen (material 'Screen') on PrizeCounter, facing the staff side.");
                return;
            }
            Transform regAnchor = FindOrCreateAnchor(counterRoot, "RegisterScreenAnchor", regCenter + regNormal * 0.004f,
                Quaternion.LookRotation(-regNormal, Vector3.up));
            RegisterScreen register = BuildRegisterScreen(regAnchor, regSize);

            if (!CardSlot(counter, frame, out Vector3 slotTop, out Vector3 slotAlong, out Bounds strip))
            {
                Debug.LogError("[Arcade Setup] Couldn't find the grey card slot (material 'Steel') on PrizeCounter.");
                return;
            }
            GameObject parkGo = FindInScene(ParkName);
            Transform parkCard = parkGo != null ? FindDeepChild(parkGo.transform, "CreditCard") : null;
            if (parkCard == null) Debug.LogWarning("[Arcade Setup] Sakura Park's CreditCard not found; using a simple stand-in card.");
            GameObject card = BuildCard(items, parkCard, slotTop);
            // Lies flat and slides out of the strip's staff-side face, toward the person behind the counter.
            Vector3 cardTravel = FlatDir(vendorSide);
            Quaternion cardRot = FlatRotation(card.transform, cardTravel, out float cardDepth);
            float stripEdge = Mathf.Abs(Vector3.Dot(strip.extents, cardTravel));
            Vector3 cardOutPos = strip.center + cardTravel * (stripEdge + cardDepth * 0.5f - 0.015f); // its back edge still in the slot
            Transform oldPose = counterRoot.Find("CardOut"); // the earlier upright pose
            if (oldPose != null) Undo.DestroyObjectImmediate(oldPose.gameObject);
            Transform cardOut = FindOrCreatePose(counterRoot, "CardOutFlat", cardOutPos, cardRot);
            card.transform.SetPositionAndRotation(cardOut.position, cardOut.rotation);
            XRGrabInteractable cardGrab = Grabbable(card, 0.02f);
            GetOrAdd<FreeAfterGrab>(card);

            var hide = GetOrAdd<QuestHideAtStart>(root);
            var soHide = new SerializedObject(hide);
            SetArray(soHide.FindProperty("m_Hide"), new Object[] { card });
            soHide.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 3. Rhythm game (MaimaiMachine) ----------
            RhythmParts rhythm = BuildRhythmParts(maimai, maimaiFront);
            if (rhythm.Buttons == null) return;
            Transform maimaiScreenAnchor = FindOrCreateAnchor(machines, "MaimaiScreenAnchor",
                rhythm.ScreenCenter + rhythm.Normal * 0.004f, Quaternion.LookRotation(-rhythm.Normal, Vector3.up));
            TMP_Text maimaiScreen = BuildScoreScreen(maimaiScreenAnchor, "MaimaiScreen", rhythm.ScreenSize);
            ItemTrigger maimaiReader = ReaderTrigger(machines, "MaimaiReader", frame, maimai, maimaiFront);
            if (maimaiReader == null) return;

            // ---------- 4. Arcade cabinet (joystick game on its screen) ----------
            Vector3 cabinetFront = ReaderFront(cabinet, frame);
            if (!PlaneOf(Triangles(cabinet, "Screen"), cabinetFront, out Vector3 cabScreenC, out Vector3 cabScreenN, out Vector2 cabScreenSize))
            {
                Debug.LogError("[Arcade Setup] Couldn't find the screen (material 'Screen') on ArcadeCabinet's front.");
                return;
            }
            Transform cabScreenAnchor = FindOrCreateAnchor(machines, "CabinetScreenAnchor",
                cabScreenC + cabScreenN * 0.004f, Quaternion.LookRotation(-cabScreenN, Vector3.up));
            CabinetScreen cabScreen = BuildCabinetScreen(cabScreenAnchor, cabScreenSize);
            ItemTrigger cabinetReader = ReaderTrigger(machines, "CabinetReader", frame, cabinet, cabinetFront);
            if (cabinetReader == null) return;
            XRSimpleInteractable stickTouch = JoystickGrip(joystick);
            JoystickLever lever = GetOrAdd<JoystickLever>(joystick.gameObject);
            var soLever = new SerializedObject(lever);
            soLever.FindProperty("m_Interactable").objectReferenceValue = stickTouch;
            soLever.FindProperty("m_Front").vector3Value = cabinetFront;
            soLever.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 5. Hoops (BasketballGame) ----------
            Vector3 hoopsFront = ReaderFront(hoops, frame);
            List<Tri> rim = Triangles(hoops, "Orange");
            if (rim.Count == 0)
            {
                Debug.LogError("[Arcade Setup] Couldn't find the hoop's rim (material 'Orange') on BasketballGame.");
                return;
            }
            Bounds rimB = TriBounds(rim);
            Transform hoopsRoot = FindOrCreate(machines, "Hoops").transform;
            Transform hoop = FindOrCreatePose(hoopsRoot, "Hoop", new Vector3(rimB.center.x, rimB.max.y, rimB.center.z), Quaternion.identity);
            float hoopRadius = Mathf.Min(rimB.size.x, rimB.size.z) * 0.5f * 0.85f;
            if (!PlaneOf(Triangles(hoops, "Screen"), hoopsFront, out Vector3 hoopScreenC, out Vector3 hoopScreenN, out Vector2 hoopScreenSize))
            {
                Debug.LogError("[Arcade Setup] Couldn't find the score screen (material 'Screen') on BasketballGame.");
                return;
            }
            Transform hoopScreenAnchor = FindOrCreateAnchor(machines, "HoopsScreenAnchor",
                hoopScreenC + hoopScreenN * 0.004f, Quaternion.LookRotation(-hoopScreenN, Vector3.up));
            TMP_Text hoopScreen = BuildScoreScreen(hoopScreenAnchor, "HoopsScreen", hoopScreenSize);
            ItemTrigger hoopsReader = ReaderTrigger(machines, "HoopsReader", frame, hoops, hoopsFront);
            if (hoopsReader == null) return;
            var ballGrabs = new List<XRGrabInteractable>();
            var ballSpots = new List<Transform>();
            for (int i = 0; i < balls.Count; i++)
            {
                ballGrabs.Add(SetupBall(balls[i].gameObject));
                ballSpots.Add(FindOrCreatePose(hoopsRoot, "BallSpot_" + (i + 1), balls[i].transform.position, balls[i].transform.rotation));
            }

            // ---------- 6. Whack-a-mole ----------
            Vector3 whackFront = ReaderFront(whack, frame);
            if (!PlaneOf(Triangles(whack, "Screen"), whackFront, out Vector3 whackScreenC, out Vector3 whackScreenN, out Vector2 whackScreenSize))
            {
                Debug.LogError("[Arcade Setup] Couldn't find the screen (material 'Screen') on WhackAMole.");
                return;
            }
            Transform whackScreenAnchor = FindOrCreateAnchor(machines, "WhackScreenAnchor",
                whackScreenC + whackScreenN * 0.004f, Quaternion.LookRotation(-whackScreenN, Vector3.up));
            TMP_Text whackScreen = BuildScoreScreen(whackScreenAnchor, "WhackScreen", whackScreenSize);
            ItemTrigger whackReader = ReaderTrigger(machines, "WhackReader", frame, whack, whackFront);
            if (whackReader == null) return;
            XRGrabInteractable hammerGrab = SetupGrabbable(hammer.gameObject, 0.5f);
            List<Tri> hammerHead = Triangles(hammer, "Red");
            Transform hammerParts = RebuildParts(hammer.transform);
            Transform head = NewPart("Head", hammerParts, hammerHead.Count > 0 ? TriBounds(hammerHead).center : hammer.bounds.center,
                hammer.transform.rotation);
            KeepPlacement(head);
            // Moles hide at the lowest modelled height and pop up to the highest (they're modelled both ways).
            float moleDown = float.MaxValue, moleUp = float.MinValue;
            var moleTs = new List<Transform>();
            foreach (Renderer r in moles)
            {
                moleTs.Add(r.transform);
                moleDown = Mathf.Min(moleDown, r.transform.position.y);
                moleUp = Mathf.Max(moleUp, r.transform.position.y);
            }
            if (moleUp - moleDown < 0.05f) moleUp = moleDown + 0.17f;

            // ---------- 7. Air hockey ----------
            Vector3 tableFront = ReaderFront(table, frame);
            List<Tri> surface = Triangles(table, "White");
            if (surface.Count == 0)
            {
                Debug.LogError("[Arcade Setup] Couldn't find the playing surface (material 'White') on AirHockeyTable.");
                return;
            }
            Transform hockeyRoot = FindOrCreate(machines, "AirHockey").transform;
            HockeyTable hockey = BuildHockeyColliders(table, surface, tableFront);
            Transform goal = FindOrCreatePose(hockeyRoot, "Goal", hockey.GoalMouth, Quaternion.LookRotation(-tableFront, Vector3.up));
            Transform puckStart = FindOrCreatePose(hockeyRoot, "PuckStart", puck.transform.position, puck.transform.rotation);
            Rigidbody puckBody = SetupPuck(puck.gameObject, hockey.SurfaceY);
            XRGrabInteractable pusherGrab = SetupGrabbable(pusher.gameObject, 0.2f);
            pusherGrab.throwOnDetach = false; // it's slid, not thrown
            EditorUtility.SetDirty(pusherGrab);
            Rigidbody pusherBody = pusher.GetComponent<Rigidbody>();
            pusherBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            EnsureStaticCollider(farPusher.gameObject); // the far pusher just stands there
            // No scoreboard: the table has no screen (an earlier run built a floating one).
            Transform oldBoard = machines.Find("HockeyScreenAnchor");
            if (oldBoard != null) Undo.DestroyObjectImmediate(oldBoard.gameObject);
            ItemTrigger hockeyReader = ReaderTrigger(machines, "HockeyReader", frame, table, tableFront);
            if (hockeyReader == null) return;

            // ---------- 8. Claw machine ----------
            Vector3 clawFront = ReaderFront(clawMachine, frame);
            ClawParts clawParts = BuildClawParts(machines, clawMachine, clawFront);
            if (clawParts.Area == null) return;
            ItemTrigger clawReader = ReaderTrigger(machines, "ClawReader", frame, clawMachine, clawFront);
            if (clawReader == null) return;
            var plushGrabs = new List<XRGrabInteractable>();
            foreach (Renderer p in plushes)
            {
                XRGrabInteractable g = SetupGrabbable(p.gameObject, 0.15f);
                GetOrAdd<FreeAfterGrab>(p.gameObject);
                plushGrabs.Add(g);
            }

            // ---------- 9-10. Counter: plush zone, card slot ----------
            Transform plushZoneT = counterRoot.Find("PlushZone");
            ItemTrigger plushZone = plushZoneT != null && plushZoneT.GetComponent<ItemTrigger>() != null
                ? plushZoneT.GetComponent<ItemTrigger>()
                : BuildPlushZone(counterRoot, counter, regCenter, vendorSide);
            var soZone = new SerializedObject(plushZone);
            soZone.FindProperty("m_LogNearMisses").boolValue = false;
            soZone.ApplyModifiedPropertiesWithoutUndo();
            Transform plushHere = FindOrCreateAnchor(counterRoot, "PlushHereAnchor",
                plushZone.transform.position + Vector3.up * 0.45f, Quaternion.LookRotation(vendorSide, Vector3.up));
            plushHere.localScale = Vector3.one * 0.7f;
            GameObject plushMarker = BuildHereMarker(plushHere);
            // Card return: at the strip's staff-side face, where the card comes out (the card goes back in flat).
            Transform oldSlot = counterRoot.Find("CardSlotTrigger"); // the earlier zone over the strip's top
            if (oldSlot != null) Undo.DestroyObjectImmediate(oldSlot.gameObject);
            ItemTrigger cardSlot = KeepOrBuildTrigger(counterRoot, "CardReturnZone", frame,
                frame.ToLocal(strip.center + cardTravel * stripEdge + Vector3.up * 0.01f), new Vector3(0.3f, 0.1f, 0.12f), vendorSide); // along the slot, up, out

            // ---------- Guide + hip holster ----------
            BuildGuide(root.transform, frame, floorL, GuideMarginAvoidingOthers(floorL, frame, root.transform));
            QuestGuide guide = root.transform.Find("Guide").GetComponent<QuestGuide>();
            HipHolster holster = BuildHolster(root.transform, guide, new[] { cardGrab }, true);

            // ---------- Panel anchors ----------
            Transform a1 = FindOrCreateAnchor(anchors, "QuestAnchor_1_Door",
                Flat(doorB.center) + outward * 0.35f + Vector3.up * (doorB.max.y + 0.35f), Quaternion.LookRotation(-outward, Vector3.up));
            Bounds cB = counter.bounds;
            // Over the register, read from the room (where you come from) before you walk behind the counter.
            Transform a2 = FindOrCreateAnchor(anchors, "QuestAnchor_2_GetCard",
                new Vector3(regCenter.x, cB.max.y + 0.85f, regCenter.z) - vendorSide * 0.15f, Quaternion.LookRotation(vendorSide, Vector3.up));
            Bounds mB = maimai.bounds;
            Transform a3 = FindOrCreateAnchor(anchors, "QuestAnchor_3_Rhythm",
                Flat(mB.center) + maimaiFront * 0.45f + Vector3.up * (mB.max.y + 0.35f), Quaternion.LookRotation(-maimaiFront, Vector3.up));

            Bounds cabB = cabinet.bounds;
            Transform a4 = FindOrCreateAnchor(anchors, "QuestAnchor_4_Cabinet",
                Flat(cabB.center) + cabinetFront * 0.5f + Vector3.up * (cabB.max.y + 0.35f), Quaternion.LookRotation(-cabinetFront, Vector3.up));
            Bounds hB = hoops.bounds;
            Transform a5 = FindOrCreateAnchor(anchors, "QuestAnchor_5_Hoops",
                Flat(hoopsReader.transform.position) + hoopsFront * 0.6f + Vector3.up * (hB.min.y + 2.35f), Quaternion.LookRotation(-hoopsFront, Vector3.up));
            Bounds wB = whack.bounds;
            Transform a6 = FindOrCreateAnchor(anchors, "QuestAnchor_6_Whack",
                Flat(wB.center) + whackFront * 0.7f + Vector3.up * (wB.max.y + 0.35f), Quaternion.LookRotation(-whackFront, Vector3.up));

            Bounds tB = table.bounds;
            Transform a7 = FindOrCreateAnchor(anchors, "QuestAnchor_7_AirHockey",
                Flat(tB.center) + tableFront * (Extent(frame.BoundsOf(table.transform), frame.LocalDir(tableFront)) + 0.3f) + Vector3.up * (tB.max.y + 1.0f),
                Quaternion.LookRotation(-tableFront, Vector3.up));
            Bounds clB = clawMachine.bounds;
            Transform a8 = FindOrCreateAnchor(anchors, "QuestAnchor_8_Claw",
                Flat(clB.center) + clawFront * 0.5f + Vector3.up * (clB.max.y + 0.35f), Quaternion.LookRotation(-clawFront, Vector3.up));
            Transform a9 = FindOrCreateAnchor(anchors, "QuestAnchor_9_PlushToCounter",
                Flat(plushZone.transform.position) - vendorSide * 0.2f + Vector3.up * (cB.max.y + 0.95f), Quaternion.LookRotation(vendorSide, Vector3.up));
            Transform a10 = FindOrCreateAnchor(anchors, "QuestAnchor_10_ReturnCard",
                new Vector3(regCenter.x, cB.max.y + 0.85f, regCenter.z) - vendorSide * 0.15f, Quaternion.LookRotation(vendorSide, Vector3.up));

            QuestCelebration hockeyFx = BuildCelebration(fx, "Celebration_AirHockey", hockey.GoalMouth + Vector3.up * 0.3f);
            QuestCelebration clawFx = BuildCelebration(fx, "Celebration_Claw", clawParts.PrizeOut.position + clawFront * 0.2f + Vector3.up * 0.4f);
            QuestCelebration plushFx = BuildCelebration(fx, "Celebration_Plush", plushZone.transform.position + Vector3.up * 0.25f);
            QuestCelebration finale = BuildCelebration(fx, "Celebration_Finale", slotTop + Vector3.up * 0.6f, CelebrationStyle.ConfettiStyle());

            QuestCelebration rhythmFx = BuildCelebration(fx, "Celebration_Rhythm", rhythm.ScreenCenter + rhythm.Normal * 0.25f);
            QuestCelebration cabinetFx = BuildCelebration(fx, "Celebration_Cabinet", cabScreenC + cabScreenN * 0.3f);
            QuestCelebration hoopsFx = BuildCelebration(fx, "Celebration_Hoops", hoop.position + hoopsFront * 0.3f);
            QuestCelebration whackFx = BuildCelebration(fx, "Celebration_Whack", wB.center + Vector3.up * (wB.extents.y + 0.2f));

            // ---------- Steps ----------
            DoorOpenStep step1 = GetOrAdd<DoorOpenStep>(FindOrCreate(steps, "Step1_OpenDoor"));
            var so1 = StepSO(step1, "Welcome to the arcade! Open the door.", a1);
            so1.FindProperty("m_Door").objectReferenceValue = doorMover;
            so1.ApplyModifiedPropertiesWithoutUndo();

            GetCardStep step2 = GetOrAdd<GetCardStep>(FindOrCreate(steps, "Step2_GetCard"));
            var so2 = StepSO(step2, "Get your arcade card at the register.", a2);
            so2.FindProperty("m_Button").objectReferenceValue = register.Button;
            so2.FindProperty("m_Screen").objectReferenceValue = register.Text;
            so2.FindProperty("m_Card").objectReferenceValue = cardGrab;
            so2.FindProperty("m_CardOut").objectReferenceValue = cardOut;
            so2.FindProperty("m_SlideDistance").floatValue = cardDepth * 0.85f;
            so2.FindProperty("m_SlideDirection").vector3Value = cardTravel;
            so2.ApplyModifiedPropertiesWithoutUndo();

            RhythmGameStep step3 = GetOrAdd<RhythmGameStep>(FindOrCreate(steps, "Step3_RhythmGame"));
            var so3 = StepSO(step3, "Play the rhythm game!", a3, rhythmFx);
            SetGameFields(so3, holster, cardGrab, maimaiReader, "at the lower right of the front", "RHYTHM", maimaiScreen, maimai.transform);
            SetArray(so3.FindProperty("m_Buttons"), rhythm.Buttons);
            SetArray(so3.FindProperty("m_Glows"), rhythm.Glows);
            so3.FindProperty("m_Sparkle").objectReferenceValue = rhythmFx;
            so3.ApplyModifiedPropertiesWithoutUndo();

            CabinetGameStep step4 = GetOrAdd<CabinetGameStep>(FindOrCreate(steps, "Step4_Cabinet"));
            var so4 = StepSO(step4, "Play the blue arcade cabinet!", a4, cabinetFx);
            SetGameFields(so4, holster, cardGrab, cabinetReader, "on the white panel below the joystick", "STAR CATCHER", cabScreen.Text, cabinet.transform);
            so4.FindProperty("m_Joystick").objectReferenceValue = lever;
            so4.FindProperty("m_PlayArea").objectReferenceValue = cabScreen.PlayArea;
            so4.FindProperty("m_Ship").objectReferenceValue = cabScreen.Ship;
            so4.FindProperty("m_Star").objectReferenceValue = cabScreen.Star;
            so4.ApplyModifiedPropertiesWithoutUndo();

            HoopsGameStep step5 = GetOrAdd<HoopsGameStep>(FindOrCreate(steps, "Step5_Hoops"));
            var so5 = StepSO(step5, "Shoot some hoops!", a5, hoopsFx);
            SetGameFields(so5, holster, cardGrab, hoopsReader, "on the front, under the ball ramp", "HOOPS", hoopScreen, hoops.transform);
            SetArray(so5.FindProperty("m_Balls"), ballGrabs);
            SetArray(so5.FindProperty("m_Spots"), ballSpots);
            so5.FindProperty("m_Hoop").objectReferenceValue = hoop;
            so5.FindProperty("m_HoopRadius").floatValue = hoopRadius;
            so5.ApplyModifiedPropertiesWithoutUndo();

            WhackGameStep step6 = GetOrAdd<WhackGameStep>(FindOrCreate(steps, "Step6_WhackAMole"));
            var so6 = StepSO(step6, "Whack-a-mole time!", a6, whackFx);
            SetGameFields(so6, holster, cardGrab, whackReader, "on the front panel", "WHACK-A-MOLE", whackScreen, whack.transform);
            so6.FindProperty("m_Hammer").objectReferenceValue = hammerGrab;
            so6.FindProperty("m_HammerHead").objectReferenceValue = head;
            SetArray(so6.FindProperty("m_Moles"), moleTs);
            so6.FindProperty("m_DownY").floatValue = moleDown;
            so6.FindProperty("m_UpY").floatValue = moleUp;
            so6.FindProperty("m_Sparkle").objectReferenceValue = whackFx;
            so6.ApplyModifiedPropertiesWithoutUndo();

            AirHockeyStep step7 = GetOrAdd<AirHockeyStep>(FindOrCreate(steps, "Step7_AirHockey"));
            var so7 = StepSO(step7, "Air hockey: score a goal!", a7, hockeyFx);
            SetGameFields(so7, holster, cardGrab, hockeyReader, "on the end of the table, by you", "AIR HOCKEY", null, table.transform);
            so7.FindProperty("m_Puck").objectReferenceValue = puckBody;
            so7.FindProperty("m_PuckStart").objectReferenceValue = puckStart;
            so7.FindProperty("m_Pusher").objectReferenceValue = pusherGrab;
            so7.FindProperty("m_Goal").objectReferenceValue = goal;
            so7.FindProperty("m_GoalHalfWidth").floatValue = hockey.GoalHalfWidth;
            so7.ApplyModifiedPropertiesWithoutUndo();

            ClawGameStep step8 = GetOrAdd<ClawGameStep>(FindOrCreate(steps, "Step8_Claw"));
            var so8 = StepSO(step8, "Win a plush at the claw machine!", a8, clawFx);
            SetGameFields(so8, holster, cardGrab, clawReader, "on the pink front, left of the black flap", "CLAW", clawParts.Text, clawMachine.transform);
            so8.FindProperty("m_Claw").objectReferenceValue = claw.transform;
            so8.FindProperty("m_ClawLength").floatValue = claw.transform.position.y - claw.bounds.min.y;
            SetArray(so8.FindProperty("m_Plushes"), plushGrabs);
            so8.FindProperty("m_Left").objectReferenceValue = clawParts.Left;
            so8.FindProperty("m_Right").objectReferenceValue = clawParts.Right;
            so8.FindProperty("m_Back").objectReferenceValue = clawParts.Back;
            so8.FindProperty("m_Front").objectReferenceValue = clawParts.Front;
            so8.FindProperty("m_Drop").objectReferenceValue = clawParts.Drop;
            so8.FindProperty("m_DropButton").objectReferenceValue = clawParts.RedButton;
            so8.FindProperty("m_Area").objectReferenceValue = clawParts.Area;
            so8.FindProperty("m_BackDir").vector3Value = -clawFront;
            so8.FindProperty("m_HalfWidth").floatValue = clawParts.HalfWidth;
            so8.FindProperty("m_HalfDepth").floatValue = clawParts.HalfDepth;
            so8.FindProperty("m_Chute").objectReferenceValue = clawParts.Chute;
            so8.FindProperty("m_PrizeOut").objectReferenceValue = clawParts.PrizeOut;
            so8.FindProperty("m_SpitDirection").vector3Value = clawFront;
            so8.ApplyModifiedPropertiesWithoutUndo();

            PlushToCounterStep step9 = GetOrAdd<PlushToCounterStep>(FindOrCreate(steps, "Step9_PlushToCounter"));
            var so9 = StepSO(step9, "Show off your prize at the counter!", a9, plushFx, new[] { plushMarker });
            so9.FindProperty("m_Claw").objectReferenceValue = step8;
            so9.FindProperty("m_Zone").objectReferenceValue = plushZone;
            so9.ApplyModifiedPropertiesWithoutUndo();

            ReturnCardStep step10 = GetOrAdd<ReturnCardStep>(FindOrCreate(steps, "Step10_ReturnCard"));
            var so10 = StepSO(step10, "All done! Return your arcade card.", a10, finale);
            so10.FindProperty("m_Holster").objectReferenceValue = holster;
            so10.FindProperty("m_Card").objectReferenceValue = cardGrab;
            so10.FindProperty("m_Slot").objectReferenceValue = cardSlot;
            so10.FindProperty("m_CardOut").objectReferenceValue = cardOut;
            so10.FindProperty("m_SlideDistance").floatValue = cardDepth * 0.85f;
            so10.FindProperty("m_SlideDirection").vector3Value = cardTravel;
            so10.FindProperty("m_Screen").objectReferenceValue = register.Text;
            so10.ApplyModifiedPropertiesWithoutUndo();

            var allSteps = new List<Object> { step1, step2, step3, step4, step5, step6, step7, step8, step9, step10 };
            SetGuideTexts(allSteps, s_ShortTexts, new Dictionary<Object, Transform>
            {
                { step1, door.transform },
            });

            var soM = new SerializedObject(manager);
            soM.FindProperty("m_Panel").objectReferenceValue = panel;
            SetArray(soM.FindProperty("m_Steps"), allSteps);
            soM.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log($"<color=green>[Arcade Setup] Arcade quest ready: actions 1-{allSteps.Count}.</color>");
        }

        private static void SetGameFields(SerializedObject so, HipHolster holster, XRGrabInteractable card, ItemTrigger reader,
            string readerWhere, string title, TMP_Text screen, Transform machine)
        {
            so.FindProperty("m_Holster").objectReferenceValue = holster;
            so.FindProperty("m_Card").objectReferenceValue = card;
            so.FindProperty("m_Reader").objectReferenceValue = reader;
            so.FindProperty("m_ReaderWhere").stringValue = readerWhere;
            so.FindProperty("m_GameTitle").stringValue = title;
            so.FindProperty("m_Screen").objectReferenceValue = screen;
            so.FindProperty("m_Machine").objectReferenceValue = machine;
        }

        // ------------------------------------------------------------------
        // Finding things
        // ------------------------------------------------------------------
        private static T Need<T>(Transform root, string name) where T : Component
        {
            Transform t = FindDeepChild(root, name);
            T comp = t != null ? t.GetComponent<T>() : null;
            if (comp == null) Debug.LogError($"[Arcade Setup] '{name}' not found under {root.name}.");
            return comp;
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
            if (list.Count == 0) Debug.LogError($"[Arcade Setup] No '{baseName}' objects found under {root.name}.");
            return list;
        }

        /// <summary>
        /// The side of a game machine the player stands at: the side its card reader is on (the reader
        /// sits on the front). Of the four walls, the one the reader is closest to, relative to the
        /// machine's size (a long machine's centre alone would point the wrong way).
        /// </summary>
        private static Vector3 ReaderFront(Renderer machine, BuildingFrame frame)
        {
            Bounds mb = frame.BoundsOf(machine.transform);
            if (!MaterialBounds(machine, "CardReader", frame, out Bounds rb)) return frame.Snap(-machine.transform.forward);
            Vector3 d = rb.center - mb.center;
            float x = d.x / Mathf.Max(mb.extents.x, 1e-3f), z = d.z / Mathf.Max(mb.extents.z, 1e-3f);
            Vector3 local = Mathf.Abs(x) >= Mathf.Abs(z) ? new Vector3(Mathf.Sign(x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(z));
            return frame.Rot * local;
        }

        private static Transform NewPart(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go.transform;
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
        // Mesh faces by material
        // ------------------------------------------------------------------
        private struct Tri
        {
            public Vector3 A, B, C;
            public Vector3 Center => (A + B + C) / 3f;
            public Vector3 Cross => Vector3.Cross(B - A, C - A);
            public float Area => Cross.magnitude * 0.5f;
            public Vector3 Normal => Cross.normalized;
        }

        /// <summary>World-space triangles of one material on a renderer (empty if it doesn't have it).</summary>
        private static List<Tri> Triangles(Renderer r, string material)
        {
            var result = new List<Tri>();
            MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return result;
            Mesh mesh = mf.sharedMesh;
            Material[] mats = r.sharedMaterials;
            Vector3[] v = mesh.vertices;
            for (int sub = 0; sub < mats.Length && sub < mesh.subMeshCount; sub++)
            {
                if (mats[sub] == null || !MaterialNameIs(mats[sub], material)) continue;
                int[] tris = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var t = new Tri
                    {
                        A = r.transform.TransformPoint(v[tris[i]]),
                        B = r.transform.TransformPoint(v[tris[i + 1]]),
                        C = r.transform.TransformPoint(v[tris[i + 2]]),
                    };
                    if (t.Cross.sqrMagnitude > 1e-14f) result.Add(t);
                }
            }
            return result;
        }

        private static Bounds TriBounds(List<Tri> tris)
        {
            var b = new Bounds(tris[0].A, Vector3.zero);
            foreach (Tri t in tris)
            {
                b.Encapsulate(t.A);
                b.Encapsulate(t.B);
                b.Encapsulate(t.C);
            }
            return b;
        }

        /// <summary>
        /// The flat face (screen, panel) among <paramref name="tris"/> that faces roughly
        /// <paramref name="towardW"/>: its centre, normal and size (width, height) as a canvas would see it.
        /// </summary>
        private static bool PlaneOf(List<Tri> tris, Vector3 towardW, out Vector3 center, out Vector3 normal, out Vector2 size)
        {
            center = normal = Vector3.zero;
            size = Vector2.zero;
            var facing = new List<Tri>();
            Vector3 sum = Vector3.zero;
            foreach (Tri t in tris)
            {
                if (Vector3.Dot(t.Normal, towardW) < 0.5f) continue;
                facing.Add(t);
                sum += t.Cross; // area-weighted
            }
            if (facing.Count == 0) return false;
            Vector3 n = sum.normalized;
            normal = n;
            // The frontmost faces only (a screen's bezel sides and back don't count).
            float dMax = float.MinValue;
            foreach (Tri t in facing) dMax = Mathf.Max(dMax, Vector3.Dot(t.Center, n));
            facing.RemoveAll(t => Vector3.Dot(t.Center, n) < dMax - 0.01f);
            Rect(facing, normal, out center, out size);
            return true;
        }

        /// <summary>Centre and (width, height) of triangles projected on a canvas facing out along <paramref name="normal"/>.</summary>
        private static void Rect(List<Tri> tris, Vector3 normal, out Vector3 center, out Vector2 size)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -normal).normalized; // a canvas's +X when it looks along -normal
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            Vector3 up = Vector3.Cross(-normal, right).normalized;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, depth = float.MinValue;
            foreach (Tri t in tris)
            {
                foreach (Vector3 p in new[] { t.A, t.B, t.C })
                {
                    float x = Vector3.Dot(p, right), y = Vector3.Dot(p, up);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    depth = Mathf.Max(depth, Vector3.Dot(p, normal));
                }
            }
            center = right * ((minX + maxX) * 0.5f) + up * ((minY + maxY) * 0.5f) + normal * depth;
            size = new Vector2(maxX - minX, maxY - minY);
        }

        /// <summary>Triangles grouped into separate parts (e.g. the 8 buttons): touching or within <paramref name="join"/> m.</summary>
        private static List<List<Tri>> Clusters(List<Tri> tris, float join)
        {
            var groups = new List<List<Tri>>();
            var bounds = new List<Bounds>();
            foreach (Tri t in tris)
            {
                var tb = new Bounds(t.A, Vector3.zero);
                tb.Encapsulate(t.B);
                tb.Encapsulate(t.C);
                int found = -1;
                for (int i = 0; i < groups.Count; i++)
                {
                    Bounds g = bounds[i];
                    g.Expand(join);
                    if (!g.Intersects(tb)) continue;
                    if (found < 0)
                    {
                        found = i;
                        groups[i].Add(t);
                        Bounds b = bounds[i];
                        b.Encapsulate(tb);
                        bounds[i] = b;
                    }
                    else
                    {
                        // This triangle joins two groups: merge them.
                        groups[found].AddRange(groups[i]);
                        Bounds b = bounds[found];
                        b.Encapsulate(bounds[i]);
                        bounds[found] = b;
                        groups.RemoveAt(i);
                        bounds.RemoveAt(i);
                        i--;
                    }
                }
                if (found < 0)
                {
                    groups.Add(new List<Tri> { t });
                    bounds.Add(tb);
                }
            }
            return groups;
        }

        // ------------------------------------------------------------------
        // Door
        // ------------------------------------------------------------------
        /// <summary>
        /// The door swings on its own pivot (its origin is on the hinge edge), outward onto the
        /// street so it never sweeps through the arcade.
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
            so.FindProperty("m_UseBuiltInSounds").boolValue = true; // latch + creak + bell for this arcade only
            so.ApplyModifiedPropertiesWithoutUndo();
            return mover;
        }

        // ------------------------------------------------------------------
        // Register, card slot and card
        // ------------------------------------------------------------------
        private struct RegisterScreen
        {
            public Button Button;
            public TMP_Text Text;
        }

        /// <summary>Over the register's screen: a status line on top and a big "Get card" button under it.</summary>
        private static RegisterScreen BuildRegisterScreen(Transform anchor, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "RegisterScreen", px, true);
            AddRounded(canvas, "Background", s_ScreenDark);

            RectTransform textRt = NewUIChild("Text", canvas);
            textRt.anchorMin = new Vector2(0.05f, 0.55f);
            textRt.anchorMax = new Vector2(0.95f, 0.96f);
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI text = AddText(textRt, "ARCADE", px.y * 0.15f, TextAlignmentOptions.Center);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = px.y * 0.16f;

            RectTransform slot = NewUIChild("GetCard", canvas);
            slot.anchorMin = new Vector2(0.12f, 0.08f);
            slot.anchorMax = new Vector2(0.88f, 0.5f);
            slot.offsetMin = slot.offsetMax = Vector2.zero;
            Button button = AddButton(slot, "Get card", ButtonGreen, px.y * 0.16f);
            return new RegisterScreen { Button = button, Text = text };
        }

        /// <summary>
        /// The grey card slot on the counter: the big flat-topped part of the counter's 'Steel'
        /// (the register's small stand shares the material). Its top centre, long direction and the
        /// whole strip's world bounds.
        /// </summary>
        private static bool CardSlot(Renderer counter, BuildingFrame frame, out Vector3 top, out Vector3 along, out Bounds strip)
        {
            top = along = Vector3.zero;
            strip = default;
            List<Tri> steel = Triangles(counter, "Steel");
            if (steel.Count == 0) return false;
            var flat = steel.FindAll(t => t.Normal.y > 0.9f);
            float biggest = 0f;
            foreach (Tri t in flat) biggest = Mathf.Max(biggest, t.Area);
            flat.RemoveAll(t => t.Area < biggest * 0.5f); // the strip's top, not the stand's little cap
            List<Tri> use = flat.Count > 0 ? flat : steel;

            bool any = false;
            var b = new Bounds();
            foreach (Tri t in use)
            {
                foreach (Vector3 p in new[] { t.A, t.B, t.C })
                {
                    Vector3 l = frame.ToLocal(p);
                    if (!any) { b = new Bounds(l, Vector3.zero); any = true; }
                    else b.Encapsulate(l);
                }
            }
            Vector3 c = frame.ToWorld(b.center);
            top = new Vector3(c.x, frame.ToWorld(new Vector3(b.center.x, b.max.y, b.center.z)).y, c.z);
            along = frame.Rot * (b.size.x >= b.size.z ? Vector3.right : Vector3.forward);

            // The whole strip: every Steel face over its top's footprint (sides included, the stand left out).
            Bounds footprint = b;
            footprint.Expand(new Vector3(0.004f, 10f, 0.004f));
            any = false;
            foreach (Tri t in steel)
            {
                if (!footprint.Contains(frame.ToLocal(t.Center))) continue;
                foreach (Vector3 p in new[] { t.A, t.B, t.C })
                {
                    if (!any) { strip = new Bounds(p, Vector3.zero); any = true; }
                    else strip.Encapsulate(p);
                }
            }
            if (!any) strip = new Bounds(top, new Vector3(0.1f, 0.02f, 0.1f));
            return true;
        }

        /// <summary>
        /// A card lying flat, as it slides out of a slot: its face up, its short side along
        /// <paramref name="travelW"/> (it comes out edge first). Also returns its length along the travel.
        /// </summary>
        private static Quaternion FlatRotation(Transform card, Vector3 travelW, out float depth)
        {
            MeshFilter mf = card.GetComponent<MeshFilter>();
            Vector3 size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
            size = Vector3.Scale(size, card.localScale);
            var axes = new List<int> { 0, 1, 2 };
            axes.Sort((a, b) => Mathf.Abs(size[a]).CompareTo(Mathf.Abs(size[b]))); // thin, middle, long
            Vector3 thin = Vector3.zero, mid = Vector3.zero;
            thin[axes[0]] = 1f;
            mid[axes[1]] = 1f;
            depth = Mathf.Abs(size[axes[1]]);
            Vector3 travel = Flat(travelW).sqrMagnitude > 1e-6f ? FlatDir(travelW) : Vector3.forward;
            // Middle axis -> travel direction, thin axis -> up.
            return Quaternion.LookRotation(travel, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(mid, thin));
        }

        /// <summary>A copy of the park's credit card in the arcade's pink (the card model counts once, in the park).</summary>
        private static GameObject BuildCard(Transform parent, Transform source, Vector3 worldPos)
        {
            Transform existing = parent.Find("ArcadeCard");
            GameObject go = existing != null ? existing.gameObject : FindOrCreate(parent, "ArcadeCard");
            if (existing == null) go.transform.position = worldPos;
            Material pink = GetOrCreateMaterial("Assets/Materials/ArcadeCard.mat", "Universal Render Pipeline/Lit", s_CardPink, 0.6f);
            if (source != null)
            {
                MeshFilter srcFilter = source.GetComponent<MeshFilter>();
                Renderer src = source.GetComponent<Renderer>();
                Mesh mesh = srcFilter != null ? srcFilter.sharedMesh : null;
                GetOrAdd<MeshFilter>(go).sharedMesh = mesh;
                Material[] mats = src != null ? (Material[])src.sharedMaterials.Clone() : new Material[0];
                // The card's body (its biggest part) turns pink; chip and stripes keep their colours.
                int body = 0;
                float bodySize = -1f;
                for (int i = 0; mesh != null && i < mesh.subMeshCount && i < mats.Length; i++)
                {
                    Vector3 s = mesh.GetSubMesh(i).bounds.size;
                    float area = s.x * s.y + s.y * s.z + s.x * s.z;
                    if (area > bodySize) { bodySize = area; body = i; }
                }
                if (mats.Length > 0) mats[body] = pink;
                GetOrAdd<MeshRenderer>(go).sharedMaterials = mats;
                go.transform.localScale = source.lossyScale;
            }
            else
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                GetOrAdd<MeshFilter>(go).sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                GetOrAdd<MeshRenderer>(go).sharedMaterial = pink;
                Object.DestroyImmediate(temp);
                go.transform.localScale = new Vector3(0.085f, 0.054f, 0.003f);
            }
            return go;
        }

        // ------------------------------------------------------------------
        // Interactables
        // ------------------------------------------------------------------
        private static XRGrabInteractable Grabbable(GameObject go, float mass)
        {
            XRGrabInteractable grab = SetupGrabbable(go, mass);
            // A card is very thin: a slightly thicker box makes it easy to pick up.
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 s = box.size;
                Vector3 lossy = go.transform.lossyScale;
                for (int i = 0; i < 3; i++)
                {
                    float min = 0.015f / Mathf.Max(Mathf.Abs(lossy[i]), 1e-4f);
                    if (s[i] < min) s[i] = min;
                }
                box.size = s;
            }
            return grab;
        }

        /// <summary>
        /// The joystick is held by its red ball, not its base: a child at the ball (it tilts with the
        /// stick) carries the touch box and the interactable. The stick's own box from earlier runs is removed.
        /// </summary>
        private static XRSimpleInteractable JoystickGrip(Renderer joystick)
        {
            GameObject go = joystick.gameObject;
            foreach (BoxCollider old in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(old);
            List<Tri> ball = Triangles(joystick, "Red");
            Bounds gb = ball.Count > 0 ? TriBounds(ball) : new Bounds(joystick.bounds.center + Vector3.up * joystick.bounds.extents.y * 0.6f, Vector3.one * 0.06f);
            Transform parts = RebuildParts(go.transform);
            Transform grip = NewPart("Grip", parts, gb.center, joystick.transform.rotation);
            grip.localScale = Unscale(parts);
            KeepPlacement(grip);
            var box = grip.gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.Max(gb.size + Vector3.one * 0.02f, Vector3.one * 0.07f); // a bit bigger than the ball: easy to grab
            // The interactable lives on the grip itself: a hand or ray holds an interactable at its own
            // origin, and the stick's origin is its base. (Earlier runs put it on the stick.)
            foreach (XRSimpleInteractable old in go.GetComponents<XRSimpleInteractable>()) Undo.DestroyObjectImmediate(old);
            XRSimpleInteractable interactable = grip.gameObject.AddComponent<XRSimpleInteractable>();
            var so = new SerializedObject(interactable);
            SetArray(so.FindProperty("m_Colliders"), new Object[] { box });
            so.ApplyModifiedPropertiesWithoutUndo();
            return interactable;
        }

        /// <summary>A basketball: grabbable, a bouncy sphere collider, frozen on the ramp until picked up.</summary>
        private static XRGrabInteractable SetupBall(GameObject go)
        {
            XRGrabInteractable grab = SetupGrabbable(go, 0.6f);
            foreach (BoxCollider b in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(b);
            SphereCollider sphere = GetOrAdd<SphereCollider>(go);
            Bounds local = LocalBounds(go.transform);
            sphere.center = local.center;
            sphere.radius = Mathf.Max(local.extents.x, local.extents.y, local.extents.z);
            sphere.sharedMaterial = GetOrCreateBallBounce();
            var so = new SerializedObject(grab);
            SetArray(so.FindProperty("m_Colliders"), new Object[] { sphere });
            so.ApplyModifiedPropertiesWithoutUndo();
            GetOrAdd<FreeAfterGrab>(go);
            return grab;
        }

        private static PhysicsMaterial GetOrCreateBallBounce()
        {
            const string path = "Assets/Materials/ArcadeBall.physicsMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat != null) return mat;
            mat = new PhysicsMaterial("ArcadeBall")
            {
                bounciness = 0.65f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0.5f,
                staticFriction = 0.5f,
            };
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>A trigger you've moved keeps its place; a new one is built where the model's feature is.</summary>
        private static ItemTrigger KeepOrBuildTrigger(Transform parent, string name, BuildingFrame frame, Vector3 centerL,
            Vector3 size, Vector3 frontW)
        {
            Transform existing = parent.Find(name);
            ItemTrigger trigger = existing != null && existing.GetComponent<ItemTrigger>() != null
                ? existing.GetComponent<ItemTrigger>()
                : BuildItemTrigger(parent, name, frame, centerL, size, frontW);
            var so = new SerializedObject(trigger);
            so.FindProperty("m_LogNearMisses").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        /// <summary>The machine's grey card reader box (material 'CardReader'), reaching a little out in front of it.</summary>
        private static ItemTrigger ReaderTrigger(Transform parent, string name, BuildingFrame frame, Renderer machine, Vector3 frontW)
        {
            if (!MaterialBounds(machine, "CardReader", frame, out Bounds reader))
            {
                Debug.LogError($"[Arcade Setup] Couldn't find the card reader (material 'CardReader') on {machine.name}.");
                return null;
            }
            Vector3 frontL = frame.LocalDir(frontW);
            const float depth = 0.1f;
            return KeepOrBuildTrigger(parent, name, frame, reader.center + frontL * (Extent(reader, frontL) + depth * 0.5f - 0.03f),
                new Vector3(Across(reader, frontL) + 0.05f, reader.size.y + 0.05f, depth), frontW);
        }

        // ------------------------------------------------------------------
        // Game screens
        // ------------------------------------------------------------------
        /// <summary>Score text over a machine's screen (dark background, big auto-sized text).</summary>
        private static TMP_Text BuildScoreScreen(Transform anchor, string name, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, name, px, false);
            AddRounded(canvas, "Background", s_ScreenDark);
            RectTransform textRt = NewUIChild("Text", canvas);
            Stretch(textRt, new Vector2(px.x * 0.06f, px.y * 0.06f), new Vector2(-px.x * 0.06f, -px.y * 0.06f));
            TextMeshProUGUI text = AddText(textRt, "GAME", px.y * 0.2f, TextAlignmentOptions.Center, FontStyles.Normal);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = px.y * 0.24f;
            return text;
        }

        // ------------------------------------------------------------------
        // Air hockey
        // ------------------------------------------------------------------
        private struct HockeyTable
        {
            public float SurfaceY;
            public Vector3 GoalMouth;
            public float GoalHalfWidth;
        }

        /// <summary>
        /// Simple colliders for the table (rebuilt each run, under its QuestParts): a box under the
        /// playing surface and rails around it, with a gap in the far rail for the goal and a wall
        /// behind it. Slippery and bouncy, so the puck glides and rebounds.
        /// </summary>
        private static HockeyTable BuildHockeyColliders(Renderer table, List<Tri> surface, Vector3 frontW)
        {
            const float thick = 0.05f, railUp = 0.1f, gapHalf = 0.15f;
            Vector3 along = -FlatDir(frontW); // away from the player
            Vector3 right = Vector3.Cross(Vector3.up, along);
            float aMin = float.MaxValue, aMax = float.MinValue, rMin = float.MaxValue, rMax = float.MinValue, top = float.MinValue;
            foreach (Tri t in surface)
            {
                foreach (Vector3 p in new[] { t.A, t.B, t.C })
                {
                    aMin = Mathf.Min(aMin, Vector3.Dot(p, along)); aMax = Mathf.Max(aMax, Vector3.Dot(p, along));
                    rMin = Mathf.Min(rMin, Vector3.Dot(p, right)); rMax = Mathf.Max(rMax, Vector3.Dot(p, right));
                    top = Mathf.Max(top, p.y);
                }
            }
            float halfL = (aMax - aMin) * 0.5f, halfW = (rMax - rMin) * 0.5f;
            Vector3 c = along * ((aMin + aMax) * 0.5f) + right * ((rMin + rMax) * 0.5f);
            c.y = top;

            Transform parts = RebuildParts(table.transform);
            PhysicsMaterial slick = GetOrCreatePuckMaterial();
            Quaternion rot = Quaternion.LookRotation(along, Vector3.up);
            void Box(string name, Vector3 center, Vector3 size) // size: (across, height, along)
            {
                Transform t = NewPart(name, parts, center, rot);
                t.localScale = Unscale(parts);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.size = size;
                box.sharedMaterial = slick;
            }
            float railY = top + (railUp - 0.02f) * 0.5f;
            Box("Surface", c - Vector3.up * 0.02f, new Vector3(halfW * 2f, 0.04f, halfL * 2f));
            Box("RailLeft", c - right * (halfW + thick * 0.5f) + Vector3.up * (railY - top), new Vector3(thick, railUp + 0.02f, halfL * 2f + thick * 2f));
            Box("RailRight", c + right * (halfW + thick * 0.5f) + Vector3.up * (railY - top), new Vector3(thick, railUp + 0.02f, halfL * 2f + thick * 2f));
            Box("RailNear", c - along * (halfL + thick * 0.5f) + Vector3.up * (railY - top), new Vector3(halfW * 2f, railUp + 0.02f, thick));
            float seg = Mathf.Max(halfW - gapHalf, 0.01f);
            Vector3 far = c + along * (halfL + thick * 0.5f) + Vector3.up * (railY - top);
            Box("RailFarLeft", far - right * (gapHalf + seg * 0.5f), new Vector3(seg, railUp + 0.02f, thick));
            Box("RailFarRight", far + right * (gapHalf + seg * 0.5f), new Vector3(seg, railUp + 0.02f, thick));
            Box("GoalBack", c + along * (halfL + 0.15f) + Vector3.up * (railY - top), new Vector3(gapHalf * 2f + 0.1f, railUp + 0.02f, thick));

            return new HockeyTable { SurfaceY = top, GoalMouth = c + along * halfL, GoalHalfWidth = gapHalf };
        }

        /// <summary>The puck slides on the table: no gravity, its height and tilt locked, a round collider, a clack on hits.</summary>
        private static Rigidbody SetupPuck(GameObject go, float surfaceY)
        {
            foreach (Collider old in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(old);
            Rigidbody rb = GetOrAdd<Rigidbody>(go);
            rb.mass = 0.05f;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Renderer r = go.GetComponent<Renderer>();
            const float radius = 0.045f;
            Vector3 centerW = r.bounds.center;
            centerW.y = surfaceY + radius + 0.003f; // floats just above the surface: only rails and pushers touch it
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.center = go.transform.InverseTransformPoint(centerW);
            sphere.radius = radius / Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), 1e-4f);
            sphere.sharedMaterial = GetOrCreatePuckMaterial();
            GetOrAdd<PuckSound>(go);
            return rb;
        }

        private static PhysicsMaterial GetOrCreatePuckMaterial()
        {
            const string path = "Assets/Materials/ArcadePuck.physicsMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat != null) return mat;
            mat = new PhysicsMaterial("ArcadePuck")
            {
                bounciness = 0.85f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------
        // Claw machine
        // ------------------------------------------------------------------
        private struct ClawParts
        {
            public Transform Area, Chute, PrizeOut;
            public float HalfWidth, HalfDepth;
            public TMP_Text Text;
            public Button Left, Right, Back, Front, Drop;
            public XRSimpleInteractable RedButton;
        }

        /// <summary>
        /// Everything the claw game needs, found from the claw machine's materials: the glass box the
        /// claw moves in, the grey chute inside it (flat-topped 'Black' part on the floor), the prize
        /// flap on the front ('Black' face under the ledge), a control panel on the ledge and a touch
        /// box on the red button. Poses you've moved keep their placement.
        /// </summary>
        private static ClawParts BuildClawParts(Transform machinesRoot, Renderer machine, Vector3 frontW)
        {
            var result = new ClawParts();
            List<Tri> glass = Triangles(machine, "Glass");
            if (glass.Count == 0)
            {
                Debug.LogError($"[Arcade Setup] Couldn't find the glass box (material 'Glass') on {machine.name}.");
                return result;
            }
            Vector3 front = FlatDir(frontW), back = -front, right = Vector3.Cross(Vector3.up, back);
            float rMin = float.MaxValue, rMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue, yMin = float.MaxValue;
            foreach (Tri t in glass)
            {
                foreach (Vector3 p in new[] { t.A, t.B, t.C })
                {
                    rMin = Mathf.Min(rMin, Vector3.Dot(p, right)); rMax = Mathf.Max(rMax, Vector3.Dot(p, right));
                    bMin = Mathf.Min(bMin, Vector3.Dot(p, back)); bMax = Mathf.Max(bMax, Vector3.Dot(p, back));
                    yMin = Mathf.Min(yMin, p.y);
                }
            }
            Vector3 Point(float r, float b, float y)
            {
                Vector3 p = right * r + back * b;
                p.y = y;
                return p;
            }
            Transform clawRoot = FindOrCreate(machinesRoot, "Claw").transform;
            Quaternion facing = Quaternion.LookRotation(back, Vector3.up);
            result.Area = FindOrCreatePose(clawRoot, "ClawArea", Point((rMin + rMax) * 0.5f, (bMin + bMax) * 0.5f, yMin), facing);
            result.HalfWidth = Mathf.Max((rMax - rMin) * 0.5f - 0.12f, 0.05f);
            result.HalfDepth = Mathf.Max((bMax - bMin) * 0.5f - 0.12f, 0.05f);

            List<Tri> black = Triangles(machine, "Black");
            // Chute: a flat top standing on the floor inside the glass.
            var chute = black.FindAll(t =>
            {
                Vector3 c = t.Center;
                float r = Vector3.Dot(c, right), b = Vector3.Dot(c, back);
                return t.Normal.y > 0.9f && c.y > yMin + 0.02f && c.y < yMin + 0.45f &&
                       r > rMin + 0.02f && r < rMax - 0.02f && b > bMin + 0.02f && b < bMax - 0.02f;
            });
            Vector3 chuteAt;
            if (chute.Count > 0)
            {
                Bounds cb = TriBounds(chute);
                chuteAt = new Vector3(cb.center.x, cb.max.y + 0.05f, cb.center.z);
            }
            else
            {
                Debug.LogWarning($"[Arcade Setup] No prize chute found inside {machine.name}; using its front right corner.");
                chuteAt = Point(rMax - 0.15f, bMin + 0.15f, yMin + 0.2f);
            }
            result.Chute = FindOrCreatePose(clawRoot, "Chute", chuteAt, facing);

            // Prize flap: the black face low on the front.
            var flap = black.FindAll(t => Vector3.Dot(t.Normal, front) > 0.8f && t.Center.y < yMin - 0.2f);
            Vector3 prizeAt;
            if (flap.Count > 0)
            {
                Bounds fb = TriBounds(flap);
                prizeAt = new Vector3(fb.center.x, fb.min.y + 0.01f, fb.center.z); // the plush is spat out from here
            }
            else
            {
                Debug.LogWarning($"[Arcade Setup] No prize flap found on {machine.name}'s front; the plush will come out at its front middle.");
                Bounds mb = machine.bounds;
                prizeAt = new Vector3(mb.center.x, mb.min.y + 0.3f, mb.center.z) + front * (Mathf.Abs(Vector3.Dot(mb.extents, front)) + 0.05f);
            }
            result.PrizeOut = FindOrCreatePose(clawRoot, "PrizeOut", prizeAt, facing);

            // Control panel on the ledge in front of the glass (left of the red button), tilted toward the player.
            var ledge = black.FindAll(t => t.Normal.y > 0.9f && Mathf.Abs(t.Center.y - yMin) < 0.06f && Vector3.Dot(t.Center, back) < bMin - 0.01f);
            Vector3 ledgeAt;
            if (ledge.Count > 0)
            {
                Bounds lb = TriBounds(ledge);
                ledgeAt = new Vector3(lb.center.x, lb.max.y, lb.center.z);
            }
            else ledgeAt = Point((rMin + rMax) * 0.5f, bMin - 0.1f, yMin);
            Vector3 panelNormal = (Vector3.up + front * 0.8f).normalized;
            Transform controls = FindOrCreateAnchor(clawRoot, "ClawControlsAnchor",
                ledgeAt - right * 0.1f + Vector3.up * 0.05f, Quaternion.LookRotation(-panelNormal, Vector3.up));
            BuildClawControls(controls, ref result);

            // The red button on the ledge.
            List<Tri> red = Triangles(machine, "Red");
            if (red.Count > 0)
            {
                Bounds rb = TriBounds(red);
                Transform parts = RebuildParts(machine.transform);
                Transform t = NewPart("RedButton", parts, rb.center, Quaternion.identity);
                t.localScale = Unscale(parts);
                KeepPlacement(t);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.size = rb.size + Vector3.one * 0.03f;
                var touch = t.gameObject.AddComponent<XRSimpleInteractable>();
                var so = new SerializedObject(touch);
                SetArray(so.FindProperty("m_Colliders"), new Object[] { box });
                so.ApplyModifiedPropertiesWithoutUndo();
                result.RedButton = touch;
            }
            return result;
        }

        /// <summary>Status line on top, then LEFT / BACK / FRONT / RIGHT and a red DROP button.</summary>
        private static void BuildClawControls(Transform anchor, ref ClawParts parts)
        {
            Vector2 px = new Vector2(450f, 170f);
            RectTransform canvas = NewWorldCanvas(anchor, "ClawControls", px, true);
            AddRounded(canvas, "Background", s_ScreenDark);

            RectTransform textRt = NewUIChild("Text", canvas);
            textRt.anchorMin = new Vector2(0.03f, 0.58f);
            textRt.anchorMax = new Vector2(0.97f, 0.97f);
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI text = AddText(textRt, "CLAW", 30f, TextAlignmentOptions.Center);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 30f;
            parts.Text = text;

            string[] labels = { "LEFT", "BACK", "FRONT", "RIGHT", "DROP" };
            var buttons = new Button[labels.Length];
            float gap = 0.015f, w = (1f - gap * (labels.Length + 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                float left = gap + i * (w + gap);
                RectTransform slot = NewUIChild("Slot_" + labels[i], canvas);
                slot.anchorMin = new Vector2(left, 0.06f);
                slot.anchorMax = new Vector2(left + w, 0.54f);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                buttons[i] = AddButton(slot, labels[i], i == labels.Length - 1 ? AccentRed : ButtonBlue, 22f);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 22f;
                }
            }
            parts.Left = buttons[0];
            parts.Back = buttons[1];
            parts.Front = buttons[2];
            parts.Right = buttons[3];
            parts.Drop = buttons[4];
        }

        // ------------------------------------------------------------------
        // Counter: "Here!" zone for the plush
        // ------------------------------------------------------------------
        /// <summary>
        /// A zone on the counter top, on the customers' half and at the other end from the register,
        /// where the won plush is set down.
        /// </summary>
        private static ItemTrigger BuildPlushZone(Transform parent, Renderer counter, Vector3 registerW, Vector3 vendorSide)
        {
            Vector3 v = FlatDir(vendorSide), along = Vector3.Cross(Vector3.up, v);
            List<Tri> tops = Triangles(counter, "Black").FindAll(t => t.Normal.y > 0.9f);
            float biggest = 0f;
            foreach (Tri t in tops) biggest = Mathf.Max(biggest, t.Area);
            tops.RemoveAll(t => t.Area < biggest * 0.3f); // the counter's slab, not the register on it
            float aMin = float.MaxValue, aMax = float.MinValue, vMin = float.MaxValue, topY;
            Bounds cb = counter.bounds;
            if (tops.Count > 0)
            {
                topY = float.MinValue;
                foreach (Tri t in tops)
                {
                    foreach (Vector3 p in new[] { t.A, t.B, t.C })
                    {
                        aMin = Mathf.Min(aMin, Vector3.Dot(p, along)); aMax = Mathf.Max(aMax, Vector3.Dot(p, along));
                        vMin = Mathf.Min(vMin, Vector3.Dot(p, v));
                        topY = Mathf.Max(topY, p.y);
                    }
                }
            }
            else
            {
                foreach (Vector3 p in Corners(cb))
                {
                    aMin = Mathf.Min(aMin, Vector3.Dot(p, along)); aMax = Mathf.Max(aMax, Vector3.Dot(p, along));
                    vMin = Mathf.Min(vMin, Vector3.Dot(p, v));
                }
                topY = cb.max.y;
            }
            float mid = (aMin + aMax) * 0.5f;
            float awayFromRegister = Vector3.Dot(registerW, along) > mid ? -1f : 1f;
            float a = mid + awayFromRegister * Mathf.Min(0.4f, (aMax - aMin) * 0.25f);
            Vector3 center = along * a + v * (vMin + 0.18f);
            center.y = topY + 0.15f;

            var go = FindOrCreate(parent, "PlushZone");
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(v, Vector3.up)); // -Z faces the room
            ItemTrigger trigger = GetOrAdd<ItemTrigger>(go);
            var so = new SerializedObject(trigger);
            so.FindProperty("m_Size").vector3Value = new Vector3(0.6f, 0.3f, 0.32f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private struct CabinetScreen
        {
            public TMP_Text Text;
            public RectTransform PlayArea;
            public RectTransform Ship;
            public RectTransform Star;
        }

        /// <summary>
        /// The cabinet's screen: the title/score along the top, a play field under it with the ship
        /// (cyan bar along the bottom) and a falling star (yellow diamond). The field is clipped.
        /// </summary>
        private static CabinetScreen BuildCabinetScreen(Transform anchor, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "CabinetScreen", px, false);
            AddRounded(canvas, "Background", s_ScreenDark);

            RectTransform textRt = NewUIChild("Text", canvas);
            textRt.anchorMin = new Vector2(0.04f, 0.04f);
            textRt.anchorMax = new Vector2(0.96f, 0.97f);
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI text = AddText(textRt, "STAR CATCHER", px.y * 0.14f, TextAlignmentOptions.Top);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = px.y * 0.14f;

            RectTransform area = NewUIChild("PlayArea", canvas);
            area.anchorMin = new Vector2(0.05f, 0.05f);
            area.anchorMax = new Vector2(0.95f, 0.66f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            area.gameObject.AddComponent<RectMask2D>();
            Image field = area.gameObject.AddComponent<Image>();
            field.color = new Color(0.1f, 0.1f, 0.25f, 0.9f);
            field.raycastTarget = false;

            float h = px.y * 0.61f;
            RectTransform ship = NewUIChild("Ship", area);
            ship.anchorMin = ship.anchorMax = new Vector2(0.5f, 0.5f);
            ship.sizeDelta = new Vector2(px.x * 0.9f * 0.16f, h * 0.1f);
            Image shipImg = ship.gameObject.AddComponent<Image>();
            shipImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            shipImg.type = Image.Type.Sliced;
            shipImg.color = new Color(0.31f, 0.94f, 1f);
            shipImg.raycastTarget = false;

            RectTransform star = NewUIChild("Star", area);
            star.anchorMin = star.anchorMax = new Vector2(0.5f, 0.5f);
            star.sizeDelta = new Vector2(h * 0.11f, h * 0.11f);
            star.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image starImg = star.gameObject.AddComponent<Image>();
            starImg.color = new Color(1f, 0.83f, 0.3f);
            starImg.raycastTarget = false;

            return new CabinetScreen { Text = text, PlayArea = area, Ship = ship, Star = star };
        }

        // ------------------------------------------------------------------
        // Rhythm game
        // ------------------------------------------------------------------
        private struct RhythmParts
        {
            public XRSimpleInteractable[] Buttons;
            public GameObject[] Glows;
            public Vector3 ScreenCenter;
            public Vector3 Normal;
            public Vector2 ScreenSize;
        }

        /// <summary>
        /// The 8 yellow buttons around the Maimai's round screen (separate parts of material 'Yellow'):
        /// each gets a touch box and a glow disc (rebuilt each run), in clockwise order from the top.
        /// The round screen in the middle of the ring gets the score.
        /// </summary>
        private static RhythmParts BuildRhythmParts(Renderer machine, Vector3 frontW)
        {
            var result = new RhythmParts();
            List<List<Tri>> groups = Clusters(Triangles(machine, "Yellow"), 0.005f);
            if (groups.Count < 3)
            {
                Debug.LogError($"[Arcade Setup] Found {groups.Count} yellow buttons on {machine.name}; expected 8.");
                return result;
            }
            if (groups.Count != 8) Debug.LogWarning($"[Arcade Setup] Found {groups.Count} yellow buttons on {machine.name} (expected 8). Using them all.");

            var centers = new List<Vector3>();
            var normals = new List<Vector3>();
            var radii = new List<float>();
            foreach (List<Tri> g in groups)
            {
                // The button's top: its faces looking out of the machine (tilted back with the front panel).
                Vector3 sum = Vector3.zero;
                foreach (Tri t in g)
                    if (Vector3.Dot(t.Normal, frontW) > 0.3f) sum += t.Cross;
                Vector3 n = sum.sqrMagnitude > 1e-12f ? sum.normalized : frontW;
                var top = g.FindAll(t => Vector3.Dot(t.Normal, n) > 0.8f);
                if (top.Count == 0) top = g;
                Rect(top, n, out Vector3 c, out Vector2 size);
                centers.Add(c);
                normals.Add(n);
                radii.Add(Mathf.Max(size.x, size.y) * 0.5f);
            }
            Vector3 ringCenter = Vector3.zero, ringNormal = Vector3.zero;
            for (int i = 0; i < centers.Count; i++) { ringCenter += centers[i]; ringNormal += normals[i]; }
            ringCenter /= centers.Count;
            ringNormal.Normalize();

            // Clockwise from the top, as you face the machine.
            Vector3 right = Vector3.Cross(Vector3.up, -ringNormal).normalized;
            Vector3 up = Vector3.Cross(-ringNormal, right).normalized;
            var order = new List<int>();
            for (int i = 0; i < centers.Count; i++) order.Add(i);
            float Angle(int i)
            {
                Vector3 d = centers[i] - ringCenter;
                float a = Mathf.Atan2(Vector3.Dot(d, right), Vector3.Dot(d, up)); // 0 at the top, growing clockwise
                return a < -0.01f ? a + 2f * Mathf.PI : a;
            }
            order.Sort((a, b) => Angle(a).CompareTo(Angle(b)));

            Transform parts = RebuildParts(machine.transform);
            Material glowMat = GetOrCreateTransparentUnlitMaterial("Assets/Materials/ArcadeButtonGlow.mat", s_ButtonGlow);
            result.Buttons = new XRSimpleInteractable[order.Count];
            result.Glows = new GameObject[order.Count];
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                float r = Mathf.Max(radii[i], 0.02f);
                var go = new GameObject("Button_" + (k + 1));
                Undo.RegisterCreatedObjectUndo(go, "Create button");
                go.transform.SetParent(parts, false);
                go.transform.SetPositionAndRotation(centers[i], Quaternion.LookRotation(-normals[i], up));
                go.transform.localScale = Unscale(parts);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(r * 2f + 0.02f, r * 2f + 0.02f, 0.05f); // pokes 2.5 cm out of the button
                var touch = go.AddComponent<XRSimpleInteractable>();
                var soT = new SerializedObject(touch);
                SetArray(soT.FindProperty("m_Colliders"), new Object[] { box });
                soT.ApplyModifiedPropertiesWithoutUndo();
                result.Buttons[k] = touch;

                // Flat glowing disc just above the button (a cylinder's axis is its Y).
                GameObject glow = Primitive(PrimitiveType.Cylinder, "Glow", go.transform, Vector3.zero, Vector3.one, glowMat);
                glow.transform.SetPositionAndRotation(centers[i] + normals[i] * 0.006f, Quaternion.FromToRotation(Vector3.up, normals[i]));
                glow.transform.localScale = new Vector3(r * 2.3f, 0.003f, r * 2.3f); // the button is unscaled: metres
                glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                result.Glows[k] = glow;
                KeepPlacement(go.transform); // a button (touch box + glow) you've nudged stays where you put it
            }

            // The round screen in the middle of the ring.
            var screen = Triangles(machine, "Screen").FindAll(t =>
                Vector3.Dot(t.Normal, ringNormal) > 0.8f && Vector3.Distance(t.Center, ringCenter) < 0.33f);
            float side;
            if (screen.Count > 0)
            {
                Rect(screen, ringNormal, out Vector3 sc, out Vector2 ss);
                result.ScreenCenter = sc;
                side = Mathf.Min(ss.x, ss.y) * 0.7f; // a square inside the round screen
            }
            else
            {
                Debug.LogWarning($"[Arcade Setup] No round screen (material 'Screen') found inside {machine.name}'s button ring; using the ring centre.");
                result.ScreenCenter = ringCenter;
                side = 0.3f;
            }
            result.Normal = ringNormal;
            result.ScreenSize = new Vector2(side, side);
            return result;
        }

        private static Vector3 Unscale(Transform parent)
        {
            Vector3 l = parent.lossyScale;
            return new Vector3(1f / Mathf.Max(Mathf.Abs(l.x), 1e-4f), 1f / Mathf.Max(Mathf.Abs(l.y), 1e-4f), 1f / Mathf.Max(Mathf.Abs(l.z), 1e-4f));
        }
    }
}
