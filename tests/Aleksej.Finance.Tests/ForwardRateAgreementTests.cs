using Aleksej.Finance.Derivatives;

namespace Aleksej.Finance.Tests;

public class ForwardRateAgreementTests
{
    [Fact]
    public void ForwardRate_MatchesIdentity()
    {
        // f = (r2*t2 - r1*t1) / (t2 - t1)
        double f = ForwardRateAgreement.ForwardRate(0.03, 1.0, 0.035, 2.0);
        Assert.Equal((0.035 * 2.0 - 0.03 * 1.0) / (2.0 - 1.0), f, 12);
    }

    [Fact]
    public void ForwardRate_FlatCurveEqualsZeroRate()
    {
        double f = ForwardRateAgreement.ForwardRate(0.04, 1.0, 0.04, 2.0);
        Assert.Equal(0.04, f, 12);
    }

    [Fact]
    public void ForwardRate_ThrowsWhenT2NotGreaterThanT1()
    {
        Assert.Throws<ArgumentException>(() => ForwardRateAgreement.ForwardRate(0.03, 2.0, 0.035, 1.0));
    }

    [Fact]
    public void ForwardRateSimple_MatchesContinuousToSimpleConversion()
    {
        double f = ForwardRateAgreement.ForwardRate(0.03, 1.0, 0.035, 2.0);
        double delta = 2.0 - 1.0;
        double expected = (Math.Exp(f * delta) - 1) / delta;
        double simple = ForwardRateAgreement.ForwardRateSimple(0.03, 1.0, 0.035, 2.0);
        Assert.Equal(expected, simple, 12);
    }

    [Fact]
    public void FraValue_IsZeroWhenFraRateEqualsForwardRate()
    {
        double rF = ForwardRateAgreement.ForwardRateSimple(0.03, 1.0, 0.035, 2.0);
        double value = ForwardRateAgreement.FraValue(1_000_000, rF, 0.03, 1.0, 0.035, 2.0, isLong: true);
        Assert.Equal(0.0, value, 6);
    }

    [Fact]
    public void FraValue_MatchesClosedForm()
    {
        double rF = ForwardRateAgreement.ForwardRateSimple(0.03, 1.0, 0.035, 2.0);
        double delta = 2.0 - 1.0;
        double expected = 1_000_000 * (0.05 - rF) * delta * Math.Exp(-0.035 * 2.0);
        double value = ForwardRateAgreement.FraValue(1_000_000, 0.05, 0.03, 1.0, 0.035, 2.0, isLong: true);
        Assert.Equal(expected, value, 6);
    }

    [Fact]
    public void FraValue_ShortIsNegationOfLong()
    {
        double longVal = ForwardRateAgreement.FraValue(1_000_000, 0.05, 0.03, 1.0, 0.035, 2.0, isLong: true);
        double shortVal = ForwardRateAgreement.FraValue(1_000_000, 0.05, 0.03, 1.0, 0.035, 2.0, isLong: false);
        Assert.Equal(-longVal, shortVal, 9);
    }

    [Fact]
    public void FraValue_LongProfitsWhenFraRateAboveForward()
    {
        double rF = ForwardRateAgreement.ForwardRateSimple(0.03, 1.0, 0.035, 2.0);
        double value = ForwardRateAgreement.FraValue(1_000_000, rF + 0.01, 0.03, 1.0, 0.035, 2.0, isLong: true);
        Assert.True(value > 0);
    }

    [Fact]
    public void FraSettlement_MatchesClosedForm()
    {
        double delta = 2.0 - 1.0;
        double expected = 1_000_000 * (0.05 - 0.06) * delta / (1 + 0.06 * delta);
        double settlement = ForwardRateAgreement.FraSettlement(1_000_000, 0.05, 0.06, 1.0, 2.0, isLong: true);
        Assert.Equal(expected, settlement, 6);
    }

    [Fact]
    public void FraSettlement_ZeroWhenRatesEqual()
    {
        double settlement = ForwardRateAgreement.FraSettlement(1_000_000, 0.05, 0.05, 1.0, 2.0, isLong: true);
        Assert.Equal(0.0, settlement, 9);
    }

    [Fact]
    public void DV01_IsNonZeroForOffMarketFra()
    {
        double dv01 = ForwardRateAgreement.DV01(1_000_000, 0.05, 0.03, 1.0, 0.035, 2.0, isLong: true);
        double shortDv01 = ForwardRateAgreement.DV01(1_000_000, 0.05, 0.03, 1.0, 0.035, 2.0, isLong: false);
        Assert.Equal(-dv01, shortDv01, 9);
    }
}
