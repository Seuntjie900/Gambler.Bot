using Acornima.Ast;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Azure.Core;
using Gambler.Bot.Core.Events;
using Gambler.Bot.Core.Helpers;
using Gambler.Bot.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gambler.Bot.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    private Window parentWindow;
    static MainView _instance;
    bool subscribed = false;
    string cookievalue = "";
    static DateTime LastRequest = DateTime.Now;
    Timer timer;
    public NativeWebView wvBypass { get; set; }
    NativeWebDialog dialog = null;
    static bool cancelled = false;
    public MainView()
    {
        InitializeComponent();
        _instance=this;
        if (!OperatingSystem.IsLinux())
        {
            wvBypass = new NativeWebView();
            wvBypass.BeginInit();
            layoutgrd.Children.Add(wvBypass);
            Grid.SetRow(wvBypass, 1);
            wvBypass.IsVisible = false;
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
        
        
        //wvBypass.WebViewCreated += WvBypass_WebViewCreated;
        //PART_WebView.
        this.AttachedToVisualTree += OnAttachedToVisualTree;
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        timer = new Timer(cookietmrCallback, null, 1000, 1000);
        
    }

    public static Task<string?> ExecJS(string js)
    {
        return _instance.wvBypass.InvokeScript(js);
    }
    
    Dictionary<string, string>? headers = new Dictionary<string, string>();
    string cookiesheader = "";
    private void WvBypass_WebResourceRequested(object? sender, WebResourceRequestedEventArgs e)
    {
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

    private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
         parentWindow = this.FindAncestorOfType<Window>();
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
        ViewModel.OnClosing();
    }
    private async void WvBypass_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!hasNavigated)
            hasNavigated = true;
        
    }

    public async void ClickHandler(object sender, RoutedEventArgs args)
    {
        

    }

    static string agent = "";
    static async Task GetAgent()
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
                    agent = await _instance.dialog.InvokeScript("navigator.userAgent");
                }
                else
                {
                    agent = await _instance.wvBypass.InvokeScript("navigator.userAgent");
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

    static BrowserConfig _conf = null;
    bool checking = false;


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
            if ((foundcookies.Count >= args.RequiredCookies.Length && args.HasTimeout||(DateTime.Now-startDate).TotalSeconds>15) || cts.IsCancellationRequested)
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
    internal void internalGetBypass(BypassRequiredArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                headers.Clear();
                cookiesheader = string.Empty;
                cookies = new Dictionary<string, Cookie>();
                instance.IsVisible = false;
                lblDisclaimer.IsVisible = true;
                btnCancelBrowser.IsVisible = true;
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
                        instance.IsVisible = true;
                    }
                }
                else
                {
                    wvBypass.ZIndex = -1;
                    wvBypass.IsVisible = true;
                    instance.IsVisible = false;
                    //wvBypass.UpdateLayout();
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
                        wvBypass.IsVisible = false;
                        lblDisclaimer.IsVisible = false;
                        btnCancelBrowser.IsVisible = false;
                        instance.IsVisible = true;
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
    internal void internalCaptchaBypass(string script)
    {
        scriptrunning = true;
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            lblDisclaimer.IsVisible = true;
            btnCancelBrowser.IsVisible = true;
            lblDisclaimer.ZIndex = -2;
            instance.IsVisible = false;
            try
            {
                wvBypass.IsVisible = true;
                
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

    internal void closeBrowser()
    {

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                //wvBypass.Navigate(new Uri("about:blank"));
                wvBypass.IsVisible = false;
                lblDisclaimer.IsVisible = false;
                btnCancelBrowser.IsVisible = false;
                instance.IsVisible = true;
            }
            catch (Exception ex)
            {

            }
        });
    }

    static DateTime startDate = default;
    internal static BrowserConfig GetBypass(BypassRequiredArgs e)
    {
        cancelled = false;
        cts = new CancellationTokenSource();
        _conf = null;
        args = e;
        startDate = DateTime.Now;
        _instance.internalGetBypass(e);
        LastRequest = DateTime.Now;
        while (_conf == null) { Thread.Sleep(100); }
        _instance.closeBrowser();
        args = null;
        cts.Cancel();
        _conf.Headers = _instance.headers;
        return _conf;
    }

    static bool hasNavigated = false;
    internal static void CFCaptchaBypass(string script)
    {
        hasNavigated = false;
        cancelled = false;
        _instance.internalCaptchaBypass(script);
        DateTime start = DateTime.Now;
        while ((_instance.scriptrunning|| !hasNavigated) && !cancelled) 
        { 
            Thread.Sleep(100);
        }
        _instance.closeBrowser();
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        cancelled = true;
    }

    
}
public static class ExtensionMethods
{
    public static async Task<object> InvokeAsync(this MethodInfo @this, object obj, params object[] parameters)
    {
        var task = (Task)@this.Invoke(obj, parameters);
        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty.GetValue(task);
    }
}