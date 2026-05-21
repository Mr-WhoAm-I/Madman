using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    // Этот компонент хранит данные, которые мы будем сохранять в файл
    public struct PlayerProgressionComponent : IComponentData
    {
        public int ArchetypeID; // ID того, кого качаем
        public int Level;
        public float CurrentXP;
        public float XPToNextLevel;
    
        // Сюда же в будущем лягут "Таланты" (например, список ID открытых навыков)
        // public FixedList64Bytes<int> UnlockedTalents; 
    }
}