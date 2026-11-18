using System.IO;

namespace MediaVacuum.Services;

public sealed record CommandLineOptions(
    bool Install,
    bool Uninstall,
    string? Folder,
    string? Url,
    bool Quiet)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var install = false;
        var uninstall = false;
        var quiet = false;
        string? folder = null;
        string? url = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--install":
                    install = true;
                    continue;
                case "--uninstall":
                    uninstall = true;
                    continue;
                case "--quiet":
                    quiet = true;
                    continue;
                case "--folder":
                    if (i + 1 < args.Length)
                    {
                        folder = args[++i];
                    }
                    continue;
                case "--url":
                    if (i + 1 < args.Length)
                    {
                        url = args[++i];
                    }
                    continue;
            }

            if (arg.StartsWith("--folder=", StringComparison.OrdinalIgnoreCase))
            {
                folder = arg["--folder=".Length..];
                continue;
            }

            if (arg.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))
            {
                url = arg["--url=".Length..];
                continue;
            }

            if (Directory.Exists(arg))
            {
                folder ??= arg;
                continue;
            }

            if (Uri.TryCreate(arg, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                url ??= arg;
            }
        }

        return new CommandLineOptions(install, uninstall, folder, url, quiet);
    }
}
