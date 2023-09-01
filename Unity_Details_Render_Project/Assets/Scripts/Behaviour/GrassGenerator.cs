using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class GrassGenerator : MonoBehaviour
{
    public GameObject grassObject;
    public LayerMask grassLayer;
        
    [Range(1.0f, 20.0f)]
    public float density = 10.0f;

    private float _density = 10.0f;
    private Vector3 _position = Vector3.zero;
    private Vector3 _scale = Vector3.one;

    private readonly List<Vector3> _grassPositions = new List<Vector3>();
    private void Start()
    {
        _density = density;
        
        Transform transform = GetComponent<Transform>();

        _position = transform.position;
        _scale = transform.localScale * 4.0f;

        for (float i = -_scale.x; i < _scale.x; i += 1.0f / _density)
        {
            for (float j = -_scale.z; j < _scale.z; j += 1.0f / _density)
            {
                _grassPositions.Add(new Vector3(_position.x + i, _position.y, _position.z + j));
            }
        }

        var volume = VolumeManager.instance.stack.GetComponent<GrassData>();
        volume.grassObject = grassObject;
        volume.grassPositions = _grassPositions;
    }

    private void Update()
    {
        // OnValidate();
    }

    private void OnDestroy()
    {
        _grassPositions.Clear();
    }

    private void OnValidate()
    {
        Transform transform = GetComponent<Transform>();
        
        if (Mathf.Abs(_density - density) > float.Epsilon || 
            Mathf.Abs((_position - transform.position).sqrMagnitude) > float.Epsilon || 
            Mathf.Abs((_scale - transform.position).sqrMagnitude) > float.Epsilon)
        {
            OnDestroy();
            Start();
        }
    }

    private void OnEnable()
    {
        Start();
    }

    private void OnDisable()
    {
        OnDestroy();
    }
}