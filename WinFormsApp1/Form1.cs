using System;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using NAudio.Wave;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SegaCDVideoConverter
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            string inputVideoPath = txtInputPath.Text; // Œcie¿ka do wejœciowego wideo
            string outputVideoPath = txtOutputPath.Text; // Œcie¿ka do wyjœciowego wideo

            // Otwórz wideo
            VideoCapture capture = new VideoCapture(inputVideoPath);
            int frameWidth = (int)capture.Get(CapProp.FrameWidth);
            int frameHeight = (int)capture.Get(CapProp.FrameHeight);
            double fps = capture.Get(CapProp.Fps);

            // Utwórz obiekt VideoWriter do zapisu wideo
            VideoWriter writer = new VideoWriter(outputVideoPath, VideoWriter.Fourcc('M', 'J', 'P', 'G'), fps, new Size(frameWidth, frameHeight), true);

            Mat frame = new Mat();
            while (capture.Read(frame))
            {
                // Przetwarzanie ramki wideo
                Mat processedFrame = ProcessFrame(frame);

                // Zapisz przetworzon¹ ramkê do wyjœciowego wideo
                writer.Write(processedFrame);
            }

            capture.Dispose();
            writer.Dispose();

            // Przetwarzanie dŸwiêku
            string outputAudioPath = ProcessAudio(inputVideoPath, outputVideoPath);

            // Po³¹czenie wideo i audio
            CombineVideoAndAudio(outputVideoPath, outputAudioPath);

            MessageBox.Show("Konwersja zakoñczona!");
        }

        private Mat ProcessFrame(Mat frame)
        {
            // Zastosuj ograniczenie kolorów do palety SegaCD
            ApplySegaCDPalette(frame);

            return frame;
        }

        private void ApplySegaCDPalette(Mat frame)
        {
            int rows = frame.Rows;
            int cols = frame.Cols;
            int channels = frame.NumberOfChannels;
            byte[] data = new byte[rows * cols * channels];
            Marshal.Copy(frame.DataPointer, data, 0, data.Length);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = (y * cols + x) * channels;
                    byte blue = data[index];
                    byte green = data[index + 1];
                    byte red = data[index + 2];

                    blue = (byte)((blue & 0xE0) | (blue >> 3));
                    green = (byte)((green & 0xE0) | (green >> 3));
                    red = (byte)((red & 0xE0) | (red >> 3));

                    data[index] = blue;
                    data[index + 1] = green;
                    data[index + 2] = red;
                }
            }

            Marshal.Copy(data, 0, frame.DataPointer, data.Length);
        }

        private string ProcessAudio(string inputVideoPath, string outputVideoPath)
        {
            string tempAudioPath = "temp_audio.wav";
            string outputAudioPath = System.IO.Path.ChangeExtension(outputVideoPath, ".wav");

            // Ekstrakcja audio z wideo
            using (var reader = new MediaFoundationReader(inputVideoPath))
            {
                WaveFileWriter.CreateWaveFile(tempAudioPath, reader);
            }

            // Konwersja audio do 8-bitowego PCM z próbkowaniem 22,05 kHz
            using (var reader = new AudioFileReader(tempAudioPath))
            {
                var outFormat = new WaveFormat(22050, 8, 1);
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    resampler.ResamplerQuality = 60;
                    WaveFileWriter.CreateWaveFile(outputAudioPath, resampler);
                }
            }

            // Usuniêcie tymczasowego pliku audio
            System.IO.File.Delete(tempAudioPath);

            return outputAudioPath;
        }

        private void CombineVideoAndAudio(string videoPath, string audioPath)
        {
            string outputPath = System.IO.Path.ChangeExtension(videoPath, "_final.avi");
            string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFMPEG", "ffmpeg-2025-03-10-git-87e5da9067-full_build", "bin", "ffmpeg.exe");

            if (!System.IO.File.Exists(ffmpegPath))
            {
                MessageBox.Show($"Nie mo¿na odnaleŸæ pliku ffmpeg.exe w œcie¿ce: {ffmpegPath}");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -strict experimental \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show($"B³¹d podczas ³¹czenia wideo i audio: {error}");
                }
            }
        }

        private void btnBrowseInput_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.mkv";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtInputPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "AVI Files|*.avi";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputPath.Text = saveFileDialog.FileName;
                }
            }
        }
    }
}
