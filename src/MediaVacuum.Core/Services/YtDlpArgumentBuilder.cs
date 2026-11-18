using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Core.Services;

public static class YtDlpArgumentBuilder
{
    public const string ProgressTemplate =
        "download:MVPROG|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.percent)s|%(progress.speed)s|%(progress.eta)s";

    public const string PostProcessTemplate = "postprocess:MVPOST|%(progress.status)s";

    public static List<string> Build(
        DownloadItem item,
        DownloadOptions options,
        string? ffmpegPath = null)
    {
        var args = new List<string>
        {
            "--newline",
            "--no-color",
            "--no-simulate",
            "--progress",
            "--progress-template", ProgressTemplate,
            "--progress-template", PostProcessTemplate,
            "--print", "before_dl:MVNAME|%(filepath)s",
            "--print", "after_move:MVFILE|%(filepath)s|%(filesize)s",
            "--no-playlist",
            "--windows-filenames",
            "--trim-filenames", "180",
            "--retries", "10",
            "--fragment-retries", "10"
        };

        if (!string.IsNullOrWhiteSpace(ffmpegPath))
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegPath);
        }

        if (!string.IsNullOrWhiteSpace(options.JsRuntime))
        {
            args.Add("--js-runtimes");
            args.Add(options.JsRuntime);
        }

        if (options.OverwriteExisting)
        {
            args.Add("--force-overwrites");
        }

        if (options.ConcurrentFragments > 1)
        {
            args.Add("--concurrent-fragments");
            args.Add(options.ConcurrentFragments.ToString());
        }

        if (options.LimitRateKb > 0)
        {
            args.Add("--limit-rate");
            args.Add($"{options.LimitRateKb}K");
        }

        if (options.Mode == DownloadMode.Audio)
        {
            args.Add("-x");
            args.Add("--audio-format");
            args.Add(FormatSelector.NormalizeAudioFormat(options.AudioFormat));
            args.Add("--audio-quality");
            args.Add("0");
        }
        else
        {
            args.Add("-f");
            args.Add(FormatSelector.Build(options));
        }

        if (options.EmbedMetadata)
        {
            args.Add("--embed-metadata");
        }

        if (options.EmbedThumbnail || options.WriteThumbnail)
        {
            args.Add("--convert-thumbnails");
            args.Add("jpg");
        }

        if (options.EmbedThumbnail)
        {
            args.Add("--embed-thumbnail");
        }

        if (options.WriteThumbnail)
        {
            args.Add("--write-thumbnail");
        }

        if (options.WriteSubtitles || options.EmbedSubtitles)
        {
            args.Add("--write-subs");
            args.Add("--sub-langs");
            args.Add(string.IsNullOrWhiteSpace(options.SubtitleLanguages) ? "en" : options.SubtitleLanguages);
        }

        if (options.EmbedSubtitles)
        {
            args.Add("--embed-subs");
        }

        if (options.Mode == DownloadMode.Video)
        {
            var container = FormatSelector.NormalizeContainer(options.VideoContainer);
            switch (container)
            {
                case "mp4":
                case "mkv":
                case "webm":
                    args.Add("--merge-output-format");
                    args.Add(container);
                    args.Add("--remux-video");
                    args.Add(container);
                    break;
                case "mp4-recode":
                    args.Add("--recode-video");
                    args.Add("mp4");
                    break;
            }
        }

        args.Add("-P");
        args.Add(options.OutputDirectory);

        args.Add("-o");
        args.Add(FilenameService.BuildOutputTemplate(item, options));

        args.Add(item.Url);
        return args;
    }
}
