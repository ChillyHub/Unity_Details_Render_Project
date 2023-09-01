using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class DetailsRendererFeatureSetting
{
    public LayerMask grassLayer;

    [Range(0.02f, 2.0f)]
    public float boundingBoxRadius = 0.2f;
    [Range(10.0f, 1000.0f)]
    public float maxCullingDistance = 100.0f;

    public bool enableCull = true;
    // public bool enableSort = true;

    public bool enableCullInSceneView = true;

    public bool enableRealtimeGI = false;

    [Range(10, 1000)] 
    public int updateProbesPerFrame = 100;

    public bool enableEdit = false;
    
    public uint DistTypeBit => 8;
    public uint DistTypeSize => (1 << 8);
    public uint DistTypeMask => (1 << 8) - 1;

    public uint MaxLODCounts => 4;

    public int TypeCounts => 1;
    public int TotalLODCounts => 1;
}

public class DetailsRendererFeature : ScriptableRendererFeature
{
    public class RenderTextures
    {
        private readonly RTHandle[] _rtHandles = new RTHandle[1];

        public ref RTHandle DepthMipmap => ref _rtHandles[0];
        public ref RTHandle this[int index] => ref _rtHandles[index];

        public void Release()
        {
            foreach (var rt in _rtHandles)
            {
                rt?.Release();
            }
        }
    }
    
    public class ComputeBuffers
    {
        private ComputeBuffer[] _computeBuffers = new ComputeBuffer[17];

        public int Count => _computeBuffers.Length;

        public ref ComputeBuffer AllInstancePositions => ref _computeBuffers[0];
        public ref ComputeBuffer AllInstanceTransforms => ref _computeBuffers[1];
        public ref ComputeBuffer AllInstanceColors => ref _computeBuffers[2];
        public ref ComputeBuffer AllInstanceInfos => ref _computeBuffers[3];
        public ref ComputeBuffer AllVertexSHAr => ref _computeBuffers[4];
        public ref ComputeBuffer AllVertexSHAg => ref _computeBuffers[5];
        public ref ComputeBuffer AllVertexSHAb => ref _computeBuffers[6];
        public ref ComputeBuffer AllVertexSHBr => ref _computeBuffers[7];
        public ref ComputeBuffer AllVertexSHBg => ref _computeBuffers[8];
        public ref ComputeBuffer AllVertexSHBb => ref _computeBuffers[9];
        public ref ComputeBuffer AllVertexSHC => ref _computeBuffers[10];
        public ref ComputeBuffer AllVertexOcclusionProbes => ref _computeBuffers[11];
        public ref ComputeBuffer AllTypesInfos => ref _computeBuffers[12];
        public ref ComputeBuffer TypeLodThresholds => ref _computeBuffers[13];
        public ref ComputeBuffer DrawIndirectArgs => ref _computeBuffers[14];
        public ref ComputeBuffer IndicesDistancesTypesLods => ref _computeBuffers[15];
        public ref ComputeBuffer Counter => ref _computeBuffers[16];

        public ref ComputeBuffer this[int index] => ref _computeBuffers[index];

        public void ReleaseWithoutCounter()
        {
            for (int i = 0; i < 16; i++)
            {
                _computeBuffers[i]?.Release();
            }
        }

        public void Release()
        {
            foreach (var buffer in _computeBuffers)
            {
                buffer?.Release();
            }
        }
    }

    #region Deprecated

    public class DoubleComputeBuffers
    {
        private ComputeBuffer[] _readComputeBuffers = new ComputeBuffer[17];
        private ComputeBuffer[] _writeComputeBuffers = new ComputeBuffer[17];
        
        public int Count => _readComputeBuffers.Length;

        public ComputeBuffer AllInstancePositions
        {
            get => _readComputeBuffers[0];
            set => _writeComputeBuffers[0] = value;
        }
        public ComputeBuffer AllInstanceTransforms
        {
            get => _readComputeBuffers[1];
            set => _writeComputeBuffers[1] = value;
        }
        public ComputeBuffer AllInstanceColors
        {
            get => _readComputeBuffers[2];
            set => _writeComputeBuffers[2] = value;
        }
        public ComputeBuffer AllInstanceInfos
        {
            get => _readComputeBuffers[3];
            set => _writeComputeBuffers[3] = value;
        }
        public ComputeBuffer AllVertexSHAr
        {
            get => _readComputeBuffers[4];
            set => _writeComputeBuffers[4] = value;
        }
        public ComputeBuffer AllVertexSHAg
        {
            get => _readComputeBuffers[5];
            set => _writeComputeBuffers[5] = value;
        }
        public ComputeBuffer AllVertexSHAb
        {
            get => _readComputeBuffers[6];
            set => _writeComputeBuffers[6] = value;
        }
        public ComputeBuffer AllVertexSHBr
        {
            get => _readComputeBuffers[7];
            set => _writeComputeBuffers[7] = value;
        }
        public ComputeBuffer AllVertexSHBg
        {
            get => _readComputeBuffers[8];
            set => _writeComputeBuffers[8] = value;
        }
        public ComputeBuffer AllVertexSHBb
        {
            get => _readComputeBuffers[9];
            set => _writeComputeBuffers[9] = value;
        }
        public ComputeBuffer AllVertexSHC
        {
            get => _readComputeBuffers[10];
            set => _writeComputeBuffers[10] = value;
        }
        public ComputeBuffer AllVertexOcclusionProbes
        {
            get => _readComputeBuffers[11];
            set => _writeComputeBuffers[11] = value;
        }
        public ComputeBuffer AllTypesInfos
        {
            get => _readComputeBuffers[12];
            set => _writeComputeBuffers[12] = value;
        }
        public ComputeBuffer TypeLodThresholds
        {
            get => _readComputeBuffers[13];
            set => _writeComputeBuffers[13] = value;
        }
        public ComputeBuffer DrawIndirectArgs
        {
            get => _readComputeBuffers[14];
            set => _writeComputeBuffers[14] = value;
        }
        public ComputeBuffer IndicesDistancesTypesLods
        {
            get => _readComputeBuffers[15];
            set => _writeComputeBuffers[15] = value;
        }
        public ComputeBuffer Counter
        {
            get => _readComputeBuffers[16];
            set => _writeComputeBuffers[16] = value;
        }

