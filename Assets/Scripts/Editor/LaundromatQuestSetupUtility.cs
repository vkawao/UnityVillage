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
    /// Builds the Laundromat (self-service laundry) quest in the Village scene:
    ///   LaundromatQuest (QuestManager, QuestHideAtStart)
    ///     QuestPanel, Anchors/, Steps/, FX/, Guide/ (as in the other houses)
    ///     HipHolster     hip pocket that follows the player (credit card, coins)
    ///     Items/         credit card and coins (copies of Sakura Park's card and coin)
    ///     Machines/      change-machine screen, queue-machine screen, card pad, coin slot, number tags
    /// The laundromat model (Laundromat.fbx) stays a linked prefab instance, so Blender re-exports keep
    /// flowing in; components are added onto its objects (switch, fans, shirts, basket).
    /// Safe to re-run: anchors, triggers and tags you've moved keep their placement.
    /// </summary>
    public static class LaundromatQuestSetupUtility
    {
        private const string RootName = "LaundromatQuest";
        private const string ModelName = "Laundromat";
        private const string ParkName = "VendingMachinePark";

        private static readonly string[] s_ShortTexts =
        {
            "Press the fan switch on the front post",
            "Tap the card from your hip on the change machine",
            "Book a washer and a dryer, then insert your coins",
            "Put all the shirts in the blue basket",
            "Put the basket on top of your washer",
            "Shirts into the washer, then pour in detergent",
            "Throw the empty detergent bottle in the bin",
            "Push a laundry cart to your washer",
            "Push the cart to your dryer",
            "Put the shirts in the dryer",
        };

        /// <summary>Shirts' size relative to the model (0.6 = a 77 cm shirt becomes 46 cm).</summary>
        private const float ShirtScale = 0.6f;

        private static readonly Color s_WasherBlue = new Color(0.12f, 0.3f, 0.63f);
        private static readonly Color s_DryerOrange = new Color(0.93f, 0.55f, 0.1f);
        private static readonly Color s_ScreenDark = new Color(0.05f, 0.07f, 0.1f, 0.97f);
        private static readonly Color s_Detergent = new Color(0.3f, 0.75f, 0.45f);

        [MenuItem("VR Village/Setup Laundromat Quest")]
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
                Debug.LogError($"[Laundromat Setup] '{ModelName}' not found in the scene. Drag Laundromat.fbx into the scene first.");
                return;
            }

            // Slots, screens and trays are located from mesh data, so the model must be readable.
            if (EnsureReadable(Need<Renderer>(model.transform, "ChangeMachine")))
                model = FindInScene(ModelName);

            Transform m = model.transform;
            Renderer building = Need<Renderer>(m, "LaundromatBuilding");
            Renderer signBoard = Need<Renderer>(m, "SignBoard");
            Renderer switchPlate = Need<Renderer>(m, "Switch");
            Renderer switchButton = Need<Renderer>(m, "SwitchButton");
            Renderer change = Need<Renderer>(m, "ChangeMachine");
            Renderer queue = FindQueueMachine(m);
            Renderer basket = Need<Renderer>(m, "LaundryBasket");
            Renderer bottle = Need<Renderer>(m, "DetergentBottle");
            Renderer bin = Need<Renderer>(m, "TrashBin");
            List<Renderer> carts = NeedNumbered(m, "LaundryCart");
            List<Renderer> fans = NeedNumbered(m, "CeilingFan");
            List<Renderer> shirts = NeedNumbered(m, "Shirt");
            List<Renderer> washers = NeedNumbered(m, "WashingMachine");
            List<Renderer> dryers = NeedNumbered(m, "Dryer");
            dryers.AddRange(NeedNumbered(m, "DryerTop"));
            if (building == null || signBoard == null || switchPlate == null || switchButton == null || change == null ||
                queue == null || basket == null || bottle == null || bin == null || carts.Count == 0 || fans.Count == 0 || shirts.Count == 0 || washers.Count == 0 || dryers.Count == 0)
            {
                Debug.LogError("[Laundromat Setup] Some laundromat objects are missing (see the errors above). Nothing was changed.");
                return;
            }

            EnsureEventSystem();

            // ---------- Colliders on scenery (so things can be set down and land) ----------
            foreach (Transform t in m.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "LaundromatBuilding" || n == "ChangeMachine" || n == queue.name || n.StartsWith("FoldingTable") ||
                    n.StartsWith("WashingMachine") || n == "Dryer" || n.StartsWith("Dryer.") || n.StartsWith("DryerTop") && !n.StartsWith("DryerTopKnob") ||
                    n.StartsWith("ClothesRack") || n == "TrashBin")
                    EnsureStaticCollider(t.gameObject);
            }

            // ---------- Hierarchy ----------
            GameObject root = FindOrCreate(null, RootName);
            Transform anchors = FindOrCreate(root.transform, "Anchors").transform;
            Transform steps = FindOrCreate(root.transform, "Steps").transform;
            // Booking moved before the basket (it's next to the change machine): keep earlier objects and placement.
            RenameChild(anchors, "QuestAnchor_4_Queue", "QuestAnchor_3_Queue");
            RenameChild(anchors, "QuestAnchor_3_Basket", "QuestAnchor_4_Basket");
            RenameChild(steps, "Step4_BookMachines", "Step3_BookMachines");
            RenameChild(steps, "Step3_ShirtsInBasket", "Step4_ShirtsInBasket");
            Transform fx = FindOrCreate(root.transform, "FX").transform;
            Transform items = FindOrCreate(root.transform, "Items").transform;
            Transform machines = FindOrCreate(root.transform, "Machines").transform;

            QuestManager manager = GetOrAdd<QuestManager>(root);
            QuestPanel panel = BuildPanel(root.transform);
            panel.transform.localScale = Vector3.one * 0.00075f; // ~1.2 m wide

            // ---------- Geometry ----------
            var frame = new BuildingFrame(m);
            if (!MaterialBounds(building, "Concrete", frame, out Bounds floorL))
            {
                Debug.LogWarning("[Laundromat Setup] No floor (material 'Concrete') on LaundromatBuilding; using the whole building.");
                floorL = frame.BoundsOf(building.transform);
            }
            Vector3 floorCenterW = frame.ToWorld(floorL.center);
            Vector3 outward = frame.Snap(signBoard.bounds.center - floorCenterW); // from the shop out to the street
            // Rows of machines face into the shop: the washers (and the change/queue machines beside them)
            // from the back wall, the dryers from the side wall.
            Vector3 washerFront = frame.Snap(floorCenterW - AverageCenter(washers.ConvertAll(r => r.transform)));
            Vector3 dryerFront = frame.Snap(floorCenterW - AverageCenter(dryers.ConvertAll(r => r.transform)));
            Vector3 changeFront = washerFront;
            Vector3 queueFront = washerFront;

            // ---------- 1. Fan switch ----------
            Vector3 switchFront = frame.Snap(switchButton.bounds.center - switchPlate.bounds.center);
            XRSimpleInteractable switchTouch = SimpleInteractable(switchButton.gameObject, 0.04f);
            var fanComps = new List<CeilingFan>();
            foreach (Renderer f in fans)
            {
                CeilingFan fan = GetOrAdd<CeilingFan>(f.gameObject);
                var soFan = new SerializedObject(fan);
                soFan.FindProperty("m_Volume").floatValue = 0.08f; // three of them together: keep each one soft
                soFan.ApplyModifiedPropertiesWithoutUndo();
                fanComps.Add(fan);
            }

            // ---------- 2. Card, coins, change machine ----------
            GameObject parkGo = FindInScene(ParkName);
            Transform park = parkGo != null ? parkGo.transform : null;
            Transform parkCard = park != null ? FindDeepChild(park, "CreditCard") : null;
            Transform parkCoin = park != null ? FindDeepChild(park, "Coin") : null;
            if (parkCard == null || parkCoin == null)
                Debug.LogWarning("[Laundromat Setup] Sakura Park's CreditCard/Coin not found; using simple stand-ins.");

            ItemTrigger pad = CardPad(machines, frame, change, changeFront);
            Transform changeTray = Tray(machines, "ChangeTray", frame, change, changeFront);
            TMP_Text changeScreen = ChangeScreen(machines, frame, change, changeFront);
            if (pad == null || changeTray == null) return;

            // Parked well above the machine while hidden: never inside the pad it's tapped on.
            GameObject card = BuildCard(items, parkCard, change.bounds.max + Vector3.up * 1.2f);
            XRGrabInteractable cardGrab = Grabbable(card, 0.02f);
            GameObject coins = BuildCoins(items, parkCoin, changeTray.position + Vector3.up * 0.03f);
            XRGrabInteractable coinsGrab = Grabbable(coins, 0.03f);

            var hide = GetOrAdd<QuestHideAtStart>(root);
            var soHide = new SerializedObject(hide);
            SetArray(soHide.FindProperty("m_Hide"), new Object[] { card, coins });
            soHide.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 3. Shirts and basket ----------
            var shirtItems = new List<LaundryItem>();
            foreach (Renderer s in shirts)
            {
                // Smaller than modelled, once (from the model's own size, so re-runs don't keep shrinking),
                // so they fit the basket, the drum and the cart without being resized there.
                Transform source = PrefabUtility.GetCorrespondingObjectFromSource(s.transform);
                Vector3 modelScale = source != null ? source.localScale : s.transform.localScale;
                Undo.RecordObject(s.transform, "Shirt size");
                s.transform.localScale = modelScale * ShirtScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(s.transform);
                XRGrabInteractable g = Grabbable(s.gameObject, 0.15f);
                g.retainTransformParent = false; // dropping a shirt must not put it back into the basket it came from
                EditorUtility.SetDirty(g);
                shirtItems.Add(GetOrAdd<LaundryItem>(s.gameObject));
            }
            SetupGrabbable(basket.gameObject, 0.6f);
            LaundryContainer basketBox = BuildContainer(basket, shirtItems);

            // ---------- 4. Queue machine ----------
            if (!FrontFaces(queue, "DarkGlass", queueFront, frame, out Bounds glassB))
            {
                Debug.LogError($"[Laundromat Setup] Couldn't find the screen (material 'DarkGlass') on {queue.name}'s front.");
                return;
            }
            Vector3 queueFrontL = frame.LocalDir(queueFront);
            Transform queueAnchor = FindOrCreateAnchor(machines, "QueueScreenAnchor",
                frame.ToWorld(glassB.center + queueFrontL * 0.004f), Quaternion.LookRotation(-queueFront, Vector3.up));
            QueueScreen screen = BuildQueueScreen(queueAnchor, new Vector2(Across(glassB, queueFrontL), glassB.size.y),
                washers.Count, dryers.Count);
            ItemTrigger coinSlot = PlateTrigger(machines, "CoinSlot", frame, queue, "Chrome", queueFront, 0.08f);
            if (coinSlot == null) return;

            Transform tagRoot = FindOrCreate(machines, "Tags").transform;
            List<Renderer> washerOrder = SortLeftToRight(washers, washerFront, false);
            List<Renderer> dryerOrder = SortLeftToRight(dryers, dryerFront, true);
            var washerTags = new List<Image>();
            for (int i = 0; i < washerOrder.Count; i++)
                washerTags.Add(BuildTag(tagRoot, "WasherTag_" + (i + 1), washerOrder[i], frame, washerFront, i + 1, s_WasherBlue, false));
            var dryerTags = new List<Image>();
            for (int i = 0; i < dryerOrder.Count; i++)
                dryerTags.Add(BuildTag(tagRoot, "DryerTag_" + (i + 1), dryerOrder[i], frame, dryerFront, i + 1, s_DryerOrange, true));

            // ---------- 5-10. Washers, dryers, detergent, bin, carts ----------
            QueueStep queueStep = GetOrAdd<QueueStep>(FindOrCreate(steps, "Step3_BookMachines"));
            var washerMachines = new List<LaundryMachine>();
            foreach (Renderer w in washerOrder) washerMachines.Add(BuildWasher(w, washerFront, shirtItems));
            var dryerMachines = new List<LaundryMachine>();
            foreach (Renderer d in dryerOrder) dryerMachines.Add(BuildDryer(d, dryerFront));
            if (washerMachines.Contains(null)) return;
            Transform washer1 = washerOrder[0].transform, dryer1 = dryerOrder[0].transform;

            XRGrabInteractable bottleGrab = Grabbable(bottle.gameObject, 0.4f);
            Transform bottleParts = RebuildParts(bottle.transform);
            Bounds btB = bottle.bounds;
            Transform spout = NewPart("Spout", bottleParts, new Vector3(btB.center.x, btB.max.y - 0.01f, btB.center.z), Quaternion.identity);
            KeepPlacement(spout);
            StreamFx detergentStream = BuildStream(machines, "DetergentStream", 0.012f);
            ItemTrigger binOpening = BinTrigger(machines, frame, bin, outward);

            var cartBoxes = new List<LaundryContainer>();
            foreach (Renderer c in carts)
            {
                XRGrabInteractable g = SetupGrabbable(c.gameObject, 20f);
                g.throwOnDetach = false; // it's pushed, not thrown
                EditorUtility.SetDirty(g);
                Rigidbody rb = c.GetComponent<Rigidbody>();
                // Stays upright on the floor: only slides and turns, wherever the hand goes.
                rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rb.linearDamping = 4f;
                rb.angularDamping = 4f;
                cartBoxes.Add(BuildContainer(c, shirtItems));
            }

            // Zones and markers placed around washer 1 / dryer 1; at runtime they follow the booking.
            Transform follow = FindOrCreate(machines, "FollowBooking").transform;
            Bounds w1 = frame.BoundsOf(washer1);
            Vector3 wFrontL = frame.LocalDir(washerFront);
            ItemTrigger topZone = KeepOrBuildTrigger(follow, "WasherTopZone", frame,
                new Vector3(w1.center.x, w1.max.y + 0.2f, w1.center.z), new Vector3(Across(w1, wFrontL) + 0.06f, 0.4f, Extent(w1, wFrontL) * 2f + 0.06f), washerFront);
            Follow(topZone.transform, queueStep, washer1, false, false);
            ItemTrigger washerFrontZone = KeepOrBuildTrigger(follow, "WasherFrontZone", frame,
                new Vector3(w1.center.x, w1.min.y + 0.5f, w1.center.z) + wFrontL * (Extent(w1, wFrontL) + 0.65f), new Vector3(1.5f, 1f, 1.1f), washerFront);
            Follow(washerFrontZone.transform, queueStep, washer1, false, true);
            Bounds d1 = frame.BoundsOf(dryer1);
            Vector3 dFrontL = frame.LocalDir(dryerFront);
            ItemTrigger dryerFrontZone = KeepOrBuildTrigger(follow, "DryerFrontZone", frame,
                new Vector3(d1.center.x, d1.min.y + 0.5f, d1.center.z) + dFrontL * (Extent(d1, dFrontL) + 0.65f), new Vector3(1.5f, 1f, 1.1f), dryerFront);
            Follow(dryerFrontZone.transform, queueStep, dryer1, true, true);

            Quaternion facesWasher = Quaternion.LookRotation(-washerFront, Vector3.up);
            Quaternion facesDryer = Quaternion.LookRotation(-dryerFront, Vector3.up);
            GameObject topMarker = FollowMarker(follow, "WasherTopMarker", topZone.transform.position + Vector3.up * 0.45f, facesWasher,
                queueStep, washer1, false, false);
            GameObject washerFrontMarker = FollowMarker(follow, "WasherFrontMarker", washerFrontZone.transform.position + Vector3.up * 0.6f,
                facesWasher, queueStep, washer1, false, true);
            GameObject dryerFrontMarker = FollowMarker(follow, "DryerFrontMarker", dryerFrontZone.transform.position + Vector3.up * 0.6f,
                facesDryer, queueStep, dryer1, true, true);

            // ---------- Guide + hip holster ----------
            // The concrete floor, not the whole building: the roof overhang would reach into the neighbours.
            BuildGuide(root.transform, frame, floorL, GuideMarginAvoidingOthers(floorL, frame, root.transform));
            QuestGuide guide = root.transform.Find("Guide").GetComponent<QuestGuide>();
            HipHolster holster = BuildHolster(root.transform, guide, new[] { cardGrab, coinsGrab }, true);

            // ---------- Panel anchors ----------
            Bounds plateB = switchPlate.bounds;
            Transform a1 = FindOrCreateAnchor(anchors, "QuestAnchor_1_FanSwitch",
                Flat(plateB.center) + switchFront * 0.4f + Vector3.up * (plateB.max.y + 0.55f), Quaternion.LookRotation(-switchFront, Vector3.up));
            Bounds chB = change.bounds;
            Transform a2 = FindOrCreateAnchor(anchors, "QuestAnchor_2_ChangeMachine",
                Flat(chB.center) + changeFront * 0.3f + Vector3.up * (chB.max.y + 0.45f), Quaternion.LookRotation(-changeFront, Vector3.up));
            Bounds qB = queue.bounds;
            Transform a3 = FindOrCreateAnchor(anchors, "QuestAnchor_3_Queue",
                Flat(qB.center) + queueFront * 0.3f + Vector3.up * (qB.max.y + 0.45f), Quaternion.LookRotation(-queueFront, Vector3.up));
            Vector3 laundryCenter = (basket.bounds.center + AverageCenter(shirts.ConvertAll(r => r.transform))) * 0.5f;
            Transform a4 = FindOrCreateAnchor(anchors, "QuestAnchor_4_Basket",
                Flat(laundryCenter) - outward * 0.3f + Vector3.up * (basket.bounds.max.y + 0.75f), Quaternion.LookRotation(-outward, Vector3.up));

            Transform panelAt = FindOrCreate(anchors, "FollowBooking").transform;
            Vector3 washerPanel = Flat(washer1.position) + washerFront * (Extent(w1, wFrontL) + 0.45f) + Vector3.up * (w1.max.y + 0.75f);
            Transform a5 = FollowAnchor(panelAt, "QuestAnchor_5_BasketOnWasher", washerPanel, facesWasher, queueStep, washer1, false, false);
            Transform a6 = FollowAnchor(panelAt, "QuestAnchor_6_LoadWasher", washerPanel, facesWasher, queueStep, washer1, false, false);
            Bounds binB = bin.bounds;
            Transform a7 = FindOrCreateAnchor(anchors, "QuestAnchor_7_Bin",
                Flat(binB.center) - outward * 0.35f + Vector3.up * (binB.max.y + 0.8f), Quaternion.LookRotation(outward, Vector3.up));
            Transform a8 = FollowAnchor(panelAt, "QuestAnchor_8_Cart", washerPanel + washerFront * 0.4f, facesWasher, queueStep, washer1, false, false);
            float stackTop = float.MinValue;
            foreach (Renderer d in dryers) stackTop = Mathf.Max(stackTop, d.bounds.max.y);
            Vector3 dryerPanel = Flat(dryer1.position) + dryerFront * (Extent(d1, dFrontL) + 0.6f) + Vector3.up * (stackTop + 0.35f);
            Transform a9 = FollowAnchor(panelAt, "QuestAnchor_9_CartToDryer", dryerPanel, facesDryer, queueStep, dryer1, true, true);
            Transform a10 = FollowAnchor(panelAt, "QuestAnchor_10_LoadDryer", dryerPanel, facesDryer, queueStep, dryer1, true, true);

            QuestCelebration finale = BuildCelebration(fx, "Celebration_Dryer", dryer1.position + Vector3.up * 1.2f + dryerFront * 0.6f,
                CelebrationStyle.ConfettiStyle());
            Follow(finale.transform, queueStep, dryer1, true, false);

            QuestCelebration basketSparkle = BuildCelebration(fx, "Celebration_Basket", basket.bounds.center + Vector3.up * 0.3f);
            QuestCelebration queueSparkle = BuildCelebration(fx, "Celebration_Queue", queueAnchor.position + queueFront * 0.15f);

            // ---------- Steps ----------
            SwitchStep step1 = GetOrAdd<SwitchStep>(FindOrCreate(steps, "Step1_FanSwitch"));
            var so1 = StepSO(step1, "Phew, it's hot in here! Turn on the ceiling fans.", a1);
            so1.FindProperty("m_Button").objectReferenceValue = switchTouch;
            so1.FindProperty("m_PushDirection").vector3Value = -switchFront;
            SetArray(so1.FindProperty("m_Fans"), fanComps);
            so1.ApplyModifiedPropertiesWithoutUndo();

            CardChangeStep step2 = GetOrAdd<CardChangeStep>(FindOrCreate(steps, "Step2_GetChange"));
            var so2 = StepSO(step2, "The machines take coins. Get change with your card.", a2);
            so2.FindProperty("m_Holster").objectReferenceValue = holster;
            so2.FindProperty("m_Card").objectReferenceValue = cardGrab;
            so2.FindProperty("m_Pad").objectReferenceValue = pad;
            so2.FindProperty("m_Coins").objectReferenceValue = coinsGrab;
            so2.FindProperty("m_Outlet").objectReferenceValue = changeTray;
            so2.FindProperty("m_Screen").objectReferenceValue = changeScreen;
            so2.ApplyModifiedPropertiesWithoutUndo();

            QueueStep step3 = GetOrAdd<QueueStep>(FindOrCreate(steps, "Step3_BookMachines"));
            var so3 = StepSO(step3, "Book a washer and a dryer at the queue machine.", a3, queueSparkle);
            SetArray(so3.FindProperty("m_WasherButtons"), screen.Washers);
            SetArray(so3.FindProperty("m_DryerButtons"), screen.Dryers);
            so3.FindProperty("m_Status").objectReferenceValue = screen.Status;
            so3.FindProperty("m_Screen").objectReferenceValue = queueAnchor;
            SetArray(so3.FindProperty("m_WasherTags"), washerTags);
            SetArray(so3.FindProperty("m_DryerTags"), dryerTags);
            so3.FindProperty("m_Coin").objectReferenceValue = coinsGrab;
            so3.FindProperty("m_CoinSlot").objectReferenceValue = coinSlot;
            SetArray(so3.FindProperty("m_Washers"), washerMachines);
            SetArray(so3.FindProperty("m_Dryers"), dryerMachines);
            so3.ApplyModifiedPropertiesWithoutUndo();

            FillContainerStep step4 = GetOrAdd<FillContainerStep>(FindOrCreate(steps, "Step4_ShirtsInBasket"));
            var so4 = StepSO(step4, "Gather your dirty shirts into the blue basket.", a4, basketSparkle);
            so4.FindProperty("m_Container").objectReferenceValue = basketBox;
            SetArray(so4.FindProperty("m_Items"), shirtItems);
            so4.FindProperty("m_Required").intValue = 0;
            so4.FindProperty("m_ItemName").stringValue = "Shirts";
            so4.FindProperty("m_ContainerName").stringValue = "the basket";
            so4.FindProperty("m_Hint").stringValue = "Pick them up from the table and let go over the basket";
            so4.ApplyModifiedPropertiesWithoutUndo();

            PlaceOnMachineStep step5 = GetOrAdd<PlaceOnMachineStep>(FindOrCreate(steps, "Step5_BasketOnWasher"));
            var so5 = StepSO(step5, "Take your basket to the washer you booked.", a5, null, new[] { topMarker });
            so5.FindProperty("m_Queue").objectReferenceValue = step3;
            so5.FindProperty("m_Dryer").boolValue = false;
            so5.FindProperty("m_Item").objectReferenceValue = basket.GetComponent<XRGrabInteractable>();
            so5.FindProperty("m_Zone").objectReferenceValue = topZone;
            so5.FindProperty("m_ItemName").stringValue = "the basket";
            so5.FindProperty("m_Where").stringValue = "on top of";
            so5.ApplyModifiedPropertiesWithoutUndo();

            WasherLoadStep step6 = GetOrAdd<WasherLoadStep>(FindOrCreate(steps, "Step6_LoadWasher"));
            var so6 = StepSO(step6, "Load the washer and add some detergent.", a6);
            so6.FindProperty("m_Queue").objectReferenceValue = step3;
            SetArray(so6.FindProperty("m_Items"), shirtItems);
            so6.FindProperty("m_Basket").objectReferenceValue = basketBox;
            so6.FindProperty("m_Bottle").objectReferenceValue = bottleGrab;
            so6.FindProperty("m_Spout").objectReferenceValue = spout;
            so6.FindProperty("m_Stream").objectReferenceValue = detergentStream;
            so6.FindProperty("m_DetergentColor").colorValue = s_Detergent;
            so6.ApplyModifiedPropertiesWithoutUndo();

            FinishWashStep step7 = GetOrAdd<FinishWashStep>(FindOrCreate(steps, "Step7_BinBottle"));
            var so7 = StepSO(step7, "Out of detergent! Throw the bottle away while it washes.", a7);
            so7.FindProperty("m_Queue").objectReferenceValue = step3;
            so7.FindProperty("m_Bottle").objectReferenceValue = bottleGrab;
            so7.FindProperty("m_Bin").objectReferenceValue = binOpening;
            SetArray(so7.FindProperty("m_Items"), shirtItems);
            so7.ApplyModifiedPropertiesWithoutUndo();

            CartLoadStep step8 = GetOrAdd<CartLoadStep>(FindOrCreate(steps, "Step8_LoadCart"));
            var so8 = StepSO(step8, "Wash done! Bring a laundry cart to your washer.", a8, null, new[] { washerFrontMarker });
            so8.FindProperty("m_Queue").objectReferenceValue = step3;
            SetArray(so8.FindProperty("m_Carts"), cartBoxes);
            SetArray(so8.FindProperty("m_Items"), shirtItems);
            so8.FindProperty("m_Zone").objectReferenceValue = washerFrontZone;
            so8.ApplyModifiedPropertiesWithoutUndo();

            CartToMachineStep step9 = GetOrAdd<CartToMachineStep>(FindOrCreate(steps, "Step9_CartToDryer"));
            var so9 = StepSO(step9, "Wheel your laundry over to your dryer.", a9, null, new[] { dryerFrontMarker });
            so9.FindProperty("m_Queue").objectReferenceValue = step3;
            so9.FindProperty("m_Dryer").boolValue = true;
            so9.FindProperty("m_CartStep").objectReferenceValue = step8;
            so9.FindProperty("m_Zone").objectReferenceValue = dryerFrontZone;
            so9.ApplyModifiedPropertiesWithoutUndo();

            DryerLoadStep step10 = GetOrAdd<DryerLoadStep>(FindOrCreate(steps, "Step10_LoadDryer"));
            var so10 = StepSO(step10, "Put your shirts in the dryer to finish!", a10, finale);
            so10.FindProperty("m_Queue").objectReferenceValue = step3;
            SetArray(so10.FindProperty("m_Items"), shirtItems);
            so10.FindProperty("m_CartStep").objectReferenceValue = step8;
            so10.FindProperty("m_CartSpot").objectReferenceValue = dryerFrontZone;
            so10.ApplyModifiedPropertiesWithoutUndo();

            var allSteps = new List<Object> { step1, step2, step3, step4, step5, step6, step7, step8, step9, step10 };
            SetGuideTexts(allSteps, s_ShortTexts, new Dictionary<Object, Transform>
            {
                { step1, switchButton.transform },
            });

            var soM = new SerializedObject(manager);
            soM.FindProperty("m_Panel").objectReferenceValue = panel;
            SetArray(soM.FindProperty("m_Steps"), allSteps);
            soM.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("<color=green>[Laundromat Setup] Laundromat quest ready: actions 1-10.</color>");
        }

        // ------------------------------------------------------------------
        // Finding things
        // ------------------------------------------------------------------
        private static T Need<T>(Transform root, string name) where T : Component
        {
            Transform t = FindDeepChild(root, name);
            T comp = t != null ? t.GetComponent<T>() : null;
            if (comp == null) Debug.LogError($"[Laundromat Setup] '{name}' not found under {root.name}.");
            return comp;
        }

        /// <summary>Rename a child left over from an older step order (only if the new name isn't taken yet).</summary>
        private static void RenameChild(Transform parent, string oldName, string newName)
        {
            Transform t = parent.Find(oldName);
            if (t == null || parent.Find(newName) != null) return;
            Undo.RecordObject(t.gameObject, "Rename " + oldName);
            t.name = newName;
        }

        /// <summary>The queue machine (named DetergentVendor in older exports of the model).</summary>
        private static Renderer FindQueueMachine(Transform root)
        {
            foreach (string n in new[] { "QueueMachine", "DetergentVendor" })
            {
                Transform t = FindDeepChild(root, n);
                if (t != null && t.GetComponent<Renderer>() != null) return t.GetComponent<Renderer>();
            }
            Debug.LogError($"[Laundromat Setup] 'QueueMachine' not found under {root.name}. Re-export Laundromat.fbx from Blender.");
            return null;
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
            if (list.Count == 0) Debug.LogError($"[Laundromat Setup] No '{baseName}' objects found under {root.name}.");
            return list;
        }

        /// <summary>
        /// Machines in reading order, as seen from in front of them: left to right, and for the
        /// stacked dryers the bottom row first.
        /// </summary>
        private static List<Renderer> SortLeftToRight(List<Renderer> machines, Vector3 front, bool rows)
        {
            var sorted = new List<Renderer>(machines);
            Vector3 right = Vector3.Cross(Vector3.up, -front); // the viewer looks along -front
            sorted.Sort((a, b) =>
            {
                Vector3 pa = a.bounds.center, pb = b.bounds.center;
                if (rows && Mathf.Abs(pa.y - pb.y) > 0.3f) return pa.y.CompareTo(pb.y);
                return Vector3.Dot(pa, right).CompareTo(Vector3.Dot(pb, right));
            });
            return sorted;
        }

        // ------------------------------------------------------------------
        // Interactables
        // ------------------------------------------------------------------
        private static XRGrabInteractable Grabbable(GameObject go, float mass)
        {
            XRGrabInteractable grab = SetupGrabbable(go, mass);
            // Very thin items (card, coins, shirts) get a slightly thicker box so they're easy to pick up.
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

        // ------------------------------------------------------------------
        // Card and coins (copies of Sakura Park's, so the model counts once, in the park)
        // ------------------------------------------------------------------
        /// <summary>A copy of the park's credit card (mesh + materials), parked (hidden) at <paramref name="worldPos"/>.</summary>
        private static GameObject BuildCard(Transform parent, Transform source, Vector3 worldPos)
        {
            GameObject go = FindOrCreatePropObject(parent, "CreditCard", worldPos);
            go.transform.position = worldPos; // the step moves it into the hip pocket
            if (source != null)
            {
                CopyMesh(source, go);
                go.transform.rotation = source.rotation;
                go.transform.localScale = source.lossyScale;
            }
            else
            {
                Material blue = GetOrCreateMaterial("Assets/Materials/LaundryCard.mat", "Universal Render Pipeline/Lit", new Color(0.15f, 0.3f, 0.75f));
                CopyPrimitiveMesh(PrimitiveType.Cube, go, blue);
                go.transform.localScale = new Vector3(0.085f, 0.003f, 0.054f);
            }
            return go;
        }

        /// <summary>A little stack of three of the park's coins, grabbed as one. The stack is rebuilt each run.</summary>
        private static GameObject BuildCoins(Transform parent, Transform source, Vector3 worldPos)
        {
            GameObject go = FindOrCreatePropObject(parent, "Coins", worldPos);
            for (int i = go.transform.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);

            Vector3 step;
            for (int i = 0; i < 3; i++)
            {
                var coin = new GameObject("Coin_" + i);
                coin.transform.SetParent(go.transform, false);
                if (source != null)
                {
                    CopyMesh(source, coin);
                    coin.transform.rotation = source.rotation;
                    coin.transform.localScale = source.lossyScale;
                    Bounds b = source.GetComponent<Renderer>().bounds; // thinnest world axis = stacking direction
                    step = b.size.y <= b.size.x && b.size.y <= b.size.z ? Vector3.up * b.size.y
                         : b.size.x <= b.size.z ? Vector3.right * b.size.x : Vector3.forward * b.size.z;
                }
                else
                {
                    Material gold = GetOrCreateMaterial("Assets/Materials/LaundryCoin.mat", "Universal Render Pipeline/Lit",
                        new Color(0.85f, 0.7f, 0.25f), 0.7f, 0.8f);
                    CopyPrimitiveMesh(PrimitiveType.Cylinder, coin, gold);
                    coin.transform.localScale = new Vector3(0.026f, 0.001f, 0.026f); // the cylinder is 2 units tall
                    step = Vector3.up * 0.002f;
                }
                coin.transform.position = go.transform.position + step * i + Vector3.Cross(step.normalized, Vector3.forward) * (0.002f * (i % 2));
            }
            return go;
        }

        private static GameObject FindOrCreatePropObject(Transform parent, string name, Vector3 worldPos)
        {
            Transform t = parent.Find(name);
            if (t != null) return t.gameObject;
            GameObject go = FindOrCreate(parent, name);
            go.transform.position = worldPos;
            return go;
        }

        private static void CopyMesh(Transform source, GameObject target)
        {
            MeshFilter mf = GetOrAdd<MeshFilter>(target);
            MeshFilter srcFilter = source.GetComponent<MeshFilter>();
            if (srcFilter != null) mf.sharedMesh = srcFilter.sharedMesh;
            MeshRenderer mr = GetOrAdd<MeshRenderer>(target);
            Renderer src = source.GetComponent<Renderer>();
            if (src != null) mr.sharedMaterials = src.sharedMaterials;
        }

        private static void CopyPrimitiveMesh(PrimitiveType type, GameObject target, Material mat)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            GetOrAdd<MeshFilter>(target).sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            GetOrAdd<MeshRenderer>(target).sharedMaterial = mat;
            Object.DestroyImmediate(temp);
        }

        // ------------------------------------------------------------------
        // Change machine
        // ------------------------------------------------------------------
        /// <summary>The right-hand one of the two grey plates under CHANGE (the left one has the note slit).</summary>
        private static ItemTrigger CardPad(Transform parent, BuildingFrame frame, Renderer machine, Vector3 frontW)
        {
            if (!FrontFaces(machine, "Chrome", frontW, frame, out Bounds plates))
            {
                Debug.LogError($"[Laundromat Setup] Couldn't find the grey plates (material 'Chrome') on {machine.name}'s front.");
                return null;
            }
            Vector3 frontL = frame.LocalDir(frontW);
            Vector3 rightL = Vector3.Cross(Vector3.up, -frontL);
            float width = Across(plates, frontL);
            const float depth = 0.1f;
            Vector3 center = plates.center + rightL * (width * 0.25f) + frontL * (depth * 0.5f - 0.01f);
            return KeepOrBuildTrigger(parent, "CardPad", frame, center,
                new Vector3(width * 0.5f + 0.03f, plates.size.y + 0.04f, depth), frontW);
        }

        /// <summary>Top of a machine's tray (the model's 'Black' box), where things it gives out land.</summary>
        private static Transform Tray(Transform parent, string name, BuildingFrame frame, Renderer machine, Vector3 frontW)
        {
            Vector3 pos;
            if (MaterialBounds(machine, "Black", frame, out Bounds tray, 0.002f)) // ignore the note slit's tiny black faces
                pos = frame.ToWorld(new Vector3(tray.center.x, tray.max.y + 0.005f, tray.center.z));
            else
            {
                Debug.LogWarning($"[Laundromat Setup] No tray (material 'Black') on {machine.name}; using its front.");
                Bounds b = machine.bounds;
                pos = new Vector3(b.center.x, b.min.y + 0.6f, b.center.z) + frontW * (Extent(frame.BoundsOf(machine.transform), frame.LocalDir(frontW)) + 0.05f);
            }
            return FindOrCreateAnchor(parent, name, pos, Quaternion.LookRotation(-frontW, Vector3.up));
        }

        /// <summary>Text over the change machine's little screen ("Tap card for coins" / "Approved").</summary>
        private static TMP_Text ChangeScreen(Transform parent, BuildingFrame frame, Renderer machine, Vector3 frontW)
        {
            if (!FrontFaces(machine, "Screen", frontW, frame, out Bounds screenB))
            {
                Debug.LogWarning($"[Laundromat Setup] No screen (material 'Screen') on {machine.name}; it won't show messages.");
                return null;
            }
            Vector3 frontL = frame.LocalDir(frontW);
            Transform anchor = FindOrCreateAnchor(parent, "ChangeScreenAnchor", frame.ToWorld(screenB.center + frontL * 0.003f),
                Quaternion.LookRotation(-frontW, Vector3.up));
            Vector2 px = new Vector2(Across(screenB, frontL), screenB.size.y) * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "ChangeScreen", px, false);
            AddRounded(canvas, "Background", s_ScreenDark);
            RectTransform textRt = NewUIChild("Text", canvas);
            Stretch(textRt, new Vector2(8f, 6f), new Vector2(-8f, -6f));
            TextMeshProUGUI text = AddText(textRt, "CHANGE", px.y * 0.3f, TextAlignmentOptions.Center, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = px.y * 0.32f;
            return text;
        }

        private static ItemTrigger PlateTrigger(Transform parent, string name, BuildingFrame frame, Renderer r, string material,
            Vector3 frontW, float depth)
        {
            if (!FrontFaces(r, material, frontW, frame, out Bounds plate))
            {
                Debug.LogError($"[Laundromat Setup] Couldn't find '{material}' on the front of {r.name} for {name}.");
                return null;
            }
            Vector3 frontL = frame.LocalDir(frontW);
            return KeepOrBuildTrigger(parent, name, frame, plate.center + frontL * (depth * 0.5f - 0.01f),
                new Vector3(Across(plate, frontL) + 0.04f, plate.size.y + 0.04f, depth), frontW);
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

        // ------------------------------------------------------------------
        // Basket
        // ------------------------------------------------------------------
        /// <summary>
        /// Makes a container of laundry (basket): an opening volume over it and a floor point inside,
        /// both generated parts that move with it (hand placement kept on re-run).
        /// </summary>
        private static LaundryContainer BuildContainer(Renderer r, List<LaundryItem> accepts)
        {
            // Packs only once its step starts (FillContainerStep / CartLoadStep switch it on), so a shirt
            // brushing past it earlier doesn't get packed.
            var own = new BuildingFrame(r.transform); // the container's own yaw-only frame
            Bounds b = own.BoundsOf(r.transform);
            Transform parts = RebuildParts(r.transform);
            Vector3 lossy = parts.lossyScale;
            Vector3 unscale = new Vector3(1f / Mathf.Max(Mathf.Abs(lossy.x), 1e-4f), 1f / Mathf.Max(Mathf.Abs(lossy.y), 1e-4f),
                1f / Mathf.Max(Mathf.Abs(lossy.z), 1e-4f));

            bool alongX = b.size.x >= b.size.z;
            Quaternion floorRot = alongX ? own.Rot : own.Rot * Quaternion.Euler(0f, 90f, 0f);
            float floorY = InsideFloor(r, own.ToWorld(b.center), b.max.y - b.min.y, own.ToWorld(new Vector3(b.center.x, b.min.y, b.center.z)).y) + 0.01f;
            Vector3 floorW = own.ToWorld(new Vector3(b.center.x, 0f, b.center.z));
            floorW.y = floorY;
            Transform floor = NewPart("Floor", parts, floorW, floorRot);
            floor.localScale = unscale;

            // Opening: from the inside floor to some room above the top, so a shirt let go of over it counts.
            float floorL = own.ToLocal(floorW).y, topL = b.max.y + 0.24f;
            Transform opening = NewPart("Opening", parts, own.ToWorld(new Vector3(b.center.x, (floorL + topL) * 0.5f, b.center.z)), own.Rot);
            opening.localScale = unscale;
            ItemTrigger trigger = opening.gameObject.AddComponent<ItemTrigger>();
            var soT = new SerializedObject(trigger);
            soT.FindProperty("m_Size").vector3Value = new Vector3(b.size.x + 0.04f, topL - floorL, b.size.z + 0.04f);
            soT.FindProperty("m_Tolerance").floatValue = 0f;
            soT.FindProperty("m_LogNearMisses").boolValue = false;
            soT.ApplyModifiedPropertiesWithoutUndo();

            LaundryContainer box = GetOrAdd<LaundryContainer>(r.gameObject);
            var so = new SerializedObject(box);
            so.FindProperty("m_Opening").objectReferenceValue = trigger;
            so.FindProperty("m_Floor").objectReferenceValue = floor;
            SetArray(so.FindProperty("m_Accepts"), accepts);
            SetArray(so.FindProperty("m_OwnColliders"), ShellColliders(r.gameObject, floorY));
            so.FindProperty("m_Accepting").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return box;
        }

        /// <summary>
        /// Replaces a container's solid grab box with a shell: a slab from its bottom up to the inside floor
        /// and four thin walls up to its top. The inside stays free, so a hand reaching in grabs the shirts
        /// there instead of the container. All five boxes grab the container.
        /// </summary>
        private static BoxCollider[] ShellColliders(GameObject go, float floorWorldY)
        {
            const float wall = 0.03f;
            Transform t = go.transform;
            Bounds lb = LocalBounds(t);
            Vector3 upL = t.InverseTransformDirection(Vector3.up);
            int up = Mathf.Abs(upL.x) > Mathf.Abs(upL.y) ? (Mathf.Abs(upL.x) > Mathf.Abs(upL.z) ? 0 : 2) : (Mathf.Abs(upL.y) > Mathf.Abs(upL.z) ? 1 : 2);
            int a = (up + 1) % 3, b = (up + 2) % 3;
            float floorL = t.InverseTransformPoint(new Vector3(t.position.x, floorWorldY, t.position.z))[up];
            float bottom = upL[up] > 0f ? lb.min[up] : lb.max[up];
            float top = upL[up] > 0f ? lb.max[up] : lb.min[up];
            Vector3 lossy = t.lossyScale;
            float wa = wall / Mathf.Max(Mathf.Abs(lossy[a]), 1e-4f), wb = wall / Mathf.Max(Mathf.Abs(lossy[b]), 1e-4f);

            var boxes = new List<BoxCollider>(go.GetComponents<BoxCollider>());
            while (boxes.Count < 5) boxes.Add(Undo.AddComponent<BoxCollider>(go));
            for (int i = boxes.Count - 1; i >= 5; i--)
            {
                Undo.DestroyObjectImmediate(boxes[i]);
                boxes.RemoveAt(i);
            }

            void Set(BoxCollider box, float lo, float hi, float aMin, float aMax, float bMin, float bMax)
            {
                Vector3 c = Vector3.zero, sz = Vector3.zero;
                c[up] = (lo + hi) * 0.5f; sz[up] = Mathf.Max(Mathf.Abs(hi - lo), 0.01f);
                c[a] = (aMin + aMax) * 0.5f; sz[a] = aMax - aMin;
                c[b] = (bMin + bMax) * 0.5f; sz[b] = bMax - bMin;
                box.center = c;
                box.size = sz;
                box.isTrigger = false;
            }
            Set(boxes[0], bottom, floorL, lb.min[a], lb.max[a], lb.min[b], lb.max[b]);
            Set(boxes[1], floorL, top, lb.min[a], lb.min[a] + wa, lb.min[b], lb.max[b]);
            Set(boxes[2], floorL, top, lb.max[a] - wa, lb.max[a], lb.min[b], lb.max[b]);
            Set(boxes[3], floorL, top, lb.min[a], lb.max[a], lb.min[b], lb.min[b] + wb);
            Set(boxes[4], floorL, top, lb.min[a], lb.max[a], lb.max[b] - wb, lb.max[b]);

            XRGrabInteractable grab = go.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                var so = new SerializedObject(grab);
                SetArray(so.FindProperty("m_Colliders"), boxes);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return boxes.ToArray();
        }

        /// <summary>
        /// Height of the inside floor of a container (basket bottom, the cart's lower shelf): the highest
        /// flat face right under its middle, read from the mesh. Falls back to <paramref name="fallbackY"/>.
        /// </summary>
        private static float InsideFloor(Renderer r, Vector3 centerW, float height, float fallbackY)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return fallbackY;
            Vector3[] v = mf.sharedMesh.vertices;
            int[] tris = mf.sharedMesh.triangles;
            Transform t = r.transform;
            var p = new Vector2(centerW.x, centerW.z);
            float best = float.MinValue;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(v[tris[i]]), b = t.TransformPoint(v[tris[i + 1]]), c = t.TransformPoint(v[tris[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f || Mathf.Abs(n.normalized.y) < 0.9f) continue; // flat faces only
                if (!InTriangle(p, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z))) continue;
                float y = (a.y + b.y + c.y) / 3f;
                if (y < centerW.y + height * 0.45f) best = Mathf.Max(best, y); // not the very top (a lid or rim)
            }
            if (best > float.MinValue) return best;
            Debug.LogWarning($"[Laundromat Setup] Couldn't find the inside floor of {r.name}; using its bottom.");
            return fallbackY;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2(p - a, b - a), d2 = Cross2(p - b, c - b), d3 = Cross2(p - c, a - c);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector3 Unscale(Transform parent)
        {
            Vector3 l = parent.lossyScale;
            return new Vector3(1f / Mathf.Max(Mathf.Abs(l.x), 1e-4f), 1f / Mathf.Max(Mathf.Abs(l.y), 1e-4f), 1f / Mathf.Max(Mathf.Abs(l.z), 1e-4f));
        }

        // Container floors and openings are recomputed every run on purpose (no KeepPlacement): a saved
        // pose from an earlier run once kept the cart's floor stuck at its wheels.

        /// <summary>An ItemTrigger as a generated part (world-sized box, -Z toward the player).</summary>
        private static ItemTrigger PartTrigger(Transform parts, string name, Vector3 centerW, Vector3 frontW, Vector3 size)
        {
            Transform t = NewPart(name, parts, centerW, Quaternion.LookRotation(-frontW, Vector3.up));
            t.localScale = Unscale(parts);
            ItemTrigger trigger = t.gameObject.AddComponent<ItemTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("m_Size").vector3Value = size;
            so.FindProperty("m_Tolerance").floatValue = 0f;
            so.FindProperty("m_LogNearMisses").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        // ------------------------------------------------------------------
        // Washers and dryers
        // ------------------------------------------------------------------
        /// <summary>
        /// A washer: its door swings out on its hinge, its drum spins about the front-back axis, and the
        /// drum holds the shirts (a laundry container that only accepts while it's this washer's turn).
        /// </summary>
        private static LaundryMachine BuildWasher(Renderer washer, Vector3 frontW, List<LaundryItem> shirts)
        {
            Transform w = washer.transform;
            Transform doorT = FindDeepChild(w, "WasherDoor" + Suffix(w.name));
            Transform drumT = FindDeepChild(w, "WasherDrum" + Suffix(w.name));
            Renderer drum = drumT != null ? drumT.GetComponent<Renderer>() : null;
            if (doorT == null || drum == null)
            {
                Debug.LogError($"[Laundromat Setup] {w.name} needs a WasherDoor and a WasherDrum child.");
                return null;
            }
            MachineDoor door = SetupMachineDoor(doorT, frontW);

            // Drum: floor low inside it, opening at its mouth (sticking out of the door hole a bit).
            Bounds db = drum.bounds;
            Vector3 axisPos = drumT.position; // modelled on the drum's axis
            float radius = Mathf.Min(Across(db, frontW), db.size.y) * 0.5f;
            float depth = Mathf.Abs(Vector3.Dot(db.size, frontW));
            Vector3 mouth = axisPos + frontW * (Vector3.Dot(db.center - axisPos, frontW) + depth * 0.5f);
            Transform parts = RebuildParts(drumT);
            Transform floor = NewPart("Floor", parts, axisPos - Vector3.up * radius * 0.55f, Quaternion.LookRotation(frontW, Vector3.up));
            floor.localScale = Unscale(parts);
            // Reaches well out of the door: a shirt is about as wide as the hole, so it's often let go of in front.
            ItemTrigger opening = PartTrigger(parts, "Opening", mouth + frontW * 0.12f, frontW,
                new Vector3(radius * 2f + 0.12f, radius * 2f + 0.12f, 0.45f));

            LaundryContainer load = GetOrAdd<LaundryContainer>(drumT.gameObject);
            var soL = new SerializedObject(load);
            soL.FindProperty("m_Opening").objectReferenceValue = opening;
            soL.FindProperty("m_Floor").objectReferenceValue = floor;
            soL.FindProperty("m_TakeWhileHeld").boolValue = true; // pushing a shirt into the door is enough
            SetArray(soL.FindProperty("m_Accepts"), shirts);
            SetArray(soL.FindProperty("m_OwnColliders"), w.GetComponents<Collider>());
            soL.FindProperty("m_Accepting").boolValue = false;
            soL.ApplyModifiedPropertiesWithoutUndo();

            LaundryMachine machine = GetOrAdd<LaundryMachine>(w.gameObject);
            var so = new SerializedObject(machine);
            so.FindProperty("m_Door").objectReferenceValue = door;
            so.FindProperty("m_Drum").objectReferenceValue = drumT;
            so.FindProperty("m_SpinAxis").vector3Value = frontW;
            so.FindProperty("m_Load").objectReferenceValue = load;
            so.FindProperty("m_Opening").objectReferenceValue = opening;
            so.FindProperty("m_RunSound").enumValueIndex = (int)SynthSound.WashLoop;
            so.ApplyModifiedPropertiesWithoutUndo();
            return machine;
        }

        /// <summary>
        /// A dryer: its door (DryerDoor / DryerTopDoor child, origin on the hinge; the drum is a cavity in the
        /// model behind it). Shirts brought into the open door go in. Without a door child the dryer still works, doorless.
        /// </summary>
        private static LaundryMachine BuildDryer(Renderer dryer, Vector3 frontW)
        {
            Transform d = dryer.transform;
            string baseName = d.name.Split('.')[0];
            Transform doorT = FindDeepChild(d, baseName + "Door" + Suffix(d.name));
            Renderer doorR = doorT != null ? doorT.GetComponent<Renderer>() : null;
            Transform parts = RebuildParts(d);
            MachineDoor door = null;
            Vector3 center;
            float diameter;
            if (doorR != null)
            {
                door = SetupMachineDoor(doorT, frontW);
                Bounds b = doorR.bounds;
                diameter = Mathf.Min(Across(b, frontW), b.size.y) * 0.85f;
                float doorDepth = Mathf.Abs(Vector3.Dot(b.size, frontW));
                center = b.center - frontW * (doorDepth * 0.5f); // the drum's mouth, behind the closed door
            }
            else
            {
                Debug.LogWarning($"[Laundromat Setup] {d.name} has no '{baseName}Door' child (re-export Laundromat.fbx). It will work without a door.");
                Bounds b = dryer.bounds;
                center = b.center + frontW * Mathf.Abs(Vector3.Dot(b.extents, frontW));
                diameter = 0.45f;
            }
            ItemTrigger opening = PartTrigger(parts, "Opening", center + frontW * 0.12f, frontW,
                new Vector3(diameter + 0.08f, diameter + 0.08f, 0.32f));

            LaundryMachine machine = GetOrAdd<LaundryMachine>(d.gameObject);
            var so = new SerializedObject(machine);
            so.FindProperty("m_Door").objectReferenceValue = door;
            so.FindProperty("m_Drum").objectReferenceValue = null;
            so.FindProperty("m_Load").objectReferenceValue = null;
            so.FindProperty("m_Opening").objectReferenceValue = opening;
            so.FindProperty("m_RunSound").enumValueIndex = (int)SynthSound.DryerHum;
            so.ApplyModifiedPropertiesWithoutUndo();
            return machine;
        }

        /// <summary>Script-swung door; opens outward, away from the machine's front.</summary>
        private static MachineDoor SetupMachineDoor(Transform door, Vector3 frontW)
        {
            Renderer r = door.GetComponent<Renderer>();
            Vector3 arm = Flat((r != null ? r.bounds.center : door.position) - door.position);
            float angle = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * arm, frontW) > 0f ? 100f : -100f;
            MachineDoor md = GetOrAdd<MachineDoor>(door.gameObject);
            var so = new SerializedObject(md);
            so.FindProperty("m_OpenAngle").floatValue = angle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return md;
        }

        /// <summary>".003" from "WashingMachine.003", "" for an unnumbered name.</summary>
        private static string Suffix(string name)
        {
            int dot = name.IndexOf('.');
            return dot >= 0 ? name.Substring(dot) : string.Empty;
        }

        // ------------------------------------------------------------------
        // Following the booking
        // ------------------------------------------------------------------
        private static void Follow(Transform t, QueueStep queue, Transform reference, bool dryer, bool keepHeight)
        {
            MachineAnchor a = GetOrAdd<MachineAnchor>(t.gameObject);
            var so = new SerializedObject(a);
            so.FindProperty("m_Queue").objectReferenceValue = queue;
            so.FindProperty("m_Dryer").boolValue = dryer;
            so.FindProperty("m_Reference").objectReferenceValue = reference;
            so.FindProperty("m_KeepHeight").boolValue = keepHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A panel anchor placed around machine 1 (your placement is kept) that follows the booking.</summary>
        private static Transform FollowAnchor(Transform parent, string name, Vector3 pos, Quaternion rot, QueueStep queue,
            Transform reference, bool dryer, bool keepHeight)
        {
            Transform t = FindOrCreateAnchor(parent, name, pos, rot);
            Follow(t, queue, reference, dryer, keepHeight);
            return t;
        }

        /// <summary>A "Here!" marker (shown only during its step) on an anchor that follows the booking.</summary>
        private static GameObject FollowMarker(Transform parent, string name, Vector3 pos, Quaternion rot, QueueStep queue,
            Transform reference, bool dryer, bool keepHeight)
        {
            Transform t = FollowAnchor(parent, name, pos, rot, queue, reference, dryer, keepHeight);
            t.localScale = Vector3.one * 0.7f;
            return BuildHereMarker(t);
        }

        // ------------------------------------------------------------------
        // Detergent, bin
        // ------------------------------------------------------------------
        /// <summary>A pouring stream (rebuilt each run): StreamFx plus the thin cylinder it stretches.</summary>
        private static StreamFx BuildStream(Transform parent, string name, float width)
        {
            Transform old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            Material mat = GetOrCreateMaterial("Assets/Materials/LaundryStream.mat", "Universal Render Pipeline/Unlit", Color.white);
            GameObject cyl = Primitive(PrimitiveType.Cylinder, "Cylinder", go.transform, Vector3.zero, Vector3.one, mat);
            cyl.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StreamFx fx = go.AddComponent<StreamFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("m_Cylinder").objectReferenceValue = cyl.transform;
            so.FindProperty("m_Width").floatValue = width;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        /// <summary>The inside of the bin (no extra reach: a bottle leaning on the outside mustn't count).</summary>
        private static ItemTrigger BinTrigger(Transform parent, BuildingFrame frame, Renderer bin, Vector3 frontW)
        {
            Bounds b = frame.BoundsOf(bin.transform);
            Vector3 frontL = frame.LocalDir(frontW);
            ItemTrigger trigger = KeepOrBuildTrigger(parent, "BinOpening", frame, b.center + Vector3.up * (b.size.y * 0.05f),
                new Vector3(Across(b, frontL) * 0.8f, b.size.y * 0.85f, Extent(b, frontL) * 2f * 0.8f), frontW);
            var so = new SerializedObject(trigger);
            so.FindProperty("m_Tolerance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private static Transform NewPart(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go.transform;
        }

        // ------------------------------------------------------------------
        // Queue machine screen and machine number tags
        // ------------------------------------------------------------------
        private struct QueueScreen
        {
            public Button[] Washers;
            public Button[] Dryers;
            public TMP_Text Status;
        }

        /// <summary>
        /// Washer buttons (2 rows) and dryer buttons (top row = top dryers, like the stack) over the
        /// machine's dark glass, with a title and a status line.
        /// </summary>
        private static QueueScreen BuildQueueScreen(Transform anchor, Vector2 sizeM, int washers, int dryers)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "QueueScreen", px, true);
            AddRounded(canvas, "Background", s_ScreenDark);

            Label(canvas, "Title", "LAUNDRY QUEUE", 0.89f, 0.97f, px.y * 0.05f, new Color(0.55f, 0.8f, 1f));
            Label(canvas, "WasherLabel", "Washer", 0.81f, 0.87f, px.y * 0.04f, Color.white);
            var result = new QueueScreen
            {
                Washers = ButtonGrid(canvas, "Washer", washers, 4, 0.53f, 0.8f, s_WasherBlue, px, false),
            };
            Label(canvas, "DryerLabel", "Dryer", 0.44f, 0.5f, px.y * 0.04f, Color.white);
            result.Dryers = ButtonGrid(canvas, "Dryer", dryers, 3, 0.17f, 0.43f, s_DryerOrange, px, true);

            RectTransform status = NewUIChild("Status", canvas);
            status.anchorMin = new Vector2(0.05f, 0.02f);
            status.anchorMax = new Vector2(0.95f, 0.15f);
            status.offsetMin = status.offsetMax = Vector2.zero;
            TextMeshProUGUI statusText = AddText(status, "Welcome!", px.y * 0.035f, TextAlignmentOptions.Center);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 10f;
            statusText.fontSizeMax = px.y * 0.04f;
            result.Status = statusText;
            return result;
        }

        private static void Label(RectTransform canvas, string name, string text, float yMin, float yMax, float size, Color color)
        {
            RectTransform rt = NewUIChild(name, canvas);
            rt.anchorMin = new Vector2(0.05f, yMin);
            rt.anchorMax = new Vector2(0.95f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            TextMeshProUGUI t = AddText(rt, text, size, TextAlignmentOptions.Center, FontStyles.Bold);
            t.color = color;
        }

        /// <summary>Numbered buttons, <paramref name="perRow"/> per row; with bottomFirst, 1..perRow is the bottom row.</summary>
        private static Button[] ButtonGrid(RectTransform canvas, string name, int count, int perRow, float yMin, float yMax,
            Color color, Vector2 px, bool bottomFirst)
        {
            var buttons = new Button[count];
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
            float gap = 0.025f;
            float h = (yMax - yMin - gap * (rows - 1)) / rows;
            float w = (0.9f - gap * (perRow - 1)) / perRow;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int fromTop = bottomFirst ? rows - 1 - row : row;
                float top = yMax - fromTop * (h + gap);
                float left = 0.05f + col * (w + gap);
                RectTransform slot = NewUIChild($"{name}_{i + 1}", canvas);
                slot.anchorMin = new Vector2(left, top - h);
                slot.anchorMax = new Vector2(left + w, top);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                buttons[i] = AddButton(slot, (i + 1).ToString(), color, Mathf.Min(px.y * h * 0.55f, px.x * w * 0.5f));
            }
            return buttons;
        }

        /// <summary>
        /// A number badge on a machine: above a washer's top, or on a dryer's control strip (dryers are stacked).
        /// Its anchor is created once, so a tag you've moved keeps its place.
        /// </summary>
        private static Image BuildTag(Transform parent, string name, Renderer machine, BuildingFrame frame, Vector3 front,
            int number, Color color, bool onFront)
        {
            Vector3 frontL = frame.LocalDir(front);
            Bounds b = frame.BoundsOf(machine.transform);
            Vector3 rightL = Vector3.Cross(Vector3.up, -frontL);
            Vector3 posL = onFront
                ? new Vector3(b.center.x, b.max.y - 0.1f, b.center.z) + frontL * (Extent(b, frontL) + 0.012f) + rightL * 0.05f
                : new Vector3(b.center.x, b.max.y + 0.1f, b.center.z) + frontL * (Extent(b, frontL) - 0.05f);
            Transform anchor = FindOrCreateAnchor(parent, name, frame.ToWorld(posL), Quaternion.LookRotation(-front, Vector3.up));
            RectTransform canvas = NewWorldCanvas(anchor, "Tag", new Vector2(110f, 110f), false);
            Image bg = AddRounded(canvas, "Background", color);
            RectTransform textRt = NewUIChild("Number", bg.rectTransform);
            Stretch(textRt, Vector2.zero, Vector2.zero);
            AddText(textRt, number.ToString(), 80f, TextAlignmentOptions.Center, FontStyles.Bold);
            return bg;
        }
    }
}
