using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    // ТЕГИ ДЛЯ ЩИТОВ
    // Вешается, если щит получен от ауры Параноика
    public struct AuraShieldTag : IComponentData { } 
    
    // Вешается, если щит куплен/выпит из зелья (чтобы аура его не стерла)
    public struct PermanentShieldTag : IComponentData { }

    // УНИВЕРСАЛЬНЫЙ БАФФ (ААА-стандарт)
    public struct ActiveBuffComponent : IComponentData
    {
        public int BuffType; // 0 = Ярость (Урон/Скорость), 1 = Защита и т.д.
        public float Power;  // Сила баффа
        public float Timer;  // Сколько секунд осталось
    }
}