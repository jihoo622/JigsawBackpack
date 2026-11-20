using System.Collections.Generic;
using UnityEngine;

// 고유명사·품목 링크 호버 → 기존 아이템/키워드 툴팁 UI(PuzzleItemTooltipView)로 미리보기 표시.
public sealed class EntityPreviewPresenter : MonoBehaviour
{
    [SerializeField] PuzzleItemTooltipView view;
    readonly List<string> lines = new List<string>();
    readonly List<string> statNames = new List<string>();
    readonly List<string> statTexts = new List<string>();
    readonly List<(string label, string value)> statRows = new List<(string label, string value)>();
    PuzzleInventoryService session;

    void OnEnable()
    {
        if (!view)
        {
            view = GetComponent<PuzzleItemTooltipView>();
        }
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        GameEvents.EntityLinkHovered += OnHovered;
        GameEvents.EntityLinkHoverMoved += OnHoverMoved;
        GameEvents.EntityLinkHoverEnded += OnHoverEnded;
        GameEvents.EntityLinkClicked += OnClicked;
        GameEvents.EntityLinkContextRequested += OnClicked;
        if (view)
        {
            view.Close();
        }
    }

    void OnDisable()
    {
        GameEvents.EntityLinkHovered -= OnHovered;
        GameEvents.EntityLinkHoverMoved -= OnHoverMoved;
        GameEvents.EntityLinkHoverEnded -= OnHoverEnded;
        GameEvents.EntityLinkClicked -= OnClicked;
        GameEvents.EntityLinkContextRequested -= OnClicked;
        if (view)
        {
            view.Close();
        }
    }

    void OnHovered(GameEvents.EntityLinkInfo _info)
    {
        if (!view || _info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        if (_info.kind == EntityLinkKind.Item)
        {
            var piece = Presentation.ItemInfoPiece(_info.id);
            if (piece == null)
            {
                view.Close();
                return;
            }
            Presentation.ItemInfoStats(piece, statNames, statTexts);
            var effects = Presentation.PuzzleTooltipPossibleEffects(
                new PuzzleItemDefinition(piece), PuzzleSynergyScope.Train);
            view.ShowPieceInfo(piece, statNames, statTexts, effects.General, effects.Synergies, _info.screenPosition);
            return;
        }

        Presentation.EntityPreview(_info.kind, _info.id, session, out string title, statRows, lines);
        string[] tags = _info.kind == EntityLinkKind.Tag ? new[] { _info.id } : null;
        view.ShowEntityInfo(title, Presentation.EntityPreviewTitleColor(_info.kind, _info.id), lines, _info.screenPosition, tags, statRows,
            _infoHeader: _info.kind == EntityLinkKind.Wagon ? BasicData.Text("presentation.quest.wagonEffectsHeader") : null);
    }

    void OnHoverMoved(GameEvents.EntityLinkInfo _info)
    {
        if (view && view.IsOpen && _info.sceneHandle == gameObject.scene.handle)
        {
            view.SetPointerPosition(_info.screenPosition);
        }
    }

    void OnHoverEnded(int _sceneHandle)
    {
        if (view && _sceneHandle == gameObject.scene.handle)
        {
            view.Close();
        }
    }

    // 클릭·우클릭으로 화면이 바뀌면 툴팁을 닫는다.
    void OnClicked(GameEvents.EntityLinkInfo _info)
    {
        if (view && _info.sceneHandle == gameObject.scene.handle)
        {
            view.Close();
        }
    }
}
