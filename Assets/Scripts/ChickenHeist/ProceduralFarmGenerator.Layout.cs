using System.Collections.Generic;
using UnityEngine;

public partial class ProceduralFarmGenerator
{
    public GameObject[] cropPlantPrefabs;
    public GameObject orchardTreePrefab;
    private static readonly string[] FarmNames = {
        "Santa Clara - Leiteira", "Boa Esperanca - Granjeira", "Sao Bento - Cereais",
        "Vale das Macieiras - Pomar", "Recanto do Cedro - Sitio Antigo", "Alto da Serra - Encosta",
        "Tres Irmaos - Leiteira", "Sol Nascente - Granjeira", "Terra Firme - Cereais",
        "Vista Alegre - Pomar", "Riacho Fundo - Sitio Antigo", "Pedra Alta - Encosta"
    };
    // Slots are separated by a central lane and two cross lanes. Roles vary by farm.
    private static readonly int[,] Roles = {
        {0, 2, 4, 3, 1, 5}, {0, 4, 2, 5, 3, 1}, {4, 0, 5, 2, 1, 3},
        {2, 0, 3, 4, 5, 1}, {0, 5, 1, 3, 2, 4}, {4, 2, 0, 5, 3, 1}
    };

    private Rect[] LayoutZones(Rect lot, int index)
    {
        float split = index % 3 == 0 ? 0.44f : index % 3 == 1 ? 0.54f : 0.49f;
        float[] starts = {0.065f, 0.375f, 0.70f};
        float[] ends = {0.30f, 0.625f, 0.945f};
        var zones = new Rect[6];
        for (int slot = 0; slot < 6; slot++)
        {
            int row = slot / 2;
            bool left = slot % 2 == 0;
            float x = left ? 0.055f : split + 0.055f;
            float w = left ? split - 0.11f : 0.945f - x;
            int role = Roles[index % 6, slot];
            zones[role] = InnerRect(lot, x, starts[row], w, ends[row] - starts[row]);
        }
        return zones;
    }

