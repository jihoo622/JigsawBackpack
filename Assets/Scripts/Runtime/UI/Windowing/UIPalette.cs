using UnityEngine;

// R20: 시안 3b 창 테두리·제목줄·배경·텍스트의 공통 색 정의 에셋.
[CreateAssetMenu(fileName = "UIPalette", menuName = "MyJG/UI/Palette")]
public sealed class UIPalette : ScriptableObject
{
    public enum ColorRole
    {
        Panel,
        Panel2,
        Border,
        BorderBright,
        TitleText,
        Text,
        TextSub,
        GoldAccent,
        Positive,
        Negative
    }

    // concept-3b 기본값: --bg-panel #0f1724, --bg-panel-2 #152033, --border #2a3f5a,
    // --border-bright #3e5b80, --text #f1f5f9, --text-sub #94a3b8, --gold #f0b429,
    // --green #22c55e, --red #ef4444
    [SerializeField] Color panel = new Color(0.0588f, 0.0902f, 0.1412f, 0.96f);
    [SerializeField] Color panel2 = new Color(0.0824f, 0.1255f, 0.2000f, 1f);
    [SerializeField] Color border = new Color(0.1647f, 0.2471f, 0.3529f, 1f);
    [SerializeField] Color borderBright = new Color(0.2431f, 0.3569f, 0.5020f, 1f);
    [SerializeField] Color titleText = new Color(0.9451f, 0.9608f, 0.9765f, 1f);
    [SerializeField] Color text = new Color(0.9451f, 0.9608f, 0.9765f, 1f);
    [SerializeField] Color textSub = new Color(0.5804f, 0.6392f, 0.7216f, 1f);
    [SerializeField] Color goldAccent = new Color(0.9412f, 0.7059f, 0.1608f, 1f);
    [SerializeField] Color positive = new Color(0.1333f, 0.7725f, 0.3686f, 1f);
    [SerializeField] Color negative = new Color(0.9373f, 0.2667f, 0.2667f, 1f);

    public Color Panel => panel;
    public Color Panel2 => panel2;
    public Color Border => border;
    public Color BorderBright => borderBright;
    public Color TitleText => titleText;
    public Color Text => text;
    public Color TextSub => textSub;
    public Color GoldAccent => goldAccent;
    public Color Positive => positive;
    public Color Negative => negative;

    public Color GetColor(ColorRole _role)
    {
        return _role switch
        {
            ColorRole.Panel => panel,
            ColorRole.Panel2 => panel2,
            ColorRole.Border => border,
            ColorRole.BorderBright => borderBright,
            ColorRole.TitleText => titleText,
            ColorRole.Text => text,
            ColorRole.TextSub => textSub,
            ColorRole.GoldAccent => goldAccent,
            ColorRole.Positive => positive,
            ColorRole.Negative => negative,
            _ => text
        };
    }
}
