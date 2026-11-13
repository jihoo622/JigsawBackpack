using System.IO;
using UnityEditor;
using UnityEngine;

public static class MapPresentationTool
{
    public const string TERRAIN_PATH = "Assets/Art/PuzzleMapTerrain.png";

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
    }
}
