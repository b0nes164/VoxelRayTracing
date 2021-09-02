using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System;

public class WorldGeneration 
{
    private ComputeShader computeShader;

    private int xChunks;
    private int yChunks;
    private int zChunks;

    private int length;
    private int height;
    private int width;

    private int chunkCount;
    private int leadingEdgeCount;
    private int localChunkSize;
    private int stepIndex;
    private int dispatchGroups;
    private int edgeDispatchGroups;
    private int globalLength;
    private int globalHeight;
    private int globalWidth;
    private int globalLeadingEdgeCount;
    private int globalStepIndex;
    private int chunkStepDepth;
    private int activeDepth;
    private int bufferArraySize;
    private int maximumActiveChunkSize;

    private ComputeBuffer b_chunkEdge;
    private ComputeBuffer b_chunkPosition;
    private ComputeBuffer b_locPos;
    private ComputeBuffer b_locEdge;
    private ComputeBuffer b_temp;
    private ComputeBuffer b_globalHeight;
    private ComputeBuffer b_heightTransfer;
    private ComputeBuffer b_globalSolid;
    private ComputeBuffer b_solidTransfer;
    private ComputeBuffer hashTransferBuffer;

    private ComputeBuffer[] mainBuffers;
    private ComputeBuffer[] newRenderBuffers;
    private ComputeBuffer[] argBuffers;

    //private uint[] chunkEdgeTable;
    private Vector3Int[] chunkPositionTable;
    private MaterialPropertyBlock[] propertyBlocks;

    private int[] xOffset;
    private int[] yOffset;
    private int[] zOffset;

    private uint[] args;

    private ComputeBuffer bugBugger;
    private uint[] test;

    #region kernel
    private int k_initChunkRef;
    private int k_transGlob;
    private int k_initLocRef;
    private int k_singleThread;
    private int k_initRayRef;
    private int k_initGlobHeight;
    private int k_initGlobalSolid;
    private int k_initLocTransBuff;
    private int k_initHash;
    private int k_noise;
    private int k_locVisCalc;
    private int k_fullVisCalcs;
    private int k_fullCull;
    #endregion

    //change names in compute shader

    public WorldGeneration(ComputeShader _computeShader, uint meshIndexCount, int _maximumActiveChunks,
            int _xChunks, int _yChunks, int _zChunks, int _length, int _width, int _height, int _activeDepth)
    {
        computeShader = _computeShader;

        xChunks = _xChunks;
        yChunks = _yChunks;
        zChunks = _zChunks;
        length = _length;
        height = _height;
        width = _width;

        activeDepth = _activeDepth;
        maximumActiveChunkSize = _maximumActiveChunks;
        
        args = new uint[5] { meshIndexCount, 0, 0, 0, 0 };

        InitNonStaticVar();
        InitShaderValues();
    }

