using System;

namespace AleksejCupic.FinancialMath.Derivatives;

/// <summary>
/// Equilibrium short-rate models for the term structure of interest rates.
/// Provides closed-form zero-coupon bond prices, yields, and bond option prices
/// under the Vasicek (1977) and Cox-Ingersoll-Ross (CIR, 1985) models.
/// Based on Hull Chapters 31–32.
/// </summary>
public static class ShortRateModels
{
    // ── Vasicek model ─────────────────────────────────────────────────────────
    //
    // dr = κ(θ − r)dt + σ dW
    //
    // κ = mean-reversion speed
    // θ = long-run mean rate
    // σ = short-rate volatility
    // r = current short rate

    /// <summary>
    /// Zero-coupon bond price under the Vasicek model.
    /// P(t, T) = A(t,T) · exp(−B(t,T) · r)
    /// where τ = T − t, B = (1 − e^{−κτ})/κ, and A encapsulates the term premium.
    /// </summary>
    /// <param name="r">Current short rate</param>
    /// <param name="tau">Time to maturity in years (T − t)</param>
    /// <param name="kappa">Mean-reversion speed (κ &gt; 0)</param>
    /// <param name="theta">Long-run mean rate (θ)</param>
    /// <param name="sigma">Short-rate volatility (σ)</param>
    public static double VasicekBondPrice(
        double r, double tau, double kappa, double theta, double sigma)
    {
        var (a, b) = VasicekAB(tau, kappa, theta, sigma);
        return a * Math.Exp(-b * r);
    }

    /// <summary>
    /// Continuously compounded yield of a zero-coupon bond under the Vasicek model.
    /// R(t,T) = −ln P(t,T) / τ
    /// </summary>
    public static double VasicekYield(
        double r, double tau, double kappa, double theta, double sigma)
    {
        double p = VasicekBondPrice(r, tau, kappa, theta, sigma);
        return -Math.Log(p) / tau;
    }

    /// <summary>
    /// Long-run yield in the Vasicek model as τ → ∞.
    /// R(∞) = θ − σ²/(2κ²)
    /// </summary>
    public static double VasicekLongRunYield(double kappa, double theta, double sigma) =>
        theta - sigma * sigma / (2 * kappa * kappa);

    /// <summary>
    /// European call option on a zero-coupon bond under the Vasicek model.
    /// Uses the Jamshidian (1989) formula.
    /// </summary>
    /// <param name="r">Current short rate</param>
    /// <param name="t">Option expiry in years</param>
    /// <param name="maturity">Bond maturity in years (maturity &gt; t)</param>
    /// <param name="k">Strike price of the bond option</param>
    /// <param name="kappa">Mean-reversion speed</param>
    /// <param name="theta">Long-run mean rate</param>
    /// <param name="sigma">Short-rate volatility</param>
    /// <param name="isPut">True for put option on the bond</param>
    public static double VasicekBondOption(
        double r, double t, double maturity, double k,
        double kappa, double theta, double sigma, bool isPut = false)
    {
        double tauT = maturity - t; // time from option expiry to bond maturity
        double pt   = VasicekBondPrice(r, t, kappa, theta, sigma);
        double pT   = VasicekBondPrice(r, maturity, kappa, theta, sigma);

        double b  = VasicekAB(tauT, kappa, theta, sigma).b;
        double sigP = sigma * b * Math.Sqrt((1 - Math.Exp(-2 * kappa * t)) / (2 * kappa));
        if (sigP < 1e-14) return isPut ? Math.Max(k - pT, 0) : Math.Max(pT - k, 0);

        double h = Math.Log(pT / (pt * k)) / sigP + sigP / 2;

        if (isPut)
            return k * pt * N(-h + sigP) - pT * N(-h);
        return pT * N(h) - k * pt * N(h - sigP);
    }

    // ── CIR model ─────────────────────────────────────────────────────────────
    //
    // dr = κ(θ − r)dt + σ√r dW
    //
    // Square-root diffusion ensures r stays non-negative when 2κθ ≥ σ².
    // Bond price has the same affine form P = A·exp(−B·r) but with different A, B.

