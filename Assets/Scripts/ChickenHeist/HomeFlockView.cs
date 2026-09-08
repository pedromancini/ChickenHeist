using System.Collections.Generic;
using UnityEngine;

public class HomeFlockView : MonoBehaviour
{
    public GameObject chickenPrefab;
    readonly List<GameObject> birds=new List<GameObject>();
    int visible=-1;
    void Update()
    {
        int desired=Mathf.Min(12,HouseholdEconomy.Instance?.Account.flock??0);
        if(desired==visible || chickenPrefab==null)return;
        foreach(var bird in birds)Destroy(bird);birds.Clear();visible=desired;
        for(int i=0;i<desired;i++)
        {
            Vector3 position=i<8?new Vector3(-2.1f+(i%4)*1.38f,.03f,-1.5f+(i/4)*.78f):new Vector3(i%2==0?-2.2f:2.2f,.03f,.5f+(i-8)/2*.82f);
            var bird=Instantiate(chickenPrefab,transform);bird.name="Galinha do sitio "+(i+1);
            Bounds bounds=ProceduralFarmGenerator.VisualBounds(bird);bird.transform.localScale*=.44f/Mathf.Max(.01f,bounds.size.y);
            bird.transform.localRotation=Quaternion.Euler(0,i*73,0);bounds=ProceduralFarmGenerator.VisualBounds(bird);
            Vector3 target=transform.TransformPoint(position);bird.transform.position+=new Vector3(target.x-bounds.center.x,target.y-bounds.min.y,target.z-bounds.center.z);
            foreach(var collider in bird.GetComponentsInChildren<Collider>())Destroy(collider);
            birds.Add(bird);
        }
    }
}
