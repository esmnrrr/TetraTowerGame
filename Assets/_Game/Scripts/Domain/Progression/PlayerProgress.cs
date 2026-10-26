using System;
using System.Collections.Generic;

namespace Game.Domain.Progression
{
    [Serializable]
    public class LevelResultData
    {
        public string levelID;
        public int starsEarned; // 0 - 3
        public bool isUnlocked;
    }

    [Serializable]
    public class PlayerProgress
    {
        public List<LevelResultData> levelResults = new List<LevelResultData>();
        public int totalStars = 0;
    }
}