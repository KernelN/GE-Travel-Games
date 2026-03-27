using System.Collections.Generic;
using GETravelGames.PrizeManager;

namespace GETravelGames.Common
{
    public sealed class PrizePullResult
    {
        public enum Outcome { RealPrize, FalsePrize, NoPrize }

        public Outcome Result { get; private set; }
        public PrizeClaimReservation Reservation { get; private set; }
        public string Message { get; private set; }

        /// <summary>
        /// Full ordered list of individual roll outcomes from a multi-try pull.
        /// Used to compute burst count and burst colors for the celebration animation.
        /// </summary>
        public List<PrizePullResult> RollSequence { get; set; }

        public bool IsRealPrize => Result == Outcome.RealPrize;
        public string PrizeName => Reservation?.ReservedPrize?.PrizeName ?? "";
        public string PrizeDescription => Reservation?.ReservedPrize?.PrizeDescription ?? "";

        /// <summary>The level of the won prize, or 0 if false/no prize.</summary>
        public ushort WinningLevel => Reservation?.ReservedPrize?.PrizeLevel ?? 0;

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

        /// <summary>Lightweight real-prize result used for intermediate dry-run rolls (no reservation).</summary>
        public static PrizePullResult RealPrizeDry(PrizeInstance prize)
        {
            var fakeReservation = new PrizeClaimReservation { ReservedPrize = prize };
            return new PrizePullResult
            {
                Result = Outcome.RealPrize,
                Reservation = fakeReservation,
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

        /// <summary>
        /// Returns the number of particle bursts to play for a won prize: one burst per
        /// rarity tier up to and including the prize's own level.
        ///   level 0 (Common)   → 1 burst
        ///   level 1 (Uncommon) → 2 bursts
        ///   level 2 (Rare)     → 3 bursts
        ///   level 3 (Epic)     → 4 bursts
        /// </summary>
        public static int ComputeBurstCount(ushort winningLevel) => winningLevel + 1;
    }
}
