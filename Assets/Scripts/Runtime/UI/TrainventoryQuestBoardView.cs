using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 술집의 의뢰 게시판. 프리팹에 미리 배치한 의뢰 슬롯을 켜고 끄며 표시만 한다. 의뢰 내용은 Presenter가 전달한다.
public sealed class TrainventoryQuestBoardView : MonoBehaviour
{
    [Serializable]
    sealed class OfferSlot
    {
        public GameObject root;
        public TextMeshProUGUI description;
        public Button acceptButton;
        public TextMeshProUGUI acceptLabel;
        public Button selectButton;
        public Image selectionHighlight;
    }

    internal readonly struct OfferContent
    {
        internal readonly string description, acceptLabel;
        internal readonly bool canAccept;

        internal OfferContent(string _description, string _acceptLabel, bool _canAccept)
        {
            description = _description;
            acceptLabel = _acceptLabel;
            canAccept = _canAccept;
        }
    }

    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI statusLabel;
    [SerializeField] TextMeshProUGUI messageLabel;
    [SerializeField] OfferSlot[] slots = Array.Empty<OfferSlot>();

    internal event Action<int> OfferAccepted;
    internal event Action<int> OfferSelected;
    internal int SlotCount => slots.Length;
    internal int SelectedIndex { get; private set; } = 0;

    void OnEnable()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            if (slots[i].acceptButton)
            {
                slots[i].acceptButton.onClick.AddListener(() => OfferAccepted?.Invoke(index));
            }
            if (slots[i].selectButton)
            {
                slots[i].selectButton.onClick.AddListener(() => SelectOffer(index));
            }
        }
    }

    void OnDisable()
    {
        foreach (var slot in slots)
        {
            if (slot.acceptButton)
            {
                slot.acceptButton.onClick.RemoveAllListeners();
            }
            if (slot.selectButton)
            {
                slot.selectButton.onClick.RemoveAllListeners();
            }
        }
    }

    internal void SelectOffer(int _index)
    {
        SelectedIndex = _index;
        UpdateSelectionVisual();
        OfferSelected?.Invoke(_index);
    }

    internal void SetSelectedIndex(int _index)
    {
        SelectedIndex = _index;
        UpdateSelectionVisual();
    }

    void UpdateSelectionVisual()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].selectionHighlight != null)
            {
                slots[i].selectionHighlight.gameObject.SetActive(i == SelectedIndex);
            }
        }
    }

    internal void Render(string _title, string _status, OfferContent[] _offers, string _message)
    {
        if (!titleLabel || !statusLabel || !messageLabel || slots.Length == 0)
        {
            throw new InvalidOperationException("의뢰 게시판에 제목·상태·안내 문구와 의뢰 슬롯을 연결하세요.");
        }
        titleLabel.text = _title;
        statusLabel.text = _status;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            bool shown = i < _offers.Length;
            slot.root.SetActive(shown);
            if (!shown)
            {
                continue;
            }
            slot.description.text = _offers[i].description;
            slot.acceptLabel.text = _offers[i].acceptLabel;
            slot.acceptButton.interactable = _offers[i].canAccept;
        }

        if (SelectedIndex >= _offers.Length)
        {
            SelectedIndex = _offers.Length > 0 ? 0 : -1;
        }
        UpdateSelectionVisual();

        messageLabel.text = _message ?? string.Empty;
        messageLabel.gameObject.SetActive(!string.IsNullOrEmpty(_message));
    }
}
