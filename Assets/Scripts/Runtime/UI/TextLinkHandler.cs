using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 고유명사 링크(<link="종류:ID">)가 들어가는 TMP 글자에 붙인다.
// 마우스를 올리면 링크 글자를 강조 색으로 바꾸고 미리보기를 요청하며, 왼쪽 클릭은 이동을 요청한다.
// 우클릭은 PauseMenu의 공통 뒤로가기 입력으로 처리한다.
// 링크가 아닌 곳의 누름·클릭은 부모(요약 띠·의뢰 슬롯 같은 버튼)로 넘겨 기존 버튼 동작을 막지 않는다.
// 화물을 끄는 중에는 반응하지 않는다.
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class TextLinkHandler : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    TextMeshProUGUI text;
    Canvas rootCanvas;
    int hoveredLink = -1;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void OnDisable()
    {
        ClearHover();
    }

    Camera EventCamera()
    {
        if (!rootCanvas)
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            rootCanvas = parentCanvas ? parentCanvas.rootCanvas : null;
        }
        return rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
    }

    // 화면 좌표 아래의 고유명사 링크 번호. 없거나 고유명사 형식이 아니면 -1.
    int LinkAt(Vector2 _screenPosition, out EntityLinkKind _kind, out string _id)
    {
        _kind = EntityLinkKind.City;
        _id = null;
        if (text.havePropertiesChanged)
        {
            text.ForceMeshUpdate();
        }
        if (text.textInfo == null || text.textInfo.linkCount == 0)
        {
            return -1;
        }
        int index = TMP_TextUtilities.FindIntersectingLink(text, _screenPosition, EventCamera());
        if (index < 0 || index >= text.textInfo.linkCount)
        {
            return -1;
        }
        return Presentation.TryParseEntityLink(text.textInfo.linkInfo[index].GetLinkID(), out _kind, out _id) ? index : -1;
    }

    public void OnPointerMove(PointerEventData _eventData)
    {
        if (_eventData.dragging || PuzzleInventoryPresenter.HasActiveDrag)
        {
            ClearHover();
            return;
        }
        int index = LinkAt(_eventData.position, out var kind, out var id);
        if (index == hoveredLink)
        {
            return;
        }
        ClearHover();
        if (index < 0)
        {
            return;
        }
        hoveredLink = index;
        Tint(index, Presentation.EntityLinkHoverColor(kind, id));
        GameEvents.RaiseEntityLinkHovered(new GameEvents.EntityLinkInfo(gameObject.scene.handle, kind, id, _eventData.position));
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        ClearHover();
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (LinkAt(_eventData.position, out _, out _) < 0)
        {
            Forward(_eventData, ExecuteEvents.pointerDownHandler);
        }
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        Forward(_eventData, ExecuteEvents.pointerUpHandler);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (_eventData.dragging || PuzzleInventoryPresenter.HasActiveDrag ||
            _eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        int index = LinkAt(_eventData.position, out var kind, out var id);
        if (index < 0)
        {
            Forward(_eventData, ExecuteEvents.pointerClickHandler);
            return;
        }
        var info = new GameEvents.EntityLinkInfo(gameObject.scene.handle, kind, id, _eventData.position);
        ClearHover();
        GameEvents.RaiseEntityLinkClicked(info);
    }

    void Forward<T>(PointerEventData _eventData, ExecuteEvents.EventFunction<T> _handler) where T : IEventSystemHandler
    {
        if (transform.parent)
        {
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, _eventData, _handler);
        }
    }

    void ClearHover()
    {
        if (hoveredLink < 0)
        {
            return;
        }
        hoveredLink = -1;
        if (text)
        {
            // 원래 색은 글자를 다시 그려 되돌린다.
            text.ForceMeshUpdate();
        }
        GameEvents.RaiseEntityLinkHoverEnded(gameObject.scene.handle);
    }

    void Tint(int _linkIndex, Color32 _color)
    {
        var info = text.textInfo;
        var link = info.linkInfo[_linkIndex];
        for (int i = 0; i < link.linkTextLength; i++)
        {
            int characterIndex = link.linkTextfirstCharacterIndex + i;
            if (characterIndex >= info.characterCount)
            {
                break;
            }
            var character = info.characterInfo[characterIndex];
            if (!character.isVisible)
            {
                continue;
            }
            var colors = info.meshInfo[character.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++)
            {
                colors[character.vertexIndex + v] = _color;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
