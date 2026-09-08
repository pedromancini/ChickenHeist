using UnityEngine;
using UnityEditor;
using System.IO;

public class FarmSceneBuilder
{
    [MenuItem("Chicken Heist/Legacy/Build Single Farm")]
    private static void Run()
    {
        // Se a fazenda já foi gerada nesta sessão, não roda de novo
        if (EditorPrefs.GetBool("FarmGenerated_v1", false)) return;
        EditorPrefs.SetBool("FarmGenerated_v1", true);

        // 1. Limpar fazenda anterior se existir para não duplicar
        GameObject oldFarm = GameObject.Find("__AUTOMATIC_FARM__");
        if (oldFarm != null)
        {
            Undo.DestroyObjectImmediate(oldFarm);
        }

        // Criar o objeto pai da fazenda
        GameObject farmParent = new GameObject("__AUTOMATIC_FARM__");
        Undo.RegisterCreatedObjectUndo(farmParent, "Create Automatic Farm");

        // 2. Carregar os Prefabs
        string marpaPath = "Assets/MarpaStudio/Built-In/Prefabs/";
        string naturePath = "Assets/SimpleNaturePack/Prefabs/";

        GameObject housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "House.prefab");
        GameObject barnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "BarnBase.prefab");
        GameObject siloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Silo.prefab");
        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Fence.prefab");
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Gate.prefab");
        GameObject chickenHousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "ChickenHouse.prefab");
        GameObject wellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Well.prefab");
        GameObject windMillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "WindMill.prefab");
        GameObject haySquarePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "HaiSquare.prefab");
        GameObject hayCylinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "HaiCylinder.prefab");
        GameObject foodTroughPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "FoodTrough.prefab");
        GameObject wheelCartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "WheelCart.prefab");
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Box.prefab");
        GameObject bucketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(marpaPath + "Bucket.prefab");

        // Prefabs de Natureza
        GameObject[] treePrefabs = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            treePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(naturePath + $"Tree_0{i + 1}.prefab");
        }

        GameObject[] bushPrefabs = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            bushPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(naturePath + $"Bush_0{i + 1}.prefab");
        }

        GameObject[] rockPrefabs = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            rockPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(naturePath + $"Rock_0{i + 1}.prefab");
        }

        GameObject groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(naturePath + "Ground_01.prefab");

        // 3. Posicionar Construções Principais
        // Casa do Fazendeiro
        if (housePrefab != null)
        {
            GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab, farmParent.transform);
            house.transform.position = new Vector3(18f, 0f, 35f);
            house.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            
            if (wellPrefab != null)
            {
                GameObject well = (GameObject)PrefabUtility.InstantiatePrefab(wellPrefab, farmParent.transform);
                well.transform.position = new Vector3(8f, 0f, 30f);
            }
        }

        // Celeiro Principal
        if (barnPrefab != null)
        {
            GameObject barn = (GameObject)PrefabUtility.InstantiatePrefab(barnPrefab, farmParent.transform);
            barn.transform.position = new Vector3(-20f, 0f, 25f);
            barn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            if (siloPrefab != null)
            {
                GameObject silo = (GameObject)PrefabUtility.InstantiatePrefab(siloPrefab, farmParent.transform);
                silo.transform.position = new Vector3(-32f, 0f, 28f);
            }

            // Feno
            if (haySquarePrefab != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject hay = (GameObject)PrefabUtility.InstantiatePrefab(haySquarePrefab, farmParent.transform);
                    hay.transform.position = new Vector3(-30f, 0.5f * i, 16f);
                    hay.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
                }
            }
        }

        // Moinho
        if (windMillPrefab != null)
        {
            GameObject wind = (GameObject)PrefabUtility.InstantiatePrefab(windMillPrefab, farmParent.transform);
            wind.transform.position = new Vector3(-35f, 0f, 45f);
        }

        // 4. Posicionar Cercados
        float fenceLength = 3f;

        // Galinheiro
        if (chickenHousePrefab != null)
        {
            GameObject coop = (GameObject)PrefabUtility.InstantiatePrefab(chickenHousePrefab, farmParent.transform);
            coop.transform.position = new Vector3(-18f, 0f, -5f);
            coop.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            BuildFenceRect(new Vector3(-25f, 0f, -12f), new Vector3(-10f, 0f, 2f), fencePrefab, gatePrefab, new Vector3(-17.5f, 0f, -12f), farmParent.transform, fenceLength);
        }

        // Cercado dos Cavalos
        BuildFenceRect(new Vector3(15f, 0f, -10f), new Vector3(35f, 0f, 10f), fencePrefab, gatePrefab, new Vector3(25f, 0f, -10f), farmParent.transform, fenceLength);
        if (foodTroughPrefab != null)
        {
            GameObject trough = (GameObject)PrefabUtility.InstantiatePrefab(foodTroughPrefab, farmParent.transform);
            trough.transform.position = new Vector3(25f, 0f, 5f);
        }
        if (wheelCartPrefab != null)
        {
            GameObject cart = (GameObject)PrefabUtility.InstantiatePrefab(wheelCartPrefab, farmParent.transform);
            cart.transform.position = new Vector3(18f, 0f, -2f);
            cart.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        }

        // Cercado das Vacas
        BuildFenceRect(new Vector3(15f, 0f, 15f), new Vector3(35f, 0f, 30f), fencePrefab, gatePrefab, new Vector3(15f, 0f, 22.5f), farmParent.transform, fenceLength);
        if (foodTroughPrefab != null)
        {
            GameObject trough = (GameObject)PrefabUtility.InstantiatePrefab(foodTroughPrefab, farmParent.transform);
            trough.transform.position = new Vector3(20f, 0f, 25f);
            trough.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }
        if (hayCylinderPrefab != null)
        {
            GameObject hay = (GameObject)PrefabUtility.InstantiatePrefab(hayCylinderPrefab, farmParent.transform);
            hay.transform.position = new Vector3(30f, 0f, 20f);
        }

        // Perímetro
        BuildFenceRect(new Vector3(-45f, 0f, -25f), new Vector3(45f, 0f, 55f), fencePrefab, gatePrefab, new Vector3(0f, 0f, -25f), farmParent.transform, fenceLength);

        // Caminhos de Terra (Ground_01)
        if (groundPrefab != null)
        {
            for (float z = -25f; z <= 20f; z += 4f)
            {
                GameObject road = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, farmParent.transform);
                road.transform.position = new Vector3(0f, -0.05f, z);
            }
            for (float x = -16f; x <= 0f; x += 4f)
            {
                GameObject road = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, farmParent.transform);
                road.transform.position = new Vector3(x, -0.05f, 15f);
            }
            for (float x = 0f; x <= 12f; x += 4f)
            {
                GameObject road = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, farmParent.transform);
                road.transform.position = new Vector3(x, -0.05f, 25f);
            }
        }

        // Árvores nas bordas
        int totalTrees = 80;
        for (int i = 0; i < totalTrees; i++)
        {
            float angle = i * (360f / totalTrees);
            float radius = Random.Range(65f, 95f);
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;

            if (z < -20f && Mathf.Abs(x) < 15f) continue;

            GameObject selectedTree = treePrefabs[Random.Range(0, 5)];
            if (selectedTree != null)
            {
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(selectedTree, farmParent.transform);
                tree.transform.position = new Vector3(x, 0f, z);
                tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float randomScale = Random.Range(0.8f, 1.4f);
                tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
            }
        }

        // Pedras nas montanhas
        int totalRocks = 30;
        for (int i = 0; i < totalRocks; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(70f, 100f);
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;

            GameObject selectedRock = rockPrefabs[Random.Range(0, 5)];
            if (selectedRock != null)
            {
                GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(selectedRock, farmParent.transform);
                rock.transform.position = new Vector3(x, radius > 80f ? 2f : 0f, z);
                rock.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                float randomScale = Random.Range(1.5f, 3f);
                rock.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
            }
        }

        // Caixas e Baldes
        Vector3[] obstaclePositions = new Vector3[] {
            new Vector3(-5f, 0f, 5f),
            new Vector3(-8f, 0f, -2f),
            new Vector3(6f, 0f, 8f),
            new Vector3(10f, 0f, -5f),
            new Vector3(-12f, 0f, 10f)
        };

        if (boxPrefab != null)
        {
            foreach (Vector3 pos in obstaclePositions)
            {
                GameObject box = (GameObject)PrefabUtility.InstantiatePrefab(boxPrefab, farmParent.transform);
                box.transform.position = pos;
                box.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
            }
        }

        if (bucketPrefab != null)
        {
            GameObject bucket = (GameObject)PrefabUtility.InstantiatePrefab(bucketPrefab, farmParent.transform);
            bucket.transform.position = new Vector3(-3f, 0f, 12f);
        }

        Debug.Log("Fazenda Completa Gerada com Sucesso!");

        // Auto-destruição para limpar o script após a execução única
        string scriptPath = "Assets/Editor/FarmSceneBuilder.cs";
        if (File.Exists(scriptPath))
        {
            File.Delete(scriptPath);
            File.Delete(scriptPath + ".meta");
            AssetDatabase.Refresh();
        }
    }

    private static void BuildFenceRect(Vector3 min, Vector3 max, GameObject fencePrefab, GameObject gatePrefab, Vector3 gatePos, Transform parent, float fenceLength)
    {
        if (fencePrefab == null) return;
        BuildFenceLine(new Vector3(min.x, 0f, min.z), new Vector3(max.x, 0f, min.z), fencePrefab, gatePrefab, gatePos, parent, fenceLength);
        BuildFenceLine(new Vector3(max.x, 0f, min.z), new Vector3(max.x, 0f, max.z), fencePrefab, gatePrefab, gatePos, parent, fenceLength);
        BuildFenceLine(new Vector3(max.x, 0f, max.z), new Vector3(min.x, 0f, max.z), fencePrefab, gatePrefab, gatePos, parent, fenceLength);
        BuildFenceLine(new Vector3(min.x, 0f, max.z), new Vector3(min.x, 0f, min.z), fencePrefab, gatePrefab, gatePos, parent, fenceLength);
    }

    private static void BuildFenceLine(Vector3 start, Vector3 end, GameObject fencePrefab, GameObject gatePrefab, Vector3 gatePos, Transform parent, float fenceLength)
    {
        float distance = Vector3.Distance(start, end);
        Vector3 direction = (end - start).normalized;
        int count = Mathf.RoundToInt(distance / fenceLength);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + direction * (i * fenceLength + fenceLength / 2f);

            if (gatePrefab != null && Vector3.Distance(pos, gatePos) < fenceLength)
            {
                if (GameObject.Find($"Gate_{gatePos}") == null)
                {
                    GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, parent);
                    gate.name = $"Gate_{gatePos}";
                    gate.transform.position = pos;
                    gate.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            else
            {
                GameObject fence = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, parent);
                fence.transform.position = pos;
                fence.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}
