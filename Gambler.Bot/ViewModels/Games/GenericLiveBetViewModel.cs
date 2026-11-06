using Avalonia.Threading;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes;
using Gambler.Bot.Common.Games.Dice;
using Gambler.Bot.Common.Games;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gambler.Bot.ViewModels.Games
{
    internal class GenericLiveBetViewModel : ViewModelBase, iLiveBet
    {
        public event EventHandler<ViewBetEventArgs> BetClicked;
        public ObservableCollection<Bet> Bets { get; set; } = new ObservableCollection<Bet>();

        public GenericLiveBetViewModel(ILogger logger) : base(logger)
        {

        }

        public void AddBet(Bet newBet)
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {


                    //if (Bets.CanRemove)
                    while (Bets.Count > UISettings.Settings.LiveBets + 1)
                    {

                        //System.Threading.Thread.Sleep(10);
                        //Bets.Remove(Bets.list);
                        Bets.RemoveAt(Bets.Count - 1);
                    }

                    Bets.Insert(0, newBet);
                }
                else
                {
                    if (UISettings.Settings.AsyncUIUpdates)
                        Dispatcher.UIThread.Post(() => AddBet(newBet));
                    else
                        Dispatcher.UIThread.Invoke(() => AddBet(newBet));
                }
            }
            catch (Exception e)
            {
                _logger?.LogError(e.ToString());
            }

        }
    }
}