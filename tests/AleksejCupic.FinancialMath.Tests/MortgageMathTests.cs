using AleksejCupic.FinancialMath.Bonds;

namespace AleksejCupic.FinancialMath.Tests;

public class MortgageMathTests
{
    [Fact]
    public void Payment_MatchesClosedForm()
    {
        // Hand-computed standard amortising payment.
        // P = 100000, annualRate = 6% => monthly r = 0.005, n = 360.
        // M = P r (1+r)^n / ((1+r)^n - 1)
        double principal = 100000, annualRate = 0.06, years = 30;
        double r = annualRate / 12;
        int n = 360;
        double factor = Math.Pow(1 + r, n);
        double expected = principal * r * factor / (factor - 1);

        double payment = MortgageMath.Payment(principal, annualRate, years);

        Assert.Equal(expected, payment, 6);
        // Sanity: ~599.55 for this classic case.
        Assert.Equal(599.55, payment, 2);
    }

    [Fact]
    public void Payment_ZeroRate_IsPrincipalOverN()
    {
        // Zero-rate edge case: payment is just principal spread evenly.
        double payment = MortgageMath.Payment(120000, 0.0, 10);
        Assert.Equal(120000.0 / 120, payment, 8);
    }

    [Fact]
    public void AmortisationSchedule_FinalBalanceIsZero()
    {
        var schedule = MortgageMath.AmortisationSchedule(100000, 0.06, 30);

        Assert.Equal(360, schedule.Length);
        Assert.Equal(0.0, schedule[^1].Balance, 6);
        Assert.Equal(360, schedule[^1].Period);
        Assert.Equal(1, schedule[0].Period);
    }

    [Fact]
    public void AmortisationSchedule_PrincipalSumsToOriginalPrincipal()
    {
        double principal = 100000;
        var schedule = MortgageMath.AmortisationSchedule(principal, 0.06, 30);

        double totalPrincipal = schedule.Sum(row => row.PrincipalPaid);
        Assert.Equal(principal, totalPrincipal, 4);
    }

    [Fact]
    public void AmortisationSchedule_RowComponentsAreConsistent()
    {
        var schedule = MortgageMath.AmortisationSchedule(100000, 0.06, 30);

        // For each row, payment = interest + principal (within fp tolerance).
        foreach (var row in schedule)
            Assert.Equal(row.Payment, row.InterestPaid + row.PrincipalPaid, 6);
    }

    [Fact]
    public void OutstandingBalance_AtZeroPaymentsIsPrincipal()
    {
        double balance = MortgageMath.OutstandingBalance(100000, 0.06, 30, paymentsMade: 0);
        Assert.Equal(100000.0, balance, 4);
    }

    [Fact]
    public void OutstandingBalance_AtFinalPaymentIsZero()
    {
        double balance = MortgageMath.OutstandingBalance(100000, 0.06, 30, paymentsMade: 360);
        Assert.Equal(0.0, balance, 4);
    }

    [Fact]
    public void OutstandingBalance_MatchesScheduleBalance()
    {
        // Closed-form balance must match the running balance from the schedule.
        var schedule = MortgageMath.AmortisationSchedule(100000, 0.06, 30);
        int k = 120;
        double balance = MortgageMath.OutstandingBalance(100000, 0.06, 30, paymentsMade: k);
        Assert.Equal(schedule[k - 1].Balance, balance, 4);
    }

    [Fact]
    public void TotalInterest_EqualsNPaymentsMinusPrincipal()
    {
        double principal = 100000, annualRate = 0.06, years = 30;
        double payment = MortgageMath.Payment(principal, annualRate, years);
        double expected = 360 * payment - principal;

        double total = MortgageMath.TotalInterest(principal, annualRate, years);
        Assert.Equal(expected, total, 4);
        Assert.True(total > 0);
    }

    [Fact]
    public void TotalInterest_EqualsScheduleInterestSum()
    {
        var schedule = MortgageMath.AmortisationSchedule(100000, 0.06, 30);
        double scheduleInterest = schedule.Sum(row => row.InterestPaid);
        double total = MortgageMath.TotalInterest(100000, 0.06, 30);
        Assert.Equal(scheduleInterest, total, 2);
    }

    [Fact]
    public void EffectiveAnnualRate_ExceedsNominalWhenCompounded()
    {
        // EAR > nominal for frequency > 1.
        double ear = MortgageMath.EffectiveAnnualRate(0.06, 12);
        Assert.True(ear > 0.06);
        // Closed form check.
        Assert.Equal(Math.Pow(1 + 0.06 / 12, 12) - 1, ear, 12);
    }

    [Fact]
    public void EffectiveAnnualRate_AnnualCompoundingEqualsNominal()
    {
        double ear = MortgageMath.EffectiveAnnualRate(0.06, 1);
        Assert.Equal(0.06, ear, 12);
    }

    [Fact]
    public void AnnualPercentageRate_IsPeriodicTimesFrequency()
    {
        double apr = MortgageMath.AnnualPercentageRate(0.005, 12);
        Assert.Equal(0.06, apr, 12);
    }

    [Fact]
    public void AprToContinuous_RoundTripsWithExp()
    {
        // r_cont = m ln(1 + APR/m); inverting via exp recovers APR.
        double apr = 0.06;
        int m = 12;
        double rCont = MortgageMath.AprToContinuous(apr, m);
        double recoveredApr = m * (Math.Exp(rCont / m) - 1);
        Assert.Equal(apr, recoveredApr, 12);
        // Continuous rate is below the nominal APR.
        Assert.True(rCont < apr);
    }
}
