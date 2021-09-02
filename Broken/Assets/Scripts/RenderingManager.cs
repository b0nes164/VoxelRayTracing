using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;

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
    private ComputeBuffer locPosBuffer;

    private ComputeBuffer[] argsBuffers;

    private Cross cross = new Cross(47, 250, 250, true, Vector2Int.zero, Vector2Int.zero, 0, 0);

    private void Start()
    {
        InitValues();

        camMovement = new CameraMovement(mainCam, text, cross, camSens, zoomSens, xChunks, yChunks, zChunks, length, height, width, activeDepth);

        worldGen = new WorldGeneration(compute, mesh.GetIndexCount(0), camMovement.GetMaximumActiveSize(),
                        xChunks, yChunks, zChunks, length, width, height, activeDepth);

        chunking = new Chunking(cross, xChunks, yChunks, zChunks, length, height, width, activeDepth);

        worldGen.GenerateWorld();
        worldGen.GenerateVisTable();

        argsBuffers = worldGen.GetArgsBuffer();

        HeightDispatch(ref chunking.GetActiveChunks());
        propertyBlocks = worldGen.GetPropertyBlocks();

        locPosBuffer = worldGen.GetInteriorChunkBuffer();
        material.SetBuffer("_LocalPositionBuffer", locPosBuffer);

        InitializeTexture();
        material.SetTexture("_MyArr", textureA);
    }

    private void Update()
    {
        camMovement.MoveCam();

        if (cross.IsUpdated)
        {
            chunking.UpdateChunks();
            HeightDispatch(ref chunking.GetActiveChunks());
        }

        DrawMain(ref chunking.GetActiveChunks());

        text.text = cross.Height + "";
    }

    private void OnDisable()
    {
        worldGen.ReleaseBuffers();
        chunking.DisposeNative();
    }

    private void HeightDispatch(ref NativeArray<int2> _nativeActiveChunks)
    {
        worldGen.GlobalRendering(cross.Height, ref _nativeActiveChunks);
    }

    //see if can use native list for active chunk list
    private void DrawMain(ref NativeArray<int2> __nativeActiveChunks)
    {
        int temp = __nativeActiveChunks.Length;

        for (int i = 0; i < temp; i++)
        {
            if (__nativeActiveChunks[i].x != 0x7FFFFFFF)
            {
                Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffers[i], 0, propertyBlocks[i]);
            }
        }
    }


    private void InitValues()
    {
        bounds = new Bounds(transform.position, Vector3.one * (range + 1));

        if (halfMesh)
        {
            mesh = CreateHalfCube();
        }
        else
        {
            mesh = GetMesh();
        }
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
