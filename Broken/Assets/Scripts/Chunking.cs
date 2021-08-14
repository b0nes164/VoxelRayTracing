using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunking 
{
    private Transform camera;

    private int cameraChunk;
    private int currentChunk;

    private int activeChunkDepth;
    private int activeChunkLength;
    private int activeChunkWidth;

    private int xChunks;
    private int yChunks;
    private int zChunks;

    private int length;
    private int height;
    private int width;

    private Vector3Int cameraChunkPosition = Vector3Int.zero;

    private List<Vector2Int> activeChunks;

    public Chunking(Transform _camera, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width, int _activeChunkDepth, int _activeChunkLength, int _activeChunkWidth)
    {
        xChunks = _xChunks;
        yChunks = _yChunks;
        zChunks = _zChunks;

        length = _length;
        height = _height;
        width = _width;

        activeChunkDepth = _activeChunkDepth;
        activeChunkLength = _activeChunkLength;
        activeChunkWidth = _activeChunkWidth;

        camera = _camera;
        camera.position = new Vector3(200, 200, 200);
        IsNewChunk(GetCameraChunkPosition(camera.position));
        activeChunks = new List<Vector2Int>();
    }

    //translates the camera position from global space to chunk space
    private Vector3Int GetCameraChunkPosition(Vector3 cameraPos)
    {
        return new Vector3Int(Mathf.FloorToInt(cameraPos.x / length), Mathf.FloorToInt(cameraPos.y / height), Mathf.FloorToInt(cameraPos.z));
    }

    private void IsNewChunk(Vector3Int newPos)
    {
        if (newPos != cameraChunkPosition)
        {
            cameraChunkPosition = newPos;
            UpdateActiveChunks();
        }
    }

    private int GetCameraChunk()
    {
        return cameraChunkPosition.x * zChunks * yChunks + cameraChunkPosition.y * zChunks + cameraChunkPosition.z;
    }

    public void UpdateActiveChunks()
    {
        cameraChunk = GetCameraChunk();

        int wh = zChunks * yChunks;

        activeChunks.Clear();

        for (int y = cameraChunk - (activeChunkDepth * zChunks); y <= cameraChunk; y += zChunks)
        {
            for (int x = (y - (wh * activeChunkLength)); x <= (y + wh * activeChunkLength); x += wh)
            {
                for (int z = (x - activeChunkWidth); z <= (x + activeChunkWidth); z++)
                {
                    activeChunks.Add(new Vector2Int(z, 0));
                }
            }
        }
    }

    
}
