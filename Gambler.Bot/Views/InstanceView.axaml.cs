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
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Gambler.Bot.Classes;

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
    /// Creates and hosts the Browser tab's <see cref="NativeWebView"/> and injects it into the
    /// browser owned by the viewmodel. Non-Linux only (Linux uses a per-session dialog); the
    /// <c>_browserUiSetup</c> guard keeps the attach one-shot.
    /// </summary>
    private void SetupBrowserUi()
    {
        if (_browserUiSetup)
            return;
        _browserUiSetup = true;

        if (OperatingSystem.IsLinux())
            return;

        var webview = new NativeWebView();
        webview.BeginInit();
        layoutgrd.Children.Add(webview);
        Grid.SetRow(webview, 1);
        webview.EndInit();
        ViewModel.AttachWebView(webview);
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
