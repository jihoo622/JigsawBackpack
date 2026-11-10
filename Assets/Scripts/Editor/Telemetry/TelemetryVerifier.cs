using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 가장 최근 분석 로그 파일의 계약을 검사한다. 게임을 실행하거나 씬을 바꾸지 않고 파일만 읽는다.
// 검사 항목: sessionId 단수 / sequence 1씩 연속·중복 없음 / session_start·session_end 경계 /
//            summaryId별 revision 증가 / time 감소 없음 / 파싱 실패 줄
// 파일 계약만 본다. 보상·피해처럼 값의 정확성은 실제 플레이 확인이 필요하다(Docs/Management/TELEMETRY.md).
public static class TelemetryVerifier
{
    public const string MENU = "Tools/MyJG/Verify Telemetry File";
    public const string REVEAL_MENU = "Tools/MyJG/Reveal Telemetry Folder";

    [MenuItem(MENU)]
    static void VerifyMenuItem()
    {
        string report = Verify();
        Debug.Log(report);
        EditorUtility.DisplayDialog("분석 로그 검증", report, "확인");
    }

    [MenuItem(REVEAL_MENU)]
    static void RevealFolder()
    {
        Directory.CreateDirectory(TelemetryLog.FolderPath);
        EditorUtility.RevealInFinder(TelemetryLog.FolderPath + Path.DirectorySeparatorChar);
    }

    public static string Verify()
    {
        string path = NewestFile();
        if (string.IsNullOrEmpty(path))
        {
            return "검사할 분석 파일이 없습니다.\n  ! Play Mode를 한 번 실행하면 " + TelemetryLog.FolderPath + "에 파일이 생긴다.";
        }

        var issues = new List<string>();
        var counts = new SortedList<string, int>(StringComparer.Ordinal);
        var revisions = new Dictionary<string, int>(StringComparer.Ordinal);
        string sessionId = Path.GetFileNameWithoutExtension(path);

        long expected = 0L;
        long lines = 0L;
        int unparsable = 0;
        int sessionEnds = 0;
        double previousTime = double.MinValue;
        string firstSessionId = null;

        foreach (string raw in File.ReadLines(path))
        {
            lines++;
            if (string.IsNullOrWhiteSpace(raw))
            {
                issues.Add($"{lines}째 줄이 비어 있습니다.");
                continue;
            }
            TelemetryLine line = null;
            try
            {
                line = JsonUtility.FromJson<TelemetryLine>(raw);
            }
            catch (Exception error)
            {
                issues.Add($"{lines}째 줄 파싱 실패: {error.Message}");
            }
            if (line == null || string.IsNullOrEmpty(line.eventName))
            {
                unparsable++;
                continue;
            }

            if (firstSessionId == null)
            {
                firstSessionId = line.sessionId;
                if (firstSessionId != sessionId)
                {
                    issues.Add($"파일 이름({sessionId})과 첫 레코드의 sessionId({line.sessionId})이 다릅니다.");
                }
            }
            else if (line.sessionId != firstSessionId)
            {
                issues.Add($"{lines}째 줄({line.eventName})의 sessionId가 파일 안의 다른 세션({line.sessionId})을 가리킵니다.");
            }

            expected++;
            if (line.sequence != expected)
            {
                issues.Add($"{lines}째 줄({line.eventName}) sequence={line.sequence}, 기대 {expected}(누락 또는 중복 기록).");
            }
            if (line.time < previousTime)
            {
                issues.Add($"{lines}째 줄({line.eventName}) 시간이 이전 레코드보다 앞섭니다({line.time} < {previousTime}).");
            }
            previousTime = line.time;

            if (line.eventName == TelemetryLog.SESSION_START_EVENT && lines != 1L)
            {
                issues.Add("session_start가 첫 레코드가 아닙니다. 세션이 두 번 열린 것일 수 있습니다.");
            }
            if (line.eventName == TelemetryLog.SESSION_END_EVENT)
            {
                sessionEnds++;
                if (sessionEnds > 1)
                {
                    issues.Add("session_end가 두 번 이상 있습니다. 이중 종료를 확인하세요.");
                }
            }
            if (line.eventName == TelemetryLog.SUMMARY_EVENT)
            {
                revisions.TryGetValue(line.summaryId, out int seen);
                if (line.revision <= seen)
                {
                    issues.Add($"summaryId '{line.summaryId}' revision={line.revision}이 이전 값({seen}) 이하입니다.");
                }
                revisions[line.summaryId] = Math.Max(seen, line.revision);
            }

            counts.TryGetValue(line.eventName, out int count);
            counts[line.eventName] = count + 1;
        }

        if (sessionEnds == 0)
        {
            issues.Add("session_end 기록이 없습니다. 종료 훅이 실행되지 않았거나 강제 종료입니다.");
        }

        var report = new StringBuilder();
        report.Append(issues.Count == 0 ? "분석 로그 검증 통과\n" : "분석 로그 문제 발견\n");
        report.Append("  · 파일: ").Append(path).Append('\n');
        report.Append("  · 줄 ").Append(lines).Append(" / 이벤트 ").Append(counts.Count).Append("종류");
        foreach (KeyValuePair<string, int> pair in counts)
        {
            report.Append(" / ").Append(pair.Key).Append(' ').Append(pair.Value);
        }
        report.Append('\n');
        report.Append("  · 요약 summaryId ").Append(revisions.Count).Append("개(가장 큰 revision만 채택하면 됩니다)\n");
        if (unparsable != 0)
        {
            report.Append("  · 파싱 실패 줄 ").Append(unparsable).Append("개\n");
        }
        foreach (string line in issues)
        {
            report.Append("  ! ").Append(line).Append('\n');
        }
        report.Append("  · 이 검사는 파일 계약만 확인합니다. 값의 정확성은 플레이로 확인합니다.");
        return report.ToString().TrimEnd();
    }

    // 가장 최근에 수정된 분석 파일. Play Mode를 막 끝낸 파일이 늘 마지막이다.
    public static string NewestFile()
    {
        string folder = TelemetryLog.FolderPath;
        if (!Directory.Exists(folder))
        {
            return string.Empty;
        }
        string newest = string.Empty;
        DateTime last = DateTime.MinValue;
        foreach (string path in Directory.GetFiles(folder, "*.jsonl"))
        {
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (stamp <= last)
            {
                continue;
            }
            last = stamp;
            newest = path;
        }
        return newest;
    }
}
