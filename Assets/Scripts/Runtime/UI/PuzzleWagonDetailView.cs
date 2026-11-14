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

    void Awake()
    {
        if (titleLabel)
        {
            titleLabel.text = "선택한 화차 상세";
        }
    }

    internal void Bind(PuzzleWagonProductData _product, Sprite _artworkSprite = null)
    {
        if (_product == null)
        {
            if (nameLabel) nameLabel.text = string.Empty;
            if (priceLabel) priceLabel.text = string.Empty;
            if (sizeLabel) sizeLabel.text = string.Empty;
            if (formationDeltaLabel) formationDeltaLabel.text = string.Empty;
            if (effectLabel) effectLabel.text = string.Empty;
            if (artworkImage) artworkImage.gameObject.SetActive(false);
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
}
