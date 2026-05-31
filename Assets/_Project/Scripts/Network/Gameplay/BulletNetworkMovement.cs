using System.Collections.Generic;
using Fusion;
using Unity.Entities;
using UnityEngine;

// ДОБАВЛЕНО для работы с Entity

namespace _Project.Scripts.Network.Gameplay
{
    public class BulletNetworkMovement : NetworkBehaviour
    {
        public float speed = 10f;
        public float damage = 25f;
        
        // ДОБАВЛЕНО: Ссылка на ECS-сущность стрелка (Игрока или Клона)
        public Entity SourceEntity; 
        
        public static readonly List<BulletNetworkMovement> ActiveBullets = new();
        public bool isHit; 
        
        [Networked] private TickTimer LifeTimer { get; set; }

        // ИСПРАВЛЕНО: Добавлен параметр sourceEntity
        public void InitNetworkState(float lifeTime, float newDamage, float newSpeed, Entity sourceEntity)
        {
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
            damage = newDamage;
            speed = newSpeed;
            SourceEntity = sourceEntity; // Запоминаем автора
        }
        
        public override void Spawned()
        {
            ActiveBullets.Add(this);

            if (HasStateAuthority)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, 2.0f);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActiveBullets.Remove(this);
        }

        public override void FixedUpdateNetwork()
        {
            transform.position += transform.up * speed * Runner.DeltaTime;

            if (HasStateAuthority && LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasStateAuthority) return;

            if (other.TryGetComponent<Health>(out var health))
            {
                if (Object.InputAuthority == health.Object.InputAuthority) return;
                health.TakeDamage(damage);
                Runner.Despawn(Object);
            }
        }
    }
}