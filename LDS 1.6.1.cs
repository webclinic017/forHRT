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

            var seconds = (Bars[1].OpenTime - Bars[0].OpenTime).TotalSeconds;

            if (seconds * period != 28800)
            {
                Stop();
            }
        }
        protected override void OnTick()
        {
            var SL = Math.Round((Bars[1].High - Bars[1].Low) / 0.0001);

            var BuyPosition = Positions.Find(Label, SymbolName, TradeType.Buy);
            var SellPosition = Positions.Find(Label, SymbolName, TradeType.Sell);

            var Equity = Account.Equity;

            if (Equity > 96000)
            {
                if (BuyPosition == null)
                {
                    if (_linearRegressionIntercept.Result.Last(1) > _simpleMovingAverage.Result.Last(1))
                    {
                        if (_donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
                        {
                            ClosePositions(TradeType.Sell);
                            ExecuteOrder(GetVolume(SL), TradeType.Buy);
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
                            ExecuteOrder(GetVolume(SL), TradeType.Sell);
                        }
                    }
                }
            }
            else if (Equity <= 96000)
            {
                Stop();
            }
        }
        private void OpenPositions(TradeType tradeType)
        {
            var BuyPosition = Positions.Find(Label, SymbolName, TradeType.Buy);
            var SellPosition = Positions.Find(Label, SymbolName, TradeType.Sell);

            if (BuyPosition == null && _donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
            {
                ClosePositions(TradeType.Buy);
            }
            else if (BuyPosition == null && _linearRegressionIntercept.Result.Last(1) > _simpleMovingAverage.Result.Last(1))
            {
                ClosePositions(TradeType.Buy);
            }
            else if (SellPosition == null && _linearRegressionIntercept.Result.Last(1) < _simpleMovingAverage.Result.Last(1))
            {
                ClosePositions(TradeType.Sell);
            }
            else if (SellPosition == null && _donchianChannel.Middle.Last(1) < _simpleMovingAverage.Result.Last(1))
            {
                ClosePositions(TradeType.Sell);
            }
        }
        private void ExecuteOrder(double volumeInUnits, TradeType tradeType)
        {
            var SL = Math.Round((Bars[1].High - Bars[1].Low) / 0.0001);
            var TP = SL / 2;

            var result = ExecuteMarketOrder(tradeType, SymbolName, volumeInUnits, Label, SL, null);

            if (result.Error == ErrorCode.NoMoney)
                Stop();
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
        private double GetVolume(double StopLoss)
        {
            var maxAmountRisked = Account.Equity * (RiskPer / 100);
            var volume = maxAmountRisked / ((StopLoss * Symbol.PipValue));
            return Symbol.NormalizeVolumeInUnits((double)volume);
        }
        protected override void OnStop()
        {
            ClosePositions(TradeType.Buy);
            ClosePositions(TradeType.Sell);
        }

    }
}
