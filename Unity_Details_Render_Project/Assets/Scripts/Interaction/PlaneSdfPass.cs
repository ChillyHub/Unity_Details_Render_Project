using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlaneSdfPass : ScriptableRenderPass
{
    // Compute shader kernel name
    private static readonly string SDFInitializeName = "SDFInitializeCSMain";
    private static readonly string SDFCalculateName = "SDFCalculateCSMain";
    private static readonly string SDFMergeName = "SDFMergeCSMain";
    
    // Temp render texture name
    private static readonly string SwapTempTexture0Name = "_SwapTempTexture0";
    private static readonly string SwapTempTexture1Name = "_SwapTempTexture1";
    private static readonly string[] SwapTempTexturesName = new[]
    {
        SwapTempTexture0Name, SwapTempTexture1Name
    };
    
    // Compute shader textures ID
    private static readonly int SDFTextureId = Shader.PropertyToID("_SDFTexture");
    private static readonly int DestTextureId = Shader.PropertyToID("_DestTexture");
    private static readonly int SrcTextureId = Shader.PropertyToID("_SrcTexture");
    private static readonly int DepthMotionTextureId = Shader.PropertyToID("_DepthMotionTexture");
    private static readonly int GroundDepthTextureId = Shader.PropertyToID("_GroundDepthTexture");
    
    // Shader Variable ID
    private static readonly int OrthoProjectionParams = Shader.PropertyToID("_OrthoProjectionParams");
    private static readonly int RelativeHeight = Shader.PropertyToID("_RelativeHeight");
    private static readonly int TextureSizeId = Shader.PropertyToID("_TextureSize");
    private static readonly int EnableOutsideId = Shader.PropertyToID("_EnableOutside");
    private static readonly int EnableInsideId = Shader.PropertyToID("_EnableInside");
    private static readonly int StepLenId = Shader.PropertyToID("_StepLen");

    // Pass state
    private ProfilingSampler _profilingSampler;
    private ShaderTagId _shaderTagId;
    
    // Shader and Material
    private readonly Shader _shader;
    private readonly ComputeShader _computeShader;
    private Material _material;
    private MaterialPropertyBlock _propertyBlock;
    
    // Camera settings
    private RecordCameraSetting[] _cameraSettings;

    // Render textures
    private SceneInteractionRendererFeature.RenderTextures[] _renderTexturesArray;
    private readonly RTHandle[] _swapTempTextures = new RTHandle[2];

    public PlaneSdfPass(string profilingName, RenderPassEvent renderPassEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(DepthMotionRecordPass));
        this.renderPassEvent = renderPassEvent;

        _profilingSampler = new ProfilingSampler(profilingName);
        _shaderTagId = new ShaderTagId("PlaneSDF");

        _shader = Shader.Find("Hidden/Custom/Interaction/PlaneSDF");
        _computeShader = Resources.Load<ComputeShader>("ComputeShader/PlaneSDF");

        if (_shader == null && !SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("Can not find shader Hidden/Custom/Interaction/PlaneSDF");
            return;
        }
        if (_shader != null)
        {
            _material = CoreUtils.CreateEngineMaterial(_shader);
        }
        
        _propertyBlock = new MaterialPropertyBlock();
        
        if (_computeShader)
        {
            SetKeyword();
        }
    }

    public void Setup(
        RecordCameraSetting[] cameraSettings, 
        SceneInteractionRendererFeature.RenderTextures[] renderTexturesArray)
    {
        _cameraSettings = cameraSettings;
        _renderTexturesArray = renderTexturesArray;
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
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = true;

            for (int j = 0; j < setting.planarSDFSettings.Length; j++)
            {
                DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTexturesArray[i].SDFRef(j), descriptor,
                    wrapMode: TextureWrapMode.Clamp, 
                    name: SceneInteractionRendererFeature.RenderTextures.ConstVars.PlaneSdfTextureName(j));
            }

            descriptor.colorFormat = RenderTextureFormat.ARGBInt;
            DrawUtils.RTHandleReAllocateIfNeeded(ref _swapTempTextures[0], descriptor, name: SwapTempTexturesName[0]);
            DrawUtils.RTHandleReAllocateIfNeeded(ref _swapTempTextures[1], descriptor, name: SwapTempTexturesName[1]);
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
                
                int planeSDFCount = setting.planarSDFSettings.Length;
                planeSDFCount = Mathf.Min(planeSDFCount, 5);
                for (int j = 0; j < planeSDFCount; j++)
                {
                    PlanarSDFSetting sdfSetting = setting.planarSDFSettings[j];
                    
                    if (SystemInfo.supportsComputeShaders && _computeShader != null)
                    {
                        ComputeShaderCommandsDispatch(cmd, i, j, setting, sdfSetting);
                    }
                    else
                    {
                        Debug.LogError("Shader is not supported");
                    }
                }
            }
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void SetKeyword() 
    {
        DrawUtils.SetKeyword(_computeShader, "_COMPUTE_SHADER_UV_STARTS_AT_TOP", SystemInfo.graphicsUVStartsAtTop);
        DrawUtils.SetKeyword(_computeShader, "_COMPUTE_SHADER_REVERSED_Z", SystemInfo.usesReversedZBuffer);
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

    private void ComputeShaderCommandsDispatch(CommandBuffer cmd, int i, int j, 
        RecordCameraSetting setting, PlanarSDFSetting sdfSetting)
    {
        cmd.SetComputeVectorParam(_computeShader, OrthoProjectionParams, setting.OrthoParams);
        cmd.SetComputeFloatParam(_computeShader, RelativeHeight, sdfSetting.planeRelativeHeight);
        
        cmd.SetComputeIntParam(_computeShader, TextureSizeId, (int)setting.renderTextureSize);
        cmd.SetComputeIntParam(_computeShader, EnableOutsideId, Convert.ToInt32(sdfSetting.enableOutside));
        cmd.SetComputeIntParam(_computeShader, EnableInsideId, Convert.ToInt32(sdfSetting.enableInside));

        int kernel = _computeShader.FindKernel(SDFInitializeName);
        cmd.SetComputeTextureParam(_computeShader, kernel, DestTextureId, _swapTempTextures[0]);
        cmd.SetComputeTextureParam(_computeShader, kernel, DepthMotionTextureId,
            _renderTexturesArray[i].DepthMotionRecord);
        cmd.SetComputeTextureParam(_computeShader, kernel, GroundDepthTextureId,
            _renderTexturesArray[i].GroundDepthRecord);

        DrawUtils.Dispatch(cmd, _computeShader, kernel, (float)setting.renderTextureSize, (float)setting.renderTextureSize);

        int step = (int)setting.renderTextureSize;
        kernel = _computeShader.FindKernel(SDFCalculateName);
        while (step > 0)
        {
            cmd.SetComputeIntParam(_computeShader, StepLenId, step);
            cmd.SetComputeTextureParam(_computeShader, kernel, SrcTextureId, _swapTempTextures[0]);
            cmd.SetComputeTextureParam(_computeShader, kernel, DestTextureId, _swapTempTextures[1]);

            DrawUtils.Dispatch(cmd, _computeShader, kernel, (float)setting.renderTextureSize, (float)setting.renderTextureSize);
            SwapTempTextures();
            step = (step >> 1);
        }

        kernel = _computeShader.FindKernel(SDFMergeName);
        cmd.SetComputeTextureParam(_computeShader, kernel, SrcTextureId, _swapTempTextures[0]);
        cmd.SetComputeTextureParam(_computeShader, kernel, DepthMotionTextureId,
            _renderTexturesArray[i].DepthMotionRecord);
        cmd.SetComputeTextureParam(_computeShader, kernel, GroundDepthTextureId,
            _renderTexturesArray[i].GroundDepthRecord);
        cmd.SetComputeTextureParam(_computeShader, kernel, SDFTextureId, _renderTexturesArray[i].SDFRef(j));

        DrawUtils.Dispatch(cmd, _computeShader, kernel, (float)setting.renderTextureSize, (float)setting.renderTextureSize);
    }

    private void SwapTempTextures()
    {
        CoreUtils.Swap(ref _swapTempTextures[0], ref _swapTempTextures[1]);
        CoreUtils.Swap(ref SwapTempTexturesName[0], ref SwapTempTexturesName[1]);
    }
}
