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

    private Cross cross;
    private int xMax;
    private int yMax;
    private int zMax;
    private readonly int camMax = 456;


    public CameraMovement(Camera _cam, Text _text, Cross _cross, float _camSens, float _zoomSens, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width)
    {
        cam = _cam;
        text = _text;
        camSens = _camSens;
        zoomSens = _zoomSens;

        xMax = (_xChunks * _length) - 1;
        zMax = (_zChunks * _width) - 1;
        yMax = (_yChunks * _height) - 1;

        cross = _cross;
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

        if (Input.GetKey(KeyCode.KeypadPlus))
        {
            cam.transform.position = new Vector3(cam.transform.position.x, Mathf.Clamp(cam.transform.position.y + camSens * Time.deltaTime, cross.Height, camMax), cam.transform.position.z);
        }

        if (Input.GetKey(KeyCode.KeypadMinus))
        {
            cam.transform.position = new Vector3(cam.transform.position.x, Mathf.Clamp(cam.transform.position.y - camSens * Time.deltaTime, cross.Height, camMax), cam.transform.position.z);
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            cross.Height = Mathf.Clamp(cross.Height - 1, 0, yMax);
        }

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            cross.Height = Mathf.Clamp(cross.Height + 1, 0, yMax);
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


