using System;

namespace AleksejCupic.FinancialMath.Credit;

/// <summary>
/// Credit risk models used in investment banking and fixed income.
/// Includes the Merton (1974) structural model for default risk
/// and a hazard-rate / reduced-form model for Credit Default Swap (CDS) pricing.
/// Based on Hull Chapters 24–25.
/// </summary>
public static class CreditDerivatives
{
    // ── Merton structural model ───────────────────────────────────────────────
    //
    // Merton (1974): equity is a call option on the firm's asset value V with
    // strike equal to the face value of debt D maturing at time T.
    // Equity:  E = V · N(d1) − D · e^{−rT} · N(d2)   (call on V)
    // Debt:    D_PV = V − E
    //
    // The probability of default (risk-neutral) = N(−d2)
    // Distance to default (DD) measures how many standard deviations the firm
    // is away from the default boundary.

    /// <summary>
    /// Equity value under the Merton model.
    /// E = V · N(d1) − D · e^{−rT} · N(d2)
    /// </summary>
    /// <param name="v">Current total asset value of the firm</param>
    /// <param name="d">Face value of debt (default boundary at maturity)</param>
    /// <param name="t">Debt maturity in years</param>
    /// <param name="r">Continuously compounded risk-free rate</param>
    /// <param name="sigmaV">Annualised volatility of firm asset value</param>
    public static double MertonEquityValue(
        double v, double d, double t, double r, double sigmaV)
    {
        if (t <= 0) return Math.Max(v - d, 0);
        var (d1, d2) = MertonD1D2(v, d, t, r, sigmaV);
        return v * N(d1) - d * Math.Exp(-r * t) * N(d2);
    }

    /// <summary>
    /// Present value of debt under the Merton model.
    /// PV(Debt) = V − E = D · e^{−rT} · N(d2) + V · N(−d1)
    /// </summary>
    public static double MertonDebtValue(
        double v, double d, double t, double r, double sigmaV) =>
        v - MertonEquityValue(v, d, t, r, sigmaV);

    /// <summary>
    /// Risk-neutral probability of default under the Merton model.
    /// PD = N(−d2)  (the probability that V_T &lt; D)
    /// </summary>
    public static double MertonDefaultProbability(
        double v, double d, double t, double r, double sigmaV)
    {
        var (_, d2) = MertonD1D2(v, d, t, r, sigmaV);
        return N(-d2);
    }

    /// <summary>
    /// Distance to default (DD) under the Merton model.
    /// DD = d2 = (ln(V/D) + (r − ½σ²)T) / (σ√T)
    /// Measures how many standard deviations the firm asset value is above
    /// the default boundary. Higher DD = lower default risk.
    /// </summary>
    public static double DistanceToDefault(
        double v, double d, double t, double r, double sigmaV)
    {
        var (_, d2) = MertonD1D2(v, d, t, r, sigmaV);
        return d2;
    }

    /// <summary>
    /// Credit spread implied by the Merton model (yield on debt minus risk-free rate).
    /// Spread = −ln(PV_Debt / D·e^{−rT}) / T
    /// </summary>
    public static double MertonCreditSpread(
        double v, double d, double t, double r, double sigmaV)
    {
        if (t <= 0) return 0;
        double pvDebt     = MertonDebtValue(v, d, t, r, sigmaV);
        double riskFreeDF = d * Math.Exp(-r * t);
        if (pvDebt <= 0 || riskFreeDF <= 0) return double.NaN;
        return -Math.Log(pvDebt / riskFreeDF) / t;
    }

    // ── Hazard rate / reduced-form CDS model ─────────────────────────────────
    //
    // Under a constant hazard rate λ:
    // Survival probability: Q(t) = exp(−λ·t)
    // Default probability density: λ·exp(−λ·t)
    //
    // CDS fair spread s makes the PV of premium payments equal to the
    // PV of the default protection leg.
    //
    // Protection leg PV  = (1 − R) · Σ [Q(t_{i-1}) − Q(t_i)] · DF(t_i)
    // Premium   leg  PV  = s · Σ δ_i · Q(t_i) · DF(t_i) + accrual on default
    //
    // R = recovery rate (typically 0.40 for senior unsecured)

    /// <summary>
    /// Survival probability from a constant hazard rate.
    /// Q(t) = exp(−λ · t)
    /// </summary>
    /// <param name="hazardRate">Constant hazard rate λ (&gt; 0)</param>
    /// <param name="t">Time horizon in years</param>
    public static double SurvivalProbability(double hazardRate, double t) =>
        Math.Exp(-hazardRate * t);

