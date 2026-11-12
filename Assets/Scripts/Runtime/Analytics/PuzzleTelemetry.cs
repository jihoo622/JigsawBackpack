using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System;
using UnityEngine;

public enum PuzzleActionKind
{
    Travel,
    Rest,
    WaitForDaylight,
    AdvanceTime,
    Place,
    QuickTransfer,
    AddWagon,
    Repay
}

[Serializable]
public sealed class PuzzleItemObservation : PuzzleItemLocation
{
    public string itemId;
    // JsonUtility는 decimal을 지원하지 않는다. 반올림 없이 invariant 문자열로 보존한다.
    public string actualValue;
    public int durability;
}

[Serializable]
public class PuzzleItemLocation
{
    public int instanceId;
    // train:0, train:1, ... 또는 market:<cityId>. 이름이나 배열 정렬에 의존하지 않는다.
    public string board;
    public int x, y, clockwiseTurns;
}

[Serializable]
public sealed class PuzzleItemValue
{
    public int instanceId;
    public string actualValue;
}

[Serializable]
public sealed class PuzzleItemDurability
{
    public int instanceId, durability;
}

[Serializable]
public sealed class PuzzleRunStartPayload
{
    public string rulesHash, cityId;
    public long funds;
    public int wagonCount;
    public PuzzleItemObservation[] items;
}

[Serializable]
public sealed class PuzzleActionAttempt
{
    public string destination;
    public int x, y, grabbedX, grabbedY, clockwiseTurns;
    public long requestedMinutes;
}

[Serializable]
public sealed class PuzzleActionPayload
{
    public string action, method, failure, targetCityId, cityId;
    public int instanceId;
    public int wagonDelta;
    public long elapsedMinutes, moneyDelta;
    public PuzzleActionAttempt attempt;
    public PuzzleItemObservation[] introduced;
    public int[] removed;
    public PuzzleItemLocation[] moved;
    public PuzzleItemValue[] values;
    public PuzzleItemDurability[] durability;
}

[Serializable]
public struct PuzzleRunEndPayload
{
    public string reason;
}

[Serializable]
public struct PuzzleRumorPayload
{
    public string targetCityId, tag;
    public long elapsedMinutes;
}

// 원본 관측값의 차이만 직렬화한다. 수익률·점유율·시너지 횟수 등 집계는 외부에서 계산한다.
public static partial class TelemetryPayload
{
    public sealed class PuzzleState
    {
        Dictionary<int, PuzzleItemObservation> previous = new Dictionary<int, PuzzleItemObservation>();
        long previousFunds;
        string previousCityId;
        int previousWagonCount;

        public PuzzleRunStartPayload Start(PuzzleInventoryService _session)
        {
            previous = Capture(_session);
            previousFunds = _session.Funds;
            previousCityId = _session.CurrentCityId;
            previousWagonCount = _session.WagonCount;
            return new PuzzleRunStartPayload
            {
                rulesHash = ArchiveRules(),
                cityId = _session.CurrentCityId,
                funds = _session.Funds,
                wagonCount = _session.WagonCount,
                items = previous.Values.OrderBy(_item => _item.instanceId).ToArray()
            };
        }

        public PuzzleActionPayload Action(GameEvents.PuzzleActionInfo _info)
        {
            var payload = new PuzzleActionPayload
            {
                action = ActionName(_info),
                method = _info.action == PuzzleActionKind.Place ? "drag"
                    : _info.action == PuzzleActionKind.QuickTransfer ? "quick_transfer" : null,
                failure = _info.failure,
                targetCityId = _info.targetCityId,
                cityId = _info.session.CurrentCityId != previousCityId ? _info.session.CurrentCityId : null,
                wagonDelta = _info.session.WagonCount - previousWagonCount,
                instanceId = _info.instanceId,
                elapsedMinutes = _info.session.ElapsedTime,
                moneyDelta = _info.session.Funds - previousFunds,
                introduced = Array.Empty<PuzzleItemObservation>(),
                removed = Array.Empty<int>(),
                moved = Array.Empty<PuzzleItemLocation>(),
                values = Array.Empty<PuzzleItemValue>(),
                durability = Array.Empty<PuzzleItemDurability>()
            };
            // 성공 이동의 요청 목적지가 실제 도착지와 같으면 같은 ID를 두 번 쓰지 않는다.
            if (_info.action == PuzzleActionKind.Travel && string.IsNullOrEmpty(_info.failure)
                && _info.targetCityId == payload.cityId)
            {
                payload.targetCityId = null;
            }
            if (!string.IsNullOrEmpty(_info.failure))
            {
                if (_info.action == PuzzleActionKind.Place || _info.action == PuzzleActionKind.QuickTransfer)
                {
                    payload.attempt = new PuzzleActionAttempt
                    {
                        destination = _info.destination,
                        x = _info.targetCell.x,
                        y = _info.targetCell.y,
                        grabbedX = _info.grabbedCell.x,
                        grabbedY = _info.grabbedCell.y,
                        clockwiseTurns = _info.clockwiseTurns
                    };
                }
                else if (_info.action == PuzzleActionKind.Rest || _info.action == PuzzleActionKind.WaitForDaylight
                    || _info.action == PuzzleActionKind.AdvanceTime)
                {
                    payload.attempt = new PuzzleActionAttempt
                    {
                        requestedMinutes = _info.requestedMinutes
                    };
                }
            }

            var current = Capture(_info.session);
            var introduced = new List<PuzzleItemObservation>();
            var moved = new List<PuzzleItemLocation>();
            var values = new List<PuzzleItemValue>();
            var durability = new List<PuzzleItemDurability>();
            foreach (var item in current.Values.OrderBy(_item => _item.instanceId))
            {
                if (!previous.TryGetValue(item.instanceId, out var old))
                {
                    introduced.Add(item);
                    continue;
                }
                if (item.board != old.board || item.x != old.x || item.y != old.y ||
                    item.clockwiseTurns != old.clockwiseTurns)
                {
                    moved.Add(new PuzzleItemLocation
                    {
                        instanceId = item.instanceId, board = item.board,
                        x = item.x, y = item.y, clockwiseTurns = item.clockwiseTurns
                    });
                }
                if (item.actualValue != old.actualValue)
                {
                    values.Add(new PuzzleItemValue
                    {
                        instanceId = item.instanceId,
                        actualValue = item.actualValue
                    });
                }
                if (item.durability != old.durability)
                {
                    durability.Add(new PuzzleItemDurability
                    {
                        instanceId = item.instanceId,
                        durability = item.durability
                    });
                }
            }
            // 도시 변화에 따른 이전 시장의 관측 종료는 cityId로 복원한다.
            payload.removed = previous.Values
                .Where(_item => !current.ContainsKey(_item.instanceId)
                    && !(_info.session.CurrentCityId != previousCityId && _item.board == "market:" + previousCityId))
                .Select(_item => _item.instanceId).OrderBy(_id => _id).ToArray();
            previous = current;
            previousFunds = _info.session.Funds;
            previousCityId = _info.session.CurrentCityId;
            previousWagonCount = _info.session.WagonCount;
            payload.introduced = introduced.ToArray();
            payload.moved = moved.ToArray();
            payload.values = values.ToArray();
            payload.durability = durability.ToArray();
            return payload;
        }

