using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tool
{
    [ExecuteAlways]
    public class SubmitMaterialData : MonoBehaviour
    {
        public Material[] faceMaterials;
        public Material[] hairMaterials;

        public GameObject headRootBone;

        private static readonly int FrontDirectionId = Shader.PropertyToID("_FrontDirection");
        private static readonly int RightDirectionId = Shader.PropertyToID("_RightDirection");

        private void Update()
        {
            if (headRootBone != null)
            {
                Vector4 frontDir = -(Vector4)headRootBone.transform.forward;
                Vector4 rightDir = -(Vector4)headRootBone.transform.right;

                for (int i = 0; i < faceMaterials.Length; i++)
                {
                    faceMaterials[i].SetVector(FrontDirectionId, frontDir);
                    faceMaterials[i].SetVector(RightDirectionId, rightDir);
                }

                for (int i = 0; i < hairMaterials.Length; i++)
                {
                    hairMaterials[i].SetVector(FrontDirectionId, frontDir);
                    hairMaterials[i].SetVector(RightDirectionId, rightDir);
                }
            }
        }
    }
}