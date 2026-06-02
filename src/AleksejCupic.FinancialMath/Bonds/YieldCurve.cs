using System;

namespace AleksejCupic.FinancialMath.Bonds;

/// <summary>
/// Yield curve construction and conversions between rate representations.
/// Provides bootstrapping of a zero (spot) curve from par rates or bond prices,
/// conversion between par, zero, and forward rate curves, and discount factors.
/// Based on Hull Chapter 4.
/// </summary>
public static class YieldCurve
{
    // ── Discount factors ──────────────────────────────────────────────────────

    /// <summary>
    /// Discount factor P(0, T) from a continuously compounded zero rate.
    /// P = exp(−r · T)
    /// </summary>
    public static double DiscountFactor(double zeroRate, double t) =>
        Math.Exp(-zeroRate * t);

    /// <summary>
    /// Discount factor from a simply-compounded rate (e.g. LIBOR convention).
    /// P = 1 / (1 + R · δ)
    /// </summary>
    public static double DiscountFactorSimple(double simpleRate, double delta) =>
        1.0 / (1 + simpleRate * delta);

    /// <summary>
    /// Array of discount factors for a set of maturities and corresponding zero rates.
    /// </summary>
    public static double[] DiscountFactors(double[] zeroRates, double[] maturities)
    {
        ValidateSameLength(zeroRates, maturities);
        var df = new double[zeroRates.Length];
        for (int i = 0; i < zeroRates.Length; i++)
            df[i] = DiscountFactor(zeroRates[i], maturities[i]);
        return df;
    }

    // ── Rate conversions ──────────────────────────────────────────────────────

    /// <summary>
    /// Convert a simply-compounded rate R (e.g. LIBOR, semi-annual bond yield)
    /// to a continuously compounded zero rate.
    /// r_cont = m · ln(1 + R/m),  where m = compounding frequency per year.
    /// </summary>
    /// <param name="simpleRate">Simply- or periodically-compounded rate</param>
    /// <param name="frequency">Compounding frequency (1=annual, 2=semi-annual, 4=quarterly, 12=monthly, 365=daily)</param>
    public static double ToContinuous(double simpleRate, int frequency = 2) =>
        frequency * Math.Log(1 + simpleRate / frequency);

    /// <summary>
    /// Convert a continuously compounded rate r to a simply-compounded rate
    /// with the given compounding frequency.
    /// R = m · (exp(r/m) − 1)
    /// </summary>
    public static double FromContinuous(double continuousRate, int frequency = 2) =>
        frequency * (Math.Exp(continuousRate / frequency) - 1);

    // ── Forward rates ─────────────────────────────────────────────────────────

    /// <summary>
    /// Continuously compounded instantaneous forward rate at time t,
    /// implied by a zero rate term structure via linear interpolation.
    /// f(t) ≈ r(t) + t · dr/dt
    /// </summary>
    /// <param name="maturities">Zero curve maturities in years (ascending)</param>
    /// <param name="zeroRates">Continuously compounded zero rates at each maturity</param>
    /// <param name="t">Maturity at which to compute the forward rate</param>
    public static double InstantaneousForwardRate(
        double[] maturities, double[] zeroRates, double t)
    {
        ValidateSameLength(maturities, zeroRates);
        double r = InterpolateZeroRate(maturities, zeroRates, t);
        double dt = 1e-4;
        double rUp = InterpolateZeroRate(maturities, zeroRates, t + dt);
        return r + t * (rUp - r) / dt;
    }

    /// <summary>
    /// Continuously compounded forward rate for the period [t1, t2].
    /// f(t1, t2) = (r2·t2 − r1·t1) / (t2 − t1)
    /// </summary>
    /// <param name="r1">Zero rate to t1</param>
    /// <param name="t1">Start of forward period</param>
    /// <param name="r2">Zero rate to t2</param>
    /// <param name="t2">End of forward period (must be &gt; t1)</param>
    public static double ForwardRate(double r1, double t1, double r2, double t2)
    {
        if (t2 <= t1) throw new ArgumentException("t2 must be greater than t1.");
        return (r2 * t2 - r1 * t1) / (t2 - t1);
    }

    /// <summary>
    /// Computes the full forward rate curve from a zero curve.
    /// Each forward rate covers the period [maturities[i−1], maturities[i]].
    /// The first element is the zero rate to maturities[0] (no prior point to difference against).
    /// </summary>
    public static double[] ForwardRateCurve(double[] maturities, double[] zeroRates)
    {
        ValidateSameLength(maturities, zeroRates);
        var fwd = new double[maturities.Length];
        fwd[0] = zeroRates[0];
        for (int i = 1; i < maturities.Length; i++)
            fwd[i] = ForwardRate(zeroRates[i - 1], maturities[i - 1], zeroRates[i], maturities[i]);
        return fwd;
    }

