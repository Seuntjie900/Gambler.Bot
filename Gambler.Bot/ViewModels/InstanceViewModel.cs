﻿using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Gambler.Bot.AutoBet.Strategies;
using Gambler.Bot.Classes;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.Core.Events;
using Gambler.Bot.ViewModels.AppSettings;
using Gambler.Bot.ViewModels.Common;
using Gambler.Bot.ViewModels.Games.Dice;
using Gambler.Bot.ViewModels.Strategies;
using Gambler.Bot.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using static Community.CsharpSqlite.Sqlite3;

namespace Gambler.Bot.ViewModels
{
    public class InstanceViewModel : ViewModelBase
    {

        iLiveBet _liveBets;

        iPlaceBet _placeBetVM = null;// = new DicePlaceBetViewModel();

        private string _status;
        private IStrategy _strategyVM;
        string BetSettingsFile = string.Empty;
        private Gambler.Bot.AutoBet.AutoBet botIns;
        private bool canResume;

        private bool canStart;

        private string lastAction;
        string PersonalSettingsFile = string.Empty;
        private bool showChart = true;

        private bool showLiveBets = true;
        private bool showSites = true;
        private bool showStats = true;

        private string title;
        private DispatcherTimer tmrStats = new DispatcherTimer();


        public InstanceViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            GetLanguages();
            tmrStats.Interval = TimeSpan.FromSeconds(1);
            tmrStats.Tick += TmrStats_Tick;
            AdvancedSettingsVM = new AdvancedViewModel(_logger);
            ResetSettingsVM = new ResetSettingsViewModel(_logger);
            ChartData = new ProfitChartViewModel(_logger);
            SiteStatsData = new SiteStatsViewModel(_logger);
            SessionStatsData = new SessionStatsViewModel(_logger);
            TriggersVM = new TriggersViewModel(_logger);

            SessionStatsData.OnResetStats += SessionStatsData_OnResetStats;

            StartCommand = ReactiveCommand.Create(Start);
            StopCommand = ReactiveCommand.Create(Stop);
            ResumeCommand = ReactiveCommand.Create(Resume);
            StopOnWinCommand = ReactiveCommand.Create(StopOnWin);

            LogOutCommand = ReactiveCommand.Create(LogOut);
            ChangeSiteCommand = ReactiveCommand.Create(ChangeSite);
            SimulateCommand = ReactiveCommand.Create(Simulate);

            ExitCommand = ReactiveCommand.Create(Exit);
            OpenCommand = ReactiveCommand.Create(Open);
            SaveCommand = ReactiveCommand.Create(Save);

            var tmp = new Gambler.Bot.AutoBet.AutoBet(_logger);
            SelectSite = new SelectSiteViewModel(_logger);
            SelectSite.SelectedSiteChanged += SelectSite_SelectedSiteChanged;
            IsSelectSiteViewVisible = true;
            ShowDialog = new Interaction<LoginViewModel, LoginViewModel?>();
            ShowSimulation = new Interaction<SimulationViewModel, SimulationViewModel?>();
            ShowRollVerifier = new Interaction<RollVerifierViewModel, Unit?>();
            ShowSettings = new Interaction<GlobalSettingsViewModel, Unit?>();
            tmp.Strategy = new Martingale(_logger);
            PlaceBetVM = new DicePlaceBetViewModel(_logger);
            PlaceBetVM.PlaceBet += PlaceBetVM_PlaceBet;
            tmp.OnGameChanged += BotIns_OnGameChanged;
            tmp.OnNotification += BotIns_OnNotification;
            tmp.OnSiteAction += BotIns_OnSiteAction;
            tmp.OnSiteBetFinished += BotIns_OnSiteBetFinished;
            tmp.OnStarted += BotIns_OnStarted;
            tmp.OnStopped += BotIns_OnStopped;
            tmp.OnStrategyChanged += BotIns_OnStrategyChanged;
            tmp.OnSiteLoginFinished += BotIns_OnSiteLoginFinished;
            tmp.OnBypassRequired += Tmp_OnBypassRequired;
            tmp.OnSiteNotify += Tmp_OnSiteNotify;
            tmp.OnSiteError += Tmp_OnSiteError;
            BotInstance = tmp;
            botIns.CurrentGame = Gambler.Bot.Core.Games.Games.Dice;
            
            
            

        }

