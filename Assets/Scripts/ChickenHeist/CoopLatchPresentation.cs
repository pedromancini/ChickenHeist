using UnityEngine;

[DefaultExecutionOrder(200)]
public class CoopLatchPresentation : MonoBehaviour
{
    public ChickenCoopLockpick owner;
    Transform root;readonly Transform[] pieces=new Transform[3];Renderer[] oldRenderers;
    readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
    AudioSource audioSource,friction;AudioClip click,slip,scrape;
    Camera eye;PlayerLook look;bool lookEnabled,view;Vector3 oldPosition,fromPosition;Quaternion oldRotation,fromRotation;float oldFov,oldNear,blend;Transform tool;Light fill;
    public bool CloseViewActive=>view;
    Renderer[] playerRenderers;bool[] renderingBefore;Vector3 focus;float viewDistance;
    public bool PiecesInFrame {get{if(eye==null)return false;foreach(var piece in pieces){var point=eye.WorldToViewportPoint(piece.position);if(point.z<=0 || point.x<.05f || point.x>.95f || point.y<.20f || point.y>.95f)return false;}return true;}}
    public void BeginView()
    {
        if(view || root==null || Camera.main==null)return;
        eye=Camera.main;oldPosition=eye.transform.localPosition;oldRotation=eye.transform.localRotation;oldFov=eye.fieldOfView;oldNear=eye.nearClipPlane;
        fromPosition=eye.transform.position;fromRotation=eye.transform.rotation;look=eye.GetComponentInParent<PlayerLook>();if(look==null)look=HeistGameManager.Instance.player.GetComponentInChildren<PlayerLook>();
        if(look!=null){lookEnabled=look.enabled;look.enabled=false;}
        view=true;blend=0;eye.nearClipPlane=.025f;
        playerRenderers=HeistGameManager.Instance.player.GetComponentsInChildren<Renderer>(true);renderingBefore=new bool[playerRenderers.Length];for(int i=0;i<playerRenderers.Length;i++){renderingBefore[i]=playerRenderers[i].forceRenderingOff;playerRenderers[i].forceRenderingOff=true;}
        var bounds=new Bounds(root.position,Vector3.zero);foreach(var renderer in root.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);focus=bounds.center;
        viewDistance=Mathf.Max(.8f,bounds.extents.magnitude/Mathf.Tan(21*Mathf.Deg2Rad)*1.35f);
        fill=new GameObject("Luz da ferramenta").AddComponent<Light>();fill.type=LightType.Point;fill.range=1.5f;fill.intensity=.7f;fill.shadows=LightShadows.None;fill.color=new Color(1,.86f,.66f);
    }
    public void EndView()
    {
        if(!view)return;view=false;
        if(eye!=null){eye.transform.localPosition=oldPosition;eye.transform.localRotation=oldRotation;eye.fieldOfView=oldFov;eye.nearClipPlane=oldNear;}
        if(look!=null)look.enabled=lookEnabled;if(fill!=null)Destroy(fill.gameObject);
        if(tool!=null)tool.gameObject.SetActive(false);
        if(playerRenderers!=null)for(int i=0;i<playerRenderers.Length;i++)if(playerRenderers[i]!=null)playerRenderers[i].forceRenderingOff=renderingBefore[i];
    }
    void LateUpdate()
    {
        if(!view || eye==null || root==null || GameMenu.BlocksInput || owner.Scare?.IsActive==true)return;
        blend=Mathf.Clamp01(blend+Time.deltaTime*4);
        Vector3 direction=(fromPosition-focus).normalized;
        Vector3 target=focus+direction*viewDistance;
        eye.transform.position=Vector3.Lerp(fromPosition,target,Mathf.SmoothStep(0,1,blend));
        eye.transform.rotation=Quaternion.Slerp(fromRotation,Quaternion.LookRotation(focus-eye.transform.position,Vector3.up),Mathf.SmoothStep(0,1,blend));eye.fieldOfView=Mathf.Lerp(oldFov,42,blend);
        fill.transform.position=eye.transform.position+eye.transform.right*.12f;
        tool.gameObject.SetActive(true);tool.localPosition=new Vector3(-.10f+owner.Latch.Travel(owner.Latch.Selected)*.13f,.105f-owner.Latch.Selected*.105f,-.105f);
        tool.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-18,12,owner.Latch.Pressure));
    }
    Material Mat(string name,Color color){var mat=SecurityEquipmentVisual.Material(name,color);materials.Add(mat);return mat;}
    void Start()
    {
        if(owner.lockAnchor==null)return;
        oldRenderers=owner.lockAnchor.GetComponentsInChildren<Renderer>();foreach(var r in oldRenderers)r.enabled=false;
        root=new GameObject("Trinco de tres pecas").transform;root.SetParent(transform,false);root.position=owner.InteractionPoint;
        root.rotation=owner.lockAnchor.rotation;root.localScale=new Vector3(1/transform.lossyScale.x,1/transform.lossyScale.y,1/transform.lossyScale.z);
        var rust=Mat("Trinco - ferro oxidado",new Color(.31f,.16f,.085f));var steel=Mat("Trinco - metal gasto",new Color(.42f,.39f,.32f));
        SecurityEquipmentVisual.Part(root,"Chapa",PrimitiveType.Cube,Vector3.zero,new Vector3(.34f,.34f,.035f),rust);
        for(int i=0;i<3;i++)
        {
            float y=.105f-i*.105f;
            SecurityEquipmentVisual.Part(root,"Guia",PrimitiveType.Cube,new Vector3(.04f,y,-.035f),new Vector3(.27f,.07f,.04f),rust);
            pieces[i]=SecurityEquipmentVisual.Part(root,"Peca "+(i+1),PrimitiveType.Cube,new Vector3(-.02f,y,-.062f),new Vector3(.23f,.035f,.04f),steel).transform;
            SecurityEquipmentVisual.Part(pieces[i],"Pino de apoio",PrimitiveType.Sphere,new Vector3(.25f,0,-.65f),new Vector3(.14f,.65f,.65f),steel);
        }
        audioSource=AudioNode("Estalos");audioSource.spatialBlend=1;audioSource.minDistance=1;audioSource.maxDistance=12;audioSource.playOnAwake=false;
        friction=AudioNode("Atrito");friction.spatialBlend=1;friction.minDistance=1;friction.maxDistance=5;friction.playOnAwake=false;friction.loop=true;friction.volume=0;
        click=Sound(false);slip=Sound(true);scrape=Scrape();friction.clip=scrape;
        tool=new GameObject("Ferramenta de tensao").transform;tool.SetParent(root,false);
        SecurityEquipmentVisual.Part(tool,"Lamina",PrimitiveType.Cube,new Vector3(-.10f,0,0),new Vector3(.22f,.012f,.012f),steel);
        SecurityEquipmentVisual.Part(tool,"Cabo",PrimitiveType.Capsule,new Vector3(-.26f,-.02f,0),new Vector3(.045f,.07f,.045f),Mat("Cabo gasto",new Color(.12f,.09f,.065f)),Quaternion.Euler(0,0,70));tool.gameObject.SetActive(false);
    }
    void Update()
    {
        if(root==null)return;root.gameObject.SetActive(!owner.IsOpen);foreach(var r in oldRenderers)if(r!=null)r.enabled=false;
        for(int i=0;i<3;i++)if(pieces[i]!=null)pieces[i].localPosition=new Vector3(-.02f+owner.Latch.Travel(i)*.13f,.105f-i*.105f,-.062f);
        if(!owner.ChallengeActive || owner.Scare?.IsActive==true)SetFriction(0);
    }
    public void Click(bool failed){if(audioSource!=null)audioSource.PlayOneShot(failed?slip:click,failed?.45f:.25f);}
    AudioSource AudioNode(string label){var node=new GameObject(label);node.transform.SetParent(transform,false);return node.AddComponent<AudioSource>();}
    public void SetFriction(float stress){if(friction==null)return;friction.volume=stress*.16f;friction.pitch=.8f+stress*.5f;if(stress>0 && !friction.isPlaying)friction.Play();else if(stress<=0)friction.Stop();}
    static AudioClip Sound(bool failed)
    {
        const int rate=22050;var samples=new float[(int)(rate*(failed?.32f:.12f))];
        for(int i=0;i<samples.Length;i++){float t=(float)i/rate;samples[i]=(Mathf.Sin(t*2450)+Mathf.Sin(t*6193)*.5f)*Mathf.Exp(-t*(failed?18:60))*.4f;}
        var clip=AudioClip.Create("Provisional latch click",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    static AudioClip Scrape(){var data=new float[4096];var r=new System.Random(42);for(int i=0;i<data.Length;i++)data[i]=((float)r.NextDouble()*2-1)*.3f;var c=AudioClip.Create("Provisional latch friction",data.Length,1,22050,false);c.SetData(data,0);return c;}
    void OnDestroy(){EndView();foreach(var m in materials)if(m!=null)Destroy(m);if(click!=null)Destroy(click);if(slip!=null)Destroy(slip);if(scrape!=null)Destroy(scrape);}
}
