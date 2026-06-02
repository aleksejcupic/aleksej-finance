# Aleksej.Finance

[![NuGet](https://img.shields.io/nuget/v/Aleksej.Finance.svg)](https://www.nuget.org/packages/Aleksej.Finance/)
[![NuGet downloads](https://img.shields.io/nuget/dt/Aleksej.Finance.svg)](https://www.nuget.org/packages/Aleksej.Finance/)
[![CI](https://github.com/aleksejcupic/aleksej-finance/actions/workflows/ci.yml/badge.svg)](https://github.com/aleksejcupic/aleksej-finance/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/aleksejcupic/aleksej-finance/branch/main/graph/badge.svg)](https://codecov.io/gh/aleksejcupic/aleksej-finance)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A comprehensive C# quantitative finance library with 180+ pure-math static methods and **zero external dependencies**. Every method is `public static` and takes explicit inputs — no state, no configuration, no side effects.

Built to back the [Excel Finance Add-In](https://github.com/aleksejcupic/excel-finance-addin) and usable standalone in any .NET project.

## Install

```bash
dotnet add package Aleksej.Finance
```

**Targets:** `netstandard2.0` and `net8.0` — works on .NET 8+, .NET Core 2.0+, and .NET Framework 4.6.1+.

---

## Modules

### Options

| Class | What it computes |
|---|---|
| `BlackScholes` | European call/put, 5 first-order Greeks (Δ Γ V Θ ρ), 5 higher-order Greeks (Vanna Charm Volga Speed Zomma), implied volatility |
| `GarmanKohlhagen` | FX option pricing, full Greeks + implied vol (extends BS with foreign risk-free rate) |
| `BinomialTree` | CRR binomial tree — European and **American** option pricing, Delta, Gamma |
| `MonteCarlo` | GBM path simulation; European pricing; American option pricing via Longstaff-Schwartz LSM |
| `ExoticOptions` | Binary (cash-or-nothing, asset-or-nothing), European barrier (knock-in/out), geometric Asian (closed-form), arithmetic Asian (Monte Carlo), floating-strike lookback call/put |
| `OptionsOnFutures` | Black (1976) model — call/put on futures, full Greeks, put-call parity, implied vol |

```csharp
using Aleksej.Finance.Options;

double call  = BlackScholes.Call(s: 100, k: 100, t: 1, r: 0.05, sigma: 0.20);  // ≈ 10.45
double delta = BlackScholes.Delta(100, 100, 1, 0.05, 0.20, isPut: false);        // ≈ 0.637
double iv    = BlackScholes.ImpliedVolatility(10.45, 100, 100, 1, 0.05, false);  // ≈ 0.200
double amPut = BinomialTree.Price(100, 100, 1, 0.05, 0.20, steps: 500, isPut: true, isAmerican: true);
double fxOpt = GarmanKohlhagen.Call(s: 1.10, k: 1.10, t: 0.5, r: 0.03, rf: 0.01, sigma: 0.10);
double barr  = ExoticOptions.BarrierCall(s: 100, k: 100, h: 120, t: 1, r: 0.05, sigma: 0.20, knockIn: false, isUp: true);
double futOpt = OptionsOnFutures.Call(f: 105, k: 100, t: 0.5, r: 0.05, sigma: 0.20);
```

### Bonds

| Class | What it computes |
|---|---|
| `BondMath` | Bond price from YTM, YTM from price (Newton-Raphson), Macaulay/modified duration, convexity, DV01, approximate price change |
| `YieldCurve` | Discount factors, rate conversions (simple ↔ continuous), forward rate curve, par yield, zero curve bootstrapping from par rates or bond prices, linear interpolation |
| `MortgageMath` | Payment formula, outstanding balance, full amortisation schedule, total interest paid, EAR/APR conversions |

```csharp
using Aleksej.Finance.Bonds;

double price   = BondMath.Price(face: 1000, couponRate: 0.05, ytm: 0.05, years: 10); // = 1000
double ytm     = BondMath.YieldToMaturity(950, 1000, 0.05, 10);
double mdur    = BondMath.ModifiedDuration(1000, 0.05, 0.05, 10);
double[] zeros = YieldCurve.Bootstrap(new[] { 0.5, 1.0, 1.5, 2.0 }, new[] { 0.04, 0.042, 0.044, 0.046 });
double payment = MortgageMath.Payment(principal: 500_000, annualRate: 0.065, years: 30); // monthly payment
```

### Derivatives

| Class | What it computes |
|---|---|
| `ForwardFutures` | Forward pricing (no income, continuous yield, known income, FX interest rate parity, commodity cost-of-carry), value of existing forward positions |
| `ForwardRateAgreement` | Forward rate from zero curve (continuous and simple), FRA mark-to-market value, settlement cash flow, DV01 |
| `InterestRateSwap` | Fixed-for-floating IRS value, par swap rate, fixed/floating leg PV, DV01 |
| `BlackModel` | Caplets, floorlets, interest rate caps, floors, forward swap rate, swaptions (payer/receiver) |
| `ShortRateModels` | Vasicek: bond price/yield/long-run yield/bond option. CIR: bond price/yield/long-run yield. Term structure generation |

```csharp
using Aleksej.Finance.Derivatives;

double fwd  = ForwardFutures.FxForwardPrice(s: 1.10, r: 0.03, rf: 0.01, t: 1);   // ≈ 1.122
double par  = InterestRateSwap.ParSwapRate(new[] { 0.5, 1.0, 1.5, 2.0 }, new[] { 0.028, 0.030, 0.031, 0.032 });
double cap  = BlackModel.CapPrice(1_000_000, strike: 0.04, sigma: 0.20, paymentTimes, zeroRates, forwardRates, accrualFracs);
double vBond = ShortRateModels.VasicekBondPrice(r: 0.03, tau: 5, kappa: 0.15, theta: 0.05, sigma: 0.01);
```

### Credit

| Class | What it computes |
|---|---|
| `CreditDerivatives` | Merton model (equity value, debt value, default probability, distance to default, credit spread). CDS: survival probability, hazard rate from spread, fair CDS spread, CDS mark-to-market |

```csharp
using Aleksej.Finance.Credit;

double pd    = CreditDerivatives.MertonDefaultProbability(v: 100, d: 80, t: 1, r: 0.05, sigmaV: 0.25);
double cdsS  = CreditDerivatives.CdsFairSpread(hazardRate: 0.02, r: 0.03, maturity: 5); // ≈ 120bps
double dd    = CreditDerivatives.DistanceToDefault(100, 80, 1, 0.05, 0.25);
```

### Portfolio

| Class | What it computes |
|---|---|
| `Markowitz` | Mean returns, covariance matrix, portfolio return/variance/volatility/Sharpe, minimum variance portfolio (analytical), efficient frontier (analytical Lagrange), unconstrained and long-only max Sharpe, risk parity (ERC), risk contributions |
| `EquityMetrics` | Market cap, enterprise value, portfolio market value, P/E, P/B, P/S, EV/EBITDA, EV/EBIT, dividend yield, earnings yield, unrealised/realised P&L, Kelly criterion (continuous and discrete), half-Kelly |
| `FeeCalculations` | Management fee accrual, performance fee with high-water mark + hurdle, carried interest (PE style), catch-up, expense ratio drag, cumulative fee impact, transaction cost, slippage |
| `PerformanceAttribution` | Time-weighted return (TWR), Modified Dietz, IRR / money-weighted return (Newton-Raphson on NPV), NPV, Brinson-Hood-Beebower attribution (allocation, selection, interaction per sector and total) |

```csharp
using Aleksej.Finance.Portfolio;

double[] mu  = Markowitz.MeanReturns(returns);       // returns is double[T, n]
double[,] cov = Markowitz.CovarianceMatrix(returns);
double[] wMV  = Markowitz.MinVariancePortfolio(cov);
double[] wMS  = Markowitz.MaxSharpePortfolioConstrained(mu, cov, riskFreeRate: 0.05);
double[] wRP  = Markowitz.RiskParityPortfolio(cov);

double pe     = EquityMetrics.PriceToEarnings(price: 150, eps: 6.25);          // 24×
double ev     = EquityMetrics.EnterpriseValue(marketCap: 500e9, debt: 50e9, cash: 30e9);
double kelly  = EquityMetrics.KellyCriterion(mu: 0.12, sigma: 0.20);           // f* = 3.0

double fee    = FeeCalculations.PerformanceFee(currentNav: 115, highWaterMark: 100,
                    previousNav: 100, carryRate: 0.20, hurdleRate: 0.08);
double carry  = FeeCalculations.CarriedInterest(totalDistributions: 200e6,
                    investedCapital: 100e6, preferredReturn: 0.08, holdingYears: 5);

double twr    = PerformanceAttribution.TimeWeightedReturn(new[] { 0.05, -0.02, 0.08 }); // ≈ 11.1%
double irr    = PerformanceAttribution.InternalRateOfReturn(
                    cashFlows: new[] { -1000.0, 200, 300, 400, 400 },
                    times:     new[] { 0.0, 1, 2, 3, 4 });

var (alloc, select, interact, active) = PerformanceAttribution.BhbAttribution(
    portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);
```

### Risk

| Class | What it computes |
|---|---|
| `RiskMetrics` | Sharpe, Sortino, Calmar, Treynor ratios; Beta; Jensen's Alpha; historical VaR; CVaR (Expected Shortfall); parametric VaR; annualised return and volatility; max drawdown; tracking error; information ratio |
| `VolatilityModels` | EWMA volatility series (RiskMetrics, λ=0.94 default), latest EWMA estimate, GARCH(1,1) conditional variance series, long-run variance, N-day ahead GARCH forecast, rolling historical volatility, GARCH MLE parameter estimation |

```csharp
using Aleksej.Finance.Risk;

double sharpe  = RiskMetrics.SharpeRatio(daily, riskFreeRateAnnual: 0.05);
double var95   = RiskMetrics.HistoricalVaR(daily, confidence: 0.95);
double sortino = RiskMetrics.SortinoRatio(daily, riskFreeRateAnnual: 0.05);
double beta    = RiskMetrics.Beta(portfolioReturns, benchmarkReturns);
double ir      = RiskMetrics.InformationRatio(portfolioReturns, benchmarkReturns);
double[] ewma  = VolatilityModels.EwmaVolatility(daily, lambda: 0.94);
double gFcast  = VolatilityModels.GarchForecast(currentVariance: 0.0001, omega: 0.000002,
                     alpha: 0.05, beta: 0.90, nDays: 10);
```

---

## Design Principles

- **Zero dependencies** — no NuGet packages, no external libraries
- **Pure functions** — every method is `public static`, takes all inputs explicitly, no global state
- **Numerically stable** — Gaussian elimination with partial pivoting, Box-Muller transform, Abramowitz & Stegun CDF, Brent's method, Newton-Raphson
- **Modular** — outputs of one method are valid inputs to another: `YieldCurve.Bootstrap()` → `InterestRateSwap.SwapValue()` → `InterestRateSwap.DV01()`

---

## Build from Source

```bash
git clone https://github.com/aleksejcupic/aleksej-finance
cd aleksej-finance
dotnet build
dotnet pack --configuration Release
```

---

## Author

Aleksej Cupic · [aleksejcupic.com](https://aleksejcupic.com) · [LinkedIn](https://linkedin.com/in/aleksejcupic)
