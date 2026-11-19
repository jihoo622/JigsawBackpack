using TMPro;
using UnityEngine;

// S5: 시장 부동 창(1136, 96, 764, 638). 상단 시세 띠(CityTradePreferencesView) + 13×9 보드(56px) + 하단 조작 안내.
[DisallowMultipleComponent]
public sealed class MarketWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] CityTradePreferencesView tradePreferencesView;
    [SerializeField] PuzzleInventoryView inventoryView;
    [SerializeField] TextMeshProUGUI hintText;
    [SerializeField] TextMeshProUGUI restockText;

    public FloatingWindow FloatingWindow => floatingWindow;
    public PuzzleInventoryView InventoryView => inventoryView;
    public CityTradePreferencesView TradePreferencesView => tradePreferencesView;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    void OnEnable()
    {
        if (hintText)
        {
            hintText.text = Presentation.MarketWindowHint;
        }
        if (restockText)
        {
            restockText.text = Presentation.MarketWindowRestock;
        }
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
    }

    internal void SetCity(string _cityName, PuzzleCityTradePreferencesData _prefs)
    {
        if (titleText)
        {
            titleText.text = Presentation.MarketWindowTitle(_cityName ?? string.Empty);
        }
        if (tradePreferencesView)
        {
            tradePreferencesView.Show(_prefs);
        }
    }
}
