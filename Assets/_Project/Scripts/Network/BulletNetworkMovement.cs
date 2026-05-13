using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace _Project.Scripts.Network
{
    public class BulletNetworkMovement : NetworkBehaviour
    {
        public float speed = 10f;
        public float damage = 25f;
        
        // 1. Статический список всех активных пуль на сцене
        public static readonly List<BulletNetworkMovement> ActiveBullets = new();
        
        // 2. Флаг, чтобы одна пуля не убила двоих врагов за один кадр
        public bool isHit; 
        
        [Networked] private TickTimer LifeTimer { get; set; }

        public void InitNetworkState(float lifeTime, float newDamage, float newSpeed)
        {
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
            damage = newDamage;
            speed = newSpeed;
        }
        
        public override void Spawned()
        {
            // Добавляем пулю в реестр, когда она появляется
            ActiveBullets.Add(this);

            if (HasStateAuthority)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, 2.0f);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Удаляем пулю из реестра перед ее уничтожением
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

        // Оставляем этот метод как есть! Он все еще нужен для попаданий по другим ИГРОКАМ
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