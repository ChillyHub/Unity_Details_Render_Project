using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class TerrainDetailsManager : MonoBehaviour
{
    [Serializable]
    public class SceneTerrain
    {
        public GameObject terrainObject;
        public bool editModeActive = false;
    }

    public SceneTerrain[] terrains = Array.Empty<SceneTerrain>();

    public bool enableEdit = false;

    [Space][Header("Runtime")][Space(2.0f)]
    public float loadToGPUDistance = 200.0f;

    public bool updateData = true;

    private bool _isUpdating = false;

    private void Start()
    {
        _isUpdating = false;
        
        OnValidate();
    }

    private void Update()
    {
        if (!_isUpdating && enableEdit)
        {
            StartCoroutine(EditUpdateTask());
        }

        //var assets = GlobalTerrainManager.Instance.detailsDataAssets;
        //int count = assets.Length;
        //
        //Vector3 center;
        //float distance = 0.0f;
        //if (enableEdit || Camera.main == null)
        //{
        //    center = Vector3.zero;
        //    distance = float.MaxValue;
        //}
        //else
        //{
        //    center = Camera.main.transform.position;
        //    center.y = 0.0f;
        //    distance = GlobalTerrainManager.Instance.loadToGPUDistance;
        //}
//
        //DoubleBufferManager<DetailsData>.Instance.UpdateData(
        //    (ref DetailsData write) =>
        //    {
        //        write.Clear();
        //    },
        //    (ref DetailsData write, int index) =>
        //    {
        //        if (assets[index] != null)
        //        {
        //            assets[index].ClearAndCopyDataTo(
        //                ref write, center, distance);
        //        }
        //    },
        //    (ref DetailsData dst, ref DetailsData src) =>
        //    {
        //        dst.Add(src);
        //    }, count);
    }

    private void OnValidate()
    {
        int len = terrains.Length;
        GlobalTerrainManager.Instance.terrains = new Terrain[len];
        GlobalTerrainManager.Instance.terrainData = new TerrainData[len];
        GlobalTerrainManager.Instance.detailsDataAssets = new DetailsDataAsset[len];

        for (int i = 0; i < len; i++)
        {
            GameObject terrainObject = terrains[i].terrainObject;
            if (terrainObject != null && terrainObject.TryGetComponent(out Terrain terrain))
            {
                GlobalTerrainManager.Instance.terrains[i] = terrain;
                GlobalTerrainManager.Instance.terrainData[i] = terrain.terrainData;
                GlobalTerrainManager.Instance.detailsDataAssets[i] = 
                    TerrainDetailsDataSerializer.Serializer(terrain, terrainObject.name);
            }
        }

        GlobalTerrainManager.Instance.loadToGPUDistance = loadToGPUDistance;
        GlobalTerrainManager.Instance.enableEdit = enableEdit;
        GlobalTerrainManager.Instance.updateData = updateData;
    }

    private void OnEnable()
    {
        Start();
    }

    private IEnumerator EditUpdateTask()
    {
        _isUpdating = true;
        
        for (int i = 0; i < terrains.Length; i++)
        {
            if (i >= terrains.Length)
            {
                break;
            }

            SceneTerrain sceneTerrain = terrains[i];

            if (sceneTerrain == null || !sceneTerrain.editModeActive || 
                GlobalTerrainManager.Instance.detailsDataAssets[i] == null)
            {
                continue;
            }

            //yield return StartCoroutine(TerrainDetailsDataSerializer.SerializerCoroutine(
            //    GlobalTerrainManager.Instance.terrains[i], 
            //    GlobalTerrainManager.Instance.detailsDataAssets[i]));

            yield return StartCoroutine(SerializerTask(
                GlobalTerrainManager.Instance.terrains[i], 
                GlobalTerrainManager.Instance.detailsDataAssets[i]));
        }

        _isUpdating = false;

        yield return null;
    }

    private IEnumerator SerializerTask(Terrain terrain, DetailsDataAsset asset)
    {
        if (terrain.isActiveAndEnabled)
        {
            yield return StartCoroutine(CreateTask(asset, terrain));
        }

        yield return null;
    }

    private IEnumerator CreateTask(DetailsDataAsset asset, Terrain terrain)
    {
        if (asset.detailsData == null)
        {
            asset.detailsData = new DetailsData();
        }
        if (asset.quadTree == null)
        {
            asset.quadTree = new QuadTree<DetailsData>();
        }

        TerrainData terrainData = terrain.terrainData;

        asset.detailsData.terrain = terrain;
        asset.detailsData.terrainData = terrainData;
        asset.detailsData.transform = terrain.transform;
        asset.detailsData.transPos = asset.detailsData.transform.position;
        asset.detailsData.resolution = terrainData.detailResolution;
        asset.detailsData.detailWidth = terrainData.detailWidth;
        asset.detailsData.detailHeight = terrainData.detailHeight;
        asset.detailsData.terrainWidth = terrainData.size.x;
        asset.detailsData.terrainHeight = terrainData.size.z;
        
        asset.detailsData.prototypes = new DetailPrototype[terrainData.detailPrototypes.Length];
        terrainData.detailPrototypes.CopyTo(asset.detailsData.prototypes, 0);

        // Vector3 center = detailsData.transform.position;
        // center.x += detailsData.terrainWidth * 0.5f;
        // center.z += detailsData.terrainHeight * 0.5f;
        
        Vector3 center = Vector3.zero;
        center.x += asset.detailsData.resolution * 0.5f;
        center.z += asset.detailsData.resolution * 0.5f;

        yield return StartCoroutine(TreeCreateTask(asset.quadTree, asset.detailsData, center));

        asset.detailsData.totalLodCount = 0;
        asset.detailsData.typeInfos = new Vector3Int[asset.detailsData.prototypes.Length];
        asset.detailsData.lodThresholds = new Vector4[asset.detailsData.prototypes.Length];
        for (int i = 0; i < asset.detailsData.prototypes.Length; i++)
        {
            // TODO: Add lod 
            GameObject gameObject = asset.detailsData.prototypes[i].prototype;
            LODGroup lodGroup = gameObject.GetComponent<LODGroup>();
            Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            lodGroup = null;
            if (lodGroup)
            {
                int lodCount = lodGroup.lodCount;
                asset.detailsData.totalLodCount += lodCount;
                Vector3Int info = new Vector3Int(i, lodCount, 0);
                asset.detailsData.typeInfos[i] = info;

                Vector4 lodThreshold = new Vector4(10.0f, 20.0f, 30.0f, float.MaxValue);
                asset.detailsData.lodThresholds[i] = lodThreshold;
            }
            else
            {
                int lodCount = mesh.subMeshCount;
                asset.detailsData.totalLodCount += lodCount;
                Vector3Int info = new Vector3Int(i, lodCount, 0);
                asset.detailsData.typeInfos[i] = info;

                float step = 100.0f / (lodCount + float.Epsilon);
                float x = lodCount > 1 ? step * 1 : float.MaxValue;
                float y = lodCount > 2 ? step * 2 : float.MaxValue;
                float z = lodCount > 3 ? step * 3 : float.MaxValue;
                float w = float.MaxValue;
                Vector4 lodThreshold = new Vector4(x, y, z, w);
                asset.detailsData.lodThresholds[i] = lodThreshold;
            }
        }
    }

    private IEnumerator TreeCreateTask(QuadTree<DetailsData> quadTree, DetailsData data, Vector3 center)
    {
        QuadTree<DetailsData>.ClearData(ref data);
        yield return StartCoroutine(UpdateNodeTask(quadTree, data, 0, 0, center));
    }

    private IEnumerator UpdateNodeTask(QuadTree<DetailsData> quadTree, DetailsData data,
        int nodeIndex, int dataIndex, Vector3 nodeCenter, int depth = 0)
    {
        var childIndex = nodeIndex * 4 + 1;
        var childIndices = new int[4];
        for (var i = 0; i < 4; i++) 
            childIndices[i] = childIndex + i;

        var size = (float)((uint)QuadTree<DetailsData>.MaxSize >> depth);
        var offset = size / 4.0f;
        Vector3[] childCenters =
        {
            nodeCenter + new Vector3(-offset, 0.0f, -offset),
            nodeCenter + new Vector3(offset, 0.0f, -offset),
            nodeCenter + new Vector3(-offset, 0.0f, offset),
            nodeCenter + new Vector3(offset, 0.0f, offset)
        };

        var dataCount = 0;
        if (depth < QuadTree<DetailsData>.TreeDepth)
        {
            for (var i = 0; i < 4; i++)
            {
                if (depth < QuadTree<DetailsData>.TreeDepth - 5)
                {
                    yield return StartCoroutine(UpdateNodeTask(quadTree, data,
                        childIndices[i], dataIndex + dataCount, childCenters[i], depth + 1));
                }
                else
                {
                    quadTree.UpdateNode(ref data, childIndices[i], dataIndex + dataCount, childCenters[i], depth + 1);
                }

                dataCount += quadTree[childIndices[i]].dataCount;
            }
        }
        else if (depth == QuadTree<DetailsData>.TreeDepth)
        {
            dataCount = quadTree.UpdateData(ref data, nodeIndex, nodeCenter, size, dataIndex);
        }
        
        quadTree[nodeIndex].index = nodeIndex;
        quadTree[nodeIndex].init = true;
        quadTree[nodeIndex].center = nodeCenter;
        quadTree[nodeIndex].size = size;
        quadTree[nodeIndex].dataIndex = dataIndex;
        quadTree[nodeIndex].dataCount = dataCount;
    }
}

public static class TerrainDetailsManagerMenus
{
    [MenuItem("GameObject/Manager/Terrain Details Manager", false, 3000)]
    public static void CreateTerrainDetailsManager(MenuCommand menuCommand)
    {
        GameObject gameObject = new GameObject("Terrain Details Manager");
        TerrainDetailsManager manager = gameObject.AddComponent<TerrainDetailsManager>();
    }
}

public class GlobalTerrainManager
{
    private static readonly Lazy<GlobalTerrainManager> Ins =
        new Lazy<GlobalTerrainManager>(() => new GlobalTerrainManager());

    public static GlobalTerrainManager Instance = Ins.Value;
    
    public Terrain[] terrains = Array.Empty<Terrain>();
    public TerrainData[] terrainData = Array.Empty<TerrainData>();
    public DetailsDataAsset[] detailsDataAssets = Array.Empty<DetailsDataAsset>();

    public float loadToGPUDistance;
    public bool enableEdit;
    public bool updateData;
}
