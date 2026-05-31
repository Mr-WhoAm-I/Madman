using System;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections.Generic;
using TMPro;
using _Project.Scripts.Core;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Managers;
using UnityEngine.InputSystem;

namespace _Project.Scripts.UI
{
    public class NetworkMenuController : MonoBehaviour
    {
        [Header("Данные игрока")]
        public TMP_InputField NicknameInput;

        [Header("Настройки Хоста")]
        public Toggle HostToggle; 
        public TMP_InputField RoomNameInput; 

        [Header("Список сессий")]
        public Transform SessionListContent; 
        public GameObject SessionEntryPrefab; 

        private List<GameObject> _spawnedEntries = new();

        private void OnEnable()
        {
            if (ProfileController.Instance != null)
            {
                NicknameInput.text = ProfileController.Instance.CurrentProfile.Nickname;
            }

            // Подписываемся на обновления списка серверов
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnSessionListUpdatedEvent += UpdateSessionList;
            }
        }
        
        private void OnDisable()
        {
            // Отписываемся, чтобы не было утечек памяти, когда меню закрыто
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnSessionListUpdatedEvent -= UpdateSessionList;
            }
        }

        public void OnNicknameChanged(string newName)
        {
            ProfileController.Instance.SetNickname(newName);
        }

        // ВЫЗЫВАЕТСЯ ПО КНОПКЕ ИЗ UI: "Применить настройки сети / Искать игры"
        public async void OnApplyNetworkSettingsClicked()
        {
            try
            {
                if (HostToggle.isOn)
                {
                    // Если тумблер включен - создаем свой сервер
                    var room = string.IsNullOrEmpty(RoomNameInput.text) 
                        ? ProfileController.Instance.CurrentProfile.Nickname + "_World" 
                        : RoomNameInput.text;
                        
                    await NetworkManager.Instance.HostOnlineGame(room);
                }
                else
                {
                    // Если тумблер выключен - идем в лобби искать чужие сервера
                    await NetworkManager.Instance.BrowseOnlineGames();
                }
            }
            catch (Exception e)
            {
                // Теперь мы будем видеть в консоли ЛЮБЫЕ ошибки, если они возникнут при создании сервера
                Debug.LogError($"[NetworkMenu] Критическая ошибка сети: {e.Message}\n{e.StackTrace}");
            }
        }

        public void UpdateSessionList(List<SessionInfo> sessions)
        {
            foreach (var entry in _spawnedEntries)
            {
                Destroy(entry);
            }
            _spawnedEntries.Clear();

            foreach (var session in sessions)
            {
                if (!session.IsOpen || !session.IsVisible) continue;

                var entryObj = Instantiate(SessionEntryPrefab, SessionListContent);
                var entryUI = entryObj.GetComponent<SessionEntryUI>();
                
                // Передаем метод OnJoinSessionClicked прямо в плашку!
                entryUI.Setup(session, OnJoinSessionClicked);
                _spawnedEntries.Add(entryObj);
            }
        }

        // Этот метод вызовется, когда мы нажмем кнопку "Join" на конкретной плашке
        private void OnJoinSessionClicked(SessionInfo session)
        {
            Debug.Log($"[NetworkMenu] Подключаемся к миру: {session.Name}");
            _ = NetworkManager.Instance.JoinOnlineGame(session.Name);
        }
    }
}