    private void CreateDesignedFarm(int index, Rect lot, Transform player)
    {
        var root = new GameObject("Fazenda " + (index + 1).ToString("00") + " - " + FarmNames[index % FarmNames.Length]);
        var parent = root.transform;
        var info = root.AddComponent<FarmLayoutInfo>();
        info.identity = FarmNames[index % FarmNames.Length];
        info.layoutIndex = index;
        info.lot = lot;
        info.zones = LayoutZones(lot, index);
        info.entrance = new Vector3(lot.center.x, 0f, lot.yMin);
        Rect houseZone = info.zones[0], barnZone = info.zones[1], coopZone = info.zones[2];
        Rect pasture = info.zones[3], field = info.zones[4], service = info.zones[5];
        CreateLotFence(lot, parent, index);

        float spine = lot.xMin + lot.width * (index % 3 == 0 ? 0.44f : index % 3 == 1 ? 0.54f : 0.49f);
        FarmPath("Acesso ao patio", info.entrance + Vector3.back * 5f, new Vector3(spine, 0, lot.yMin + lot.height * 0.035f), 4.5f, parent);
        FarmPath("Via de servico", new Vector3(spine, 0, lot.yMin + lot.height * 0.035f), new Vector3(spine, 0, lot.yMax - 2f), 3.8f, parent);
        foreach (Rect zone in info.zones)
        {
            float laneZ = zone.yMin - 1.9f;
            FarmPath("Caminho entre setores", new Vector3(spine, 0, laneZ), new Vector3(zone.center.x, 0, laneZ), 2.6f, parent);
            FarmPath("Entrada do setor", new Vector3(zone.center.x, 0, laneZ), new Vector3(zone.center.x, 0, zone.yMin + 1.4f), 2.5f, parent);
        }

        GameObject house = PlaceInZone("Casa - " + info.identity, ChooseAsset(housePrefabs, housePrefab, index),
            houseZone, 12.5f + index % 3, 11f, 8f, parent, houseFallbackMaterial);
        GameObject barn = PlaceInZone("Celeiro - " + info.identity, ChooseAsset(barnPrefabs, barnPrefab, index),
            barnZone, 19f, 13f, 11f, parent, barnFallbackMaterial);
        Rect siloZone = InnerRect(service, 0.05f, 0.40f, 0.28f, 0.55f);
        PlaceInZone("Silo - " + info.identity, ChooseAsset(siloPrefabs, siloPrefab, index), siloZone, 5f, 5f, 12f, parent, buildingMaterial);
        Rect landmarkZone = InnerRect(service, 0.58f, 0.42f, 0.36f, 0.50f);
        PlaceInZone("Marco Rural - " + info.identity, ChooseAsset(landmarkPrefabs, null, index), landmarkZone, 5f, 5f, 12f, parent, woodPropFallbackMaterial);
        PlaceInZone("Poco - " + info.identity, ChooseAsset(wellPrefabs, null, index),
            InnerRect(service, 0.40f, 0.10f, 0.2f, 0.25f), 2.7f, 2.7f, 3f, parent, woodPropFallbackMaterial);

        Rect coopRect = new Rect(coopZone.center.x - 7f, coopZone.center.y - 5.5f, 14f, 11f);
        ChickenCoopLockpick coop = CreateChickenCoop(coopRect, parent, ChooseAsset(chickenCoopPrefabs, chickenHousePrefab, index), index);
        var coopObstacles = new List<Rect>();
        foreach (Transform child in coop.transform.parent)
        {
            if (!child.name.StartsWith("Casinha") && !child.name.StartsWith("Comedouro")) continue;
            var b = VisualBounds(child.gameObject);
            coopObstacles.Add(new Rect(b.min.x - 0.5f, b.min.z - 0.5f, b.size.x + 1f, b.size.z + 1f));
        }
        SpawnContainedAnimals(coopRect, coop.transform.parent, true, index, coopObstacles.ToArray(), coop);
        CreatePen("Pastagem das Vacas", pasture, false, parent, index);
        // Fit the pen's equipment independently of source importer scales.
        Transform penRoot = parent.Find("Pastagem das Vacas");
        var cowObstacles = new List<Rect>();
        int equipmentIndex = 0;
        foreach (Transform child in penRoot)
        {
            if (child.name.Contains("Fence") || child.name.Contains("Portao")) continue;
            FitAsset(child.gameObject, new Vector3(pasture.xMin + 3.5f + equipmentIndex * 5f, 0f, pasture.yMax - 3f), new Vector3(3f, 1f, 1.5f));
            Bounds b = VisualBounds(child.gameObject);
            cowObstacles.Add(new Rect(b.min.x - 1.6f, b.min.z - 1.6f, b.size.x + 3.2f, b.size.z + 3.2f));
            equipmentIndex++;
        }
        SpawnContainedAnimals(pasture, penRoot, false, index, cowObstacles.ToArray(), null);
        CreateCropPen("Horta e Plantacao", field, parent, index);
        CreateDesignedCrops(field, index, parent);
        DressYard(houseZone, index, parent, storagePropPrefabs, "Utensilios da Casa");
        DressYard(barnZone, index + 3, parent, hayPrefabs, "Reserva de Feno");
        DressYard(service, index + 1, parent, storagePropPrefabs, "Armazem de Racao");
        CreateFarmLighting(house.transform.position, barn.transform.position, index, parent);

        var patrol = new List<Transform>();
        AddPatrolPoint(patrol, new Vector3(spine, 0, houseZone.yMin - 1.9f), parent);
        AddPatrolPoint(patrol, new Vector3(spine, 0, coopZone.yMin - 1.9f), parent);
        AddPatrolPoint(patrol, new Vector3(coopRect.center.x, 0, coopZone.yMin - 1.9f), parent);
        AddPatrolPoint(patrol, new Vector3(spine, 0, coopZone.yMin - 1.9f), parent);
        AddPatrolPoint(patrol, new Vector3(spine, 0, barnZone.yMin - 1.9f), parent);
        Transform farmer = CreateFarmer(new Vector3(houseZone.center.x, 0.05f, houseZone.yMin - 1.9f), player, index);
        farmer.SetParent(parent);
        farmer.GetComponent<FarmerStateMachine>().patrolPoints = patrol.ToArray();
        farmers.Add(farmer.GetComponent<FarmerSleepSystem>());
        CreateMountedSecurity(coopRect, index, player, parent);
        CreatePhaseTwoWires(info);
        PlantFarmEdges(lot, index, parent);
    }

