using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class WorldMapEditorWindow
{
    bool HasFareSettings => routes["defaultBaseCost"] != null && routes["fareDistancePixelsPerUnit"] != null;

    int CalculatedFareDistance(JObject _rail)
    {
        double unit = (double)routes["fareDistancePixelsPerUnit"];
        if (double.IsNaN(unit) || double.IsInfinity(unit) || unit <= 0)
        {
            throw new InvalidDataException("거리 1당 지도 길이는 유한한 양수여야 합니다.");
        }
        double length = FareLineLength(_rail);
        if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0)
        {
            throw new InvalidDataException("길이가 있는 유한한 표시 노선이 필요합니다.");
        }
        double rounded = Math.Max(1, Math.Round(length / unit, MidpointRounding.AwayFromZero));
        if (rounded > int.MaxValue)
        {
            throw new OverflowException("자동 계산 거리가 정수 범위를 초과합니다.");
        }
        return checked((int)rounded);
    }

    static double FareLineLength(JObject _rail)
    {
        return PuzzleTimeManager.PolylineLength(DisplayPoints(_rail).Select(_value => (float)_value).ToArray());
    }

    void SetFareDistance(JObject _rail)
    {
        int distance = CalculatedFareDistance(_rail);
        _rail["distance"] = distance;
        Forward(_rail)["distance"] = distance;
        Reverse(_rail)["distance"] = distance;
    }

    void SetBaseFare(JObject _rail)
    {
        int cost = (int)routes["defaultBaseCost"];
        if (cost < 0)
        {
            throw new InvalidDataException("기준 기본요금은 0 이상이어야 합니다.");
        }
        _rail["cost"] = cost;
        Forward(_rail)["cost"] = cost;
        Reverse(_rail)["cost"] = cost;
    }

    void DrawFarePreview(JObject _rail)
    {
        if (!HasFareSettings)
        {
            EditorGUILayout.HelpBox("요금 설정 원본이 갱신되었습니다. 필요한 초안을 내보낸 뒤 다시 읽기를 누르세요.", MessageType.Info);
            return;
        }
        try
        {
            int proposed = CalculatedFareDistance(_rail);
            EditorGUILayout.LabelField("표시 선 길이", FareLineLength(_rail).ToString("0.##") + " 지도 단위");
            if (GUILayout.Button("선 길이로 거리 계산 → " + proposed))
            {
                Change("선택 노선 요금 거리 자동 계산", () => SetFareDistance(_rail));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException || exception is OverflowException)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Warning);
        }
    }

    void DrawFareTools()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("노선 요금 일괄 설정", EditorStyles.boldLabel);
        if (!HasFareSettings)
        {
            EditorGUILayout.HelpBox("요금 설정 원본이 갱신되었습니다. 필요한 초안을 내보낸 뒤 다시 읽기를 누르세요.", MessageType.Info);
            return;
        }
        IntField(routes, "defaultBaseCost", "기준 기본요금");
        using (new EditorGUI.DisabledScope((int)routes["defaultBaseCost"] < 0))
        {
            if (GUILayout.Button("전체 왕복 기본요금에 적용"))
            {
                Change("전체 노선 기본요금 적용", () =>
                {
                    foreach (var rail in Rails)
                    {
                        SetBaseFare(rail);
                    }
                });
            }
        }
        double before = (double)routes["fareDistancePixelsPerUnit"];
        double unit = EditorGUILayout.DelayedDoubleField("거리 1당 지도 길이", before);
        if (unit != before)
        {
            Change("요금 거리 환산 기준", () => routes["fareDistancePixelsPerUnit"] = unit);
        }
        EditorGUILayout.HelpBox("현재 표시 선의 모든 구간 길이를 합산합니다. 거리 = 선 길이 ÷ 기준 길이를 반올림한 정수(최소 1). 화면 확대율과 무관하며 왕복에 같은 값을 적용합니다. 아래 적용 버튼을 누를 때 초안을 갱신합니다.", MessageType.Info);
        try
        {
            var proposals = Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal)
                .Select(_rail => new { Rail = _rail, Distance = CalculatedFareDistance(_rail) }).ToArray();
            if (GUILayout.Button("전체 요금용 거리 자동 계산"))
            {
                Change("전체 노선 요금 거리 자동 계산", () =>
                {
                    foreach (var proposal in proposals)
                    {
                        SetFareDistance(proposal.Rail);
                    }
                });
            }
            EditorGUILayout.LabelField("거리 계산 미리보기", EditorStyles.boldLabel);
            foreach (var proposal in proposals)
            {
                var rail = proposal.Rail;
                EditorGUILayout.LabelField((string)rail["sourceId"] + " " + CityName((string)rail["fromCityId"]) + " ↔ " + CityName((string)rail["toCityId"]), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(FareLineLength(rail).ToString("0.##") + " 지도 단위", (int)rail["distance"] + " → " + proposal.Distance);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException || exception is OverflowException)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Warning);
        }
    }
}
