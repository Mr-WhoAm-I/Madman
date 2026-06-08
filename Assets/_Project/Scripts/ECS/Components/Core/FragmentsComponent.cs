using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Core
{
    // Отражение сессионной валюты ("Фрагменты") внутри ECS мира
    public struct FragmentsComponent : IComponentData
    {
        public int Value;
    }
}