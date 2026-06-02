using System;
using Aleksej.Finance.Bonds;

namespace Aleksej.Finance.Tests;

public class BondMathMoreTests
{
    // Concrete par bond used across the duration/convexity/DV01 checks.
    private const double Face = 1000.0;
    private const double CouponRate = 0.05;
    private const double Ytm = 0.05;
    private const double Years = 10.0;
    private const int Frequency = 2;

    [Fact]
    public void ModifiedDuration_EqualsMacaulayDividedByOnePlusPeriodicYield()
    {
        double mac = BondMath.MacaulayDuration(Face, CouponRate, Ytm, Years, Frequency);
        double mod = BondMath.ModifiedDuration(Face, CouponRate, Ytm, Years, Frequency);

        double expected = mac / (1 + Ytm / Frequency);
        Assert.Equal(expected, mod, 12);
    }

    [Fact]
    public void MacaulayDuration_IsPositiveAndLessThanMaturity()
    {
        double mac = BondMath.MacaulayDuration(Face, CouponRate, Ytm, Years, Frequency);

        Assert.True(mac > 0, "Macaulay duration of a coupon bond should be positive.");
        Assert.True(mac < Years, "Macaulay duration of a coupon bond should be less than its maturity.");
    }

    [Fact]
    public void MacaulayDuration_OfZeroCouponBond_ApproximatelyEqualsMaturity()
    {
        // With no coupons, all weight sits on the final principal payment, so
        // Macaulay duration ≈ years to maturity.
        double mac = BondMath.MacaulayDuration(Face, couponRate: 0.0, ytm: 0.05, years: Years, frequency: Frequency);

        Assert.Equal(Years, mac, 6);
    }

    [Fact]
    public void Convexity_IsPositive()
    {
        double conv = BondMath.Convexity(Face, CouponRate, Ytm, Years, Frequency);
        Assert.True(conv > 0, "Convexity should be positive for a standard bond.");
    }

    [Fact]
    public void DV01_IsPositive_AndMatchesModifiedDurationTimesPriceConvention()
    {
        double dv01 = BondMath.DV01(Face, CouponRate, Ytm, Years, Frequency);

        double modDur = BondMath.ModifiedDuration(Face, CouponRate, Ytm, Years, Frequency);
        double price = BondMath.Price(Face, CouponRate, Ytm, Years, Frequency);
        double expected = modDur * price * 0.0001;

        Assert.True(dv01 > 0, "DV01 should be positive.");
        Assert.Equal(expected, dv01, 12);
    }

    [Theory]
    [InlineData(0.0001)]  // 1 bp
    [InlineData(0.0010)]  // 10 bp
    [InlineData(-0.0010)] // -10 bp
    public void ApproximatePriceChange_TracksActualPriceChange_ForSmallYieldShift(double dy)
    {
        double price0 = BondMath.Price(Face, CouponRate, Ytm, Years, Frequency);
        double price1 = BondMath.Price(Face, CouponRate, Ytm + dy, Years, Frequency);
        double actualChange = price1 - price0;

        double approxChange = BondMath.ApproximatePriceChange(Face, CouponRate, Ytm, Years, dy, Frequency);

        // Second-order Taylor (duration + convexity) should match the actual
        // reprice closely for a small yield move. Loose absolute tolerance in price units.
        Assert.Equal(actualChange, approxChange, 2);
    }

    [Fact]
    public void ApproximatePriceChange_IsNegative_ForYieldIncrease()
    {
        double approx = BondMath.ApproximatePriceChange(Face, CouponRate, Ytm, Years, deltaYtm: 0.0010, frequency: Frequency);
        Assert.True(approx < 0, "A yield increase should produce a negative approximate price change.");
    }
}
