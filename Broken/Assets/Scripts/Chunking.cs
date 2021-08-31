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

    private NativeArray<int2> activeChunkNATIVE;

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
            MultiThreadUpdate(pseudoPosition, _diagRight, _diagUp);
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


    private void MultiThreadUpdate(Vector3Int _pseudoPosition, Vector2Int diagRight, Vector2Int diagUp)
    {
        if (activeChunkNATIVE.IsCreated)
        {
            activeChunkNATIVE.Dispose();
        }

        int posLength;
        int negLength;
        int posWidth;
        int negWidth;
        int depth;

        truePosition = GetTruePosition(_pseudoPosition);
        currentChunkIndex = GetChunkIndex(truePosition);

        int2 up = new int2(truePosition.x - diagUp.x, truePosition.z - diagUp.y);
        int2 down = new int2(truePosition.x + diagUp.x, truePosition.z + diagUp.y);
        int2 right = new int2(truePosition.x - diagRight.x, truePosition.z + diagRight.y);
        int2 left = new int2(truePosition.x + diagRight.x, truePosition.z - diagRight.y);

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

        int3 gridOffset = new int3(truePosition.x - negLength, truePosition.y - depth, truePosition.z - negWidth);

        int3 maxPosition = new int3(truePosition.x + posLength, truePosition.y, truePosition.z + posWidth);
        if (maxPosition.x > down.x)
        {
            maxPosition.x = down.x;
        }
        if (maxPosition.z > down.y)
        {
            maxPosition.z = down.y;
        }

        int totalLength = negLength + posLength + 1;
        int totalWidth = negWidth + posWidth + 1;
        int totalDepth = depth + 1;

        activeChunkNATIVE = new NativeArray<int2>(totalLength * totalWidth * totalDepth, Allocator.Persistent);

        ChunkJob cj = new ChunkJob()
        {
            _truePosition = int3.zero,
            _gridOffset = gridOffset,
            _maxPosition = maxPosition,
            _totalLength = totalLength,
            _totalWidth = totalWidth,
            _totalDepth = totalDepth,
            _yChunks = yChunks,
            _zChunks = zChunks,
            _up = up,
            _down = down,
            _left = left,
            _right = right,
            _activeChunks = activeChunkNATIVE,
        };

        JobHandle cjHandle = cj.Schedule(activeChunkNATIVE.Length, 16);
        cjHandle.Complete();
    }
    
    /// <summary>
    /// Creates grid of potentially active chunks then checks to see which are within the bounds of the camera. If it is not within the bounds, sets to empty sentinel
    /// </summary>
    [BurstCompile]
    private struct ChunkJob : IJobParallelFor
    {
        [ReadOnly]
        public int3 _truePosition;

        [ReadOnly]
        public int3 _gridOffset;

        [ReadOnly]
        public int3 _maxPosition;

        [ReadOnly]
        public int _totalLength;

        [ReadOnly]
        public int _totalWidth;

        [ReadOnly]
        public int _totalDepth;

        [ReadOnly]
        public int _yChunks;

        [ReadOnly]
        public int _zChunks;

        [ReadOnly]
        public int2 _up;

        [ReadOnly]
        public int2 _down;

        [ReadOnly]
        public int2 _left;

        [ReadOnly]
        public int2 _right;

        public NativeArray<int2> _activeChunks;

        public void Execute(int index)
        {
            int3 pos = new int3(Mathf.FloorToInt(index / (_totalDepth * _totalWidth)), Mathf.FloorToInt(index / _totalWidth) % _totalDepth, index % _totalWidth);

            pos += _gridOffset;

            if (_up.x + _up.y <= pos.x + pos.z && pos.x + pos.z <= _down.x + _down.y && _left.y - _left.x <= pos.z - pos.x && pos.z - pos.x <= _right.y - _right.x)
            {
                if (pos.x == _maxPosition.x || pos.y == _maxPosition.y || pos.z == _maxPosition.z)
                {
                    _activeChunks[index] = new int2((pos.x * _zChunks * _yChunks) + (pos.y * _zChunks) + pos.z, 1);
                }
                else
                {
                    _activeChunks[index] = new int2((pos.x * _zChunks * _yChunks) + (pos.y * _zChunks) + pos.z, 0);
                }
            }
            else
            {
                _activeChunks[index] = 0x7FFFFFFF;
            }
        }
    }

    public ref NativeArray<int2> GetActiveChunks()
    {
        return ref activeChunkNATIVE;
    }

    public void DisposeNative()
    {
        if (activeChunkNATIVE.IsCreated)
        {
            activeChunkNATIVE.Dispose();
        }
    }
}
