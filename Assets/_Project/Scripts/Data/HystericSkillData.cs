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
        
        [Header("Пассивный навык: Двойная ярость")]
        [Tooltip("Порог здоровья для активации ярости (0.3 = 30%)")]
        public float furyHealthThreshold = 0.3f;
        
        [Tooltip("Множитель скорости бега в состоянии ярости")]
        public float furySpeedMultiplier = 1.5f;
        
        [Tooltip("Процент вампиризма в состоянии ярости (0.05 = 5%)")]
        public float furyLifesteal = 0f;

        [Header("Активный навык: Свинцовое торнадо")]
        [Tooltip("Множитель выпускаемых пуль при ульте")]
        public int tornadoBulletMultiplier = 1;

        [Header("Будущие оружейные перки")]
        public int pierceCount = 0;
        public int ricochetCount = 0;
        public int extraProjectiles = 0;
    }
}