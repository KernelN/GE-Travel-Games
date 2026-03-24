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

        public static bool HasData => !string.IsNullOrWhiteSpace(Phone);

        public static void Clear()
        {
            FirstName = LastName = Phone = Office = "";
        }
    }
}
