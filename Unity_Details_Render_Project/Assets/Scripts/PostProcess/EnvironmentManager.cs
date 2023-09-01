using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[ExecuteAlways]
[DisallowMultipleComponent]
public class EnvironmentManager : MonoBehaviour
{
    private static readonly int ScatteringId = Shader.PropertyToID("_Scattering");
    private static readonly int ScatteringRedWaveId = Shader.PropertyToID("_ScatteringRedWave");
    private static readonly int ScatteringGreenWaveId = Shader.PropertyToID("_ScatteringGreenWave");
    private static readonly int ScatteringBlueWaveId = Shader.PropertyToID("_ScatteringBlueWave");
    private static readonly int ScatteringMoonId = Shader.PropertyToID("_ScatteringMoon");

    private static readonly int DayScatteringFacId = Shader.PropertyToID("_dayScatteringFac");
    private static readonly int NightScatteringFacId = Shader.PropertyToID("_nightScatteringFac");
    private static readonly int GDayMieId = Shader.PropertyToID("_gDayMie");
    private static readonly int GNightMieId = Shader.PropertyToID("_gNightMie");

    private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    private static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");

    public Material skyboxMaterial;
    public Light sunLight;
    public Light moonLight;

    public Vector3 rotation;
    
    public bool autoRotateSun = false;
    
    [Range(0.0f, 1.0f)]
    public float autoRotateSpeed = 0.5f;

    public bool inverse = false;
    
    private void Start()
    {
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.sun = sunLight;

            rotation = sunLight.transform.rotation.eulerAngles;
        }
    }

    private void Update()
    {
        var volume = VolumeManager.instance.stack.GetComponent<ScreenSpaceFog>();
        if (volume == null || !volume.IsActive() || skyboxMaterial == null || sunLight == null)
        {
            return;
        }
        
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(rotation);
            if (Vector3.Dot(-sunLight.transform.forward, Vector3.up) < 0.0f)
            {
                sunLight.intensity = 0.0f;
                sunLight.enabled = false;
            }
            else
            {
                sunLight.intensity = 1.0f;
                sunLight.enabled = true;
            }
        }

        if (moonLight != null && sunLight != null)
        {
            moonLight.transform.forward = -sunLight.transform.forward;
            if (Vector3.Dot(-moonLight.transform.forward, Vector3.up) < 0.0f)
            {
                moonLight.intensity = 0.0f;
                moonLight.enabled = false;
            }
            else
            {
                moonLight.intensity = 0.2f;
                moonLight.enabled = true;
            }
        }

        volume.sunDirection = -sunLight.transform.forward;
        volume.moonDirection = moonLight == null ? sunLight.transform.forward : -moonLight.transform.forward;
        
        skyboxMaterial.SetFloat(ScatteringId, volume.scattering.value);
        skyboxMaterial.SetFloat(ScatteringRedWaveId, volume.scatteringRedWave.value);
        skyboxMaterial.SetFloat(ScatteringGreenWaveId, volume.scatteringGreenWave.value);
        skyboxMaterial.SetFloat(ScatteringBlueWaveId, volume.scatteringBlueWave.value);
        skyboxMaterial.SetFloat(ScatteringMoonId, volume.scatteringMoon.value);
        skyboxMaterial.SetFloat(DayScatteringFacId, volume.dayScatteringFac.value);
        skyboxMaterial.SetFloat(NightScatteringFacId, volume.nightScatteringFac.value);
        skyboxMaterial.SetFloat(GDayMieId, volume.gDayMie.value);
        skyboxMaterial.SetFloat(GNightMieId, volume.gNightMie.value);
        skyboxMaterial.SetVector(SunDirId, volume.sunDirection);
        skyboxMaterial.SetVector(MoonDirId, volume.moonDirection);
        
        if (autoRotateSun)
        {
            float delta = Time.deltaTime * 60.0f * autoRotateSpeed * (inverse ? -1.0f : 1.0f);
            rotation.x += delta;
            if (rotation.x > 360.0f)
            {
                rotation.x -= 360.0f;
            }
            else if (rotation.x < -360.0f)
            {
                rotation.x += 360.0f;
            }
        }
    }

    private void OnValidate()
    {
        Start();
    }
}

public static class EnvironmentManagerMenus
{
    [MenuItem("GameObject/Manager/Environment Manager", false, 3000)]
    public static void CreateEnvironmentManager(MenuCommand menuCommand)
    {
        GameObject gameObject = new GameObject("Environment Manager");
        EnvironmentManager manager = gameObject.AddComponent<EnvironmentManager>();
        manager.skyboxMaterial = RenderSettings.skybox;
        manager.sunLight = RenderSettings.sun;
    }
}
