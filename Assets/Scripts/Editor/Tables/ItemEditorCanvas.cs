using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Sprites;

public sealed partial class ItemEditorWindow
{
    const float MIN_CELL_PIXELS = 12f;
    const float MAX_CELL_PIXELS = 160f;
    const float SCROLLBAR_SIZE = 16f;
    const int CANVAS_CONTROL_HINT = 186917;
    [SerializeField] float cellPixels = 48f;
    [SerializeField] Vector2 canvasScroll;
    [SerializeField] int previewTurns;
    [SerializeField] int canvasTool;
    [SerializeField] bool showSprite = true;
    [SerializeField] bool lockSpriteAspect;
    [SerializeField] bool showRaw;
    [SerializeField] bool showCellDetails;
    [SerializeField] int selectedCellIndex;
    bool shapeValidationDirty = true;
    bool fitItemCanvas = true;
    string canvasPieceId;
    readonly List<string> shapeErrors = new List<string>();
    readonly HashSet<Vector2Int> paintedCells = new HashSet<Vector2Int>();
    int itemCanvasControl;
    int itemCanvasUndoGroup = -1;
    bool paintingCells, removingCells, movingSprite, panningCanvas;
    Vector2 dragMouse, dragIconOffset, dragScroll;
    Vector2 canvasViewportSize;
    Vector2Int? previousPaintCell;
    Rect canvasDomain;

    sealed class ItemPreview
    {
        public Vector2Int[] originalCells;
        public Vector2Int[] boardCells;
        public Vector2Int originalMin, originalMax;
        public PuzzleIconLayout icon;
        public Rect iconBounds;
    }

    void OnLostFocus()
    {
        ReleaseCanvasDrag();
    }

    static float Number(JObject _piece, string _key, float _missing)
    {
        return (float?)_piece[_key] ?? _missing;
    }

    static Vector2Int CellPosition(JObject _cell)
    {
        return new Vector2Int((int?)_cell["x"] ?? -1, (int?)_cell["y"] ?? -1);
    }

    static Vector2 IconOffset(JObject _piece)
    {
        return new Vector2(Number(_piece, "iconOffsetX", 0f), Number(_piece, "iconOffsetY", 0f));
    }

    static Vector2 IconSize(JObject _piece)
    {
        return new Vector2(Number(_piece, "iconWidth", -1f), Number(_piece, "iconHeight", -1f));
    }

    int MaxShapeSide()
    {
        var boards = new List<JToken> { document["train"] };
        boards.AddRange(document["cities"].OfType<JObject>().Select(_city => _city["market"]));
        return boards.Max(_board => Mathf.Max((int?)_board?["width"] ?? 0, (int?)_board?["height"] ?? 0));
    }

    void ReadShapeErrors(JObject _piece)
    {
        if (!shapeValidationDirty)
        {
            return;
        }
        shapeErrors.Clear();
        try
        {
            var cells = _piece["cells"]?.ToObject<PuzzleCellData[]>();
            new GameTableRules(shapeErrors).ValidatePieceCells(cells, (int?)_piece["slotCount"] ?? -1,
                MaxShapeSide(), "PuzzleTableData.pieces." + (string)_piece["id"]);
        }
        catch (Exception exception)
        {
            shapeErrors.Add("모양을 읽을 수 없습니다: " + exception.Message);
        }
        shapeValidationDirty = false;
    }

