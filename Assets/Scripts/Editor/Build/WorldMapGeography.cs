using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;

// 지도 지형 원본(칸 격자). Map Editor가 칠하고 MapPresentationTool이 지형 이미지로 만든다.
public sealed class WorldMapGeography
{
    public const string PATH = "Docs/Management/rail_map_geography_seed_20265146.json";
    public const char SEA = '~';
    public const char NO_REGION = '.';
    // 바다는 육지에서 멀어질수록 얕은 색에서 깊은 색으로 바뀐다(칸 수).
    const float WATER_GRADIENT_CELLS = 18f;

    public static readonly char[] KINDS = { SEA, 'p', 'g', 'h', 'm', 'M', 'r' };

    readonly JObject source;
    public int Width { get; }
    public int Height { get; }
    public float CellWidth { get; }
    public float CellHeight { get; }
    public Vector2 Center { get; }
    public List<string> Regions { get; }
    public char[] Terrain { get; }
    public char[] Region { get; }
    public Vector2 Size => new Vector2(Width * CellWidth, Height * CellHeight);

    WorldMapGeography(JObject _source)
    {
        source = _source;
        var grid = (JObject)_source["grid"];
        Width = (int)grid["width"];
        Height = (int)grid["height"];
        CellWidth = (float)grid["cellWidth"];
        CellHeight = (float)grid["cellHeight"];
        Center = new Vector2((float)grid["centerX"], (float)grid["centerY"]);
        Regions = _source["regions"].Select(_id => (string)_id).ToList();
        Terrain = Rows(_source["terrain"]);
        Region = Rows(_source["region"]);
        if (Width <= 0 || Height <= 0 || CellWidth <= 0f || CellHeight <= 0f)
        {
            throw new InvalidDataException("지형 격자 크기가 올바르지 않습니다.");
        }
        foreach (char kind in Terrain)
        {
            if (Array.IndexOf(KINDS, kind) < 0)
            {
                throw new InvalidDataException("알 수 없는 지형 글자입니다: " + kind);
            }
        }
        foreach (char region in Region)
        {
            if (region != NO_REGION && RegionIndex(region) >= Regions.Count)
            {
                throw new InvalidDataException("regions에 없는 지역 글자입니다: " + region);
            }
        }
    }

    public static WorldMapGeography Parse(string _json)
    {
        return new WorldMapGeography(JObject.Parse(_json));
    }

    public static WorldMapGeography Load()
    {
        return Parse(File.ReadAllText(PATH));
    }

    char[] Rows(JToken _rows)
    {
        var rows = _rows.Select(_row => (string)_row).ToArray();
        if (rows.Length != Height || rows.Any(_row => _row.Length != Width))
        {
            throw new InvalidDataException("지형 행 수·길이가 격자 크기와 맞지 않습니다.");
        }
        return string.Concat(rows).ToCharArray();
    }

    public string ToJson()
    {
        source["regions"] = new JArray(Regions);
        source["terrain"] = new JArray(Enumerable.Range(0, Height).Select(_y => new string(Terrain, _y * Width, Width)));
        source["region"] = new JArray(Enumerable.Range(0, Height).Select(_y => new string(Region, _y * Width, Width)));
        return source.ToString(Formatting.Indented) + "\n";
    }

    public static string KindName(char _kind)
    {
        switch (_kind)
        {
            case SEA:
                return "바다";
            case 'p':
                return "평야";
            case 'g':
                return "초원";
            case 'h':
                return "구릉";
            case 'm':
                return "산";
            case 'M':
                return "고산";
            default:
                return "강";
        }
    }

    public static Color KindColor(char _kind)
    {
        switch (_kind)
        {
            case SEA:
                return Presentation.PuzzleMapTerrainShallow;
            case 'p':
                return Presentation.PuzzleMapTerrainPlain;
            case 'g':
                return Presentation.PuzzleMapTerrainGrass;
            case 'h':
                return Presentation.PuzzleMapTerrainHill;
            case 'm':
                return Presentation.PuzzleMapTerrainMountain;
            case 'M':
                return Presentation.PuzzleMapTerrainPeak;
            default:
                return Presentation.PuzzleMapRiver;
        }
    }

    public static char RegionCode(int _index)
    {
        return (char)('a' + _index);
    }

    static int RegionIndex(char _code)
    {
        return _code - 'a';
    }

    public string RegionId(int _x, int _y)
    {
        char code = Region[_y * Width + _x];
        return code == NO_REGION ? null : Regions[RegionIndex(code)];
    }

