using Avalonia.Threading;
using Gambler.Bot.Classes;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.Dice;
using Gambler.Bot.Common.Games.Limbo;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Gambler.Bot.ViewModels.Games.Limbo
{
    internal class LimboLiveBetViewModel : ViewModelBase, iLiveBet
    {
        public event EventHandler<ViewBetEventArgs> BetClicked;
        public ObservableCollection<LimboBet> Bets { get; set; } = new ObservableCollection<LimboBet>();
        
        public LimboLiveBetViewModel(ILogger logger):base(logger)
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

                    Bets.Insert(0, newBet as LimboBet);
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
