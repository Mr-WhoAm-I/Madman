using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    public struct ManaComponent : IComponentData
    {
        public float CurrentMana;
        public float RegenCooldownTimer; // Таймер блокировки регенерации после траты маны
    }
}