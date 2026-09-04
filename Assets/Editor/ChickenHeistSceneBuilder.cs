using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChickenHeistSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ChickenHeistPrototype.unity";
    private const string AnimalLibraryPath = "Assets/ThirdParty/EverythingLibrary_Animals/Models/EverythingLibrary_Animals_001.fbx";
    private const string GeneratedAnimalFolder = "Assets/ChickenHeistGenerated/Animals";
    private const string VertexColorShaderPath = "Assets/Shaders/ChickenHeistVertexColor.shader";
    private const string VertexColorMaterialPath = "Assets/ChickenHeistGenerated/Animals/EverythingAnimalsVertexColor.mat";

    [MenuItem("Chicken Heist/Open Prototype Scene")]
    public static void OpenPrototypeScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Chicken Heist/Build Prototype Scene")]
    public static void BuildPrototypeScene()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ChickenHeistPrototype";

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.03f;
        cameraObject.AddComponent<AudioListener>();

        GameObject generatorObject = new GameObject("Procedural Farm Generator");
        ProceduralFarmGenerator generator = generatorObject.AddComponent<ProceduralFarmGenerator>();
        generator.randomizeSeed = true;
        generator.difficulty = 2;
        generator.farmColumns = 4;
        generator.farmRows = 3;
        generator.lotWidth = 66f;
        generator.lotDepth = 56f;
        generator.roadWidth = 118f;
        generator.farmScatter = 0.38f;
        generator.worldBorderPadding = 140f;
        generator.pathWidth = 6f;
        generator.fenceReferenceLength = 3.6f;
        generator.fenceOverlap = 1.65f;
        generator.forestTreeCount = 3200;
        generator.forestBushCount = 1500;
        generator.forestRockCount = 950;
        generator.wildGrassCount = 1800;
        generator.flowerCount = 1000;
        generator.stumpCount = 300;
        generator.forestDetailCount = 950;
        generator.forestClusterCount = 36;
        generator.treesPerCluster = 22;
        generator.forestClusterRadius = 21f;
        generator.reliefCount = 24;
        generator.reliefMinHeight = 1.5f;
        generator.reliefMaxHeight = 4.8f;
        generator.reliefMinRadius = 20f;
        generator.reliefMaxRadius = 38f;
        generator.terrainResolutionX = 132;
        generator.terrainResolutionZ = 104;
        generator.terrainMaxHeight = 7f;
        generator.terrainEdgeHeight = 10f;
        generator.terrainNoiseScale = 0.012f;
        generator.chickenCount = 8;
        generator.cowCount = 3;
        generator.housePrefab = LoadAsset("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_FarmerHouse.prefab");
        generator.barnPrefab = LoadAsset("Assets/Quaternius_Farm_Buildings/FBX/Barn.fbx");
        generator.fencePrefab = LoadAsset("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_WoodFence_01.prefab");
        generator.gatePrefab = LoadAsset("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_WoodFence_05.prefab");
        generator.chickenHousePrefab = LoadAsset("Assets/Quaternius_Farm_Buildings/FBX/ChickenCoop.fbx");
        generator.troughPrefab = LoadAsset("Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Trough_01.fbx");
        generator.siloPrefab = LoadAsset("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_Silo_01.prefab");
        generator.housePrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_FarmerHouse.prefab");
        generator.barnPrefabs = LoadAssets(
            "Assets/Quaternius_Farm_Buildings/FBX/Barn.fbx",
            "Assets/Quaternius_Farm_Buildings/FBX/BigBarn.fbx",
            "Assets/Quaternius_Farm_Buildings/FBX/SmallBarn.fbx",
            "Assets/Quaternius_Farm_Buildings/FBX/OpenBarn.fbx",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_Barn_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_Barn_02.prefab");
        generator.fencePrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_WoodFence_01.prefab");
        generator.gatePrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_WoodFence_05.prefab");
        generator.chickenCoopPrefabs = LoadAssets(
            "Assets/Quaternius_Farm_Buildings/FBX/ChickenCoop.fbx",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_ChickenCoop.prefab",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Bld_Chicken_Coop_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Bld_Chicken_Coop_02.fbx");
        generator.troughPrefabs = LoadAssets(
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Trough_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Trough_02.fbx");
        generator.feederPrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Prop_AnimalFeeder_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Prop_AnimalFeeder_02.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Prop_AnimalFeeder_03.prefab");
        generator.siloPrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_Silo_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_Silo_02.prefab");
        generator.ruralLandPrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_FarmLand_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_FarmLand_02.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_FarmLand_03.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_FarmLand_04.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_FarmLand_05.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Wheat.prefab",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Dirt_Rows_Long_Watered_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Dirt_Large_Watered_01.fbx");
        generator.ruralPropPrefabs = LoadAssets(
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Hay_Bale_Round_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Hay_Bale_Square_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Crate_Box_Pile_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Barrel_Pile_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Tool_Wheelbarrow_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Scarecrow_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Water_Barrel_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Plank_Stack_01.fbx");
        generator.hayPrefabs = LoadAssets(
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Hay_Bale_Round_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Hay_Bale_Square_01.fbx",
            "Assets/DuNguyn/Hay Bale/Prefabs/HayRoll_Straw.prefab",
            "Assets/DuNguyn/Hay Bale/Prefabs/HayCock_Straw.prefab");
        generator.storagePropPrefabs = LoadAssets(
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Crate_Box_Pile_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Barrel_Pile_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Water_Barrel_01.fbx",
            "Assets/Prefabs/Environment/WoodenBox_Stack.fbx");
        generator.fieldPropPrefabs = LoadAssets(
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Tool_Wheelbarrow_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Prop_Scarecrow_01.fbx");
        generator.wellPrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Well_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Well_02.prefab",
            "Assets/Quaternius_Farm_Buildings/FBX/Well.fbx");
        generator.landmarkPrefabs = LoadAssets(
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_FarmMill_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_FarmMill_02.prefab",
            "Assets/Quaternius_Farm_Buildings/FBX/Windmill.fbx",
            "Assets/Quaternius_Farm_Buildings/FBX/WaterTower.fbx",
            "Assets/Quaternius_Farm_Buildings/FBX/TowerWindmill.fbx");
        generator.treePrefabs = LoadAssets(
            "Assets/LowPoly_ForestPack/Prefabs/Tree_Summer.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Tree_Summer_Small.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/CircleTree_Summer.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Tree_Autumn.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Tree_AutumnLight.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/CircleTree_Autumn.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/CircleTree_AutumnLight.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Dead_Tree.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_03.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_04.prefab",
            "Assets/SimpleNaturePack/Prefabs/Tree_05.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_02.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_03.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_04.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_05.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_06.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Tree_07.prefab",
            "Assets/Prefabs/Environment/Tree_RedPlum.fbx");
        generator.bushPrefabs = LoadAssets(
            "Assets/LowPoly_ForestPack/Prefabs/Bush.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Bush_Flat.prefab",
            "Assets/SimpleNaturePack/Prefabs/Bush_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Bush_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Bush_03.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Bush_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Bush_02.prefab");
        generator.rockPrefabs = LoadAssets(
            "Assets/LowPoly_ForestPack/Prefabs/Rock_Big.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Rock_Small.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_03.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_04.prefab",
            "Assets/SimpleNaturePack/Prefabs/Rock_05.prefab",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Rock_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Rock_02.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Rock_03.fbx");
        generator.grassPrefabs = LoadAssets(
            "Assets/LowPoly_ForestPack/Prefabs/Grass_01.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Grass_02.prefab",
            "Assets/LowPoly_ForestPack/Prefabs/Grass_03.prefab",
            "Assets/SimpleNaturePack/Prefabs/Grass_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Grass_02.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_01.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_02.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_03.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_04.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_05.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_06.prefab",
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_07.prefab",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Green_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Green_02.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Green_03.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Green_04.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Yellow_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Grass_Patch_Yellow_02.fbx");
        generator.flowerPrefabs = LoadAssets(
            "Assets/SimpleNaturePack/Prefabs/Flowers_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Flowers_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Mushroom_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Mushroom_02.prefab",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flower_Patch_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flower_Patch_02.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flower_Patch_03.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_01.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_02.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_03.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_04.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_05.fbx",
            "Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/Env_Flowers_06.fbx");
        generator.stumpPrefabs = LoadAssets(
            "Assets/LowPoly_ForestPack/Prefabs/Tree_Stump.prefab",
            "Assets/SimpleNaturePack/Prefabs/Stump_01.prefab",
            "Assets/Prefabs/Environment/Log_Cluster.fbx",
            "Assets/Prefabs/Environment/LogStorage.fbx");
        generator.forestDetailPrefabs = LoadAssets(
            "Assets/SimpleNaturePack/Prefabs/Mushroom_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Mushroom_02.prefab",
            "Assets/SimpleNaturePack/Prefabs/Flowers_01.prefab",
            "Assets/SimpleNaturePack/Prefabs/Flowers_02.prefab");
        Material animalMaterial = CreateAnimalVertexColorMaterial();
        generator.chickenAnimalPrefabs = CreateAnimalPrefabs(new[] { "Chicken", "Hen" }, animalMaterial);
        generator.cowAnimalPrefabs = CreateAnimalPrefabs(new[] { "HolsteinCow", "BrownCow", "WhiteCow" }, animalMaterial);
        generator.generateOnStart = false;

        generator.Generate();
        Object.DestroyImmediate(generatorObject);

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        AssetDatabase.SaveAssets();

        Debug.Log("Chicken Heist prototype scene created at " + ScenePath);
    }

    private static GameObject LoadAsset(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject[] LoadAssets(params string[] paths)
    {
        GameObject[] assets = new GameObject[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            assets[i] = LoadAsset(paths[i]);

        return assets;
    }

    private static Material CreateAnimalVertexColorMaterial()
    {
        EnsureAssetFolder(GeneratedAnimalFolder);
        AssetDatabase.ImportAsset(VertexColorShaderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(VertexColorShaderPath);
        if (shader == null)
        {
            Debug.LogError("Chicken Heist: vertex color shader was not found.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(VertexColorMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            material.name = "Everything Animals Vertex Color";
            AssetDatabase.CreateAsset(material, VertexColorMaterialPath);
        }
        else
            material.shader = shader;

        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject[] CreateAnimalPrefabs(string[] animalNames, Material material)
    {
        EnsureAssetFolder(GeneratedAnimalFolder);
        AssetDatabase.ImportAsset(AnimalLibraryPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GameObject library = AssetDatabase.LoadAssetAtPath<GameObject>(AnimalLibraryPath);
        if (library == null)
        {
            Debug.LogError("Chicken Heist: animal library FBX was not imported.");
            return new GameObject[0];
        }

        GameObject libraryInstance = PrefabUtility.InstantiatePrefab(library) as GameObject;
        if (libraryInstance == null)
            return new GameObject[0];

        GameObject[] prefabs = new GameObject[animalNames.Length];
        try
        {
            for (int i = 0; i < animalNames.Length; i++)
            {
                Transform source = FindChildRecursive(libraryInstance.transform, animalNames[i]);
                if (source == null)
                {
                    Debug.LogWarning("Chicken Heist: animal '" + animalNames[i] + "' was not found in the library.");
                    continue;
                }

                GameObject animal = Object.Instantiate(source.gameObject);
                animal.name = animalNames[i];
                animal.transform.position = Vector3.zero;
                animal.transform.rotation = source.rotation;
                animal.transform.localScale = source.lossyScale;
                ApplyMaterial(animal, material);

                string prefabPath = GeneratedAnimalFolder + "/" + animalNames[i] + ".prefab";
                prefabs[i] = PrefabUtility.SaveAsPrefabAsset(animal, prefabPath);
                Object.DestroyImmediate(animal);
            }
        }
        finally
        {
            Object.DestroyImmediate(libraryInstance);
        }

        AssetDatabase.SaveAssets();
        return prefabs;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        Transform partialMatch = null;
        Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hierarchy.Length; i++)
        {
            if (string.Equals(hierarchy[i].name, childName, System.StringComparison.OrdinalIgnoreCase))
                return hierarchy[i];

            if (partialMatch == null && hierarchy[i].name.IndexOf(childName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                partialMatch = hierarchy[i];
        }

        return partialMatch;
    }

    private static void ApplyMaterial(GameObject animal, Material material)
    {
        if (material == null)
            return;

        Renderer[] renderers = animal.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            int materialCount = Mathf.Max(1, renderers[i].sharedMaterials.Length);
            Material[] materials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = material;
            renderers[i].sharedMaterials = materials;
        }
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
