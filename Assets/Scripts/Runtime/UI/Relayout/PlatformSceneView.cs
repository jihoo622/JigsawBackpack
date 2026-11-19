using UnityEngine;

// S2: 좌측 승강장·기차길 배경(26, 134, 510, 929). 순수 배경이며 S7에서 속도선 등을 연동한다.
[DisallowMultipleComponent]
public sealed class PlatformSceneView : MonoBehaviour
{
    [SerializeField] GameObject travelSpeedLines;

    void Awake()
    {
        SetTraveling(false);
    }

    public void SetTraveling(bool _traveling)
    {
        if (travelSpeedLines && travelSpeedLines.activeSelf != _traveling)
        {
            travelSpeedLines.SetActive(_traveling);
        }
    }
}