        static Dictionary<int, PuzzleItemObservation> Capture(PuzzleInventoryService _session)
        {
            var items = new Dictionary<int, PuzzleItemObservation>();
            for (int i = 0; i < _session.WagonCount; i++)
            {
                CaptureBoard(_session.TrainWagons[i], _session.BoardId(_session.TrainWagons[i]), items);
            }
            CaptureBoard(_session.Market, _session.BoardId(_session.Market), items);
            return items;
        }

        static void CaptureBoard(PuzzleInventory _board, string _id, Dictionary<int, PuzzleItemObservation> _items)
        {
            foreach (var item in _board.Placements)
            {
                _items.Add(item.InstanceId, new PuzzleItemObservation
                {
                    instanceId = item.InstanceId,
                    itemId = item.Definition.Id,
                    board = _id,
                    x = item.Pose.Origin.x,
                    y = item.Pose.Origin.y,
                    clockwiseTurns = item.Pose.ClockwiseTurns,
                    actualValue = item.ActualValue.ToString("G29", CultureInfo.InvariantCulture),
                    durability = item.Durability
                });
            }
        }

        string ActionName(GameEvents.PuzzleActionInfo _info)
        {
            if (_info.action == PuzzleActionKind.Place || _info.action == PuzzleActionKind.QuickTransfer)
            {
                if (!previous.TryGetValue(_info.instanceId, out var item) || string.IsNullOrEmpty(_info.destination))
                {
                    return "placement";
                }
                bool sourceMarket = item.board.StartsWith("market:", StringComparison.Ordinal);
                bool destinationMarket = _info.destination.StartsWith("market:", StringComparison.Ordinal);
                return sourceMarket == destinationMarket ? "reposition" : sourceMarket ? "buy" : "sell";
            }
            return _info.action switch
            {
                PuzzleActionKind.Travel => "travel",
                PuzzleActionKind.Rest => "rest",
                PuzzleActionKind.WaitForDaylight => "wait_for_daylight",
                PuzzleActionKind.AdvanceTime => "advance_time",
                PuzzleActionKind.AddWagon => "add_wagon",
                PuzzleActionKind.Repay => "repay",
                _ => throw new ArgumentOutOfRangeException(nameof(_info))
            };
        }

        [Serializable]
        sealed class RulesArchive
        {
            public RuleSource[] tables;
        }

        [Serializable]
        sealed class RuleSource
        {
            public string name, json;
        }

        // 로컬은 해시별 보관하고 LogKit에는 앱 세션 안에서 한 번만 전송한다.
        static string ArchiveRules()
        {
            var archive = new RulesArchive
            {
                tables = Resources.LoadAll<TextAsset>("Tables")
                    .OrderBy(_asset => _asset.name, StringComparer.Ordinal)
                    .Select(_asset => new RuleSource
                    {
                        name = _asset.name,
                        json = _asset.text.Replace("\r\n", "\n")
                    })
                    .ToArray()
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(archive));
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
            TelemetryLog.SendRules(hash, bytes);
            string folder = Path.Combine(TelemetryLog.FolderPath, "Rules");
            string path = Path.Combine(folder, hash + ".json");
            try
            {
                Directory.CreateDirectory(folder);
                if (!File.Exists(path))
                {
                    string temporary = path + ".tmp";
                    File.WriteAllBytes(temporary, bytes);
                    File.Move(temporary, path);
                }
            }
            catch (Exception error)
            {
                // 보조 파일 실패가 run_start 누락으로 번지지 않게 한다. 해시는 외부 원본 대조에 쓸 수 있다.
                Debug.LogError($"분석 테이블 원본 보관 실패({hash}): {error.Message}");
            }
            return hash;
        }
    }
}
