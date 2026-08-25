# Tome Unlocker for Dead by Daylight

Standalone FiddlerCore proxy that intercepts DBD traffic, captures tome selections, and automatically completes tome challenges via the DBD API.

## How It Works

1. **Proxy**: Starts a FiddlerCore HTTPS proxy on a random port and registers as the system proxy
2. **API Key Capture**: Intercepts `/api/v1/config` request to extract the `api-key` header
3. **Tome Selection**: Captures the request/response when you select a tome challenge node (`update/active-node`)
4. **Match Detection**: Detects when a match ends (`api/v1/match` with status `CLOSED`)
5. **Auto-Complete**: Calls DBD's `quest-progress-v3` API endpoint with inflated progression values to instantly complete the active tome challenge

## Usage

1. Launch `TomeUnlocker.exe` as Administrator (required for system proxy registration)
2. Wait for the loading overlay to clear (cat picture with "Initializing...")
3. Launch Dead by Daylight
4. Select a tome challenge in-game (it will be auto-captured)
5. Play a match (any role)
6. When the match ends, the tome challenge will be auto-completed

## Features

- Dark theme WPF UI matching UnlockerCoreV2 style
- Loading overlay with cat placeholder image at startup
- Real-time status display: API key, platform, tome active, match ID
- Auto-Select Next Tome mode (completes and advances automatically)
- Copy API key button
- Automatic SSL certificate generation and trust setup
- Blocks logout requests and client version checks

## Build Dependencies

- .NET 8.0 SDK
- FiddlerCore.dll (not redistributed - obtain from Telerik or use an existing installation)
- NuGet packages (auto-restored): Newtonsoft.Json, RestSharp, BouncyCastle.Cryptography

## Legal

This project is for educational and research purposes only. Use at your own risk.
