using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EffectAssets
{
    const string Folder="Assets/Resources/Effects";
    public static void Install()
    {
        Directory.CreateDirectory(Folder);
        string texturePath=Folder+"/SprayPuff.png";
        var t=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/32f;float a=Mathf.Clamp01(1-d);t.SetPixel(x,y,new Color(1,1,1,a*a*(3-2*a)));}
        File.WriteAllBytes(texturePath,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(texturePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        string materialPath=Folder+"/SprayMist.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
        material.shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
    }
}
