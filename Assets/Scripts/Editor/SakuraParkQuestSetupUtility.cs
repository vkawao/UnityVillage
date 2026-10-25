using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static VRVillage.Editor.QuestBuildKit;

namespace VRVillage.Editor
{
    /// <summary>
    /// Builds the Sakura Park (vending corner) quest in the Village scene:
    ///   SakuraParkQuest (QuestManager, QuestHideAtStart)
    ///     QuestPanel, Anchors/, Steps/, FX/, Guide/ (as in the Guitar Shop)
    ///     Triggers/      mail slot, coin slot, card reader, bin openings
    ///     Machines/      vending buttons, smart-machine screen, tray points
    ///     Bench/         seat point, teleport anchor, "Sit down" button
    ///     ScreenFader    black blink used while sitting down
    ///     HipHolster     hip pocket that follows the player (credit card)
    /// The park model (VendingMachinePark.blend) stays a linked prefab instance, so Blender
    /// edits keep flowing in. Features (slots, screen, plates) are found by material name.
    /// Safe to re-run: anchors and triggers you've moved keep their placement.
    /// </summary>
    public static class SakuraParkQuestSetupUtility
    {
        private const string RootName = "SakuraParkQuest";
        private const string ParkName = "VendingMachinePark";

        private static readonly string[] s_ShortTexts =
        {
            "Post the mail in the mailbox",
            "Put the coin in the vending machine",
            "Pick a drink on the vending machine",
            "Grab your drink and sit on the bench",
            "Drink up! Raise the can to your mouth",
            "Throw the can in the Recycle bin",
            "Pick a snack on the smart machine's screen",
            "Tap the card from your hip on the reader",
            "Eat up! Raise the bag to your mouth",
            "Throw the bag in the General bin",
        };

        private static readonly Color s_Coke = new Color(0.85f, 0.1f, 0.12f);
        private static readonly Color s_Pepsi = new Color(0.1f, 0.25f, 0.75f);
        private static readonly Color s_Sprite = new Color(0.15f, 0.62f, 0.25f);
        private static readonly Color s_ChipsYellow = new Color(0.98f, 0.8f, 0.1f);
        private static readonly Color s_ChipsTeal = new Color(0.3f, 0.78f, 0.72f);
        private static readonly Color s_ChipsGreen = new Color(0.25f, 0.6f, 0.2f);

        [MenuItem("VR Village/Setup Sakura Park Quest")]
        public static void Setup()
        {
            if (EditorSceneManager.GetActiveScene().name != "Village")
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/Village.unity", OpenSceneMode.Single);
            }

            GameObject park = FindInScene(ParkName);
            if (park == null)
            {
                Debug.LogError($"[Park Setup] '{ParkName}' not found in the scene. Drag VendingMachinePark.blend into the scene first.");
                return;
            }

            // Features are located from mesh data, so the model must be readable.
            if (EnsureReadable(Need<Renderer>(park.transform, "VendingMachine")))
                park = FindInScene(ParkName);

            Transform p = park.transform;
            Renderer mailbox = Need<Renderer>(p, "Mailbox");
            Renderer vending = Need<Renderer>(p, "VendingMachine");
            Renderer smart = Need<Renderer>(p, "SmartMachine");
            Renderer recycle = Need<Renderer>(p, "RecyeleBin") ?? Need<Renderer>(p, "RecycleBin");
            Renderer general = Need<Renderer>(p, "TrashBin");
            Transform gate = FindDeepChild(p, "Gate");
            Transform grass = FindDeepChild(p, "Grass");
            var benches = new List<Transform>();
            foreach (string n in new[] { "Bench", "Bench.001" })
            {
                Transform t = FindDeepChild(p, n);
                if (t != null) benches.Add(t);
            }
            GameObject mail = FindDeepChild(p, "Mail")?.gameObject;
            GameObject coin = FindDeepChild(p, "Coin")?.gameObject;
            GameObject card = FindDeepChild(p, "CreditCard")?.gameObject;
            var cans = FindAll(p, "Can", "Can.001", "Can.002");
            var bags = FindAll(p, "BagOfChips", "BagOfChips.001", "BagOfChips.002");
            if (mailbox == null || vending == null || smart == null || recycle == null || general == null || gate == null ||
                grass == null || benches.Count == 0 || mail == null || coin == null || card == null || cans == null || bags == null)
            {
                Debug.LogError("[Park Setup] Some park objects are missing (see the errors above). Nothing was changed.");
                return;
            }

