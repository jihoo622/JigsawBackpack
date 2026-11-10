using System;
using UnityEngine;

// 분석 파일에 남기는 이벤트 이름의 원본. 분석 서버·문서(Docs/Management/PLAY_LOG.md)와 같은 이름을 쓴다.
// 이름을 바꾸려면 서버와 문서를 함께 고친다. 개별 시스템에 문자열 리터럴로 퍼뜨리지 않는다.
public static class TelemetryEvents
{
    // TelemetryLog가 기둥으로 쓰는 이벤트(세션 시작·종료·요약)와 함께 유지한다.
    public const string RUN_START = "run_start";
    public const string RUN_END = "run_end";
    public const string SCENE_CHANGE = "scene_change";
    public const string UPGRADE_GRANTED = "upgrade_granted";
    public const string PANEL_VISIBILITY = "panel_visibility";
    public const string TRADE_COMPLETED = "trade_completed";
}

// payload 자료형은 [Serializable] 구조체로 둔다(JsonUtility는 익명 타입을 직렬화하지 않는다).
// 필드는 서버 JSON 계약과 같은 camelCase. 값을 늘릴 때 기존 필드 이름·뜻을 바꾸지 않고 새 필드를 추가한다.
[Serializable]
public struct SessionPayload
{
    public string reason;
    public string appVersion;
    public string unityVersion;
    public string platform;
    public bool isEditor;
    public string scene;
}

[Serializable]
public struct RunPayload
{
    public string scene;
    public double time;
    public string reason;
}

[Serializable]
public struct ScenePayload
{
    public string scene;
    public bool loaded;
}

[Serializable]
public struct UpgradePayload
{
    public string id;
    public string displayName;
}

[Serializable]
public struct PanelPayload
{
    public string panel;
    public bool open;
}

[Serializable]
public struct TradePayload
{
    public string cityId, itemId, direction;
    public int instanceId, x, y, clockwiseTurns;
    public long price, fundsAfter;
}

// 좌표는 히트맵에 충분한 1cm 단위로 줄여 행 크기를 낮춘다.
[Serializable]
public struct TelemetryPosition
{
    public float x;
    public float y;
    public float z;
}

// payload 생성의 단일 창. GameEvents 페이로드 → 분석 필드 대응을 여기 모아 이름 흩어지는 것을 막는다.
public static class TelemetryPayload
{
    const float CENTIMETERS_PER_METER = 100f;

    public static SessionPayload Session(string _reason)
    {
        var payload = new SessionPayload();
        payload.reason = _reason ?? string.Empty;
        payload.appVersion = Application.version;
        payload.unityVersion = Application.unityVersion;
        payload.platform = Application.platform.ToString();
        payload.isEditor = Application.isEditor;
        payload.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return payload;
    }

    public static RunPayload Run(GameEvents.RunInfo _info)
    {
        var payload = new RunPayload();
        payload.scene = _info.scene;
        payload.time = _info.time;
        payload.reason = _info.reason ?? string.Empty;
        return payload;
    }

    public static ScenePayload Scene(GameEvents.SceneInfo _info)
    {
        var payload = new ScenePayload();
        payload.scene = _info.scene;
        payload.loaded = _info.loaded;
        return payload;
    }

    public static UpgradePayload Upgrade(GameEvents.UpgradeInfo _info)
    {
        var payload = new UpgradePayload();
        payload.id = _info.id;
        payload.displayName = _info.displayName;
        return payload;
    }

    public static PanelPayload Panel(GameEvents.PanelInfo _info)
    {
        var payload = new PanelPayload();
        payload.panel = _info.panel;
        payload.open = _info.open;
        return payload;
    }

    public static TradePayload Trade(GameEvents.TradeInfo _info)
    {
        return new TradePayload
        {
            cityId = _info.cityId,
            itemId = _info.itemId,
            direction = _info.direction.ToString().ToLowerInvariant(),
            instanceId = _info.instanceId,
            x = _info.destination.x,
            y = _info.destination.y,
            clockwiseTurns = _info.clockwiseTurns,
            price = _info.price,
            fundsAfter = _info.fundsAfter
        };
    }

    public static TelemetryPosition Position(Vector3 _value)
    {
        var position = new TelemetryPosition();
        position.x = Quantize(_value.x);
        position.y = Quantize(_value.y);
        position.z = Quantize(_value.z);
        return position;
    }

    static float Quantize(float _value)
    {
        return Mathf.Round(_value * CENTIMETERS_PER_METER) / CENTIMETERS_PER_METER;
    }
}

// 한 줄의 형태. 분석 파일을 읽는 쪽은 이 필드만으로 해석한다(dataJson은 안쪽에 payload JSON 문자열).
[Serializable]
public sealed class TelemetryLine
{
    public string sessionId = string.Empty;
    public long sequence;
    public double time;
    public string timestampUtc = string.Empty;
    public string eventName = string.Empty;
    public string summaryId = string.Empty;
    public int revision;
    public string dataJson = string.Empty;
}
