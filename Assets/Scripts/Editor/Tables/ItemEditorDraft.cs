using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ItemEditorDraft : ScriptableObject
{
    public string puzzle;
    public string synergy;
    public string effectMigrationBaseline;
    public string effectMigrationOriginal;
    public List<ItemEditorIcon> icons = new List<ItemEditorIcon>();
}

[Serializable]
public sealed class ItemEditorIcon
{
    public string pieceId;
    public string spriteGuid;
    public long spriteLocalId;

    public Sprite Resolve()
    {
        string path = AssetDatabasePath();
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite &&
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId) &&
                guid == spriteGuid && localId == spriteLocalId)
            {
                return sprite;
            }
        }
        return null;
    }

    public string AssetDatabasePath()
    {
        return string.IsNullOrEmpty(spriteGuid) ? null : UnityEditor.AssetDatabase.GUIDToAssetPath(spriteGuid);
    }

    public static ItemEditorIcon FromSprite(string _pieceId, Sprite _sprite)
    {
        var icon = new ItemEditorIcon { pieceId = _pieceId };
        if (!_sprite)
        {
            return icon;
        }
        string path = UnityEditor.AssetDatabase.GetAssetPath(_sprite);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_sprite, out string guid, out long localId))
        {
            throw new InvalidOperationException("프로젝트 Assets 안에 저장된 Sprite를 선택하세요.");
        }
        icon.spriteGuid = guid;
        icon.spriteLocalId = localId;
        return icon;
    }
}
