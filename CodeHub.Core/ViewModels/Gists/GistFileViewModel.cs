using System;
using System.Linq;
using CodeHub.Core.Services;
using GitHubSharp.Models;
using ReactiveUI;
using Xamarin.Utilities.Core.Services;
using CodeHub.Core.ViewModels.Source;
using Xamarin.Utilities.Core.ViewModels;
using System.Reactive.Linq;
using System.Reactive;

namespace CodeHub.Core.ViewModels.Gists
{
    public class GistFileViewModel : FileSourceViewModel, ILoadableViewModel
    {
        public const string MarkdownLanguage = "Markdown";

        private string _id;
	    public string Id
	    {
	        get { return _id; }
	        set { this.RaiseAndSetIfChanged(ref _id, value); }
	    }

        private string _filename;
	    public string Filename
	    {
	        get { return _filename; }
	        set { this.RaiseAndSetIfChanged(ref _filename, value); }
	    }

        private GistFileModel _gistFile;
	    public GistFileModel GistFile
	    {
	        get { return _gistFile; }
	        set { this.RaiseAndSetIfChanged(ref _gistFile, value); }
	    }

        private readonly ObservableAsPropertyHelper<bool> _isMarkdown;
        public bool IsMarkdown
        {
            get { return _isMarkdown.Value; }
        }

        public IReactiveCommand<object> ShareCommand { get; private set; }

        public IReactiveCommand<object> OpenWithCommand { get; private set; }

        public IReactiveCommand GoToUrlCommand { get; private set; }

        public IReactiveCommand LoadCommand { get; private set; }

        public IReactiveCommand<Unit> ShowMenuCommand { get; private set; }

        public IReactiveCommand<Unit> ShowThemePickerCommand { get; private set; }

        public GistFileViewModel(IAccountsService accounts, IApplicationService applicationService, 
            IFilesystemService filesystemService, IShareService shareService, IActionMenuService actionMenuService)
            : base(accounts)
	    {
	        this.WhenAnyValue(x => x.Filename).Subscribe(x =>
	        {
                Title = x == null ? "Gist" : x.Substring(x.LastIndexOf('/') + 1);
	        });

            GoToUrlCommand = this.CreateUrlCommand();

            OpenWithCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SourceItem).Select(x => x != null));

            _isMarkdown = this.WhenAnyValue(x => x.GistFile).IsNotNull().Select(x => 
                string.Equals(x.Language, MarkdownLanguage, StringComparison.OrdinalIgnoreCase)).ToProperty(this, x => x.IsMarkdown);

            ShowThemePickerCommand = ReactiveCommand.CreateAsyncTask(async _ =>
            {
                var path = System.IO.Path.Combine("WebResources", "styles");
                if (!System.IO.Directory.Exists(path))
                    return;

                var themes = System.IO.Directory.GetFiles(path)
                    .Where(x => x.EndsWith(".css", StringComparison.Ordinal))
                    .Select(x => System.IO.Path.GetFileNameWithoutExtension(x))
                    .ToList();

                var picker = actionMenuService.CreatePicker();
                themes.ForEach(picker.Options.Add);
                picker.SelectedOption = themes.IndexOf(Theme ?? "idea");

                var selected = await picker.Show();

                if (selected >= 0 && selected <= themes.Count)
                    Theme = themes[selected];
            });

            ShowMenuCommand = ReactiveCommand.CreateAsyncTask(
                this.WhenAnyValue(x => x.SourceItem).Select(x => x != null),
                _ =>
            {
                var menu = actionMenuService.Create(Title);
                menu.AddButton("Open With", OpenWithCommand);
                if (!IsMarkdown) 
                    menu.AddButton("Change Theme", ShowThemePickerCommand);
                return menu.Show();
            });

            LoadCommand = ReactiveCommand.CreateAsyncTask(async t =>
            {
                if (GistFile == null)
			    {
                    var data = await applicationService.Client.ExecuteAsync(applicationService.Client.Gists[_id].Get());
                    GistFile = data.Data.Files[_filename];
			    }

			    //Check to make sure...
                if (GistFile == null || GistFile.Content == null)
			    {
				    throw new Exception("Unable to retreive gist!");
			    }

                var content = GistFile.Content;
                if (MarkdownLanguage.Equals(GistFile.Language, StringComparison.OrdinalIgnoreCase))
                    content = await applicationService.Client.Markdown.GetMarkdown(content);

                var gistFileName = System.IO.Path.GetFileName(GistFile.Filename);
                string filePath;
                
                using (var stream = filesystemService.CreateTempFile(out filePath, gistFileName))
                {
                    using (var fs = new System.IO.StreamWriter(stream))
                    {
                        fs.Write(content);
                    }
                }

                var fileUri = new Uri(filePath);
                SourceItem = new FileSourceItemViewModel(fileUri, false);
            });
	    }
    }
}