            EnsureEventSystem();

            // ---------- Colliders on scenery (so things can be dropped and land) ----------
            foreach (string n in new[] { "Grass", "GravelPath", "ConcretePad", "ConcretePad.001", "Bench", "Bench.001",
                         "VendingMachine", "SmartMachine", "Mailbox", "RecyeleBin", "RecycleBin", "TrashBin", "BulletinBoard" })
            {
                Transform t = FindDeepChild(p, n);
                if (t != null) EnsureStaticCollider(t.gameObject);
            }
            float grassTop = RendererBounds(grass).max.y;
            if (Mathf.Abs(grassTop) > 0.1f)
                Debug.LogWarning($"[Park Setup] The park's grass is at y = {grassTop:0.00} m but the village floor is at 0. " +
                                 "Move VendingMachinePark so its grass sits on the floor, then re-run this setup.");

            // ---------- Grabbable items ----------
            XRGrabInteractable mailGrab = Grabbable(mail, 0.05f);
            XRGrabInteractable coinGrab = Grabbable(coin, 0.02f);
            XRGrabInteractable cardGrab = Grabbable(card, 0.02f);
            var canGrabs = new List<XRGrabInteractable>();
            foreach (GameObject c in cans) canGrabs.Add(Grabbable(c, 0.35f));
            var bagGrabs = new List<XRGrabInteractable>();
            foreach (GameObject b in bags) bagGrabs.Add(Grabbable(b, 0.1f));

            // Flavours follow the button order: Coca Cola = red, Pepsi = blue, Sprite = green,
            // whatever colour each can had in Blender.
            string[] canColours = { "CanRed", "CanBlue", "CanGreen" };
            for (int i = 0; i < cans.Count; i++)
            {
                Material target = FindMaterial(vending, canColours[i]);
                foreach (string slot in canColours) ReplaceMaterial(cans[i].GetComponent<Renderer>(), slot, target);
            }
            // Bags: 1 = Original (yellow, as modelled), 2 = Sour Cream (teal), 3 = Seaweed (green).
            ReplaceMaterial(bags[1].GetComponent<Renderer>(), "ChipsYellow",
                GetOrCreateMaterial("Assets/Materials/QuestChipsTeal.mat", "Universal Render Pipeline/Lit", s_ChipsTeal));
            ReplaceMaterial(bags[2].GetComponent<Renderer>(), "ChipsYellow",
                GetOrCreateMaterial("Assets/Materials/QuestChipsGreen.mat", "Universal Render Pipeline/Lit", s_ChipsGreen));

            // ---------- Hierarchy ----------
            GameObject root = FindOrCreate(null, RootName);
            Transform anchors = FindOrCreate(root.transform, "Anchors").transform;
            Transform steps = FindOrCreate(root.transform, "Steps").transform;
            Transform fx = FindOrCreate(root.transform, "FX").transform;
            Transform triggers = FindOrCreate(root.transform, "Triggers").transform;
            Transform machines = FindOrCreate(root.transform, "Machines").transform;
            Transform benchRoot = FindOrCreate(root.transform, "Bench").transform;

            QuestManager manager = GetOrAdd<QuestManager>(root);
            QuestPanel panel = BuildPanel(root.transform);
            panel.transform.localScale = Vector3.one * 0.00075f; // ~1.2 m wide: a park is more compact than the shop

            var hide = GetOrAdd<QuestHideAtStart>(root);
            var soHide = new SerializedObject(hide);
            var hidden = new List<Object>();
            hidden.AddRange(cans);
            hidden.AddRange(bags);
            hidden.Add(card);
            SetArray(soHide.FindProperty("m_Hide"), hidden);
            soHide.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Geometry ----------
            var frame = new BuildingFrame(p);
            Vector3 frontW = frame.Snap(RendererBounds(gate).center - vending.bounds.center); // machines face the gate
            Vector3 frontL = frame.LocalDir(frontW);
            Quaternion facePlayer = Quaternion.LookRotation(-frontW, Vector3.up); // panels read from in front of the machines

