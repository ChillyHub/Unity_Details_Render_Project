using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassData : VolumeComponent
{
    public GameObject grassObject;
    
    public List<Vector3> grassPositions = new List<Vector3>();
}