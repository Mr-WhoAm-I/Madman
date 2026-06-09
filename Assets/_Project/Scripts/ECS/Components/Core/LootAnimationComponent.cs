using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components.Core
{
    public struct LootAnimationComponent : IComponentData
    {
        public float BobbingSpeed;
        public float BobbingAmount;
        
        public float3 BasePosition; // Запоминаем точку падения, чтобы качаться вокруг нее
        public float Timer;         // Внутреннее время для синусоиды
    }
}