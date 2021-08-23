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
    private float deltaX = 48;
    private float deltaZ = 48;
    private float deltaY;

    private Vector2 projectionDimension = Vector2.zero;

    private float chunkDiagonalDistance;
    //this is fixed
    private readonly float projectionAspectRatio;


    public CameraMovement(Camera _cam, Text _text, Cross _cross, float _camSens, float _zoomSens, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width)
    {
        cam = _cam;
        text = _text;
        cross = _cross;
        camSens = _camSens;
        zoomSens = _zoomSens;

        xMax = (_xChunks * _length) - 1;
        zMax = (_zChunks * _width) - 1;
        yMax = (_yChunks * _height) - 1;

        
        chunkDiagonalDistance = Mathf.Sqrt(Mathf.Pow(_length, 2) + Mathf.Pow(_width, 2));

        deltaY = cam.ViewportToWorldPoint(new Vector3(0, 0, 0)).y - cross.Height;


        cam.transform.position = new Vector3(cross.X + deltaX, cross.Height + deltaY, cross.Z + deltaZ);

        float tempX = CalcViewWidth();
        float tempY = CalcViewHeight();

        projectionAspectRatio = tempX / tempY;

        projectionDimension = new Vector2(tempX, tempY);
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

        /*
         if (Input.GetKey(KeyCode.KeypadPlus))
        {
            cam.transform.position = new Vector3(cam.transform.position.x, Mathf.Clamp(cam.transform.position.y + camSens * Time.deltaTime, cross.Height, camMax), cam.transform.position.z);
        }

        if (Input.GetKey(KeyCode.KeypadMinus))
        {
            cam.transform.position = new Vector3(cam.transform.position.x, Mathf.Clamp(cam.transform.position.y - camSens * Time.deltaTime, cross.Height, camMax), cam.transform.position.z);
        }
         */




        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            cross.Height = Mathf.Clamp(cross.Height - 1, 0, yMax);

            cam.transform.position = new Vector3(cam.transform.position.x, cross.Height + deltaY, cam.transform.position.z);
        }

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            cross.Height = Mathf.Clamp(cross.Height + 1, 0, yMax);

            cam.transform.position = new Vector3(cam.transform.position.x, cross.Height + deltaY, cam.transform.position.z);
        }

        if (Input.GetKey(KeyCode.Keypad2))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomSens, .1f, 40);

            float temp = CalcViewHeight();
            projectionDimension = new Vector2(CalcViewWidth(temp), temp);
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSens, .1f, 40);

            float temp = CalcViewHeight();
            projectionDimension = new Vector2(CalcViewWidth(temp), temp);
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


    private float CalcViewHeight()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 1));
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0, 1, 1));
        float deltaY = topLeft.y - bottomLeft.y;

        float tempFortyFive = Mathf.Sin(cam.transform.eulerAngles.y * Mathf.Deg2Rad);

        float shift = tempFortyFive * deltaY / Mathf.Tan(cam.transform.eulerAngles.x * Mathf.Deg2Rad);

        return (topLeft.x + shift - bottomLeft.x) / tempFortyFive;
    }

    private float CalcViewWidth()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 1));
        Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1, 0, 1));

        return Distance(bottomRight.x, bottomLeft.x, bottomRight.z, bottomLeft.z);
    }



    //why do math when you can cheat
    private float CalcViewWidth(float leftSide)
    {
        return leftSide * projectionAspectRatio;
    }

    //why doesnt this exist already?
    private float Distance(float xOne, float xTwo, float yOne, float yTwo)
    {
        return Mathf.Sqrt(Mathf.Pow(xTwo - xOne, 2) + Mathf.Pow(yTwo - yOne, 2));
    }

    public Vector2 GetProjDim()
    {
        return projectionDimension;
    }

}


