using _Project.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.UI
{
    public class SkillUIController : MonoBehaviour
    {
        [Header("UI Элементы")]
        public Image DarkCooldownFill; // Картинка затемнения
        
        private void Update()
        {
            // Берем локального игрока (предполагается, что у тебя есть статический доступ к локальному мосту)
            // Если его нет, найди игрока через FindObjectOfType или Singleton
            var localPlayer = PlayerNetworkBridge.LocalPlayer; 
            if (localPlayer)
            {
                // Получаем процент отката (0 - готов, 1 - только что прожали)
                DarkCooldownFill.fillAmount = localPlayer.GetSkillCooldownPercentage();
            }
        }
    }
}