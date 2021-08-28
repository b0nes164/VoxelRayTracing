using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

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

    private readonly int diagonalRight;
    private readonly int diagonalUp;

    private Cross cross;

    private Vector3Int[] chunkPositionTable;
    private Vector3Int pseudoPosition = Vector3Int.zero;
    private Vector3Int truePosition = Vector3Int.zero;

    private List<ChunkStruct> activeChunks;

    private NativeArray<int3> activeChunkNATIVE;

    public Chunking(Transform _camera, List<ChunkStruct> _activeChunks, Vector3Int[] _chunkPositionTable, Cross _cross, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width, int _activeChunkDepth, int _activeChunkLength, int _activeChunkWidth)
    {
        xChunks = _xChunks;
        yChunks = _yChunks;
        zChunks = _zChunks;

        length = _length;
        height = _height;
        width = _width;

        cross = _cross;

        chunkPositionTable = _chunkPositionTable;

        diagonalRight = (zChunks * yChunks * -1) + 1;
        diagonalUp = (zChunks * yChunks + 1) * -1;

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

    public bool IsNewChunk(Vector2Int _diagRight, Vector2Int _diagUp)
    {
        Vector3Int newPseud = PseudoChunkPosition();

        if (pseudoPosition != newPseud)
        {
            pseudoPosition = newPseud;
            ViewportDefinedChunkUpdate(pseudoPosition, _diagRight, _diagUp);
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
        return _truePosition.x * zChunks * yChunks + _truePosition.y * zChunks + _truePosition.z;
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

    private void ViewportDefinedChunkUpdate(Vector3Int _pseudoPosition, Vector2Int diagRight, Vector2Int diagUp)
    {
        int posLength;
        int negLength;
        int posWidth;
        int negWidth;
        int depth;

        truePosition = GetTruePosition(_pseudoPosition);
        currentChunkIndex = GetChunkIndex(truePosition);

        Vector2Int up = new Vector2Int(truePosition.x - diagUp.x, truePosition.z - diagUp.y);
        Vector2Int down = new Vector2Int(truePosition.x + diagUp.x, truePosition.z + diagUp.y);
        Vector2Int right = new Vector2Int(truePosition.x - diagRight.x, truePosition.z + diagRight.y);
        Vector2Int left = new Vector2Int(truePosition.x + diagRight.x, truePosition.z - diagRight.y);

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

        int tempX;
        int tempZ;

        for (int y = currentChunkIndex - (depth * zChunks); y <= currentChunkIndex; y += zChunks)
        {
            for (int x = -1 * negLength; x < 0; x++)
            {
                tempX = y + (wh * x);

                //top quadrant
                for (int z = -1 * negWidth; z < 0; z++)
                {
                    tempZ = tempX + z;

                    if (chunkPositionTable[tempZ].x + chunkPositionTable[tempZ].z >= up.x + up.y)
                    {
                        activeChunks.Add(new ChunkStruct(tempZ, 0));
                    }
                }
                //right quadrant
                for (int z = 0; z <= posWidth; z++)
                {
                    tempZ = tempX + z;

                    if(chunkPositionTable[tempZ].z - chunkPositionTable[tempZ].x <= right.y - right.x)
                    {
                        activeChunks.Add(new ChunkStruct(tempZ, 0));
                    }
                }
            }

            for (int x = 0; x <= posLength; x++)
            {
                tempX = y + (wh * x);

                //left quadrant
                for (int z = -1 * negWidth; z < 0; z++)
                {
                    tempZ = tempX + z;

                    if (chunkPositionTable[tempZ].z - chunkPositionTable[tempZ].x >= left.y - left.x)
                    {
                        activeChunks.Add(new ChunkStruct(tempZ, 0));
                    }
                }

                //top quadrant
                for (int z = 0; z <= posWidth; z++)
                {
                    tempZ = tempX + z;

                    if (chunkPositionTable[tempZ].x + chunkPositionTable[tempZ].z <= down.x + down.y)
                    {
                        activeChunks.Add(new ChunkStruct(tempZ, 0));
                    }
                }
            }
        }
    }


    private void MultiThreadUpdate()
    {
        int posLength;
        int negLength;
        int posWidth;
        int negWidth;
        int depth;

        truePosition = GetTruePosition(_pseudoPosition);
        currentChunkIndex = GetChunkIndex(truePosition);

        Vector2Int up = new Vector2Int(truePosition.x - diagUp.x, truePosition.z - diagUp.y);
        Vector2Int down = new Vector2Int(truePosition.x + diagUp.x, truePosition.z + diagUp.y);
        Vector2Int right = new Vector2Int(truePosition.x - diagRight.x, truePosition.z + diagRight.y);
        Vector2Int left = new Vector2Int(truePosition.x + diagRight.x, truePosition.z - diagRight.y);

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

        activeChunkNATIVE = new NativeArray<int3>((negLength + posLength + 1) * (negWidth + posWidth + 1) * (depth + 1), Allocator.Persistent);



    }

    private struct ChunkJob : IJobParallelFor
    {
        [ReadOnly]
        public Vector3Int[] _chunkPositionTable;

        public NativeArray<int3> activeChunks;
        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }
    }















    //scrap
    /*
     private void ViewportDefinedUpdate(Vector3Int _pseudoPosition, Vector2 _projDim)
    {
        int depth;

        activeChunks.Clear();

        truePosition = GetTruePosition(_pseudoPosition);
        currentChunkIndex = GetChunkIndex(truePosition);


        int halfLength = Mathf.CeilToInt((_projDim.x / (22.627f * 2f)) + .5f);
        int halfHeight = Mathf.CeilToInt((_projDim.y / (22.627f * 2f)) + .5f);

        int posDiagLength;
        int negDiagLength;
        int posDiagHeight;
        int negDiagHeight;

        //diagright 
        //decreasing x, increasing z

        //positive diagonal right
        if (truePosition.x < halfLength || truePosition.z + halfLength + 1 > zChunks)
        {
            posDiagLength = Mathf.Min(truePosition.x, zChunks - (truePosition.z + 1));
            negDiagLength = halfLength;
        }
        else
        {
            if (truePosition.x + halfLength + 1 > xChunks || (truePosition.z - 1) < halfLength)
            {
                posDiagLength = halfLength;
                negDiagLength = Mathf.Min(xChunks - (truePosition.x + 1), (truePosition.z - 1));
            }
            else
            {
                posDiagLength = halfLength;
                negDiagLength = halfLength;
            }
        }

        //diagup
        //decreasing x, decreasing z

        if (truePosition.x < halfHeight || (truePosition.z - 1) < halfHeight)
        {
            posDiagHeight = Mathf.Min(truePosition.x, (truePosition.z - 1));
            negDiagHeight = halfHeight;
        }
        else
        {
            if (truePosition.x + halfHeight + 1 > xChunks || truePosition.z + halfHeight + 1 > zChunks)
            {
                posDiagHeight = halfHeight;
                negDiagHeight = Mathf.Min(xChunks - (truePosition.x + 1), zChunks - (truePosition.z + 1));
            }
            else
            {
                posDiagHeight = halfHeight;
                negDiagHeight = halfHeight;
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
            //inline diagonal
            for (int diagX = y - diagonalRight * posDiagLength; diagX >= y + diagonalRight * negDiagLength; diagX += diagonalRight)
            {
                for (int diagZ = diagX - diagonalUp * posDiagHeight; diagZ >= diagX + diagonalUp * negDiagHeight; diagZ += diagonalUp)
                {
                    activeChunks.Add(new ChunkStruct(diagZ - 1, 0));
                    activeChunks.Add(new ChunkStruct(diagZ, 0));
                }
            }


            for (int diagX = (y - 1) - (diagonalRight * (posDiagLength - 1)); diagX >= (y - 1) + negDiagLength * diagonalRight; diagX += diagonalRight)
            {
                for (int diagZ = diagX - diagonalUp * posDiagHeight; diagZ >= diagX + diagonalUp * negDiagHeight; diagZ += diagonalUp)
                {
                    activeChunks.Add(new ChunkStruct(diagZ, 0));
                }
            }
 
}
    }
     */
}
