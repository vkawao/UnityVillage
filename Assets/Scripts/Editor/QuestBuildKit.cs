using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRVillage.Editor
{
    /// <summary>
    /// Shared building blocks for every house's quest setup (Guitar Shop, Sakura Park, ...):
    /// scene helpers, world-space UI, celebrations, grabbables, generated parts that keep
    /// hand placement, materials, the guide HUD, and BuildingFrame geometry helpers.
    /// Use from a setup file with <c>using static VRVillage.Editor.QuestBuildKit;</c>.
    /// </summary>
    public static class QuestBuildKit
    {
        public const string InterFontPath = "Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF.asset";

        public const string SparkleTexPath = "Assets/Materials/QuestSparkle.png";

        public const string SparkleMatPath = "Assets/Materials/QuestSparkle.mat";

        public static QuestCelebration BuildCelebration(Transform parent, string name, Vector3 position,
            CelebrationStyle style = null)
        {
            style ??= new CelebrationStyle();
            Transform existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject); // rebuild so tweaks here apply

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            // Particle system (child so it can be moved for small sparkles)
            var psGo = new GameObject("Sparkles");
            psGo.transform.SetParent(go.transform, false);
            var ps = psGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = style.Lifetime;
            main.startSpeed = style.Speed;
            main.startSize = style.Size;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = style.Colors;
            main.gravityModifier = style.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            var emission = ps.emission;
            emission.enabled = false; // bursts are triggered from script with Emit()

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = style.Radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = style.Noise;
            noise.frequency = 1.5f;

            var psr = psGo.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = style.Confetti ? GetOrCreateConfettiMaterial() : GetOrCreateSparkleMaterial();

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.5f;
            audio.minDistance = 2f;
            audio.maxDistance = 25f;

            var cel = go.AddComponent<QuestCelebration>();
            var so = new SerializedObject(cel);
            so.FindProperty("m_Particles").objectReferenceValue = ps;
            so.FindProperty("m_AudioSource").objectReferenceValue = audio;
            so.FindProperty("m_BurstCount").intValue = style.Burst;
            so.ApplyModifiedPropertiesWithoutUndo();
            return cel;
        }

        public static Material GetOrCreateSparkleMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatPath);
            if (mat != null) return mat;

            Texture2D tex = GetOrCreateSparkleTexture();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[Quest Setup] URP Particles/Unlit shader not found; sparkles will use the default particle material.");
                return AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            }

            mat = new Material(shader) { name = "QuestSparkle" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // Transparent + additive (glowy) blending.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, SparkleMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Generates a soft 4-point star texture so sparkles don't need an imported image.</summary>
        public static Texture2D GetOrCreateSparkleTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkleTexPath);
            if (existing != null) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 2.5f);
                float rays = Mathf.Clamp01(1f - Mathf.Abs(u) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(v))
                           + Mathf.Clamp01(1f - Mathf.Abs(v) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(u));
                float a = Mathf.Clamp01(glow + rays * 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            System.IO.Directory.CreateDirectory("Assets/Materials");
            System.IO.File.WriteAllBytes(SparkleTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SparkleTexPath);

            if (AssetImporter.GetAtPath(SparkleTexPath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SparkleTexPath);
        }

        public static QuestPanel BuildPanel(Transform parent)
        {
            // Always rebuild so layout changes here are applied on re-run.
            Transform existing = parent.Find("QuestPanel");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var go = new GameObject("QuestPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create Quest Panel");
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1600f, 640f);    // 1.6 m x 0.64 m at 0.001 scale
            rt.localScale = Vector3.one * 0.001f;

            // Background
            var bg = NewUIChild("Background", go.transform);
            Stretch(bg, Vector2.zero, Vector2.zero);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.pixelsPerUnitMultiplier = 0.15f; // softer, rounder corners at this size
            bgImg.color = new Color(0.07f, 0.07f, 0.09f, 0.88f);
            bgImg.raycastTarget = false;

            // Red accent bar on the left (matches the shop sign)
            var accent = NewUIChild("Accent", go.transform);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.offsetMin = new Vector2(32f, 40f);
            accent.offsetMax = new Vector2(52f, -40f);
            var accImg = accent.gameObject.AddComponent<Image>();
            accImg.color = new Color(0.86f, 0.16f, 0.16f, 1f);
            accImg.raycastTarget = false;

            // Text
            var textRt = NewUIChild("Text", go.transform);
            Stretch(textRt, new Vector2(100f, 48f), new Vector2(-64f, -48f));
            var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            var inter = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterFontPath);
            if (inter != null) tmp.font = inter;
            tmp.text = "1. Enter the store";
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 56f;
            tmp.fontSizeMax = 120f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;

            var panel = go.AddComponent<QuestPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("m_Text").objectReferenceValue = tmp;
            so.FindProperty("m_CanvasGroup").objectReferenceValue = go.GetComponent<CanvasGroup>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        public static RectTransform NewUIChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// A direct child of <paramref name="parent"/> by name, created if missing. With no parent,
        /// the object is searched for anywhere in the scene (so roots can be grouped under folders)
        /// and only created at the top level if it doesn't exist yet.
        /// </summary>
        public static GameObject FindOrCreate(Transform parent, string name)
        {
            Transform t = parent != null ? parent.Find(name) : FindInScene(name)?.transform;
            if (t != null) return t.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>First object with this name anywhere in the active scene (inactive ones included), or null.</summary>
        public static GameObject FindInScene(string name)
        {
            foreach (GameObject top in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeepChild(top.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        public static Transform FindOrCreateAnchor(Transform parent, string name, Vector3 worldPos, Quaternion worldRot)
        {
            Transform t = parent.Find(name);
            if (t != null)
            {
                // Keep any manual placement; only aim anchors that were never rotated.
                if (t.rotation == Quaternion.identity) t.rotation = worldRot;
                return t;
            }
            var go = FindOrCreate(parent, name);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go.transform;
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        public static XRGrabInteractable FindGrab(Transform root, string name)
        {
            Transform t = FindDeepChild(root, name);
            return t != null ? t.GetComponent<XRGrabInteractable>() : null;
        }

        public static void SetArray<T>(SerializedProperty prop, IList<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        public static Bounds RendererBounds(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Drop the Y component (horizontal position).</summary>
        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>Horizontal unit direction; falls back to world forward.</summary>
        public static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        public static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform r = FindDeepChild(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        public const string PartsName = "QuestParts";

        public const string KnobMatPath = "Assets/Materials/QuestKnob.mat";

        public const string MetalMatPath = "Assets/Materials/QuestMetal.mat";

        public const string MarkMatPath = "Assets/Materials/QuestKnobMark.mat";

        public const string LedMatPath = "Assets/Materials/QuestLed.mat";

        public const string ConfettiTexPath = "Assets/Materials/QuestConfetti.png";

        public const string ConfettiMatPath = "Assets/Materials/QuestConfetti.mat";

        public const string ArrowTexPath = "Assets/Materials/QuestGuideArrow.png";

        public static readonly Color ButtonBlue = new Color(0.2f, 0.36f, 0.78f);

        public static readonly Color ButtonGreen = new Color(0.16f, 0.65f, 0.3f);

        public static readonly Color PanelDark = new Color(0.07f, 0.07f, 0.09f, 0.92f);

        public static readonly Color AccentRed = new Color(0.86f, 0.16f, 0.16f, 1f);

        /// <summary>
        /// Guide that only works inside this house's area (floor + margin, so the
        /// outside of the door counts too). Lives under the quest root, not on the rig.
        /// </summary>
        public static void BuildGuide(Transform root, BuildingFrame frame, Bounds floorL, float margin = 2.5f)
        {
            Transform guideRoot = FindOrCreate(root, "Guide").transform;
            guideRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            guideRoot.localScale = Vector3.one;

            // From 1 m under the floor to 4 m above it. Measured from the bottom, so passing a whole
            // building (walls and roof) instead of just its floor still puts the box at head height.
            Transform area = FindOrCreate(guideRoot, "HouseArea").transform;
            area.SetPositionAndRotation(
                frame.ToWorld(new Vector3(floorL.center.x, floorL.min.y + 1.5f, floorL.center.z)), frame.Rot);

            RectTransform hud = NewWorldCanvas(guideRoot, "GuideHUD", new Vector2(760f, 300f), false);
            hud.localScale = Vector3.one * 0.0006f; // ~0.46 m wide, 0.8 m away
            var hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;

            RectTransform pill = NewUIChild("TaskLine", hud);
            pill.anchorMin = Vector2.zero;
            pill.anchorMax = new Vector2(1f, 0f);
            pill.pivot = new Vector2(0.5f, 0f);
            pill.offsetMin = Vector2.zero;
            pill.offsetMax = new Vector2(0f, 120f);
            AddRounded(pill, "Background", PanelDark);
            RectTransform accent = NewUIChild("Accent", pill);
            accent.anchorMin = Vector2.zero;
            accent.anchorMax = new Vector2(0f, 1f);
            accent.offsetMin = new Vector2(18f, 22f);
            accent.offsetMax = new Vector2(30f, -22f);
            var accImg = accent.gameObject.AddComponent<Image>();
            accImg.color = AccentRed;
            accImg.raycastTarget = false;
            RectTransform textRt = NewUIChild("Text", pill);
            Stretch(textRt, new Vector2(46f, 10f), new Vector2(-24f, -10f));
            TextMeshProUGUI text = AddText(textRt, "1. Enter the store", 44f, TextAlignmentOptions.MidlineLeft);
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 44f;

            RectTransform arrow = NewUIChild("Arrow", hud);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
            arrow.sizeDelta = new Vector2(150f, 150f);
            arrow.anchoredPosition = new Vector2(0f, -85f);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = GetOrCreateArrowSprite();
            arrowImg.color = new Color(1f, 0.8f, 0.3f);
            arrowImg.raycastTarget = false;
            var arrowGroup = arrow.gameObject.AddComponent<CanvasGroup>();

            QuestGuide guide = GetOrAdd<QuestGuide>(guideRoot.gameObject);
            var so = new SerializedObject(guide);
            so.FindProperty("m_Manager").objectReferenceValue = root.GetComponent<QuestManager>();
            so.FindProperty("m_Area").objectReferenceValue = area;
            so.FindProperty("m_AreaSize").vector3Value =
                new Vector3(floorL.size.x + margin * 2f, 5f, floorL.size.z + margin * 2f);
            so.FindProperty("m_Hud").objectReferenceValue = hud;
            so.FindProperty("m_HudGroup").objectReferenceValue = hudGroup;
            so.FindProperty("m_Text").objectReferenceValue = text;
            so.FindProperty("m_Arrow").objectReferenceValue = arrow;
            so.FindProperty("m_ArrowGroup").objectReferenceValue = arrowGroup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Guide area margin around a house's floor: <paramref name="margin"/>, or 0 if that would
        /// reach into another house's guide area.
        /// </summary>
        public static float GuideMarginAvoidingOthers(Bounds floorL, BuildingFrame frame, Transform ownRoot, float margin = 1f)
        {
            Bounds mine = new Bounds(frame.ToWorld(floorL.center), floorL.size + new Vector3(margin * 2f, 5f, margin * 2f));
            foreach (QuestGuide g in Object.FindObjectsByType<QuestGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
                    Debug.LogWarning($"[Quest Setup] {ownRoot.name}'s guide area would overlap '{g.transform.parent.name}'. Using no margin.");
                    return 0f;
                }
            }
            return margin;
        }

        /// <summary>
        /// Upward-pointing arrow (wide head on a shaft) with a dark outline, imported as a sprite.
        /// The fill is white so the Image colour tints it; the outline stays dark.
        /// </summary>
        public static Sprite GetOrCreateArrowSprite()
        {
            const string oldArrowPath = "Assets/Materials/QuestArrow.png"; // earlier head-only version
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(oldArrowPath) != null) AssetDatabase.DeleteAsset(oldArrowPath);

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowTexPath);
            if (existing != null) return existing;

            const int size = 128;
            const float outline = 0.08f; // in -1..1 units
            const float sharp = 60f;     // edge anti-aliasing
            var dark = new Color(0.08f, 0.08f, 0.1f, 1f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f; // -1..1, left to right
                float v = (y + 0.5f) / size * 2f - 1f; // -1..1, bottom to top

                // Head: tip at v = 0.88, base at v = 0.05, half-width 0.72 at the base.
                float headHalf = (0.88f - v) / 0.83f * 0.72f;
                float head = Mathf.Min((headHalf - Mathf.Abs(u)) * 0.75f, Mathf.Min(v - 0.05f, 0.88f - v));
                // Shaft: from v = -0.88 up into the head, half-width 0.2.
                float shaft = Mathf.Min(0.2f - Mathf.Abs(u), Mathf.Min(v + 0.88f, 0.1f - v));
                float inside = Mathf.Max(head, shaft); // > 0 inside the arrow

                float alpha = Mathf.Clamp01((inside + outline) * sharp);
                float fill = Mathf.Clamp01(inside * sharp);
                Color c = Color.Lerp(dark, Color.white, fill);
                c.a = alpha;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();

            System.IO.Directory.CreateDirectory("Assets/Materials");
            System.IO.File.WriteAllBytes(ArrowTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ArrowTexPath);
            if (AssetImporter.GetAtPath(ArrowTexPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowTexPath);
        }

        public static SerializedObject StepSO(QuestStep step, string text, Transform anchor,
            QuestCelebration celebration = null, GameObject[] showWhileActive = null, float hideDelay = 0f)
        {
            var so = new SerializedObject(step);
            so.FindProperty("m_Instruction").stringValue = text;
            so.FindProperty("m_PanelAnchor").objectReferenceValue = anchor;
            so.FindProperty("m_Celebration").objectReferenceValue = celebration;
            SetArray(so.FindProperty("m_ShowWhileActive"), showWhileActive ?? new GameObject[0]);
            so.FindProperty("m_HideDelay").floatValue = hideDelay;
            return so;
        }

        public static XRGrabInteractable SetupGrabbable(GameObject go, float mass)
        {
            foreach (MeshCollider mc in go.GetComponents<MeshCollider>())
            {
                if (!mc.convex) Undo.DestroyObjectImmediate(mc); // non-convex colliders can't be on a moving body
            }

            Rigidbody rb = GetOrAdd<Rigidbody>(go);
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider box = GetOrAdd<BoxCollider>(go);
            Bounds local = LocalBounds(go.transform);
            box.center = local.center;
            box.size = local.size;

            XRGrabInteractable grab = GetOrAdd<XRGrabInteractable>(go);
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true; // grab where the hand is (pivots of these meshes may be off-centre)

            // Only the item's own box grabs it; knobs/footswitches are separate interactables.
            var so = new SerializedObject(grab);
            SetArray(so.FindProperty("m_Colliders"), new Object[] { box });
            so.ApplyModifiedPropertiesWithoutUndo();
            return grab;
        }

        public static CounterItem SetupCounterItem(GameObject go, string displayName, int price)
        {
            CounterItem item = GetOrAdd<CounterItem>(go);
            var so = new SerializedObject(item);
            so.FindProperty("m_DisplayName").stringValue = displayName;
            so.FindProperty("m_Price").intValue = price;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        /// <summary>Static mesh collider on the object itself (not children: items may sit under it).</summary>
        public static void EnsureStaticCollider(GameObject go)
        {
            if (go.GetComponent<Collider>() != null) return;
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Undo.AddComponent<MeshCollider>(go);
        }

        public static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                return;
            }
            if (es.GetComponent<XRUIInputModule>() != null) return;
            foreach (BaseInputModule m in es.GetComponents<BaseInputModule>()) m.enabled = false;
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
        }

        /// <summary>Local poses of the previous QuestParts children, so hand placement survives a re-run.</summary>
        public static readonly Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> s_SavedParts =
            new Dictionary<string, (Vector3, Quaternion, Vector3)>();

        public static Transform RebuildParts(Transform owner)
        {
            s_SavedParts.Clear();
            Transform existing = owner.Find(PartsName);
            if (existing != null)
            {
                foreach (Transform child in existing)
                    s_SavedParts[child.name] = (child.localPosition, child.localRotation, child.localScale);
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
            var go = new GameObject(PartsName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + PartsName);
            go.transform.SetParent(owner, false);
            return go.transform;
        }

        /// <summary>If this part existed before the rebuild, put it back where it was (moved/rotated/scaled by hand).</summary>
        public static void KeepPlacement(Transform part)
        {
            if (!s_SavedParts.TryGetValue(part.name, out var saved)) return;
            part.localPosition = saved.pos;
            part.localRotation = saved.rot;
            part.localScale = saved.scale;
        }

        /// <summary>Primitive without a collider. Position/scale are world values when parent is null, else local.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static RectTransform NewWorldCanvas(Transform parent, string name, Vector2 size, bool interactive)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject); // rebuild so layout tweaks apply

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) canvas.worldCamera = Camera.main;
            if (interactive) go.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.001f;
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            return rt;
        }

        public static Image AddRounded(RectTransform parent, string name, Color color, bool raycast = false)
        {
            RectTransform rt = NewUIChild(name, parent);
            Stretch(rt, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.15f;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI AddText(RectTransform rt, string text, float size, TextAlignmentOptions align,
            FontStyles style = FontStyles.Normal)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var inter = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterFontPath);
            if (inter != null) tmp.font = inter;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button AddButton(RectTransform parent, string label, Color color, float fontSize)
        {
            Image img = AddRounded(parent, "Button", color, true);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white; // scripts recolour disabled buttons themselves
            cb.fadeDuration = 0.08f;
            button.colors = cb;

            RectTransform labelRt = NewUIChild("Label", img.rectTransform);
            Stretch(labelRt, new Vector2(12f, 8f), new Vector2(-12f, -8f));
            AddText(labelRt, label, fontSize, TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        public static Button BuildButtonCanvas(Transform anchor, string name, Vector2 size, string label, Color color, float fontSize)
        {
            RectTransform canvas = NewWorldCanvas(anchor, name, size, true);
            return AddButton(canvas, label, color, fontSize);
        }

        public static GameObject BuildHereMarker(Transform anchor, string text = "Here!")
        {
            RectTransform canvas = NewWorldCanvas(anchor, "HereMarker", new Vector2(420f, 260f), false);
            canvas.gameObject.AddComponent<QuestBob>();

            RectTransform box = NewUIChild("Box", canvas);
            box.anchorMin = new Vector2(0f, 0.3f);
            box.anchorMax = Vector2.one;
            box.offsetMin = box.offsetMax = Vector2.zero;
            AddRounded(box, "Background", AccentRed);
            RectTransform textRt = NewUIChild("Text", box);
            Stretch(textRt, Vector2.zero, Vector2.zero);
            TextMeshProUGUI hereText = AddText(textRt, text, 110f, TextAlignmentOptions.Center, FontStyles.Bold);
            hereText.enableAutoSizing = true;
            hereText.fontSizeMin = 50f;
            hereText.fontSizeMax = 110f;

            // Downward pointer: a rotated square poking out under the box.
            RectTransform arrow = NewUIChild("Arrow", canvas);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.3f);
            arrow.sizeDelta = new Vector2(70f, 70f);
            arrow.anchoredPosition = new Vector2(0f, -5f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
            arrow.SetAsFirstSibling();
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.color = AccentRed;
            arrowImg.raycastTarget = false;
            return canvas.gameObject;
        }

        public static Material GetOrCreateMaterial(string path, string shaderName, Color color, float smoothness = 0.5f, float metallic = 0f)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material GetOrCreateConfettiMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ConfettiMatPath);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return GetOrCreateSparkleMaterial();

            mat = new Material(shader) { name = "QuestConfetti" };
            mat.SetTexture("_BaseMap", GetOrCreateConfettiTexture());
            mat.SetColor("_BaseColor", Color.white);
            // Transparent, normal alpha blending (solid paper look, not glowy).
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, ConfettiMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Small white rounded rectangle (a confetti strip).</summary>
        public static Texture2D GetOrCreateConfettiTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ConfettiTexPath);
            if (existing != null) return existing;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float a = Mathf.Clamp01((0.9f - u) * 12f) * Mathf.Clamp01((0.5f - v) * 12f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            System.IO.Directory.CreateDirectory("Assets/Materials");
            System.IO.File.WriteAllBytes(ConfettiTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ConfettiTexPath);
            if (AssetImporter.GetAtPath(ConfettiTexPath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ConfettiTexPath);
        }

        public class CelebrationStyle
        {
            public ParticleSystem.MinMaxCurve Lifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            public ParticleSystem.MinMaxCurve Speed = new ParticleSystem.MinMaxCurve(0.6f, 2.4f);
            public ParticleSystem.MinMaxCurve Size = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            public ParticleSystem.MinMaxGradient Colors =
                new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.97f, 0.85f));
            public float Gravity = 0.25f;
            public float Radius = 0.35f;
            public float Noise = 0.35f;
            public int Burst = 160;
            public bool Confetti;

            public static CelebrationStyle ConfettiStyle()
            {
                var g = new Gradient();
                g.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f),
                        new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.2f),
                        new GradientColorKey(new Color(0.3f, 0.9f, 0.4f), 0.4f),
                        new GradientColorKey(new Color(0.25f, 0.75f, 1f), 0.6f),
                        new GradientColorKey(new Color(0.6f, 0.35f, 1f), 0.8f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.85f), 1f),
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return new CelebrationStyle
                {
                    Lifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f),
                    Speed = new ParticleSystem.MinMaxCurve(1.2f, 3f),
                    Size = new ParticleSystem.MinMaxCurve(0.025f, 0.045f),
                    Colors = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor },
                    Gravity = 0.6f,
                    Radius = 0.15f,
                    Noise = 0.6f,
                    Burst = 110,
                    Confetti = true,
                };
            }

            public static CelebrationStyle GreenStyle()
            {
                return new CelebrationStyle
                {
                    Speed = new ParticleSystem.MinMaxCurve(1f, 3.2f),
                    Size = new ParticleSystem.MinMaxCurve(0.035f, 0.09f),
                    Colors = new ParticleSystem.MinMaxGradient(new Color(0.2f, 1f, 0.35f), new Color(0.75f, 1f, 0.8f)),
                    Radius = 0.45f,
                    Burst = 280,
                };
            }
        }

        /// <summary>
        /// The shop's horizontal frame: origin at GuitarCenter, rotated to match the
        /// shop's walls (yaw only), unit scale. Directions are snapped to its axes.
        /// </summary>
        public readonly struct BuildingFrame
        {
            public readonly Vector3 Origin;
            public readonly Quaternion Rot;

            public BuildingFrame(Transform shop)
            {
                Origin = shop.position;
                // Use whichever of the shop's axes is most horizontal as "forward" (FBX roots are often pitched -90).
                Vector3 best = Vector3.forward;
                float bestLen = 0f;
                foreach (Vector3 axis in new[] { shop.forward, shop.right, shop.up })
                {
                    Vector3 flat = new Vector3(axis.x, 0f, axis.z);
                    if (flat.magnitude > bestLen) { bestLen = flat.magnitude; best = flat; }
                }
                Rot = Quaternion.LookRotation(best.normalized, Vector3.up);
            }

            public Vector3 ToWorld(Vector3 local) => Origin + Rot * local;
            public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(Rot) * (world - Origin);
            public Vector3 LocalDir(Vector3 worldDir) => Quaternion.Inverse(Rot) * worldDir;

            /// <summary>Horizontal world direction snapped to the nearest shop axis.</summary>
            public Vector3 Snap(Vector3 worldDir)
            {
                Vector3 l = LocalDir(worldDir);
                Vector3 snapped = Mathf.Abs(l.x) >= Mathf.Abs(l.z)
                    ? new Vector3(Mathf.Sign(l.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(l.z));
                return Rot * snapped;
            }

            /// <summary>Bounds of t's renderers in frame space (ignores generated QuestParts).</summary>
            public Bounds BoundsOf(Transform t)
            {
                bool any = false;
                var b = new Bounds(ToLocal(t.position), Vector3.zero);
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsQuestPart(r.transform, t) || r is ParticleSystemRenderer) continue;
                    foreach (Vector3 corner in Corners(r.localBounds))
                    {
                        Vector3 p = ToLocal(r.transform.TransformPoint(corner));
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                        else b.Encapsulate(p);
                    }
                }
                return b;
            }
        }

        /// <summary>Bounds of t's renderers in t's own local space (ignores generated QuestParts).</summary>
        public static Bounds LocalBounds(Transform t)
        {
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (IsQuestPart(r.transform, t)) continue;
                foreach (Vector3 corner in Corners(r.localBounds))
                {
                    Vector3 p = t.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        public static bool IsQuestPart(Transform t, Transform stopAt)
        {
            for (Transform p = t; p != null && p != stopAt; p = p.parent)
            {
                if (p.name == PartsName) return true;
            }
            return false;
        }

        public static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
                yield return c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
        }

        /// <summary>Half-size of frame-space bounds along a frame axis direction.</summary>
        public static float Extent(Bounds b, Vector3 axisL) =>
            Mathf.Abs(axisL.x) * b.extents.x + Mathf.Abs(axisL.y) * b.extents.y + Mathf.Abs(axisL.z) * b.extents.z;

        /// <summary>Snapped direction from something against a wall into the room.</summary>
        public static Vector3 InwardDir(BuildingFrame frame, Transform t, Vector3 floorCenter)
        {
            Vector3 c = RendererBounds(t).center;
            return frame.Snap(floorCenter - c);
        }

        public static Vector3 AverageCenter(List<Transform> ts)
        {
            Vector3 sum = Vector3.zero;
            foreach (Transform t in ts) sum += RendererBounds(t).center;
            return sum / Mathf.Max(ts.Count, 1);
        }

        /// <summary>The closest transform (by renderer bounds, horizontally) within 2 m, or null.</summary>
        public static Transform Nearest(List<Transform> ts, Vector3 point)
        {
            Transform best = null;
            float bestDist = 2f;
            foreach (Transform t in ts)
            {
                Bounds b = RendererBounds(t);
                Vector3 closest = b.ClosestPoint(point);
                float d = Vector2.Distance(new Vector2(closest.x, closest.z), new Vector2(point.x, point.z));
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }
        // ------------------------------------------------------------------
        // Model features (find a slot / screen / panel by its material)
        // ------------------------------------------------------------------

        /// <summary>
        /// Model meshes are read at setup time, so make sure the importer keeps a CPU copy.
        /// Returns true if the model had to be reimported.
        /// </summary>
        public static bool EnsureReadable(Renderer anyRendererOfModel)
        {
            var mf = anyRendererOfModel != null ? anyRendererOfModel.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.isReadable) return false;
            string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"[Quest Setup] Enabled Read/Write on {path} so its features can be located.");
            return true;
        }

        /// <summary>
        /// Frame-space bounds of the frontmost faces of one material that face <paramref name="frontW"/>
        /// (e.g. a slot inset, a screen, a metal plate). False if the material isn't on this renderer.
        /// </summary>
        public static bool FrontFaces(Renderer r, string material, Vector3 frontW, BuildingFrame frame, out Bounds frameBounds)
        {
            frameBounds = default;
            if (!TryGetTriangles(r, material, out Vector3[] world, out int[] tris)) return false;

            var picked = new List<(float d, Vector3 a, Vector3 b, Vector3 c)>();
            float dMax = float.MinValue;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = world[tris[i]], b = world[tris[i + 1]], c = world[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f || Vector3.Dot(n.normalized, frontW) < 0.8f) continue;
                float d = Vector3.Dot((a + b + c) / 3f, frontW);
                picked.Add((d, a, b, c));
                dMax = Mathf.Max(dMax, d);
            }
            bool any = false;
            foreach (var t in picked)
            {
                if (t.d < dMax - 0.01f) continue;
                foreach (Vector3 p in new[] { t.a, t.b, t.c })
                {
                    Vector3 l = frame.ToLocal(p);
                    if (!any) { frameBounds = new Bounds(l, Vector3.zero); any = true; }
                    else frameBounds.Encapsulate(l);
                }
            }
            return any;
        }

        /// <summary>
        /// Frame-space bounds of every face of one material (e.g. the inside of a dispenser tray).
        /// Triangles smaller than <paramref name="minTriangleArea"/> (m^2) are ignored, so small
        /// details sharing the material (a coin slit) don't stretch the bounds.
        /// </summary>
        public static bool MaterialBounds(Renderer r, string material, BuildingFrame frame, out Bounds frameBounds,
            float minTriangleArea = 0f)
        {
            frameBounds = default;
            if (!TryGetTriangles(r, material, out Vector3[] world, out int[] tris)) return false;
            bool any = false;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = world[tris[i]], b = world[tris[i + 1]], c = world[tris[i + 2]];
                if (Vector3.Cross(b - a, c - a).magnitude * 0.5f < minTriangleArea) continue;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    Vector3 l = frame.ToLocal(p);
                    if (!any) { frameBounds = new Bounds(l, Vector3.zero); any = true; }
                    else frameBounds.Encapsulate(l);
                }
            }
            return any;
        }

        private static bool TryGetTriangles(Renderer r, string material, out Vector3[] world, out int[] tris)
        {
            world = null;
            tris = null;
            var mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return false;
            Mesh mesh = mf.sharedMesh;
            Material[] mats = r.sharedMaterials;
            int sub = -1;
            for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
            {
                if (mats[i] != null && MaterialNameIs(mats[i], material)) { sub = i; break; }
            }
            if (sub < 0) return false;
            Vector3[] verts = mesh.vertices;
            world = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) world[i] = r.transform.TransformPoint(verts[i]);
            tris = mesh.GetTriangles(sub);
            return true;
        }

        /// <summary>Material asset used by a renderer slot with this name (to reuse the model's own colours).</summary>
        public static Material FindMaterial(Renderer r, string name)
        {
            if (r == null) return null;
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && MaterialNameIs(m, name)) return m;
            }
            return null;
        }

        /// <summary>
        /// Blender material name match. Extracted materials get " 1", " 2"... when the name is already
        /// taken in the folder (e.g. the cafe's "Steel" became "Steel 1"), so that suffix is ignored.
        /// </summary>
        public static bool MaterialNameIs(Material m, string name)
        {
            string n = m.name;
            if (n == name) return true;
            if (n.Length <= name.Length + 1 || !n.StartsWith(name) || n[name.Length] != ' ') return false;
            for (int i = name.Length + 1; i < n.Length; i++)
            {
                if (!char.IsDigit(n[i])) return false;
            }
            return true;
        }

        /// <summary>Swap one material slot (by name) on a renderer; records the override on prefab instances.</summary>
        public static void ReplaceMaterial(Renderer r, string slotName, Material with)
        {
            if (r == null || with == null) return;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && (mats[i].name == slotName || mats[i] == with)) { changed |= mats[i] != with; mats[i] = with; }
            }
            if (!changed) return;
            Undo.RecordObject(r, "Recolour " + r.name);
            r.sharedMaterials = mats;
            if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }

        /// <summary>
        /// An <see cref="VRVillage.ItemTrigger"/> box: centre in frame space, size as
        /// (width across, height, depth along front). Its -Z faces the player (front).
        /// </summary>
        public static ItemTrigger BuildItemTrigger(Transform parent, string name, BuildingFrame frame, Vector3 centerL,
            Vector3 size, Vector3 frontW)
        {
            GameObject go = FindOrCreate(parent, name);
            go.transform.SetPositionAndRotation(frame.ToWorld(centerL), Quaternion.LookRotation(-frontW, Vector3.up));
            ItemTrigger trigger = GetOrAdd<ItemTrigger>(go);
            var so = new SerializedObject(trigger);
            so.FindProperty("m_Size").vector3Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        /// <summary>Width across <paramref name="frontL"/> (horizontal) of frame-space bounds.</summary>
        public static float Across(Bounds b, Vector3 frontL) => Extent(b, Vector3.Cross(Vector3.up, frontL)) * 2f;

        private const string RingTexPath = "Assets/Materials/QuestRing.png";

        /// <summary>Soft white ring (e.g. a hip-pocket marker), imported as a sprite.</summary>
        public static Sprite GetOrCreateRingSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingTexPath);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.1f);
                float glow = Mathf.Clamp01(1f - r) * 0.18f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + glow)));
            }
            tex.Apply();

            System.IO.Directory.CreateDirectory("Assets/Materials");
            System.IO.File.WriteAllBytes(RingTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RingTexPath);
            if (AssetImporter.GetAtPath(RingTexPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingTexPath);
        }

        /// <summary>
        /// A <see cref="VRVillage.ScreenFader"/> under the house root: a big black world-space
        /// panel that's held in front of the camera only while fading.
        /// </summary>
        public static ScreenFader BuildScreenFader(Transform root)
        {
            GameObject go = FindOrCreate(root, "ScreenFader");
            ScreenFader fader = GetOrAdd<ScreenFader>(go);
            RectTransform panel = NewWorldCanvas(go.transform, "Panel", new Vector2(2000f, 2000f), false);
            panel.localScale = Vector3.one * 0.001f; // 2 m square at 0.12 m covers any headset's view
            Canvas canvas = panel.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            RectTransform black = NewUIChild("Black", panel);
            Stretch(black, Vector2.zero, Vector2.zero);
            var img = black.gameObject.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            var so = new SerializedObject(fader);
            so.FindProperty("m_Group").objectReferenceValue = group;
            so.FindProperty("m_Panel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fader;
        }

        // ------------------------------------------------------------------
        // Hip holster
        // ------------------------------------------------------------------
        /// <summary>
        /// A hip pocket under the house root that follows the player (see <see cref="VRVillage.HipHolster"/>),
        /// accepting only the listed items. Its ring only shows inside the house area.
        /// With <paramref name="dropZone"/>, a glowing ball + "Put it here" label also shows where to let go
        /// while you hold one of those items, and the catch radius is bigger.
        /// </summary>
        public static HipHolster BuildHolster(Transform root, QuestGuide guide, XRGrabInteractable[] accepts, bool dropZone = false)
        {
            GameObject go = FindOrCreate(root, "HipHolster");
            HipHolster holster = GetOrAdd<HipHolster>(go);

            GameObject socketGo = FindOrCreate(go.transform, "Socket");
            socketGo.transform.localPosition = Vector3.zero;
            socketGo.transform.localRotation = Quaternion.identity;
            SphereCollider sphere = GetOrAdd<SphereCollider>(socketGo);
            sphere.isTrigger = true;
            sphere.radius = dropZone ? 0.18f : 0.13f;
            HolsterSocket socket = GetOrAdd<HolsterSocket>(socketGo);
            Transform attach = FindOrCreate(socketGo.transform, "Attach").transform;
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.Euler(0f, 0f, 90f); // card stands upright against the hip
            socket.attachTransform = attach;
            socket.showInteractableHoverMeshes = true;
            var soS = new SerializedObject(socket);
            SetArray(soS.FindProperty("m_Accepts"), accepts);
            soS.ApplyModifiedPropertiesWithoutUndo();

            // Ring + label lying flat, readable when you look down at your hip.
            RectTransform visual = NewWorldCanvas(go.transform, "Visual", new Vector2(300f, 360f), false);
            visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.localScale = Vector3.one * 0.0008f;
            RectTransform ringRt = NewUIChild("Ring", visual);
            ringRt.anchorMin = new Vector2(0f, 0.2f);
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = ringRt.offsetMax = Vector2.zero;
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = GetOrCreateRingSprite();
            ring.preserveAspect = true;
            ring.color = new Color(1f, 0.85f, 0.35f, 0.85f);
            ring.raycastTarget = false;
            RectTransform label = NewUIChild("Label", visual);
            label.anchorMin = Vector2.zero;
            label.anchorMax = new Vector2(1f, 0.22f);
            label.offsetMin = label.offsetMax = Vector2.zero;
            AddText(label, "Hip pocket", 52f, TextAlignmentOptions.Center, FontStyles.Bold);

            Transform oldZone = go.transform.Find("DropZone");
            if (oldZone != null) Undo.DestroyObjectImmediate(oldZone.gameObject); // rebuilt so tweaks here apply
            GameObject zone = dropZone ? BuildDropZone(go.transform, sphere.radius) : null;

            var so = new SerializedObject(holster);
            so.FindProperty("m_Socket").objectReferenceValue = socket;
            so.FindProperty("m_Visual").objectReferenceValue = visual.gameObject;
            so.FindProperty("m_Guide").objectReferenceValue = guide;
            so.FindProperty("m_DropZone").objectReferenceValue = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
            return holster;
        }

        /// <summary>See-through glowing ball the size of the pocket's catch area, with a label above it.</summary>
        private static GameObject BuildDropZone(Transform holster, float radius)
        {
            var zone = new GameObject("DropZone");
            Undo.RegisterCreatedObjectUndo(zone, "Create DropZone");
            zone.transform.SetParent(holster, false);
            Material glow = GetOrCreateTransparentUnlitMaterial("Assets/Materials/QuestPocketGlow.mat", new Color(1f, 0.82f, 0.25f, 0.28f));
            GameObject ball = Primitive(PrimitiveType.Sphere, "Ball", zone.transform, Vector3.zero, Vector3.one * radius * 2f, glow);
            ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            RectTransform label = NewWorldCanvas(zone.transform, "Label", new Vector2(520f, 130f), false);
            label.localPosition = Vector3.up * (radius + 0.07f);
            label.localScale = Vector3.one * 0.0005f;
            AddRounded(label, "Background", PanelDark);
            RectTransform textRt = NewUIChild("Text", label);
            Stretch(textRt, new Vector2(10f, 6f), new Vector2(-10f, -6f));
            TextMeshProUGUI text = AddText(textRt, "Hip pocket: let go here", 60f, TextAlignmentOptions.Center, FontStyles.Bold);
            text.color = new Color(1f, 0.85f, 0.35f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 30f;
            text.fontSizeMax = 60f;
            return zone;
        }

        /// <summary>See-through unlit colour (alpha from the colour), created once.</summary>
        public static Material GetOrCreateTransparentUnlitMaterial(string path, Color color)
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

        /// <summary>Fill each step's guide task line (and optional fixed arrow target), in step order.</summary>
        public static void SetGuideTexts(IList<Object> steps, string[] shortTexts, IDictionary<Object, Transform> targets = null)
        {
            for (int i = 0; i < steps.Count && i < shortTexts.Length; i++)
            {
                var so = new SerializedObject(steps[i]);
                so.FindProperty("m_ShortInstruction").stringValue = shortTexts[i];
                Transform target = null;
                if (targets != null) targets.TryGetValue(steps[i], out target);
                so.FindProperty("m_GuideTarget").objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
