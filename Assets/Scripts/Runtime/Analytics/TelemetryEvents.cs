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
    public const string BUTCHERY_COMMITTED = "butchery_committed";
    public const string BUTCHERY_READY = "butchery_ready";
    public const string PUZZLE_ACTION = "puzzle_action";
    public const string RUMOR_LEARNED = "rumor_learned";
    public const string RULES_CHUNK = "rules_chunk";
}

// LogKit 전송용 분할 원본. Base64를 조각별 디코딩한 바이트를 chunkIndex 순서로 이어 붙인다.
[Serializable]
public sealed class TelemetryRulesChunk
{
    public string sessionId, rulesHash;
    public int chunkIndex, chunkCount;
    public string contentBase64;
}

// payload는 [Serializable] 자료형으로 둔다. 계약 변경은 session_start.schemaVersion으로 구분한다.
[Serializable]
public struct SessionPayload
{
    public int schemaVersion;
    public string reason;
    public string appVersion;
    public string unityVersion;
    public string platform;
    public bool isEditor;
    public string scene;
}

[Serializable]
public struct SessionEndPayload
{
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
}

[Serializable]
public struct PanelPayload
{
    public string panel;
    public bool open;
}

[Serializable]
public struct ButcheryCommittedPayload
{
    public string actionId, cityId;
    public int inputCount;
    public long readyAtMinutes;
    public double time;
}

[Serializable]
public struct ButcheryReadyPayload
{
    public string actionId, cityId;
    public int outputCount;
    public double time;
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
public static partial class TelemetryPayload
{
    const float CENTIMETERS_PER_METER = 100f;

    public static SessionPayload Session(string _reason)
    {
        var payload = new SessionPayload();
        payload.schemaVersion = 3;
        payload.reason = _reason ?? string.Empty;
        payload.appVersion = Application.version;
        payload.unityVersion = Application.unityVersion;
        payload.platform = Application.platform.ToString();
        payload.isEditor = Application.isEditor;
        payload.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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
        return payload;
    }

    public static PanelPayload Panel(GameEvents.PanelInfo _info)
    {
        var payload = new PanelPayload();
        payload.panel = _info.panel;
        payload.open = _info.open;
        return payload;
    }

    public static ButcheryCommittedPayload ButcheryCommitted(GameEvents.ButcheryCommittedInfo _info)
    {
        return new ButcheryCommittedPayload
        {
            actionId = _info.actionId,
            cityId = _info.cityId,
            inputCount = _info.inputCount,
            readyAtMinutes = _info.readyAtMinutes,
            time = _info.time
        };
    }

    public static ButcheryReadyPayload ButcheryReady(GameEvents.ButcheryReadyInfo _info)
    {
        return new ButcheryReadyPayload
        {
            actionId = _info.actionId,
            cityId = _info.cityId,
            outputCount = _info.outputCount,
            time = _info.time
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
