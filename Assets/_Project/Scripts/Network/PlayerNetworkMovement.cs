using Fusion;
using UnityEngine;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    public class PlayerNetworkMovement : NetworkBehaviour
    {
        [Header("Базовое передвижение")]
        public float speed = 5f;
        
        public static Vector3 LocalPlayerPosition;
        public static Health LocalPlayerHealth;

        private PlayerNetworkBridge _bridge;

        public override void Spawned()
        {
            if (HasInputAuthority || HasStateAuthority)
            {
                LocalPlayerHealth = GetComponent<Health>();
            }
            
            // Кешируем ссылку на мост, чтобы читать ECS
            _bridge = GetComponent<PlayerNetworkBridge>();
        }

        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;

            // Флаг для удобной проверки валидности ECS-сущности
            bool hasBridge = _bridge != null && _bridge.EntityManager != default && _bridge.EntityManager.Exists(_bridge.PlayerEntity);

            // 1. Проверяем, есть ли мост и жива ли сущность в ECS
            if (hasBridge)
            {
                // 2. СПРАШИВАЕМ ECS: Мы сейчас в состоянии рывка?
                if (_bridge.EntityManager.HasComponent<DashComponent>(_bridge.PlayerEntity))
                {
                    // Читаем параметры рывка, которые задала HystericSkillSystem
                    var dash = _bridge.EntityManager.GetComponentData<DashComponent>(_bridge.PlayerEntity);
                    
                    // Летим!
                    var dashMove = new Vector3(dash.Direction.x, dash.Direction.y, 0f);
                    transform.position += dashMove * dash.Speed * Runner.DeltaTime;
                    
                    // Обновляем локальную позицию и ВЫХОДИМ ИЗ МЕТОДА, чтобы обычный бег не перебил рывок
                    UpdateLocalPosition();
                    return; 
                }
            }

            // 3. ОБЫЧНОЕ ДВИЖЕНИЕ (если рывка нет)
            if (GetInput(out NetworkInputData data))
            {
                float currentSpeed = speed;

                // === ААА-МЕХАНИКА: ПАССИВКА ИСТЕРИКА (Ускорение) ===
                if (hasBridge && _bridge.EntityManager.HasComponent<HystericFuryStateTag>(_bridge.PlayerEntity))
                {
                    var config = _bridge.EntityManager.GetComponentData<SkillConfigComponent>(_bridge.PlayerEntity);
                    currentSpeed *= config.FurySpeedMultiplier;
                }

                var moveDirection = new Vector3(data.MovementInput.x, data.MovementInput.y, 0f);
                transform.position += moveDirection * currentSpeed * Runner.DeltaTime;
            }
            
            UpdateLocalPosition();
            
            if (hasBridge)
            {
                _bridge.EntityManager.SetComponentData(_bridge.PlayerEntity, 
                    Unity.Transforms.LocalTransform.FromPosition(transform.position));
            }
        }

        private void UpdateLocalPosition()
        {
            if (HasInputAuthority || HasStateAuthority)
            {
                LocalPlayerPosition = transform.position;
            }
        }
    }
}