    private void InitNonStaticVar()
    {
        chunkCount = xChunks * yChunks * zChunks;
        leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
        localChunkSize = length * width * height;
        stepIndex = (width * height) + width + 1;
        dispatchGroups = Mathf.CeilToInt(localChunkSize / 1024f);
        edgeDispatchGroups = Mathf.CeilToInt(leadingEdgeCount / 768f);
        globalLength = xChunks * length;
        globalHeight = yChunks * height;
        globalWidth = zChunks * width;
        globalLeadingEdgeCount = (globalLength * globalWidth) + (globalLength * (globalHeight - 1)) + ((globalWidth - 1) * (globalHeight - 1));
        globalStepIndex = (globalWidth * globalHeight) + globalWidth + 1;
        chunkStepDepth = yChunks;
        bufferArraySize = 0;

        //chunkEdgeTable = new uint[chunkCount];
        chunkPositionTable = new Vector3Int[chunkCount];

        mainBuffers = new ComputeBuffer[chunkCount];

        newRenderBuffers = new ComputeBuffer[maximumActiveChunkSize];
        argBuffers = new ComputeBuffer[maximumActiveChunkSize];
        propertyBlocks = new MaterialPropertyBlock[maximumActiveChunkSize];

        xOffset = new int[chunkCount];
        yOffset = new int[chunkCount];
        zOffset = new int[chunkCount];

        //this can be jobified
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset[i] = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset[i] = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset[i] = (i % zChunks) * width;
        }
    }

    private void InitShaderValues()
    {
        #region constants
        computeShader.SetInt("e_stepIndex", stepIndex);
        computeShader.SetInt("e_localChunkSize", localChunkSize);
        computeShader.SetInt("e_xChunks", xChunks);
        computeShader.SetInt("e_yChunks", yChunks);
        computeShader.SetInt("e_zChunks", zChunks);
        computeShader.SetInt("e_length", length);
        computeShader.SetInt("e_height", height);
        computeShader.SetInt("e_width", width);
        computeShader.SetInt("e_heightPackedSize", HeightPackedSize());
        computeShader.SetInt("e_solidPackedSize", SolidPackedSize());
        computeShader.SetInt("e_heightSizeInBits", HeightToBits(height));
        computeShader.SetInt("e_solidSizeInBits", 1);
        computeShader.SetInt("e_leadingEdgeCount", leadingEdgeCount);
        computeShader.SetInt("e_globalLength", globalLength);
        computeShader.SetInt("e_globalHeight", globalHeight);
        computeShader.SetInt("e_globalWidth", globalWidth);
        computeShader.SetInt("e_globalStepIndex", globalStepIndex);
        computeShader.SetInt("e_chunkStepDepth", chunkStepDepth);
        #endregion

        #region kernels
        k_initChunkRef = computeShader.FindKernel("InitializeChunkReference");
        k_transGlob = computeShader.FindKernel("TransferToGlobal");
        k_initLocRef = computeShader.FindKernel("InitializeLocalReference");
        k_singleThread = computeShader.FindKernel("SingleThread");
        k_initRayRef = computeShader.FindKernel("InitializeRayReference");
        k_initGlobHeight = computeShader.FindKernel("InitializeGlobalHeightBuffer");
        k_initGlobalSolid = computeShader.FindKernel("InitializeGlobalSolidBuffer");
        k_initLocTransBuff = computeShader.FindKernel("InitializeLocalTransferBuffers");
        k_initHash = computeShader.FindKernel("InitializeHashTransferBuffer");
        k_noise = computeShader.FindKernel("Noise");
        k_locVisCalc = computeShader.FindKernel("LocalVisibilityCalcs");
        k_fullVisCalcs = computeShader.FindKernel("FullVisCalcs");
        k_fullCull = computeShader.FindKernel("FullCull");
        #endregion

        b_chunkEdge = new ComputeBuffer(chunkCount, sizeof(uint));
        b_chunkPosition = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
        computeShader.SetBuffer(k_initChunkRef, "_ChunkEdgeTable", b_chunkEdge);
        computeShader.SetBuffer(k_initChunkRef, "_ChunkPositionTable", b_chunkPosition);
        computeShader.Dispatch(k_initChunkRef, Mathf.CeilToInt(chunkCount / 1024f), 1, 1);
        //b_chunkEdge.GetData(chunkEdgeTable);
        b_chunkEdge.Release();
        b_chunkPosition.GetData(chunkPositionTable);

        b_locPos = new ComputeBuffer(localChunkSize, sizeof(uint) * 3);
        b_locEdge = new ComputeBuffer(localChunkSize, sizeof(uint) * 3);
        computeShader.SetBuffer(k_initLocRef, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_initLocRef, "_LocalEdgeBuffer", b_locEdge);
        computeShader.Dispatch(k_initLocRef, dispatchGroups, 1, 1);

        b_temp = new ComputeBuffer(localChunkSize, sizeof(uint) * 2);
        computeShader.SetBuffer(k_singleThread, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_singleThread, "_TempTable", b_temp);
        computeShader.Dispatch(k_singleThread, 1, 1, 1);

        computeShader.SetBuffer(k_initRayRef, "_LocalEdgeBuffer", b_locEdge);
        computeShader.SetBuffer(k_initRayRef, "_TempTable", b_temp);
        computeShader.SetBuffer(k_initRayRef, "_LocalPositionBuffer", b_locPos);
        computeShader.Dispatch(k_initRayRef, dispatchGroups, 1, 1);
        b_temp.Release();

        b_globalHeight = new ComputeBuffer(Mathf.CeilToInt(globalLeadingEdgeCount * chunkStepDepth * 1f / HeightPackedSize()), sizeof(uint));
        computeShader.SetBuffer(k_initGlobHeight, "_GlobalHeightTable", b_globalHeight);
        computeShader.Dispatch(k_initGlobHeight, Mathf.CeilToInt(b_globalHeight.count / 1024f), 1, 1);

        b_globalSolid = new ComputeBuffer(Mathf.CeilToInt(globalLeadingEdgeCount * chunkStepDepth * 1f / SolidPackedSize()), sizeof(uint));
        computeShader.SetBuffer(k_initGlobalSolid, "GlobalSolidBuffer", b_globalSolid);
        computeShader.Dispatch(k_initGlobalSolid, Mathf.CeilToInt(b_globalSolid.count / 1024f), 1, 1);

        b_heightTransfer = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
        b_solidTransfer = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
        computeShader.SetBuffer(k_initLocTransBuff, "HeightTransferBuffer", b_heightTransfer);
        computeShader.SetBuffer(k_initLocTransBuff, "SolidTransferBuffer", b_solidTransfer);
        computeShader.Dispatch(k_initLocTransBuff, edgeDispatchGroups, 1, 1);

        hashTransferBuffer = new ComputeBuffer((int)Mathf.Pow(2, HeightToBits(maximumActiveChunkSize * length * width * height)), sizeof(uint) * 2);
        computeShader.SetInt("e_hashBufferSize", hashTransferBuffer.count);
        computeShader.SetBuffer(k_initHash, "HashTransferBuffer", hashTransferBuffer);
        computeShader.Dispatch(k_initHash, Mathf.CeilToInt(hashTransferBuffer.count / 1024f), 1, 1);
    }

    #region GenerateWorld
    public void GenerateWorld()
    {
        InitGenWorld();

        for (int i = 0; i < chunkCount; i++)
        {
            computeShader.SetInt("e_xOffset", xOffset[i]);
            computeShader.SetInt("e_yOffset", yOffset[i]);
            computeShader.SetInt("e_zOffset", zOffset[i]);

            mainBuffers[i] = new ComputeBuffer(localChunkSize, sizeof(uint) * 2);
            computeShader.SetBuffer(k_noise, "_MeshProperties", mainBuffers[i]);
            computeShader.SetBuffer(k_noise, "_LocalPositionBuffer", b_locPos);
            computeShader.Dispatch(k_noise, dispatchGroups, 1, 1);
        }
    }

    private void InitGenWorld()
    {
        computeShader.SetBuffer(k_noise, "_LocalPositionBuffer", b_locPos);
    }
    #endregion

    #region Generate Global Visibility Values
    public void GenerateVisTable()
    {
        InitGenVis();
        for (int i = chunkCount - 1; i > -1; i--)
        {
            computeShader.SetInt("currentYChunk", chunkPositionTable[i].y);
            computeShader.SetInt("chunkIndex", i);

            ResetLocalTransferBuffers();

            computeShader.SetBuffer(k_locVisCalc, "_MeshProperties", mainBuffers[i]);
            computeShader.Dispatch(k_locVisCalc, dispatchGroups, 1, 1);

            TransferLocalValuesToGlobal();

            /*
             test = new uint[solidTransferBuffer.count];
            solidTransferBuffer.GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(Convert.ToString(g, 2));
            }
             */

            /*
            test = new uint[heightTransferBuffer.count];
            heightTransferBuffer.GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(g);
            }
            */
        }

        /*
        test = new uint[globalHeightBuffer.count];
        globalHeightBuffer.GetData(test);
        Debug.Log(test.Length);
        for (int g = 0; g < test.Length; g++)
        {
            for (int i = 0; i < 8; i++)
            {
                Debug.Log((g >> (i * 4)) & 15U);
            }
        }
        */

        /*
        test = new uint[globalSolidBuffer.count];
        globalSolidBuffer.GetData(test);
        Debug.Log(test.Length);
        for (int g = 0; g < test.Length; g++)
        {
            for (int i = 0; i < 32; i++)
            {
                Debug.Log((test[g] >> i) & 1);
            }
        }
        */
    }

    private void InitGenVis()
    {
        computeShader.SetBuffer(k_locVisCalc, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_locVisCalc, "_LocalEdgeBuffer", b_locEdge);
        computeShader.SetBuffer(k_locVisCalc, "SolidTransferBuffer", b_solidTransfer);
        computeShader.SetBuffer(k_locVisCalc, "HeightTransferBuffer", b_heightTransfer);

        computeShader.SetBuffer(k_transGlob, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_transGlob, "HeightTransferBuffer", b_heightTransfer);
        computeShader.SetBuffer(k_transGlob, "_GlobalHeightTable", b_globalHeight);
        computeShader.SetBuffer(k_transGlob, "_LocalEdgeBuffer", b_locEdge);
        computeShader.SetBuffer(k_transGlob, "_ChunkPositionTable", b_chunkPosition);
        computeShader.SetBuffer(k_transGlob, "SolidTransferBuffer", b_solidTransfer);
        computeShader.SetBuffer(k_transGlob, "GlobalSolidBuffer", b_globalSolid);
    }
    private void ResetLocalTransferBuffers()
    {
        computeShader.Dispatch(k_initLocTransBuff, edgeDispatchGroups, 1, 1);
    }

    private void TransferLocalValuesToGlobal()
    {
        computeShader.Dispatch(k_transGlob, edgeDispatchGroups, 1, 1);
    }

    #endregion

    public void GlobalRendering(int cross, ref NativeArray<int2> _nativeActiveChunks)
    {

        UpdateRenderBuffers(_nativeActiveChunks.Length, bufferArraySize);

        int crossYChunk = CrossYChunk(cross);
        computeShader.SetInt("e_crossYChunk", crossYChunk);
        computeShader.SetInt("e_localCrossHeight", LocalCrossHeight(cross));
        computeShader.SetInt("e_trueCrossHeight", cross);
        computeShader.SetInt("e_activeDepth", crossYChunk - activeDepth);
 
        if (crossYChunk <= (activeDepth -1))
        {
            computeShader.SetInt("e_activeDepth", -1);
        }
        else
        {
            computeShader.SetInt("e_activeDepth", crossYChunk - activeDepth);
        }

        RefVisCalcs(ref _nativeActiveChunks);

        /*
         test = new uint[hashTransferBuffer.count * 2];
        hashTransferBuffer.GetData(test);
        for (int g = 1; g < test.Length; g += 2)
        {
            Debug.Log(test[g]);
        }
         */

        RefDispatch(ref _nativeActiveChunks);

        /*
         test = new uint[hashTransferBuffer.count * 2];
        hashTransferBuffer.GetData(test);
        for (int i = 1; i < test.Length; i += 2)
        {
            Debug.Log(test[i]);
        }
         */
    }

    private void RefVisCalcs(ref NativeArray<int2> __nativeActiveChunks)
    {
        computeShader.SetBuffer(k_fullVisCalcs, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_fullVisCalcs, "_ChunkPositionTable", b_chunkPosition);
        computeShader.SetBuffer(k_fullVisCalcs, "_LocalEdgeBuffer", b_locEdge);
        computeShader.SetBuffer(k_fullVisCalcs, "_GlobalHeightTable", b_globalHeight);
        computeShader.SetBuffer(k_fullVisCalcs, "GlobalSolidBuffer", b_globalSolid);
        computeShader.SetBuffer(k_fullVisCalcs, "HashTransferBuffer", hashTransferBuffer);

        for (int i = __nativeActiveChunks.Length - 1; i > -1; i--)
        {
            if (__nativeActiveChunks[i].y == 1)
            {
                computeShader.SetInt("chunkIndex", __nativeActiveChunks[i].x);
                computeShader.Dispatch(k_fullVisCalcs, edgeDispatchGroups, 1, 1);
            }
        }
    }

    private void RefDispatch(ref NativeArray<int2> __nativeActiveChunks)
    {
        computeShader.SetBuffer(k_fullCull, "_LocalPositionBuffer", b_locPos);
        computeShader.SetBuffer(k_fullCull, "_ChunkPositionTable", b_chunkPosition);
        computeShader.SetBuffer(k_fullCull, "_LocalEdgeBuffer", b_locEdge);
        computeShader.SetBuffer(k_fullCull, "HashTransferBuffer", hashTransferBuffer);

        for (int i = __nativeActiveChunks.Length - 1; i > -1; i--)
        {
            if (__nativeActiveChunks[i].x != 0x7FFFFFFF)
            {
                computeShader.SetInt("chunkIndex", __nativeActiveChunks[i].x);
                computeShader.SetInt("currentYChunk", chunkPositionTable[__nativeActiveChunks[i].x].y);
                ClearArgsBuffer(i);

                computeShader.SetBuffer(k_fullCull, "_ArgsBuffer", argBuffers[i]);
                computeShader.SetBuffer(k_fullCull, "_RenderBuffer", newRenderBuffers[i]);
                newRenderBuffers[i].SetCounterValue(0);

                computeShader.SetBuffer(k_fullCull, "_MeshProperties", mainBuffers[__nativeActiveChunks[i].x]);
                computeShader.Dispatch(k_fullCull, edgeDispatchGroups, 1, 1);

                propertyBlocks[i] = new MaterialPropertyBlock();
                propertyBlocks[i].SetBuffer("_RenderProperties", newRenderBuffers[i]);
                propertyBlocks[i].SetInt("e_xOffset", xOffset[__nativeActiveChunks[i].x]);
                propertyBlocks[i].SetInt("e_yOffset", yOffset[__nativeActiveChunks[i].x]);
                propertyBlocks[i].SetInt("e_zOffset", zOffset[__nativeActiveChunks[i].x]);
            }
        }
    }

    /// <summary>
    /// Updates the argsBuffer and renderBuffer arrays to ensure that they contain the correct number of compute buffers. Used to be list but dont care about resizing to save memory.
    /// The size of the buffer arrays is equalt to the maximum possible size, and are not equal to the correct size of active chunks. To accomodate this, a secondary variable bufferArraySize
    /// is used to keep track of how big the "current" buffer array size. Resets the hash transfer buffer, and resizes if it if the size has changed.
    /// </summary>
    /// <param name="_activeChunkSize"></param>
    /// this is the number of active chunks as returned by the chunking algorithm
    /// <param name="_bufferArraySize"></param>
    /// this is the the current size of the array buffers
    private void UpdateRenderBuffers(int _activeChunkSize, int _bufferArraySize)
    {
        for (int i = _activeChunkSize; i < _bufferArraySize; i++)
        {
            newRenderBuffers[i].Release();
            argBuffers[i].Release();
        }
        for (int i = _bufferArraySize; i < _activeChunkSize; i++)
        {
            newRenderBuffers[i] = new ComputeBuffer(721, sizeof(uint), ComputeBufferType.Append);
            argBuffers[i] = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        }

        if (_activeChunkSize != _bufferArraySize)
        {
            hashTransferBuffer.Release();
            hashTransferBuffer = new ComputeBuffer((int)Mathf.Pow(2, HeightToBits(_activeChunkSize * length * width * height)), sizeof(uint) * 2);
            computeShader.SetInt("e_hashBufferSize", hashTransferBuffer.count);
            computeShader.SetBuffer(k_initHash, "HashTransferBuffer", hashTransferBuffer);
        }
        computeShader.Dispatch(k_initHash, Mathf.CeilToInt(hashTransferBuffer.count / 1024f), 1, 1);

        bufferArraySize = _activeChunkSize;
    }

    private int LocalCrossHeight(int _cross)
    {
        return _cross % height;
    }

    private int CrossYChunk(int _cross)
    {
        return Mathf.FloorToInt(_cross / height);
    }

    private void ClearArgsBuffer(int index)
    {
        argBuffers[index].SetData(args);
    }

    public ComputeBuffer[] GetArgsBuffer()
    {
        return argBuffers;
    }

    public ref MaterialPropertyBlock[] GetPropertyBlocks()
    {
        return ref propertyBlocks;
    }

    public ref ComputeBuffer GetInteriorChunkBuffer()
    {
        return ref b_locPos;
    }

    public ref Vector3Int[] GetChunkPositionTable()
    {
        return ref chunkPositionTable;
    }

    public void ReleaseBuffers()
    {
        b_chunkPosition.Release();
        b_locPos.Release();
        b_locEdge.Release();
        b_globalHeight.Release();
        b_heightTransfer.Release();
        b_globalSolid.Release();
        b_solidTransfer.Release();
        hashTransferBuffer.Release();


        for (int i = 0; i < chunkCount; i++)
        {
            if (mainBuffers[i] != null)
            {
                mainBuffers[i].Release();
            }
        }

        for (int i = 0; i < newRenderBuffers.Length; i++)
        {
            if (newRenderBuffers[i] != null)
            {
                newRenderBuffers[i].Release();
                argBuffers[i].Release();
            }
        }
    }

    //return the mininum number of bits to store height
    private int HeightToBits(int _height)
    {
        _height--;
        int counter = 0;

        while (_height > 0)
        {
            _height >>= 1;
            counter++;
        }

        return counter;
    }

    //return the number of height sequences that can be stored in a 32bit uint
    private int HeightPackedSize()
    {
        return Mathf.FloorToInt(32U / HeightToBits(height));
    }

    //returns the number of solid sequences that can be stored in a 32bit uint 
    //more for my own reference
    private int SolidPackedSize()
    {
        return 32;
    }
}

