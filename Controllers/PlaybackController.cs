using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TruckerM3U8.Services;

namespace TruckerM3U8.Controllers;


[ApiController]
[Route("api/[controller]")]
public class PlaybackController : ControllerBase
{
    protected readonly ILogger<PlaybackController> _logger;
    protected readonly RestreamService _restreamService;

    public PlaybackController(ILogger<PlaybackController> logger, RestreamService restreamService)
    {
        _logger = logger;
        _restreamService = restreamService;
    }

    private static readonly string RadioJsonPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "radio.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly object FileLock = new();

    /// <summary>
    /// GET /api/radiolist — return the full radio list.
    /// </summary>
    [HttpGet("radiolist")]
    public IActionResult GetRadioList()
    {
        var list = ReadRadioList();
        return Ok(list);
    }

    // 安全問題，POST DELETE 暫時停用

    /// <summary>
    /// POST /api/radiolist — add or update an entry (matched by name).
    /// Body: { "name": "...", "url": "..." }
    /// </summary>
    // [HttpPost("radiolist")]
    // public IActionResult PostRadioEntry([FromBody] RadioEntry entry)
    // {
    //     if (string.IsNullOrWhiteSpace(entry.name) || string.IsNullOrWhiteSpace(entry.url))
    //         return BadRequest("Both 'name' and 'url' are required.");

    //     lock (FileLock)
    //     {
    //         var list = ReadRadioList();

    //         var existing = list.FindIndex(r => r.name == entry.name);
    //         if (existing >= 0)
    //             list[existing] = entry;   // edit
    //         else
    //             list.Add(entry);          // create

    //         WriteList(list);
    //     }

    //     return Ok(ReadRadioList());
    // }

    /// <summary>
    /// DELETE /api/radiolist — remove an entry by name.
    /// Body: { "name": "..." }
    /// </summary>
    // [HttpDelete("radiolist")]
    // public IActionResult DeleteRadioEntry([FromBody] RadioEntry entry)
    // {
    //     if (string.IsNullOrWhiteSpace(entry.name))
    //         return BadRequest("'name' is required.");

    //     lock (FileLock)
    //     {
    //         var list = ReadRadioList();
    //         list.RemoveAll(r => r.name == entry.name);
    //         WriteList(list);
    //     }

    //     return Ok(ReadRadioList());
    // }

    // ─── Play back  ────────────────────────────────────────────────────

    /// <summary>
    /// /// GET /api/playback/sourceUrl — return the current source URL.
    /// </summary>
    [HttpGet("sourceUrl")]
    public IActionResult GetSourceUrl()
    {
        return Ok(_restreamService.SourceUrl);
    }

    /// <summary>
    /// POST /api/playback/playByName — start playing a radio by its name.
    /// </summary>
    [HttpPost("playByName")]
    public IActionResult Play([FromBody] string radioName)
    {
        var radioList = ReadRadioList();
        var entry = radioList.Find(r => r.name == radioName);
        if(entry == null)
            return NotFound($"Radio '{radioName}' not found.");

        try
        {
            _restreamService.SetSourceUrl(entry.url);
        }
        catch(NotSupportedException)
        {
            return BadRequest("This website is not supported. Try again later or use another url.");
        }
        catch
        {
            return StatusCode(500, "Failed to start playback. Try again later.");
        }

        return Ok(new
        {
            sourceUrl = _restreamService.SourceUrl,
            activeStreams = _restreamService.GetActiveStreams,
        });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _restreamService.StopPlayback();
        return Ok();
    }

    // ─── radio list helpers ────────────────────────────────────────────────────

    private static List<RadioEntry> ReadRadioList()
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

public record RadioEntry(string name, string url);
