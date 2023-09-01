using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[Serializable]
public class DetailsData : IQuadTreeData
{
    public List<Vector3> positions;
    public List<Vector3> scales;
    public List<float> rotateYs;
    public List<Vector4> colors;
    public List<uint> types;

    public Vector3Int[] typeInfos;
    public Vector4[] lodThresholds;
    public DetailPrototype[] prototypes;

    public Terrain terrain;
    public TerrainData terrainData;
    public Transform transform;
    public Vector3 transPos;
    public int resolution;
    public int detailWidth;
    public int detailHeight;
    public float terrainWidth;
    public float terrainHeight;

    public int totalLodCount;

    public int PositionCount => positions.Count;

    public DetailsData()
    {
        positions = new List<Vector3>();
        scales = new List<Vector3>();
        rotateYs = new List<float>();
        colors = new List<Vector4>();
        types = new List<uint>();

        prototypes = Array.Empty<DetailPrototype>();
        typeInfos = Array.Empty<Vector3Int>();
        lodThresholds = Array.Empty<Vector4>();
    }

    public DetailsData(DetailsData src) : this()
    {
        positions = src.positions;
        scales = src.scales;
        rotateYs = src.rotateYs;
        colors = src.colors;
        types = src.types;

        prototypes = new DetailPrototype[src.prototypes.Length];
        typeInfos = new Vector3Int[src.typeInfos.Length];
        lodThresholds = new Vector4[src.lodThresholds.Length];
        src.prototypes.CopyTo(prototypes, 0);
        src.typeInfos.CopyTo(typeInfos, 0);
        src.lodThresholds.CopyTo(lodThresholds, 0);

        terrain = src.terrain;
        terrainData = src.terrainData;
        transform = src.transform;
        transPos = transform.position;
        resolution = src.resolution;
        detailWidth = src.detailWidth;
        detailHeight = src.detailHeight;
        terrainWidth = src.terrainWidth;
        terrainHeight = src.terrainHeight;

        totalLodCount = src.totalLodCount;
    }

    public DetailsData(DetailsDataAsset src) : this(src.detailsData) {}

    // public void Configure(TerrainData data, Transform trans)
    // {
    //     positions = new List<Vector3>();
    //     scales = new List<Vector3>();
    //     rotateYs = new List<float>();
    //     colors = new List<Vector4>();
    //     types = new List<uint>();
    //     lodThresholds = new List<Vector4>();
    //     
    //     typeInfos = new List<Vector3Int>();
    // }

