using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components
{
    // Компонент пассивного навыка "Квантовая нестабильность"
    public struct QuantumInstabilityComponent : IComponentData
    {
        public int CurrentStacks;       // Текущее количество накопленных стаков
        public float Timer;             // Внутренний таймер накопления следующего стака
        public float TimeSinceLastDamage; // Таймер безопасности: сколько секунд назад получали урон
    }

    // Состояние невидимости игрока
    public struct InvisibilityStateComponent : IComponentData
    {
        public float TimeRemaining;          // Сколько секунд скрытности осталось тикать
        public float SpeedMultiplier;        // На сколько умножать скорость (по дефолту 1.0f, с перком "Паркур" 1.4f)
        public bool IsFirstShotBonusActive;  // Флаг для перка "Удар из тени"
    }

    // Компонент-команда. Добавляется на игрока на 1 сетевой тик, чтобы мост выбросил Клона в сеть
    public struct SpawnCloneCommand : IComponentData
    {
        public float3 SpawnPosition;
        public float2 RunDirection;
    }

    // Компонент, управляющий поведением самого Клона в ECS
    public struct CloneComponent : IComponentData
    {
        public Entity OwnerEntity; // Ссылка на настоящего игрока
    }
    
    public struct PoisonDebuffComponent : IComponentData
    {
        public float DPS;
        public Entity OwnerEntity; // Владелец, чтобы засчитать убийство
    }
}