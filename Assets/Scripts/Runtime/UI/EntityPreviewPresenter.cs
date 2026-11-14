using System.Collections.Generic;
using UnityEngine;

// 고유명사 링크 호버 → 미리보기 카드. 내용은 Presentation.EntityPreview가 테이블·세션에서 만든다.
public sealed class EntityPreviewPresenter : MonoBehaviour
{
    [SerializeField] EntityPreviewView view;
    readonly List<string> lines = new List<string>();
    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
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
        Presentation.EntityPreview(_info.kind, _info.id, session, out string title, lines);
        view.Show(title, lines, _info.screenPosition);
    }

    void OnHoverEnded(int _sceneHandle)
    {
        if (_sceneHandle == gameObject.scene.handle)
        {
            view.Hide();
        }
    }

    // 클릭·우클릭으로 화면이 바뀌면 카드를 닫는다.
    void OnClicked(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            view.Hide();
        }
    }
}
