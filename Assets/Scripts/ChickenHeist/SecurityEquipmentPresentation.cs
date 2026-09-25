using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Geometry is authored in metres, independently of legacy primitive scales.
public static class SecurityEquipmentVisual
{
    public static Material Material(string name, Color color, bool glow=false)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.45f);
        if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}
        return m;
    }
    public static GameObject Part(Transform root,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material,Quaternion? rotation=null)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);
        go.transform.localPosition=p;go.transform.localScale=scale;go.transform.localRotation=rotation??Quaternion.identity;
        go.GetComponent<Renderer>().sharedMaterial=material;
        var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
        return go;
    }
    public static void Rod(Transform root,string name,Vector3 a,Vector3 b,float diameter,Material material)
    {Part(root,name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(diameter,Vector3.Distance(a,b)*.5f,diameter),material,Quaternion.FromToRotation(Vector3.up,b-a));}
}

[RequireComponent(typeof(SecurityCamera))]
public class SecurityEquipmentPresentation : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    SecurityCamera cameraUnit;
    Transform head;
    Renderer lens,led;
    Mesh field;
    Mesh hood;
    MeshRenderer fieldRenderer;
    Material coating,glass;
    MaterialPropertyBlock stateBlock;
    float nextField;
    const int Samples=64;
    readonly Vector3[] vertices=new Vector3[Samples+2];
    readonly int[] triangles=new int[Samples*3];
    Renderer legacyRenderer;Collider legacyCollider;Renderer[] equipmentRenderers;
    Material Mat(string name,Color color,bool glow=false){var m=SecurityEquipmentVisual.Material(name,color,glow);materials.Add(m);return m;}
    void Awake()
    {
        stateBlock=new MaterialPropertyBlock();
        cameraUnit=GetComponent<SecurityCamera>();
        var old=GetComponent<Renderer>();if(old!=null)old.enabled=false;
        var oldCollider=GetComponent<Collider>();if(oldCollider!=null)oldCollider.enabled=false;
        transform.localScale=Vector3.one;
        var body=Mat("Camera - aluminium ivory",new Color(.73f,.75f,.69f));
        var rubber=Mat("Camera - seals and joint",new Color(.055f,.066f,.064f));
        glass=Mat("Camera - recessed optical glass",new Color(.025f,.10f,.13f));glass.SetFloat("_Smoothness",.96f);
        coating=Mat("Camera - spray coating",new Color(.25f,.12f,.09f));
        var light=Mat("Camera - status LED",Color.green,true);
        SecurityEquipmentVisual.Part(transform,"Mount plate",PrimitiveType.Cylinder,new Vector3(0,-.24f,.11f),new Vector3(.20f,.025f,.20f),body,Quaternion.Euler(90,0,0));
        SecurityEquipmentVisual.Rod(transform,"Mount arm",new Vector3(0,-.24f,.12f),new Vector3(0,-.10f,.38f),.06f,body);
        SecurityEquipmentVisual.Part(transform,"Ball joint",PrimitiveType.Sphere,new Vector3(0,-.09f,.38f),Vector3.one*.13f,rubber);
        head=new GameObject("Swivelling camera head").transform;head.SetParent(transform,false);head.localPosition=new Vector3(0,0,.38f);cameraUnit.scanHead=head;
        SecurityEquipmentVisual.Part(head,"Rounded weatherproof housing",PrimitiveType.Capsule,Vector3.zero,new Vector3(.27f,.27f,.27f),body,Quaternion.Euler(90,0,0));
        CreateHood(body);
        SecurityEquipmentVisual.Part(head,"Lens recess",PrimitiveType.Cylinder,new Vector3(0,0,.262f),new Vector3(.23f,.025f,.23f),rubber,Quaternion.Euler(90,0,0));
        lens=SecurityEquipmentVisual.Part(head,"Optical glass",PrimitiveType.Cylinder,new Vector3(0,0,.289f),new Vector3(.145f,.004f,.145f),glass,Quaternion.Euler(90,0,0)).GetComponent<Renderer>();
        SecurityEquipmentVisual.Part(head,"Inner optic",PrimitiveType.Sphere,new Vector3(0,0,.292f),new Vector3(.072f,.072f,.012f),rubber);
        for(int i=0;i<8;i++)
        {float a=i*Mathf.PI/4;SecurityEquipmentVisual.Part(head,"Infrared emitter",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.092f,Mathf.Sin(a)*.092f,.291f),Vector3.one*.018f,glass);}
        led=SecurityEquipmentVisual.Part(head,"Status LED",PrimitiveType.Sphere,new Vector3(.11f,-.05f,.286f),Vector3.one*.024f,light).GetComponent<Renderer>();
        var hit=head.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,0,.03f);hit.size=new Vector3(.32f,.27f,.60f);
        var overlay=new GameObject("Detection footprint");overlay.transform.SetParent(transform,false);
        field=new Mesh{name="Occluded camera footprint"};field.MarkDynamic();overlay.AddComponent<MeshFilter>().sharedMesh=field;
        fieldRenderer=overlay.AddComponent<MeshRenderer>();var fm=Mat("Security field",new Color(.2f,.65f,.46f,.14f),true);
        fm.SetFloat("_Surface",1);fm.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);fm.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        fm.SetFloat("_ZWrite",0);fm.SetFloat("_Cull",0);fm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");fm.renderQueue=3000;
        fieldRenderer.sharedMaterial=fm;fieldRenderer.shadowCastingMode=ShadowCastingMode.Off;fieldRenderer.receiveShadows=false;
        legacyRenderer=old;legacyCollider=oldCollider;equipmentRenderers=GetComponentsInChildren<Renderer>();
    }
    void LateUpdate()
    {
        // The obsolete root cube is never shown by progression/checkpoint refreshes.
        if(legacyRenderer!=null)legacyRenderer.enabled=false;
        if(legacyCollider!=null)legacyCollider.enabled=false;
        bool installed=FarmSecurityProgression.Installed;
        foreach(var r in equipmentRenderers)if(r!=legacyRenderer && r!=fieldRenderer)r.enabled=installed;
        bool painted=cameraUnit.PaintSecondsRemaining>0;
        lens.sharedMaterial=painted?coating:glass;
        Color c=painted?new Color(.72f,.39f,.15f):!cameraUnit.Operational?Color.gray:cameraUnit.Detecting?new Color(1,.14f,.08f):new Color(.2f,.9f,.5f);
        stateBlock.SetColor("_BaseColor",c);stateBlock.SetColor("_EmissionColor",c*2);led.SetPropertyBlock(stateBlock);
        fieldRenderer.enabled=installed && cameraUnit.Operational;
        if(!fieldRenderer.enabled || GameMenu.BlocksInput)return;
        c.a=.13f;fieldRenderer.sharedMaterial.SetColor("_BaseColor",c);fieldRenderer.sharedMaterial.SetColor("_EmissionColor",new Color(c.r,c.g,c.b)*.25f);
        if(Time.time<nextField)return;nextField=Time.time+.12f;
        Vector3 origin=cameraUnit.Eye;origin.y=.045f;vertices[0]=transform.InverseTransformPoint(origin);
        float targetHeight=cameraUnit.player!=null?cameraUnit.player.position.y+(cameraUnit.player.GetComponent<PlayerMovement>()?.estaAgachado==true?.5f:1f):1f;
        for(int i=0;i<=Samples;i++)
        {
            var direction=Quaternion.Euler(0,Mathf.Lerp(-cameraUnit.viewAngle,cameraUnit.viewAngle,(float)i/Samples),0)*head.forward;
            direction.y=0;direction.Normalize();float distance=cameraUnit.ClearRange(direction,targetHeight);
            vertices[i+1]=transform.InverseTransformPoint(origin+direction*distance);
            if(i<Samples){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        }
        field.Clear();field.vertices=vertices;field.triangles=triangles;field.RecalculateNormals();field.RecalculateBounds();
    }
    void CreateHood(Material material)
    {
        const int steps=24;var v=new Vector3[(steps+1)*4];var t=new List<int>();
        for(int i=0;i<=steps;i++)
        {
            float angle=Mathf.Lerp(-.12f,Mathf.PI+.12f,(float)i/steps);
            for(int k=0;k<4;k++){float radius=k<2?.165f:.154f;v[i*4+k]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,(k%2==0?-.20f:.33f));}
            if(i==steps)continue;int a=i*4,b=(i+1)*4;
            t.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+2,a+3,b+2,a+3,b+3,b+2,a+1,b+1,a+3,a+3,b+1,b+3});
        }
        hood=new Mesh{name="Open-ended curved rain shield"};hood.vertices=v;hood.triangles=t.ToArray();hood.RecalculateNormals();
        var go=new GameObject("Sun and rain shield");go.transform.SetParent(head,false);go.AddComponent<MeshFilter>().sharedMesh=hood;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void OnDestroy(){if(field!=null)Destroy(field);if(hood!=null)Destroy(hood);foreach(var m in materials)if(m!=null)Destroy(m);}
}
