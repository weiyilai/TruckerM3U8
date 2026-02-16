using Microsoft.AspNetCore.Mvc;

namespace TruckerM3U8.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileCheckController : ControllerBase
{
    private const string StreamingUrl = "http://localhost:3378/mp3";
    private const string StreamDataEntry =
        @"stream_data[0]: ""http://localhost:3378/mp3|<color value=FFBBC539>TruckerM3U8|You Choose|TW|128|0""";

    private static readonly string DocumentsPath =
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private static readonly Dictionary<string, string> GamePaths = new()
    {
        ["ets2"] = Path.Combine(DocumentsPath, "Euro Truck Simulator 2", "live_streams.sii"),
        ["ats"]  = Path.Combine(DocumentsPath, "American Truck Simulator", "live_streams.sii"),
    };

    /// <summary>
    /// Check whether live_streams.sii in ETS2/ATS contains our streaming URL.
    /// -1 = file not found, 0 = file found but URL missing, 1 = URL found.
    /// </summary>
    [HttpGet("liveStreamListCheck")]
    public IActionResult LiveStreamListCheck()
    {
        var result = new Dictionary<string, int>();
        foreach (var (game, path) in GamePaths)
        {
            result[game] = CheckFile(path);
        }
        return Ok(result);
    }

    /// <summary>
    /// If the check state is 0 (file exists but URL missing), inject our stream entry.
    /// Returns the same format as the check endpoint after the operation.
    /// </summary>
    [HttpGet("liveStreamListApply")]
    public IActionResult LiveStreamListApply()
    {
        foreach (var (_, path) in GamePaths)
        {
            ApplyIfNeeded(path);
        }

        // Return the updated state
        var result = new Dictionary<string, int>();
        foreach (var (game, path) in GamePaths)
        {
            result[game] = CheckFile(path);
        }
        return Ok(result);
    }

    // ─── helpers ────────────────────────────────────────────────────

    private static int CheckFile(string path)
    {
        if (!System.IO.File.Exists(path))
            return -1;

        var content = System.IO.File.ReadAllText(path);
        return content.Contains(StreamingUrl, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static void ApplyIfNeeded(string path)
    {
        if (!System.IO.File.Exists(path))
            return;

        var content = System.IO.File.ReadAllText(path);

        // Already has our URL  skip
        if (content.Contains(StreamingUrl, StringComparison.OrdinalIgnoreCase))
            return;

        var lines = System.IO.File.ReadAllLines(path).ToList();

        // --- locate the "stream_data" count line and existing entries ---
        int countLineIndex = -1;
        int firstDataLineIndex = -1;
        int currentCount = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();

            // Match the count line, e.g.  stream_data: 5
            if (trimmed.StartsWith("stream_data:", StringComparison.Ordinal)
                && !trimmed.StartsWith("stream_data[", StringComparison.Ordinal))
            {
                countLineIndex = i;
                var parts = trimmed.Split(':');
                if (parts.Length >= 2)
                    int.TryParse(parts[1].Trim(), out currentCount);
            }

            // Find the first stream_data[0] line
            if (trimmed.StartsWith("stream_data[0]", StringComparison.Ordinal) && firstDataLineIndex == -1)
            {
                firstDataLineIndex = i;
            }
        }

        // Could not understand the file structure  bail out
        if (countLineIndex == -1)
            return;

        // --- shift existing indices by +1 (from highest to lowest to avoid collisions) ---
        for (int idx = currentCount - 1; idx >= 0; idx--)
        {
            string oldPrefix = $"stream_data[{idx}]";
            string newPrefix = $"stream_data[{idx + 1}]";

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith(oldPrefix, StringComparison.Ordinal))
                {
                    lines[i] = lines[i].Replace(oldPrefix, newPrefix);
                }
            }
        }

        // --- insert our new entry at index 0 ---
        int insertAt = firstDataLineIndex != -1 ? firstDataLineIndex : countLineIndex + 1;
        // Detect indentation from surrounding lines
        string indent = " ";
        if (firstDataLineIndex != -1 && firstDataLineIndex < lines.Count)
        {
            var existingLine = lines[firstDataLineIndex]; // already shifted to [1]
            indent = existingLine.Substring(0, existingLine.Length - existingLine.TrimStart().Length);
        }

        lines.Insert(insertAt, indent + StreamDataEntry);

        // --- update the count ---
        var leadingWhitespace = lines[countLineIndex].Substring(0,
            lines[countLineIndex].Length - lines[countLineIndex].TrimStart().Length);
        lines[countLineIndex] = $"{leadingWhitespace}stream_data: {currentCount + 1}";

        System.IO.File.WriteAllLines(path, lines);
    }
}
