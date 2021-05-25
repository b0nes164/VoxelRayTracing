using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    [SerializeField]
    private ComputeShader computeShader;

    [SerializeField]
    private GameObject prefab;

    [SerializeField]
    private Material material;

    [SerializeField]
    private Texture2D[] texInit;

    [SerializeField]
    private bool render;

    [SerializeField]
    private bool chunkInfo;

    private static readonly int xChunks = 2;
    private static readonly int yChunks = 2;
    private static readonly int zChunks = 2;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 16;
    private static int height = 16;
    private static int width = 16;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private ComputeBuffer testBuffer;
    private ComputeBuffer testBufferTwo;

    private ComputeBuffer chunkEdgeBuffer;
    private uint[] chunkTable = new uint[chunkCount];
    private ComputeBuffer[] dummyBuffers = new ComputeBuffer[7];

    private ComputeBuffer interiorChunkBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[] count = new int[1];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int[] xOffset = new int[chunkCount];
    private int[] yOffset = new int[chunkCount];
    private int[] zOffset = new int[chunkCount];

    private Bounds bounds;
    private Mesh mesh;
    private Texture2DArray texture;

    uint[] test = new uint[cubeCount];
    Vector3Int[] testTwo = new Vector3Int[cubeCount];

    void Start()
    {
        #region initializes
        bounds = new Bounds(transform.position, Vector3.one * 10000);
        computeShader.SetInt("xChunks", xChunks);
        computeShader.SetInt("yChunks", yChunks);
        computeShader.SetInt("zChunks", zChunks);
        computeShader.SetInt("length", length);
        computeShader.SetInt("height", height);
        computeShader.SetInt("width", width);
        computeShader.SetInt("cubeCount", cubeCount);

        int initilializeChunkKernel = computeShader.FindKernel("InitializeChunks");
        chunkEdgeBuffer = new ComputeBuffer(chunkCount, sizeof(uint));
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(chunkCount / 8f), 1, 1);
        chunkEdgeBuffer.GetData(chunkTable);
        chunkEdgeBuffer.Release();

        if (chunkInfo)
        {
            for (int i = 0; i < chunkTable.Length; i++)
            {
                Debug.Log("Chunk: " + i + ", " + chunkTable[i]);
            }
        }

        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        interiorChunkBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", interiorChunkBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);
        edgeBuffer.GetData(test);
        interiorChunkBuffer.GetData(testTwo);
        
        for (int i = 0; i < dummyBuffers.Length; i++)
        {
            int initializeDummyKernel = computeShader.FindKernel("InitializeDummyChunk");
            dummyBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint));
            computeShader.SetBuffer(initializeDummyKernel, "_DummyChunk", dummyBuffers[i]);
            computeShader.Dispatch(initializeDummyKernel, dispatchGroups, 1, 1);
        }

        //populate the index
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset[i] = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset[i] = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset[i] = (i % zChunks) * width;
            computeShader.SetInt("xOffset", xOffset[i]);
            computeShader.SetInt("yOffset", yOffset[i]);
            computeShader.SetInt("zOffset", zOffset[i]);

            int noiseKernel = computeShader.FindKernel("Noise");
            mainBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint));
            computeShader.SetBuffer(noiseKernel, "_ChunkTable", interiorChunkBuffer);
            computeShader.SetBuffer(noiseKernel, "_MeshProperties", mainBuffers[i]);
            computeShader.Dispatch(noiseKernel, dispatchGroups, 1, 1);
        }
        #endregion

        if (render)
        {
            for (int i = (chunkCount - 1); i > 0; i--)
            {
                if (((chunkTable[i] >> 4) & 1U) == 1)
                {
                    int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
                    countBuffer = new ComputeBuffer(1, sizeof(int));
                    computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
                    computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

                    int stupidCullKernel = computeShader.FindKernel("StupidCull");
                    computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[i]);
                    computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
                    computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);

                    int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[i]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);

                    switch (chunkTable[i] & 0x7)
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
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[i + 1]);
                            break;
                        //Diagonal Middle
                        case 2:
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                            computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                            computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                            computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[i + zChunks]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                            break;
                        //Middle Left
                        case 3:
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                            computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                            computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffers[3]);

                            computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[i + zChunks + 1]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[i + zChunks]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[i + 1]);
                            break;
                        //Top Right
                        case 4:
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                            computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                            computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[i + (zChunks * yChunks)]);

                            computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
                            break;
                        //Top Middle
                        case 5:
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                            computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffers[1]);
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[i + (zChunks * yChunks) + 1]);
                            computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[i + (zChunks * yChunks)]);

                            computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffers[5]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[i + 1]);
                            break;
                        //Middle right
                        case 6:
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffers[0]);
                            computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[i + (zChunks * yChunks) + zChunks]);
                            computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffers[2]);
                            computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[i + (zChunks * yChunks)]);

                            computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffers[4]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[i + zChunks]);
                            computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffers[6]);
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
                    countBuffer.GetData(count);

                    if (count[0] != 0)
                    {
                        int renderKernel = computeShader.FindKernel("PopulateRender");
                        renderBuffers[i] = new ComputeBuffer(count[0], sizeof(uint), ComputeBufferType.Append);
                        renderBuffers[i].SetCounterValue(0);
                        computeShader.SetBuffer(renderKernel, "_MeshProperties", mainBuffers[i]);
                        computeShader.SetBuffer(renderKernel, "_RenderProperties", renderBuffers[i]);
                        computeShader.Dispatch(renderKernel, dispatchGroups, 1, 1);
                    }

                    if (chunkInfo)
                    {
                        Debug.Log("Chunk: " + i + ", " + count[0]);
                    }

                    propertyBlocks[i] = new MaterialPropertyBlock();
                    propertyBlocks[i].SetBuffer("_RenderProperties", renderBuffers[i]);
                    propertyBlocks[i].SetInt("xChunk", xOffset[i]);
                    propertyBlocks[i].SetInt("yChunk", yOffset[i]);
                    propertyBlocks[i].SetInt("zChunk", zOffset[i]);
                    countBuffer.Release();
                }
            }
            for (int i = (chunkCount - 1); i > 0; i--)
            {
                if (((chunkTable[i] >> 4) & 1U) == 0)
                {
                    int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
                    countBuffer = new ComputeBuffer(1, sizeof(int));
                    computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
                    computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

                    int stupidCullKernel = computeShader.FindKernel("StupidCull");
                    computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[i]);
                    computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
                    computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);

                    int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[i]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);

                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", mainBuffers[i + (zChunks * yChunks) + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[i + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[i + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[i + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[i + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[i + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[i + 1]);

                    computeShader.Dispatch(stupidCullKernel, dispatchGroups, 1, 1);
                    computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
                    countBuffer.GetData(count);

                    if (count[0] != 0)
                    {
                        int renderKernel = computeShader.FindKernel("PopulateRender");
                        renderBuffers[i] = new ComputeBuffer(count[0], sizeof(uint), ComputeBufferType.Append);
                        renderBuffers[i].SetCounterValue(0);
                        computeShader.SetBuffer(renderKernel, "_MeshProperties", mainBuffers[i]);
                        computeShader.SetBuffer(renderKernel, "_RenderProperties", renderBuffers[i]);
                        computeShader.Dispatch(renderKernel, dispatchGroups, 1, 1);
                    }
                    if (chunkInfo)
                    {
                        Debug.Log("Chunk: " + i + ", " + count[0]);
                    }

                    propertyBlocks[i] = new MaterialPropertyBlock();
                    propertyBlocks[i].SetBuffer("_RenderProperties", renderBuffers[i]);
                    propertyBlocks[i].SetInt("xChunk", xOffset[i]);
                    propertyBlocks[i].SetInt("yChunk", yOffset[i]);
                    propertyBlocks[i].SetInt("zChunk", zOffset[i]);
                    countBuffer.Release();
                }
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
                computeShader.SetBuffer(testKernel, "_ChunkTable", interiorChunkBuffer);
                computeShader.Dispatch(testKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count);

                if (count[0] != 0)
                {
                    int renderKernel = computeShader.FindKernel("PopulateRender");
                    renderBuffers[i] = new ComputeBuffer(count[0], sizeof(uint), ComputeBufferType.Append);
                    renderBuffers[i].SetCounterValue(0);
                    computeShader.SetBuffer(renderKernel, "_MeshProperties", mainBuffers[i]);
                    computeShader.SetBuffer(renderKernel, "_RenderProperties", renderBuffers[i]);
                    computeShader.Dispatch(renderKernel, dispatchGroups, 1, 1);
                }

                propertyBlocks[i] = new MaterialPropertyBlock();
                propertyBlocks[i].SetBuffer("_RenderProperties", renderBuffers[i]);
                propertyBlocks[i].SetInt("xChunk", xOffset[i]);
                propertyBlocks[i].SetInt("yChunk", yOffset[i]);
                propertyBlocks[i].SetInt("zChunk", zOffset[i]);
                countBuffer.Release();
            }
        }
        Debug.Log(Time.realtimeSinceStartup);

        mesh = GetMesh();
        InitializeTexture();
        material.SetBuffer("_ChunkTable", interiorChunkBuffer);
        material.SetTexture("_MyArr", texture);
    }

    void Update()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            if( renderBuffers[i] != null)
            {
                Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, renderBuffers[i].count, propertyBlocks[i]);
            }
        }
    }

    private void OnDisable()
    {
        interiorChunkBuffer.Release();
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

    private Mesh GetMesh()
    {
        Instantiate(prefab);
        return prefab.GetComponent<MeshFilter>().sharedMesh;
    }

    private void InitializeTexture()
    {
        texture = new Texture2DArray(256, 256, texInit.Length, TextureFormat.RGBA32, false);

        for (int i = 0; i < texInit.Length; i++)
        {
            texture.SetPixels(texInit[i].GetPixels(), i, 0);
        }

        texture.Apply();
    }
}
