using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    // Наследуемся от NetworkBehaviour, чтобы иметь доступ к магии Photon
    public class PlayerNetworkMovement : NetworkBehaviour
    {
        public float speed = 5f;
        public static Vector3 LocalPlayerPosition;
        public static Health LocalPlayerHealth;

        public override void Spawned()
        {
            // При спавне находим здоровье и сохраняем в статичную переменную,
            // если этот куб принадлежит нам (или мы Сервер)
            if (HasInputAuthority || HasStateAuthority)
            {
                LocalPlayerHealth = GetComponent<Health>();
            }
        }
        // FixedUpdateNetwork - это сетевой аналог Update(). Он синхронизирован у всех игроков.
        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;
            // Спрашиваем: "Есть ли ввод от игрока, которому принадлежит этот куб?"
            if (GetInput(out NetworkInputData data))
            {
                var moveDirection = new Vector3(data.MovementInput.x, data.MovementInput.y, 0f);
                transform.position += moveDirection * speed * Runner.DeltaTime;
            }
            
            if (HasInputAuthority || HasStateAuthority)
            {
                LocalPlayerPosition = transform.position;
            }
        }
    }
}