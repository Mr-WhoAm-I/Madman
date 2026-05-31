using UnityEngine;

namespace _Project.Scripts.Data.Shop
{
    [CreateAssetMenu(fileName = "NewUpgrade", menuName = "Madman/Shop/Upgrade Data")]
    public class UpgradeData : ScriptableObject
    {
        [Header("Отображение в Магазине")]
        public string upgradeID; // Уникальный идентификатор (например "steroids_1")
        public string displayName;
        [TextArea(2, 4)] 
        public string description;
        public Sprite icon;
        
        [Header("Экономика")]
        public UpgradeRarity rarity;
        public int baseCost;

        [Header("Механика (Что меняем)")]
        public UpgradeType upgradeType;
        [Tooltip("Значение модификатора. Для FlatDamage это целое число (10), для MoveSpeed это процент (0.15 = 15%)")]
        public float value;

        [Header("Дерево прокачки (Зависимости)")]
        [Tooltip("Оставьте пустым, если это базовый перк. Иначе выберите перк предыдущего уровня.")]
        public UpgradeData requiredUpgrade;
        
        [Tooltip("Оставьте -1, если доступно всем. Иначе укажите ID архетипа (0 - Истерик, 1 - Параноик и т.д.)")]
        public int requiredArchetypeID = -1; 
    }
}