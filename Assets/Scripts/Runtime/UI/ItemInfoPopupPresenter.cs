using System.Collections.Generic;
using UnityEngine;

// 품목 링크 호버 → 아이템 정보 팝업. 내용은 Presentation.ItemInfo*가 테이블에서 만든다.
public sealed class ItemInfoPopupPresenter : MonoBehaviour
{
    [SerializeField] ItemInfoPopupView view;
    readonly List<string> statNames = new List<string>();
    readonly List<string> statTexts = new List<string>();
    readonly List<string> synergies = new List<string>();

    void OnEnable()
    {
        GameEvents.EntityLinkHovered += OnHovered;
        GameEvents.EntityLinkHoverEnded += OnHoverEnded;
        GameEvents.EntityLinkClicked += OnClicked;
        GameEvents.EntityLinkContextRequested += OnClicked;
        view.Hide();
    }

    void OnDisable()
    {
        GameEvents.EntityLinkHovered -= OnHovered;
        GameEvents.EntityLinkHoverEnded -= OnHoverEnded;
        GameEvents.EntityLinkClicked -= OnClicked;
        GameEvents.EntityLinkContextRequested -= OnClicked;
    }

    void OnHovered(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        var piece = _info.kind == EntityLinkKind.Item ? Presentation.ItemInfoPiece(_info.id) : null;
        if (piece == null)
        {
            view.Hide();
            return;
        }
        Presentation.ItemInfoStats(piece, statNames, statTexts);
        Presentation.ItemInfoSynergies(piece, synergies);
        view.Show(piece, statNames, statTexts, synergies, _info.screenPosition);
    }

    void OnHoverEnded(int _sceneHandle)
    {
        if (_sceneHandle == gameObject.scene.handle)
        {
            view.Hide();
        }
    }

    // 클릭·우클릭으로 화면이 바뀌면 팝업을 닫는다.
    void OnClicked(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            view.Hide();
        }
    }
}
