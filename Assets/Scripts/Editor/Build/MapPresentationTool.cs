using System.IO;
using UnityEditor;
using UnityEngine;

public static class MapPresentationTool
{
    public const string TERRAIN_PATH = "Assets/Art/PuzzleMapTerrain.png";
    // 전장의 안개용 고지도. 칸당 1픽셀이며 게임이 픽셀을 읽어 안개 텍스처를 만든다(읽기 허용·점 필터·무압축).
    public const string OLD_MAP_PATH = "Assets/Art/PuzzleMapOldTerrain.png";

    [MenuItem("Tools/MyJG/Generate Map Terrain Image")]
    public static void GenerateTerrain()
    {
        WriteTerrain(WorldMapGeography.Load());
    }

    public static void WriteTerrain(WorldMapGeography _geography)
    {
        int pixelsPerCell = BasicData.Int("editor.MapPresentationTool.pixelspertile");
        var texture = new Texture2D(_geography.Width * pixelsPerCell, _geography.Height * pixelsPerCell, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels32(_geography.Render(pixelsPerCell, BasicData.Float("editor.MapPresentationTool.generateTerrain4")));
            texture.Apply();
            File.WriteAllBytes(TERRAIN_PATH, texture.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
        WriteOldMap(_geography);
    }

    [MenuItem("Tools/MyJG/Generate Map Old Terrain Image")]
    public static void GenerateOldMap()
    {
        WriteOldMap(WorldMapGeography.Load());
    }

    public static void WriteOldMap(WorldMapGeography _geography)
    {
        var texture = new Texture2D(_geography.Width, _geography.Height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels32(_geography.RenderOldMap(Presentation.PuzzleMapOldWater, Presentation.PuzzleMapOldLand,
                Presentation.PuzzleMapOldHigh, Presentation.PuzzleMapOldBurn));
            texture.Apply();
            File.WriteAllBytes(OLD_MAP_PATH, texture.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
        // 생성한 이미지를 바로 가져와 안개가 픽셀을 읽을 수 있게 가져오기 설정을 맞춘다.
        AssetDatabase.ImportAsset(OLD_MAP_PATH);
        if (AssetImporter.GetAtPath(OLD_MAP_PATH) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }
    }
}
