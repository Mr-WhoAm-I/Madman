using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Header("Настройки")]
        public float moveSpeed = 5f;

        private Entity _playerEntity;
        private EntityManager _entityManager;

        public override void Spawned()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Добавили PlayerTransformSync в список
            _playerEntity = _entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementComponent),
                typeof(LocalTransform),
                typeof(PlayerTransformSync) 
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = moveSpeed });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            
            // Передаем ссылку на настоящий Transform в ECS
            _entityManager.SetComponentData(_playerEntity, new PlayerTransformSync { Value = transform });
        }

        public override void FixedUpdateNetwork()
        {
            if (!_entityManager.Exists(_playerEntity)) return;

            // 1. ROLLBACK-СИНХРОНИЗАЦИЯ (ОЧЕНЬ ВАЖНО):
            // Если Fusion "откатил" GameObject назад в прошлое, мы обязаны вернуть туда и ECS-сущность.
            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            ecsTransform.Position = transform.position;
            _entityManager.SetComponentData(_playerEntity, ecsTransform);

            // 2. Читаем и передаем инпут для этого конкретного тика
            if (GetInput(out NetworkInputData data))
            {
                var inputComp = _entityManager.GetComponentData<PlayerInputComponent>(_playerEntity);
                inputComp.MovementVector = new float2(data.MovementInput.x, data.MovementInput.y);
                _entityManager.SetComponentData(_playerEntity, inputComp);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_entityManager != default && _entityManager.Exists(_playerEntity))
            {
                _entityManager.DestroyEntity(_playerEntity);
            }
        }
    }
}