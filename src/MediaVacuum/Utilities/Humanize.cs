using System.Globalization;

namespace MediaVacuum.Utilities;

public static class Humanize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long? bytes)
    {
        if (bytes is null or <= 0)
        {
            return "—";
        }

        double value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 ? "0" : "0.#";
        return string.Format(CultureInfo.CurrentCulture, "{0} {1}", value.ToString(format, CultureInfo.CurrentCulture), Units[unit]);
    }

    public static string Speed(double? bytesPerSecond)
    {
        if (bytesPerSecond is null or <= 0)
        {
            return string.Empty;
        }

        return Bytes((long)bytesPerSecond.Value) + "/s";
    }

    public static string Duration(double? seconds)
    {
        if (seconds is null or < 0 || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value))
        {
            return "—";
        }

        var time = TimeSpan.FromSeconds(seconds.Value);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }
}
