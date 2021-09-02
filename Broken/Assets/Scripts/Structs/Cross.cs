using UnityEngine;

public class Cross 
{
    private int _crossHeight;
    private float _crossX;
    private float _crossZ;
    private bool _isUpdated;
    private Vector2Int _diagRight;
    private Vector2Int _diagUp;
    private int _activeSize;
    private int _activeSizeTotal;

    public Cross(int crossHeight, float crossX, float crossZ, bool isUpdated, Vector2Int diagRight, Vector2Int diagUp, int activeSize, int activeSizeTotal)
    {
        _crossHeight = crossHeight;
        _crossX = crossX;
        _crossZ = crossZ;
        _isUpdated = isUpdated;
        _diagRight = diagRight;
        _diagUp = diagUp;
        _activeSize = activeSize;
        _activeSizeTotal = activeSizeTotal;
    }

    public int Height
    {
        get { return _crossHeight; }
        set { _crossHeight = value; }
    }

    public float X
    {
        get { return _crossX; }
        set { _crossX = value;  }
    }

    public float Z
    {
        get { return _crossZ; }
        set { _crossZ = value; }
    }

    public bool IsUpdated
    {
        get { return _isUpdated; }
        set { _isUpdated = value; }
    }
    
    public Vector2Int DiagRight
    {
        get { return _diagRight; }
        set { _diagRight = value; }
    }

    public Vector2Int DiagUp
    {
        get { return _diagUp; }
        set { _diagUp = value; }
    }

    public int ActiveSize
    {
        get { return _activeSize; }
        set { _activeSize = value; }
    }

    public int ActiveSizeTotal
    {
        get { return _activeSizeTotal; }
        set { _activeSizeTotal = value; }
    }

}
