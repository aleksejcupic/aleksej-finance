using System;

namespace Aleksej.Finance.Derivatives;

/// <summary>
/// Vanilla fixed-for-floating interest rate swap (IRS) valuation.
/// Values a swap as the difference between a fixed-coupon bond and a floating-rate bond.
/// All rates are continuously compounded. Based on Hull Chapter 7.
/// </summary>
public static class InterestRateSwap
{
    // ── Swap value ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Net present value of a fixed-for-floating interest rate swap.
    /// The floating leg is valued using the next-reset approximation:
    /// at the next reset date the floating bond is worth par, so its current value is
    /// (notional + next known floating coupon) discounted to today.
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="fixedRate">Annual fixed coupon rate (e.g. 0.05 = 5%)</param>
    /// <param name="paymentTimes">Remaining coupon payment times in years (strictly ascending)</param>
    /// <param name="zeroRates">Continuously compounded zero rates at each payment time</param>
    /// <param name="nextFloatCoupon">
    ///   Already-fixed floating coupon for the next period, as a fraction of notional
    ///   (e.g. 0.026 means the next floating payment is 0.026 × notional)
    /// </param>
    /// <param name="timeToNextReset">Time in years to the next floating reset date</param>
    /// <param name="zeroRateAtNextReset">Zero rate at the next floating reset date</param>
    /// <param name="isPayFixed">
    ///   True = pay fixed / receive floating (payer swap).
    ///   False = receive fixed / pay floating (receiver swap).
    /// </param>
    public static double SwapValue(
        double notional,
        double fixedRate,
        double[] paymentTimes,
        double[] zeroRates,
        double nextFloatCoupon,
        double timeToNextReset,
        double zeroRateAtNextReset,
        bool isPayFixed = true)
    {
        double bFixed = FixedLegPV(notional, fixedRate, paymentTimes, zeroRates,
                                   includePrincipal: true);
        double bFloat = FloatingLegPV(notional, nextFloatCoupon,
                                      timeToNextReset, zeroRateAtNextReset);
        return isPayFixed ? bFloat - bFixed : bFixed - bFloat;
    }

    // ── Par swap rate ──────────────────────────────────────────────────────────

    /// <summary>
    /// Par (fair) swap rate — the fixed coupon rate that makes the swap NPV zero at inception.
    /// Assumes equal accrual fractions between consecutive payment dates and from today to the
    /// first payment. For irregular schedules, provide paymentTimes spaced accordingly.
    /// K = (1 − P(T_n)) / (δ · Σ P(T_i))
    /// where P(t) = exp(−r(t) · t) and δ = 1/frequency.
    /// </summary>
    /// <param name="paymentTimes">Payment times in years (ascending, starting from first payment)</param>
    /// <param name="zeroRates">Zero rates at each payment time</param>
    /// <param name="frequency">Coupon payments per year (default 2 = semi-annual)</param>
    public static double ParSwapRate(double[] paymentTimes, double[] zeroRates, int frequency = 2)
    {
        if (paymentTimes.Length != zeroRates.Length)
            throw new ArgumentException("paymentTimes and zeroRates must have the same length.");
        if (paymentTimes.Length == 0)
            throw new ArgumentException("At least one payment time is required.");

        double delta    = 1.0 / frequency;
        double annuity  = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            annuity += delta * Math.Exp(-zeroRates[i] * paymentTimes[i]);

        double pTn = Math.Exp(-zeroRates[^1] * paymentTimes[^1]);
        return (1 - pTn) / annuity;
    }

    // ── Leg present values ────────────────────────────────────────────────────

    /// <summary>
    /// Present value of the fixed coupon leg.
    /// PV = notional · (fixedRate/frequency) · Σ exp(−r_i · t_i) [+ notional · exp(−r_n · t_n) if includePrincipal]
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="fixedRate">Annual fixed coupon rate</param>
    /// <param name="paymentTimes">Coupon payment times in years (ascending)</param>
    /// <param name="zeroRates">Zero rates at each payment time</param>
    /// <param name="frequency">Coupon payments per year (default 2)</param>
    /// <param name="includePrincipal">
    ///   True to add the discounted notional repayment (treats as a bond, needed for SwapValue).
    ///   False for the coupon-only present value (pure swap fixed leg).
    /// </param>
    public static double FixedLegPV(
        double notional,
        double fixedRate,
        double[] paymentTimes,
        double[] zeroRates,
        int frequency = 2,
        bool includePrincipal = false)
    {
        if (paymentTimes.Length != zeroRates.Length)
            throw new ArgumentException("paymentTimes and zeroRates must have the same length.");

        double coupon = notional * fixedRate / frequency;
        double pv     = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            pv += coupon * Math.Exp(-zeroRates[i] * paymentTimes[i]);

        if (includePrincipal && paymentTimes.Length > 0)
            pv += notional * Math.Exp(-zeroRates[^1] * paymentTimes[^1]);

        return pv;
    }

    /// <summary>
    /// Present value of the floating leg using the next-reset approximation.
    /// Between reset dates the floating bond is worth par at the next reset, so:
    /// PV = notional · (1 + nextFloatCoupon) · exp(−zeroRateAtNextReset · timeToNextReset)
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="nextFloatCoupon">
    ///   Already-fixed coupon for the next period as a fraction of notional
    /// </param>
    /// <param name="timeToNextReset">Time in years until the next reset date</param>
    /// <param name="zeroRateAtNextReset">Zero rate at the next reset date</param>
    public static double FloatingLegPV(
        double notional,
        double nextFloatCoupon,
        double timeToNextReset,
        double zeroRateAtNextReset)
    {
        if (timeToNextReset <= 0)
            return notional * (1 + nextFloatCoupon);
        return notional * (1 + nextFloatCoupon) * Math.Exp(-zeroRateAtNextReset * timeToNextReset);
    }

    // ── Risk ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// DV01 (dollar value of a basis point) — change in swap value for a 1 bp (0.0001)
    /// parallel upward shift of the zero curve. Computed via central finite difference.
    /// A payer swap has negative DV01 (rising rates hurt the fixed payer).
    /// A receiver swap has positive DV01.
    /// </summary>
    public static double DV01(
        double notional,
        double fixedRate,
        double[] paymentTimes,
        double[] zeroRates,
        double nextFloatCoupon,
        double timeToNextReset,
        double zeroRateAtNextReset,
        bool isPayFixed = true)
    {
        const double shift = 0.0001;

        double[] up   = new double[zeroRates.Length];
        double[] down = new double[zeroRates.Length];
        for (int i = 0; i < zeroRates.Length; i++) { up[i] = zeroRates[i] + shift; down[i] = zeroRates[i] - shift; }

        double vUp   = SwapValue(notional, fixedRate, paymentTimes, up,
                                 nextFloatCoupon, timeToNextReset, zeroRateAtNextReset + shift, isPayFixed);
        double vDown = SwapValue(notional, fixedRate, paymentTimes, down,
                                 nextFloatCoupon, timeToNextReset, zeroRateAtNextReset - shift, isPayFixed);
        return (vUp - vDown) / 2;
    }
}
