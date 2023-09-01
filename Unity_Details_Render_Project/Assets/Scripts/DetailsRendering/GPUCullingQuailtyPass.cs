using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

public class GPUCullingQualityPass : ScriptableRenderPass
{
    // Compute shader main function name
    private const string FrustumAndHiZCullingName = "FrustumAndHiZCullingCSMain";
    private const string FillHistogramTableName = "FillHistogramTableCSMain";
    private const string ColumnScanHistogramTableName = "ColumnScanHistogramTableCSMain";
    private const string PrefixScanName = "PrefixScanCSMain";
    private const string PrefixScanTableName = "PrefixScanTableCSMain";
    private const string DistRearrangeName = "DistRearrangeCSMain";
    private const string FillIndirectArgsName = "FillIndirectArgsCSMain";
    private const string TypeRearrangeName = "TypeRearrangeCSMain";

    // Compute buffer name ID
    private static readonly int AllInstancePositionsId = Shader.PropertyToID("_AllInstancePositions");
    private static readonly int AllInstanceTransformsId = Shader.PropertyToID("_AllInstanceTransforms");
    private static readonly int AllInstanceColorsId = Shader.PropertyToID("_AllInstanceColors");
    private static readonly int AllInstanceInfosId = Shader.PropertyToID("_AllInstanceInfos");
    private static readonly int AllTypesInfosId = Shader.PropertyToID("_AllTypesInfos");
    private static readonly int TypeLodThresholdsId = Shader.PropertyToID("_TypeLodThresholds");
    private static readonly int DrawIndirectArgsId = Shader.PropertyToID("_DrawIndirectArgs");
    private static readonly int IndicesDistancesTypesLodsId = Shader.PropertyToID("_IndicesDistancesTypesLods");
    private static readonly int RWIndicesDistancesTypesLodsId = Shader.PropertyToID("_RWIndicesDistancesTypesLods");

    private static readonly int TempIndicesDistancesTypesLodsId = Shader.PropertyToID("_TempIndicesDistancesTypesLods");
    private static readonly int DistHistogramTableId = Shader.PropertyToID("_DistHistogramTable");
    private static readonly int DistPrefixScanId = Shader.PropertyToID("_DistPrefixScan");
    private static readonly int TypeHistogramTableId = Shader.PropertyToID("_TypeHistogramTable");
    private static readonly int TypePrefixScanId = Shader.PropertyToID("_TypePrefixScan");
    private static readonly int LodHistogramTableId = Shader.PropertyToID("_LodHistogramTable");
    private static readonly int LodPrefixScanId = Shader.PropertyToID("_LodPrefixScan");
    
    // Compute texture name ID
    private static readonly int DepthMipmapTextureId = Shader.PropertyToID("_DepthMipmapTexture");

    // Shader Variable ID
    private static readonly int MatrixVPId = Shader.PropertyToID("_MatrixVP");
    private static readonly int DepthTextureSizeId = Shader.PropertyToID("_DepthTextureSize");
    private static readonly int CameraPosId = Shader.PropertyToID("_CameraPos");
    private static readonly int CameraDirId = Shader.PropertyToID("_CameraDir");
    private static readonly int BoundingBoxRadiusId = Shader.PropertyToID("_BoundingBoxRadius");
    private static readonly int MaxCullingDistanceId = Shader.PropertyToID("_MaxCullingDistance");
    private static readonly int VertexCountsId = Shader.PropertyToID("_VertexCounts");
    private static readonly int ResultCountsId = Shader.PropertyToID("_ResultCounts");
    private static readonly int TotalLodCountsId = Shader.PropertyToID("_TotalLodCounts");

    // Static const
    private static readonly int GroupSize = (1 << 8);

    // Pass info
    private ProfilingSampler _profilingSampler;

    private Terrain _terrain;
    private DetailsDataAsset _asset;
    
    // Compute Shader
    private ComputeShader _computeShader;
    
    // Pass setting
    private DetailsRendererFeatureSetting _setting;
    private bool _enableComputeShader;
    
    // Depth mipmap texture RT
    private DetailsRendererFeature.RenderTextures _renderTextures;
    private RenderTextureDescriptor _depthMipmapTextureDesc;
    
    // Structure Buffer
    private DetailsRendererFeature.DoubleComputeBuffers _computeBuffers;

