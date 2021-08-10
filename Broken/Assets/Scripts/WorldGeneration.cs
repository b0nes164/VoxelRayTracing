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
    private bool render;
    private ComputeShader computeShader;
    private bool chunkInfo;

    private static readonly int xChunks = 2;
    private static readonly int yChunks = 1;
    private static readonly int zChunks = 2;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 16;
    private static int height = 16;
    private static int width = 16;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int step = (width * height) + width + 1;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private static int globalLength = xChunks * length;
    private static int globalHeight = yChunks * height;
    private static int globalWidth = zChunks * width;
    private static int globalLeadingEdgeCount = (globalLength * globalWidth) + (globalLength * (globalHeight - 1)) + ((globalWidth - 1) * (globalHeight - 1));
    private static int globalStep = (globalWidth * globalHeight) + globalWidth + 1;
    private static int chunkStepDepth = Mathf.Min(Mathf.Min(xChunks, yChunks), zChunks);

    private static int chunkSizeX = xChunks;
    private static int chunkSizeY = yChunks;
    private static int chunkSizeZ = zChunks;

    private int crossYChunk = 1000000000; 

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
    private ComputeBuffer globalHeightBuffer;
    private ComputeBuffer heightTransferBuffer;
    private ComputeBuffer globalSolidBuffer;
    private ComputeBuffer solidTransferBuffer;
    private ComputeBuffer hashTransferBuffer;

    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] visibilityBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[][] count = new int[chunkCount][];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];


    private ComputeBuffer CalcBuffer;
    private List<ChunkStruct> chunkList = new List<ChunkStruct>();

    private int[] xOffset = new int[chunkCount];
    private int[] yOffset = new int[chunkCount];
    private int[] zOffset = new int[chunkCount];

    private bool[] nullChecks = new bool[chunkCount];
    private bool[][] renderCalcCheck = new bool[chunkCount][];

    private ComputeBuffer bugBugger;
    private uint[] test;



    #region kernel
    private int initializeGlobalSolidsKernel;
    private int initializeLocalTransferKernel;
    private int transferGlobalHeightsKernel;

    private int lowKern;
    private int botLeftkern;
    private int botRightKern;
    private int centBotKern;
    private int diagMidKern;
    private int midLeftKern;
    private int midRightKern;
    private int shadowKern;
    private int finalCullKern;
    private int initHashKern;
    private int clearMeshKern;
    #endregion

    //shove all kern inits into one method
    //change the buffers so they are all only called once
    //change names in compute shader
    //delete old methods
    //change count to a uint
    // change the name of chunktable to localpositiontable in this file and the render manager

    public WorldGeneration(bool _render, bool _chunkInfo, ComputeShader _computeShader)
    {
        render = _render;
        chunkInfo = _chunkInfo;
        computeShader = _computeShader;
        InitializeShaderValues();
    }

    private void InitializeShaderValues()
    {
        bugBugger = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
        computeShader.SetInt("stepIndex", step);
        computeShader.SetInt("cubeCount", cubeCount);
        computeShader.SetInt("xChunks", xChunks);
        computeShader.SetInt("yChunks", yChunks);
        computeShader.SetInt("zChunks", zChunks);
        computeShader.SetInt("length", length);
        computeShader.SetInt("height", height);
        computeShader.SetInt("width", width);
        computeShader.SetInt("e_heightPackedSize", HeightPackedSize());
        computeShader.SetInt("e_solidPackedSize", SolidPackedSize());
        //change to be filled by a method later
        computeShader.SetInt("e_heightSizeInBits", 4);
        computeShader.SetInt("e_solidSizeInBits", 1);
        computeShader.SetInt("leadingEdgeCount", leadingEdgeCount);
        computeShader.SetInt("globalLength", globalLength);
        computeShader.SetInt("globalHeight", globalHeight);
        computeShader.SetInt("globalWidth", globalWidth);
        computeShader.SetInt("globalStep", globalStep);
        computeShader.SetInt("e_chunkStepDepth", chunkStepDepth);

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

        int initilializeChunkKernel = computeShader.FindKernel("InitializeChunks");
        chunkEdgeBuffer = new ComputeBuffer(chunkCount, sizeof(uint));
        chunkPositionBuffer = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkPositionTable", chunkPositionBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(chunkCount / 1024f), 1, 1);
        chunkEdgeBuffer.GetData(chunkEdgeTable);
        chunkPositionBuffer.GetData(chunkPositionTable);

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

        int initializeGlobalHeightKernel = computeShader.FindKernel("InitializeGlobalHeightTable");
        globalHeightBuffer = new ComputeBuffer(Mathf.CeilToInt(globalLeadingEdgeCount * chunkStepDepth * 1f / HeightPackedSize()), sizeof(uint));
        computeShader.SetBuffer(initializeGlobalHeightKernel, "_GlobalHeightTable", globalHeightBuffer);
        computeShader.Dispatch(initializeGlobalHeightKernel, Mathf.CeilToInt(globalHeightBuffer.count / 1024f), 1, 1);

        InitKernLocalTransferBuffers();
        InitKernTransferGlobalHeights();
        InitKernGlobalSolids();
        InitKernChunkList();
        InitHashTransferBuffer();

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

        PopulateChunkList();
        SortChunkList(chunkList);
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
            computeShader.SetInt("currentYChunk", chunkPositionTable[index].y);
            computeShader.SetInt("chunkIndex", index);

            int chunkVisTableKernel = computeShader.FindKernel("ChunkVisibilityTable");
            computeShader.SetBuffer(chunkVisTableKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernel, "_ChunkTable", cubeBuffer);
            computeShader.SetBuffer(chunkVisTableKernel, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(chunkVisTableKernel, "SolidTransferBuffer", solidTransferBuffer);
            computeShader.SetBuffer(chunkVisTableKernel, "HeightTransferBuffer", heightTransferBuffer);
            computeShader.SetBuffer(chunkVisTableKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
            computeShader.Dispatch(chunkVisTableKernel, dispatchGroups, 1, 1);

            int chunkVisTableKernelTwo = computeShader.FindKernel("ChunkVisibilityTableTwo");
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_ChunkTable", cubeBuffer);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_ChunkVisibilityTables", visibilityBuffers[index]);
            computeShader.SetBuffer(chunkVisTableKernelTwo, "_EdgeTable", edgeBuffer);
            computeShader.Dispatch(chunkVisTableKernelTwo, dispatchGroups, 1, 1);

            DispatchTransferGlobalHeights();

            /*
             test = new uint[solidTransferBuffer.count];
            solidTransferBuffer.GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(Convert.ToString(g, 2));
            }
             */

            ResetLocalTransferBuffers();

            /*
             test = new uint[leadingEdgeCount];
            visibilityBuffers[index].GetData(test);
            foreach (uint g in test)
            {
                //g & 0xF
                //g & 16U >> 4
                Debug.Log((g & 16U) >> 4);
            }
             */

            /*
             test = new uint[bugBugger.count];
            bugBugger.GetData(test);
            foreach (uint g in test)
            {
                Debug.Log(g);
            }
             */

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

            /*
             test = new uint[leadingEdgeCount];
            visibilityBuffers[index].GetData(test);
            Debug.Log(test.Length);
            foreach (uint g in test)
            {
                Debug.Log("Chunk Index: " + index + ", " + Convert.ToString(g >> 5, 2));
            }
             */
        }

        /*
         test = new uint[globalHeightBuffer.count];
        globalHeightBuffer.GetData(test);
        Debug.Log(test.Length);
        for (int g = 0; g < test.Length; g++)
        {
            Debug.Log(Convert.ToString(test[g], 2));
        }
         */

        /*
         test = new uint[globalSolidBuffer.count];
        globalSolidBuffer.GetData(test);
        Debug.Log(test.Length);
        for (int g = 0; g < test.Length; g++)
        {
            Debug.Log(Convert.ToString(test[g], 2));
        }
         */
    }

    public void GenerateMeshProperties()
    {
        if (render)
        {
            for (int i = chunkCount; i > 0; i--)
            {
                int index = i - 1;

                computeShader.SetInt("e_localCrossHeight", height);

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
                computeShader.SetInt("e_localCrossHeight", 1);

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
        computeShader.SetInt("e_localCrossHeight", chunkCross);
        int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
        countBuffer = new ComputeBuffer(1, sizeof(int));
        computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
        computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

        if (topEdge)
        {
            computeShader.SetBool("topEdge", topEdge);
            computeShader.SetInt("currentYChunk", Mathf.FloorToInt(cross / height));
            // will need to be changed
            computeShader.SetInt("sameLevelOffset", yOffset[index]);
            computeShader.SetInt("topOffset", yOffset[index] + 16);
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
            computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
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
        }

        countBuffer.GetData(count[index]);
        countBuffer.Release();
        renderCalcCheck[index][cross - 1] = true;
        GenerateRenderProperties(index);
    }


    public void GlobalRendering(int cross)
    {
        bool recalculate = false;

        if (crossYChunk != CrossYChunk(cross))
        {
            crossYChunk = CrossYChunk(cross);
            computeShader.SetInt("e_crossYChunk", crossYChunk);
            recalculate = true;
        }
            
        computeShader.SetInt("e_localCrossHeight", LocalCrossHeight(cross));
        for (int i = chunkCount; i > 0; i--)
        {
            int index = i - 1;
            nullChecks[index] = false;

            if (cross > chunkPositionTable[index].y * height)
            {
                GlobalDispatch(index, recalculate);
            }
        }
    }

    private void GlobalDispatch(int index, bool _recalculate)
    {
        computeShader.SetInt("chunkIndex", index);
        computeShader.SetBool("topEdge", false);

        ResetCountBuffer();
        ClearMeshBuffer(index);

        computeShader.SetBuffer(shadowKern, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(shadowKern, "_ChunkPositionTable", chunkPositionBuffer);
        computeShader.SetBuffer(shadowKern, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(shadowKern, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(shadowKern, "_GlobalHeightTable", globalHeightBuffer);
        computeShader.SetBuffer(shadowKern, "GlobalSolidBuffer", globalSolidBuffer);
        computeShader.SetBuffer(shadowKern, "HashTransferBuffer", hashTransferBuffer);
        computeShader.Dispatch(shadowKern, Mathf.CeilToInt(leadingEdgeCount / 768f), 1, 1);

        if (_recalculate)
        {
            
        }

        /*
         * test = new uint[hashTransferBuffer.count * 2];
        hashTransferBuffer.GetData(test);

        for (int i = 1; i < test.Length; i += 2)
        {
            Debug.Log(test[i]);
        }
         */




        computeShader.SetBuffer(finalCullKern, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(finalCullKern, "_ChunkPositionTable", chunkPositionBuffer);
        computeShader.SetBuffer(finalCullKern, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(finalCullKern, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(finalCullKern, "HashTransferBuffer", hashTransferBuffer);
        computeShader.SetBuffer(finalCullKern, "_Counter", countBuffer);
        computeShader.SetBuffer(finalCullKern, "_MeshProperties", mainBuffers[index]);
        computeShader.Dispatch(finalCullKern, Mathf.CeilToInt(leadingEdgeCount / 768f), 1, 1);

        countBuffer.GetData(count[index]);
        countBuffer.Release();
        GenerateRenderProperties(index);
    }

    private int LocalCrossHeight(int _cross)
    {
        return (_cross - 1) % height;
    }

    private int CrossYChunk(int _cross)
    {
        return Mathf.FloorToInt((_cross - 1)/ height);
    }

    //This will need to be changed alongside new buffer setting paradigms.
    //CHANGE
    private void ResetCountBuffer()
    {
        int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
        countBuffer = new ComputeBuffer(1, sizeof(int));
        computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
        computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);
    }
    private void ClearMeshBuffer(int _index)
    {
        computeShader.SetBuffer(clearMeshKern, "_MeshProperties", mainBuffers[_index]);
        computeShader.Dispatch(clearMeshKern, Mathf.CeilToInt(cubeCount / 1024f), 1, 1);
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
        globalHeightBuffer.Release();
        heightTransferBuffer.Release();
        globalSolidBuffer.Release();
        solidTransferBuffer.Release();
        bugBugger.Release();
        hashTransferBuffer.Release();

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

    #region buffer methods
    private void InitKernLocalTransferBuffers()
    {
        heightTransferBuffer = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
        solidTransferBuffer = new ComputeBuffer(leadingEdgeCount, sizeof(uint));
        initializeLocalTransferKernel = computeShader.FindKernel("InitializeLocalTransferBuffers");
        ResetLocalTransferBuffers();
    }

    private void ResetLocalTransferBuffers()
    {
        computeShader.SetBuffer(initializeLocalTransferKernel, "HeightTransferBuffer", heightTransferBuffer);
        computeShader.SetBuffer(initializeLocalTransferKernel, "SolidTransferBuffer", solidTransferBuffer);
        computeShader.Dispatch(initializeLocalTransferKernel, Mathf.CeilToInt(leadingEdgeCount / 1024f), 1, 1);
    }

    private void InitKernGlobalSolids()
    {
        globalSolidBuffer = new ComputeBuffer(Mathf.CeilToInt(globalLeadingEdgeCount * chunkStepDepth * 1f / SolidPackedSize()), sizeof(uint));
        initializeGlobalSolidsKernel = computeShader.FindKernel("InitializeGlobalSolidBuffer");
        computeShader.SetBuffer(initializeGlobalSolidsKernel, "GlobalSolidBuffer", globalSolidBuffer);
        computeShader.Dispatch(initializeGlobalSolidsKernel, Mathf.CeilToInt(leadingEdgeCount / 1024f), 1, 1);
    }

    private void InitKernTransferGlobalHeights()
    {
        transferGlobalHeightsKernel = computeShader.FindKernel("TransferGlobalHeights");
    }

    //CHANGE THIS TO ADD ALL KERNELS TO ONE INIT
    private void InitKernChunkList()
    {
        clearMeshKern = computeShader.FindKernel("ClearMeshProperties");
        lowKern = computeShader.FindKernel("LowVis");
        botLeftkern = computeShader.FindKernel("BottomLeftVis");
        botRightKern = computeShader.FindKernel("BottomRightVis");
        centBotKern = computeShader.FindKernel("CenterBottomVis");
        diagMidKern = computeShader.FindKernel("DiagonalMiddleVis");
        midLeftKern = computeShader.FindKernel("MiddleLeftVis");
        midRightKern = computeShader.FindKernel("MiddleRightVis");

        initHashKern = computeShader.FindKernel("InitializeHashTransferBuffer");
        shadowKern = computeShader.FindKernel("GlobalShadowCalcs");
        finalCullKern = computeShader.FindKernel("FinalCull");
    }

    private void InitHashTransferBuffer()
    {
        int x = chunkSizeX * length;
        int y = chunkSizeY * height;
        int z = chunkSizeZ * width;
        int size = (x * z) + (x * (y - 1)) + ((z - 1) * (y - 1));
        hashTransferBuffer = new ComputeBuffer((int)Mathf.Pow(2, HeightToBits(size)), sizeof(uint) * 2);
        computeShader.SetInt("e_hashBufferSize", hashTransferBuffer.count);

        computeShader.SetBuffer(initHashKern, "HashTransferBuffer", hashTransferBuffer);
        computeShader.Dispatch(initHashKern, Mathf.CeilToInt(hashTransferBuffer.count / 1024f), 1, 1);
    }

    private void DispatchTransferGlobalHeights()
    {
        computeShader.SetBuffer(transferGlobalHeightsKernel, "_ChunkTable", cubeBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "HeightTransferBuffer", heightTransferBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "_GlobalHeightTable", globalHeightBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "_ChunkPositionTable", chunkPositionBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "SolidTransferBuffer", solidTransferBuffer);
        computeShader.SetBuffer(transferGlobalHeightsKernel, "GlobalSolidBuffer", globalSolidBuffer);
        computeShader.Dispatch(transferGlobalHeightsKernel, Mathf.CeilToInt(leadingEdgeCount / 1024f), 1, 1);
    }

    #endregion

    //use this method to push all chunk indexes into the chunking list until proper chunking system is developed
    /*
     private void populateChunkList(int currentYChunk)
    {
        for (int i = 0; i < xChunks * yChunks; i++)
        {

        }
    }
     */

    private void PopulateChunkList()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            chunkList.Add(new ChunkStruct(i, 0));
        }
    }

    private void clearChunkList()
    {
        chunkList.Clear();
    }


    //takes the inputs from the chunking method
    private void SortChunkList(List<ChunkStruct> _chunkList)
    {
        int lowIndex = _chunkList[0].Index;
        int highIndex = _chunkList[_chunkList.Count - 1].Index;

        for (int i = 0; i < _chunkList.Count; i++)
        {
            if (chunkPositionTable[_chunkList[i].Index].y == chunkPositionTable[lowIndex].y)
            {
                if (chunkPositionTable[_chunkList[i].Index].z == chunkPositionTable[lowIndex].z)
                {
                    if (chunkPositionTable[_chunkList[i].Index].x == chunkPositionTable[lowIndex].x)
                    {
                        //This is the lowest index
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 0);
                    }
                    else
                    {
                        //bottom left
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 1);
                    }
                }
                else
                {
                    if (chunkPositionTable[_chunkList[i].Index].x == chunkPositionTable[lowIndex].x)
                    {
                        //bottom right
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 2);
                    }
                    else
                    {
                        //center bottom
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 3);
                    }
                }
            }
            else
            {
                if (chunkPositionTable[_chunkList[i].Index].z == chunkPositionTable[lowIndex].z)
                {
                    if (chunkPositionTable[_chunkList[i].Index].x == chunkPositionTable[lowIndex].x)
                    {
                        //diagonal middle
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 4);
                    }
                    else
                    {
                        //middle left
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 5);
                    }
                }
                else
                {
                    if (chunkPositionTable[_chunkList[i].Index].x == chunkPositionTable[lowIndex].x)
                    {
                        //middle right
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 6);
                    }
                    else
                    {
                        //not on the trailing face
                        _chunkList[i] = new ChunkStruct(_chunkList[i].Index, 7);
                    }
                }
            }
        }

        
    }

    private void CallVisCalc(ChunkStruct chunk)
    {
        switch (chunk.ChunkCase)
        {
            case 0:
                //BottomLeftDispatch();
                break;
        }
    }



    //necessary to convert the the 2d chunking process to 3d
    private int TwoDimIndexToThree(int twoDimHigh, int currentYChunk)
    {
        return (chunkPositionTable[twoDimHigh].x * yChunks * zChunks) + (currentYChunk * zChunks) + chunkPositionTable[twoDimHigh].z; 
    }


    [BurstCompile]
    private struct IndexChunkJob : IJobParallelFor
    {
        [ReadOnly]
        public int xValue;

        [ReadOnly]
        public int yValue;

        [ReadOnly]

        public NativeArray<int2> chunkListArray;

        public void Execute(int index)
        {
        }
    }

    private struct ChunkStruct
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
}
