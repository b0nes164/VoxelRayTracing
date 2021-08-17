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

    private Cross cross;
    private int lastCross;

    private Vector3Int cameraChunkPosition = Vector3Int.zero;

    private List<ChunkStruct> activeChunks;

    public Chunking(Transform _camera, List<ChunkStruct> _activeChunks, Cross _cross, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width, int _activeChunkDepth, int _activeChunkLength, int _activeChunkWidth)
    {
        xChunks = _xChunks;
        yChunks = _yChunks;
        zChunks = _zChunks;

        length = _length;
        height = _height;
        width = _width;

        cross = _cross;
        lastCross = cross.Height;

        activeChunkDepth = (_activeChunkDepth - 1);
        activeChunkLength = _activeChunkLength;
        activeChunkWidth = _activeChunkWidth;

        camera = _camera;
        camera.position = new Vector3(0, cross.Height + 100, 0);
        activeChunks = _activeChunks;
        IsNewChunk();
    }

    //translates the camera position from global space to chunk space
    private Vector3Int GetCameraChunkPosition(Vector3 cameraPos)
    {
        return new Vector3Int(Mathf.FloorToInt(cameraPos.x / length), Mathf.FloorToInt(cross.Height / height), Mathf.FloorToInt(cameraPos.z /width));
    }

    public bool IsNewChunk()
    {
        Vector3Int newPos = GetCameraChunkPosition(camera.position);

        if (newPos != cameraChunkPosition || lastCross != cross.Height)
        {
            lastCross = cross.Height;
            cameraChunkPosition = newPos;
            UpdateActiveChunks();

            return true;
        }
        else
        {
            return false;
        }
    }

    private int GetCameraChunk()
    {
        return cameraChunkPosition.x * zChunks * yChunks + cameraChunkPosition.y * zChunks + cameraChunkPosition.z;
    }

    private void UpdateActiveChunks()
    {
        int posLength;
        int negLength;
        int posWidth;
        int negWidth;

        cameraChunk = GetCameraChunk();

        int wh = zChunks * yChunks;

        activeChunks.Clear();

        if (cameraChunkPosition.x < activeChunkLength)
        {
            negLength = cameraChunkPosition.x;
            posLength = activeChunkLength;
        }
        else
        {
            if (cameraChunkPosition.x + activeChunkLength + 1 > xChunks)
            {
                posLength = xChunks - (cameraChunkPosition.x + 1);
                negLength = activeChunkLength;
            }
            else
            {
                posLength = activeChunkLength;
                negLength = activeChunkLength;
            }
        }

        if (cameraChunkPosition.z < activeChunkWidth)
        {
            negWidth = cameraChunkPosition.z;
            posWidth = activeChunkWidth;
        }
        else
        {
            if (cameraChunkPosition.z + activeChunkWidth + 1 > zChunks)
            {
                posWidth = zChunks - (cameraChunkPosition.z + 1);
                negWidth = activeChunkWidth;
            }
            else
            {
                posWidth = activeChunkLength;
                negWidth = activeChunkWidth;
            }
        }


        for (int y = cameraChunk - (activeChunkDepth * zChunks); y <= cameraChunk; y += zChunks)
        {
            for (int x = (y - wh * negLength); x <= (y + wh * posLength); x += wh)
            {
                for (int z = (x - negWidth); z <= (x + posWidth); z++)
                {
                    activeChunks.Add(new ChunkStruct(z, 0));
                }
            }
        }

        //PopulateChunkList();

        //activeChunks.Add(new ChunkStruct(cameraChunk, 0));

    }

    private void PopulateChunkList()
    {
        int chunkcount = xChunks * yChunks * zChunks;
        for (int i = 0; i < chunkcount; i++)
        {
            activeChunks.Add(new ChunkStruct(i, 0));
        }
    }



}
