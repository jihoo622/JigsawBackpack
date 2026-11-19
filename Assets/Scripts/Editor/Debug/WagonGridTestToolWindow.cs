using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 10x10 그리드에서 클릭·드래그로 칸을 선택해 화차 인벤토리의 기본 그리드 크기와 모양을 테스트·적용하는 에디터 도구.
/// </summary>
public sealed class WagonGridTestToolWindow : EditorWindow
{
    public const string MENU = "Tools/MyJG/Wagon Grid Test Tool";
    const int GRID_SIZE = 10;
    const float CELL_SIZE = 38f;
    const float CELL_GAP = 3f;
    static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

    enum TargetKind
    {
        StartingTrain,
        DefaultTrain,
        AllWagonProducts,
        SingleWagonProduct
    }

    sealed class TargetEntry
    {
        public TargetKind kind;
        public string productId;
        public string label;
        public string summary;
        public PuzzleBoardData board;
    }

    [SerializeField] bool[] selectedCells = new bool[GRID_SIZE * GRID_SIZE];
    [SerializeField] int selectedTargetIndex;
    [SerializeField] bool clearZonesOnApply = true;
    [SerializeField] bool alsoApplyToDefaultTrain = true;
    [SerializeField] bool applyToRunningSession = true;

    readonly List<TargetEntry> targets = new List<TargetEntry>();
    readonly Dictionary<Vector2Int, string> CargoOccupiedCells = new Dictionary<Vector2Int, string>();
    readonly Dictionary<Vector2Int, (string ZoneName, string Tag, Color Color)> ZoneCells =
        new Dictionary<Vector2Int, (string, string, Color)>();

    bool isDragging;
    bool dragPaintValue;
    Vector2Int? lastPaintedCell;
    Vector2 mainScroll;
    string statusMessage;
    MessageType statusType = MessageType.None;

    [MenuItem(MENU)]
    public static void Open()
    {
        var window = GetWindow<WagonGridTestToolWindow>("화차 그리드 테스트");
        window.minSize = new Vector2(560f, 700f);
    }

    void OnEnable()
    {
        minSize = new Vector2(560f, 700f);
        ReloadTargets(_loadBoardShape: true);
    }

    void OnLostFocus()
    {
        isDragging = false;
        lastPaintedCell = null;
    }

    bool GetCell(int _x, int _y)
    {
        if (_x < 0 || _x >= GRID_SIZE || _y < 0 || _y >= GRID_SIZE)
        {
            return false;
        }
        return selectedCells[_y * GRID_SIZE + _x];
    }

    void SetCell(int _x, int _y, bool _value)
    {
        if (_x < 0 || _x >= GRID_SIZE || _y < 0 || _y >= GRID_SIZE)
        {
            return;
        }
        selectedCells[_y * GRID_SIZE + _x] = _value;
    }

    void ReloadTargets(bool _loadBoardShape)
    {
        targets.Clear();
        try
        {
            TableManager.Reload();
            var tables = TableManager.Instance;
            var startingBoard = tables.StartingTrainBoard;
            targets.Add(new TargetEntry
            {
                kind = TargetKind.StartingTrain,
                label = "시작 화차 (PuzzleTableData.startingTrain)",
                summary = DescribeBoard(startingBoard),
                board = startingBoard
            });
            targets.Add(new TargetEntry
            {
                kind = TargetKind.DefaultTrain,
                label = "기본 화차 폴백 (PuzzleTableData.train)",
                summary = DescribeBoard(tables.Puzzle.train),
                board = tables.Puzzle.train
            });

            var products = tables.PuzzleTrade.wagonProducts ?? Array.Empty<PuzzleWagonProductData>();
            targets.Add(new TargetEntry
            {
                kind = TargetKind.AllWagonProducts,
                label = $"상점 화차 상품 전체 ({products.Length}종 일괄 적용)",
                summary = "모든 상점 화차 상품(PuzzleTradeTableData.wagonProducts)의 보드를 동일 모양으로 변경",
                board = products.FirstOrDefault()?.board ?? tables.Puzzle.train
            });

            foreach (var product in products)
            {
                if (product == null)
                {
                    continue;
                }
                var board = tables.WagonBoardOf(product);
                targets.Add(new TargetEntry
                {
                    kind = TargetKind.SingleWagonProduct,
                    productId = product.id,
                    label = $"[상점 화차] {product.displayName} ({product.id})",
                    summary = DescribeBoard(board),
                    board = board
                });
            }

            if (selectedTargetIndex < 0 || selectedTargetIndex >= targets.Count)
            {
                selectedTargetIndex = 0;
            }
            if (_loadBoardShape && targets.Count > 0)
            {
                LoadBoardIntoGrid(targets[selectedTargetIndex].board);
            }
            RebuildOverlays();
        }
        catch (Exception exception)
        {
            statusMessage = "테이블 로드 실패: " + exception.Message;
            statusType = MessageType.Error;
        }
    }

    static string DescribeBoard(PuzzleBoardData _board)
    {
        if (_board == null)
        {
            return "없음";
        }
        int blocked = _board.blockedCells?.Length ?? 0;
        int usable = Math.Max(0, _board.width * _board.height - blocked);
        int zones = _board.zones?.Length ?? 0;
        int placements = _board.startingPlacements?.Length ?? 0;
        return $"{_board.width}×{_board.height} (사용 가능 {usable}칸, 막힘 {blocked}칸, 전용 구역 {zones}개, 시작 화물 {placements}개)";
    }

