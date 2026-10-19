using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gambler.Bot.Classes.SiteBrowser;
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
    private bool _browserUiSetup;

    public InstanceView()
    {
        InitializeComponent();
        Loaded += InstanceView_Loaded;
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
                SetupBrowserUi();
            });
        }

        this.AttachedToVisualTree += OnAttachedToVisualTree;
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;

    }

    /// <summary>
    /// Wires the Browser tab UI to the <see cref="SiteBrowser"/> owned by the viewmodel:
    /// hosts the webview, tracks bypass/captcha events, and triggers the Done/Cancel
    /// interactions.
    /// </summary>
    private void SetupBrowserUi()
    {
        if (_browserUiSetup)
            return;
        _browserUiSetup = true;

        var browser = ViewModel.SiteBrowser;
        if (browser.WebView != null)
        {
            browser.WebView.BeginInit();
            layoutgrd.Children.Add(browser.WebView);
            Grid.SetRow(browser.WebView, 1);
            browser.WebView.EndInit();
        }
        browser.BypassStarted += Browser_BypassStarted;
        browser.BypassFinished += Browser_BypassFinished;
        browser.CaptchaStarting += Browser_CaptchaStarting;
        browser.CaptchaFinished += Browser_CaptchaFinished;
        btnCancelBrowser.Click += (s, e) => _ = ViewModel.BrowserCancelInteraction.Handle(null);
        btnDoneBrowser.Click += (s, e) => _ = ViewModel.BrowserDoneInteraction.Handle(null);
    }

    private void Browser_BypassStarted(object sender, BypassStartedEventArgs e)
    {
        RunOnUi(() =>
        {
            lblDisclaimer.IsVisible = true;
            lblDisclaimer.ZIndex = -2;
            lblDisclaimer.Text = e.Message;
            btnCancelBrowser.IsVisible = true;
            btnDoneBrowser.IsVisible = true;
            if (!OperatingSystem.IsLinux())
                TabMenu.SelectedIndex = 1;
        });
    }

    private void Browser_BypassFinished(object sender, BrowserConfig e)
    {
        RunOnUi(() =>
        {
            lblDisclaimer.IsVisible = false;
            btnCancelBrowser.IsVisible = false;
            btnDoneBrowser.IsVisible = false;
            if (!OperatingSystem.IsLinux())
                TabMenu.SelectedIndex = 0;
        });
    }

    private void Browser_CaptchaStarting(object sender, EventArgs e)
    {
        RunOnUi(() =>
        {
            lblDisclaimer.IsVisible = true;
            lblDisclaimer.ZIndex = -2;
            btnCancelBrowser.IsVisible = true;
            btnDoneBrowser.IsVisible = true;
            if (!OperatingSystem.IsLinux())
                TabMenu.SelectedIndex = 1;
        });
    }

    private void Browser_CaptchaFinished(object sender, EventArgs e)
    {
        RunOnUi(() =>
        {
            lblDisclaimer.IsVisible = false;
            btnCancelBrowser.IsVisible = false;
            btnDoneBrowser.IsVisible = false;
            if (!OperatingSystem.IsLinux())
                TabMenu.SelectedIndex = 0;
        });
    }

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
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
}