    /// <summary>
    /// Risk-neutral default probability over [0, t] from a constant hazard rate.
    /// PD(t) = 1 − exp(−λ·t)
    /// </summary>
    public static double DefaultProbability(double hazardRate, double t) =>
        1 - SurvivalProbability(hazardRate, t);

    /// <summary>
    /// Implied constant hazard rate from a credit spread and recovery rate.
    /// Approximate: λ ≈ s / (1 − R)
    /// Exact (continuous): λ = −ln(Q(T)) / T where Q is backed out from CDS price.
    /// </summary>
    /// <param name="creditSpread">Market credit spread (continuously compounded)</param>
    /// <param name="recoveryRate">Recovery rate on default (e.g. 0.40 = 40%)</param>
    public static double HazardRateFromSpread(double creditSpread, double recoveryRate = 0.40) =>
        creditSpread / (1 - recoveryRate);

    /// <summary>
    /// Fair CDS spread (running premium) via the standard reduced-form hazard-rate model
    /// with a constant hazard rate and flat risk-free curve.
    ///
    /// s = (1 − R) · Σ [Q(t_{i-1}) − Q(t_i)] · DF(t_i)
    ///             / Σ δ_i · Q(t_i) · DF(t_i)
    ///
    /// </summary>
    /// <param name="hazardRate">Constant hazard rate λ</param>
    /// <param name="r">Continuously compounded risk-free rate (flat curve)</param>
    /// <param name="maturity">CDS maturity in years</param>
    /// <param name="recoveryRate">Recovery rate (default 0.40)</param>
    /// <param name="frequency">Premium payment frequency per year (default 4 = quarterly)</param>
    public static double CdsFairSpread(
        double hazardRate, double r, double maturity,
        double recoveryRate = 0.40, int frequency = 4)
    {
        double delta   = 1.0 / frequency;
        int    n       = (int)Math.Round(maturity * frequency);
        double protPV  = 0;
        double premPV  = 0;
        double qPrev   = 1.0; // Q(0) = 1

        for (int i = 1; i <= n; i++)
        {
            double t  = i * delta;
            double q  = SurvivalProbability(hazardRate, t);
            double df = Math.Exp(-r * t);

            protPV += (qPrev - q) * df;   // default leg contribution
            premPV += delta * q * df;     // premium leg (no accrual simplification)
            qPrev   = q;
        }

        if (premPV < 1e-14) return double.NaN;
        return (1 - recoveryRate) * protPV / premPV;
    }

    /// <summary>
    /// Mark-to-market value of an existing CDS position (protection buyer's perspective).
    /// Value = (Fair spread − Contracted spread) × annuity factor (in running spread bps)
    /// </summary>
    /// <param name="contractedSpread">Spread agreed at inception (running bps as decimal)</param>
    /// <param name="hazardRate">Current implied hazard rate</param>
    /// <param name="r">Current risk-free rate</param>
    /// <param name="notional">Notional of the CDS</param>
    /// <param name="remainingMaturity">Remaining maturity in years</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="frequency">Premium payment frequency</param>
    public static double CdsMtm(
        double contractedSpread, double hazardRate, double r,
        double notional, double remainingMaturity,
        double recoveryRate = 0.40, int frequency = 4)
    {
        double fairSpread = CdsFairSpread(hazardRate, r, remainingMaturity, recoveryRate, frequency);
        double delta      = 1.0 / frequency;
        int    n          = (int)Math.Round(remainingMaturity * frequency);
        double annuity    = 0;

        for (int i = 1; i <= n; i++)
        {
            double t = i * delta;
            annuity += delta * SurvivalProbability(hazardRate, t) * Math.Exp(-r * t);
        }

        // Buyer of protection benefits when credit deteriorates (fair > contracted)
        return notional * (fairSpread - contractedSpread) * annuity;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static (double d1, double d2) MertonD1D2(
        double v, double d, double t, double r, double sigmaV)
    {
        double d1 = (Math.Log(v / d) + (r + 0.5 * sigmaV * sigmaV) * t) / (sigmaV * Math.Sqrt(t));
        return (d1, d1 - sigmaV * Math.Sqrt(t));
    }

    private static double N(double x)
    {
        const double a1 =  0.254829592, a2 = -0.284496736, a3 =  1.421413741;
        const double a4 = -1.453152027, a5 =  1.061405429, p  =  0.3275911;
        double sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);
        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return 0.5 * (1.0 + sign * (1.0 - poly * Math.Exp(-x * x / 2)));
    }
}
