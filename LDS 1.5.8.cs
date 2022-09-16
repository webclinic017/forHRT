using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LDSTradingSystem : Robot
    {
        private double _volumeInUnits;
        private DonchianChannel _donchianChannel;
        private LinearRegressionIntercept _linearRegressionIntercept;
        private SimpleMovingAverage _simpleMovingAverage;

        [Parameter("Period", DefaultValue = 20, Group = "Trade")]
        public int P { get; set; }

        [Parameter("Volume (Lots)", DefaultValue = 10, Group = "Trade")]
        public double VolumeInLots { get; set; }

        [Parameter("Lable", DefaultValue = "trade", Group = "Trade")]
        public string Label { get; set; }

        public Position[] BotPositions
        {
            get { return Positions.FindAll(Label); }
        }
        protected override double GetFitness(GetFitnessArgs args)
        {
            var WT = (args.WinningTrades / args.TotalTrades);
            var LT = (args.TotalTrades / args.LosingTrades);
            var TT = (args.TotalTrades);
            var MEDP = (args.Equity / args.MaxEquityDrawdownPercentages);
            var MBDP = (args.Equity / args.MaxBalanceDrawdownPercentages);
            var PF = (args.ProfitFactor / args.TotalTrades);
            var NP = (args.NetProfit / args.Equity);
            var SOR = (args.SortinoRatio);


            return WT + LT + MEDP + MBDP + PF + NP + SOR + TT;
        }
        protected override void OnStart()
        {
            _volumeInUnits = Symbol.QuantityToVolumeInUnits(VolumeInLots);
            _donchianChannel = Indicators.DonchianChannel(P);
            _linearRegressionIntercept = Indicators.LinearRegressionIntercept(Bars.ClosePrices, P / 2);
            _simpleMovingAverage = Indicators.SimpleMovingAverage(Bars.ClosePrices, P);

        }
        protected override void OnTick()
        {
            var BuyPosition = Positions.Find(Label, SymbolName, TradeType.Buy);
            var SellPosition = Positions.Find(Label, SymbolName, TradeType.Sell);

            var BuyStopLossInPips = Math.Round(Symbol.Ask - 10 * Symbol.PipSize);
            var SellStopLossInPips = Math.Round(Symbol.Ask - 10 * Symbol.PipSize);

            var Equity = Account.Equity;

            if (Equity > 95000)
            {
                if (BuyPosition == null)
                {
                    if (_linearRegressionIntercept.Result.Last(1) > _simpleMovingAverage.Result.Last(1))
                    {
                        if (_donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
                        {
                            ClosePositions(TradeType.Sell);
                            ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, null, null);
                            if (Equity <= 95000)
                            {
                                ClosePositions(TradeType.Buy);
                            }
                        }
                    }
                }
                else if (SellPosition == null)
                {
                    if (_donchianChannel.Middle.Last(1) > _simpleMovingAverage.Result.Last(1))
                    {
                        if (_linearRegressionIntercept.Result.Last(1) < _simpleMovingAverage.Result.Last(1))
                        {
                            ClosePositions(TradeType.Buy);
                            ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, null, null);
                            if (Equity <= 95000)
                            {
                                ClosePositions(TradeType.Sell);
                            }
                        }
                    }
                }
            }
            else if (Equity <= 95000)
            {
                Stop();
            }
        }
        private void ClosePositions(TradeType tradeType)
        {
            foreach (var position in BotPositions)
            {
                if (position.TradeType != tradeType)
                    continue;
                ClosePosition(position);
            }
        }
        protected override void OnStop()
        {
            ClosePositions(TradeType.Buy);
            ClosePositions(TradeType.Sell);
        }
    }
}
