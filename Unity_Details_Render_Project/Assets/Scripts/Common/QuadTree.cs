using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IQuadTreeData
{
    public void Clear(bool clearPrototypes = true);
    public int Update(Vector3 center, float size);
    public int Update(IQuadTreeData srcData, QuadTreeNode srcNode);
}

[Serializable]
public struct QuadTreeNode
{
    public int index;
    public bool init;

    public Vector3 center;
    public float size;

    public int dataIndex;
    public int dataCount;
}

[Serializable]
public class QuadTree<T> where T : IQuadTreeData
{
    public static readonly float MaxSize = 1024.0f; // meter
    public static readonly float MinSize = 8.0f;    // meter
    public static readonly int TreeDepth = 7;
    public static readonly int NodeCounts = 0x5555; // 21845;

    [SerializeField]
    private QuadTreeNode[] _nodes = new QuadTreeNode[NodeCounts];

    public QuadTreeNode Root => _nodes[0];

    public ref QuadTreeNode this[int index] => ref _nodes[index];

    public QuadTreeNode GetParentNode(QuadTreeNode node)
    {
        return _nodes[(node.index - 1) / 4];
    }

    public QuadTreeNode[] GetChildNode(QuadTreeNode node)
    {
        var res = new QuadTreeNode[4];
        for (var i = 0; i < 4; i++) 
            res[i] = _nodes[node.index * 4 + 1 + i];

        return res;
    }

    public bool FindDataNode(Vector3 pos, out QuadTreeNode node)
    {
        node = new QuadTreeNode();
        var localPos = pos - Root.center;

        var posX = localPos.x;
        var posZ = localPos.z;
        var posU = posX / MaxSize + 0.5f;
        var posV = posZ / MaxSize + 0.5f;

        if (posU < 0.0f || posU > 1.0f || posV < 0.0f || posV > 1.0f) 
            return false;

        var indexU = (uint)Mathf.Floor(posU * (1 << TreeDepth));
        var indexV = (uint)Mathf.Floor(posV * (1 << TreeDepth));

        uint index = 0;
        uint mul = 1;
        for (var i = 0; i < TreeDepth; i++)
        {
            var bitU = indexU & 0x1;
            var bitV = indexV & 0x1;

            indexU = (indexU >> 1);
            indexV = (indexV >> 1);

            index += mul * (bitV * 2 + bitU);
            mul *= 4;
        }

        index += 0x5555; // 21845

        node = _nodes[index];
        return true;
    }

    public void SearchAndFillQuadData(ref T data, ref T srcData, Vector3 center, float quadSize, int index = 0, int depth = 0)
    {
        float size = ((int)MaxSize) >> depth;
        float halfSize = size * 0.5f;
        float halfHalfSize = halfSize * 0.5f;

        QuadTreeNode currNode = _nodes[index];

        Vector2 searchCenter = new Vector2(center.x, center.z);
        Vector2 nodeCenter = new Vector2(currNode.center.x, currNode.center.z);

        Vector2[] aabb = new Vector2[4]
        {
            searchCenter + new Vector2(-quadSize, -quadSize), // min, min
            searchCenter + new Vector2( quadSize, -quadSize), // max, min
            searchCenter + new Vector2(-quadSize,  quadSize), // min, max
            searchCenter + new Vector2( quadSize,  quadSize)  // max, max
        };
        Vector2[] corner = new Vector2[4]
        {
            nodeCenter + new Vector2(-halfSize, -halfSize),
            nodeCenter + new Vector2( halfSize, -halfSize),
            nodeCenter + new Vector2(-halfSize,  halfSize),
            nodeCenter + new Vector2( halfSize,  halfSize)
        };

        bool insert = false;
        bool include = true;
        Vector2[] ins = new Vector2[4]
        {
            new Vector2(Mathf.Max(aabb[0].x, corner[0].x), Mathf.Max(aabb[0].y, corner[0].y)),
            new Vector2(Mathf.Min(aabb[1].x, corner[1].x), Mathf.Max(aabb[1].y, corner[1].y)),
            new Vector2(Mathf.Max(aabb[2].x, corner[2].x), Mathf.Min(aabb[2].y, corner[2].y)),
            new Vector2(Mathf.Min(aabb[3].x, corner[3].x), Mathf.Min(aabb[3].y, corner[3].y)),
        };
        if (ins[0].x < ins[3].x && ins[0].y < ins[3].y)
        {
            insert = true;
        }
        for (int i = 0; i < 4; i++)
        {
            float absX = Mathf.Abs(corner[i].x - searchCenter.x);
            float absY = Mathf.Abs(corner[i].y - searchCenter.y);
            if (absX > quadSize || absY > quadSize)
            {
                include = false;
            }
        }

        if (!insert)
        {
            return;
        }
        if (include || depth >= TreeDepth)
        {
            data.Update(srcData, currNode);
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            SearchAndFillQuadData(ref data, ref srcData, 
                center, quadSize, index * 4 + 1 + i, depth + 1);
        }
    }
    
    public IEnumerator CreateCoroutine(T data, Vector3 rootCenter)
    {
        ClearData(ref data);
        yield return UpdateNodeCoroutine(data, 0, 0, rootCenter);
    }
    
    public void Clear(ref T data)
    {
        ClearData(ref data);
    }

    public void Create(ref T data, Vector3 rootCenter)
    {
        UpdateNode(ref data, 0, 0, rootCenter);
    }

