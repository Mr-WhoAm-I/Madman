using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Project.Scripts.Gameplay;
using _Project.Scripts.Network;

namespace _Project.Scripts.UI
{
    public class ShopWindowUI : UIWindow
    {
        [Header("Ссылки на префабы и контейнеры")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform cardsContainer;

        [Header("Управление Магазином (Реролл)")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollButtonText;

        private readonly List<GameObject> _spawnedCards = new();

        private void Awake()
        {
            if (rerollButton != null)
            {
                // Подписываемся на кнопку обновления магазина
                rerollButton.onClick.AddListener(OnRerollClicked);
            }
        }

        public override void Open()
        {
            base.Open();
            DrawOffers();
            RefreshUI();
        }

        public override void Close()
        {
            base.Close();
            ClearOldCards();
        }

        // Метод для отрисовки ТЕКУЩЕГО состояния витрины
        private void DrawOffers()
        {
            ClearOldCards();

            if (LocalShopManager.Instance == null) return;

            // Берем уже сформированные предложения (они сохраняются между открытиями окна)
            var offers = LocalShopManager.Instance.CurrentOffers;

            foreach (var offer in offers)
            {
                if (offer == null) continue;

                var cardInstance = Instantiate(cardPrefab, cardsContainer);
                var cardUI = cardInstance.GetComponent<UpgradeCardUI>();
                
                if (cardUI != null)
                {
                    // Передаем ссылку на это окно, чтобы карточка могла вызвать RefreshUI при покупке
                    cardUI.Setup(offer, this); 
                }
                
                _spawnedCards.Add(cardInstance);
            }
        }

        // Обновляет состояние интерфейса (например, текст кнопки реролла)
        public void RefreshUI()
        {
            if (LocalShopManager.Instance == null) return;

            if (rerollButton != null && rerollButtonText != null)
            {
                int rerolls = LocalShopManager.Instance.AvailableRerolls;
                rerollButtonText.text = $"Обновить ({rerolls})";
                
                // Делаем кнопку неактивной, если рероллы кончились
                rerollButton.interactable = rerolls > 0;
            }
        }

        private void OnRerollClicked()
        {
            if (LocalShopManager.Instance != null && LocalShopManager.Instance.TryReroll())
            {
                // Если реролл успешен — перерисовываем витрину
                DrawOffers();
                RefreshUI();
            }
        }

        private void ClearOldCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Destroy(card);
            }
            _spawnedCards.Clear();
        }
    }
}