    public char RegionCodeFor(string _regionId)
    {
        if (string.IsNullOrEmpty(_regionId))
        {
            return NO_REGION;
        }
        int index = Regions.IndexOf(_regionId);
        if (index < 0)
        {
            Regions.Add(_regionId);
            index = Regions.Count - 1;
        }
        return RegionCode(index);
    }

    // 지도 좌표(위가 +y)의 왼쪽 위 모서리.
    public Vector2 TopLeft => Center + new Vector2(-Size.x * 0.5f, Size.y * 0.5f);

    public Vector2 CellPoint(Vector2 _world)
    {
        Vector2 local = _world - TopLeft;
        return new Vector2(local.x / CellWidth, -local.y / CellHeight);
    }

    // 붓 반경은 칸 단위다. 바뀐 칸이 있으면 true.
    public bool Paint(Vector2 _cell, float _radius, char _kind, bool _region)
    {
        var target = _region ? Region : Terrain;
        bool changed = false;
        int minX = Mathf.Max(0, Mathf.FloorToInt(_cell.x - _radius)), maxX = Mathf.Min(Width - 1, Mathf.FloorToInt(_cell.x + _radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(_cell.y - _radius)), maxY = Mathf.Min(Height - 1, Mathf.FloorToInt(_cell.y + _radius));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if ((new Vector2(x + 0.5f, y + 0.5f) - _cell).sqrMagnitude > _radius * _radius)
                {
                    continue;
                }
                int index = y * Width + x;
                if (target[index] != _kind)
                {
                    target[index] = _kind;
                    changed = true;
                }
            }
        }
        return changed;
    }

    // 텍스처 픽셀(아래쪽 행부터). 칸당 픽셀이 1보다 크면 지역 경계선을 칸 가장자리에 그린다.
    public Color32[] Render(int _pixelsPerCell, float _borderBlend, Func<int, int, Color, Color> _tint = null)
    {
        int width = Width * _pixelsPerCell;
        int height = Height * _pixelsPerCell;
        var water = WaterDistance();
        var cells = new Color[Width * Height];
        Color shallow = Presentation.PuzzleMapTerrainShallow, deep = Presentation.PuzzleMapTerrainDeep;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                Color color = Terrain[index] == SEA
                    ? Color.Lerp(shallow, deep, Mathf.SmoothStep(0f, 1f, water[index] / WATER_GRADIENT_CELLS))
                    : KindColor(Terrain[index]);
                cells[index] = _tint != null ? _tint(x, y, color) : color;
            }
        }
        var pixels = new Color32[width * height];
        Color border = Presentation.PuzzleMapTerrainBorder;
        for (int py = 0; py < height; py++)
        {
            int y = py / _pixelsPerCell;
            int row = height - 1 - py;
            for (int px = 0; px < width; px++)
            {
                int x = px / _pixelsPerCell;
                int index = y * Width + x;
                Color color = cells[index];
                if (_pixelsPerCell > 1 && _borderBlend > 0f)
                {
                    bool edge = px % _pixelsPerCell == 0 && x > 0 && RegionBorder(index, index - 1);
                    edge |= py % _pixelsPerCell == 0 && y > 0 && RegionBorder(index, index - Width);
                    if (edge)
                    {
                        color = Color.Lerp(color, border, _borderBlend);
                    }
                }
                color.a = 1f;
                pixels[row * width + px] = color;
            }
        }
        return pixels;
    }

    // 두 칸이 모두 육지이고 지역이 다르면 경계를 그린다(지역 밖 육지와의 경계 포함).
    bool RegionBorder(int _a, int _b)
    {
        return Terrain[_a] != SEA && Terrain[_b] != SEA && Region[_a] != Region[_b];
    }

    float[] WaterDistance()
    {
        var distance = new float[Width * Height];
        var queue = new Queue<int>();
        for (int i = 0; i < distance.Length; i++)
        {
            if (Terrain[i] == SEA)
            {
                distance[i] = float.MaxValue;
            }
            else
            {
                queue.Enqueue(i);
            }
        }
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % Width, y = index / Width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                    {
                        continue;
                    }
                    int next = ny * Width + nx;
                    float value = distance[index] + 1f;
                    if (value < distance[next])
                    {
                        distance[next] = value;
                        queue.Enqueue(next);
                    }
                }
            }
        }
        return distance;
    }
}
