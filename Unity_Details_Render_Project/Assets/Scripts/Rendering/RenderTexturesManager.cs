using System;

public class RenderTexturesManager
{
    private static readonly Lazy<RenderTexturesManager> Ins =
        new Lazy<RenderTexturesManager>(() => new RenderTexturesManager());

    public static RenderTexturesManager Instance => Ins.Value;

    public SceneInteractionRendererFeature.RenderTextures[] renderTexturesArray;

    public RenderTexturesManager()
    {
        renderTexturesArray = Array.Empty<SceneInteractionRendererFeature.RenderTextures>();
    }
    
    public SceneInteractionRendererFeature.RenderTextures[] ReAllocRenderTexturesArray(int size)
    {
        if (renderTexturesArray.Length == size)
        {
            return renderTexturesArray;
        }

        SceneInteractionRendererFeature.RenderTextures[] newTextures = 
            new SceneInteractionRendererFeature.RenderTextures[size];
        if (renderTexturesArray.Length < size)
        {
            for (int i = 0; i < renderTexturesArray.Length; i++)
            {
                newTextures[i] = renderTexturesArray[i];
            }

            for (int i = renderTexturesArray.Length; i < size; i++)
            {
                newTextures[i] = new SceneInteractionRendererFeature.RenderTextures();
            }
        }
        else if (renderTexturesArray.Length > size)
        {
            for (int i = 0; i < size; i++)
            {
                newTextures[i] = renderTexturesArray[i];
            }

            for (int i = size; i < renderTexturesArray.Length; i++)
            {
                renderTexturesArray[i]?.Release();
            }
        }

        renderTexturesArray = newTextures;
        return renderTexturesArray;
    }
}