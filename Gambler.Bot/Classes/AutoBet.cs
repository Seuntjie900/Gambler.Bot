using Gambler.Bot.Common.Enums;
using Gambler.Bot.Common.Events;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.Dice;
using Gambler.Bot.Common.Helpers;
using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;
using Gambler.Bot.Core.Sites;
using Gambler.Bot.Core.Sites.Classes;
using Gambler.Bot.Core.Storage;
using Gambler.Bot.Helpers;
using Gambler.Bot.Strategies.Helpers;
using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Scripting.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gambler.Bot.Views;
using static Gambler.Bot.Classes.PersonalSettings;
using ErrorEventArgs = Gambler.Bot.Common.Events.ErrorEventArgs;

namespace Gambler.Bot.Classes
{
    public class AutoBet: INotifyPropertyChanged
    {
        #region Internal Variables
        private Task RunningThread;
        private readonly ILogger _Logger;
        List<Common.Events.ErrorEventArgs> ActiveErrors = new List<ErrorEventArgs>();
        //PwDatabase Passdb = new PwDatabase();
        System.Timers.Timer BetTimer = new System.Timers.Timer { Interval=1000, Enabled=false, AutoReset=true };
        

        public bool KeepassOpen
        {
            get { return false;// Passdb.IsOpen;
                               }            
        }


        public BotContext DBInterface { get; private set; }

        string LastBetGuid = "";
        Queue<string> LastBetsGuids = new Queue<string>();

        /// <summary>
        /// Indicates that the bot is currently running placing bets
        /// </summary>
        public bool Running { get; private set; }      

        public bool RunningSimulation { get; private set; }
        private long totalRuntime = new long();

        public long TotalRuntime
        {
            get { return totalRuntime ; }
            set { totalRuntime = value; }
        }

        public bool LoggedIn { get; set; }
        public SessionStats Stats 
        { 
            get; 
            set; 
        }

        public ExportBetSettings StoredBetSettings { get; set; } = new ExportBetSettings { BetSettings = new InternalBetSettings() };

        public InternalBetSettings BetSettings 
        { 
            get => StoredBetSettings.BetSettings; 
            set => StoredBetSettings.BetSettings=value; 
        }

        public PersonalSettings PersonalSettings { get; set; } = new PersonalSettings();
        Bet MostRecentBet = null;
        DateTime MostRecentBetTime = new DateTime();
        PlaceBet NextBext = null;
        int Retries = 0;
        bool Retrying = false;
        public bool StopOnWin { get; set; } = false;
        //internal variables
        #endregion

        string VersionStr = "";


        public AutoBet(ILogger logger)
        {
            _Logger = logger;
            _Logger.LogDebug("AutoBet instance creating");
            VersionStr = App.GetVersion();
            Stats = new SessionStats();
            Running = false;
            BetTimer.Elapsed += BetTimer_Elapsed;
            CurrentGame = Games.Dice;
            _Logger.LogDebug("AutoBet instance created");
        }

        

        public static List<SitesList> Sites = new List<SitesList>();
        public SitesList[] CompileSites()
        {
            
            if (Sites?.Count == 0)
            {
                _Logger?.LogInformation("Compiling Sites");
                List<string> Files = new List<string>();

                Sites = new List<SitesList>();
                if (Directory.Exists("Sites"))
                {
                    _Logger?.LogDebug("Sites dir found, searching for files");
                    string Sites = "";
                    foreach (string s in Directory.GetFiles("Sites"))
                    {
                        _Logger?.LogDebug("Sites dir found, searching for files. Found {Description}", s);
                        string outs = File.ReadAllText(s);
                        Sites += outs;
                        Files.Add(outs);
                    }

                    //Compile site fies

                }
                //else
                {
                    _Logger?.LogDebug("Site dir not found, Stepping Through Types");
                    Assembly SiteAss = Assembly.GetAssembly(typeof(BaseSite));
                    Type[] tps = SiteAss.GetTypes();

                    List<string> sites = new List<string>();
                    foreach (Type x in tps)
                    {
                        _Logger?.LogDebug("Stepping Through Types - {Description}", x.Name);
                        if (x.IsSubclassOf(typeof(Gambler.Bot.Core.Sites.BaseSite)))
                        {
                            _Logger?.LogDebug("Found Type - " + x.Name, 6);
                            //sites.Add(x.Name);
                            string[] currenices = new string[] { "btc" };
                            string url = "";
                            Games[] games = new Games[] { Games.Dice };
                            try
                            {
                                _Logger?.LogDebug("Fetching currencies for - {Description}",x.Name);
                                BaseSite SiteInst = Activator.CreateInstance(x, _Logger) as BaseSite;
                                if (!SiteInst.IsEnabled)
                                    continue;
                                sites.Add(SiteInst.SiteName);
                                currenices = (SiteInst).Currencies;
                                games = (Activator.CreateInstance(x, _Logger) as BaseSite).SupportedGames;
                                url = SiteInst.SiteURL;
                                Sites.Add(new SitesList { Name = SiteInst.SiteName, Currencies = currenices, SupportedGames = games, URL = url }.SetType(x));

                                if (DBInterface != null)
                                {
                                    DBInterface.Sites.Add(SiteInst.SiteDetails);

                                }
                            }
                            catch (Exception e)
                            {
                                _Logger?.LogError(e.ToString());
                            }
                            
                            
                        }
                    }
                }
                _Logger?.LogInformation("Populated Sites");

                if (DBInterface != null)
                {
                    try
                    {
                        _Logger?.LogInformation("Updating Sites Table");
                        DBInterface.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _Logger?.LogError(ex.ToString());
                    }
                }
                else
                {
                    _Logger?.LogInformation("Not Updating Sites Table");
                }
            }
            return Sites.ToArray();  
        }
        Dictionary<string, Type> strategies;
        public Dictionary<string, Type> Strategies { get => strategies; set { strategies = value; RaisePropertyChanged(); } }
        public Dictionary<string, Type> GetStrats()
        {
            var tmpStrategies = new Dictionary<string, Type>();
            Type[] tps = Assembly.GetAssembly(typeof(BaseStrategy)).GetTypes();
            List<string> sites = new List<string>();

            Type BaseTyope = typeof(Gambler.Bot.Strategies.Strategies.Abstractions.BaseStrategy);
            foreach (Type x in tps)
            { 
                if (x.IsSubclassOf(BaseTyope))
                {
                    tmpStrategies.Add((Activator.CreateInstance(x,_Logger) as BaseStrategy).StrategyName, x);
                }
            }
            Strategies = tmpStrategies;
            return Strategies;
        }

