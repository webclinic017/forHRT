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


            if (BuyPosition == null)
            {
                if (_linearRegressionIntercept.Result.Last(1) > _simpleMovingAverage.Result.Last(1))
                {
                    if (_donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
                    {
                        ClosePositions(TradeType.Sell);
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, BuyStopLossInPips, null);
                        if (Equity > 90000)
                        {
                            if (_donchianChannel.Middle.Last(1) > _simpleMovingAverage.Result.Last(1))
                            {
                                ClosePositions(TradeType.Buy);
                            }
                            else if (_linearRegressionIntercept.Result.Last(1) < _simpleMovingAverage.Result.Last(1))
                            {
                                ClosePositions(TradeType.Buy);
                            }
                        }
                        else if (Equity <= 90000)
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
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, SellStopLossInPips, null);
                        if (Equity > 90000)
                        {
                            if (_linearRegressionIntercept.Result.Last(1) > _simpleMovingAverage.Result.Last(1))
                            {
                                ClosePositions(TradeType.Sell);
                            }
                            else if (_donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
                            {
                                ClosePositions(TradeType.Sell);
                            }
                        }
                        else if (Equity <= 90000)
                        {
                            ClosePositions(TradeType.Sell);
                        }
                    }
                }
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
