using Unity.Entities;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Components
{
    public class HealthLinkComponent : IComponentData
    {
        public Health Value;
    }
}