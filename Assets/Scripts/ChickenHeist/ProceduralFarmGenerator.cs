using System.Collections.Generic;
using UnityEngine;

public class ProceduralFarmGenerator : MonoBehaviour
{
    [Header("Seed")]
    public bool generateOnStart = true;
    public bool randomizeSeed = true;
    public int seed = 12345;

    [Header("Open World")]
    [Range(1, 5)] public int difficulty = 2;
    public int farmColumns = 3;
    public int farmRows = 2;
    public float lotWidth = 54f;
    public float lotDepth = 44f;
    public float roadWidth = 34f;
    [Range(0f, 0.42f)] public float farmScatter = 0.34f;
    public float worldBorderPadding = 90f;
    public float pathWidth = 5.5f;
    public float fenceReferenceLength = 3.6f;
    [Range(1f, 2f)] public float fenceOverlap = 1.65f;
    public int forestTreeCount = 340;
    public int forestBushCount = 150;
    public int forestRockCount = 55;
    public int wildGrassCount = 220;
    public int flowerCount = 160;
    public int stumpCount = 45;
    public int forestDetailCount = 140;
    public int forestClusterCount = 24;
    public int treesPerCluster = 18;
    public float forestClusterRadius = 24f;
    public int reliefCount = 48;
    public float reliefMinHeight = 2.5f;
    public float reliefMaxHeight = 8f;
    public float reliefMinRadius = 9f;
    public float reliefMaxRadius = 22f;
    public int terrainResolutionX = 80;
    public int terrainResolutionZ = 56;
    public float terrainMaxHeight = 7f;
    public float terrainEdgeHeight = 10f;
    public float terrainNoiseScale = 0.012f;
    public int chickenCount = 8;
    public int cowCount = 3;

    [Header("Optional Farm Pack Prefabs")]
    public GameObject housePrefab;
    public GameObject barnPrefab;
    public GameObject fencePrefab;
    public GameObject gatePrefab;
    public GameObject chickenHousePrefab;
    public GameObject troughPrefab;
    public GameObject siloPrefab;
    public GameObject[] housePrefabs;
    public GameObject[] barnPrefabs;
    public GameObject[] fencePrefabs;
    public GameObject[] gatePrefabs;
    public GameObject[] chickenCoopPrefabs;
    public GameObject[] troughPrefabs;
    public GameObject[] feederPrefabs;
    public GameObject[] siloPrefabs;
    public GameObject[] chickenAnimalPrefabs;
    public GameObject[] cowAnimalPrefabs;
    public GameObject[] ruralLandPrefabs;
    public GameObject[] ruralPropPrefabs;
    public GameObject[] hayPrefabs;
    public GameObject[] storagePropPrefabs;
    public GameObject[] fieldPropPrefabs;
    public GameObject[] wellPrefabs;
    public GameObject[] landmarkPrefabs;
    public GameObject[] treePrefabs;
    public GameObject[] bushPrefabs;
    public GameObject[] rockPrefabs;
    public GameObject[] grassPrefabs;
    public GameObject[] flowerPrefabs;
    public GameObject[] stumpPrefabs;
    public GameObject[] forestDetailPrefabs;

    private readonly List<FarmerSleepSystem> farmers = new List<FarmerSleepSystem>();
    private readonly List<Vector4> dirtPathSegments = new List<Vector4>();
    private int primaryDirtPathSegmentCount;
    private Material groundMaterial;
    private Material roadMaterial;
    private Material fenceMaterial;
    private Material chickenMaterial;
    private Material cowMaterial;
    private Material beakMaterial;
    private Material legMaterial;
    private Material hornMaterial;
    private Material hoofMaterial;
    private Material dangerMaterial;
    private Material buildingMaterial;
    private Material natureLeafMaterial;
    private Material natureAutumnLeafMaterial;
    private Material natureBarkMaterial;
    private Material natureGrassMaterial;
    private Material natureFlowerMaterial;
    private Material natureRockMaterial;
    private Material houseFallbackMaterial;
    private Material barnFallbackMaterial;
    private Material woodPropFallbackMaterial;
    private Material cropFallbackMaterial;
    private Material reliefMaterial;
    private Material reliefDarkMaterial;
    private Material terrainMidMaterial;
    private Material terrainHighMaterial;
    private Material coopBarMaterial;
    private Material coopWoodMaterial;
    private Material coopMeshMaterial;
    private Material coopRoofMaterial;
    private float terrainSeedX;
    private float terrainSeedZ;

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    public void Generate()
    {
        if (randomizeSeed)
            seed = Random.Range(1000, 999999);

        Random.InitState(seed);
        terrainSeedX = seed * 0.0137f;
        terrainSeedZ = seed * 0.0211f;
        BuildDirtPathLayout();
        farmers.Clear();
        CreateMaterials();
        CreateLighting();
        CreateGroundAndRoads();

        HeistGameManager game = CreateGameManager();
        Transform player = CreatePlayer(GetPlayerStartPosition());
        game.player = player;
        game.backpack = player.GetComponent<BackpackInventory>();

        CreateExtractionZone(GetPlayerStartPosition() + new Vector3(-8f, -1.15f, -4f));

        for (int row = 0; row < farmRows; row++)
        {
            for (int column = 0; column < farmColumns; column++)
            {
                int farmIndex = row * farmColumns + column;
                CreateFarmLot(farmIndex, GetLotRect(column, row), player);
            }
        }

        CreateForest();
        AttachCameraToPlayer(player);
        game.farmerSleep = farmers.Count > 0 ? farmers[0] : null;
        game.farmers = farmers.ToArray();
        game.targetChickens = Mathf.Min(chickenCount * farmColumns * farmRows, 10 + difficulty * 2);
        game.statusMessage = "Mundo aberto: roube galinhas de varias fazendas e volte para a caminhonete.";
    }

    private void CreateFarmLot(int farmIndex, Rect lot, Transform player)
    {
        GameObject root = new GameObject("Fazenda " + (farmIndex + 1).ToString("00"));
        GameObject selectedHouse = ChooseAsset(housePrefabs, housePrefab, farmIndex);
        GameObject selectedBarn = ChooseAsset(barnPrefabs, barnPrefab, farmIndex);
        GameObject selectedSilo = ChooseAsset(siloPrefabs, siloPrefab, farmIndex);
        GameObject selectedCoop = ChooseAsset(chickenCoopPrefabs, chickenHousePrefab, farmIndex);

        CreateLotFence(lot, root.transform, farmIndex);

        Vector3 housePosition = LotPoint(lot, 0.20f, 0.79f, 0.5f);
        Vector3 barnPosition = LotPoint(lot, 0.66f, 0.79f, 0.5f);
        Vector3 siloPosition = LotPoint(lot, 0.88f, 0.56f, 0.5f);

        CreateBuilding("Casa - Fazenda " + (farmIndex + 1), selectedHouse, housePosition, new Vector3(1.7f, 1.35f, 1.7f), new Vector3(7f, 4.5f, 6f), root.transform);
        CreateBuilding("Celeiro - Fazenda " + (farmIndex + 1), selectedBarn, barnPosition, new Vector3(2f, 1.55f, 2f), new Vector3(9f, 5.5f, 7f), root.transform);

        if (selectedSilo != null)
        {
            GameObject silo = Instantiate(selectedSilo, siloPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), root.transform);
            silo.name = "Silo - Fazenda " + (farmIndex + 1);
            ApplyAssetMaterials(silo, buildingMaterial);
        }
        else
            CreateColoredCube("Silo", siloPosition + Vector3.up * 2.5f, new Vector3(3f, 5f, 3f), buildingMaterial, root.transform);

        Rect chickenPen = InnerRect(lot, 0.08f, 0.08f, 0.34f, 0.30f);
        Rect cowPen = InnerRect(lot, 0.58f, 0.08f, 0.34f, 0.30f);
        Rect fieldRect = InnerRect(lot, 0.10f, 0.43f, 0.80f, 0.22f);

        // As galinhas ficam somente no galinheiro fechado; nao criar o cercado antigo ao redor.
        CreatePen("Cercado das Vacas - Fazenda " + (farmIndex + 1), cowPen, false, root.transform, farmIndex + 1);
        CreateCropPen("Cercado da Plantacao - Fazenda " + (farmIndex + 1), fieldRect, root.transform, farmIndex + 2);
        Rect enclosedCoop = InnerRect(chickenPen, 0.10f, 0.10f, 0.80f, 0.80f);
        ChickenCoopLockpick lockpick = CreateChickenCoop(enclosedCoop, root.transform, selectedCoop, farmIndex);
        List<InteractableChicken> coopChickens = SpawnChickens(enclosedCoop, root.transform);
        if (lockpick != null && coopChickens.Count > 0)
            lockpick.scareChicken = coopChickens[0].transform;
        SpawnCows(cowPen, root.transform);
        SpawnFarmLand(fieldRect, farmIndex, root.transform);
        SpawnFarmClutter(lot, farmIndex, housePosition, barnPosition, root.transform);
        SpawnRuralDetails(lot, farmIndex, root.transform);
        CreateFarmLighting(housePosition, barnPosition, farmIndex, root.transform);

        List<Transform> patrolPoints = CreatePatrolPoints(lot, housePosition, chickenPen, cowPen, root.transform);
        Transform farmer = CreateFarmer(housePosition + new Vector3(0f, 1f, -3f), player, farmIndex);
        farmer.SetParent(root.transform);
        farmer.GetComponent<FarmerStateMachine>().patrolPoints = patrolPoints.ToArray();
        farmers.Add(farmer.GetComponent<FarmerSleepSystem>());

