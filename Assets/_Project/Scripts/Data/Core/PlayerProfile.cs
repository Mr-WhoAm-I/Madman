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
        
        [SerializeField] private int _crystals = 1000; // Даем 1000 для тестов
        public int Crystals => _crystals;
        
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
        
        public bool TrySpendCrystals(int amount)
        {
            if (amount < 0 || _crystals < amount) return false;
            
            _crystals -= amount;
            return true;
        }

        public void AddCrystals(int amount)
        {
            if (amount > 0) _crystals += amount;
        }
    }
}