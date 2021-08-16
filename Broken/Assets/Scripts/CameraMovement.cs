using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraMovement 
{
    private Camera cam;

    private Text text;

    private float camSens;

    private float zoomSens;

    private int xMax;
    private int zMax;

    public CameraMovement(Camera _cam, Text _text, float _camSens, float _zoomSens, int _xChunks, int _zChunks, int _length, int _width)
    {
        cam = _cam;
        text = _text;
        camSens = _camSens;
        zoomSens = _zoomSens;

        xMax = (_xChunks * _length) - 1;
        zMax = (_zChunks * _width) - 1;

    }

    public void MoveCam()
    {
        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x - camSens * Time.deltaTime, 0, xMax), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z - camSens * Time.deltaTime, 0, zMax));
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x + camSens * Time.deltaTime, 0, xMax), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z + camSens * Time.deltaTime, 0, zMax));
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x + camSens * Time.deltaTime, 0, xMax), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z - camSens * Time.deltaTime, 0, zMax));
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            cam.transform.position = new Vector3(Mathf.Clamp(cam.transform.position.x - camSens * Time.deltaTime, 0, xMax), cam.transform.position.y, Mathf.Clamp(cam.transform.position.z + camSens * Time.deltaTime, 0, zMax));
        }

        if (Input.GetKey(KeyCode.Keypad2))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomSens, .1f, 40);
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSens, .1f, 40);
        }


    }
}


