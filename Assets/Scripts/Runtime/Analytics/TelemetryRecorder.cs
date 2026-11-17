using UnityEngine;

// 게임 도메인 이벤트를 분석 파일로 흘리는 단 한 곳의 구독자.
// 개별 시스템이 TelemetryLog를 직접 호출하면 이벤트 이름과 필드가 흩어진다. GameEvents를 추가하고 여기서 받아 쓴다.
// TelemetryLog가 같은 원본을 로컬에 보관하고 LogKit Data로 전송한다.
public sealed class TelemetryRecorder : MonoBehaviour
{
    static bool initialized;

    PuzzleInventoryService activeRun;
    TelemetryPayload.PuzzleState puzzleState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        initialized = false;
    }

    // Preload 씬의 AppBootstrap이 Awake에서 부른다. 씬 진입 이벤트(AfterSceneLoad)보다 먼저 구독을 건다.
    // 다시 불러도 구독자를 겹쳐 만들지 않는다.
    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;
        var host = new GameObject("Telemetry recorder");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<TelemetryRecorder>();
    }

    // 구독은 OnEnable/OnDisable 대칭(해제하지 않으면 정적 이벤트에 갇혀 재진입 시 중복 기록된다).
    void OnEnable()
    {
        GameEvents.SceneChanged += OnSceneChanged;
        GameEvents.UpgradeGranted += OnUpgradeGranted;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.WorkbenchReady += OnWorkbenchReady;
        GameEvents.PuzzleRunStarted += OnPuzzleRunStarted;
        GameEvents.PuzzleRunEnded += OnPuzzleRunEnded;
        GameEvents.PuzzleActionCommitted += OnPuzzleAction;
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        GameEvents.EntityLinkClicked += OnEntityLinkClicked;
        GameEvents.EntityLinkContextRequested += OnEntityLinkContextRequested;
        GameEvents.RoutePlanned += OnRoutePlanned;
        GameEvents.RouteEnded += OnRouteEnded;
        GameEvents.AutoTravelStopped += OnAutoTravelStopped;
        GameEvents.RouteStopChanged += OnRouteStopChanged;
        GameEvents.StationScreenChanged += OnStationScreenChanged;
        TelemetryLog.BeforeClose += OnSessionClosing;
    }

    void OnDisable()
    {
        FinishRun("recorder_disabled");
        GameEvents.SceneChanged -= OnSceneChanged;
        GameEvents.UpgradeGranted -= OnUpgradeGranted;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.WorkbenchReady -= OnWorkbenchReady;
        GameEvents.PuzzleRunStarted -= OnPuzzleRunStarted;
        GameEvents.PuzzleRunEnded -= OnPuzzleRunEnded;
        GameEvents.PuzzleActionCommitted -= OnPuzzleAction;
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        GameEvents.EntityLinkClicked -= OnEntityLinkClicked;
        GameEvents.EntityLinkContextRequested -= OnEntityLinkContextRequested;
        GameEvents.RoutePlanned -= OnRoutePlanned;
        GameEvents.RouteEnded -= OnRouteEnded;
        GameEvents.AutoTravelStopped -= OnAutoTravelStopped;
        GameEvents.RouteStopChanged -= OnRouteStopChanged;
        GameEvents.StationScreenChanged -= OnStationScreenChanged;
        TelemetryLog.BeforeClose -= OnSessionClosing;
    }

    void OnSceneChanged(GameEvents.SceneInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.SCENE_CHANGE, TelemetryPayload.Scene(_info));
    }

    void OnUpgradeGranted(GameEvents.UpgradeInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.UPGRADE_GRANTED, TelemetryPayload.Upgrade(_info));
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        // 호버 툴팁처럼 포인터를 따라 열리고 닫히는 패널은 수집 경계 밖이다.
        if (_info.transient)
        {
            return;
        }
        TelemetryLog.Write(TelemetryEvents.PANEL_VISIBILITY, TelemetryPayload.Panel(_info));
    }

    void OnPuzzleRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (!TelemetryLog.IsOpen)
        {
            return;
        }
        FinishRun("replaced");
        activeRun = _info.session;
        puzzleState = new TelemetryPayload.PuzzleState();
        TelemetryLog.Write(TelemetryEvents.RUN_START, puzzleState.Start(activeRun));
    }

    void OnPuzzleRunEnded(GameEvents.PuzzleRunInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            FinishRun(_info.reason);
        }
    }

    void OnPuzzleAction(GameEvents.PuzzleActionInfo _info)
    {
        if (!ReferenceEquals(activeRun, _info.session) || puzzleState == null || !TelemetryLog.IsOpen)
        {
            return;
        }
        var payload = puzzleState.Action(_info);
        // 같은 위치에 그대로 놓은 성공은 상태 변화가 아니므로 기록하지 않는다.
        bool unchangedPlacement = (_info.action == PuzzleActionKind.Place || _info.action == PuzzleActionKind.QuickTransfer)
            && string.IsNullOrEmpty(payload.failure) && payload.moneyDelta == 0
            && payload.introduced.Length == 0 && payload.moved.Length == 0
            && string.IsNullOrEmpty(payload.cityId) && payload.wagonDelta == 0
            && payload.removed.Length == 0 && payload.values.Length == 0 && payload.durability.Length == 0
            && payload.transformed.Length == 0;
        if (!unchangedPlacement)
        {
            TelemetryLog.Write(TelemetryEvents.PUZZLE_ACTION, payload);
        }
        if (_info.session.RunStatus != PuzzleRunStatus.InProgress)
        {
            FinishRun(_info.session.RunStatus == PuzzleRunStatus.Won ? "won" : "deadline");
        }
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.RUMOR_LEARNED, TelemetryPayload.Rumor(_info));
        }
    }

    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.PROFICIENCY_GAINED, new PuzzleProficiencyPayload
            {
                tag = _info.tag,
                source = _info.source,
                gainedXp = _info.gainedXp,
                xp = _info.xp,
                previousLevel = _info.previousLevel,
                level = _info.level,
                instanceId = _info.instanceId,
                questId = _info.questId,
                elapsedHours = _info.elapsedHours
            });
        }
    }

    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.QUEST_UPDATED, new PuzzleQuestPayload
            {
                questId = _info.questId,
                cargoInstanceId = _info.cargoInstanceId,
                templateId = _info.templateId,
                pieceId = _info.pieceId,
                sourceCityId = _info.sourceCityId,
                targetCityId = _info.targetCityId,
                penaltyId = _info.penaltyId,
                state = _info.state,
                failure = _info.failure,
                reward = _info.reward,
                deposit = _info.deposit,
                payout = _info.payout,
                deadlineHours = _info.deadlineHours,
                elapsedHours = _info.elapsedHours
            });
        }
    }

    void OnSessionClosing()
    {
        FinishRun("shutdown");
    }

    void FinishRun(string _reason)
    {
        if (puzzleState == null)
        {
            return;
        }
        activeRun = null;
        puzzleState = null;
        if (TelemetryLog.IsOpen)
        {
            TelemetryLog.Write(TelemetryEvents.RUN_END, new PuzzleRunEndPayload
            {
                reason = _reason
            });
        }
    }

    void OnWorkbenchReady(GameEvents.WorkbenchReadyInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.WORKBENCH_READY, TelemetryPayload.WorkbenchReady(_info));
        }
    }

    void OnRouteStopChanged(GameEvents.RouteStopChangedInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.ROUTE_STOP_CHANGED, TelemetryPayload.RouteStopChanged(_info));
        }
    }

    void OnStationScreenChanged(GameEvents.StationScreenInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.STATION_SCREEN, TelemetryPayload.StationScreen(_info));
    }

    void OnEntityLinkClicked(GameEvents.EntityLinkInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.ENTITY_LINK_CLICKED, TelemetryPayload.EntityLink(_info, "click"));
    }

    void OnEntityLinkContextRequested(GameEvents.EntityLinkInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.ENTITY_LINK_CLICKED, TelemetryPayload.EntityLink(_info, "context"));
    }

    void OnRoutePlanned(GameEvents.RoutePlannedInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.ROUTE_PLANNED, TelemetryPayload.RoutePlanned(_info));
        }
    }

    void OnRouteEnded(GameEvents.RouteEndedInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.ROUTE_ENDED, TelemetryPayload.RouteEnded(_info));
        }
    }

    void OnAutoTravelStopped(GameEvents.AutoTravelStoppedInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.AUTO_TRAVEL_STOPPED, TelemetryPayload.AutoTravelStopped(_info));
        }
    }
}
