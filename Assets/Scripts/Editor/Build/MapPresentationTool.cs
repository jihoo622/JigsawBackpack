using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class MapPresentationTool
{
    const string TERRAIN_PATH = "Assets/Art/PuzzleMapTerrain.png";
    const string GEOGRAPHY_PATH = "Docs/Management/rail_map_geography_seed_20265146.json";

    [MenuItem("Tools/MyJG/Generate Map Terrain Image")]
    public static void GenerateTerrain()
    {
        var data = JObject.Parse(File.ReadAllText(GEOGRAPHY_PATH));
        int width = (int)data["grid"]["width"];
        int height = (int)data["grid"]["height"];
        int pixelsPerTile = BasicData.Int("editor.MapPresentationTool.pixelspertile");
        var texture = new Texture2D(width * pixelsPerTile, height * pixelsPerTile, TextureFormat.RGB24, false);
        var pixels = new Color32[texture.width * texture.height];
        var layers = data["layers"];
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int tileX = x / pixelsPerTile;
                int tileY = y / pixelsPerTile;
                int index = tileY * width + tileX;
                Color color = Presentation.PuzzleMapOcean;
                if ((int)layers["land"][index] == 1)
                {
                    int region = (int)data["regionIndex"][index];
                    Color tint = region < 0 ? Presentation.PuzzleMapLand :
                        Presentation.PuzzleMapLineColor((string)data["regionIndexToId"][region]);
                    color = Color.Lerp(Presentation.PuzzleMapLand, tint, BasicData.Float("editor.MapPresentationTool.generateTerrain"));
                    float elevation = (float)layers["elevationNormalized"][index];
                    color *= BasicData.Float("editor.MapPresentationTool.generateTerrain2") + elevation * BasicData.Float("editor.MapPresentationTool.generateTerrain3");
                    if ((int)layers["river"][index] == 1)
                    {
                        color = Presentation.PuzzleMapRiver;
                    }
                    bool edge = tileX > 0 && (int)data["regionIndex"][index - 1] != region && x % pixelsPerTile == 0;
                    edge |= tileY > 0 && (int)data["regionIndex"][index - width] != region && y % pixelsPerTile == 0;
                    if (edge || (int)layers["coast"][index] == 1)
                    {
                        color = Color.Lerp(color, Presentation.PuzzleMapTerrainBorder, edge ? BasicData.Float("editor.MapPresentationTool.generateTerrain4") : BasicData.Float("editor.MapPresentationTool.generateTerrain5"));
                    }
                }
                color.a = 1f;
                pixels[(texture.height - 1 - y) * texture.width + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(TERRAIN_PATH, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }
}
