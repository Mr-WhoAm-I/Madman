using Fusion;
using Unity.Entities;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Systems;

namespace _Project.Scripts.Network
{
    public class ECSNetworkTicker : SimulationBehaviour
    {
        private EntityManager _entityManager;
        private FusionUpdateGroup _fusionGroup;
        private Entity _timeEntity;
        private bool _isInitialized;

        // Используем Start, так как скрипт висит на локальном менеджере, а не на сетевом префабе
        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            _entityManager = world.EntityManager;
            
            // Получаем доступ к нашей ручной группе систем
            _fusionGroup = world.GetOrCreateSystemManaged<FusionUpdateGroup>();
            
            // Создаем сущность для хранения времени
            _timeEntity = _entityManager.CreateEntity(typeof(NetworkTimeComponent));
            
            _isInitialized = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isInitialized || _entityManager == default || !_entityManager.Exists(_timeEntity)) 
                return;

            // 1. Записываем текущую сетевую дельту времени в синглтон
            _entityManager.SetComponentData(_timeEntity, new NetworkTimeComponent { DeltaTime = Runner.DeltaTime });

            // 2. ЗАПУСКАЕМ СИМУЛЯЦИЮ ECS (Всех сетевых объектов)
            _fusionGroup.Update();
        }

        // Очищаем память при выходе из сессии
        private void OnDestroy()
        {
            if (_isInitialized && _entityManager != default && _entityManager.Exists(_timeEntity))
            {
                _entityManager.DestroyEntity(_timeEntity);
            }
        }
    }
}