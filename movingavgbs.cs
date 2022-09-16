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

        [Parameter("Reward % Per Trade", DefaultValue = 20, Group = "Trade")]
        public int reward { get; set; }

        [Parameter("Risk % Per Trade", DefaultValue = 20, Group = "Trade")]
        public int risk { get; set; }

        public Position[] BotPositions
        {
            get { return Positions.FindAll(Label); }
        }
        protected override double GetFitness(GetFitnessArgs args)
        {
            var WT = (args.WinningTrades / args.TotalTrades);
            var LT = (args.TotalTrades / args.LosingTrades);
            var MEDP = (args.Equity / args.MaxEquityDrawdownPercentages);
            var MBDP = (args.Equity / args.MaxBalanceDrawdownPercentages);

            return WT + LT + MEDP + MBDP;
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

            var SL = Math.Round((0.0001 / Symbol.Bid) * risk / 100);
            var TP = Math.Round((0.0001 / Symbol.Bid) * reward / 100);

            var Equity = Account.Equity;

            if (Equity > 96000)
            {
                if (BuyPosition == null && _simpleMovingAverage.Result.Last(1) < _simpleMovingAverage.Result.Last(2))
                {
                    SL = SL - (SL * 0.1);
                    ClosePositions(TradeType.Buy);
                    ClosePositions(TradeType.Sell);
                    ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, SL, TP);
                }
                else if (SellPosition == null && _simpleMovingAverage.Result.Last(1) > _simpleMovingAverage.Result.Last(2))
                {
                    SL = SL - (SL * 0.1);
                    ClosePositions(TradeType.Buy);
                    ClosePositions(TradeType.Sell);
                    ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, SL, TP);
                }
                else if (SellPosition == null && _simpleMovingAverage.Result.Last(1) < _simpleMovingAverage.Result.Last(2))
                {
                    TP = TP + (TP * 0.1);
                    ClosePositions(TradeType.Buy);
                    ClosePositions(TradeType.Sell);
                    ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, SL, TP);
                }
                else if (BuyPosition == null && _simpleMovingAverage.Result.Last(1) > _simpleMovingAverage.Result.Last(2))
                {
                    TP = TP + (TP * 0.1);
                    ClosePositions(TradeType.Buy);
                    ClosePositions(TradeType.Sell);
                    ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, SL, TP);
                }
            }
            else if (Equity <= 96000)
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
