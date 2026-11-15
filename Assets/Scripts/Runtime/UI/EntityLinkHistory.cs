using System.Collections.Generic;
using UnityEngine;

// 링크로 이동한 기록(씬 로컬). SceneServices.For<EntityLinkHistory>()가 씬 안 호스트에 만들고 씬과 함께 사라진다.
// 라우터가 이동할 때 쌓고, 우클릭 메뉴가 '이전' 항목에 쓴다. 같은 대상이 연속으로 쌓이지 않는다.
public sealed class EntityLinkHistory : MonoBehaviour
{
    const int MAX_ENTRIES = 20;
    readonly List<(EntityLinkKind kind, string id)> entries = new List<(EntityLinkKind, string)>();

    internal void Push(EntityLinkKind _kind, string _id)
    {
        if (entries.Count > 0 && entries[entries.Count - 1].kind == _kind && entries[entries.Count - 1].id == _id)
        {
            return;
        }
        entries.Add((_kind, _id));
        if (entries.Count > MAX_ENTRIES)
        {
            entries.RemoveAt(0);
        }
    }

    // 지금 보고 있는 대상(_kind·_id)과 다른 가장 최근 기록.
    internal bool TryGetPrevious(EntityLinkKind _kind, string _id, out EntityLinkKind _previousKind, out string _previousId)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].kind != _kind || entries[i].id != _id)
            {
                _previousKind = entries[i].kind;
                _previousId = entries[i].id;
                return true;
            }
        }
        _previousKind = EntityLinkKind.City;
        _previousId = null;
        return false;
    }
}
