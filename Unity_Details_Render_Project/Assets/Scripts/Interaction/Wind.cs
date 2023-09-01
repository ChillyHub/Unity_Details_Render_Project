using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Wind : VolumeComponent, IPostProcessComponent
{
    public Vector4Parameter windDirection = new Vector4Parameter(new Vector3(0.0f, 0.0f, 0.0f));
    public ClampedFloatParameter blankingSpeed = new ClampedFloatParameter(0.2f, 0.0f, 1.0f);
    
    public bool IsActive() => true;

    public bool IsTileCompatible() => false;
}