using _Project.Scripts.Data.Weapons;
using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    public struct TakeDamageComponent : IComponentData
    {
        public float Amount; // Сколько урона нанести
        public Entity SourceEntity; // КТО нанес урон (чтобы Меланхолик мог накинуть стак Апатии в ответ)
        public WeaponElementalType Element; // <- ОПРЕДЕЛЯЕТ ЦВЕТ ЦИФР И ДЕБАФФЫ
        public bool IsCritical;
    }
}