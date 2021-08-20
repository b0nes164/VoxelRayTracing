using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunking 
{
    private Transform camera;

    private int currentChunkIndex;

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

    private Vector3Int pseudoPosition = Vector3Int.zero;
    private Vector3Int truePosition = Vector3Int.zero;

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

        activeChunkDepth = (_activeChunkDepth - 1);
        activeChunkLength = _activeChunkLength;
        activeChunkWidth = _activeChunkWidth;
        activeChunks = _activeChunks;

        IsNewChunk();
    }

    //translates the camera position from global space to chunk space
    private Vector3Int PseudoChunkPosition()
    {
        return new Vector3Int(Mathf.FloorToInt(cross.X / length), cross.Height,  Mathf.FloorToInt(cross.Z /width));
    }

    public bool IsNewChunk()
    {
        Vector3Int newPseud = PseudoChunkPosition();

        if (pseudoPosition != newPseud)
        {
            pseudoPosition = newPseud;
            UpdateActiveChunks(pseudoPosition);
            return true;
        }
        else
        {
            return false;
        }
    }

    private Vector3Int GetTruePosition(Vector3Int _pseudoPosition)
    {
        return new Vector3Int(_pseudoPosition.x, Mathf.FloorToInt(_pseudoPosition.y * 1f / height), _pseudoPosition.z);
    }

    private int GetChunkIndex(Vector3Int _truePosition)
    {
        return truePosition.x * zChunks * yChunks + truePosition.y * zChunks + truePosition.z;
    }

    private void UpdateActiveChunks(Vector3Int _pseudoPosition)
    {
        int posLength;
        int negLength;
        int posWidth;
        int negWidth;
        int depth;

        truePosition = GetTruePosition(_pseudoPosition);
        currentChunkIndex = GetChunkIndex(truePosition);

        int wh = zChunks * yChunks;

        activeChunks.Clear();

        if (truePosition.x < activeChunkLength)
        {
            negLength = truePosition.x;
            posLength = activeChunkLength;
        }
        else
        {
            if (truePosition.x + activeChunkLength + 1 > xChunks)
            {
                posLength = xChunks - (truePosition.x + 1);
                negLength = activeChunkLength;
            }
            else
            {
                posLength = activeChunkLength;
                negLength = activeChunkLength;
            }
        }

        if (truePosition.z < activeChunkWidth)
        {
            negWidth = truePosition.z;
            posWidth = activeChunkWidth;
        }
        else
        {
            if (truePosition.z + activeChunkWidth + 1 > zChunks)
            {
                posWidth = zChunks - (truePosition.z + 1);
                negWidth = activeChunkWidth;
            }
            else
            {
                posWidth = activeChunkLength;
                negWidth = activeChunkWidth;
            }
        }

        if (truePosition.y < activeChunkDepth)
        {
            depth = truePosition.y;
        }
        else
        {
            depth = activeChunkDepth;
        }


        for (int y = currentChunkIndex - (depth * zChunks); y <= currentChunkIndex; y += zChunks)
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