        #region Site Stuff
        private BaseSite baseSite;

        protected BaseSite CurrentSite
        {
            get { return baseSite; }
            set
            {
                if (baseSite!=null)
                {
                    baseSite.Action -= BaseSite_Action;
                    baseSite.ChatReceived -= BaseSite_ChatReceived;
                    baseSite.Error -= BaseSite_Error;
                    baseSite.LoginFinished -= BaseSite_LoginFinished;
                    baseSite.Notify -= BaseSite_Notify;
                    baseSite.RegisterFinished -= BaseSite_RegisterFinished;
                    baseSite.StatsUpdated -= BaseSite_StatsUpdated;
                    baseSite.OnBrowserBypassRequired = null;
                    baseSite.OnCFCaptchaBypass = null;
                    baseSite.ExecJS = null;
                    baseSite.Disconnect();                    
                }
                baseSite = value;
                if (baseSite != null)
                {
                    baseSite.Action += BaseSite_Action;
                    baseSite.ChatReceived += BaseSite_ChatReceived;
                    baseSite.Error += BaseSite_Error;
                    baseSite.LoginFinished += BaseSite_LoginFinished;
                    baseSite.Notify += BaseSite_Notify;
                    baseSite.RegisterFinished += BaseSite_RegisterFinished;
                    baseSite.StatsUpdated += BaseSite_StatsUpdated;
                    baseSite.ExecJS = BaseSite_ExecJS;
                    baseSite.OnBrowserBypassRequired = BaseSite_OnBrowserBypassRequired;
                    baseSite.OnCFCaptchaBypass = OnCFCaptchaBypass;
                    int tmpcurrency = baseSite.Currencies.FindIndex(x=>x.ToLower() == CurrentCurrency.ToLower());
                    if (tmpcurrency < 0)
                    {
                        CurrentCurrency = baseSite.Currencies.First();
                    }
                    else
                    {
                        CurrentCurrency = baseSite.Currencies[tmpcurrency];
                    }
                    if (!new List<Games>(baseSite.SupportedGames).Contains(CurrentGame))
                    {
                        CurrentGame = baseSite.SupportedGames[0];
                    }

                }
                if (Strategy is IProgrammerMode prog)
                {
                    prog.UpdateSite(baseSite.SiteDetails, baseSite.CurrentCurrency);//CopyHelper.CreateCopy<SiteDetails>(baseSite.SiteDetails));
                }
                this.RaisePropertyChanged(nameof(SupportsBrowserLogin));
                this.RaisePropertyChanged(nameof(SupportsNormalLogin));
            }
        }

        
        private async Task BaseSite_OnCFCaptchaBypass( string e)
        {
            await OnCFCaptchaBypass?.Invoke(e);
        }
        private async Task<BrowserConfig> BaseSite_OnBrowserBypassRequired(BypassRequiredArgs e)
        {
            return await onBrowserBypassRequired?.Invoke(e);
        }
        private async Task<string> BaseSite_ExecJS(string e)
        {
            return await ExecJS?.Invoke(e);
        }

        private Games currentGame;

        public Games CurrentGame
        {
            get { return currentGame; }
            set { currentGame = value; OnGameChanged?.Invoke(this, new EventArgs()); this.RaisePropertyChanged(); }
        }


        public event EventHandler<StatsUpdatedEventArgs > OnSiteStatsUpdated;
        public event EventHandler<GenericEventArgs> OnSiteAction;
        public event EventHandler<GenericEventArgs> OnSiteChat;
        public event EventHandler<BetFinisedEventArgs> OnSiteBetFinished;
        public event EventHandler<ErrorEventArgs> OnSiteError;
        public event EventHandler<LoginFinishedEventArgs> OnSiteLoginFinished;
        public event EventHandler<GenericEventArgs> OnSiteNotify;
        public event EventHandler<GenericEventArgs> OnSiteRegisterFinished;
        public event EventHandler OnGameChanged;
        public event EventHandler OnStrategyChanged;
        public event EventHandler<NotificationEventArgs> OnNotification;
        public event EventHandler<GetConstringPWEventArgs> NeedConstringPassword;
        public event EventHandler<GetConstringPWEventArgs> NeedKeepassPassword;
        public event EventHandler OnStarted;
        public event EventHandler<GenericEventArgs> OnStopped;
        private Func<BypassRequiredArgs, Task<BrowserConfig>> onBrowserBypassRequired;
        public Func<BypassRequiredArgs, Task<BrowserConfig>> OnBrowserBypassRequired 
        { 
            get=>onBrowserBypassRequired;
            set 
            { 
                onBrowserBypassRequired = value;
                if (this.CurrentSite != null)
                {
                    this.CurrentSite.OnBrowserBypassRequired = onBrowserBypassRequired;
                }
            }
        }
        public  Func<string,Task> OnCFCaptchaBypass;
        private Func<string, Task<string?>> ExecJS;
        public event PropertyChangedEventHandler PropertyChanged;

        private void BaseSite_StatsUpdated(object sender, StatsUpdatedEventArgs e)
        {
            OnSiteStatsUpdated?.Invoke(sender, e);
            if (Strategy is IProgrammerMode)
            {
                (Strategy as IProgrammerMode).UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(e.NewStats));
            }
        }

        private void BaseSite_RegisterFinished(object sender, GenericEventArgs e)
        {
            OnSiteRegisterFinished?.Invoke(sender, e);
        }

        private void BaseSite_Notify(object sender, GenericEventArgs e)
        {
            OnSiteNotify?.Invoke(sender, e);
        }