            Bounds vmB = frame.BoundsOf(vending.transform);
            Bounds smB = frame.BoundsOf(smart.transform);
            Bounds mailB = frame.BoundsOf(mailbox.transform);
            Bounds recB = frame.BoundsOf(recycle.transform);
            Bounds genB = frame.BoundsOf(general.transform);
            Bounds grassB = frame.BoundsOf(grass);
            float groundL = grassB.max.y;

            // ---------- Slots and readers ----------
            ItemTrigger mailSlot = MailSlot(triggers, frame, mailbox, frontW, frontL);
            ItemTrigger coinSlot = PlateTrigger(triggers, "CoinSlot", frame, vending, "BrushedSteel", frontW, frontL, 0.08f);
            ItemTrigger reader = PlateTrigger(triggers, "CardReader", frame, smart, "BrushedSteel", frontW, frontL, 0.1f);
            ItemTrigger recycleBin = BinTrigger(triggers, "RecycleBinOpening", frame, recB, frontW, frontL);
            ItemTrigger generalBin = BinTrigger(triggers, "GeneralBinOpening", frame, genB, frontW, frontL);
            if (mailSlot == null || coinSlot == null || reader == null) return;

            Transform vmOutlet = Outlet(machines, "VendingTray", frame, vending, frontW);
            Transform smOutlet = Outlet(machines, "SnackTray", frame, smart, frontW);

            // ---------- Vending machine buttons (on its white panel) ----------
            if (!FrontFaces(vending, "SmartMachine", frontW, frame, out Bounds whiteB))
            {
                Debug.LogError("[Park Setup] Couldn't find the white panel (material 'SmartMachine') on VendingMachine's front.");
                return;
            }
            Transform vmPanelAnchor = FindOrCreateAnchor(machines, "VendingButtonsAnchor",
                frame.ToWorld(whiteB.center + frontL * 0.004f), facePlayer);
            Button[] drinkButtons = BuildDrinkButtons(vmPanelAnchor, new Vector2(Across(whiteB, frontL), whiteB.size.y));

            // ---------- Smart machine screen ----------
            if (!FrontFaces(smart, "TealScreen", frontW, frame, out Bounds screenB))
            {
                Debug.LogError("[Park Setup] Couldn't find the screen (material 'TealScreen') on SmartMachine's front.");
                return;
            }
            Transform screenAnchor = FindOrCreateAnchor(machines, "SmartScreenAnchor",
                frame.ToWorld(screenB.center + frontL * 0.004f), facePlayer);
            SmartMachineScreen screen = BuildSmartScreen(screenAnchor, new Vector2(Across(screenB, frontL), screenB.size.y));

            // ---------- Bench: seat, teleport anchor, marker ----------
            Transform bench = Nearest(benches, vending.bounds.center) ?? benches[0];
            Bounds bb = frame.BoundsOf(bench);
            Vector3 benchL = new Vector3(bb.center.x, bb.min.y, bb.center.z);
            // Where the player ends up: just in front of the seat edge (feet/head of a seated person),
            // so the body never lands inside the bench's collider. Faces the machines.
            Vector3 seatL = benchL - frontL * (Extent(bb, frontL) + 0.16f);
            Transform seat = FindOrCreateAnchor(benchRoot, "BenchSeat", frame.ToWorld(seatL), facePlayer);
            BuildTeleportAnchor(benchRoot, frame, bb, frontL, seat);
            Transform sitAnchor = FindOrCreateAnchor(benchRoot, "SitMarkerAnchor",
                frame.ToWorld(benchL + Vector3.up * 1.05f), Quaternion.LookRotation(frontW, Vector3.up));
            Transform oldMarker = sitAnchor.Find("HereMarker"); // replaced by the Sit down button
            if (oldMarker != null) Undo.DestroyObjectImmediate(oldMarker.gameObject);
            Button sitButton = BuildButtonCanvas(sitAnchor, "SitButton", new Vector2(420f, 150f), "Sit down", ButtonGreen, 64f);
            GameObject sitMarker = sitButton.transform.parent.gameObject;
            ScreenFader fader = BuildScreenFader(root.transform);