        public List<string> Languages { get; set; }

        void GetLanguages()
        {
            Languages = new List<string>();
            Languages.Add("en-US");
            Languages.Add("af-ZA");
            /*var langs = App.Current.Resources.MergedDictionaries;
            var langs2 = langs.Where(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false).ToList();*/
        }

        public void SetLanguage(string newLanguage)
        {
            var translations = App.Current.Resources.MergedDictionaries.OfType<ResourceInclude>().FirstOrDefault(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false);

            if (translations != null)
                App.Current.Resources.MergedDictionaries.Remove(translations);

            

            App.Current.Resources.MergedDictionaries.Add(
                new ResourceInclude(new Uri($"avares://Gambler.Bot/Assets/Lang/{newLanguage}.axaml"))
                {
                    Source = new Uri($"avares://Gambler.Bot/Assets/Lang/{newLanguage}.axaml")
                });
        }

        private void TmrStats_Tick(object? sender, EventArgs e)
        {
            if (botIns.Running)
            {
                SessionStatsData.StatsUpdated(botIns.Stats);
                SiteStatsData.StatsUpdated(botIns.CurrentSite?.Stats);
            }
        }

        private void BotIns_OnGameChanged(object? sender, EventArgs e)
        {
            if (PlaceBetVM != null)
                PlaceBetVM.PlaceBet -= PlaceBetVM_PlaceBet;

            switch (botIns.CurrentGame)
            {
                case Gambler.Bot.Core.Games.Games.Crash:
                case Gambler.Bot.Core.Games.Games.Roulette:
                case Gambler.Bot.Core.Games.Games.Plinko:
                    break;
                case
                    Gambler.Bot.Core.Games.Games.Dice:
                    PlaceBetVM = new DicePlaceBetViewModel(_logger);
                    LiveBets = new DiceLiveBetViewModel(_logger);
                    break;

            }
            if (PlaceBetVM != null)
                PlaceBetVM.PlaceBet += PlaceBetVM_PlaceBet;

            setTitle();
        }

        private void BotIns_OnNotification(object? sender, Gambler.Bot.Core.Helpers.NotificationEventArgs e)
        {
            throw new NotImplementedException();
            switch (e.NotificationTrigger.Action)
            {
                case Gambler.Bot.Core.Helpers.TriggerAction.Alarm: break;
                case Gambler.Bot.Core.Helpers.TriggerAction.Chime: break;
                case Gambler.Bot.Core.Helpers.TriggerAction.Email: break;
                case Gambler.Bot.Core.Helpers.TriggerAction.Popup: break;
            }
        }

        private void BotIns_OnSiteAction(object sender, GenericEventArgs e)
        {
            LastAction = e.Message;
        }

        private void BotIns_OnSiteBetFinished(object sender, BetFinisedEventArgs e)
        {
            SiteStatsData.StatsUpdated(botIns.CurrentSite.Stats);
            SessionStatsData.StatsUpdated(botIns.Stats);
            ChartData.AddPoint(e.NewBet.Profit, e.NewBet.IsWin);
            LiveBets.AddBet(e.NewBet);
        }

