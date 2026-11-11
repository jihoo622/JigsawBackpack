using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleTradeNotebookView : MonoBehaviour
{
    internal const string PanelName = "TradeNotebook";

    void OnEnable()
    {
        GameEvents.RaisePanelVisibility(PanelName, true);
    }

    void OnDisable()
    {
        GameEvents.RaisePanelVisibility(PanelName, false);
    }

    [SerializeField] CanvasGroup panel;
    [SerializeField] RectTransform bookSpread;
    [SerializeField] Button closeNotebookButton;
    [SerializeField] Button previousCityButton;
    [SerializeField] Button nextCityButton;
    [SerializeField] Button previousRecordButton;
    [SerializeField] Button nextRecordButton;
    [SerializeField] TextMeshProUGUI notebookCityLabel;
    [SerializeField] TextMeshProUGUI notebookMessage;
    [SerializeField] TextMeshProUGUI notebookSource;
    [SerializeField] TextMeshProUGUI notebookRecordCount;

    IReadOnlyList<PuzzleInventoryService.TavernRumor> rumors;
    PuzzleInventoryService.TavernRumor[][] notebookCities;
    int notebookCityIndex;
    int notebookRecordIndex;

    internal void Initialize(IReadOnlyList<PuzzleInventoryService.TavernRumor> _rumors, UnityEngine.Events.UnityAction _close)
    {
        if (!panel || !bookSpread || !closeNotebookButton || !previousCityButton || !nextCityButton ||
            !previousRecordButton || !nextRecordButton || !notebookCityLabel ||
            !notebookMessage || !notebookSource || !notebookRecordCount)
        {
            throw new InvalidOperationException("교역 수첩 프리팹에 버튼과 텍스트 참조가 필요합니다.");
        }

        if (rumors == null)
        {
            closeNotebookButton.onClick.AddListener(_close);
            previousCityButton.onClick.AddListener(() => ChangeNotebookCity(-1));
            nextCityButton.onClick.AddListener(() => ChangeNotebookCity(1));
            previousRecordButton.onClick.AddListener(() => ChangeNotebookRecord(-1));
            nextRecordButton.onClick.AddListener(() => ChangeNotebookRecord(1));
        }
        rumors = _rumors;
    }

    internal void ShowRecords()
    {
        // 최신 소식부터 정렬한 뒤, 소식이 가리키는 마을별로 묶는다.
        notebookCities = rumors.Reverse().GroupBy(rumor => rumor.TargetCityId)
            .Select(city => city.ToArray()).ToArray();
        notebookCityIndex = 0;
        notebookRecordIndex = 0;
        RefreshNotebook();
        StartCoroutine(UnfoldBook());
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(closeNotebookButton.gameObject);
        }
    }

    IEnumerator UnfoldBook()
    {
        const float duration = 0.16f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float width = Mathf.SmoothStep(0.72f, 1f, elapsed / duration);
            bookSpread.localScale = new Vector3(width, 1f, 1f);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        bookSpread.localScale = Vector3.one;
    }

    internal void SetInteraction(bool _enabled)
    {
        panel.interactable = panel.blocksRaycasts = _enabled;
    }

    void ChangeNotebookCity(int _step)
    {
        notebookCityIndex += _step;
        notebookRecordIndex = 0;
        RefreshNotebook();
    }

    void ChangeNotebookRecord(int _step)
    {
        notebookRecordIndex += _step;
        RefreshNotebook();
    }

    void RefreshNotebook()
    {
        bool hasRecords = notebookCities.Length > 0;
        int recordCount = hasRecords ? notebookCities[notebookCityIndex].Length : 0;
        previousCityButton.interactable = notebookCityIndex > 0;
        nextCityButton.interactable = notebookCityIndex + 1 < notebookCities.Length;
        previousRecordButton.interactable = notebookRecordIndex > 0;
        nextRecordButton.interactable = notebookRecordIndex + 1 < recordCount;

        if (!hasRecords)
        {
            notebookCityLabel.text = string.Empty;
            notebookMessage.text = "술집에서 들은 교역 소식이 여기에 기록됩니다.";
            notebookSource.text = string.Empty;
            notebookRecordCount.text = string.Empty;
            return;
        }

        var rumor = notebookCities[notebookCityIndex][notebookRecordIndex];
        var tables = TableManager.Instance;
        notebookCityLabel.text = tables.CityPrice(rumor.TargetCityId).displayName;
        notebookMessage.text = rumor.Message;
        notebookSource.text = $"{rumor.RecordedTime}\n{tables.CityPrice(rumor.SourceCityId).displayName} 술집";
        notebookRecordCount.text = $"기록 {notebookRecordIndex + 1} / {recordCount}";
    }
}
