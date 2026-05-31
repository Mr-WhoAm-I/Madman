using UnityEngine;

namespace _Project.Scripts.Data.Skills
{
    [CreateAssetMenu(fileName = "ParanoiacSkillData", menuName = "Madman/Skills/Paranoiac Skill Data")]
    public class ParanoiacSkillData : SkillData
    {
        [Header("Пассивный навык: Энергетический барьер")]
        [Tooltip("Максимальная прочность щита")]
        public float shieldCapacity = 100f;
        
        [Tooltip("Время без получения урона до начала регенерации (в секундах)")]
        public float shieldRechargeTime = 5f;
        
        [Tooltip("Радиус командной ауры, передающей щит союзникам")]
        public float shieldAuraRadius = 3f;

        [Header("Активный навык: Установка Цербер")]
        [Tooltip("Сколько турелей можно поставить одновременно")]
        public int maxTurrets = 1;
        
        [Tooltip("Время жизни турели в секундах до самоуничтожения")]
        public float turretLifeTime = 15f;
        
        [Tooltip("Боевые настройки самой турели (перетащи сюда TurretSkillData)")]
        public TurretSkillData turretCombatSettings;
    }
}