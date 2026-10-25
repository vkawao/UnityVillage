using System.Collections.Generic;
using TMPro;
using Unity.VRTemplate;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

using static VRVillage.Editor.QuestBuildKit;

namespace VRVillage.Editor
{
    /// <summary>
    /// Actions 5-10 of the Guitar Shop quest (counter, pedals, amps, try-out, purchase)
    /// plus the permanent cashier screen. Called from <see cref="QuestSetupUtility.Setup"/>.
    ///   GuitarShopQuest
    ///     Counter/CounterZone          - detection box on top of the counter
    ///     Counter/CashierAnchor        - permanent cashier screen (move/rotate freely)
    ///     Counter/HereMarkerAnchor     - "Here!" marker for action 5
    ///     Counter/DoneButtonAnchor     - "Done!" button for action 9
    ///     AmpButtons/AmpButtonAnchor_N - selection buttons for action 7
    /// Pedals and amps get a "QuestParts" child (footswitch/LED, volume knob/pilot light)
    /// that is rebuilt on every run, keeping any position/rotation/scale you gave its parts by hand.
    /// </summary>
    public static partial class QuestSetupUtility
    {
        /// <summary>Short task-line text for the guide, actions 1-10.</summary>
        private static readonly string[] s_ShortTexts =
        {
            "Enter the store",
            "Hang the electric guitars on the rack",
            "Hang the acoustic guitars on the rack",
            "Tune the acoustic guitars",
            "Bring the Les Paul to the cashier",
            "Bring an effect pedal to the cashier",
            "Find the amp that isn't faulty",
            "Bring the working amp to the cashier",
            "Try out your gear, then press Done!",
            "Press Purchase on the cashier screen",
        };

        private static readonly string[] s_PedalNames = { "Red Effect", "Blue Effect", "Yellow Effect" };
        private static readonly string[] s_PedalLabels = { "Red Overdrive Pedal", "Blue Chorus Pedal", "Yellow Distortion Pedal" };
        private static readonly int[] s_PedalPrices = { 129, 149, 119 };
        private static readonly string[] s_ExtraGuitarNames = { "RedStrat", "YamahaGuitar", "TaylorGuitar", "LavaMeGuitar" };
        private static readonly string[] s_ExtraGuitarLabels = { "Fender Stratocaster", "Yamaha Acoustic", "Taylor Acoustic", "Lava Me Acoustic" };
        private static readonly int[] s_ExtraGuitarPrices = { 1499, 399, 1299, 699 };
        private static readonly string[] s_AmpNames = { "MarshallAmp", "MarshallAmp (1)", "MarshallAmp (2)", "MarshallAmp (3)" };
        private static readonly string[] s_ShelfNames = { "Shelf", "Shelf.001", "Shelf.002" };

