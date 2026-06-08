using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Core
{
    // Чистая структура данных, говорящая ECS-системам, есть ли у нас права сервера
    public struct NetworkAuthorityComponent : IComponentData
    {
        public bool HasStateAuthority;
    }
}