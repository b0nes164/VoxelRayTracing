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

    private static int xChunks = 2;
    private static int yChunks = 2;
    private static int zChunks = 2;
    private static int chunkCount = xChunks * yChunks * zChunks;
    private static int leadingChunkCount = (xChunks * zChunks) + (xChunks * (yChunks - 1)) + ((zChunks - 1) * (yChunks - 1));

    private static int length = 32;
    private static int height = 16;
    private static int width = 32;
    private static int leadingEdgeCount = (length * width) + (length * (height - 1)) + ((width - 1) * (height - 1));
    private static int cubeCount = length * width * height;
    private static int dispatchGroups = Mathf.CeilToInt(cubeCount / 1024f);
    private bool render = false;

    private ComputeBuffer chunkBuffer;
    private ComputeBuffer leadingChunkEdgeBuffer;
    private uint[] leadingChunks = new uint[leadingChunkCount];
    private Vector3Int[] chunkPos = new Vector3Int[chunkCount];

    private ComputeBuffer interiorChunkBuffer;
    private ComputeBuffer edgeBuffer;
    private ComputeBuffer leadingEdgeBuffer;
    private ComputeBuffer[] mainBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer[] renderBuffers = new ComputeBuffer[chunkCount];
    private ComputeBuffer countBuffer;
    private int[] count = new int[1];
    private MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[chunkCount];

    private int xChunk;
    private int yChunk;
    private int zChunk;
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


        int initilializeChunkKernel = computeShader.FindKernel("InitializeChunks");
        chunkBuffer = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
        leadingChunkEdgeBuffer = new ComputeBuffer(leadingChunkCount, sizeof(uint), ComputeBufferType.Append);
        leadingChunkEdgeBuffer.SetCounterValue(0);
        computeShader.SetBuffer(initilializeChunkKernel, "_ChunkTable", chunkBuffer);
        computeShader.SetBuffer(initilializeChunkKernel, "_LeadingEdgeTemp", leadingChunkEdgeBuffer);
        computeShader.Dispatch(initilializeChunkKernel, Mathf.CeilToInt(leadingChunkCount/ 8f), 1, 1);
        chunkBuffer.GetData(chunkPos);
        leadingChunkEdgeBuffer.GetData(leadingChunks);
        chunkBuffer.Release();
        leadingChunkEdgeBuffer.Release();

        /*
         for (int i = 0; i < chunkPos.Length; i++)
        {
            Debug.Log(chunkPos[i]);

            if (i < leadingChunks.Length)
            {
                Debug.Log(leadingChunks[i]);
            }
        }
         */

        int initializeKernel = computeShader.FindKernel("InitializeCubes");
        interiorChunkBuffer = new ComputeBuffer(cubeCount, sizeof(uint) * 3);
        edgeBuffer = new ComputeBuffer(cubeCount, sizeof(uint));
        leadingEdgeBuffer = new ComputeBuffer(leadingEdgeCount, sizeof(uint), ComputeBufferType.Append);
        leadingEdgeBuffer.SetCounterValue(0);
        computeShader.SetBuffer(initializeKernel, "_ChunkTable", interiorChunkBuffer);
        computeShader.SetBuffer(initializeKernel, "_EdgeTable", edgeBuffer);
        computeShader.SetBuffer(initializeKernel, "_LeadingEdgeTemp", leadingEdgeBuffer);
        computeShader.Dispatch(initializeKernel, dispatchGroups, 1, 1);
        
        for (int i = 0; i < chunkCount; i++)
        {
            xOffset = Mathf.FloorToInt(i / (yChunks * zChunks)) * length;
            yOffset = (Mathf.FloorToInt(i / (zChunks)) % yChunks) * height;
            zOffset = (i % zChunks) * width;
            computeShader.SetInt("xOffset", xOffset);
            computeShader.SetInt("yOffset", yOffset);
            computeShader.SetInt("zOffset", zOffset);

            int countKernel = computeShader.FindKernel("InitializeCounter");
            countBuffer = new ComputeBuffer(1, sizeof(int));
            computeShader.SetBuffer(countKernel, "_Counter", countBuffer);
            computeShader.Dispatch(countKernel, 1, 1, 1);

            int noiseKernel = computeShader.FindKernel("Noise");
            mainBuffers[i] = new ComputeBuffer(cubeCount, sizeof(uint) * 2);
            computeShader.SetBuffer(noiseKernel, "_ChunkTable", interiorChunkBuffer);
            computeShader.SetBuffer(noiseKernel, "_MeshProperties", mainBuffers[i]);
            computeShader.Dispatch(noiseKernel, dispatchGroups, 1, 1);

            if (render)
            {
                int facesKernel = computeShader.FindKernel("CheckFaces");
                computeShader.SetBuffer(facesKernel, "_ChunkTable", interiorChunkBuffer);
                computeShader.SetBuffer(facesKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(facesKernel, "_Counter", countBuffer);
                computeShader.Dispatch(facesKernel, dispatchGroups, 1, 1);
                countBuffer.GetData(count);
            }
            else
            {
                int rayKernel = computeShader.FindKernel("RaycastCull");
                computeShader.SetBuffer(rayKernel, "_LeadingEdgeTable", leadingEdgeBuffer);
                computeShader.SetBuffer(rayKernel, "_EdgeTable", edgeBuffer);
                computeShader.SetBuffer(rayKernel, "_MeshProperties", mainBuffers[i]);
                computeShader.SetBuffer(rayKernel, "_Counter", countBuffer);
                computeShader.Dispatch(rayKernel, Mathf.CeilToInt(leadingEdgeCount / 32f), 1, 1);
                countBuffer.GetData(count);
            }

            int renderKernel = computeShader.FindKernel("PopulateRender");
            renderBuffers[i] = new ComputeBuffer(count[0], sizeof(uint), ComputeBufferType.Append);
            renderBuffers[i].SetCounterValue(0);
            computeShader.SetBuffer(renderKernel, "_MeshProperties", mainBuffers[i]);
            computeShader.SetBuffer(renderKernel, "_RenderProperties", renderBuffers[i]);
            computeShader.Dispatch(renderKernel, dispatchGroups, 1, 1);

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
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, renderBuffers[i].count, propertyBlocks[i]);
        }
    }

    private void OnDisable()
    {
        interiorChunkBuffer.Release();
        edgeBuffer.Release();
        leadingEdgeBuffer.Release();

        for (int i = 0; i < chunkCount; i++)
        {
            mainBuffers[i].Release();
            renderBuffers[i].Release();
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
