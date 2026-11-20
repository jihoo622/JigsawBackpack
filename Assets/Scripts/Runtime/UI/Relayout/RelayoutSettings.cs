using System;
using UnityEngine;

// R21/R30: 개편 레이아웃과 기존 레이아웃 전환 스위치였다. 2026-11-20부터 개편 레이아웃만 쓴다(R58).
// 예전에 저장한 끔 값(PlayerPrefs ui.relayout.enabled)은 무시한다. 옛 UI 오브젝트·분기 코드 삭제는 별도 작업으로 남긴다.
public static class RelayoutSettings
{
    // 더는 바뀌지 않지만 구독 코드(옛·새 레이아웃 전환 처리)를 그대로 두기 위해 남긴다.
#pragma warning disable 0067
    public static event Action<bool> Changed;
#pragma warning restore 0067

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
    }

    public static bool Enabled => true;
}