            // Where the can rolls to: most of the way from the tray toward the bench, on the ground.
            Vector3 trayL = frame.ToLocal(vmOutlet.position);
            Vector3 rollL = trayL + (seatL - trayL) * 0.6f;
            rollL.y = groundL;
            Vector3 rollW = GroundAt(frame.ToWorld(rollL));
            Transform rollTarget = FindOrCreateAnchor(machines, "CanRollTarget", rollW, Quaternion.identity);

            // ---------- Hip holster ----------
            BuildGuide(root.transform, frame, grassB, GuideMargin(grassB, frame, root.transform));
            QuestGuide guide = root.transform.Find("Guide").GetComponent<QuestGuide>();
            HipHolster holster = BuildHolster(root.transform, guide, new[] { cardGrab, coinGrab, mailGrab });

            // ---------- Panel anchors ----------
            Transform a1 = FindOrCreateAnchor(anchors, "QuestAnchor_1_Mailbox",
                frame.ToWorld(Flat(mailB.center) + frontL * (Extent(mailB, frontL) + 0.35f) + Vector3.up * (mailB.max.y + 0.5f)), facePlayer);
            Vector3 coinW = coin.transform.position;
            Vector3 awayFromGate = frame.Snap(coinW - RendererBounds(gate).center);
            Transform a2 = FindOrCreateAnchor(anchors, "QuestAnchor_2_Coin",
                coinW + Vector3.up * 1.1f - awayFromGate * 0.3f, Quaternion.LookRotation(awayFromGate, Vector3.up));
            Vector3 toBins = frame.LocalDir(frame.Snap(frame.ToWorld(recB.center) - frame.ToWorld(vmB.center)));
            Transform a3 = FindOrCreateAnchor(anchors, "QuestAnchor_3_PickDrink",
                frame.ToWorld(Flat(vmB.center) + toBins * (Extent(vmB, toBins) + 0.75f) + frontL * 0.5f + Vector3.up * (vmB.min.y + 1.6f)), facePlayer);
            Transform a4 = FindOrCreateAnchor(anchors, "QuestAnchor_4_Bench",
                frame.ToWorld(seatL + Vector3.up * (bb.size.y + 0.9f)), Quaternion.LookRotation(frontW, Vector3.up));
            Transform a5 = FindOrCreateAnchor(anchors, "QuestAnchor_5_Drink",
                frame.ToWorld(seatL - frontL * 1.8f + Vector3.up * 1.35f), facePlayer);
            Transform a6 = FindOrCreateAnchor(anchors, "QuestAnchor_6_RecycleBin",
                frame.ToWorld(Flat(recB.center) + frontL * 0.15f + Vector3.up * (recB.max.y + 0.6f)), facePlayer);
            Vector3 awayFromVm = -toBins;
            Vector3 besideSmartL = Flat(smB.center) + awayFromVm * (Extent(smB, awayFromVm) + 0.75f) + frontL * 0.5f + Vector3.up * (smB.min.y + 1.6f);
            Transform a7 = FindOrCreateAnchor(anchors, "QuestAnchor_7_Snack", frame.ToWorld(besideSmartL), facePlayer);
            Transform a8 = FindOrCreateAnchor(anchors, "QuestAnchor_8_Card", frame.ToWorld(besideSmartL), facePlayer);
            Transform a9 = FindOrCreateAnchor(anchors, "QuestAnchor_9_Eat", frame.ToWorld(besideSmartL), facePlayer);
            Transform a10 = FindOrCreateAnchor(anchors, "QuestAnchor_10_GeneralBin",
                frame.ToWorld(Flat(genB.center) + frontL * 0.15f + Vector3.up * (genB.max.y + 0.6f)), facePlayer);

            QuestCelebration confetti = BuildCelebration(fx, "Celebration_GeneralBin",
                frame.ToWorld(Flat(genB.center) + Vector3.up * (genB.max.y + 0.35f)), CelebrationStyle.ConfettiStyle());

            // ---------- Steps ----------
            InsertItemStep step1 = GetOrAdd<InsertItemStep>(FindOrCreate(steps, "Step1_PostMail"));
            var so1 = StepSO(step1, "Someone left their mail. Let's put it into the Mailbox.", a1);
            so1.FindProperty("m_Item").objectReferenceValue = mailGrab;
            so1.FindProperty("m_Slot").objectReferenceValue = mailSlot;
            so1.FindProperty("m_Sound").enumValueIndex = (int)SynthSound.Swish;
            so1.ApplyModifiedPropertiesWithoutUndo();

