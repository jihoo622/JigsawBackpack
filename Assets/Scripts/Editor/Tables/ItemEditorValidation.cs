using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class ItemEditorIssue
{
    public GameTableIssue issue;
    public string stablePath;
    public string field;
    public string assetPath;
    public readonly List<string> pieceIds = new List<string>();
    public bool existing;
    public string Identity => stablePath + "\n" + issue.Message + "\n" + issue.PieceId + "\n" + issue.Field;
}

public sealed class ItemEditorDefinitionReview
{
    public readonly List<ItemEditorIssue> issues = new List<ItemEditorIssue>();
    public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
}

public static class ItemEditorValidation
{
    public static ItemEditorDefinitionReview Review(ItemEditorSource _source, ItemEditorDraft _draft)
    {
        var originals = _source.files.Where(_file => _file.tableType != null)
            .ToDictionary(_file => Type.GetType(_file.tableType, true), _file => _file.source);
        if (_source.originalValidation == null)
        {
            _source.originalValidation = Read(originals, _source.icons);
        }
        var result = Read(ItemEditorStore.DraftSources(_source, _draft), _draft.icons);
        var remaining = _source.originalValidation.issues.GroupBy(_issue => _issue.Identity, StringComparer.Ordinal)
            .ToDictionary(_group => _group.Key, _group => _group.Count(), StringComparer.Ordinal);
        foreach (var issue in result.issues)
        {
            if (remaining.TryGetValue(issue.Identity, out int count) && count > 0)
            {
                issue.existing = true;
                remaining[issue.Identity] = count - 1;
            }
        }
        return result;
    }

