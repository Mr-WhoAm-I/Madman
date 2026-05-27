using Fusion;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    public class TurretNetworkBridge : NetworkBehaviour
    {
        [Networked] public float Health { get; set; }
        [Networked] public float MaxHealth { get; set; }
        [Networked] public int OwnerArchetypeID { get; set; } // Передается при спавне

        private Entity _turretEntity;
        private EntityManager _entityManager;

        public override void Spawned()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var archetypeData = ProfileController.Instance.GetArchetypeAsset(OwnerArchetypeID);

            // 1. Инициализация статов (только на сервере)
            if (HasStateAuthority)
            {
                float baseHp = archetypeData != null ? archetypeData.turretBaseHealth : 500f;
                // TODO: Здесь мы добавим множитель от PlayerProgressionData (уровня игрока)
                MaxHealth = baseHp; 
                Health = MaxHealth;
            }

            // 2. Создаем ECS-аватар турели, чтобы враги её видели
            _turretEntity = _entityManager.CreateEntity(
                typeof(TargetableTag),
                typeof(TauntComponent),
                typeof(LocalTransform)
            );

            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));

            float tauntRad = archetypeData != null ? archetypeData.turretTauntRadius : 10f;
            float tauntDur = archetypeData != null ? archetypeData.turretTauntDuration : 5f;

            _entityManager.SetComponentData(_turretEntity, new TauntComponent 
            { 
                Radius = tauntRad,
                TimeRemaining = tauntDur
            });
        }

        public override void FixedUpdateNetwork()
        {
            if (!_entityManager.Exists(_turretEntity)) return;

            // Синхронизируем позицию (если вдруг турель может отбрасывать)
            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));

            // Обновляем таймер Агро (работает идеально с откатами Fusion)
            if (_entityManager.HasComponent<TauntComponent>(_turretEntity))
            {
                var taunt = _entityManager.GetComponentData<TauntComponent>(_turretEntity);
                taunt.TimeRemaining -= Runner.DeltaTime;

                if (taunt.TimeRemaining <= 0)
                {
                    // Время вышло: удаляем TauntComponent! Турель остается Targetable, но больше не стягивает всех на себя
                    _entityManager.RemoveComponent<TauntComponent>(_turretEntity);
                }
                else
                {
                    _entityManager.SetComponentData(_turretEntity, taunt);
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_entityManager != default && _entityManager.Exists(_turretEntity))
            {
                _entityManager.DestroyEntity(_turretEntity);
            }
        }
    }
}