    public int Update(Vector3 center, float size)
    {
        if (terrainData == null)
        {
            return 0;
        }
        
        float terrainResolutionWidthPerMeter = (float)detailWidth / terrainWidth;
        float terrainResolutionHeightPerMeter = (float)detailHeight / terrainHeight;
        float terrainResolutionWidthScale = 1.0f / terrainResolutionWidthPerMeter;
        float terrainResolutionHeightScale = 1.0f / terrainResolutionHeightPerMeter;

        // Vector3 posOnTerrain = center - transform.position;
        // posOnTerrain.x *= terrainResolutionWidthPerMeter;
        // posOnTerrain.z *= terrainResolutionHeightPerMeter;
        // posOnTerrain.y = 0.0f;

        int indexScale = (int)(resolution / QuadTree<DetailsData>.MaxSize);
        int baseX = (int)Mathf.Floor(center.x - size * 0.5f) * indexScale;
        int baseZ = (int)Mathf.Floor(center.z - size * 0.5f) * indexScale;
        int width = (int)size * indexScale;
        int height = (int)size * indexScale;

        // float baseX = terrainResolutionWidthPerMeter * (posOnTerrain.x - size / 2.0f);
        // float baseZ = terrainResolutionHeightPerMeter * (posOnTerrain.z - size / 2.0f);
        // float width = terrainResolutionWidthPerMeter * size;
        // float height = terrainResolutionHeightPerMeter * size;

        List<Vector3> tempPositions = new List<Vector3>();
        List<Vector3> tempScales = new List<Vector3>();
        List<float> tempRotateYs = new List<float>();
        List<Vector4> tempColors = new List<Vector4>();
        List<uint> tempTypes = new List<uint>();

        List<Vector3Int> tempInfos = new List<Vector3Int>();

        int total = 0;
        for (int i = 0; i < prototypes.Length; i++)
        {
            int[,] layers = terrainData.GetDetailLayer(baseX, baseZ, width, height, i);
            Vector3 position00 = new Vector3(baseX, 0.0f, baseZ);
            position00.x += 0.5f;
            position00.z += 0.5f;

            DetailPrototype prototype = prototypes[i];

            MeshRenderer meshRenderer = prototype.prototype.GetComponent<MeshRenderer>();
            MeshFilter meshFilter = prototype.prototype.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;

            float maxHeight = prototype.maxHeight;
            float minHeight = prototype.minHeight;
            float maxWidth = prototype.maxWidth;
            float minWidth = prototype.minWidth;

            Vector3 maxScale = Vector3.one;
            Vector3 minScale = Vector3.one;
            maxScale.x *= maxWidth;
            maxScale.z *= maxWidth;
            maxScale.y *= maxHeight;
            minScale.x *= minWidth;
            minScale.z *= minWidth;
            minScale.y *= minHeight;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Random.InitState(prototypes[i].noiseSeed + x * width + z);
                    
                    int count = layers[z, x];
                    Vector3 positionWS = position00;
                    positionWS.x += x;
                    positionWS.z += z;
                    positionWS.x *= terrainResolutionWidthScale;
                    positionWS.z *= terrainResolutionHeightScale;
                    positionWS += transform.position;

                    for (int j = 0; j < count; j++)
                    {
                        float offsetX = Random.value * terrainResolutionWidthScale;
                        float offsetZ = Random.value * terrainResolutionHeightScale;

                        float perlinNoise = Mathf.PerlinNoise(offsetX, offsetZ);
                        Vector3 position = positionWS + new Vector3(offsetX, 0.0f, offsetZ);
                        position.y = terrain.SampleHeight(position);
                        Vector3 scale = Vector3.Lerp(minScale, maxScale, perlinNoise);
                        float rotateY = perlinNoise * 2.0f * Mathf.PI;
                        Vector4 color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                        uint type = (uint)i;

                        tempPositions.Add(position);
                        tempScales.Add(scale);
                        tempRotateYs.Add(rotateY);
                        tempColors.Add(color);
                        tempTypes.Add(type);
                    }

                    total += count;
                }
            }
        }

        positions.AddRange(tempPositions);
        scales.AddRange(tempScales);
        rotateYs.AddRange(tempRotateYs);
        colors.AddRange(tempColors);
        types.AddRange(tempTypes);

        return total;
    }

    public int Update(IQuadTreeData srcData, QuadTreeNode srcNode)
    {
        Add((DetailsData)srcData, srcNode.dataIndex, srcNode.dataCount);
        return srcNode.dataCount;
    }

    public void InitData(DetailsDataAsset src)
    {
        terrain = src.detailsData.terrain;
        terrainData = src.detailsData.terrainData;
        transform = src.detailsData.transform;
        transPos = src.detailsData.transPos;
        resolution = src.detailsData.resolution;
        detailWidth = src.detailsData.detailWidth;
        detailHeight = src.detailsData.detailHeight;
        terrainWidth = src.detailsData.terrainWidth;
        terrainHeight = src.detailsData.terrainHeight;

        totalLodCount = src.detailsData.totalLodCount;

        prototypes = new DetailPrototype[src.detailsData.prototypes.Length];
        typeInfos = new Vector3Int[src.detailsData.typeInfos.Length];
        lodThresholds = new Vector4[src.detailsData.lodThresholds.Length];
        src.detailsData.prototypes.CopyTo(prototypes, 0);
        src.detailsData.typeInfos.CopyTo(typeInfos, 0);
        src.detailsData.lodThresholds.CopyTo(lodThresholds, 0);
    }
    
    public void InitData(DetailsData src)
    {
        terrain = src.terrain;
        terrainData = src.terrainData;
        transform = src.transform;
        transPos = src.transPos;
        prototypes = src.prototypes;
        resolution = src.resolution;
        detailWidth = src.detailWidth;
        detailHeight = src.detailHeight;
        terrainWidth = src.terrainWidth;
        terrainHeight = src.terrainHeight;

        totalLodCount = src.totalLodCount;

        prototypes = new DetailPrototype[src.prototypes.Length];
        typeInfos = new Vector3Int[src.typeInfos.Length];
        lodThresholds = new Vector4[src.lodThresholds.Length];
        src.prototypes.CopyTo(prototypes, 0);
        src.typeInfos.CopyTo(typeInfos, 0);
        src.lodThresholds.CopyTo(lodThresholds, 0);
    }

    public void Add(DetailsData src, int start, int count)
    {
        if (src.positions.Count <= start + count)
        {
            return;
        }
        
        positions.AddRange(src.positions.GetRange(start, count));
        scales.AddRange(src.scales.GetRange(start, count));
        rotateYs.AddRange(src.rotateYs.GetRange(start, count));
        colors.AddRange(src.colors.GetRange(start, count));
        types.AddRange(src.types.GetRange(start, count));
    }
    
    public void Add(DetailsData src)
    {
        positions.AddRange(src.positions);
        scales.AddRange(src.scales);
        rotateYs.AddRange(src.rotateYs);
        colors.AddRange(src.colors);
        types.AddRange(src.types);

        DetailPrototype[] tmp1 = new DetailPrototype[prototypes.Length + src.prototypes.Length];
        Vector3Int[] tmp2 = new Vector3Int[prototypes.Length + src.prototypes.Length];
        Vector4[] tmp3 = new Vector4[prototypes.Length + src.prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            tmp1[i] = prototypes[i];
            tmp2[i] = typeInfos[i];
            tmp3[i] = lodThresholds[i];
        }
        for (int i = prototypes.Length; i < tmp1.Length; i++)
        {
            if (i - prototypes.Length < src.prototypes.Length)
            {
                tmp1[i] = src.prototypes[i - prototypes.Length];
            }
            if (i - prototypes.Length < src.typeInfos.Length)
            {
                tmp2[i] = src.typeInfos[i - prototypes.Length];
            }
            if (i - prototypes.Length < src.lodThresholds.Length)
            {
                tmp3[i] = src.lodThresholds[i - prototypes.Length];
            }
        }

        prototypes = tmp1;
        typeInfos = tmp2;
        lodThresholds = tmp3;
    }

    public void Clear(bool clearPrototypes = true)
    {
        positions?.Clear();
        scales?.Clear();
        rotateYs?.Clear();
        colors?.Clear();
        types?.Clear();

        if (clearPrototypes)
        {
            prototypes = Array.Empty<DetailPrototype>();
            typeInfos = Array.Empty<Vector3Int>();
            lodThresholds = Array.Empty<Vector4>();
        }
    }
}

