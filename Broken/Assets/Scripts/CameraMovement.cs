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
    private float deltaX = 32;
    private float deltaZ = 32;

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
            cross.X = Mathf.Clamp(cross.X - camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z - camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + deltaX, cam.transform.position.y, cross.Z + deltaZ);

        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            cross.X = Mathf.Clamp(cross.X + camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z + camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + deltaX, cam.transform.position.y, cross.Z + deltaZ);
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            cross.X = Mathf.Clamp(cross.X + camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z - camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + deltaX, cam.transform.position.y, cross.Z + deltaZ);
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            cross.X = Mathf.Clamp(cross.X - camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z + camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + deltaX, cam.transform.position.y, cross.Z + deltaZ);
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

            Debug.Log(CalcViewMaxX());
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSens, .1f, 40);
        }


    }


    private void CalculateViewportProjectionSize()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 1));
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0, 1, 1));
        float deltaY = topLeft.y - bottomLeft.y;

        float shift = Mathf.Sin(cam.transform.localEulerAngles.y) * deltaY / Mathf.Tan(cam.transform.localEulerAngles.x);

        topLeft.x += shift;
        topLeft.y = bottomLeft.y;
        topLeft.z += shift;
    }


    private float CalcViewMaxX()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 1));
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0, 1, 1));
        float deltaY = topLeft.y - bottomLeft.y;


        float shift = Mathf.Sin(cam.transform.eulerAngles.y * Mathf.PI / 180) * deltaY / Mathf.Tan(cam.transform.eulerAngles.x * Mathf.PI / 180);

        Debug.Log((cam.ViewportToWorldPoint(new Vector3(1, 1, 1)).x + shift) - cam.ViewportToWorldPoint(new Vector3(1, 0, 1)).x);
        return (topLeft.x + shift) - bottomLeft.x;
    }

    //for testing this should be 16/9 * maxX
    private float CalcViewMaxZ()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 1));
        Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1, 0, 1));

        return 1f;
    }
}


