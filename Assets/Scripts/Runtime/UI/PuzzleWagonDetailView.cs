using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleWagonDetailView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] TextMeshProUGUI sizeLabel;
    [SerializeField] TextMeshProUGUI formationDeltaLabel;
    [SerializeField] TextMeshProUGUI effectLabel;
    [SerializeField] Image artworkImage;
    [SerializeField] TextMeshProUGUI emptyLabel;

    void Awake()
    {
        if (titleLabel)
        {
            titleLabel.text = Presentation.WagonDetailTitle;
        }
        if (emptyLabel)
        {
            emptyLabel.text = Presentation.WagonDetailEmpty;
        }
    }

    // 화차가 없으면(선택 전) 상세 줄을 모두 숨기고 고르기 안내만 보인다.
    internal void Bind(PuzzleWagonProductData _product, Sprite _artworkSprite = null)
    {
        bool hasProduct = _product != null;
        SetLineVisible(nameLabel, hasProduct);
        SetLineVisible(priceLabel, hasProduct);
        SetLineVisible(sizeLabel, hasProduct);
        SetLineVisible(formationDeltaLabel, hasProduct);
        SetLineVisible(effectLabel, hasProduct);
        SetLineVisible(emptyLabel, !hasProduct);
        if (!hasProduct)
        {
            if (artworkImage)
            {
                artworkImage.gameObject.SetActive(false);
            }
            return;
        }

        if (nameLabel)
        {
            nameLabel.text = _product.displayName;
        }
        if (priceLabel)
        {
            priceLabel.text = $"가격: {_product.price:N0}$";
        }
        if (sizeLabel)
        {
            sizeLabel.text = Presentation.WagonSizeLabel(5, 5);
        }
        if (formationDeltaLabel)
        {
            formationDeltaLabel.text = Presentation.WagonFormationDelta(1, 25);
        }
        if (effectLabel)
        {
            if (string.IsNullOrEmpty(_product.modificationId))
            {
                effectLabel.text = "특수 효과: 없음";
            }
            else if (string.Equals(_product.modificationId, PuzzleWagonRefrigeration.ModificationId, System.StringComparison.Ordinal))
            {
                effectLabel.text = $"특수 효과: {Presentation.PuzzleWagonRefrigerationInstalled}";
            }
            else
            {
                effectLabel.text = $"특수 효과: {_product.modificationId}";
            }
        }
        if (artworkImage)
        {
            if (_artworkSprite != null)
            {
                artworkImage.sprite = _artworkSprite;
                artworkImage.gameObject.SetActive(true);
            }
            else
            {
                artworkImage.gameObject.SetActive(false);
            }
        }
    }

    static void SetLineVisible(TextMeshProUGUI _label, bool _visible)
    {
        if (_label)
        {
            _label.gameObject.SetActive(_visible);
        }
    }
}
