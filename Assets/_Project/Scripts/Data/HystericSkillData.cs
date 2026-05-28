using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "HystericSkillData", menuName = "Madman/Skills/Hysteric Skill Data")]
    public class HystericSkillData : SkillData
    {
        [Header("Настройки Двойной Ярости (Торнадо)")]
        [Tooltip("Количество пуль, которые выпускаются во все стороны")]
        public int bulletCount = 8; 
        
        // В будущем сюда можно добавить модификатор урона ульты (например, damageMultiplier = 1.5f),
        // чтобы пули скилла наносили больше урона, чем обычные выстрелы.
    }
}