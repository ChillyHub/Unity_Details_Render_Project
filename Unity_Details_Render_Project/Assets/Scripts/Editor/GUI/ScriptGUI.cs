using UnityEngine;
using Tool;

namespace UnityEditor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(AvatarNormalSmoothTool))]
    public class AvatarNormalSmoothToolGUI : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (GUILayout.Button("Smooth Normal"))
            {
                ((AvatarNormalSmoothTool)target).rebuild = true;
                ((AvatarNormalSmoothTool)target).SmoothNormals();
            }

            if (((AvatarNormalSmoothTool)target).mesh == null)
            {
                EditorGUILayout.HelpBox("Can't find skinned mesh renderer", MessageType.Warning);
            }
        }
    }
}