using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Project.Scripts.Data.Shop;

namespace _Project.Scripts.UI
{
    public class ConsumableCardUI : MonoBehaviour
    {
        [Header("UI Ссылки")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI costText;

        [Header("Кнопки")]
        public Button unlockButton;
        public Button equipSlot1Button;
        public Button equipSlot2Button;

        // Инициализация карточки данными
        public void Setup(ConsumableData data, Action<ConsumableData> onUnlock, Action<ConsumableData, int> onEquip)
        {
            nameText.text = data.displayName;
            descriptionText.text = data.description;
            costText.text = data.unlockCost.ToString();
            if (iconImage != null) iconImage.sprite = data.icon;

            // Очищаем старые подписки (защита при переиспользовании)
            unlockButton.onClick.RemoveAllListeners();
            unlockButton.onClick.AddListener(() => onUnlock(data));

            equipSlot1Button.onClick.RemoveAllListeners();
            equipSlot1Button.onClick.AddListener(() => onEquip(data, 0)); // 0 - Слот 1

            equipSlot2Button.onClick.RemoveAllListeners();
            equipSlot2Button.onClick.AddListener(() => onEquip(data, 1)); // 1 - Слот 2
        }

        // Обновление состояния кнопок (вызывается из Менеджера)
        public void RefreshState(bool isUnlocked, bool isEquippedInSlot1, bool isEquippedInSlot2)
        {
            unlockButton.gameObject.SetActive(!isUnlocked);
            
            equipSlot1Button.gameObject.SetActive(isUnlocked);
            equipSlot1Button.interactable = !isEquippedInSlot1; // Кнопка неактивна, если уже в этом слоте
            
            equipSlot2Button.gameObject.SetActive(isUnlocked);
            equipSlot2Button.interactable = !isEquippedInSlot2;
        }
    }
}