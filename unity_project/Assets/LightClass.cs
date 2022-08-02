using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightClass : MonoBehaviour
{
    private float timer = 0.0f;
    // Start is called before the first frame update
    Light myLight;
    void Start()
    {
        myLight = GetComponent<Light>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        myLight.intensity = 0.5f + 0.25f * (float)Math.Floor(timer / (0.45f * 40));
    }
}
