using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Path = System.IO.Path;
using Avalonia.Media;
using Avalonia.Styling;

namespace TestClaudeAI;

public class MainWindow : Window, INotifyPropertyChanged
{
    private WaveOutEvent? outputDevice;
    private AudioFileReader? audioFile;
    private bool _isPlaying;
    private string? selectedFile;
    private double currentPosition;
    private readonly Timer positionTimer;

    private IEnumerable<float>? waveformData;

    public ObservableCollection<string> AudioPlaylist { get; } = [];

    private const string CacheDirectory = "AudioCache";
    private readonly string cachePath;

    public string SelectedFile
    {
        get => selectedFile;
        set
        {
            if (selectedFile != value)
            {
                selectedFile = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
    }

    public double CurrentPosition
    {
        get => currentPosition;
        set
        {
            if (Math.Abs(currentPosition - value) > 0.01) // Add a small threshold to reduce updates
            {
                currentPosition = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<float>? WaveformData
    {
        get => waveformData;
        set
        {
            if (waveformData != value)
            {
                waveformData = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private const string PlaylistFileName = "playlist.txt";

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        positionTimer = new Timer(100);
        positionTimer.Elapsed += PositionTimer_Elapsed;

        // Initialize cache path
        cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestClaudeAI",
            CacheDirectory
        );
        Directory.CreateDirectory(cachePath);

        // Load cached playlist
        LoadCachedPlaylist();

        // Get the accent color
        UpdateAccentColor();

        // Subscribe to theme changes
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var waveformDisplay = this.FindControl<WaveformDisplay>("WaveformDisplay");
        if (waveformDisplay != null)
        {
            waveformDisplay.PositionChanged += WaveformDisplay_PositionChanged;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        this.AttachDevTools();
        base.OnLoaded(e);
    }

    private async void ImportFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        var result = await dialog.ShowAsync(this);

        if (result != null)
        {
            await ImportFilesFromPath(result);
        }
    }

    private async Task ImportFilesFromPath(string folderPath)
    {
        var audioExtensions = new[] { ".mp3", ".wav", ".ogg", ".flac" };
        var files = Directory
            .GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLower()));

        foreach (var file in files)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!AudioPlaylist.Contains(file))
                {
                    AudioPlaylist.Add(file);
                }
            });
        }

        SavePlaylistToCache();
    }

    private void SavePlaylistToCache()
    {
        var playlistFile = Path.Combine(cachePath, PlaylistFileName);
        File.WriteAllLines(playlistFile, AudioPlaylist.Distinct());
    }

    private void LoadCachedPlaylist()
    {
        var playlistFile = Path.Combine(cachePath, PlaylistFileName);
        if (File.Exists(playlistFile))
        {
            var cachedFiles = File.ReadAllLines(playlistFile);
            foreach (var file in cachedFiles)
            {
                if (File.Exists(file) && !AudioPlaylist.Contains(file))
                {
                    AudioPlaylist.Add(file);
                }
            }
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile != null)
        {
            PlayAudio(SelectedFile);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (outputDevice != null)
        {
            outputDevice.Pause();
            IsPlaying = false;
            positionTimer.Stop();
        }
    }

    private async void PlaylistListBox_DoubleTapped(object sender, TappedEventArgs e)
    {
        if (SelectedFile != null)
        {
            await LoadWaveformData(SelectedFile);
            PlayAudio(SelectedFile);
        }
    }

    private async Task LoadWaveformData(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            Console.WriteLine($"Loading waveform data for: {filePath}");
            var waveformData = await Task.Run(() => WaveformDisplay.GetWaveformData(filePath).ToList());
            Console.WriteLine($"Waveform data loaded. Count: {waveformData.Count}");
            WaveformData = waveformData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading waveform data: {ex.Message}");
        }
    }

    private void PlayAudio(string filePath)
    {
        // Stop and dispose of the current playback
        if (outputDevice != null)
        {
            outputDevice.Stop();
            outputDevice.Dispose();
            audioFile?.Dispose();
        }

        audioFile = new AudioFileReader(filePath);
        outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);
        outputDevice.Play();
        IsPlaying = true;
        positionTimer.Start();

        // Update song length
        TimeSpan totalTime = audioFile.TotalTime;
        SongLength = $"{totalTime.Minutes:D2}:{totalTime.Seconds:D2}";
    }

    private void PositionTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (audioFile != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    CurrentPosition = (audioFile.Position * 100.0 / audioFile.Length);
                    TimeSpan currentTime = audioFile.CurrentTime;
                    TimeSpan totalTime = audioFile.TotalTime;
                    SongLength = $"{currentTime.Minutes:D2}:{currentTime.Seconds:D2} / {totalTime.Minutes:D2}:{totalTime.Seconds:D2}";
                    
                    // Update the progress of the waveform display
                    var waveformDisplay = this.FindControl<WaveformDisplay>("WaveformDisplay");
                    if (waveformDisplay != null)
                    {
                        waveformDisplay.Progress = CurrentPosition / 100.0;
                    }
                }
                catch (Exception)
                {
                    CurrentPosition = 0;
                    SongLength = "00:00 / 00:00";
                }
            });
        }
    }

    private void PositionSlider_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "Value" && audioFile != null)
        {
            var newPosition = (double)e.NewValue;
            audioFile.Position = (long)(audioFile.Length * (newPosition / 100));
        }
    }

    private void OnPlaybackStopped(object sender, StoppedEventArgs e)
    {
        outputDevice?.Dispose();
        outputDevice = null;
        audioFile?.Dispose();
        audioFile = null;
        IsPlaying = false;
        positionTimer.Stop();
        Dispatcher.UIThread.Post(() => CurrentPosition = 0);
    }

    protected override void OnClosed(EventArgs e)
    {
        positionTimer.Dispose();
        outputDevice?.Dispose();
        audioFile?.Dispose();
        base.OnClosed(e);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        ClearCache();
    }

    private void ClearCache()
    {
        var playlistFile = Path.Combine(cachePath, PlaylistFileName);
        if (File.Exists(playlistFile))
        {
            File.Delete(playlistFile);
        }

        AudioPlaylist.Clear();
    }

    private IBrush? accentBrush;
    public IBrush? AccentBrush
    {
        get => accentBrush;
        set
        {
            if (accentBrush != value)
            {
                accentBrush = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateAccentColor()
    {
        var theme = Application.Current.ActualThemeVariant;
        var accentColor = Application.Current.FindResource(theme == ThemeVariant.Dark ? "SystemAccentColorDark1" : "SystemAccentColor");
        
        if (accentColor is Color color)
        {
            AccentBrush = new SolidColorBrush(color);
        }
        else
        {
            AccentBrush = Brushes.LightBlue; // Fallback color
        }
    }

    private void OnActualThemeVariantChanged(object sender, EventArgs e)
    {
        UpdateAccentColor();
    }

    private string songLength;
    public string SongLength
    {
        get => songLength;
        set
        {
            if (songLength != value)
            {
                songLength = value;
                OnPropertyChanged();
            }
        }
    }

    private void WaveformDisplay_PositionChanged(object? sender, double newProgress)
    {
        if (audioFile != null)
        {
            audioFile.Position = (long)(audioFile.Length * newProgress);
            CurrentPosition = newProgress * 100;
        }
    }
}
