using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 현재 테이블로 경제를 플레이 없이 확인한다. 계산은 모두 EconomyProbe·EconomySimulation(실제 게임 코드)을 거친다.
// 품목: 최저 구매처·최고 판매처 / 노선: 칸당 차익·1량 시간당 이익 / 시뮬레이션: 봇 N판의 일자별 자금과 지출 항목.
public sealed class EconomyAnalyzerWindow : EditorWindow
{
    const string MENU = "Tools/MyJG/Economy Analyzer";
    const string EXPORT_FOLDER = "Logs/Economy";
    const float CHART_HEIGHT = 220f;
    static readonly string[] TABS = { "품목", "노선", "시뮬레이션" };
    static readonly string[] ROUTE_SORTS = { "1량 시간당 이익", "칸당 차익", "출발 도시" };

    int tab;
    int routeSort;
    bool chartAssets;
    Vector2 scroll;
    string error;
    List<EconomyItemRow> itemRows;
    List<EconomyRouteRow> routeRows;
    EconomySimulationReport report;
    readonly EconomySimulationSettings settings = new EconomySimulationSettings();

    [MenuItem(MENU)]
    static void Open()
    {
        GetWindow<EconomyAnalyzerWindow>("경제 분석");
    }

    void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, TABS);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("플레이 중에는 실행하지 않는다. 편집 모드에서 현재 테이블로 계산한다.", MessageType.Info);
            return;
        }
        if (!string.IsNullOrEmpty(error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case 0:
                DrawItems();
                break;
            case 1:
                DrawRoutes();
                break;
            default:
                DrawSimulation();
                break;
        }
        EditorGUILayout.EndScrollView();
    }

    bool Guard(Action _action)
    {
        error = null;
        try
        {
            _action();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    #region 품목

    void DrawItems()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("계산", GUILayout.Width(80)))
            {
                Guard(() => itemRows = EconomyProbe.BuildItemRows());
            }
            using (new EditorGUI.DisabledScope(itemRows == null))
            {
                if (GUILayout.Button("CSV 내보내기", GUILayout.Width(100)))
                {
                    Guard(ExportItems);
                }
            }
        }
        EditorGUILayout.HelpBox("기본 가치 기준이다. 시너지·신선도·동물 효과로 바뀌는 실제 가치는 시뮬레이션 탭에서 본다. 구매처는 시장 후보(고정·추첨)에 오르는 도시만 센다.", MessageType.None);
        if (itemRows == null)
        {
            return;
        }

        Row(true, ("품목", 140), ("기본", 40), ("칸", 30), ("입고", 40), ("최저 구매처", 160), ("최고 판매처", 160), ("차익", 50), ("칸당", 50));
        foreach (var row in itemRows.OrderByDescending(_row => _row.MarginPerCell).ThenBy(_row => _row.ItemId, StringComparer.Ordinal))
        {
            Row(false,
                (row.DisplayName + (row.ExcludeFromMarket ? " *" : ""), 140),
                (row.BaseValue.ToString(), 40),
                (row.Cells.ToString(), 30),
                (row.StockedCityCount.ToString(), 40),
                (row.CheapestBuyCityId == null ? "-" : $"{EconomyProbe.CityName(row.CheapestBuyCityId)} {row.CheapestBuyPrice}", 160),
                (row.BestSellCityId == null ? "-" : $"{EconomyProbe.CityName(row.BestSellCityId)} {row.BestSellPrice}", 160),
                (row.HasMargin ? row.Margin.ToString() : "-", 50),
                (row.HasMargin ? row.MarginPerCell.ToString("0.0") : "-", 50));
        }
        EditorGUILayout.LabelField("* 시장 제외 품목", EditorStyles.miniLabel);
    }

    void ExportItems()
    {
        var csv = new StringBuilder("itemId,name,baseValue,cells,stockedCities,excludeFromMarket,cheapestBuyCity,cheapestBuyPrice,bestSellCity,bestSellPrice,margin,marginPerCell\n");
        foreach (var row in itemRows)
        {
            csv.AppendLine(string.Join(",", row.ItemId, Csv(row.DisplayName), row.BaseValue, row.Cells, row.StockedCityCount, row.ExcludeFromMarket,
                row.CheapestBuyCityId, row.CheapestBuyCityId == null ? "" : row.CheapestBuyPrice.ToString(),
                row.BestSellCityId, row.BestSellCityId == null ? "" : row.BestSellPrice.ToString(),
                row.Margin, row.MarginPerCell.ToString("0.###")));
        }
        Reveal(WriteExport("items", ("items.csv", csv.ToString())));
    }

    #endregion

    #region 노선

    void DrawRoutes()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("계산", GUILayout.Width(80)))
            {
                Guard(() => routeRows = EconomyProbe.BuildRouteRows());
            }
            using (new EditorGUI.DisabledScope(routeRows == null))
            {
                if (GUILayout.Button("CSV 내보내기", GUILayout.Width(100)))
                {
                    Guard(ExportRoutes);
                }
            }
            GUILayout.Label("정렬", GUILayout.Width(30));
            routeSort = EditorGUILayout.Popup(routeSort, ROUTE_SORTS, GUILayout.Width(120));
        }
        EditorGUILayout.HelpBox("출발 시장 후보 중 칸당 차익이 가장 큰 품목 하나로 객차 1량을 채운다고 가정한다(* 고정 입고). 1량 시간당 이익 = (칸당 차익 × 객차 칸 수 − 만차 운임) ÷ 이동 시간.", MessageType.None);
        if (routeRows == null)
        {
            return;
        }

        IEnumerable<EconomyRouteRow> rows = routeSort == 0 ? routeRows.OrderByDescending(_row => _row.WagonProfitPerHour)
            : routeSort == 1 ? routeRows.OrderByDescending(_row => _row.BestProfitPerCell)
            : routeRows.OrderBy(_row => _row.FromCityId, StringComparer.Ordinal).ThenBy(_row => _row.ToCityId, StringComparer.Ordinal);
        Row(true, ("노선", 220), ("시간", 50), ("운임 빈/만", 70), ("최적 품목", 130), ("매입→매도", 80), ("칸당", 50), ("1량/시간", 60));
        foreach (var row in rows)
        {
            Row(false,
                ($"{EconomyProbe.CityName(row.FromCityId)} → {EconomyProbe.CityName(row.ToCityId)}", 220),
                (Hours(row.TravelMinutes), 50),
                ($"{row.EmptyCost}/{row.FullWagonCost}", 70),
                (row.BestItemId == null ? "-" : EconomyProbe.ItemName(row.BestItemId) + (row.BestItemFixed ? " *" : ""), 130),
                (row.BestItemId == null ? "-" : $"{row.BestBuy}→{row.BestSell}", 80),
                (row.BestProfitPerCell.ToString("0.0"), 50),
                (row.WagonProfitPerHour.ToString("0.0"), 60));
        }
    }

    void ExportRoutes()
    {
        var csv = new StringBuilder("fromCity,toCity,travelMinutes,emptyCost,fullWagonCost,bestItem,bestItemFixed,buy,sell,profitPerCell,wagonProfitPerHour\n");
        foreach (var row in routeRows)
        {
            csv.AppendLine(string.Join(",", row.FromCityId, row.ToCityId, row.TravelMinutes, row.EmptyCost, row.FullWagonCost,
                row.BestItemId, row.BestItemFixed, row.BestBuy, row.BestSell,
                row.BestProfitPerCell.ToString("0.###"), row.WagonProfitPerHour.ToString("0.###")));
        }
        Reveal(WriteExport("routes", ("routes.csv", csv.ToString())));
    }

    #endregion

    #region 시뮬레이션

    void DrawSimulation()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.runs = Mathf.Clamp(EditorGUILayout.IntField("판 수", settings.runs), 1, 10000);
            settings.seed = EditorGUILayout.IntField("시드", settings.seed);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.policy = (EconomyBotPolicy)EditorGUILayout.EnumPopup("봇", settings.policy);
            settings.repayEarly = EditorGUILayout.ToggleLeft("상환 가능하면 바로 귀환", settings.repayEarly);
        }
        settings.freezeFreshness = EditorGUILayout.ToggleLeft("실험: 신선도 고정(이번 실행만 유통기한 없음, 테이블 파일은 그대로)", settings.freezeFreshness);
        settings.disableSynergy = EditorGUILayout.ToggleLeft("실험: 시너지 끄기(이번 실행만 배율 100%·배설·번식 없음)", settings.disableSynergy);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("실행", GUILayout.Width(80)))
            {
                RunSimulation();
            }
            using (new EditorGUI.DisabledScope(report == null))
            {
                if (GUILayout.Button("CSV 내보내기", GUILayout.Width(100)))
                {
                    Guard(ExportSimulation);
                }
            }
        }
        EditorGUILayout.HelpBox("Planner: 2구간 앞까지의 경로 중 (보유 화물 차익 + 살 화물 차익 − 운임) ÷ 시간이 가장 큰 경로의 첫 구간으로 간다. Greedy: 같은 기준으로 바로 옆 도시만 본다. Random: 무작위 거래·이동(하한 기준). 모두 기한 안에 상환 도시로 돌아가며 배치 최적화·도축·개조는 쓰지 않는다.", MessageType.None);
        if (report == null)
        {
            return;
        }

        DrawSummary();
        DrawChart();
        DrawDays();
        DrawFlows();
    }

    void RunSimulation()
    {
        EconomySimulationReport result = null;
        var snapshot = JsonUtility.FromJson<EconomySimulationSettings>(JsonUtility.ToJson(settings));
        bool done = Guard(() => result = EconomySimulation.Run(snapshot, _finished =>
            !EditorUtility.DisplayCancelableProgressBar("경제 시뮬레이션", $"{_finished}/{snapshot.runs}판", _finished / (float)snapshot.runs)));
        if (done)
        {
            report = result;
        }
    }

    void DrawSummary()
    {
        int total = report.Runs.Count;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"결과 ({report.Settings.policy}, {total}판, 시드 {report.Settings.seed}{(report.Settings.freezeFreshness ? ", 신선도 고정" : "")}{(report.Settings.disableSynergy ? ", 시너지 끔" : "")})", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("승리", Rate(report.Count(EconomyRunOutcome.Won), total));
        EditorGUILayout.LabelField("기한 초과", Rate(report.Count(EconomyRunOutcome.Lost), total));
        EditorGUILayout.LabelField("상환 도시에서 자금 부족", Rate(report.Count(EconomyRunOutcome.ShortOfFunds), total));
        EditorGUILayout.LabelField("이동 불가", Rate(report.Count(EconomyRunOutcome.Stuck) + report.Count(EconomyRunOutcome.StepLimit), total));
        EditorGUILayout.LabelField("상환 시점 p10/p50/p90",
            float.IsNaN(report.ClearHoursP50) ? "-" : $"{report.ClearHoursP10:0}h / {report.ClearHoursP50:0}h / {report.ClearHoursP90:0}h");
        EditorGUILayout.LabelField("판당 평균 매입 / 매출 / 처분", $"{report.Average(_run => _run.Bought):0} / {report.Average(_run => _run.Sold):0} / {report.Average(_run => _run.Disposed):0}");
        EditorGUILayout.LabelField("판당 평균 운임 / 증차 / 상환", $"{report.Average(_run => _run.TravelCost):0} / {report.Average(_run => _run.WagonCost):0} / {report.Average(_run => _run.Repaid):0}");
        EditorGUILayout.LabelField("판당 평균 이동 / 최종 객차", $"{report.Average(_run => _run.Travels):0.0} / {report.Average(_run => _run.Wagons):0.0}");
    }

    void DrawChart()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("일자별 " + (chartAssets ? "자산(자금+화물 가치)" : "자금") + " p10 / p50 / p90", EditorStyles.boldLabel);
            chartAssets = EditorGUILayout.ToggleLeft("자산으로 보기", chartAssets, GUILayout.Width(110));
        }
        var rect = GUILayoutUtility.GetRect(10, CHART_HEIGHT, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        var days = report.Days.Where(_day => _day.Active > 0).ToArray();
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
        if (days.Length < 2)
        {
            return;
        }

        long clearCost = TableManager.Instance.PuzzleTrade.gameClearCost;
        long max = Math.Max(clearCost, days.Max(_day => chartAssets ? _day.AssetsP90 : _day.FundsP90));
        long min = Math.Min(0, days.Min(_day => chartAssets ? _day.AssetsP10 : _day.FundsP10));
        var plot = new Rect(rect.x + 44, rect.y + 8, rect.width - 52, rect.height - 26);
        int lastDay = report.Days.Length - 1;
        Vector3 Point(int _day, long _value)
        {
            float x = plot.x + plot.width * _day / Mathf.Max(1, lastDay);
            float y = plot.yMax - plot.height * (_value - min) / Mathf.Max(1f, max - min);
            return new Vector3(x, y);
        }

        Handles.color = new Color(0.9f, 0.4f, 0.3f, 0.8f);
        Handles.DrawDottedLine(Point(0, clearCost), Point(lastDay, clearCost), 3f);
        GUI.Label(new Rect(rect.x + 2, Point(0, clearCost).y - 8, 44, 16), clearCost.ToString(), EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 2, plot.y - 4, 44, 16), max.ToString(), EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 2, plot.yMax - 10, 44, 16), min.ToString(), EditorStyles.miniLabel);
        for (int day = 0; day <= lastDay; day++)
        {
            GUI.Label(new Rect(Point(day, min).x - 8, plot.yMax + 2, 24, 16), day.ToString(), EditorStyles.miniLabel);
        }

        DrawSeries(days.Select(_day => Point(_day.Day, chartAssets ? _day.AssetsP10 : _day.FundsP10)), new Color(0.5f, 0.6f, 0.9f, 0.6f), 1.5f);
        DrawSeries(days.Select(_day => Point(_day.Day, chartAssets ? _day.AssetsP90 : _day.FundsP90)), new Color(0.5f, 0.6f, 0.9f, 0.6f), 1.5f);
        DrawSeries(days.Select(_day => Point(_day.Day, chartAssets ? _day.AssetsP50 : _day.FundsP50)), new Color(0.4f, 0.8f, 1f), 3f);
    }

    static void DrawSeries(IEnumerable<Vector3> _points, Color _color, float _width)
    {
        Handles.color = _color;
        Handles.DrawAAPolyLine(_width, _points.ToArray());
    }

    void DrawDays()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("일자별 (24시간 × 일 시점, 조기 상환한 판은 상환 후 제외)", EditorStyles.boldLabel);
        Row(true, ("일", 30), ("진행 판", 50), ("자금 p10", 70), ("자금 p50", 70), ("자금 p90", 70), ("자산 p50", 70));
        foreach (var day in report.Days)
        {
            Row(false, (day.Day.ToString(), 30), (day.Active.ToString(), 50), (day.FundsP10.ToString(), 70), (day.FundsP50.ToString(), 70),
                (day.FundsP90.ToString(), 70), (day.AssetsP50.ToString(), 70));
        }
    }

    void DrawFlows()
    {
        int total = Math.Max(1, report.Runs.Count);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("품목별 판당 평균 (순이익 순)", EditorStyles.boldLabel);
        Row(true, ("품목", 140), ("구매 수", 60), ("매입", 70), ("판매 수", 60), ("매출", 70), ("순이익", 70));
        foreach (var pair in report.ItemFlows.OrderByDescending(_pair => _pair.Value.Earned - _pair.Value.Spent))
        {
            var flow = pair.Value;
            Row(false, (EconomyProbe.ItemName(pair.Key), 140), ((flow.Purchases / (float)total).ToString("0.0"), 60),
                ((flow.Spent / (float)total).ToString("0"), 70), ((flow.Sales / (float)total).ToString("0.0"), 60),
                ((flow.Earned / (float)total).ToString("0"), 70), (((flow.Earned - flow.Spent) / (float)total).ToString("0"), 70));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("도시 방문 (판당 평균)", EditorStyles.boldLabel);
        foreach (var pair in report.CityVisits.OrderByDescending(_pair => _pair.Value).ThenBy(_pair => _pair.Key, StringComparer.Ordinal))
        {
            EditorGUILayout.LabelField(EconomyProbe.CityName(pair.Key), (pair.Value / (float)total).ToString("0.0"));
        }
    }

    void ExportSimulation()
    {
        var runs = new StringBuilder("run,stockSeed,outcome,endHours,finalFunds,wagons,travels,bought,sold,disposed,travelCost,wagonCost,repaid,path\n");
        var days = new StringBuilder("run,day,funds,assets\n");
        var events = new StringBuilder("run,minutes,city,kind,itemId,amount,fundsAfter\n");
        foreach (var run in report.Runs)
        {
            foreach (var entry in run.Events)
            {
                events.AppendLine(string.Join(",", run.Index, entry.Minutes, entry.CityId, entry.Kind, entry.ItemId, entry.Amount, entry.FundsAfter));
            }
            runs.AppendLine(string.Join(",", run.Index, run.StockSeed, run.Outcome, run.EndHours.ToString("0.##"), run.FinalFunds, run.Wagons,
                run.Travels, run.Bought, run.Sold, run.Disposed, run.TravelCost, run.WagonCost, run.Repaid, string.Join(">", run.Path)));
            for (int day = 0; day < run.ValidDays; day++)
            {
                days.AppendLine(string.Join(",", run.Index, day, run.DayFunds[day], run.DayAssets[day]));
            }
        }
        var summary = new StringBuilder("day,active,fundsP10,fundsP50,fundsP90,assetsP10,assetsP50,assetsP90\n");
        foreach (var day in report.Days)
        {
            summary.AppendLine(string.Join(",", day.Day, day.Active, day.FundsP10, day.FundsP50, day.FundsP90, day.AssetsP10, day.AssetsP50, day.AssetsP90));
        }
        var items = new StringBuilder("itemId,purchases,spent,sales,earned\n");
        foreach (var pair in report.ItemFlows)
        {
            items.AppendLine(string.Join(",", pair.Key, pair.Value.Purchases, pair.Value.Spent, pair.Value.Sales, pair.Value.Earned));
        }
        var settingsJson = JsonUtility.ToJson(report.Settings, true);
        Reveal(WriteExport("simulation", ("runs.csv", runs.ToString()), ("days.csv", days.ToString()), ("events.csv", events.ToString()),
            ("days_summary.csv", summary.ToString()), ("items.csv", items.ToString()), ("settings.json", settingsJson)));
    }

    #endregion

    static void Row(bool _header, params (string Text, float Width)[] _cells)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var style = _header ? EditorStyles.boldLabel : EditorStyles.label;
            foreach (var cell in _cells)
            {
                GUILayout.Label(cell.Text, style, GUILayout.Width(cell.Width));
            }
        }
    }

    static string Hours(long _minutes)
    {
        return $"{_minutes / PuzzleTimeManager.MINUTES_PER_HOUR}:{_minutes % PuzzleTimeManager.MINUTES_PER_HOUR:00}";
    }

    static string Rate(int _count, int _total)
    {
        return _total == 0 ? "-" : $"{_count} ({_count * 100f / _total:0.#}%)";
    }

    static string Csv(string _text)
    {
        return _text != null && (_text.Contains(",") || _text.Contains("\"")) ? "\"" + _text.Replace("\"", "\"\"") + "\"" : _text;
    }

    // 프로젝트 Logs 폴더(버전 관리 제외)에 실행 시각별로 쓴다. 엑셀이 한글을 읽도록 BOM을 붙인다.
    static string WriteExport(string _kind, params (string Name, string Content)[] _files)
    {
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), EXPORT_FOLDER, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + _kind);
        Directory.CreateDirectory(folder);
        foreach (var file in _files)
        {
            File.WriteAllText(Path.Combine(folder, file.Name), file.Content, new UTF8Encoding(true));
        }
        return folder;
    }

    static void Reveal(string _folder)
    {
        EditorUtility.RevealInFinder(_folder + Path.DirectorySeparatorChar);
    }
}
