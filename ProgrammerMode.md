# Gambler.Bot Programmer Mode Documentation

## Process


Like previous versions, Gambler.Bot’s programmer mode runs in a loop. When the users starts the bot, the bot executes the Reset function (detailed below) for the applicable game. The NextBet parameter from the reset function will be used as the first bet. Once the bet result for the first bet is received from the site, the bot will do its internal processing (like logging it to the database, sending it to the GUI etc). Once all internal processing is completed, the global variables for the programmer mode is updated (this includes the stats, sitedetails and sitestats objects). The CalculateBet function is then called. You have access to:
- PreviousBet: result of the previous bet, includes betamount, profit, roll, betid. Full UML for class available in this document.
- Win: a boolean called that indicates whether the previous bet is seen as a win internally or not, 
- NextBet: An Instance of the specific PlaceBet class used for the game, with the amount, and game specific values copied from the previous bet. This object is used to place the next bet, so set the properties of this class to manipulate the next bet.

## Required functions
For a script to be valid, it MUST implement the following functions:
- Void CalculateBet()
- Reset()

CalculateBet is the function that handles the core of your logic. It gets called when a bet result has been received and the next bet needs to be calculated. The NextBet variable is used to send the bet to the site, and must thus be used to specify the bet details needed.

Reset is called when the script starts and the result is used for the first bet. It can be used in CalculateBet to reset your script easily, and will be called from Gambler.Bot if internal triggers or error handling is used and set to reset. It is recommended that any variables that might influence the functionality of your script be reset to their definition/instantiation values in this function. The NextBet parameter is used to send the bet to the site, and must thus be used to specify the bet details needed.

## Optional Functions
Gambler.Bot has some optional functions that might be called from Gambler.Bot, and can be used in the script to extend functionality. These are not yet implemented but will include custom error handling for errors received from the site or while betting.

## Available variables
Unlike version 3 of Gambler.Bot, the amount of internal variables has been reduced, but the amount of information available to the script has been greatly increased. There are now only 4 global values defined in Gambler.Bot’s programmer mode
- Stats: Instance of SessionStats, provides statistics of the current betting session, including wins, losses, profit,streak profits etc. 
- SiteDetails: Instance of SiteDetails, Provides details of the site being used 
- SiteStats: Instance of SiteStats, provides site level stats of the current user including amount of bets at the site, wins and losses at the site, wagered, profit and balance.
- Balance: Users balance for the currency being used at the site being used.

These variables are updated after each bet and are read only. While the programmer mode will technically allow a script to assign a value to any of these variables and their properties, the changes will be overwritten with the next bet. The instances received in the programmer mode are disposable copies of the instances used inside of the bot and is disposed of soon after the result of the next bet is received.

## Available functions
There are internal functions available for the programmer modes, to access features of the bot and sites being used.

- Void Withdraw(string Address, decimal Amount)
- Void Bank(string Address, decimal Amount)
- Void Invest(decimal Amount)
- Void Tip(string User, decimal Amount)
- Void ResetSeed()
- Void Print(string Message)
- Void RunSim(decimal StartingBalance, long NumberOfBets)
- Void ResetStats()
- Object Read(string Prompt, int ReturnType)
- Object Readadv(string Prompt, string AccetText, string CancelText, int ReturnType)
- Void Alarm()
- Void Ching()
- Void ResetBuiltIn()
- Void ExportSim(string FileName)
- Void Stop()

## Legacy support

Gambler.Bot aims to support legacy scripts from version 3 and partially from the unofficial version 4. To use a legacy script, just select the Programmer Lua mode and use as usual. If the v5 script definitions are available, the bot will prefer them but fall back to legacy scripts when not available.

Legacy features that will not break the script but will perform no action when called:
- resetbuiltin

Legacy features that will break the script if called:
- getHistory
- getHistoryByDate
- getHistoryByQuery
- runsim
- martingale
- labouchere
- fibonacci
- dalembert
- presetlist
- setvalueint
- setvaluestring
- setvaluedecimal
- setvaluebool
- getvalue
- loadstrategy

The gethistory methods will be added in time, but the other methods will be added, if possible, as needed and requested by the community.

# Class Definitions

## SessionStats
- long RunningTime 
- long Losses 
- long Wins 
- long Bets 
- long LossStreak 
- long WinStreak 
- decimal Profit 
- decimal PartialProfit
- decimal TotalProfit
- decimal Wagered 
- long WorstStreak 
- long WorstStreak3 
- long WorstStreak2 
- long BestStreak 
- long BestStreak3 
- long BestStreak2 
- DateTime StartTime 
- DateTime EndTime 
- long laststreaklose 
- long laststreakwin 
- decimal LargestBet 
- decimal LargestLoss 
- decimal LargestWin 
- decimal luck 
- decimal AvgWin         
- decimal AvgLoss 
- decimal AvgStreak 
- decimal CurrentProfit 
- decimal StreakProfitSinceLastReset 
- decimal StreakLossSinceLastReset 
- decimal ProfitSinceLastReset 
- long winsAtLastReset 
- long NumLossStreaks 
- long NumWinStreaks 
- long NumStreaks 
- decimal PorfitSinceLimitAction 

## SiteDetails
- string name 
- decimal edge 
- decimal maxroll 
- bool cantip 
- bool tipusingname 
- bool canwithdraw 
- bool canresetseed 
- bool caninvest 
- string siteurl 
- long Wins 
- long Losses 
- decimal Profit 
- decimal Wagered 
- decimal Balance 
- long Bets 
- Dictionary<string, IGameConfig> GameSettings

## DiceConfig: IGameConfig
- Decimal Edge
- Decimal MaxRoll

## CrashConfig: IGameConfig
- Decimal Edge
- bool IsMultiplayer

## LimboConfig: IGameConfig
- Decimal Edge
- Decimal MinChance

## TwistConfig: IGameConfig
- Decimal Edge
- Decimal maxrol




## PlaceBet - NextBet
- decimal Amount 
- bool High 
- decimal Chance 

## Bet - PreviousBet
- public Games Game 
- public decimal TotalAmount 
- public decimal Date 
- public DateTime DateValue 
- public string BetID 
- public decimal Profit 
- public long Userid 
- public string Currency 
- public string Guid 
- public decimal Edge 
- public bool IsWin
- public string Site 

## DiceBet : Bet
- public decimal Roll
- public bool High
- public decimal Chance
- public long Nonce 
- public string? ServerHash
- public string? ServerSeed
- public string? ClientSeed
- public int WinnableType 

## LimboBet: Bet
- public decimal Payout
- public decimal Result 
- public long Nonce 
- public string? ServerHash
- public string? ServerSeed
- public string? ClientSeed
- public int WinnableType

## CrashBet:Bet
- public decimal Payout
- public decimal Crash
- public long Nonce 
- public string? ServerHash
- public string? ServerSeed
- public decimal Amount

## TwistBet:Bet
- public decimal Roll
- public bool High
- public decimal Chance
- public long Nonce 
- public string? ServerHash
- public string? ServerSeed
- public string? ClientSeed
- public int WinnableType

