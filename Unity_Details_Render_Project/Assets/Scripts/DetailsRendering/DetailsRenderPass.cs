using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DetailsRenderPass : ScriptableRenderPass
{
    // Shader textures ID
    private static readonly int WindFieldTexture0Id = Shader.PropertyToID("_WindFieldTexture0");
    private static readonly int WindFieldTexture1Id = Shader.PropertyToID("_WindFieldTexture1");
    private static readonly int WindFieldTexture2Id = Shader.PropertyToID("_WindFieldTexture2");
    private static readonly int WindFieldTexture3Id = Shader.PropertyToID("_WindFieldTexture3");
    private static readonly int SDFTexture0Id = Shader.PropertyToID("_SDFTexture0");
    private static readonly int SDFTexture1Id = Shader.PropertyToID("_SDFTexture1");
    private static readonly int SDFTexture2Id = Shader.PropertyToID("_SDFTexture2");
    private static readonly int SDFTexture3Id = Shader.PropertyToID("_SDFTexture3");
    
    // Compute buffer ID
    private static readonly int DetailsPositionsBufferId = Shader.PropertyToID("_DetailsPositionsBuffer");
    private static readonly int DetailsTransformsBufferId = Shader.PropertyToID("_DetailsTransformsBuffer");
    private static readonly int DetailsColorsBufferId = Shader.PropertyToID("_DetailsColorsBuffer");
    private static readonly int SHArBufferId = Shader.PropertyToID("_SHArBuffer");
    private static readonly int SHAgBufferId = Shader.PropertyToID("_SHAgBuffer");
    private static readonly int SHAbBufferId = Shader.PropertyToID("_SHAbBuffer");
    private static readonly int SHBrBufferId = Shader.PropertyToID("_SHBrBuffer");
    private static readonly int SHBgBufferId = Shader.PropertyToID("_SHBgBuffer");
    private static readonly int SHBbBufferId = Shader.PropertyToID("_SHBbBuffer");
    private static readonly int SHCBufferId = Shader.PropertyToID("_SHCBuffer");
    private static readonly int SHOcclusionProbesBufferId = Shader.PropertyToID("_SHOcclusionProbesBuffer");
    private static readonly int IndicesDistancesTypesLodsBufferId = Shader.PropertyToID("_IndicesDistancesTypesLodsBuffer");
    private static readonly int DrawIndirectArgsId = Shader.PropertyToID("_DrawIndirectArgs");
    
    // Compute variable ID
    private static readonly int ArgsOffsetId = Shader.PropertyToID("_ArgsOffset");
    private static readonly int InteractionMatrixVP0Id = Shader.PropertyToID("_InteractionMatrixVP0");
    private static readonly int InteractionMatrixVP1Id = Shader.PropertyToID("_InteractionMatrixVP1");
    private static readonly int InteractionMatrixVP2Id = Shader.PropertyToID("_InteractionMatrixVP2");
    private static readonly int InteractionMatrixVP3Id = Shader.PropertyToID("_InteractionMatrixVP3");
    private static readonly int RecordDistance0Id = Shader.PropertyToID("_RecordDistance0");
    private static readonly int RecordTextureSize0Id = Shader.PropertyToID("_RecordTextureSize0");

    // Pass info
    private ProfilingSampler _profilingSampler;
    private RenderStateBlock _renderStateBlock;
    
    private List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();

    private int _probeUpdateIndex = 0;

    // Pass setting
    private DetailsRendererFeatureSetting _setting;
    private FilteringSettings _filteringSettings;
    private MaterialPropertyBlock _propertyBlock;
    
    // Structure Buffer
    private DetailsRendererFeature.ComputeBuffers _computeBuffers;

    public DetailsRenderPass(string profilingName, RenderPassEvent passEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(DetailsRenderPass));
        this.renderPassEvent = passEvent;
        
        _profilingSampler = new ProfilingSampler(profilingName);

        _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        
        _shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
        _shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
        _shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));

        _propertyBlock = new MaterialPropertyBlock();
    }
    
    public void Setup(DetailsRendererFeatureSetting setting, DetailsRendererFeature.ComputeBuffers computeBuffers)
    {
        _setting = setting;
        
        RenderQueueRange renderQueueRange = RenderQueueRange.all;
        _filteringSettings = new FilteringSettings(renderQueueRange, setting.grassLayer);

        _computeBuffers = computeBuffers;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
        DrawingSettings drawingSettings =
            CreateDrawingSettings(_shaderTagIdList, ref renderingData, sortingCriteria);
            
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;

        if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
        {
            return;
        }

        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            //var volum = VolumeManager.instance.stack.GetComponent<GrassData>();
            //
            //if (volum.grassPositions.Count <= 0)
            //{
            //    return;
            //}

            DetailsData data = DoubleBufferManager<DetailsData>.Instance.GetData();
            
            // Debug.Log($"Count: {data.positions.Count}");

            if (data.positions.Count <= 0 || _computeBuffers.Counter == null || _computeBuffers.DrawIndirectArgs == null)
            {
                return;
            }

            // int size = _computeBuffers[0].count;
            //uint[] indDisTypLod = new uint[size * 4];
            //uint[] counts = new uint[1] { 0 };
            //uint[] args = new uint[data.prototypes.Length * 4 * 5];
            //
            //// _computeBuffers.IndicesDistancesTypesLods.GetData(indDisTypLod);
            //ComputeBuffer.CopyCount(_computeBuffers.IndicesDistancesTypesLods, _computeBuffers.Counter, 0);
            //_computeBuffers.Counter.GetData(counts);
            //_computeBuffers.DrawIndirectArgs.GetData(args);
            //
            //Debug.Log($"Count: {counts[0]}");
            //Debug.Log($"Args: {string.Join(" ", args)}");

            //int count = (int)counts[0];

            //if (count <= 0)
            //{
            //    return;
            //}

            //uint[] ind = new uint[size];
            //uint[] dis = new uint[size];
            //uint[] typ = new uint[size];
            //uint[] lod = new uint[size];
            //for (int i = 0; i < size; i++)
            //{
            //    ind[i] = indDisTypLod[i * 4 + 0];
            //    dis[i] = indDisTypLod[i * 4 + 1];
            //    typ[i] = indDisTypLod[i * 4 + 2];
            //    lod[i] = indDisTypLod[i * 4 + 3];
            //}
            
            //Debug.Log($"Size: {size}");
            //Debug.Log($"Count: {count}");
            //Debug.Log($"Ind: {string.Join(" ", ind)}");
            //Debug.Log($"Dis: {string.Join(" ", dis)}");
            //Debug.Log($"Typ: {string.Join(" ", typ)}");
            //Debug.Log($"Lod: {string.Join(" ", lod)}");
            //Debug.Log($"Count: {string.Join(" ", count)}");
            // Debug.Log($"Args: {string.Join(" ", args)}");

            // UpdateLightProbeGI();

            int interactCount = RenderTexturesManager.Instance.renderTexturesArray.Length;
            if (interactCount > 0)
            {
                RecordCameraSetting recordCameraSetting = InteractionDataManager.Instance.RecordCameraSettings[0];
                
                _propertyBlock.SetTexture(WindFieldTexture0Id,
                    RenderTexturesManager.Instance.renderTexturesArray[0].PlaneWindField1);
                
                _propertyBlock.SetTexture(SDFTexture0Id, 
                    RenderTexturesManager.Instance.renderTexturesArray[0].PlaneSdf1);
                
                _propertyBlock.SetMatrix(InteractionMatrixVP0Id, 
                    recordCameraSetting.Projection * recordCameraSetting.CurrView);
                _propertyBlock.SetFloat(RecordDistance0Id, recordCameraSetting.recordDistance);
                _propertyBlock.SetFloat(RecordTextureSize0Id, (int)recordCameraSetting.renderTextureSize);
            }

            _propertyBlock.SetBuffer(DetailsPositionsBufferId, _computeBuffers.AllInstancePositions);
            _propertyBlock.SetBuffer(DetailsTransformsBufferId, _computeBuffers.AllInstanceTransforms);
            _propertyBlock.SetBuffer(DetailsColorsBufferId, _computeBuffers.AllInstanceColors);
            _propertyBlock.SetBuffer(SHArBufferId, _computeBuffers.AllVertexSHAr);
            _propertyBlock.SetBuffer(SHAgBufferId, _computeBuffers.AllVertexSHAg);
            _propertyBlock.SetBuffer(SHAbBufferId, _computeBuffers.AllVertexSHAb);
            _propertyBlock.SetBuffer(SHBrBufferId, _computeBuffers.AllVertexSHBr);
            _propertyBlock.SetBuffer(SHBgBufferId, _computeBuffers.AllVertexSHBg);
            _propertyBlock.SetBuffer(SHBbBufferId, _computeBuffers.AllVertexSHBb);
            _propertyBlock.SetBuffer(SHCBufferId, _computeBuffers.AllVertexSHC);
            _propertyBlock.SetBuffer(SHOcclusionProbesBufferId, _computeBuffers.AllVertexOcclusionProbes);
            _propertyBlock.SetBuffer(IndicesDistancesTypesLodsBufferId, _computeBuffers.IndicesDistancesTypesLods);
            _propertyBlock.SetBuffer(DrawIndirectArgsId, _computeBuffers.DrawIndirectArgs);
            
            // _propertyBlock.SetVector(InteractionMap0Position, );

            // cmd.DrawMeshInstancedProcedural(_setting.mesh, 0, 
            //     _setting.material, 0, count, _propertyBlock);
            
            for (int i = 0; i < data.prototypes.Length; i++)
            {
                DetailPrototype prototype = data.prototypes[i];
                Mesh mesh = prototype.prototype.GetComponent<MeshFilter>().sharedMesh;
                Material material = prototype.prototype.GetComponent<MeshRenderer>().sharedMaterial;
                
                var typeInfo = data.typeInfos[i];
                for (int j = 0; j < typeInfo.y; j++)
                {
                    int offset = (i * 4 + j) * 5;
                    _propertyBlock.SetFloat(ArgsOffsetId, offset);
                    cmd.DrawMeshInstancedIndirect(mesh, 0, material, 0, 
                        _computeBuffers.DrawIndirectArgs, offset * sizeof(uint), _propertyBlock);
                }
            }
            
            //Debug.Log($"Args: {string.Join(" ", args)}");

            // _setting.material.enableInstancing = true;
            // Graphics.DrawMeshInstancedProcedural(_setting.mesh, 0, _setting.material, 
            //     new Bounds(Vector3.zero, Vector3.one * 1000.0f), count, _propertyBlock, 
            //     ShadowCastingMode.Off, true, 0, camera, LightProbeUsage.BlendProbes);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void UpdateLightProbeGI()
    {
        if (!_setting.enableRealtimeGI)
        {
            return;
        }
        
        int upf = _setting.updateProbesPerFrame;
                
        var positions = new Vector3[upf];
        var shArList = new Vector4[upf];
        var shAgList = new Vector4[upf];
        var shAbList = new Vector4[upf];
        var shBrList = new Vector4[upf];
        var shBgList = new Vector4[upf];
        var shBbList = new Vector4[upf];
        var shCList = new Vector4[upf];
        var opList = new Vector4[upf];
        var shList = new SphericalHarmonicsL2[upf];

        int end = _computeBuffers.AllInstancePositions.count;
        int mod = (end - _probeUpdateIndex) < 0 ? end % upf : end - _probeUpdateIndex;
        int updateCount = Math.Min(upf, mod);

        int index = (end - _probeUpdateIndex) < 0 ? end - updateCount : _probeUpdateIndex;
                
        _computeBuffers.AllInstancePositions.GetData(positions, 0, index, updateCount);
                
        LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, shList, opList);
                
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

        _computeBuffers.AllVertexSHAr.SetData(shArList, 0, index, updateCount);
        _computeBuffers.AllVertexSHAg.SetData(shAgList, 0, index, updateCount);
        _computeBuffers.AllVertexSHAb.SetData(shAbList, 0, index, updateCount);
        _computeBuffers.AllVertexSHBr.SetData(shBrList, 0, index, updateCount);
        _computeBuffers.AllVertexSHBg.SetData(shBgList, 0, index, updateCount);
        _computeBuffers.AllVertexSHBb.SetData(shBbList, 0, index, updateCount);
        _computeBuffers.AllVertexSHC.SetData(shCList, 0, index, updateCount);
        _computeBuffers.AllVertexOcclusionProbes.SetData(opList, 0, index, updateCount);

        _probeUpdateIndex += updateCount;
        _probeUpdateIndex %= end;
    }
}