    public static Bounds VisualBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(instance.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void FitAsset(GameObject instance, Vector3 groundCenter, Vector3 maximumSize)
    {
        Bounds b = VisualBounds(instance);
        float scale = Mathf.Min(maximumSize.x / Mathf.Max(b.size.x, 0.01f),
            maximumSize.y / Mathf.Max(b.size.y, 0.01f), maximumSize.z / Mathf.Max(b.size.z, 0.01f));
        instance.transform.localScale *= scale;
        b = VisualBounds(instance);
        instance.transform.position += new Vector3(groundCenter.x - b.center.x, groundCenter.y - b.min.y, groundCenter.z - b.center.z);
    }

    private GameObject PlaceInZone(string name, GameObject prefab, Rect zone, float width, float depth, float height, Transform parent, Material fallback)
    {
        Vector3 center = new Vector3(zone.center.x, 0f, zone.center.y);
        GameObject obj = prefab != null ? Instantiate(prefab, parent) : CreateColoredCube(name, center, Vector3.one, fallback, parent);
        obj.name = name;
        // Preserve FBX axis conversion at its root instead of replacing its rotation.
        FitAsset(obj, center, new Vector3(Mathf.Min(width, zone.width * 0.85f), height, Mathf.Min(depth, zone.height * 0.8f)));
        ApplyAssetMaterials(obj, fallback);
        EnsureBuildingCollision(obj);
        return obj;
    }

    private void EnsureBuildingCollision(GameObject obj)
    {
        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }

    private void FitFenceToSpan(GameObject fence, Vector3 center, Vector3 direction, float length)
    {
        // Measure the instantiated geometry, including prefab and importer transforms.
        fence.transform.rotation = Quaternion.identity;
        Bounds b = VisualBounds(fence);
        if (b.size.y < Mathf.Min(b.size.x, b.size.z)) fence.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        b = VisualBounds(fence);
        if (b.size.z > b.size.x) fence.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * fence.transform.rotation;
        b = VisualBounds(fence);
        // Scale in world-aligned axes with a wrapper, so imported local axes cannot flatten a panel.
        var holder = new GameObject(fence.name + " - Encaixe");
        holder.transform.SetParent(fence.transform.parent);
        holder.transform.position = b.center;
        fence.transform.SetParent(holder.transform, true);
        holder.transform.localScale = new Vector3(length / Mathf.Max(b.size.x, 0.01f), 1.35f / Mathf.Max(b.size.y, 0.01f), 1f);
        holder.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        b = VisualBounds(holder);
        holder.transform.position += new Vector3(center.x - b.center.x, -b.min.y, center.z - b.center.z);
        EnsureBuildingCollision(fence);
    }

    private void FarmPath(string name, Vector3 a, Vector3 b, float width, Transform parent)
    {
        BuildDirtRoad(name, a, b, width, parent, false);
    }

    private void DressYard(Rect zone, int index, Transform parent, GameObject[] prefabs, string name)
    {
        for (int i = 0; i < 3 + index % 2; i++)
        {
            var asset = ChooseAsset(prefabs, null, index+i);
            if (asset == null) continue;
            float side = i % 2 == 0 ? zone.xMin + 2f : zone.xMax - 2f;
            float z = zone.yMin + 2.2f + (i / 2)*3.4f;
            var prop = Instantiate(asset, parent);
            prop.name = name + " " + (i+1);
            FitAsset(prop, new Vector3(side, 0f, z), new Vector3(2.3f, 2.1f, 2.3f));
            ApplyAssetMaterials(prop, woodPropFallbackMaterial);
            EnsureBuildingCollision(prop);
        }
    }

    private void CreateDesignedCrops(Rect field, int index, Transform parent)
    {
        var root = new GameObject("Canteiros - " + FarmNames[index % FarmNames.Length]);
        root.transform.SetParent(parent);
        Rect usable = ExpandRect(field, -2.3f);
        int columns = index % 6 == 2 ? 5 : 3;
        int rows = 3;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            Vector3 center = new Vector3(usable.xMin + usable.width*(x+0.5f)/columns, 0, usable.yMin + usable.height*(z+0.5f)/rows);
            if (index % 6 == 3)
            {
                var tree = Instantiate(orchardTreePrefab != null ? orchardTreePrefab : ChooseAsset(treePrefabs, null, index+x), root.transform);
                FitAsset(tree, center, new Vector3(3.4f, 5f, 3.4f));
                ApplyNatureMaterials(tree, "Arvore do Pomar");
                continue;
            }
            var prefab = ChooseAsset(ruralLandPrefabs, null, index % 5);
            if (prefab == null) continue;
            var crop = Instantiate(prefab, root.transform);
            crop.name = "Canteiro " + (z*columns+x+1);
            FitAsset(crop, center, new Vector3(usable.width/columns - 0.75f, 1.2f, usable.height/rows - 0.65f));
            // Soil tiles sit flush; the crop's leaves remain above ground.
            Bounds b = VisualBounds(crop);
            if (!prefab.name.Contains("Wheat")) crop.transform.position += Vector3.up * (0.035f - b.max.y);
            ApplyAssetMaterials(crop, cropFallbackMaterial);
            GameObject plantPrefab = ChooseAsset(cropPlantPrefabs, null, index % 6 == 2 ? 0 : 1 + index % 4);
            if (plantPrefab == null) continue;
            float bedWidth = Mathf.Min(b.size.x, usable.width / columns - 0.75f);
            float bedDepth = Mathf.Min(b.size.z, usable.height / rows - 0.65f);
            for (int rz = 0; rz < 3; rz++)
            for (int rx = 0; rx < 3; rx++)
            {
                var plant = Instantiate(plantPrefab, root.transform);
                plant.name = "Cultivo " + plantPrefab.name;
                Vector3 p = center + new Vector3((rx - 1) * bedWidth * 0.27f, 0.035f, (rz - 1) * bedDepth * 0.27f);
                FitAsset(plant, p, new Vector3(bedWidth * 0.23f, index % 6 == 2 ? 1.25f : 0.65f, bedDepth * 0.23f));
                ApplyAssetMaterials(plant, natureGrassMaterial);
            }
        }
    }