    // ── Par rates ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Par yield (coupon rate that makes bond price equal to face value)
    /// at a given maturity, computed from the zero curve.
    /// y = (1 − P(T)) / Σ P(t_i) · δ
    /// where P(t) = exp(−r(t)·t) and δ = 1/frequency.
    /// </summary>
    /// <param name="maturities">Zero curve maturities in years (ascending, evenly spaced matching frequency)</param>
    /// <param name="zeroRates">Zero rates at each maturity</param>
    /// <param name="targetMaturity">The desired par yield maturity</param>
    /// <param name="frequency">Coupon payments per year (default 2 = semi-annual)</param>
    public static double ParYield(
        double[] maturities, double[] zeroRates,
        double targetMaturity, int frequency = 2)
    {
        ValidateSameLength(maturities, zeroRates);

        // Collect discount factors up to targetMaturity
        double delta   = 1.0 / frequency;
        double annuity = 0;
        double pFinal  = 0;

        for (int i = 0; i < maturities.Length; i++)
        {
            if (maturities[i] > targetMaturity + 1e-10) break;
            double p   = DiscountFactor(InterpolateZeroRate(maturities, zeroRates, maturities[i]),
                                        maturities[i]);
            annuity   += delta * p;
            pFinal     = p;
        }

        if (annuity < 1e-14) return double.NaN;
        return (1 - pFinal) / annuity;
    }

    // ── Zero curve bootstrapping ──────────────────────────────────────────────

    /// <summary>
    /// Bootstrap continuously compounded zero rates from par coupon rates.
    /// Uses the standard iterative bootstrapping procedure:
    /// the zero rate at each maturity is solved so that a par bond prices at face value.
    ///
    /// Assumes equally spaced coupon payment dates at 1/frequency year intervals.
    /// The first maturity is assumed to be a zero-coupon instrument (coupon = 0 or short-tenor).
    /// </summary>
    /// <param name="maturities">Coupon bond maturities in years (ascending, equally spaced by 1/frequency)</param>
    /// <param name="parRates">Par coupon rates at each maturity (e.g. 0.05 = 5%)</param>
    /// <param name="frequency">Coupon payments per year (default 2 = semi-annual)</param>
    /// <returns>Array of bootstrapped continuously compounded zero rates</returns>
    public static double[] Bootstrap(double[] maturities, double[] parRates, int frequency = 2)
    {
        ValidateSameLength(maturities, parRates);
        int n       = maturities.Length;
        var zeros   = new double[n];
        double delta = 1.0 / frequency;

        for (int i = 0; i < n; i++)
        {
            double c   = parRates[i];
            double T   = maturities[i];

            // Coupon PV from all prior periods (already bootstrapped)
            double pvCoupons = 0;
            for (int j = 0; j < i; j++)
            {
                double t = maturities[j];
                pvCoupons += c * delta * Math.Exp(-zeros[j] * t);
            }

            // Solve: pvCoupons + (1 + c·δ) · exp(−z·T) = 1
            // => exp(−z·T) = (1 − pvCoupons) / (1 + c·δ)
            double pFinal = (1 - pvCoupons) / (1 + c * delta);
            zeros[i]      = -Math.Log(pFinal) / T;
        }
        return zeros;
    }

    /// <summary>
    /// Bootstrap zero rates from a set of coupon bond prices.
    /// Each bond pays coupon c·δ·face at each coupon date and face at maturity.
    /// </summary>
    /// <param name="maturities">Bond maturities in years (ascending, equally spaced by 1/frequency)</param>
    /// <param name="couponRates">Annual coupon rates (e.g. 0.06 = 6%)</param>
    /// <param name="prices">Observed dirty prices as a fraction of face (e.g. 1.02 = 102)</param>
    /// <param name="frequency">Coupon payments per year (default 2)</param>
    public static double[] BootstrapFromPrices(
        double[] maturities, double[] couponRates, double[] prices, int frequency = 2)
    {
        ValidateSameLength(maturities, couponRates);
        ValidateSameLength(maturities, prices);
        int n        = maturities.Length;
        var zeros    = new double[n];
        double delta = 1.0 / frequency;

        for (int i = 0; i < n; i++)
        {
            double c = couponRates[i];
            double T = maturities[i];
            double P = prices[i];

            double pvCoupons = 0;
            for (int j = 0; j < i; j++)
                pvCoupons += c * delta * Math.Exp(-zeros[j] * maturities[j]);

            // P = pvCoupons + (1 + c·δ) · exp(−z·T)
            double pFinal = (P - pvCoupons) / (1 + c * delta);
            if (pFinal <= 0)
                throw new InvalidOperationException(
                    $"Bootstrap failed at maturity {T}: discount factor is non-positive. Check input data.");
            zeros[i] = -Math.Log(pFinal) / T;
        }
        return zeros;
    }

    // ── Interpolation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Linear interpolation of a zero rate at time t from a zero curve.
    /// Flat extrapolation at both ends.
    /// </summary>
    public static double InterpolateZeroRate(double[] maturities, double[] zeroRates, double t)
    {
        ValidateSameLength(maturities, zeroRates);
        if (t <= maturities[0]) return zeroRates[0];
        if (t >= maturities[^1]) return zeroRates[^1];

        for (int i = 1; i < maturities.Length; i++)
        {
            if (t <= maturities[i])
            {
                double w = (t - maturities[i - 1]) / (maturities[i] - maturities[i - 1]);
                return zeroRates[i - 1] + w * (zeroRates[i] - zeroRates[i - 1]);
            }
        }
        return zeroRates[^1];
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ValidateSameLength(double[] a, double[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Input arrays must have the same length.");
    }
}
