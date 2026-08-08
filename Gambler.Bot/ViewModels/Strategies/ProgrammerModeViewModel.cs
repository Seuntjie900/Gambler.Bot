using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.Classes.Strategies;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Gambler.Bot.Common.Games;

namespace Gambler.Bot.ViewModels.Strategies
{
    public class ProgrammerModeViewModel : ViewModelBase, IStrategy
    {
        private readonly Interaction<FilePickerSaveOptions, string> saveFile;
        public Interaction<FilePickerSaveOptions, string> SaveFileInteraction => saveFile;

        private readonly Interaction<FilePickerOpenOptions, string> openFile;
        public Interaction<FilePickerOpenOptions, string> OpenFileInteraction => openFile;

        public string fileName { get=>Strat?.FileName; set{ Strat.FileName = value; this.RaisePropertyChanged(); } }

        private string _content;

        public string Content
        {
            get { return _content; }
            set { _content = value; this.RaisePropertyChanged(); }
        }

        public string FileExtension { get; protected set; } = "lua";
        public string LanguageName { get; protected set; } = "LUA";

        public string TemplateName { get; protected set; } = "LUATemplate.lua";

        public IProgrammerMode Strat { get; private set; }
        DateTime LastChanged = DateTime.Now;
        FileSystemWatcher FileWatcher;
        private readonly ILogger _logger;

        public ProgrammerModeViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            _logger = logger;
            saveFile = new Interaction<FilePickerSaveOptions, string>();
            openFile = new Interaction<FilePickerOpenOptions, string>();
            NewCommand = ReactiveCommand.Create(New);
            OpenCommand = ReactiveCommand.Create(Open);
            SaveCommand = ReactiveCommand.Create(Save);
            SaveAsCommand = ReactiveCommand.Create(SaveAs);

            SetLanguage();
            CreateWatcher();
        }

        protected virtual void SetLanguage()
        {
            FileExtension = "lua";
            LanguageName = "LUA";
            TemplateName = "LUATemplate.lua";
        }
        void CreateWatcher()
        {
            if (FileWatcher != null)
            {
                FileWatcher.Dispose();
            }
            if (Strat != null && File.Exists(Strat.FileName))
            {
                FileWatcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(Strat.FileName), System.IO.Path.GetFileName(Strat.FileName));
                FileWatcher.Changed += FileWatcher_Changed;
                FileWatcher.EnableRaisingEvents = true;
            }
        }
        bool saving = false;
        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                if (!saving)
                    LoadDocument();
            }
        }
        void LoadDocument()
        {
            if (!Dispatcher.UIThread.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Dispatcher.UIThread.Invoke(LoadDocument);
                return;
            }
            if (Strat != null)
            {
                string path = Strat.FileName;
                
                try
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gambler.Bot", $"default.{FileExtension}");
                        if (!File.Exists(path))
                            CreateNewScript(path);
                    }
                    Content = File.ReadAllText(path);
                    Strat.FileName = path;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e.ToString());
                }
                this.RaisePropertyChanged(nameof(fileName));
            }
        }

        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
            
        }

        public void Saving()
        {
            Save();
        }

       

        public void SetStrategy(BaseStrategy Strategy)
        {
            Strat = Strategy as IProgrammerMode;
            LoadDocument();
            CreateWatcher();
        }

        public bool TopAlign()
        {
            return false;
        }

        public ICommand NewCommand { get; set; }
        async Task New()
        {
            var result =await SaveFileInteraction.Handle(new FilePickerSaveOptions 
            { 
                DefaultExtension= FileExtension, 
                ShowOverwritePrompt=true ,
                 FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType(LanguageName) { Patterns= new List<string>() { $"*.{FileExtension}" } } },
                 Title="Save Script",
                  SuggestedFileName = $"NewScript.{FileExtension}"
            
            });
            
            if (result!=null)
            {
                string FileName = result;
                CreateNewScript(FileName);
                LoadDocument();
                CreateWatcher();
            }
        }

        void CreateNewScript(string FileName)
        {
            Stream x = typeof(BaseStrategy).Assembly.GetManifestResourceStream($"Gambler.Bot.Strategies.Samples.{TemplateName}");
            string[] items = typeof(BaseStrategy).Assembly.GetManifestResourceNames();
            byte[] buffer = new byte[x.Length];
            x.Read(buffer, 0, (int)x.Length);
            try
            {
                File.WriteAllBytes(FileName, buffer);
            }
            catch (Exception e)
            {
                _logger?.LogError(e.ToString());
            }
            Strat.FileName = FileName;
            
        }

        public ICommand OpenCommand { get; set; }
        async Task Open()
        {
            var result = await OpenFileInteraction.Handle(new FilePickerOpenOptions
            {   
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType(LanguageName) { Patterns = new List<string>() { $"*.{FileExtension}" } } },
                Title = "Save Script",
            });

            if (File.Exists(result))
            {
                Strat.FileName = result;
                LoadDocument();
                CreateWatcher();
            }
        }

        public ICommand SaveCommand { get; set; }
        void Save()
        {
            
            try
            {
                saving = true;
                File.WriteAllText(Strat.FileName, Content);
            }
            catch (Exception e)
            {
                _logger?.LogError(e.ToString());
            }
            finally
            {
                saving = false;
            }
        }

        public ICommand SaveAsCommand { get; set; }
        async Task SaveAs()
        {
            var result = await SaveFileInteraction.Handle(new FilePickerSaveOptions
            {
                DefaultExtension = FileExtension,
                ShowOverwritePrompt = true,
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType(LanguageName) { Patterns = new List<string>() { $"*.{FileExtension}" } } },
                Title = "Save Script",
                SuggestedFileName = $"NewScript.{FileExtension}"

            });
            if (result == null)
                return;
            Strat.FileName= result;
            try
            {
                saving = true;
                File.WriteAllText(Strat.FileName, Content);
            }
            catch (Exception e)
            {
                _logger?.LogError(e.ToString());
            }
            finally
            {
                saving = false;
            }
        }

        public void Dispose()
        {
            if (FileWatcher != null)
            {
                FileWatcher.Dispose();
                
            }
            Content = null;

        }
    }
}
