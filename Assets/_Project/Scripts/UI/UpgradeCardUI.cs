using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Project.Scripts.Data;
using _Project.Scripts.Network;
using _Project.Scripts.Gameplay; // Добавлено

namespace _Project.Scripts.UI
{
    public class UpgradeCardUI : MonoBehaviour
    {
        [Header("Ссылки на UI элементы")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI costText;
        public Image iconImage;
        public Image backgroundRarityImage; 
        public Button buyButton;

        private ShopOffer _currentOffer;
        private ShopWindowUI _parentWindow; // Ссылка на окно, чтобы обновить UI после покупки

        public void Setup(ShopOffer offer, ShopWindowUI parentWindow)
        {
            _currentOffer = offer;
            _parentWindow = parentWindow;
            var data = offer.Data;
            
            if (titleText != null) titleText.text = data.displayName;
            if (descriptionText != null) descriptionText.text = data.description;
            
            // --- ОТОБРАЖЕНИЕ ЦЕНЫ СО СКИДКОЙ ---
            if (costText != null)
            {
                if (data.baseCost == 0)
                {
                    costText.text = "<color=#00FF00>БЕСПЛАТНО</color>";
                }
                else if (offer.DiscountPercent > 0)
                {
                    // Зачеркиваем старую цену серым, пишем новую зеленую и показываем % скидки
                    costText.text = $"<color=#808080><s>{data.baseCost}</s></color> <color=#32CD32>{offer.FinalCost}</color> <size=60%><color=#FFD700>(-{offer.DiscountPercent}%)</color></size>";
                }
                else
                {
                    costText.text = offer.FinalCost.ToString();
                }
            }
            
            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = true;
            }
            
            SetRarityColor(data.rarity);

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                
                if (offer.IsSold)
                {
                    buyButton.interactable = false;
                    costText.text = "<color=#FF0000>ПРОДАНО</color>";
                }
                else
                {
                    buyButton.interactable = true;
                    buyButton.onClick.AddListener(OnBuyClicked);
                }
            }
        }

        private void SetRarityColor(UpgradeRarity rarity)
        {
            if (backgroundRarityImage == null) return;
            switch (rarity)
            {
                case UpgradeRarity.Ordinary: backgroundRarityImage.color = new Color(0.7f, 0.7f, 0.7f); break;
                case UpgradeRarity.Experimental: backgroundRarityImage.color = new Color(0.2f, 0.8f, 0.2f); break;
                case UpgradeRarity.Banned: backgroundRarityImage.color = new Color(0.6f, 0.2f, 0.8f); break;
                case UpgradeRarity.Anomalous: backgroundRarityImage.color = new Color(0.9f, 0.2f, 0.2f); break;
            }
        }

        private void OnBuyClicked()
        {
            if (_currentOffer == null || PlayerNetworkBridge.LocalPlayer == null) return;
            
            // Отправляем FinalCost (со скидкой), а не базовую!
            PlayerNetworkBridge.LocalPlayer.Rpc_RequestPurchaseUpgrade(_currentOffer.Data.upgradeID, _currentOffer.FinalCost);
            
            // Помечаем локально как проданное и обновляем витрину
            _currentOffer.IsSold = true;
            buyButton.interactable = false;
            
            // Сообщаем окну магазина, что нужно перерисовать кнопки
            _parentWindow.RefreshUI(); 
        }
    }
}