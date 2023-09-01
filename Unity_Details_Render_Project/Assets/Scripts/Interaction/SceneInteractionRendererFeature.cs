using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[Serializable]
public class SceneInteractionRendererFeatureSetting
{
    public bool generateSDF = true;
    public bool generateWindField = true;
}

[Serializable]
public class RecordCameraSetting
{
    [Header("Camera Setting")][Space(1.0f)]
    public GameObject recordTarget = null;
    public float recordDistance    = 50.0f;     // Meter
    public float cameraNear        = -50.0f;
    public float cameraFar         = 50.0f;
    public bool lookUp             = true;

    [Space][Header("Filter Setting")][Space(1.0f)]
    public LayerMask recordLayerMask = 0;
    public LayerMask groundLayerMask = 0;
    
    public enum TextureSize : int
    {
        _256x256 = 256,
        _512x512 = 512,
        _1024x1024 = 1024,
        _2048x2048 = 2048,
        _4096x4096 = 4096
    }
    [Space][Header("Render Texture Setting")][Space(1.0f)]
    public TextureSize renderTextureSize = TextureSize._1024x1024;
    
    [Space]
    public PlanarSDFSetting[] planarSDFSettings;
    public string[] additionalLightModeTags;
    
    [Space][Header("Pass Setting")][Space(1.0f)]
    public bool generateWindField = true;

    public Vector3 CurrCameraPosition { get; set; }
    public Vector3 PrevCameraPosition { get; set; }
    public Matrix4x4 Projection { get; set; }
    public Matrix4x4 CurrView { get; set; }
    public Matrix4x4 PrevView { get; set; }
    public Vector4 OrthoParams { get; set; }

    private int _frame = 0;

    public void Update()
    {
        PrevCameraPosition = CurrCameraPosition;

        if (recordTarget != null)// && _frame % 100 == 0)
        {
            CurrCameraPosition = recordTarget.transform.position;
        }

        _frame++;
        _frame %= 100000;
        
        float near = cameraNear;
        float far = cameraFar;
        float size = recordDistance;
        Projection = Matrix4x4.Ortho(-size, size, -size, size, near, far);

        Matrix4x4 currView = Matrix4x4.zero;
        currView[0, 0] = 1.0f;
        currView[1, 2] = -1.0f;
        currView[2, 1] = -1.0f;
        currView[3, 3] = 1.0f;
        currView[0, 3] = -CurrCameraPosition.x;
        currView[1, 3] = CurrCameraPosition.z;
        currView[2, 3] = CurrCameraPosition.y;
        CurrView = currView;
        
        Matrix4x4 prevView = Matrix4x4.zero;
        prevView[0, 0] = 1.0f;
        prevView[1, 2] = -1.0f;
        prevView[2, 1] = -1.0f;
        prevView[3, 3] = 1.0f;
        prevView[0, 3] = -PrevCameraPosition.x;
        prevView[1, 3] = PrevCameraPosition.z;
        prevView[2, 3] = PrevCameraPosition.y;
        PrevView = prevView;
        
        float reversed = Convert.ToInt32(SystemInfo.usesReversedZBuffer);
        OrthoParams = new Vector4(near, far, reversed, 0.0f);
    }
}

[Serializable]
public class PlanarSDFSetting
{
    public enum IntersectMode
    {
        RelativeHeight,
        AbsoluteHeight,
        TerrainGround
    }

    public IntersectMode intersectMode = IntersectMode.TerrainGround;
    public float planeRelativeHeight = 0.2f;
    public float planeAbsoluteHeight = 0.0f;
    public bool enableOutside = true;
    public bool enableInside = true;
}

public class SceneInteractionRendererFeature : ScriptableRendererFeature
{
    public class RenderTextures
    {
        public static class ConstVars
        {
            public static readonly string DepthMotionRecordTextureName = "_DepthMotionRecordTexture";
            public static readonly string GroundDepthRecordTextureName = "_GroundDepthRecordTexture";
            public static readonly string PlaneSdfTexture1Name = "_PlaneSdfTexture1";
            public static readonly string PlaneSdfTexture2Name = "_PlaneSdfTexture2";
            public static readonly string PlaneSdfTexture3Name = "_PlaneSdfTexture3";
            public static readonly string PlaneSdfTexture4Name = "_PlaneSdfTexture4";
            public static readonly string PlaneSdfTexture5Name = "_PlaneSdfTexture5";
            public static readonly string PlaneWindTexture0Name = "_PlaneWindFieldTexture0";
            public static readonly string PlaneWindTexture1Name = "_PlaneWindFieldTexture1";

