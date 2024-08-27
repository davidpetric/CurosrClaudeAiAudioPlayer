using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;

namespace TestClaudeAI;

public class WaveformDisplay : Control
{
    public static readonly StyledProperty<IEnumerable<float>> WaveformDataProperty =
        AvaloniaProperty.Register<WaveformDisplay, IEnumerable<float>>(nameof(WaveformData));

    public IEnumerable<float> WaveformData
    {
        get => GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    static WaveformDisplay()
    {
        AffectsRender<WaveformDisplay>(WaveformDataProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (WaveformData == null || !WaveformData.Any())
            return;

        var pen = new Pen(Brushes.LightBlue, 1);
        var bounds = Bounds;
        var centerY = bounds.Height / 2;

        using (context.PushClip(bounds))
        {
            var dataPoints = new List<float>(WaveformData);
            var pointsPerPixel = dataPoints.Count / bounds.Width;
            var currentX = 0d;

            for (int i = 0; i < bounds.Width; i++)
            {
                var startIndex = (int)(i * pointsPerPixel);
                var endIndex = (int)((i + 1) * pointsPerPixel);
                var slice = dataPoints.GetRange(startIndex, endIndex - startIndex);
                var max = slice.Max();
                var min = slice.Min();

                var topY = centerY + (min * centerY);
                var bottomY = centerY + (max * centerY);

                context.DrawLine(pen, new Point(currentX, topY), new Point(currentX, bottomY));
                currentX += 1;
            }
        }
    }

    public static IEnumerable<float> GetWaveformData(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        var sampleProvider = reader.ToSampleProvider();
        var sampleRate = sampleProvider.WaveFormat.SampleRate;
        var channels = sampleProvider.WaveFormat.Channels;
        var buffer = new float[sampleRate * channels];
        var waveformData = new List<float>();

        int samplesRead;
        while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i += channels)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++)
                {
                    sum += buffer[i + c];
                }
                waveformData.Add(sum / channels);
            }
        }

        return waveformData;
    }
}