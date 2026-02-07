using ActiproSoftware.UI.Avalonia.Controls;
using ActiproSoftware.UI.Avalonia.Themes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Gambler.Bot.Classes;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.Common.Events;
using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;
using Gambler.Bot.Strategies.Helpers;
using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.ViewModels.AppSettings;
using Gambler.Bot.ViewModels.Common;
using Gambler.Bot.ViewModels.Games;
using Gambler.Bot.ViewModels.Games.Dice;
using Gambler.Bot.ViewModels.Games.Limbo;
using Gambler.Bot.ViewModels.Games.Twist;
using Gambler.Bot.ViewModels.Strategies;
using Gambler.Bot.Views;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels
{
    public class InstanceViewModel : ViewModelBase
    {
        private readonly INotificationManager _notificationManager;
        iLiveBet _liveBets;

        iPlaceBet _placeBetVM = null;// = new DicePlaceBetViewModel();

        private string _status;
        private IStrategy _strategyVM;
        string BetSettingsFile = string.Empty;
        string InstanceSettingsFile = string.Empty;
        private Classes.AutoBet botIns;
        private bool canResume;

        private bool canStart;

        private string lastAction;
        string PersonalSettingsFile = string.Empty;
        private bool showChart = true;

        private bool showLiveBets = true;
        private bool showSites = false;
        private bool showStats = true;
        private bool showBrowser = true;
        

        private string title;
        private DispatcherTimer tmrStats = new DispatcherTimer();
        private MediaPlayer _chime;
        private MediaPlayer _alarm;

        public InstanceViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            _logger.LogDebug("Instance viewmodel creating");
            GetLanguages();
            CreateMediaPlayers();
            tmrStats.Interval = TimeSpan.FromSeconds(1);
            tmrStats.Tick += TmrStats_Tick;

            AdvancedSettingsVM = new AdvancedViewModel(_logger);
            ConsoleVM = new ConsoleViewModel(_logger);
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
            BrowserCancelCommand = ReactiveCommand.Create(BrowserCancel);
            BrowserDoneCommand = ReactiveCommand.Create(BrowserDone);

            LogOutCommand = ReactiveCommand.Create(LogOut);
            ChangeSiteCommand = ReactiveCommand.Create(ChangeSite);
            SimulateCommand = ReactiveCommand.Create(Simulate);

            ExitCommand = ReactiveCommand.Create(Exit);
            OpenCommand = ReactiveCommand.Create(Open);
            SaveCommand = ReactiveCommand.Create(Save);

            ManualBetCommand = ReactiveCommand.Create(ManualBet);

            var tmp = new Classes.AutoBet(_logger);
            SelectSite = new SelectSiteViewModel(_logger);
            SelectSite.SelectedSiteChanged += SelectSite_SelectedSiteChanged;
            IsSelectSiteViewVisible = true;
            BrowserBypass = new Interaction<BypassRequiredArgs, BrowserConfig>();
            CFCaptchaBypass = new Interaction<string, Unit?>();
            ShowDialog = new Interaction<LoginViewModel, LoginViewModel?>();
            ShowAbout = new Interaction<AboutViewModel, Unit?>();
            ShowSimulation = new Interaction<SimulationViewModel, SimulationViewModel?>();
            ShowRollVerifier = new Interaction<RollVerifierViewModel, Unit?>();
            ExitInteraction = new Interaction<Unit?, Unit?>();
            BrowserCancelInteraction = new Interaction<Unit?, Unit?>();
            BrowserDoneInteraction = new Interaction<Unit?, Unit?>();
            ShowSettings = new Interaction<GlobalSettingsViewModel, Unit?>();
            ShowBetHistory = new Interaction<BetHistoryViewModel, Unit?>();
            ShowNotification = new Interaction<INotification, Unit?>();
            ShowUserInput = new Interaction<UserInputViewModel, Unit?>();
            saveFile = new Interaction<FilePickerSaveOptions, string>();
            openFile = new Interaction<FilePickerOpenOptions, string>();
            tmp.Strategy = new Martingale(_logger);
            PlaceBetVM = new DicePlaceBetViewModel(_logger);
            LoginVM = new LoginViewModel(_logger) { Site = tmp, LoginFinished = LoginFinished };
            LoginVM.ChangeSite += LoginVM_ChangeSite;
            PlaceBetVM.PlaceBet += PlaceBetVM_PlaceBet;
            tmp.OnGameChanged += BotIns_OnGameChanged;
            tmp.OnNotification += BotIns_OnNotification;
            tmp.OnSiteAction += BotIns_OnSiteAction;
            tmp.OnSiteBetFinished += BotIns_OnSiteBetFinished;
            tmp.OnStarted += BotIns_OnStarted;
            tmp.OnStopped += BotIns_OnStopped;
            tmp.OnStrategyChanged += BotIns_OnStrategyChanged;
            tmp.OnSiteLoginFinished += BotIns_OnSiteLoginFinished;
            tmp.OnBrowserBypassRequired = Tmp_OnBypassRequired;
            tmp.OnCFCaptchaBypass = Tmp_OnCFCaptchaBypass;
            tmp.OnSiteNotify += Tmp_OnSiteNotify;
            tmp.OnSiteError += Tmp_OnSiteError;
            tmp.PropertyChanged += Tmp_PropertyChanged;
            tmp.GetStrats();
            BotInstance = tmp;
            botIns.CurrentGame = Bot.Common.Games.Games.Dice;
            _logger.LogDebug("Instance viewmodel created");
            genLiveBetView = new GenericLiveBetViewModel(_logger);
        }

        private void BrowserDone()
        {
            
        }

        private void BrowserCancel()
        {
            
        }

        private async Task Tmp_OnCFCaptchaBypass(string e)
        {
            try
            {
                await CFCaptchaBypass?.Handle(e);
            }
            catch (Exception ex)
            {
                
            }
        }

       
        private void Tmp_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.RaisePropertyChanged(e.PropertyName);
        }

        private async void CreateMediaPlayers()
        {
            _logger.LogDebug("Creating media players");
            try
            {
                _chime = new MediaPlayer();
                _chime.LoadedBehavior = MediaPlayerLoadedBehavior.Manual;
                //await _chime.InitializeAsync();
                _chime.Source = new UriSource(Path.Combine(Environment.CurrentDirectory, @"Assets/Sounds/chime.wav"));
                await _chime.PrepareAsync();
                //await _chime.PlayAsync();

                _alarm = new MediaPlayer();
//            await _alarm.InitializeAsync();
                _alarm.Source = new UriSource(Path.Combine(Environment.CurrentDirectory, @"Assets/Sounds/alarm.wav"));
                await _alarm.PrepareAsync();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                
                #if LINUX
                if (e.Message.ToLower().Contains("libvlc"))
                {
                    var msgResult = await MessageBox.Show(
                        $"Could not initialize media players. Please ensure libvlc is installed. Please try: {Environment.NewLine}{Environment.NewLine}" +
                        $"apt install libvlc{Environment.NewLine}or{Environment.NewLine}dnf install libvlc{Environment.NewLine}{Environment.NewLine}" +
                        $"Gambler.Bot will continue to run but audio alerts will not work.",
                        "Failed to create media player",
                        MessageBoxButtons.OK);
                }
                #endif
            }
            
            
        }

        private void LoginVM_ChangeSite(object? sender, EventArgs e)
        {
            ChangeSite();
        }

        public List<string> Languages { get; set; }

        void GetLanguages()
        {
            _logger.LogDebug("Getting languages");
            Languages = new List<string>();
            Languages.Add("en-US");
            Languages.Add("af-ZA");
            /*var langs = App.Current.Resources.MergedDictionaries;
var langs2 = langs.Where(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false).ToList();*/
        }

        public void SetLanguage(string newLanguage)
        {
            var translations = App.Current.Resources.MergedDictionaries
                .OfType<ResourceInclude>()
                .FirstOrDefault(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false);

            if (translations != null)
                App.Current.Resources.MergedDictionaries.Remove(translations);


            App.Current.Resources.MergedDictionaries
                .Add(
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
                SiteStatsData.StatsUpdated(botIns.SiteStats);
            }
        }

        private void SetGameVM(Bot.Common.Games.Games? game, bool force =false)
        {
            iLiveBet tmpLive = null;
            if (game == null )
                LiveBets = genLiveBetView;

            if (LivebetVMs.ContainsKey(game?? Bot.Common.Games.Games.Dice))
                tmpLive = LivebetVMs[botIns.CurrentGame];

            if (tmpLive == null)
            {
                switch (game)
                {
                    case Bot.Common.Games.Games.Crash:
                    case Bot.Common.Games.Games.Roulette:
                    case Bot.Common.Games.Games.Plinko:
                        break;
                    case
                        Bot.Common.Games.Games.Dice:
                        {
                            LivebetVMs[game??default] = new DiceLiveBetViewModel(_logger);
                            tmpLive = LivebetVMs[game ?? default];
                            break;
                        }
                    case
                    Bot.Common.Games.Games.Twist:
                        {
                            LivebetVMs[game ?? default] = new DiceLiveBetViewModel(_logger);
                            tmpLive = LivebetVMs[game ?? default];

                            break;
                        }
                    case
                        Bot.Common.Games.Games.Limbo:
                        LivebetVMs[game ?? default] = new LimboLiveBetViewModel(_logger);
                        tmpLive = LivebetVMs[game ?? default];

                        break;

                }
            }
            if (SelectedView != null && (SelectedView!= game || force))
            {
                _selectedView = game;
                LiveBets = tmpLive;
            }
            else if (LiveBets == null)
                LiveBets = genLiveBetView;
        }

        private void BotIns_OnGameChanged(object? sender, EventArgs e)
        {
            if (PlaceBetVM != null)
                PlaceBetVM.PlaceBet -= PlaceBetVM_PlaceBet;
            
            switch (botIns.CurrentGame)
            {
                case Bot.Common.Games.Games.Crash:
                case Bot.Common.Games.Games.Roulette:
                case Bot.Common.Games.Games.Plinko:
                    break;
                case
                    Bot.Common.Games.Games.Dice:

                    {
                        PlaceBetVM = new DicePlaceBetViewModel(_logger);                        
                        break;
                    }
                case
                Bot.Common.Games.Games.Twist:

                    {
                        PlaceBetVM = new TwistPlaceBetViewModel(_logger);                       
                        break;
                    }
                case
                    Bot.Common.Games.Games.Limbo:
                    break;
            }
            SetGameVM(botIns.CurrentGame);
            if (PlaceBetVM != null)
            {
                PlaceBetVM.PlaceBet += PlaceBetVM_PlaceBet;
                PlaceBetVM.GameSettings = botIns?.GetCurrentSite()?.GetGameSettings(botIns.CurrentGame);
            }
            if (StrategyVM != null)
                StrategyVM.GameChanged(botIns.CurrentGame, botIns?.GetCurrentSite()?.GetGameSettings(botIns.CurrentGame));
            setTitle();
            this.RaisePropertyChanged(nameof(CurrentGame));
        }

        private void BotIns_OnNotification(object? sender, NotificationEventArgs e)
        {
            switch (e.NotificationTrigger.Action)
            {
                case TriggerAction.Alarm:
                    PlaySound(_alarm);
                    break;
                case TriggerAction.Chime:
                    PlaySound(_chime);
                    break;
                case TriggerAction.Email:
                    break;
                case TriggerAction.Popup:
                    Avalonia.Controls.Notifications.Notification notification = new Avalonia.Controls.Notifications.Notification(
                        e.NotificationTrigger.ToString(),
                        e.NotificationTrigger.ToString(),
                        NotificationType.Information);

                    NotificationAsync(notification);
                    break;
            }
        }


        async Task NotificationAsync(INotification notification) { await ShowNotification.Handle(notification); }

        private void BotIns_OnSiteAction(object sender, GenericEventArgs e)
        {
            LastAction = e.Message;
            ConsoleVM.AddLine(e.Message);
        }

        private async void BotIns_OnSiteBetFinished(object sender, BetFinisedEventArgs e)
        {
            if (e.NewBet != null)
            {
                SiteStatsData.StatsUpdated(botIns.SiteStats);
                SessionStatsData.StatsUpdated(botIns.Stats);
                ChartData.AddPoint(e.NewBet.Profit, e.NewBet.IsWin);
                genLiveBetView.AddBet(e.NewBet);
                
                if (LivebetVMs.ContainsKey(e.NewBet.Game))
                    LivebetVMs[e.NewBet.Game].AddBet(e.NewBet);
            }
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

        async Task PlaySound(MediaPlayer sound)
        {
            
            /*if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread
                    .Invoke(
                        () =>
                        {
                            PlaySound(sound);
                        });
            }*/
            try
            {
                if (sound != null)
                {
                    await sound.StopAsync();
                    await sound.PlayAsync();
                }
                else
                {
                    _logger.LogWarning("Attempted to play sound without player initialized.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
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
                case "Martingale":
                    tmpStrat = new MartingaleViewModel(_logger);
                    break;
                case "D'Alembert":
                    tmpStrat = new DAlembertViewModel(_logger);
                    break;
                case "Fibonacci":
                    tmpStrat = new FibonacciViewModel(_logger);
                    break;
                case "Labouchere":
                    tmpStrat = new LabouchereViewModel(_logger);
                    break;
                case "PresetList":
                    tmpStrat = new PresetListViewModel(_logger);
                    break;
                case "ProgrammerLUA":
                    tmpStrat = new ProgrammerModeLUAViewModel(_logger);
                    break;
                case "ProgrammerCS":
                    tmpStrat = new ProgrammerModeCSViewModel(_logger);
                    break;
                case "ProgrammerJS":
                    tmpStrat = new ProgrammerModeJSViewModel(_logger);
                    break;
                case "ProgrammerPython":
                    tmpStrat = new ProgrammerModePYViewModel(_logger);
                    break;
                default:
                    tmpStrat = null;
                    break;
                    ;
            }
            if (tmpStrat != null)
            {
                tmpStrat.SetStrategy(BotInstance.Strategy);
                tmpStrat.GameChanged(BotInstance.CurrentGame, botIns?.GetCurrentSite()?.GetGameSettings(BotInstance.CurrentGame));
            }
            if (BotInstance.Strategy is IProgrammerMode prog)
            {
                ConsoleVM.Strategy = prog;
                prog.OnAlarm -= Prog_OnAlarm;
                prog.OnAlarm += Prog_OnAlarm;
                prog.OnChing -= Prog_OnChing;
                prog.OnChing += Prog_OnChing;
                prog.OnExportSim -= Prog_OnExportSim;
                prog.OnExportSim += Prog_OnExportSim;
                prog.OnPrint -= Prog_OnPrint;
                prog.OnPrint += Prog_OnPrint;
                prog.OnRead -= Prog_OnRead;
                prog.OnRead += Prog_OnRead;
                prog.OnReadAdv -= Prog_OnReadAdv;
                prog.OnReadAdv += Prog_OnReadAdv;
                prog.OnRunSim -= Prog_OnRunSim;
                prog.OnRunSim += Prog_OnRunSim;
                prog.OnScriptError -= Prog_OnScriptError;
                prog.OnScriptError += Prog_OnScriptError;
            }
            else
            {
                ConsoleVM.Strategy = null;
            }
            StrategyVM?.Dispose();
            StrategyVM = tmpStrat;
            this.RaisePropertyChanged(nameof(SelectedStrategy));
            setTitle();
        }

        private void Prog_OnScriptError(object? sender, PrintEventArgs e) { ConsoleVM.AddLine(e.Message); }

        private void Prog_OnRunSim(object? sender, RunSimEventArgs e)
        {
            if (simControl?.Running ?? false)
            {
                ConsoleVM.AddLine("Cannot start simulation, already running");
            }
            else
            {
                simControl = simControl ?? new SimulationViewModel(_logger);
                simControl.Bot = BotInstance;
                simControl.CanStart += SimControl_CanStart;
                simControl.NumberOfBets = e.Bets;
                simControl.StartingBalance = e.Balance;
                simControl.Log = e.WriteLog;
                simControl.StartCommand.Execute(null);
            }
        }

        private void Prog_OnReadAdv(object? sender, ReadEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => Prog_OnReadAdv(sender, e));
                return;
            }
            using (var source = new CancellationTokenSource())
            {
                Read(e).ContinueWith(t => source.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.UIThread.MainLoop(source.Token);
            }
        }

        bool WaitForInput = false;

        private void Prog_OnRead(object? sender, ReadEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess() )
            {
                Dispatcher.UIThread.Invoke(() => Prog_OnRead(sender,e));
                return;
            }
            if (e.DataType == 0)
            {
                e.btncanceltext = "No";
                e.btnoktext = "Yes";
            }
            else
            {
                e.btncanceltext = "Cancel";
                e.btnoktext = "Ok";
            }
            using (var source = new CancellationTokenSource())
            {
                Read(e).ContinueWith(t => source.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.UIThread.MainLoop(source.Token);
            }
        }

        public async Task Read(ReadEventArgs e)
        {
            try
            {
                
                UserInputViewModel tmp = new UserInputViewModel(_logger);
                tmp.Args = e;

                await ShowUserInput.Handle(tmp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading user input");
                ConsoleVM.AddLine("Error reading user input: " + ex.Message);
            }
        }

        private void Prog_OnPrint(object? sender, PrintEventArgs e) { ConsoleVM.AddLine(e.Message); }

        private void Prog_OnExportSim(object? sender, ExportSimEventArgs e)
        {
            if (simControl == null)
            {
                ConsoleVM.AddLine("No simulation to export");
                return;
            }
            if (simControl.Running)
            {
                ConsoleVM.AddLine("Cannot export simulation, it is still running");
                return;
            }
            if (simControl.CurrentSimulation == null)
            {
                ConsoleVM.AddLine("No simulation to export");
                return;
            }
            if (!simControl.Log)
            {
                ConsoleVM.AddLine("Cannot export simulation, log was not enabled");
                return;
            }
            simControl.Save(e.FileName);
        }

        private void Prog_OnChing(object? sender, EventArgs e) { PlaySound(_chime); }

        private void Prog_OnAlarm(object? sender, EventArgs e) { PlaySound(_alarm); }

        void ChangeSite()
        {
            botIns.StopStrategy("Logging Out");
            botIns.Disconnect();
            ShowSites = true;

            this.RaisePropertyChanged(nameof(LoggedIn));
            this.RaisePropertyChanged(nameof(NotLoggedIn));
        }

        public async Task Exit() { await ExitInteraction.Handle(null); }

        void LoadInstanceSettings(string FileLocation)
        {
            string Settings = string.Empty;
            using (StreamReader sr = new StreamReader(FileLocation))
            {
                Settings = sr.ReadToEnd();
            }
            InstanceSettings tmp = JsonSerializer.Deserialize<InstanceSettings>(Settings);
            //botIns.ga

            var tmpsite = Classes.AutoBet.Sites.FirstOrDefault(m => m.Name == tmp.Site);
            if (tmpsite != null)
            {
                ShowSites = false;
                SiteChanged(tmpsite, tmp.Currency, tmp.Game);
            }
            else
            {
                ShowSites = true;
            }
            if (tmp.Game != null)
                botIns.CurrentGame = Enum.Parse<Bot.Common.Games.Games>(tmp.Game);
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
            botIns.Disconnect();
            ShowLogin();
            this.RaisePropertyChanged(nameof(LoggedIn));
            this.RaisePropertyChanged(nameof(NotLoggedIn));
        }

        async Task Open()
        {
            var result = await OpenFileInteraction.Handle(new FilePickerOpenOptions
            {
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("json") { Patterns = new List<string>() { $"*.json" } } },
                Title = "Save Script",
            });

            if (File.Exists(result))
            {
                try
                {
                    botIns.LoadBetSettings(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    var msgResult = await MessageBox.Show(
    "Could not load the seetings. It's likely not a valid settings file.",
    "Could not import.",
    MessageBoxButtons.OK);

                }
            }
        }

        private void PlaceBetVM_PlaceBet(object? sender, PlaceBetEventArgs e) { botIns.PlaceBet(e.NewBet); }
        void Resume() { botIns.Resume(); }

        async Task Save()
        {
            var result = await SaveFileInteraction.Handle(new FilePickerSaveOptions
            {
                DefaultExtension = ".json",
                ShowOverwritePrompt = true,
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("Bet Settings") { Patterns = new List<string>() { $"*.json" } } },
                Title = "Save Script",
                SuggestedFileName = $"NewScript.json"

            });
            if (result == null)
                return;
            botIns.SaveBetSettings(result);
        }

        void ManualBet()
        {
            PlaceBetVM.BetCommand();
        }

        void SaveINstanceSettings(string FileLocation)
        {
            if (Path.GetDirectoryName(FileLocation) != string.Empty && !Directory.Exists(Path.GetDirectoryName(FileLocation)))
                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation));
            string Settings = JsonSerializer.Serialize<InstanceSettings>(
                new InstanceSettings
                {
                    Site = botIns.SiteName,
                    AutoLogin = false,
                    Game = botIns.CurrentGame.ToString(),
                    Currency = botIns.CurrentCurrency
                });
            File.WriteAllText(FileLocation, Settings);
        }

        private void SelectSite_SelectedSiteChanged(object? sender, Gambler.Bot.Core.Helpers.SitesList e)
        {
            if (sender is SelectSiteViewModel selectSiteViewModel)
            {
                SiteChanged(e, e.SelectedCurrency?.Name, e.SelectedGame?.Name, !selectSiteViewModel.BypassLogIn);
            }
            if (SiteStatsData != null)
                SiteStatsData.SiteName = botIns?.SiteName;
        }

        private void SessionStatsData_OnResetStats(object? sender, EventArgs e)
        {
            botIns.ResetStats();
            SessionStatsData.StatsUpdated(botIns.Stats);
        }

        void setCanResume()
        {
            CanResume = ((botIns?.LoggedIn ?? false) &&
                botIns.Strategy != null &&
                !botIns.Running &&
                !botIns.RunningSimulation);
        }
        void setCanStart()
        {
            CanStart = ((botIns?.LoggedIn ?? false) &&
                botIns.Strategy != null &&
                !botIns.Running &&
                !botIns.RunningSimulation);
        }

        void SetStrategy(string name)
        {
            if (botIns.Strategy.StrategyName != name && !string.IsNullOrWhiteSpace(BetSettingsFile))
            {
                StrategyVM?.Saving();

                botIns.SaveBetSettings(BetSettingsFile);
                var Settings = botIns.LoadBetSettings(BetSettingsFile, false);
                IEnumerable<PropertyInfo> Props = Settings.GetType()
                    .GetProperties()
                    .Where(m => typeof(BaseStrategy).IsAssignableFrom(m.PropertyType));
                BaseStrategy newStrat = null;
                foreach (PropertyInfo x in Props)
                {
                    BaseStrategy strat = (BaseStrategy)x.GetValue(Settings);
                    if (strat != null)
                    {
                        PropertyInfo StratNameProp = strat.GetType().GetProperty("StrategyName");
                        string nm = (string)StratNameProp.GetValue(strat);
                        if (nm == name)
                        {
                            newStrat = strat;
                            break;
                        }
                    }
                }
                if (newStrat == null)
                {
                    newStrat = Activator.CreateInstance(botIns.Strategies[name]) as BaseStrategy;
                }
                botIns.Strategy = newStrat;
            }
        }

        void setTitle()
        {
            Title = $"{botIns?.SiteName} - {botIns?.CurrentGame.ToString()} - {botIns?.Strategy?.StrategyName} ({(Running ? "Running" : "Sopped")}";
        }

        async Task ShowLogin()
        {
            try
            {
                //LoginVM.Site = botIns;
                LoginVM.RefreshParams();
                LoginVM.LoginFinished = LoginFinished;
                /*var result = await ShowDialog.Handle(store);*/
            }
            catch (Exception e)
            {
            }
        }

        SimulationViewModel simControl;

        async Task Simulate()
        {
            if (!(simControl?.Running ?? false))
            {
                simControl = new SimulationViewModel(_logger);
                simControl.Bot = botIns;
                simControl.CanStart += SimControl_CanStart;
                await ShowSimulation.Handle(simControl);
            }
            else
            {
                await MessageBox.Show(
                    "There is already a simulation running. Please wait for it to finish or close the simulation window.",
                    "Already running");
            }
        }

        private void SimControl_CanStart(object? sender, CanSimulateEventArgs e)
        {
            e.CanSimulate = !BotInstance.Running &&
                BotInstance.CurrentGame != null &&
                BotInstance.SiteName != null &&
                BotInstance.Strategy != null &&
                !(simControl?.Running ?? false);
        }

        public async Task RollVerifier()
        {
            RollVerifierViewModel simControl = new RollVerifierViewModel(
                _logger,
                BotInstance?.GetCurrentSite(),
                BotInstance?.CurrentGame ?? Bot.Common.Games.Games.Dice);

            await ShowRollVerifier.Handle(simControl);
        }

        void SiteChanged(SitesList SiteName, string currency, string game, bool showLogin = true)
        {
            botIns.SetSite(SiteName);
            if (currency != null && Array.IndexOf(botIns.Currencies, currency) >= 0)
                botIns.CurrentCurrency = currency;
            object curGame = Bot.Common.Games.Games.Dice;
            if (game != null &&
                Enum.TryParse(typeof(Bot.Common.Games.Games), game, out curGame) &&
                Array.IndexOf(botIns.SiteGames, (Bot.Common.Games.Games)curGame) >= 0)
                botIns.CurrentGame = (Bot.Common.Games.Games)curGame;
            PlaceBetVM.GameSettings = botIns.GetCurrentSite().GetGameSettings(botIns.CurrentGame);
            string tmpCurrency = CurrentCurrency;
            var tmpGame = botIns.CurrentGame;
            this.RaisePropertyChanged(nameof(Currencies));
            CurrentCurrency = tmpCurrency;
            this.RaisePropertyChanged(nameof(Games));
            CurrentGame = tmpGame;
            //this.RaisePropertyChanged(nameof(CurrentCurrency));
            //this.RaisePropertyChanged(nameof(CurrentGame));
            this.RaisePropertyChanged(nameof(SiteName));
            ShowLogin();
            /*if (showLogin)
               //.Wait();
            else*/
            ShowSites = false;
            ShowGameMode = botIns.GetCurrentSite()?.GameModes.Count > 1;
            this.RaisePropertyChanged(nameof(GameModes));
            this.RaisePropertyChanged(nameof(SelectedGameMode));
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

        void Stop() { botIns.StopStrategy("Stop button clicked"); }
        void StopOnWin() { botIns.StopOnWin = true; }

        private async Task<BrowserConfig> Tmp_OnBypassRequired(BypassRequiredArgs e)
        {
            e.Config = await BrowserBypass.Handle(e);
            return e.Config;
        }

        private void Tmp_OnSiteError(object sender, Bot.Common.Events.ErrorEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread
                    .Invoke(
                        () =>
                        {
                            Tmp_OnSiteError(sender, e);
                        });
            else
            {
                StatusMessage = e.Message;
                ConsoleVM.AddLine(e.Message);
            }
        }

        private void Tmp_OnSiteNotify(object sender, GenericEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread
                    .Invoke(
                        () =>
                        {
                            Tmp_OnSiteNotify(sender, e);
                        });
            else
            {
                StatusMessage = e.Message;
                ConsoleVM.AddLine(e.Message);
            }
        }

        public void LoadSettings(string Name)
        {
            try
            {
                string path = string.Empty;
                if (UISettings.Portable)
                    path = "";
                else
                {
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gambler.Bot");
                }
                InstanceName = Name;
                //load bet settings
                BetSettingsFile = Path.Combine(path, Name + ".betset");

                InstanceSettingsFile = Path.Combine(path, Name + ".siteset");
                if (File.Exists(InstanceSettingsFile))
                {
                    LoadInstanceSettings(InstanceSettingsFile);
                }
                else
                {
                    ShowSites = true;
                }
                if (!File.Exists(BetSettingsFile))
                {
                    //botIns.BetSettings = new Gambler.Bot.Strategies.AutoBet.BetSettings();
                    botIns.BetSettings = new InternalBetSettings();
                    botIns.Strategy = new Gambler.Bot.Strategies.Strategies.Martingale(_logger);
                    botIns.SaveBetSettings(BetSettingsFile);
                }
                botIns.LoadBetSettings(BetSettingsFile);
                this.RaisePropertyChanged(nameof(SelectedStrategy));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            //if password is available, log in.
            //do all of this async to the gui somewhow?
        }

        public void OnClosing()
        {
            botIns.StopStrategy("Application Closing");
            botIns.Disconnect();
            if (!UISettings.Resetting)
            {
                botIns.SaveBetSettings(Path.Combine(BetSettingsFile));
                botIns.SavePersonalSettings(PersonalSettingsFile);
                SaveINstanceSettings(Path.Combine(InstanceSettingsFile));
            }
        }

        internal void Loaded()
        {            
            //botIns.GetStrats();
            this.RaisePropertyChanged(nameof(Strategies));
            if (UISettings.Portable)
            {
                PersonalSettingsFile = "PersonalSettings.json";
            }
            //Check if global settings for this account exists
            else
            {
                PersonalSettingsFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Gambler.Bot",
                    "PersonalSettings.json");
            }
            if (!File.Exists(PersonalSettingsFile))
            {
                botIns.PersonalSettings = PersonalSettings.Default();
                botIns.SavePersonalSettings(PersonalSettingsFile);
            }
            botIns.LoadPersonalSettings(PersonalSettingsFile);
            LoadSettings("default");
            this.RaisePropertyChanged(nameof(Currencies));
            this.RaisePropertyChanged(nameof(Games));
            this.RaisePropertyChanged(nameof(CurrentCurrency));
            this.RaisePropertyChanged(nameof(CurrentGame));
        }

        public AdvancedViewModel AdvancedSettingsVM { get; set; }// = new AdvancedViewModel();

        public ConsoleViewModel ConsoleVM { get; set; }// = new AdvancedViewModel();

        public Classes.AutoBet? BotInstance
        {
            get => botIns;
            set
            {
                botIns = value;
                this.RaisePropertyChanged();
            }
        }

        public bool CanResume
        {
            get { return canResume; }
            set
            {
                canResume = value;
                this.RaisePropertyChanged();
            }
        }

        public bool CanStart
        {
            get { return canStart; }
            set
            {
                canStart = value;
                this.RaisePropertyChanged();
            }
        }

        public ICommand ChangeSiteCommand { get; set; }

        public ProfitChartViewModel ChartData { get; set; }// = new ProfitChartViewModel();

        public string[] Currencies { get { return BotInstance?.Currencies; } }

        public string? CurrentCurrency
        {
            get { return BotInstance?.CurrentCurrency; }
            set
            {
                BotInstance.CurrentCurrency = value;
                this.RaisePropertyChanged();
            }
        }
        public List<string> GameModes { get => botIns.GetCurrentSite()?.GameModes; }
        public string SelectedGameMode
        {
            get => botIns.GetCurrentSite()?.SelectedGameMode; set
            {
                if (botIns.GetCurrentSite() != null)
                    botIns.GetCurrentSite().SelectedGameMode = value;
                this.RaisePropertyChanged(nameof(SelectedGameMode));
            }
        }
        private bool showGameMode;

        public bool ShowGameMode
        {
            get { return showGameMode; }
            set { showGameMode = value; this.RaisePropertyChanged(); }
        }



        public Bot.Common.Games.Games? CurrentGame
        {
            get
            {
                if (BotInstance?.SiteGames == null)
                    return null;
                return BotInstance?.CurrentGame;
            }
            set
            {
                if (BotInstance?.SiteGames != null)
                    BotInstance.CurrentGame = value ?? BotInstance.SiteGames.First();
                this.RaisePropertyChanged();
            }
        }

        public ICommand ExitCommand { get; }

        public Bot.Common.Games.Games[] Games { get { return BotInstance?.SiteGames; } }

        public string InstanceName { get; set; }

        public bool IsSelectSiteViewVisible { get; set; }

        public string LastAction
        {
            get { return lastAction; }
            set
            {
                lastAction = value;
                this.RaisePropertyChanged();
            }
        }
        GenericLiveBetViewModel genLiveBetView;
        Dictionary<Bot.Common.Games.Games, iLiveBet> LivebetVMs = new Dictionary<Bot.Common.Games.Games, iLiveBet>();
        Bot.Common.Games.Games? _selectedView;
        public Bot.Common.Games.Games? SelectedView
        {
            get => _selectedView; set
            {
                _selectedView = value;
                this.RaisePropertyChanged();
                SetGameVM(value, true);
            }
        }

        public iLiveBet LiveBets
        {
            get => _liveBets;
            set
            {
                _liveBets = value;
                this.RaisePropertyChanged();
            }
        }

        public bool LoggedIn { get { return botIns?.LoggedIn ?? false; } }

        public ICommand LogOutCommand { get; set; }

        public bool NotLoggedIn { get { return !(botIns?.LoggedIn ?? false); } }

        public ICommand OpenCommand { get; }

        public iPlaceBet PlaceBetVM
        {
            get => _placeBetVM;
            set
            {
                _placeBetVM = value;
                this.RaisePropertyChanged();
            }
        }

        private LoginViewModel LoginViewModel;

        public LoginViewModel LoginVM
        {
            get { return LoginViewModel; }
            set { LoginViewModel = value; this.RaisePropertyChanged(); }
        }

        public ResetSettingsViewModel ResetSettingsVM { get; set; }// = new ResetSettingsViewModel();

        public ICommand ResumeCommand { get; set; }

        public bool Running { get { return botIns?.Running ?? false; } }

        public ICommand SaveCommand { get; }
        public ICommand ManualBetCommand { get; }

        public IEnumerable<string> Strategies { get { return BotInstance?.Strategies?.Keys; } }

        public string SelectedStrategy
        {
            get { return BotInstance?.Strategy?.StrategyName; }
            set { SetStrategy(value); }
        }

        public SelectSiteViewModel SelectSite { get; set; }

        public SessionStatsViewModel SessionStatsData { get; set; }// = new SessionStatsViewModel();


        public bool ShowBot { get { return !ShowSites; } }

        public bool ShowChart
        {
            get { return showChart; }
            set
            {
                showChart = value;
                this.RaisePropertyChanged();
            }
        }
        public Interaction<BypassRequiredArgs?,BrowserConfig> BrowserBypass { get; }
        public Interaction<string,Unit?> CFCaptchaBypass { get; }
        
        public Interaction<LoginViewModel, LoginViewModel?> ShowDialog { get; }

        public Interaction<AboutViewModel, Unit?> ShowAbout { get; }

        public bool ShowLiveBets
        {
            get { return showLiveBets; }
            set
            {
                showLiveBets = value;
                this.RaisePropertyChanged();
            }
        }

        public Interaction<SimulationViewModel, SimulationViewModel?> ShowSimulation { get; }


        public string SiteName { get { return BotInstance?.SiteName ?? "Site"; } }

        public bool ShowSites
        {
            get { return showSites; }
            set
            {
                showSites = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(ShowBot));
            }
        }

        public bool ShowStats
        {
            get { return showStats; }
            set
            {
                showStats = value;
                this.RaisePropertyChanged();
            }
        }
        
        public bool ShowBrowser
        {
            get { return showBrowser; }
            set
            {
                showBrowser = value;
                this.RaisePropertyChanged();
            }
        }

        public ICommand SimulateCommand { get; }

        public SiteStatsViewModel SiteStatsData { get; set; }// = new SiteStatsViewModel();

        public ICommand StartCommand { get; set; }
        public ICommand BrowserDoneCommand { get; set; }
        public ICommand BrowserCancelCommand { get; set; }

        public string StatusMessage
        {
            get { return _status; }
            set
            {
                _status = value;
                this.RaisePropertyChanged();
            }
        }

        public ICommand StopCommand { get; set; }

        public ICommand StopOnWinCommand { get; set; }

        public bool Stopped { get { return !(botIns?.Running ?? false); } }

        public IStrategy StrategyVM
        {
            get { return _strategyVM; }
            set
            {
                _strategyVM = value;
                this.RaisePropertyChanged();
            }
        }

        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                this.RaisePropertyChanged();
            }
        }

        public async Task OpenSettingsCommand()
        {
            GlobalSettingsViewModel settingsControl = new GlobalSettingsViewModel(_logger);
            settingsControl.SetSettings(botIns.PersonalSettings);
            settingsControl.SettingsSaved += GlobalSettingsViewModel_SettingsSaved;
            await ShowSettings.Handle(settingsControl);
        }

        public async Task BetHistoryCommand()
        {
            BetHistoryViewModel settingsControl = new BetHistoryViewModel(_logger);
            settingsControl.Site = botIns?.SiteName;
            settingsControl.Game = botIns.CurrentGame;
            settingsControl.Context = botIns.DBInterface;

            await ShowBetHistory.Handle(settingsControl);
        }

        private void GlobalSettingsViewModel_SettingsSaved(object? sender, EventArgs e)
        {
            BotInstance.PersonalSettings = (sender as GlobalSettingsViewModel).Settings;
            BotInstance.SavePersonalSettings(PersonalSettingsFile);
            BotInstance.LoadPersonalSettings(PersonalSettingsFile);
        }

        public TriggersViewModel TriggersVM { get; set; }

        public Interaction<Unit?, Unit?> ExitInteraction { get; internal set; }
        public Interaction<Unit?, Unit?> BrowserCancelInteraction { get; internal set; }
        public Interaction<Unit?, Unit?> BrowserDoneInteraction { get; internal set; }

        public Interaction<RollVerifierViewModel, Unit?> ShowRollVerifier { get; internal set; }

        public Interaction<GlobalSettingsViewModel, Unit?> ShowSettings { get; internal set; }

        public Interaction<BetHistoryViewModel, Unit?> ShowBetHistory { get; internal set; }

        public Interaction<INotification, Unit?> ShowNotification { get; internal set; }

        public Interaction<UserInputViewModel, Unit?> ShowUserInput { get; internal set; }
        private readonly Interaction<FilePickerSaveOptions, string> saveFile;
        public Interaction<FilePickerSaveOptions, string> SaveFileInteraction => saveFile;

        private readonly Interaction<FilePickerOpenOptions, string> openFile;
        public Interaction<FilePickerOpenOptions, string> OpenFileInteraction => openFile;

        public void ThemeToggled()
        {
            ModernTheme.TryGetCurrent(out var theme);
            UISettings.Settings.DarkMode = App.Current.ActualThemeVariant.Key == "Light";
            //determine if dark theme
            //set uisettings
        }


        public void OpenLink(string link)
        {
            Process tmpProcess = new Process();
            tmpProcess.StartInfo.FileName = link;
            tmpProcess.StartInfo.UseShellExecute = true;
            tmpProcess.Start();
        }

        public async Task ResetCommand()
        {
            var result = await MessageBox.Show(
                @"Are you sure you want to reset Gambler.Bot to its default settings?

                                                                                                This will clear your bet settings, interface settings and personal settings.
                                                                                                It will not delete or clear your bet history and it will not delete any programmer mode scripts from your computer.",
                "Reset Gambler.Bot to default",
                MessageBoxButtons.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                if (File.Exists(BetSettingsFile))
                    File.Delete(PersonalSettingsFile);
                if (File.Exists(BetSettingsFile))
                    File.Delete(BetSettingsFile);
                if (File.Exists(InstanceSettingsFile))
                    File.Delete(InstanceSettingsFile);
                MainViewModel.ClearUiSettings();
                UISettings.Resetting = true;
                result = await MessageBox.Show(
                    @"Gambler.Bot has been reset to default and will close. Please restart the application to resume betting.",
                    "Reset Complete");
                await Exit();
            }
        }

        public async Task AboutClicked() { await ShowAbout.Handle(new AboutViewModel(_logger)); }

    }
}

