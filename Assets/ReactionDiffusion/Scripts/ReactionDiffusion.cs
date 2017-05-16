using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public struct RDData
{
    public float a;
    public float b;
}

public class ReactionDiffusion : MonoBehaviour {
    const int THREAD_NUM_X = 32;

    public int texWidth = 256;
    public int texHeight = 256;

    public float da = 1;
    public float db = 0.5f;
    [Range(0,0.1f)]
    public float f = 0.055f;
    [Range(0, 0.1f)]
    public float k = 0.062f;
    public float speed = 1;

    public int seedSize = 10;
    public int seedNum = 10;

    public Color topColor = Color.white;
    public Color bottomColor = Color.black;

    public ComputeShader cs;

    public RenderTexture colorTexture;
    public RenderTexture heightMapTexture;
    public RenderTexture normalMapTexture;

    private int kernelUpdate = -1;
    private int kernelDraw = -1;

    private ComputeBuffer[] buffers;
    private RDData[] bufData;
    private RDData[] bufData2;
    private List<Renderer> rendererList = new List<Renderer>();

    void ResetBuffer()
    {
        for (int x = 0; x < texWidth; x++)
        {
            for (int y = 0; y < texHeight; y++)
            {
                int idx = x + y * texWidth;
                bufData[idx].a = 1;
                bufData[idx].b = 0;

                bufData2[idx].a = 1;
                bufData2[idx].b = 0;

            }
        }

        // ランダム
        int w = seedSize;
        int h = seedSize;
        for (int i = 0; i < seedNum; i++)
        {
            int centerX = Random.Range(seedSize, texWidth - seedSize) - w / 2;
            int centerY = Random.Range(seedSize, texHeight - seedSize) - h / 2;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    int idx = (centerX + x) + (centerY + y) * texWidth;
                    bufData[idx].b = 1;
                }
            }
        }

        buffers[0].SetData(bufData);
        buffers[1].SetData(bufData2);
    }

    void Initialize()
    {
        kernelUpdate = cs.FindKernel("Update");
        kernelDraw = cs.FindKernel("Draw");

        colorTexture = CreateTexture(texWidth, texHeight);
        heightMapTexture = CreateTexture(texWidth, texHeight);
        normalMapTexture = CreateTexture(texWidth, texHeight);

        int wh = texWidth * texHeight;
        buffers = new ComputeBuffer[2];
        
        cs.SetInt("_TexWidth", texWidth);
        cs.SetInt("_TexHeight", texHeight);

        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = new ComputeBuffer(wh, Marshal.SizeOf(typeof(RDData)));
        }

        bufData = new RDData[texWidth * texHeight];
        bufData2 = new RDData[texWidth * texHeight];

        ResetBuffer();

        var ren = GetComponentsInChildren<Renderer>();
        if (ren != null)
        {
            foreach (var r in ren)
            {
                rendererList.Add(r);
                r.material.SetTexture("_MainTex", heightMapTexture);
                r.material.SetColor("_Color0", bottomColor);
                r.material.SetColor("_Color1", topColor);
            }
        }
    }

    RenderTexture CreateTexture(int width, int height)
    {
        RenderTexture tex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        tex.enableRandomWrite = true;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Create();

        return tex;
    }

    void UpdateBuffer()
    {
        cs.SetInt("_TexWidth", texWidth);
        cs.SetInt("_TexHeight", texHeight);
        cs.SetFloat("_DA", da);
        cs.SetFloat("_DB", db);
        cs.SetFloat("_Feed", f);
        cs.SetFloat("_K", k);
        cs.SetBuffer(kernelUpdate, "_BufferRead", buffers[0]);
        cs.SetBuffer(kernelUpdate, "_BufferWrite", buffers[1]);
        cs.Dispatch(kernelUpdate, Mathf.CeilToInt((float)texWidth / THREAD_NUM_X), Mathf.CeilToInt((float)texHeight / THREAD_NUM_X), 1);

        SwapBuffer();
    }

    void UpdateMaterial()
    {
        for (int i = 0; i < rendererList.Count; i++)
        {
            rendererList[i].material.SetTexture("_MainTex", heightMapTexture);
            rendererList[i].material.SetColor("_Color0", bottomColor);
            rendererList[i].material.SetColor("_Color1", topColor);
        }
    }

    void DrawTexture()
    {
        cs.SetInt("_TexWidth", texWidth);
        cs.SetInt("_TexHeight", texHeight);
        cs.SetVector("_TopColor", topColor);
        cs.SetVector("_BottomColor", bottomColor);
        cs.SetBuffer(kernelDraw, "_BufferRead", buffers[0]);
        cs.SetTexture(kernelDraw, "_DistTex", colorTexture);
        cs.SetTexture(kernelDraw, "_HeightMap", heightMapTexture);
        cs.SetTexture(kernelDraw, "_NormalMap", normalMapTexture);
        cs.Dispatch(kernelDraw, Mathf.CeilToInt((float)texWidth / THREAD_NUM_X), Mathf.CeilToInt((float)texHeight / THREAD_NUM_X), 1);
    }

    void SwapBuffer()
    {
        ComputeBuffer temp = buffers[0];
        buffers[0] = buffers[1];
        buffers[1] = temp; 
    }

    // Use this for initialization
    void Start () {
        Initialize();
	}
	
	// Update is called once per frame
	void Update () {
        // 係数ランダム
        if (Input.GetKeyDown(KeyCode.T))
        {
            f = Random.Range(0.01f, 0.1f);
            k = Random.Range(0.01f, 0.1f);
        }

        // リセット
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBuffer();
        }

        for (int i = 0; i < speed; i++)
        {
            UpdateBuffer();
        }

        UpdateMaterial();

        DrawTexture();
    }

    private void OnDestroy()
    {
        if(buffers != null)
        {
            for(int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Release();
                buffers[i] = null;
            }
        }
    }
}
