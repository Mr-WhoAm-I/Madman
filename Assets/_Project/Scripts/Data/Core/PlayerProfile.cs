using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Scripts.Data.Core
{
    [Serializable]
    public class PlayerProfile
    {
        public string Nickname = "Player";
        public int LastSelectedArchetypeID = 0;
        
        [SerializeField] private int _memoryShards = 1000; // Осколки памяти (Мета-валюта)
        public int MemoryShards => _memoryShards;
        
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
        
        // Безопасная транзакция с защитой от отрицательных значений
        public bool TrySpendMemoryShards(int amount)
        {
            if (amount < 0 || _memoryShards < amount) return false;
            
            _memoryShards -= amount;
            return true;
        }

        public void AddMemoryShards(int amount)
        {
            if (amount > 0) _memoryShards += amount;
        }
    }
}