using UnityEngine;

// 고유명사 링크 클릭을 받아 대표 화면으로 보내고 이동 기록을 쌓는다. GameCanvas 루트에 둔다.
public sealed class EntityLinkRouter : MonoBehaviour
{
    EntityLinkHistory history;

    void OnEnable()
    {
        history = SceneServices.For<EntityLinkHistory>(gameObject.scene);
        GameEvents.EntityLinkClicked += OnClicked;
    }

    void OnDisable()
    {
        GameEvents.EntityLinkClicked -= OnClicked;
    }

    void OnClicked(GameEvents.EntityLinkInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        history.Push(_info.kind, _info.id);
        EntityLinkNavigation.Navigate(_info.sceneHandle, _info.kind, _info.id, EntityLinkAction.Open);
    }
}
