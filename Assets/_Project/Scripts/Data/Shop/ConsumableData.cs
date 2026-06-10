// Путь: Assets/_Project/Scripts/Data/Shop/ConsumableData.cs
using UnityEngine;

namespace _Project.Scripts.Data.Shop
{
    public enum ConsumableType 
    { 
        Heal,       // Восстанавливает здоровье
        Mana,       // Восстанавливает ману
        Shield,     // Дает временный щит (Параноику)
        FuryBuff    // Дает временный бафф (например, скорости или урона)
    }

    [CreateAssetMenu(fileName = "NewConsumable", menuName = "Madman/Consumable Data")]
    public class ConsumableData : ScriptableObject
    {
        [Header("Отображение")]
        public string consumableID;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Экономика (Мета-магазин)")]
        [Tooltip("Стоимость разблокировки предмета в Осколках памяти")]
        public int unlockCost = 150;

        [Header("Механика (Эстус-фляга)")]
        public ConsumableType type;
        
        [Tooltip("Сила эффекта (Сколько ХП/Маны восстанавливает)")]
        public float value; 
        
        [Tooltip("Максимум зарядов на одну миссию")]
        public int maxChargesPerMission = 3; 
        
        [Tooltip("Задержка перед повторным применением (чтобы не выпить все 3 сразу)")]
        public float cooldown = 15f; 
    }
}