# Privacy Policy

MediaVacuum does not collect, store, or transmit any personal data. There is no telemetry, analytics, advertising or crash reporting.

## Network requests

MediaVacuum makes network requests only for the features you use:

- **yt-dlp** — downloading and updating `yt-dlp.exe` from GitHub Releases, and checking the latest release through the GitHub API.
- **MediaVacuum itself** — checking the latest MediaVacuum release through the GitHub API and downloading the new executable when a self-update is available.
- **FFmpeg** — downloading an FFmpeg build from gyan.dev or GitHub (BtbN) when you click **Download ffmpeg**.
- **Deno** — downloading the Deno JavaScript runtime from GitHub when you click **Download Deno**.
- **Media thumbnails** — fetching playlist/video thumbnails to display them in the download queue.
- **Media downloads** — performed by yt-dlp against the URLs you provide.

GitHub, gyan.dev, BtbN and the media websites receive the requests necessary to serve those files. Their own privacy policies apply.

## Local data

The following data stays on your computer, under `%LOCALAPPDATA%\MediaVacuum`:

- `config.json` — your settings
- `yt-dlp.exe` and `ffmpeg\` — managed tools
- `thumbnails\` — a cache of queue thumbnails
- `logs\app.log` — technical logs (rotated automatically)
- `Translations\` — optional user translation overrides

Your downloaded media is stored in the output folder you choose. MediaVacuum never uploads logs, configuration or downloaded files.

## Uninstall

Uninstalling MediaVacuum removes only its own application data, and only if you confirm it. Your downloads are never deleted.

This policy applies to the latest release of MediaVacuum.
