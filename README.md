# TruckerM3U8 v2

Convert (not only) m3u8 stream to mp3 that ETS2/ATS can recognize.  
Now with an interactive dashboard!

![eng_preview](https://static.jcxyis.com/images/Mv03k359QB.webp)

## Features

- **Stream Converter**: Listen to YouTube LIVE or M3U8 radio directly in ETS2/ATS.
- **Interactive Web Dashboard**: A beautiful interface to manage your radios and see telemetry data.
- **Built-in Telemetry**: Synchronize with Euro Truck Simulator 2 / American Truck Simulator to display your truck's data in the dashboard!
- **Auto-Installation**: Easy one-click setup to inject the stream link into your truck's radio and install the telemetry plugin.

## How to use

### Installation

1. Download `TruckerM3U8_x64.zip` from [the release page](https://github.com/JCxYIS/TruckerM3U8/releases/latest)

2. Unzip and execute `TruckerM3U8.exe`, a browser screen will show up. (If no browser is opened, you can open it manually by visiting `http://localhost:3378/`)

3. Click ℹ️ icon on the upper right to open Settings.

4. Click "Add URL To Stream List" button to automatically add TruckerM3U8 URL into the game.

5. (Optional) If you want to use the dashboard function, click the "Install Telemetry DLL" button.

6. Pick a radio using "Radio Stations" button, wait a little moment until the radio name shows up.

7. Launch the game, open up the radio menu, select `TruckerM3U8`, and you are good to go!

## Interface

![info](https://static.jcxyis.com/images/5XxL2OFSZd.webp)

![radiostations](https://static.jcxyis.com/images/DiwW0PAxMR.webp)

## Adding your own radio

Add your own radio to `Data/radio.json`.  
The application will automatically parse and display it in the radio station list.

> [!TIP]  
> Youtube links are supported too.  
> For supported websites, please refer to [yt-dlp supported websites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md) page.

![radio.json](https://static.jcxyis.com/images/sLDGa5orJB.webp)

---

## For developers

### How it works

```text
+----------+      +--------+      (Download & convert via yt-dlp & FFMPEG)
| YouTube/ | ---> | FFmpeg |
| M3U8     |      +--------+
+----------+          |
                      | Output MP3 stream (Port 1049)
                      v
+-------------------------------+  SignalR (Port 3378)   +-----------------+
| TruckerM3U8 (ASP.NET Core)    | <--------------------> | Web Dashboard   |
+-------------------------------+                        +-----------------+
                      |
                      | HTTP GET /mp3 (Port 3378)
                      v
              +-------------+
              | User (ETS2) |
              +-------------+
```

### How to build

1. Ensure you have the `.NET SDK` installed (.NET 10).
2. Clone the repository.
3. The project requires third-party tools (like `ffmpeg.exe`, `yt-dlp.exe`, and `scs-telemetry.dll`). They should be placed in the `ThirdParty` directory.
4. Run `dotnet run` to start the development server, or `dotnet publish -c Release` to build an executable.

### Routes

- `/dashboard.html` : Main Dashboard. Shows basic telemetry data and radio player.
- `/telemetry.html` : Telemetry Data. A straightforward table showing raw telemetry fields and their real-time values, useful for debugging or monitoring.
- `/settings.html` : Old page.
- `/mp3` : MP3 Stream if radio is playing. (Used by game)

### Dashboard simulation

To simulate telemetry data without the game running, you can press 'm' key to start the "mocked" simulation in `dashboard` page.

- `m`: start/stop mocked simulation
- `s`: show speed limit
- `c`: set cruise control
- `e`: start engine
- ` `: toggle parking brake
- `i`: reset trip odometer
- `[`: left blinker
- `]`: right blinker
- `l`: low beam light
- `k`: high beam light
- `f`: set fuel amount (randomized)
- `r`: set fatigue meter (randomized)
- `j`: set job (randomized)
- `d`: set damage (randomized)

## Third Party Tools Used

- [FFmpeg](https://github.com/FFmpeg/FFmpeg) to convert the stream to mp3 format.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) to resolves the direct media URL.
- [scs-sdk-plugin](https://github.com/RenCloud/scs-sdk-plugin) to push game data to our application

## Happy Trucking 🥰🥰🥰

![my_truck_v2_🥰🥰🥰](https://static.jcxyis.com/images/xn1QjplX7A.webp)
