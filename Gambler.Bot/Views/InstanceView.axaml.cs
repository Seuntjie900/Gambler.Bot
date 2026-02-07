using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gambler.Bot.ViewModels;
using Gambler.Bot.ViewModels.AppSettings;
using Gambler.Bot.ViewModels.Common;
using Gambler.Bot.Views.AppSettings;
using Gambler.Bot.Views.Common;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Gambler.Bot.Classes;
using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;

namespace Gambler.Bot.Views;

public partial class InstanceView : ReactiveUserControl<InstanceViewModel>
{
    private Window parentWindow;
    private INotificationManager notificationManager;
    public NativeWebView wvBypass { get; set; }
    NativeWebDialog dialog = null;
    BrowserConfig _conf = null;
    bool closeClicked = false;
    static DateTime LastRequest = DateTime.Now;
    bool checking = false;
    
    static bool cancelled = false;

    
    public InstanceView()
    {
        InitializeComponent();
        Loaded += InstanceView_Loaded;
        if (!OperatingSystem.IsLinux())
        {
            wvBypass = new NativeWebView();
            wvBypass.BeginInit();
            layoutgrd.Children.Add(wvBypass);
            Grid.SetRow(wvBypass, 1);
            wvBypass.NavigationCompleted += WvBypass_NavigationCompleted;
            wvBypass.WebMessageReceived += WvBypass_WebMessageReceived;
            wvBypass.Loaded += WvBypass_Loaded;
            wvBypass.SizeChanged += WvBypass_SizeChanged;
            wvBypass.AdapterCreated += WvBypass_AdapterCreated; ;
            wvBypass.EnvironmentRequested += WvBypass_EnvironmentRequested;
            wvBypass.NavigationStarted += WvBypass_NavigationStarted;
            wvBypass.WebResourceRequested += WvBypass_WebResourceRequested;
            
            wvBypass.EndInit();
        }
        if (!Design.IsDesignMode)
        {
            this.WhenActivated(action =>
            {
                ViewModel!.ShowSimulation.RegisterHandler(DoShowSimulation);
                ViewModel!.ShowRollVerifier.RegisterHandler(ShowRollVerifier);
                ViewModel!.ShowSettings.RegisterHandler(ShowSettings);
                ViewModel!.ShowBetHistory.RegisterHandler(ShowBetHistory);
                ViewModel!.ExitInteraction.RegisterHandler(Close);
                ViewModel!.ShowDialog.RegisterHandler(DoShowDialogAsync);
                ViewModel!.ShowAbout.RegisterHandler(ShowAbout);
                ViewModel!.ShowNotification.RegisterHandler(ShowNotification);
                ViewModel!.ShowUserInput.RegisterHandler(ShowUserInput);
                ViewModel.SaveFileInteraction.RegisterHandler(SaveFile);
                ViewModel.OpenFileInteraction.RegisterHandler(OpenFile);
                ViewModel.BrowserBypass.RegisterHandler(GetBypass);
                ViewModel.CFCaptchaBypass.RegisterHandler(CFCaptchaBypass);
                ViewModel.BrowserCancelInteraction.RegisterHandler(BrowserCancel);
                ViewModel.BrowserDoneInteraction.RegisterHandler(DoneClick);

            });
        }

        this.AttachedToVisualTree += OnAttachedToVisualTree;
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;

    }

    private async Task ShowUserInput(IInteractionContext<UserInputViewModel, Unit?> context)
    {
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow<UserInputViewModel> window = new();
        window.DataContext = context.Input;
        var dialog = new UserInputView();
        window.Content = dialog;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        window.ExtendClientAreaToDecorationsHint = true;
        window.Title = context.Input.Args.Prompt;
        dialog.DataContext = context.Input;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Title = $"User input";
        await window.ShowDialog(this.parentWindow);
        context.SetOutput(default);
    }

