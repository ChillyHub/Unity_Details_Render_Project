using System.Collections;
using UnityEditor;
using UnityEngine;

public static class TerrainDetailsDataSerializer
{
    private const string Path = "Assets/Resources/";
    private const string Prefix = "DetailsData_";
    
    public static DetailsDataAsset Serializer(Terrain terrain, string terrainName)
    {
        DetailsDataAsset asset = Resources.Load($"{Prefix}{terrainName}") as DetailsDataAsset;

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DetailsDataAsset>();
            AssetDatabase.CreateAsset(asset, $"{Path}{Prefix}{terrainName}.asset");
        }

        if (terrain.isActiveAndEnabled)
        {
            asset.Create(terrain);
        }

        return asset;
    }
    
    public static void Serializer(Terrain terrain, ref DetailsDataAsset asset)
    {
        if (terrain.isActiveAndEnabled)
        {
            asset.Create(terrain);
        }
    }
    
    public static IEnumerator SerializerCoroutine(Terrain terrain, DetailsDataAsset asset)
    {
        if (terrain.isActiveAndEnabled)
        {
            yield return DetailsDataAsset.CreateCoroutine(asset, terrain);
        }

        yield return null;
    }
    

    public static DetailsDataAsset Deserializer(string terrainName)
    {
        return Resources.Load($"{Prefix}{terrainName}") as DetailsDataAsset;
    }
}