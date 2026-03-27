using System.Collections.Generic;
using System.Linq;
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
        /// Computes how many particle-burst explosions should play during the celebration
        /// animation, based on the roll sequence.
        ///
        /// Rules:
        ///   +1  if the sequence started with a false/no-prize AND a real prize was
        ///       later reached  ("escape" burst)
        ///   +1  the first time each distinct prize level appears in the sequence
        /// </summary>
        public static int ComputeBurstCount(List<PrizePullResult> sequence)
        {
            if (sequence == null || sequence.Count == 0) return 0;

            bool startedWithFalse = sequence[0].Result != Outcome.RealPrize;
            bool everReal = sequence.Any(r => r.Result == Outcome.RealPrize);

            int bursts = 0;
            if (startedWithFalse && everReal) bursts++; // escape burst

            var seenLevels = new HashSet<ushort>();
            foreach (var roll in sequence)
                if (roll.Result == Outcome.RealPrize && seenLevels.Add(roll.WinningLevel))
                    bursts++;

            return bursts;
        }
    }
}
