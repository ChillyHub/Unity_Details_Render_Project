using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FinalBlitPass : ScriptableRenderPass
{
    // Pass state
    private ProfilingSampler _profilingSampler;

    // Render textures
    private SwapRenderTexture _postProcessTexture;
    
    public FinalBlitPass(string profilingName, RenderPassEvent renderPassEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(ScreenSpaceFogPass));
        this.renderPassEvent = renderPassEvent;

        _profilingSampler = new ProfilingSampler(profilingName);
    }
    
    public void Setup(SwapRenderTexture postProcessTexture)
    {
        _postProcessTexture = postProcessTexture;
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
            Blit(cmd, _postProcessTexture.SrcTex, cameraData.renderer.cameraColorTarget);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    
    private bool CheckEnableRenderPass(Camera camera = null)
    {
        if (!PostProcessRendererFeature.beforeProcessDone)
        {
            return false;
        }
        
        if (camera != null 
            && (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection))
        {
            return false;
        }

        return true;
    }
}