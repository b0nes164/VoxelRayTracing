using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraMovement : MonoBehaviour
{
    [SerializeField]
    private Camera cam;


    [SerializeField]
    private float sensitivity;

    [SerializeField]
    private float zoomSense;

    [SerializeField]
    private Text text;

    private float frameRate;
    private float time;
    private string debugText;

    void Update()
    {
        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            cam.transform.position -= new Vector3(sensitivity * Time.deltaTime, 0, sensitivity * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            cam.transform.position += new Vector3(sensitivity * Time.deltaTime, 0, sensitivity * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            cam.transform.position += new Vector3(sensitivity * Time.deltaTime, 0, -sensitivity * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            cam.transform.position += new Vector3(-sensitivity * Time.deltaTime, 0, sensitivity * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.Keypad2))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomSense, .1f, 40);
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSense, .1f, 40);
        }

        if (time > 1f)
        {
            frameRate = (int)(1f / Time.unscaledDeltaTime);
            time = 0;
        }
        else
        {
            time += Time.deltaTime;
        }

        //text.text = frameRate + ", " + cam.transform.position.x + ", " + cam.transform.position.z;


    }
}


