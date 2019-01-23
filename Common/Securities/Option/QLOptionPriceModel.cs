/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QLNet;

namespace QuantConnect.Securities.Option
{
    using Logging;

    /// <summary>
    /// Creates the <see cref="IPricingEngine"/> to estimate the specified process
    /// </summary>
    public delegate IPricingEngine PricingEngineFunc(GeneralizedBlackScholesProcess process);

    /// <summary>
    /// Creates the <see cref="IPricingEngine"/> to estimate the specified process
    /// </summary>
    public delegate IPricingEngine PricingEngineFuncEx(Symbol symbol, GeneralizedBlackScholesProcess process);

    /// <summary>
    /// Creates an <see cref="AmericanExercise"/> or <see cref="EuropeanExercise"/> depending on the pricing engine and symbol
    /// </summary>
    public delegate Exercise ExerciseFunc(Symbol symbol, DateTime settlementDate, DateTime maturityDate);

    /// <summary>
    /// Creates a <see cref="StrikedTypePayoff"/>, such as a <see cref="PlainVanillaPayoff"/>
    /// </summary>
    public delegate StrikedTypePayoff PayoffFunc(Symbol symbol, OptionContract contract);

    /// <summary>
    /// Provides QuantLib(QL) implementation of <see cref="IOptionPriceModel"/> to support major option pricing models, available in QL.
    /// </summary>
    public class QLOptionPriceModel : IOptionPriceModel
    {
        private readonly PayoffFunc _payoffFunc;
        private readonly IQLUnderlyingVolatilityEstimator _underlyingVolEstimator;
        private readonly IQLRiskFreeRateEstimator _riskFreeRateEstimator;
        private readonly IQLDividendYieldEstimator _dividendYieldEstimator;
        private readonly PricingEngineFuncEx _pricingEngineFunc;
        private readonly ExerciseFunc _exerciseFunc;

        /// <summary>
        /// When enabled, approximates Greeks if corresponding pricing model didn't calculate exact numbers.
        /// The default value is true.
        /// </summary>
        public bool EnableGreekApproximation { get; set; } = true;

        /// <summary>
        /// Method constructs QuantLib option price model with necessary estimators of underlying volatility, risk free rate, and underlying dividend yield
        /// </summary>
        /// <param name="pricingEngineFunc">Function modeled stochastic process, and returns new pricing engine to run calculations for that option</param>
        /// <param name="underlyingVolEstimator">The underlying volatility estimator</param>
        /// <param name="riskFreeRateEstimator">The risk free rate estimator</param>
        /// <param name="dividendYieldEstimator">The underlying dividend yield estimator</param>
        /// <param name="exerciseFunc">Optional exercise func for defining american or european</param>
        /// <param name="payoffFunc">Optional payoff func for defining the type of payoff to model</param>
        public QLOptionPriceModel(PricingEngineFunc pricingEngineFunc,
            IQLUnderlyingVolatilityEstimator underlyingVolEstimator,
            IQLRiskFreeRateEstimator riskFreeRateEstimator,
            IQLDividendYieldEstimator dividendYieldEstimator,
            ExerciseFunc exerciseFunc = null,
            PayoffFunc payoffFunc = null
            )
        {
            _pricingEngineFunc = (option, process) => pricingEngineFunc(process);
            _underlyingVolEstimator = underlyingVolEstimator ?? new ConstantQLUnderlyingVolatilityEstimator();
            _riskFreeRateEstimator = riskFreeRateEstimator ?? new ConstantQLRiskFreeRateEstimator(OptionPriceModels.DefaultRiskFreeRate);
            _dividendYieldEstimator = dividendYieldEstimator ?? new ConstantQLDividendYieldEstimator(OptionPriceModels.DefaultDividendRate);
            // odds is the model is most likely european
            _exerciseFunc = exerciseFunc ?? OptionPriceModels.EuropeanExercise;
            _payoffFunc = payoffFunc ?? OptionPriceModels.PlainVanillaPayoff;
        }