    private ComputeBuffer _tempIndicesDistancesTypesLods;
    private ComputeBuffer _distHistogramTableBuffer;
    private ComputeBuffer _distPrefixScanBuffer;
    private ComputeBuffer _typeHistogramTableBuffer;
    private ComputeBuffer _typePrefixScanBuffer;
    private ComputeBuffer _lodHistogramTableBuffer;
    private ComputeBuffer _lodPrefixScanBuffer;

    private bool _computeBufferInit = false;

    private int _vertexCount = -1;

#if UNITY_EDITOR
    private int _updateCount = 0;
#endif
    
    // Job system
    [BurstCompatible]
    public struct SetComputeBuffersJob : IJob
    {
        [ReadOnly] public float maxCullingDistance;
        [ReadOnly] public uint indexCount;
        [ReadOnly] public uint indexStart;
        [ReadOnly] public uint baseVertex;
        
        [ReadOnly] public NativeArray<Vector3> positions;
        [ReadOnly] public NativeArray<SphericalHarmonicsL2> shList;
        [ReadOnly] public NativeArray<Vector4> opList;
        
        public NativeArray<float4> shArList;
        public NativeArray<float4> shAgList;
        public NativeArray<float4> shAbList;
        public NativeArray<float4> shBrList;
        public NativeArray<float4> shBgList;
        public NativeArray<float4> shBbList;
        public NativeArray<float4> shCList;
        public NativeArray<uint2> allTypesInfo;
        public NativeArray<float> typeLodThresholds;
        public NativeArray<uint> args;
        public NativeArray<uint> counter;

        public int vertexCount;
        public int size;

        public void Init(DetailsRendererFeatureSetting setting, List<Vector3> positionsList)
        {
            maxCullingDistance = setting.maxCullingDistance;

            vertexCount = positionsList.Count;
            
            size = Mathf.CeilToInt((float)vertexCount / GroupSize);
            size = size > 0 ? size : 1;
            size *= GroupSize;

            shArList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shAgList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shAbList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shBrList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shBgList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shBbList = new NativeArray<float4>(vertexCount, Allocator.TempJob);
            shCList  = new NativeArray<float4>(vertexCount, Allocator.TempJob);

            allTypesInfo = new NativeArray<uint2>(255, Allocator.TempJob);
            typeLodThresholds = new NativeArray<float>(255, Allocator.TempJob);
            args = new NativeArray<uint>(size * 4 * 5, Allocator.TempJob);
            counter = new NativeArray<uint>(1, Allocator.TempJob);

            var sh = new List<SphericalHarmonicsL2>(vertexCount);
            var op = new List<Vector4>(vertexCount);
            
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positionsList, sh, op);
            
            positions = positionsList.ToNativeArray(Allocator.TempJob);
            shList = sh.ToNativeArray(Allocator.TempJob);
            opList = op.ToNativeArray(Allocator.TempJob);
        }
    
        public void Execute()
        {
            if (vertexCount <= 0)
            {
                return;
            }

            for (int i = 0; i < shList.Length; i++)
            {
                SphericalHarmonicsL2 sh = shList[i];
    
                shArList[i] = new Vector4(sh[0, 3], sh[0, 1], sh[0, 2], sh[0, 0] - sh[0, 5]);
                shAgList[i] = new Vector4(sh[1, 3], sh[1, 1], sh[1, 2], sh[1, 0] - sh[1, 5]);
                shAbList[i] = new Vector4(sh[2, 3], sh[2, 1], sh[2, 2], sh[2, 0] - sh[2, 5]);
                shBrList[i] = new Vector4(sh[0, 4], sh[0, 6], sh[0, 5] * 3.0f, sh[0, 7]);
                shBgList[i] = new Vector4(sh[1, 4], sh[1, 6], sh[1, 5] * 3.0f, sh[1, 7]);
                shBbList[i] = new Vector4(sh[2, 4], sh[2, 6], sh[2, 5] * 3.0f, sh[2, 7]);
                shCList[i]  = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1.0f);
            }

            allTypesInfo[0] = new uint2(0, 1);
            
            typeLodThresholds[0] = maxCullingDistance;
            
            args[0] = indexCount;
            args[1] = 0;
            args[2] = indexStart;
            args[3] = baseVertex;
            args[4] = 0;