    void LoadBoardIntoGrid(PuzzleBoardData _board)
    {
        Undo.RecordObject(this, "화차 보드 불러오기");
        Array.Clear(selectedCells, 0, selectedCells.Length);
        if (_board == null || _board.width <= 0 || _board.height <= 0)
        {
            RebuildOverlays();
            Repaint();
            return;
        }

        var blocked = new HashSet<Vector2Int>(
            (_board.blockedCells ?? Array.Empty<PuzzleBoardCellData>())
            .Where(_cell => _cell != null)
            .Select(_cell => new Vector2Int(_cell.x, _cell.y)));

        int width = Mathf.Min(_board.width, GRID_SIZE);
        int height = Mathf.Min(_board.height, GRID_SIZE);
        int offsetX = (GRID_SIZE - width) / 2;
        int offsetY = (GRID_SIZE - height) / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SetCell(offsetX + x, offsetY + y, !blocked.Contains(new Vector2Int(x, y)));
            }
        }
        RebuildOverlays();
        Repaint();
    }

    void RebuildOverlays()
    {
        CargoOccupiedCells.Clear();
        ZoneCells.Clear();
        if (selectedTargetIndex < 0 || selectedTargetIndex >= targets.Count)
        {
            return;
        }

        var entry = targets[selectedTargetIndex];
        var board = entry.board;
        if (board == null)
        {
            return;
        }

        var tables = TableManager.Instance;
        if (!clearZonesOnApply)
        {
            foreach (var zone in board.zones ?? Array.Empty<PuzzleBoardZoneData>())
            {
                if (zone == null || zone.width <= 0 || zone.height <= 0)
                {
                    continue;
                }
                string firstTag = zone.allowedTags != null && zone.allowedTags.Length > 0 ? zone.allowedTags[0] : string.Empty;
                Color tagColor = GetZoneHighlightColor(firstTag);
                for (int y = zone.y; y < zone.y + zone.height; y++)
                {
                    for (int x = zone.x; x < zone.x + zone.width; x++)
                    {
                        ZoneCells[new Vector2Int(x, y)] = (zone.displayName ?? zone.id, firstTag, tagColor);
                    }
                }
            }
        }

        foreach (var placement in board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
        {
            if (placement == null || !tables.TryGetPiece(placement.pieceId, out var piece))
            {
                continue;
            }
            var shape = (piece.cells ?? Array.Empty<PuzzleCellData>())
                .Select(_cell => new Vector2Int(_cell.x, _cell.y))
                .ToArray();
            if (shape.Length == 0)
            {
                continue;
            }
            int turns = Mathf.Clamp(placement.clockwiseTurns, 0, PuzzleRotation.TURN_COUNT - 1);
            var rotation = new PuzzleRotation(shape, turns);
            foreach (var local in shape)
            {
                Vector2Int pos = new Vector2Int(placement.x, placement.y) + rotation.GetBoardOffset(local);
                CargoOccupiedCells[pos] = piece.displayName ?? piece.id;
            }
        }
    }

    static Color GetZoneHighlightColor(string _tag)
    {
        return _tag switch
        {
            "Ore" or "Metal" => new Color(0.45f, 0.72f, 0.95f, 1f),
            "Grain" or "Food" => new Color(0.95f, 0.78f, 0.28f, 1f),
            "Wood" => new Color(0.76f, 0.54f, 0.32f, 1f),
            "Waste" => new Color(0.68f, 0.48f, 0.78f, 1f),
            _ => new Color(0.45f, 0.82f, 0.65f, 1f)
        };
    }

    void OnGUI()
    {
        DrawToolbar();

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
        EditorGUILayout.Space(6f);

        DrawTargetSelector();
        EditorGUILayout.Space(8f);

        DrawPresetBar();
        EditorGUILayout.Space(8f);

        DrawGridSection();
        EditorGUILayout.Space(8f);

        DrawLegendAndSummary();
        EditorGUILayout.Space(8f);

        DrawApplySection();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("테이블 다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                ReloadTargets(_loadBoardShape: true);
                statusMessage = "테이블에서 현재 화차 보드 설정을 다시 읽었습니다.";
                statusType = MessageType.Info;
            }
            if (GUILayout.Button("현재 대상 모양 불러오기", EditorStyles.toolbarButton, GUILayout.Width(145f)))
            {
                if (selectedTargetIndex >= 0 && selectedTargetIndex < targets.Count)
                {
                    LoadBoardIntoGrid(targets[selectedTargetIndex].board);
                    statusMessage = $"'{targets[selectedTargetIndex].label}'의 현재 보드 모양을 10×10 그리드에 불러왔습니다.";
                    statusType = MessageType.Info;
                }
            }
            GUILayout.FlexibleSpace();
        }
    }

    void DrawTargetSelector()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("1. 적용 대상 화차 선택", EditorStyles.boldLabel);

        if (targets.Count == 0)
        {
            EditorGUILayout.HelpBox("화차 대상을 불러오지 못했습니다.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] labels = targets.Select(_target => _target.label).ToArray();
        int nextIndex = EditorGUILayout.Popup("대상 화차", selectedTargetIndex, labels);
        if (nextIndex != selectedTargetIndex)
        {
            selectedTargetIndex = nextIndex;
            LoadBoardIntoGrid(targets[selectedTargetIndex].board);
            statusMessage = null;
        }

        var current = targets[selectedTargetIndex];
        EditorGUILayout.LabelField("현재 테이블 설정", current.summary, EditorStyles.wordWrappedMiniLabel);

        if (current.kind == TargetKind.StartingTrain)
        {
            alsoApplyToDefaultTrain = EditorGUILayout.ToggleLeft(
                "기본 화차 폴백(PuzzleTableData.train)에도 같은 크기·모양 적용", alsoApplyToDefaultTrain);
        }
        else if (current.kind == TargetKind.SingleWagonProduct || current.kind == TargetKind.AllWagonProducts)
        {
            bool nextClearZones = EditorGUILayout.ToggleLeft(
                "적용 시 해당 화차의 전용 구역(zones) 초기화 (자유 적재 테스트용)", clearZonesOnApply);
            if (nextClearZones != clearZonesOnApply)
            {
                clearZonesOnApply = nextClearZones;
                RebuildOverlays();
            }
        }

        if (EditorApplication.isPlaying)
        {
            applyToRunningSession = EditorGUILayout.ToggleLeft(
                "현재 플레이 중인 씬의 화차에도 즉시 모양 반영", applyToRunningSession);
        }

        EditorGUILayout.EndVertical();
    }

    void DrawPresetBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("빠른 크기 선택 & 편집 도구", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 선택 (10×10)"))
            {
                FillRect(10, 10);
            }
            if (GUILayout.Button("전체 해제"))
            {
                FillRect(0, 0);
            }
            if (GUILayout.Button("선택 반전"))
            {
                Undo.RecordObject(this, "화차 그리드 반전");
                for (int i = 0; i < selectedCells.Length; i++)
                {
                    selectedCells[i] = !selectedCells[i];
                }
                Repaint();
            }
            if (GUILayout.Button("왼쪽 위(0,0)로 당기기"))
            {
                AlignToOrigin();
            }
        }

        GUILayout.Label("직사각형 프리셋");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("3×5")) FillRect(3, 5);
            if (GUILayout.Button("3×6")) FillRect(3, 6);
            if (GUILayout.Button("3×7")) FillRect(3, 7);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("4×4")) FillRect(4, 4);
            if (GUILayout.Button("4×6")) FillRect(4, 6);
            if (GUILayout.Button("4×7")) FillRect(4, 7);
            if (GUILayout.Button("4×8")) FillRect(4, 8);
            if (GUILayout.Button("4×9")) FillRect(4, 9);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("5×4")) FillRect(5, 4);
            if (GUILayout.Button("5×5")) FillRect(5, 5);
            if (GUILayout.Button("5×7")) FillRect(5, 7);
            if (GUILayout.Button("5×8")) FillRect(5, 8);
            if (GUILayout.Button("5×9")) FillRect(5, 9);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("7×3")) FillRect(7, 3);
            if (GUILayout.Button("7×4")) FillRect(7, 4);
            if (GUILayout.Button("7×5")) FillRect(7, 5);
            if (GUILayout.Button("7×6")) FillRect(7, 6);
            if (GUILayout.Button("7×7")) FillRect(7, 7);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("8×6")) FillRect(8, 6);
        }

        EditorGUILayout.EndVertical();
    }

    void FillRect(int _width, int _height)
    {
        Undo.RecordObject(this, $"화차 그리드 {_width}x{_height} 설정");
        int offsetX = (GRID_SIZE - _width) / 2;
        int offsetY = (GRID_SIZE - _height) / 2;
        for (int y = 0; y < GRID_SIZE; y++)
        {
            for (int x = 0; x < GRID_SIZE; x++)
            {
                SetCell(x, y, x >= offsetX && x < offsetX + _width &&
                    y >= offsetY && y < offsetY + _height);
            }
        }
        Repaint();
    }

    void AlignToOrigin()
    {
        if (!TryGetSelectionBounds(out int minX, out int minY, out int maxX, out int maxY, out _))
        {
            return;
        }
        if (minX == 0 && minY == 0)
        {
            return;
        }

        Undo.RecordObject(this, "화차 그리드 원점 정렬");
        bool[] next = new bool[GRID_SIZE * GRID_SIZE];
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (GetCell(x, y))
                {
                    next[(y - minY) * GRID_SIZE + (x - minX)] = true;
                }
            }
        }
        selectedCells = next;
        Repaint();
    }

    void DrawGridSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("2. 10×10 화차 그리드 편집 (클릭 또는 드래그하여 칸 선택/해제)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("인게임 보드와 동일하게 아래쪽이 y=0, 위쪽이 y=9, 왼쪽이 x=0, 오른쪽이 x=9입니다.", EditorStyles.miniLabel);

        bool hasBounds = TryGetSelectionBounds(out int minX, out int minY, out int maxX, out int maxY, out int selectedCount);

        float labelMarginLeft = 26f;
        float labelMarginBottom = 22f;
        float gridPixels = GRID_SIZE * CELL_SIZE + (GRID_SIZE - 1) * CELL_GAP;
        float totalWidth = labelMarginLeft + gridPixels + 12f;
        float totalHeight = gridPixels + labelMarginBottom + 12f;

        Rect container = GUILayoutUtility.GetRect(totalWidth, totalHeight, GUILayout.ExpandWidth(false));
        Vector2 gridOrigin = new Vector2(container.x + labelMarginLeft, container.y + 6f);

        // 외접 사각형(Bounding Box) 배경 테두리 표시
        if (hasBounds)
        {
            float boxX = gridOrigin.x + minX * (CELL_SIZE + CELL_GAP) - 3f;
            int topVisualRow = GRID_SIZE - 1 - maxY;
            int bottomVisualRow = GRID_SIZE - 1 - minY;
            float boxY = gridOrigin.y + topVisualRow * (CELL_SIZE + CELL_GAP) - 3f;
            float boxW = (maxX - minX + 1) * (CELL_SIZE + CELL_GAP) - CELL_GAP + 6f;
            float boxH = (bottomVisualRow - topVisualRow + 1) * (CELL_SIZE + CELL_GAP) - CELL_GAP + 6f;
            Rect bboxRect = new Rect(boxX, boxY, boxW, boxH);
            EditorGUI.DrawRect(bboxRect, new Color(0.95f, 0.78f, 0.25f, 0.14f));
            DrawOutline(bboxRect, new Color(0.95f, 0.78f, 0.25f, 0.85f), 2f);
        }

        // 마우스 클릭 & 드래그 입력 처리
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            if (TryGetCellAtMouse(evt.mousePosition, gridOrigin, out Vector2Int hitCell))
            {
                Undo.RecordObject(this, "화차 그리드 셀 선택");
                isDragging = true;
                dragPaintValue = !GetCell(hitCell.x, hitCell.y);
                SetCell(hitCell.x, hitCell.y, dragPaintValue);
                lastPaintedCell = hitCell;
                evt.Use();
                Repaint();
            }
        }
        else if (evt.type == EventType.MouseDrag && isDragging && evt.button == 0)
        {
            if (TryGetCellAtMouse(evt.mousePosition, gridOrigin, out Vector2Int hitCell) && lastPaintedCell != hitCell)
            {
                Undo.RecordObject(this, "화차 그리드 셀 드래그");
                SetCell(hitCell.x, hitCell.y, dragPaintValue);
                lastPaintedCell = hitCell;
                evt.Use();
                Repaint();
            }
        }
        else if (evt.type == EventType.MouseUp && isDragging)
        {
            isDragging = false;
            lastPaintedCell = null;
            evt.Use();
            Repaint();
        }

        // 10x10 셀 렌더링 (위쪽이 y = 9, 아래쪽이 y = 0)
        for (int y = GRID_SIZE - 1; y >= 0; y--)
        {
            int visualRow = GRID_SIZE - 1 - y;
            float rowY = gridOrigin.y + visualRow * (CELL_SIZE + CELL_GAP);

            // Y축 번호
            Rect yLabelRect = new Rect(container.x, rowY, labelMarginLeft - 4f, CELL_SIZE);
            GUI.Label(yLabelRect, y.ToString(), EditorStyles.centeredGreyMiniLabel);

            for (int x = 0; x < GRID_SIZE; x++)
            {
                float colX = gridOrigin.x + x * (CELL_SIZE + CELL_GAP);
                Rect cellRect = new Rect(colX, rowY, CELL_SIZE, CELL_SIZE);
                Vector2Int coord = new Vector2Int(x, y);
                // 외접 사각형 기준 로컬 좌표 (적용 후 실제 보드에서의 좌표)
                Vector2Int localCoord = hasBounds ? new Vector2Int(x - minX, y - minY) : coord;

                bool selected = GetCell(x, y);
                bool insideBounds = hasBounds && x >= minX && x <= maxX && y >= minY && y <= maxY;
                bool hasCargo = CargoOccupiedCells.TryGetValue(localCoord, out string cargoName) && insideBounds;
                bool hasZone = ZoneCells.TryGetValue(localCoord, out var zoneInfo) && insideBounds;

                Color fillColor;
                Color borderColor;
                string badgeText = $"{x},{y}";

                if (selected)
                {
                    if (hasCargo)
                    {
                        // 시작 화물이 배치된 칸: 특별한 주황/금색으로 강조 표시
                        fillColor = new Color(0.94f, 0.58f, 0.18f, 0.92f);
                        borderColor = new Color(1f, 0.84f, 0.42f, 1f);
                        badgeText = "화물";
                    }
                    else if (hasZone)
                    {
                        // 화차 전용 배치 구역(Zone): 구역 태그 전용 색상으로 표시
                        fillColor = Color.Lerp(new Color(0.22f, 0.62f, 0.36f, 0.9f), zoneInfo.Color, 0.55f);
                        borderColor = zoneInfo.Color;
                        badgeText = zoneInfo.Tag;
                    }
                    else
                    {
                        // 일반 활성(선택된) 화차 칸: 선명한 에메랄드/초록색
                        fillColor = new Color(0.24f, 0.68f, 0.42f, 0.9f);
                        borderColor = new Color(0.45f, 0.88f, 0.60f, 1f);
                    }
                }
                else if (insideBounds)
                {
                    if (hasCargo)
                    {
                        // 원래 시작 화물이 있던 칸인데 막힌 칸으로 바뀐 경우: 충돌 경고 색상(보라/붉은색)
                        fillColor = new Color(0.75f, 0.22f, 0.38f, 0.85f);
                        borderColor = new Color(1f, 0.45f, 0.55f, 1f);
                        badgeText = "충돌";
                    }
                    else
                    {
                        // 외접 사각형 안의 미선택 칸 -> 적용 시 blockedCells(막힌 칸)가 되는 칸
                        fillColor = new Color(0.45f, 0.20f, 0.20f, 0.78f);
                        borderColor = new Color(0.75f, 0.32f, 0.32f, 0.95f);
                        badgeText = "막힘";
                    }
                }
                else
                {
                    // 외접 사각형 바깥의 비활성 칸
                    fillColor = new Color(0.18f, 0.19f, 0.22f, 0.65f);
                    borderColor = new Color(0.30f, 0.32f, 0.36f, 0.7f);
                }

                EditorGUI.DrawRect(cellRect, fillColor);
                DrawOutline(cellRect, borderColor, selected ? 1.5f : 1f);

                string tooltip = BuildCellTooltip(coord, localCoord, selected, insideBounds, hasCargo ? cargoName : null,
                    hasZone ? zoneInfo.ZoneName : null);
                GUI.Label(cellRect, new GUIContent(badgeText, tooltip), EditorStyles.centeredGreyMiniLabel);
            }
        }

        // X축 번호
        float xLabelY = gridOrigin.y + gridPixels + 4f;
        for (int x = 0; x < GRID_SIZE; x++)
        {
            float colX = gridOrigin.x + x * (CELL_SIZE + CELL_GAP);
            Rect xLabelRect = new Rect(colX, xLabelY, CELL_SIZE, 18f);
            GUI.Label(xLabelRect, x.ToString(), EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    static string BuildCellTooltip(Vector2Int _gridCoord, Vector2Int _boardCoord, bool _selected, bool _insideBounds,
        string _cargoName, string _zoneName)
    {
        var sb = new StringBuilder();
        sb.Append($"그리드 좌표: ({_gridCoord.x}, {_gridCoord.y})");
        if (_insideBounds)
        {
            sb.Append($"\n적용 보드 좌표: ({_boardCoord.x}, {_boardCoord.y})");
            sb.Append(_selected ? "\n상태: 사용 가능 칸 (선택됨)" : "\n상태: 막힌 칸 (blockedCells)");
        }
        else
        {
            sb.Append("\n상태: 보드 외곽 밖 (미포함)");
        }
        if (!string.IsNullOrEmpty(_cargoName))
        {
            sb.Append($"\n시작 배치 화물: {_cargoName}");
            if (!_selected)
            {
                sb.Append(" (적용 시 빈 칸으로 자동 재배치되거나 제거됨)");
            }
        }
        if (!string.IsNullOrEmpty(_zoneName))
        {
            sb.Append($"\n전용 배치 구역: {_zoneName}");
        }
        return sb.ToString();
    }

    bool TryGetCellAtMouse(Vector2 _mousePos, Vector2 _gridOrigin, out Vector2Int _cell)
    {
        float step = CELL_SIZE + CELL_GAP;
        Vector2 rel = _mousePos - _gridOrigin;
        int x = Mathf.FloorToInt(rel.x / step);
        int visualRow = Mathf.FloorToInt(rel.y / step);
        int y = GRID_SIZE - 1 - visualRow;

        if (x >= 0 && x < GRID_SIZE && y >= 0 && y < GRID_SIZE &&
            rel.x >= 0f && rel.y >= 0f &&
            (rel.x % step) <= CELL_SIZE && (rel.y % step) <= CELL_SIZE)
        {
            _cell = new Vector2Int(x, y);
            return true;
        }

        _cell = default;
        return false;
    }

    static void DrawOutline(Rect _rect, Color _color, float _thickness)
    {
        EditorGUI.DrawRect(new Rect(_rect.x, _rect.y, _rect.width, _thickness), _color);
        EditorGUI.DrawRect(new Rect(_rect.x, _rect.yMax - _thickness, _rect.width, _thickness), _color);
        EditorGUI.DrawRect(new Rect(_rect.x, _rect.y, _thickness, _rect.height), _color);
        EditorGUI.DrawRect(new Rect(_rect.xMax - _thickness, _rect.y, _thickness, _rect.height), _color);
    }

    void DrawLegendAndSummary()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("색상 범례 & 계산된 보드 규격", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawColorLegendChip(new Color(0.24f, 0.68f, 0.42f, 0.9f), "선택된 칸 (적재 가능)");
            DrawColorLegendChip(new Color(0.94f, 0.58f, 0.18f, 0.92f), "시작 화물 배치 구역");
            DrawColorLegendChip(new Color(0.45f, 0.72f, 0.95f, 0.9f), "전용 태그 구역 (Zone)");
            DrawColorLegendChip(new Color(0.45f, 0.20f, 0.20f, 0.78f), "막힌 칸 (blockedCells)");
        }

        EditorGUILayout.Space(4f);
        if (!TryGetSelectionBounds(out int minX, out int minY, out int maxX, out int maxY, out int selectedCount))
        {
            EditorGUILayout.HelpBox("선택된 칸이 없습니다. 최소 1칸 이상 선택해야 화차 보드로 적용할 수 있습니다.", MessageType.Warning);
        }
        else
        {
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int totalBoxCells = width * height;
            int blockedCount = totalBoxCells - selectedCount;
            EditorGUILayout.LabelField(
                $"계산된 화차 크기: {width} × {height} (총 {totalBoxCells}칸 중 사용 가능 {selectedCount}칸 / 막힌 칸 {blockedCount}칸)",
                EditorStyles.boldLabel);
            if (minX > 0 || minY > 0)
            {
                EditorGUILayout.LabelField(
                    $"참고: 선택한 영역의 왼쪽 아래 기준점 ({minX}, {minY})이 보드의 원점 (0, 0)으로 정규화되어 적용됩니다.",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
    }

    static void DrawColorLegendChip(Color _color, string _label)
    {
        Rect rect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
        rect.y += 2f;
        EditorGUI.DrawRect(rect, _color);
        GUILayout.Label(_label, EditorStyles.miniLabel);
        GUILayout.Space(8f);
    }

    void DrawApplySection()
    {
        bool hasSelection = TryGetSelectionBounds(out _, out _, out _, out _, out _);
        using (new EditorGUI.DisabledScope(!hasSelection))
        {
            GUI.backgroundColor = new Color(0.35f, 0.82f, 0.52f);
            if (GUILayout.Button("적용하기 (테이블 저장 & 화차 기본 그리드 반영)", GUILayout.Height(36f)))
            {
                ApplySelectedGrid();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    bool TryGetSelectionBounds(out int _minX, out int _minY, out int _maxX, out int _maxY, out int _selectedCount)
    {
        _minX = GRID_SIZE;
        _minY = GRID_SIZE;
        _maxX = -1;
        _maxY = -1;
        _selectedCount = 0;

        for (int y = 0; y < GRID_SIZE; y++)
        {
            for (int x = 0; x < GRID_SIZE; x++)
            {
                if (!GetCell(x, y))
                {
                    continue;
                }
                _selectedCount++;
                if (x < _minX) _minX = x;
                if (y < _minY) _minY = y;
                if (x > _maxX) _maxX = x;
                if (y > _maxY) _maxY = y;
            }
        }

        return _selectedCount > 0;
    }

    PuzzleBoardData BuildNormalizedBoardData(PuzzleBoardData _referenceBoard)
    {
        if (!TryGetSelectionBounds(out int minX, out int minY, out int maxX, out int maxY, out _))
        {
            throw new InvalidOperationException("최소 1칸 이상 선택해야 합니다.");
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        var blocked = new List<PuzzleBoardCellData>();
        var blockedSet = new HashSet<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!GetCell(minX + x, minY + y))
                {
                    blocked.Add(new PuzzleBoardCellData { x = x, y = y });
                    blockedSet.Add(new Vector2Int(x, y));
                }
            }
        }

        var result = new PuzzleBoardData
        {
            displayName = _referenceBoard?.displayName,
            description = _referenceBoard?.description,
            width = width,
            height = height,
            blockedCells = blocked.ToArray(),
            zones = Array.Empty<PuzzleBoardZoneData>(),
            startingPlacements = Array.Empty<PuzzleStartingPlacementData>()
        };

        if (!clearZonesOnApply && _referenceBoard?.zones != null)
        {
            var validZones = new List<PuzzleBoardZoneData>();
            foreach (var zone in _referenceBoard.zones)
            {
                if (zone == null)
                {
                    continue;
                }
                int zx = Mathf.Clamp(zone.x, 0, width);
                int zy = Mathf.Clamp(zone.y, 0, height);
                int zw = Mathf.Min(zone.width, width - zx);
                int zh = Mathf.Min(zone.height, height - zy);
                if (zw > 0 && zh > 0)
                {
                    validZones.Add(new PuzzleBoardZoneData
                    {
                        id = zone.id,
                        displayName = zone.displayName,
                        x = zx,
                        y = zy,
                        width = zw,
                        height = zh,
                        allowedTags = zone.allowedTags,
                        maxPieceCells = zone.maxPieceCells
                    });
                }
            }
            result.zones = validZones.ToArray();
        }

        if (_referenceBoard?.startingPlacements != null && _referenceBoard.startingPlacements.Length > 0)
        {
            result.startingPlacements = RelocateOrFilterStartingPlacements(
                _referenceBoard.startingPlacements, width, height, blockedSet);
        }

        return result;
    }

    static PuzzleStartingPlacementData[] RelocateOrFilterStartingPlacements(
        PuzzleStartingPlacementData[] _placements, int _width, int _height, HashSet<Vector2Int> _blocked)
    {
        var tables = TableManager.Instance;
        var occupied = new HashSet<Vector2Int>();
        var kept = new List<PuzzleStartingPlacementData>();
        var needsRelocation = new List<(PuzzleStartingPlacementData Placement, Vector2Int[] Shape)>();

        foreach (var placement in _placements)
        {
            if (placement == null || !tables.TryGetPiece(placement.pieceId, out var piece))
            {
                continue;
            }
            var shape = (piece.cells ?? Array.Empty<PuzzleCellData>())
                .Select(_cell => new Vector2Int(_cell.x, _cell.y))
                .ToArray();
            if (shape.Length == 0)
            {
                continue;
            }

            int turns = Mathf.Clamp(placement.clockwiseTurns, 0, PuzzleRotation.TURN_COUNT - 1);
            if (TryPlaceOnBoard(shape, new Vector2Int(placement.x, placement.y), turns, _width, _height, _blocked, occupied,
                out var cells))
            {
                foreach (var cell in cells)
                {
                    occupied.Add(cell);
                }
                kept.Add(new PuzzleStartingPlacementData
                {
                    id = placement.id,
                    pieceId = placement.pieceId,
                    x = placement.x,
                    y = placement.y,
                    clockwiseTurns = turns
                });
            }
            else
            {
                needsRelocation.Add((placement, shape));
            }
        }

        foreach (var (placement, shape) in needsRelocation)
        {
            if (TryFindAnyValidSpot(shape, placement.clockwiseTurns, _width, _height, _blocked, occupied,
                out Vector2Int newOrigin, out int newTurns, out var cells))
            {
                foreach (var cell in cells)
                {
                    occupied.Add(cell);
                }
                kept.Add(new PuzzleStartingPlacementData
                {
                    id = placement.id,
                    pieceId = placement.pieceId,
                    x = newOrigin.x,
                    y = newOrigin.y,
                    clockwiseTurns = newTurns
                });
            }
        }

        return kept.ToArray();
    }

    static bool TryFindAnyValidSpot(Vector2Int[] _shape, int _preferredTurns, int _width, int _height,
        HashSet<Vector2Int> _blocked, HashSet<Vector2Int> _occupied,
        out Vector2Int _origin, out int _turns, out List<Vector2Int> _cells)
    {
        for (int t = 0; t < PuzzleRotation.TURN_COUNT; t++)
        {
            int turns = (Mathf.Max(0, _preferredTurns) + t) % PuzzleRotation.TURN_COUNT;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var candidateOrigin = new Vector2Int(x, y);
                    if (TryPlaceOnBoard(_shape, candidateOrigin, turns, _width, _height, _blocked, _occupied, out _cells))
                    {
                        _origin = candidateOrigin;
                        _turns = turns;
                        return true;
                    }
                }
            }
        }

        _origin = default;
        _turns = 0;
        _cells = null;
        return false;
    }

    static bool TryPlaceOnBoard(Vector2Int[] _shape, Vector2Int _origin, int _turns, int _width, int _height,
        HashSet<Vector2Int> _blocked, HashSet<Vector2Int> _occupied, out List<Vector2Int> _cells)
    {
        var rotation = new PuzzleRotation(_shape, _turns);
        _cells = new List<Vector2Int>(_shape.Length);
        foreach (var local in _shape)
        {
            Vector2Int pos = _origin + rotation.GetBoardOffset(local);
            if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height ||
                _blocked.Contains(pos) || _occupied.Contains(pos))
            {
                _cells = null;
                return false;
            }
            _cells.Add(pos);
        }
        return true;
    }

    void ApplySelectedGrid()
    {
        if (selectedTargetIndex < 0 || selectedTargetIndex >= targets.Count)
        {
            return;
        }

        try
        {
            var entry = targets[selectedTargetIndex];
            var newBoard = BuildNormalizedBoardData(entry.board);

            string puzzlePath = TableManager.EditorAssetPath<PuzzleTableData>();
            string tradePath = TableManager.EditorAssetPath<PuzzleTradeTableData>();

            string puzzleRaw = File.ReadAllText(puzzlePath, UTF8);
            string tradeRaw = File.ReadAllText(tradePath, UTF8);
            string puzzleNewline = puzzleRaw.Contains("\r\n") ? "\r\n" : "\n";
            string tradeNewline = tradeRaw.Contains("\r\n") ? "\r\n" : "\n";

            var puzzleRoot = JObject.Parse(puzzleRaw);
            var tradeRoot = JObject.Parse(tradeRaw);

            bool modifiedPuzzle = false;
            bool modifiedTrade = false;

            if (entry.kind == TargetKind.StartingTrain)
            {
                if (puzzleRoot["startingTrain"] is JObject startingObj)
                {
                    WriteBoardToJObject(startingObj, newBoard, _keepPlacements: true);
                }
                else
                {
                    var created = new JObject();
                    WriteBoardToJObject(created, newBoard, _keepPlacements: true);
                    puzzleRoot["startingTrain"] = created;
                }
                if (alsoApplyToDefaultTrain && puzzleRoot["train"] is JObject defaultTrainObj)
                {
                    var trainBoard = BuildNormalizedBoardData(TableManager.Instance.Puzzle.train);
                    WriteBoardToJObject(defaultTrainObj, trainBoard, _keepPlacements: true);
                }
                modifiedPuzzle = true;
            }
            else if (entry.kind == TargetKind.DefaultTrain)
            {
                if (puzzleRoot["train"] is JObject defaultTrainObj)
                {
                    WriteBoardToJObject(defaultTrainObj, newBoard, _keepPlacements: true);
                    modifiedPuzzle = true;
                }
            }
            else if (entry.kind == TargetKind.AllWagonProducts)
            {
                if (tradeRoot["wagonProducts"] is JArray productsArray)
                {
                    foreach (var productObj in productsArray.OfType<JObject>())
                    {
                        var boardObj = productObj["board"] as JObject ?? new JObject();
                        WriteBoardToJObject(boardObj, newBoard, _keepPlacements: false);
                        productObj["board"] = boardObj;
                    }
                    modifiedTrade = true;
                }
            }
            else if (entry.kind == TargetKind.SingleWagonProduct)
            {
                if (tradeRoot["wagonProducts"] is JArray productsArray)
                {
                    var productObj = productsArray.OfType<JObject>()
                        .FirstOrDefault(_p => (string)_p["id"] == entry.productId);
                    if (productObj == null)
                    {
                        throw new InvalidOperationException($"화차 상품 '{entry.productId}'를 찾지 못했습니다.");
                    }
                    var boardObj = productObj["board"] as JObject ?? new JObject();
                    WriteBoardToJObject(boardObj, newBoard, _keepPlacements: false);
                    productObj["board"] = boardObj;
                    modifiedTrade = true;
                }
            }

            string nextPuzzleJson = modifiedPuzzle
                ? puzzleRoot.ToString(Formatting.Indented).Replace("\r\n", "\n").Replace("\n", puzzleNewline) + puzzleNewline
                : puzzleRaw;
            string nextTradeJson = modifiedTrade
                ? tradeRoot.ToString(Formatting.Indented).Replace("\r\n", "\n").Replace("\n", tradeNewline) + tradeNewline
                : tradeRaw;

            var validationSources = new Dictionary<Type, string>();
            if (modifiedPuzzle)
            {
                validationSources[typeof(PuzzleTableData)] = nextPuzzleJson;
            }
            if (modifiedTrade)
            {
                validationSources[typeof(PuzzleTradeTableData)] = nextTradeJson;
            }

            var issues = TableManager.ValidateSourceIssues(validationSources);
            if (issues.Count > 0)
            {
                statusMessage = "테이블 검증 오류로 적용하지 않았습니다:\n" +
                    string.Join("\n", issues.Select(_issue => _issue.ToString()));
                statusType = MessageType.Error;
                return;
            }

            if (modifiedPuzzle)
            {
                File.WriteAllText(puzzlePath, nextPuzzleJson, UTF8);
            }
            if (modifiedTrade)
            {
                File.WriteAllText(tradePath, nextTradeJson, UTF8);
            }

            AssetDatabase.Refresh();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                TableManager.ReloadAfterTableEdit();
            }
            else
            {
                TableManager.Reload();
            }

            string runtimeSummary = string.Empty;
            if (EditorApplication.isPlaying && applyToRunningSession)
            {
                runtimeSummary = ApplyToRunningPlaySession(entry, newBoard);
            }

            int keepIndex = selectedTargetIndex;
            ReloadTargets(_loadBoardShape: false);
            selectedTargetIndex = Mathf.Clamp(keepIndex, 0, Math.Max(0, targets.Count - 1));
            if (selectedTargetIndex < targets.Count)
            {
                LoadBoardIntoGrid(targets[selectedTargetIndex].board);
            }

            int usable = newBoard.width * newBoard.height - (newBoard.blockedCells?.Length ?? 0);
            statusMessage = $"적용 완료: '{entry.label}' → {newBoard.width}×{newBoard.height} (사용 가능 {usable}칸, 막힘 {newBoard.blockedCells?.Length ?? 0}칸){runtimeSummary}";
            statusType = MessageType.Info;
        }
        catch (Exception exception)
        {
            statusMessage = "적용 중 오류: " + exception.Message;
            statusType = MessageType.Error;
        }
    }

    static string ApplyToRunningPlaySession(TargetEntry _entry, PuzzleBoardData _newBoard)
    {
        var service = FindFirstObjectByType<PuzzleInventoryService>();
        if (!service || service.TrainWagons == null || service.TrainWagons.Count == 0)
        {
            return string.Empty;
        }

        int reshapedCount = 0;
        int removedItems = 0;
        foreach (var wagon in service.TrainWagons)
        {
            bool match = _entry.kind switch
            {
                TargetKind.StartingTrain => wagon.WagonProduct == null,
                TargetKind.DefaultTrain => true,
                TargetKind.AllWagonProducts => wagon.WagonProduct != null,
                TargetKind.SingleWagonProduct => wagon.WagonProduct != null &&
                    string.Equals(wagon.WagonProduct.id, _entry.productId, StringComparison.Ordinal),
                _ => false
            };

            if (!match && wagon == service.Train)
            {
                // 현재 화면에 선택된 화차가 대상과 달라도 바로 테스트할 수 있게 함께 갱신
                match = true;
            }

            if (match)
            {
                removedItems += service.ReshapeWagonForTest(wagon, _newBoard);
                reshapedCount++;
            }
        }

        if (reshapedCount > 0)
        {
            var presenter = FindFirstObjectByType<PuzzleInventoryPresenter>();
            if (presenter)
            {
                presenter.RefreshAfterTestReshape();
            }
            return $" · 실행 중인 씬 화차 {reshapedCount}량 즉시 반영" +
                (removedItems > 0 ? $" (공간 부족 화물 {removedItems}개 제거됨)" : string.Empty);
        }

        return string.Empty;
    }

    static void WriteBoardToJObject(JObject _boardObj, PuzzleBoardData _board, bool _keepPlacements)
    {
        _boardObj["width"] = _board.width;
        _boardObj["height"] = _board.height;

        var blockedArray = new JArray();
        foreach (var cell in _board.blockedCells ?? Array.Empty<PuzzleBoardCellData>())
        {
            blockedArray.Add(new JObject
            {
                ["x"] = cell.x,
                ["y"] = cell.y
            });
        }
        _boardObj["blockedCells"] = blockedArray;

        if (_board.zones != null && _board.zones.Length > 0)
        {
            var zonesArray = new JArray();
            foreach (var zone in _board.zones)
            {
                var zoneObj = new JObject
                {
                    ["id"] = zone.id,
                    ["displayName"] = zone.displayName,
                    ["x"] = zone.x,
                    ["y"] = zone.y,
                    ["width"] = zone.width,
                    ["height"] = zone.height,
                    ["allowedTags"] = new JArray(zone.allowedTags ?? Array.Empty<string>())
                };
                if (zone.maxPieceCells > 0)
                {
                    zoneObj["maxPieceCells"] = zone.maxPieceCells;
                }
                zonesArray.Add(zoneObj);
            }
            _boardObj["zones"] = zonesArray;
        }
        else
        {
            _boardObj.Remove("zones");
        }

        var placementsArray = new JArray();
        if (_keepPlacements)
        {
            foreach (var placement in _board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
            {
                placementsArray.Add(new JObject
                {
                    ["id"] = placement.id,
                    ["pieceId"] = placement.pieceId,
                    ["x"] = placement.x,
                    ["y"] = placement.y,
                    ["clockwiseTurns"] = placement.clockwiseTurns
                });
            }
        }
        _boardObj["startingPlacements"] = placementsArray;
    }
}
