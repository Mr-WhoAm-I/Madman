using Fusion;
using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Systems;

namespace _Project.Scripts.Network
{
    // Гарантируем, что тикер сработает ПОСЛЕ того, как все PlayerNetworkBridge 
    // обновят свои данные и запишут свежий инпут в сущности (DefaultExecutionOrder > 0)
    [DefaultExecutionOrder(100)]
    public class ECSNetworkTicker : SimulationBehaviour
    {
        private EntityManager _entityManager;
        private FusionUpdateGroup _fusionGroup;
        private Entity _timeEntity;
        private bool _isInitialized;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            _entityManager = world.EntityManager;
            _fusionGroup = world.GetOrCreateSystemManaged<FusionUpdateGroup>();
            _timeEntity = _entityManager.CreateEntity(typeof(NetworkTimeComponent));
            
            _isInitialized = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isInitialized || _entityManager == default || !_entityManager.Exists(_timeEntity)) 
                return;

            // 1. Обновляем время
            _entityManager.SetComponentData(_timeEntity, new NetworkTimeComponent { DeltaTime = Runner.DeltaTime });

            // 2. ВЫЗЫВАЕМ НАШ РУЧНОЙ МЕТОД
            _fusionGroup.ManualUpdate();
        }

        private void OnDestroy()
        {
            if (_isInitialized && _entityManager != default && _entityManager.Exists(_timeEntity))
            {
                _entityManager.DestroyEntity(_timeEntity);
            }
        }
    }
}