    ItemPreview MakePreview(JObject _piece)
    {
        var cells = (_piece["cells"] as JArray)?.OfType<JObject>().Select(CellPosition).ToArray()
            ?? Array.Empty<Vector2Int>();
        var preview = new ItemPreview
        {
            originalCells = cells,
            boardCells = Array.Empty<Vector2Int>()
        };
        Vector2Int boardMin = Vector2Int.zero, boardMax = Vector2Int.zero;
        if (cells.Length != 0)
        {
            preview.originalMin = cells.Aggregate(Vector2Int.Min);
            preview.originalMax = cells.Aggregate(Vector2Int.Max);
            var rotation = new PuzzleRotation(cells, previewTurns);
            // 0도 편집에서는 원본 좌표를 유지한다. 원점이 틀린 초안도 셀이 이동하지 않아 고칠 수 있다.
            var origin = previewTurns == 0
                ? new Vector2Int(preview.originalMin.x, -preview.originalMin.y) : Vector2Int.zero;
            preview.boardCells = cells.Select(_cell => origin + rotation.GetBoardOffset(_cell)).ToArray();
            boardMin = preview.boardCells.Aggregate(Vector2Int.Min);
            boardMax = preview.boardCells.Aggregate(Vector2Int.Max);
        }
        preview.icon = PuzzleIconLayout.Calculate(boardMin, boardMax, preview.originalMin, preview.originalMax,
            IconSize(_piece), IconOffset(_piece), previewTurns);
        Vector2 size = previewTurns % 2 == 0 ? preview.icon.Size : new Vector2(preview.icon.Size.y, preview.icon.Size.x);
        Vector2 center = new Vector2(preview.icon.Center.x + 0.5f, -preview.icon.Center.y + 0.5f);
        preview.iconBounds = new Rect(center - size * 0.5f, size);
        return preview;
    }