            counter[0] = 0;
        }

        public void SetData(DetailsRendererFeature.DoubleComputeBuffers computeBuffers)
        {
            ReAllocComputeBuffers(computeBuffers, size, size * 4);

            computeBuffers.WriteAllInstancePositions.SetData(positions);
            computeBuffers.WriteAllVertexSHAr.SetData(shArList);
            computeBuffers.WriteAllVertexSHAg.SetData(shAgList);
            computeBuffers.WriteAllVertexSHAb.SetData(shAbList);
            computeBuffers.WriteAllVertexSHBr.SetData(shBrList);
            computeBuffers.WriteAllVertexSHBg.SetData(shBgList);
            computeBuffers.WriteAllVertexSHBb.SetData(shBbList);
            computeBuffers.WriteAllVertexSHC.SetData(shCList);
            computeBuffers.WriteAllVertexOcclusionProbes.SetData(opList);
            computeBuffers.WriteAllTypesInfos.SetData(allTypesInfo);
            computeBuffers.WriteTypeLodThresholds.SetData(typeLodThresholds);
            computeBuffers.WriteDrawIndirectArgs.SetData(args);
            computeBuffers.WriteCounter.SetData(counter);
        }

        public void Dispose()
        {
            positions.Dispose();
            shList.Dispose();
            opList.Dispose();
            shArList.Dispose();
            shAgList.Dispose();
            shAbList.Dispose();
            shBrList.Dispose();
            shBgList.Dispose();
            shBbList.Dispose();
            shCList.Dispose();
            allTypesInfo.Dispose();
            typeLodThresholds.Dispose();
            args.Dispose();
            counter.Dispose();
        }
    }
    
    public GPUCullingQualityPass(string profilingName, RenderPassEvent passEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(GPUCullingPass));
        this.renderPassEvent = passEvent;
        
        _profilingSampler = new ProfilingSampler(profilingName);

        _computeShader = Resources.Load<ComputeShader>("GpuCulling");

        if (_computeShader == null)
        {
            Debug.LogError("Can't find compute shader GpuCulling.compute");
        }
        
        GameObject gameObject = GameObject.Find("Terrain");
        if (gameObject == null)
        {
            return;
        }

        _terrain = gameObject.GetComponent<Terrain>();
        
#if UNITY_EDITOR
        _asset = TerrainDetailsDataSerializer.Serializer(_terrain, _terrain.name);
#else
        _asset = TerrainDetailsDataSerializer.Deserializer(_terrain.name);