public class DetailsDataAsset : ScriptableObject
{
    public DetailsData detailsData;
    public QuadTree<DetailsData> quadTree;

    public void Create(Terrain terrain)
    {
        detailsData = new DetailsData();
        quadTree = new QuadTree<DetailsData>();

        TerrainData terrainData = terrain.terrainData;

        detailsData.terrain = terrain;
        detailsData.terrainData = terrainData;
        detailsData.transform = terrain.transform;
        detailsData.transPos = detailsData.transform.position;
        detailsData.resolution = terrainData.detailResolution;
        detailsData.detailWidth = terrainData.detailWidth;
        detailsData.detailHeight = terrainData.detailHeight;
        detailsData.terrainWidth = terrainData.size.x;
        detailsData.terrainHeight = terrainData.size.z;

        detailsData.prototypes = new DetailPrototype[terrainData.detailPrototypes.Length];
        terrainData.detailPrototypes.CopyTo(detailsData.prototypes, 0);

        // Vector3 center = detailsData.transform.position;
        // center.x += detailsData.terrainWidth * 0.5f;
        // center.z += detailsData.terrainHeight * 0.5f;
        
        Vector3 center = Vector3.zero;
        center.x += detailsData.resolution * 0.5f;
        center.z += detailsData.resolution * 0.5f;

        quadTree.Create(ref detailsData, center);
        
        detailsData.totalLodCount = 0;
        detailsData.typeInfos = new Vector3Int[detailsData.prototypes.Length];
        detailsData.lodThresholds = new Vector4[detailsData.prototypes.Length];
        for (int i = 0; i < detailsData.prototypes.Length; i++)
        {
            // TODO: Add lod 
            GameObject gameObject = detailsData.prototypes[i].prototype;
            LODGroup lodGroup = gameObject.GetComponent<LODGroup>();
            Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            lodGroup = null;
            if (lodGroup)
            {
                int lodCount = lodGroup.lodCount;
                detailsData.totalLodCount += lodCount;
                Vector3Int info = new Vector3Int(i, lodCount, 0);
                detailsData.typeInfos[i] = info;

                Vector4 lodThreshold = new Vector4(10.0f, 20.0f, 30.0f, float.MaxValue);
                detailsData.lodThresholds[i] = lodThreshold;
            }
            else
            {
                int lodCount = mesh.subMeshCount;
                detailsData.totalLodCount += lodCount;
                Vector3Int info = new Vector3Int(i, lodCount, 0);
                detailsData.typeInfos[i] = info;

                float step = 100.0f / (lodCount + float.Epsilon);
                float x = lodCount > 1 ? step * 1 : float.MaxValue;
                float y = lodCount > 2 ? step * 2 : float.MaxValue;
                float z = lodCount > 3 ? step * 3 : float.MaxValue;
                float w = float.MaxValue;
                Vector4 lodThreshold = new Vector4(x, y, z, w);
                detailsData.lodThresholds[i] = lodThreshold;
            }
        }
    }
    
