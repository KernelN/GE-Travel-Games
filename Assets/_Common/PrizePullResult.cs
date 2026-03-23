using GETravelGames.PrizeManager;

namespace GETravelGames.Common
{
    public sealed class PrizePullResult
    {
        public enum Outcome { RealPrize, FalsePrize, NoPrize }

        public Outcome Result { get; private set; }
        public PrizeClaimReservation Reservation { get; private set; }
        public string Message { get; private set; }

        public bool IsRealPrize => Result == Outcome.RealPrize;
        public string PrizeName => Reservation?.ReservedPrize?.PrizeName ?? "";
        public string PrizeDescription => Reservation?.ReservedPrize?.PrizeDescription ?? "";

        PrizePullResult() { }

        public static PrizePullResult RealPrize(PrizeClaimReservation reservation)
        {
            return new PrizePullResult
            {
                Result = Outcome.RealPrize,
                Reservation = reservation,
                Message = string.Empty,
            };
        }

        public static PrizePullResult FalsePrize()
        {
            return new PrizePullResult
            {
                Result = Outcome.FalsePrize,
                Message = string.Empty,
            };
        }

        public static PrizePullResult NoPrize(string message = "")
        {
            return new PrizePullResult
            {
                Result = Outcome.NoPrize,
                Message = message ?? string.Empty,
            };
        }
    }
}