        private void BotIns_OnSiteLoginFinished(object sender, LoginFinishedEventArgs e)
        {
            SiteStatsData.Stats = e.Stats;
            SiteStatsData.RaisePropertyChanged(nameof(SiteStatsData.Stats));
            this.RaisePropertyChanged(nameof(LoggedIn));
            this.RaisePropertyChanged(nameof(NotLoggedIn));
            setCanResume();
            setCanStart();
            setTitle();
        }
        private void BotIns_OnStarted(object? sender, EventArgs e)
        {
            SessionStatsData.Stats = botIns.Stats;
            SessionStatsData.RaisePropertyChanged(nameof(SessionStatsData.Stats));
            this.RaisePropertyChanged(nameof(Running));
            this.RaisePropertyChanged(nameof(Stopped));
            setCanResume();
            setCanStart();
            setTitle();
            tmrStats.Start();
        }

        private void BotIns_OnStopped(object? sender, GenericEventArgs e)
        {
            //if (!Dispatcher.CheckAccess())
            //    Dispatcher.Invoke(new Action<object, Gambler.Bot.Core.Sites.GenericEventArgs>(BotIns_OnStopped), sender, e);
            //else
            //{
            //    bbtnSimulator.IsEnabled = true;
            //    StatusBar.Content = $"Stopping: {e.Message}";
            //    btcStart.IsEnabled = true;
            //    btnResume.IsEnabled = true;

            //}
            StatusMessage = "Stopping: " + e.Message;
            this.RaisePropertyChanged(nameof(Running));
            this.RaisePropertyChanged(nameof(Stopped));
            setCanResume();
            setCanStart();
            setTitle();
            tmrStats.Stop();
        }

        private void BotIns_OnStrategyChanged(object? sender, EventArgs e)
        {
            AdvancedSettingsVM.BetSettings = botIns.BetSettings;
            ResetSettingsVM.BetSettings = botIns.BetSettings;
            TriggersVM.SetTriggers(botIns.BetSettings?.Triggers);
            IStrategy tmpStrat = null;
            //this needs to set the istrategy property to the appropriate viewmodel
            switch (BotInstance.Strategy?.StrategyName)
            {
                case "Martingale": tmpStrat = new MartingaleViewModel(_logger); break;
                case "D'Alembert": tmpStrat = new DAlembertViewModel(_logger); break;
                case "Fibonacci": tmpStrat = new FibonacciViewModel(_logger); break;
                case "Labouchere": tmpStrat = new LabouchereViewModel(_logger); break;
                case "PresetList": tmpStrat = new PresetListViewModel(_logger); break;
                case "ProgrammerLUA": tmpStrat = new ProgrammerModeLUAViewModel(_logger); break;
                case "ProgrammerCS": tmpStrat = new ProgrammerModeCSViewModel(_logger); break;
                case "ProgrammerJS": tmpStrat = new ProgrammerModeCSViewModel(_logger); break;
                case "ProgrammerPython": tmpStrat = new ProgrammerModePYViewModel(_logger); break;
                default: tmpStrat = null; break; ;
            }
            if (tmpStrat != null)
            {
                tmpStrat.SetStrategy(BotInstance.Strategy);
                tmpStrat.GameChanged(BotInstance.CurrentGame);
            }
            StrategyVM?.Dispose();
            StrategyVM = tmpStrat;
            setTitle();
        }
        void ChangeSite()
        {
            botIns.StopStrategy("Logging Out");
            botIns.CurrentSite.Disconnect();
            ShowSites = true;
        }

        void Exit()
        {
            throw new NotImplementedException();
        }

        void LoadInstanceSettings(string FileLocation)
        {
            string Settings = string.Empty;
            using (StreamReader sr = new StreamReader(FileLocation))
            {
                Settings = sr.ReadToEnd();
            }
            InstanceSettings tmp = JsonSerializer.Deserialize<InstanceSettings>(Settings);
            //botIns.ga

            var tmpsite = Gambler.Bot.AutoBet.AutoBet.Sites.FirstOrDefault(m => m.Name == tmp.Site);
            if (tmpsite != null)
            {
                botIns.CurrentSite = Activator.CreateInstance(tmpsite.SiteType(), _logger) as Gambler.Bot.Core.Sites.BaseSite;                
                ShowSites = false;
                SiteChanged(botIns.CurrentSite, tmp.Currency, tmp.Game);
            }
            if (tmp.Game != null)
                botIns.CurrentGame = Enum.Parse<Gambler.Bot.Core.Games.Games>(tmp.Game);

        }

