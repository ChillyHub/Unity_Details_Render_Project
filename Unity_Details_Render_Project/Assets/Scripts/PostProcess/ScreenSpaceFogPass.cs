using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceFogPass : ScriptableRenderPass
{
    private static class ShaderConstants
    {
        public static readonly int SourceTexId = Shader.PropertyToID("_SourceTex");
        public static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        
        public static readonly int FogColorDayId = Shader.PropertyToID("_FogColorDay");
        public static readonly int FogColorNightId = Shader.PropertyToID("_FogColorNight");
        public static readonly int DensityId = Shader.PropertyToID("_Density");
        public static readonly int HeightFogStartId = Shader.PropertyToID("_HeightFogStart");
        public static readonly int HeightFogDensityId = Shader.PropertyToID("_HeightFogDensity");
        public static readonly int DistanceFogMaxLengthId = Shader.PropertyToID("_DistanceFogMaxLength");
        public static readonly int DistanceFogDensityId = Shader.PropertyToID("_DistanceFogDensity");
        public static readonly int DayScatteringColorId = Shader.PropertyToID("_DayScatteringColor");
        public static readonly int NightScatteringColorId = Shader.PropertyToID("_NightScatteringColor");
        public static readonly int ScatteringId = Shader.PropertyToID("_Scattering");
        public static readonly int ScatteringRedWaveId = Shader.PropertyToID("_ScatteringRedWave");
        public static readonly int ScatteringGreenWaveId = Shader.PropertyToID("_ScatteringGreenWave");
        public static readonly int ScatteringBlueWaveId = Shader.PropertyToID("_ScatteringBlueWave");
        public static readonly int ScatteringMoonId = Shader.PropertyToID("_ScatteringMoon");
        public static readonly int ScatteringFogDensityId = Shader.PropertyToID("_ScatteringFogDensity");
        public static readonly int DayScatteringFacId = Shader.PropertyToID("_DayScatteringFac");
        public static readonly int NightScatteringFacId = Shader.PropertyToID("_NightScatteringFac");
        public static readonly int GDayMieId = Shader.PropertyToID("_gDayMie");
        public static readonly int GNightMieId = Shader.PropertyToID("_gNightMie");
        public static readonly int DynamicFogHeightId = Shader.PropertyToID("_DynamicFogHeight");
        public static readonly int DynamicFogDensityId = Shader.PropertyToID("_DynamicFogDensity");
        public static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        public static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");
    }
    
    // Pass state
    private ProfilingSampler _profilingSampler;

    // Shader and Material
    private readonly Shader _shader;
    private Material _material;
    private MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
    
    // Camera settings
    private RecordCameraSetting[] _cameraSettings;

    // Render textures
    private SwapRenderTexture _postProcessTexture;
    
    public ScreenSpaceFogPass(string profilingName, RenderPassEvent renderPassEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(ScreenSpaceFogPass));
        this.renderPassEvent = renderPassEvent;

        _profilingSampler = new ProfilingSampler(profilingName);

        _shader = Shader.Find("Hidden/Custom/PostProcess/Screen Space Fog");

        if (_shader == null)
        {
            Debug.LogError("Can not find shader Hidden/Custom/PostProcess/Screen Space Fog");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(_shader);
        _propertyBlock = new MaterialPropertyBlock();
    }
    
    public void Setup(SwapRenderTexture postProcessTexture)
    {
        _postProcessTexture = postProcessTexture;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        if (!CheckEnableRenderPass())
        {
            return;
        }

        RenderTextureDescriptor descriptor = cameraTextureDescriptor;
        descriptor.depthBufferBits = 0;

        DrawUtils.RTHandleReAllocateIfNeeded(ref _postProcessTexture.DstTex, descriptor,
            name: _postProcessTexture.DstName);
        
        ConfigureTarget(_postProcessTexture.DstTex);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;

        if (!CheckEnableRenderPass(camera))
        {
            return;
        }
        
        var volume = VolumeManager.instance.stack.GetComponent<ScreenSpaceFog>();
        if (volume == null || !volume.IsActive() || _material == null)
        {
            return;
        }
        
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            _propertyBlock.SetColor(ShaderConstants.FogColorDayId, volume.fogColorDay.value);
            _propertyBlock.SetColor(ShaderConstants.FogColorNightId, volume.fogColorNight.value);
            _propertyBlock.SetFloat(ShaderConstants.DensityId, volume.density.value);
            _propertyBlock.SetFloat(ShaderConstants.HeightFogStartId, volume.heightFogStart.value);
            _propertyBlock.SetFloat(ShaderConstants.HeightFogDensityId, volume.heightFogDensity.value);
            _propertyBlock.SetFloat(ShaderConstants.DistanceFogMaxLengthId, volume.distanceFogMaxLength.value);
            _propertyBlock.SetFloat(ShaderConstants.DistanceFogDensityId, volume.distanceFogDensity.value);
            _propertyBlock.SetColor(ShaderConstants.DayScatteringColorId, volume.dayScatteringColor.value);
            _propertyBlock.SetColor(ShaderConstants.NightScatteringColorId, volume.nightScatteringColor.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringId, volume.scattering.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringRedWaveId, volume.scatteringRedWave.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringGreenWaveId, volume.scatteringGreenWave.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringBlueWaveId, volume.scatteringBlueWave.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringMoonId, volume.scatteringMoon.value);
            _propertyBlock.SetFloat(ShaderConstants.ScatteringFogDensityId, volume.scatteringFogDensity.value);
            _propertyBlock.SetFloat(ShaderConstants.DayScatteringFacId, volume.dayScatteringFac.value);
            _propertyBlock.SetFloat(ShaderConstants.NightScatteringFacId, volume.nightScatteringFac.value);
            _propertyBlock.SetFloat(ShaderConstants.GDayMieId, volume.gDayMie.value);
            _propertyBlock.SetFloat(ShaderConstants.GNightMieId, volume.gNightMie.value);
            _propertyBlock.SetFloat(ShaderConstants.DynamicFogHeightId, volume.dynamicFogHeight.value);
            _propertyBlock.SetFloat(ShaderConstants.DynamicFogDensityId, volume.dynamicFogDensity.value);
            _propertyBlock.SetVector(ShaderConstants.SunDirectionId, (Vector4)volume.sunDirection);
            _propertyBlock.SetVector(ShaderConstants.MoonDirectionId, (Vector4)volume.moonDirection);
            
            RenderTargetIdentifier src = PostProcessRendererFeature.beforeProcessDone
                ? _postProcessTexture.SrcTex
                : cameraData.renderer.cameraColorTarget;
            cmd.SetGlobalTexture(ShaderConstants.SourceTexId, src);
            cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, cameraData.renderer.cameraDepthTarget);
            
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            DrawUtils.DrawFullscreenMesh(cmd, _material, _propertyBlock);
            cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix());
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        PostProcessRendererFeature.beforeProcessDone = true;
    }
    
    private bool CheckEnableRenderPass(Camera camera = null)
    {
        if (camera != null 
            && (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection))
        {
            return false;
        }

        return true;
    }
}
