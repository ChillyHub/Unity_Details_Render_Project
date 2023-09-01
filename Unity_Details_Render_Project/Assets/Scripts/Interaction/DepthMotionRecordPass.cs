using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthMotionRecordPass : ScriptableRenderPass
{
    // Shader variable ID
    private static readonly int PrevMatrixVPId = Shader.PropertyToID("_PrevMatrixVP");
    
    // Pass state
    private ProfilingSampler _profilingSampler;

    // Shader and Material
    private readonly Shader _shader;
    private Material _material;
    private MaterialPropertyBlock _propertyBlock;
    
    // Camera settings
    private RecordCameraSetting[] _cameraSettings;

    // Render textures
    private SceneInteractionRendererFeature.RenderTextures[] _renderTexturesArray;
    private RTHandle _depthTexture;
    
    public DepthMotionRecordPass(string profilingName, RenderPassEvent renderPassEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(DepthMotionRecordPass));
        this.renderPassEvent = renderPassEvent;

        _profilingSampler = new ProfilingSampler(profilingName);

        _shader = Shader.Find("Hidden/Custom/Interaction/DepthMotionRecord");

        if (_shader == null)
        {
            Debug.LogError("Can not find shader Hidden/Custom/Interaction/DepthMotionRecord");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(_shader);
        _propertyBlock = new MaterialPropertyBlock();

        _material.enableInstancing = false;
    }

    public void Setup(
        RecordCameraSetting[] cameraSettings, 
        SceneInteractionRendererFeature.RenderTextures[] renderTexturesArray)
    {
        _cameraSettings = cameraSettings;
        _renderTexturesArray = renderTexturesArray;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        if (!CheckEnableRenderPass())
        {
            return;
        }
        
        int cameraCount = _cameraSettings.Length;
        for (int i = 0; i < cameraCount; i++)
        {
            RecordCameraSetting setting = _cameraSettings[i];
            if (setting.recordTarget == null)
            {
                return;
            }

            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.width = (int)setting.renderTextureSize;
            descriptor.height = (int)setting.renderTextureSize;
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;

            DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTexturesArray[i].DepthMotionRecord, descriptor,
                name: SceneInteractionRendererFeature.RenderTextures.ConstVars.DepthMotionRecordTextureName);
            DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTexturesArray[i].GroundDepthRecord, descriptor,
                name: SceneInteractionRendererFeature.RenderTextures.ConstVars.GroundDepthRecordTextureName);

            descriptor.depthBufferBits = 32;
            DrawUtils.RTHandleReAllocateIfNeeded(ref _depthTexture, descriptor, name: "_TempDepthTexture");
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;

        if (!CheckEnableRenderPass(camera))
        {
            return;
        }

        SortingCriteria sortingCriteria = cameraData.defaultOpaqueSortFlags;
        RenderStateBlock recordStateBlock = new RenderStateBlock()
        {
            rasterState = new RasterState(CullMode.Back), 
            depthState = new DepthState(true, CompareFunction.LessEqual),
            mask = RenderStateMask.Raster | RenderStateMask.Depth
        };
        RenderStateBlock groundStateBlock = new RenderStateBlock()
        {
            rasterState = new RasterState(CullMode.Front),
            depthState = new DepthState(true, CompareFunction.GreaterEqual), 
            mask = RenderStateMask.Raster | RenderStateMask.Depth
        };

        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            int cameraCount = _cameraSettings.Length;
            for (int i = 0; i < cameraCount; i++)
            {
                RecordCameraSetting setting = _cameraSettings[i];
                if (setting.recordTarget == null)
                {
                    return;
                }
                
                // Set motion vector
                var textureMode = renderingData.cameraData.camera.depthTextureMode;
                DepthTextureMode depthTextureMode = textureMode;
                textureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
                renderingData.cameraData.camera.depthTextureMode = textureMode;

                // TODO: Set ortho view projection override matrix
                Matrix4x4 currView = setting.CurrView;
                Matrix4x4 prevView = setting.PrevView;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(
                    setting.Projection, cameraData.IsCameraProjectionMatrixFlipped());

                RenderingUtils.SetViewAndProjectionMatrices(cmd, currView, projection, true);
                _material.SetMatrix(PrevMatrixVPId, projection * prevView);

                // TODO: Execute record pass
                DrawingSettings drawingSettings = CreateDrawingSettings(
                    GetShaderTagIds(setting.additionalLightModeTags), ref renderingData, sortingCriteria);
                drawingSettings.overrideMaterial = _material;
                drawingSettings.overrideMaterialPassIndex = 0;
                drawingSettings.perObjectData = PerObjectData.MotionVectors;

                FilteringSettings recordFiltering = new FilteringSettings(RenderQueueRange.all, (int)setting.recordLayerMask);
                FilteringSettings groundFiltering = new FilteringSettings(RenderQueueRange.all, (int)setting.groundLayerMask);

                cmd.SetRenderTarget(_renderTexturesArray[i].DepthMotionRecord, _depthTexture);
                cmd.ClearRenderTarget(true, true, Color.clear, 1.0f);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref recordFiltering, ref recordStateBlock);
                
                cmd.SetRenderTarget(_renderTexturesArray[i].GroundDepthRecord, _depthTexture);
                cmd.ClearRenderTarget(true, true, Color.clear, 0.0f);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref groundFiltering, ref groundStateBlock);
            }
            
            RenderingUtils.SetViewAndProjectionMatrices(
                cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), true);
            cmd.SetRenderTarget(cameraData.renderer.cameraColorTarget, cameraData.renderer.cameraDepthTarget);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private bool CheckEnableRenderPass(Camera camera = null)
    {
        if (camera != null 
            && (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection))
        {
            return false;
        }
        
        if (_cameraSettings.Length == 0 || _renderTexturesArray.Length == 0)
        {
            return false;
        }

        if (_cameraSettings.Length != _renderTexturesArray.Length)
        {
            Debug.LogError("Render textures nums do not march cameras nums");
            return false;
        }

        return true;
    }
    
    private List<ShaderTagId> GetShaderTagIds(string[] tags)
    {
        List<ShaderTagId> shaderTagIds = new List<ShaderTagId>();
        shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
        shaderTagIds.Add(new ShaderTagId("UniversalForward"));
        shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        foreach (var tag in tags)
        {
            shaderTagIds.Add(new ShaderTagId(tag));
        }

        return shaderTagIds;
    }
}
