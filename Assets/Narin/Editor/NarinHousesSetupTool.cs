#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace NarinVillage.Editor
{
    /// <summary>
    /// One-click Unity Editor builder for the VR Village project.
    /// Fixes all 5 Houses, collisions, God Mode, door/window/fridge hinges, hollow Basket/Drawer colliders,
    /// Garage door submesh extraction & Car Lift alignment, Shop scale & UI, and Backrooms house door & teleport.
    /// </summary>
    public static class NarinHousesSetupTool
    {
        private const string HousesFolder = "Assets/Narin/Houses";
        private const string ExternalTexFolder = "Assets/Narin/Houses/ExternalTextures";
        private const string MaterialsFolder = "Assets/Narin/Houses/GeneratedMaterials";

        private const string VillageScenePath = "Assets/Village.unity";
        private const string AreaName = "NarinVillageArea";
        // Clear of the other houses; the area faces +Z (toward the rest of the village). Move it freely afterwards.
        private static readonly Vector3 DefaultAreaPosition = new Vector3(20f, 0f, -60f);
        private static readonly Quaternion DefaultAreaRotation = Quaternion.Euler(0f, 180f, 0f);

        // Village-side parts that move together with the area; the Backrooms dimension stays far away.
        private static readonly string[] AreaParts =
        {
            "Village_SpawnPoint", "House1_StorageFacility", "House2_ResidentialHouse",
            "House3_ConvenienceShop", "House4_MechanicsShop", "House5_Backrooms_VillageExterior",
            "Village_Master_Control_Board"
        };

        [MenuItem("VR Village/Setup Narin Houses", false, 60)]
        public static void BuildCompleteVillageScene()
        {
            if (EditorSceneManager.GetActiveScene().path != VillageScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(VillageScenePath);
            }
            Scene scene = SceneManager.GetActiveScene();

            AssetDatabase.Refresh();
            EnsureFolder(MaterialsFolder);

            // 1. Configure Model Importers so meshes are readable & clean
            ConfigureAllBlendImporters();

            // 2. Rebuild the root, keeping where the user placed the village area
            // (and each house's own placement, e.g. from VR Village/Arrange Village Layout)
            Vector3 areaPos = DefaultAreaPosition;
            Quaternion areaRot = DefaultAreaRotation;
            var partPlacement = new Dictionary<string, Pose>();
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                if (top.name != NarinScope.RootName) continue;
                Transform oldArea = top.transform.Find(AreaName);
                if (oldArea != null)
                {
                    areaPos = oldArea.position;
                    areaRot = oldArea.rotation;
                    foreach (string partName in AreaParts)
                    {
                        Transform oldPart = oldArea.Find(partName);
                        if (oldPart != null) partPlacement[partName] = new Pose(oldPart.position, oldPart.rotation);
                    }
                }
                UnityEngine.Object.DestroyImmediate(top);
            }

            GameObject worldRoot = new GameObject(NarinScope.RootName);

            // 3. Create VillageActionManager
            GameObject managerGO = new GameObject("VillageActionManager");
            managerGO.transform.SetParent(worldRoot.transform);
            VillageActionManager actionManager = managerGO.AddComponent<VillageActionManager>();

            // 4. Spawn points: the village one is where "reset / back from the Backrooms" lands
            GameObject villageSpawn = new GameObject("Village_SpawnPoint");
            villageSpawn.transform.SetParent(worldRoot.transform);
            villageSpawn.transform.position = new Vector3(0f, 0.65f, -12f);
            villageSpawn.transform.rotation = Quaternion.identity;
            actionManager.villageSpawnPoint = villageSpawn.transform;

            GameObject backroomsSpawn = new GameObject("Backrooms_SpawnPoint");
            backroomsSpawn.transform.SetParent(worldRoot.transform);
            backroomsSpawn.transform.position = new Vector3(2003.3f, 0.65f, 2002.7f);
            backroomsSpawn.transform.rotation = Quaternion.identity;
            actionManager.backroomsSpawnPoint = backroomsSpawn.transform;

            // 5. Interaction manager / EventSystem only if the scene has none (the shared rig is left alone)
            SetupXROriginAndEventSystem(worldRoot.transform, actionManager);

            // 6. Narin's plaza and trees are not built: the shared village ground and roads replace them

            // 7. Build the 5 Houses & Wire Up All 50 Actions
            BuildHouse1_StorageFacility(worldRoot.transform, actionManager);
            BuildHouse2_ResidentialHouse(worldRoot.transform, actionManager);
            BuildHouse3_ConvenienceShop(worldRoot.transform, actionManager);
            BuildHouse4_MechanicsShop(worldRoot.transform, actionManager);
            BuildHouse5_Backrooms(worldRoot.transform, actionManager);

            // 8. Build Central Master Progress & Debug Hub (God Mode + Pause Entity AI + Teleports)
            BuildCentralDebugAndProgressHub(worldRoot.transform, actionManager);

            // 9. Group the village-side parts so the whole village can be moved as one
            GameObject area = new GameObject(AreaName);
            area.transform.SetParent(worldRoot.transform, false);
            foreach (string partName in AreaParts)
            {
                Transform part = worldRoot.transform.Find(partName);
                if (part != null) part.SetParent(area.transform, true);
                else Debug.LogWarning($"[Narin] {partName} was not built.");
            }
            area.transform.SetPositionAndRotation(areaPos, areaRot);
            foreach (var kv in partPlacement)
            {
                Transform part = area.transform.Find(kv.Key);
                if (part != null) part.SetPositionAndRotation(kv.Value.position, kv.Value.rotation);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Narin] Built Narin's 5 houses under {NarinScope.RootName}/{AreaName} at {areaPos}.");
            if (!suppressDialog && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Narin's houses built",
                    $"Built under {NarinScope.RootName} in Village.unity.\n\n" +
                    $"Move or rotate {NarinScope.RootName}/{AreaName} to place the village part; " +
                    "re-running this menu keeps that placement. The Backrooms dimension stays far away at (2000, 0, 2000).",
                    "OK");
            }
        }

        public static bool suppressDialog = false;

        [MenuItem("VR Village/Narin Debug/Toggle God Mode (Monster Invisibility)", false, 61)]
        public static void ToggleGodModeMenu()
        {
            VillageActionManager mgr = UnityEngine.Object.FindFirstObjectByType<VillageActionManager>();
            if (mgr != null)
            {
                mgr.ToggleGodMode();
                EditorUtility.SetDirty(mgr);
            }
        }

        [MenuItem("VR Village/Narin Debug/Toggle Pause Backrooms Entity AI", false, 62)]
        public static void TogglePauseMonsterMenu()
        {
            VillageActionManager mgr = UnityEngine.Object.FindFirstObjectByType<VillageActionManager>();
            if (mgr != null)
            {
                mgr.ToggleMonsterPause();
                EditorUtility.SetDirty(mgr);
            }
        }

        [MenuItem("VR Village/Narin Debug/Complete All 50 Actions", false, 63)]
        public static void CompleteAllActionsDebug()
        {
            VillageActionManager mgr = UnityEngine.Object.FindFirstObjectByType<VillageActionManager>();
            if (mgr != null)
            {
                for (int h = 1; h <= 5; h++)
                    for (int a = 1; a <= 10; a++)
                        mgr.CompleteAction(h, a);
            }
        }

        private static void ConfigureAllBlendImporters()
        {
            AssetDatabase.ImportAsset($"{HousesFolder}/Mechanics shop/Mechanic.blend", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{HousesFolder}/House/House.blend", ImportAssetOptions.ForceUpdate);

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { HousesFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)) continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }

        // =====================================================================
        // EXACT BLENDER PER-SLOT MATERIAL & TEXTURE RESOLVER
        // =====================================================================
        private static Material GetTexMat(string matSaveName, string texAssetPath, float smoothness = 0.2f)
        {
            string matPath = $"{MaterialsFolder}/M_{matSaveName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = urpLit;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);
            if (tex == null)
            {
                AssetDatabase.ImportAsset(texAssetPath, ImportAssetOptions.ForceUpdate);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);
            }
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateColorMaterial(string name, Color color, float smoothness = 0.25f, bool emissive = false, bool transparent = false)
        {
            string matPath = $"{MaterialsFolder}/M_{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = urpLit;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.2f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ApplyExactHouseMaterials(GameObject root, int houseNumber)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string objName = r.gameObject.name;
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slotName = slots[i] != null ? slots[i].name.Replace(" (Instance)", "").Trim() : "";
                    Material resolved = ResolveSlotMaterial(houseNumber, objName, slotName, i);
                    if (resolved != null)
                    {
                        slots[i] = resolved;
                    }
                }
                r.sharedMaterials = slots;
            }
        }

        private static Material ResolveSlotMaterial(int houseNumber, string objName, string slotName, int slotIndex)
        {
            switch (houseNumber)
            {
                case 1: // HOUSE 1: STORAGE UNIT
                    if (slotName.StartsWith("Material.002") || objName.StartsWith("Storage Unit"))
                        return GetTexMat("H1_StorageUnit", $"{HousesFolder}/Storage Unit/Storage Unit Color.png");
                    if (slotName.StartsWith("Material.003") || objName.StartsWith("Door"))
                        return GetTexMat("H1_Door", $"{HousesFolder}/Storage Unit/Door.png", 0.4f);
                    if (slotName.StartsWith("Wooden Table") || objName.StartsWith("Table"))
                        return GetTexMat("H1_Table", $"{HousesFolder}/Storage Unit/Table.png");
                    if (slotName.StartsWith("Material.001") || objName.StartsWith("Box"))
                        return GetTexMat("H1_Box", $"{HousesFolder}/Storage Unit/Box.png");
                    if (slotName.StartsWith("Trash") || objName.StartsWith("Trash"))
                        return GetTexMat("H1_Trash", $"{HousesFolder}/Storage Unit/Trash.png", 0.45f);
                    if (slotName.StartsWith("L Block") || objName.StartsWith("Tetris L"))
                        return GetTexMat("H1_TetrisL", $"{HousesFolder}/Storage Unit/L Block Base Color.png", 0.35f);
                    if (slotName.StartsWith("T Block") || objName.StartsWith("Tetris T"))
                        return GetTexMat("H1_TetrisT", $"{HousesFolder}/Storage Unit/T Block Base Color.png", 0.35f);
                    if (slotName.Equals("Material") || objName.StartsWith("Tetris Line"))
                        return GetTexMat("H1_TetrisLine", $"{HousesFolder}/Storage Unit/Tetris Line Base Color.png", 0.35f);
                    if (slotName.StartsWith("Cube Puzzle") || objName.StartsWith("Cube Puzzle"))
                        return CreateColorMaterial("H1_CubePuzzle", new Color(0.18f, 0.24f, 0.88f), 0.4f);
                    if (slotName.StartsWith("Pyramid Puzzle") || objName.StartsWith("Pyramid Puzzle"))
                        return CreateColorMaterial("H1_PyramidPuzzle", new Color(0.92f, 0.72f, 0.12f), 0.4f);
                    if (slotName.StartsWith("Sphere Puzzle") || objName.StartsWith("Sphere Puzzle"))
                        return CreateColorMaterial("H1_SpherePuzzle", new Color(0.10f, 0.58f, 0.10f), 0.4f);
                    if (slotName.StartsWith("Puzzle") || objName.StartsWith("Puzzle"))
                        return slotIndex == 1
                            ? GetTexMat("H1_Table", $"{HousesFolder}/Storage Unit/Table.png")
                            : CreateColorMaterial("H1_PuzzleFrame", new Color(0.85f, 0.24f, 0.18f), 0.3f);
                    break;

                case 2: // HOUSE 2: RESIDENTIAL HOUSE
                    // Check separated Door and Doorbell objects FIRST because they retain all 5 material slots from House
                    if (objName.Equals("Door", StringComparison.OrdinalIgnoreCase) || objName.Contains("Front_Door"))
                        return GetTexMat("H2_Door", $"{HousesFolder}/House/Door Base Color.png", 0.3f);
                    if (objName.Equals("Doorbell", StringComparison.OrdinalIgnoreCase) || objName.Contains("Doorbell"))
                        return GetTexMat("H2_Doorbell", $"{HousesFolder}/House/Doorbell Base Color.png", 0.4f);
                    if (slotName.StartsWith("Roof"))
                        return GetTexMat("H2_Roof", $"{ExternalTexFolder}/360_F_226990194_EEfT36yI2MoQpvlCQj6GmQh5feXx4hK9.jpg", 0.15f);
                    if (slotName.StartsWith("Walls"))
                        return GetTexMat("H2_Walls", $"{HousesFolder}/House/Walls Base Color.png", 0.2f);
                    if (slotName.StartsWith("Floor"))
                        return GetTexMat("H2_Floor", $"{HousesFolder}/House/Floor.jpg", 0.3f);
                    if (slotName.Equals("Door"))
                        return GetTexMat("H2_Door", $"{HousesFolder}/House/Door Base Color.png", 0.3f);
                    if (slotName.StartsWith("Doorbell"))
                        return GetTexMat("H2_Doorbell", $"{HousesFolder}/House/Doorbell Base Color.png", 0.4f);
                    if (slotName.StartsWith("Wood") || objName.StartsWith("Table"))
                        return GetTexMat("H2_TableWood", $"{HousesFolder}/House/Table.jpg", 0.3f);
                    if (slotName.StartsWith("Stove") || objName.StartsWith("Stove"))
                        return GetTexMat("H2_Stove", $"{HousesFolder}/House/Stove Base Color.png", 0.5f);
                    if (slotName.StartsWith("Pan") || objName.StartsWith("Pan"))
                        return GetTexMat("H2_Pan", $"{HousesFolder}/House/Pan Base Color.png", 0.6f);
                    if (slotName.StartsWith("Bacon") || objName.StartsWith("bacon"))
                        return GetTexMat("H2_Bacon", $"{HousesFolder}/House/Bacon Base Color.png", 0.35f);
                    if (slotName.StartsWith("Boombox") || objName.StartsWith("Boombox"))
                        return GetTexMat("H2_Boombox", $"{HousesFolder}/House/Boombox Base Color.png", 0.4f);
                    if (slotName.StartsWith("Window") || objName.Contains("Window"))
                        return CreateColorMaterial("H2_WindowGlass", new Color(0.65f, 0.85f, 0.96f, 0.38f), 0.85f, false, true);
                    if (slotName.StartsWith("Toilet") || objName.StartsWith("Toilet"))
                        return GetTexMat("H2_Toilet", $"{HousesFolder}/House/Toilet Base Color.png", 0.75f);
                    break;

                case 3: // HOUSE 3: CONVENIENCE SHOP
                    if (objName.StartsWith("Shop Building"))
                    {
                        if (slotName.StartsWith("Wall") || slotIndex == 1)
                            return GetTexMat("H3_CementWall", $"{ExternalTexFolder}/hi-res-white-cement-wall-background-texture_861973-78.jpg", 0.15f);
                        if (slotName.StartsWith("roof") || slotIndex == 2)
                            return GetTexMat("H3_ShopRoof", $"{HousesFolder}/Shop/Roof.png", 0.25f);
                        return CreateColorMaterial("H3_ShopGlass", new Color(0.68f, 0.88f, 0.96f, 0.32f), 0.9f, false, true);
                    }
                    if (objName.StartsWith("Door (transparent)") || objName.StartsWith("Fridge Door"))
                        return CreateColorMaterial("H3_ClearGlass", new Color(0.72f, 0.90f, 0.98f, 0.36f), 0.9f, false, true);
                    if (slotName.StartsWith("Fridge") || objName.Equals("Fridge"))
                        return GetTexMat("H3_Fridge", $"{HousesFolder}/Shop/Fridge Base Color.png", 0.5f);
                    if (slotName.StartsWith("Bottle") || objName.StartsWith("Bottle"))
                        return GetTexMat("H3_Bottle", $"{HousesFolder}/Shop/Bottle Base Color.png", 0.6f);
                    if (slotName.StartsWith("Chips") || objName.StartsWith("Chips"))
                        return GetTexMat("H3_Chips", $"{HousesFolder}/Shop/Chips Base Color.png", 0.35f);
                    if (slotName.StartsWith("Leek") || objName.StartsWith("Leak"))
                        return GetTexMat("H3_Leek", $"{HousesFolder}/Shop/Leek Base Color.png", 0.25f);
                    if (slotName.StartsWith("Basket") || objName.StartsWith("Basket"))
                        return CreateColorMaterial("H3_RedBasket", new Color(0.88f, 0.12f, 0.24f), 0.4f);
                    if (slotName.StartsWith("Cash Reg") || objName.StartsWith("Cash Register"))
                        return CreateColorMaterial("H3_CashReg", new Color(0.82f, 0.83f, 0.86f), 0.5f);
                    if (slotName.StartsWith("Table") || objName.StartsWith("Table") || objName.StartsWith("Shelf"))
                        return CreateColorMaterial("H3_WoodCounter", new Color(0.42f, 0.24f, 0.10f), 0.3f);
                    break;

                case 4: // HOUSE 4: MECHANIC SHOP
                    if (objName.StartsWith("Door L") || objName.StartsWith("Door R") || objName.Contains("Garage_RollUp_Door"))
                        return CreateColorMaterial("H4_GarageTrim", new Color(0.92f, 0.58f, 0.08f), 0.4f);
                    if (objName.Equals("Cube"))
                    {
                        if (slotName.StartsWith("Floor") || slotIndex == 1)
                            return CreateColorMaterial("H4_GarageFloor", new Color(0.42f, 0.40f, 0.41f), 0.25f);
                        if (slotName.StartsWith("Material.001") || slotIndex == 2)
                            return CreateColorMaterial("H4_GarageTrim", new Color(0.92f, 0.58f, 0.08f), 0.4f);
                        return CreateColorMaterial("H4_GarageWall", new Color(0.24f, 0.24f, 0.26f), 0.2f);
                    }
                    if (slotName.StartsWith("Car Lift Base") || objName.StartsWith("Cube.001"))
                        return CreateColorMaterial("H4_LiftBase", new Color(0.10f, 0.16f, 0.52f), 0.5f);
                    if (slotName.StartsWith("Car Lift arms") || objName.StartsWith("Cube.002"))
                        return CreateColorMaterial("H4_LiftArms", new Color(0.92f, 0.65f, 0.05f), 0.5f);
                    if (objName.StartsWith("Car Body"))
                    {
                        if (slotName.StartsWith("Wheel") || slotIndex == 1)
                            return CreateColorMaterial("H4_TireRubber", new Color(0.10f, 0.10f, 0.11f), 0.2f);
                        return GetTexMat("H4_CarBody", $"{HousesFolder}/Mechanics shop/Car Base Color.png", 0.65f);
                    }
                    if (slotName.StartsWith("ToolBox") || objName.Equals("Box"))
                        return CreateColorMaterial("H4_ToolboxRed", new Color(0.65f, 0.06f, 0.08f), 0.5f);
                    if (slotName.StartsWith("Toolbox Drawer") || objName.StartsWith("Drawer"))
                        return CreateColorMaterial("H4_ToolboxDrawer", new Color(0.14f, 0.14f, 0.16f), 0.45f);
                    if (slotName.StartsWith("Handle") || objName.StartsWith("Handle"))
                        return CreateColorMaterial("H4_ChromeHandle", new Color(0.85f, 0.86f, 0.88f), 0.8f);
                    if (slotName.StartsWith("Rim") || objName.StartsWith("Rims"))
                        return CreateColorMaterial("H4_SilverRim", new Color(0.68f, 0.69f, 0.72f), 0.75f);
                    if (slotName.StartsWith("Wheel") || objName.StartsWith("Tires"))
                        return CreateColorMaterial("H4_TireRubber", new Color(0.10f, 0.10f, 0.11f), 0.2f);
                    if (slotName.StartsWith("Screw Driver") || objName.StartsWith("Screw Driver"))
                        return GetTexMat("H4_ScrewDriver", $"{HousesFolder}/Mechanics shop/Screw Driver Base Color.png", 0.5f);
                    if (slotName.StartsWith("Material.002") || objName.StartsWith("Wrench"))
                        return CreateColorMaterial("H4_WrenchSteel", new Color(0.52f, 0.53f, 0.56f), 0.7f);
                    break;

                case 5: // HOUSE 5: BACKROOMS
                    // Check objName first so House, Door, and Roof always get their exact extracted textures even if multiple slots exist
                    if (objName.Equals("Door", StringComparison.OrdinalIgnoreCase) || slotName.StartsWith("Door"))
                        return GetTexMat("H5_ExtDoor", $"{HousesFolder}/BackRooms/Door Base Color.png", 0.3f);
                    if (objName.Equals("Roof", StringComparison.OrdinalIgnoreCase) || slotName.StartsWith("Roof"))
                        return CreateColorMaterial("H5_ExtRoof", new Color(0.62f, 0.16f, 0.12f), 0.2f);
                    if (objName.Equals("House", StringComparison.OrdinalIgnoreCase) || slotName.StartsWith("House"))
                        return GetTexMat("H5_ExtHouse", $"{HousesFolder}/BackRooms/House Base Color.png", 0.2f);
                    if (slotName.StartsWith("Modular Wall") || objName.StartsWith("Wall"))
                        return GetTexMat("H5_Wallpaper", $"{ExternalTexFolder}/high_quality_backroom_s_wallpaper_texture_by_planetary4820_dm6xboe-fullview.jpg", 0.15f);
                    if (slotName.StartsWith("Light"))
                        return CreateColorMaterial("H5_CeilingFluorescent", new Color(1.0f, 0.98f, 0.88f), 0.5f, true);
                    if (slotName.StartsWith("Ceiling") || objName.StartsWith("Ceiling"))
                        return GetTexMat("H5_CeilingTile", $"{ExternalTexFolder}/8659-v6.jpg", 0.15f);
                    if (slotName.StartsWith("Material.001") || objName.StartsWith("Desk"))
                        return GetTexMat("H5_WoodDesk", $"{ExternalTexFolder}/Texturelabs_Wood_189S.jpg", 0.3f);
                    if (slotName.StartsWith("Drawer") || objName.StartsWith("Drawer"))
                        return CreateColorMaterial("H5_DeskDrawer", new Color(0.32f, 0.18f, 0.10f), 0.3f);
                    if (slotName.StartsWith("Almond Water") || objName.StartsWith("Almond Water"))
                        return GetTexMat("H5_AlmondWater", $"{HousesFolder}/BackRooms/Almond Water Base Color.png", 0.5f);
                    if (slotName.StartsWith("Flashlight") || objName.StartsWith("Flash Light"))
                        return GetTexMat("H5_Flashlight", $"{HousesFolder}/BackRooms/Flashlight Base Color.png", 0.5f);
                    if (slotName.StartsWith("Material.003") || objName.StartsWith("NurbsPath") || objName.Contains("Monster"))
                        return GetTexMat("H5_MonsterTex", $"{ExternalTexFolder}/image (4).png", 0.3f);
                    if (slotName.StartsWith("Material.002") || objName.Equals("Cube"))
                        return GetTexMat("H5_CubeTex", $"{ExternalTexFolder}/65218c9bc1306b6ee4d4d631ea86680e.jpg", 0.25f);
                    break;
            }
            return null;
        }

        // =====================================================================
        // XR ORIGIN, COLLISION ENFORCER, SIMULATOR & VILLAGE ENVIRONMENT
        // =====================================================================
        private static void SetupXROriginAndEventSystem(Transform worldRoot, VillageActionManager actionManager)
        {
            // Shared village: the XR Origin, its CharacterController, controllers and the simulator belong to
            // everyone, so nothing is added to or moved on them here. The manager finds the rig at runtime.
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>() == null)
            {
                GameObject im = new GameObject("XR Interaction Manager");
                im.transform.SetParent(worldRoot);
                im.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            }

            EventSystem existingEs = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existingEs == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.transform.SetParent(worldRoot);
                existingEs = es.AddComponent<EventSystem>();
                existingEs.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        private static void BuildWristDebugUI(Transform handParent, VillageActionManager actionManager)
        {
            GameObject canvasGO = CreateWorldCanvas("WristDebugCanvas", handParent, new Vector3(-0.02f, 0.08f, -0.06f), Quaternion.Euler(50f, 0f, 0f), new Vector2(360, 310), 0.0006f);
            WristUIController wristCtrl = canvasGO.AddComponent<WristUIController>();

            CreateText(canvasGO.transform, "Header", "<b>VR VILLAGE WRIST MENU</b>", 16, new Vector2(0, 120), new Vector2(340, 30), TextAnchor.MiddleCenter);

            Text statusText = CreateText(canvasGO.transform, "Status", "<b>PROGRESS:</b> 0/50 ACTIONS", 13, new Vector2(0, 95), new Vector2(340, 22), TextAnchor.MiddleCenter);
            wristCtrl.statusText = statusText;

            Text godText = CreateButton(canvasGO.transform, "Btn_GodMode", "GOD MODE (INVISIBLE): OFF", new Vector2(0, 55), new Vector2(310, 36), new Color(0.25f, 0.45f, 0.8f),
                "GodMode");
            actionManager.godModeButtonText = godText;
            actionManager.godModeButtonTexts.Add(godText);
            wristCtrl.godModeText = godText;

            Text pauseText = CreateButton(canvasGO.transform, "Btn_PauseMonster", "PAUSE ENTITY AI: ACTIVE", new Vector2(0, 12), new Vector2(310, 36), new Color(0.75f, 0.35f, 0.2f),
                "PauseMonster");
            actionManager.monsterPauseButtonTexts.Add(pauseText);
            wristCtrl.pauseEntityText = pauseText;

            CreateButton(canvasGO.transform, "Btn_Spawn", "TELEPORT TO VILLAGE SPAWN", new Vector2(0, -32), new Vector2(310, 36), new Color(0.2f, 0.65f, 0.35f),
                "TeleportSpawn");

            CreateButton(canvasGO.transform, "Btn_Backrooms", "TELEPORT TO BACKROOMS", new Vector2(0, -75), new Vector2(310, 36), new Color(0.65f, 0.55f, 0.15f),
                "TeleportBackrooms");
        }

        private static void BuildVillageEnvironment(Transform worldRoot)
        {
            GameObject envRoot = new GameObject("Village_Environment");
            envRoot.transform.SetParent(worldRoot);

            Material roadMat = CreateColorMaterial("VillageRoad", new Color(0.35f, 0.33f, 0.30f), 0.15f);
            Material woodMat = CreateColorMaterial("VillageWood", new Color(0.45f, 0.28f, 0.14f), 0.2f);
            Material leafMat = CreateColorMaterial("VillageLeaves", new Color(0.18f, 0.55f, 0.20f), 0.15f);

            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaza.name = "Village_Central_Plaza";
            plaza.transform.SetParent(envRoot.transform);
            plaza.transform.position = new Vector3(0f, 0.01f, 8f);
            plaza.transform.localScale = new Vector3(84f, 0.04f, 60f);
            plaza.GetComponent<Renderer>().sharedMaterial = roadMat;

            Vector3[] treePositions =
            {
                new Vector3(-40f, 0f, -18f), new Vector3(-40f, 0f, 4f), new Vector3(-40f, 0f, 26f),
                new Vector3(40f, 0f, -18f),  new Vector3(40f, 0f, 4f),  new Vector3(40f, 0f, 26f),
                new Vector3(-18f, 0f, 38f),  new Vector3(0f, 0f, 38f),   new Vector3(18f, 0f, 38f),
                new Vector3(-18f, 0f, -22f), new Vector3(18f, 0f, -22f)
            };

            for (int i = 0; i < treePositions.Length; i++)
            {
                GameObject tree = new GameObject($"Village_Tree_{i + 1}");
                tree.transform.SetParent(envRoot.transform);
                tree.transform.position = treePositions[i];

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(tree.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                trunk.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
                trunk.GetComponent<Renderer>().sharedMaterial = woodMat;

                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = new Vector3(0f, 3.1f, 0f);
                crown.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
                crown.GetComponent<Renderer>().sharedMaterial = leafMat;
            }

        }

        // =====================================================================
        // HOUSE 1: STORAGE FACILITY (5 Storage Units lined up side-by-side along X!)
        // =====================================================================
        private static void BuildHouse1_StorageFacility(Transform worldRoot, VillageActionManager actionManager)
        {
            GameObject houseRoot = new GameObject("House1_StorageFacility");
            houseRoot.transform.SetParent(worldRoot);
            // Position on the West side of the village; rotate -90 Y so local -Z doorways face East (+X) toward the plaza!
            // Located at z = -12.14m to keep it comfortably separated from House 2 (z = 18m)
            houseRoot.transform.position = new Vector3(-26f, 0f, -12.14f);
            houseRoot.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            AddInteriorLight(houseRoot.transform, new Vector3(4.5f, 2.8f, 0.5f), new Color(1f, 0.95f, 0.85f), 2.5f, 18f);

            GameObject blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Storage Unit/Storage Unit.blend");
            if (blendAsset == null)
                blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narin/Blender/Storage Unit.blend");
            if (blendAsset == null) return;

            ScriptedMover primaryDoorMover = null;

            int unitCount = 5;
            float unitWidthX = 3.10f;

            for (int u = 0; u < unitCount; u++)
            {
                GameObject unitInst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
                PrefabUtility.UnpackPrefabInstance(unitInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                unitInst.name = $"Storage_Unit_10{u + 1}";
                unitInst.transform.SetParent(houseRoot.transform, false);
                unitInst.transform.localPosition = new Vector3(u * unitWidthX, 0f, 0f);
                unitInst.transform.localRotation = Quaternion.identity;
                unitInst.transform.localScale = Vector3.one * 1.45f;

                CleanBlenderHelpers(unitInst);
                ApplyExactHouseMaterials(unitInst, 1);

                // In unitInst local space:
                // Storage Unit shell spans X = [-0.10 .. +1.90] (center X = +0.90), Y = [0.00 .. +2.20], Z = [-1.00 .. +3.00].
                // The open front doorway is at X = +0.90, Y = 1.00, Z = -0.95!
                Transform doorT = FindPart(unitInst.transform, "Door");
                if (doorT != null)
                {
                    doorT.localPosition = new Vector3(0.90f, 1.00f, -0.95f);
                }

                // Place all props in unitInst local space well inside the walls (X in [0.45 .. 1.35], Z in [-0.10 .. +2.10])!
                PlacePropsInsideStorageUnitLocal(unitInst.transform);

                AddStaticCollidersToBuilding(unitInst, new[] { "Storage Unit", "Table", "Puzzle" });

                // Action 1 [SCRIPT]: Roll-up Door slides straight DOWN along parent Y (-2.85m to clear enlarged opening!)
                if (doorT != null)
                {
                    ScriptedMover doorMover = doorT.gameObject.AddComponent<ScriptedMover>();
                    doorMover.houseNumber = 1;
                    doorMover.actionNumber = 1;
                    doorMover.secondaryUnlockActionNumber = 0;
                    doorMover.openPositionOffset = new Vector3(0f, 2.1f, 0f);
                    if (u == 0) primaryDoorMover = doorMover;
                }

                ConfigureStorageUnitItems(unitInst, u);
            }

            // House 1 UI Canvas placed right beside Unit 101 facing the player (local -Z)
            GameObject uiCanvas = CreateWorldCanvas("House1_Storage_UI", houseRoot.transform,
                new Vector3(-2.2f, 1.6f, -1.5f), Quaternion.identity, new Vector2(520, 560), 0.003f);

            HouseUIController uiCtrl = uiCanvas.AddComponent<HouseUIController>();
            uiCtrl.storageMainDoor = primaryDoorMover;
            uiCtrl.storageStatusLabel = CreateText(uiCanvas.transform, "Status", "UNIT STATUS: <color=#44FF44>READY</color>", 20, new Vector2(0, 210), new Vector2(480, 40), TextAnchor.MiddleCenter);

            CreateButton(uiCanvas.transform, "Btn_LockDoor", "9. [UI] LOCK STORAGE UNIT DOOR", new Vector2(0, 145), new Vector2(440, 50), new Color(0.75f, 0.2f, 0.2f),
                "StorageLock", uiCtrl);

            CreateButton(uiCanvas.transform, "Btn_UnlockDoor", "10. [UI] UNLOCK STORAGE UNIT DOOR", new Vector2(0, 80), new Vector2(440, 50), new Color(0.2f, 0.65f, 0.3f),
                "StorageUnlock", uiCtrl);

            Text boardText = CreateText(uiCanvas.transform, "Checklist", "", 15, new Vector2(0, -100), new Vector2(480, 280), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 0, boardText);
        }

        /// <summary>
        /// Places all Storage Unit props using exact local coordinates inside the bay (X in [0.45..1.35], Z in [-0.15..+2.10])
        /// while preserving their original Blender heights so nothing clips into the walls or floor!
        /// </summary>
        private static void PlacePropsInsideStorageUnitLocal(Transform unitRoot)
        {
            SetPartLocalPos(unitRoot, "Table",          new Vector3(0.50f, 0.40f, 0.90f));
            SetPartLocalPos(unitRoot, "Trash Bin",      new Vector3(1.30f, 0.46f, 0.85f));
            SetPartLocalPos(unitRoot, "Box",            new Vector3(1.25f, 0.16f, 0.15f));
            SetPartLocalPos(unitRoot, "Tetris L",       new Vector3(0.50f, 0.60f, 0.90f));
            SetPartLocalPos(unitRoot, "Tetris T",       new Vector3(0.65f, 0.22f, 0.10f));
            SetPartLocalPos(unitRoot, "Tetris Line",    new Vector3(0.95f, 0.22f, -0.25f));
            SetPartLocalPos(unitRoot, "Puzzle",         new Vector3(0.90f, 0.09f, 2.05f));
            SetPartLocalPos(unitRoot, "Cube Puzzle",    new Vector3(0.55f, 0.39f, 1.55f));
            SetPartLocalPos(unitRoot, "Pyramid Puzzle", new Vector3(0.90f, 0.39f, 1.55f));
            SetPartLocalPos(unitRoot, "Sphere Puzzle",  new Vector3(1.25f, 0.39f, 1.55f));
        }

        private static void SetPartLocalPos(Transform root, string partName, Vector3 localPos)
        {
            Transform t = FindPart(root, partName);
            if (t != null) t.localPosition = localPos;
        }

        private static void ConfigureStorageUnitItems(GameObject unitInst, int unitIndex)
        {
            SetupGrabRule(unitInst.transform, "Box", 1, 2, "Trash");
            SetupGrabRule(unitInst.transform, "Trash Bin", 1, 3, "Table");
            Transform trashT = FindPart(unitInst.transform, "Trash Bin");
            if (trashT != null)
            {
                GrabAndPlaceItem trashGrab = trashT.GetComponent<GrabAndPlaceItem>();
                if (trashGrab == null) trashGrab = trashT.gameObject.AddComponent<GrabAndPlaceItem>();
                trashGrab.isHollowContainer = true;
                GrabAndPlaceItem.BuildHollowOpenTopColliders(trashT.gameObject);

                Rigidbody rb = trashT.GetComponent<Rigidbody>();
                if (rb == null) rb = trashT.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = 2.0f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                XRGrabInteractable xri = trashT.GetComponent<XRGrabInteractable>();
                if (xri == null) xri = trashT.gameObject.AddComponent<XRGrabInteractable>();
                xri.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                xri.throwOnDetach = true;
                xri.interactionLayers = 1;
                xri.retainTransformParent = false;
                GrabAndPlaceItem.SyncGrabColliders(trashT.gameObject);
            }
            SetupGrabRule(unitInst.transform, "Tetris L", 1, 4, "Table");
            SetupGrabRule(unitInst.transform, "Tetris T", 1, 4, "Tetris");
            AddAdditionalRule(unitInst.transform, "Tetris T", 1, 4, "Table");

            SetupGrabRule(unitInst.transform, "Tetris Line", 1, 5, "Tetris");
            AddAdditionalRule(unitInst.transform, "Tetris Line", 1, 5, "Table");

            SetupGrabRule(unitInst.transform, "Cube Puzzle", 1, 6, "Tetris");
            AddAdditionalRule(unitInst.transform, "Cube Puzzle", 1, 6, "Table");

            SetupGrabRule(unitInst.transform, "Pyramid Puzzle", 1, 7, "Puzzle");
            AddAdditionalRule(unitInst.transform, "Pyramid Puzzle", 1, 7, "Table");

            SetupGrabRule(unitInst.transform, "Sphere Puzzle", 1, 8, "Puzzle");
            AddAdditionalRule(unitInst.transform, "Sphere Puzzle", 1, 8, "Table");

            if (unitIndex == 1)
                SetOnlyActiveProps(unitInst.transform, new[] { "Storage Unit", "Door", "Table", "Box", "Trash Bin" });
            else if (unitIndex == 2)
                SetOnlyActiveProps(unitInst.transform, new[] { "Storage Unit", "Door", "Table", "Tetris L", "Tetris T", "Tetris Line" });
            else if (unitIndex == 3)
                SetOnlyActiveProps(unitInst.transform, new[] { "Storage Unit", "Door", "Table", "Puzzle", "Cube Puzzle", "Pyramid Puzzle" });
            else if (unitIndex == 4)
                SetOnlyActiveProps(unitInst.transform, new[] { "Storage Unit", "Door", "Table", "Puzzle", "Sphere Puzzle" });
        }

        // =====================================================================
        // HOUSE 2: RESIDENTIAL HOUSE ("Morning Kitchen & Boombox")
        // =====================================================================
        private static void BuildHouse2_ResidentialHouse(Transform worldRoot, VillageActionManager actionManager)
        {
            GameObject houseRoot = new GameObject("House2_ResidentialHouse");
            houseRoot.transform.SetParent(worldRoot);
            houseRoot.transform.position = new Vector3(-18.12f, 0f, 18f);

            AddInteriorLight(houseRoot.transform, new Vector3(0f, 3.2f, 0f), new Color(1f, 0.92f, 0.78f), 3.0f, 16f);

            GameObject blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/House/House.blend");
            if (blendAsset == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Residential_House_Model";
            inst.transform.SetParent(houseRoot.transform, false);
            // Scale House 2 at 1.6x for realistic human VR proportions (door height ~2.1m)
            inst.transform.localScale = Vector3.one * 1.6f;

            CleanBlenderHelpers(inst);
            ApplyExactHouseMaterials(inst, 2);

            // Support BOTH separate Door & Doorbell objects (in updated House.blend) OR submesh fallback
            Transform houseMeshT = FindExactChild(inst.transform, "House");
            Transform extractedDoor = FindExactChild(inst.transform, "Door");
            Transform extractedDoorbell = FindExactChild(inst.transform, "Doorbell");

            if (houseMeshT != null && (extractedDoor == null || extractedDoorbell == null))
            {
                SplitHouseDoorAndDoorbellSubmeshes(houseMeshT, out Transform subDoor, out Transform subBell);
                if (extractedDoor == null) extractedDoor = subDoor;
                if (extractedDoorbell == null) extractedDoorbell = subBell;
            }
            else
            {
                if (extractedDoor != null)
                    SetMeshPivotToBoundsEdge(extractedDoor, true);
                if (extractedDoorbell != null)
                    SetMeshPivotToBoundsCenter(extractedDoorbell);
            }

            // Action 7: Extract the built-in Stove Knob from the Stove mesh BEFORE adding static MeshCollider to Stove,
            // and create a Fire Effect on the Middle Stove Burner (-3.995, 0.915, -3.645)!
            Transform stoveT = FindPart(inst.transform, "Stove");
            Transform builtInStoveKnob = null;
            if (stoveT != null)
            {
                builtInStoveKnob = ExtractStoveKnobFromStove(stoveT);
            }

            AddStaticCollidersToBuilding(inst, new[] { "House", "Stove", "Table", "Toilet", "Boombox" });

            // Action 2 [SCRIPT]: Front Door swings open around vertical Y axis at its edge
            if (extractedDoor != null)
            {
                ScriptedMover doorMover = extractedDoor.gameObject.AddComponent<ScriptedMover>();
                doorMover.houseNumber = 2;
                doorMover.actionNumber = 2;
                doorMover.openYawAngle = 85f;
            }

            // Action 1: Doorbell button trigger (plays doorbell chime only; Front Door opens when clicked for Action 2)
            if (extractedDoorbell != null)
            {
                InteractiveTriggerButton bellBtn = extractedDoorbell.gameObject.AddComponent<InteractiveTriggerButton>();
                bellBtn.houseNumber = 2;
                bellBtn.actionNumber = 1;
                bellBtn.soundEffectType = "Chime";
            }

            // Action 3 [SCRIPT]: Extract ONLY the Front Window pane from the 4-wall Window mesh
            // so the Window collider never spans across the doorway or doorbell!
            Transform winT = FindExactChild(inst.transform, "Window");
            if (winT != null)
            {
                Transform frontWinPane = ExtractFrontWindowPane(winT);
                Transform targetWin = frontWinPane != null ? frontWinPane : winT;
                ScriptedMover winMover = targetWin.gameObject.AddComponent<ScriptedMover>();
                winMover.houseNumber = 2;
                winMover.actionNumber = 3;
                winMover.openPositionOffset = new Vector3(0.75f, 0f, 0f);
            }

            // Action 6 & 9 [GRAB]: Pan -> Stove (Action 6) and Pan -> Table (Action 9)
            Transform panT = FindPart(inst.transform, "Pan");
            if (panT != null)
            {
                GrabAndPlaceItem panGrab = panT.gameObject.AddComponent<GrabAndPlaceItem>();
                panGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Stove",
                    houseNumber = 2,
                    actionNumber = 6
                });
                panGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Table",
                    houseNumber = 2,
                    actionNumber = 9
                });
            }

            // Action 7: Wire built-in Stove Knob to toggle Fire Effect on the Middle Stove Burner!
            if (stoveT != null)
            {
                GameObject fireEffectGO = CreateMiddleBurnerFireEffect(inst.transform, out Light bLight);

                if (builtInStoveKnob == null)
                {
                    GameObject knobTriggerGO = new GameObject("Stove_BuiltIn_Knob");
                    knobTriggerGO.transform.SetParent(inst.transform, false);
                    knobTriggerGO.transform.localPosition = new Vector3(-3.40f, 0.77f, -3.65f);
                    BoxCollider bc = knobTriggerGO.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.35f, 0.35f, 1.75f);
                    builtInStoveKnob = knobTriggerGO.transform;
                }
                else
                {
                    BoxCollider bc = builtInStoveKnob.GetComponent<BoxCollider>();
                    if (bc == null) bc = builtInStoveKnob.gameObject.AddComponent<BoxCollider>();
                    MeshFilter kmf = builtInStoveKnob.GetComponent<MeshFilter>();
                    if (kmf != null && kmf.sharedMesh != null)
                    {
                        bc.center = kmf.sharedMesh.bounds.center + new Vector3(0.08f, 0f, 0f);
                        bc.size = Vector3.Max(kmf.sharedMesh.bounds.size + new Vector3(0.25f, 0.20f, 0.20f), new Vector3(0.30f, 0.30f, 0.50f));
                    }
                }

                InteractiveTriggerButton knobBtn = builtInStoveKnob.gameObject.AddComponent<InteractiveTriggerButton>();
                knobBtn.houseNumber = 2;
                knobBtn.actionNumber = 7;
                knobBtn.soundEffectType = "Burner";
                knobBtn.optionalToggleObject = fireEffectGO;
                knobBtn.optionalBurnerLight = bLight;
                knobBtn.rotateSelfOnBurner = false;
            }

            // Action 8 [GRAB]: Raw bacon -> Pan (with sizzling sound!)
            Transform baconT = FindPart(inst.transform, "bacon");
            if (baconT != null)
            {
                GrabAndPlaceItem baconGrab = baconT.gameObject.AddComponent<GrabAndPlaceItem>();
                baconGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Pan",
                    houseNumber = 2,
                    actionNumber = 8,
                    attachToTargetOnPlace = true,
                    specialSoundType = "Sizzle"
                });
                baconGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Stove",
                    houseNumber = 2,
                    actionNumber = 8,
                    specialSoundType = "Sizzle"
                });
            }

            // Action 10: Toilet flush handle in bathroom
            Transform toiletT = FindPart(inst.transform, "Toilet");
            if (toiletT != null)
            {
                InteractiveTriggerButton flushBtn = toiletT.gameObject.AddComponent<InteractiveTriggerButton>();
                flushBtn.houseNumber = 2;
                flushBtn.actionNumber = 10;
                flushBtn.soundEffectType = "Flush";
            }

            // Actions 4 & 5 [UI]: Boombox Canvas UI placed INSIDE House 2 right next to the Boombox on the Table!
            // In House.blend, Boombox is on the Table at localPosition (4.00f, 0.68f, -2.00f)
            Transform boomboxT = FindPart(inst.transform, "Boombox");
            AudioSource boomboxAudio = null;
            if (boomboxT != null)
            {
                boomboxAudio = boomboxT.GetComponent<AudioSource>();
                if (boomboxAudio == null) boomboxAudio = boomboxT.gameObject.AddComponent<AudioSource>();
                boomboxAudio.spatialBlend = 0.35f;
                boomboxAudio.loop = true;
                boomboxAudio.playOnAwake = false;
            }

            Vector3 boomboxUiLocalPos = boomboxT != null
                ? boomboxT.localPosition + new Vector3(-0.35f, 0.68f, 0.45f)
                : new Vector3(3.65f, 1.36f, -1.55f);

            GameObject uiCanvas = CreateWorldCanvas("House2_Boombox_UI", houseRoot.transform,
                boomboxUiLocalPos, Quaternion.Euler(0f, 35f, 0f), new Vector2(460, 290), 0.0022f);

            HouseUIController uiCtrl = uiCanvas.AddComponent<HouseUIController>();
            uiCtrl.boomboxAudioSource = boomboxAudio;
            uiCtrl.boomboxStatusLabel = CreateText(uiCanvas.transform, "RadioStatus", "BOOMBOX: READY", 20, new Vector2(0, 95), new Vector2(420, 40), TextAnchor.MiddleCenter);

            CreateButton(uiCanvas.transform, "Btn_BoomboxPower", "4. [UI] POWER ON / OFF", new Vector2(-105, 25), new Vector2(205, 52), new Color(0.2f, 0.55f, 0.8f),
                "BoomboxPower", uiCtrl);

            CreateButton(uiCanvas.transform, "Btn_BoomboxOff", "STOP / OFF", new Vector2(110, 25), new Vector2(200, 52), new Color(0.75f, 0.25f, 0.25f),
                "BoomboxOff", uiCtrl);

            CreateButton(uiCanvas.transform, "Btn_BoomboxNext", "5. [UI] BOOMBOX NEXT TRACK", new Vector2(0, -45), new Vector2(420, 52), new Color(0.55f, 0.3f, 0.75f),
                "BoomboxNext", uiCtrl);

            if (boomboxT != null)
            {
                if (boomboxT.GetComponent<Collider>() == null)
                {
                    MeshFilter bmf = boomboxT.GetComponent<MeshFilter>();
                    BoxCollider bbc = boomboxT.gameObject.AddComponent<BoxCollider>();
                    if (bmf != null && bmf.sharedMesh != null)
                    {
                        bbc.center = bmf.sharedMesh.bounds.center;
                        bbc.size = bmf.sharedMesh.bounds.size;
                    }
                }
                if (boomboxT.GetComponent<XRSimpleInteractable>() == null)
                    boomboxT.gameObject.AddComponent<XRSimpleInteractable>();
                VillageUIButtonBinding directBoomboxBtn = boomboxT.gameObject.AddComponent<VillageUIButtonBinding>();
                directBoomboxBtn.commandType = "BoomboxPower";
                directBoomboxBtn.uiController = uiCtrl;
            }

            // House 2 Exterior Checklist Board outside to the right of the front door (with quick-access Boombox buttons too)
            GameObject extBoardH2 = CreateWorldCanvas("House2_Exterior_Checklist_UI", houseRoot.transform,
                new Vector3(3.2f, 1.65f, -6.2f), Quaternion.identity, new Vector2(520, 420), 0.003f);
            Text boardText = CreateText(extBoardH2.transform, "Checklist", "", 15, new Vector2(0, 0), new Vector2(480, 380), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 1, boardText);
        }

        /// <summary>
        /// Extracts the protruding front Stove Knobs from House.blend's Stove mesh so the actual built-in stove knobs
        /// are interactive without adding any extra cylinder geometry!
        /// </summary>
        private static Transform ExtractStoveKnobFromStove(Transform stoveT)
        {
            MeshFilter mf = stoveT.GetComponent<MeshFilter>();
            MeshRenderer mr = stoveT.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return null;

            Mesh srcMesh = mf.sharedMesh;
            Vector3[] verts = srcMesh.vertices;
            int[] tris = srcMesh.GetTriangles(0);
            Transform modelRoot = stoveT.parent;

            List<int> knobTris = new List<int>();
            List<int> bodyTris = new List<int>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = modelRoot.InverseTransformPoint(stoveT.TransformPoint(verts[tris[i]]));
                Vector3 v1 = modelRoot.InverseTransformPoint(stoveT.TransformPoint(verts[tris[i + 1]]));
                Vector3 v2 = modelRoot.InverseTransformPoint(stoveT.TransformPoint(verts[tris[i + 2]]));
                Vector3 center = (v0 + v1 + v2) / 3f;

                // Stove front plate is at X = -3.496 in modelRoot local space; protruding knobs are at X > -3.492, Y > 0.62
                if (center.x > -3.492f && center.y > 0.62f)
                {
                    knobTris.Add(tris[i]);
                    knobTris.Add(tris[i + 1]);
                    knobTris.Add(tris[i + 2]);
                }
                else
                {
                    bodyTris.Add(tris[i]);
                    bodyTris.Add(tris[i + 1]);
                    bodyTris.Add(tris[i + 2]);
                }
            }

            if (knobTris.Count == 0) return null;

            Transform knobChild = BuildChildFromTriangleSubset(stoveT, srcMesh, knobTris.ToArray(), "Stove_BuiltIn_Knob", mr.sharedMaterial, false);

            if (bodyTris.Count > 0)
            {
                Mesh remMesh = UnityEngine.Object.Instantiate(srcMesh);
                remMesh.name = "Stove_Body_Mesh";
                remMesh.subMeshCount = 1;
                remMesh.SetTriangles(bodyTris.ToArray(), 0);
                remMesh.RecalculateBounds();
                mf.sharedMesh = remMesh;
            }

            return knobChild;
        }

        /// <summary>
        /// Creates a multi-flame gas burner fire effect + ParticleSystem + PointLight on the Middle Stove Burner
        /// at (-3.995, 0.915, -3.645) in Residential_House_Model local space, toggled on/off by the Stove Knob!
        /// </summary>
        private static GameObject CreateMiddleBurnerFireEffect(Transform houseModelRoot, out Light burnerLight)
        {
            GameObject fireRoot = new GameObject("Middle_Stove_Fire_Effect");
            fireRoot.transform.SetParent(houseModelRoot, false);
            // Exact center of the Middle Burner on top of Stove in House.blend:
            fireRoot.transform.localPosition = new Vector3(-3.995f, 0.915f, -3.645f);
            fireRoot.transform.localRotation = Quaternion.identity;

            Material outerFlameMat = CreateColorMaterial("H2_FireOuterOrange", new Color(1.0f, 0.35f, 0.05f), 0.7f, true);
            Material innerFlameMat = CreateColorMaterial("H2_FireInnerYellow", new Color(1.0f, 0.82f, 0.15f), 0.8f, true);
            Material blueCoreMat = CreateColorMaterial("H2_FireBlueCore", new Color(0.20f, 0.55f, 1.0f), 0.8f, true);

            // Central blue/orange gas burner core
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Burner_Core_Flame";
            core.transform.SetParent(fireRoot.transform, false);
            core.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            core.transform.localScale = new Vector3(0.14f, 0.035f, 0.14f);
            UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());
            core.GetComponent<Renderer>().sharedMaterial = blueCoreMat;

            // Ring of 8 flame tongues around the middle burner ring
            int flameCount = 8;
            float ringRadius = 0.115f;
            for (int i = 0; i < flameCount; i++)
            {
                float angleRad = i * Mathf.PI * 2f / flameCount;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * ringRadius, 0.045f, Mathf.Sin(angleRad) * ringRadius);

                GameObject tongue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tongue.name = $"Flame_Tongue_{i + 1}";
                tongue.transform.SetParent(fireRoot.transform, false);
                tongue.transform.localPosition = pos;
                tongue.transform.localRotation = Quaternion.Euler(0f, -angleRad * Mathf.Rad2Deg, -18f);
                tongue.transform.localScale = new Vector3(0.045f, 0.095f, 0.045f);
                UnityEngine.Object.DestroyImmediate(tongue.GetComponent<Collider>());
                tongue.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? outerFlameMat : innerFlameMat;
            }

            // Rising Fire ParticleSystem for dynamic flickering flames
            GameObject psGO = new GameObject("Middle_Burner_Fire_Particles");
            psGO.transform.SetParent(fireRoot.transform, false);
            psGO.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            psGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = psGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.78f, 0.15f, 0.9f), new Color(1.0f, 0.25f, 0.02f, 0.9f));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            ParticleSystemRenderer psr = psGO.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = outerFlameMat;
            }

            // Warm orange PointLight above the middle burner
            GameObject lightGO = new GameObject("Stove_Burner_Glow");
            lightGO.transform.SetParent(fireRoot.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            burnerLight = lightGO.AddComponent<Light>();
            burnerLight.type = LightType.Point;
            burnerLight.color = new Color(1.0f, 0.42f, 0.08f);
            burnerLight.intensity = 2.6f;
            burnerLight.range = 2.8f;
            burnerLight.enabled = false;

            // Start with fire effect OFF until the player clicks the stove knob!
            fireRoot.SetActive(false);
            return fireRoot;
        }

        /// <summary>
        /// Splits out ONLY the front-wall window triangles from House.blend's 4-wall Window mesh
        /// so the sliding window has a tight collider on the front window pane only, never blocking the doorbell or doorway!
        /// </summary>
        private static Transform ExtractFrontWindowPane(Transform windowT)
        {
            MeshFilter mf = windowT.GetComponent<MeshFilter>();
            MeshRenderer mr = windowT.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return null;

            Mesh srcMesh = mf.sharedMesh;
            Vector3[] verts = srcMesh.vertices;
            int[] tris = srcMesh.GetTriangles(0);
            Transform modelRoot = windowT.parent;

            List<int> frontTris = new List<int>();
            List<int> otherTris = new List<int>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = modelRoot.InverseTransformPoint(windowT.TransformPoint(verts[tris[i]]));
                Vector3 v1 = modelRoot.InverseTransformPoint(windowT.TransformPoint(verts[tris[i + 1]]));
                Vector3 v2 = modelRoot.InverseTransformPoint(windowT.TransformPoint(verts[tris[i + 2]]));
                Vector3 center = (v0 + v1 + v2) / 3f;

                // Front wall of House 2 is at local Z approx -5.14, left of the front door (X < -1.0f)
                if (center.z < -4.5f && center.x < -1.0f)
                {
                    frontTris.Add(tris[i]);
                    frontTris.Add(tris[i + 1]);
                    frontTris.Add(tris[i + 2]);
                }
                else
                {
                    otherTris.Add(tris[i]);
                    otherTris.Add(tris[i + 1]);
                    otherTris.Add(tris[i + 2]);
                }
            }

            if (frontTris.Count == 0) return null;

            Transform frontPane = BuildChildFromTriangleSubset(windowT, srcMesh, frontTris.ToArray(), "Front_Sliding_Window", mr.sharedMaterial, false);

            if (otherTris.Count > 0)
            {
                Mesh remMesh = UnityEngine.Object.Instantiate(srcMesh);
                remMesh.name = "Window_Static_Panes";
                remMesh.subMeshCount = 1;
                remMesh.SetTriangles(otherTris.ToArray(), 0);
                remMesh.RecalculateBounds();
                mf.sharedMesh = remMesh;
            }

            return frontPane;
        }

        private static void SplitHouseDoorAndDoorbellSubmeshes(Transform houseT, out Transform doorOut, out Transform bellOut)
        {
            doorOut = null;
            bellOut = null;

            MeshFilter mf = houseT.GetComponent<MeshFilter>();
            MeshRenderer mr = houseT.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 5)
                return;

            Mesh srcMesh = mf.sharedMesh;
            Material[] mats = mr.sharedMaterials;

            doorOut = BuildChildFromTriangleSubset(houseT, srcMesh, srcMesh.GetTriangles(3), "Front_Door", mats.Length > 3 ? mats[3] : mats[0], true);
            bellOut = BuildChildFromTriangleSubset(houseT, srcMesh, srcMesh.GetTriangles(4), "Front_Doorbell", mats.Length > 4 ? mats[4] : mats[0], false);

            Mesh bodyMesh = new Mesh();
            bodyMesh.name = "House_Body_NoDoor";
            bodyMesh.vertices = srcMesh.vertices;
            bodyMesh.normals = srcMesh.normals;
            bodyMesh.uv = srcMesh.uv;
            bodyMesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++)
            {
                bodyMesh.SetTriangles(srcMesh.GetTriangles(s), s);
            }
            bodyMesh.RecalculateBounds();
            mf.sharedMesh = bodyMesh;
            MeshCollider houseMC = houseT.GetComponent<MeshCollider>();
            if (houseMC != null) houseMC.sharedMesh = bodyMesh;

            Material[] bodyMats = new Material[3];
            for (int s = 0; s < 3; s++) bodyMats[s] = mats[s];
            mr.sharedMaterials = bodyMats;
        }

        private static Transform BuildChildFromTriangleSubset(Transform sourceMeshT, Mesh srcMesh, int[] tris, string childName, Material mat, bool hingePivotMinX)
        {
            if (tris == null || tris.Length == 0) return null;

            Transform parentT = sourceMeshT.parent != null ? sourceMeshT.parent : sourceMeshT;
            Vector3[] srcVerts = srcMesh.vertices;
            Vector3[] srcNorms = srcMesh.normals;
            Vector2[] srcUVs = srcMesh.uv;

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> parentVerts = new List<Vector3>();
            List<Vector3> parentNorms = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();
            List<int> newTris = new List<int>();

            Vector3 minP = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxP = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (int oldIdx in tris)
            {
                if (!remap.TryGetValue(oldIdx, out int newIdx))
                {
                    newIdx = parentVerts.Count;
                    remap[oldIdx] = newIdx;
                    Vector3 wv = sourceMeshT.TransformPoint(srcVerts[oldIdx]);
                    Vector3 pv = parentT.InverseTransformPoint(wv);
                    parentVerts.Add(pv);
                    if (srcNorms != null && oldIdx < srcNorms.Length)
                    {
                        Vector3 wn = sourceMeshT.TransformDirection(srcNorms[oldIdx]).normalized;
                        parentNorms.Add(parentT.InverseTransformDirection(wn).normalized);
                    }
                    if (srcUVs != null && oldIdx < srcUVs.Length)
                        newUVs.Add(srcUVs[oldIdx]);

                    minP = Vector3.Min(minP, pv);
                    maxP = Vector3.Max(maxP, pv);
                }
                newTris.Add(newIdx);
            }

            Vector3 pivotParentLocal = hingePivotMinX
                ? new Vector3(minP.x, (minP.y + maxP.y) * 0.5f, (minP.z + maxP.z) * 0.5f)
                : (minP + maxP) * 0.5f;

            for (int i = 0; i < parentVerts.Count; i++)
            {
                parentVerts[i] -= pivotParentLocal;
            }

            Mesh subMesh = new Mesh();
            subMesh.name = childName + "_Mesh";
            subMesh.SetVertices(parentVerts);
            if (parentNorms.Count == parentVerts.Count) subMesh.SetNormals(parentNorms);
            if (newUVs.Count == parentVerts.Count) subMesh.SetUVs(0, newUVs);
            subMesh.SetTriangles(newTris, 0);
            subMesh.RecalculateBounds();

            GameObject childGO = new GameObject(childName);
            childGO.transform.SetParent(parentT, false);
            childGO.transform.localPosition = pivotParentLocal;
            childGO.transform.localRotation = Quaternion.identity;
            childGO.transform.localScale = Vector3.one;

            MeshFilter cmf = childGO.AddComponent<MeshFilter>();
            cmf.sharedMesh = subMesh;
            MeshRenderer cmr = childGO.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = mat;

            return childGO.transform;
        }

        /// <summary>
        /// Rebuilds a door mesh in its parent's local coordinate system with its transform origin at either its min
        /// or max horizontal width edge so hinge rotation swings around the edge even when the house is rotated or scaled!
        /// </summary>
        private static void SetMeshPivotToBoundsEdge(Transform targetT, bool minEdge)
        {
            MeshFilter mf = targetT.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || targetT.parent == null) return;

            Transform parentT = targetT.parent;
            Mesh src = mf.sharedMesh;
            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNorms = src.normals;
            Vector2[] srcUVs = src.uv;

            Vector3 minP = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxP = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3[] parentVerts = new Vector3[srcVerts.Length];
            Vector3[] parentNorms = srcNorms != null && srcNorms.Length == srcVerts.Length ? new Vector3[srcNorms.Length] : null;

            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 wv = targetT.TransformPoint(srcVerts[i]);
                Vector3 pv = parentT.InverseTransformPoint(wv);
                parentVerts[i] = pv;
                if (parentNorms != null)
                {
                    Vector3 wn = targetT.TransformDirection(srcNorms[i]).normalized;
                    parentNorms[i] = parentT.InverseTransformDirection(wn).normalized;
                }
                minP = Vector3.Min(minP, pv);
                maxP = Vector3.Max(maxP, pv);
            }

            bool widerAlongX = (maxP.x - minP.x) >= (maxP.z - minP.z);
            Vector3 hingeParentLocal = widerAlongX
                ? new Vector3(minEdge ? minP.x : maxP.x, (minP.y + maxP.y) * 0.5f, (minP.z + maxP.z) * 0.5f)
                : new Vector3((minP.x + maxP.x) * 0.5f, (minP.y + maxP.y) * 0.5f, minEdge ? minP.z : maxP.z);

            for (int i = 0; i < parentVerts.Length; i++)
            {
                parentVerts[i] -= hingeParentLocal;
            }

            targetT.localPosition = hingeParentLocal;
            targetT.localRotation = Quaternion.identity;
            targetT.localScale = Vector3.one;

            Mesh newMesh = UnityEngine.Object.Instantiate(src);
            newMesh.name = targetT.name + "_EdgeHinged";
            newMesh.vertices = parentVerts;
            if (parentNorms != null) newMesh.normals = parentNorms;
            if (srcUVs != null && srcUVs.Length == parentVerts.Length) newMesh.uv = srcUVs;
            newMesh.RecalculateBounds();
            mf.sharedMesh = newMesh;
        }

        /// <summary>
        /// Rebuilds a separated mesh in its parent's local coordinate system with its transform origin at its bounding-box center
        /// and upright identity localRotation so BoxColliders and ScriptedMovers align cleanly!
        /// </summary>
        private static void SetMeshPivotToBoundsCenter(Transform targetT)
        {
            MeshFilter mf = targetT.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || targetT.parent == null) return;

            Transform parentT = targetT.parent;
            Mesh src = mf.sharedMesh;
            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNorms = src.normals;
            Vector2[] srcUVs = src.uv;

            Vector3 minP = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxP = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3[] parentVerts = new Vector3[srcVerts.Length];
            Vector3[] parentNorms = srcNorms != null && srcNorms.Length == srcVerts.Length ? new Vector3[srcNorms.Length] : null;

            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 wv = targetT.TransformPoint(srcVerts[i]);
                Vector3 pv = parentT.InverseTransformPoint(wv);
                parentVerts[i] = pv;
                if (parentNorms != null)
                {
                    Vector3 wn = targetT.TransformDirection(srcNorms[i]).normalized;
                    parentNorms[i] = parentT.InverseTransformDirection(wn).normalized;
                }
                minP = Vector3.Min(minP, pv);
                maxP = Vector3.Max(maxP, pv);
            }

            Vector3 centerParentLocal = (minP + maxP) * 0.5f;
            for (int i = 0; i < parentVerts.Length; i++)
            {
                parentVerts[i] -= centerParentLocal;
            }

            targetT.localPosition = centerParentLocal;
            targetT.localRotation = Quaternion.identity;
            targetT.localScale = Vector3.one;

            Mesh newMesh = UnityEngine.Object.Instantiate(src);
            newMesh.name = targetT.name + "_Centered";
            newMesh.vertices = parentVerts;
            if (parentNorms != null) newMesh.normals = parentNorms;
            if (srcUVs != null && srcUVs.Length == parentVerts.Length) newMesh.uv = srcUVs;
            newMesh.RecalculateBounds();
            mf.sharedMesh = newMesh;
        }

        // =====================================================================
        // HOUSE 3: SHOP ("Village Convenience Store")
        // =====================================================================
        private static void BuildHouse3_ConvenienceShop(Transform worldRoot, VillageActionManager actionManager)
        {
            GameObject houseRoot = new GameObject("House3_ConvenienceShop");
            houseRoot.transform.SetParent(worldRoot);
            // Rotated -90 Y so the front entrance and sliding glass doors face South directly toward spawn!
            houseRoot.transform.position = new Vector3(4f, 0f, 21f);
            houseRoot.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            AddInteriorLight(houseRoot.transform, new Vector3(-2f, 3.5f, 0f), Color.white, 3.2f, 18f);

            GameObject blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Shop/Shop.blend");
            if (blendAsset == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Convenience_Shop_Model";
            inst.transform.SetParent(houseRoot.transform, false);
            // Scale Shop up by 2.05x so doorway, ceiling, fridge & shelves feel spacious and realistic in VR!
            inst.transform.localScale = Vector3.one * 2.05f;

            CleanBlenderHelpers(inst);
            ApplyExactHouseMaterials(inst, 3);
            AddStaticCollidersToBuilding(inst, new[] { "Shop Building", "Table", "Shelf", "Fridge", "Cash Register" });

            // Place Bottle in Fridge and Chips & Leak on top of the upright Shelf so they are easy to grab!
            Transform bottleT = FindPart(inst.transform, "Bottle");
            if (bottleT != null) bottleT.localPosition = new Vector3(-1.59f, 0.48f, 4.42f);

            Transform chipsT = FindPart(inst.transform, "Chips");
            if (chipsT != null) chipsT.localPosition = new Vector3(-0.18f, 0.56f, -1.05f);

            Transform leakT = FindPart(inst.transform, "Leak");
            if (leakT != null) leakT.localPosition = new Vector3(0.18f, 0.58f, -1.05f);

            // Action 3 [SCRIPT]: Move Fridge Door L and Fridge Door R pivots to their outer edges (min X and max X)!
            Transform fridgeL = FindPart(inst.transform, "Fridge Door L");
            if (fridgeL != null)
            {
                SetMeshPivotToBoundsEdge(fridgeL, true); // Outer left edge
                ScriptedMover mL = fridgeL.gameObject.AddComponent<ScriptedMover>();
                mL.houseNumber = 3;
                mL.actionNumber = 3;
                mL.openYawAngle = 95f;
            }
            Transform fridgeR = FindPart(inst.transform, "Fridge Door R");
            if (fridgeR != null)
            {
                SetMeshPivotToBoundsEdge(fridgeR, false); // Outer right edge
                ScriptedMover mR = fridgeR.gameObject.AddComponent<ScriptedMover>();
                mR.houseNumber = 3;
                mR.actionNumber = 3;
                mR.openYawAngle = -95f;
            }

            // Configure Basket as a hollow open-top container so Bottle, Chips & Leak drop right inside!
            Transform basketT = FindPart(inst.transform, "Basket");
            if (basketT != null)
            {
                GrabAndPlaceItem basketGrab = basketT.gameObject.AddComponent<GrabAndPlaceItem>();
                basketGrab.isHollowContainer = true;
                GrabAndPlaceItem.BuildHollowOpenTopColliders(basketT.gameObject);
            }

            // Action 4 [GRAB]: Bottle -> Basket
            SetupGrabRule(inst.transform, "Bottle", 3, 4, "Basket", true);
            AddAdditionalRule(inst.transform, "Bottle", 3, 4, "Table");

            // Action 6 [GRAB]: Chips -> Basket
            SetupGrabRule(inst.transform, "Chips", 3, 6, "Basket", true);
            AddAdditionalRule(inst.transform, "Chips", 3, 6, "Table");

            // Action 7 [GRAB]: Leak -> Basket
            SetupGrabRule(inst.transform, "Leak", 3, 7, "Basket", true);
            AddAdditionalRule(inst.transform, "Leak", 3, 7, "Table");

            // Action 8 [GRAB]: Full Basket -> Checkout Table
            SetupGrabRule(inst.transform, "Basket", 3, 8, "Table");
            AddAdditionalRule(inst.transform, "Basket", 3, 8, "Cash Register");

            // Action 10 [SCRIPT]: Automatic Proximity Sliding Door slides side-to-side along Z (parallel to storefront)!
            Transform glassDoorT = FindPart(inst.transform, "Door (transparent)");
            if (glassDoorT != null)
            {
                ScriptedMover autoDoorMover = glassDoorT.gameObject.AddComponent<ScriptedMover>();
                autoDoorMover.houseNumber = 3;
                autoDoorMover.actionNumber = 10;
                autoDoorMover.useProximityTrigger = true;
                autoDoorMover.proximityDistance = 4.0f;
                autoDoorMover.openPositionOffset = new Vector3(0f, 0f, 1.05f);
            }

            // Action 9 [SCRIPT / UI]: Add mechanical sliding cash register drawer to Cash Register
            ScriptedMover cashDrawerMover = null;
            Transform cashRegT = FindPart(inst.transform, "Cash Register");
            if (cashRegT != null)
            {
                Transform existingDrawer = cashRegT.Find("Cash_Register_Drawer");
                GameObject drawerGO;
                if (existingDrawer != null)
                {
                    drawerGO = existingDrawer.gameObject;
                }
                else
                {
                    drawerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    drawerGO.name = "Cash_Register_Drawer";
                    drawerGO.transform.SetParent(cashRegT, false);
                    drawerGO.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    drawerGO.transform.localRotation = Quaternion.identity;
                    drawerGO.transform.localScale = new Vector3(0.85f, 0.85f, 0.35f);

                    Renderer r = drawerGO.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.sharedMaterial = CreateColorMaterial("H3_RegisterDrawer", new Color(0.22f, 0.24f, 0.28f), 0.7f);
                    }
                }

                cashDrawerMover = drawerGO.GetComponent<ScriptedMover>();
                if (cashDrawerMover == null) cashDrawerMover = drawerGO.AddComponent<ScriptedMover>();
                cashDrawerMover.houseNumber = 3;
                cashDrawerMover.actionNumber = 9;
                cashDrawerMover.openPositionOffset = new Vector3(-0.65f, 0f, 0f);
                cashDrawerMover.speed = 3.5f;
            }

            // Actions 1, 2, 5, 9 [UI]: Cash Register Interactive Touchscreen Canvas placed right at the Checkout Counter!
            GameObject uiCanvas = CreateWorldCanvas("House3_CashRegister_UI", houseRoot.transform,
                new Vector3(-2.15f, 1.35f, -7.05f), Quaternion.Euler(15f, 0f, 0f), new Vector2(560, 640), 0.003f);

            HouseUIController uiCtrl = uiCanvas.AddComponent<HouseUIController>();
            uiCtrl.cashRegisterDrawer = cashDrawerMover;
            uiCtrl.registerStatusLabel = CreateText(uiCanvas.transform, "RegStatus", "CASH REGISTER: PRESS START", 18, new Vector2(0, 265), new Vector2(520, 45), TextAnchor.MiddleCenter);

            CreateButton(uiCanvas.transform, "Btn_StartShop", "1. [UI] START SHOPPING ($0.00)", new Vector2(0, 205), new Vector2(460, 44), new Color(0.2f, 0.6f, 0.85f),
                "ShopStart", uiCtrl);

            CreateButton(uiCanvas.transform, "Btn_PriceBottle", "2. [UI] +$2.50 BOTTLE", new Vector2(-170, 145), new Vector2(165, 44), new Color(0.75f, 0.55f, 0.15f),
                "ShopBottlePrice", uiCtrl, 2.50f);

            CreateButton(uiCanvas.transform, "Btn_PriceChips", "5a. [UI] +$1.99 CHIPS", new Vector2(0, 145), new Vector2(165, 44), new Color(0.75f, 0.55f, 0.15f),
                "ShopPrice", uiCtrl, 1.99f);

            CreateButton(uiCanvas.transform, "Btn_PriceLeek", "5b. [UI] +$1.50 LEEK", new Vector2(170, 145), new Vector2(165, 44), new Color(0.75f, 0.55f, 0.15f),
                "ShopPrice", uiCtrl, 1.50f);

            CreateButton(uiCanvas.transform, "Btn_PayConfirm", "9. [UI] PAY & CONFIRM PURCHASE", new Vector2(0, 85), new Vector2(460, 46), new Color(0.2f, 0.75f, 0.3f),
                "ShopPay", uiCtrl);

            Text boardText = CreateText(uiCanvas.transform, "Checklist", "", 14, new Vector2(0, -115), new Vector2(520, 290), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 2, boardText);

            // Exterior Convenience Shop Checklist Board outside next to the automatic sliding door in front of the right-most window
            GameObject extBoardH3 = CreateWorldCanvas("House3_Exterior_Checklist_UI", houseRoot.transform,
                new Vector3(-6.4f, 1.65f, -7.5f), Quaternion.Euler(0f, 90f, 0f), new Vector2(520, 420), 0.003f);
            Text extBoardText = CreateText(extBoardH3.transform, "Checklist", "", 15, Vector2.zero, new Vector2(480, 380), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 2, extBoardText);
        }

        // =====================================================================
        // HOUSE 4: MECHANICS SHOP ("Garage Tune-Up")
        // =====================================================================
        private static void BuildHouse4_MechanicsShop(Transform worldRoot, VillageActionManager actionManager)
        {
            GameObject houseRoot = new GameObject("House4_MechanicsShop");
            houseRoot.transform.SetParent(worldRoot);
            houseRoot.transform.position = new Vector3(26f, 0f, 12f);

            AddInteriorLight(houseRoot.transform, new Vector3(0f, 3.8f, 0f), new Color(0.92f, 0.96f, 1f), 3.2f, 18f);

            GameObject blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Mechanics shop/Mechanic.blend");
            if (blendAsset == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Mechanics_Shop_Model";
            inst.transform.SetParent(houseRoot.transform, false);
            inst.transform.localPosition = new Vector3(18f, 0f, 0f);

            // Position Garage Building (Cube) and its separated Door L & Door R at local X = -13.5f so its interior spans X = [-23.75 .. -13.5],
            // giving Car Body ([-21.6 .. -15.5]) over 2.1m of clearance from the front garage doors (-23.75)!
            Transform garageShell = FindExactChild(inst.transform, "Cube");
            Transform doorL = FindExactChild(inst.transform, "Door L");
            Transform doorR = FindExactChild(inst.transform, "Door R");
            Vector3 garageAlignedPos = new Vector3(-13.5f, 3.42f, 0.01f);
            if (garageShell != null) garageShell.localPosition = garageAlignedPos;
            if (doorL != null) doorL.localPosition = garageAlignedPos;
            if (doorR != null) doorR.localPosition = garageAlignedPos;

            // Align Car Lift Base (Cube.001) and Car Lift Arms (Cube.002) by the exact same X offset (-5.88f)
            // so the Lift Base and Lift Arms stay 100% aligned with each other AND sit directly under Car Body (X = -18.55f)!
            Transform liftBase = FindExactChild(inst.transform, "Cube.001");
            Transform liftArms = FindExactChild(inst.transform, "Cube.002");
            Transform carBodyT = FindExactChild(inst.transform, "Car Body");
            if (liftBase != null)
            {
                liftBase.localPosition += new Vector3(-5.88f, 0f, 0f);
            }
            if (liftArms != null)
            {
                liftArms.localPosition += new Vector3(-5.88f, 0f, 0f);
            }

            CleanBlenderHelpers(inst);
            ApplyExactHouseMaterials(inst, 4);

            AddStaticCollidersToBuilding(inst, new[] { "Cube", "Cube.001", "Box", "Car Body" });

            // Action 1 [SCRIPT]: Wire updated Door L and Door R models (or fallback submesh) to slide straight UP (+3.35m) when clicked!
            if (doorL != null || doorR != null)
            {
                if (doorL != null)
                {
                    SetMeshPivotToBoundsCenter(doorL);
                    ScriptedMover moverL = doorL.gameObject.AddComponent<ScriptedMover>();
                    moverL.houseNumber = 4;
                    moverL.actionNumber = 1;
                    moverL.openPositionOffset = new Vector3(0f, 5.2f, 0f);
                }
                if (doorR != null)
                {
                    SetMeshPivotToBoundsCenter(doorR);
                    ScriptedMover moverR = doorR.gameObject.AddComponent<ScriptedMover>();
                    moverR.houseNumber = 4;
                    moverR.actionNumber = 1;
                    moverR.openPositionOffset = new Vector3(0f, 5.2f, 0f);
                }
            }
            else if (garageShell != null)
            {
                Transform extractedGarageDoor = SplitGarageDoorSubmesh(garageShell);
                if (extractedGarageDoor != null)
                {
                    ScriptedMover garageDoorMover = extractedGarageDoor.gameObject.AddComponent<ScriptedMover>();
                    garageDoorMover.houseNumber = 4;
                    garageDoorMover.actionNumber = 1;
                    garageDoorMover.openPositionOffset = new Vector3(0f, 5.2f, 0f);
                }
            }

            // Also make the Car Lift Arms raise/lower together with Car Body when clicked!
            if (liftArms != null)
            {
                ScriptedMover liftMover = liftArms.gameObject.AddComponent<ScriptedMover>();
                liftMover.houseNumber = 4;
                liftMover.actionNumber = 1;
                liftMover.openPositionOffset = new Vector3(0f, 1.25f, 0f);
                liftMover.linkedFollowTarget = carBodyT;
            }

            // Shift Toolbox (Box and Drawer) forward by -0.7m along X so it clears the garage back wall without clipping!
            Transform boxT = FindExactChild(inst.transform, "Box");
            if (boxT != null) boxT.localPosition += new Vector3(-0.7f, 0f, 0f);

            // Action 2 [SCRIPT]: Toolbox Drawer with hollow open-top colliders so tools can be placed inside!
            Transform drawerT = FindExactChild(inst.transform, "Drawer");
            if (drawerT != null) drawerT.localPosition += new Vector3(-0.7f, 0f, 0f);
            Transform handleT = FindExactChild(inst.transform, "Handle");
            if (drawerT != null)
            {
                if (handleT != null)
                {
                    handleT.SetParent(drawerT, true);
                }
                GrabAndPlaceItem.BuildHollowOpenTopColliders(drawerT.gameObject);
                ScriptedMover drawerMover = drawerT.gameObject.AddComponent<ScriptedMover>();
                drawerMover.houseNumber = 4;
                drawerMover.actionNumber = 2;
                drawerMover.openPositionOffset = new Vector3(-0.65f, 0f, 0f);
            }

            // Action 3 [GRAB] & Action 4 (Touch Car Body with Screwdriver):
            // Place Screw Driver right inside the hollow Toolbox Drawer (parented to Drawer until grabbed)!
            Transform screwT = FindPart(inst.transform, "Screw Driver");
            if (screwT != null)
            {
                if (drawerT != null)
                {
                    screwT.position = drawerT.position + new Vector3(-0.15f, 0.04f, -0.35f);
                    screwT.SetParent(drawerT, true);
                    // Lay screwdriver flat horizontally inside the drawer
                    screwT.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                GrabAndPlaceItem screwGrab = screwT.gameObject.AddComponent<GrabAndPlaceItem>();
                screwGrab.completeActionOnGrab = true;
                screwGrab.grabHouseNumber = 4;
                screwGrab.grabActionNumber = 3;
                screwGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Car",
                    houseNumber = 4,
                    actionNumber = 4,
                    specialSoundType = "Ratchet"
                });
            }

            // Action 5 [GRAB] & Action 6 (Use Wrench on Wheel Bolt / Tire):
            // Place Wrench right inside the hollow Toolbox Drawer (parented to Drawer until grabbed)!
            Transform wrenchT = FindPart(inst.transform, "Wrench");
            if (wrenchT != null)
            {
                if (drawerT != null)
                {
                    wrenchT.position = drawerT.position + new Vector3(-0.15f, 0.08f, 0.35f);
                    wrenchT.SetParent(drawerT, true);
                }
                GrabAndPlaceItem wrenchGrab = wrenchT.gameObject.AddComponent<GrabAndPlaceItem>();
                wrenchGrab.completeActionOnGrab = true;
                wrenchGrab.grabHouseNumber = 4;
                wrenchGrab.grabActionNumber = 5;
                wrenchGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Tire",
                    houseNumber = 4,
                    actionNumber = 6,
                    specialSoundType = "Ratchet"
                });
                wrenchGrab.placementRules.Add(new PlacementTargetRule
                {
                    targetNameContains = "Car",
                    houseNumber = 4,
                    actionNumber = 6,
                    specialSoundType = "Ratchet"
                });
            }

            // Action 7 [GRAB]: Stack Tires on top of each other (or put Rim in Tire)
            Transform tiresT = FindPart(inst.transform, "Tires");
            if (tiresT != null)
            {
                GameObject tire2 = UnityEngine.Object.Instantiate(tiresT.gameObject, tiresT.parent);
                tire2.name = "Tires_Spare";
                tire2.transform.localPosition += new Vector3(0f, 0f, 1.15f);

                SetupGrabRule(inst.transform, "Tires", 4, 7, "Tire");
                AddAdditionalRule(inst.transform, "Tires", 4, 7, "Rim");
                SetupGrabRule(inst.transform, "Tires_Spare", 4, 7, "Tire");
                AddAdditionalRule(inst.transform, "Tires_Spare", 4, 7, "Rim");
            }

            // Action 8 [GRAB]: Stack Rims on top of each other
            Transform rimsT = FindPart(inst.transform, "Rims");
            if (rimsT != null)
            {
                GameObject rim2 = UnityEngine.Object.Instantiate(rimsT.gameObject, rimsT.parent);
                rim2.name = "Rims_Spare";
                rim2.transform.localPosition += new Vector3(0f, 0f, 1.0f);

                SetupGrabRule(inst.transform, "Rims", 4, 8, "Rim");
                AddAdditionalRule(inst.transform, "Rims", 4, 8, "Tire");
                SetupGrabRule(inst.transform, "Rims_Spare", 4, 8, "Rim");
                AddAdditionalRule(inst.transform, "Rims_Spare", 4, 8, "Tire");
            }

            // Actions 9 & 10 [UI]: Test Horn & Test Engine Canvas placed back outside in front of the car shop,
            // facing the player approaching the shop!
            GameObject uiCanvas = CreateWorldCanvas("House4_Mechanics_UI", houseRoot.transform,
                new Vector3(-6.8f, 1.65f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(520, 560), 0.003f);

            HouseUIController uiCtrl = uiCanvas.AddComponent<HouseUIController>();
            uiCtrl.mechanicStatusLabel = CreateText(uiCanvas.transform, "MechStatus", "VEHICLE DIAGNOSTICS: READY", 19, new Vector2(0, 210), new Vector2(480, 40), TextAnchor.MiddleCenter);

            CreateButton(uiCanvas.transform, "Btn_TestHorn", "9. [UI] TEST HORN (HONK HORN)", new Vector2(0, 145), new Vector2(440, 50), new Color(0.8f, 0.55f, 0.15f),
                "MechanicHorn", uiCtrl);

            CreateButton(uiCanvas.transform, "Btn_TestEngine", "10. [UI] TEST ENGINE (REV ENGINE)", new Vector2(0, 80), new Vector2(440, 50), new Color(0.2f, 0.65f, 0.35f),
                "MechanicEngine", uiCtrl);

            Text boardText = CreateText(uiCanvas.transform, "Checklist", "", 15, new Vector2(0, -100), new Vector2(480, 280), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 3, boardText);
        }

        /// <summary>
        /// Splits Submesh 2 (Material.001 - the orange Garage Roll-Up Doors) out of Mechanic.blend's Cube mesh
        /// so the Garage Doors slide open when clicked and the garage bays are walkable!
        /// </summary>
        private static Transform SplitGarageDoorSubmesh(Transform garageCubeT)
        {
            MeshFilter mf = garageCubeT.GetComponent<MeshFilter>();
            MeshRenderer mr = garageCubeT.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 3)
                return null;

            Mesh srcMesh = mf.sharedMesh;
            Material[] mats = mr.sharedMaterials;

            Transform doorChild = BuildChildFromTriangleSubset(garageCubeT, srcMesh, srcMesh.GetTriangles(2), "Garage_RollUp_Door", mats.Length > 2 ? mats[2] : mats[0], false);

            Mesh bodyMesh = new Mesh();
            bodyMesh.name = "Garage_Building_NoDoor";
            bodyMesh.vertices = srcMesh.vertices;
            bodyMesh.normals = srcMesh.normals;
            bodyMesh.uv = srcMesh.uv;
            bodyMesh.subMeshCount = 2;
            bodyMesh.SetTriangles(srcMesh.GetTriangles(0), 0);
            bodyMesh.SetTriangles(srcMesh.GetTriangles(1), 1);
            bodyMesh.RecalculateBounds();
            mf.sharedMesh = bodyMesh;

            Material[] bodyMats = new Material[2] { mats[0], mats[1] };
            mr.sharedMaterials = bodyMats;

            return doorChild;
        }

        // =====================================================================
        // HOUSE 5: BACKROOMS ("The Glitched Noclip Reality")
        // =====================================================================
        private static void BuildHouse5_Backrooms(Transform worldRoot, VillageActionManager actionManager)
        {
            // Part A: Village Exterior House with Front Door & Noclip Trigger
            // Rotate +90 Y so local -Z (where Door is at Z = -5.64!) faces West (-X) toward the village plaza!
            GameObject exteriorRoot = new GameObject("House5_Backrooms_VillageExterior");
            exteriorRoot.transform.SetParent(worldRoot);
            exteriorRoot.transform.position = new Vector3(26f, 0f, -8f);
            exteriorRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject blendAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/BackRooms/Backrooms.blend");
            if (blendAsset == null) return;

            GameObject extInst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
            PrefabUtility.UnpackPrefabInstance(extInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            extInst.name = "Backrooms_Exterior_Shell";
            extInst.transform.SetParent(exteriorRoot.transform, false);

            CleanBlenderHelpers(extInst);
            ApplyExactHouseMaterials(extInst, 5);

            SetOnlyActiveProps(extInst.transform, new[] { "House", "Roof", "Door" });
            AddStaticCollidersToBuilding(extInst, new[] { "House", "Roof" });

            // Action 1: Open Front Door of Backrooms House (hinged on its left edge in extInst local space so it swings open cleanly!)
            Transform extDoorT = FindExactChild(extInst.transform, "Door");
            if (extDoorT != null)
            {
                SetMeshPivotToBoundsEdge(extDoorT, true);
                ScriptedMover extDoorMover = extDoorT.gameObject.AddComponent<ScriptedMover>();
                extDoorMover.houseNumber = 5;
                extDoorMover.actionNumber = 1;
                extDoorMover.openYawAngle = 85f;
            }

            // Action 2 [SCRIPT/TELEPORT]: Step through doorway Noclip Portal -> Teleports to Backrooms!
            GameObject portalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portalGO.name = "Backrooms_Noclip_Portal_Trigger";
            portalGO.transform.SetParent(exteriorRoot.transform, false);
            portalGO.transform.localPosition = new Vector3(0f, 1.85f, -5.25f);
            portalGO.transform.localRotation = Quaternion.identity;
            portalGO.transform.localScale = new Vector3(2.5f, 3.6f, 0.9f);
            portalGO.GetComponent<Collider>().isTrigger = true;
            portalGO.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial("GlitchPortal", new Color(0.95f, 0.88f, 0.25f, 0.35f), 0.9f, true, true);
            portalGO.AddComponent<BackroomsNoclipPortal>();

            // Exterior Board for House 5 placed right beside the Front Door (local Z = -6.2f, facing local -Z!)
            GameObject extBoard = CreateWorldCanvas("House5_Exterior_Board", exteriorRoot.transform,
                new Vector3(-4.2f, 1.6f, -6.2f), Quaternion.identity, new Vector2(520, 520), 0.003f);
            CreateButton(extBoard.transform, "Btn_EnterBackrooms", "2. STEP IN / TELEPORT TO BACKROOMS", new Vector2(0, 195), new Vector2(460, 48), new Color(0.75f, 0.6f, 0.15f),
                "TeleportBackrooms");
            Text extBoardText = CreateText(extBoard.transform, "Checklist", "", 15, new Vector2(0, -45), new Vector2(480, 380), TextAnchor.UpperLeft);
            EnsureBoardSlot(actionManager, 4, extBoardText);

            // =================================================================
            // Part B: The Backrooms Complex at (2000, 0, 2000) — Scaled 1.6x (2km away from Village!)
            // Completely isolated in its own void dimension, loading dynamically only when nocliped!
            // =================================================================
            GameObject backroomsRoot = new GameObject("House5_Backrooms_ProceduralDimension");
            backroomsRoot.transform.SetParent(worldRoot);
            backroomsRoot.transform.position = new Vector3(2000f, 0f, 2000f);

            Material carpetMat = GetTexMat("H5_CarpetFloor", $"{HousesFolder}/BackRooms/Backrooms_Carpet_Texture.png", 0.1f);
            if (carpetMat == null) carpetMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_H5_CarpetFloor.mat");

            GameObject kitInst = (GameObject)PrefabUtility.InstantiatePrefab(blendAsset);
            PrefabUtility.UnpackPrefabInstance(kitInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            kitInst.name = "Backrooms_Core_Assets";
            kitInst.transform.SetParent(backroomsRoot.transform, false);
            kitInst.transform.localPosition = Vector3.zero;

            CleanBlenderHelpers(kitInst);
            ApplyExactHouseMaterials(kitInst, 5);

            foreach (string hideName in new[] { "House", "Roof", "Door", "Plane" })
            {
                Transform ht = FindExactChild(kitInst.transform, hideName);
                if (ht != null) ht.gameObject.SetActive(false);
            }

            // Action 8 [SCRIPT]: Ceiling with Light flickers and buzzes overhead
            Transform ceilLightT = FindPart(kitInst.transform, "Ceiling with Light");
            if (ceilLightT != null)
            {
                GameObject ptLightGO = new GameObject("Fluorescent_PointLight");
                ptLightGO.transform.SetParent(ceilLightT, false);
                ptLightGO.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                Light pt = ptLightGO.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.color = new Color(1f, 0.97f, 0.82f);
                pt.intensity = 2.6f;
                pt.range = 12f;
                pt.shadows = LightShadows.None;

                BackroomsFlickerLight flicker = ceilLightT.gameObject.AddComponent<BackroomsFlickerLight>();
                flicker.pointLight = pt;
            }

            // Position Desk, Drawer, Cube, Flash Light, and Almond Water inside the Starting Foyer relative to backroomsRoot!
            Transform deskT = FindExactChild(kitInst.transform, "Desk");
            Transform deskDrawerT = FindExactChild(kitInst.transform, "Drawer");
            Transform cubePropT = FindExactChild(kitInst.transform, "Cube");

            if (deskT != null)
            {
                Vector3 targetDeskPos = backroomsRoot.transform.position + new Vector3(4.2f, 0.24f, 4.8f);
                Vector3 deskDelta = targetDeskPos - deskT.position;
                deskT.position += deskDelta;
                if (deskDrawerT != null)
                {
                    deskDrawerT.position += deskDelta;
                }
                AddStaticCollidersToBuilding(deskT.gameObject, new[] { "Desk" });
            }
            if (cubePropT != null)
            {
                cubePropT.position = backroomsRoot.transform.position + new Vector3(5.6f, 0.46f, 4.8f);
                AddStaticCollidersToBuilding(cubePropT.gameObject, new[] { "Cube" });
            }

            // Action 7 [SCRIPT]: Desk Drawer with upright baked pivot and hollow open-top collider slides open via script!
            if (deskDrawerT != null)
            {
                SetMeshPivotToBoundsCenter(deskDrawerT);
                GrabAndPlaceItem.BuildHollowOpenTopColliders(deskDrawerT.gameObject);
                ScriptedMover drawerMover = deskDrawerT.gameObject.AddComponent<ScriptedMover>();
                drawerMover.houseNumber = 5;
                drawerMover.actionNumber = 7;
                drawerMover.openPositionOffset = new Vector3(0f, 0f, -0.55f);
            }

            // Action 3 [GRAB] & Action 4 [UI/AUTO]: Flash Light + Forward Spotlight Beam + Auto On when picked up / Off when put down!
            Transform flashT = FindPart(kitInst.transform, "Flash Light");
            Light flashlightSpot = null;
            FlashlightAutoLightController flashAutoCtrl = null;
            if (flashT != null)
            {
                flashT.position = backroomsRoot.transform.position + new Vector3(2.8f, 0.45f, 4.2f);
                GrabAndPlaceItem flashGrab = flashT.gameObject.AddComponent<GrabAndPlaceItem>();
                flashGrab.completeActionOnGrab = true;
                flashGrab.grabHouseNumber = 5;
                flashGrab.grabActionNumber = 3;

                GameObject beamGO = new GameObject("Flashlight_Spotlight_Beam");
                beamGO.transform.SetParent(flashT, false);
                beamGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                beamGO.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                flashlightSpot = beamGO.AddComponent<Light>();
                flashlightSpot.type = LightType.Spot;
                flashlightSpot.spotAngle = 48f;
                flashlightSpot.range = 24f;
                flashlightSpot.intensity = 4.5f;
                flashlightSpot.color = new Color(1f, 0.98f, 0.9f);
                flashlightSpot.enabled = false;

                GameObject attachGO = new GameObject("Flashlight_HandAttach");
                attachGO.transform.SetParent(flashT, false);
                attachGO.transform.localPosition = new Vector3(0f, 0f, 0.16f);
                attachGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                XRGrabInteractable xrg = flashT.GetComponent<XRGrabInteractable>();
                if (xrg != null)
                {
                    xrg.useDynamicAttach = false;
                    xrg.matchAttachPosition = true;
                    xrg.matchAttachRotation = true;
                    xrg.attachTransform = attachGO.transform;
                }

                flashAutoCtrl = flashT.gameObject.AddComponent<FlashlightAutoLightController>();
                flashAutoCtrl.spotlightBeam = flashlightSpot;
            }

            // Action 5 [GRAB] (Place Almond Water onto Desk/Drawer) & Action 6 [SCRIPT] (Drink 5 Almond Waters to teleport out)
            // Exactly ONE starter bottle placed on the Desk in the Starting Foyer!
            // Remaining bottles are procedurally distributed to distant corners/dead-ends across the Backrooms.
            Transform almondT = FindPart(kitInst.transform, "Almond Water");
            List<AlmondWaterBottle> allBottles = new List<AlmondWaterBottle>();
            if (almondT != null)
            {
                almondT.position = backroomsRoot.transform.position + new Vector3(3.4f, 0.45f, 4.2f);
                ConfigureAlmondWaterBottle(almondT.gameObject, allBottles);
            }

            // Extract Surreal "No-Clipped" Junk Prop Templates from Houses 1-4
            GameObject junkTemplatesRoot = new GameObject("Backrooms_Junk_Templates");
            junkTemplatesRoot.transform.SetParent(backroomsRoot.transform, false);
            junkTemplatesRoot.SetActive(false);

            List<GameObject> h1Props = new List<GameObject>();
            List<GameObject> h2Props = new List<GameObject>();
            List<GameObject> h3Props = new List<GameObject>();
            List<GameObject> h4Props = new List<GameObject>();

            // House 1 (Storage Unit)
            GameObject h1Blend = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Storage Unit/Storage Unit.blend");
            if (h1Blend != null)
            {
                GameObject h1Inst = (GameObject)PrefabUtility.InstantiatePrefab(h1Blend);
                h1Inst.transform.SetParent(junkTemplatesRoot.transform, false);
                CleanBlenderHelpers(h1Inst);
                ApplyExactHouseMaterials(h1Inst, 1);
                foreach (string p in new[] { "Box", "Trash", "L Block", "T Block", "Tetris Line" })
                {
                    Transform t = FindPart(h1Inst.transform, p);
                    if (t != null)
                    {
                        t.gameObject.SetActive(true);
                        EnsureStaticColliderHelper(t.gameObject);
                        h1Props.Add(t.gameObject);
                    }
                }
            }

            // House 2 (Residential House)
            GameObject h2Blend = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/House/House.blend");
            if (h2Blend != null)
            {
                GameObject h2Inst = (GameObject)PrefabUtility.InstantiatePrefab(h2Blend);
                h2Inst.transform.SetParent(junkTemplatesRoot.transform, false);
                CleanBlenderHelpers(h2Inst);
                ApplyExactHouseMaterials(h2Inst, 2);
                foreach (string p in new[] { "Pan", "Stove", "Toilet", "Window" })
                {
                    Transform t = FindPart(h2Inst.transform, p);
                    if (t != null)
                    {
                        t.gameObject.SetActive(true);
                        EnsureStaticColliderHelper(t.gameObject);
                        h2Props.Add(t.gameObject);
                    }
                }
            }

            // House 3 (Convenience Shop)
            GameObject h3Blend = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Shop/Shop.blend");
            if (h3Blend != null)
            {
                GameObject h3Inst = (GameObject)PrefabUtility.InstantiatePrefab(h3Blend);
                h3Inst.transform.SetParent(junkTemplatesRoot.transform, false);
                CleanBlenderHelpers(h3Inst);
                ApplyExactHouseMaterials(h3Inst, 3);
                foreach (string p in new[] { "Shelf", "Bottle", "Chips", "Leak", "Basket" })
                {
                    Transform t = FindPart(h3Inst.transform, p);
                    if (t != null)
                    {
                        t.gameObject.SetActive(true);
                        EnsureStaticColliderHelper(t.gameObject);
                        h3Props.Add(t.gameObject);
                    }
                }
            }

            // House 4 (Mechanics Shop)
            GameObject h4Blend = AssetDatabase.LoadAssetAtPath<GameObject>($"{HousesFolder}/Mechanics shop/Mechanic.blend");
            if (h4Blend != null)
            {
                GameObject h4Inst = (GameObject)PrefabUtility.InstantiatePrefab(h4Blend);
                h4Inst.transform.SetParent(junkTemplatesRoot.transform, false);
                CleanBlenderHelpers(h4Inst);
                ApplyExactHouseMaterials(h4Inst, 4);
                foreach (string p in new[] { "Wheel", "Rim", "Box", "Screw Driver", "Wrench" })
                {
                    Transform t = FindPart(h4Inst.transform, p);
                    if (t != null)
                    {
                        t.gameObject.SetActive(true);
                        EnsureStaticColliderHelper(t.gameObject);
                        h4Props.Add(t.gameObject);
                    }
                }
            }

            // Actions 9 & 10 [SCRIPT]: Backrooms Monster Entity using the NurbsPath mesh from Backrooms.blend!
            Transform nurbsMonsterT = FindPart(kitInst.transform, "NurbsPath");
            BuildBackroomsMonster(backroomsRoot.transform, nurbsMonsterT);

            // Procedural Backrooms Generator (Supports Infinite Chunk Streaming & Enclosed Labyrinth)
            BackroomsProceduralGenerator procGen = backroomsRoot.AddComponent<BackroomsProceduralGenerator>();
            procGen.gridWidth = 8;
            procGen.gridDepth = 8;
            procGen.scaleMultiplier = 1.6f;
            procGen.cellSize = 4.8f;
            procGen.generationMode = BackroomsGenMode.InfiniteStreaming;
            // 65% of cell edges passable, so the infinite maze connects instead of closing into pockets
            procGen.openSpaceChance = 0.35f;
            procGen.doorwayChance = 0.30f;
            procGen.carpetMaterial = carpetMat;
            procGen.customBackroomsSkybox = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_BackroomsDarkSkybox.mat");
            procGen.backroomsVoidColor = new Color(0.035f, 0.033f, 0.028f, 1f);
            procGen.enableBackroomsFog = true;
            procGen.backroomsFogMode = FogMode.Linear;
            procGen.backroomsFogStart = 22f;
            procGen.backroomsFogEnd = 70f;
            procGen.backroomsFogColor = new Color(0.045f, 0.042f, 0.035f, 1f);
            procGen.ceilingLightChance = 0.65f;
            procGen.lightFlickerChance = 0.15f;
            procGen.ceilingLightIntensity = 2.8f;
            procGen.ceilingLightRange = 13.5f;
            procGen.enableBackroomsAmbient = true;
            procGen.backroomsAmbientColor = new Color(0.22f, 0.20f, 0.16f, 1f);
            procGen.backroomsAmbientIntensity = 1.2f;
            procGen.enableDelayedMonsterSpawn = true;
            procGen.monsterSpawnCondition = MonsterSpawnTriggerMode.TimeOrDrinks;
            procGen.monsterSpawnDelaySeconds = 60f;
            procGen.monsterAwakenDrinkCount = 2;
            procGen.playAwakenAudioCue = true;
            procGen.awakenAudioVolume = 0.95f;

            Transform wallT = FindExactChild(kitInst.transform, "Wall");
            Transform wallDoorT = FindExactChild(kitInst.transform, "Wall With Door");
            Transform ceilT = FindExactChild(kitInst.transform, "Ceiling");
            if (wallT != null) procGen.solidWallTemplate = wallT.gameObject;
            if (wallDoorT != null) procGen.doorwayWallTemplate = wallDoorT.gameObject;
            if (ceilT != null) procGen.ceilingTemplate = ceilT.gameObject;
            if (ceilLightT != null) procGen.ceilingLightTemplate = ceilLightT.gameObject;
            if (deskT != null) procGen.deskTemplate = deskT.gameObject;
            if (deskDrawerT != null) procGen.drawerTemplate = deskDrawerT.gameObject;
            if (flashT != null) procGen.flashlightTemplate = flashT.gameObject;
            if (almondT != null) procGen.almondWaterTemplate = almondT.gameObject;

            procGen.house1Props = h1Props.ToArray();
            procGen.house2Props = h2Props.ToArray();
            procGen.house3Props = h3Props.ToArray();
            procGen.house4Props = h4Props.ToArray();

            if (wallT != null) wallT.gameObject.SetActive(false);
            if (wallDoorT != null) wallDoorT.gameObject.SetActive(false);
            if (ceilT != null) ceilT.gameObject.SetActive(false);
            if (ceilLightT != null) ceilLightT.gameObject.SetActive(false);

            // Backrooms Interior Control & Flashlight UI Canvas right in front of Backrooms_SpawnPoint
            GameObject brCanvas = CreateWorldCanvas("House5_Backrooms_Interior_UI", backroomsRoot.transform,
                new Vector3(1.2f, 1.65f, 5.2f), Quaternion.Euler(0f, -20f, 0f), new Vector2(540, 260), 0.003f);

            // Player-facing controls only. Narin's test buttons (generation mode, regenerate, sound test, skybox
            // toggle, force monster) are left out; god mode and pause AI stay under VR Village/Narin Debug.
            HouseUIController brUiCtrl = brCanvas.AddComponent<HouseUIController>();
            brUiCtrl.flashlightBeam = flashlightSpot;
            brUiCtrl.flashlightStatusLabel = CreateText(brCanvas.transform, "FlashStatus", "BACKROOMS TERMINAL | LIGHT: OFF (GRAB TO TURN ON)", 16, new Vector2(0, 95), new Vector2(500, 36), TextAnchor.MiddleCenter);
            if (flashAutoCtrl != null)
            {
                flashAutoCtrl.statusLabel = brUiCtrl.flashlightStatusLabel;
            }

            CreateButton(brCanvas.transform, "Btn_FlashlightToggle", "4. [UI] TOGGLE FLASHLIGHT SWITCH", new Vector2(0, 40), new Vector2(460, 44), new Color(0.75f, 0.6f, 0.15f),
                "FlashlightToggle", brUiCtrl);

            CreateButton(brCanvas.transform, "Btn_DrinkAlmond", "6. DRINK ALMOND WATER (5x TO ESCAPE)", new Vector2(0, -15), new Vector2(460, 44), new Color(0.2f, 0.65f, 0.75f),
                "DrinkAlmond", brUiCtrl);

            CreateButton(brCanvas.transform, "Btn_EscapeSpawn", "EMERGENCY TELEPORT TO VILLAGE", new Vector2(0, -70), new Vector2(460, 44), new Color(0.25f, 0.7f, 0.35f),
                "TeleportSpawn");

            // Deactivate Backrooms dimension by default: it does NOT load or use resources until the player noclips!
            backroomsRoot.SetActive(false);
        }

        private static void ConfigureAlmondWaterBottle(GameObject bottleGO, List<AlmondWaterBottle> list)
        {
            // Remove any previously cloned child button canvas BEFORE GrabAndPlaceItem.Awake collects colliders!
            Transform existingBtn = bottleGO.transform.Find("Bottle_Drink_Button_Canvas");
            if (existingBtn != null) UnityEngine.Object.DestroyImmediate(existingBtn.gameObject);

            GrabAndPlaceItem grab = bottleGO.GetComponent<GrabAndPlaceItem>();
            if (grab == null) grab = bottleGO.AddComponent<GrabAndPlaceItem>();
            grab.placementRules.Clear();
            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = "Desk",
                houseNumber = 5,
                actionNumber = 5
            });
            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = "Drawer",
                houseNumber = 5,
                actionNumber = 5
            });

            // Ensure physical BoxCollider
            BoxCollider bc = bottleGO.GetComponent<BoxCollider>();
            if (bc == null) bc = bottleGO.AddComponent<BoxCollider>();
            MeshFilter mf = bottleGO.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                bc.center = b.center;
                Vector3 s = b.size;
                bc.size = new Vector3(Mathf.Max(s.x, 0.12f), Mathf.Max(s.y, 0.12f), Mathf.Max(s.z, 0.12f));
            }
            else
            {
                bc.center = Vector3.zero;
                bc.size = new Vector3(0.15f, 0.35f, 0.15f);
            }
            bc.isTrigger = false;

            // Ensure Rigidbody
            Rigidbody rb = bottleGO.GetComponent<Rigidbody>();
            if (rb == null) rb = bottleGO.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 localUp = bottleGO.transform.InverseTransformDirection(Vector3.up);
            rb.centerOfMass = -localUp * 0.08f;

            AlmondWaterBottle drink = bottleGO.GetComponent<AlmondWaterBottle>();
            if (drink == null) drink = bottleGO.AddComponent<AlmondWaterBottle>();
            list.Add(drink);

            // Remove any conflicting XRSimpleInteractable from bottle root
            XRSimpleInteractable simple = bottleGO.GetComponent<XRSimpleInteractable>();
            if (simple != null) UnityEngine.Object.DestroyImmediate(simple);

            // Ensure XRGrabInteractable
            XRGrabInteractable xri = bottleGO.GetComponent<XRGrabInteractable>();
            if (xri == null) xri = bottleGO.AddComponent<XRGrabInteractable>();
            xri.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            xri.throwOnDetach = true;
            xri.interactionLayers = 1;
            xri.retainTransformParent = false;
            xri.colliders.Clear();
            if (bc != null) xri.colliders.Add(bc);

            VillageUIButtonBinding[] bindings = bottleGO.GetComponentsInChildren<VillageUIButtonBinding>(true);
            foreach (var b in bindings)
            {
                if (b != null && b.commandType == "DrinkAlmond")
                    b.targetAlmondBottle = drink;
            }

            Transform existingCanvas = bottleGO.transform.Find("Bottle_Drink_Button_Canvas");
            if (existingCanvas == null)
            {
                // Attach a compact floating World-Space [DRINK WATER] button well above each Almond Water bottle's collider
                GameObject btnCanvas = CreateWorldCanvas("Bottle_Drink_Button_Canvas", bottleGO.transform,
                    new Vector3(0f, 0f, 0.52f), Quaternion.Euler(90f, 0f, 0f), new Vector2(200, 64), 0.0020f);
                Text btnTxt = CreateButton(btnCanvas.transform, "Btn_DrinkThisBottle", "DRINK WATER", Vector2.zero, new Vector2(186, 52),
                    new Color(0.15f, 0.65f, 0.8f), "DrinkAlmond");
                VillageUIButtonBinding binding = btnTxt.transform.parent.GetComponent<VillageUIButtonBinding>();
                if (binding != null)
                {
                    binding.targetAlmondBottle = drink;
                }
            }
        }

        private static void BuildBackroomsMonster(Transform backroomsRoot, Transform nurbsMonsterT)
        {
            GameObject monster = new GameObject("Backrooms_Monster_Entity");
            monster.transform.SetParent(backroomsRoot, false);
            monster.transform.localPosition = new Vector3(14.4f, 0.05f, 19.2f);

            if (nurbsMonsterT != null)
            {
                nurbsMonsterT.gameObject.SetActive(true);
                nurbsMonsterT.SetParent(monster.transform, false);
                nurbsMonsterT.localScale = Vector3.one * 0.60f;
                nurbsMonsterT.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                nurbsMonsterT.localPosition = Vector3.zero;

                Renderer mr = nurbsMonsterT.GetComponentInChildren<Renderer>();
                if (mr != null)
                {
                    Bounds b = mr.bounds;
                    Vector3 offset = monster.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                    nurbsMonsterT.position += offset;
                }
            }

            BackroomsMonsterAI ai = monster.AddComponent<BackroomsMonsterAI>();
            ai.playerFoundVolume = 0.95f;
            ai.playerFoundPitch = 1.0f;
            ai.playerFoundSpatialBlend = 0.2f;
            ai.playerFoundCooldown = 3.5f;
            ai.proceduralSoundStyle = PlayerFoundSoundStyle.DramaticHorrorStinger;
            ai.patrolWaypoints = new List<Vector3>
            {
                new Vector3(214.4f, 0.05f, 219.2f),
                new Vector3(219.2f, 0.05f, 219.2f),
                new Vector3(224.0f, 0.05f, 219.2f),
                new Vector3(224.0f, 0.05f, 224.0f),
                new Vector3(219.2f, 0.05f, 224.0f),
                new Vector3(214.4f, 0.05f, 224.0f)
            };
        }

        private static void BuildCentralDebugAndProgressHub(Transform worldRoot, VillageActionManager actionManager)
        {
            GameObject hubCanvas = CreateWorldCanvas("Village_Master_Control_Board", worldRoot,
                new Vector3(0f, 1.7f, -7.5f), Quaternion.identity, new Vector2(680, 560), 0.0035f);

            CreateText(hubCanvas.transform, "Title", "<b>VR VILLAGE MASTER PROGRESS & DEBUG</b>", 22, new Vector2(0, 235), new Vector2(640, 40), TextAnchor.MiddleCenter);

            Text summaryText = CreateText(hubCanvas.transform, "Summary", "", 17, new Vector2(-145, 20), new Vector2(340, 320), TextAnchor.UpperLeft);
            actionManager.wristStatusText = summaryText;

            Text masterGodText = CreateButton(hubCanvas.transform, "Btn_MasterGodMode", "GOD MODE (INVISIBLE): OFF", new Vector2(165, 135), new Vector2(300, 48), new Color(0.35f, 0.25f, 0.75f),
                "GodMode");
            actionManager.godModeButtonTexts.Add(masterGodText);

            Text masterPauseText = CreateButton(hubCanvas.transform, "Btn_MasterPauseMonster", "PAUSE ENTITY AI: ACTIVE", new Vector2(165, 75), new Vector2(300, 48), new Color(0.75f, 0.35f, 0.2f),
                "PauseMonster");
            actionManager.monsterPauseButtonTexts.Add(masterPauseText);

            CreateButton(hubCanvas.transform, "Btn_MasterBackrooms", "TELEPORT TO BACKROOMS (HOUSE 5)", new Vector2(165, 15), new Vector2(300, 48), new Color(0.75f, 0.55f, 0.15f),
                "TeleportBackrooms");

            CreateButton(hubCanvas.transform, "Btn_MasterSpawn", "RESET TO VILLAGE SPAWN", new Vector2(165, -45), new Vector2(300, 48), new Color(0.2f, 0.65f, 0.35f),
                "TeleportSpawn");

            actionManager.RefreshAllUI();
        }

        // =====================================================================
        // HELPER METHODS FOR COLLIDERS, BLENDER CLEANUP & UI CANVASES
        // =====================================================================
        private static void CleanBlenderHelpers(GameObject root)
        {
            List<GameObject> toRemove = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;

                // IMPORTANT: Do NOT overwrite negative localScale with Mathf.Abs!
                // Objects like Shop Shelf, Mechanic Car Lift Arms (Cube.002), and Storage Table
                // rely on their Blender negative scale to stay right-side-up and aligned!

                string n = t.name;
                if (n.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("Sun", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("Camera", StringComparison.OrdinalIgnoreCase))
                {
                    toRemove.Add(t.gameObject);
                }
            }
            foreach (GameObject go in toRemove)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void AddStaticCollidersToBuilding(GameObject root, string[] staticNames)
        {
            foreach (string sName in staticNames)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals(sName, StringComparison.OrdinalIgnoreCase))
                    {
                        MeshFilter mf = t.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            MeshCollider mc = t.GetComponent<MeshCollider>();
                            if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                        }
                    }
                }
            }
        }

        private static void EnsureStaticColliderHelper(GameObject go)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        private static void SetupGrabRule(Transform root, string partName, int houseNum, int actionNum, string targetContains, bool attachOnPlace = false)
        {
            Transform part = FindPart(root, partName);
            if (part == null) return;

            GrabAndPlaceItem grab = part.GetComponent<GrabAndPlaceItem>();
            if (grab == null) grab = part.gameObject.AddComponent<GrabAndPlaceItem>();

            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = targetContains,
                houseNumber = houseNum,
                actionNumber = actionNum,
                attachToTargetOnPlace = attachOnPlace
            });
        }

        private static void AddAdditionalRule(Transform root, string partName, int houseNum, int actionNum, string targetContains)
        {
            Transform part = FindPart(root, partName);
            if (part == null) return;

            GrabAndPlaceItem grab = part.GetComponent<GrabAndPlaceItem>();
            if (grab == null) grab = part.gameObject.AddComponent<GrabAndPlaceItem>();

            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = targetContains,
                houseNumber = houseNum,
                actionNumber = actionNum
            });
        }

        private static void SetOnlyActiveProps(Transform unitRoot, string[] keepNames)
        {
            foreach (Transform child in unitRoot)
            {
                bool keep = false;
                foreach (string k in keepNames)
                {
                    if (child.name.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                        child.name.StartsWith(k, StringComparison.OrdinalIgnoreCase))
                    {
                        keep = true;
                        break;
                    }
                }
                child.gameObject.SetActive(keep);
            }
        }

        private static Transform FindExactChild(Transform root, string exactName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        private static Transform FindPart(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string nameContains)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            return null;
        }

        private static void AddInteriorLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            GameObject lightGO = new GameObject("Interior_PointLight");
            lightGO.transform.SetParent(parent, false);
            lightGO.transform.localPosition = localPos;
            Light l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static GameObject CreateWorldCanvas(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector2 size, float scale)
        {
            GameObject canvasGO = new GameObject(name);
            canvasGO.transform.SetParent(parent, false);
            canvasGO.transform.localPosition = localPos;
            canvasGO.transform.localRotation = localRot;

            Canvas c = canvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) c.worldCamera = Camera.main;
            CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
            // 10 overflowed the shared legacy font texture (4096) with the big checklist boards
            cs.dynamicPixelsPerUnit = 3f;

            GraphicRaycaster gr = canvasGO.AddComponent<GraphicRaycaster>();
            gr.ignoreReversedGraphics = false;

            TrackedDeviceGraphicRaycaster tdgr = canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();
            tdgr.ignoreReversedGraphics = false;

            RectTransform rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;

            GameObject panel = new GameObject("BackgroundPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            bg.raycastTarget = false;
            RectTransform bgRt = panel.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            return canvasGO;
        }

        private static Text CreateText(Transform canvasParent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor align)
        {
            GameObject txtGO = new GameObject(name);
            txtGO.transform.SetParent(canvasParent, false);
            Text t = txtGO.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;

            RectTransform rt = txtGO.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return t;
        }

        private static Text CreateButton(Transform canvasParent, string name, string label, Vector2 anchoredPos, Vector2 size, Color bgColor, string commandType, HouseUIController uiCtrl = null, float floatParam = 0f)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(canvasParent, false);
            Image img = btnGO.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            // Universal VR Interaction: Trigger BoxCollider + XRSimpleInteractable + UGUI Button
            BoxCollider bc = btnGO.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(size.x, size.y, 40f);

            btnGO.AddComponent<XRSimpleInteractable>();

            VillageUIButtonBinding binding = btnGO.AddComponent<VillageUIButtonBinding>();
            binding.commandType = commandType;
            binding.uiController = uiCtrl;
            binding.floatParameter = floatParam;

            Text txt = CreateText(btnGO.transform, "Label", $"<b>{label}</b>", 15, Vector2.zero, size, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            return txt;
        }

        private static void EnsureBoardSlot(VillageActionManager manager, int index, Text boardText)
        {
            while (manager.houseBoardTexts.Count <= index)
            {
                manager.houseBoardTexts.Add(null);
            }
            manager.houseBoardTexts[index] = boardText;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
