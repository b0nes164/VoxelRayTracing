using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class WorldGeneration
{
    private bool render;
    private ComputeShader computeShader;
    private bool chunkInfo;

    private static readonly int xChunks = 1;
    private static readonly int yChunks = 2;
    private static readonly int zChunks = 1;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 16;
    private static int height = 16;
    private static int width = 16;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int step = (width * height) + width + 1;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private ComputeBuffer chunkEdgeBuffer;
    private ComputeBuffer chunkPositionBuffer;
    private uint[] chunkEdgeTable = new uint[chunkCount];
    private Vector3Int[] chunkPositionTable = new Vector3Int[chunkCount];
    private ComputeBuffer[] dummyBuffersOne = new ComputeBuffer[7];
    private ComputeBuffer[] dummyBuffersTwo = new ComputeBuffer[7];
    private ComputeBuffer[] altDummyBuffers = new ComputeBuffer[7];

    private ComputeBuffer cubeBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer tempBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] visibilityBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[][] count = new int[chunkCount][];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int[] xOffset = new int[chunkCount];
    private int[] yOffset = new int[chunkCount];
    private int[] zOffset = new int[chunkCount];

    private bool[] nullChecks = new bool[chunkCount];
    private bool[][] renderCalcCheck = new bool[chunkCount][];

    private uint[] test;

    public WorldGeneration(bool _render, bool _chunkInfo, ComputeShader _computeShader)
    {
        render = _render;
        chunkInfo = _chunkInfo;
        computeShader = _computeShader;
        InitializeShaderValues();
    }

    private void InitializeShaderValues()
    {
        computeShader.SetInt("stepIndex", step);
        computeShader.SetInt("cubeCount", cubeCount);
        // ?? check this
        computeShader.SetInt("height", height);
        //
        computeShader.SetBool("limitingAxis", width <= height);
        computeShader.SetInt("leadingEdgeCount", leadingEdgeCount);
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset[i] = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset[i] = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset[i] = (i % zChunks) * width;

            

            count[i] = new int[1];
            renderCalcCheck[i] = new bool[height * yChunks];

            nullChecks[i] = false;

            int initializeVisTableKernel = computeShader.FindKernel("InitializeChunkVisibilityTable");
            visibilityBuffers[i] = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
            computeShader.SetBuffer(initializeVisTableKernel, "_ChunkVisibilityTables", visibilityBuffers[i]);
            computeShader.Dispatch(initializeVisTableKernel, Mathf.CeilToInt(leadingEdgeCount / 1024f), 1, 1);
            /*
            test = new uint[leadingEdgeCount];
            visibilityBuffers[i].GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(Convert.ToString(g, 2));
            }
            */
        }

        computeShader.SetInt("xChunks", xChunks);
        computeShader.SetInt("yChunks", yChunks);
        computeShader.SetInt("zChunks", zChunks);
        int initilializeChunkKernel = computeShader.FindKernel("InitializeChunks");
        chunkEdgeBuffer = new ComputeBuffer(chunkCount, sizeof(uint));
        chunkPositionBuffer = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkPositionTable", chunkPositionBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(chunkCount / 1024f), 1, 1);
        chunkEdgeBuffer.GetData(chunkEdgeTable);
        chunkPositionBuffer.GetData(chunkPositionTable);

        computeShader.SetInt("length", length);
        computeShader.SetInt("height", height);
        computeShader.SetInt("width", width);
        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        cubeBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);

        computeShader.SetInt("counter", 0);
        int singleThreadKernel = computeShader.FindKernel("SingleThread");
        tempBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(singleThreadKernel, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(singleThreadKernel, "_TempTable", tempBuffer);
        computeShader.Dispatch(singleThreadKernel, 1, 1, 1);

        int initializeRaysKernel = computeShader.FindKernel("InitializeRays");
        computeShader.SetBuffer(initializeRaysKernel, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(initializeRaysKernel, "_TempTable", tempBuffer);
        computeShader.Dispatch(initializeRaysKernel, dispatchGroups, 1, 1);

        int initializeRaysKernelTwo = computeShader.FindKernel("InitializeRaysTwo");
        computeShader.SetBuffer(initializeRaysKernelTwo, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(initializeRaysKernelTwo, "_TempTable", tempBuffer);
        computeShader.Dispatch(initializeRaysKernelTwo, dispatchGroups, 1, 1);
        tempBuffer.Release();

        for (int i = 0; i < dummyBuffersOne.Length; i++)
        {
            int initializeDummyKernel = computeShader.FindKernel("InitializeDummyChunks");
            dummyBuffersOne[i] = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
            dummyBuffersTwo[i] = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
            altDummyBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint) * 2);
            computeShader.SetBuffer(initializeDummyKernel, "_DummyChunkOne", dummyBuffersOne[i]);
            computeShader.SetBuffer(initializeDummyKernel, "_DummyChunkTwo", dummyBuffersTwo[i]);
            computeShader.SetBuffer(initializeDummyKernel, "_AltDummyChunk", altDummyBuffers[i]);
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

    public void GenerateVisTable()
    {
        for (int i = chunkCount; i > 0; i--)
        {
            int index = i - 1;

            int chunkVisTableKernel = computeShader.FindKernel("ChunkVisibilityTable");
            computeShader.SetBuffer(chunkVisTableKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernel, "_ChunkTable", cubeBuffer);
            computeShader.SetBuffer(chunkVisTableKernel, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernel, "_EdgeTable", edgeBuffer);
            computeShader.Dispatch(chunkVisTableKernel, dispatchGroups, 1, 1);

            int chunkVisTableKernelTwo = computeShader.FindKernel("ChunkVisibilityTableTwo");
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_ChunkTable", cubeBuffer);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_EdgeTable", edgeBuffer);
            computeShader.Dispatch(chunkVisTableKernelTwo, dispatchGroups, 1, 1);

            /*
             test = new uint[leadingEdgeCount];
            visibilityBuffers[index].GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(Convert.ToString(g, 2));
            }
            */

            computeShader.SetInt("currentYChunk", chunkPositionTable[index].y);
            computeShader.SetInt("chunkIndex", index);

            int chunkGlobalVisKernel = computeShader.FindKernel("ChunkGlobalVis");
            computeShader.SetBuffer(chunkGlobalVisKernel, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkGlobalVisKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(chunkGlobalVisKernel, "_ChunkEdgeTable", chunkEdgeBuffer);

            int chunkGlobalVisKernelTwo = computeShader.FindKernel("ChunkGlobalVisTwo");
            computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_ChunkEdgeTable", chunkEdgeBuffer);

            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", visibilityBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks + 1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", visibilityBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(chunkGlobalVisKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(chunkGlobalVisKernelTwo, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
            }
            computeShader.Dispatch(chunkGlobalVisKernel, yChunks, 1, 1);
            computeShader.Dispatch(chunkGlobalVisKernelTwo, yChunks, 1, 1);

            test = new uint[leadingEdgeCount];
            visibilityBuffers[index].GetData(test);
            foreach (uint g in test)
            {
                Debug.Log("Chunk Index: " + index + ", " + Convert.ToString(g >> 5, 2));
            }
        }
    }

    public void GenerateMeshProperties()
    {
        if (render)
        {
            for (int i = chunkCount; i > 0; i--)
            {
                int index = i - 1;

                computeShader.SetInt("crossHeight", height);

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
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                        break;
                    //Top Left
                    case 1:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Diagonal Middle
                    case 2:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                        break;
                    //Middle Left
                    case 3:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Top Right
                    case 4:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                        break;
                    //Top Middle
                    case 5:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    //Middle right
                    case 6:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                        break;
                    //inside
                    case 7:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", mainBuffers[index + (zChunks * yChunks) + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[index + (zChunks * yChunks) + zChunks]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", mainBuffers[index + zChunks + 1]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", mainBuffers[index + zChunks]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[index + 1]);
                        break;
                    default:
                        computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                        computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                        computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                        computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                        computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
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
            computeShader.SetBool("topEdge", false);
            for (int i = 0; i < chunkCount; i++)
            {
                computeShader.SetInt("crossHeight", 1);

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
            Debug.Log("Chunk: " + index + ", " + count[index][0]);
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
                    HeightDispatch((cross - 1) % height, cross, index, true);
                }
                else
                {
                    if (cross == chunkPositionTable[index].y * height + height)
                    {
                        HeightDispatch(height - 1, cross, index, false);
                    }
                    else
                    {
                        if (cross > chunkPositionTable[index].y * height)
                        {
                            HeightDispatch((cross - 1) % height, cross, index, false);
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
            computeShader.SetBool("topEdge", topEdge);
            computeShader.SetInt("currentYChunk", Mathf.FloorToInt(cross / height));
            computeShader.SetInt("yOffset", yOffset[index]);
            computeShader.SetInt("trueHeight", cross);

            int stupidCullKernel = computeShader.FindKernel("StupidCull");
            computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(stupidCullKernel, "_ChunkVisibilityTables", visibilityBuffers[index]);

            int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
            computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(stupidCullTwoKernel, "_ChunkVisibilityTables", visibilityBuffers[index]);


            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", visibilityBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", visibilityBuffers[index + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", visibilityBuffers[index + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", visibilityBuffers[index + zChunks]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffersOne[0]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffersOne[1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopRightChunk", dummyBuffersOne[4]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleTopChunk", dummyBuffersOne[5]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_DiagonalMiddleChunk", dummyBuffersOne[2]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleLeftChunk", dummyBuffersOne[3]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffersOne[6]);
                    break;
            }
            computeShader.Dispatch(stupidCullKernel, dispatchGroups, 1, 1);
            //computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
        }
        else
        {
            computeShader.SetBool("topEdge", topEdge);
            int coolKernel = computeShader.FindKernel("CoolCull");
            computeShader.SetBuffer(coolKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(coolKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(coolKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(coolKernel, "_ChunkTable", cubeBuffer);
            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", altDummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", altDummyBuffers[2]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", altDummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", altDummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", altDummyBuffers[2]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", altDummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", altDummyBuffers[2]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", mainBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", altDummyBuffers[1]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", mainBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", mainBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", mainBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(coolKernel, "_IntDiagonalMiddleChunk", altDummyBuffers[0]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleLeftChunk", altDummyBuffers[1]);
                    computeShader.SetBuffer(coolKernel, "_IntMiddleRightChunk", altDummyBuffers[2]);
                    break;
            }
            computeShader.Dispatch(coolKernel, dispatchGroups, 1, 1);
            #region
            /*
             int coolKernel = computeShader.FindKernel("StoolCull");
            computeShader.SetBuffer(coolKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(coolKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(coolKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(coolKernel, "_ChunkTable", cubeBuffer);

            switch (chunkEdgeTable[index] & 0x7)
            {
                //Diagonal Top
                case 0:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[3]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Top Left
                case 1:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[3]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Diagonal Middle
                case 2:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[3]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Middle Left
                case 3:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[3]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Top Right
                case 4:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //Top Middle
                case 5:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                //Middle right
                case 6:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
                //inside
                case 7:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", visibilityBuffers[index + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", visibilityBuffers[index + (zChunks * yChunks)]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", visibilityBuffers[index + 1]);
                    break;
                default:
                    computeShader.SetBuffer(coolKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                    computeShader.SetBuffer(coolKernel, "_MiddleLeftChunk", dummyBuffers[3]);
                    computeShader.SetBuffer(coolKernel, "_MiddleRightChunk", dummyBuffers[6]);
                    break;
            }
             */
            #endregion
        }

        countBuffer.GetData(count[index]);
        countBuffer.Release();
        renderCalcCheck[index][cross - 1] = true;
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
        chunkPositionBuffer.Release();
        chunkEdgeBuffer.Release();
        cubeBuffer.Release();
        edgeBuffer.Release();

        foreach (ComputeBuffer c in dummyBuffersOne)
        {
            c.Release();
        }

        foreach (ComputeBuffer c in dummyBuffersTwo)
        {
            c.Release();
        }

        foreach (ComputeBuffer g in altDummyBuffers)
        {
            g.Release();
        }

        for (int i = 0; i < chunkCount; i++)
        {
            mainBuffers[i].Release();
            visibilityBuffers[i].Release();

            if (renderBuffers[i] != null)
            {
                renderBuffers[i].Release();
            }
        }
    }

    public struct MeshProperties
    {
        public uint lowIndex;
        public uint highIndex;
    }
}
