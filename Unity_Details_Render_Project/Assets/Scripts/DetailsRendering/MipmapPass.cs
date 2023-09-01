using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MipmapPass : ScriptableRenderPass
{
    // Shader texture ID
    private static readonly int SourceDepthTextureId = Shader.PropertyToID("_SourceDepthTexture");
    private static readonly int MipmapDepthTextureId = Shader.PropertyToID("_MipmapDepthTexture");
    
    // Pass info
    private ProfilingSampler _profilingSampler;
    
    // Material
    private Material _material;
    
    // Pass setting
    private DetailsRendererFeatureSetting _setting;
    private bool _enableComputeShader;
    
    // Depth mipmap texture RT
    private DetailsRendererFeature.RenderTextures _renderTextures;
    private RenderTextureDescriptor _depthMipmapTextureDesc;

    public MipmapPass(string profilingName, RenderPassEvent passEvent)
    {
        this.profilingSampler = new ProfilingSampler(nameof(MipmapPass));
        this.renderPassEvent = passEvent;
        
        _profilingSampler = new ProfilingSampler(profilingName);
        
        Shader depthMipmapShader = Shader.Find("Hidden/Custom/Depth/DepthMipmap");

        if (depthMipmapShader == null)
        {
            Debug.LogError("Can not find shader: Hidden/Custom/Depth/DepthMipmap");
        }

        _material = CoreUtils.CreateEngineMaterial(depthMipmapShader);
    }
    
    public void Setup(DetailsRendererFeatureSetting setting, DetailsRendererFeature.RenderTextures renderTextures)
    {
        _setting = setting;
        _renderTextures = renderTextures;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        _depthMipmapTextureDesc = cameraTextureDescriptor;
        // _depthMipmapTextureDesc.width = _depthMipmapTextureDesc.height;
        _depthMipmapTextureDesc.autoGenerateMips = true;
        _depthMipmapTextureDesc.useMipMap = true;
        _depthMipmapTextureDesc.depthBufferBits = 0;

        DrawUtils.RTHandleReAllocateIfNeeded(ref _renderTextures[0], _depthMipmapTextureDesc,
            name: DetailsRendererFeature.DepthMipmapTextureName);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;
            
            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }

            int width = _depthMipmapTextureDesc.width;
            int height = _depthMipmapTextureDesc.height;

            int mipLevel = 0;

            RenderTexture src = null;
            RenderTexture dest = null;

            while (width > 4 || height > 4)
            {
                dest = RenderTexture.GetTemporary(width, height, 0, _renderTextures.DepthMipmap.rt.format);
                dest.filterMode = FilterMode.Point;

                if (src == null)
                {
                    cmd.Blit(cameraData.renderer.cameraDepthTarget, dest, _material);
                }
                else
                {
                    _material.SetTexture(Shader.PropertyToID("_MainTex"), src);
                    cmd.Blit(src, dest, _material);
                    RenderTexture.ReleaseTemporary(src);
                }
                
                cmd.CopyTexture(dest, 0, 0, 
                    _renderTextures[0], 0, mipLevel);

                src = dest;
                width /= 2;
                height /= 2;
                mipLevel++;
            }
            RenderTexture.ReleaseTemporary(dest);
            
            cmd.SetRenderTarget(cameraData.renderer.cameraColorTarget, cameraData.renderer.cameraDepthTarget);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}