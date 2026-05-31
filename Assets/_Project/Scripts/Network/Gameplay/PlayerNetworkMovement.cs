using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network.Bridges;
using _Project.Scripts.Network.Core;
using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network.Gameplay
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
            
            _bridge = GetComponent<PlayerNetworkBridge>();
        }

        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;

            bool hasBridge = _bridge != null && _bridge.EntityManager != default && _bridge.EntityManager.Exists(_bridge.PlayerEntity);

            if (hasBridge)
            {
                if (_bridge.EntityManager.HasComponent<DashComponent>(_bridge.PlayerEntity))
                {
                    var dash = _bridge.EntityManager.GetComponentData<DashComponent>(_bridge.PlayerEntity);
                    var dashMove = new Vector3(dash.Direction.x, dash.Direction.y, 0f);
                    transform.position += dashMove * dash.Speed * Runner.DeltaTime;
                    
                    UpdateLocalPosition();
                    return; 
                }
            }

            if (GetInput(out NetworkInputData data))
            {
                float currentSpeed = speed;

                if (hasBridge)
                {
                    // === ААА-МЕХАНИКА: ПАССИВКА ИСТЕРИКА (Ускорение) ===
                    if (_bridge.EntityManager.HasComponent<HystericFuryStateTag>(_bridge.PlayerEntity))
                    {
                        var config = _bridge.EntityManager.GetComponentData<SkillConfigComponent>(_bridge.PlayerEntity);
                        currentSpeed *= config.FurySpeedMultiplier;
                    }

                    // === МЕХАНИКА: ПАРКУР ШИЗОИДА (Скорость в инвизе) ===
                    if (_bridge.EntityManager.HasComponent<InvisibilityStateComponent>(_bridge.PlayerEntity))
                    {
                        var invis = _bridge.EntityManager.GetComponentData<InvisibilityStateComponent>(_bridge.PlayerEntity);
                        currentSpeed *= invis.SpeedMultiplier; // Умножаем на наш 1.4 (или другой бонус)
                    }
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