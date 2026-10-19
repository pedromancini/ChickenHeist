using UnityEngine;

// Elias' desk as the story has it: the overdue bills (PlayerHomeBuilder lays three sheets with four stripes each) get the
// printed electricity bill of the opening (Tools/make_bill_texture.py) on their top face, and the stripes go; the old
// cracked phone PlayerHomeBuilder leaves there goes too (Elias has no phone: the tablet comes in a box by the door).
public sealed class DeskBills : MonoBehaviour
{
    Material print;readonly System.Collections.Generic.List<Renderer> faces=new System.Collections.Generic.List<Renderer>(),phone=new System.Collections.Generic.List<Renderer>();
    public static DeskBills Ensure(Transform home)
    {
        var found=home.GetComponentInChildren<DeskBills>();if(found!=null)return found;
        var node=new GameObject("Contas impressas");node.transform.SetParent(home,false);return node.AddComponent<DeskBills>();
    }
    void Start()
    {
        var home=transform.parent;
        foreach(var r in home.GetComponentsInChildren<Renderer>(true))if(r.name=="Celular antigo - tela rachada" || r.name=="Tela" || r.name=="Vidro rachado")phone.Add(r);
        foreach(var dock in home.GetComponentsInChildren<HomePhoneDock>(true)){dock.enabled=false;foreach(var c in dock.GetComponentsInChildren<Collider>())c.enabled=false;}
        var printed=Resources.Load<Texture2D>("Cinematics/ContaDeEnergia");if(printed==null)return;
        print=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Conta de energia impressa"};
        print.SetColor("_BaseColor",Color.white);print.SetTexture("_BaseMap",printed);print.SetFloat("_Smoothness",.12f);
        foreach(var r in home.GetComponentsInChildren<Renderer>(true))
        {
            if(r.name=="Linha da cobranca"){r.forceRenderingOff=true;continue;}
            if(r.name!="Conta atrasada" || r.transform.Find("Conta impressa")!=null)continue;
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Conta impressa";Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(r.transform,false);face.transform.localPosition=new Vector3(0,.6f,0);face.transform.localRotation=Quaternion.Euler(90,0,180);
            face.GetComponent<Renderer>().sharedMaterial=print;faces.Add(face.GetComponent<Renderer>());
        }
    }
    // the opening hides the desk papers (it lays its own bill there): the prints go with them
    void LateUpdate()
    {
        foreach(var f in faces)if(f!=null)f.forceRenderingOff=StoryDirector.Active;
        // kept off every frame: the opening restores what it hid when it ends
        foreach(var r in phone)if(r!=null)r.forceRenderingOff=true;
    }
    void OnDestroy(){if(print!=null)Destroy(print);}
}
