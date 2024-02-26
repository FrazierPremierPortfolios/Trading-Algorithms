//@version=4
study(title = "3 Series OB SMA Cross", overlay = true)

GetPipSize() =>
    syminfo.mintick * (syminfo.type == "futures" ? 1 : syminfo.type == "crypto" ? 1 : syminfo.type == "cfd" ? 10 : 1)

    
// Inputs
minEngulfPips = input(title="Min. Engulf Pips", type=input.integer, defval=1, minval=1) * GetPipSize()
considerTrend = input(title="Consider Trend", type=input.bool, defval=false)

// Calculate Moving Averages
fastMA = sma(close, 1)
slowMA = sma(close, 5)

// Bullish Structure Shift Condition
bullishStructureShift = crossover(fastMA, slowMA)

// Bearish Structure Shift Condition
bearishStructureShift = crossunder(fastMA, slowMA)

// Logic
isBullEngulf = bullishStructureShift and open[4] < close[4] and open[3] > close[3] and open[2] > close[2] and open[1] > close[1] and close > open
isBearEngulf = bearishStructureShift and open[4] > close[4] and open[3] < close[3] and open[2] < close[2] and open[1] < close[1] and open > close

// Graphics
barcolor = isBullEngulf ? color.rgb(72, 241, 123) : isBearEngulf ? color.rgb(153, 26, 26) : na
barcolor(color = barcolor, title= "Engulf Candle Color")

dif = abs(close - open) / GetPipSize()
difStr = tostring(dif)
                 
if isBullEngulf
    label.new(bar_index, 0, tostring(dif), yloc = yloc.belowbar, textcolor = color.rgb(75, 158, 75), style = label.style_triangleup, color = color.green)
if isBearEngulf
    label.new(bar_index, 0, tostring(dif), yloc = yloc.abovebar, textcolor = color.red, style = label.style_triangledown, color = color.red)

// Alerts
if (isBullEngulf)
    alert(message = "Bullish Engulfing", freq = alert.freq_once_per_bar)
if (isBearEngulf)
    alert(message = "Bearish Engulfing", freq = alert.freq_once_per_bar)

// Plots
plot(fastMA, color = color.blue, title = "Fast MA")
plot(slowMA, color = color.orange, title = "Slow MA")
