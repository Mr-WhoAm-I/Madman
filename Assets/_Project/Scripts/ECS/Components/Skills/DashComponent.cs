using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Components.Skills
{
    public struct DashComponent : IComponentData
    {
        public float TimeLeft;      // Сколько еще лететь
        public float Speed;      // Скорость полета
        public Vector2 Direction;// Куда летим
    }
}