            InsertItemStep step2 = GetOrAdd<InsertItemStep>(FindOrCreate(steps, "Step2_InsertCoin"));
            var so2 = StepSO(step2, "Your karma has rewarded you! You found a spare coin. Get yourself a drink.", a2);
            so2.FindProperty("m_Item").objectReferenceValue = coinGrab;
            so2.FindProperty("m_Slot").objectReferenceValue = coinSlot;
            so2.FindProperty("m_Sound").enumValueIndex = (int)SynthSound.CoinDrop;
            so2.ApplyModifiedPropertiesWithoutUndo();

            VendingChoiceStep step3 = GetOrAdd<VendingChoiceStep>(FindOrCreate(steps, "Step3_PickDrink"));
            var so3 = StepSO(step3, "Pick your drink!", a3);
            SetArray(so3.FindProperty("m_Buttons"), drinkButtons);
            SetArray(so3.FindProperty("m_Cans"), canGrabs);
            so3.FindProperty("m_Outlet").objectReferenceValue = vmOutlet;
            so3.FindProperty("m_RollTarget").objectReferenceValue = rollTarget;
            so3.ApplyModifiedPropertiesWithoutUndo();

            SitStep step4 = GetOrAdd<SitStep>(FindOrCreate(steps, "Step4_SitOnBench"));
            var so4 = StepSO(step4, "Grab your drink and relax on the bench.", a4, null, new[] { sitMarker });
            so4.FindProperty("m_Seat").objectReferenceValue = seat;
            so4.FindProperty("m_SitButton").objectReferenceValue = sitButton;
            so4.FindProperty("m_Fader").objectReferenceValue = fader;
            so4.FindProperty("m_SnapRadius").floatValue = 0f;
            so4.ApplyModifiedPropertiesWithoutUndo();

            ConsumeStep step5 = GetOrAdd<ConsumeStep>(FindOrCreate(steps, "Step5_Drink"));
            var so5 = StepSO(step5, "Drink up!", a5);
            so5.FindProperty("m_ItemSource").objectReferenceValue = step3;
            so5.FindProperty("m_Mode").enumValueIndex = (int)ConsumeStep.Mode.Drink;
            so5.ApplyModifiedPropertiesWithoutUndo();

            DisposeItemStep step6 = GetOrAdd<DisposeItemStep>(FindOrCreate(steps, "Step6_RecycleCan"));
            var so6 = StepSO(step6, "Throw your empty can away in the Recycling bin", a6);
            so6.FindProperty("m_ItemSource").objectReferenceValue = step3;
            so6.FindProperty("m_CorrectBin").objectReferenceValue = recycleBin;
            SetArray(so6.FindProperty("m_WrongBins"), new[] { generalBin });
            so6.FindProperty("m_WrongHint").stringValue = "That's the General bin. Cans go in the blue Recycle bin!";
            so6.FindProperty("m_Sound").enumValueIndex = (int)SynthSound.CanDrop;
            so6.ApplyModifiedPropertiesWithoutUndo();

            SnackChoiceStep step7 = GetOrAdd<SnackChoiceStep>(FindOrCreate(steps, "Step7_PickSnack"));
            var so7 = StepSO(step7, "There's a new machine! Let's try it out.", a7);
            so7.FindProperty("m_Screen").objectReferenceValue = screen;
            SetArray(so7.FindProperty("m_Bags"), bagGrabs);
            so7.ApplyModifiedPropertiesWithoutUndo();

            CardPaymentStep step8 = GetOrAdd<CardPaymentStep>(FindOrCreate(steps, "Step8_PayByCard"));
            var so8 = StepSO(step8, "Pay for it with your credit card.", a8);
            so8.FindProperty("m_Screen").objectReferenceValue = screen;
            so8.FindProperty("m_Snack").objectReferenceValue = step7;
            so8.FindProperty("m_Holster").objectReferenceValue = holster;
            so8.FindProperty("m_Card").objectReferenceValue = cardGrab;
            so8.FindProperty("m_Reader").objectReferenceValue = reader;
            so8.FindProperty("m_Outlet").objectReferenceValue = smOutlet;
            so8.ApplyModifiedPropertiesWithoutUndo();

