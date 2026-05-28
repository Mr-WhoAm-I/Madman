using System;
using System.Collections.Generic;
using System.Linq;

namespace _Project.Scripts.Data
{
    [Serializable]
    public class PlayerProfile
    {
        // ИСПРАВЛЕНИЕ: Убрали вызов Random.Range из инлайн-инициализатора полей. 
        // Теперь здесь только безопасная базовая строка.
        public string Nickname = "Player";
        public int LastSelectedArchetypeID = 0;
        public List<ArchetypeEntry> ProgressList = new ();

        [Serializable]
        public struct ArchetypeEntry
        {
            public int ArchetypeID;
            public PlayerProgressionData Data;
        }

        public PlayerProgressionData GetProgressForArchetype(int archetypeID)
        {
            var entry = ProgressList.FirstOrDefault(x => x.ArchetypeID == archetypeID);
            if (entry.Data != null) return entry.Data;

            var newData = new PlayerProgressionData();
            ProgressList.Add(new ArchetypeEntry { ArchetypeID = archetypeID, Data = newData });
            return newData;
        }
    }
}