using TMPro;
using UnityEngine;

// S2: 상단 중앙 상태 알림 띠(660, 90, 460, 34). 내용이 없으면 숨긴다.
// 릴레이아웃 ON: 16pt 한 줄을 기본으로 하고, 두 줄이면 띠 높이를 위쪽 기준으로 키운다.
// 그래도 넘치면 12~16pt 자동 크기로 줄이고, 마지막까지 넘치면 말줄임표로 자른다.
[DisallowMultipleComponent]
public sealed class StatusBannerView : MonoBehaviour
{
    const float MaxFontSize = 16f;
    const float MinFontSize = 12f;
    const float MaxBannerHeight = 56f;
    const float HeightPadding = 8f;

    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] GameObject root;

    bool initializedState;
    float baseHeight;

    void Awake()
    {
        if (!root)
        {
            root = gameObject;
        }
        var rt = transform as RectTransform;
        if (rt)
        {
            baseHeight = rt.sizeDelta.y;
        }
        if (!initializedState)
        {
            root.SetActive(false);
        }
    }

    public void ShowStatus(string _status)
    {
        initializedState = true;
        bool hasText = !string.IsNullOrEmpty(_status);
        var target = root ? root : gameObject;
        if (target.activeSelf != hasText)
        {
            target.SetActive(hasText);
        }
        if (!statusText)
        {
            return;
        }
        statusText.richText = true;
        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        statusText.text = hasText ? _status : string.Empty;
        // 활성화 후에 측정해야 TMP가 현재 폭 기준으로 줄 수를 계산한다.
        if (hasText)
        {
            FitText();
        }
    }

    // 텍스트가 띠 안에 들어가도록 글자 크기와 띠 높이를 맞춘다.
    void FitText()
    {
        if (!RelayoutSettings.Enabled)
        {
            // 구형 레이아웃: 기존 자동 크기(10~14pt)와 기본 높이를 그대로 쓴다.
            SetBannerHeight(baseHeight);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 10f;
            statusText.fontSizeMax = 14f;
            return;
        }

        // 1단계: 16pt 고정으로 측정해 한 줄이면 그대로 둔다.
        statusText.enableAutoSizing = false;
        statusText.fontSize = MaxFontSize;
        SetBannerHeight(baseHeight);
        statusText.ForceMeshUpdate(true);
        if (statusText.textInfo.lineCount <= 1)
        {
            return;
        }

        // 2단계: 두 줄 이상이면 띠 높이를 글자 높이에 맞춰 위쪽 기준으로 키운다.
        SetBannerHeight(Mathf.Clamp(statusText.preferredHeight + HeightPadding, baseHeight, MaxBannerHeight));
        statusText.ForceMeshUpdate(true);
        if (!statusText.isTextOverflowing)
        {
            return;
        }

        // 3단계: 그래도 넘치면 12~16pt 자동 크기로 줄인다. 마지막까지 넘치면 말줄임표가 붙는다.
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = MinFontSize;
        statusText.fontSizeMax = MaxFontSize;
        statusText.ForceMeshUpdate(true);
    }

    void SetBannerHeight(float _height)
    {
        var rt = transform as RectTransform;
        if (!rt || Mathf.Approximately(rt.sizeDelta.y, _height))
        {
            return;
        }
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, _height);
    }
}
