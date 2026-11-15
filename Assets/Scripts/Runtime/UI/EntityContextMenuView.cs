using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 고유명사 우클릭 메뉴. 사전 배치한 버튼 슬롯(행동 2개 + '이전' 1개)을 켜고 끄며, 바깥(화면 전체 막)을 누르면 닫힌다.
public sealed class EntityContextMenuView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button blocker;
    [SerializeField] RectTransform panel;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] Button[] slots;
    [SerializeField] TextMeshProUGUI[] slotLabels;
    [SerializeField] float screenMargin = 16f;

    internal event Action<int> SlotSelected;
    internal event Action Dismissed;
    internal bool IsOpen => root.activeSelf;

    void OnEnable()
    {
        blocker.onClick.AddListener(Dismiss);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].onClick.AddListener(() => SlotSelected?.Invoke(index));
        }
    }

    void OnDisable()
    {
        blocker.onClick.RemoveListener(Dismiss);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].onClick.RemoveAllListeners();
        }
    }

    void Dismiss()
    {
        Dismissed?.Invoke();
    }

    internal void Show(string _title, string[] _labels, Vector2 _screenPosition)
    {
        titleLabel.text = _title;
        titleLabel.color = Presentation.PuzzleHeading;
        for (int i = 0; i < slots.Length; i++)
        {
            bool used = i < _labels.Length && !string.IsNullOrEmpty(_labels[i]);
            slots[i].gameObject.SetActive(used);
            if (used)
            {
                slotLabels[i].text = _labels[i];
            }
        }
        root.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        var parent = (RectTransform)panel.parent;
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _screenPosition, eventCamera, out var local);
        Rect bounds = parent.rect;
        Vector2 size = panel.rect.size;
        panel.pivot = new Vector2(0f, 1f);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, bounds.xMin + screenMargin, bounds.xMax - screenMargin - size.x),
            Mathf.Clamp(local.y, bounds.yMin + screenMargin + size.y, bounds.yMax - screenMargin));
    }

    internal void Hide()
    {
        root.SetActive(false);
    }
}
