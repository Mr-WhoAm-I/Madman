using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components
{
    // Дебафф замедления от выстрелов Меланхолика. Вешается на мобов.
    public struct FrostSlowComponent : IComponentData
    {
        public float SpeedMultiplier; // Отражает остаточную скорость (например, 0.8f = 80% от базовой скорости)
        public float TimeRemaining;   // Таймер затухания замедления
    }

    // Дебафф "Апатия". Отслеживает стаки и заморозку. Вешается на мобов.
    public struct ApathyDebuffComponent : IComponentData
    {
        public int CurrentStacks;      // Текущее число стаков
        public float FreezeTimer;      // Если таймер > 0, враг превращен в глыбу льда
        public float DebuffLifeTimer;  // Сколько времени стаки еще "живут", прежде чем спадут
    }

    // Команда для каста ульты (вешается на игрока, мост спавнит префаб)
    public struct SpawnIceProjectileCommand : IComponentData
    {
        public float2 CastDirection; // Направление выстрела из джойстика
    }
}