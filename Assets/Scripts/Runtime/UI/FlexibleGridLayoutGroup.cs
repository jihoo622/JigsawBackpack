using UnityEngine;
using UnityEngine.UI;

// 영역 폭에 맞춰 열 수와 칸 폭을 정하는 그리드. 칸이 최소 폭보다 좁아지지 않는 만큼 열을 두고 남는 폭은 칸이 나눠 가진다.
// 켜진 자식 수가 바뀌면 줄 수가 따라 바뀌고, 스크롤 Content에 두면 ContentSizeFitter가 줄 수만큼 높이를 잡는다.
// 칸 크기·열 제약은 이 컴포넌트가 매 배치마다 계산해 덮어쓰므로 프리팹에서는 아래 필드만 고친다.
[AddComponentMenu("Layout/Flexible Grid Layout Group")]
public sealed class FlexibleGridLayoutGroup : GridLayoutGroup
{
    [SerializeField, Min(1f)] float minCellWidth = 480f;
    [SerializeField, Min(1)] int maxColumns = 2;
    [SerializeField, Min(1f)] float cellHeight = 184f;
    // 켜면 모든 줄이 부모(스크롤 Viewport) 높이 안에 들어갈 때 남는 높이를 줄마다 나눠 칸을 키운다. maxCellHeight가 상한이다.
    [SerializeField] bool fillParentHeight;
    [SerializeField, Min(1f)] float maxCellHeight = 240f;

    public int Columns { get; private set; } = 1;

    public override void CalculateLayoutInputHorizontal()
    {
        UpdateCells();
        base.CalculateLayoutInputHorizontal();
    }

    public override void CalculateLayoutInputVertical()
    {
        UpdateCells();
        base.CalculateLayoutInputVertical();
    }

    public override void SetLayoutHorizontal()
    {
        UpdateCells();
        base.SetLayoutHorizontal();
    }

    void UpdateCells()
    {
        float width = rectTransform.rect.width - padding.horizontal;
        int columns = Mathf.FloorToInt((width + spacing.x) / (minCellWidth + spacing.x));
        columns = Mathf.Clamp(columns, 1, maxColumns);
        float cellWidth = Mathf.Max(1f, (width - spacing.x * (columns - 1)) / columns);
        float height = cellHeight;
        if (fillParentHeight)
        {
            height = FillHeight(columns);
        }
        Columns = columns;
        m_Constraint = Constraint.FixedColumnCount;
        m_ConstraintCount = columns;
        m_CellSize = new Vector2(cellWidth, height);
    }

    float FillHeight(int _columns)
    {
        RectTransform parent = rectTransform.parent as RectTransform;
        int count = ActiveChildCount();
        if (!parent || count == 0)
        {
            return cellHeight;
        }
        int rows = (count + _columns - 1) / _columns;
        float available = parent.rect.height - padding.vertical - spacing.y * (rows - 1);
        float fitted = available / rows;
        if (fitted < cellHeight)
        {
            return cellHeight;
        }
        return Mathf.Min(fitted, Mathf.Max(cellHeight, maxCellHeight));
    }

    int ActiveChildCount()
    {
        int count = 0;
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            Transform child = rectTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy)
            {
                continue;
            }
            ILayoutIgnorer ignorer = child.GetComponent<ILayoutIgnorer>();
            if (ignorer != null && ignorer.ignoreLayout)
            {
                continue;
            }
            count++;
        }
        return count;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        maxCellHeight = Mathf.Max(maxCellHeight, cellHeight);
    }
#endif
}
