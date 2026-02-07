### Betting Functionality
- [x] *reset stats
- [x] *login check for specific cookie and immediately proceed. seems to work for now
- [x] *custom triggers
- [x] *programmer mode error handler (kind of done i think)
- [x] *stop simulation
- [x] *update stats window on timer if bot is running but stalled
- [x] *Indicate but running status in status bar/title bar
- [x] Multi game support
- [x] *Bank, (still needs to be added and tested for Bitsler, WinDice, WolfBet, DuckDice but waiting for feedback from sites.)
- [ ] *Finish crash for ethercrash
- [ ] Add extra games - _Twist_, _Boom_, keno, plinko, roulette, hilo
- [x] Bet settings need to get the game settings to accomodate edge etc
- [x] Move browser to tab in instance view so each instance can have its own browser.
- [ ] fix browser login
- [ ] Add browser based integration/betting for when CF is being dumb

	
### Tabs
- [x] *Console (still needs read methods ???? )

### Windows
- [x] *settings import/export
- [ ] additional charts (streak occurance? roll distrobution? win/loss ratio? )
- [x] *roll verifier
- [x] *bet history


### General
- [x] *notifications (only in app notifications that kind of go over the stop button, but sounds working. need to add an option to move audio sounds again.)
- [x] *Prettify UI a bit with icons and loaders where possible
- [x] *Auto updates	
- [ ] Improve logging - adding debug and info logging where needed, ensure exceptions are logged and log warnings where relevant. Include logging level option either in config or settings
- [x] *triggers for notifications (This seems to work but still having the weird issue on the settings screen.)
- [ ] *improve programmer mode documentation
- [x] *manual betting extra buttons (x2, 1/2, max, min, etc)
- [ ] multiple tabs
- [ ] proxy
- [ ] Telemetry (???The stats could be tremendously important but need to be really carefull about what is logged and how.???)
- [ ] CLI interface? old one should still be somewhere in the github repo, maybe compile a secondary exe for it? Should it be an interface or only accept command line arguments in run without a UI?
- [ ] Include site name in charts and titles?
- [x] *disable/enable start/stop/resume/stoponwin based on bot state
- [x] option to run simulation without bet history
- [ ] Simulator for other games
- [ ] verifiers for other games
- [x] Chart for simulator
- [ ] Enable chart animation for normal bets but disable for simulation
- [ ] Bet history for other kinds of bets

### Sites
- [x] *wolfbet
- [ ] **BCH.game

### Help windows:
- [ ] Setup wizard
- [x] *Reset to default
- [x] *source code (link to githuib repo)
- [x] *tutorials (link)
- [x] *FAQ (link)
- [x] *Conact (link)
- [x] *About

### Settings: 
- [ ] *language localization (functionality mostly in place, but strings file not yet created)
- [x] *Errors
- [ ] Donate
- [ ] Proxy
- [ ] Telemetry
- [ ] storage strategies
- [x] *notifications
- [x] *Updates (I think this is basically done)
- [x] *Live bets
- [x] *skins
- [x] *storage: bets


### Known issues
- [ ] *resume does not
- [x] *Selected theme doesn't show in settings
- [x] *Some sites are not remembered but others are
- [x] *Disconnecting/logging out does not
- [x] *browser bypass sometimes only works on second/third attempt
- [x] *Login screen values are not cleared. Or rather they are cleared but UI not updating? or something.
- [x] *Bet storage!
- [x] *Manual bets not added to chart/history/storage probably
- [x] *Advanced settings dropdowns not working
- [x] *Programmer mode templates and testing after multi game changes
- [x] *Lable colours not setting correctly
- [x] *Betamounts not set correctly when changing strategies
- [x] *Retry Bet not restarting bet loop - errors also won't...
- [ ] *Double check site settings
- [ ] *reset seed caused betting to stop (this probably just needs to implemented on all of the sites?)
- [x] *SiteDetails and SiteStats class are not defined before until we do a first bet
- [x] *Add a new boolean variable that indicates if script is running in simulator
- [x] *Balance variable is not updated in programmer mode
- [x] *Sqlite update tables needs to be implemented (I have the EF migrations but not quite sure if that's going to work.... meh)
- [x] *Script conversion tool /legacy script support (untested but there for lua)
- [x] *bot speed
- [ ] ???Specify system requirements in the readme file???
- [ ] charting line colours
- [x] *Programmer mode crash when file dialog cancelled
