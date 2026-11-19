using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S4: 수첩 지도 탭의 목적지 핀(R6). 평면 사각형 + 1px 테두리 + 텍스트(이모지/라운딩 없음).
[DisallowMultipleComponent]
public sealed class MapDestinationPinView : MonoBehaviour
{
    [SerializeField] RectTransform rectTransform;
    [SerializeField] Image background;
    [SerializeField] Outline border;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image leaderLine;

    public RectTransform RectTransform => rectTransform ? rectTransform : (RectTransform)transform;
    public Color BorderColor => border ? border.effectColor : Color.white;

    public void Show(string _text, Color _backgroundColor, Color _borderColor, Color _textColor)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        if (background) background.color = _backgroundColor;
        if (border) border.effectColor = _borderColor;
        if (label)
        {
            label.text = _text;
            label.color = _textColor;
        }
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetPosition(Vector2 _localPos)
    {
        RectTransform.anchoredPosition = _localPos;
    }

    public void SetLeader(float _height, Color _color)
    {
        if (!leaderLine)
        {
            return;
        }
        bool show = _height > 1f;
        if (leaderLine.gameObject.activeSelf != show)
        {
            leaderLine.gameObject.SetActive(show);
        }
        if (show)
        {
            leaderLine.color = _color;
            var rt = leaderLine.rectTransform;
            rt.sizeDelta = new Vector2(1f, _height);
        }
    }
}
