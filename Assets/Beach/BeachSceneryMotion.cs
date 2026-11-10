using UnityEngine;

public class BeachSceneryMotion : MonoBehaviour
{
    public Transform beacon;
    public Transform boat;
    Vector3 boatOrigin;
    void Start(){if(boat)boatOrigin=boat.localPosition;}
    void Update()
    {
        if(beacon)beacon.localRotation=Quaternion.Euler(0,Time.time*22,0);
        if(boat)
        {
            boat.localPosition=boatOrigin+Vector3.up*(Mathf.Sin(Time.time*.8f)*.008f);
            boat.localRotation=Quaternion.Euler(0,-25,Mathf.Sin(Time.time*1.1f)*2);
        }
    }
}
