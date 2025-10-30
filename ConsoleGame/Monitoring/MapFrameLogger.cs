using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleGame.Monitoring
{
    /// <summary>
    /// Logs map frames to text files with human-readable ASCII representation
    /// </summary>
    public class MapFrameLogger : IDisposable
    {
        private readonly string _outputPath;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private StreamWriter? _currentWriter;
        private string? _currentFileName;
        private int _framesInCurrentFile = 0;
        private const int MaxFramesPerFile = 100; // Rotate log files after this many frames

        public MapFrameLogger(string outputPath)
        {
            _outputPath = outputPath;
            
            // Ensure output directory exists
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        /// <summary>
        /// Logs a frame update to file
        /// </summary>
        public async Task LogFrameAsync(MapFrameUpdate frame)
        {
            await _writeLock.WaitAsync();
            try
            {
                // Rotate file if needed
                if (_currentWriter == null || _framesInCurrentFile >= MaxFramesPerFile)
                {
                    await RotateLogFileAsync();
                }

                if (_currentWriter != null)
                {
                    await WriteFrameToFileAsync(_currentWriter, frame);
                    _framesInCurrentFile++;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Creates a new log file and closes the old one
        /// </summary>
        private async Task RotateLogFileAsync()
        {
            if (_currentWriter != null)
            {
                await _currentWriter.FlushAsync();
                _currentWriter.Close();
                _currentWriter.Dispose();
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFileName = Path.Combine(_outputPath, $"game_monitor_{timestamp}.txt");
            _currentWriter = new StreamWriter(_currentFileName, false, Encoding.UTF8);
            _framesInCurrentFile = 0;

            // Write header
            await _currentWriter.WriteLineAsync("================================================================================");
            await _currentWriter.WriteLineAsync($"Game Monitoring Log - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await _currentWriter.WriteLineAsync("================================================================================");
            await _currentWriter.WriteLineAsync();
        }

        /// <summary>
        /// Writes a single frame to the log file
        /// </summary>
        private async Task WriteFrameToFileAsync(StreamWriter writer, MapFrameUpdate frame)
        {
            await writer.WriteLineAsync($"────────────────────────────────────────────────────────────────────────────────");
            await writer.WriteLineAsync($"Frame #{frame.FrameNumber} - {frame.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
            await writer.WriteLineAsync($"────────────────────────────────────────────────────────────────────────────────");
            
            // Write perception summary
            if (frame.RawPerception != null)
            {
                await writer.WriteLineAsync($"Player Location: ({frame.RawPerception.PlayerLocation.X}, {frame.RawPerception.PlayerLocation.Y}, {frame.RawPerception.PlayerLocation.Z})");
                await writer.WriteLineAsync($"Player Heading: {frame.RawPerception.PlayerHeading}");
                await writer.WriteLineAsync($"Visible Tiles: {frame.RawPerception.Visuals.Count}");
                
                if (frame.RawPerception.Inventory != null)
                {
                    await writer.WriteLineAsync($"Inventory: {frame.RawPerception.Inventory.Items.Count}/{frame.RawPerception.Inventory.Capacity}");
                }
                
                await writer.WriteLineAsync();
            }

            // Write ASCII map
            await writer.WriteLineAsync("Map:");
            await writer.WriteLineAsync(frame.AsciiMap.ToAsciiString());
            await writer.WriteLineAsync();

            await writer.FlushAsync();
        }

        public void Dispose()
        {
            _writeLock.Wait();
            try
            {
                if (_currentWriter != null)
                {
                    _currentWriter.Flush();
                    _currentWriter.Close();
                    _currentWriter.Dispose();
                }
            }
            finally
            {
                _writeLock.Release();
                _writeLock.Dispose();
            }
        }
    }
}