        private void BaseSite_LoginFinished(object sender, LoginFinishedEventArgs e)
        {
            LoggedIn = e.Success;
            OnSiteLoginFinished?.Invoke(sender, e);
        }
        List<ErrorType> BettingErrorTypes = new List<ErrorType>(new ErrorType[] { ErrorType.BalanceTooLow, ErrorType.BetMismatch, ErrorType.InvalidBet, ErrorType.NotImplemented, ErrorType.Other, ErrorType.Unknown });
        List<ErrorType> NonBettingErrorTypes = new List<ErrorType>(new ErrorType[] { ErrorType.Withdrawal, ErrorType.Tip, ErrorType.ResetSeed });
        void RetryDelay()
        {
            double delay = (double)PersonalSettings.RetryDelay - (DateTime.Now - MostRecentBetTime).TotalSeconds;
            if (delay < 0)
                delay = 1;
            Thread.Sleep((int)(delay * 1000));
        }
        private void BaseSite_Error(object sender, ErrorEventArgs ea)
        {
            BotErrorEventArgs be = new BotErrorEventArgs(ea);
            ActiveErrors.Add(be);
            if (Running && Strategy != null)
            {
                Strategy.OnError(be);
                if (be.Handled)
                {
                    switch (be.Action)
                    {
                        /*ResumeAsWin, ResumeAsLoss, Resume, Stop, Reset, Retry*/
                        case ErrorActions.Stop: 
                            StopStrategy(be.Type.ToString() + " error occurred - Set to stop.");
                            break;
                        case ErrorActions.Reset:
                            NextBext = (Strategy.RunReset(CurrentGame));
                            CalculateNextBet();
                            break;
                        case ErrorActions.Resume:
                            //what and how?
                            break;
                        case ErrorActions.Retry:
                            if (Running )
                            {
                                /*if (Retries++<= PersonalSettings.RetryAttempts || PersonalSettings.RetryAttempts<=0)
                                    RetryDelay();
                                PlaceBet(NextBext, true);*/
                            }
                            break;
                        case ErrorActions.ResumeAsWin:
                            CalculateNextBet();//what and how?
                            break;
                        case ErrorActions.ResumeAsLoss:
                            CalculateNextBet();//what and how?
                            break;
                            

                    }
                }
            }
            if (!be.Handled)
            {
                ErrorSetting tmpSetting = PersonalSettings.GetErrorSetting(be.Type);
                if (tmpSetting!=null)
                {
                    if (tmpSetting.Action == ErrorActions.Stop)
                        StopStrategy(tmpSetting.Type.ToString() + " error occurred - Set to stop.");
                    
                    
                    if (BettingErrorTypes.Contains(tmpSetting.Type))
                    {
                        if (ErrorActions.Reset == tmpSetting.Action)
                        {
                            if (Running)
                            {
                                //if (Retries <= PersonalSettings.RetryAttempts)
                                {
                                    NextBext = (Strategy.RunReset(CurrentGame));
                                    //Thread.Sleep(PersonalSettings.RetryDelay);
                                    //Retries++;
                                    CalculateNextBet();
                                }
                            }
                        }
                        else if (tmpSetting.Action == ErrorActions.Resume)
                        {

                        }
                        else if (ErrorActions.Retry == tmpSetting.Action)
                        {
                            /*if (Retries <= PersonalSettings.RetryAttempts || PersonalSettings.RetryAttempts<=0)
                            {
                                RetryDelay();
                                Retries++;
                                if (Running )
                                {
                                    PlaceBet(NextBext, true);
                                }
                            }*/
                        }
                    }
                    else
                    {
                        switch (tmpSetting.Action)
                        {
                            case ErrorActions.Reset:
                                if (Running)
                                {
                                    //if (Retries <= PersonalSettings.RetryAttempts || PersonalSettings.RetryAttempts <= 0)
                                    {
                                        NextBext = (Strategy.RunReset(CurrentGame));
                                        //RetryDelay();
                                        //Retries++;
                                        if (Running)
                                        {
                                            PlaceBet(NextBext, true);
                                        }
                                    }
                                }
                                break;
                            case ErrorActions.Resume:break;
                            case ErrorActions.Retry:
                               /* if (Retries <= PersonalSettings.RetryAttempts || PersonalSettings.RetryAttempts <= 0)
                                {
                                    RetryDelay();
                                    
                                    Retries++;
                                    //CalculateNextBet();
                                    PlaceBet(NextBext, true);
                                }*/ break;
                        }
                    }
                }
            }
            OnSiteError?.Invoke(sender, be);
            try
            {
                ActiveErrors.Remove(be);
            }
            catch
            {

            }
        }

        PlaceBet CalculateNextBet()
        {
            if (CurrentSite.ActiveActions.Count > 0 || ActiveErrors.Count > 0)
                return null;
            if (Strategy is IProgrammerMode)
            {
                (Strategy as IProgrammerMode).UpdateSessionStats(CopyHelper.CreateCopy<SessionStats>(Stats));
                (Strategy as IProgrammerMode).UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(CurrentSite.Stats));
                (Strategy as IProgrammerMode).UpdateSite((CurrentSite.SiteDetails), baseSite.CurrentCurrency);
            }
            bool win = MostRecentBet.IsWin;
            if (StopOnWin && win)
            {
                StopStrategy("Stop On Win enabled - Bet won");
                return null;
            }
            if (NextBext ==null)
                NextBext = Strategy.CalculateNextBet(MostRecentBet, win);
            if (NextBext.Game != CurrentGame)
                CurrentGame = NextBext.Game;
            if (BetSettings.EnableMaxBet && NextBext.Amount > BetSettings.MaxBet)
            {
                NextBext.Amount = BetSettings.MaxBet;
            }