            ConsumeStep step9 = GetOrAdd<ConsumeStep>(FindOrCreate(steps, "Step9_EatChips"));
            var so9 = StepSO(step9, "Eat up!", a9);
            so9.FindProperty("m_ItemSource").objectReferenceValue = step7;
            so9.FindProperty("m_Mode").enumValueIndex = (int)ConsumeStep.Mode.Eat;
            so9.ApplyModifiedPropertiesWithoutUndo();

            DisposeItemStep step10 = GetOrAdd<DisposeItemStep>(FindOrCreate(steps, "Step10_BinBag"));
            var so10 = StepSO(step10, "Throw your empty bag of chips into a normal bin", a10, confetti);
            so10.FindProperty("m_ItemSource").objectReferenceValue = step7;
            so10.FindProperty("m_CorrectBin").objectReferenceValue = generalBin;
            SetArray(so10.FindProperty("m_WrongBins"), new[] { recycleBin });
            so10.FindProperty("m_WrongHint").stringValue = "That's the Recycle bin. Snack bags go in the white General bin!";
            so10.ApplyModifiedPropertiesWithoutUndo();

            var allSteps = new List<Object> { step1, step2, step3, step4, step5, step6, step7, step8, step9, step10 };
            SetGuideTexts(allSteps, s_ShortTexts, new Dictionary<Object, Transform>
            {
                { step3, vmPanelAnchor },
                { step7, screenAnchor },
            });

            var soM = new SerializedObject(manager);
            soM.FindProperty("m_Panel").objectReferenceValue = panel;
            SetArray(soM.FindProperty("m_Steps"), allSteps);
            soM.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("<color=green>[Park Setup] Sakura Park quest ready: actions 1-10.</color>");
        }

        // ------------------------------------------------------------------
        // Finding things
        // ------------------------------------------------------------------
        private static T Need<T>(Transform root, string name) where T : Component
        {
            Transform t = FindDeepChild(root, name);
            T c = t != null ? t.GetComponent<T>() : null;
            if (c == null && name != "RecyeleBin") Debug.LogError($"[Park Setup] '{name}' not found under {root.name}.");
            return c;
        }

