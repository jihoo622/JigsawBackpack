using UnityEngine;

// 개편 UI 배치 기준값. 창 틀·여백·간격·글자 크기를 한곳에서 정하고, 에디터 빌더가 이 값으로 창을 만든다.
// 값을 바꾸면 빌더를 다시 실행해야 프리팹에 반영된다(런타임에는 읽지 않는다).
[CreateAssetMenu(menuName = "MyJG/UI/Layout Tokens", fileName = "UILayoutTokens")]
public sealed class UILayoutTokens : ScriptableObject
{
    [Header("창 틀")]
    [Tooltip("제목줄 높이(px)")]
    public float titleBarHeight = 30f;
    [Tooltip("창 테두리 두께(px)")]
    public float borderWidth = 2f;
    [Tooltip("닫기 버튼 한 변(px)")]
    public float closeButtonSize = 24f;
    [Tooltip("닫기 버튼과 창 오른쪽 사이(px)")]
    public float closeButtonInset = 4f;
    [Tooltip("제목 글자 왼쪽 여백(px)")]
    public float titleInset = 12f;

    [Header("여백·간격")]
    [Tooltip("창 가장자리와 내용 사이 최소 여백(px). 배치 검사 기준이기도 하다.")]
    public float windowPadding = 8f;
    [Tooltip("내용 요소 사이 기본 간격(px)")]
    public float gap = 8f;
    [Tooltip("작은 간격(px): 같은 묶음 안 요소 사이")]
    public float gapSmall = 4f;

    [Header("버튼")]
    public float buttonHeight = 28f;
    public float buttonHeightSmall = 22f;

    [Header("글자 크기(px)")]
    public float fontTitle = 16f;
    public float fontBody = 13f;
    public float fontSmall = 11f;
    public float fontNumber = 20f;
    public float fontButton = 13f;

    [Tooltip("글자 상자 높이 = 글자 크기 × 이 값. 한글 위·아래 여유를 포함한다.")]
    public float lineHeightRatio = 1.5f;

    public float LineHeight(float _fontSize) => Mathf.Ceil(_fontSize * lineHeightRatio);

    // 내용 영역의 위쪽 시작(제목줄 아래 여백 포함)
    public float ContentTop => titleBarHeight + windowPadding;
}
