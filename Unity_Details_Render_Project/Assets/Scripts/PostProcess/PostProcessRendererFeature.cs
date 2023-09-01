using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SwapRenderTexture
{
    private readonly RTHandle[] _swapRenderTextures = new RTHandle[2];
    private readonly string[] _swapRenderTextureNames = new string[2];
    
    public ref RTHandle SrcTex => ref _swapRenderTextures[0];
    public ref RTHandle DstTex => ref _swapRenderTextures[1];
    
    public string SrcName => _swapRenderTextureNames[0];
    public string DstName => _swapRenderTextureNames[1];

    public SwapRenderTexture(string name = "_SwapRenderTexture")
    {
        _swapRenderTextureNames[0] = $"{name}0";
        _swapRenderTextureNames[1] = $"{name}1";
    }

    public void Swap()
    {
        CoreUtils.Swap(ref _swapRenderTextures[0], ref _swapRenderTextures[1]);
        CoreUtils.Swap(ref _swapRenderTextureNames[0], ref _swapRenderTextureNames[1]);
    }
}

public class PostProcessRendererFeature : ScriptableRendererFeature
{
    public static bool beforeProcessDone;
    
    private ScreenSpaceFogPass _screenSpaceFogPass;
    private FinalBlitPass _beforeProcessingBlitPass;
    
    private readonly SwapRenderTexture _postProcessTexture = new SwapRenderTexture("_PostProcessTexture");
    
    public override void Create()
    {
        _screenSpaceFogPass = 
            new ScreenSpaceFogPass("Screen Space Fog", RenderPassEvent.BeforeRenderingPostProcessing);
        _beforeProcessingBlitPass =
            new FinalBlitPass("Before Processing Blit", RenderPassEvent.BeforeRenderingPostProcessing);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        beforeProcessDone = false;
        
        _screenSpaceFogPass.Setup(_postProcessTexture);
        renderer.EnqueuePass(_screenSpaceFogPass);
        
        _postProcessTexture.Swap();
        
        _beforeProcessingBlitPass.Setup(_postProcessTexture);
        renderer.EnqueuePass(_beforeProcessingBlitPass);
    }
}