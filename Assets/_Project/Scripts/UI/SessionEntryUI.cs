using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System;

namespace _Project.Scripts.UI
{
    public class SessionEntryUI : MonoBehaviour
    {
        [Header("UI Ссылки")]
        public TextMeshProUGUI SessionNameText;
        public TextMeshProUGUI PlayerCountText;
        public Button JoinButton;

        private SessionInfo _sessionInfo;

        // Метод инициализации вызывается, когда мы спавним эту плашку в списке
        public void Setup(SessionInfo info, Action<SessionInfo> onJoinClicked)
        {
            _sessionInfo = info;
            SessionNameText.text = info.Name;
            PlayerCountText.text = $"{info.PlayerCount} / {info.MaxPlayers}";

            // Очищаем старые подписки и вешаем новую (чтобы не было двойных кликов при переиспользовании)
            JoinButton.onClick.RemoveAllListeners();
            JoinButton.onClick.AddListener(() => onJoinClicked?.Invoke(_sessionInfo));
        }
    }
}