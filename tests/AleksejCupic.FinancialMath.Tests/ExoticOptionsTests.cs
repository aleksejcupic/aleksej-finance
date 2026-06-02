using AleksejCupic.FinancialMath.Options;

namespace AleksejCupic.FinancialMath.Tests;

public class ExoticOptionsTests
{
    // Standard case: S=100, K=100, T=1, r=5%, sigma=20%.
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    // ── Binary / digital options ──────────────────────────────────────────────

    [Fact]
    public void CashOrNothing_IsWithinDiscountedPayout()
    {
        // A cash-or-nothing option is bounded by [0, Q e^{-rT}].
        const double cash = 10.0;
        double price = ExoticOptions.CashOrNothing(S, K, T, R, Sigma, cashPayoff: cash);
        Assert.InRange(price, 0.0, cash * Math.Exp(-R * T));
    }

    [Fact]
    public void CashOrNothing_CallPlusPut_EqualsDiscountedPayout()
    {
        // N(d2) + N(-d2) = 1, so call + put = Q e^{-rT}.
        const double cash = 10.0;
        double call = ExoticOptions.CashOrNothing(S, K, T, R, Sigma, cashPayoff: cash, isPut: false);
        double put = ExoticOptions.CashOrNothing(S, K, T, R, Sigma, cashPayoff: cash, isPut: true);
        Assert.Equal(cash * Math.Exp(-R * T), call + put, 1e-9);
    }

    [Fact]
    public void AssetOrNothing_CallPlusPut_EqualsSpot()
    {
        // N(d1) + N(-d1) = 1, so asset-or-nothing call + put = S.
        double call = ExoticOptions.AssetOrNothing(S, K, T, R, Sigma, isPut: false);
        double put = ExoticOptions.AssetOrNothing(S, K, T, R, Sigma, isPut: true);
        Assert.Equal(S, call + put, 1e-9);
    }

    [Fact]
    public void CashAndAssetOrNothing_Reconstruct_VanillaCall()
    {
        // Vanilla call = AssetOrNothing(call) - K * CashOrNothing(call, payoff=1).
        double assetCall = ExoticOptions.AssetOrNothing(S, K, T, R, Sigma, isPut: false);
        double cashCall = ExoticOptions.CashOrNothing(S, K, T, R, Sigma, cashPayoff: 1.0, isPut: false);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.Equal(vanilla, assetCall - K * cashCall, 1e-9);
    }

    // ── Barrier options ────────────────────────────────────────────────────────

    [Fact]
    public void DownAndOutCall_IsAtMostVanilla()
    {
        // Barrier H below spot: a down-and-out call cannot be worth more than the vanilla.
        double barrier = ExoticOptions.BarrierCall(S, K, h: 90, T, R, Sigma, knockIn: false, isUp: false);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.InRange(barrier, 0.0, vanilla + 1e-9);
    }

    [Fact]
    public void DownBarrier_KnockInPlusKnockOut_EqualsVanilla()
    {
        // In + out parity for barrier options.
        double ki = ExoticOptions.BarrierCall(S, K, h: 90, T, R, Sigma, knockIn: true, isUp: false);
        double ko = ExoticOptions.BarrierCall(S, K, h: 90, T, R, Sigma, knockIn: false, isUp: false);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.Equal(vanilla, ki + ko, 1e-9);
    }

    [Fact]
    public void UpBarrierPut_KnockInPlusKnockOut_EqualsVanilla()
    {
        double ki = ExoticOptions.BarrierPut(S, K, h: 110, T, R, Sigma, knockIn: true, isUp: true);
        double ko = ExoticOptions.BarrierPut(S, K, h: 110, T, R, Sigma, knockIn: false, isUp: true);
        double vanilla = BlackScholes.Put(S, K, T, R, Sigma);
        Assert.Equal(vanilla, ki + ko, 1e-9);
    }

    [Fact]
    public void BarrierPrices_AreNonNegative()
    {
        Assert.True(ExoticOptions.BarrierCall(S, K, h: 90, T, R, Sigma, knockIn: false, isUp: false) >= -1e-9);
        Assert.True(ExoticOptions.BarrierPut(S, K, h: 110, T, R, Sigma, knockIn: false, isUp: true) >= -1e-9);
    }

    // ── Asian options ────────────────────────────────────────────────────────