        SpawnSecurity(player, chickenPen, cowPen, housePosition, farmIndex, root.transform);
    }

    private HeistGameManager CreateGameManager()
    {
        GameObject gameObject = new GameObject("HeistGameManager");
        gameObject.AddComponent<ChickenHeistHUD>();
        return gameObject.AddComponent<HeistGameManager>();
    }

    private Transform CreatePlayer(Vector3 position)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = position;
        DestroySafe(player.GetComponent<CapsuleCollider>());

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 1f, 0f);

        player.AddComponent<PlayerMovement>();
        BackpackInventory backpack = player.AddComponent<BackpackInventory>();
        backpack.capacity = 6 + difficulty;

        return player.transform;
    }

    private Transform CreateFarmer(Vector3 position, Transform player, int farmIndex)
    {
        GameObject farmer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        farmer.name = "Fazendeiro Sonolento " + (farmIndex + 1).ToString("00");
        farmer.transform.position = position;
        farmer.GetComponent<Renderer>().material = dangerMaterial;
        DestroySafe(farmer.GetComponent<CapsuleCollider>());

        CharacterController controller = farmer.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);

        FarmerSleepSystem sleep = farmer.AddComponent<FarmerSleepSystem>();
        sleep.player = player;
        sleep.startingSleep = 6f + difficulty * 2.5f + farmIndex;
        sleep.lighterSleepMultiplier = 1f + (difficulty - 1) * 0.16f + farmIndex * 0.03f;
        sleep.hearingRange = 42f;

        FarmerStateMachine stateMachine = farmer.AddComponent<FarmerStateMachine>();
        stateMachine.sleepSystem = sleep;
        stateMachine.player = player;

        return farmer.transform;
    }

    private void CreateLighting()
    {
        RenderSettings.ambientLight = new Color(0.075f, 0.105f, 0.16f);
        RenderSettings.ambientIntensity = 0.72f;
        RenderSettings.reflectionIntensity = 0.42f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.075f, 0.085f);
        RenderSettings.fogDensity = 0.0045f;

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.name = "Chicken Heist Night Sky";
            sky.SetColor("_SkyTint", new Color(0.08f, 0.13f, 0.23f));
            sky.SetColor("_GroundColor", new Color(0.025f, 0.045f, 0.045f));
            sky.SetFloat("_AtmosphereThickness", 0.72f);
            sky.SetFloat("_Exposure", 0.48f);
            RenderSettings.skybox = sky;
        }

        GameObject moon = new GameObject("Moon Light");
        Light light = moon.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.49f, 0.66f, 1f);
        light.intensity = 0.68f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
        light.shadowBias = 0.06f;
        moon.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
    }

    private void CreateFarmLighting(Vector3 housePosition, Vector3 barnPosition, int farmIndex, Transform parent)
    {
        Color warmLight = farmIndex % 3 == 0
            ? new Color(1f, 0.52f, 0.20f)
            : new Color(1f, 0.67f, 0.32f);

        CreateWarmLight("Luz da Varanda", housePosition + new Vector3(0f, 2.8f, -3.2f), warmLight, 12f, 2.2f, parent);
        CreateWarmLight("Luz do Celeiro", barnPosition + new Vector3(0f, 3.2f, -3.8f), warmLight, 15f, 1.75f, parent);
    }

    private void CreateWarmLight(string objectName, Vector3 position, Color color, float range, float intensity, Transform parent)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = position;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.45f;
        light.shadowBias = 0.08f;
    }

    private void CreateGroundAndRoads()
    {
        float width = GetWorldMaxX() - GetWorldMinX();
        float depth = GetWorldMaxZ() - GetWorldMinZ();

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Vale Rural Noturno";
        ground.transform.position = new Vector3(0f, -0.55f, 0f);
        ground.transform.localScale = new Vector3(width, 1f, depth);
        ground.GetComponent<Renderer>().material = groundMaterial;

        CreateRollingTerrainMesh(width, depth);
        CreateDirtPathNetwork();
    }

    private void CreateRollingTerrainMesh(float width, float depth)
    {
        int columns = Mathf.Max(16, terrainResolutionX);
        int rows = Mathf.Max(16, terrainResolutionZ);
        Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int z = 0; z <= rows; z++)
        {
            for (int x = 0; x <= columns; x++)
            {
                float normalizedX = x / (float)columns;
                float normalizedZ = z / (float)rows;
                float worldX = Mathf.Lerp(-width * 0.5f, width * 0.5f, normalizedX);
                float worldZ = Mathf.Lerp(-depth * 0.5f, depth * 0.5f, normalizedZ);
                int index = z * (columns + 1) + x;
                vertices[index] = new Vector3(worldX, SampleTerrainHeight(worldX, worldZ), worldZ);
                uvs[index] = new Vector2(normalizedX * 8f, normalizedZ * 8f);
            }
        }

        List<int> lowTriangles = new List<int>();
        List<int> midTriangles = new List<int>();
        List<int> highTriangles = new List<int>();
        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                int bottomLeft = z * (columns + 1) + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + columns + 1;
                int topRight = topLeft + 1;
                float averageHeight = (vertices[bottomLeft].y + vertices[bottomRight].y + vertices[topLeft].y + vertices[topRight].y) * 0.25f;
                List<int> target = averageHeight > terrainMaxHeight * 0.68f ? highTriangles : averageHeight > terrainMaxHeight * 0.28f ? midTriangles : lowTriangles;

                target.Add(bottomLeft);
                target.Add(topLeft);
                target.Add(bottomRight);
                target.Add(bottomRight);
                target.Add(topLeft);
                target.Add(topRight);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Terreno Rural Continuo";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(lowTriangles, 0);
        mesh.SetTriangles(midTriangles, 1);
        mesh.SetTriangles(highTriangles, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject terrain = new GameObject("Terreno Ondulado Low Poly");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = terrain.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { groundMaterial, terrainMidMaterial, terrainHighMaterial };
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private float SampleTerrainHeight(float worldX, float worldZ)
    {
        float broadNoise = Mathf.PerlinNoise(terrainSeedX + worldX * terrainNoiseScale, terrainSeedZ + worldZ * terrainNoiseScale);
        float detailNoise = Mathf.PerlinNoise(terrainSeedZ + worldX * terrainNoiseScale * 2.35f, terrainSeedX + worldZ * terrainNoiseScale * 2.35f);
        float ridgeNoise = 1f - Mathf.Abs(Mathf.PerlinNoise(terrainSeedX * 0.5f + worldX * terrainNoiseScale * 0.65f, terrainSeedZ * 0.5f + worldZ * terrainNoiseScale * 0.65f) * 2f - 1f);
        float rolling = broadNoise * 0.62f + detailNoise * 0.18f + ridgeNoise * 0.20f;
        float height = Mathf.Max(0f, rolling - 0.30f) * terrainMaxHeight;

        float normalizedEdgeX = Mathf.Abs(worldX) / Mathf.Max(1f, GetWorldMaxX() + 15f);
        float normalizedEdgeZ = Mathf.Abs(worldZ) / Mathf.Max(1f, GetWorldMaxZ() + 15f);
        float edge = Mathf.Max(normalizedEdgeX, normalizedEdgeZ);
        height += Mathf.SmoothStep(0f, terrainEdgeHeight, Mathf.InverseLerp(0.72f, 1f, edge));

        float flatten = GetTerrainFlattenInfluence(worldX, worldZ);
        return Mathf.Lerp(height, 0f, flatten);
    }

    private float GetTerrainFlattenInfluence(float worldX, float worldZ)
    {
        Vector2 point = new Vector2(worldX, worldZ);
        float influence = 0f;

        for (int row = 0; row < farmRows; row++)
        {
            for (int column = 0; column < farmColumns; column++)
            {
                Rect lot = GetLotRect(column, row);
                float dx = Mathf.Max(lot.xMin - worldX, 0f, worldX - lot.xMax);
                float dz = Mathf.Max(lot.yMin - worldZ, 0f, worldZ - lot.yMax);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                influence = Mathf.Max(influence, 1f - Mathf.SmoothStep(0f, 12f, distance));
            }
        }

        float pathDistance = DistanceToDirtPath(point);
        float pathInfluence = 1f - Mathf.SmoothStep(pathWidth * 0.75f, pathWidth * 2.2f, pathDistance);
        return Mathf.Clamp01(Mathf.Max(influence, pathInfluence));
    }

    private float DistanceToDirtPath(Vector2 point)
    {
        float minimum = float.MaxValue;

        for (int i = 0; i < dirtPathSegments.Count; i++)
        {
            Vector4 segment = dirtPathSegments[i];
            minimum = Mathf.Min(minimum, DistancePointToSegment(point, new Vector2(segment.x, segment.y), new Vector2(segment.z, segment.w)));
        }

        return minimum;
    }

    private void BuildDirtPathLayout()
    {
        dirtPathSegments.Clear();
        List<Vector2> connectedEntrances = new List<Vector2>();
        connectedEntrances.Add(new Vector2(GetPlayerStartPosition().x, GetPlayerStartPosition().z));

        int farmCount = farmColumns * farmRows;
        for (int farmIndex = 0; farmIndex < farmCount; farmIndex++)
        {
            int row = farmIndex / farmColumns;
            int column = farmIndex % farmColumns;
            Rect lot = GetLotRect(column, row);
            Vector2 destination = new Vector2(lot.center.x, lot.yMin + 2.5f);
            Vector2 source = connectedEntrances[0];
            float nearestDistance = Vector2.SqrMagnitude(destination - source);

            for (int i = 1; i < connectedEntrances.Count; i++)
            {
                float distance = Vector2.SqrMagnitude(destination - connectedEntrances[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    source = connectedEntrances[i];
                }
            }

            Vector2 direction = (destination - source).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float bendDirection = Hash01(farmIndex * 43 + 17) < 0.5f ? -1f : 1f;
            float bend = Mathf.Min(roadWidth * 0.26f, Vector2.Distance(source, destination) * 0.18f);
            Vector2 midpoint = Vector2.Lerp(source, destination, 0.48f) + perpendicular * bend * bendDirection;

            AddDirtPathSegment(source, midpoint);
            AddDirtPathSegment(midpoint, destination);
            connectedEntrances.Add(destination);
        }

        primaryDirtPathSegmentCount = dirtPathSegments.Count;
        AddFarmShortcuts();
        AddForestAccessTrails();
    }

    private void AddFarmShortcuts()
    {
        for (int row = 0; row < farmRows; row++)
        {
            for (int column = row % 2; column < farmColumns - 1; column += 2)
            {
                Rect firstLot = GetLotRect(column, row);
                Rect secondLot = GetLotRect(column + 1, row);
                Vector2 start = new Vector2(firstLot.center.x, firstLot.yMin - pathWidth);
                Vector2 end = new Vector2(secondLot.center.x, secondLot.yMin - pathWidth);
                AddBentDirtPath(start, end, row * 67 + column * 19 + 301, 0.13f);
            }
        }

        for (int column = 0; column < farmColumns; column += 2)
        {
            for (int row = 0; row < farmRows - 1; row++)
            {
                Rect firstLot = GetLotRect(column, row);
                Rect secondLot = GetLotRect(column, row + 1);
                Vector2 start = new Vector2(firstLot.xMax + pathWidth, firstLot.center.y);
                Vector2 end = new Vector2(secondLot.xMax + pathWidth, secondLot.center.y);
                AddBentDirtPath(start, end, column * 83 + row * 29 + 509, 0.16f);
            }
        }
    }

    private void AddForestAccessTrails()
    {
        int farmCount = farmColumns * farmRows;
        for (int farmIndex = 0; farmIndex < farmCount; farmIndex += 2)
        {
            int row = farmIndex / farmColumns;
            int column = farmIndex % farmColumns;
            Rect lot = GetLotRect(column, row);
            Vector2 start = new Vector2(lot.center.x, lot.yMin - pathWidth);
            Vector2 end;

            switch ((farmIndex / 2) % 4)
            {
                case 0:
                    end = new Vector2(GetWorldMinX() + 18f, Mathf.Lerp(GetWorldMinZ() + 35f, GetWorldMaxZ() - 35f, Hash01(farmIndex + 701)));
                    break;
                case 1:
                    end = new Vector2(GetWorldMaxX() - 18f, Mathf.Lerp(GetWorldMinZ() + 35f, GetWorldMaxZ() - 35f, Hash01(farmIndex + 719)));
                    break;
                case 2:
                    end = new Vector2(Mathf.Lerp(GetWorldMinX() + 35f, GetWorldMaxX() - 35f, Hash01(farmIndex + 733)), GetWorldMinZ() + 18f);
                    break;
                default:
                    end = new Vector2(Mathf.Lerp(GetWorldMinX() + 35f, GetWorldMaxX() - 35f, Hash01(farmIndex + 751)), GetWorldMaxZ() - 18f);
                    break;
            }

            AddBentDirtPath(start, end, farmIndex * 37 + 811, 0.20f);
        }
    }

    private void AddBentDirtPath(Vector2 start, Vector2 end, int hashKey, float bendRatio)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float sign = Hash01(hashKey) < 0.5f ? -1f : 1f;
        float bend = Mathf.Min(roadWidth * 0.34f, Vector2.Distance(start, end) * bendRatio);
        Vector2 midpoint = Vector2.Lerp(start, end, 0.5f) + perpendicular * bend * sign;
        AddDirtPathSegment(start, midpoint);
        AddDirtPathSegment(midpoint, end);
    }

    private void AddDirtPathSegment(Vector2 start, Vector2 end)
    {
        dirtPathSegments.Add(new Vector4(start.x, start.y, end.x, end.y));
    }

    private void CreateDirtPathNetwork()
    {
        for (int i = 0; i < dirtPathSegments.Count; i++)
        {
            Vector4 segment = dirtPathSegments[i];
            Vector3 start = new Vector3(segment.x, -0.018f, segment.y);
            Vector3 end = new Vector3(segment.z, -0.018f, segment.w);
            bool isPrimary = i < primaryDirtPathSegmentCount;
            float width = isPrimary ? pathWidth * (i < 2 ? 1.12f : 1f) : pathWidth * 0.68f;
            string pathName = isPrimary ? "Caminho Rural Principal " : "Trilha de Terra Secundaria ";
            CreateDirtPathSegment(pathName + (i + 1).ToString("00"), start, end, width);
        }
    }

    private void CreateReliefLayer()
    {
        GameObject root = new GameObject("Relevos e Morros Low Poly");
        int created = 0;
        int attempts = Mathf.Max(reliefCount * 8, 80);

        for (int attempt = 0; attempt < attempts && created < reliefCount; attempt++)
        {
            float radius = Random.Range(reliefMinRadius, reliefMaxRadius);
            Vector3 position = RandomPoint(
                GetWorldMinX() + radius,
                GetWorldMaxX() - radius,
                GetWorldMinZ() + radius,
                GetWorldMaxZ() - radius);

            if (!IsReliefPlacementAllowed(position, radius * 0.45f))
                continue;

            float height = Random.Range(reliefMinHeight, reliefMaxHeight);
            CreateLowPolyRelief("Morro Low Poly " + (created + 1).ToString("00"), position, radius, height, root.transform);
            SpawnReliefRocks(position, radius, root.transform, created);
            created++;
        }
    }

    private bool IsReliefPlacementAllowed(Vector3 position, float margin)
    {
        for (int row = 0; row < farmRows; row++)
        {
            for (int column = 0; column < farmColumns; column++)
            {
                if (ExpandRect(GetLotRect(column, row), margin).Contains(new Vector2(position.x, position.z)))
                    return false;
            }
        }

        return !IsNearDirtPath(position, margin + pathWidth * 0.7f);
    }

    private void CreateLowPolyRelief(string name, Vector3 position, float radius, float height, Transform parent)
    {
        GameObject hill = new GameObject(name);
        hill.transform.SetParent(parent);
        hill.transform.position = position + Vector3.down * 0.05f;
        hill.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        const int sides = 8;
        const int rings = 5;
        Vector3[] vertices = new Vector3[sides * rings + 1];
        float[] ringRadii = { 1f, 0.94f, 0.76f, 0.48f, 0.20f };
        float[] ringHeights = { 0f, 0.10f, 0.28f, 0.52f, 0.68f };

        for (int ring = 0; ring < rings; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float variation = Random.Range(0.88f, 1.12f);
                vertices[ring * sides + side] = new Vector3(
                    Mathf.Cos(angle) * radius * ringRadii[ring] * variation,
                    height * ringHeights[ring] + Random.Range(-0.12f, 0.12f),
                    Mathf.Sin(angle) * radius * ringRadii[ring] * variation);
            }
        }

        vertices[vertices.Length - 1] = Vector3.up * (height * 0.72f);
        List<int> triangles = new List<int>();
        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int current = ring * sides + side;
                int currentNext = ring * sides + next;
                int upper = (ring + 1) * sides + side;
                int upperNext = (ring + 1) * sides + next;
                triangles.Add(current);
                triangles.Add(upper);
                triangles.Add(currentNext);
                triangles.Add(currentNext);
                triangles.Add(upper);
                triangles.Add(upperNext);
            }
        }

        int top = vertices.Length - 1;
        int lastRing = (rings - 1) * sides;
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            triangles.Add(lastRing + side);
            triangles.Add(top);
            triangles.Add(lastRing + next);
        }

        Mesh mesh = new Mesh();
        mesh.name = name + " Mesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        hill.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = hill.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Random.value > 0.5f ? reliefMaterial : reliefDarkMaterial;
        hill.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void SpawnReliefRocks(Vector3 center, float radius, Transform parent, int reliefIndex)
    {
        int rockCount = Random.Range(1, 4);
        for (int i = 0; i < rockCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius * 0.8f;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
            GameObject rock = InstantiateNature(rockPrefabs, "Pedra do Morro", position, parent);
            float scale = Random.Range(0.8f, 2f);
            rock.transform.localScale = Vector3.one * scale;
            rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            ApplyNatureMaterials(rock, "Pedra do Morro " + reliefIndex);
            PlaceAssetOnGround(rock);
        }
    }

    private void CreateDirtPathSegment(string name, Vector3 start, Vector3 end, float width)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length <= 0.1f)
            return;

        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = name;
        path.transform.position = (start + end) * 0.5f;
        path.transform.rotation = Quaternion.LookRotation(direction.normalized);
        path.transform.localScale = new Vector3(width, 0.08f, length);
        path.GetComponent<Renderer>().material = roadMaterial;
    }

    private void CreateForest()
    {
        GameObject root = new GameObject("Floresta Low Poly");
        CreateDenseForestClusters(root.transform);

        for (int i = 0; i < forestTreeCount; i++)
            SpawnNatureObject("Arvore Low Poly", treePrefabs, root.transform, 1.4f, 2.7f, true);

        for (int i = 0; i < forestBushCount; i++)
            SpawnNatureObject("Arbusto Low Poly", bushPrefabs, root.transform, 0.8f, 1.6f, false);

        for (int i = 0; i < forestRockCount; i++)
            SpawnNatureObject("Pedra Low Poly", rockPrefabs, root.transform, 0.8f, 1.9f, false);

        for (int i = 0; i < wildGrassCount; i++)
            SpawnNatureObject("Capim Low Poly", grassPrefabs, root.transform, 0.7f, 1.5f, false);

        for (int i = 0; i < flowerCount; i++)
            SpawnNatureObject("Flores Low Poly", flowerPrefabs, root.transform, 0.55f, 1.15f, false);

        for (int i = 0; i < stumpCount; i++)
            SpawnNatureObject("Tocos Low Poly", stumpPrefabs, root.transform, 0.7f, 1.25f, true);

        for (int i = 0; i < forestDetailCount; i++)
            SpawnNatureObject("Detalhes da Floresta", forestDetailPrefabs, root.transform, 0.45f, 1.05f, false);
    }

    private void CreateDenseForestClusters(Transform parent)
    {
        for (int clusterIndex = 0; clusterIndex < forestClusterCount; clusterIndex++)
        {
            Vector3 center = Vector3.zero;
            bool foundCenter = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                center = RandomPoint(GetWorldMinX() + 15f, GetWorldMaxX() - 15f, GetWorldMinZ() + 15f, GetWorldMaxZ() - 15f);
                if (IsForestPlacementAllowed(center, 10f))
                {
                    foundCenter = true;
                    break;
                }
            }

            if (!foundCenter)
                continue;

            GameObject clusterRoot = new GameObject("Nucleo de Mata " + (clusterIndex + 1).ToString("00"));
            clusterRoot.transform.SetParent(parent);

            for (int treeIndex = 0; treeIndex < treesPerCluster; treeIndex++)
            {
                Vector2 offset = Random.insideUnitCircle * forestClusterRadius;
                Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
                TrySpawnNatureObjectAt("Arvore da Mata Fechada", treePrefabs, clusterRoot.transform, position, 1.35f, 2.85f, 5f);
            }

            int bushCount = Mathf.Max(5, treesPerCluster / 2);
            for (int bushIndex = 0; bushIndex < bushCount; bushIndex++)
            {
                Vector2 offset = Random.insideUnitCircle * forestClusterRadius * 0.9f;
                Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
                TrySpawnNatureObjectAt("Arbusto da Mata Fechada", bushPrefabs, clusterRoot.transform, position, 0.8f, 1.7f, 3f);
            }

            int rockCount = Mathf.Max(4, treesPerCluster / 3);
            for (int rockIndex = 0; rockIndex < rockCount; rockIndex++)
            {
                Vector2 offset = Random.insideUnitCircle * forestClusterRadius;
                Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
                TrySpawnNatureObjectAt("Pedra da Mata Fechada", rockPrefabs, clusterRoot.transform, position, 0.75f, 2.25f, 2.5f);
            }
        }
    }

    private void SpawnNatureObject(string name, GameObject[] prefabs, Transform parent, float minScale, float maxScale, bool keepAwayFromFarms)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector3 position = RandomPoint(GetWorldMinX() + 3f, GetWorldMaxX() - 3f, GetWorldMinZ() + 3f, GetWorldMaxZ() - 3f);
            if (TrySpawnNatureObjectAt(name, prefabs, parent, position, minScale, maxScale, keepAwayFromFarms ? 6f : 2.5f))
                return;
        }
    }

    private bool TrySpawnNatureObjectAt(string name, GameObject[] prefabs, Transform parent, Vector3 position, float minScale, float maxScale, float placementMargin)
    {
        if (!IsForestPlacementAllowed(position, placementMargin))
            return false;

        position.y = SampleTerrainHeight(position.x, position.z);
        GameObject instance = InstantiateNature(prefabs, name, position, parent);
        float scale = Random.Range(minScale, maxScale);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, Vector3.one * scale);
        instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        ApplyNatureMaterials(instance, name + " " + instance.name);
        PlaceAssetOnGround(instance, position.y);
        MarkNatureStatic(instance);
        return true;
    }

    private void MarkNatureStatic(GameObject instance)
    {
        Transform[] hierarchy = instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hierarchy.Length; i++)
            hierarchy[i].gameObject.isStatic = true;
    }

    private GameObject InstantiateNature(GameObject[] prefabs, string fallbackName, Vector3 position, Transform parent)
    {
        if (prefabs != null && prefabs.Length > 0)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab != null)
                return Instantiate(prefab, position, Quaternion.identity, parent);
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallback.name = fallbackName;
        fallback.transform.SetParent(parent);
        fallback.transform.position = position;
        fallback.GetComponent<Renderer>().material = fenceMaterial;
        ApplyNatureMaterials(fallback, fallbackName);
        return fallback;
    }

    private void ApplyNatureMaterials(GameObject instance, string assetName)
    {
        string lowerName = assetName.ToLowerInvariant();
        bool isTree = lowerName.Contains("tree") || lowerName.Contains("arvore");
        bool isBush = lowerName.Contains("bush") || lowerName.Contains("arbusto");
        bool isRock = lowerName.Contains("rock") || lowerName.Contains("pedra");
        bool isGrass = lowerName.Contains("grass") || lowerName.Contains("capim") || lowerName.Contains("plant");
        bool isFlower = lowerName.Contains("flower") || lowerName.Contains("flores") || lowerName.Contains("mushroom");
        bool isStump = lowerName.Contains("stump") || lowerName.Contains("toco") || lowerName.Contains("dead");

        if (!isTree && !isBush && !isRock && !isGrass && !isFlower && !isStump)
            return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                materials = new Material[1];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (isTree)
                {
                    Material leafMaterial = lowerName.Contains("autumn") ? natureAutumnLeafMaterial : natureLeafMaterial;
                    materials[materialIndex] = materials.Length > 1 && materialIndex > 0 ? natureBarkMaterial : leafMaterial;
                }
                else if (isBush)
                    materials[materialIndex] = natureLeafMaterial;
                else if (isRock)
                    materials[materialIndex] = natureRockMaterial;
                else if (isFlower)
                    materials[materialIndex] = natureFlowerMaterial;
                else if (isStump)
                    materials[materialIndex] = natureBarkMaterial;
                else
                    materials[materialIndex] = natureGrassMaterial;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private bool IsForestPlacementAllowed(Vector3 position, float margin)
    {
        for (int row = 0; row < farmRows; row++)
        {
            for (int column = 0; column < farmColumns; column++)
            {
                Rect lot = ExpandRect(GetLotRect(column, row), margin);
                if (lot.Contains(new Vector2(position.x, position.z)))
                    return false;
            }
        }

        return !IsNearDirtPath(position, pathWidth * 1.25f);
    }

    private bool IsNearDirtPath(Vector3 position, float margin)
    {
        Vector2 point = new Vector2(position.x, position.z);
        for (int i = 0; i < dirtPathSegments.Count; i++)
        {
            Vector4 segment = dirtPathSegments[i];
            if (DistancePointToSegment(point, new Vector2(segment.x, segment.y), new Vector2(segment.z, segment.w)) < margin)
                return true;
        }

        return false;
    }

    private float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    private Rect ExpandRect(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private void CreateLotFence(Rect lot, Transform parent, int farmIndex)
    {
        float gateWidth = 5f;
        Vector3 frontLeft = new Vector3(lot.xMin, 0f, lot.yMin);
        Vector3 frontGateLeft = new Vector3(lot.center.x - gateWidth * 0.5f, 0f, lot.yMin);
        Vector3 frontGateRight = new Vector3(lot.center.x + gateWidth * 0.5f, 0f, lot.yMin);

        CreateFenceLine(frontLeft, frontGateLeft, parent, farmIndex);
        CreateFenceLine(frontGateRight, new Vector3(lot.xMax, 0f, lot.yMin), parent, farmIndex);
        CreateFenceLine(new Vector3(lot.xMin, 0f, lot.yMax), new Vector3(lot.xMax, 0f, lot.yMax), parent, farmIndex);
        CreateFenceLine(new Vector3(lot.xMin, 0f, lot.yMin), new Vector3(lot.xMin, 0f, lot.yMax), parent, farmIndex);
        CreateFenceLine(new Vector3(lot.xMax, 0f, lot.yMin), new Vector3(lot.xMax, 0f, lot.yMax), parent, farmIndex);

        GameObject selectedGate = ChooseAsset(gatePrefabs, gatePrefab, farmIndex);
        if (selectedGate != null)
            CreateGate(selectedGate, new Vector3(lot.center.x, 0f, lot.yMin), gateWidth, parent);
        else
            CreateColoredCube("Portao do Lote", new Vector3(lot.center.x, 0.75f, lot.yMin), new Vector3(4f, 1.5f, 0.22f), buildingMaterial, parent);
    }

    private void CreateBuilding(string name, GameObject prefab, Vector3 position, Vector3 prefabScale, Vector3 fallbackScale, Transform parent)
    {
        GameObject building;
        if (prefab != null)
        {
            building = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f), parent);
            building.transform.localScale = prefabScale;
            ApplyAssetMaterials(building, name.Contains("Celeiro") ? barnFallbackMaterial : houseFallbackMaterial);
        }
        else
        {
            building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.SetParent(parent);
            building.transform.position = position + Vector3.up * (fallbackScale.y * 0.5f);
            building.transform.localScale = fallbackScale;
            building.GetComponent<Renderer>().material = buildingMaterial;
        }

        building.name = name;
    }

    private void CreatePen(string name, Rect rect, bool chickenPen, Transform parent, int farmIndex)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);

        float gateWidth = chickenPen ? 2.8f : 3.4f;
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMin), new Vector3(rect.center.x - gateWidth * 0.5f, 0f, rect.yMin), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.center.x + gateWidth * 0.5f, 0f, rect.yMin), new Vector3(rect.xMax, 0f, rect.yMin), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMax), new Vector3(rect.xMax, 0f, rect.yMax), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMin), new Vector3(rect.xMin, 0f, rect.yMax), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMax, 0f, rect.yMin), new Vector3(rect.xMax, 0f, rect.yMax), root.transform, farmIndex);

        Vector3 gatePosition = new Vector3(rect.center.x, 0f, rect.yMin);
        GameObject selectedGate = ChooseAsset(gatePrefabs, gatePrefab, farmIndex);
        if (selectedGate != null)
            CreateGate(selectedGate, gatePosition, gateWidth, root.transform);
        else
            CreateColoredCube("Portao", gatePosition + Vector3.up * 0.8f, new Vector3(2.4f, 1.6f, 0.2f), buildingMaterial, root.transform);

        GameObject selectedTrough = ChooseAsset(troughPrefabs, troughPrefab, farmIndex);
        if (!chickenPen && selectedTrough != null)
            Instantiate(selectedTrough, new Vector3(rect.center.x, 0f, rect.center.y), Quaternion.identity, root.transform);

        GameObject selectedFeeder = ChooseAsset(feederPrefabs, null, farmIndex + (chickenPen ? 1 : 3));
        if (selectedFeeder != null)
        {
            Vector3 feederPosition = new Vector3(rect.center.x + (chickenPen ? -1.2f : 1.2f), 0f, rect.center.y - 1.5f);
            Instantiate(selectedFeeder, feederPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), root.transform);
        }
    }

    private void CreateCropPen(string name, Rect rect, Transform parent, int farmIndex)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);

        float gateWidth = 4f;
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMin), new Vector3(rect.center.x - gateWidth * 0.5f, 0f, rect.yMin), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.center.x + gateWidth * 0.5f, 0f, rect.yMin), new Vector3(rect.xMax, 0f, rect.yMin), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMax), new Vector3(rect.xMax, 0f, rect.yMax), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMin, 0f, rect.yMin), new Vector3(rect.xMin, 0f, rect.yMax), root.transform, farmIndex);
        CreateFenceLine(new Vector3(rect.xMax, 0f, rect.yMin), new Vector3(rect.xMax, 0f, rect.yMax), root.transform, farmIndex);

        GameObject selectedGate = ChooseAsset(gatePrefabs, gatePrefab, farmIndex);
        if (selectedGate != null)
            CreateGate(selectedGate, new Vector3(rect.center.x, 0f, rect.yMin), gateWidth, root.transform);
    }

    private void CreateFenceLine(Vector3 start, Vector3 end, Transform parent, int farmIndex)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length <= 0.5f)
            return;

        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(length / fenceReferenceLength));
        float segmentLength = length / segmentCount;
        // Os prefabs de cerca usam o eixo X como comprimento. LookRotation usaria o Z.

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 position = start + direction.normalized * (segmentLength * (i + 0.5f));
            CreateFenceSegment(position, direction.normalized, segmentLength, parent, farmIndex);
        }
    }

    private void CreateFenceSegment(Vector3 position, Vector3 direction, float segmentLength, Transform parent, int farmIndex)
    {
        GameObject selectedFence = ChooseAsset(fencePrefabs, fencePrefab, farmIndex);
        if (selectedFence != null)
        {
            Vector3 lengthAxis;
            float prefabLength = GetPrefabLength(selectedFence, out lengthAxis);
            Quaternion rotation = Quaternion.FromToRotation(lengthAxis, direction);
            GameObject fence = Instantiate(selectedFence, position, rotation, parent);
            float lengthScale = segmentLength * fenceOverlap / Mathf.Max(0.1f, prefabLength);
            ScaleAlongLocalAxis(fence.transform, lengthAxis, lengthScale);
            fence.transform.localScale = Vector3.Scale(fence.transform.localScale, new Vector3(1.1f, 1.1f, 1.1f));
        }
        else
        {
            GameObject fence = CreateColoredCube("Cerca", position + Vector3.up * 0.6f, new Vector3(segmentLength, 1.2f, 0.18f), fenceMaterial, parent);
            fence.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        }
    }

    private void CreateGate(GameObject prefab, Vector3 position, float gateWidth, Transform parent)
    {
        Vector3 lengthAxis;
        float prefabLength = GetPrefabLength(prefab, out lengthAxis);
        Quaternion rotation = Quaternion.FromToRotation(lengthAxis, Vector3.right);
        GameObject gate = Instantiate(prefab, position, rotation, parent);
        gate.name = "Portao alinhado";
        float gateScale = gateWidth * 1.04f / Mathf.Max(0.1f, prefabLength);
        ScaleAlongLocalAxis(gate.transform, lengthAxis, gateScale);
        gate.transform.localScale = Vector3.Scale(gate.transform.localScale, new Vector3(1.1f, 1.1f, 1.1f));
    }

    private float GetPrefabLength(GameObject prefab, out Vector3 lengthAxis)
    {
        // Estes assets possuem escalas internas diferentes no importador. Usar
        // Mesh.bounds aqui mistura a escala do FBX com a escala do prefab e
        // pode gerar valores enormes. Mantemos as medidas reais dos modelos usados.
        lengthAxis = Vector3.right;

        if (prefab.name == "Fence" || prefab.name.StartsWith("Fence"))
            return 5.89f;

        if (prefab.name.Contains("Env_WoodFence_05"))
            return 3.6f;

        if (prefab.name.Contains("Env_WoodFence_01"))
            return 3.6f;

        return fenceReferenceLength;
    }

    private void ScaleAlongLocalAxis(Transform target, Vector3 axis, float scale)
    {
        Vector3 axisScale = Vector3.one;
        if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.z))
            axisScale.x = scale;
        else
            axisScale.z = scale;

        target.localScale = Vector3.Scale(target.localScale, axisScale);
    }

    private ChickenCoopLockpick CreateChickenCoop(Rect rect, Transform parent, GameObject selectedCoop, int farmIndex)
    {
        GameObject enclosure = new GameObject("Galinheiro Fechado - Lockpick");
        enclosure.transform.SetParent(parent);

        float height = 2.8f;
        Vector3 center = new Vector3(rect.center.x, 0f, rect.center.y);
        if (selectedCoop != null)
        {
            GameObject coop = Instantiate(selectedCoop, center + new Vector3(0f, 0f, 0.35f), Quaternion.Euler(0f, 90f, 0f), enclosure.transform);
            coop.name = "Casinha das Galinhas - Fazenda " + (farmIndex + 1);
            ApplyAssetMaterials(coop, buildingMaterial);
            FitCoopHouseInside(coop, rect, height);
            PlaceAssetOnGround(coop, 0.12f);
        }
        else
            CreateColoredCube("Casinha das Galinhas - Fazenda " + (farmIndex + 1), center + Vector3.up, new Vector3(3f, 2f, 2.4f), buildingMaterial, enclosure.transform);

        GameObject selectedFeeder = ChooseAsset(feederPrefabs, null, farmIndex + 1);
        if (selectedFeeder == null)
            selectedFeeder = ChooseAsset(troughPrefabs, troughPrefab, farmIndex + 1);

        Vector3 feederPosition = center + new Vector3(rect.width * 0.22f, 0f, -rect.height * 0.22f);
        if (selectedFeeder != null)
        {
            GameObject feeder = Instantiate(selectedFeeder, feederPosition, Quaternion.Euler(0f, 180f + (farmIndex % 3) * 12f, 0f), enclosure.transform);
            feeder.name = "Comedouro das Galinhas - Fazenda " + (farmIndex + 1);
            ApplyAssetMaterials(feeder, woodPropFallbackMaterial);
            PlaceAssetOnGround(feeder, 0.08f);
        }
        else
            CreateColoredCube("Comedouro das Galinhas - Fazenda " + (farmIndex + 1), feederPosition + Vector3.up * 0.16f, new Vector3(1.15f, 0.32f, 0.55f), woodPropFallbackMaterial, enclosure.transform);

        float doorWidth = 2.5f;
        float bottomZ = rect.yMin;
        float topZ = rect.yMax;
        float leftX = rect.xMin;
        float rightX = rect.xMax;

        float postWidth = 0.24f;
        float eaveHeight = height + 0.05f;
        float ridgeHeight = height + 0.85f;
        float doorZ = bottomZ - 0.04f;

        // Estrutura de madeira: quatro cantos e as duas laterais da porta.
        CreateCoopBar(new Vector3(leftX, height * 0.5f, bottomZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rightX, height * 0.5f, bottomZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(leftX, height * 0.5f, topZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rightX, height * 0.5f, topZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x - doorWidth * 0.5f, height * 0.5f, doorZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x + doorWidth * 0.5f, height * 0.5f, doorZ), new Vector3(postWidth, height, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x, height - 0.05f, bottomZ), new Vector3(rect.width, postWidth, postWidth), enclosure.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x, height - 0.05f, topZ), new Vector3(rect.width, postWidth, postWidth), enclosure.transform, coopWoodMaterial);

        // Tela fina em todos os lados, com abertura real para a porta.
        CreateCoopMeshPanel(rect, bottomZ, topZ, eaveHeight, ridgeHeight, doorWidth, enclosure.transform);

        // Piso baixo e pequena rampa deixam a construcao apoiada no terreno.
        CreateColoredCube("Piso do Galinheiro", new Vector3(rect.center.x, 0.08f, rect.center.y), new Vector3(rect.width, 0.16f, rect.height), coopWoodMaterial, enclosure.transform);
        GameObject ramp = CreateColoredCube("Rampa do Galinheiro", new Vector3(rect.center.x, 0.05f, bottomZ - 0.72f), new Vector3(doorWidth, 0.12f, 1.45f), coopWoodMaterial, enclosure.transform);
        ramp.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);

        // Telhado de duas aguas, com beiral e cumeeira marcados.
        float halfRoofDepth = rect.height * 0.5f + 0.45f;
        float roofRise = ridgeHeight - eaveHeight;
        float roofLength = Mathf.Sqrt(halfRoofDepth * halfRoofDepth + roofRise * roofRise);
        float roofAngle = Mathf.Atan2(roofRise, halfRoofDepth) * Mathf.Rad2Deg;
        Vector3 roofCenter = new Vector3(rect.center.x, (ridgeHeight + eaveHeight) * 0.5f, rect.center.y);
        GameObject nearRoof = CreateColoredCube("Telhado de Chapa - Frente", roofCenter + new Vector3(0f, 0f, halfRoofDepth * 0.5f), new Vector3(rect.width + 0.75f, 0.14f, roofLength), coopRoofMaterial, enclosure.transform);
        nearRoof.transform.rotation = Quaternion.Euler(roofAngle, 0f, 0f);
        GameObject farRoof = CreateColoredCube("Telhado de Chapa - Fundo", roofCenter - new Vector3(0f, 0f, halfRoofDepth * 0.5f), new Vector3(rect.width + 0.75f, 0.14f, roofLength), coopRoofMaterial, enclosure.transform);
        farRoof.transform.rotation = Quaternion.Euler(-roofAngle, 0f, 0f);
        CreateCoopBar(new Vector3(rect.center.x, ridgeHeight, rect.center.y), new Vector3(rect.width + 0.85f, 0.18f, 0.18f), enclosure.transform, coopWoodMaterial);

        GameObject door = CreateCoopDoor(rect, eaveHeight, doorWidth, enclosure.transform);
        ChickenCoopLockpick lockpick = door.AddComponent<ChickenCoopLockpick>();
        lockpick.door = door.transform;
        lockpick.interactionDistance = 3.8f;
        return lockpick;
    }

    private void CreateCoopMeshPanel(Rect rect, float bottomZ, float topZ, float eaveHeight, float ridgeHeight, float doorWidth, Transform parent)
    {
        float meshThickness = 0.045f;
        float verticalSpacing = 0.68f;
        float horizontalSpacing = 0.58f;
        float leftX = rect.xMin;
        float rightX = rect.xMax;
        float frontHeight = eaveHeight - 0.06f;

        // Frente e fundo ficam no beiral. A porta permanece na frente.
        for (float x = leftX + 0.35f; x < rightX; x += verticalSpacing)
        {
            CreateCoopBar(new Vector3(x, frontHeight * 0.5f, topZ), new Vector3(meshThickness, frontHeight, meshThickness), parent, coopMeshMaterial);
            if (Mathf.Abs(x - rect.center.x) > doorWidth * 0.5f + 0.05f)
                CreateCoopBar(new Vector3(x, frontHeight * 0.5f, bottomZ), new Vector3(meshThickness, frontHeight, meshThickness), parent, coopMeshMaterial);
        }

        for (float y = 0.16f; y < eaveHeight - 0.04f; y += horizontalSpacing)
        {
            CreateCoopBar(new Vector3(rect.center.x, y, topZ), new Vector3(rect.width - 0.32f, meshThickness, meshThickness), parent, coopMeshMaterial);
            float sideWidth = (rect.width - doorWidth) * 0.5f;
            CreateCoopBar(new Vector3(leftX + sideWidth * 0.5f, y, bottomZ), new Vector3(sideWidth, meshThickness, meshThickness), parent, coopMeshMaterial);
            CreateCoopBar(new Vector3(rightX - sideWidth * 0.5f, y, bottomZ), new Vector3(sideWidth, meshThickness, meshThickness), parent, coopMeshMaterial);
        }

        // As laterais seguem o perfil do telhado: beiral nas pontas e cumeeira no centro.
        for (float z = bottomZ + 0.35f; z < topZ; z += verticalSpacing)
        {
            float sideHeight = GetCoopDepthRoofHeight(z, bottomZ, topZ, eaveHeight, ridgeHeight) - 0.06f;
            CreateCoopBar(new Vector3(leftX, sideHeight * 0.5f, z), new Vector3(meshThickness, sideHeight, meshThickness), parent, coopMeshMaterial);
            CreateCoopBar(new Vector3(rightX, sideHeight * 0.5f, z), new Vector3(meshThickness, sideHeight, meshThickness), parent, coopMeshMaterial);
        }

        for (float y = 0.16f; y < ridgeHeight - 0.04f; y += horizontalSpacing)
        {
            CreateCoopSideHorizontalLine(leftX, bottomZ, topZ, y, eaveHeight, ridgeHeight, parent, meshThickness);
            CreateCoopSideHorizontalLine(rightX, bottomZ, topZ, y, eaveHeight, ridgeHeight, parent, meshThickness);
        }

        CreateCoopBar(new Vector3(leftX, eaveHeight - 0.04f, (bottomZ + topZ) * 0.5f), new Vector3(meshThickness, 0.08f, rect.height - 0.28f), parent, coopMeshMaterial);
        CreateCoopBar(new Vector3(rightX, eaveHeight - 0.04f, (bottomZ + topZ) * 0.5f), new Vector3(meshThickness, 0.08f, rect.height - 0.28f), parent, coopMeshMaterial);
    }

    private float GetCoopDepthRoofHeight(float z, float bottomZ, float topZ, float eaveHeight, float ridgeHeight)
    {
        float distanceFromRidge = Mathf.Abs(z - (bottomZ + topZ) * 0.5f) / Mathf.Max(0.01f, (topZ - bottomZ) * 0.5f);
        float arch = 1f - Mathf.Clamp01(distanceFromRidge);
        arch = arch * arch * (3f - 2f * arch);
        return Mathf.Lerp(eaveHeight, ridgeHeight, arch);
    }

    private void CreateCoopSideHorizontalLine(float x, float bottomZ, float topZ, float y, float eaveHeight, float ridgeHeight, Transform parent, float thickness)
    {
        float segmentStep = 0.68f;
        for (float z = bottomZ + 0.12f; z < topZ - 0.08f; z += segmentStep)
        {
            float nextZ = Mathf.Min(z + segmentStep, topZ - 0.12f);
            float startTop = GetCoopDepthRoofHeight(z, bottomZ, topZ, eaveHeight, ridgeHeight);
            float endTop = GetCoopDepthRoofHeight(nextZ, bottomZ, topZ, eaveHeight, ridgeHeight);
            if (startTop < y + 0.03f || endTop < y + 0.03f)
                continue;

            CreateCoopRailSegment(new Vector3(x, y, z), new Vector3(x, y, nextZ), thickness, parent, coopMeshMaterial);
        }
    }

    private void CreateCoopRailSegment(Vector3 start, Vector3 end, float thickness, Transform parent, Material material)
    {
        Vector3 midpoint = (start + end) * 0.5f;
        float length = Mathf.Abs(end.z - start.z);
        CreateColoredCube("Grade lateral do Galinheiro", midpoint, new Vector3(thickness, thickness, length), material, parent);
    }

    private GameObject CreateCoopDoor(Rect rect, float height, float doorWidth, Transform parent)
    {
        GameObject door = new GameObject("Porta com Cadeado - Lockpick");
        door.transform.SetParent(parent);
        float z = rect.yMin - 0.04f;
        door.transform.position = new Vector3(rect.center.x, height * 0.5f, z);

        BoxCollider collider = door.AddComponent<BoxCollider>();
        collider.size = new Vector3(doorWidth, height, 0.18f);

        float frame = 0.16f;
        CreateCoopBar(new Vector3(rect.center.x - doorWidth * 0.5f, height * 0.5f, z), new Vector3(frame, height, frame), door.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x + doorWidth * 0.5f, height * 0.5f, z), new Vector3(frame, height, frame), door.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x, height - frame * 0.5f, z), new Vector3(doorWidth, frame, frame), door.transform, coopWoodMaterial);
        CreateCoopBar(new Vector3(rect.center.x, frame * 0.5f, z), new Vector3(doorWidth, frame, frame), door.transform, coopWoodMaterial);

        for (float x = rect.center.x - doorWidth * 0.5f + 0.3f; x < rect.center.x + doorWidth * 0.5f; x += 0.38f)
            CreateCoopBar(new Vector3(x, height * 0.5f, z - 0.04f), new Vector3(0.035f, height - frame * 2.2f, 0.035f), door.transform, coopMeshMaterial);
        for (float y = frame + 0.05f; y < height - frame; y += 0.46f)
            CreateCoopBar(new Vector3(rect.center.x, y, z - 0.04f), new Vector3(doorWidth - 0.28f, 0.035f, 0.035f), door.transform, coopMeshMaterial);

        CreateColoredCube("Cadeado do Galinheiro", new Vector3(rect.center.x + doorWidth * 0.22f, height * 0.53f, z - 0.13f), new Vector3(0.20f, 0.28f, 0.08f), dangerMaterial, door.transform);
        return door;
    }

    private void CreateCoopBar(Vector3 position, Vector3 scale, Transform parent, Material material = null)
    {
        CreateColoredCube("Estrutura do Galinheiro", position, scale, material != null ? material : coopBarMaterial, parent);
    }

    private List<InteractableChicken> SpawnChickens(Rect pen, Transform parent)
    {
        List<InteractableChicken> chickens = new List<InteractableChicken>();
        List<Vector3> occupiedPositions = new List<Vector3>();
        float minimumSpacing = 1.75f;
        for (int i = 0; i < chickenCount; i++)
        {
            Vector3 position = Vector3.zero;
            bool foundPosition = false;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                Vector3 candidate = RandomPoint(pen.xMin + 1.5f, pen.xMax - 1.5f, pen.yMin + 1.5f, pen.yMax - 1.5f);
                bool isSeparated = true;
                for (int occupiedIndex = 0; occupiedIndex < occupiedPositions.Count; occupiedIndex++)
                {
                    if (Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(occupiedPositions[occupiedIndex].x, occupiedPositions[occupiedIndex].z)) < minimumSpacing)
                    {
                        isSeparated = false;
                        break;
                    }
                }

                if (isSeparated)
                {
                    position = candidate;
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                int gridColumns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(chickenCount)));
                int gridRow = i / gridColumns;
                int gridColumn = i % gridColumns;
                float x = Mathf.Lerp(pen.xMin + 1.5f, pen.xMax - 1.5f, gridColumns == 1 ? 0.5f : (float)gridColumn / (gridColumns - 1));
                float z = Mathf.Lerp(pen.yMin + 1.5f, pen.yMax - 1.5f, gridColumns == 1 ? 0.5f : (float)gridRow / (gridColumns - 1));
                position = new Vector3(x, 0f, z);
            }

            occupiedPositions.Add(position);
            GameObject chicken = CreateLowPolyChicken(position + Vector3.up * 0.1f, parent);
            InteractableChicken interactable = chicken.AddComponent<InteractableChicken>();
            interactable.sleeping = Random.value < 0.45f;
            chickens.Add(interactable);
        }
        return chickens;
    }

    private void SpawnCows(Rect pen, Transform parent)
    {
        for (int i = 0; i < cowCount; i++)
        {
            Vector3 position = RandomPoint(pen.xMin + 2f, pen.xMax - 2f, pen.yMin + 2f, pen.yMax - 2f);
            GameObject cow = CreateLowPolyCow(position + Vector3.up * 0.1f, parent);
            cow.AddComponent<SimpleAnimalWander>();
        }
    }

    private GameObject CreateLowPolyChicken(Vector3 position, Transform parent)
    {
        GameObject importedChicken = CreateImportedAnimal(
            "Galinha Roubavel",
            chickenAnimalPrefabs,
            position,
            parent,
            1.05f,
            new Vector3(0.72f, 1.05f, 0.72f));
        if (importedChicken != null)
            return importedChicken;

        GameObject root = new GameObject("Galinha Roubavel");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        CreateAnimalPart(PrimitiveType.Sphere, "Corpo", root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.72f, 0.62f, 0.9f), chickenMaterial);
        CreateAnimalPart(PrimitiveType.Sphere, "Cabeca", root.transform, new Vector3(0f, 0.8f, 0.28f), new Vector3(0.46f, 0.46f, 0.46f), chickenMaterial);
        CreateAnimalPart(PrimitiveType.Cylinder, "Crista", root.transform, new Vector3(0f, 1.1f, 0.28f), new Vector3(0.15f, 0.26f, 0.15f), dangerMaterial);

        CreateAnimalPart(PrimitiveType.Cylinder, "Bico", root.transform, new Vector3(0f, 0.78f, 0.58f), new Vector3(0.12f, 0.16f, 0.12f), beakMaterial);
        CreateAnimalPart(PrimitiveType.Cube, "Asa Esquerda", root.transform, new Vector3(-0.34f, 0.43f, 0.08f), new Vector3(0.12f, 0.32f, 0.48f), chickenMaterial);
        CreateAnimalPart(PrimitiveType.Cube, "Asa Direita", root.transform, new Vector3(0.34f, 0.43f, 0.08f), new Vector3(0.12f, 0.32f, 0.48f), chickenMaterial);

        CreateAnimalPart(PrimitiveType.Cylinder, "Perna Esquerda", root.transform, new Vector3(-0.14f, 0.08f, 0f), new Vector3(0.07f, 0.22f, 0.07f), legMaterial);
        CreateAnimalPart(PrimitiveType.Cylinder, "Perna Direita", root.transform, new Vector3(0.14f, 0.08f, 0f), new Vector3(0.07f, 0.22f, 0.07f), legMaterial);
        return root;
    }

    private GameObject CreateLowPolyCow(Vector3 position, Transform parent)
    {
        GameObject importedCow = CreateImportedAnimal(
            "Vaca Low Poly",
            cowAnimalPrefabs,
            position,
            parent,
            1.85f,
            new Vector3(1.65f, 1.85f, 2.4f));
        if (importedCow != null)
            return importedCow;

        GameObject root = new GameObject("Vaca Low Poly");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        CreateAnimalPart(PrimitiveType.Cube, "Corpo", root.transform, new Vector3(0f, 0.82f, 0f), new Vector3(1.8f, 1.05f, 0.9f), cowMaterial);
        CreateAnimalPart(PrimitiveType.Cube, "Pescoco", root.transform, new Vector3(0f, 1.15f, 0.58f), new Vector3(0.7f, 1.05f, 0.7f), cowMaterial);
        CreateAnimalPart(PrimitiveType.Sphere, "Cabeca", root.transform, new Vector3(0f, 1.45f, 0.92f), new Vector3(0.76f, 0.62f, 0.7f), cowMaterial);

        CreateAnimalPart(PrimitiveType.Cylinder, "Chifre Esquerdo", root.transform, new Vector3(-0.28f, 1.85f, 0.9f), new Vector3(0.12f, 0.3f, 0.12f), hornMaterial);
        CreateAnimalPart(PrimitiveType.Cylinder, "Chifre Direito", root.transform, new Vector3(0.28f, 1.85f, 0.9f), new Vector3(0.12f, 0.3f, 0.12f), hornMaterial);
        CreateAnimalPart(PrimitiveType.Cube, "Focinho", root.transform, new Vector3(0f, 1.32f, 1.25f), new Vector3(0.56f, 0.32f, 0.24f), hoofMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int front = -1; front <= 1; front += 2)
            {
                Vector3 legPosition = new Vector3(side * 0.55f, 0.27f, front * 0.28f);
                CreateAnimalPart(PrimitiveType.Cylinder, "Perna", root.transform, legPosition, new Vector3(0.16f, 0.55f, 0.16f), cowMaterial);
                CreateAnimalPart(PrimitiveType.Cube, "Casco", root.transform, legPosition + Vector3.down * 0.34f, new Vector3(0.22f, 0.12f, 0.24f), hoofMaterial);
            }
        }

        return root;
    }

    private GameObject CreateImportedAnimal(string animalName, GameObject[] prefabs, Vector3 position, Transform parent, float targetHeight, Vector3 colliderSize)
    {
        GameObject selectedPrefab = ChooseAsset(prefabs, null, Random.Range(0, 10000));
        if (selectedPrefab == null)
            return null;

        GameObject root = new GameObject(animalName);
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject visual = Instantiate(selectedPrefab, root.transform);
        visual.name = selectedPrefab.name + " Visual";
        visual.transform.localPosition = Vector3.zero;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            DestroySafe(root);
            return null;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float heightScale = targetHeight / Mathf.Max(0.01f, bounds.size.y);
        visual.transform.localScale = Vector3.Scale(visual.transform.localScale, Vector3.one * heightScale);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        visual.transform.position += Vector3.up * (position.y - bounds.min.y);

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
        collider.size = colliderSize;
        return root;
    }

    private GameObject CreateAnimalPart(PrimitiveType primitive, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().material = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            DestroySafe(collider);
        return part;
    }

    private void SpawnSecurity(Transform player, Rect chickenPen, Rect cowPen, Vector3 housePosition, int farmIndex, Transform parent)
    {
        int cameras = Mathf.Clamp(difficulty + farmIndex / 2, 1, 4);
        for (int i = 0; i < cameras; i++)
        {
            Vector3 position = i % 2 == 0
                ? new Vector3(chickenPen.center.x + Random.Range(-5f, 5f), 2.2f, chickenPen.yMin - 4f)
                : new Vector3(housePosition.x + Random.Range(-3f, 3f), 2.4f, housePosition.z + Random.Range(-4f, 4f));

            GameObject camera = GameObject.CreatePrimitive(PrimitiveType.Cube);
            camera.name = "Camera de Seguranca";
            camera.transform.SetParent(parent);
            camera.transform.position = position;
            camera.transform.localScale = new Vector3(0.45f, 0.25f, 0.35f);
            camera.GetComponent<Renderer>().material = dangerMaterial;
            SecurityCamera securityCamera = camera.AddComponent<SecurityCamera>();
            securityCamera.player = player;
            securityCamera.viewDistance = 11f + difficulty * 1.5f;
        }

        int trapCount = difficulty + 1 + farmIndex / 2;
        for (int i = 0; i < trapCount; i++)
        {
            Vector3 position = Random.value > 0.45f
                ? RandomPoint(chickenPen.xMin - 2f, chickenPen.xMax + 2f, chickenPen.yMin - 2f, chickenPen.yMax + 2f)
                : RandomPoint(cowPen.xMin - 2f, cowPen.xMax + 2f, cowPen.yMin - 2f, cowPen.yMax + 2f);

            GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trap.name = "Armadilha Sonora";
            trap.transform.SetParent(parent);
            trap.transform.position = position + Vector3.up * 0.03f;
            trap.transform.localScale = new Vector3(0.75f, 0.03f, 0.75f);
            trap.GetComponent<Renderer>().material = dangerMaterial;
            Collider collider = trap.GetComponent<Collider>();
            collider.isTrigger = true;
            trap.AddComponent<TrapSystem>();
        }
    }

    private void CreateExtractionZone(Vector3 position)
    {
        GameObject truck = CreateColoredCube("Caminhonete de Fuga", position + Vector3.up * 0.7f, new Vector3(3.6f, 1.4f, 2f), buildingMaterial, null);
        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zone.name = "Zona de Extracao";
        zone.transform.position = position;
        zone.transform.localScale = new Vector3(6f, 0.1f, 5f);
        zone.GetComponent<Renderer>().material = dangerMaterial;
        Collider collider = zone.GetComponent<Collider>();
        collider.isTrigger = true;
        zone.AddComponent<ExtractionZone>();
        truck.transform.SetParent(zone.transform);
    }

    private void SpawnFarmClutter(Rect lot, int farmIndex, Vector3 housePosition, Vector3 barnPosition, Transform parent)
    {
        int storageCount = 2 + farmIndex % 3;
        int hayCount = 2 + (farmIndex + 1) % 3;
        int fieldCount = 1 + farmIndex % 3;

        SpawnPropCluster("Itens do Patio", storagePropPrefabs, ruralPropPrefabs, housePosition + new Vector3(5f, 0f, -3f), storageCount, 3.5f, parent, farmIndex * 10);
        SpawnPropCluster("Feno do Celeiro", hayPrefabs, ruralPropPrefabs, barnPosition + new Vector3(-3f, 0f, -3f), hayCount, 2.5f, parent, farmIndex * 10 + 3);

        Vector3 fieldAnchor = LotPoint(lot, 0.50f, 0.46f, 0f);
        SpawnPropCluster("Itens da Plantacao", fieldPropPrefabs, ruralPropPrefabs, fieldAnchor, fieldCount, 4.5f, parent, farmIndex * 10 + 7);
    }

    private void SpawnPropCluster(string groupName, GameObject[] preferred, GameObject[] fallback, Vector3 center, int count, float radius, Transform parent, int seedOffset)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);

        for (int i = 0; i < count; i++)
        {
            GameObject prop = ChooseAsset(preferred, null, seedOffset + i);
            if (prop == null)
                continue;

            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
            GameObject instance = Instantiate(prop, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), group.transform);
            ApplyAssetMaterials(instance, groupName.Contains("Plantacao") ? cropFallbackMaterial : woodPropFallbackMaterial);
        }
    }

    private void SpawnFarmLand(Rect fieldRect, int farmIndex, Transform parent)
    {
        GameObject root = new GameObject("Area de Plantio - Fazenda " + (farmIndex + 1));
        root.transform.SetParent(parent);

        int columns = farmIndex % 2 == 0 ? 3 : 2;
        int rows = farmIndex % 3 == 0 ? 2 : 3;
        Vector3 start = new Vector3(fieldRect.xMin + 4f, 0.02f, fieldRect.yMin + 1.8f);
        float spacingX = Mathf.Max(5f, fieldRect.width / Mathf.Max(1, columns));
        float spacingZ = Mathf.Max(4f, fieldRect.height / Mathf.Max(1, rows));

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject tilePrefab = ChooseAsset(ruralLandPrefabs, null, farmIndex + x + z);
                Vector3 position = start + new Vector3(x * spacingX, 0f, z * spacingZ);
                if (tilePrefab != null)
                {
                    GameObject tile = Instantiate(tilePrefab, position, Quaternion.Euler(0f, farmIndex % 2 == 0 ? 0f : 90f, 0f), root.transform);
                    tile.transform.localScale = Vector3.Scale(tile.transform.localScale, new Vector3(0.82f, 1f, 0.82f));
                    ApplyAssetMaterials(tile, cropFallbackMaterial);
                    PlaceAssetOnGround(tile);
                }
                else
                {
                    CreateColoredCube("Plantio", position + Vector3.up * 0.04f, new Vector3(5.5f, 0.08f, 4.5f), roadMaterial, root.transform);
                }
            }
        }
    }

    private void SpawnRuralDetails(Rect lot, int farmIndex, Transform parent)
    {
        GameObject selectedWell = ChooseAsset(wellPrefabs, null, farmIndex);
        if (selectedWell != null)
            Instantiate(selectedWell, LotPoint(lot, farmIndex % 2 == 0 ? 0.44f : 0.60f, 0.72f, 0f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);

        GameObject landmark = ChooseAsset(landmarkPrefabs, null, farmIndex);
        if (landmark != null)
        {
            Vector3 position = farmIndex % 2 == 0 ? LotPoint(lot, 0.88f, 0.80f, 0f) : LotPoint(lot, 0.16f, 0.84f, 0f);
            GameObject instance = Instantiate(landmark, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(1.15f, 1.15f, 1.15f));
        }
    }

    private List<Transform> CreatePatrolPoints(Rect lot, Vector3 housePosition, Rect chickenPen, Rect cowPen, Transform parent)
    {
        List<Transform> points = new List<Transform>();
        AddPatrolPoint(points, housePosition + new Vector3(0f, 0f, 5f), parent);
        AddPatrolPoint(points, housePosition + new Vector3(5f, 0f, 0f), parent);
        AddPatrolPoint(points, new Vector3(chickenPen.center.x, 0f, chickenPen.center.y), parent);
        AddPatrolPoint(points, new Vector3(cowPen.center.x, 0f, cowPen.center.y), parent);
        AddPatrolPoint(points, LotPoint(lot, 0.5f, 0.5f, 0f), parent);
        return points;
    }

    private void AddPatrolPoint(List<Transform> points, Vector3 position, Transform parent)
    {
        GameObject point = new GameObject("Ponto de Patrulha");
        point.transform.SetParent(parent);
        point.transform.position = position;
        points.Add(point.transform);
    }

    private void AttachCameraToPlayer(Transform player)
    {
        Camera playerCamera = Camera.main;
        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            playerCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        playerCamera.transform.SetParent(player);
        playerCamera.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        playerCamera.transform.localRotation = Quaternion.identity;

        PlayerLook look = playerCamera.GetComponent<PlayerLook>();
        if (look == null)
            look = playerCamera.gameObject.AddComponent<PlayerLook>();

        look.corpoDojogador = player;
    }

    private GameObject CreateColoredCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.transform.SetParent(parent);
        cube.GetComponent<Renderer>().material = material;
        return cube;
    }

    private Rect GetLotRect(int column, int row)
    {
        float spacingX = lotWidth + roadWidth;
        float spacingZ = lotDepth + roadWidth;
        int index = row * farmColumns + column;
        float centerX = (column - (farmColumns - 1) * 0.5f) * spacingX;
        float centerZ = (row - (farmRows - 1) * 0.5f) * spacingZ;
        float jitterX = (Hash01(index * 31 + 5) * 2f - 1f) * roadWidth * farmScatter;
        float jitterZ = (Hash01(index * 47 + 11) * 2f - 1f) * roadWidth * farmScatter;

        centerX += jitterX + (row % 2 == 0 ? -spacingX * 0.08f : spacingX * 0.08f);
        centerZ += jitterZ;
        return new Rect(centerX - lotWidth * 0.5f, centerZ - lotDepth * 0.5f, lotWidth, lotDepth);
    }

    private float Hash01(int value)
    {
        float hash = Mathf.Sin(value * 12.9898f + seed * 0.0173f) * 43758.5453f;
        return hash - Mathf.Floor(hash);
    }

    private Rect InnerRect(Rect lot, float x, float z, float width, float depth)
    {
        return new Rect(
            lot.xMin + lot.width * x,
            lot.yMin + lot.height * z,
            lot.width * width,
            lot.height * depth);
    }

    private Vector3 LotPoint(Rect lot, float normalizedX, float normalizedZ, float y)
    {
        return new Vector3(
            Mathf.Lerp(lot.xMin, lot.xMax, normalizedX),
            y,
            Mathf.Lerp(lot.yMin, lot.yMax, normalizedZ));
    }

    private Vector3 GetPlayerStartPosition()
    {
        return new Vector3(GetWorldMinX() + roadWidth * 0.5f, 1.2f, GetWorldMinZ() + roadWidth * 0.5f);
    }

    private float GetWorldMinX()
    {
        float width = farmColumns * lotWidth + (farmColumns + 1) * roadWidth;
        return -(width + worldBorderPadding * 2f) * 0.5f;
    }

    private float GetWorldMaxX()
    {
        return -GetWorldMinX();
    }

    private float GetWorldMinZ()
    {
        float depth = farmRows * lotDepth + (farmRows + 1) * roadWidth;
        return -(depth + worldBorderPadding * 2f) * 0.5f;
    }

    private float GetWorldMaxZ()
    {
        return -GetWorldMinZ();
    }

    private Vector3 RandomPoint(float minX, float maxX, float minZ, float maxZ)
    {
        return new Vector3(Random.Range(minX, maxX), 0f, Random.Range(minZ, maxZ));
    }

    private GameObject ChooseAsset(GameObject[] options, GameObject fallback, int index)
    {
        if (options != null && options.Length > 0)
        {
            for (int attempt = 0; attempt < options.Length; attempt++)
            {
                GameObject option = options[Mathf.Abs(index + attempt) % options.Length];
                if (option != null)
                    return option;
            }
        }

        return fallback;
    }

    private void CreateMaterials()
    {
        groundMaterial = CreateMaterial(new Color(0.12f, 0.28f, 0.14f));
        roadMaterial = CreateMaterial(new Color(0.15f, 0.13f, 0.11f));
        fenceMaterial = CreateMaterial(new Color(0.47f, 0.32f, 0.18f));
        chickenMaterial = CreateMaterial(new Color(0.95f, 0.92f, 0.78f));
        cowMaterial = CreateMaterial(new Color(0.16f, 0.14f, 0.13f));
        beakMaterial = CreateMaterial(new Color(0.95f, 0.58f, 0.12f));
        legMaterial = CreateMaterial(new Color(0.86f, 0.48f, 0.08f));
        hornMaterial = CreateMaterial(new Color(0.72f, 0.64f, 0.44f));
        hoofMaterial = CreateMaterial(new Color(0.08f, 0.07f, 0.06f));
        dangerMaterial = CreateMaterial(new Color(0.78f, 0.12f, 0.09f));
        buildingMaterial = CreateMaterial(new Color(0.42f, 0.37f, 0.32f));
        natureLeafMaterial = CreateMaterial(new Color(0.16f, 0.48f, 0.18f));
        natureAutumnLeafMaterial = CreateMaterial(new Color(0.62f, 0.28f, 0.08f));
        natureBarkMaterial = CreateMaterial(new Color(0.30f, 0.18f, 0.09f));
        natureGrassMaterial = CreateMaterial(new Color(0.25f, 0.55f, 0.16f));
        natureFlowerMaterial = CreateMaterial(new Color(0.82f, 0.35f, 0.12f));
        natureRockMaterial = CreateMaterial(new Color(0.30f, 0.34f, 0.32f));
        houseFallbackMaterial = CreateMaterial(new Color(0.72f, 0.58f, 0.38f));
        barnFallbackMaterial = CreateMaterial(new Color(0.48f, 0.12f, 0.08f));
        woodPropFallbackMaterial = CreateMaterial(new Color(0.36f, 0.20f, 0.09f));
        cropFallbackMaterial = CreateMaterial(new Color(0.52f, 0.34f, 0.12f));
        reliefMaterial = CreateMaterial(new Color(0.24f, 0.42f, 0.19f));
        reliefDarkMaterial = CreateMaterial(new Color(0.18f, 0.33f, 0.16f));
        terrainMidMaterial = CreateMaterial(new Color(0.16f, 0.31f, 0.14f));
        terrainHighMaterial = CreateMaterial(new Color(0.12f, 0.24f, 0.13f));
        coopBarMaterial = CreateMaterial(new Color(0.16f, 0.19f, 0.17f));
        coopWoodMaterial = CreateMaterial(new Color(0.36f, 0.20f, 0.09f));
        coopMeshMaterial = CreateMaterial(new Color(0.08f, 0.10f, 0.09f));
        coopRoofMaterial = CreateMaterial(new Color(0.24f, 0.27f, 0.23f));
    }

    private void ApplyAssetMaterials(GameObject instance, Material fallback)
    {
        if (instance == null || fallback == null)
            return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                materials = new Material[1];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                bool missing = material == null || material.shader == null;
                bool plainWhite = !missing && material.mainTexture == null && material.color.r > 0.94f && material.color.g > 0.94f && material.color.b > 0.94f;
                if (missing || plainWhite)
                    materials[materialIndex] = fallback;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private void PlaceAssetOnGround(GameObject instance, float groundY = 0f)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        instance.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    private void FitCoopHouseInside(GameObject instance, Rect rect, float wallHeight)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float maximumWidth = rect.width * 0.42f;
        float maximumDepth = rect.height * 0.42f;
        float maximumHeight = wallHeight * 0.68f;
        float scaleFactor = Mathf.Min(
            maximumWidth / Mathf.Max(0.01f, bounds.size.x),
            maximumDepth / Mathf.Max(0.01f, bounds.size.z));
        scaleFactor = Mathf.Min(scaleFactor, maximumHeight / Mathf.Max(0.01f, bounds.size.y));
        scaleFactor = Mathf.Min(scaleFactor, 1f);

        if (scaleFactor > 0f && scaleFactor < 1f)
            instance.transform.localScale *= scaleFactor;
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void DestroySafe(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
