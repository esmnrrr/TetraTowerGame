namespace Game.Domain.Turns
{
    // Oyun bittiğinde UI'a gönderilecek tüm bilgileri tutan paket (DTO)
    public class GameResultData
    {
        public GameMode Mode { get; set; }
        public int ScoreOrTurns { get; set; }
        
        // Pathway Modu İçin
        public int LevelIndex { get; set; }
        public int StarsEarned { get; set; }
        public bool IsTowerCollapsed { get; set; }
        
        // VS / Multiplayer Modları İçin
        public bool IsWinner { get; set; } 
        public string WinnerName { get; set; } 
    }
}