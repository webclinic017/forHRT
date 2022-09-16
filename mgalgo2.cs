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

        [Parameter("Volume", DefaultValue = 10, Group = "Trade")]
        public double VolumeInLots { get; set; }

        [Parameter("Stop Loss", DefaultValue = 40, Group = "Trade")]
        public int StopLoss { get; set; }

        [Parameter("Lable", DefaultValue = "trade", Group = "Trade")]
        public string Label { get; set; }

        public int TakeProfit { get; set; }

        public Position[] BotPositions
        {
            get { return Positions.FindAll(Label); }
        }
        protected override void OnStart()
        {
            _volumeInUnits = Symbol.QuantityToVolumeInUnits(VolumeInLots);

            TakeProfit = (StopLoss * 2);

            ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);
            ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);

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
        private void OnPositionsClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            var equity = Account.Equity;

            if (position.Label != Label || position.SymbolName != SymbolName)
                return;

            if (position.NetProfit > 0 && position.TradeType == TradeType.Buy)
            {
                ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);
            }
            else if (position.NetProfit > 0 && position.TradeType == TradeType.Sell)
            {
                ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);
            }
            else if (position.NetProfit < 0 && position.TradeType == TradeType.Buy)
            {
                ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);
            }
            else if (position.NetProfit < 0 && position.TradeType == TradeType.Sell)
            {
                ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, Label, StopLoss, TakeProfit);
            }
        }
        protected override void OnStop()
        {
            ClosePositions(TradeType.Buy);
            ClosePositions(TradeType.Sell);
        }
    }
}
