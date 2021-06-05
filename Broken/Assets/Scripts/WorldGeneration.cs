using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGeneration
{
    private bool render;
    private ComputeShader computeShader;
    private bool chunkInfo;

    private static readonly int xChunks = 2;
    private static readonly int yChunks = 2;
    private static readonly int zChunks = 2;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 32;
    private static int height = 32;
    private static int width = 32;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int step = (width * height) + width + 1;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private ComputeBuffer chunkEdgeBuffer;
    private ComputeBuffer chunkPositionBuffer;
    private uint[] chunkEdgeTable = new uint[chunkCount];
    private Vector3Int[] chunkPositionTable = new Vector3Int[chunkCount];
    private ComputeBuffer[] dummyBuffers = new ComputeBuffer[7];

    private ComputeBuffer cubeBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[][] count = new int[chunkCount][];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int[] xOffset = new int[chunkCount];
    private int[] yOffset = new int[chunkCount];
    private int[] zOffset = new int[chunkCount];

    private bool[] nullChecks = new bool[chunkCount];
    private bool[][] renderCalcCheck = new bool[chunkCount][];

    public WorldGeneration(bool _render, bool _chunkInfo, ComputeShader _computeShader)
    {
        render = _render;
        chunkInfo = _chunkInfo;
        computeShader = _computeShader;
        InitializeShaderValues();
    }

    private void InitializeShaderValues()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset[i] = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset[i] = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset[i] = (i % zChunks) * width;

            count[i] = new int[1];
            renderCalcCheck[i] = new bool[height * yChunks];

            nullChecks[i] = false;
        }

        computeShader.SetInt("stepIndex", step);
        computeShader.SetInt("cubeCount", cubeCount);

        computeShader.SetInt("length", xChunks);
        computeShader.SetInt("height", yChunks);
        computeShader.SetInt("width", zChunks);
        int initilializeChunkKernel = computeShader.FindKernel("InitializeCubes");
        chunkEdgeBuffer = new ComputeBuffer(chunkCount, sizeof(uint));
        chunkPositionBuffer = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
        computeShader.SetBuffer(initilializeChunkKernel, "_EdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkTable", chunkPositionBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(chunkCount / 1024f), 1, 1);
        chunkEdgeBuffer.GetData(chunkEdgeTable);
        chunkEdgeBuffer.Release();
        chunkPositionBuffer.GetData(chunkPositionTable);
        chunkPositionBuffer.Release();

        computeShader.SetInt("length", length);
        computeShader.SetInt("height", height);
        computeShader.SetInt("width", width);
        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        cubeBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);

        for (int i = 0; i < dummyBuffers.Length; i++)
        {
            int initializeDummyKernel = computeShader.FindKernel("InitializeDummyChunk");
            dummyBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint) * 2);
            computeShader.SetBuffer(initializeDummyKernel, "_DummyChunk", dummyBuffers[i]);
            computeShader.Dispatch(initializeDummyKernel, dispatchGroups, 1, 1);
        }
    }

    public void GenerateWorld()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            computeShader.SetInt("xOffset", xOffset[i]);
            computeShader.SetInt("yOffset", yOffset[i]);
            computeShader.SetInt("zOffset", zOffset[i]);

            int noiseKernel = computeShader.FindKernel("Noise");
            mainBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint) * 2);
            computeShader.SetBuffer(noiseKernel, "_ChunkTable", cubeBuffer);
            computeShader.SetBuffer(noiseKernel, "_MeshProperties", mainBuffers[i]);
            computeShader.Dispatch(noiseKernel, dispatchGroups, 1, 1);
        }
    }

    public void GenerateMeshProperties()
    {
        if (render)
        {
            for (int i = chunkCount; i > 0; i--)
            {
                int index = i - 1;

                int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
                countBuffer = new ComputeBuffer(1, sizeof(int));
                computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
                computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

                int stupidCullKernel = computeShader.FindKernel("StupidCull");
                computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[index]);
                computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);

                int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
                computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[index]);
                computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);

                switch (chunkEdgeTable[index] & 0x7)
                {
                    //Diagonal Top
                    case 0:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                        break;
                    //Top Left
                    case 1:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Diagonal Middle
                    case 2:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                        break;
                    //Middle Left
                    case 3:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Top Right
                    case 4:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                        break;
                    //Top Middle
                    case 5:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Middle right
                    case 6:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                        break;
                    //inside
                    case 7:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", mainBuffers[index + (zChunks * yChunks) + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    default:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                        break;
                }

                computeShader.Dispatch(stupidCullKernel, dispatchGroups, 1, 1);
                computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count[index]);
                countBuffer.Release();

                GenerateRenderProperties(index);
            }
        }
        else
        {
            for (int i = 0; i < chunkCount; i++)
            {
                int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
                countBuffer = new ComputeBuffer(1, sizeof(int));
                computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
                computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

                int testKernel = computeShader.FindKernel("CheckFaces");
                computeShader.SetBuffer(testKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(testKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(testKernel, "_ChunkTable", cubeBuffer);
                computeShader.Dispatch(testKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count[i]);
                countBuffer.Release();

                GenerateRenderProperties(i);
            }
        }

        Debug.Log(Time.realtimeSinceStartup);
    }

    private void GenerateRenderProperties(int index)
    {
        if (count[index][0] != 0)
        {
            int renderKernel = computeShader.FindKernel("PopulateRender");
            renderBuffers[index] = new ComputeBuffer(count[index][0], sizeof(uint), ComputeBufferType.Append);
            renderBuffers[index].SetCounterValue(0);
            computeShader.SetBuffer(renderKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(renderKernel, "_RenderProperties", renderBuffers[index]);
            computeShader.Dispatch(renderKernel, dispatchGroups, 1, 1);

            nullChecks[index] = true;
        }

        if (chunkInfo)
        {
            Debug.Log("Chunk: " + index + ", " + count[0]);
        }

        propertyBlocks[index] = new MaterialPropertyBlock();
        propertyBlocks[index].SetBuffer("_RenderProperties", renderBuffers[index]);
        propertyBlocks[index].SetInt("xChunk", xOffset[index]);
        propertyBlocks[index].SetInt("yChunk", yOffset[index]);
        propertyBlocks[index].SetInt("zChunk", zOffset[index]);
    }

    public void HeightRendering(int cross)
    {
        for (int i = chunkCount; i > 0; i--)
        {
            int index = i - 1;
            nullChecks[index] = false;
            renderCalcCheck[index][cross - 1] = false;
            if (renderCalcCheck[index][cross - 1])
            {
            }
            else
            {
                if (cross > chunkPositionTable[index].y * height + height)
                {
                    HeightDispatch(height, cross - 1, index, true);
                }
                else
                {
                    if (cross == chunkPositionTable[index].y * height + height)
                    {
                        HeightDispatch(height, cross - 1, index, false);
                    }
                    else
                    {
                        if (cross > chunkPositionTable[index].y * height)
                        {
                            HeightDispatch(cross % height, cross - 1, index, false);
                        }
                    }
                }
            }
        }
    }

    private void HeightDispatch(int chunkCross, int cross, int index, bool topEdge)
    {
        computeShader.SetInt("crossHeight", chunkCross);
        int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
        countBuffer = new ComputeBuffer(1, sizeof(int));
        computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
        computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);


        if (topEdge)
        {
            int stupidCullKernel = computeShader.FindKernel("StupidCull");
            computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);

            int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
            computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);

            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", mainBuffers[index + (zChunks * yChunks) + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
            }

            computeShader.Dispatch(stupidCullKernel, dispatchGroups, 1, 1);
            computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
        }
        else
        {
            int coolKernel = computeShader.FindKernel("CoolCull");
            computeShader.SetBuffer(coolKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(coolKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(coolKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(coolKernel, "_ChunkTable", cubeBuffer);

            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[2]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[2]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[2]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[1]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[2]);
                    break;
            }
            computeShader.Dispatch(coolKernel, dispatchGroups, 1, 1);
        }

        countBuffer.GetData(count[index]);
        countBuffer.Release();
        renderCalcCheck[index][cross] = true;
        GenerateRenderProperties(index);
    }

    public ref MaterialPropertyBlock[] GetPropertyBlocks()
    {
        return ref propertyBlocks;
    }

    public ref ComputeBuffer GetInteriorChunkBuffer()
    {
        return ref cubeBuffer;
    }

    public ref ComputeBuffer[] GetRenderBuffers()
    {
        return ref renderBuffers;
    }

    public ref bool[] GetNullChecks()
    {
        return ref nullChecks;
    }

    public void ZeroNullChecks()
    {
        for (int i = 0; i < nullChecks.Length; i++)
        {
            nullChecks[i] = false;
        }
    }
    public void ReleaseRenderBuffers()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            if (renderBuffers[i] != null)
            {
                renderBuffers[i].Dispose();
            }
        }
    }

    public void ReleaseBuffers()
    {
        cubeBuffer.Release();
        edgeBuffer.Release();

        foreach (ComputeBuffer c in dummyBuffers)
        {
            c.Release();
        }

        for (int i = 0; i < chunkCount; i++)
        {
            mainBuffers[i].Release();

            if (renderBuffers[i] != null)
            {
                renderBuffers[i].Release();
            }
        }
    }
}
