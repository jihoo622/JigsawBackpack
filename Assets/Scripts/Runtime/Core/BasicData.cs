using System.Globalization;
using UnityEngine;

// 기초데이터의 조회 창구. 경로·로더·캐시는 TableManager 하나만 사용한다.
public static class BasicData
{
    public static float Float(string _id)
    {
        return (float)TableManager.Instance.RequireValid().BasicNumber(_id).value;
    }

    public static int Int(string _id)
    {
        return checked((int)TableManager.Instance.RequireValid().BasicNumber(_id).value);
    }

    public static string Text(string _id)
    {
        return TableManager.Instance.RequireValid().BasicText(_id).value;
    }

    public static string Format(string _id, params object[] _arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Text(_id), _arguments);
    }

    public static Color Color(string _id)
    {
        var value = TableManager.Instance.RequireValid().BasicColor(_id);
        return new Color(value.r, value.g, value.b, value.a);
    }

    public static Color Color(string _id, float _alpha)
    {
        var color = Color(_id);
        color.a = _alpha;
        return color;
    }
}
