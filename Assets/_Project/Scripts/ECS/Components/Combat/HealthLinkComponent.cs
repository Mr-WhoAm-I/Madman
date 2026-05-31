using _Project.Scripts.Network;
using _Project.Scripts.Network.Gameplay;
using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    public class HealthLinkComponent : IComponentData
    {
        public Health Value;
    }
}