        /// <summary>
        /// Method constructs QuantLib option price model with necessary estimators of underlying volatility, risk free rate, and underlying dividend yield
        /// </summary>
        /// <param name="pricingEngineFunc">Function takes option and modeled stochastic process, and returns new pricing engine to run calculations for that option</param>
        /// <param name="underlyingVolEstimator">The underlying volatility estimator</param>
        /// <param name="riskFreeRateEstimator">The risk free rate estimator</param>
        /// <param name="dividendYieldEstimator">The underlying dividend yield estimator</param>
        /// <param name="exerciseFunc">Optional exercise func for defining american or european</param>
        /// <param name="payoffFunc">Optional payoff func for defining the type of payoff to model</param>
        public QLOptionPriceModel(PricingEngineFuncEx pricingEngineFunc,
            IQLUnderlyingVolatilityEstimator underlyingVolEstimator,
            IQLRiskFreeRateEstimator riskFreeRateEstimator,
            IQLDividendYieldEstimator dividendYieldEstimator,
            ExerciseFunc exerciseFunc = null,
            PayoffFunc payoffFunc = null
            )
        {
            _pricingEngineFunc = pricingEngineFunc;
            _exerciseFunc = exerciseFunc;
            _underlyingVolEstimator = underlyingVolEstimator ?? new ConstantQLUnderlyingVolatilityEstimator();
            _riskFreeRateEstimator = riskFreeRateEstimator ?? new ConstantQLRiskFreeRateEstimator(OptionPriceModels.DefaultRiskFreeRate);
            _dividendYieldEstimator = dividendYieldEstimator ?? new ConstantQLDividendYieldEstimator(OptionPriceModels.DefaultDividendRate);
            // odds is the model is most likely european
            _exerciseFunc = exerciseFunc ?? OptionPriceModels.EuropeanExercise;
            _payoffFunc = payoffFunc ?? OptionPriceModels.PlainVanillaPayoff;
        }

        /// <summary>
        /// Evaluates the specified option contract to compute a theoretical price, IV and greeks
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>An instance of <see cref="OptionPriceModelResult"/> containing the theoretical
        /// price of the specified option contract</returns>
        public OptionPriceModelResult Evaluate(Security security, Slice slice, OptionContract contract)
        {
            try
            {
                // setting up option pricing parameters
                var calendar = new UnitedStates();
                var dayCounter = new Actual365Fixed();
                var optionSecurity = (Option)security;

                var settlementDate = contract.Time.Date.AddDays(Option.DefaultSettlementDays);
                var maturityDate = contract.Expiry.Date.AddDays(Option.DefaultSettlementDays);
                var underlyingQuoteValue = new SimpleQuote((double)optionSecurity.Underlying.Price);

                var dividendYieldValue = new SimpleQuote(_dividendYieldEstimator.Estimate(security, slice, contract));
                var dividendYield = new Handle<YieldTermStructure>(new FlatForward(0, calendar, dividendYieldValue, dayCounter));

                var riskFreeRateValue = new SimpleQuote(_riskFreeRateEstimator.Estimate(security, slice, contract));
                var riskFreeRate = new Handle<YieldTermStructure>(new FlatForward(0, calendar, riskFreeRateValue, dayCounter));

                var underlyingVolValue = new SimpleQuote(_underlyingVolEstimator.Estimate(security, slice, contract));
                var underlyingVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(underlyingVolValue), dayCounter));