    [Fact]
    public void GeometricAsianCall_IsPositive_AndBelowVanilla()
    {
        // Averaging reduces volatility, so the Asian call is positive but cheaper than the vanilla.
        double asian = ExoticOptions.GeometricAsian(S, K, T, R, Sigma, isPut: false);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.True(asian > 0);
        Assert.True(asian < vanilla);
    }

    [Fact]
    public void GeometricAsianPut_IsPositive()
    {
        double asian = ExoticOptions.GeometricAsian(S, K, T, R, Sigma, isPut: true);
        Assert.True(asian > 0);
    }

    [Fact]
    public void ArithmeticAsianCall_IsPositive_AndBelowVanilla()
    {
        double asian = ExoticOptions.ArithmeticAsian(S, K, T, R, Sigma,
            monitoringSteps: 50, paths: 5000, isPut: false, seed: 42);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.True(asian > 0);
        Assert.True(asian < vanilla + 1e-6);
    }

    [Fact]
    public void ArithmeticAsian_IsDeterministicForSameSeed()
    {
        double a = ExoticOptions.ArithmeticAsian(S, K, T, R, Sigma,
            monitoringSteps: 50, paths: 2000, isPut: false, seed: 7);
        double b = ExoticOptions.ArithmeticAsian(S, K, T, R, Sigma,
            monitoringSteps: 50, paths: 2000, isPut: false, seed: 7);
        Assert.Equal(a, b, 1e-12);
    }

    // ── Lookback options ─────────────────────────────────────────────────────

    [Fact]
    public void LookbackCall_IsAtLeastVanillaCall()
    {
        // A floating-strike lookback call is worth at least the vanilla call (it locks in the minimum).
        double lookback = ExoticOptions.LookbackCall(S, sMin: S, T, R, Sigma);
        double vanilla = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.True(lookback > 0);
        Assert.True(lookback >= vanilla - 1e-9);
    }

    [Fact]
    public void LookbackPut_IsPositive()
    {
        double lookback = ExoticOptions.LookbackPut(S, sMax: S, T, R, Sigma);
        Assert.True(lookback > 0);
    }

    [Fact]
    public void LookbackPut_MatchesMonteCarlo()
    {
        // Cross-check the closed form against a fine-monitoring Monte Carlo simulation.
        double analytic = ExoticOptions.LookbackPut(S, sMax: S, T, R, Sigma);
        double mc = MonteCarloFloatingLookbackPut(S, T, R, Sigma, steps: 252, paths: 40_000, seed: 123);
        // Discrete monitoring systematically UNDERprices the continuous lookback (it misses the
        // true running max between observations). The Broadie-Glasserman-Kou bias at 252 steps is
        // ~0.58*sigma*sqrt(dt)*S ~ 0.73, so MC should sit just below the analytic value.
        Assert.True(mc < analytic);
        Assert.Equal(analytic, mc, 1.0);
    }

    // Floating-strike lookback put (payoff max(S) - S_T) via Monte Carlo, used only as an
    // independent cross-check of the closed form.
    private static double MonteCarloFloatingLookbackPut(
        double s, double t, double r, double sigma, int steps, int paths, int seed)
    {
        double dt = t / steps;
        double drift = (r - 0.5 * sigma * sigma) * dt;
        double diff = sigma * Math.Sqrt(dt);
        var rng = new Random(seed);
        double sum = 0;
        for (int p = 0; p < paths; p++)
        {
            double st = s, max = s;
            for (int i = 0; i < steps; i++)
            {
                // Box-Muller standard normal.
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                st *= Math.Exp(drift + diff * z);
                if (st > max) max = st;
            }
            sum += max - st;
        }
        return Math.Exp(-r * t) * sum / paths;
    }

    [Fact]
    public void AtExpiry_ReturnsIntrinsicValues()
    {
        Assert.Equal(Math.Max(110 - K, 0), ExoticOptions.GeometricAsian(110, K, t: 0, R, Sigma, isPut: false), 1e-12);
        Assert.Equal(Math.Max(S - 90, 0), ExoticOptions.LookbackCall(S, sMin: 90, t: 0, R, Sigma), 1e-12);
        Assert.Equal(Math.Max(110 - S, 0), ExoticOptions.LookbackPut(S, sMax: 110, t: 0, R, Sigma), 1e-12);
    }
}
