using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 역 ID 체계: city.<역 이름 로마자>. 지역을 바꿔도 ID는 그대로 둔다.
// ID를 바꾸면 초안의 모든 참조(역·노선·레이아웃·마을 행동·거래)를 함께 바꾸고, 저장할 때 지도 프리팹 노드도 다시 만들지 않고 이름만 바꾼다.
public sealed partial class WorldMapEditorWindow
{
    const string CITY_PREFIX = "city.", EDITOR_SOURCE = "editor.";
    static readonly Regex CITY_ID = new Regex("^city\\.[a-z][a-z0-9]*$");
    static readonly string[] INITIALS = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
    static readonly string[] MEDIALS = { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i" };
    static readonly string[] FINALS = { "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t" };
    const int HANGUL_FIRST = 0xAC00, HANGUL_LAST = 0xD7A3, MEDIAL_EU = 18, INITIAL_NONE = 11, INITIAL_R = 5, FINAL_L = 8;
    string renameId, renameFor;

    // 외래어 지명이 많아 받침 없는 '으'(브·크·트)는 모음을 빼고 자음만 쓴다. 예: 리오브루크 → riobruk.
    static string Romanize(string _name)
    {
        var text = new StringBuilder();
        bool lastFinalL = false;
        foreach (char letter in _name)
        {
            if (letter >= HANGUL_FIRST && letter <= HANGUL_LAST)
            {
                int code = letter - HANGUL_FIRST;
                int initial = code / 588, medial = code % 588 / 28, final = code % 28;
                text.Append(lastFinalL && initial == INITIAL_R ? "l" : INITIALS[initial]);
                if (medial != MEDIAL_EU || final != 0 || initial == INITIAL_NONE)
                {
                    text.Append(MEDIALS[medial]);
                }
                text.Append(FINALS[final]);
                lastFinalL = final == FINAL_L;
            }
            else
            {
                if (char.IsLetterOrDigit(letter) && letter < 128)
                {
                    text.Append(char.ToLowerInvariant(letter));
                }
                lastFinalL = false;
            }
        }
        return text.ToString();
    }

    string SuggestCityId(string _name)
    {
        string body = Romanize(_name ?? "");
        if (body.Length == 0 || !char.IsLetter(body[0]))
        {
            body = "station" + body;
        }
        string id = CITY_PREFIX + body;
        for (int i = 2; Cities.Any(_city => (string)_city["id"] == id); i++)
        {
            id = CITY_PREFIX + body + i;
        }
        return id;
    }

    void DrawStationId(JObject _city)
    {
        string id = (string)_city["id"];
        if (renameFor != id)
        {
            renameFor = id;
            renameId = id;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            renameId = EditorGUILayout.TextField("역 ID", renameId);
            if (GUILayout.Button("이름으로", GUILayout.Width(60f)))
            {
                renameId = SuggestCityId((string)_city["displayName"]);
                GUI.FocusControl(null);
            }
        }
        using (new EditorGUI.DisabledScope(renameId == id))
        {
            if (GUILayout.Button("ID 바꾸기 (모든 참조 함께)"))
            {
                string next = renameId;
                Change("역 ID 바꾸기", () => RenameStation(id, next));
                GUIUtility.ExitGUI();
            }
        }
        if (!CITY_ID.IsMatch(id))
        {
            EditorGUILayout.LabelField("이전 체계의 ID입니다. 새 체계는 city.<역 이름 로마자>입니다(예: city.riobruk).", EditorStyles.wordWrappedMiniLabel);
        }
    }

    void RenameStation(string _from, string _to)
    {
        _to = (_to ?? "").Trim();
        if (!CITY_ID.IsMatch(_to))
        {
            throw new InvalidDataException("역 ID는 city.<영문 소문자·숫자> 형식이어야 합니다(예: city.riobruk).");
        }
        if (Cities.Any(_city => (string)_city["id"] == _to))
        {
            throw new InvalidDataException("이미 있는 역 ID입니다: " + _to);
        }
        var city = City(_from);
        var node = Node(_from);
        city["id"] = _to;
        RenamePlacements(city);
        node["cityId"] = _to;
        // 원본 ID는 저장된 역의 이름 변경을 찾는 기준이므로, 아직 저장하지 않은 에디터 추가 역만 따라 바꾼다.
        string source = (string)node["sourceId"];
        if (source == EDITOR_SOURCE + _from && !JObject.Parse(originalLayout)["nodes"].Any(_node => (string)_node["sourceId"] == source))
        {
            node["sourceId"] = EDITOR_SOURCE + _to;
        }
        foreach (var row in Rails.Concat(Routes))
        {
            ReplaceValue(row, "fromCityId", _from, _to);
            ReplaceValue(row, "toCityId", _from, _to);
        }
        foreach (var root in new[] { puzzle, layout })
        {
            ReplaceValue(root, "startCity", _from, _to);
            ReplaceValue(root, "planningTargetCityId", _from, _to);
            ReplaceValue(root, "debtCityId", _from, _to);
        }
        // 서식을 유지하려고 따옴표로 감싼 값만 글자 그대로 바꾼다. 역 ID는 이 테이블에서 문자열 값으로만 쓰인다.
        draft.cityActions = draft.cityActions.Replace("\"" + _from + "\"", "\"" + _to + "\"");
        draft.trade = draft.trade.Replace("\"" + _from + "\"", "\"" + _to + "\"");
        selectedId = selectedId == _from ? _to : selectedId;
        newFrom = newFrom == _from ? _to : newFrom;
        newTo = newTo == _from ? _to : newTo;
        renameFor = null;
    }

    static void ReplaceValue(JObject _row, string _key, string _from, string _to)
    {
        if ((string)_row[_key] == _from)
        {
            _row[_key] = _to;
        }
    }

    // 저장된 레이아웃과 초안을 레이아웃 노드의 원본 ID(sourceId)로 맞춰 바뀐 역 ID를 찾는다.
    Dictionary<string, string> RenamedCities()
    {
        var saved = JObject.Parse(originalLayout)["nodes"].OfType<JObject>()
            .GroupBy(_node => (string)_node["sourceId"]).ToDictionary(_group => _group.Key, _group => (string)_group.First()["cityId"]);
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            string source = (string)node["sourceId"];
            if (source != null && saved.TryGetValue(source, out var before) && before != (string)node["cityId"])
            {
                renamed[before] = (string)node["cityId"];
            }
        }
        return renamed;
    }
}
