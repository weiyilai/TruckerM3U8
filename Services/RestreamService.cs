using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System;

namespace TruckerM3U8.Services
{
    public class RestreamService
    {
        private readonly ILogger<RestreamService> _logger;
        private Process? _ffmpegProcess = null;
        private CancellationTokenSource _listenerCancelTokenSource = new CancellationTokenSource();
        private readonly List<Stream> _streams = new List<Stream>();
        private string _sourceUrl = "";

        /// <summary>
        /// How many audiences?
        /// </summary>
        public int GetActiveStreams => _streams.Count;

        /// <summary>
        /// Fetch the stream from which source
        /// </summary>
        public string SourceUrl => _sourceUrl;


        public RestreamService(ILogger<RestreamService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Set Source Url
        /// </summary>
        /// <param name="url"></param>
        public void SetSourceUrl(string url)
        {
            _sourceUrl = url;
            StartFfmpeg();
        }

        /// <summary>
        /// Start the main service
        /// </summary>
        private void StartFfmpeg()
        {
            // Clean up lat session
            if (_ffmpegProcess != null)
            {
                _listenerCancelTokenSource.Cancel();
                _listenerCancelTokenSource = new CancellationTokenSource();
                _ffmpegProcess.Kill();
                _ffmpegProcess.Dispose();
                _logger.LogInformation($"Stop the last FFMPEG session");
            }

            // YT-DLP
            // run yt-dlp -g 'URL'
            // get the direct m3u8 url, if the url is m3u8, use it directly
            string playbackUrl = SourceUrl;
            if(SourceUrl.EndsWith(".m3u8"))
            {
                playbackUrl = SourceUrl;
            }
            else
            {
                _logger.LogInformation($"Not a M3U8 stream, fetching direct URL from {SourceUrl}");
                using(var ytDlpProcess = new Process())
                {
                    ytDlpProcess.StartInfo.FileName = @"ThirdParty/yt-dlp.exe";
                    ytDlpProcess.StartInfo.Arguments = $"-g \"{SourceUrl}\"";
                    ytDlpProcess.StartInfo.UseShellExecute = false;
                    ytDlpProcess.StartInfo.RedirectStandardOutput = true;
                    ytDlpProcess.StartInfo.RedirectStandardError = true;
                    ytDlpProcess.StartInfo.CreateNoWindow = true;
                    ytDlpProcess.Start();
                    playbackUrl = ytDlpProcess.StandardOutput.ReadToEnd().Trim();
                    string error = ytDlpProcess.StandardError.ReadToEnd();
                    ytDlpProcess.WaitForExit();

                    if (ytDlpProcess.ExitCode != 0)
                    {
                        _logger.LogError($"yt-dlp error (ExitCode {ytDlpProcess.ExitCode}): {error}");
                    }
                    if (string.IsNullOrEmpty(playbackUrl))
                    {
                        _logger.LogError("yt-dlp returned no playback URL.");
                        return; // Stop processing if no URL found
                    }
                }
            }
            _logger.LogInformation($"Playback URL: {playbackUrl}");

            // FFMPEG
            _ffmpegProcess = new Process();
            _ffmpegProcess.StartInfo.FileName = @"ThirdParty/ffmpeg.exe";
            _ffmpegProcess.StartInfo.Arguments =
                $"-re -i {playbackUrl} -listen 1 -c libmp3lame -reconnect 1 -reconnect_at_eof 1 -reconnect_streamed 1 -reconnect_delay_max 4 -f mp3 tcp://127.0.0.1:1049";
            //_ffmpegProcess.StartInfo.CreateNoWindow = true; // uncomment to display FFMPEG logs            
            _ffmpegProcess.Start();
            _logger.LogInformation($"FFMPEG started (PID={_ffmpegProcess.Id})");

            // Listener
            Task.Run(() => StreamListener(_listenerCancelTokenSource.Token));
        }

        /// <summary>
        /// Read FFMPEG output and distribute to the streams registered
        /// </summary>
        /// <returns></returns>
        private async Task StreamListener(CancellationToken token)
        {
            //Tcp
            using (TcpClient client = new TcpClient("127.0.0.1", 1049))
            {
                using (var inStream = client.GetStream())
                {
                    byte[] buffer = new byte[512];
                    int length = 0;
                    int retryCount = 0;
                    while (!token.IsCancellationRequested)
                    {
                        // Read from ff
                        length = 0;
                        try
                        {
                            length = await inStream.ReadAsync(buffer, token);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"StreamListener READ error: {e}");
                            StartFfmpeg();
                            break;
                        }

                        // Read nothing, might be an error
                        if (length == 0)
                        {
                            retryCount++;
                            _logger.LogWarning("StreamListener READ nothing, retry {0}", retryCount);
                            if(retryCount >= 5)
                            {
                                _logger.LogError("StreamListener READ nothing, retry {0} times, restart stream", retryCount);
                                StartFfmpeg();
                                return;
                            }
                            await Task.Delay(500);
                            continue;
                        }
                        
                        // Write to all output streams
                        for (int i = 0; i < _streams.Count; i++)
                        {
                            try
                            {
                                await _streams[i].WriteAsync(buffer, 0, length);
                                //await _streams[i].FlushAsync();
                            }
                            catch (Exception e)
                            {
                                _logger.LogWarning($"StreamListener WRITE error: {e}");
                            }
                        }
                        _logger.LogTrace($"Copy {length} bytes to {_streams.Count} streams");
                    }
                    _logger.LogInformation("StreamListener stopped");
                }
            }
        }

        /// <summary>
        /// Register an output stream
        /// </summary>
        /// <param name="stream"></param>
        public void RegisterStream(Stream stream)
        {
            _streams.Add(stream);
        }

        /// <summary>
        /// Unregister an output stream
        /// </summary>
        /// <param name="stream"></param>
        public void UnregisterStream(Stream stream)
        {
            _streams.Remove(stream);
        }


    }
}
