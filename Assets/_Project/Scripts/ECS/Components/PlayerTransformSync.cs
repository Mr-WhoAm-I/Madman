using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Components
{
    public class PlayerTransformSync : IComponentData
    {
        public Transform Value;
    }
}