using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class RuralWorldReview : ScriptableObject
{
    public const string WorldScene = "Assets/Scenes/ChickenHeistRuralWorld.unity";
    private const string ReviewFolder = "output/world-review";

    [InitializeOnLoadMethod]
    private static void ConsumeReviewRequest()
    {
        // An explicit, one-shot request allows the same review to run without UI input.
        EditorApplication.delayCall += () =>
        {
            const string request = "Temp/RuralWorldReview.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string action = File.ReadAllText(request).Trim();
            File.Delete(request);
            if (action == "build") Build();
            else if (action == "review") AuditAndCapture();
            else if (action == "build-test") { Build(); RuralWorldSmokeTest.Run(); }
            else if (action == "roads") RuralRoadRepair.Repair();
            else if (action == "home-preview") PlayerHomeBuilder.Preview();
            else if (action == "home") PlayerHomeBuilder.Build();
            else if (action == "home-finish") PlayerHomeBuilder.Finish();
            else if (action == "interior-inspect") PlayerHomeBuilder.InspectInterior();
            else if (action == "home-interior") PlayerHomeBuilder.BuildInterior();
            else if (action == "interior-review") PlayerHomeBuilder.CaptureInterior();
            else if (action == "interior-test") HomeInteriorTests.Run();
            else if (action == "interior-build-test") { PlayerHomeBuilder.BuildInterior(); HomeInteriorTests.Run(); }
            else if (action == "interior-furniture") PlayerHomeBuilder.PreviewFurniture();
            else if (action == "interior-probe") HomeInteriorTests.ProbePassage();
            else if (action == "interior-finish") PlayerHomeBuilder.FinishInterior();
            else if (action == "furniture-layout") PlayerHomeBuilder.ArrangeFurniture();
            else if (action == "home-fixtures") PlayerHomeBuilder.FinishFixtures();
            else if (action == "trees-inspect") RuralTreeReview.Inspect();
            else if (action == "trees-repair") RuralTreeReview.Repair();
            else if (action == "playable-market") PlayerHomeBuilder.BuildMarket();
            else if (action == "villager-inspect") PlayerHomeBuilder.InspectVillagers();
            else if (action == "villager-populate")
            {
                VillagerPopulation.Build();
                SessionState.SetBool("ChickenHeist.PlayableVillageTests.build",true);
                PlayableVillageTests.Run();
            }
            else if (action == "hidden-market-release")
            {
                PlayerHomeBuilder.BuildMarket();
                SessionState.SetBool("ChickenHeist.PlayableVillageTests.build",true);
                PlayableVillageTests.Run();
            }
            else if (action == "playable-test") {PlayerHomeBuilder.BuildMarket();PlayableVillageTests.Run();}
            else if (action == "playable-release")
            {
                SessionState.SetBool("ChickenHeist.PlayableVillageTests.build",true);
                PlayableVillageTests.Run();
            }
        };
    }

    [MenuItem("Chicken Heist/Build Rural World and Review")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before building.");
        Directory.CreateDirectory(ReviewFolder);
        Scene active = SceneManager.GetActiveScene();
        if (active.isDirty)
            EditorSceneManager.SaveScene(active, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeRuralLayout.unity"), true);
        ChickenHeistSceneBuilder.BuildScene(WorldScene);
        AuditAndCapture();
    }

    public static void PersistGeneratedAssets(Scene scene)
    {
        if (!AssetDatabase.IsValidFolder("Assets/ChickenHeistGenerated/World"))
            AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated", "World");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/ChickenHeistGenerated/World/RuralWorldResources.asset");
        var container = CreateInstance<RuralWorldReview>();
        AssetDatabase.CreateAsset(container, path);
        var saved = new HashSet<Object>();
        var converted = new Dictionary<Material, Material>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (source.shader != null && (source.shader.name == "Standard" || source.shader.name.StartsWith("Legacy Shaders/")))
                    {
                        if (!converted.TryGetValue(source, out Material replacement))
                        {
                            replacement = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            replacement.name = source.name + " Rural URP";
                            replacement.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
                            if (source.HasProperty("_MainTex")) replacement.SetTexture("_BaseMap", source.mainTexture);
                            replacement.SetFloat("_Smoothness", 0.15f);
                            converted.Add(source, replacement);
                        }
                        materials[i] = replacement;
                    }
                    if (!AssetDatabase.Contains(materials[i])) materials[i].enableInstancing = true;
                    SaveSubAsset(materials[i], path, saved);
                }
                renderer.sharedMaterials = materials;
            }
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true)) SaveSubAsset(mf.sharedMesh, path, saved);
            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true)) SaveSubAsset(mc.sharedMesh, path, saved);
        }
        SaveSubAsset(RenderSettings.skybox, path, saved);
        AssetDatabase.SaveAssets();
    }

    private static void SaveSubAsset(Object asset, string path, HashSet<Object> saved)
    {
        if (asset == null || AssetDatabase.Contains(asset) || !saved.Add(asset)) return;
        AssetDatabase.AddObjectToAsset(asset, path);
    }

    [MenuItem("Chicken Heist/Review Rural World")]
    public static void AuditAndCapture()
    {
        // This method is also used from batch-mode release checks.  In that
        // context Unity opens an empty bootstrap scene, so auditing before the
        // rural scene is loaded used to report a false material failure and
        // then crash while looking for the world lights.
        if (SceneManager.GetActiveScene().path != WorldScene)
            EditorSceneManager.OpenScene(WorldScene);
        Directory.CreateDirectory(ReviewFolder);
        var farms = Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f => f.layoutIndex).ToArray();
        var lines = new List<string>();
        int failures = 0;
        foreach (var farm in farms)
        {
            int houses = 0, barns = 0, silos = 0;
            foreach (Transform child in farm.transform)
            {
                if (child.name.StartsWith("Casa -")) { houses++; CheckBuilding(child.gameObject, farm.zones[0], lines, ref failures); }
                if (child.name.StartsWith("Celeiro -")) { barns++; CheckBuilding(child.gameObject, farm.zones[1], lines, ref failures); }
                if (child.name.StartsWith("Silo -")) { silos++; CheckBuilding(child.gameObject, farm.zones[5], lines, ref failures); }
            }
            int chickens = farm.GetComponentsInChildren<InteractableChicken>().Length;
            int cows = farm.GetComponentsInChildren<SimpleAnimalWander>().Length;
            int coops = farm.GetComponentsInChildren<ChickenCoopLockpick>().Length;
            if (houses != 1 || barns != 1 || silos != 1 || chickens != 8 || cows != 3 || coops != 1) failures++;
            for (int i = 0; i < farm.zones.Length; i++)
            for (int j = i+1; j < farm.zones.Length; j++) if (farm.zones[i].Overlaps(farm.zones[j])) failures++;
            foreach (var animal in farm.GetComponentsInChildren<FarmAnimalBoundary>())
            {
                var p = new Vector2(animal.transform.position.x, animal.transform.position.z);
                if (!animal.area.Contains(p) || animal.obstacles.Any(o => o.Contains(p))) { failures++; lines.Add("Animal intersects boundary/equipment: " + animal.name); }
            }
            var animals = farm.GetComponentsInChildren<FarmAnimalBoundary>();
            for (int i = 0; i < animals.Length; i++)
            for (int j = i + 1; j < animals.Length; j++)
            {
                Vector3 delta = animals[i].transform.position - animals[j].transform.position;
                delta.y = 0f;
                if (delta.magnitude < animals[i].radius + animals[j].radius - 0.01f)
                { failures++; lines.Add("Overlapping animals: " + farm.identity); }
            }
            MeshCollider terrain = GameObject.Find("Terreno Ondulado Low Poly").GetComponent<MeshCollider>();
            foreach (Transform t in farm.GetComponentsInChildren<Transform>())
            {
                if (!t.name.EndsWith(" - Encaixe")) continue;
                Bounds b = ProceduralFarmGenerator.VisualBounds(t.gameObject);
                if (b.size.y < 1.30f || b.size.y > 1.40f) { failures++; lines.Add("Fence height: " + t.name); }
                Ray ray = new Ray(new Vector3(b.center.x, 30f, b.center.z), Vector3.down);
                if (terrain.Raycast(ray, out RaycastHit hit, 60f) && Mathf.Abs(b.min.y - hit.point.y) > 0.08f)
                { failures++; lines.Add("Fence buried/floating: " + t.name); }
            }
            lines.Add(farm.identity + " | house="+houses+" barn="+barns+" silo="+silos+" coop="+coops+" chickens="+chickens+" cows="+cows);
        }
        if (farms.Length != 12) failures++;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (mf.sharedMesh == null || !AssetDatabase.Contains(mf.sharedMesh)) { failures++; lines.Add("Unsaved mesh: " + mf.name); }
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (Material mat in renderer.sharedMaterials)
                if (mat == null || mat.shader == null || !AssetDatabase.Contains(mat)) { failures++; lines.Add("Invalid material: " + renderer.name); }
        lines.Add("Farm count=" + farms.Length + " | failures=" + failures);
        File.WriteAllLines(ReviewFolder + "/layout-audit.txt", lines);
        Capture(farms);
        if (farms.Length > 0 && SceneView.lastActiveSceneView != null)
        {
            Vector3 center = new Vector3(farms[0].lot.center.x, 0, farms[0].lot.center.y);
            SceneView.lastActiveSceneView.LookAt(center, Quaternion.Euler(42, 0, 0), 75f);
            SceneView.lastActiveSceneView.showGrid = false;
            SceneView.lastActiveSceneView.Repaint();
        }
        Debug.Log("Rural world reviewed: " + failures + " failures. " + ReviewFolder);
    }

    private static void CheckBuilding(GameObject obj, Rect zone, List<string> lines, ref int failures)
    {
        Bounds b = ProceduralFarmGenerator.VisualBounds(obj);
        if (b.min.y < -0.04f || b.min.y > 0.08f || b.min.x < zone.xMin || b.max.x > zone.xMax || b.min.z < zone.yMin || b.max.z > zone.yMax)
        { failures++; lines.Add("Building fit failed: " + obj.name + " " + b); }
    }

    private static void Capture(FarmLayoutInfo[] farms)
    {
        var go = new GameObject("Review Camera");
        Camera camera = go.AddComponent<Camera>();
        camera.fieldOfView = 48f;
        camera.farClipPlane = 2500f;
        camera.nearClipPlane = 0.1f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.40f,0.55f,0.65f);
        bool fog = RenderSettings.fog;
        Color ambient = RenderSettings.ambientLight;
        var moonNode = GameObject.Find("Moon Light");
        var moon = moonNode != null ? moonNode.GetComponent<Light>() : null;
        float intensity = moon != null ? moon.intensity : 0f;
        Color moonColor = moon != null ? moon.color : Color.white;
        RenderSettings.fog = false;
        RenderSettings.ambientLight = new Color(0.55f,0.57f,0.60f);
        if (moon != null)
        {
            moon.intensity = 1.6f;
            moon.color = new Color(1f,0.94f,0.82f);
        }
        try
        {
            foreach (var farm in farms)
            {
                Vector3 center = new Vector3(farm.lot.center.x,0,farm.lot.center.y);
                float span = Mathf.Max(farm.lot.width, farm.lot.height);
                camera.transform.position = center + new Vector3(span*0.28f,span*0.94f,-span*1.00f);
                camera.transform.LookAt(center);
                SaveCamera(camera, ReviewFolder+"/farm-"+(farm.layoutIndex+1).ToString("00")+".png");
            }
            foreach (var farm in farms.Take(3))
            {
                ChickenCoopLockpick coop = farm.GetComponentInChildren<ChickenCoopLockpick>();
                Vector3 door = coop.transform.position;
                camera.transform.position = door + new Vector3(8f, 2.4f, -13f);
                camera.transform.LookAt(door + Vector3.up * 0.8f);
                SaveCamera(camera, ReviewFolder + "/coop-" + (farm.layoutIndex + 1).ToString("00") + ".png");
            }
            if (farms.Length > 0)
            {
                Vector3 entrance = farms[0].entrance;
                camera.transform.position = entrance + new Vector3(5f, 3.2f, -16f);
                camera.transform.LookAt(entrance + Vector3.forward * 6f);
                SaveCamera(camera, ReviewFolder + "/dirt-road.png");
                for (int angle=0;angle<4;angle++)
                {
                    camera.transform.position=entrance+Quaternion.Euler(0,angle*20f-30f,0)*new Vector3(0,9f,-15f);
                    camera.transform.LookAt(entrance);
                    SaveCamera(camera,ReviewFolder+"/junction-"+angle+".png");
                }
            }
            camera.orthographic = true;
            camera.orthographicSize = 570f;
            camera.transform.position = new Vector3(0,1300,-250);
            camera.transform.LookAt(Vector3.zero);
            SaveCamera(camera, ReviewFolder+"/world.png");
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderSettings.ambientLight = ambient;
            if (moon != null)
            {
                moon.intensity = intensity;
                moon.color = moonColor;
            }
            Object.DestroyImmediate(go);
        }
    }

    private static void SaveCamera(Camera camera, string path)
    {
        var rt = new RenderTexture(1440,1080,24);
        var old = RenderTexture.active;
        var texture = new Texture2D(1440,1080,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,1440,1080),0,0);
            texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = old;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
