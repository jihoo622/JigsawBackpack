using TMPro;
using UnityEngine;

// S2: 상단 중앙 상태 알림 띠(660, 90, 460, 34). 내용이 없으면 숨긴다.
[DisallowMultipleComponent]
public sealed class StatusBannerView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] GameObject root;

    bool initializedState;

    void Awake()
    {
        if (!root)
        {
            root = gameObject;
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
        if (statusText)
        {
            statusText.richText = true;
            statusText.text = hasText ? _status : string.Empty;
        }
        var target = root ? root : gameObject;
        if (target.activeSelf != hasText)
        {
            target.SetActive(hasText);
        }
    }
}