    static ItemEditorDefinitionReview Read(IReadOnlyDictionary<Type, string> _sources, IReadOnlyList<ItemEditorIcon> _icons)
    {
        var result = new ItemEditorDefinitionReview();
        var issues = TableManager.ValidateSourceIssues(_sources);
        JObject puzzle;
        PuzzlePieceData[] pieces;
        try
        {
            puzzle = JObject.Parse(_sources[typeof(PuzzleTableData)]);
            if (!(puzzle["pieces"] is JArray entries))
            {
                throw new JsonSerializationException("pieces는 품목 배열이어야 합니다.");
            }
            pieces = entries.ToObject<PuzzlePieceData[]>();
        }
        catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
        {
            issues.Add(new GameTableIssue("PuzzleTableData.pieces", "품목 목록을 읽을 수 없습니다: " + exception.Message));
            result.issues.AddRange(issues.Select(_issue => new ItemEditorIssue
            {
                issue = _issue,
                stablePath = _issue.Path
            }));
            return result;
        }
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var piece in pieces.Where(_piece => _piece != null && !string.IsNullOrWhiteSpace(_piece.id)))
        {
            string path = "PuzzleTableData.pieces." + piece.id;
            var matches = _icons.Where(_icon => _icon.pieceId == piece.id).ToArray();
            if (matches.Length != 1)
            {
                issues.Add(new GameTableIssue(path + ".sprite", "Sprite 초안이 누락되었거나 중복되었습니다."));
                continue;
            }
            try
            {
                var sprite = matches[0].Resolve();
                if (!sprite)
                {
                    issues.Add(new GameTableIssue(path + ".sprite", "유효한 Sprite를 선택하세요. 원본 참조가 서로 다를 때에도 명시적인 선택이 필요합니다."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(piece.visualKey))
                {
                    continue;
                }
                ItemEditorIcon.FromSprite(piece.id, sprite);
                if (result.sprites.TryGetValue(piece.visualKey, out var shared) && shared != sprite)
                {
                    string message = "공유 이미지 키 '" + piece.visualKey + "'에 서로 다른 Sprite가 지정되었습니다.";
                    issues.Add(new GameTableIssue(path + ".visualKey", message));
                    issues.Add(new GameTableIssue("PuzzleTableData.pieces." + owners[piece.visualKey] + ".visualKey", message));
                    continue;
                }
                result.sprites[piece.visualKey] = sprite;
                owners[piece.visualKey] = piece.id;
            }
            catch (Exception exception)
            {
                issues.Add(new GameTableIssue(path + ".sprite", exception.Message));
            }
        }
        new GameTableRules(null, issues).ValidateItemVisuals(pieces,
            _key => _key != null && result.sprites.TryGetValue(_key, out var sprite) && sprite, "Item Editor Sprite 초안");
        foreach (var issue in issues)
        {
            result.issues.Add(Locate(issue, _sources, puzzle));
        }
        return result;
    }

    static ItemEditorIssue Locate(GameTableIssue _issue, IReadOnlyDictionary<Type, string> _sources, JObject _puzzle)
    {
        var result = new ItemEditorIssue
        {
            issue = _issue,
            stablePath = _issue.Path,
            field = _issue.Field
        };
        AddPiece(result, _issue.PieceId);
        var type = _sources.Keys.FirstOrDefault(_type => _issue.Path.StartsWith(_type.Name + ".", StringComparison.Ordinal));
        if (type != null)
        {
            JObject table;
            try
            {
                table = JObject.Parse(_sources[type]);
            }
            catch (JsonException)
            {
                return result;
            }
            string localPath = _issue.Path.Substring(type.Name.Length + 1);
            result.stablePath = type.Name + "." + Regex.Replace(localPath, @"\[(\d+)\]", _match =>
            {
                JArray array;
                try
                {
                    array = table.SelectToken(localPath.Substring(0, _match.Index), false) as JArray;
                }
                catch (JsonException)
                {
                    return _match.Value;
                }
                if (!int.TryParse(_match.Groups[1].Value, out int index))
                {
                    return _match.Value;
                }
                var row = array != null && index < array.Count ? array[index] as JObject : null;
                string id = (string)(row?["id"] ?? row?["parentPieceId"]);
                return string.IsNullOrEmpty(id) ? _match.Value : "[" + id + "]";
            });
            var pieceMatch = Regex.Match(localPath, @"^pieces\[(\d+)\](?:\.(.*))?$");
            if (type == typeof(PuzzleTableData) && pieceMatch.Success)
            {
                int index = int.Parse(pieceMatch.Groups[1].Value);
                var pieces = _puzzle["pieces"] as JArray;
                if (pieces != null && index < pieces.Count && pieces[index] is JObject piece)
                {
                    AddPiece(result, (string)piece["id"]);
                    result.field = pieceMatch.Groups[2].Success ? pieceMatch.Groups[2].Value.Split('.', '[')[0] : "id";
                }
            }
            if (type == typeof(PuzzleSynergyTableData) && localPath.StartsWith("animals.breeding", StringComparison.Ordinal))
            {
                result.field = "breeding";
                var match = Regex.Match(localPath, @"^animals\.breeding\[(\d+)\]");
                var breeding = table["animals"]?["breeding"] as JArray;
                if (match.Success && breeding != null && int.TryParse(match.Groups[1].Value, out int index) && index < breeding.Count)
                {
                    AddPiece(result, (string)breeding[index]?["parentPieceId"]);
                    AddPiece(result, (string)breeding[index]?["offspringPieceId"]);
                }
            }
        }
        foreach (var piece in _puzzle["pieces"].OfType<JObject>().OrderByDescending(_piece => ((string)_piece["id"] ?? "").Length))
        {
            string id = (string)piece["id"];
            string prefix = "PuzzleTableData.pieces." + id;
            if (!string.IsNullOrEmpty(id) && (_issue.Path == prefix || _issue.Path.StartsWith(prefix + ".", StringComparison.Ordinal)))
            {
                AddPiece(result, id);
                result.field = _issue.Path.Length == prefix.Length ? "id" : _issue.Path.Substring(prefix.Length + 1).Split('.', '[')[0];
                result.stablePath = "PuzzleTableData.pieces[" + id + "]" + _issue.Path.Substring(prefix.Length);
                break;
            }
            if (!string.IsNullOrEmpty(id) && _issue.Message.Contains("'" + id + "'"))
            {
                AddPiece(result, id);
            }
        }
        return result;
    }

    static void AddPiece(ItemEditorIssue _issue, string _id)
    {
        if (!string.IsNullOrEmpty(_id) && !_issue.pieceIds.Contains(_id))
        {
            _issue.pieceIds.Add(_id);
        }
    }
}