                // preparing stochastic process and payoff functions
                var stochasticProcess = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValue), dividendYield, riskFreeRate, underlyingVol);
                var payoff = _payoffFunc(security.Symbol, contract);

                // creating option QL object
                var exercise = _exerciseFunc(security.Symbol, settlementDate, maturityDate);
                var option = new VanillaOption(payoff, exercise);

                Settings.setEvaluationDate(settlementDate);

                // preparing pricing engine QL object
                option.setPricingEngine(_pricingEngineFunc(contract.Symbol, stochasticProcess));

                // running calculations
                var npv = EvaluateOption(option);

                // function extracts QL greeks catching exception if greek is not generated by the pricing engine and reevaluates option to get numerical estimate of the seisitivity
                Func<Func<double>, Func<double>, decimal> tryGetGreekOrReevaluate = (greek, reevalFunc) =>
                {
                    try
                    {
                        return (decimal)greek();
                    }
                    catch (Exception)
                    {
                        return EnableGreekApproximation ? (decimal)reevalFunc() : 0.0m;
                    }
                };

                // function extracts QL greeks catching exception if greek is not generated by the pricing engine
                Func<Func<double>, decimal> tryGetGreek = greek => tryGetGreekOrReevaluate(greek, () => 0.0);

                // function extracts QL IV catching exception if IV is not generated by the pricing engine
                Func<decimal> tryGetImpliedVol = () =>
                {
                    try
                    {
                        return (decimal)option.impliedVolatility((double)optionSecurity.Price, stochasticProcess);
                    }
                    catch (Exception err)
                    {
                        Log.Debug("tryGetImpliedVol() error: " + err.Message);
                        return 0m;
                    }
                };

                Func<Tuple<decimal, decimal>> evalDeltaGamma = () =>
                {
                    try
                    {
                        return Tuple.Create((decimal)option.delta(), (decimal)option.gamma());
                    }
                    catch (Exception)
                    {
                        if (EnableGreekApproximation)
                        {
                            var step = 0.01;
                            var initial = underlyingQuoteValue.value();
                            underlyingQuoteValue.setValue(initial - step);
                            var npvMinus = EvaluateOption(option);
                            underlyingQuoteValue.setValue(initial + step);
                            var npvPlus = EvaluateOption(option);
                            underlyingQuoteValue.setValue(initial);

                            return Tuple.Create((decimal)((npvPlus - npvMinus) / (2 * step)),
                                                (decimal)((npvPlus - 2 * npv + npvMinus) / (step * step)));
                        }
                        else
                            return Tuple.Create(0.0m, 0.0m);
                    }
                };

                Func<double> reevalVega = () =>
                {
                    var step = 0.001;
                    var initial = underlyingVolValue.value();
                    underlyingVolValue.setValue(initial + step);
                    var npvPlus = EvaluateOption(option);
                    underlyingVolValue.setValue(initial);

                    return (npvPlus - npv) / step;
                };

                Func<double> reevalTheta = () =>
                {
                    var step = 1.0 / 365.0;

                    Settings.setEvaluationDate(settlementDate.AddDays(-1));
                    var npvMinus = EvaluateOption(option);
                    Settings.setEvaluationDate(settlementDate);

                    return (npv - npvMinus) / step;
                };

                Func<double> reevalRho = () =>
                {
                    var step = 0.001;
                    var initial = riskFreeRateValue.value();
                    riskFreeRateValue.setValue(initial + step);
                    var npvPlus = EvaluateOption(option);
                    riskFreeRateValue.setValue(initial);

                    return (npvPlus - npv) / step;
                };

                // producing output with lazy calculations of IV and greeks

                return new OptionPriceModelResult((decimal)npv,
                            tryGetImpliedVol,
                            () => new Greeks(evalDeltaGamma,
                                            () => tryGetGreekOrReevaluate(() => option.vega(), reevalVega),
                                            () => tryGetGreekOrReevaluate(() => option.theta(), reevalTheta),
                                            () => tryGetGreekOrReevaluate(() => option.rho(), reevalRho),
                                            () => tryGetGreek(() => option.elasticity())));
            }
            catch(Exception err)
            {
                Log.Debug("QLOptionPriceModel.Evaluate() error: " + err.Message);
                return new OptionPriceModelResult(0m, new Greeks());
            }
        }

        /// <summary>
        /// Runs option evaluation and logs exceptions
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        private static double EvaluateOption(VanillaOption option)
        {
            try
            {
                var npv = option.NPV();

                if (double.IsNaN(npv) ||
                    double.IsInfinity(npv))
                    npv = 0.0;

                return npv;
            }
            catch (Exception err)
            {
                Log.Debug("QLOptionPriceModel.EvaluateOption() error: " + err.Message);
                return 0.0;
            }
        }

        public static QLNet.Option.Type GetType(OptionRight right)
        {
            if (right == OptionRight.Call)
            {
                return QLNet.Option.Type.Call;
            }

            return QLNet.Option.Type.Put;
        }
    }
}
