using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.ViewModels.Games.Dice;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Gambler.Bot.Common.Games;

namespace Gambler.Bot.ViewModels.Strategies
{
    internal class LabouchereViewModel:ViewModelBase, IStrategy
    {
		private Labouchere _strategy;

		public Labouchere Strategy
		{
			get { return _strategy; }
			set { _strategy = value; this.RaisePropertyChanged(); }
		}
        private Bot.Common.Games.Games _game;

        private iPlaceBet _placeBetVM;

        public iPlaceBet PlaceBetVM
        {
            get { return _placeBetVM; }
            set { _placeBetVM = value; SyncStartControl(); this.RaisePropertyChanged(); }
        }

        private ObservableCollection<LabListItem> _betList;

        public ObservableCollection<LabListItem> BetList
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


        public LabouchereViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            MoveUpCommand = ReactiveCommand.Create(MoveUp);
            MoveDownCommand = ReactiveCommand.Create(MoveDown);
            RemoveCommand = ReactiveCommand.Create(Remove);
            AddCommand = ReactiveCommand.Create(Add);
            OpenCommand = ReactiveCommand.Create(Open);
            SaveCommand = ReactiveCommand.Create(Save);
        }

        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= Notify2_PropertyChanged;
            }
            Game = newGame;
            switch (Game)
            {
                case Bot.Common.Games.Games.Dice: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowToggle = true }; break;
                default: PlaceBetVM = null; break;
            }
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify2)
            {
                notify2.PropertyChanged += Notify2_PropertyChanged;
            }
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
            if (!(Strategy is Labouchere mart))
                throw new ArgumentException("Must be martingale to use thise viewmodel");

            this.Strategy = mart;
            SyncStartControl();
            BetList = new ObservableCollection<LabListItem>(mart.BetList.Select(x=> new LabListItem(_logger) { Item=x }));
        }
        public void Saving()
        {
            Strategy.BetList = BetList.Select(x=>x.Item).ToList();
        }
        public bool TopAlign()
        {
            return true;
        }
        void SyncStartControl()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                //dice.Amount = Strategy.MinBet;
                dice.Chance = Strategy.Chance;
                dice.ShowAmount = false;
            }
        }

        public ICommand MoveUpCommand { get; set; }
        void MoveUp()
        {
            if (SelectedIndex >0)
            {
                int tmpindex = SelectedIndex;
                LabListItem tmp = BetList[tmpindex - 1];
                BetList[tmpindex - 1] = BetList[tmpindex];
                BetList[tmpindex] = tmp;
                SelectedIndex = tmpindex;
            }
        }

        public ICommand MoveDownCommand { get; set; }
        void MoveDown()
        {
            if (SelectedIndex<BetList.Count-1)
            {
                int tmpindex = SelectedIndex;
                LabListItem tmp = BetList[tmpindex];
                BetList[tmpindex] = BetList[tmpindex + 1];
                BetList[tmpindex + 1] = tmp;
                SelectedIndex = tmpindex;
            }
        }

        public ICommand RemoveCommand { get; set; }
        void Remove()
        {
            BetList.RemoveAt(SelectedIndex);
        }

        public ICommand AddCommand { get; set; }
        void Add()
        {
            int insertindex = SelectedIndex + 1;
            if (insertindex >= BetList.Count)
                BetList.Add(new LabListItem(_logger) { Item = 0 });
            else
                BetList.Insert(SelectedIndex+1, new LabListItem(_logger) { Item = 0 });
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
            _placeBetVM = null;
        }
    }

    public class LabListItem:ViewModelBase
    {
        public LabListItem(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            
        }
        private decimal item;

        public decimal Item
        {
            get { return item; }
            set { item = value; this.RaisePropertyChanged(); }
        }

    }
}