            if (BetSettings.EnableMinBet && NextBext.Amount < BetSettings.MinBet)
            {
                NextBext.Amount = BetSettings.MinBet;
            }
            if (Running)
            {
                decimal secondsPerBet = 0;
                if (BetSettings.EnableBotSpeed && BetSettings.BotSpeed>0)
                    secondsPerBet = 1m / BetSettings.BotSpeed;
                decimal msPerBet = secondsPerBet * 1000m;
                decimal timetoBet = CurrentSite.TimeToBet(NextBext);
                decimal TimeSinceLastBet = (decimal)(DateTime.Now - MostRecentBetTime).TotalMilliseconds;
                decimal maxDelay = System.Math.Max(timetoBet,
                    NextBext?.BetDelay ?? 0 -TimeSinceLastBet);
                if (BetSettings.EnableBotSpeed)
                {
                    maxDelay = System.Math.Max(maxDelay,msPerBet-TimeSinceLastBet);
                }
                maxDelay = System.Math.Max(maxDelay,strategy.GetBetDelay()-TimeSinceLastBet);
                if (maxDelay > 0 )
                {
                    
                    
                    Thread.Sleep((int)maxDelay);
                    maxDelay = 0;
                }
                return NextBext;
            }
            return null;
        }

        private void BaseSite_ChatReceived(object sender, GenericEventArgs e)
        {
            OnSiteChat?.Invoke(sender, e);
        }

        private void BaseSite_Action(object sender, GenericEventArgs e)
        {
            OnSiteAction?.Invoke(sender, e);
        }

        public async Task<bool> Login(string url, LoginParamValue[] LoginParams)
        {
            if (CurrentSite==null)
            {
                throw new Exception("Cannot login without a site. Assign a value to CurrentSite, then log in.");
            }
            LoggedIn = await CurrentSite.LogIn(url,LoginParams);
            return LoggedIn;
        }
        public async Task<bool> BrowserLogin(string url)
        {
            if (CurrentSite == null)
            {
                throw new Exception("Cannot login without a site. Assign a value to CurrentSite, then log in.");
            }
            LoggedIn = await CurrentSite.BrowserLogin(url);
            return LoggedIn;
        }


        //Site Stuff
        #endregion

        private BaseStrategy strategy;

        public BaseStrategy Strategy
        {
            get { return strategy; }
            set
            {
                if (strategy != null)
                {
                    strategy.NeedBalance -= Strategy_NeedBalance;
                    strategy.Stop -= Strategy_Stop;
                    strategy.OnNeedStats -= Strategy_OnNeedStats;
                    if (strategy is IProgrammerMode progs)
                    {
                        progs.OnInvest -= Autobet_OnInvest;
                        progs.OnResetSeed -= Autobet_OnResetSeed;
                        progs.OnResetStats -= Autobet_OnResetStats;
                        progs.OnTip -= Autobet_OnTip;
                        progs.OnWithdraw -= Autobet_OnWithdraw;
                        progs.OnScriptError -= Autobet_OnScriptError;
                        progs.OnSetCurrency -= Autobet_OnSetCurrency;
                        progs.OnBank -= Prog_OnBank;
                        progs.OnResetProfit -= Prog_OnResetProfit;
                        progs.OnResetPartialProfit -= Prog_OnResetPartialProfit;
                        progs.OnSetBotSpeed -= ProgsOnOnSetBotSpeed;
                    }
                }
                strategy = value;
                if (strategy != null)
                {
                    strategy.NeedBalance += Strategy_NeedBalance;
                    strategy.Stop += Strategy_Stop;
                    strategy.OnNeedStats += Strategy_OnNeedStats;
                    if (strategy is IProgrammerMode prog)
                    {
                        prog.CreateRuntime();
                        if (CurrentSite != null)
                        {
                            prog.UpdateSite(CurrentSite.SiteDetails, baseSite.CurrentCurrency);
                            prog.UpdateSessionStats(CopyHelper.CreateCopy<SessionStats>(Stats));
                            prog.UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(CurrentSite.Stats));
                        }
                        prog.OnInvest += Autobet_OnInvest;
                        prog.OnResetSeed += Autobet_OnResetSeed;
                        prog.OnResetStats += Autobet_OnResetStats;
                        prog.OnResetProfit += Prog_OnResetProfit;
                        prog.OnResetPartialProfit += Prog_OnResetPartialProfit;

                        prog.OnTip += Autobet_OnTip;
                        prog.OnWithdraw += Autobet_OnWithdraw;
                        prog.OnScriptError += Autobet_OnScriptError;
                        prog.OnSetCurrency += Autobet_OnSetCurrency;
                        prog.OnBank += Prog_OnBank;
                        prog.OnSetBotSpeed += ProgsOnOnSetBotSpeed;
                    }
                }
                StoredBetSettings.SetStrategy(value);
                OnStrategyChanged?.Invoke(this, new EventArgs());
                
            }
        }

        private void ProgsOnOnSetBotSpeed(object? sender, SetBotSpeedEventArgs e)
        {
            this.BetSettings.EnableBotSpeed = e.Enabled;
            this.BetSettings.BotSpeed = e.BetsPerSecond;
        }

        private void Prog_OnResetPartialProfit(object? sender, EventArgs e)
        {
            
            Stats.PartialProfit = 0;
            
        }

        private void Prog_OnResetProfit(object? sender, EventArgs e)
        {
            Stats.Profit = 0;
            Stats.PartialProfit = 0;
            Stats.ProfitSinceLastReset = 0;
            Stats.StreakProfitSinceLastReset = 0;
            Stats.StreakLossSinceLastReset = 0;
            this.RaisePropertyChanged(nameof(Stats));
        }

        public string SiteName { get => CurrentSite?.SiteName; }
        public SiteStats SiteStats { get => CurrentSite?.Stats; }
        public string[] Currencies { get => CurrentSite?.Currencies; }
        public string CurrentCurrency 
        { 
            get => CurrentSite?.CurrentCurrency;  
            set 
            { 
                if (CurrentSite != null)
                    CurrentSite.CurrentCurrency = value;
                this.RaisePropertyChanged(nameof(CurrentCurrency));
            } 
        }

       
        public Games[] SiteGames { get => CurrentSite?.SupportedGames; }
        public bool SupportsBrowserLogin { get => CurrentSite?.SupportsBrowserLogin??false; }
        public bool SupportsNormalLogin { get => CurrentSite?.SupportsNormalLogin ?? true; }
        private void Autobet_OnSetCurrency(object sender, PrintEventArgs e)
        {
            if (CurrentSite != null)
            {
                if (Array.IndexOf(this.CurrentSite.Currencies, e.Message) > 0)//should I do this check?
                {
                    CurrentCurrency = e.Message;
                    
                }
            }
        }

        private void Autobet_OnScriptError(object sender, PrintEventArgs e)
        {
            StopStrategy("Error received from programmer mode, check console for more details.");
        }

        private void Autobet_OnWithdraw(object sender, WithdrawEventArgs e)
        {
            if (CurrentSite.AutoWithdraw)
                CurrentSite.Withdraw(e.Address, e.Amount);
        }

        private void Autobet_OnTip(object sender, TipEventArgs e)
        {
            if (CurrentSite.CanTip)
                CurrentSite.SendTip(e.Receiver, e.Amount);
        }

        private void Autobet_OnStop(object sender, EventArgs e)
        {
            StopStrategy("Programmer mode stop signal received.");
        }

        private void Autobet_OnResetStats(object sender, EventArgs e)
        {
            ResetStats();
        }
        

        private void Autobet_OnResetSeed(object sender, EventArgs e)
        {
            if (CurrentSite.CanChangeSeed)
                CurrentSite.ResetSeed(CurrentSite.GenerateNewClientSeed().ToString());
        }

        private void Autobet_OnResetBuiltIn(object sender, EventArgs e)
        {
            Strategy.RunReset(CurrentGame);
        }
        private void Prog_OnBank(object? sender, InvestEventArgs e)
        {
            if (CurrentSite.AutoBank)
                CurrentSite.Bank(e.Amount);
        }
        private void Autobet_OnInvest(object sender, InvestEventArgs e)
        {
            if (CurrentSite.AutoInvest)
                CurrentSite.Invest(e.Amount);
        }

        private SessionStats Strategy_OnNeedStats(object sender, EventArgs e)
        {
            return CopyHelper.CreateCopy<SessionStats>(Stats);
        }

        private void Strategy_Stop(object sender, StopEventArgs e)
        {
            StopStrategy(e.Reason);
        }

        private decimal Strategy_NeedBalance()
        {
            if (CurrentSite == null)
                return 0;
            else if (CurrentSite.Stats == null)
                return 0;
            else
                return CurrentSite.Stats.Balance;
        }
       
        public void Start()
        {
            StopOnWin = false;
            if (Running)
                throw new Exception("Cannot start bot while it's running");
            if (RunningSimulation)
                throw new Exception("Cannot start bot while it's running a simulation");

            CurrentSite.ActiveActions.Clear();
            ActiveErrors.Clear();

            if (!Running && !RunningSimulation)
            {
                Task.Run(() =>
                {
                    if (Strategy is IProgrammerMode prog)
                    {
                        prog.SetSimulation(false);

                        prog.UpdateSessionStats(CopyHelper.CreateCopy<SessionStats>(Stats));
                        prog.UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(CurrentSite.Stats));
                        prog.UpdateSite(CurrentSite.SiteDetails, baseSite.CurrentCurrency);
                        prog.LoadScript();

                    }
                    Running = true;
                    if (Stats == null)
                        Stats = new SessionStats();
                    Stats.StartTime = DateTime.Now;
                    //Indicate to the selected strategy to create a working set and start betting.
                    OnStarted?.Invoke(this, new EventArgs());
                    MostRecentBetTime = DateTime.Now;
                    BetTimer.Enabled = true;
                    RunningThread = RunBot();
                });
                
            }
            /*
             * if not running
             * and not running simulator
             * get initial bet values (chance, high, amount)
             * mark bot as running
             * reset run variables
             * continue session variables
             * run dicebet thread to place initial bet
             */
        }
        
        private async Task RunBot(bool resume = false)
        {
            await Task.Run(async () =>
            {
                if (!resume)
                    await PlaceBet(Strategy.Start(CurrentGame));
                if (MostRecentBet==null)
                {
                    StopStrategy("Initial bet failed.");
                    return;
                }
                while (Running)
                {
                    
                    while (MostRecentBet == null && Running)
                    {
                        await Task.Delay(10);
                    }
                    if (!Running)
                    {
                        return;
                    }
                    try
                    {
                        bool win = MostRecentBet.IsWin;
                        string Response = "";
                        bool Reset = false;
                        NextBext = null;
                        if (BetSettings?.CheckResetPreStats(MostRecentBet, win, Stats, CurrentSite.Stats) ?? false)
                        {
                            Reset = true;
                            NextBext = Strategy.RunReset(CurrentGame);
                        }
                        if (BetSettings?.CheckStopPreStats(MostRecentBet, win, Stats, out Response, CurrentSite.Stats) ?? false)
                        {
                            StopStrategy(Response);
                        }
                        Stats.UpdateStats(MostRecentBet, win);
                        if (Strategy is IProgrammerMode prog)
                        {
                            prog.UpdateSessionStats(CopyHelper.CreateCopy<SessionStats>(Stats));
                            prog.UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(CurrentSite.Stats));
                            prog.UpdateSite((CurrentSite.SiteDetails), baseSite.CurrentCurrency);
                        }
                        


                        if (MostRecentBet.Guid != LastBetGuid || LastBetsGuids.Contains(MostRecentBet.Guid))
                        {
                            StopStrategy("Last bet did not match the latest bet placed.");
                            //stop
                            return;
                        }
                        else
                        {
                            LastBetsGuids.Enqueue(MostRecentBet.Guid);
                            if (LastBetsGuids.Count > 10)
                                LastBetsGuids.Dequeue();
                        }

                        foreach (Trigger x in PersonalSettings.Notifications)
                        {
                            if (x.Enabled)
                            {
                                if (x.CheckNotification(Stats, CurrentSite.Stats))
                                {
                                    switch (x.Action)
                                    {
                                        case TriggerAction.Alarm:
                                        case TriggerAction.Chime:
                                        case TriggerAction.Popup: OnNotification?.Invoke(this, new NotificationEventArgs { NotificationTrigger = x }); break;
                                        case TriggerAction.Email: throw new NotImplementedException("Supporting infrastructure for this still needs to be built.");
                                    }
                                }
                            }
                        }
                        

                        foreach (Trigger x in BetSettings.Triggers)
                        {
                            if (x.Enabled)
                            {
                                if (x.CheckNotification(Stats, CurrentSite.Stats))
                                {
                                    switch (x.Action)
                                    {

                                        case TriggerAction.Bank: await CurrentSite.Bank(x.GetValue(Stats, CurrentSite.Stats)); break;
                                        case TriggerAction.Invest: await CurrentSite.Invest(x.GetValue(Stats, CurrentSite.Stats)); break;
                                        case TriggerAction.Reset: NextBext = Strategy.RunReset(CurrentGame); Reset = true; break;
                                        case TriggerAction.ResetSeed: await CurrentSite.ResetSeed(CurrentSite.GenerateNewClientSeed()); break;
                                        case TriggerAction.Stop: StopStrategy($"Stop trigger fired: {x.ToString()}"); break;
                                        //case TriggerAction.Switch: Strategy.High = !Strategy.High; if (NewBetObject != null)NewBetObject.High = !NewBet.High;  break;
                                        case TriggerAction.Tip: await CurrentSite.SendTip(x.Destination, x.GetValue(Stats, CurrentSite.Stats)); break;
                                        case TriggerAction.Withdraw: await CurrentSite.Withdraw(x.Destination, x.GetValue(Stats, CurrentSite.Stats)); break;

                                    }
                                }
                            }
                        }
                        if (BetSettings.CheckResetPostStats(MostRecentBet, win, Stats, CurrentSite.Stats))
                        {
                            Reset = true;
                            NextBext = Strategy.RunReset(CurrentGame);
                        }
                        if (BetSettings.CheckStopPOstStats(MostRecentBet, win, Stats, out Response, CurrentSite.Stats))
                        {
                            StopStrategy(Response);
                        }
                        decimal withdrawamount = 0;
                        string address = null;
                        if (BetSettings.CheckWithdraw(MostRecentBet, win, Stats, out withdrawamount, CurrentSite.Stats, out address))
                        {
                            await CurrentSite.Withdraw(address, withdrawamount);
                        }
                        if (BetSettings.CheckBank(MostRecentBet, win, Stats, out withdrawamount, CurrentSite.Stats))
                        {
                            await CurrentSite.Bank(withdrawamount);
                            //this.Balance -= withdrawamount;
                        }
                        if (BetSettings.CheckTips(MostRecentBet, win, Stats, out withdrawamount, CurrentSite.Stats, out address))
                        {
                            await CurrentSite.SendTip(address, withdrawamount);
                            //this.Balance -= withdrawamount;
                        }
                        bool NewHigh = false;
                        if (BetSettings.CheckResetSeed(MostRecentBet, win, Stats, CurrentSite.Stats))
                        {
                            if (CurrentSite.CanChangeSeed)
                                await CurrentSite.ResetSeed("");
                        }
                        if (BetSettings.CheckHighLow(MostRecentBet, win, Stats, out NewHigh, SiteStats))
                        {
                            (strategy as iDiceStrategy).High = NewHigh;
                        }
                        if (Running)
                        {
                            if (!Reset)
                             NextBext = CalculateNextBet();
                            if (NextBext == null)
                            {
                                StopStrategy("Strategy failed to generate next bet.");
                                return;
                            }
                            
                            await PlaceBet(NextBext);
                        }
                    }
                    catch (Exception E)
                    {
                        _Logger?.LogError(E.ToString());
                    }
                }
            });
        }

        public void Resume()
        {
            StopOnWin = false;
            if (Running)
                throw new Exception("Cannot start bot while it's running");
            if (RunningSimulation)
                throw new Exception("Cannot start bot while it's running a simulation");

            CurrentSite.ActiveActions.Clear();
            ActiveErrors.Clear();

            if (!Running && !RunningSimulation)
            {
                if (Strategy is IProgrammerMode)
                {
                    (Strategy as IProgrammerMode).LoadScript();
                    (Strategy as IProgrammerMode).UpdateSessionStats(CopyHelper.CreateCopy<SessionStats>(Stats));
                    (Strategy as IProgrammerMode).UpdateSiteStats(CopyHelper.CreateCopy<SiteStats>(CurrentSite.Stats));
                    (Strategy as IProgrammerMode).UpdateSite((CurrentSite.SiteDetails), baseSite.CurrentCurrency);
                }
                Running = true;
                Stats.StartTime = DateTime.Now;
                //Indicate to the selected strategy to create a working set and start betting.
                MostRecentBetTime = DateTime.Now;
                BetTimer.Enabled = true;
                OnStarted?.Invoke(this, new EventArgs());
                //CalculateNextBet();
                RunningThread = RunBot();
            }
        }
        
        private void BetTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!Running)
            {
                return;
            }
            if ((DateTime.Now-MostRecentBetTime).TotalSeconds> PersonalSettings.RetryDelay && (Retries< PersonalSettings.RetryAttempts || PersonalSettings.RetryAttempts<0))
            {
                if (NextBext != null && ((DateTime.Now - MostRecentBetTime).Milliseconds > NextBext.BetDelay))
                {
                    Retries++;
                    MostRecentBetTime = DateTime.Now;                    
                    PlaceBet(NextBext, true);
                }
            }
        }

        public void StopStrategy(string Reason)
        {
            
            bool wasrunning = Running;
            Running = false;
            if (Stats != null)
            {
                Stats.EndTime = DateTime.Now;
                Stats.RunningTime += (long)(Stats.EndTime - Stats.StartTime).TotalMilliseconds;
                if (wasrunning && DBInterface != null)
                {
                    if (Stats.SessionStatsId<=0)
                        DBInterface.Add(Stats);
                    try
                    {
                        DBInterface?.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        _Logger.LogError(e.ToString());
                    }
                }
            }
            
            //TotalRuntime +=Stats.EndTime - Stats.StartTime;
            _Logger?.LogInformation(Reason);
            OnStopped?.Invoke(this, new GenericEventArgs { Message = Reason });
        }

        public void ResetStats()
        {
            if (Running)
            {
                Stats.EndTime = DateTime.Now;
                Stats.RunningTime += (long)(Stats.EndTime - Stats.EndTime).TotalMilliseconds;
            }
            TotalRuntime += Stats.RunningTime;
            if (this.DBInterface != null)
            {
               this.DBInterface.Add(Stats);
                try
                {
                    this.DBInterface.SaveChanges();
                }
                catch (Exception ex)
                {
                    _Logger?.LogError(ex.ToString());
                }
            }

            Stats = new SessionStats();
        }

        public async Task<Bet> PlaceBet(PlaceBet Bet, bool isretry= false)
        {
            if (Retrying)
                return null;
            Retrying = Retrying|| isretry;
            if (Bet == null)
            {
                //notify of an error?
                throw new Exception("Bet cannot be null");
            }
                
            Bet.GUID = Guid.NewGuid().ToString();
            LastBetGuid = Bet.GUID;
            var NewBet =  await CurrentSite.PlaceBet(Bet);
            try
            {
                if (NewBet == null)
                {
                    _Logger.LogWarning("Bet is null, nothing to save");
                }
                else
                {
                    if (DBInterface != null)
                    {
                        DBInterface?.Add(NewBet);
                        await DBInterface?.SaveChangesAsync();
                    }
                    else
                    {
                        _Logger.LogError("DBInterface not initialized");
                    }
                }
            }
            catch (Exception ex)
            {
                //alert alert alert! bet not saved Not sure how yet.
                _Logger.LogError(ex.ToString());
            }
            MostRecentBet = NewBet;
            MostRecentBetTime = DateTime.Now;
            Retrying = false;
            if (NewBet!=null)
                Retries = 0;
            OnSiteBetFinished?.Invoke(CurrentSite, new BetFinisedEventArgs(NewBet));
            
            return NewBet;
        }

        void DiceBetThread(object Bet)
        {
            PlaceDiceBet tmpBet = Bet as PlaceDiceBet;
            CurrentSite.PlaceBet(tmpBet);
        }

        public class ExportBetSettings
        {
            public string Strategy { get; set; }
            public InternalBetSettings BetSettings { get; set; }
            public DAlembert dAlembert { get; set; }
            public Fibonacci Fibonacci { get; set; }
            public Labouchere Labouchere { get; set; }
            public PresetList PresetList { get; set; }
            public Martingale Martingale { get; set; }
            public ProgrammerCS ProgrammerCS { get; set; }
            public ProgrammerJS ProgrammerJS { get; set; }
            public ProgrammerLUA ProgrammerLUA { get; set; }
            public ProgrammerPython ProgrammerPython { get; set; }
            //public Strategies.Dice.Programmer Programmer { get; set; }
            public BaseStrategy GetStrat()
            {
                switch (Strategy)
                {
                    case "DAlembert": return dAlembert; 
                    case "PresetList": return PresetList; 
                    case "Labouchere": return Labouchere; 
                    case "Fibonacci": return Fibonacci; 
                    case "Martingale": return Martingale; 
                    case "ProgrammerCS": return ProgrammerCS; 
                    case "ProgrammerJS": return ProgrammerJS; 
                    case "ProgrammerLUA": return ProgrammerLUA; 
                    case "ProgrammerPython": return ProgrammerPython; 
                    default: return new Martingale(null); 
                }               
            }
            public void SetStrategy(BaseStrategy Strat)
            {
                /*dAlembert = null;
                Fibonacci = null;
                Labouchere = null;
                PresetList = null;
                Martingale = null;
                ProgrammerCS = null;
                ProgrammerJS = null;
                ProgrammerLUA = null;*/
                //ProgrammerPython = null;
                if (Strat is DAlembert)
                {
                    dAlembert = Strat as DAlembert;
                    Strategy = "DAlembert";
                }
                if (Strat is Fibonacci)
                {                    
                    Fibonacci = Strat as Fibonacci;
                    Strategy = "Fibonacci";
                }
                if (Strat is Labouchere)
                {                    
                    Labouchere = Strat as Labouchere;
                    Strategy = "Labouchere";
                }
                if (Strat is PresetList)
                {                   
                    PresetList = Strat as PresetList;
                    Strategy = "PresetList";
                }
                if (Strat is Martingale)                
                {                    
                    Martingale = Strat as Martingale;
                    Strategy = "Martingale";

                }
                if (Strat is ProgrammerCS)
                {
                    ProgrammerCS = Strat as ProgrammerCS;
                    Strategy = "ProgrammerCS";
                }
                if (Strat is ProgrammerJS)
                {
                    ProgrammerJS = Strat as ProgrammerJS;
                    Strategy = "ProgrammerJS";

                }
                if (Strat is ProgrammerLUA)
                {
                    ProgrammerLUA = Strat as ProgrammerLUA;

                    Strategy = "ProgrammerLUA";
                }
               if (Strat is ProgrammerPython)
                {
                    ProgrammerPython = Strat as ProgrammerPython;
                        Strategy = "ProgrammerPython";
                }
            }
        }

        public void SavePersonalSettings(string FileLocation)
        {
            if (Path.GetDirectoryName(FileLocation)!= string.Empty && !Directory.Exists(Path.GetDirectoryName(FileLocation)))
                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation));
            string Settings = JsonSerializer.Serialize(PersonalSettings);
            using (StreamWriter sw = new StreamWriter(FileLocation, false))
            {
                sw.Write(Settings);
            }
            //incl proxy settings without password - prompt password on startup.

        }
        
        public void SaveBetSettings(string FileLocation)
        {
            /*ExportBetSettings tmp = new ExportBetSettings()
            {
                BetSettings = this.BetSettings
            };
            tmp.SetStrategy(Strategy);*/
            StoredBetSettings.SetStrategy(strategy);
            string Settings = JsonSerializer.Serialize(this.StoredBetSettings);
            if (Path.GetDirectoryName(FileLocation)!= string.Empty && !Directory.Exists(Path.GetDirectoryName(FileLocation)))
                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation));
            using (StreamWriter sw = new StreamWriter(FileLocation, false)) 
            {
                sw.Write(Settings);
            }
            
        }

        public void LoadPersonalSettings(string FileLocation)
        {
            string Settings = "";
            //var files = System.IO.Directory.GetFiles(Path.GetDirectoryName(FileLocation));
            
            using (StreamReader sr = new StreamReader(FileLocation))
            {
                Settings = sr.ReadToEnd();
            }
            _Logger?.LogInformation("Loaded Personal Settings File");
            PersonalSettings tmp = JsonSerializer.Deserialize<PersonalSettings>(Settings);
            tmp.EnsureErrorSettings();
            _Logger?.LogInformation("Parsed Personal Settings File");
            this.PersonalSettings = tmp;
            string pw = "";

            if (tmp.EncryptConstring)
            {
                GetConstringPWEventArgs tmpArgs = new GetConstringPWEventArgs();
                NeedConstringPassword?.Invoke(this,tmpArgs);
                pw = tmpArgs.Password;
            }
            if (!string.IsNullOrWhiteSpace(tmp.KeepassDatabase))
            {
                try
                {
                    GetConstringPWEventArgs tmpArgs = new GetConstringPWEventArgs();
                    NeedKeepassPassword?.Invoke(this, tmpArgs);
                   /* var ioConnInfo = new IOConnectionInfo { Path = tmp.KeepassDatabase };
                    var compKey = new CompositeKey();
                    compKey.AddUserKey(new KcpPassword(tmpArgs.Password));
                    Passdb.Open(ioConnInfo, compKey, null);*/
                }
                catch (Exception exc)
                {
                    OnSiteNotify?.Invoke(this, new GenericEventArgs { Message="Failed to open KeePass Database." });
                }
            }
            try
            {
                _Logger?.LogInformation("Attempting DB Interface Creation: {DBProvider}", PersonalSettings.Provider);
                DBInterface = new BotContext(_Logger);//create a bot context here. 
                
                DBInterface.Settings = PersonalSettings;
                //if (!DBInterface.Database.EnsureCreated())
                {
                    try
                    {
                        DBInterface.Database.Migrate();
                    }
                    catch (Exception e)
                    {
                        _Logger?.LogError(e.ToString());
                    }
                }
                _Logger?.LogInformation("DB Interface Created: {DBProvider}", PersonalSettings.Provider);
            }
            catch (Exception e)
            {
                _Logger?.LogError(e.ToString()); 
                DBInterface = null;
            }
        }

        public ExportBetSettings LoadBetSettings(string FileLocation, bool ApplySettings = true)
        {
            List<Trigger> trig = JsonSerializer.Deserialize<List<Trigger>>(@"[{""Action"":4,""Enabled"":true,""TriggerProperty"":""Wins"",""TargetType"":1,""Target"":""Wins"",""Comparison"":3,""Percentage"":50,""ValueType"":0,""ValueProperty"":null,""ValueValue"":0,""Destination"":null}]");
            string Settings = "";
            try
            {
                using (StreamReader sr = new StreamReader(FileLocation))
                {
                    Settings = sr.ReadToEnd();
                }
                var tmp = JsonSerializer.Deserialize<ExportBetSettings>(Settings);

                if (string.IsNullOrWhiteSpace(tmp.Strategy))
                {
                    throw new Exception("Invalid settings file");
                }

                this.StoredBetSettings = JsonSerializer.Deserialize<ExportBetSettings>(Settings);
                if (StoredBetSettings?.BetSettings != null && ApplySettings)
                    this.BetSettings = StoredBetSettings.BetSettings;
                else
                    this.BetSettings = new InternalBetSettings();
                var tmpStrat = StoredBetSettings?.GetStrat();
                if (tmpStrat==null)
                {
                    throw new Exception("Invalid settings file");
                }
                this.Strategy = tmpStrat;
                this.Strategy.SetLogger(_Logger);
                return StoredBetSettings;
                
            }
            catch (Exception ex)
            {
                _Logger?.LogError(ex.ToString());
                throw;
            }
        }
        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {

            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        }
        #region Accounts
        public KPHelper[] GetAccounts()
        {
            //var Entries = Passdb.RootGroup.GetEntries(true);
            List<KPHelper> tmpHelpers = new List<KPHelper>();
            /*int i = 0;
            foreach (var x in Entries)
            {
                tmpHelpers.Add(new KPHelper {
                    Index = i++,
                    Title = x.Strings.ReadSafe("Title"),
                    Username = x.Strings.ReadSafe("Username"),
                    URL = x.Strings.ReadSafe("URL"),
                    Id = x.Uuid.UuidBytes
                });
            }*/
            //Passdb.RootGroup.FindEntry(new PwUuid(tmpHelpers[0].Id),true);
            return tmpHelpers.ToArray();
        }

        public string GetPw(KPHelper Helper, out string Note)
        {
            /*PwEntry pwEntry = Passdb.RootGroup.FindEntry(new PwUuid(Helper.Id), true);
            Note = pwEntry.Strings.ReadSafe("Note");
            return pwEntry.Strings.ReadSafe("Password");*/
            throw new NotImplementedException();
        }

        internal void Disconnect()
        {
            CurrentSite?.Disconnect();
            LoggedIn = CurrentSite?.LoggedIn??false;
        }

        internal void SetSite(SitesList newSite)
        {            
            CurrentSite = Activator.CreateInstance(newSite.SiteType(), this._Logger) as Gambler.Bot.Core.Sites.BaseSite;
        }
        Simulation CurrentSimulation;
        internal Simulation InitializeSim(decimal StartingBalance, long NumberOfBets,string File, bool Log)
        {
            CurrentSimulation = new Gambler.Bot.Strategies.Helpers.Simulation(null);
            CurrentSimulation.Initialize(StartingBalance, NumberOfBets, CurrentSite, CurrentSite.SiteDetails, Strategy, BetSettings, File, Log);
            CurrentSimulation.OnSimulationComplete += CurrentSimulation_OnSimulationComplete;
            return CurrentSimulation;
        }

        private void CurrentSimulation_OnSimulationComplete(object? sender, EventArgs e)
        {
            RunningSimulation = false;
        }

        internal List<LoginParamValue> GetLoginParams()
        {
            return CurrentSite.LoginParams.Select(x => new LoginParamValue { Param = x }).ToList();
        }

        internal BaseSite GetCurrentSite()
        {
            return CurrentSite;
        }
        #endregion

    }
}
