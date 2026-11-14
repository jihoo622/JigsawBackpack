using System.Collections.Generic;
using UnityEngine;

// 고유명사 우클릭 → 행동 메뉴(종류별 화면 + '이전' 기록). 고른 행동은 EntityLinkNavigation으로 보낸다.
public sealed class EntityContextMenuPresenter : MonoBehaviour
{
    [SerializeField] EntityContextMenuView view;
    PuzzleInventoryService session;
    EntityLinkHistory history;
    GameEvents.EntityLinkInfo target;
    readonly List<System.Action> actions = new List<System.Action>();

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        history = SceneServices.For<EntityLinkHistory>(gameObject.scene);
        GameEvents.EntityLinkContextRequested += OnContextRequested;
        GameEvents.EntityLinkClicked += OnClicked;
        view.SlotSelected += OnSlotSelected;
        view.Dismissed += Close;
        view.Hide();
    }

    void OnDisable()
    {
        GameEvents.EntityLinkContextRequested -= OnContextRequested;
        GameEvents.EntityLinkClicked -= OnClicked;
        view.SlotSelected -= OnSlotSelected;
        view.Dismissed -= Close;
    }

    // Esc 처리 순서에서 메뉴가 열려 있으면 먼저 닫는다.
    public bool TryConsumeCancel()
    {
        if (view && view.IsOpen)
        {
            Close();
            return true;
        }
        return false;
    }

    void OnContextRequested(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        target = _info;
        actions.Clear();
        var labels = new List<string>();
        foreach (var action in EntityLinkNavigation.Actions(_info.kind))
        {
            var chosen = action;
            labels.Add(Presentation.EntityLinkActionLabel(chosen));
            actions.Add(() =>
            {
                history.Push(target.kind, target.id);
                EntityLinkNavigation.Navigate(target.sceneHandle, target.kind, target.id, chosen);
            });
        }
        if (history.TryGetPrevious(_info.kind, _info.id, out var previousKind, out var previousId))
        {
            labels.Add(Presentation.EntityLinkBackLabel(Presentation.EntityName(previousKind, previousId, session)));
            actions.Add(() =>
            {
                history.Push(previousKind, previousId);
                EntityLinkNavigation.Navigate(target.sceneHandle, previousKind, previousId, EntityLinkAction.Open);
            });
        }
        view.Show(Presentation.EntityName(_info.kind, _info.id, session), labels.ToArray(), _info.screenPosition);
    }

    void OnClicked(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            Close();
        }
    }

    void OnSlotSelected(int _index)
    {
        if (_index >= 0 && _index < actions.Count)
        {
            var action = actions[_index];
            Close();
            action();
        }
    }

    void Close()
    {
        view.Hide();
    }
}