    private void SpawnContainedAnimals(Rect area, Transform parent, bool chicken, int index, Rect[] obstacles, ChickenCoopLockpick coop)
    {
        float margin = chicken ? 1.1f : 2.2f;
        Rect walk = ExpandRect(area, -margin);
        int count = chicken ? chickenCount : cowCount;
        var occupied = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            Vector3 position = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                position = RandomPoint(walk.xMin, walk.xMax, walk.yMin, walk.yMax);
                Vector2 p = new Vector2(position.x, position.z);
                bool valid = true;
                foreach (Rect obstacle in obstacles) if (obstacle.Contains(p)) valid = false;
                foreach (Vector2 other in occupied) if (Vector2.Distance(p, other) < (chicken ? 1.6f : 4f)) valid = false;
                if (valid) { occupied.Add(p); found = true; break; }
            }
            if (!found) { Debug.LogWarning("No free animal position in farm " + index); break; }
            position.y = chicken ? 0.16f : 0.02f;
            var animal = chicken ? CreateLowPolyChicken(position, parent) : CreateLowPolyCow(position, parent);
            var boundary = animal.AddComponent<FarmAnimalBoundary>();
            boundary.area = walk;
            boundary.obstacles = obstacles;
            boundary.radius = chicken ? 0.60f : 1.55f;
            if (chicken)
            {
                var interaction = animal.AddComponent<InteractableChicken>();
                interaction.sleeping = Random.value < 0.45f;
                interaction.coop = coop;
                if (i == 0) coop.scareChicken = animal.transform;
            }
            else animal.AddComponent<SimpleAnimalWander>();
        }
    }

    private void CreateMountedSecurity(Rect coop, int index, Transform player, Transform parent)
    {
        int count = index % 3 == 0 ? 1 : 2;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = new Vector3(i == 0 ? coop.xMin - 1f : coop.xMax + 1f, 0, coop.yMin - 1.5f);
            CreateColoredCube("Poste de Vigilancia", p + Vector3.up * 1.8f, new Vector3(0.18f, 3.6f, 0.18f), coopWoodMaterial, parent);
            var cam = CreateColoredCube("Camera de Seguranca", p + Vector3.up * 3.4f, new Vector3(0.35f, 0.22f, 0.5f), coopMeshMaterial, parent);
            var ai = cam.AddComponent<SecurityCamera>();
            ai.player = player;
            ai.viewDistance = 12f;
        }
    }

    public static void CreatePhaseTwoWires(FarmLayoutInfo farm)
    {
        if(farm.GetComponentInChildren<TrapSystem>()!=null)return;
        // Across the open entry lane, away from fences and building walls.
        var wire=new GameObject("Fio de alarme - entrada");wire.transform.SetParent(farm.transform,false);
        wire.transform.position=farm.entrance+Vector3.forward*2.5f;
        wire.AddComponent<TrapSystem>();
    }

    private void PlantFarmEdges(Rect lot, int index, Transform parent)
    {
        for (int i = 0; i < 7; i++)
        {
            var prefab = ChooseAsset(bushPrefabs, null, index+i);
            if (prefab == null) continue;
            var bush = Instantiate(prefab, parent);
            bush.name = "Sebe Rural";
            FitAsset(bush, new Vector3(lot.xMin + 2f, 0, Mathf.Lerp(lot.yMin+3f, lot.yMax-3f, i/6f)), new Vector3(1.8f, 1.4f, 2f));
            ApplyNatureMaterials(bush, "Arbusto");
        }
    }
}
