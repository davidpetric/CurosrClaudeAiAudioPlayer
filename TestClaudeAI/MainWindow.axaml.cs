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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;

namespace TestClaudeAI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private WaveOutEvent outputDevice;
    private AudioFileReader audioFile;
    private bool isPlaying;
    private string selectedFile;
    private double currentPosition;
    private readonly Timer positionTimer;

    private IEnumerable<float> waveformData;

    public ObservableCollection<string> AudioPlaylist { get; } = new ObservableCollection<string>();


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
        get => isPlaying;
        set
        {
            if (isPlaying != value)
            {
                isPlaying = value;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        positionTimer = new Timer(100);
        positionTimer.Elapsed += PositionTimer_Elapsed;
    }

    private void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            AllowMultiple = true,
            Filters = new() { new FileDialogFilter { Name = "Audio Files", Extensions = { "mp3", "wav" } } }
        };

        var result = dialog.ShowAsync(this);
        result.ContinueWith(t =>
        {
            if (t.Result != null && t.Result.Length > 0)
            {
                foreach (var file in t.Result)
                {
                    AudioPlaylist.Add(file);
                }
            }
        });
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
            outputDevice.Stop();
            IsPlaying = false;
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

        await Task.Run(() =>
        {
            WaveformData = WaveformDisplay.GetWaveformData(filePath).ToList();
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(WaveformData));
                //WaveformDisplay.InvalidateVisual();
            });
        });
    }

    private async void PlayAudio(string filePath)
    {
        if (outputDevice == null)
        {
            outputDevice = new WaveOutEvent();
            outputDevice.PlaybackStopped += OnPlaybackStopped;
        }
        else
        {
            outputDevice.Stop();
        }

        if (audioFile == null || audioFile.FileName != filePath)
        {
            audioFile?.Dispose();
            audioFile = new AudioFileReader(filePath);
            outputDevice.Init(audioFile);

            await LoadWaveformData(filePath);
        }
        else
        {
            audioFile.Position = 0;
        }

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
                CurrentPosition = (double)audioFile.Position / audioFile.Length * 100;
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
}