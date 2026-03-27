namespace GETravelGames.Common
{
    /// <summary>
    /// Carries the current player's registration data across scene loads
    /// (RegisterUser → Game → PrizeGiving). Cleared after each session ends.
    /// </summary>
    public static class PlayerSessionData
    {
        public static string FirstName { get; set; } = "";
        public static string LastName  { get; set; } = "";
        public static string Phone     { get; set; } = "";
        public static string Office    { get; set; } = "";

        /// <summary>
        /// Number of stage milestones the player's final score cleared. Determines how
        /// many prize-giving tries (and boxes) they receive.
        /// </summary>
        public static int StageIndex { get; set; } = 0;

        public static bool HasData => !string.IsNullOrWhiteSpace(Phone);

        public static void Clear()
        {
            FirstName = LastName = Phone = Office = "";
            StageIndex = 0;
        }
    }
}
