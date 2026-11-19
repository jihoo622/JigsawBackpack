using System;
using UnityEngine;

// R21: 개편 레이아웃 단계별 개발 중 기존 레이아웃과 전환하는 스위치. S7 완료 전까지 기본값은 꺼짐(0).
public static class RelayoutSettings
{
    const string PREFS_KEY = "ui.relayout.enabled";

    public static event Action<bool> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
    }

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PREFS_KEY, 0) != 0;
        set
        {
            bool current = Enabled;
            if (current == value)
            {
                return;
            }
            PlayerPrefs.SetInt(PREFS_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }
}
