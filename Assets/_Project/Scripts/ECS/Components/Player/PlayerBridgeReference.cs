using _Project.Scripts.Network.Bridges;
using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Player
{
    // Управляемый компонент (Managed Component) для хранения чистой ссылки на MonoBehaviour моста.
    // Не содержит никакой логики, не ломает Burst, так как используется только в системax ввода/вывода.
    public class PlayerBridgeReference : IComponentData
    {
        public PlayerNetworkBridge Bridge;
    }
}