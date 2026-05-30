using System.Collections.Generic;
using UnityEngine;
using _Project.Scripts.Gameplay;
using _Project.Scripts.Network;

namespace _Project.Scripts.UI
{
    public class ShopWindowUI : UIWindow
    {
        [Header("Ссылки на префабы и контейнеры")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform cardsContainer;

        private readonly List<GameObject> _spawnedCards = new();

        public override void Open()
        {
            base.Open();
            
            // Гарантированно чистим витрину перед генерацией новых предложений
            ClearOldCards();
            GenerateOffers();
        }

        public override void Close()
        {
            base.Close();
            ClearOldCards();
        }

        private void GenerateOffers()
        {
            if (PlayerNetworkBridge.LocalPlayer == null || LocalShopManager.Instance == null) return;

            // Запрашиваем у бэкенда 3 случайные карточки с учетом взвешенного рандома
            var offers = LocalShopManager.Instance.GenerateShopOffers(PlayerNetworkBridge.LocalPlayer);

            foreach (var upgradeData in offers)
            {
                if (upgradeData == null) continue;

                // Спавним карточку внутри нашего UI-контейнера
                var cardInstance = Instantiate(cardPrefab, cardsContainer);
                var cardUI = cardInstance.GetComponent<UpgradeCardUI>();
                
                if (cardUI != null)
                {
                    cardUI.Setup(upgradeData);
                }
                
                _spawnedCards.Add(cardInstance);
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