    /// <summary>
    /// Zero-coupon bond price under the Cox-Ingersoll-Ross (CIR) model.
    /// P(t, T) = A(τ) · exp(−B(τ) · r)
    /// </summary>
    /// <param name="r">Current short rate</param>
    /// <param name="tau">Time to maturity in years</param>
    /// <param name="kappa">Mean-reversion speed</param>
    /// <param name="theta">Long-run mean rate</param>
    /// <param name="sigma">Volatility coefficient</param>
    public static double CirBondPrice(
        double r, double tau, double kappa, double theta, double sigma)
    {
        var (a, b) = CirAB(tau, kappa, theta, sigma);
        return a * Math.Exp(-b * r);
    }

    /// <summary>
    /// Continuously compounded yield of a zero-coupon bond under the CIR model.
    /// </summary>
    public static double CirYield(
        double r, double tau, double kappa, double theta, double sigma)
    {
        double p = CirBondPrice(r, tau, kappa, theta, sigma);
        return -Math.Log(p) / tau;
    }

    /// <summary>
    /// Long-run yield in the CIR model as τ → ∞.
    /// R(∞) = 2κθ / (κ + γ),  where γ = √(κ² + 2σ²)
    /// </summary>
    public static double CirLongRunYield(double kappa, double theta, double sigma)
    {
        double gamma = Math.Sqrt(kappa * kappa + 2 * sigma * sigma);
        return 2 * kappa * theta / (kappa + gamma);
    }

    // ── Term structure generation ─────────────────────────────────────────────

    /// <summary>
    /// Generates a term structure of zero rates under the Vasicek model
    /// for an array of maturities.
    /// </summary>
    /// <param name="r">Current short rate</param>
    /// <param name="maturities">Array of maturities in years</param>
    /// <param name="kappa">Mean-reversion speed</param>
    /// <param name="theta">Long-run mean rate</param>
    /// <param name="sigma">Short-rate volatility</param>
    /// <returns>Array of continuously compounded zero rates (same length as maturities)</returns>
    public static double[] VasicekTermStructure(
        double r, double[] maturities, double kappa, double theta, double sigma)
    {
        var rates = new double[maturities.Length];
        for (int i = 0; i < maturities.Length; i++)
            rates[i] = VasicekYield(r, maturities[i], kappa, theta, sigma);
        return rates;
    }

    /// <summary>
    /// Generates a term structure of zero rates under the CIR model.
    /// </summary>
    public static double[] CirTermStructure(
        double r, double[] maturities, double kappa, double theta, double sigma)
    {
        var rates = new double[maturities.Length];
        for (int i = 0; i < maturities.Length; i++)
            rates[i] = CirYield(r, maturities[i], kappa, theta, sigma);
        return rates;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    // Vasicek A(τ) and B(τ) coefficients.
    private static (double a, double b) VasicekAB(
        double tau, double kappa, double theta, double sigma)
    {
        double b   = (1 - Math.Exp(-kappa * tau)) / kappa;
        double sig2 = sigma * sigma;
        // A = exp[(B(τ) − τ)(κ²θ − σ²/2)/κ² − σ²B²(τ)/(4κ)]
        double aLog = (b - tau) * (kappa * kappa * theta - 0.5 * sig2) / (kappa * kappa)
                    - sig2 * b * b / (4 * kappa);
        return (Math.Exp(aLog), b);
    }

    // CIR A(τ) and B(τ) coefficients.
    private static (double a, double b) CirAB(
        double tau, double kappa, double theta, double sigma)
    {
        double sig2  = sigma * sigma;
        double gamma = Math.Sqrt(kappa * kappa + 2 * sig2);

        double expg  = Math.Exp(gamma * tau);
        double denom = (gamma + kappa) * (expg - 1) + 2 * gamma;

        double b    = 2 * (expg - 1) / denom;
        double aLog = Math.Log(2 * gamma * Math.Exp(0.5 * (kappa + gamma) * tau) / denom)
                    * (2 * kappa * theta / sig2);
        return (Math.Exp(aLog), b);
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
