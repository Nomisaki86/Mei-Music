using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Mei_Music
{
    /// <summary>
    /// Secondary window that imports media from online URLs.
    /// Coordinates yt-dlp/ffmpeg tool execution and reports resulting files back to <see cref="MainWindow"/>.
    /// </summary>
    public partial class SearchThroughURLWindow : Window
    {
        /// <summary>
        /// Host main window used to register imported media in the song list.
        /// </summary>
        MainWindow mainWindow;

        /// <summary>
        /// Creates URL import window and stores callback target to main window.
        /// </summary>
        public SearchThroughURLWindow(MainWindow main_Window)
        {
            InitializeComponent();
            mainWindow = main_Window;
        }

        /// <summary>
        /// Hides placeholder when URL input receives focus while empty.
        /// </summary>
        private void RemovePlaceholderText(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "")
            {
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Restores placeholder when URL input loses focus and remains empty.
        /// </summary>
        private void AddPlaceholderText(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "")
            {
                PlaceholderText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Resolves an absolute path for a bundled tool and verifies it exists.
        /// Returns null (after showing a message) when the file is missing.
        /// </summary>
        private string? GetToolPath(string relativeToolPath)
        {
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeToolPath);
            if (!File.Exists(fullPath))
            {
                string toolName = System.IO.Path.GetFileName(fullPath);
                MessageBox.Show(
                    $"Required tool '{toolName}' was not found at:\n{fullPath}\n\nPlease reinstall the application.",
                    "Missing Tool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }
            return fullPath;
        }

        /// <summary>
        /// Remuxes a downloaded .webm file into .mp4 without re-encoding streams.
        /// </summary>
        private void ConvertWebMToMp4(string inputWebMPath, string outputMp4Path)
        {
            try
            {
                string? ffmpegPath = GetToolPath(@"Resources\ffmpeg\ffmpeg.exe");
                if (ffmpegPath == null) return;

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{inputWebMPath}\" -c:v copy -c:a copy \"{outputMp4Path}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process? process = Process.Start(startInfo);
                if (process == null)
                {
                    MessageBox.Show("Failed to start FFmpeg for webm conversion.");
                    return;
                }

                using (process)
                {
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        MessageBox.Show($"FFmpeg failed to convert .webm to .mp4: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert .webm to .mp4: {ex.Message}");
            }
        }

        /// <summary>
        /// End-to-end URL import flow: download media, normalize/merge formats,
        /// move final files to app storage, then update the main playlist UI.
        /// </summary>
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessingProgressBar.Visibility = Visibility.Visible;
            ProcessingText.Visibility = Visibility.Visible;

            string videoUrl = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoUrl))
            {
                MessageBox.Show("Please enter a valid URL.");
                HideProcessingUI();
                return;
            }

            string customFileName = PromptForFileName();
            if (string.IsNullOrEmpty(customFileName))
            {
                MessageBox.Show("File name cannot be empty.");
                HideProcessingUI();
                return;
            }

            string downloadedDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mei Music",
                "temp",
                "downloaded");

            string finalVideoDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mei Music",
                "temp",
                "video");

            Directory.CreateDirectory(downloadedDirectory);
            Directory.CreateDirectory(finalVideoDirectory);
            // Start from a clean temp folder so stale files are not mistaken for this run.
            Directory.GetFiles(downloadedDirectory).ToList().ForEach(File.Delete);

            string finalVideoPath = System.IO.Path.Combine(finalVideoDirectory, customFileName + ".mp4");

            try
            {
                // 1) Download in the background
                await Task.Run(() => DownloadVideo(videoUrl, downloadedDirectory));

                // 2) Examine the downloaded files
                var downloadedFiles = Directory.GetFiles(downloadedDirectory);

                // Some sites return .webm; convert each one so downstream logic only handles .mp4.
                var webmFiles = downloadedFiles.Where(f => f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (webmFiles.Length > 0)
                {
                    foreach (string webmFile in webmFiles)
                    {
                        string mp4Path = System.IO.Path.ChangeExtension(webmFile, ".mp4");
                        await Task.Run(() => ConvertWebMToMp4(webmFile, mp4Path));
                    }
                }

                //After potential conversions, refresh the file list
                downloadedFiles = Directory.GetFiles(downloadedDirectory);

                // 3) Check if we have an mp4 or m4a
                var downloadedVideoPath = downloadedFiles.FirstOrDefault(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
                var downloadedAudioPath = downloadedFiles.FirstOrDefault(f => f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase));

                // If we still don't have an .mp4, fail out
                if (downloadedVideoPath == null)
                {
                    // Fallback: yt-dlp may have produced audio-only; keep that workflow usable.
                    var downloadedMp3 = downloadedFiles
                        .FirstOrDefault(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));

                    if (downloadedMp3 != null)
                    {
                        // Move the MP3 to the playlist directory
                        string playlistDirectory = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Mei Music",
                            "playlist");

                        Directory.CreateDirectory(playlistDirectory);

                        // Normalize output name so URL imports follow the same naming rule.
                        string mp3FinalName = customFileName + ".mp3";
                        string mp3FinalPath = System.IO.Path.Combine(playlistDirectory, mp3FinalName);

                        File.Move(downloadedMp3, mp3FinalPath);

                        // Register this imported file in the main playlist immediately.
                        mainWindow.AddFileToUI(mp3FinalPath);

                        //MessageBox.Show($"Video was not downloaded, but the audio was found and saved as '{mp3FinalName}' in your playlist.");
                    }
                    else
                    {
                        // If no .mp3 either, then we truly have no content to work with
                        MessageBox.Show("Video file was not downloaded.");
                    }

                    HideProcessingUI();
                    return;
                }

                // 4) Combine if .m4a exists, otherwise copy the .mp4 to final location
                if (downloadedAudioPath != null)
                {
                    string? aacAudioPath = await Task.Run(() => ConvertM4aToAac(downloadedAudioPath));
                    if (aacAudioPath != null)
                    {
                        await Task.Run(() => CombineVideoAndAudio(downloadedVideoPath, aacAudioPath, finalVideoPath));
                    }
                    else
                    {
                        MessageBox.Show("AAC conversion failed.");
                        HideProcessingUI();
                        return;
                    }
                }
                else
                {
                    File.Copy(downloadedVideoPath, finalVideoPath, overwrite: true);
                }

                // 5) Clean up the temporary downloaded files
                Directory.GetFiles(downloadedDirectory).ToList().ForEach(File.Delete);

                // 6) If final mp4 exists, add to main UI and convert to audio for playlist
                if (File.Exists(finalVideoPath))
                {
                    mainWindow.AddFileToUI(finalVideoPath);
                    string convertedAudioPath = await Task.Run(() => ConvertVideoToAudio(finalVideoPath));
                    if (!string.IsNullOrWhiteSpace(convertedAudioPath))
                    {
                        mainWindow.RefreshSongDurationFromFile(convertedAudioPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            finally
            {
                HideProcessingUI();
            }
        }

        /// <summary>
        /// Hides progress indicators after URL-import work completes or fails.
        /// </summary>
        private void HideProcessingUI()
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingProgressBar.Visibility = Visibility.Collapsed;
                ProcessingText.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Downloads media with yt-dlp and applies source-specific fallback rules.
        /// </summary>
        private void DownloadVideo(string videoUrl, string downloadDirectory)
        {
            try
            {
                string? ytDlpPath = GetToolPath(@"Resources\yt-dlp\yt-dlp.exe");
                if (ytDlpPath == null) return;

                // Sanitize the filename to avoid invalid characters
                string sanitizedFileName = SanitizeFileName("video"); // Basic name to ensure fallback
                string videoFilePath = System.IO.Path.Combine(downloadDirectory, $"{sanitizedFileName}.mp4");

                // Define the arguments based on the site URL
                if (videoUrl.StartsWith("https://www.bilibili.com/"))
                {
                    // For Bilibili: Download best video and audio, avoid playlists
                    // (Leave your Bilibili code intact.)
                    string bilibiliArgs =
                        $"--no-playlist --format \"bestvideo+bestaudio/best\" -o \"{System.IO.Path.Combine(downloadDirectory, "%(title)s.%(ext)s")}\" \"{videoUrl}\"";

                    if (!RunYtDlp(ytDlpPath, bilibiliArgs))
                    {
                        MessageBox.Show("Failed to download from Bilibili.");
                        return;
                    }
                }
                else
                {
                    // Video-first logic can be re-enabled when needed; currently we force
                    // audio fallback for YouTube to keep imports reliable across formats.
                    bool videoSuccess = false;

                    if (!videoSuccess)
                    {
                        // If full video download fails, clear directory then do audio-only
                        Directory.GetFiles(downloadDirectory).ToList().ForEach(File.Delete);

                        // ============================
                        // YOUTUBE LOGIC: THEN TRY AUDIO
                        // ============================
                        string youtubeAudioArgs =
                            $"--no-playlist -f bestaudio " +
                            $"--extract-audio " +
                            $"--audio-format mp3 " +
                            $"--audio-quality 0 " +
                            $"-o \"{System.IO.Path.Combine(downloadDirectory, "%(title)s.%(ext)s")}\" " +
                            $"\"{videoUrl}\"";

                        bool audioSuccess = RunYtDlp(ytDlpPath, youtubeAudioArgs);
                        if (!audioSuccess)
                        {
                            // If both attempts fail, we exit
                            MessageBox.Show("Could not download either video or audio from YouTube.");
                            return;
                        }
                    }
                }

                // After the above, continue as usual:
                var downloadedFiles = Directory.GetFiles(downloadDirectory);
                if (downloadedFiles.Length > 2)
                {
                    foreach (var file in downloadedFiles)
                    {
                        if (!file.EndsWith(".mp4") && !file.EndsWith(".m4a"))
                        {
                            File.Delete(file); // Delete non-mp4 and non-m4a files
                        }
                    }
                }

                downloadedFiles = Directory.GetFiles(downloadDirectory, "*.mp4")
                                           .Concat(Directory.GetFiles(downloadDirectory, "*.m4a"))
                                           .ToArray();

                // If no *.mp4 was downloaded, you might handle that differently if you need an actual video file.
                // If you're okay with audio-only, adjust accordingly.
                //if (downloadedFiles.Length == 0 || !downloadedFiles.Any(f => f.EndsWith(".mp4")))
                //{
                //    MessageBox.Show("No video was downloaded. The URL may be invalid or the video is not available.");
                //    return;
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during download: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes yt-dlp and returns false when process launch or command execution fails.
        /// </summary>
        private bool RunYtDlp(string ytDlpPath, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? process = Process.Start(startInfo);
            if (process == null)
            {
                Debug.WriteLine("Failed to start yt-dlp process.");
                return false;
            }

            using (process)
            {
                process.WaitForExit(); // Wait for the process to complete
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    // Optionally log or show the error for debugging
                    Debug.WriteLine($"yt-dlp error: {error}");
                    return false; // indicate failure
                }
            }

            return true; // indicate success
        }

        /// <summary>
        /// Utility watchdog that can terminate a process when too many files appear in temp folder.
        /// Reserved for defensive download constraints.
        /// </summary>
        private async Task MonitorDirectoryForFileLimit(string directory, Process process, int maxFiles)
        {
            try
            {
                while (!process.HasExited)
                {
                    var files = Directory.GetFiles(directory);
                    if (files.Length > maxFiles)
                    {
                        // Kill the download process if there are more than maxFiles in the directory
                        process.Kill();
                        MessageBox.Show("Exceeded file limit; terminating download.");
                        break;
                    }
                    await Task.Delay(500); // Check every 500 ms
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while monitoring files: {ex.Message}");
            }
        }

        /// <summary>
        /// Replaces invalid filename characters with underscores.
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_'); // Replace invalid characters with underscore
            }
            return fileName;
        }

        /// <summary>
        /// Muxes downloaded video and audio streams into one MP4 file using ffmpeg stream copy.
        /// </summary>
        private void CombineVideoAndAudio(string videoPath, string audioPath, string outputPath)
        {
            string? ffmpegPath = GetToolPath(@"Resources\ffmpeg\ffmpeg.exe");
            if (ffmpegPath == null) return;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a copy -strict experimental \"{outputPath}\" -y",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? process = Process.Start(startInfo);
            if (process == null)
            {
                MessageBox.Show("Failed to start FFmpeg for media merge.");
                return;
            }

            using (process)
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show($"FFmpeg failed to combine video and audio: {error}");
                }
            }
        }

        /// <summary>
        /// Converts .m4a to .aac so FFmpeg can safely mux with downloaded video.
        /// </summary>
        private string? ConvertM4aToAac(string audioPath)
        {
            try
            {
                
                // Ensure the output file has a different name by appending "_converted" to avoid conflicts
                string outputDirectory = System.IO.Path.GetDirectoryName(audioPath) ?? "";
                string aacAudioPath = System.IO.Path.Combine(outputDirectory, System.IO.Path.GetFileNameWithoutExtension(audioPath) + "_converted.aac");

                string? ffmpegPath = GetToolPath(@"Resources\ffmpeg\ffmpeg.exe");
                if (ffmpegPath == null) return null;

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{audioPath}\" -c:a aac \"{aacAudioPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process? process = Process.Start(startInfo);
                if (process == null)
                {
                    MessageBox.Show("Failed to start FFmpeg for AAC conversion.");
                    return null;
                }

                using (process)
                {
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    // Check if the FFmpeg command completed successfully
                    if (process.ExitCode != 0)
                    {
                        MessageBox.Show($"FFmpeg failed to convert m4a to aac: {error}");
                        return null;
                    }
                }

                // Confirm if the output file is created
                if (File.Exists(aacAudioPath))
                {
                    return aacAudioPath;
                }
                else
                {
                    MessageBox.Show("AAC file was not created.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert m4a to aac: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts high-quality audio from a downloaded video into the playlist directory.
        /// </summary>
        private string ConvertVideoToAudio(string videoFilePath) //perform conversion from video to audio
        {
            try
            {
                string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
                Directory.CreateDirectory(outputDirectory); // Ensure the directory structure exists

                string audioFilePath = System.IO.Path.Combine(outputDirectory, System.IO.Path.GetFileNameWithoutExtension(videoFilePath) + ".mp3");

                string? ffmpegPath = GetToolPath(@"Resources\ffmpeg\ffmpeg.exe");
                if (ffmpegPath == null) throw new InvalidOperationException("ffmpeg.exe is missing.");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    //q:a 0 set audio quality to best. map a allows extraction of only audio -y allows to overwrite output files
                    Arguments = $"-i \"{videoFilePath}\" -q:a 0 -map a \"{audioFilePath}\" -y",
                    RedirectStandardOutput = true, //capture processing info
                    RedirectStandardError = true,  //capture processing error
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process? process = Process.Start(startInfo); //the ? indicate that process may be null
                if (process == null)
                {
                    throw new InvalidOperationException("Fail to start the ffmpeg process.");
                }
                using (process)
                {
                    process.WaitForExit();
                }
                return audioFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert Video: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prompts user for output filename and sanitizes invalid characters.
        /// </summary>
        private string PromptForFileName()
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for the downloaded file (without extension):",
                "File Name",
                "MyVideo"); // Default name is "MyVideo"

            // Sanitize the input by removing invalid characters
            string sanitizedFileName = string.Join("_", input.Split(System.IO.Path.GetInvalidFileNameChars()));

            return sanitizedFileName;
        }

        //------------------------- Icon bar implementation --------------------------------
        /// <summary>
        /// Enables drag-move from custom title bar area.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// Minimizes URL import window.
        /// </summary>
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles maximize/restore state for URL import window.
        /// </summary>
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        /// <summary>
        /// Closes URL import window.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //----------------------------------------------------------------------------------
    }
}
