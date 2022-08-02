using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Screenshooter : MonoBehaviour
{
    private bool ssEnabled = true;
    private bool loopEnabled = true;
    private string savePath = "F:/3D object/flower_data";
    System.IO.StreamWriter objWriter;
    private string file_name ="dataset.txt";
    GameObject forData;
    
    

    void Start()
    {
        objWriter = new System.IO.StreamWriter(savePath +"\\"+ file_name, true);
        if (GetEnvironmentVariable("IMGEN_TAKE_SCREENSHOTS", "") == "1")
        {
            ssEnabled = true;
        }
        if (GetEnvironmentVariable("IMGEN_LOOP", "") == "1")
        {
            loopEnabled = true;
        }
        //savePath = GetEnvironmentVariable(
        //    "IMGEN_SCREENSHOT_PATH",
        //    System.Environment.CurrentDirectory
        //);

        Debug.Log("Take screenshots = " + ssEnabled);
        Debug.Log("Loop mode = " + loopEnabled);
        Debug.Log("Screenshot directory = " + savePath);

        if (loopEnabled)
        {
            StartCoroutine(Loop());
        }
    }
    private void OnApplicationQuit()
    {
        Debug.Log("Quit");
        objWriter.Close();
    }

    private string GetEnvironmentVariable(string key, string defaultValue)
    {
        var envs = Environment.GetEnvironmentVariables();
        if (envs.Contains(key))
        {
            return envs[key].ToString();
        }
        return defaultValue;
    }

    private IEnumerator Loop()
    {
        for (var i = 0; i < 200; i++)
        {
            Debug.Log("Iteration " + i);
            if (ssEnabled)
            {
                CaptureNamedScreenshot(i);
            }
            Debug.Log("Done.");
            yield return new WaitForSeconds(0.45f);
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CaptureNamedScreenshot(int index)
    {
        var name = GetFilename(index);
        Debug.Log("Writing screenshot: " + name);
        ScreenCapture.CaptureScreenshot(name);
        objWriter.WriteLine(name + "," + gameObject.GetComponent<Transform>().localEulerAngles.x + "," + gameObject.GetComponent<Transform>().localEulerAngles.y + "," + gameObject.GetComponent<Transform>().localEulerAngles.z);
    }

    private string GetFilename(int index)
    {
        return string.Format(
            "{0}/dataset/Input_dataset/cellular_telephone/cellular_telephone_{1}.jpg",
            savePath,
            index
        );
    }
}
