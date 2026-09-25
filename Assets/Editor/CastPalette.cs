using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Present-day rural clothing for the Medieval People pack. The pack colours every garment from
// horizontal bands of a 64x64 atlas, so each variant repaints whole bands. Bands are shared across
// garments (e.g. peasant_5's beard and peasant_2's dress), so every variant lists only bands that are
// safe for the models it is assigned to in UniformCastUpgrade.
public static class CastPalette
{
    const string Source="Assets/ImportedMedievalPeople/texture/people_texture_map.png";
    const string Folder="Assets/ChickenHeistGenerated/Characters/Villagers";
    // Band rows counted from the top of the atlas: [first,last).
    public static readonly Dictionary<string,(int top,int bottom)> Bands=new Dictionary<string,(int,int)>
    {
        {"white",(7,11)},{"skin",(11,15)},{"skinLight",(15,19)},{"brownGray",(19,23)},{"tan",(23,28)},{"blue",(28,32)},
        {"green",(32,36)},{"red",(36,40)},{"yellow",(40,44)},{"slate",(44,49)},{"lightBrown",(49,54)},{"brightYellow",(54,58)},{"darkBrown",(58,64)}
    };
    public static readonly Dictionary<string,Dictionary<string,Color32>> Variants=new Dictionary<string,Dictionary<string,Color32>>
    {
        // Elias (peasant_3): brown hair, faded olive T-shirt, dark work trousers (the back of the hair shares the trousers band).
        {"elias",new Dictionary<string,Color32>{{"slate",new Color32(58,42,30,255)},{"lightBrown",new Color32(108,116,90,255)},{"darkBrown",new Color32(52,40,32,255)},{"white",new Color32(150,148,138,255)}}},
        // peasant_5 farmer: khaki work shirt, leather vest, jeans.
        {"farmerKhaki",new Dictionary<string,Color32>{{"brownGray",new Color32(62,80,112,255)},{"tan",new Color32(172,152,108,255)}}},
        // rich_citizzens_1 farmer: red flannel, olive trim, brown work trousers.
        {"flannel",new Dictionary<string,Color32>{{"red",new Color32(138,44,40,255)},{"green",new Color32(58,64,46,255)}}},
        // peasant_1 shopkeeper: green shop apron, grey trousers.
        {"shop",new Dictionary<string,Color32>{{"tan",new Color32(70,98,74,255)},{"darkBrown",new Color32(72,72,76,255)}}},
        // peasant_2 / peasant_6: faded blue cotton dress or denim skirt.
        {"dress",new Dictionary<string,Color32>{{"lightBrown",new Color32(98,116,146,255)}}},
    };

    public static Material Material(string variant)
    {
        string texturePath=Folder+"/MedievalAtlas-"+variant+".png",materialPath=Folder+"/MedievalAtlas-"+variant+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var atlas=new Texture2D(2,2);atlas.LoadImage(File.ReadAllBytes(Source));
        var pixels=atlas.GetPixels32();int w=atlas.width,h=atlas.height;
        foreach(var band in Variants[variant])
        {
            var rows=Bands[band.Key];
            for(int top=rows.top;top<rows.bottom;top++)for(int x=0;x<w;x++)pixels[(h-1-top)*w+x]=band.Value;
        }
        atlas.SetPixels32(pixels);File.WriteAllBytes(texturePath,atlas.EncodeToPNG());Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(texturePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Medieval atlas "+variant};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetFloat("_Smoothness",.07f);
        AssetDatabase.CreateAsset(material,materialPath);
        return material;
    }
}
