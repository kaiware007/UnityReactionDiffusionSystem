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

    #region public
    public int texWidth = 256;
    public int texHeight = 256;

    public float da = 1;
    public float db = 0.5f;

    [Header("Normal Feed/Kill")]
    [Range(0, 0.1f)]
    public float feed = 0.055f;
    [Range(0, 0.1f)]
    public float kill = 0.062f;

    [Header("Min Feed/Kill(Only Enable FeedMap)")]
    [Range(0,0.1f)]
    public float minf = 0.055f;
    [Range(0, 0.1f)]
    public float mink = 0.062f;

    [Header("Max Feed/Kill(Only Enable FeedMap)")]
    [Range(0, 0.1f)]
    public float maxf = 0.055f;
    [Range(0, 0.1f)]
    public float maxk = 0.062f;
    [Space]

    [Range(0, 64)]
    public int speed = 1;

    public int seedSize = 10;
    public int seedNum = 10;

    // Albedo
    public Color topColor = Color.white;
    public Color bottomColor = Color.black;

    // Emittion
    public Color topEmit = Color.black;
    public Color bottomEmit = Color.black;
    [Range(0,10)]
    public float topEmitIntensity = 0;
    [Range(0, 10)]
    public float bottomEmitIntensity = 0;

    public int inputMax = 32;

    [Space]
    public bool isFeedMap = false;
    public Texture feedMap;

    public ComputeShader cs;

    public RenderTexture colorTexture;
    public RenderTexture heightMapTexture;
    public RenderTexture normalMapTexture;

    #endregion

    #region private
    private int kernelUpdate = -1;
    private int kernelDraw = -1;
    private int kernelAddSeed = -1;

    private ComputeBuffer[] buffers;
    private ComputeBuffer inputBuffer;
    private RDData[] bufData;
    private RDData[] bufData2;
    private Vector2[] inputData;
    private int inputIndex = 0;
    private List<Renderer> rendererList = new List<Renderer>();
    #endregion

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

        //// ランダム
        //int w = seedSize;
        //int h = seedSize;
        //for (int i = 0; i < seedNum; i++)
        //{
        //    int centerX = Random.Range(seedSize, texWidth - seedSize) - w / 2;
        //    int centerY = Random.Range(seedSize, texHeight - seedSize) - h / 2;
        //    for (int x = 0; x < w; x++)
        //    {
        //        for (int y = 0; y < h; y++)
        //        {
        //            int idx = (centerX + x) + (centerY + y) * texWidth;
        //            bufData[idx].b = 1;
        //        }
        //    }
        //}

        buffers[0].SetData(bufData);
        buffers[1].SetData(bufData2);
    }

    void Initialize()
    {
        kernelUpdate = cs.FindKernel("Update");
        kernelDraw = cs.FindKernel("Draw");
        kernelAddSeed = cs.FindKernel("AddSeed");

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

        inputData = new Vector2[inputMax];
        inputIndex = 0;
        inputBuffer = new ComputeBuffer(inputMax, Marshal.SizeOf(typeof(Vector2)));

        var ren = GetComponentsInChildren<Renderer>();
        if (ren != null)
        {
            foreach (var r in ren)
            {
                rendererList.Add(r);
                r.material.SetTexture("_MainTex", heightMapTexture);
                r.material.SetTexture("_DispTex", heightMapTexture);
                r.material.SetColor("_Color0", bottomColor);
                r.material.SetColor("_Color1", topColor);
                r.material.SetColor("_Emit0", bottomEmit);
                r.material.SetColor("_Emit1", topEmit);
                r.material.SetFloat("_EmitInt0", bottomEmitIntensity);
                r.material.SetFloat("_EmitInt1", topEmitIntensity);
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

        cs.SetFloat("_Feed", feed);
        cs.SetFloat("_K", kill);
        
        cs.SetFloat("_FeedMin", minf);
        cs.SetFloat("_FeedMax", maxf);
        cs.SetFloat("_KMin", mink);
        cs.SetFloat("_KMax", maxk);

        cs.SetBool("_IsFeedMap", isFeedMap);
        if (feedMap != null)
        {
            cs.SetTexture(kernelUpdate, "_FeedMap", feedMap);
        }
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
            rendererList[i].material.SetTexture("_DispTex", heightMapTexture);
            rendererList[i].material.SetColor("_Color0", bottomColor);
            rendererList[i].material.SetColor("_Color1", topColor);
            rendererList[i].material.SetColor("_Emit0", bottomEmit);
            rendererList[i].material.SetColor("_Emit1", topEmit);
            rendererList[i].material.SetFloat("_EmitInt0", bottomEmitIntensity);
            rendererList[i].material.SetFloat("_EmitInt1", topEmitIntensity);
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

    void AddSeedBuffer()
    {
        if(inputIndex > 0)
        {
            inputBuffer.SetData(inputData);
            cs.SetInt("_InputNum", inputIndex);
            cs.SetInt("_TexWidth", texWidth);
            cs.SetInt("_TexHeight", texHeight);
            cs.SetInt("_SeedSize", seedSize);
            cs.SetBuffer(kernelAddSeed, "_InputBufferRead", inputBuffer);
            cs.SetBuffer(kernelAddSeed, "_BufferWrite", buffers[0]);    // update前なので0
            cs.Dispatch(kernelAddSeed, Mathf.CeilToInt((float)inputIndex / (float)THREAD_NUM_X), 1, 1);
            inputIndex = 0;
        }
    }

    void AddSeed(int x, int y)
    {
        if(inputIndex < inputMax)
        {
            inputData[inputIndex].x = x;
            inputData[inputIndex].y = y;
            inputIndex++;
        }
    }

    void AddRandomSeed(int num)
    {
        for(int i = 0; i < num; i++)
        {
            AddSeed(Random.Range(0, texWidth), Random.Range(0, texHeight));
        }
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
        //// 係数ランダム
        //if (Input.GetKeyDown(KeyCode.T))
        //{
        //    f = Random.Range(0.01f, 0.1f);
        //    k = Random.Range(0.01f, 0.1f);
        //}

        // リセット
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBuffer();
        }

        // 追加
        if (Input.GetKeyDown(KeyCode.A))
        {
            AddRandomSeed(seedNum);
        }

        // 中心に1つ追加
        if (Input.GetKeyDown(KeyCode.C))
        {
            AddSeed(texWidth / 2, texHeight / 2);
        }

        AddSeedBuffer();

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
        if(inputBuffer != null)
        {
            inputBuffer.Release();
            inputBuffer = null;
        }
    }
}
