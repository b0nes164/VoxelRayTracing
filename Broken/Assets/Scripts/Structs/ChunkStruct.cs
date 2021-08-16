public struct ChunkStruct
{
    private int _index;
    private int _chunkCase;

    public ChunkStruct(int index, int chunkCase)
    {
        _index = index;
        _chunkCase = chunkCase;
    }

    public int Index
    {
        get { return _index; }
        set { _index = value; }
    }
    public int ChunkCase
    {
        get { return _chunkCase; }
        set { _chunkCase = value; }
    }
}
