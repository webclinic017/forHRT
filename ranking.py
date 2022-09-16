from alpaca_trade_api.rest import REST, TimeFrame
from alpaca.data.historical import StockHistoricalDataClient
from alpaca.data.requests import StockLatestQuoteRequest
from alpaca.trading.client import TradingClient
from alpaca.trading.requests import MarketOrderRequest
from alpaca.trading.enums import OrderSide, TimeInForce
from paperconfig import api_key2, secret_key2
from config import api_key, secret_key
import time
from datetime import date
from dateutil.relativedelta import relativedelta
import math
from sklearn.model_selection import train_test_split
from lightgbm import LGBMRegressor
from sklearn.metrics import mean_squared_error
import optuna
import pandas as pd
import schedule

trading_client = TradingClient(api_key2, secret_key2)

def buy(symbol):
    order_data = MarketOrderRequest(symbol=symbol, qty=10, side=OrderSide.BUY, time_in_force=TimeInForce.DAY)
    trading_client.submit_order(order_data=order_data)
    print ('Bought:',symbol)
def sell(symbol):
    order_data = MarketOrderRequest(symbol=symbol, qty=10, side=OrderSide.SELL, time_in_force=TimeInForce.DAY)
    trading_client.submit_order(order_data=order_data)
    print ('Sold:',symbol)
def closeall():
    trading_client.close_all_positions(cancel_orders=True)

def rankall():
    for symbol in portfolio:
        rank(symbol)
    rankings = pd.DataFrame()
    rankings['symbol'] = portfolio
    rankings['Predictions'] = data
    rankings['Predictions'] = rankings['Predictions'].str[0]
    rankings = rankings.sort_values(by = "Predictions", ascending=False)
    rankings = rankings.reset_index()
    rankings = rankings.drop(columns=['index'])
    rankings['Ranks'] = rankings.index+1
    buyc = round(((len(rankings))*0.2),0)
    sellc = round(((len(rankings))*0.8),0)
    buypos = []
    sellpos = []

    for symbol in rankings['symbol'].loc[rankings['Ranks'] <= buyc]:
        buy(symbol)
        buypos.append(symbol)
    for symbol in rankings['symbol'].loc[rankings['Ranks'] >= sellc]:
        sell(symbol)
        sellpos.append(symbol)
def rank(symbol):
    data = []
    start = (date.today() - relativedelta(weeks=1)).strftime("%Y-%m-%d")
    end = (date.today()- relativedelta(days=1)).strftime("%Y-%m-%d")

    pcontent = api.get_bars(symbol, TimeFrame.Day, start, end, adjustment='raw').df
    pcontent['range'] = (pcontent['high'] - pcontent['low']) / pcontent['close']
    pcontent['change'] = (pcontent['close'] - pcontent['open']) / pcontent['open']

    factors = pcontent[['open','high','low','close','volume','trade_count','range','change']]
    factors = factors.dropna()
    forecast_col = 'close'
    forecast_out = int(math.ceil(0.01 * len(factors)))
    factors['forecast'] = factors[forecast_col].shift(-forecast_out)
    factors.dropna(inplace=True)
    X = factors.iloc[:,:-1]
    y = factors.forecast

    def objective(trial,data=data):
    
        X_train,X_test,y_train,y_test = train_test_split(X,y)

        params = {
        'metric': 'rmse', 
        'random_state': 48,
        'n_estimators': 20000,
        'reg_alpha': trial.suggest_loguniform('reg_alpha', 1e-3, 10.0),
        'reg_lambda': trial.suggest_loguniform('reg_lambda', 1e-3, 10.0),
        'colsample_bytree': trial.suggest_categorical('colsample_bytree', [0.3,0.4,0.5,0.6,0.7,0.8,0.9, 1.0]),
        'subsample': trial.suggest_categorical('subsample', [0.4,0.5,0.6,0.7,0.8,1.0]),
        'learning_rate': trial.suggest_categorical('learning_rate', [0.006,0.008,0.01,0.014,0.017,0.02]),
        'max_depth': trial.suggest_categorical('max_depth', [10,20,100]),
        'num_leaves' : trial.suggest_int('num_leaves', 7, 1000),
        'min_child_samples': trial.suggest_int('min_child_samples', 1, 300),
        'cat_smooth' : trial.suggest_int('min_data_per_groups', 1, 100)
        }

        model = LGBMRegressor(**params)
        model.fit(X_train,y_train)
        prediction = model.predict(X_test)
        pred = (mean_squared_error(prediction,y_test))
        return pred


    study = optuna.create_study(direction='maximize',)
    study.optimize(objective, n_trials=10)
    pred = study.best_value

    ticker = StockLatestQuoteRequest(symbol_or_symbols=[symbol])
    quote = client.get_stock_latest_quote(ticker)
    denom = int(quote[symbol].ask_price)

    #denom = factors['close'].iat[-1]
    ranks = round(pred/denom,4)
    data.append([ranks])

api = REST(api_key, secret_key)
sp = pd.read_html('https://stockmarketmba.com/stocksinthesp500.php')

client = StockHistoricalDataClient(api_key, secret_key)

portfolio = []
data = []

for x in sp:
    string =  (x['Symbol'])
    for y in string:
        portfolio.append(y)
portfolio.remove('TOTAL')

rankall()
schedule.every().day.at("15:55").do(closeall)