        private void LoginFinished(bool ChangeScreens)
        {
            if (ChangeScreens)
            {
                ShowSites = false;
            }
        }
        void LogOut()
        {
            botIns.StopStrategy("Logging Out");
            botIns.CurrentSite.Disconnect();
            ShowLogin();
        }

        void Open()
        {
            throw new NotImplementedException();
        }

        private void PlaceBetVM_PlaceBet(object? sender, PlaceBetEventArgs e)
        {
            botIns.PlaceBet(e.NewBet);
        }
        void Resume()
        {
            botIns.Resume();
        }

        void Save()
        {
            throw new NotImplementedException();
        }

        void SaveINstanceSettings(string FileLocation)
        {
            string Settings = JsonSerializer.Serialize<InstanceSettings>(new InstanceSettings
            {
                Site = botIns.CurrentSite?.GetType()?.Name,
                AutoLogin = false,
                Game = botIns.CurrentGame.ToString(),
                Currency = botIns.CurrentSite?.CurrentCurrency
            });
            File.WriteAllText(FileLocation, Settings);
        }

        private void SelectSite_SelectedSiteChanged(object? sender, Gambler.Bot.Core.Helpers.SitesList e)
        {
            SiteChanged(Activator.CreateInstance(e.SiteType(), _logger) as Gambler.Bot.Core.Sites.BaseSite, e.SelectedCurrency?.Name, e.SelectedGame?.Name);
            if (SiteStatsData != null)
                SiteStatsData.SiteName = botIns.CurrentSite?.SiteName;
        }

        private void SessionStatsData_OnResetStats(object? sender, EventArgs e)
        {
            botIns.ResetStats();
            SessionStatsData.StatsUpdated(botIns.Stats);
        }

        void setCanResume()
        {
            CanResume = ((botIns?.LoggedIn ?? false) && botIns.Strategy != null && !botIns.Running && !botIns.RunningSimulation);
        }
        void setCanStart()
        {
            CanStart = ((botIns?.LoggedIn ?? false) && botIns.Strategy != null && !botIns.Running && !botIns.RunningSimulation);
        }

        void SetStrategy(string name)
        {
            if (botIns.Strategy.StrategyName != name && !string.IsNullOrWhiteSpace(BetSettingsFile))
            {
                StrategyVM?.Saving();

                botIns.SaveBetSettings(BetSettingsFile);
                var Settings = botIns.LoadBetSettings(BetSettingsFile, false);
                IEnumerable<PropertyInfo> Props = Settings.GetType().GetProperties().Where(m => typeof(Gambler.Bot.AutoBet.Strategies.BaseStrategy).IsAssignableFrom(m.PropertyType));
                Gambler.Bot.AutoBet.Strategies.BaseStrategy newStrat = null;
                foreach (PropertyInfo x in Props)
                {
                    Gambler.Bot.AutoBet.Strategies.BaseStrategy strat = (Gambler.Bot.AutoBet.Strategies.BaseStrategy)x.GetValue(Settings);
                    if (strat != null)
                    {
                        PropertyInfo StratNameProp = strat.GetType().GetProperty("StrategyName");
                        string nm = (string)StratNameProp.GetValue(strat);
                        if (nm == BetSettingsFile.ToString())
                        {
                            newStrat = strat;
                            break;
                        }
                    }
                }
                if (newStrat == null)
                {
                    newStrat = Activator.CreateInstance(botIns.Strategies[name]) as Gambler.Bot.AutoBet.Strategies.BaseStrategy;
                }
                botIns.Strategy = newStrat;
            }
        }

        void setTitle()
        {
            Title = $"{botIns?.CurrentSite?.SiteName} - {botIns?.CurrentGame.ToString()} - {botIns?.Strategy?.StrategyName} ({(Running ? "Running" : "Sopped")}";
        }

