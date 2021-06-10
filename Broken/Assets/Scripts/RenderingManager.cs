using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RenderingManager : MonoBehaviour
{
    [SerializeField]
    private float range;

    [SerializeField]
    private Material material;

    [SerializeField]
    private Texture[] texture;

    [SerializeField]
    private Transform cameraPos;

    [SerializeField]
    private ComputeShader compute;

    [SerializeField]
    private GameObject prefab;

    [SerializeField]
    private bool renderMethod;

    [SerializeField]
    private bool render;

    [SerializeField]
    private bool chunkInfo;

    [SerializeField]
    private bool halfMesh;

    [SerializeField]
    private Text text;


    private Texture2DArray textureA;
    private Mesh mesh;
    private Bounds bounds;

    private MaterialPropertyBlock[] propertyBlocks;
    private ComputeBuffer[] renderBuffers;
    private ComputeBuffer interiorChunkBuffer;
    private WorldGeneration worldGen;
    private bool[] nullChecks;

    private int cross = 5;

    private void Start()
    {
        bounds = new Bounds(transform.position, Vector3.one * (range + 1));

        worldGen = new WorldGeneration(render, chunkInfo, compute);
        worldGen.GenerateWorld();
        nullChecks = worldGen.GetNullChecks();

        if (renderMethod)
        {
            worldGen.GenerateMeshProperties();
        }
        else
        {
            worldGen.HeightRendering(cross);
        }

        propertyBlocks = worldGen.GetPropertyBlocks();
        renderBuffers = worldGen.GetRenderBuffers();
        interiorChunkBuffer = worldGen.GetInteriorChunkBuffer();
        material.SetBuffer("_ChunkTable", interiorChunkBuffer);

        InitializeTexture();
        material.SetTexture("_MyArr", textureA);

        if (halfMesh)
        {
            mesh = CreateHalfCube();
        }
        else
        {
            mesh = GetMesh();
        }

        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            cross = Mathf.Clamp(cross -= 1, 1, 32);
            worldGen.ReleaseRenderBuffers();
            worldGen.HeightRendering(cross);
        }

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            cross = Mathf.Clamp(cross += 1, 1, 32);
            worldGen.ReleaseRenderBuffers();
            worldGen.HeightRendering(cross);
        }

        for (int i = 0; i < renderBuffers.Length; i++)
        {
            if (nullChecks[i])
            {
                Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, renderBuffers[i].count, propertyBlocks[i]);
            }
        }

        text.text = cross + "";
    }

    private void OnDisable()
    {
        worldGen.ReleaseBuffers();
    }

    private void InitializeTexture()
    {
        textureA = new Texture2DArray(256, 256, texture.Length, TextureFormat.RGBA32, false);

        for (int i = 0; i < texture.Length; i++)
        {
            Graphics.CopyTexture(texture[i], 0, 0, textureA, i, 0);
        }
    }

    private Mesh CreateHalfCube()
    {
        Mesh mesh = new Mesh();
  
         Vector3[] vertices = new Vector3[8]
        {
            new Vector3(1,0,1),

            
            new Vector3(0,0,1),
            new Vector3(0,1,0),
            new Vector3(0,1,1),
            new Vector3(1,0,0),
            new Vector3(1,0,1),
            new Vector3(1,1,0),
            new Vector3(1,1,1)
        };

        int[] triangles = new int[18]
        {
            //front
            6,7,4,
            7,0,4,
            
            //Right
            7, 3, 5,
            3, 1, 5,

            //top;
            2, 3, 6,
            3, 7, 6,

        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        Vector2[] uvs = new Vector2[8]
        {
            new Vector2(0f, 1f),

            new Vector2(2f,0f),

            new Vector2(2f,2f),
            new Vector2(2f, 1f),

            new Vector2(0f,2f),
            new Vector2(1f, 0f),

            new Vector2(1f,2f),
            new Vector2(1f,1f),
        };

        mesh.uv = uvs;
        mesh.Optimize();
        mesh.RecalculateNormals();

        return mesh;
    }

    private Mesh GetMesh()
    {
        Instantiate(prefab);
        return prefab.GetComponent<MeshFilter>().sharedMesh;
    }
}