        private static List<GameObject> FindAll(Transform root, params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string n in names)
            {
                Transform t = FindDeepChild(root, n);
                if (t == null)
                {
                    Debug.LogError($"[Park Setup] '{n}' not found under {root.name}.");
                    return null;
                }
                list.Add(t.gameObject);
            }
            return list;
        }

        private static XRGrabInteractable Grabbable(GameObject go, float mass)
        {
            XRGrabInteractable grab = SetupGrabbable(go, mass);
            // Very thin items (coin, card, mail) get a slightly thicker box so they're easy to pick up.
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

        /// <summary>Raycast down to the first collider below; falls back to the given point.</summary>
        private static Vector3 GroundAt(Vector3 world)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(world + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : world;
        }

        /// <summary>Guide area margin around the park: 1 m, or 0 if that would reach into another house's area.</summary>
        private static float GuideMargin(Bounds grassL, BuildingFrame frame, Transform ownRoot)
        {
            const float margin = 1f;
            var others = Object.FindObjectsByType<QuestGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Bounds mine = new Bounds(frame.ToWorld(grassL.center), grassL.size + new Vector3(margin * 2f, 5f, margin * 2f));
            foreach (QuestGuide g in others)
            {
                if (g.transform.IsChildOf(ownRoot)) continue; // our own guide from a previous run
                var so = new SerializedObject(g);
                var area = so.FindProperty("m_Area").objectReferenceValue as Transform;
                if (area == null) continue;
                Vector3 size = so.FindProperty("m_AreaSize").vector3Value;
                var theirs = new Bounds(area.position, Vector3.zero);
                foreach (Vector3 c in new[] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1) })
                    theirs.Encapsulate(area.TransformPoint(Vector3.Scale(c, size * 0.5f)));
                theirs.Expand(new Vector3(0f, 10f, 0f));
                if (theirs.Intersects(mine))
                {
                    Debug.LogWarning($"[Park Setup] The park's guide area would overlap '{g.transform.parent.name}'. Using no margin.");
                    return 0f;
                }
            }
            return margin;
        }

        // ------------------------------------------------------------------
        // Triggers
        // ------------------------------------------------------------------
        private static ItemTrigger MailSlot(Transform parent, BuildingFrame frame, Renderer mailbox, Vector3 frontW, Vector3 frontL)
        {
            if (!FrontFaces(mailbox, "BlackTorii", frontW, frame, out Bounds slot) &&
                !FrontFaces(mailbox, "MailboxBlack", frontW, frame, out slot))
            {
                Debug.LogError("[Park Setup] Couldn't find the mail slot (material 'BlackTorii') on Mailbox's front.");
                return null;
            }
            float slotPlane = Vector3.Dot(slot.center, frontL);
            float frontPlane = FrontFaces(mailbox, "MailboxRed", frontW, frame, out Bounds red)
                ? Mathf.Max(Vector3.Dot(red.center, frontL), slotPlane)
                : slotPlane;
            float lo = slotPlane - 0.03f, hi = frontPlane + 0.06f;
            Vector3 center = slot.center + frontL * ((lo + hi) * 0.5f - slotPlane);
            return KeepOrBuildTrigger(parent, "MailSlot", frame, center,
                new Vector3(Across(slot, frontL) + 0.04f, slot.size.y + 0.04f, hi - lo), frontW);
        }

        private static ItemTrigger PlateTrigger(Transform parent, string name, BuildingFrame frame, Renderer r, string material,
            Vector3 frontW, Vector3 frontL, float depth)
        {
            if (!FrontFaces(r, material, frontW, frame, out Bounds plate))
            {
                Debug.LogError($"[Park Setup] Couldn't find '{material}' on the front of {r.name} for {name}.");
                return null;
            }
            return KeepOrBuildTrigger(parent, name, frame, plate.center + frontL * (depth * 0.5f - 0.01f),
                new Vector3(Across(plate, frontL) + 0.04f, plate.size.y + 0.04f, depth), frontW);
        }

        private static ItemTrigger BinTrigger(Transform parent, string name, BuildingFrame frame, Bounds bin, Vector3 frontW, Vector3 frontL)
        {
            Vector3 center = bin.center + Vector3.up * (bin.size.y * 0.05f);
            ItemTrigger trigger = KeepOrBuildTrigger(parent, name, frame, center,
                new Vector3(Across(bin, frontL) * 0.8f, bin.size.y * 0.85f, Extent(bin, frontL) * 2f * 0.8f), frontW);
            // No extra reach for bins: an item leaning on the outside wall mustn't count as "in".
            var so = new SerializedObject(trigger);
            so.FindProperty("m_Tolerance").floatValue = 0f;
            so.FindProperty("m_LogNearMisses").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        /// <summary>A trigger you've moved keeps its place; a new one is built where the model's feature is.</summary>
        private static ItemTrigger KeepOrBuildTrigger(Transform parent, string name, BuildingFrame frame, Vector3 centerL,
            Vector3 size, Vector3 frontW)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.GetComponent<ItemTrigger>() != null) return existing.GetComponent<ItemTrigger>();
            return BuildItemTrigger(parent, name, frame, centerL, size, frontW);
        }

        /// <summary>Point on the floor of a dispenser tray (the model's 'Black' recess).</summary>
        private static Transform Outlet(Transform parent, string name, BuildingFrame frame, Renderer machine, Vector3 frontW)
        {
            Vector3 pos;
            if (MaterialBounds(machine, "Black", frame, out Bounds tray, 0.002f)) // ignore the coin slit's tiny black faces
                pos = frame.ToWorld(new Vector3(tray.center.x, tray.min.y + 0.005f, tray.center.z));
            else
            {
                Debug.LogWarning($"[Park Setup] No tray (material 'Black') on {machine.name}; using its front bottom.");
                Bounds b = machine.bounds;
                pos = new Vector3(b.center.x, b.min.y + 0.3f, b.center.z) + frontW * b.extents.z;
            }
            return FindOrCreateAnchor(parent, name, pos, Quaternion.LookRotation(-frontW, Vector3.up));
        }

        // ------------------------------------------------------------------
        // Bench
        // ------------------------------------------------------------------
        private static void BuildTeleportAnchor(Transform parent, BuildingFrame frame, Bounds bench, Vector3 frontL, Transform seat)
        {
            GameObject go = FindOrCreate(parent, "BenchTeleport");
            go.transform.SetPositionAndRotation(
                frame.ToWorld(new Vector3(bench.center.x, bench.min.y + 0.45f, bench.center.z)), seat.rotation);
            BoxCollider box = GetOrAdd<BoxCollider>(go);
            box.size = new Vector3(Across(bench, frontL), 0.06f, Extent(bench, frontL) * 2f);
            box.center = Vector3.zero;

            TeleportationAnchor anchor = GetOrAdd<TeleportationAnchor>(go);
            anchor.teleportAnchorTransform = seat;
            anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
            anchor.interactionLayers = InteractionLayerMask.GetMask("Teleport");
            EditorUtility.SetDirty(anchor);
        }

        // ------------------------------------------------------------------
        // Machine UIs
        // ------------------------------------------------------------------
        private static Button[] BuildDrinkButtons(Transform anchor, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "DrinkButtons", px, true);
            string[] labels = { "Coca Cola", "Pepsi", "Sprite" };
            Color[] colors = { s_Coke, s_Pepsi, s_Sprite };
            var buttons = new Button[3];
            float gap = 0.06f, h = (1f - gap * 4f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float top = 1f - gap - i * (h + gap);
                RectTransform slot = NewUIChild("Slot_" + labels[i], canvas);
                slot.anchorMin = new Vector2(0.06f, top - h);
                slot.anchorMax = new Vector2(0.94f, top);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                buttons[i] = AddButton(slot, labels[i], colors[i], Mathf.Max(24f, px.y * h * 0.55f));
            }
            return buttons;
        }

        private static SmartMachineScreen BuildSmartScreen(Transform anchor, Vector2 sizeM)
        {
            Vector2 px = sizeM * 1000f;
            RectTransform canvas = NewWorldCanvas(anchor, "SmartScreen", px, true);
            AddRounded(canvas, "Background", new Color(0.04f, 0.16f, 0.16f, 0.96f));

            RectTransform title = NewUIChild("Title", canvas);
            title.anchorMin = new Vector2(0.06f, 0.85f);
            title.anchorMax = new Vector2(0.94f, 0.97f);
            title.offsetMin = title.offsetMax = Vector2.zero;
            TextMeshProUGUI titleText = AddText(title, "SNACK BAR", px.y * 0.08f, TextAlignmentOptions.Center, FontStyles.Bold);
            titleText.color = new Color(0.55f, 0.95f, 0.88f);

            string[] labels = { "Lays Original", "Lays Sour Cream", "Lays Seaweed" };
            Color[] colors = { s_ChipsYellow, s_ChipsTeal, s_ChipsGreen };
            var buttons = new Button[3];
            float[] tops = { 0.81f, 0.61f, 0.41f };
            for (int i = 0; i < 3; i++)
            {
                RectTransform slot = NewUIChild("Slot_" + i, canvas);
                slot.anchorMin = new Vector2(0.08f, tops[i] - 0.17f);
                slot.anchorMax = new Vector2(0.92f, tops[i]);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                buttons[i] = AddButton(slot, labels[i], colors[i], px.y * 0.07f);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = new Color(0.08f, 0.08f, 0.1f); // dark text on bright buttons
            }

            RectTransform status = NewUIChild("Status", canvas);
            status.anchorMin = new Vector2(0.06f, 0.03f);
            status.anchorMax = new Vector2(0.94f, 0.2f);
            status.offsetMin = status.offsetMax = Vector2.zero;
            TextMeshProUGUI statusText = AddText(status, "Welcome! Fresh snacks inside.", px.y * 0.055f, TextAlignmentOptions.Center);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14f;
            statusText.fontSizeMax = px.y * 0.06f;

            SmartMachineScreen screen = canvas.gameObject.AddComponent<SmartMachineScreen>();
            var so = new SerializedObject(screen);
            SetArray(so.FindProperty("m_Buttons"), buttons);
            so.FindProperty("m_Status").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }
    }
}