        public ref ComputeBuffer WriteAllInstancePositions => ref _writeComputeBuffers[0];
        public ref ComputeBuffer WriteAllInstanceTransforms => ref _writeComputeBuffers[1];
        public ref ComputeBuffer WriteAllInstanceColors => ref _writeComputeBuffers[2];
        public ref ComputeBuffer WriteAllInstanceInfos => ref _writeComputeBuffers[3];
        public ref ComputeBuffer WriteAllVertexSHAr => ref _writeComputeBuffers[4];
        public ref ComputeBuffer WriteAllVertexSHAg => ref _writeComputeBuffers[5];
        public ref ComputeBuffer WriteAllVertexSHAb => ref _writeComputeBuffers[6];
        public ref ComputeBuffer WriteAllVertexSHBr => ref _writeComputeBuffers[7];
        public ref ComputeBuffer WriteAllVertexSHBg => ref _writeComputeBuffers[8];
        public ref ComputeBuffer WriteAllVertexSHBb => ref _writeComputeBuffers[9];
        public ref ComputeBuffer WriteAllVertexSHC => ref _writeComputeBuffers[10];
        public ref ComputeBuffer WriteAllVertexOcclusionProbes => ref _writeComputeBuffers[11];
        public ref ComputeBuffer WriteAllTypesInfos => ref _writeComputeBuffers[12];
        public ref ComputeBuffer WriteTypeLodThresholds => ref _writeComputeBuffers[13];
        public ref ComputeBuffer WriteDrawIndirectArgs => ref _writeComputeBuffers[14];
        public ref ComputeBuffer WriteIndicesDistancesTypesLods => ref _writeComputeBuffers[15];
        public ref ComputeBuffer WriteCounter => ref _writeComputeBuffers[16];

        public ComputeBuffer this[int index]
        {
            get => _readComputeBuffers[index];
            set => _writeComputeBuffers[index] = value;
        }

        public void SwapReadWrite()
        {
            CoreUtils.Swap(ref _readComputeBuffers, ref _writeComputeBuffers);
        }

        public void ReleaseWriteBuffer()
        {
            foreach (var buffer in _writeComputeBuffers)
            {
                buffer?.Release();
            }
        }
        
        public void ReleaseWriteBufferWithoutCounter()
        {
            for (int i = 0; i < 16; i++)
            {
                _writeComputeBuffers[i]?.Release();
            }
        }

        public void Release()
        {
            foreach (var buffer in _readComputeBuffers)
            {
                buffer?.Release();
            }

            foreach (var buffer in _writeComputeBuffers)
            {
                buffer?.Release();
            }
        }
    }

    #endregion

    public const string DepthMipmapTextureName = "_DepthMipmapTexture";
    
    // Renderer feature setting
    public DetailsRendererFeatureSetting setting;
    
    // Render Pass
    private MipmapPass _mipmapPass;
    private GPUCullingPass _gpuCullingPass;
    private DetailsRenderPass _detailsRenderPass;
    
    // Depth mipmap texture RT
    private readonly RenderTextures _renderTextures = new RenderTextures();
    
    // Structure Buffer
    private readonly ComputeBuffers _computeBuffers = new ComputeBuffers();

    public override void Create()
    {
        _mipmapPass = new MipmapPass("Depth Mipmap", RenderPassEvent.AfterRenderingOpaques);
        _gpuCullingPass = new GPUCullingPass("Details Culling", RenderPassEvent.AfterRenderingOpaques);
        _detailsRenderPass = new DetailsRenderPass("Draw Details", RenderPassEvent.AfterRenderingOpaques);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _mipmapPass.Setup(setting, _renderTextures);
        renderer.EnqueuePass(_mipmapPass);
        
        _gpuCullingPass.Setup(setting, _renderTextures, _computeBuffers);
        renderer.EnqueuePass(_gpuCullingPass);
        
        _detailsRenderPass.Setup(setting, _computeBuffers);
        renderer.EnqueuePass(_detailsRenderPass);
    }

    public void Reset()
    {
        OnDestroy();
        Create();
    }

    public void OnDisable()
    {
        OnDestroy();
    }

    public void OnDestroy()
    {
        _renderTextures?.Release();
        _computeBuffers?.Release();
    }
}
