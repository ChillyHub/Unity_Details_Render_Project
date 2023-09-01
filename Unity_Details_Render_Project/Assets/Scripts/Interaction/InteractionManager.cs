using System;
using UnityEditor;
using UnityEngine;

public class InteractionDataManager
{
    private static readonly Lazy<InteractionDataManager> Ins =
        new Lazy<InteractionDataManager>(() => new InteractionDataManager());

    public static InteractionDataManager Instance => Ins.Value;
    
    public RecordCameraSetting[] RecordCameraSettings { get; set; }
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class InteractionManager : MonoBehaviour
{
    public RecordCameraSetting[] recordCameraSettings;

    private void Update()
    {
        foreach (var recordCameraSetting in recordCameraSettings)
        {
            recordCameraSetting.Update();
        }
        
        InteractionDataManager.Instance.RecordCameraSettings = recordCameraSettings;
    }
}

public static class InteractionManagerMenus
{
    [MenuItem("GameObject/Manager/Interaction Manager", false, 3000)]
    public static void CreateInteractionManager(MenuCommand menuCommand)
    {
        GameObject gameObject = new GameObject("Interaction Manager");
        InteractionManager manager = gameObject.AddComponent<InteractionManager>();
        manager.recordCameraSettings = new RecordCameraSetting[1];
        manager.recordCameraSettings[0] = new RecordCameraSetting();
        manager.recordCameraSettings[0].planarSDFSettings = new PlanarSDFSetting[1];
        manager.recordCameraSettings[0].planarSDFSettings[0] = new PlanarSDFSetting();
    }
}
