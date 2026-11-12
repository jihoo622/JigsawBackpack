using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class WorldMapEditorWindow
{
    [SerializeField] bool autoTimeLabels = true;
    bool pendingTimeLabels;
    string timeLabelNotice;

    string LabelGeometryKey()
    {
        return new JArray(Nodes.OrderBy(_node => (string)_node["cityId"], StringComparer.Ordinal)
            .Select(_node => new JArray(_node["cityId"].DeepClone(), _node["x"].DeepClone(), _node["y"].DeepClone(), CityName((string)_node["cityId"]))))
            .ToString(Formatting.None) + new JArray(Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal)
            .Select(_rail => new JArray(_rail["sourceId"].DeepClone(), DisplayPoints(_rail).DeepClone(), _rail["points"].DeepClone()))).ToString(Formatting.None);
    }

    void DrawTimeLabelTools()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("시간 라벨 자동 배치", EditorStyles.boldLabel);
        autoTimeLabels = EditorGUILayout.Toggle("역·노선 수정 시 자동 재배치", autoTimeLabels);
        EditorGUILayout.HelpBox("수동으로 옮긴 라벨은 고정해 유지합니다. 자동 배치는 고정되지 않은 라벨만 옮깁니다. 선택 노선의 자동 배치는 해당 노선 고정을 해제합니다.", MessageType.Info);
        if (GUILayout.Button("전체 시간 라벨 자동 배치"))
        {
            Change("전체 시간 라벨 자동 배치", () => PlaceTimeLabels(null));
            showTimes = true;
        }
        if (GUILayout.Button("고정 해제 후 전체 자동 배치"))
        {
            Change("시간 라벨 전체 고정 해제 및 자동 배치", () =>
            {
                foreach (var rail in Rails)
                {
                    rail["labelManual"] = false;
                }
                PlaceTimeLabels(null);
            });
            showTimes = true;
        }
        if (!string.IsNullOrEmpty(timeLabelNotice))
        {
            EditorGUILayout.HelpBox(timeLabelNotice, MessageType.Info);
        }
    }

    Vector2 TimeLabelSize(JObject _rail)
    {
        string text = (string)_rail["sourceId"];
        try
        {
            text += " · " + Presentation.PuzzleMapTime(PuzzleTimeManager.TravelMinutes(Forward(_rail).ToObject<PuzzleRouteData>(), TableManager.Instance.PuzzleTime));
        }
        catch (OverflowException)
        {
            text += " · 시간 범위 초과";
        }
        var style = new GUIStyle(EditorStyles.label) { fontSize = Presentation.PuzzleMapTimeFontSize };
        return style.CalcSize(new GUIContent(text)) + new Vector2(12f, 8f);
    }

    void PlaceTimeLabels(string _onlyId)
    {
        if (_onlyId != null)
        {
            Rails.Single(_rail => (string)_rail["sourceId"] == _onlyId)["labelManual"] = false;
        }
        var reserved = new List<Rect>();
        float clearance = BasicData.Float("editor.RailMapLayoutTool.labelnodeclearance");
        foreach (var node in Nodes)
        {
            Vector2 point = Position(node);
            reserved.Add(new Rect(point - Vector2.one * clearance, Vector2.one * clearance * 2f));
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = Presentation.PuzzleMapStationFontSize(1f, (string)node["cityId"] == (string)puzzle["startCity"] || (string)node["cityId"] == (string)puzzle["planningTargetCityId"], (int)City((string)node["cityId"])["mapLabelImportance"] >= BasicData.Int("ui.mapLabels.majorThreshold"))
            };
            Vector2 size = style.CalcSize(new GUIContent(CityName((string)node["cityId"]))) + new Vector2(8f, 8f);
            reserved.Add(new Rect(point + new Vector2(-size.x * 0.5f, clearance * 0.5f), size));
        }
        var rails = Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal).ToArray();
        // 미배치 노선의 기존 라벨도 예약해 실패 시 유지한 위치와 새 라벨이 겹치지 않게 한다.
        var labelBoxes = rails.ToDictionary(_rail => (string)_rail["sourceId"], _rail => new Rect(new Vector2((float)_rail["labelX"], (float)_rail["labelY"]) - TimeLabelSize(_rail) * 0.5f, TimeLabelSize(_rail)));
        var failed = new List<string>();
        foreach (var rail in rails.Where(_rail => !((bool?)_rail["labelManual"] ?? false) && (_onlyId == null || (string)_rail["sourceId"] == _onlyId)))
        {
            string id = (string)rail["sourceId"];
            var points = DisplayPoints(rail);
            Vector2 size = TimeLabelSize(rail);
            double total = FareLineLength(rail);
            double traversed = 0;
            Vector2 bestPoint = Vector2.zero;
            double bestScore = double.PositiveInfinity;
            for (int i = 0; i < points.Count - 2; i += 2)
            {
                Vector2 from = ArrayPoint(points, i), to = ArrayPoint(points, i + 2);
                float length = Vector2.Distance(from, to);
                if (length <= 0f)
                {
                    continue;
                }
                Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
                float middle = Mathf.Clamp01((float)((total * 0.5 - traversed) / length));
                foreach (float fraction in new[] { middle, 0.5f, 0.25f, 0.75f })
                {
                    for (int offset = 0; offset < 9; offset++)
                    {
                        float displacement = offset == 0 ? 0f : ((offset + 1) / 2) * (size.y * 0.5f + 8f) * (offset % 2 == 0 ? -1f : 1f);
                        Vector2 point = Vector2.Lerp(from, to, fraction) + normal * displacement;
                        var box = new Rect(point - size * 0.5f, size);
                        if (reserved.Any(_other => _other.Overlaps(box)) || labelBoxes.Any(_other => _other.Key != id && _other.Value.Overlaps(box)) || CrossesOtherRail(box, id, rails))
                        {
                            continue;
                        }
                        double score = Math.Abs(traversed + length * fraction - total * 0.5) + Math.Abs(displacement) * 2;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestPoint = point;
                        }
                    }
                }
                traversed += length;
            }
            if (double.IsPositiveInfinity(bestScore))
            {
                failed.Add(id);
                continue;
            }
            rail["labelX"] = bestPoint.x;
            rail["labelY"] = bestPoint.y;
            labelBoxes[id] = new Rect(bestPoint - size * 0.5f, size);
        }
        timeLabelNotice = failed.Count == 0 ? "시간 라벨 위치를 자동 배치했습니다." : "빈 공간을 찾지 못해 기존 위치를 유지한 노선: " + string.Join(", ", failed);
    }

    static bool CrossesOtherRail(Rect _box, string _id, IEnumerable<JObject> _rails)
    {
        foreach (var rail in _rails)
        {
            if ((string)rail["sourceId"] == _id)
            {
                continue;
            }
            var points = DisplayPoints(rail);
            for (int i = 0; i < points.Count - 2; i += 2)
            {
                Vector2 from = ArrayPoint(points, i), delta = ArrayPoint(points, i + 2) - from;
                float low = 0f, high = 1f;
                for (int axis = 0; axis < 2; axis++)
                {
                    float min = axis == 0 ? _box.xMin : _box.yMin, max = axis == 0 ? _box.xMax : _box.yMax;
                    if (Mathf.Abs(delta[axis]) < 0.0001f)
                    {
                        if (from[axis] < min || from[axis] > max)
                        {
                            high = -1f;
                            break;
                        }
                    }
                    else
                    {
                        float enter = (min - from[axis]) / delta[axis], leave = (max - from[axis]) / delta[axis];
                        low = Mathf.Max(low, Mathf.Min(enter, leave));
                        high = Mathf.Min(high, Mathf.Max(enter, leave));
                    }
                }
                if (low <= high)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