#endif
        
        DoubleBufferManager<DetailsData>.Instance.CreateData((read, write) =>
        {
            read.InitData(_asset);
            write.InitData(_asset);
        });
    }

    public void Setup(DetailsRendererFeatureSetting setting, 
        DetailsRendererFeature.RenderTextures renderTextures, 
        DetailsRendererFeature.DoubleComputeBuffers computeBuffers)
    {
        _setting = setting;

        _renderTextures = renderTextures;
        _computeBuffers = computeBuffers;
        
        SetKeyword();
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;
        
        CommandBuffer cmd = CommandBufferPool.Get();
        
        if (!CheckWhetherExecute(camera))
        {
            return;
        }

        using (new ProfilingScope(cmd, new ProfilingSampler("UpdateDetails")))
        {
            UpdateDetailsData(cameraData);
            ConfigureComputeBuffers();
        }

        if (_vertexCount <= 0)
        {
            return;
        }
        
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            DetailsData data = DoubleBufferManager<DetailsData>.Instance.GetData();

            Matrix4x4 matrixVP = cameraData.GetGPUProjectionMatrix() * cameraData.GetViewMatrix();
            Vector4 depthTextureSize = new Vector4(
                (float)_renderTextures.DepthMipmap.rt.width, (float)_renderTextures.DepthMipmap.rt.height,
                (float)_renderTextures.DepthMipmap.rt.width / (float)_renderTextures.DepthMipmap.rt.height,
                (float)_renderTextures.DepthMipmap.rt.height / (float)_renderTextures.DepthMipmap.rt.depth);

            cmd.SetComputeMatrixParam(_computeShader, MatrixVPId, matrixVP);
            cmd.SetComputeVectorParam(_computeShader, DepthTextureSizeId, depthTextureSize);
            cmd.SetComputeVectorParam(_computeShader, CameraPosId, camera.transform.position);
            cmd.SetComputeVectorParam(_computeShader, CameraDirId, camera.transform.forward);
            
            cmd.SetComputeFloatParam(_computeShader, BoundingBoxRadiusId, _setting.boundingBoxRadius);
            cmd.SetComputeFloatParam(_computeShader, MaxCullingDistanceId, _setting.maxCullingDistance);
            
            cmd.SetComputeIntParam(_computeShader, VertexCountsId, _vertexCount);
            cmd.SetComputeIntParam(_computeShader, TotalLodCountsId, data.totalLodCount);

            int kernel = _computeShader.FindKernel(FrustumAndHiZCullingName);
            cmd.SetBufferCounterValue(_computeBuffers.IndicesDistancesTypesLods, 0);
            cmd.SetComputeBufferParam(_computeShader, kernel, AllInstancePositionsId, _computeBuffers.AllInstancePositions);
            cmd.SetComputeBufferParam(_computeShader, kernel, AllInstanceTransformsId, _computeBuffers.AllInstanceTransforms);
            cmd.SetComputeBufferParam(_computeShader, kernel, AllInstanceInfosId, _computeBuffers.AllInstanceInfos);
            cmd.SetComputeBufferParam(_computeShader, kernel, AllTypesInfosId, _computeBuffers.AllTypesInfos);
            cmd.SetComputeBufferParam(_computeShader, kernel, TypeLodThresholdsId, _computeBuffers.TypeLodThresholds);
            cmd.SetComputeBufferParam(_computeShader, kernel, IndicesDistancesTypesLodsId, _computeBuffers.IndicesDistancesTypesLods);
            cmd.SetComputeTextureParam(_computeShader, kernel, DepthMipmapTextureId, _renderTextures.DepthMipmap);

            DrawUtils.Dispatch(cmd, _computeShader, kernel, _vertexCount);
            
            // Get Culling Result count
            uint[] counter = new uint[1] { 0 };
            ComputeBuffer.CopyCount(_computeBuffers.IndicesDistancesTypesLods, _computeBuffers.Counter, 0);
            _computeBuffers.Counter.GetData(counter);

            int resultCount = (int)counter[0];// - (int)_setting.DistanceSize;
            
            // Debug.Log($"Result Count: {resultCount}");

            if (resultCount > 0)
            {
                cmd.SetComputeIntParam(_computeShader, ResultCountsId, (int)resultCount);
                
                // 1
                kernel = _computeShader.FindKernel(FillHistogramTableName);
                cmd.SetComputeBufferParam(_computeShader, kernel, RWIndicesDistancesTypesLodsId, _computeBuffers.IndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, AllTypesInfosId, _computeBuffers.AllTypesInfos);
                cmd.SetComputeBufferParam(_computeShader, kernel, TempIndicesDistancesTypesLodsId, _tempIndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistHistogramTableId, _distHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypeHistogramTableId, _typeHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, LodHistogramTableId, _lodHistogramTableBuffer);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel, resultCount);

                // 2
                kernel = _computeShader.FindKernel(ColumnScanHistogramTableName);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistHistogramTableId, _distHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypeHistogramTableId, _typeHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, LodHistogramTableId, _lodHistogramTableBuffer);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel);
                
                ///////////////////////////////////////////////////////////  
                int size = _computeBuffers[0].count;
                uint[] lodHistogram = new uint[size * 4];

                _lodHistogramTableBuffer.GetData(lodHistogram);

                Debug.Log($"Size: {resultCount}");
                Debug.Log($"LodHist: {string.Join(" ", lodHistogram)}");
                ////////////////////////////////////////////////////////

                // 3
                kernel = _computeShader.FindKernel(PrefixScanName);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistHistogramTableId, _distHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypeHistogramTableId, _typeHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, LodHistogramTableId, _lodHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistPrefixScanId, _distPrefixScanBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypePrefixScanId, _typePrefixScanBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, LodPrefixScanId, _lodPrefixScanBuffer);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel);
                
                ///////////////////////////////////////////////////////////  
                uint[] lodPrefix = new uint[256 * 4 + 1];
                
                _lodPrefixScanBuffer.GetData(lodPrefix);
                
                Debug.Log($"LodPref: {string.Join(" ", lodPrefix)}");
                
                uint[] typePref = new uint[256 + 1];
                
                _typePrefixScanBuffer.GetData(typePref);
                
                Debug.Log($"TypePref: {string.Join(" ", typePref)}");

                uint[] distPref = new uint[256 + 1];
                
                _distPrefixScanBuffer.GetData(distPref);
                
                Debug.Log($"DistPref: {string.Join(" ", distPref)}");
                ////////////////////////////////////////////////////////

                // 4
                kernel = _computeShader.FindKernel(PrefixScanTableName);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistHistogramTableId, _distHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypeHistogramTableId, _typeHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistPrefixScanId, _distPrefixScanBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypePrefixScanId, _typePrefixScanBuffer);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel, resultCount);
                
                ///////////////////////////////////////////////////////////  
                uint[] typeHist = new uint[size];
                
                _typeHistogramTableBuffer.GetData(typeHist);
                
                Debug.Log($"TypeHist2: {string.Join(" ", typeHist)}");
                ////////////////////////////////////////////////////////
                
                // 5
                kernel = _computeShader.FindKernel(DistRearrangeName);
                cmd.SetComputeBufferParam(_computeShader, kernel, RWIndicesDistancesTypesLodsId, _computeBuffers.IndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, TempIndicesDistancesTypesLodsId, _tempIndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistHistogramTableId, _distHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, DistPrefixScanId, _distPrefixScanBuffer);

                DrawUtils.Dispatch(cmd, _computeShader, kernel, resultCount);
                
                // 6
                kernel = _computeShader.FindKernel(FillIndirectArgsName);
                cmd.SetComputeBufferParam(_computeShader, kernel, RWIndicesDistancesTypesLodsId, _computeBuffers.IndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, TempIndicesDistancesTypesLodsId, _tempIndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, DrawIndirectArgsId, _computeBuffers.DrawIndirectArgs);
                cmd.SetComputeBufferParam(_computeShader, kernel, LodPrefixScanId, _lodPrefixScanBuffer);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel, resultCount);
                
                // 7
                kernel = _computeShader.FindKernel(TypeRearrangeName);
                cmd.SetComputeBufferParam(_computeShader, kernel, RWIndicesDistancesTypesLodsId, _computeBuffers.IndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, TempIndicesDistancesTypesLodsId, _tempIndicesDistancesTypesLods);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypeHistogramTableId, _typeHistogramTableBuffer);
                cmd.SetComputeBufferParam(_computeShader, kernel, TypePrefixScanId, _typePrefixScanBuffer);

                DrawUtils.Dispatch(cmd, _computeShader, kernel, resultCount);
            }
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void UpdateDetailsData(CameraData cameraData)
    {
        if (_terrain == null)
        {
            return;
        }
        
#if UNITY_EDITOR
        if (_setting.enableEdit && _updateCount % 10 == 0)
        {
            TerrainDetailsDataSerializer.Serializer(_terrain, ref _asset);
        }
        //TerrainDetailsDataSerializer.Serializer(_terrain, ref _asset);
        _updateCount++;
        _updateCount %= 10;
#endif
        
        Vector3 center = cameraData.worldSpaceCameraPos;
        center.y = 0.0f;

        //DoubleBufferManager<DetailsData>.Instance.UpdateData((ref DetailsData write) =>
        //{
        //    _asset.ClearAndCopyDataTo(ref write, center, 1000.0f);
        //});
    }

    private void ConfigureComputeBuffers()
    {
        // var volume = VolumeManager.instance.stack.GetComponent<GrassData>();

        DetailsData data = DoubleBufferManager<DetailsData>.Instance.GetData();
        
        // Debug.Log($"position: {string.Join(" ", data.positions)}");

        if (_vertexCount != data.positions.Count)
        {
            UpdateComputeBuffers(data);
            if (!_computeBufferInit)
            {
                UpdateComputeBuffers(data);
                _computeBufferInit = true;
            }
        }
    }

    private void InitComputeBuffers(DetailsData data)
    {
        _vertexCount = SetComputeBuffers(data);

        _computeBufferInit = true;
    }
    
    private void UpdateComputeBuffers(DetailsData data)
    {
        // int vertexCount = 0;
        // await Task.Run(() =>
        // {
        //     ReleaseComputeBuffers();
        //     vertexCount = SetComputeBuffers(positions);
        // });
        
        int vertexCount = 0;
        ReleaseComputeBuffers();
        vertexCount = SetComputeBuffers(data);

        _computeBuffers.SwapReadWrite();
        _vertexCount = vertexCount;
        
        int size = Mathf.CeilToInt((float)_vertexCount / GroupSize);
        size = size > 0 ? size : 1;
        size *= GroupSize;
        
        ReleaseTempBuffers();
        ReAllocTempBuffers(size);
        
#if UNITY_EDITOR
        for (int i = 0; i < _computeBuffers.Count; i++)
        {
            if (_computeBuffers[i] != null)
            {
                GC.SuppressFinalize(_computeBuffers[i]);
            }
        }
        GC.SuppressFinalize(_tempIndicesDistancesTypesLods);
        GC.SuppressFinalize(_distHistogramTableBuffer);
        GC.SuppressFinalize(_typeHistogramTableBuffer);
        GC.SuppressFinalize(_lodHistogramTableBuffer);
        GC.SuppressFinalize(_distPrefixScanBuffer);
        GC.SuppressFinalize(_typePrefixScanBuffer);
        GC.SuppressFinalize(_lodPrefixScanBuffer);
#endif
    }

    private void UpdateComputeBufferss(List<Vector3> positions)
    {
        // int vertexCount = 0;
        // await Task.Run(() =>
        // {
        //     ReleaseComputeBuffers();
        //     vertexCount = SetComputeBuffers(positions);
        // });
        
        SetComputeBuffersJob job = new SetComputeBuffersJob();
        job.Init(_setting, positions);
        JobHandle handle = job.Schedule();
        
        handle.Complete();
        job.SetData(_computeBuffers);
        job.Dispose();

        _computeBuffers.SwapReadWrite();
        _vertexCount = job.vertexCount;
        
        int size = Mathf.CeilToInt((float)_vertexCount / GroupSize);
        size = size > 0 ? size : 1;
        size *= GroupSize;
        
        ReleaseTempBuffers();
        ReAllocTempBuffers(size);
        
#if UNITY_EDITOR
        for (int i = 0; i < _computeBuffers.Count; i++)
        {
            GC.SuppressFinalize(_computeBuffers[i]);
        }
        GC.SuppressFinalize(_tempIndicesDistancesTypesLods);
        GC.SuppressFinalize(_distHistogramTableBuffer);
        GC.SuppressFinalize(_typeHistogramTableBuffer);
        GC.SuppressFinalize(_lodHistogramTableBuffer);
        GC.SuppressFinalize(_distPrefixScanBuffer);
        GC.SuppressFinalize(_typePrefixScanBuffer);
        GC.SuppressFinalize(_lodPrefixScanBuffer);
#endif
    }

    private int SetComputeBuffers(DetailsData data)
    {
        int vertexCount = data.positions.Count;

        if (vertexCount <= 0)
        {
            return vertexCount;
        }

        int size = Mathf.CeilToInt((float)vertexCount / GroupSize);
        size = size > 0 ? size : 1;
        size *= GroupSize;

        var transformList = new Vector4[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 scale = data.scales[i];
            float rotate = data.rotateYs[i];
            transformList[i] = new Vector4(scale.x, scale.y, rotate, 0.0f);
        }

        var shList = new SphericalHarmonicsL2[vertexCount];
        var opList = new Vector4[vertexCount];

        LightProbes.CalculateInterpolatedLightAndOcclusionProbes(data.positions.ToArray(), shList, opList);

        var shArList = new Vector4[vertexCount];
        var shAgList = new Vector4[vertexCount];
        var shAbList = new Vector4[vertexCount];
        var shBrList = new Vector4[vertexCount];
        var shBgList = new Vector4[vertexCount];
        var shBbList = new Vector4[vertexCount];
        var shCList  = new Vector4[vertexCount];

        for (int i = 0; i < shList.Length; i++)
        {
            SphericalHarmonicsL2 sh = shList[i];

            shArList[i] = new Vector4(sh[0, 3], sh[0, 1], sh[0, 2], sh[0, 0] - sh[0, 5]);
            shAgList[i] = new Vector4(sh[1, 3], sh[1, 1], sh[1, 2], sh[1, 0] - sh[1, 5]);
            shAbList[i] = new Vector4(sh[2, 3], sh[2, 1], sh[2, 2], sh[2, 0] - sh[2, 5]);
            shBrList[i] = new Vector4(sh[0, 4], sh[0, 6], sh[0, 5] * 3.0f, sh[0, 7]);
            shBgList[i] = new Vector4(sh[1, 4], sh[1, 6], sh[1, 5] * 3.0f, sh[1, 7]);
            shBbList[i] = new Vector4(sh[2, 4], sh[2, 6], sh[2, 5] * 3.0f, sh[2, 7]);
            shCList[i]  = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1.0f);
        }

        Vector2Int[] allTypesInfo = new Vector2Int[255];
        for (int i = 0; i < data.prototypes.Length; i++)
        {
            var typeInfo = data.typeInfos[i];
            allTypesInfo[i] = new Vector2Int(typeInfo.x, typeInfo.y);
        }

        float[] typeLodThresholds = new float[255];
        int ind = 0;
        for (int i = 0; i < data.prototypes.Length; i++)
        {
            var lodLevel = data.typeInfos[i].y;
            for (int j = 0; j < lodLevel; j++)
            {
                typeLodThresholds[ind++] = _setting.maxCullingDistance;
            }
        }

        uint[] args = new uint[data.totalLodCount * 5];
        int typeIndex = 0;
        for (int i = 0; i < data.prototypes.Length; i++)
        {
            DetailPrototype prototype = data.prototypes[i];
            Mesh mesh = prototype.prototype.GetComponent<MeshFilter>().sharedMesh;
            
            int lodCount = data.typeInfos[i].y;
            for (int j = 0; j < lodCount; j++)
            {
                args[typeIndex + j * 5 + 0] = mesh.GetIndexCount(0);
                args[typeIndex + j * 5 + 1] = 0;
                args[typeIndex + j * 5 + 2] = mesh.GetIndexStart(0);
                args[typeIndex + j * 5 + 3] = mesh.GetBaseVertex(0);
                args[typeIndex + j * 5 + 4] = 0;
            }

            typeIndex += 5 * lodCount;
        }
        
        // Debug.Log($"PreArgs: {string.Join(" ", args)}");
        // Debug.Log($"Types: {string.Join(" ", data.types.Count)}");
        
        uint[] counter = new uint[1] { 0 };
        
        ReAllocComputeBuffers(_computeBuffers, size, data.totalLodCount);

        _computeBuffers.WriteAllInstancePositions.SetData(data.positions);
        _computeBuffers.WriteAllInstanceTransforms.SetData(transformList);
        _computeBuffers.WriteAllInstanceColors.SetData(data.colors);
        _computeBuffers.WriteAllInstanceInfos.SetData(data.types);
        _computeBuffers.WriteAllVertexSHAr.SetData(shArList);
        _computeBuffers.WriteAllVertexSHAg.SetData(shAgList);
        _computeBuffers.WriteAllVertexSHAb.SetData(shAbList);
        _computeBuffers.WriteAllVertexSHBr.SetData(shBrList);
        _computeBuffers.WriteAllVertexSHBg.SetData(shBgList);
        _computeBuffers.WriteAllVertexSHBb.SetData(shBbList);
        _computeBuffers.WriteAllVertexSHC.SetData(shCList);
        _computeBuffers.WriteAllVertexOcclusionProbes.SetData(opList);
        _computeBuffers.WriteAllTypesInfos.SetData(allTypesInfo);
        _computeBuffers.WriteTypeLodThresholds.SetData(typeLodThresholds);
        _computeBuffers.WriteDrawIndirectArgs.SetData(args);
        _computeBuffers.WriteCounter.SetData(counter);

        // if (!_computeBufferInit)
        // {
        //     _computeBuffers.SwapReadWrite();
        //     
        //     ReAllocComputeBuffers(size);
        //     
        //     _computeBuffers.AllInstancePositions.SetData(positions);
        //     _computeBuffers.AllVertexSHAr.SetData(shArList);
        //     _computeBuffers.AllVertexSHAg.SetData(shAgList);
        //     _computeBuffers.AllVertexSHAb.SetData(shAbList);
        //     _computeBuffers.AllVertexSHBr.SetData(shBrList);
        //     _computeBuffers.AllVertexSHBg.SetData(shBgList);
        //     _computeBuffers.AllVertexSHBb.SetData(shBbList);
        //     _computeBuffers.AllVertexSHC.SetData(shCList);
        //     _computeBuffers.AllVertexOcclusionProbes.SetData(opList);
        //     _computeBuffers.AllTypesInfos.SetData(allTypesInfo);
        //     _computeBuffers.TypeLodThresholds.SetData(typeLodThresholds);
        //     _computeBuffers.DrawIndirectArgs.SetData(args);
        //     _computeBuffers.Counter.SetData(counter);
        // }

