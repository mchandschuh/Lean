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
*/

using QLNet;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Static class contains definitions of major option pricing models that can be used in LEAN
    /// </summary>
    /// <remarks>
    /// To introduce particular model into algorithm add the following line to the algorithm's Initialize() method:
    ///
    ///     option.PriceModel = OptionPriceModels.BjerksundStensland(); // Option pricing model of choice
    ///
    /// </remarks>
    public static class OptionPriceModels
    {
        /// <summary>
        /// Gets an <see cref="ExerciseFunc"/> that creates <see cref="EuropeanExercise"/>
        /// </summary>
        public static readonly ExerciseFunc EuropeanExercise
            = (symbol, settlementDate, maturityDate) => new EuropeanExercise(maturityDate);

        /// <summary>
        /// Gets an <see cref="ExerciseFunc"/> that creates <see cref="AmericanExercise"/>
        /// </summary>
        public static readonly ExerciseFunc AmericanExercise
            = (symbol, settlementDate, maturityDate) => new AmericanExercise(settlementDate, maturityDate);

        /// <summary>
        /// Get a <see cref="PayoffFunc"/> that creates <see cref="PlainVanillaPayoff"/>
        /// </summary>
        public static readonly PayoffFunc PlainVanillaPayoff
            = (symbol, contract) => new PlainVanillaPayoff(QLOptionPriceModel.GetType(contract.Right), (double) contract.Strike);

        /// <summary>
        /// Gets or set the default risk free rate used when constructing <see cref="IQLRiskFreeRateEstimator"/>
        /// </summary>
        public static double DefaultRiskFreeRate = 0.01;

        /// <summary>
        /// Gets or set the default risk free rate used when constructing <see cref="IQLDividendYieldEstimator"/>
        /// </summary>
        public static double DefaultDividendRate = 0.0;

        private const int TimeStepsBinomial = 100;
        private const int TimeStepsFd = 100;

        /// <summary>
        /// Pricing engine for European vanilla options using analytical formulae.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_analytic_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BlackScholes()
        {
            return CreatePriceModel(
                (sym, process) => new AnalyticEuropeanEngine(process),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Barone-Adesi and Whaley pricing engine for American options (1987)
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_barone_adesi_whaley_approximation_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BaroneAdesiWhaley()
        {
            return CreatePriceModel(
                (sym, process) => new BaroneAdesiWhaleyApproximationEngine(process),
                exerciseFunc: AmericanExercise
            );
        }

        /// <summary>
        /// Bjerksund and Stensland pricing engine for American options (1993)
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_bjerksund_stensland_approximation_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BjerksundStensland()
        {
            return CreatePriceModel(
                (sym, process) => new BjerksundStenslandApproximationEngine(process),
                exerciseFunc: AmericanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using integral approach.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_integral_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel Integral()
        {
            return CreatePriceModel(
                (sym, process) => new IntegralEngine(process),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European options using finite-differences.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel CrankNicolsonFD()
        {
            PricingEngineFuncEx pricingEngineFunc = (symbol, process) =>
                symbol.ID.OptionStyle == OptionStyle.American
                    ? new FDAmericanEngine(process, TimeStepsFd, TimeStepsFd - 1) as IPricingEngine
                    : new FDEuropeanEngine(process, TimeStepsFd, TimeStepsFd - 1) as IPricingEngine;

            ExerciseFunc exerciseFunc = (symbol, date, maturityDate) => symbol.ID.OptionStyle == OptionStyle.American
                ? AmericanExercise(symbol, date, maturityDate)
                : EuropeanExercise(symbol, date, maturityDate);

            return CreatePriceModel(pricingEngineFunc, exerciseFunc: exerciseFunc);
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Jarrow-Rudd model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialJarrowRudd()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<JarrowRudd>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Cox-Ross-Rubinstein(CRR) model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialCoxRossRubinstein()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<CoxRossRubinstein>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Additive Equiprobabilities model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel AdditiveEquiprobabilities()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<AdditiveEQPBinomialTree>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Trigeorgis model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialTrigeorgis()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<Trigeorgis>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Tian model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialTian()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<Tian>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Leisen-Reimer model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialLeisenReimer()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<LeisenReimer>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Pricing engine for European vanilla options using binomial trees. Joshi model.
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_f_d_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static IOptionPriceModel BinomialJoshi()
        {
            return CreatePriceModel(
                (sym, process) => new BinomialVanillaEngine<Joshi4>(process, TimeStepsBinomial),
                exerciseFunc: EuropeanExercise
            );
        }

        /// <summary>
        /// Creates an options price model using the specified models
        /// </summary>
        public static IOptionPriceModel CreatePriceModel(PricingEngineFuncEx pricingEngineFunc,
            IQLUnderlyingVolatilityEstimator underlyingVolEstimator = null,
            IQLRiskFreeRateEstimator riskFreeRateEstimator = null,
            IQLDividendYieldEstimator dividendYieldEstimator = null,
            ExerciseFunc exerciseFunc = null,
            PayoffFunc payoffFunc = null
            )
        {
            return new QLOptionPriceModel(
                pricingEngineFunc,
                underlyingVolEstimator,
                riskFreeRateEstimator,
                dividendYieldEstimator,
                exerciseFunc,
                payoffFunc
            );
        }
    }
}
