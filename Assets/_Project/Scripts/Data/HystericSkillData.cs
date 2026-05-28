using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "HystericSkillData", menuName = "Madman/Skills/Hysteric Skill Data")]
    public class HystericSkillData : SkillData
    {
        [Header("Настройки Свинцового Торнадо")]
        [Tooltip("Количество пуль, выпускаемых во все стороны")]
        public int bulletCount = 8; 
        
        [Tooltip("Множитель урона для пуль ульты")]
        public float damageMultiplier = 1.5f;

        [Header("Настройки Рывка (Dash)")]
        [Tooltip("Скорость, с которой летит Истерик во время ульты")]
        public float dashSpeed = 25f;
        
        [Tooltip("Как долго длится рывок (в секундах)")]
        public float dashDuration = 0.2f;
    }
}