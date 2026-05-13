using Fusion;
using Unity.Cinemachine; 
using UnityEngine;

namespace _Project.Scripts.Network
{
    public class LocalCameraSetup : NetworkBehaviour
    {
        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                // Используем самый современный и быстрый метод Unity без сортировки
                var vcam = FindAnyObjectByType<CinemachineCamera>();

                if (vcam == null) return;
                vcam.Follow = transform;
                    
                Debug.Log("Камера успешно привязана к локальному игроку.");
            }
        }
    }
}