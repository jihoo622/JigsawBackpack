using System;

// 링크로 갈 수 있는 화면(행동). 기본(Open)은 종류별 대표 화면이다.
public enum EntityLinkAction
{
    Open,
    Map,
    Synergy,
    Rumors,
    Proficiency,
    Quests
}

// 고유명사 링크의 이동 규칙. 상태 없이 GameEvents 요청만 보낸다(라우터·우클릭 메뉴가 같이 쓴다).
public static class EntityLinkNavigation
{
    // 종류별 대표 화면: 도시·지역 → 지도, 품목·태그 → 수첩 상성표, 의뢰 → 수첩 의뢰.
    public static EntityLinkAction DefaultAction(EntityLinkKind _kind)
    {
        switch (_kind)
        {
            case EntityLinkKind.City:
            case EntityLinkKind.Region:
                return EntityLinkAction.Map;
            case EntityLinkKind.Quest:
                return EntityLinkAction.Quests;
            default:
                return EntityLinkAction.Synergy;
        }
    }

    // 우클릭 메뉴에 보일 행동(대표 화면 먼저).
    public static EntityLinkAction[] Actions(EntityLinkKind _kind)
    {
        switch (_kind)
        {
            case EntityLinkKind.City:
                return new[] { EntityLinkAction.Map, EntityLinkAction.Rumors };
            case EntityLinkKind.Region:
                return new[] { EntityLinkAction.Map };
            case EntityLinkKind.Tag:
                return new[] { EntityLinkAction.Synergy, EntityLinkAction.Proficiency };
            case EntityLinkKind.Quest:
                return new[] { EntityLinkAction.Quests };
            default:
                return new[] { EntityLinkAction.Synergy };
        }
    }

    public static void Navigate(int _sceneHandle, EntityLinkKind _kind, string _id, EntityLinkAction _action)
    {
        if (_action == EntityLinkAction.Open)
        {
            _action = DefaultAction(_kind);
        }
        switch (_action)
        {
            case EntityLinkAction.Map:
                GameEvents.RaiseMapFocusRequested(new GameEvents.MapFocusInfo(_sceneHandle,
                    _kind == EntityLinkKind.City ? _id : null, _kind == EntityLinkKind.Region ? _id : null));
                break;
            case EntityLinkAction.Synergy:
                GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Synergy);
                GameEvents.RaiseSynergyHighlightRequested(new GameEvents.SynergyHighlightInfo(_sceneHandle,
                    HighlightTags(_kind, _id), _kind == EntityLinkKind.Item ? _id : null));
                break;
            case EntityLinkAction.Rumors:
                GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Rumors);
                break;
            case EntityLinkAction.Proficiency:
                GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Proficiency);
                break;
            case EntityLinkAction.Quests:
                GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Quests);
                break;
        }
    }

    static string[] HighlightTags(EntityLinkKind _kind, string _id)
    {
        if (_kind == EntityLinkKind.Tag)
        {
            return new[] { _id };
        }
        if (_kind == EntityLinkKind.Item)
        {
            foreach (var piece in TableManager.Instance.Pieces)
            {
                if (piece.id == _id)
                {
                    return piece.tags ?? Array.Empty<string>();
                }
            }
        }
        return Array.Empty<string>();
    }
}