        private static bool SetupShopActions(Transform gc, Transform root, Transform anchors, Transform steps, Transform fx,
            Bounds floor, Vector3 doorCenter, Transform rackAnchor, XRGrabInteractable lesPaul, List<Object> allSteps)
        {
            // ---------- Find scene objects ----------
            Transform counterT = FindDeepChild(gc, "Counter");
            if (counterT == null)
            {
                Debug.LogError("[Quest Setup] 'Counter' not found.");
                return false;
            }
            var pedals = new List<Transform>();
            foreach (string n in s_PedalNames)
            {
                Transform t = FindDeepChild(gc, n);
                if (t == null) { Debug.LogError($"[Quest Setup] Pedal '{n}' not found."); return false; }
                pedals.Add(t);
            }
            var amps = new List<Transform>();
            foreach (string n in s_AmpNames)
            {
                Transform t = FindDeepChild(gc, n);
                if (t == null) { Debug.LogError($"[Quest Setup] Amp '{n}' not found."); return false; }
                amps.Add(t);
            }
            var shelves = new List<Transform>();
            foreach (string n in s_ShelfNames)
            {
                Transform t = FindDeepChild(gc, n);
                if (t != null) shelves.Add(t);
            }

            EnsureEventSystem();
            foreach (Transform shelf in shelves) EnsureStaticCollider(shelf.gameObject);
            EnsureStaticCollider(counterT.gameObject);

            var frame = new BuildingFrame(gc);
            Bounds floorL = frame.BoundsOf(FindDeepChild(gc, "Floor") ?? gc);
            Vector3 floorCenter = floor.center;

            // ---------- Shop items ----------
            CounterItem lesPaulItem = SetupCounterItem(lesPaul.gameObject, "Gibson Les Paul", 2499);

            // The other guitars aren't part of the tasks, but they're for sale too.
            for (int i = 0; i < s_ExtraGuitarNames.Length; i++)
            {
                Transform g = FindDeepChild(gc, s_ExtraGuitarNames[i]);
                if (g != null) SetupCounterItem(g.gameObject, s_ExtraGuitarLabels[i], s_ExtraGuitarPrices[i]);
            }

            var pedalItems = new List<CounterItem>();
            var effectPedals = new List<EffectPedal>();
            Transform pedalShelf = Nearest(shelves, AverageCenter(pedals));
            Vector3 pedalFrontW = InwardDir(frame, pedalShelf != null ? pedalShelf : pedals[0], floorCenter);
            for (int i = 0; i < pedals.Count; i++)
            {
                SetupGrabbable(pedals[i].gameObject, 0.6f);
                pedalItems.Add(SetupCounterItem(pedals[i].gameObject, s_PedalLabels[i], s_PedalPrices[i]));
                effectPedals.Add(BuildPedalParts(frame, pedals[i], pedalFrontW));
            }

            var ampUnits = new List<AmpUnit>();
            Transform ampShelf = Nearest(shelves, AverageCenter(amps));
            Vector3 ampShelfFrontW = InwardDir(frame, ampShelf != null ? ampShelf : amps[0], floorCenter);
            foreach (Transform amp in amps)
            {
                SetupGrabbable(amp.gameObject, 5f);
                SetupCounterItem(amp.gameObject, "Marshall Amp", 899);
                ampUnits.Add(BuildAmpParts(frame, amp, ampShelfFrontW));
            }

            // ---------- Counter: zone, customer side, cashier ----------
            Transform counterRoot = FindOrCreate(root, "Counter").transform;
            Bounds cb = frame.BoundsOf(counterT);
            float topY = cb.max.y;

            Vector3 counterCenterW = frame.ToWorld(cb.center);
            Vector3 toFloor = counterCenterW - floorCenter;
            toFloor.y = 0f;
            Vector3 custW = toFloor.magnitude > 0.4f
                ? frame.Snap(floorCenter - counterCenterW)
                : frame.Snap(doorCenter - counterCenterW);
            Vector3 custL = frame.LocalDir(custW);
            Vector3 rightL = Vector3.Cross(Vector3.up, -custL); // viewer's right while facing the counter
            float depth = Extent(cb, custL);
            Vector3 counterFlatL = new Vector3(cb.center.x, 0f, cb.center.z);

            CounterZone zone = GetOrAdd<CounterZone>(FindOrCreate(counterRoot, "CounterZone"));
            zone.transform.SetPositionAndRotation(frame.ToWorld(counterFlatL + Vector3.up * (topY + 0.35f)), frame.Rot);
            var soZ = new SerializedObject(zone);
            soZ.FindProperty("m_Size").vector3Value = new Vector3(cb.size.x + 0.1f, 0.8f, cb.size.z + 0.1f);
            soZ.ApplyModifiedPropertiesWithoutUndo();

            Quaternion facingCustomer = Quaternion.LookRotation(-custW, Vector3.up);
            Vector3 cashierL = counterFlatL - custL * (depth * 0.45f) + Vector3.up * (topY + 0.33f);
            Transform cashierAnchor = FindOrCreateAnchor(counterRoot, "CashierAnchor", frame.ToWorld(cashierL),
                facingCustomer * Quaternion.Euler(15f, 0f, 0f));
            CashierUI cashier = BuildCashier(cashierAnchor, zone);

            Transform hereAnchor = FindOrCreateAnchor(counterRoot, "HereMarkerAnchor",
                frame.ToWorld(counterFlatL + custL * (depth * 0.2f) + Vector3.up * (topY + 0.85f)), facingCustomer);
            GameObject hereMarker = BuildHereMarker(hereAnchor);

            Transform doneAnchor = FindOrCreateAnchor(counterRoot, "DoneButtonAnchor",
                frame.ToWorld(cashierL + rightL * 0.6f - Vector3.up * 0.1f), facingCustomer * Quaternion.Euler(15f, 0f, 0f));
            Button doneButton = BuildButtonCanvas(doneAnchor, "DoneButton", new Vector2(360f, 140f), "Done!", ButtonGreen, 64f);

            // ---------- Anchors for the instruction panel ----------
            Vector3 overCounterL = counterFlatL - custL * (depth * 0.45f) + Vector3.up * (topY + 1.05f);
            Transform anchor5 = FindOrCreateAnchor(anchors, "QuestAnchor_5_LesPaul", rackAnchor.position, rackAnchor.rotation);

            Bounds pb = frame.BoundsOf(pedals[0]);
            for (int i = 1; i < pedals.Count; i++) pb.Encapsulate(frame.BoundsOf(pedals[i]));
            Vector3 pedalFrontL = frame.LocalDir(pedalFrontW);
            Vector3 anchor6L = new Vector3(pb.center.x, 0f, pb.center.z) + pedalFrontL * 0.35f
                               + Vector3.up * Mathf.Min(pb.max.y + 0.55f, floorL.max.y + 2.4f);
            Transform anchor6 = FindOrCreateAnchor(anchors, "QuestAnchor_6_Pedals", frame.ToWorld(anchor6L),
                Quaternion.LookRotation(-pedalFrontW, Vector3.up));

            Bounds ab = frame.BoundsOf(amps[0]);
            for (int i = 1; i < amps.Count; i++) ab.Encapsulate(frame.BoundsOf(amps[i]));
            Vector3 ampFrontL = frame.LocalDir(ampShelfFrontW);
            Vector3 anchor7L = new Vector3(ab.center.x, 0f, ab.center.z) + ampFrontL * 0.35f
                               + Vector3.up * Mathf.Min(ab.max.y + 0.5f, floorL.max.y + 2.4f);
            Transform anchor7 = FindOrCreateAnchor(anchors, "QuestAnchor_7_Amps", frame.ToWorld(anchor7L),
                Quaternion.LookRotation(-ampShelfFrontW, Vector3.up));

            Transform anchor8 = FindOrCreateAnchor(anchors, "QuestAnchor_8_AmpToCounter", frame.ToWorld(overCounterL), facingCustomer);
            Transform anchor9 = FindOrCreateAnchor(anchors, "QuestAnchor_9_TryGear", frame.ToWorld(overCounterL), facingCustomer);
            Transform anchor10 = FindOrCreateAnchor(anchors, "QuestAnchor_10_Purchase", frame.ToWorld(overCounterL), facingCustomer);

            // ---------- Amp selection buttons ----------
            Transform ampButtonsRoot = FindOrCreate(root, "AmpButtons").transform;
            var ampButtons = new List<Button>();
            for (int i = 0; i < amps.Count; i++)
            {
                Bounds b = frame.BoundsOf(amps[i]);
                float ext = Extent(b, ampFrontL);
                float y = b.min.y - 0.09f;
                if (y < floorL.max.y + 0.12f) y = b.min.y + 0.09f; // amp on the floor: put it on the amp's front instead
                Vector3 posL = new Vector3(b.center.x, y, b.center.z) + ampFrontL * (ext + 0.06f);
                Transform a = FindOrCreateAnchor(ampButtonsRoot, $"AmpButtonAnchor_{i + 1}", frame.ToWorld(posL),
                    Quaternion.LookRotation(-ampShelfFrontW, Vector3.up));
                ampButtons.Add(BuildButtonCanvas(a, "AmpButton", new Vector2(320f, 120f), "This one", ButtonBlue, 52f));
            }

            // ---------- Celebrations ----------
            QuestCelebration confetti = BuildCelebration(fx, "Celebration_AmpConfetti",
                frame.ToWorld(new Vector3(ab.center.x, ab.max.y + 0.2f, ab.center.z) + ampFrontL * 0.4f),
                CelebrationStyle.ConfettiStyle());
            QuestCelebration purchaseFx = BuildCelebration(fx, "Celebration_Purchase",
                frame.ToWorld(counterFlatL + Vector3.up * (topY + 0.3f)), CelebrationStyle.GreenStyle());

            // ---------- Step 5: Les Paul to the counter ----------
            ItemToCounterStep step5 = GetOrAdd<ItemToCounterStep>(FindOrCreate(steps, "Step5_LesPaulToCounter"));
            var so5 = StepSO(step5, "It's the Les Paul you've always wanted! Bring it to the cashier.", anchor5,
                null, new[] { hereMarker });
            so5.FindProperty("m_Zone").objectReferenceValue = zone;
            SetArray(so5.FindProperty("m_Items"), new[] { lesPaulItem });
            so5.FindProperty("m_PointAtItem").boolValue = true;
            so5.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 6: a pedal to the counter ----------
            ItemToCounterStep step6 = GetOrAdd<ItemToCounterStep>(FindOrCreate(steps, "Step6_PedalToCounter"));
            var so6 = StepSO(step6, "Pick an effect and bring it to the cashier.", anchor6);
            so6.FindProperty("m_Zone").objectReferenceValue = zone;
            SetArray(so6.FindProperty("m_Items"), pedalItems);
            so6.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 7: find the working amp ----------
            AmpTestStep step7 = GetOrAdd<AmpTestStep>(FindOrCreate(steps, "Step7_TestAmps"));
            var so7 = StepSO(step7,
                "This brand of amp failed quality control recently. Test them out, find the one that isn't faulty.",
                anchor7, confetti, new[] { ampButtonsRoot.gameObject }, 1.5f);
            SetArray(so7.FindProperty("m_Amps"), ampUnits);
            SetArray(so7.FindProperty("m_Buttons"), ampButtons);
            so7.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 8: working amp to the counter ----------
            WorkingAmpToCounterStep step8 = GetOrAdd<WorkingAmpToCounterStep>(FindOrCreate(steps, "Step8_AmpToCounter"));
            var so8 = StepSO(step8, "Take the working amp to the cashier.", anchor8);
            so8.FindProperty("m_Zone").objectReferenceValue = zone;
            so8.FindProperty("m_AmpTest").objectReferenceValue = step7;
            so8.FindProperty("m_PointAtItem").boolValue = true;
            so8.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 9: try the gear ----------
            TryGearStep step9 = GetOrAdd<TryGearStep>(FindOrCreate(steps, "Step9_TryGear"));
            var so9 = StepSO(step9, "Try out your gear!", anchor9, null, new[] { doneButton.transform.parent.gameObject });
            so9.FindProperty("m_Guitar").objectReferenceValue = lesPaul;
            so9.FindProperty("m_AmpTest").objectReferenceValue = step7;
            SetArray(so9.FindProperty("m_Pedals"), effectPedals);
            so9.FindProperty("m_DoneButton").objectReferenceValue = doneButton;
            so9.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 10: purchase ----------
            PurchaseStep step10 = GetOrAdd<PurchaseStep>(FindOrCreate(steps, "Step10_Purchase"));
            var so10 = StepSO(step10, "Purchase them!", anchor10, purchaseFx);
            so10.FindProperty("m_Cashier").objectReferenceValue = cashier;
            so10.FindProperty("m_AmpTest").objectReferenceValue = step7;
            SetArray(so10.FindProperty("m_Required"), new[] { lesPaulItem });
            SetArray(so10.FindProperty("m_AnyOneOf"), pedalItems);
            so10.FindProperty("m_VanishSparkle").objectReferenceValue = purchaseFx;
            so10.ApplyModifiedPropertiesWithoutUndo();

            allSteps.AddRange(new Object[] { step5, step6, step7, step8, step9, step10 });

            // ---------- Guide: short texts, arrow targets, HUD ----------
            for (int i = 0; i < allSteps.Count && i < s_ShortTexts.Length; i++)
            {
                var soS = new SerializedObject(allSteps[i]);
                soS.FindProperty("m_ShortInstruction").stringValue = s_ShortTexts[i];
                Transform target = allSteps[i] == step9 || allSteps[i] == step10 ? cashierAnchor : null;
                soS.FindProperty("m_GuideTarget").objectReferenceValue = target;
                soS.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildGuide(root, frame, floorL);
            return true;
        }

        // ------------------------------------------------------------------
        // Grabbable shop items
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Pedal footswitch + LED, amp knob + pilot light
        // ------------------------------------------------------------------
        private static EffectPedal BuildPedalParts(BuildingFrame frame, Transform pedal, Vector3 frontW)
        {
            Transform parts = RebuildParts(pedal);
            Bounds b = frame.BoundsOf(pedal);
            Vector3 frontL = frame.LocalDir(frontW);
            float ext = Extent(b, frontL);
            Vector3 topL = new Vector3(b.center.x, b.max.y, b.center.z);

            // Footswitch: grab it to toggle. Bigger invisible sphere so it's easy to hit.
            var fs = new GameObject("Footswitch");
            fs.transform.SetPositionAndRotation(frame.ToWorld(topL + frontL * (ext * 0.4f) + Vector3.up * 0.006f), frame.Rot);
            var fsCol = fs.AddComponent<SphereCollider>();
            fsCol.radius = 0.03f;
            fs.AddComponent<XRSimpleInteractable>();
            GameObject cap = Primitive(PrimitiveType.Cylinder, "Cap", fs.transform, Vector3.zero, new Vector3(0.022f, 0.006f, 0.022f),
                GetOrCreateMaterial(MetalMatPath, "Universal Render Pipeline/Lit", new Color(0.75f, 0.75f, 0.78f), 0.85f, 1f));
            cap.transform.localPosition = Vector3.zero;
            fs.transform.SetParent(parts, true);
            KeepPlacement(fs.transform);

            GameObject led = Primitive(PrimitiveType.Sphere, "Led", null,
                frame.ToWorld(topL - frontL * (ext * 0.45f) + Vector3.up * 0.004f), Vector3.one * 0.012f,
                GetOrCreateMaterial(LedMatPath, "Universal Render Pipeline/Unlit", Color.white));
            led.transform.SetParent(parts, true);
            KeepPlacement(led.transform);

            EffectPedal ep = GetOrAdd<EffectPedal>(pedal.gameObject);
            var so = new SerializedObject(ep);
            so.FindProperty("m_Footswitch").objectReferenceValue = fs.GetComponent<XRSimpleInteractable>();
            so.FindProperty("m_Led").objectReferenceValue = led.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return ep;
        }

        private static AmpUnit BuildAmpParts(BuildingFrame frame, Transform amp, Vector3 frontW)
        {
            Transform parts = RebuildParts(amp);
            Bounds b = frame.BoundsOf(amp);
            Vector3 frontL = frame.LocalDir(frontW);
            Vector3 rightL = Vector3.Cross(Vector3.up, -frontL);
            float extF = Extent(b, frontL);
            float extR = Extent(b, rightL);
            Vector3 faceL = b.center + frontL * (extF + 0.002f) + Vector3.up * (b.extents.y * 0.55f);

            // Knob: XRKnob turns its handle around its local Y, so point local Y out of the amp's front.
            var knob = new GameObject("VolumeKnob");
            knob.transform.SetPositionAndRotation(frame.ToWorld(faceL + rightL * (extR * 0.55f)),
                Quaternion.LookRotation(Vector3.up, frontW));
            var knobCol = knob.AddComponent<SphereCollider>();
            knobCol.radius = 0.045f;
            knobCol.center = new Vector3(0f, 0.012f, 0f);

            var handle = new GameObject("Handle").transform;
            handle.SetParent(knob.transform, false);
            GameObject body = Primitive(PrimitiveType.Cylinder, "Body", handle, Vector3.zero, new Vector3(0.05f, 0.012f, 0.05f),
                GetOrCreateMaterial(KnobMatPath, "Universal Render Pipeline/Lit", new Color(0.06f, 0.06f, 0.07f), 0.6f));
            body.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            GameObject mark = Primitive(PrimitiveType.Cube, "Pointer", handle, Vector3.zero, new Vector3(0.006f, 0.003f, 0.02f),
                GetOrCreateMaterial(MarkMatPath, "Universal Render Pipeline/Unlit", Color.white));
            mark.transform.localPosition = new Vector3(0f, 0.025f, 0.013f);

            XRKnob xrKnob = knob.AddComponent<XRKnob>();
            var soK = new SerializedObject(xrKnob);
            soK.FindProperty("m_Handle").objectReferenceValue = handle;
            soK.FindProperty("m_Value").floatValue = 0f;
            soK.FindProperty("m_MinAngle").floatValue = -135f;
            soK.FindProperty("m_MaxAngle").floatValue = 135f;
            soK.FindProperty("m_ClampedMotion").boolValue = true;
            soK.ApplyModifiedPropertiesWithoutUndo();
            knob.transform.SetParent(parts, true);
            KeepPlacement(knob.transform);

            GameObject light = Primitive(PrimitiveType.Sphere, "PilotLight", null,
                frame.ToWorld(faceL - rightL * (extR * 0.55f)), Vector3.one * 0.022f,
                GetOrCreateMaterial(LedMatPath, "Universal Render Pipeline/Unlit", Color.white));
            light.transform.SetParent(parts, true);
            KeepPlacement(light.transform);

            AudioSource audio = GetOrAdd<AudioSource>(amp.gameObject);
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.8f;
            audio.maxDistance = 20f;

            AmpUnit unit = GetOrAdd<AmpUnit>(amp.gameObject);
            var so = new SerializedObject(unit);
            so.FindProperty("m_Knob").objectReferenceValue = xrKnob;
            so.FindProperty("m_AudioSource").objectReferenceValue = audio;
            so.FindProperty("m_PilotLight").objectReferenceValue = light.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        // ------------------------------------------------------------------
        // World-space UI
        // ------------------------------------------------------------------
        private static CashierUI BuildCashier(Transform anchor, CounterZone zone)
        {
            RectTransform canvas = NewWorldCanvas(anchor, "CashierScreen", new Vector2(700f, 600f), true);
            AddRounded(canvas, "Background", PanelDark);

            RectTransform bar = NewUIChild("Accent", canvas);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(40f, -30f);
            bar.offsetMax = new Vector2(-40f, -18f);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.color = AccentRed;
            barImg.raycastTarget = false;

            RectTransform title = NewUIChild("Title", canvas);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = Vector2.one;
            title.pivot = new Vector2(0.5f, 1f);
            title.offsetMin = new Vector2(40f, -120f);
            title.offsetMax = new Vector2(-40f, -40f);
            AddText(title, "CASHIER", 58f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            RectTransform items = NewUIChild("Items", canvas);
            Stretch(items, new Vector2(40f, 220f), new Vector2(-40f, -130f));
            TextMeshProUGUI itemsText = AddText(items, "Place items on the counter", 38f, TextAlignmentOptions.TopLeft);
            itemsText.enableAutoSizing = true;
            itemsText.fontSizeMin = 24f;
            itemsText.fontSizeMax = 38f;

            RectTransform line = NewUIChild("Divider", canvas);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.offsetMin = new Vector2(40f, 206f);
            line.offsetMax = new Vector2(-40f, 210f);
            var lineImg = line.gameObject.AddComponent<Image>();
            lineImg.color = new Color(1f, 1f, 1f, 0.25f);
            lineImg.raycastTarget = false;

            RectTransform total = NewUIChild("Total", canvas);
            total.anchorMin = new Vector2(0f, 0f);
            total.anchorMax = new Vector2(1f, 0f);
            total.offsetMin = new Vector2(40f, 130f);
            total.offsetMax = new Vector2(-40f, 200f);
            TextMeshProUGUI totalText = AddText(total, "Total  <b>$0</b>", 46f, TextAlignmentOptions.MidlineRight);

            RectTransform buttonArea = NewUIChild("PurchaseArea", canvas);
            buttonArea.anchorMin = new Vector2(0f, 0f);
            buttonArea.anchorMax = new Vector2(1f, 0f);
            buttonArea.offsetMin = new Vector2(40f, 28f);
            buttonArea.offsetMax = new Vector2(-40f, 118f);
            Button purchase = AddButton(buttonArea, "Purchase", ButtonGreen, 52f);

            CashierUI cashier = canvas.gameObject.AddComponent<CashierUI>();
            var so = new SerializedObject(cashier);
            so.FindProperty("m_Zone").objectReferenceValue = zone;
            so.FindProperty("m_ItemsText").objectReferenceValue = itemsText;
            so.FindProperty("m_TotalText").objectReferenceValue = totalText;
            so.FindProperty("m_PurchaseButton").objectReferenceValue = purchase;
            so.FindProperty("m_PurchaseImage").objectReferenceValue = purchase.image;
            so.FindProperty("m_PurchaseLabel").objectReferenceValue = purchase.GetComponentInChildren<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return cashier;
        }

        // ------------------------------------------------------------------
        // Materials / FX styles
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Geometry helpers
        // ------------------------------------------------------------------

    }
}