    private void ShowNotification(IInteractionContext<INotification, Unit?> context)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(() => ShowNotification(context));
            return;
        }

        try
        {
            notificationManager.Show(context.Input);
        }
        catch (Exception e)
        {

        }
    }

    private void ShowAbout(IInteractionContext<AboutViewModel, Unit?> context)
    {
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow<AboutViewModel> window = new();
        window.DataContext = context.Input;
        var dialog = new AboutView();
        window.Content = dialog;
        dialog.DataContext = context.Input;
        window.Width = 600;
        window.Height = 300;
        window.Title = $"About: Gambler.Bot";
        window.Show();
    }

    private void Close(IInteractionContext<Unit?, Unit?> context)
    {
        this.parentWindow.Close();
    }

    private void ShowBetHistory(IInteractionContext<BetHistoryViewModel, Unit?> context)
    {
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow<BetHistoryViewModel> window = new();
        window.DataContext = context.Input;
        var dialog = new BetHistoryView();
        window.Content = dialog;
        dialog.DataContext = context.Input;
        window.Width = 1400;
        window.Height = 700;
        window.Title = $"Bet History";
        window.Show();
    }

    private void InstanceView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Loaded();
    }

    private async Task DoShowDialogAsync(IInteractionContext<LoginViewModel,
                                        LoginViewModel?> interaction)
    {
        var dialog = new LoginView();
        dialog.DataContext = interaction.Input;
        var ParentWindow = this.FindAncestorOfType<Window>();
        //var result = await dialog.ShowDialog<LoginViewModel?>(ParentWindow);
        //interaction.SetOutput(result);
    }

    private async Task ShowRollVerifier(IInteractionContext<RollVerifierViewModel,
                                        Unit?> interaction)
    {
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow<RollVerifierViewModel> window = new();
        window.DataContext = interaction.Input;
        var dialog = new RollVerifierView();
        window.Content = dialog;
        dialog.DataContext = interaction.Input;
        window.Width = 500;
        window.Height = 500;
        window.Title = $"Roll Verifier - {interaction.Input.Site?.SiteName}";
        window.Show();
    }
    private async Task ShowSettings(IInteractionContext<GlobalSettingsViewModel,
                                        Unit?> interaction)
    {
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow<GlobalSettingsViewModel> window = new();
        window.DataContext = interaction.Input;
        var dialog = new GlobalSettingsView();
        window.Content = dialog;
        dialog.DataContext = interaction.Input;
        window.Width = 700;
        window.Height = 500;
        window.Title = $"Settings";
        window.Show();
    }

    private async Task DoShowSimulation(IInteractionContext<SimulationViewModel,
                                        SimulationViewModel?> interaction)
    {
        
        
        var ParentWindow = this.FindAncestorOfType<Window>();
        ReactiveWindow< SimulationViewModel> window = new();
        window.DataContext = interaction.Input;
        var dialog = new SimulationView();
        window.Content = dialog;
        dialog.DataContext = interaction.Input;
        window.Width = 800;
        window.Height = 450;
        window.Title=$"Simulation - {interaction.Input.Bot?.SiteName} - {interaction.Input.Bot.Strategy?.StrategyName}";
        window.Show();
    }
    private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        parentWindow = this.FindAncestorOfType<Window>();
        this.notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(this));
        if (parentWindow != null)
        {
            parentWindow.Closing += OnWindowClosing;
        }
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (parentWindow != null)
        {
            parentWindow.Closing -= OnWindowClosing;
        }
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Handle window closing logic here
        ViewModel.OnClosing();
    }

    private void Binding(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void OnMenuItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            if (menuItem.Icon is CheckBox checkBox)
            {
                // Toggle check state
                checkBox.IsChecked = !checkBox.IsChecked;
            }
            else if (menuItem.Icon is RadioButton radioButton)
            {
                // Select new radio button and other radio buttons
                // with same group name will be unselected
                radioButton.IsChecked = true;
            }
        }
           
	
    }

    private void Expander_Expanding(object? sender, Avalonia.Interactivity.CancelRoutedEventArgs e)
    {
        
    }

    private void Expander_Collapsing_1(object? sender, Avalonia.Interactivity.CancelRoutedEventArgs e)
    {
        if (e.Source == sender)
        {
            e.Cancel = true;
        }
    }
    async Task SaveFile(IInteractionContext<FilePickerSaveOptions, string?> interaction)
    {
        await IOHelper.SaveFile(interaction, TopLevel.GetTopLevel(this));
    }

    async Task OpenFile(IInteractionContext<FilePickerOpenOptions, string?> interaction)
    {
        await IOHelper.OpenFile(interaction, TopLevel.GetTopLevel(this));
    }

    private void Button_Click_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        cmbGrid.Clear();
    }
    
    private async void WvBypass_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!hasNavigated)
            hasNavigated = true;
        
    }
    
    Dictionary<string, string>? headers = new Dictionary<string, string>();
    string cookiesheader = "";
    private void WvBypass_WebResourceRequested(object? sender, WebResourceRequestedEventArgs e)
    {
        Debug.WriteLine(e.Request.Uri.ToString());
        if (args == null)
            return;
        if (e.Request.Uri.ToString().ToLower().Contains(args?.HeadersPath?.ToLower() ?? "ifhcf"))
        {
            headers.Clear();
            foreach (var x in e.Request.Headers)
            {
                headers[x.Key] = x.Value;
            }
            string requestCookies = e.Request.Headers.ContainsKey("cookie") ? e.Request.Headers["cookie"] : string.Empty;
            cookiesheader = requestCookies;
        }
        

    }
    private void WvBypass_NavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        
    }

    private void WvBypass_EnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        e.EnableDevTools = true;
    }

    private void WvBypass_AdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        
    }

    private void WvBypass_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        
    }

    private void cookietmrCallback(object? state)
    {
        if (args != null)
        {
            if ((DateTime.Now - LastRequest).TotalSeconds >= 2)
            {
                LastRequest = DateTime.Now;
                if (OperatingSystem.IsLinux() && dialog ==null)
                {
                    return;
                }
                
                CheckCookies();
            }
        }
    }
    Dictionary<string,Cookie> cookies;
    
    private void WvBypass_Loaded(object? sender, RoutedEventArgs e)
    {
       
    }

    private void WvBypass_WebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        
    }
    static string agent = "";
    async Task GetAgent()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await GetAgent());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(agent))
            {
                if (OperatingSystem.IsLinux())
                {
                    agent = await dialog.InvokeScript("navigator.userAgent");
                }
                else
                {
                    agent = await wvBypass.InvokeScript("navigator.userAgent");
                }
                if (agent == null)
                    return;
                if (agent.StartsWith("\\"))
                    agent = agent.Substring(1);
                if (agent.EndsWith("\\"))
                    agent = agent.Substring(0, agent.Length - 1);
                if (agent.StartsWith("\""))
                    agent = agent.Substring(1);
                if (agent.EndsWith("\""))
                    agent = agent.Substring(0, agent.Length - 1);
            }
        }
    }
   async Task CheckCookies()
    {
        if (checking)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await CheckCookies());
            return;
        }
       
        checking = true;

        if (args == null || _conf != null)
            return;
        var bc = new BrowserConfig();
        CookieContainer cs = new CookieContainer();
        Uri cururi = new Uri(args.URL);
        bool found = false;
        try
        {

            await GetAgent();
            string result = agent;
            NativeWebViewCookieManager Cookiemanager = null;
            if (OperatingSystem.IsLinux())
            {
                if (dialog == null)
                    return;
                Cookiemanager = dialog.TryGetCookieManager();
            }
            else
            {
                Cookiemanager = wvBypass.TryGetCookieManager();
            }
            
            if (Cookiemanager == null)
                return;
            var cookies = await Cookiemanager.GetCookiesAsync();
            //cs.SetCookies(cururi, cookiesheader);
            HashSet<string> foundcookies = new HashSet<string>();
            foreach (var cookie in cookies)
            {
                try
                {
                    //if (cookie.Domain.ToLower().Contains(cururi.Host.ToLower()))
                    { 
                        cs.Add(cookie);
                        if (args.RequiredCookies.Contains( cookie.Name) && cookie.Domain.ToLower().Contains(cururi.Host.ToLower()))
                        {
                            if (!foundcookies.Contains(cookie.Name))
                            {
                                foundcookies.Add(cookie.Name);
                            }
                            
                        }
                    }
                }
                catch (Exception ex)
                {

                }
                
            }
            

            bc.UserAgent = agent;
            bc.Cookies = cs;
            if (closeClicked && !string.IsNullOrEmpty(args.PostNavScript ))
            {
                await this.internalExecScript(args.PostNavScript);
               
                bc.scriptResponse = scriptresult;
            }
            if ((foundcookies.Count >= args.RequiredCookies.Length 
                 || (!string.IsNullOrEmpty(args.PostNavScript) && !string.IsNullOrEmpty(bc.scriptResponse))
                 || args.HasTimeout&&(DateTime.Now-startDate).TotalSeconds>15) || cts.IsCancellationRequested)
                _conf = bc;


            
        }
        catch (Exception e)
        {

        }
        finally
        {
            checking = false;
        }
    }

    static CancellationTokenSource cts;

    static BypassRequiredArgs args = null;
    internal async Task internalGetBypass(BypassRequiredArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                headers.Clear();
                cookiesheader = string.Empty;
                cookies = new Dictionary<string, Cookie>();
                
                lblDisclaimer.IsVisible = true;
                btnDoneBrowser.IsVisible = true;
                btnCancelBrowser.IsVisible = true;
                this.closeClicked = false;
                
                lblDisclaimer.ZIndex =- 2;

                if (OperatingSystem.IsLinux())
                {
                    var url = new Uri(e.URL);
                    dialog = new NativeWebDialog
                    {
                        Source = url
                    };
                    dialog.Resize(400, 500);
                    dialog.Show();
                    dialog.NavigationCompleted += WvBypass_NavigationCompleted;
                    dialog.WebMessageReceived += WvBypass_WebMessageReceived; ;                    
                    dialog.AdapterCreated += WvBypass_AdapterCreated; ;
                    dialog.EnvironmentRequested += WvBypass_EnvironmentRequested;
                    dialog.NavigationStarted += WvBypass_NavigationStarted;
                    dialog.WebResourceRequested += WvBypass_WebResourceRequested;
                    
                    if (e.HasTimeout)
                    {
                        try
                        {
                            await Task.Delay(15000, cts.Token);
                        }
                        catch (Exception ex)
                        {

                        }
                        if (_conf == null)
                        {
                            cts.Cancel();
                            await CheckCookies();
                        }
                        dialog.Close();
                        dialog.NavigationCompleted -= WvBypass_NavigationCompleted;
                        dialog.WebMessageReceived -= WvBypass_WebMessageReceived; ;
                        dialog.AdapterCreated -= WvBypass_AdapterCreated; ;
                        dialog.EnvironmentRequested -= WvBypass_EnvironmentRequested;
                        dialog.NavigationStarted -= WvBypass_NavigationStarted;
                        dialog.WebResourceRequested -= WvBypass_WebResourceRequested;
                        dialog = null;
                        lblDisclaimer.IsVisible = false;
                        btnCancelBrowser.IsVisible = false;
                        btnDoneBrowser.IsVisible = false;

                    }
                }
                else
                {
                    
                    //wvBypass.UpdateLayout();
                    tabs.SelectedIndex = 1;
                    wvBypass.Navigate(new Uri(e.URL));
                    if (e.HasTimeout)
                    {
                        lblDisclaimer.Text = "Please wait a moment. You do not need to log in to the website below, but please solve any captchas if there are any.";
                        try
                        {
                            await Task.Delay(15000, cts.Token);
                        }
                        catch (Exception ex)
                        {

                        }
                        if (_conf == null)
                        {
                            cts.Cancel();
                            await CheckCookies();
                        }
                       // wvBypass.Navigate(new Uri("about:blank"));
                        
                        lblDisclaimer.IsVisible = false;
                        btnCancelBrowser.IsVisible = false;
                        btnDoneBrowser.IsVisible = false;
                        tabs.SelectedIndex = 0;
                    }
                    else
                    {
                        lblDisclaimer.Text = "Please log in to the site below.";
                    }
                }
            }
            catch (Exception ex)
            {

            }
        });
    }
    bool scriptrunning = false;
    async Task internalCaptchaBypass(string script)
    {
        scriptrunning = true;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            lblDisclaimer.IsVisible = true;
            btnCancelBrowser.IsVisible = true;
            btnDoneBrowser.IsVisible = true;
            this.closeClicked = false;
            tabs.SelectedIndex = 1;
            lblDisclaimer.ZIndex = -2;
            try
            {
                
                
                string result = await wvBypass.InvokeScript("console.log('start');");
                result = await wvBypass.InvokeScript("document.open();");
                result = await wvBypass.InvokeScript($"document.write('{script.Replace("'", "\\'").Replace("\r", "").Replace("\n", "")}');");
                result = await wvBypass.InvokeScript("document.close();");
                result = await wvBypass.InvokeScript("console.log('finish');");
            
            }
            catch (Exception ex)
            {

            }
            scriptrunning = false;
        });
    }
    string scriptresult = "";
    internal async Task internalExecScript(string script)
    {
        scriptrunning = true;
        scriptresult = "";
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            lblDisclaimer.IsVisible = true;
            btnCancelBrowser.IsVisible = true;
            btnDoneBrowser.IsVisible = true;
            this.closeClicked = false;
            lblDisclaimer.ZIndex = -2;
            try
            {
               

                scriptresult = await wvBypass.InvokeScript(script);
            }
            catch (Exception ex)
            {

            }
            scriptrunning = false;
        });
    }
    internal void closeBrowser()
    {

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                //wvBypass.Navigate(new Uri("about:blank"));
                lblDisclaimer.IsVisible = false;
                btnCancelBrowser.IsVisible = false;
                btnDoneBrowser.IsVisible = false;
                tabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {

            }
        });
    }
    static DateTime startDate = default;
    //IInteractionContext<FilePickerOpenOptions, string?> interaction
    internal async Task GetBypass(IInteractionContext<BypassRequiredArgs, BrowserConfig> interaction)
    {
        cancelled = false;
        cts = new CancellationTokenSource();
        _conf = null;
        args = interaction.Input;
        startDate = DateTime.Now;
        LastRequest = DateTime.Now;
        await internalGetBypass(args);
        closeBrowser();
        args = null;
        cts.Cancel();
        _conf.Headers = headers;
        interaction.SetOutput(_conf);
    }

    static bool hasNavigated = false;
    async Task CFCaptchaBypass(IInteractionContext<string, Unit?> interaction)
    {
        hasNavigated = false;
        cancelled = false;
        await internalCaptchaBypass(interaction.Input);
        DateTime start = DateTime.Now;
        await Task.Run(() =>
        {
            while ((scriptrunning|| !hasNavigated) && !cancelled) 
            { 
                Thread.Sleep(100);
            }
        });
        closeBrowser();
    }

    async Task BrowserCancel(IInteractionContext<Unit?, Unit?> interaction)
    {
        cancelled = true;
    }

    async Task DoneClick(IInteractionContext<Unit?, Unit?> interaction)
    {
        closeClicked = true;
        await CheckCookies();
        closeBrowser();
    }
}