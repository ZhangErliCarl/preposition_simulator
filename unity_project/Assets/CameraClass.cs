using UnityEngine;
using System.Collections;
using System;

public class CameraClass : MonoBehaviour
{

    public GameObject target;//the target object
    private float speedMod = 100.0f;//a speed modifier
    private float timer = 0.0f;
    private Vector3 point;//the coord to the point where the camera looks at

    void Start()
    {//Set up things on the start method
        point = target.transform.position;//get target's coords
        transform.LookAt(point);//makes the camera look to it
    }

    void Update()
    {//makes the camera rotate around "point" coords, rotating around its Y axis, 20 degrees per second times the speed modifier
        //transform.RotateAround(point, new Vector3(Random.Range(-1.0f,0f)*10, Random.Range(-1.0f, 0f)*10, 0), 1 * Time.deltaTime * speedMod);
        transform.RotateAround(point, Vector3.up, 1 * Time.deltaTime * speedMod);
        timer += Time.deltaTime;
        Camera.main.fieldOfView = 5 + 12.5f * (float)Math.Floor(timer / (0.45f * 8) %5);
       
    }
}