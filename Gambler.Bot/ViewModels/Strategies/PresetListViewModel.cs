using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.Strategies.Strategies.PresetListModels;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.ViewModels.Games.Dice;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Gambler.Bot.Common.Games;

namespace Gambler.Bot.ViewModels.Strategies
{
    public class PresetListViewModel : ViewModelBase, IStrategy
    {
        public string[] Options { get; set; } = new string[] { "Step","Reset","Stop" };
        
        public string OnLossAction 
        { 
            get=>Strategy.LossAction; set 
            {
                Strategy.LossAction = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(ShowLossSteps));
            } 
        }
        public string OnWinAction
        {
            get => Strategy.WinAction; set
            {
                Strategy.WinAction = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(ShowWinSteps));
            }
        }
        public string OnFinishAction
        {
            get => Strategy.EndAction; set
            {
                Strategy.EndAction = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(ShowFinishSteps));
            }
        }

        public bool ShowLossSteps { get => OnLossAction=="Step"; }
        public bool ShowWinSteps { get => OnWinAction == "Step"; }
        public bool ShowFinishSteps { get => OnFinishAction == "Step"; }

        private PresetList _strategy;

        public PresetList Strategy
        {
            get { return _strategy; }
            set { _strategy = value; this.RaisePropertyChanged(); }
        }
        private Bot.Common.Games.Games _game;

        private ObservableCollection<PresetDiceBet> _betList;

        public ObservableCollection<PresetDiceBet> BetList
        {
            get { return _betList; }
            set { _betList = value; }
        }

        public Bot.Common.Games.Games Game
        {
            get { return _game; }
            set { _game = value; this.RaisePropertyChanged(); }
        }
        private int _selectedIndex;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { _selectedIndex = value; }
        }

        public PresetListViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            MoveUpCommand = ReactiveCommand.Create(MoveUp);
            MoveDownCommand = ReactiveCommand.Create(MoveDown);
            RemoveCommand = ReactiveCommand.Create(Remove);
            AddCommand = ReactiveCommand.Create(Add);
            OpenCommand = ReactiveCommand.Create(Open);
            SaveCommand = ReactiveCommand.Create(Save);
        }

        public ICommand AddCommand { get; set; }
        void Add()
        {
            int insertindex = SelectedIndex + 1;
            if (insertindex >= BetList.Count)
                BetList.Add(new PresetDiceBet { Amount = 0 });
            else
                BetList.Insert(SelectedIndex + 1, new PresetDiceBet { Amount = 0 });
        }

        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
           
        }

        private void Notify2_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;
            object value = sender.GetType().GetProperty(e.PropertyName).GetValue(sender);
            switch (e.PropertyName)
            {
                case "Amount":
                    Strategy.Amount = (decimal)value;
                    break;
                case "Chance":
                    Strategy.Chance = (decimal)value;
                    break;
            }
        }

        public void SetStrategy(BaseStrategy Strategy)
        {
            if (Strategy == null)
                throw new ArgumentNullException();
            if (!(Strategy is PresetList mart))
                throw new ArgumentException("Must be martingale to use thise viewmodel");

            this.Strategy = mart;
            BetList = new ObservableCollection<PresetDiceBet>(mart.PresetBets);
           
        }
        public void Saving()
        {
            Strategy.PresetBets = new BindingList<PresetDiceBet>(BetList);
        }
        public bool TopAlign()
        {
            return true;
        }

        public ICommand MoveUpCommand { get; set; }
        void MoveUp()
        {
            if (SelectedIndex > 0)
            {
                int tmpindex = SelectedIndex;
                PresetDiceBet tmp = BetList[tmpindex - 1];
                BetList[tmpindex - 1] = BetList[tmpindex];
                BetList[tmpindex] = tmp;
                SelectedIndex = tmpindex-1;
            }
        }

        public ICommand MoveDownCommand { get; set; }
        void MoveDown()
        {
            if (SelectedIndex < BetList.Count - 1)
            {
                int tmpindex = SelectedIndex;
                PresetDiceBet tmp = BetList[tmpindex];
                BetList[tmpindex] = BetList[tmpindex + 1];
                BetList[tmpindex + 1] = tmp;
                SelectedIndex = tmpindex+1;
            }
        }

        public ICommand RemoveCommand { get; set; }
        void Remove()
        {
            BetList.RemoveAt(SelectedIndex);
        }

        public ICommand OpenCommand { get; set; }
        void Open()
        {

        }

        public ICommand SaveCommand { get; set; }
        void Save()
        {

        }
        public void Dispose()
        {
            
        }
    }
}