#if UNITY_EDITOR
        //for (int i = 0; i < _computeBuffers.Count; i++)
        //{
        //    GC.SuppressFinalize(_computeBuffers[i]);
        //}
        //GC.SuppressFinalize(_tempIndicesDistancesTypesLods);
        //GC.SuppressFinalize(_distHistogramTableBuffer);
        //GC.SuppressFinalize(_typeHistogramTableBuffer);
        //GC.SuppressFinalize(_lodHistogramTableBuffer);
        //GC.SuppressFinalize(_distPrefixScanBuffer);
        //GC.SuppressFinalize(_typePrefixScanBuffer);
        //GC.SuppressFinalize(_lodPrefixScanBuffer);
#endif

        return vertexCount;
    }

    private void ReleaseComputeBuffers()
    {
        _computeBuffers?.ReleaseWriteBuffer();
    }

    private void ReleaseTempBuffers()
    {
        _tempIndicesDistancesTypesLods?.Release();
        _distHistogramTableBuffer?.Release();
        _typeHistogramTableBuffer?.Release();
        _lodHistogramTableBuffer?.Release();
        _distPrefixScanBuffer?.Release();
        _typePrefixScanBuffer?.Release();
        _lodPrefixScanBuffer?.Release();
    }

    private void SetKeyword() 
    {
        DrawUtils.SetKeyword(_computeShader, "_COMPUTE_SHADER_UV_STARTS_AT_TOP", SystemInfo.graphicsUVStartsAtTop);
        DrawUtils.SetKeyword(_computeShader, "_COMPUTE_SHADER_REVERSED_Z", SystemInfo.usesReversedZBuffer);
    }

    private bool CheckWhetherExecute(Camera camera)
    {
        if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection ||
            !_setting.enableCullInSceneView && camera.cameraType == CameraType.SceneView)
        {
            return false;
        }

        return true;
    }

    private static void ReAllocComputeBuffers(DetailsRendererFeature.DoubleComputeBuffers computeBuffers, int size, int argsSize)
    {
        computeBuffers.AllInstancePositions = new ComputeBuffer(size, sizeof(float) * 3);
        computeBuffers.AllInstanceTransforms = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllInstanceColors = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllInstanceInfos = new ComputeBuffer(size, sizeof(uint));
        computeBuffers.AllVertexSHAr = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHAg = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHAb = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHBr = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHBg = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHBb = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexSHC = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllVertexOcclusionProbes = new ComputeBuffer(size, sizeof(float) * 4);
        computeBuffers.AllTypesInfos = new ComputeBuffer(255, sizeof(uint) * 2);
        computeBuffers.TypeLodThresholds = new ComputeBuffer(255, sizeof(float));
        computeBuffers.DrawIndirectArgs = new ComputeBuffer(argsSize * 5, sizeof(uint), ComputeBufferType.IndirectArguments);
        computeBuffers.IndicesDistancesTypesLods = new ComputeBuffer(size, sizeof(uint) * 4, ComputeBufferType.Append);
        computeBuffers.Counter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void ReAllocTempBuffers(int size)
    {
        _tempIndicesDistancesTypesLods = new ComputeBuffer(size, sizeof(uint) * 4);
        _distHistogramTableBuffer = new ComputeBuffer(size, sizeof(uint));
        _typeHistogramTableBuffer = new ComputeBuffer(size, sizeof(uint));
        _lodHistogramTableBuffer = new ComputeBuffer(size * (int)_setting.MaxLODCounts, sizeof(uint));
        _distPrefixScanBuffer = new ComputeBuffer((int)_setting.DistTypeSize + 1, sizeof(uint));
        _typePrefixScanBuffer = new ComputeBuffer((int)_setting.DistTypeSize + 1, sizeof(uint));
        _lodPrefixScanBuffer =
            new ComputeBuffer((int)(_setting.DistTypeSize * _setting.MaxLODCounts) + 1, sizeof(uint));
    }
}