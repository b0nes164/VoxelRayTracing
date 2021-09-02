using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

public class Chunking 
{
    private int activeChunkDepth;

    private int xChunks;
    private int yChunks;
    private int zChunks;

    private int length;
    private int height;
    private int width;

    private Cross cross;

    private Vector3Int truePosition = Vector3Int.zero;

    private int2 up = int2.zero;
    private int2 down = int2.zero;
    private int2 left = int2.zero;
    private int2 right = int2.zero;

    private int3 gridOffset = int3.zero;
    private int3 maxPosition = int3.zero;

    private int posLength = 0;
    private int negLength = 0;
    private int posWidth = 0;
    private int negWidth = 0;
    private int depth = 0;
    private int totalLength = 0;
    private int totalDepth = 0;
    private int totalWidth = 0;

    private NativeArray<int2> activeChunks;

    public Chunking(Cross _cross, int _xChunks, int _yChunks, int _zChunks, int _length, int _height, int _width, int _activeChunkDepth)
    {
        xChunks = _xChunks;
        yChunks = _yChunks;
        zChunks = _zChunks;

        length = _length;
        height = _height;
        width = _width;

        cross = _cross;

        activeChunkDepth = _activeChunkDepth - 1;

        activeChunks = new NativeArray<int2>(cross.ActiveSizeTotal, Allocator.Persistent);

        UpdateChunks();
    }

    public void UpdateChunks()
    {
        if(cross.IsUpdated)
        {
            MultiThreadUpdate();
        }
    }

    private Vector3Int GetTrueChunkPosition()
    {
        return new Vector3Int(Mathf.FloorToInt(cross.X / length), Mathf.FloorToInt(cross.Height * 1f / height), Mathf.FloorToInt(cross.Z / width));
    }

    private void MultiThreadUpdate()
    {
        truePosition = GetTrueChunkPosition();

        up = new int2(truePosition.x - cross.DiagUp.x, truePosition.z - cross.DiagUp.y);
        down = new int2(truePosition.x + cross.DiagUp.x, truePosition.z + cross.DiagUp.y);
        right = new int2(truePosition.x - cross.DiagRight.x, truePosition.z + cross.DiagRight.y);
        left = new int2(truePosition.x + cross.DiagRight.x, truePosition.z - cross.DiagRight.y);

        if (truePosition.x < cross.ActiveSize)
        {
            negLength = truePosition.x;
            posLength = cross.ActiveSize;
        }
        else
        {
            if (truePosition.x + cross.ActiveSize + 1 > xChunks)
            {
                posLength = xChunks - (truePosition.x + 1);
                negLength = cross.ActiveSize;
            }
            else
            {
                posLength = cross.ActiveSize;
                negLength = cross.ActiveSize;
            }
        }

        if (truePosition.z < cross.ActiveSize)
        {
            negWidth = truePosition.z;
            posWidth = cross.ActiveSize;
        }
        else
        {
            if (truePosition.z + cross.ActiveSize + 1 > zChunks)
            {
                posWidth = zChunks - (truePosition.z + 1);
                negWidth = cross.ActiveSize;
            }
            else
            {
                posWidth = cross.ActiveSize;
                negWidth = cross.ActiveSize;
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

        gridOffset = new int3(truePosition.x - negLength, truePosition.y - depth, truePosition.z - negWidth);

        maxPosition = new int3(truePosition.x + posLength, truePosition.y, truePosition.z + posWidth);
        if (maxPosition.x > down.x)
        {
            maxPosition.x = down.x;
        }
        if (maxPosition.z > down.y)
        {
            maxPosition.z = down.y;
        }

        totalLength = negLength + posLength + 1;
        totalWidth = negWidth + posWidth + 1;
        totalDepth = depth + 1;

        if (activeChunks.Length != cross.ActiveSizeTotal)
        {
            activeChunks.Dispose();
            activeChunks = new NativeArray<int2>(cross.ActiveSizeTotal, Allocator.Persistent);
        }

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
            _activeChunks = activeChunks,
        };

        JobHandle cjHandle = cj.Schedule(activeChunks.Length, 16);
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
        return ref activeChunks;
    }

    public void DisposeNative()
    {
        if (activeChunks.IsCreated)
        {
            activeChunks.Dispose();
        }
    }
}
