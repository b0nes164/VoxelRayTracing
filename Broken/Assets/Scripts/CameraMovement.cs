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

    private int length;
    private int width;

    private Vector3 delta = Vector3.zero;
    private Vector2 projectionDimension = Vector2.zero;

    private int activeSize = 0;
    private int maximumActiveChunkSize;

    private Vector2Int diagUp = Vector2Int.zero;
    private Vector2Int diagRight = Vector2Int.zero;

    //this is a fixed value;
    private float projectionAspectRatio;

    private readonly float sqrtEight = Mathf.Sqrt(8f);
    private readonly float sqrtOneTwoEightTimesTwo = Mathf.Sqrt(128) * 2;

    private readonly float maxViewSize = 50;

    public CameraMovement(Camera _cam, Text _text, Cross _cross, float _camSens, float _zoomSens, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width)
    {
        cam = _cam;
        text = _text;
        cross = _cross;
        camSens = _camSens;
        zoomSens = _zoomSens;

        length = _length;
        width = _width;

        xMax = (_xChunks * _length) - 1;
        zMax = (_zChunks * _width) - 1;
        yMax = (_yChunks * _height) - 1;

        cam.orthographicSize = maxViewSize;

        InitProjDim();
        UpdateActiveDim(projectionDimension.x);
        InitMaxActiveDim(activeSize);
        UpdateDiagonals();
        UpdateOffset();

        cam.transform.position = new Vector3(cross.X + delta.x, cross.Height + delta.y, cross.Z + delta.z);
    }

    public void MoveCam()
    {
        cross.IsUpdated = false;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            cross.X = Mathf.Clamp(cross.X - camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z - camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + delta.x, cam.transform.position.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            cross.X = Mathf.Clamp(cross.X + camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z + camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + delta.x, cam.transform.position.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            cross.X = Mathf.Clamp(cross.X + camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z - camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + delta.x, cam.transform.position.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            cross.X = Mathf.Clamp(cross.X - camSens * Time.deltaTime, 0, xMax);
            cross.Z = Mathf.Clamp(cross.Z + camSens * Time.deltaTime, 0, zMax);

            cam.transform.position = new Vector3(cross.X + delta.x, cam.transform.position.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            cross.Height = Mathf.Clamp(cross.Height - 1, 0, yMax);

            cam.transform.position = new Vector3(cam.transform.position.x, cross.Height + delta.y, cam.transform.position.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            cross.Height = Mathf.Clamp(cross.Height + 1, 0, yMax);

            cam.transform.position = new Vector3(cam.transform.position.x, cross.Height + delta.y, cam.transform.position.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKey(KeyCode.Keypad2))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomSens, .1f, maxViewSize);

            UpdateProjectionDimension();
            UpdateDiagonals();
            UpdateOffset();
            UpdateActiveDim(projectionDimension.x);

            cam.transform.position = new Vector3(cross.X + delta.x, cross.Height + delta.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }

        if (Input.GetKey(KeyCode.Keypad8))
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - zoomSens, .1f, maxViewSize);

            UpdateProjectionDimension();
            UpdateDiagonals();
            UpdateOffset();
            UpdateActiveDim(projectionDimension.x);

            cam.transform.position = new Vector3(cross.X + delta.x, cross.Height + delta.y, cross.Z + delta.z);

            cross.IsUpdated = true;
        }
    }

    //**************************************************************************************************************************************************
    //**************************************************************************************************************************************************
    //**************************************************************************************************************************************************
    
    /// <summary>
    /// Distance formula in two dimensions
    /// </summary>
    /// <param name="xOne"></param>
    /// <param name="xTwo"></param>
    /// <param name="yOne"></param>
    /// <param name="yTwo"></param>
    /// <returns></returns>
    private float Distance(float xOne, float xTwo, float yOne, float yTwo)
    {
        return Mathf.Sqrt(Mathf.Pow(xTwo - xOne, 2) + Mathf.Pow(yTwo - yOne, 2));
    }

    private float CalcViewHeight()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0, 1, 0));
        float deltaY = topLeft.y - bottomLeft.y;

        float tempFortyFive = Mathf.Sin(cam.transform.eulerAngles.y * Mathf.Deg2Rad);

        float shift = tempFortyFive * deltaY / Mathf.Tan(cam.transform.eulerAngles.x * Mathf.Deg2Rad);

        return (topLeft.x + shift - bottomLeft.x) / tempFortyFive;
    }

    private float CalcViewWidth()
    {
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1, 0, 0));

        return Distance(bottomRight.x, bottomLeft.x, bottomRight.z, bottomLeft.z);
    }

    //why do math when you can cheat
    private float CalcViewWidth(float leftSide)
    {
        return leftSide * projectionAspectRatio;
    }
    private Vector3 CalcTopRightProjection()
    {
        Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1, 0, 0));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        float deltaY = topRight.y - bottomRight.y;

        float shift = Mathf.Sin(cam.transform.eulerAngles.y * Mathf.Deg2Rad) * deltaY / Mathf.Tan(cam.transform.eulerAngles.x * Mathf.Deg2Rad);

        return new Vector3(topRight.x + shift, bottomRight.y, topRight.z + shift);
    }

    /// <summary>
    /// Calcualtes the size of the viewport in world units. Also initializes the the aspect ratio based on the viewport size.
    /// </summary>
    private void InitProjDim()
    {
        float tempX = CalcViewWidth();
        float tempY = CalcViewHeight();

        projectionAspectRatio = tempX / tempY;
        projectionDimension = new Vector2(tempX, tempY);
    }

    /// <summary>
    /// Calculates the size of the viewport in world units based on the aspect ratio. User this everywhere besides constructor.
    /// </summary>
    private void UpdateProjectionDimension()
    {
        float temp = CalcViewHeight();
        projectionDimension = new Vector2(CalcViewWidth(temp), temp);
    }

    /// <summary>
    /// Calculates the minimum size of the inscribing square needed to accomodate the viewport projection
    /// </summary>
    /// <param name="projectionWidth"></param>
    /// the size of the width of the projection. We take the width of the projection because its always bigger.
    private void UpdateActiveDim(float projectionWidth)
    {
        activeSize = Mathf.CeilToInt((projectionWidth + 16) / sqrtOneTwoEightTimesTwo);
    }

    /// <summary>
    /// Calculates the number of chunks in one height slice at the maxmium viewport size
    /// </summary>
    /// <param name="_activeSize"></param>
    /// This SHOULD BE active size calculated when the viewport is at the maximum
    private void InitMaxActiveDim(int _activeSize)
    {
        maximumActiveChunkSize = ((_activeSize * 2) + 1) * ((_activeSize * 2) + 1);
    }

    /// <summary>
    /// Calculates the x and z chunk position that corresponds to the world position edge of the viewport.
    /// </summary>
    private void UpdateDiagonals()
    {
        float tempDiag = projectionDimension.y / sqrtEight;

        diagUp = new Vector2Int(Mathf.CeilToInt(tempDiag / length), Mathf.CeilToInt(tempDiag / width));

        tempDiag = projectionDimension.x / sqrtEight;
        diagRight = new Vector2Int(Mathf.CeilToInt(tempDiag / length), Mathf.CeilToInt(tempDiag / width));
    }

    /// <summary>
    /// Calculate the the offset from the cross position in world units that will ensure camera will be centered.
    /// </summary>
    private void UpdateOffset()
    {
        //calculate the midpoint of bottom left and right unit points.
        Vector3 tempDelta = (cam.ViewportToWorldPoint(Vector3.right) - cam.ViewportToWorldPoint(Vector3.zero)) / 2f;

        //set the x and z offsets to the midpoint
        delta.x = -1 * tempDelta.x;
        delta.z = tempDelta.z;

        //set the y offset based on the hypotenuse formed by x and z
        delta.y = Distance(tempDelta.x, 0, tempDelta.z, 0) * Mathf.Tan(cam.transform.eulerAngles.x * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Calls all of the update methods
    /// </summary>
    private void UpdateAll()
    {

    }

    public int GetActiveSize()
    {
        return activeSize;
    }

    public int GetMaximumActiveSize()
    {
        return maximumActiveChunkSize;
    }

    public Vector2Int GetDiagUp()
    {
        return diagUp;
    }

    public Vector2Int GetDiagRight()
    {
        return diagRight;
    }

}


