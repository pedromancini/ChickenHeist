using System.Collections.Generic;
using UnityEngine;

public class HomeFlockView : MonoBehaviour
{
    public GameObject chickenPrefab;
    public Vector3 DeliveryPoint=>transform.TransformPoint(new Vector3(-1,0,-2.7f));
    readonly List<GameObject> birds=new List<GameObject>();
    int visible=-1;
    bool delivering;Vector3 deliveryOrigin;
    public void AnimateDelivery(Vector3 origin){delivering=true;deliveryOrigin=origin;}
    System.Collections.IEnumerator Transfer(Transform bird,Vector3 target,float delay)
    {
        var motion=bird.GetComponent<FarmAnimalMotion>();if(motion!=null)motion.enabled=false;
        bird.position=deliveryOrigin;
        yield return new WaitForSeconds(delay);
        if(bird==null)yield break;
        Vector3 start=bird.position;
        for(float t=0;t<1;t+=Time.deltaTime/.9f){if(bird==null)yield break;bird.position=Vector3.Lerp(start,target,Mathf.SmoothStep(0,1,t))+Vector3.up*Mathf.Sin(t*Mathf.PI)*.15f;yield return null;}
        if(bird!=null){bird.position=target;if(motion!=null)motion.enabled=true;}
    }
    void Update()
    {
        int desired=Mathf.Min(12,HouseholdEconomy.Instance?.Account.flock??0);
        if(desired==visible || chickenPrefab==null)return;
        while(birds.Count>desired){Destroy(birds[birds.Count-1]);birds.RemoveAt(birds.Count-1);}
        int previous=birds.Count;visible=desired;
        for(int i=previous;i<desired;i++)
        {
            Vector3 position=i<8?new Vector3(-2.1f+(i%4)*1.38f,.03f,-1.5f+(i/4)*.78f):new Vector3(i%2==0?-2.2f:2.2f,.03f,.5f+(i-8)/2*.82f);
            var bird=Instantiate(chickenPrefab,transform);bird.name="Galinha do sitio "+(i+1);
            Bounds bounds=ProceduralFarmGenerator.VisualBounds(bird);bird.transform.localScale*=.44f/Mathf.Max(.01f,bounds.size.y);
            bird.transform.localRotation=Quaternion.Euler(0,i*73,0);bounds=ProceduralFarmGenerator.VisualBounds(bird);
            Vector3 target=transform.TransformPoint(position);bird.transform.position+=new Vector3(target.x-bounds.center.x,target.y-bounds.min.y,target.z-bounds.center.z);
            foreach(var collider in bird.GetComponentsInChildren<Collider>())Destroy(collider);
            if(bird.GetComponent<FarmAnimalMotion>()==null)bird.AddComponent<FarmAnimalMotion>();
            birds.Add(bird);
            if(delivering)StartCoroutine(Transfer(bird.transform,bird.transform.position,(i-previous)*.12f));
        }
        delivering=false;
    }
}
