using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestClaudeAI
{
    public class WaveformDisplay : Control
    {
        public static readonly StyledProperty<IEnumerable<float>> WaveformDataProperty =
            AvaloniaProperty.Register<WaveformDisplay, IEnumerable<float>>(nameof(WaveformData));

        public IEnumerable<float> WaveformData
        {
            get => GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public static readonly StyledProperty<IBrush> WaveformBrushProperty =
            AvaloniaProperty.Register<WaveformDisplay, IBrush>(nameof(WaveformBrush));

        public IBrush WaveformBrush
        {
            get => GetValue(WaveformBrushProperty);
            set => SetValue(WaveformBrushProperty, value);
        }

        static WaveformDisplay()
        {
            AffectsRender<WaveformDisplay>(WaveformDataProperty, WaveformBrushProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (WaveformData == null || !WaveformData.Any())
            {
                return;
            }

            var pen = new Pen(WaveformBrush ?? Brushes.LightBlue, 3);
            var bounds = Bounds;
            var centerY = bounds.Height / 2;

            var dataPoints = WaveformData.ToList();
            var pointsPerPixel = (double)dataPoints.Count / bounds.Width;
            var currentX = 0d;

            for (int i = 0; i < bounds.Width; i++)
            {
                var startIndex = (int)(i * pointsPerPixel);
                var endIndex = (int)((i + 1) * pointsPerPixel);
                endIndex = Math.Min(endIndex, dataPoints.Count);
                var slice = dataPoints.GetRange(startIndex, endIndex - startIndex);
                var max = slice.Max();
                var min = slice.Min();

                // Flip the waveform vertically
                var topY = centerY - (max * centerY);
                var bottomY = centerY - (min * centerY);

                context.DrawLine(pen, new Point(currentX, topY), new Point(currentX, bottomY));
                currentX += 1;
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
                        sum += Math.Abs(buffer[i + c]);
                    }
                    waveformData.Add(sum / channels);
                }
            }

            return waveformData;
        }
    }
}
