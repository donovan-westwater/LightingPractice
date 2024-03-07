using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightOrbit : MonoBehaviour
{
    public Light l;
    public float speed = 20f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        l.transform.RotateAround(this.transform.position,this.transform.up, speed * Time.deltaTime);   
    }
}
