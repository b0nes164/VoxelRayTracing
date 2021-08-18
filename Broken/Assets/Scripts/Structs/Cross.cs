public class Cross 
{
    private int _crossHeight;
    private float _crossX;
    private float _crossZ;

    public Cross(int crossHeight, float crossX, float crossZ)
    {
        _crossHeight = crossHeight;
        _crossX = crossX;
        _crossZ = crossZ;
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
}
