using Aleksej.Finance.Bonds;

namespace Aleksej.Finance.Tests;

public class BondMathTests
{
    [Fact]
    public void PriceThenYield_RoundTrips()
    {
        // Price a bond at a known yield, then recover that yield from the price.
        double face = 1000, couponRate = 0.05, ytm = 0.06, years = 10;

        double price = BondMath.Price(face, couponRate, ytm, years);
        double recovered = BondMath.YieldToMaturity(price, face, couponRate, years);

        Assert.Equal(ytm, recovered, 6);
    }

    [Fact]
    public void ParBond_PricesAtFace()
    {
        // When coupon equals yield, a bond prices at par.
        double price = BondMath.Price(face: 1000, couponRate: 0.05, ytm: 0.05, years: 10);
        Assert.Equal(1000.0, price, 4);
    }

    [Fact]
    public void DiscountBond_PricesBelowFace()
    {
        // Yield above coupon => price below par.
        double price = BondMath.Price(face: 1000, couponRate: 0.05, ytm: 0.08, years: 10);
        Assert.True(price < 1000.0);
    }
}
