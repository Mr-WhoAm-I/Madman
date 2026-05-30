using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Project.Scripts.Data;
using _Project.Scripts.Network;

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

        private UpgradeData _currentData;

        public void Setup(UpgradeData data)
        {
            _currentData = data;
            
            // Заполняем визуал из Scriptable Object
            if (titleText != null) titleText.text = data.displayName;
            if (descriptionText != null) descriptionText.text = data.description;
            if (costText != null) costText.text = data.baseCost.ToString();
            
            // Если иконка назначена — включаем, иначе можно выключить компонент
            if (iconImage != null)
            {
                if (data.icon != null)
                {
                    iconImage.sprite = data.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
            
            SetRarityColor(data.rarity);

            // Очищаем старые подписки (защита от пулинга) и вешаем новую
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
                buyButton.interactable = true; // Сбрасываем состояние кнопки
            }
        }

        private void SetRarityColor(UpgradeRarity rarity)
        {
            if (backgroundRarityImage == null) return;
            
            // Простейшая цветовая дифференциация штанов. 
            // Позже можно заменить на красивые спрайты/материалы.
            switch (rarity)
            {
                case UpgradeRarity.Ordinary: 
                    backgroundRarityImage.color = new Color(0.7f, 0.7f, 0.7f); // Серый
                    break;
                case UpgradeRarity.Experimental: 
                    backgroundRarityImage.color = new Color(0.2f, 0.8f, 0.2f); // Зеленый
                    break;
                case UpgradeRarity.Banned: 
                    backgroundRarityImage.color = new Color(0.6f, 0.2f, 0.8f); // Фиолетовый
                    break;
                case UpgradeRarity.Anomalous: 
                    backgroundRarityImage.color = new Color(0.9f, 0.2f, 0.2f); // Красный
                    break;
            }
        }

        private void OnBuyClicked()
        {
            if (_currentData == null || PlayerNetworkBridge.LocalPlayer == null) return;
            
            // Отправляем RPC на сервер с запросом покупки
            PlayerNetworkBridge.LocalPlayer.Rpc_RequestPurchaseUpgrade(_currentData.upgradeID, _currentData.baseCost);
            
            // Визуально отключаем кнопку локально, чтобы игрок не спамил кликами
            if (buyButton != null)
            {
                buyButton.interactable = false;
            }
        }
    }
}