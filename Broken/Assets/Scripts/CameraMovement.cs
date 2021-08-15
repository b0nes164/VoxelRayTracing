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

    void Update()
    {
        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x - sensitivity * Time.deltaTime, 0, 80), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z - sensitivity * Time.deltaTime, 0, 80));
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x + sensitivity * Time.deltaTime, 0, 80), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z + sensitivity * Time.deltaTime, 0, 80));
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x + sensitivity * Time.deltaTime, 0, 80), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z - sensitivity * Time.deltaTime, 0, 80));
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x - sensitivity * Time.deltaTime, 0, 80), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z + sensitivity * Time.deltaTime, 0, 80));
        }

        if (Input.GetKey(KeyCode.Keypad2))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomSense, .1f, 40);
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSense, .1f, 40);
        }


    }
}


