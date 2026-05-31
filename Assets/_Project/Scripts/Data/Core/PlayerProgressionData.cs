using System;

namespace _Project.Scripts.Data.Core
{
    [Serializable]
    public class PlayerProgressionData
    {
        public int Level = 1;
        public float CurrentXP = 0f;
        public float XPToNextLevel = 100f;
        
        // Сюда в будущем добавим список открытых талантов
        // public List<int> UnlockedTalentIDs = new List<int>();
    }
}