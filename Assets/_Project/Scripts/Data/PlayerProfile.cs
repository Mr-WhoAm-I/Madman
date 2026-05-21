using System;
using System.Collections.Generic;
using System.Linq;

namespace _Project.Scripts.Data
{
    [Serializable]
    public class PlayerProfile
    {
        // Список данных для сериализации (Unity понимает List, но не Dictionary)
        public int LastSelectedArchetypeID = 0;
        public List<ArchetypeEntry> ProgressList = new ();

        [Serializable]
        public struct ArchetypeEntry
        {
            public int ArchetypeID;
            public PlayerProgressionData Data;
        }

        // Вспомогательный метод для удобного доступа к данным в коде
        public PlayerProgressionData GetProgressForArchetype(int archetypeID)
        {
            var entry = ProgressList.FirstOrDefault(x => x.ArchetypeID == archetypeID);
            if (entry.Data != null) return entry.Data;
            // Если данных нет, создаем "чистые" (первый уровень)
            var newData = new PlayerProgressionData();
            ProgressList.Add(new ArchetypeEntry { ArchetypeID = archetypeID, Data = newData });
            return newData;
        }
    }
}