using System.Collections.Generic;
using _Project.Scripts.Data.Weapons; // Для доступа к WeaponElementalType
using Fusion;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.Network.Gameplay
{
    public class BulletNetworkMovement : NetworkBehaviour
    {
        public float speed = 10f;
        public float damage = 25f;
        
        public Entity SourceEntity; 
        
        public static readonly List<BulletNetworkMovement> ActiveBullets = new();
        
        [Networked] private TickTimer LifeTimer { get; set; }
        
        public bool pierceEnemies;
        public WeaponElementalType currentElement;
        public bool isCritical;
        
        public bool isDespawning; 
        public HashSet<Entity> HitEntities = new();
            
        [Header("Спрайты стихий")]
        public Sprite physicalSprite;
        public Sprite fireSprite;
        public Sprite cryoSprite;
        public Sprite toxicSprite;
        
        // Коллекция для предотвращения двойного урона при пробивании
        private HashSet<Health> _hitTargets = new(); 

        public void InitNetworkState(float lifeTime, float newDamage, float newSpeed, Entity sourceEntity, bool pierce, WeaponElementalType element, bool isCrit)
        {
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
            damage = newDamage;
            speed = newSpeed;
            SourceEntity = sourceEntity; 
            pierceEnemies = pierce;
            currentElement = element;
            isCritical = isCrit;
        }
        
        public override void Spawned()
        {
            ActiveBullets.Add(this);
            HitEntities.Clear();
            isDespawning = false;

            if (HasStateAuthority)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, 2.0f);
            }
            UpdateSprite();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActiveBullets.Remove(this);
            HitEntities.Clear();
        }

        public override void FixedUpdateNetwork()
        {
            transform.position += transform.right * speed * Runner.DeltaTime;

            if (HasStateAuthority && LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }
        
        private void UpdateSprite()
        {
            var sr = GetComponent<SpriteRenderer>();
            sr.sprite = currentElement switch
            {
                WeaponElementalType.Physical => physicalSprite,
                WeaponElementalType.Fire => fireSprite,
                WeaponElementalType.Cryo => cryoSprite,
                WeaponElementalType.Toxic => toxicSprite,
                _ => sr.sprite
            };
        }
    }
}