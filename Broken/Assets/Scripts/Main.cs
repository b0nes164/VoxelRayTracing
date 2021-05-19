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

    private static int xChunks = 1;
    private static int yChunks = 1;
    private static int zChunks = 1;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 16;
    private static int height = 16;
    private static int width = 16;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private ComputeBuffer chunkEdgeBuffer;
    private uint[] chunkTable = new uint[chunkCount];
    private ComputeBuffer dummyBuffer;

    private ComputeBuffer interiorChunkBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[] count = new int[1];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int xOffset;
    private int yOffset;
    private int zOffset;

    private Bounds bounds;
    private Mesh mesh;
    private Texture2DArray texture;

    uint[] test;

    void Start()
    {
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
        computeShader.Dispatch(initilializeChunkKernel, chunkCount, 1, 1);
        chunkEdgeBuffer.GetData(chunkTable);
        chunkEdgeBuffer.Release();

        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        interiorChunkBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", interiorChunkBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);
        /*
         * test = new uint[cubeCount];
        edgeBuffer.GetData(test);
        for (int c = 0; c < test.Length; c++)
        {
            if (((test[c] >> 5) & 1U) == 1U)
            {
                Debug.Log(test[c] & 0x7);
            }   
        }
         */
        int initializeDummyKernel = computeShader.FindKernel("InitializeDummyChunk");
        dummyBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(initializeDummyKernel, "_DummyChunk", dummyBuffer);
        computeShader.Dispatch(initializeDummyKernel, dispatchGroups, 1, 1);

        //populate the index
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset = (i % zChunks) * width;
            computeShader.SetInt("xOffset", xOffset);
            computeShader.SetInt("yOffset", yOffset);
            computeShader.SetInt("zOffset", zOffset);

            int noiseKernel = computeShader.FindKernel("Noise");
            mainBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint));
            computeShader.SetBuffer(noiseKernel, "_ChunkTable", interiorChunkBuffer);
            computeShader.SetBuffer(noiseKernel, "_MeshProperties", mainBuffers[i]);
            computeShader.Dispatch(noiseKernel, dispatchGroups, 1, 1);
        }

        int b = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset = (i % zChunks) * width;

            int countKernel = computeShader.FindKernel("InitializeCounter");
            countBuffer = new ComputeBuffer(1, sizeof(int));
            computeShader.SetBuffer(countKernel, "_Counter", countBuffer);
            computeShader.Dispatch(countKernel, 1, 1, 1);

            if (render)
            {
                int stupidCullKernel = computeShader.FindKernel("StupidCull");
                computeShader.SetBuffer(stupidCullKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(stupidCullKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(stupidCullKernel, "_EdgeTable", edgeBuffer);

                int stupidCullTwoKernel = computeShader.FindKernel("StupidCull2");
                computeShader.SetBuffer(stupidCullTwoKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(stupidCullTwoKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(stupidCullTwoKernel, "_EdgeTable", edgeBuffer);
                 
                if (chunkTable[i] == 1)
                {
                    //set the adjacent chunks to the empty dummyBuffer
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", dummyBuffer);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", dummyBuffer);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", dummyBuffer);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", dummyBuffer);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", dummyBuffer);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", dummyBuffer);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", dummyBuffer);
                    b++;
                }
                else
                {
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalTopChunk", mainBuffers[i + (zChunks * yChunks) + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_TopLeftChunk", mainBuffers[i + (zChunks * yChunks) + zChunks]);
                    computeShader.SetBuffer(stupidCullKernel, "_DiagonalMiddleChunk", mainBuffers[i + (zChunks * yChunks) + 1]);
                    computeShader.SetBuffer(stupidCullKernel, "_MiddleLeftChunk", mainBuffers[i + (zChunks * yChunks)]);

                    computeShader.SetBuffer(stupidCullTwoKernel, "_TopRightChunk", mainBuffers[i + zChunks + 1]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleTopChunk", mainBuffers[i + zChunks]);
                    computeShader.SetBuffer(stupidCullTwoKernel, "_MiddleRightChunk", mainBuffers[i + 1]);
                }
                computeShader.Dispatch(stupidCullKernel, dispatchGroups, 1, 1);
                computeShader.Dispatch(stupidCullTwoKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count);
            }
            else
            {
                int testKernel = computeShader.FindKernel("CheckFaces");
                computeShader.SetBuffer(testKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(testKernel, "_Counter", countBuffer);
                computeShader.SetBuffer(testKernel, "_ChunkTable", interiorChunkBuffer);
                computeShader.Dispatch(testKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count);
            }


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
            propertyBlocks[i].SetInt("xChunk", xOffset);
            propertyBlocks[i].SetInt("yChunk", yOffset);
            propertyBlocks[i].SetInt("zChunk", zOffset);
            countBuffer.Release();
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
        dummyBuffer.Release();

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
