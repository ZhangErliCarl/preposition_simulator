using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lookat : MonoBehaviour
{
    public Transform target;
    // Start is called before the first frame update
    void Start()
    {
        GameObject teacher;
        teacher = GameObject.Find("Teacher");

        if (teacher != null) {
            Debug.Log("Teacher found");
            target = teacher.transform;
        }
        
        
    }

    // Update is called once per frame
    void Update()
    {
        if (target != null) {
            if (target.position.x != transform.position.x && target.position.z != transform.position.z) {
            
                transform.LookAt(target);
            }
        }
        
    }
}