            private static string[] _planeSdfTextureNames = new string[]
            {
                PlaneSdfTexture1Name,
                PlaneSdfTexture2Name,
                PlaneSdfTexture3Name,
                PlaneSdfTexture4Name,
                PlaneSdfTexture5Name
            };
            
            private static string[] _planeWindFieldTextureNames = new string[]
            {
                PlaneWindTexture0Name,
                PlaneWindTexture1Name
            };

            public static string PlaneSdfTextureName(int index) => _planeSdfTextureNames[index];
            public static string PlaneWindFieldTextureName(int index) => _planeWindFieldTextureNames[index];

            public static void SwapWindFieldTextureName() =>
                CoreUtils.Swap(ref _planeWindFieldTextureNames[0], ref _planeWindFieldTextureNames[1]);
        }

        private readonly RTHandle[] _rtHandles = new RTHandle[9];

        public ref RTHandle DepthMotionRecord => ref _rtHandles[0];
        public ref RTHandle GroundDepthRecord => ref _rtHandles[1];
        public ref RTHandle PlaneSdf1 => ref _rtHandles[2];
        public ref RTHandle PlaneSdf2 => ref _rtHandles[3];
        public ref RTHandle PlaneSdf3 => ref _rtHandles[4];
        public ref RTHandle PlaneSdf4 => ref _rtHandles[5];
        public ref RTHandle PlaneSdf5 => ref _rtHandles[6];
        public ref RTHandle PlaneWindField0 => ref _rtHandles[7];
        public ref RTHandle PlaneWindField1 => ref _rtHandles[8];

        public ref RTHandle this[int index] => ref _rtHandles[index];

        public ref RTHandle SDFRef(int index) => ref this[index + 2];

        public void SwapPlaneWindField()
        {
            CoreUtils.Swap(ref PlaneWindField0, ref PlaneWindField1);
            ConstVars.SwapWindFieldTextureName();
        }

        public void Release()
        {
            foreach (var handle in _rtHandles)
            {
                handle?.Release();
            }
        }
    }

    // Renderer feature setting
    public SceneInteractionRendererFeatureSetting setting;
    
    // Renderer Cameras setting
    private RecordCameraSetting[] _recordCameraSettings;
    
    // Render passes
    private DepthMotionRecordPass _depthMotionRecordPass;
    private PlaneSdfPass _planeSdfPass;
    private PlaneWindFieldPass _planeWindFieldPass;
    
    // Render textures
    private RenderTextures[] _renderTexturesArray;

    public override void Create()
    {
        _depthMotionRecordPass = new DepthMotionRecordPass("DepthMotionRecord", RenderPassEvent.BeforeRenderingGbuffer);
        _planeSdfPass = new PlaneSdfPass("PlaneSdfRender", RenderPassEvent.BeforeRenderingGbuffer);
        _planeWindFieldPass = new PlaneWindFieldPass("PlaneWindFieldRender", RenderPassEvent.BeforeRenderingGbuffer);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _recordCameraSettings = InteractionDataManager.Instance.RecordCameraSettings;
        if (_recordCameraSettings == null)
        {
            return;
        }

        _renderTexturesArray = 
            RenderTexturesManager.Instance.ReAllocRenderTexturesArray(_recordCameraSettings.Length);
        
        _depthMotionRecordPass.Setup(_recordCameraSettings, _renderTexturesArray);
        renderer.EnqueuePass(_depthMotionRecordPass);

        if (setting.generateSDF)
        {
            _planeSdfPass.Setup(_recordCameraSettings, _renderTexturesArray);
            renderer.EnqueuePass(_planeSdfPass);
        }

        if (setting.generateWindField)
        {
            //foreach (var rt in _renderTexturesArray)
            //{
            //    rt.SwapPlaneWindField();
            //}

            for (int i = 0; i < _renderTexturesArray.Length; i++)
            {
                RenderTexturesManager.Instance.renderTexturesArray[i].SwapPlaneWindField();
            }
            
            _planeWindFieldPass.Setup(_recordCameraSettings, _renderTexturesArray);
            renderer.EnqueuePass(_planeWindFieldPass);
        }
    }

    public void OnDestroy()
    {
        foreach (var textures in RenderTexturesManager.Instance.renderTexturesArray)
        {
            textures?.Release();
        }
    }

    public void OnDisable()
    {
        OnDestroy();
    }
}
