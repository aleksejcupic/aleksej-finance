using System;

namespace AleksejCupic.FinancialMath.Derivatives;

/// <summary>
/// Forward and futures pricing via the cost-of-carry model.
/// All rates are continuously compounded. Based on Hull Chapters 5–6.
/// </summary>
public static class ForwardFutures
{
    // ── Forward prices ─────────────────────────────────────────────────────────

    /// <summary>
    /// Forward price on an asset paying no income.
    /// F = S · exp(r · T)
    /// </summary>
    /// <param name="s">Current spot price</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="t">Time to delivery in years</param>
    public static double ForwardPrice(double s, double r, double t) =>
        s * Math.Exp(r * t);

    /// <summary>
    /// Forward price on an asset paying a continuous dividend yield q.
    /// F = S · exp((r − q) · T)
    /// </summary>
    /// <param name="s">Current spot price</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="q">Continuous dividend yield</param>
    /// <param name="t">Time to delivery in years</param>
    public static double ForwardPriceWithYield(double s, double r, double q, double t) =>
        s * Math.Exp((r - q) * t);

    /// <summary>
    /// Forward price on an asset with known discrete income whose present value is I.
    /// F = (S − I) · exp(r · T)
    /// Use <see cref="PresentValueOfIncome"/> to compute I from a cash flow schedule.
    /// </summary>
    /// <param name="s">Current spot price</param>
    /// <param name="incomesPv">Present value of known income payments during the forward period</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="t">Time to delivery in years</param>
    public static double ForwardPriceWithIncome(double s, double incomesPv, double r, double t) =>
        (s - incomesPv) * Math.Exp(r * t);

    /// <summary>
    /// FX forward price via covered interest rate parity.
    /// F = S · exp((r − rf) · T)
    /// where r is the domestic rate and rf is the foreign risk-free rate.
    /// </summary>
    /// <param name="s">Current spot exchange rate (domestic per unit of foreign)</param>
    /// <param name="r">Domestic continuous risk-free rate</param>
    /// <param name="rf">Foreign continuous risk-free rate</param>
    /// <param name="t">Time to delivery in years</param>
    public static double FxForwardPrice(double s, double r, double rf, double t) =>
        s * Math.Exp((r - rf) * t);

    /// <summary>
    /// Commodity forward price via the general cost-of-carry model.
    /// F = S · exp((r + u − y) · T)
    /// where u is the continuous storage cost rate and y is the convenience yield.
    /// </summary>
    /// <param name="s">Current spot price</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="storageCost">Continuous storage cost as a fraction of spot per year</param>
    /// <param name="convenienceYield">Continuous convenience yield</param>
    /// <param name="t">Time to delivery in years</param>
    public static double ForwardPriceCommodity(
        double s, double r, double storageCost, double convenienceYield, double t) =>
        s * Math.Exp((r + storageCost - convenienceYield) * t);

    // ── Value of existing forward ──────────────────────────────────────────────

    /// <summary>
    /// Current value of an existing long forward contract with delivery price K.
    /// f = (F − K) · exp(−r · T)
    /// where F is the current fair forward price computed via one of the ForwardPrice* methods.
    /// </summary>
    /// <param name="f">Current fair forward price</param>
    /// <param name="k">Delivery price agreed at inception</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="t">Remaining time to delivery in years</param>
    public static double ForwardValue(double f, double k, double r, double t) =>
        (f - k) * Math.Exp(-r * t);

    /// <summary>
    /// Current value of an existing short forward position with delivery price K.
    /// f = (K − F) · exp(−r · T)
    /// </summary>
    /// <param name="f">Current fair forward price</param>
    /// <param name="k">Delivery price agreed at inception</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="t">Remaining time to delivery in years</param>
    public static double ForwardValueShort(double f, double k, double r, double t) =>
        (k - f) * Math.Exp(-r * t);

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Present value of a stream of known discrete cash flows, discounted at rate r.
    /// I = Σ CF_i · exp(−r · t_i)
    /// Use this result as <c>incomesPv</c> in <see cref="ForwardPriceWithIncome"/>.
    /// </summary>
    /// <param name="cashFlows">Cash flow amounts</param>
    /// <param name="times">Times of each cash flow in years (must match cashFlows in length)</param>
    /// <param name="r">Continuous discount rate</param>
    public static double PresentValueOfIncome(double[] cashFlows, double[] times, double r)
    {
        if (cashFlows.Length != times.Length)
            throw new ArgumentException("cashFlows and times must have the same length.");
        double pv = 0;
        for (int i = 0; i < cashFlows.Length; i++)
            pv += cashFlows[i] * Math.Exp(-r * times[i]);
        return pv;
    }
}