        async Task ShowLogin()
        {
            try
            {
                var store = new LoginViewModel(botIns.CurrentSite, _logger);
                store.LoginFinished = LoginFinished;
                var result = await ShowDialog.Handle(store);
            }
            catch (Exception e)
            {

            }
        }

        async Task Simulate()
        {
            SimulationViewModel simControl = new SimulationViewModel(_logger);
            simControl.CurrentSite = botIns.CurrentSite;
            simControl.Strategy = botIns.Strategy;
            simControl.BetSettings = botIns.BetSettings;
            await ShowSimulation.Handle(simControl);
        }

        public async Task RollVerifier()
        {
            RollVerifierViewModel simControl = new RollVerifierViewModel(_logger, BotInstance?.CurrentSite, BotInstance?.CurrentGame ?? Gambler.Bot.Core.Games.Games.Dice);
            
            await ShowRollVerifier.Handle(simControl);
        }

        void SiteChanged(Gambler.Bot.Core.Sites.BaseSite NewSite, string currency, string game)
        {
            botIns.CurrentSite = NewSite;
            if (currency != null && Array.IndexOf(botIns.CurrentSite.Currencies, currency) >= 0)
                botIns.CurrentSite.Currency = Array.IndexOf(botIns.CurrentSite.Currencies, currency);
            object curGame = Gambler.Bot.Core.Games.Games.Dice;
            if (game != null && Enum.TryParse(typeof(Gambler.Bot.Core.Games.Games), game, out curGame) && Array.IndexOf(botIns.CurrentSite.SupportedGames, (Gambler.Bot.Core.Games.Games)curGame) >= 0)
                botIns.CurrentGame = (Gambler.Bot.Core.Games.Games)curGame;
            this.RaisePropertyChanged(nameof(Currencies));
            this.RaisePropertyChanged(nameof(CurrentCurrency));
            ShowLogin();//.Wait();
            /*LoginControl.CurrentSite = botIns.CurrentSite;
            lciSelectSite1.Visibility = Visibility.Collapsed;
            lciLoginControl.Visibility = Visibility.Visible;
            itmCurrency.Items.Clear();
            foreach (string x in botIns.CurrentSite.Currencies)
            {
                var itm = new BarCheckItem();
                itm.Content = x;
                itm.CheckedChanged += Itm_CheckedChanged;
                itmCurrency.Items.Add(itm);
            }
            itmGame.Items.Clear();
            foreach (var x in botIns.CurrentSite.SupportedGames)
            {
                var itm = new BarCheckItem();
                itm.Content = x.ToString();
                itm.CheckedChanged += Itm_CheckedChanged;
                itmGame.Items.Add(itm);
            }
            lueCurrencies.ItemsSource = botIns.CurrentSite.Currencies;
            lueCurrencies.EditValue = botIns.CurrentSite.CurrentCurrency;
            lueGames.ItemsSource = botIns.CurrentSite.SupportedGames;
            lueGames.EditValue = botIns.CurrentGame;
            Rename?.Invoke(this, new RenameEventArgs { newName = "Log in - " + NewSite?.SiteName });*/
        }
        void Start()
        {
            if (!botIns.Running)
            {
                StrategyVM?.Saving();
                botIns.SaveBetSettings(BetSettingsFile);
                botIns.Start();
            }
        }
        void Stop()
        {
            botIns.StopStrategy("Stop button clicked");
        }
        void StopOnWin()
        {
            botIns.StopOnWin = true;
        }

        private void Tmp_OnBypassRequired(object? sender, BypassRequiredArgs e)
        {
            e.Config = MainView.GetBypass(e);
        }

        private void Tmp_OnSiteError(object sender, Core.Events.ErrorEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread.Invoke(() => { Tmp_OnSiteError(sender, e); });
            else
            {
                StatusMessage = e.Message;
            }
        }

