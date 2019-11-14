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

using System;
using System.Linq;
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    public class WalkForwardOptimizationPortfolioConstructionModel : PortfolioConstructionModel
    {
        /// <summary>
        /// Gets the currently elected leader.
        /// </summary>
        public IPortfolioConstructionModel Leader { get; private set; }

        private DateTime nextElection;
        private readonly TimeSpan _period;
        private readonly List<SimulatedPortfolio> _simulations;

        public WalkForwardOptimizationPortfolioConstructionModel(TimeSpan period, params IPortfolioConstructionModel[] models)
        {
            if (models.IsNullOrEmpty())
            {
                throw new ArgumentException("At least one PortfolioConstructionModel must be provided.");
            }

            _period = period;

            _simulations = models.ToList(model => new SimulatedPortfolio(model));

            // TODO : Probably wrong, likely better to do nothing until we start validating some models
            Leader = _simulations[0].Model;
        }

        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithm algorithm, Insight[] insights)
        {
            foreach (var simulation in _simulations)
            {
                // simulate applying each insight to each candidate model every 'period' we'll elect the leader and use him for trading
                foreach (var target in simulation.Model.CreateTargets(algorithm, insights))
                {
                    // apply the target to our simulated portfolio
                    simulation.ApplyTarget(algorithm, target);
                }
            }

            if (algorithm.UtcTime > nextElection)
            {
                Leader = _simulations.OrderByDescending(s => s.ProfitLoss).First().Model;
            }

            return Leader.CreateTargets(algorithm, insights);
        }

        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
            foreach (var model in _simulations)
            {
                var modelName = model.GetType().Name;

                // keep our dictionaries synchronized with the algorithm's selected securities
                NotifiedSecurityChanges.UpdateDictionary(model.Holdings, changes,
                    security => new SimulatedHoldings(modelName, security, algorithm.Portfolio.CashBook)
                );
            }
        }

        private class SimulatedPortfolio
        {
            public IPortfolioConstructionModel Model { get; }
            public Dictionary<Symbol, SimulatedHoldings> Holdings { get; }

            public decimal ProfitLoss => Holdings.Sum(kvp => kvp.Value.UnrealizedProfit);

            public SimulatedPortfolio(IPortfolioConstructionModel model)
            {
                Model = model;
                Holdings = new Dictionary<Symbol, SimulatedHoldings>();
            }

            public void ApplyTarget(QCAlgorithm algorithm, IPortfolioTarget target)
            {
                SimulatedHoldings holding;
                if (!Holdings.TryGetValue(target.Symbol, out holding))
                {
                    throw new KeyNotFoundException($"Unable to find security holdings in {Model.GetType().Name} for: '{target.Symbol}'");
                }

                holding.Target = target;
                if (holding.RequiresFill)
                {
                    holding.SimulateFill(algorithm);
                }
            }
        }

        private class SimulatedHoldings : SecurityHolding
        {
            public string ModelName { get; }
            // expose protected member
            public new Security Security => base.Security;
            public bool RequiresFill => Quantity != Target.Quantity;

            public SimulatedHoldings(string modelName, Security security, ICurrencyConverter currencyConverter)
                : base(security, currencyConverter)
            {
                ModelName = modelName;
            }

            public void SimulateFill(QCAlgorithm algorithm)
            {
                // we'll use market orders for now to keep things simple
                var order = new MarketOrder(
                    Symbol,
                    Target.Quantity,
                    algorithm.UtcTime,
                    ModelName
                );

                // simulate a fill using the security's settings
                var fill = Security.FillModel.Fill(
                    new FillModelParameters(
                        Security,
                        order,
                        algorithm.SubscriptionManager.SubscriptionDataConfigService,
                        algorithm.Settings.StalePriceTimeSpan
                    )
                ).OrderEvent;

                // apply the fill
                SetHoldings(fill.FillPrice, fill.FillQuantity);
            }
        }
    }
}
