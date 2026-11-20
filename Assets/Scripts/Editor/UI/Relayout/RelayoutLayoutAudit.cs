using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 릴레이아웃 프리팹(Assets/Art/UI/Relayout/*.prefab)의 uGUI 레이아웃을 읽기 전용으로 점검해 마크다운 보고서를 만든다.
// 프리팹은 LoadPrefabContents로 메모리에서만 열고 저장하지 않는다. 좌표는 프리팹 루트 로컬 px 기준이다.
public static class RelayoutLayoutAudit
{
    const string MENU = "Tools/MyJG/UI/Relayout/Audit Layout";
    const string PREFAB_DIR = "Assets/Art/UI/Relayout";
    const string REPORT_DIR = ".orca/analysis/relayout";
    const string REPORT_FILE = "layout_audit.md";
    const string RULE_OUT = "OUT_OF_PARENT", RULE_EDGE = "EDGE_PADDING", RULE_TEXT = "TEXT_OVERFLOW";
    const string RULE_OVERLAP = "SIBLING_OVERLAP", RULE_ALIGN = "ALIGN";
    static readonly string[] RULES = { RULE_OUT, RULE_EDGE, RULE_TEXT, RULE_OVERLAP, RULE_ALIGN };

    const float OUT_TOL = 1f;      // 부모 밖으로 나간 허용 오차(px)
    const float TEXT_TOL = 1f;     // 텍스트 넘침 허용 오차(px)
    const float OVERLAP_TOL = 2f;  // 형제 겹침 허용 오차(px)
    const float ALPHA_MIN = 0.01f; // 보이는 그래픽으로 볼 최소 알파
    const int NEAR_MISS_MAX = 6;   // 정렬 근접 실패로 볼 최대 차이(px), 최소 1px
    const int MAX_SIBLINGS = 120;  // 형제 비교를 할 최대 형제 수

    // 검사 결과 한 줄 (Rule, Path, Details)
    sealed class Finding
    {
        public string Rule, Path, Details;
    }

    // 프리팹 하나의 검사 결과
    sealed class PrefabResult
    {
        public string Name, Error;
        public List<Finding> Findings = new List<Finding>();
    }

    [MenuItem(MENU)]
    static void RunFromMenu()
    {
        Run();
        Debug.Log("[RelayoutLayoutAudit] 보고서 저장: " + ReportPath());
    }

    // 모든 릴레이아웃 프리팹을 점검해 보고서 문자열을 반환하고, 같은 내용을 파일로도 저장한다.
    public static string Run(int _minPadding = 8)
    {
        var results = new List<PrefabResult>();
        string prefabDir = Path.Combine(ProjectRoot(), PREFAB_DIR);
        if (Directory.Exists(prefabDir))
        {
            // 출력 순서를 고정하려고 파일 이름 기준 정렬
            var files = Directory.GetFiles(prefabDir, "*.prefab", SearchOption.TopDirectoryOnly)
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);
            foreach (string file in files)
                results.Add(AuditPrefab(PREFAB_DIR + "/" + Path.GetFileName(file), _minPadding));
        }

        string report = BuildReport(results, _minPadding);
        Directory.CreateDirectory(Path.Combine(ProjectRoot(), REPORT_DIR));
        File.WriteAllText(ReportPath(), report, new UTF8Encoding(false));
        return report;
    }

    static string ProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    static string ReportPath()
    {
        return Path.Combine(ProjectRoot(), REPORT_DIR, REPORT_FILE);
    }

    // 프리팹 하나를 열어 검사하고 반드시 언로드한다.
    // 플레이 중 실제로 열린 창(FloatingWindow 루트)과 고정 틀을 런타임 내용 그대로 검사한다.
    // 프리팹 상태에서는 런타임에 채우는 글자·목록이 비어 있어 놓치는 문제를 잡기 위한 것이다.
    public static string RunLive(string _label, int _minPadding = 8)
    {
        var results = new List<PrefabResult>();
        var roots = new List<GameObject>();
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || !mb.isActiveAndEnabled) continue;
            string tn = mb.GetType().Name;
            if (tn == "FloatingWindow" || tn == "TopHudView" || tn == "QuickToolbarView" || tn == "StatusBannerView" ||
                tn == "RailStripView" || tn == "TownSceneView" || tn == "PlatformSceneView")
            {
                if (!roots.Contains(mb.gameObject)) roots.Add(mb.gameObject);
            }
        }
        roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var go in roots)
        {
            var r = new PrefabResult { Name = go.name };
            try { r.Findings = AuditRoot(go, _minPadding); }
            catch (Exception e) { r.Error = e.Message; }
            results.Add(r);
        }
        string report = BuildReport(results, _minPadding);
        Directory.CreateDirectory(Path.Combine(ProjectRoot(), REPORT_DIR));
        File.WriteAllText(Path.Combine(ProjectRoot(), REPORT_DIR, "layout_audit_live_" + _label + ".md"), report, new UTF8Encoding(false));
        return report;
    }

    static PrefabResult AuditPrefab(string assetPath, int minPadding)
    {
        var result = new PrefabResult { Name = Path.GetFileNameWithoutExtension(assetPath) };
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null) result.Error = "프리팹 로드 실패";
            else result.Findings = AuditRoot(root, minPadding);
        }
        catch (Exception e)
        {
            result.Error = e.Message;
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
        return result;
    }

    static List<Finding> AuditRoot(GameObject root, int minPadding)
    {
        var rootRt = root.GetComponent<RectTransform>();
        if (rootRt == null) throw new InvalidOperationException("루트에 RectTransform이 없음");

        // 검사 전에 루트부터 레이아웃을 강제로 계산해 rect를 확정한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);

        // 모든 RectTransform의 rect를 루트 로컬 좌표로 구한다 (GetComponentsInChildren은 계층 DFS 순서)
        var all = root.GetComponentsInChildren<RectTransform>(true);
        var rects = new Dictionary<RectTransform, Rect>();
        foreach (var rt in all) rects[rt] = LocalRect(rt, rootRt);

        // 검사 대상: 활성 상태이고 보이는 그래픽이 있는 RectTransform
        var elems = all.Where(IsElement).ToList();

        var findings = new List<Finding>();
        CheckOutOfParent(elems, rects, rootRt, findings);
        CheckEdgePadding(elems, rects, rootRt, minPadding, findings);
        CheckText(elems, rootRt, findings);
        CheckSiblings(elems, rects, rootRt, findings);

        // 규칙 순서로 정렬하되 같은 규칙 안에서는 검사(계층) 순서를 유지한다 (OrderBy는 안정 정렬)
        return findings.OrderBy(f => Array.IndexOf(RULES, f.Rule)).ToList();
    }

    // 1. OUT_OF_PARENT: 보이는 요소가 부모 rect를 허용 오차 이상 벗어나는지 검사
    static void CheckOutOfParent(List<RectTransform> elems, Dictionary<RectTransform, Rect> rects, RectTransform rootRt, List<Finding> list)
    {
        foreach (var rt in elems)
        {
            var parentRt = rt.parent as RectTransform;
            // ScrollRect 콘텐츠(및 그 하위)는 뷰포트보다 커도 정상이므로 제외
            if (rt == rootRt || parentRt == null || IsDecor(rt) || IsInsideScrollContent(rt, rootRt)) continue;

            Rect r = rects[rt], p = rects[parentRt];
            float left = p.xMin - r.xMin, right = r.xMax - p.xMax;
            float bottom = p.yMin - r.yMin, top = r.yMax - p.yMax;
            var sides = new List<string>();
            if (left > OUT_TOL) sides.Add("왼쪽 " + Px(left) + "px");
            if (right > OUT_TOL) sides.Add("오른쪽 " + Px(right) + "px");
            if (bottom > OUT_TOL) sides.Add("아래 " + Px(bottom) + "px");
            if (top > OUT_TOL) sides.Add("위 " + Px(top) + "px");
            if (sides.Count == 0) continue;

            Add(list, RULE_OUT, PathOf(rt, rootRt),
                "부모(" + PathOf(parentRt, rootRt) + ") 밖으로 " + string.Join(", ", sides) + " 초과");
        }
    }

    // 2. EDGE_PADDING: 부동 창 안 요소가 창 좌·우·아래 가장자리에 너무 붙었는지 검사 (제목 바는 제외)
    static void CheckEdgePadding(List<RectTransform> elems, Dictionary<RectTransform, Rect> rects, RectTransform rootRt, int minPadding, List<Finding> list)
    {
        foreach (var rt in elems)
        {
            RectTransform win = FindWindowRoot(rt, rootRt);
            string path = PathOf(rt, rootRt);
            if (win == null || path.Contains("TitleBar") || IsDecor(rt) || IsInsideScrollContent(rt, rootRt)) continue;

            Rect w = rects[win], r = rects[rt];
            // 창 전체를 덮는 배경 채움(네 변이 모두 1px 이내 일치)은 여백 대상이 아님
            bool fullBleed = Mathf.Abs(r.xMin - w.xMin) <= 1f && Mathf.Abs(r.xMax - w.xMax) <= 1f
                && Mathf.Abs(r.yMin - w.yMin) <= 1f && Mathf.Abs(r.yMax - w.yMax) <= 1f;
            if (fullBleed) continue;

            float left = r.xMin - w.xMin, right = w.xMax - r.xMax, bottom = r.yMin - w.yMin;
            // 배율 때문에 생기는 0.5px 미만 반올림 차이는 여백 부족으로 보지 않는다.
            float need = minPadding - 0.5f;
            if (left < need || right < need || bottom < need)
            {
                Add(list, RULE_EDGE, path,
                    "창 가장자리까지 왼쪽 " + Px(left) + "px, 오른쪽 " + Px(right) + "px, 아래 " + Px(bottom) + "px (기준 " + minPadding + "px)");
            }
        }
    }

    // 3. TEXT_OVERFLOW: TMP 텍스트가 상자를 넘치는지 검사 (ForceMeshUpdate 후 측정)
    static void CheckText(List<RectTransform> elems, RectTransform rootRt, List<Finding> list)
    {
        foreach (var rt in elems)
        {
            var tmp = rt.GetComponent<TMP_Text>();
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;

            tmp.ForceMeshUpdate();
            string path = PathOf(rt, rootRt);
            float boxW = rt.rect.width, boxH = rt.rect.height;

            if (tmp.isTextOverflowing)
                Add(list, RULE_TEXT, path, "isTextOverflowing = true (상자 " + Px(boxW) + "x" + Px(boxH) + "px)");
            if (!tmp.enableWordWrapping && tmp.preferredWidth > boxW + TEXT_TOL)
                Add(list, RULE_TEXT, path, "줄바꿈 꺼짐: 필요 가로 " + Px(tmp.preferredWidth) + "px > 상자 " + Px(boxW) + "px");
            // 자동 크기 글자는 선호 높이를 최대 글자 크기로 계산한다. 실제로 줄여 맞췄는지는 isTextOverflowing으로만 본다.
            if (!tmp.enableAutoSizing && tmp.preferredHeight > boxH + TEXT_TOL)
                Add(list, RULE_TEXT, path, "필요 세로 " + Px(tmp.preferredHeight) + "px > 상자 " + Px(boxH) + "px");
        }
    }

    // 4. SIBLING_OVERLAP, 5. ALIGN: 같은 부모를 가진 형제끼리 비교 (인덱스가 낮을수록 아래에 그려짐)
    static void CheckSiblings(List<RectTransform> elems, Dictionary<RectTransform, Rect> rects, RectTransform rootRt, List<Finding> list)
    {
        foreach (var group in elems.GroupBy(e => e.parent))
        {
            var kids = group.ToList();
            // 형제가 매우 많은 묶음(지도 길·점 같은 그림 요소)은 칸 단위 배치가 아니므로 건너뛴다.
            // 비교가 형제 수의 제곱~세제곱으로 늘어 에디터가 멈춘 적이 있다.
            if (kids.Count > MAX_SIBLINGS) continue;
            for (int i = 0; i < kids.Count; i++)
            {
                for (int j = i + 1; j < kids.Count; j++)
                {
                    RectTransform a = kids[i], b = kids[j];
                    Rect ra = rects[a], rb = rects[b];
                    float ox = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
                    float oy = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
                    if (ox <= OVERLAP_TOL || oy <= OVERLAP_TOL) continue;
                    // 장식끼리 또는 장식 위에 놓인 요소는 의도된 겹침이다. 단, 칸 안 글자 둘(신선도·가격)은 서로 겹치면 안 된다.
                    bool slotTexts = a.GetComponentInChildren<TMP_Text>() && b.GetComponentInChildren<TMP_Text>()
                        && a.parent != null && IsSlotName(a.parent.name);
                    if ((IsDecor(a) || IsDecor(b)) && !slotTexts) continue;
                    // 지도 선로처럼 움직이는 내용 안의 그림은 서로 이어 그린다.
                    if (IsInsideScrollContent(a, rootRt) && IsInsideScrollContent(b, rootRt) && !a.GetComponent<TMP_Text>() && !b.GetComponent<TMP_Text>()) continue;

                    // 무시 규칙: 아래(인덱스 낮음)가 순수 배경이고 위 요소를 완전히 포함하면 제외
                    bool inside = ra.xMin <= rb.xMin && rb.xMax <= ra.xMax && ra.yMin <= rb.yMin && rb.yMax <= ra.yMax;
                    if (inside && IsPureBackground(a)) continue;

                    Add(list, RULE_OVERLAP, PathOf(a, rootRt) + " vs " + PathOf(b, rootRt),
                        "겹침 가로 " + Px(ox) + "px, 세로 " + Px(oy) + "px");
                }
            }
            if (kids.Count >= 3) CheckAlign(kids, rects, rootRt, list);
        }
    }

    // 세로 열 정렬: x 범위가 2개 이상의 형제와 겹치는 요소들의 왼쪽 가장자리를 최빈값과 비교한다.
    static void CheckAlign(List<RectTransform> kids, Dictionary<RectTransform, Rect> rects, RectTransform rootRt, List<Finding> list)
    {
        var column = kids.Where(a => !IsDecor(a) && !IsInsideScrollContent(a, rootRt) && kids.Count(b => b != a && XOverlaps(rects[a], rects[b])) >= 2).ToList();
        if (column.Count < 3) return;

        // 최빈 왼쪽 가장자리(동률이면 작은 값), 정수 px로 반올림해 비교
        int mode = column.Select(a => Mathf.RoundToInt(rects[a].xMin))
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        // 가운데 맞춤 열(폭이 다른 요소를 같은 중심에 둔 경우)은 왼쪽이 달라도 정렬된 것이다.
        int centerMode = column.Select(a => Mathf.RoundToInt(rects[a].center.x))
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        foreach (var a in column)
        {
            int left = Mathf.RoundToInt(rects[a].xMin);
            int diff = Math.Abs(left - mode);
            if (Math.Abs(Mathf.RoundToInt(rects[a].center.x) - centerMode) <= 1) continue;
            if (diff >= 1 && diff <= NEAR_MISS_MAX)
                Add(list, RULE_ALIGN, PathOf(a, rootRt), "왼쪽 " + left + "px, 열의 최빈 왼쪽 " + mode + "px와 " + diff + "px 차이");
        }
    }

    // 보고서: 요약 표 + 프리팹별 상세 표
    static string BuildReport(List<PrefabResult> results, int minPadding)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Relayout 레이아웃 감사 보고서");
        sb.AppendLine();
        sb.AppendLine("- 생성 시각: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sb.AppendLine("- 최소 여백(EDGE_PADDING): " + minPadding + "px, 좌표 단위: 프리팹 루트 로컬 px");
        sb.AppendLine();

        sb.AppendLine("## 요약");
        sb.AppendLine();
        sb.AppendLine("| 프리팹 | " + string.Join(" | ", RULES) + " | 오류 |");
        sb.AppendLine("|---|" + string.Concat(RULES.Select(_ => "---:|")) + "---|");
        foreach (var r in results)
        {
            var counts = RULES.Select(rule => r.Findings.Count(f => f.Rule == rule).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("| " + r.Name + " | " + string.Join(" | ", counts) + " | " + (r.Error ?? "-") + " |");
        }
        sb.AppendLine();

        foreach (var r in results)
        {
            sb.AppendLine("## " + r.Name);
            sb.AppendLine();
            if (r.Error != null) sb.AppendLine("오류: " + r.Error);
            else if (r.Findings.Count == 0) sb.AppendLine("문제 없음.");
            else
            {
                sb.AppendLine("| rule | object path | details |");
                sb.AppendLine("|---|---|---|");
                // 표 셀 안의 파이프 문자는 깨지지 않도록 '/'로 바꾼다
                foreach (var f in r.Findings)
                    sb.AppendLine("| " + f.Rule + " | " + f.Path.Replace("|", "/") + " | " + f.Details.Replace("|", "/") + " |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // 일부러 겹치거나 부모 밖으로 나가게 만든 장식 요소. 겹침·부모 밖·여백 검사에서 뺀다(글자 넘침은 그대로 검사).
    // - 보드 칸 내부(PlacedSlot_/GhostSlot_의 바탕 채움·테두리·그림·가격 칩): 칸 경계를 넘어 이웃 칸과 이어 그린다.
    // - 선로 침목·레일·연결기, 마을 도로·광장, 시각 막대 표시: 배경 장식으로 위아래에 겹쳐 그린다.
    static readonly string[] DECOR_PREFIXES = { "Sleeper_", "RailLine", "Coupler", "Road", "Plaza", "Tick_", "Marker", "ProgressA", "ProgressB" };

    static bool IsDecor(Transform t)
    {
        foreach (string p in DECOR_PREFIXES)
            if (t.name.StartsWith(p, StringComparison.Ordinal)) return true;
        // 입력 칸의 안내 문구(Placeholder)와 입력 글자(Text)는 같은 자리에 겹쳐 두고 하나만 보인다.
        if (t.parent != null && t.parent.name == "TextArea") return true;
        return t.parent != null && IsSlotName(t.parent.name);
    }

    static bool IsSlotName(string _name)
    {
        return _name.StartsWith("PlacedSlot_", StringComparison.Ordinal) || _name.StartsWith("GhostSlot_", StringComparison.Ordinal);
    }

    // 검사 대상: 활성 상태, 이름에 SubMesh가 없고, 보이는 Image 또는 텍스트가 있는 요소
    static bool IsElement(RectTransform rt)
    {
        if (!rt.gameObject.activeInHierarchy || rt.name.Contains("SubMesh")) return false;
        var img = rt.GetComponent<Image>();
        if (img != null && img.enabled && img.color.a > ALPHA_MIN) return true;
        var tmp = rt.GetComponent<TMP_Text>();
        return tmp != null && tmp.enabled && !string.IsNullOrEmpty(tmp.text);
    }

    // 순수 배경: 보이는 Image이고 하위(자신 포함)에 텍스트가 없음
    static bool IsPureBackground(RectTransform rt)
    {
        var img = rt.GetComponent<Image>();
        if (img == null || !img.enabled || img.color.a <= ALPHA_MIN) return false;
        return !rt.GetComponentsInChildren<TMP_Text>(false).Any(t => !string.IsNullOrEmpty(t.text));
    }

    // 월드 코너 4개를 루트 로컬 좌표로 바꾼 축 정렬 rect
    static Rect LocalRect(RectTransform rt, RectTransform rootRt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var pts = corners.Select(w => rootRt.InverseTransformPoint(w)).ToList();
        return Rect.MinMaxRect(pts.Min(p => p.x), pts.Min(p => p.y), pts.Max(p => p.x), pts.Max(p => p.y));
    }

    // t가 ScrollRect의 content이거나 그 하위이면 true (루트까지 검사)
    // 지도처럼 끌어서 움직이는 내용(MapContent)도 보이는 영역보다 큰 것이 정상이므로 같이 취급한다.
    static bool IsInsideScrollContent(Transform t, Transform rootT)
    {
        for (Transform c = t; c != null; c = c.parent)
        {
            if (c != t && c.name == "MapContent") return true;
            // ScrollRect의 content는 보통 Viewport 아래에 있어 바로 위 부모가 ScrollRect가 아니다. 조상 전체에서 찾는다.
            var sr = c.GetComponentInParent<ScrollRect>(true);
            if (sr != null && sr.content == c) return true;
            if (c == rootT) break;
        }
        return false;
    }

    // 가장 가까운 FloatingWindow 조상(자기 자신 제외, 루트까지). 없으면 null
    // 타입 이름으로 판별해 Editor 어셈블리의 참조 여부에 기대지 않는다
    static RectTransform FindWindowRoot(Transform t, Transform rootT)
    {
        for (Transform c = t.parent; c != null; c = c.parent)
        {
            if (c.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "FloatingWindow"))
                return c as RectTransform;
            if (c == rootT) break;
        }
        return null;
    }

    static bool XOverlaps(Rect a, Rect b)
    {
        return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0f;
    }

    // 프리팹 루트부터의 경로 (루트 자신은 루트 이름)
    static string PathOf(Transform t, Transform rootT)
    {
        if (t == rootT) return rootT.name;
        var parts = new List<string>();
        for (Transform c = t; c != null && c != rootT; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static void Add(List<Finding> list, string rule, string path, string details)
    {
        list.Add(new Finding { Rule = rule, Path = path, Details = details });
    }

    static string Px(float v)
    {
        return Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);
    }
}