    void DrawItemCanvas(JObject _piece)
    {
        if (canvasPieceId != selectedId)
        {
            ReleaseCanvasDrag();
            canvasPieceId = selectedId;
            selectedCellIndex = 0;
            previewTurns = 0;
            shapeValidationDirty = true;
            fitItemCanvas = true;
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            int tool = GUILayout.Toolbar(canvasTool, new[] { "모양", "이미지 이동" }, EditorStyles.toolbarButton);
            if (tool != canvasTool)
            {
                ReleaseCanvasDrag();
                canvasTool = tool;
                if (tool == 0)
                {
                    previewTurns = 0;
                    fitItemCanvas = true;
                }
            }
            int turns = GUILayout.Toolbar(previewTurns, new[] { "0°", "90°", "180°", "270°" }, EditorStyles.toolbarButton);
            if (turns != previewTurns)
            {
                ReleaseCanvasDrag();
                previewTurns = turns;
                fitItemCanvas = true;
            }
            if (GUILayout.Button("전체 보기", EditorStyles.toolbarButton))
            {
                fitItemCanvas = true;
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("격자 크기", GUILayout.Width(55f));
            float next = GUILayout.HorizontalSlider(cellPixels, MIN_CELL_PIXELS, MAX_CELL_PIXELS);
            if (!Mathf.Approximately(next, cellPixels))
            {
                SetCanvasZoom(next, canvasViewportSize * 0.5f);
            }
            GUILayout.Label(Mathf.RoundToInt(cellPixels) + " px", GUILayout.Width(45f));
            showSprite = GUILayout.Toggle(showSprite, "이미지", GUILayout.Width(65f));
        }
        EditorGUILayout.LabelField("0°: 클릭/드래그로 셀 추가·제거 · 중간 버튼: 이동 · Ctrl+휠: 확대/축소", EditorStyles.wordWrappedMiniLabel);
        if (canvasTool == 0 && previewTurns != 0)
        {
            EditorGUILayout.HelpBox("회전은 미리보기입니다. 셀 편집은 0° 또는 모양 버튼을 선택하세요.", MessageType.None);
        }
        ReadShapeErrors(_piece);
        if (shapeErrors.Count != 0)
        {
            EditorGUILayout.HelpBox("모양 오류 " + shapeErrors.Count + "개 · 오른쪽 모양 속성에서 내용을 확인하세요.", MessageType.Error);
        }
        var sprite = draft.icons.FirstOrDefault(_icon => _icon.pieceId == selectedId)?.Resolve();
        var preview = MakePreview(_piece);
        Rect viewport = GUILayoutUtility.GetRect(160f, 280f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
        canvasViewportSize = viewport.size - Vector2.one * SCROLLBAR_SIZE;
        int side = MaxShapeSide();
        if (itemCanvasControl == 0)
        {
            float margin = Mathf.Max(1f, 48f / cellPixels);
            float minX = Mathf.Min(-margin, preview.iconBounds.xMin - margin);
            float minY = Mathf.Min(-margin, preview.iconBounds.yMin - margin);
            float maxX = Mathf.Max(side + margin, preview.iconBounds.xMax + margin);
            float maxY = Mathf.Max(side + margin, preview.iconBounds.yMax + margin);
            foreach (var cell in preview.boardCells)
            {
                minX = Mathf.Min(minX, cell.x - 1f);
                minY = Mathf.Min(minY, -cell.y - 1f);
                maxX = Mathf.Max(maxX, cell.x + 2f);
                maxY = Mathf.Max(maxY, -cell.y + 2f);
            }
            var nextDomain = Rect.MinMaxRect(minX, minY, maxX, maxY);
            canvasScroll += (canvasDomain.min - nextDomain.min) * cellPixels;
            canvasDomain = nextDomain;
        }
        if (fitItemCanvas && Event.current.type == EventType.Repaint)
        {
            var bounds = preview.iconBounds;
            foreach (var cell in preview.boardCells)
            {
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, cell.x), Mathf.Min(bounds.yMin, -cell.y),
                    Mathf.Max(bounds.xMax, cell.x + 1f), Mathf.Max(bounds.yMax, -cell.y + 1f));
            }
            cellPixels = Mathf.Clamp(Mathf.Min((viewport.width - 70f) / Mathf.Max(1f, bounds.width),
                (viewport.height - 70f) / Mathf.Max(1f, bounds.height)), MIN_CELL_PIXELS, MAX_CELL_PIXELS);
            canvasScroll = (bounds.center - canvasDomain.min) * cellPixels -
                (viewport.size - Vector2.one * SCROLLBAR_SIZE) * 0.5f;
            fitItemCanvas = false;
        }
        Vector2 contentSize = Vector2.Max(canvasDomain.size * cellPixels,
            viewport.size - Vector2.one * SCROLLBAR_SIZE);
        canvasScroll = Vector2.Max(Vector2.zero, Vector2.Min(canvasScroll,
            Vector2.Max(Vector2.zero, contentSize - viewport.size + Vector2.one * SCROLLBAR_SIZE)));
        HandleCanvasInput(viewport, _piece, preview, sprite);
        contentSize = Vector2.Max(canvasDomain.size * cellPixels, canvasViewportSize);
        canvasScroll = GUI.BeginScrollView(viewport, canvasScroll, new Rect(Vector2.zero, contentSize), true, true);
        try
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawGrid(side, preview);
                if (showSprite && sprite && preview.originalCells.Length != 0)
                {
                    DrawSprite(preview, sprite);
                }
            }
        }
        finally
        {
            GUI.EndScrollView();
        }
    }

    Vector2 CanvasPoint(Vector2 _local)
    {
        return (_local - canvasDomain.min) * cellPixels;
    }

    void DrawGrid(int _side, ItemPreview _preview)
    {
        Color lines = EditorStyles.label.normal.textColor;
        lines.a = 0.25f;
        for (int i = 0; i <= _side; i++)
        {
            Vector2 vertical = CanvasPoint(new Vector2(i, 0f));
            Vector2 horizontal = CanvasPoint(new Vector2(0f, i));
            EditorGUI.DrawRect(new Rect(vertical.x, vertical.y, 1f, _side * cellPixels), lines);
            EditorGUI.DrawRect(new Rect(horizontal.x, horizontal.y, _side * cellPixels, 1f), lines);
            if (i < _side)
            {
                GUI.Label(new Rect(vertical.x, vertical.y - 20f, cellPixels, 20f), i.ToString(), EditorStyles.centeredGreyMiniLabel);
                GUI.Label(new Rect(horizontal.x - 24f, horizontal.y, 22f, cellPixels), i.ToString(), EditorStyles.centeredGreyMiniLabel);
            }
        }
        var occupied = new HashSet<Vector2Int>(_preview.boardCells);
        Color fill = GUI.skin.settings.selectionColor;
        fill.a = 0.35f;
        foreach (var cell in occupied)
        {
            Vector2 topLeft = CanvasPoint(new Vector2(cell.x, -cell.y));
            var rect = new Rect(topLeft, Vector2.one * cellPixels);
            EditorGUI.DrawRect(rect, fill);
            Color outline = EditorStyles.label.normal.textColor;
            if (!occupied.Contains(cell + Vector2Int.up))
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), outline);
            }
            if (!occupied.Contains(cell + Vector2Int.down))
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), outline);
            }
            if (!occupied.Contains(cell + Vector2Int.left))
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), outline);
            }
            if (!occupied.Contains(cell + Vector2Int.right))
            {
                EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), outline);
            }
        }
        Vector2 origin = CanvasPoint(Vector2.zero);
        GUI.Label(new Rect(origin.x, origin.y - 40f, 150f, 20f), "원점 · +x 오른쪽 / +y 아래", EditorStyles.miniLabel);
    }

    void DrawSprite(ItemPreview _preview, Sprite _sprite)
    {
        Vector2 center = CanvasPoint(new Vector2(_preview.icon.Center.x + 0.5f, -_preview.icon.Center.y + 0.5f));
        Vector2 size = _preview.icon.Size * cellPixels;
        Rect box = new Rect(center - size * 0.5f, size);
        var previousMatrix = GUI.matrix;
        try
        {
            GUIUtility.RotateAroundPivot(90f * previewTurns, center);
            if (_sprite.rect.width / _sprite.rect.height > size.x / size.y)
            {
                size.y = size.x * _sprite.rect.height / _sprite.rect.width;
            }
            else
            {
                size.x = size.y * _sprite.rect.width / _sprite.rect.height;
            }
            Rect full = new Rect(center - size * 0.5f, size);
            Vector4 padding = DataUtility.GetPadding(_sprite);
            float width = Mathf.RoundToInt(_sprite.rect.width);
            float height = Mathf.RoundToInt(_sprite.rect.height);
            Rect trimmed = Rect.MinMaxRect(full.xMin + full.width * padding.x / width,
                full.yMin + full.height * padding.w / height,
                full.xMax - full.width * padding.z / width, full.yMax - full.height * padding.y / height);
            Vector4 uv = DataUtility.GetOuterUV(_sprite);
            GUI.DrawTextureWithTexCoords(trimmed, _sprite.texture, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y), true);
            DrawImageFrame(box, false);
            DrawImageFrame(full, true);
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
        Color color = EditorStyles.label.normal.textColor;
        EditorGUI.DrawRect(new Rect(center.x - 4f, center.y - 1f, 8f, 2f), color);
        EditorGUI.DrawRect(new Rect(center.x - 1f, center.y - 4f, 2f, 8f), color);
    }

    static void DrawImageFrame(Rect _rect, bool _dotted)
    {
        Color color = EditorStyles.label.normal.textColor;
        float longest = Mathf.Max(_rect.width, _rect.height);
        float dash = _dotted ? Mathf.Max(4f, longest / 512f) : longest;
        for (float x = 0f; x < _rect.width; x += dash * 2f)
        {
            float width = Mathf.Min(dash, _rect.width - x);
            EditorGUI.DrawRect(new Rect(_rect.x + x, _rect.y, width, 1f), color);
            EditorGUI.DrawRect(new Rect(_rect.x + x, _rect.yMax - 1f, width, 1f), color);
        }
        for (float y = 0f; y < _rect.height; y += dash * 2f)
        {
            float height = Mathf.Min(dash, _rect.height - y);
            EditorGUI.DrawRect(new Rect(_rect.x, _rect.y + y, 1f, height), color);
            EditorGUI.DrawRect(new Rect(_rect.xMax - 1f, _rect.y + y, 1f, height), color);
        }
    }

    void SetCanvasZoom(float _pixels, Vector2 _anchor)
    {
        Vector2 point = (canvasScroll + _anchor) / cellPixels;
        cellPixels = Mathf.Clamp(_pixels, MIN_CELL_PIXELS, MAX_CELL_PIXELS);
        canvasScroll = point * cellPixels - _anchor;
        fitItemCanvas = false;
    }

    void BeginCanvasDrag(int _control, bool _undo)
    {
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = _control;
        itemCanvasControl = _control;
        if (_undo)
        {
            Undo.IncrementCurrentGroup();
            itemCanvasUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(movingSprite ? "아이템 이미지 이동" : "아이템 모양 그리기");
        }
    }

    void ReleaseCanvasDrag(bool _collapse = true)
    {
        if (itemCanvasControl != 0 && GUIUtility.hotControl == itemCanvasControl)
        {
            GUIUtility.hotControl = 0;
        }
        if (_collapse && itemCanvasUndoGroup >= 0)
        {
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(itemCanvasUndoGroup);
            Undo.IncrementCurrentGroup();
        }
        itemCanvasControl = 0;
        itemCanvasUndoGroup = -1;
        paintingCells = movingSprite = panningCanvas = false;
        previousPaintCell = null;
        paintedCells.Clear();
    }

    void HandleCanvasInput(Rect _viewport, JObject _piece, ItemPreview _preview, Sprite _sprite)
    {
        var input = Event.current;
        int control = GUIUtility.GetControlID(CANVAS_CONTROL_HINT, FocusType.Passive);
        Rect area = new Rect(_viewport.position, _viewport.size - Vector2.one * SCROLLBAR_SIZE);
        bool inside = area.Contains(input.mousePosition);
        Vector2 local = (input.mousePosition - _viewport.position + canvasScroll) / cellPixels + canvasDomain.min;
        var cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        if (input.type == EventType.ScrollWheel && input.control && inside && itemCanvasControl == 0)
        {
            SetCanvasZoom(cellPixels * Mathf.Exp(-input.delta.y * 0.08f), input.mousePosition - _viewport.position);
            input.Use();
            Repaint();
        }
        if (itemCanvasControl != 0)
        {
            if (GUIUtility.hotControl != itemCanvasControl)
            {
                ReleaseCanvasDrag();
                return;
            }
            if (input.type == EventType.MouseUp ||
                (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape))
            {
                ReleaseCanvasDrag();
                input.Use();
                Repaint();
                return;
            }
            if (input.type == EventType.MouseDrag)
            {
                if (panningCanvas)
                {
                    canvasScroll = dragScroll - (input.mousePosition - dragMouse);
                }
                else if (movingSprite)
                {
                    Vector2 delta = (input.mousePosition - dragMouse) / cellPixels;
                    Vector2 offset = dragIconOffset + PuzzleIconLayout.RotateOffset(new Vector2(delta.x, -delta.y),
                        (PuzzleRotation.TURN_COUNT - previewTurns) % PuzzleRotation.TURN_COUNT);
                    Change("아이템 이미지 이동", () =>
                    {
                        _piece["iconOffsetX"] = offset.x;
                        _piece["iconOffsetY"] = offset.y;
                    });
                }
                else if (paintingCells && inside)
                {
                    PaintCells(_piece, cell);
                }
                else
                {
                    previousPaintCell = null;
                }
                input.Use();
                Repaint();
            }
            return;
        }
        if (input.type != EventType.MouseDown || !inside)
        {
            return;
        }
        dragMouse = input.mousePosition;
        dragScroll = canvasScroll;
        if (input.button == 2)
        {
            panningCanvas = true;
            BeginCanvasDrag(control, false);
        }
        else if (input.button == 0 && canvasTool == 1 && showSprite && _sprite &&
            _preview.originalCells.Length > 0 && _preview.iconBounds.Contains(local))
        {
            movingSprite = true;
            dragIconOffset = IconOffset(_piece);
            BeginCanvasDrag(control, true);
        }
        else if (input.button == 0 && canvasTool == 0 && previewTurns == 0 &&
            ((cell.x >= 0 && cell.y >= 0 && cell.x < MaxShapeSide() && cell.y < MaxShapeSide()) ||
                _piece["cells"].OfType<JObject>().Any(_entry => CellPosition(_entry) == cell)))
        {
            paintingCells = true;
            removingCells = _piece["cells"].OfType<JObject>().Any(_entry => CellPosition(_entry) == cell);
            BeginCanvasDrag(control, true);
            PaintCells(_piece, cell);
        }
        else
        {
            return;
        }
        input.Use();
    }

    void PaintCells(JObject _piece, Vector2Int _cell)
    {
        int side = MaxShapeSide();
        if (!removingCells && (_cell.x < 0 || _cell.y < 0 || _cell.x >= side || _cell.y >= side))
        {
            previousPaintCell = null;
            return;
        }
        var positions = new List<Vector2Int>();
        var start = previousPaintCell ?? _cell;
        int steps = Mathf.Max(Mathf.Abs(_cell.x - start.x), Mathf.Abs(_cell.y - start.y));
        if (steps > side * 2)
        {
            start = _cell;
            steps = 0;
        }
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = steps == 0 ? (Vector2)_cell : Vector2.Lerp(start, _cell, (float)i / steps);
            var position = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
            var previous = positions.Count == 0 ? start : positions[positions.Count - 1];
            if (previous.x != position.x && previous.y != position.y)
            {
                var between = new Vector2Int(position.x, previous.y);
                if (paintedCells.Add(between))
                {
                    positions.Add(between);
                }
            }
            if (paintedCells.Add(position))
            {
                positions.Add(position);
            }
        }
        previousPaintCell = _cell;
        var cells = (JArray)_piece["cells"];
        positions = positions.Where(_position => removingCells == cells.OfType<JObject>()
            .Any(_entry => CellPosition(_entry) == _position)).ToList();
        if (positions.Count == 0)
        {
            return;
        }
        Change("아이템 모양 그리기", () =>
        {
            foreach (var position in positions)
            {
                if (removingCells)
                {
                    foreach (var existing in cells.OfType<JObject>().Where(_entry => CellPosition(_entry) == position).ToArray())
                    {
                        existing.Remove();
                    }
                }
                else
                {
                    cells.Add(new JObject
                    {
                        ["x"] = position.x, ["y"] = position.y,
                        ["north"] = "Flat", ["east"] = "Flat", ["south"] = "Flat", ["west"] = "Flat"
                    });
                }
            }
            _piece["slotCount"] = cells.Count;
            selectedCellIndex = Mathf.Clamp(selectedCellIndex, 0, Mathf.Max(0, cells.Count - 1));
        });
    }

    void DrawGeometryFields(JObject _piece, Sprite _sprite)
    {
        EditorGUILayout.Space();
        GUILayout.Label("이미지 위치·크기 · 칸 단위", EditorStyles.boldLabel);
        Vector2 offset = IconOffset(_piece);
        MarkValidationField("iconOffsetX");
        float offsetX = EditorGUILayout.DelayedFloatField("X 오프셋 (+오른쪽)", offset.x);
        MarkValidationField("iconOffsetY");
        float offsetY = EditorGUILayout.DelayedFloatField("Y 오프셋 (+위)", offset.y);
        if (offsetX != offset.x || offsetY != offset.y)
        {
            Change("이미지 오프셋", () =>
            {
                RequireFinite(offsetX, offsetY);
                _piece["iconOffsetX"] = offsetX;
                _piece["iconOffsetY"] = offsetY;
            });
        }
        Vector2 size = IconSize(_piece);
        bool automatic = size.x <= 0f || size.y <= 0f;
        bool nextAutomatic = EditorGUILayout.Toggle("외접 사각형 크기 사용", automatic);
        if (automatic != nextAutomatic)
        {
            Change("이미지 크기 모드", () =>
            {
                Vector2 initial = MakePreview(_piece).icon.Size;
                if (!nextAutomatic && lockSpriteAspect && _sprite)
                {
                    initial.y = initial.x * _sprite.rect.height / _sprite.rect.width;
                }
                _piece["iconWidth"] = nextAutomatic ? -1f : initial.x;
                _piece["iconHeight"] = nextAutomatic ? -1f : initial.y;
            });
            size = IconSize(_piece);
            automatic = nextAutomatic;
        }
        using (new EditorGUI.DisabledScope(!_sprite))
        {
            bool nextLock = EditorGUILayout.Toggle("원본 종횡비 고정", lockSpriteAspect);
            if (nextLock != lockSpriteAspect)
            {
                lockSpriteAspect = nextLock;
                if (nextLock && !automatic && _sprite)
                {
                    SetIconSize(_piece, new Vector2(size.x, size.x * _sprite.rect.height / _sprite.rect.width));
                    size = IconSize(_piece);
                }
            }
        }
        using (new EditorGUI.DisabledScope(automatic))
        {
            Vector2 shown = automatic ? MakePreview(_piece).icon.Size : size;
            MarkValidationField("iconWidth");
            float width = EditorGUILayout.DelayedFloatField("폭", shown.x);
            MarkValidationField("iconHeight");
            float height = EditorGUILayout.DelayedFloatField("높이", shown.y);
            if (!automatic && (width != size.x || height != size.y))
            {
                if (lockSpriteAspect && _sprite)
                {
                    if (width != size.x)
                    {
                        height = width * _sprite.rect.height / _sprite.rect.width;
                    }
                    else
                    {
                        width = height * _sprite.rect.width / _sprite.rect.height;
                    }
                }
                SetIconSize(_piece, new Vector2(width, height));
            }
        }
        if (GUILayout.Button("이미지 위치·크기 초기화"))
        {
            Change("이미지 배치 초기화", () =>
            {
                _piece["iconOffsetX"] = 0f;
                _piece["iconOffsetY"] = 0f;
                _piece["iconWidth"] = -1f;
                _piece["iconHeight"] = -1f;
            });
            fitItemCanvas = true;
        }
        EditorGUILayout.HelpBox("십자: 이미지 중심 · 실선: 지정 크기 · 점선: 원본 비율 영역(투명 여백 포함). 회전은 저장하지 않습니다.", MessageType.None);
        var cells = _piece["cells"] as JArray;
        EditorGUILayout.Space();
        MarkValidationField("cells");
        GUILayout.Label("모양", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("슬롯 수 (셀에서 자동 계산)", (cells?.Count ?? 0).ToString());
        ReadShapeErrors(_piece);
        foreach (string error in shapeErrors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
        if (GUILayout.Button("모양을 원점에 맞추기") && cells != null && cells.Count != 0)
        {
            Change("모양 원점 정렬", () =>
            {
                Vector2Int minimum = cells.OfType<JObject>().Select(CellPosition).Aggregate(Vector2Int.Min);
                foreach (var cell in cells.OfType<JObject>())
                {
                    var position = CellPosition(cell) - minimum;
                    cell["x"] = position.x;
                    cell["y"] = position.y;
                }
                _piece["slotCount"] = cells.Count;
            });
        }
        showCellDetails = EditorGUILayout.Foldout(showCellDetails, "셀 고급 속성", true);
        if (showCellDetails && cells != null && cells.Count > 0)
        {
            string[] labels = cells.Select((_cell, _index) => _index + ": (" + (string)_cell["x"] + ", " + (string)_cell["y"] + ")").ToArray();
            selectedCellIndex = EditorGUILayout.Popup("셀", Mathf.Clamp(selectedCellIndex, 0, cells.Count - 1), labels);
            var cell = (JObject)cells[selectedCellIndex];
            foreach (string direction in new[] { "north", "east", "south", "west" })
            {
                string current = (string)cell[direction] ?? "";
                string next = EditorGUILayout.DelayedTextField(direction, current);
                if (next != current)
                {
                    Change("셀 방향 속성", () => cell[direction] = next);
                }
            }
            EditorGUILayout.HelpBox("방향별 연결 효과는 현재 적용되지 않음. 현재 테이블은 네 방향 모두 Flat만 허용합니다.", MessageType.Info);
        }
    }

    void SetIconSize(JObject _piece, Vector2 _size)
    {
        Change("이미지 크기", () =>
        {
            RequireFinite(_size.x, _size.y);
            if (_size.x <= 0f || _size.y <= 0f)
            {
                throw new InvalidOperationException("폭과 높이는 양수로 입력하거나 외접 사각형 크기를 선택하세요.");
            }
            _piece["iconWidth"] = _size.x;
            _piece["iconHeight"] = _size.y;
        });
    }

    static void RequireFinite(float _x, float _y)
    {
        if (float.IsNaN(_x) || float.IsInfinity(_x) || float.IsNaN(_y) || float.IsInfinity(_y))
        {
            throw new InvalidOperationException("이미지 위치와 크기는 유한한 숫자여야 합니다.");
        }
    }
}
