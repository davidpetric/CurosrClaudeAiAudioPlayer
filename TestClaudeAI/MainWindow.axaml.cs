using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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


namespace TestClaudeAI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private WaveOutEvent outputDevice;
    private AudioFileReader audioFile;
    private bool _isPlaying;
    private string selectedFile;
    private double currentPosition;
    private readonly Timer positionTimer;

    private IEnumerable<float> waveformData;

    public ObservableCollection<string> AudioPlaylist { get; } = new ObservableCollection<string>();

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
            if (currentPosition != value)
            {
                currentPosition = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<float> WaveformData
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

    public event PropertyChangedEventHandler PropertyChanged;

    private const string PlaylistFileName = "playlist.txt";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        positionTimer = new Timer(100);
        positionTimer.Elapsed += PositionTimer_Elapsed;

        // Initialize cache path
        cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestClaudeAI", CacheDirectory);
        Directory.CreateDirectory(cachePath);

        // Load cached playlist
        LoadCachedPlaylist();
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
        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
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

        WaveformData = WaveformDisplay.GetWaveformData(filePath).ToList();
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(WaveformData));
            //WaveformDisplay.InvalidateVisual();
        });
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
                }
                catch (Exception)
                {
                    CurrentPosition = 0;
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

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
}