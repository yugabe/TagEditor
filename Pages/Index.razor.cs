using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TagEditor.Pages;

public partial class Index : ComponentBase
{
    private string? selectedFolder;
    private string? targetFolder;
    private string? message;
    private Entry? streamingEntry;
    private bool showOnlyWarnings = true;
    private ILookup<DirectoryEntry, Entry>? files;

    [Inject]
    public IJSRuntime JS { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            selectedFolder = (await JS.InvokeAsync<string>("getLastUsedFolder")) ?? Directory.GetCurrentDirectory();
            targetFolder = (await JS.InvokeAsync<string>("getLastUsedTargetFolder")) ?? Directory.GetCurrentDirectory();
            Search();
            StateHasChanged();
        }
    }

    private async Task SaveLastFolderAsync() => await JS.InvokeAsync<string>("setLastUsedFolder", selectedFolder);
    private async Task SaveLastTargetFolderAsync() => await JS.InvokeAsync<string>("setLastUsedTargetFolder", targetFolder);

    private async Task ChangeFolderAsync(string newFolder)
    {
        selectedFolder = newFolder;
        await SaveLastFolderAsync();
        Search();
    }

    private void ClearMessage() => message = null;

    private void Search()
    {
        message = null;
        files = null;
        if (selectedFolder == null)
        {
            message = "No folder selected.";
            return;
        }
        var directory = new DirectoryInfo(selectedFolder);
        if (!directory.Exists)
        {
            message = "The selected folder doesn't exist.";
            return;
        }

        var search = directory.EnumerateFiles("*", SearchOption.AllDirectories).Select(f => new Entry(f, selectedFolder));
        if (showOnlyWarnings)
            search = search.Where(e => e.State != EntryState.None);
        files = search.OrderBy(e => e.Track).ToLookup(e => new DirectoryEntry(e.File.DirectoryName!));
    }

    private IEnumerable<Entry> GetSelectedEntries()
    {
        return files!.SelectMany(f => f).Where(f => f.IsSelected);
    }

    private void CopyPerformers()
    {
        foreach (var entry in GetSelectedEntries())
        {
            entry.TagFile!.Tag.AlbumArtists = entry.TagFile.Tag.Performers;
            entry.TagFile.Save();
        }
        Search();
    }

    private void FileRenameToTitle()
    {
        foreach(var entry in GetSelectedEntries())
            entry.File.MoveTo(Path.Combine(entry.File.DirectoryName!, $"{entry.PathSafeTitle}.mp3"));
        Search();
    }
    private void FileRenameToTrackNoAndTitle()
    {
        foreach (var entry in GetSelectedEntries())
            entry.File.MoveTo(Path.Combine(entry.File.DirectoryName!, $"{entry.Track:00} - {entry.PathSafeTitle}.mp3"));
        Search();
    }

    private void FileRenameToArtistAndTitle()
    {
        foreach (var entry in GetSelectedEntries())
            entry.File.MoveTo(Path.Combine(entry.File.DirectoryName!, $"{entry.PathSafePerformers} - {entry.PathSafeTitle}.mp3"));
        Search();
    }

    private void FileRenameTrackArtistsAndTitle()
    {
        foreach (var entry in GetSelectedEntries())
            entry.File.MoveTo(Path.Combine(entry.File.DirectoryName!, $"{entry.Track:00} - {entry.PathSafePerformers} - {entry.PathSafeTitle}.mp3"));
        Search();
    }

    private static string GetFileLengthFormatted(long length)
        => length switch
        {
            0 => "0",
            < 1_000_000 => $"{((double)length / 1024):N2} kB",
            < 1_000_000_000 => $"{((double)length / (1024 * 1024)):N2} MB",
            _ => $"{((double)length / (1024 * 1024 * 1024)):N2} GB",
        };

    public record DirectoryEntry(string Directory)
    {
        public bool IsSelected { get; set; }
    }

    public record Entry
    {
        private static HashSet<char> InvalidTitleChars { get; } = Path.GetInvalidFileNameChars().ToHashSet();
        public Entry(FileInfo file, string rootFolder)
        {
            File = file;
            RootFolder = rootFolder;
            RelativePath = Path.GetRelativePath(rootFolder, file.DirectoryName!);
            if (file.Extension.ToLower() == ".mp3")
            {
                TagFile = TagLib.File.Create(file.FullName);
                Performers = string.Join(", ", TagFile.Tag.Performers);
                AlbumArtists = string.Join(", ", TagFile.Tag.AlbumArtists);
                PathSafePerformers = new((Performers ?? "").Where(t => !InvalidTitleChars.Contains(t)).ToArray());
                PathSafeAlbumArtists = new((AlbumArtists ?? "").Where(t => !InvalidTitleChars.Contains(t)).ToArray());
                Album = TagFile.Tag.Album;
                PathSafeAlbum = new((Album ?? "").Where(t => !InvalidTitleChars.Contains(t)).ToArray());
                Year = (int)TagFile.Tag.Year;
                Track = (int)TagFile.Tag.Track;
                Disc = (int)TagFile.Tag.Disc;
                Title = TagFile.Tag.Title;
                PathSafeTitle = new((Title ?? "").Where(t => !InvalidTitleChars.Contains(t)).ToArray());
                Duration = TagFile.Properties.Duration;

                if (
                    Performers != AlbumArtists
                    || Performers?.Length == 0
                    || AlbumArtists?.Length == 0
                    || Year == 0
                    || string.IsNullOrWhiteSpace(Title))
                {
                    State |= EntryState.MetadataWarning;
                }

                if (RelativePath != $"{PathSafeAlbumArtists}\\{Year} - {PathSafeAlbum}")
                {
                    State |= EntryState.PathWarning;
                }

                if (file.Name != $"{Track:00} - {PathSafeTitle}.mp3")
                {
                    State |= EntryState.FileNameWarning;
                }
            }
        }
        public FileInfo File { get; }
        public string RootFolder { get; }
        public TagLib.File? TagFile { get; }
        public string? Performers { get; set; }
        public string? PathSafePerformers { get; }
        public string? AlbumArtists { get; set; }
        public string? PathSafeAlbumArtists { get; }
        public string? Album { get; set; }
        public string? PathSafeAlbum { get; }
        public int? Year { get; set; }
        public int? Track { get; set; }
        public int? Disc { get; set; }
        public string? Title { get; set; }
        public string? PathSafeTitle { get; }
        public string? RelativePath { get; }
        public TimeSpan? Duration { get; }
        public EntryState State { get; }

        public string? ProposedFileName { get; }

        public bool IsSelected { get; set; }

        public void Save()
        {
            if (TagFile != null)
            {
                TagFile.Tag.Title = Title;
                TagFile.Tag.Year = (uint)Year.GetValueOrDefault();
                TagFile.Tag.Track = (uint)Track.GetValueOrDefault();
                TagFile.Tag.Disc = (uint)Disc.GetValueOrDefault();
                TagFile.Tag.Album = Album;
                TagFile.Tag.AlbumArtists = AlbumArtists?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                TagFile.Tag.Performers = Performers?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                TagFile.Save();
            }
        }
    }

    public enum EntryState
    {
        None = 0,
        PathWarning = 1,
        FileNameWarning = 2,
        MetadataWarning = 4,
    }
}
