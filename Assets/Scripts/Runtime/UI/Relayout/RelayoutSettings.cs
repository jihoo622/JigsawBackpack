using System;
using UnityEngine;

// R21/R30: 개편 레이아웃과 기존 레이아웃 전환 스위치. S7부터 기본값은 켜짐(1).
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
        get => PlayerPrefs.GetInt(PREFS_KEY, 1) != 0;
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
