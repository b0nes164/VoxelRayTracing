using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RenderingManager : MonoBehaviour
{
    [SerializeField]
    private int xChunks;

    [SerializeField]
    private int yChunks;

    [SerializeField]
    private int zChunks;

    [SerializeField]
    private int activeDepth;

    [SerializeField]
    private float camSens;

    [SerializeField]
    private float zoomSens;

    [SerializeField]
    private float range;

    [SerializeField]
    private Material material;

    [SerializeField]
    private Texture[] texture;

    [SerializeField]
    private Camera mainCam;

    [SerializeField]
    private ComputeShader compute;

    [SerializeField]
    private GameObject prefab;

    [SerializeField]
    private bool halfMesh;

    [SerializeField]
    private Text text;


    private WorldGeneration worldGen;
    private Chunking chunking;
    private CameraMovement camMovement;

    private readonly int length = 16;
    private readonly int height = 16;
    private readonly int width = 16;

    private Texture2DArray textureA;
    private Mesh mesh;
    private Bounds bounds;

    private MaterialPropertyBlock[] propertyBlocks;
    private ComputeBuffer[] renderBuffers;
    private ComputeBuffer locPosBuffer;

    private bool[] nullChecks;
    private List<ChunkStruct> activeChunks = new List<ChunkStruct>();

    private Cross cross = new Cross(47, 250, 250);

    private void Start()
    {
        InitValues();

        worldGen = new WorldGeneration(compute, activeChunks, xChunks, yChunks, zChunks, length, width, height, activeDepth);
        worldGen.GenerateWorld();
        worldGen.GenerateVisTable();
        nullChecks = worldGen.GetNullChecks();

        chunking = new Chunking(mainCam.transform, activeChunks, worldGen.GetChunkPositionTable(), cross, xChunks, yChunks, zChunks, length, width, height, activeDepth, 6, 6);

        camMovement = new CameraMovement(mainCam, text, cross, camSens, zoomSens, xChunks, yChunks, zChunks, length, height, width);

        HeightDispatch();

        propertyBlocks = worldGen.GetPropertyBlocks();
        renderBuffers = worldGen.GetRenderBuffers();
        locPosBuffer = worldGen.GetInteriorChunkBuffer();
        material.SetBuffer("_LocalPositionBuffer", locPosBuffer);

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
        camMovement.MoveCam();

        if (chunking.IsNewChunk(camMovement.GetDiagRight(), camMovement.GetDiagUp()))
        {
            HeightDispatch();
        }


        for (int i = 0; i < renderBuffers.Length; i++)
        {
            if (nullChecks[i])
            {
                Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, renderBuffers[i].count, propertyBlocks[i]);
            }
        }

        text.text = cross.Height + "";
    }

    private void OnDisable()
    {
        worldGen.ReleaseBuffers();
    }

    private void HeightDispatch()
    {
        worldGen.ReleaseRenderBuffers();
        worldGen.GlobalRendering(cross.Height);
    }


    private void InitValues()
    {
        bounds = new Bounds(transform.position, Vector3.one * (range + 1));
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
