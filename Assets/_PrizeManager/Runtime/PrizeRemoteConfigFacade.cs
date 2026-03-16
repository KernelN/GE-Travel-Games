namespace GETravelGames.PrizeManager
{
    public sealed class PrizeRemoteConfigFacade
    {
        public PrizeRuntimeSettings CreateSettingsSnapshot(PrizeRuntimeSettings settings)
        {
            return settings == null ? new PrizeRuntimeSettings() : settings.Clone();
        }
    }
}
