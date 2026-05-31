using Fusion;
using Unity.Cinemachine;
using UnityEngine;

namespace _Project.Scripts.Network.Core
{
    public class LocalCameraSetup : NetworkBehaviour
    {
        private CinemachineCamera _virtualCamera; 

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                AttachCamera();
            }
        }

        private void Update()
        {
            // МАГИЯ ЗДЕСЬ: Если это наш игрок, но камера вдруг пропала (удалилась при смене сцены)
            // скрипт автоматически найдет новую камеру на новой сцене!
            if (HasInputAuthority && _virtualCamera == null)
            {
                AttachCamera();
            }
        }

        private void AttachCamera()
        {
            _virtualCamera = FindAnyObjectByType<CinemachineCamera>();

            if (_virtualCamera == null) return;
            _virtualCamera.Follow = transform;
            Debug.Log("[Камера] Успешно привязана к локальному игроку.");
        }
    }
}