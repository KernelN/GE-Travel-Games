namespace GETravelGames.PrizeManager
{
    public sealed class CloudCodePrizeAdminGateway
    {
        public PrizeAdminOperationResult BuildUnavailableResult(string operationName)
        {
            return new PrizeAdminOperationResult
            {
                Success = false,
                Summary = $"{operationName} is not wired to a live Cloud Code backend in Prize Manager slice 1.",
            };
        }
    }
}
