using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace TruckerM3U8.Controllers;

public record RadioEntry(string name, string url);

[ApiController]
[Route("api/[controller]")]
public class PlaybackController : ControllerBase
{
    private static readonly string RadioJsonPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "radio.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly object FileLock = new();

    /// <summary>
    /// GET /api/playback/radiolist — return the full radio list.
    /// </summary>
    [HttpGet("radiolist")]
    public IActionResult GetRadioList()
    {
        var list = ReadList();
        return Ok(list);
    }

    /// <summary>
    /// POST /api/playback/radiolist — add or update an entry (matched by name).
    /// Body: { "name": "...", "url": "..." }
    /// </summary>
    [HttpPost("radiolist")]
    public IActionResult PostRadioEntry([FromBody] RadioEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.name) || string.IsNullOrWhiteSpace(entry.url))
            return BadRequest("Both 'name' and 'url' are required.");

        lock (FileLock)
        {
            var list = ReadList();

            var existing = list.FindIndex(r => r.name == entry.name);
            if (existing >= 0)
                list[existing] = entry;   // edit
            else
                list.Add(entry);          // create

            WriteList(list);
        }

        return Ok(ReadList());
    }

    /// <summary>
    /// DELETE /api/playback/radiolist — remove an entry by name.
    /// Body: { "name": "..." }
    /// </summary>
    [HttpDelete("radiolist")]
    public IActionResult DeleteRadioEntry([FromBody] RadioEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.name))
            return BadRequest("'name' is required.");

        lock (FileLock)
        {
            var list = ReadList();
            list.RemoveAll(r => r.name == entry.name);
            WriteList(list);
        }

        return Ok(ReadList());
    }

    // ─── helpers ────────────────────────────────────────────────────

    private static List<RadioEntry> ReadList()
    {
        if (!System.IO.File.Exists(RadioJsonPath))
            return new List<RadioEntry>();

        var json = System.IO.File.ReadAllText(RadioJsonPath);
        return JsonSerializer.Deserialize<List<RadioEntry>>(json, JsonOptions)
               ?? new List<RadioEntry>();
    }

    private static void WriteList(List<RadioEntry> list)
    {
        // Ensure directory exists
        var dir = Path.GetDirectoryName(RadioJsonPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonOptions);
        System.IO.File.WriteAllText(RadioJsonPath, json);
    }
}
