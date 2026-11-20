using TMPro;
using UnityEngine;

// S2: 좌측 승강장·기차길 배경(26, 134, 510, 929). 순수 배경이며 S7에서 속도선 등을 연동한다.
// 간판판 글자는 현재 도시 이름으로 채운다. 도시 정보가 없으면 빈 칸으로 둔다.
[DisallowMultipleComponent]
public sealed class PlatformSceneView : MonoBehaviour
{
    [SerializeField] GameObject travelSpeedLines;
    [SerializeField] TextMeshProUGUI signText;

    void Awake()
    {
        SetTraveling(false);
    }

    void OnEnable()
    {
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.PuzzleRunStarted += OnRunStarted;
        RefreshSign();
    }

    void OnDisable()
    {
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.PuzzleRunStarted -= OnRunStarted;
    }

    public void SetTraveling(bool _traveling)
    {
        if (travelSpeedLines && travelSpeedLines.activeSelf != _traveling)
        {
            travelSpeedLines.SetActive(_traveling);
        }
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info) => RefreshSign();
    // 판 시작(서비스 Initialize) 전에 켜지면 도시 정보가 없으므로 시작 시점에 출발 도시 이름을 다시 채운다.
    void OnRunStarted(GameEvents.PuzzleRunInfo _info) => RefreshSign();

    // 간판에 현재 도시의 표시 이름만 보인다. 승강장 번호 문구는 BasicData에 없어 보류 중이다.
    void RefreshSign()
    {
        if (!signText)
        {
            return;
        }
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        signText.text = session != null && session.CityData != null ? Presentation.PlatformSignTitle(session.CityData.displayName) : string.Empty;
    }
}
