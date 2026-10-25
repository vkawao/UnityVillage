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
    /// Builds the Guitar Shop quest in the Village scene:
    ///   GuitarShopQuest (QuestManager)
    ///     QuestPanel            - floating world-space instruction canvas
    ///     FX/Celebration_...    - sparkles + fanfare
    ///     Anchors/QuestAnchor_N - where the panel appears for each step (move freely)
    ///     Steps/StepN_...       - one QuestStep per action
    /// Safe to re-run: existing objects are reused, and anchors you have moved keep their position.
    /// </summary>
    public static partial class QuestSetupUtility
    {
        private const string RootName = "GuitarShopQuest";
        [MenuItem("VR Village/Setup Guitar Shop Quest")]
        public static void Setup()
        {
            if (EditorSceneManager.GetActiveScene().name != "Village")
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/Village.unity", OpenSceneMode.Single);
            }

            GameObject gc = FindInScene("GuitarCenter");
            if (gc == null)
            {
                Debug.LogError("[Quest Setup] 'GuitarCenter' not found in the Village scene.");
                return;
            }

            // The shop's own door (other houses have DoorMovers too).
            DoorMover door = gc.GetComponentInChildren<DoorMover>(true);
            if (door == null)
            {
                Debug.LogError("[Quest Setup] No DoorMover found. Run 'VR Village/Setup Scene Interactions' first.");
                return;
            }

            if (!FindRack(gc.transform, "Wall Guitar Rack", out Transform rack1, out XRSocketInteractor[] rack1Sockets)) return;
            if (!FindRack(gc.transform, "Wall Guitar Rack.001", out Transform rack2, out XRSocketInteractor[] rack2Sockets)) return;

            XRGrabInteractable lesPaul = FindGrab(gc.transform, "LesPaul");
            XRGrabInteractable redStrat = FindGrab(gc.transform, "RedStrat");
            XRGrabInteractable yamaha = FindGrab(gc.transform, "YamahaGuitar");
            XRGrabInteractable taylor = FindGrab(gc.transform, "TaylorGuitar");
            XRGrabInteractable lavaMe = FindGrab(gc.transform, "LavaMeGuitar");
            if (lesPaul == null || redStrat == null || yamaha == null || taylor == null || lavaMe == null)
            {
                Debug.LogError("[Quest Setup] A guitar is missing its XRGrabInteractable. Run 'VR Village/Setup Scene Interactions' first.");
                return;
            }
            XRGrabInteractable[] acoustics = { yamaha, taylor, lavaMe };

            // ---------- Hierarchy ----------
            GameObject root = FindOrCreate(null, RootName);
            Transform anchors = FindOrCreate(root.transform, "Anchors").transform;
            Transform steps = FindOrCreate(root.transform, "Steps").transform;
            Transform fx = FindOrCreate(root.transform, "FX").transform;

            QuestManager manager = GetOrAdd<QuestManager>(root);
            QuestPanel panel = BuildPanel(root.transform);

            // ---------- Anchor positions (only set when first created) ----------
            Bounds floor = RendererBounds(FindDeepChild(gc.transform, "Floor") ?? gc.transform);
            float floorY = floor.max.y;

            Bounds doorBounds = RendererBounds(door.transform);
            Vector3 outward = FlatDir(doorBounds.center - floor.center);
            Vector3 anchor1Pos = Flat(doorBounds.center) + outward * 1.3f + Vector3.up * (floorY + 1.6f);

            RackAnchor(rack1, rack1Sockets, floor, out Vector3 anchor2Pos, out Vector3 inward1);
            RackAnchor(rack2, rack2Sockets, floor, out Vector3 rack2AnchorPos, out Vector3 inward2);

            // Panel is static and reads from the anchor's -Z side, so aim +Z away from the player:
            //   door panel -> player stands outside, looking at the door  (+Z = -outward)
            //   rack panel -> player stands in the room, looking at the rack (+Z = -inward)
            Transform anchor1 = FindOrCreateAnchor(anchors, "QuestAnchor_1_Door", anchor1Pos, Quaternion.LookRotation(-outward, Vector3.up));
            Transform anchor2 = FindOrCreateAnchor(anchors, "QuestAnchor_2_Rack", anchor2Pos, Quaternion.LookRotation(-inward1, Vector3.up));
            Transform anchor3 = FindOrCreateAnchor(anchors, "QuestAnchor_3_AcousticRack", rack2AnchorPos, Quaternion.LookRotation(-inward2, Vector3.up));
            Transform anchor4 = FindOrCreateAnchor(anchors, "QuestAnchor_4_Tune", rack2AnchorPos, Quaternion.LookRotation(-inward2, Vector3.up));

            // ---------- Celebration FX (sparkles + fanfare) at the acoustic rack ----------
            Vector3 rack2Center = Vector3.zero;
            foreach (var s in rack2Sockets) rack2Center += s.transform.position;
            rack2Center /= rack2Sockets.Length;
            QuestCelebration celebration = BuildCelebration(fx, "Celebration_AcousticRack", rack2Center + inward2 * 0.15f);

            // ---------- Step 1: Enter the store ----------
            DoorOpenStep step1 = GetOrAdd<DoorOpenStep>(FindOrCreate(steps, "Step1_EnterStore"));
            var so1 = new SerializedObject(step1);
            so1.FindProperty("m_Instruction").stringValue = "Enter the store";
            so1.FindProperty("m_PanelAnchor").objectReferenceValue = anchor1;
            so1.FindProperty("m_Door").objectReferenceValue = door;
            so1.ApplyModifiedPropertiesWithoutUndo();

            // This shop's door gets the built-in latch/creak/bell sounds (other houses' doors stay as they are).
            var soDoor = new SerializedObject(door);
            soDoor.FindProperty("m_UseBuiltInSounds").boolValue = true;
            soDoor.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 2: Organize the electric guitars ----------
            RackGuitarsStep step2 = GetOrAdd<RackGuitarsStep>(FindOrCreate(steps, "Step2_RackElectricGuitars"));
            var so2 = new SerializedObject(step2);
            so2.FindProperty("m_Instruction").stringValue =
                "Looks like the previous customers never learned manners. Be kind and put the electric guitars onto the rack.";
            so2.FindProperty("m_PanelAnchor").objectReferenceValue = anchor2;
            SetArray(so2.FindProperty("m_RackSockets"), rack1Sockets);
            SetArray(so2.FindProperty("m_RequiredGuitars"), new Object[] { lesPaul, redStrat });
            so2.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 3: Organize the acoustic guitars (with celebration) ----------
            RackGuitarsStep step3 = GetOrAdd<RackGuitarsStep>(FindOrCreate(steps, "Step3_RackAcousticGuitars"));
            var so3 = new SerializedObject(step3);
            so3.FindProperty("m_Instruction").stringValue = "Poor Acoustic guitars... Put them on the rack as well.";
            so3.FindProperty("m_PanelAnchor").objectReferenceValue = anchor3;
            so3.FindProperty("m_Celebration").objectReferenceValue = celebration;
            SetArray(so3.FindProperty("m_RackSockets"), rack2Sockets);
            SetArray(so3.FindProperty("m_RequiredGuitars"), acoustics);
            so3.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Step 4: Tune the acoustic guitars ----------
            var tuners = new List<GuitarTuner>();
            foreach (var g in acoustics)
            {
                GuitarTuner tuner = GetOrAdd<GuitarTuner>(g.gameObject);
                var soT = new SerializedObject(tuner);
                soT.FindProperty("m_Sparkle").objectReferenceValue = celebration;
                soT.ApplyModifiedPropertiesWithoutUndo();
                tuners.Add(tuner);
            }

            TuneGuitarsStep step4 = GetOrAdd<TuneGuitarsStep>(FindOrCreate(steps, "Step4_TuneAcousticGuitars"));
            var so4 = new SerializedObject(step4);
            so4.FindProperty("m_Instruction").stringValue = "The acoustic guitars must be untuned. Tune them.";
            so4.FindProperty("m_PanelAnchor").objectReferenceValue = anchor4;
            SetArray(so4.FindProperty("m_Guitars"), tuners);
            so4.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Steps 5-10: counter, pedals, amps, try-out, purchase ----------
            var allSteps = new List<Object> { step1, step2, step3, step4 };
            if (!SetupShopActions(gc.transform, root.transform, anchors, steps, fx, floor, doorBounds.center, anchor2,
                    lesPaul, allSteps))
            {
                return;
            }

            // ---------- Manager ----------
            var soM = new SerializedObject(manager);
            soM.FindProperty("m_Panel").objectReferenceValue = panel;
            SetArray(soM.FindProperty("m_Steps"), allSteps);
            soM.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("<color=green>[Quest Setup] Guitar Shop quest ready: actions 1-10.</color>");
        }

        private static bool FindRack(Transform gc, string rackName, out Transform rack, out XRSocketInteractor[] sockets)
        {
            rack = FindDeepChild(gc, rackName);
            sockets = null;
            if (rack == null)
            {
                Debug.LogError($"[Quest Setup] '{rackName}' not found.");
                return false;
            }
            sockets = rack.GetComponentsInChildren<XRSocketInteractor>(true);
            if (sockets.Length == 0)
            {
                Debug.LogError($"[Quest Setup] '{rackName}' has no sockets. Run 'VR Village/Setup Scene Interactions' first.");
                return false;
            }
            return true;
        }

        /// <summary>Default panel spot above a rack, nudged into the room.</summary>
        private static void RackAnchor(Transform rack, XRSocketInteractor[] sockets, Bounds floor, out Vector3 pos, out Vector3 inward)
        {
            float floorY = floor.max.y;
            Bounds rackBounds = RendererBounds(rack);
            inward = FlatDir(floor.center - rackBounds.center);
            float topY = rackBounds.max.y;
            foreach (var s in sockets) topY = Mathf.Max(topY, s.transform.position.y + 0.55f);
            float y = Mathf.Min(topY + 0.35f, floorY + 2.6f);
            pos = Flat(rackBounds.center) + inward * 0.3f + Vector3.up * y;
        }

        // ------------------------------------------------------------------
        // Celebration FX
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Floating panel
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
    }
}
