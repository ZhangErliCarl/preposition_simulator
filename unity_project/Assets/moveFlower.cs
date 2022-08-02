using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class moveFlower : MonoBehaviour
{
    public float rotationX = 14f;
    public float rotationZ = -10f;
    private float smooth = 5.0f;
    private Quaternion target;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            DetermineNewRotation();
            yield return new WaitForSeconds(1);
        }
    }
    private void DetermineNewRotation()
    {
        target = Quaternion.Euler(Random.Range(-360.0f, 360.0f), Random.Range(-360.0f, 360.0f), Random.Range(-360.0f, 360.0f));
    }


    // Update is called once per frame
    void Update()
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * smooth);
    }
}