    public void Update(ref T data, Vector3 rootCenter, bool immediately = false)
    {
        if (rootCenter != Root.center || immediately)
        {
            ClearData(ref data);
            UpdateNode(ref data, 0, 0, rootCenter);
        }
    }

    public void Update(ref T data, Vector3 rootCenter, QuadTree<T> srcTree, T srcData, bool immediately = false)
    {
        if (rootCenter != Root.center || immediately)
        {
            ClearData(ref data);
            UpdateNode(ref data, 0, 0, rootCenter, srcTree, srcData);
        }
    }
    
    private IEnumerator<int> UpdateNodeCoroutine(T data, int nodeIndex, int dataIndex, Vector3 nodeCenter, int depth = 0)
    {
        var childIndex = nodeIndex * 4 + 1;
        var childIndices = new int[4];
        for (var i = 0; i < 4; i++) 
            childIndices[i] = childIndex + i;

        var size = (float)((uint)MaxSize >> depth);
        var offset = size / 4.0f;
        Vector3[] childCenters =
        {
            nodeCenter + new Vector3(-offset, 0.0f, -offset),
            nodeCenter + new Vector3(offset, 0.0f, -offset),
            nodeCenter + new Vector3(-offset, 0.0f, offset),
            nodeCenter + new Vector3(offset, 0.0f, offset)
        };

        var dataCount = 0;
        if (depth < TreeDepth)
        {
            for (var i = 0; i < 4; i++)
            {
                var res = UpdateNodeCoroutine(data, 
                    childIndices[i], dataIndex + dataCount, childCenters[i], depth + 1);

                dataCount += res.Current;
            }
        }
        else if (depth == TreeDepth)
        {
            dataCount = UpdateData(ref data, nodeIndex, nodeCenter, size, dataIndex);
        }
        
        _nodes[nodeIndex].index = nodeIndex;
        _nodes[nodeIndex].init = true;
        _nodes[nodeIndex].center = nodeCenter;
        _nodes[nodeIndex].size = size;
        _nodes[nodeIndex].dataIndex = dataIndex;
        _nodes[nodeIndex].dataCount = dataCount;

        yield return dataCount;
    }

    public int UpdateNode(ref T data, int nodeIndex, int dataIndex, Vector3 nodeCenter, int depth = 0)
    {
        var childIndex = nodeIndex * 4 + 1;
        var childIndices = new int[4];
        for (var i = 0; i < 4; i++) 
            childIndices[i] = childIndex + i;

        var size = (float)((uint)MaxSize >> depth);
        var offset = size / 4.0f;
        Vector3[] childCenters =
        {
            nodeCenter + new Vector3(-offset, 0.0f, -offset),
            nodeCenter + new Vector3(offset, 0.0f, -offset),
            nodeCenter + new Vector3(-offset, 0.0f, offset),
            nodeCenter + new Vector3(offset, 0.0f, offset)
        };

        var dataCount = 0;
        if (depth < TreeDepth)
        {
            for (var i = 0; i < 4; i++)
                dataCount += UpdateNode(ref data, 
                    childIndices[i], dataIndex + dataCount, childCenters[i], depth + 1);
        }
        else if (depth == TreeDepth)
        {
            dataCount = UpdateData(ref data, nodeIndex, nodeCenter, size, dataIndex);
        }

        ref QuadTreeNode node = ref _nodes[nodeIndex];
        node.index = nodeIndex;
        node.init = true;
        node.center = nodeCenter;
        node.size = size;
        node.dataIndex = dataIndex;
        node.dataCount = dataCount;

        return dataCount;
    }

    private int UpdateNode(ref T data, int nodeIndex, int dataIndex, Vector3 nodeCenter,
        QuadTree<T> srcTree, T srcData, int depth = 0)
    {
        var childIndex = nodeIndex * 4 + 1;
        var childIndices = new int[4];
        for (var i = 0; i < 4; i++) 
            childIndices[i] = childIndex + i;

        var size = (float)((uint)MaxSize >> depth);
        var offset = size / 4.0f;
        Vector3[] childCenters =
        {
            nodeCenter + new Vector3(-offset, 0.0f, -offset),
            nodeCenter + new Vector3(offset, 0.0f, -offset),
            nodeCenter + new Vector3(-offset, 0.0f, offset),
            nodeCenter + new Vector3(offset, 0.0f, offset)
        };

        var dataCount = 0;
        if (depth < TreeDepth)
        {
            for (var i = 0; i < 4; i++)
                dataCount += UpdateNode(ref data, 
                    childIndices[i], dataIndex + dataCount, childCenters[i], depth + 1);
        }
        else if (depth == TreeDepth)
        {
            if (FindDataNode(nodeCenter, out var srcNode)) 
                dataCount = UpdateData(ref data, srcData, srcNode);
        }

        ref QuadTreeNode node = ref _nodes[nodeIndex];
        node.index = nodeIndex;
        node.init = true;
        node.center = nodeCenter;
        node.size = size;
        node.dataIndex = dataIndex;
        node.dataCount = dataCount;

        return dataCount;
    }

    public static void ClearData(ref T data)
    {
        data.Clear(false);
    }

    public int UpdateData(ref T data, int nodeIndex, Vector3 center, float size, int dataIndex)
    {
        return data.Update(center, size);
    }

    public int UpdateData(ref T data, T srcData, QuadTreeNode srcNode)
    {
        return data.Update(srcData, srcNode);
    }
}