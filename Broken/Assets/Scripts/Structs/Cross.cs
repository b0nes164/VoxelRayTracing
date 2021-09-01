public class Cross 
{
    private int _crossHeight;
    private float _crossX;
    private float _crossZ;
    private bool _isUpdated;

    public Cross(int crossHeight, float crossX, float crossZ, bool isUpdated)
    {
        _crossHeight = crossHeight;
        _crossX = crossX;
        _crossZ = crossZ;
        _isUpdated = isUpdated;
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
}
