using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CrackedHomeMirror : MonoBehaviour
{
    public Renderer surface;
    public Camera viewer;
    public RenderTexture Reflection { get; private set; }
    public int RenderCount { get; private set; }
    Camera reflectionCamera;
    MaterialPropertyBlock properties;
    float nextRender;
    bool rendering;
    void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;}
    void BeforeCamera(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=viewer || rendering || Time.unscaledTime<nextRender)return;
        if(Vector3.Distance(camera.transform.position,transform.position)>9)return;
        if(Vector3.Dot(transform.forward,camera.transform.position-transform.position)<.05f)return;
        RenderReflection(context,camera);nextRender=Time.unscaledTime+.066f;
    }
    public void RenderReflection(ScriptableRenderContext context,Camera camera)
    {
        if(surface==null || rendering)return;
        if(reflectionCamera==null)
        {
            var go=new GameObject("Camera do espelho"){hideFlags=HideFlags.HideAndDontSave};
            reflectionCamera=go.AddComponent<Camera>();reflectionCamera.enabled=false;
            var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderShadows=true;data.renderPostProcessing=false;
            Reflection=new RenderTexture(768,768,24){name="Reflexo da casa",filterMode=FilterMode.Bilinear};Reflection.Create();
            properties=new MaterialPropertyBlock();
        }
        Vector3 n=transform.forward,p=transform.position;float d=-Vector3.Dot(n,p);
        var plane=new Vector4(n.x,n.y,n.z,d);Matrix4x4 r=Matrix4x4.identity;
        for(int row=0;row<3;row++)for(int col=0;col<4;col++)r[row,col]-=2*plane[row]*plane[col];
        reflectionCamera.CopyFrom(camera);reflectionCamera.enabled=false;
        reflectionCamera.targetTexture=Reflection;reflectionCamera.cullingMask=(camera.cullingMask | (1<<31)) & ~(1<<30);
        reflectionCamera.worldToCameraMatrix=camera.worldToCameraMatrix*r;
        reflectionCamera.transform.position=r.MultiplyPoint(camera.transform.position);
        reflectionCamera.transform.rotation=Quaternion.LookRotation(r.MultiplyVector(camera.transform.forward),r.MultiplyVector(camera.transform.up));
        var view=reflectionCamera.worldToCameraMatrix;
        Vector3 point=view.MultiplyPoint(p+n*.012f),normal=view.MultiplyVector(n).normalized;
        reflectionCamera.projectionMatrix=camera.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
        bool culling=GL.invertCulling;bool visible=surface.enabled;rendering=true;
        try
        {
            surface.enabled=false;GL.invertCulling=!culling;
            UniversalRenderPipeline.RenderSingleCamera(context,reflectionCamera);
            surface.GetPropertyBlock(properties);properties.SetTexture("_Reflection",Reflection);surface.SetPropertyBlock(properties);RenderCount++;
        }
        finally{surface.enabled=visible;GL.invertCulling=culling;rendering=false;}
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering-=BeforeCamera;
        if(reflectionCamera!=null)Destroy(reflectionCamera.gameObject);
        if(Reflection!=null){Reflection.Release();Destroy(Reflection);}
    }
}
