using TMPro;
using UnityEngine;

// S5: 행선지 렌즈 부동 창(26, 134, 510, 366). 기본 닫힘, 도구줄(L) 및 화차 창 헤더 버튼으로 토글.
[DisallowMultipleComponent]
public sealed class LensWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] PuzzleRouteLensView routeLensView;

    public FloatingWindow FloatingWindow => floatingWindow;
    public PuzzleRouteLensView RouteLensView => routeLensView;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    void OnEnable()
    {
        if (titleText)
        {
            titleText.text = Presentation.LensWindowTitle;
        }
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }
}