        private void Tmp_OnSiteNotify(object sender, GenericEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread.Invoke(() => { Tmp_OnSiteNotify(sender, e); });
            else
            {
                StatusMessage = e.Message;
            }
        }

        public void LoadSettings(string Name)
        {
            string path = string.Empty;
            if (UISettings.Portable)
                path = "";
            else
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KryGamesBot");
            }
            InstanceName = Name;
            //load bet settings

            if (File.Exists(Path.Combine(path,Name + ".betset")))
            {
                BetSettingsFile = Path.Combine(path, Name + ".betset");

            }

            //load layout
            //if (File.Exists(path + Name + ".layout"))
            //{
            //    dlmMainLayout.RestoreLayoutFromXml(path + Name + ".layout");
            //}
            string InstanceSettingsFile =Path.Combine( path,Name + ".siteset");
            if (File.Exists(InstanceSettingsFile))
            {
                LoadInstanceSettings(InstanceSettingsFile);

            }
            if (File.Exists(BetSettingsFile))
                botIns.LoadBetSettings(BetSettingsFile);
            else
            {
                botIns.StoredBetSettings = new Gambler.Bot.AutoBet.AutoBet.ExportBetSettings
                {
                    BetSettings = new Gambler.Bot.AutoBet.Helpers.InternalBetSettings(),


                };
                botIns.Strategy = new Gambler.Bot.AutoBet.Strategies.Martingale(_logger);
            }
            this.RaisePropertyChanged(nameof(SelectedStrategy));
            
            
            //if password is available, log in.
            //do all of this async to the gui somewhow?
        }

        public void OnClosing()
        {
            botIns.StopStrategy("Application Closing");
            if (botIns.CurrentSite != null)
                botIns.CurrentSite.Disconnect();
            string path = string.Empty;
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) , "KryGamesBot");
            botIns.SaveBetSettings(Path.Combine(path, InstanceName + ".betset"));
            SaveINstanceSettings(Path.Combine(path, InstanceName + ".siteset"));
        }

        internal async Task Loaded()
        {
            botIns.GetStrats();
            if (UISettings.Portable && File.Exists("PersonalSettings.json"))
            {
                PersonalSettingsFile ="PersonalSettings.json";

            }
            //Check if global settings for this account exists
            else if (!UISettings.Portable && File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\KryGamesBot\\PersonalSettings.json"))
            {
                PersonalSettingsFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\KryGamesBot\\PersonalSettings.json";
                botIns.LoadPersonalSettings(PersonalSettingsFile);
            }

            LoadSettings("default");
        }

        public AdvancedViewModel AdvancedSettingsVM { get; set; }// = new AdvancedViewModel();
        public Gambler.Bot.AutoBet.AutoBet? BotInstance { get => botIns; set { botIns = value; this.RaisePropertyChanged(); } }

        public bool CanResume
        {
            get { return canResume; }
            set { canResume = value; this.RaisePropertyChanged(); }
        }

        public bool CanStart
        {
            get { return canStart; }
            set { canStart = value; this.RaisePropertyChanged(); }
        }

        public ICommand ChangeSiteCommand { get; set; }
        public ProfitChartViewModel ChartData { get; set; }// = new ProfitChartViewModel();

        public string[] Currencies
        {
            get { return BotInstance?.CurrentSite?.Currencies; }
        }
        public int? CurrentCurrency
        {
            get { return BotInstance?.CurrentSite?.Currency; }
            set { if (BotInstance?.CurrentSite != null) BotInstance.CurrentSite.Currency = (value >= 0 ? value : 0) ?? 0; this.RaisePropertyChanged(); }
        }
        public int? CurrentGame
        {
            get {  
                if (BotInstance?.CurrentSite == null) 
                    return -1;
                return  Array.IndexOf(BotInstance?.CurrentSite?.SupportedGames, BotInstance?.CurrentGame); 
            }
            set { if (BotInstance?.CurrentSite != null) BotInstance.CurrentGame = BotInstance?.CurrentSite?.SupportedGames[(value >= 0 ? value : 0) ?? 0] ?? Gambler.Bot.Core.Games.Games.Dice; }
        }

        public ICommand ExitCommand { get; }
        public Gambler.Bot.Core.Games.Games[] Games
        {
            get { return BotInstance?.CurrentSite?.SupportedGames; }
        }
        public string InstanceName { get; set; }
        public bool IsSelectSiteViewVisible { get; set; }

        public string LastAction
        {
            get { return lastAction; }
            set { lastAction = value; this.RaisePropertyChanged(); }
        }
        public iLiveBet LiveBets { get => _liveBets; set { _liveBets = value; this.RaisePropertyChanged(); } }

        public bool LoggedIn
        {
            get { return botIns?.LoggedIn ?? false; }
        }
        public ICommand LogOutCommand { get; set; }

        public bool NotLoggedIn
        {
            get { return !(botIns?.LoggedIn ?? false); }
        }

        public ICommand OpenCommand { get; }
        public iPlaceBet PlaceBetVM { get => _placeBetVM; set { _placeBetVM = value; this.RaisePropertyChanged(); } }
        public ResetSettingsViewModel ResetSettingsVM { get; set; }// = new ResetSettingsViewModel();

        public ICommand ResumeCommand { get; set; }
        public bool Running
        {
            get { return botIns?.Running ?? false; }
        }

        public ICommand SaveCommand { get; }




        public string SelectedStrategy
        {
            get { return BotInstance?.Strategy?.StrategyName; }
            set { SetStrategy(value); }
        }
        public SelectSiteViewModel SelectSite { get; set; }
        public SessionStatsViewModel SessionStatsData { get; set; }// = new SessionStatsViewModel();


        public bool ShowBot
        {
            get { return !ShowSites; }

        }

        public bool ShowChart
        {
            get { return showChart; }
            set { showChart = value; this.RaisePropertyChanged(); }
        }
        public Interaction<LoginViewModel, LoginViewModel?> ShowDialog { get; }

        public bool ShowLiveBets
        {
            get { return showLiveBets; }
            set { showLiveBets = value; this.RaisePropertyChanged(); }
        }
        public Interaction<SimulationViewModel, SimulationViewModel?> ShowSimulation { get; }

        public bool ShowSites
        {
            get { return showSites; }
            set { showSites = value; this.RaisePropertyChanged(); this.RaisePropertyChanged(nameof(ShowBot)); }
        }

        public bool ShowStats
        {
            get { return showStats; }
            set { showStats = value; this.RaisePropertyChanged(); }
        }

        public ICommand SimulateCommand { get; }
        public SiteStatsViewModel SiteStatsData { get; set; }// = new SiteStatsViewModel();

        public ICommand StartCommand { get; set; }

        public string StatusMessage
        {
            get { return _status; }
            set { _status = value; this.RaisePropertyChanged(); }
        }

        public ICommand StopCommand { get; set; }

        public ICommand StopOnWinCommand { get; set; }

        public bool Stopped
        {
            get { return !(botIns?.Running ?? false); }
        }

        public IStrategy StrategyVM
        {
            get { return _strategyVM; }
            set { _strategyVM = value; this.RaisePropertyChanged(); }
        }

        public string Title
        {
            get { return title; }
            set { title = value; this.RaisePropertyChanged(); }
        }

        public async Task OpenSettingsCommand()
        {
            GlobalSettingsViewModel simControl = new GlobalSettingsViewModel(_logger);
            simControl.Settings = botIns.PersonalSettings;
            await ShowSettings.Handle(simControl);
        }
        public TriggersViewModel TriggersVM { get; set; }
        public Interaction<RollVerifierViewModel, Unit?> ShowRollVerifier { get; internal set; }
        public Interaction<GlobalSettingsViewModel, Unit?> ShowSettings { get; internal set; }
    }
}