    public static IEnumerable CreateCoroutine(DetailsDataAsset asset, Terrain terrain)
    {
        asset.detailsData = new DetailsData();
        asset.quadTree = new QuadTree<DetailsData>();

        TerrainData terrainData = terrain.terrainData;

        asset.detailsData.terrain = terrain;
        asset.detailsData.terrainData = terrainData;
        asset.detailsData.transform = terrain.transform;
        asset.detailsData.transPos = asset.detailsData.transform.position;
        asset.detailsData.prototypes = terrainData.detailPrototypes;
        asset.detailsData.resolution = terrainData.detailResolution;
        asset.detailsData.detailWidth = terrainData.detailWidth;
        asset.detailsData.detailHeight = terrainData.detailHeight;
        asset.detailsData.terrainWidth = terrainData.size.x;
        asset.detailsData.terrainHeight = terrainData.size.z;

        // Vector3 center = detailsData.transform.position;
        // center.x += detailsData.terrainWidth * 0.5f;
        // center.z += detailsData.terrainHeight * 0.5f;
        
        Vector3 center = Vector3.zero;
        center.x += asset.detailsData.resolution * 0.5f;
        center.z += asset.detailsData.resolution * 0.5f;

        yield return asset.quadTree.CreateCoroutine(asset.detailsData, center);
        
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

    public void ClearAndCopyDataTo(ref DetailsData destData, Vector3 center, float size)
    {
        destData.Clear();
        destData.InitData(detailsData);

        float width = Mathf.Min(detailsData.terrainWidth, detailsData.terrainHeight);
        Vector3 localPos = (center - detailsData.transPos) / width * QuadTree<DetailsData>.MaxSize;
        float localSize = size / width * QuadTree<DetailsData>.MaxSize;
        quadTree.SearchAndFillQuadData(ref destData, ref detailsData, localPos, localSize);
    }
}
