using Microsoft.Win32;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AudioPlayerWPF;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private WaveOutEvent outputDevice;
    private AudioFileReader audioFile;
    private readonly DispatcherTimer timer;

    private bool userInitiatedStop = false;

    // Playlist collection
    private readonly ObservableCollection<PlaylistItem> playlist;
    private int currentTrackIndex = -1;

    public event PropertyChangedEventHandler PropertyChanged;

    private PlaylistItem currentPlayingItem;
    public PlaylistItem CurrentPlayingItem
    {
        get => currentPlayingItem;
        set
        {
            if (currentPlayingItem != value)
            {
                currentPlayingItem = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(CurrentPlayingItem))
                );
            }
        }
    }

    public class PlaylistItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);

        private bool isPlaying;
        public bool IsPlaying
        {
            get => isPlaying;
            set
            {
                if (isPlaying != value)
                {
                    isPlaying = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += Timer_Tick;

        // Initialize playlist
        playlist = new ObservableCollection<PlaylistItem>();
        // Bind playlist to a ListBox in XAML
        PlaylistListBox.ItemsSource = playlist;

        // Initialize volume
        VolumeSlider.Value = 1.0;

        // Add event handler for playlist item selection
        PlaylistListBox.SelectionChanged += PlaylistListBox_SelectionChanged;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav",
            Multiselect = true // Allow multiple file selection
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (string file in openFileDialog.FileNames)
            {
                playlist.Add(new PlaylistItem { FilePath = file });
            }
            UpdateWindowTitle();
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (playlist.Count == 0)
        {
            MessageBox.Show("Please add songs to the playlist first.");
            return;
        }

        if (currentTrackIndex == -1)
        {
            currentTrackIndex = 0;
        }

        PlayCurrentTrack();
    }

    private void PlaylistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistListBox.SelectedIndex != -1)
        {
            currentTrackIndex = PlaylistListBox.SelectedIndex;
            PlayCurrentTrack();
        }
    }

    private void PlayCurrentTrack()
    {
        if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
        {
            string selectedFilePath = playlist[currentTrackIndex].FilePath;

            // Stop any existing playback
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }

            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }

            // Create new instances
            outputDevice = new WaveOutEvent();
            audioFile = new AudioFileReader(selectedFilePath);

            outputDevice.Init(audioFile);
            outputDevice.Volume = (float)VolumeSlider.Value;
            outputDevice.PlaybackStopped += OnPlaybackStopped;
            outputDevice.Play();

            timer.Start();
            UpdateWindowTitle();

            // Update the currently playing item
            if (CurrentPlayingItem != null)
            {
                CurrentPlayingItem.IsPlaying = false;
            }
            CurrentPlayingItem = playlist[currentTrackIndex];
            CurrentPlayingItem.IsPlaying = true;
        }
    }

    private void OnPlaybackStopped(object sender, StoppedEventArgs e)
    {
        if (!userInitiatedStop)
        {
            // Play next track when current track finishes
            currentTrackIndex++;
            if (currentTrackIndex < playlist.Count)
            {
                PlayCurrentTrack();
            }
            else
            {
                currentTrackIndex = -1;
                UpdateWindowTitle();
            }
        }
    }

    private void UpdateWindowTitle()
    {
        if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
        {
            string fileName = Path.GetFileName(playlist[currentTrackIndex].FilePath);
            Title = $"Playing: {fileName}";
        }
        else
        {
            Title = "Audio Player";
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        userInitiatedStop = true;

        // Stop and dispose of the output device
        if (outputDevice != null)
        {
            outputDevice.Stop();
            outputDevice.Dispose();
            outputDevice = null;
        }

        // Dispose of the audio file
        if (audioFile != null)
        {
            audioFile.Dispose();
            audioFile = null;
        }

        // Stop the timer
        timer.Stop();

        // Reset track index and update window title
        currentTrackIndex = -1;
        UpdateWindowTitle();

        // Reset the currently playing item
        if (CurrentPlayingItem != null)
        {
            CurrentPlayingItem.IsPlaying = false;
            CurrentPlayingItem = null;
        }

        // Update UI
        ProgressBar.Value = 0;
        TimeLabel.Text = "00:00 / 00:00";

        // Force UI refresh
        PlaylistListBox.Items.Refresh();

        // Ensure the UI is updated immediately
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

        userInitiatedStop = false;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (audioFile != null)
        {
            ProgressBar.Value = audioFile.Position * 100 / audioFile.Length;
            TimeLabel.Text = $"{audioFile.CurrentTime:mm\\:ss} / {audioFile.TotalTime:mm\\:ss}";
        }
        else
        {
            ProgressBar.Value = 0;
            TimeLabel.Text = "00:00 / 00:00";
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (outputDevice != null)
        {
            outputDevice.Volume = (float)e.NewValue;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        outputDevice?.Dispose();
        timer.Stop();
        audioFile?.Dispose();
        base.OnClosed(e);
    }
}
