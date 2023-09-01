using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlaneWindFieldPass : ScriptableRenderPass
{
    // Shader textures ID
    private static readonly int HistoryWindFieldTextureId = Shader.PropertyToID("_HistoryWindFieldTexture");
    private static readonly int DepthMotionRecordTextureId = Shader.PropertyToID("_DepthMotionRecordTexture");
    private static readonly int GroundDepthRecordTextureId = Shader.PropertyToID("_GroundDepthRecordTexture");
    private static readonly int SDFTextureId = Shader.PropertyToID("_SDFTexture");
    
    // Shader variables ID
    private static readonly int PrevMatrixVPId = Shader.PropertyToID("_PrevMatrixVP");
    private static readonly int CurrMatrixVPId = Shader.PropertyToID("_CurrMatrixVP");
    private static readonly int OffsetUVId = Shader.PropertyToID("_OffsetUV");
    private static readonly int CurrWindDirectionId = Shader.PropertyToID("_CurrWindDirection");
    private static readonly int BlankingSpeedId = Shader.PropertyToID("_BlankingSpeed");
    private static readonly int RecordDistanceId = Shader.PropertyToID("_RecordDistance");
    private static readonly int TextureSizeId = Shader.PropertyToID("_TextureSize");
    
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
    private bool _rtInit = false;

    public PlaneWindFieldPass(string profilingName, RenderPassEvent renderPassEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(DepthMotionRecordPass));
        this.renderPassEvent = renderPassEvent;

        _profilingSampler = new ProfilingSampler(profilingName);
        _shaderTagId = new ShaderTagId("PlaneSDF");

        _shader = Shader.Find("Hidden/Custom/Interaction/PlaneWindField");
        // _computeShader = Resources.Load<ComputeShader>("");

        if (_shader == null && !SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("Can not find shader Hidden/Custom/Interaction/PlaneWindField");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(_shader);
        _propertyBlock = new MaterialPropertyBlock();
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
            if (setting.recordTarget == null || !setting.generateWindField)
            {
                return;
            }

            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.width = (int)setting.renderTextureSize;
            descriptor.height = (int)setting.renderTextureSize;
            descriptor.colorFormat = RenderTextureFormat.RGFloat;
            descriptor.depthBufferBits = 0;

            DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTexturesArray[i].PlaneWindField1, descriptor,
                wrapMode: TextureWrapMode.Clamp, filterMode: FilterMode.Bilinear,
                name: SceneInteractionRendererFeature.RenderTextures.ConstVars.PlaneWindFieldTextureName(1));

            if (!_rtInit)
            {
                DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTexturesArray[i].PlaneWindField0, descriptor,
                    wrapMode: TextureWrapMode.Clamp, filterMode: FilterMode.Bilinear,
                    name: SceneInteractionRendererFeature.RenderTextures.ConstVars.PlaneWindFieldTextureName(0));
                _rtInit = true;
            }
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
                if (setting.recordTarget == null || !setting.generateWindField)
                {
                    return;
                }
                
                // TODO: Set ortho view projection override matrix
                Matrix4x4 currView = setting.CurrView;
                Matrix4x4 prevView = setting.PrevView;
                
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(
                    setting.Projection, cameraData.IsCameraProjectionMatrixFlipped());
                
                //Debug.Log($"CurrView: {projection * currView}");
                //Debug.Log($"PrevView: {projection * prevView}");
                //Debug.Log($"Proj: {projection}");

                Vector3 offsetPos = setting.CurrCameraPosition - setting.PrevCameraPosition;
                Vector2 offsetUV = new Vector2(offsetPos.x, offsetPos.z);
                offsetUV /= setting.recordDistance * 2.0f;

                var volume = VolumeManager.instance.stack.GetComponent<Wind>();
                if (volume == null || !volume.IsActive())
                {
                    Debug.LogWarning("Can't find Wind Volume or it isn't active");
                    return;
                }

                _propertyBlock.SetTexture(HistoryWindFieldTextureId, _renderTexturesArray[i].PlaneWindField0);
                _propertyBlock.SetTexture(DepthMotionRecordTextureId, _renderTexturesArray[i].DepthMotionRecord);
                _propertyBlock.SetTexture(SDFTextureId, _renderTexturesArray[i].PlaneSdf1);

                _propertyBlock.SetMatrix(PrevMatrixVPId, projection * prevView);
                _propertyBlock.SetMatrix(CurrMatrixVPId, projection * currView);
                _propertyBlock.SetVector(OffsetUVId, offsetUV);
                _propertyBlock.SetVector(CurrWindDirectionId, volume.windDirection.value);
                _propertyBlock.SetFloat(BlankingSpeedId, volume.blankingSpeed.value);
                _propertyBlock.SetFloat(RecordDistanceId, setting.recordDistance);
                _propertyBlock.SetFloat(TextureSizeId, (int)setting.renderTextureSize);
                
                cmd.SetRenderTarget(_renderTexturesArray[i].PlaneWindField1);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                DrawUtils.DrawFullscreenMesh(cmd, _material, _propertyBlock);
                cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix());
                cmd.SetRenderTarget(cameraData.renderer.cameraColorTarget, cameraData.renderer.cameraDepthTarget);
            }
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
}