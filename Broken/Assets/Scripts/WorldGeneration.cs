using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGeneration
{
    private bool render;
    private ComputeShader computeShader;
    private bool chunkInfo;

    private static readonly int xChunks = 5;
    private static readonly int yChunks = 1;
    private static readonly int zChunks = 5;
    private static int chunkCount = xChunks * yChunks * zChunks;

    private static int length = 32;
    private static int height = 32;
    private static int width = 32;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int step = (width * height) + width + 1;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);

    private ComputeBuffer chunkEdgeBuffer;
    private uint[] chunkTable = new uint[chunkCount];
    private ComputeBuffer[] dummyBuffers = new ComputeBuffer[7];

    private ComputeBuffer interiorChunkBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[][] count = new int[chunkCount][];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int[] xOffset = new int[chunkCount];
    private int[] yOffset = new int[chunkCount];
    private int[] zOffset = new int[chunkCount];

    private Bounds bounds;
    private Mesh mesh;
    private Texture2DArray texture;

    public WorldGeneration(bool _render, bool _chunkInfo, ComputeShader _computeShader)
    {
        render = _render;
        chunkInfo = _chunkInfo;
        computeShader = _computeShader;
        InitializeShaderValues();
    }

    private void InitializeShaderValues()
    {
        computeShader.SetInt("xChunks", xChunks);
        computeShader.SetInt("yChunks", yChunks);
        computeShader.SetInt("zChunks", zChunks);
        computeShader.SetInt("length", length);
        computeShader.SetInt("height", height);
        computeShader.SetInt("width", width);
        computeShader.SetInt("stepIndex", step);
        computeShader.SetInt("cubeCount", cubeCount);

        for (int i = 0; i < count.Length; i++)
        {
            count[i] = new int[1];
        }

        int initilializeChunkKernel = computeShader.FindKernel("InitializeChunks");
        chunkEdgeBuffer = new ComputeBuffer(chunkCount, sizeof(uint));
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkEdgeTable", chunkEdgeBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(chunkCount / 8f), 1, 1);
        chunkEdgeBuffer.GetData(chunkTable);
        chunkEdgeBuffer.Release();

        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        interiorChunkBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", interiorChunkBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);

        for (int i = 0; i < dummyBuffers.Length; i++)
        {
            int initializeDummyKernel = computeShader.FindKernel("InitializeDummyChunk");
            dummyBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint));
            computeShader.SetBuffer(initializeDummyKernel, "_DummyChunk", dummyBuffers[i]);
            computeShader.Dispatch(initializeDummyKernel, dispatchGroups, 1, 1);
        }

        for (int i = 0; i < chunkCount; i++)
        {
            xOffset[i] = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset[i] = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset[i] = (i % zChunks) * width;
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
            mainBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint));
            computeShader.SetBuffer(noiseKernel, "_ChunkTable", interiorChunkBuffer);
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

                switch (chunkTable[index] & 0x7)
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
                countBuffer.GetData(count[i]);
                countBuffer.Release();
            }
        }

        Debug.Log(Time.realtimeSinceStartup);
    }

    public void GenerateRenderProperties()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            if (count[i][0] != 0)
            {
                int renderKernel = computeShader.FindKernel("PopulateRender");
                renderBuffers[i] = new ComputeBuffer(count[i][0], sizeof(uint), ComputeBufferType.Append);
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
        }   
    }

    public void HeightRendering(int cross)
    {
        computeShader.SetInt("crossHeight", cross);

        for (int i = chunkCount; i > 0; i--)
        {
            int index = i - 1;
            int zeroCountBufferKernel = computeShader.FindKernel("ZeroCounter");
            countBuffer = new ComputeBuffer(1, sizeof(int));
            computeShader.SetBuffer(zeroCountBufferKernel, "_Counter", countBuffer);
            computeShader.Dispatch(zeroCountBufferKernel, 1, 1, 1);

            int coolKernel = computeShader.FindKernel("CoolCull");
            computeShader.SetBuffer(coolKernel, "_MeshProperties", mainBuffers[index]);
            computeShader.SetBuffer(coolKernel, "_Counter", countBuffer);
            computeShader.SetBuffer(coolKernel, "_EdgeTable", edgeBuffer);
            computeShader.SetBuffer(coolKernel, "_ChunkTable", interiorChunkBuffer);
            computeShader.Dispatch(coolKernel, dispatchGroups, 1, 1);
            countBuffer.GetData(count[index]);
            countBuffer.Release();
        }

        Debug.Log(Time.realtimeSinceStartup);
    }

    public ref MaterialPropertyBlock[] GetPropertyBlocks()
    {
        return ref propertyBlocks;
    }

    public ref ComputeBuffer GetInteriorChunkBuffer()
    {
        return ref interiorChunkBuffer;
    }

    public ref ComputeBuffer[] GetRenderBuffers()
    {
        return ref renderBuffers;
    }

    public void ReleaseRenderBuffers()
    {
        foreach (ComputeBuffer c in renderBuffers)
        {
            c.Release();
        }
    }

    public void ReleaseBuffers()
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


    /*
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
     */


}
