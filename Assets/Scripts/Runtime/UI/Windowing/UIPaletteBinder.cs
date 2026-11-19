using UnityEngine;
using UnityEngine.UI;

// 지정된 UIPalette 색 역할을 Awake 시점에 Graphic(Image, TMP_Text 등)에 적용한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class UIPaletteBinder : MonoBehaviour
{
    const string RESOURCE_PATH = "UI/UIPalette";

    [SerializeField] UIPalette palette;
    [SerializeField] UIPalette.ColorRole role = UIPalette.ColorRole.Panel;
    [SerializeField] bool preserveAlpha;

    void Awake()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        var source = palette ? palette : Resources.Load<UIPalette>(RESOURCE_PATH);
        var graphic = GetComponent<Graphic>();
        if (!source || !graphic)
        {
            return;
        }
        Color target = source.GetColor(role);
        if (preserveAlpha)
        {
            target.a = graphic.color.a;
        }
        graphic.color = target;
    }
}
