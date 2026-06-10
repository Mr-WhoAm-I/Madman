using Unity.Entities;
using _Project.Scripts.Data.Shop;

namespace _Project.Scripts.ECS.Components.Player
{
    public struct ConsumableSlot
    {
        public bool IsEmpty; // Есть ли тут зелье?
        public ConsumableType Type; // Тип зелья (ХП, Мана и т.д.)
        public float Power; // Сколько восстанавливает
        public int CurrentCharges; // Оставшиеся глотки на текущую миссию
        public float MaxCooldown; // Общий кулдаун зелья
        public float CurrentCooldown; // Текущий таймер кулдауна
    }

    public struct ConsumableInventoryComponent : IComponentData
    {
        public ConsumableSlot Slot1;
        public ConsumableSlot Slot2;
    }
}