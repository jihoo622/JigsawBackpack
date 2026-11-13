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
    internal int SlotCount => slots.Length;

    void OnEnable()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].acceptButton.onClick.AddListener(() => OfferAccepted?.Invoke(index));
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
        messageLabel.text = _message ?? string.Empty;
        messageLabel.gameObject.SetActive(!string.IsNullOrEmpty(_message));
    }
}
