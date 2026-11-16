using System.Globalization;

namespace Bravo.Infrastructure.Tse.Parsing;

/// <summary>
/// Culture-invariant parsing helpers for TSE source values.
/// </summary>
public static class TseValues
{
    private static readonly string[] Sentinels = ["#NULO", "#NE", "-1", "-3", ""];

    public static bool IsSentinel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();

        return Sentinels.Contains(trimmed, StringComparer.OrdinalIgnoreCase);
    }

    public static string? Optional(string? value)
        => IsSentinel(value) ? null : value!.Trim();

    public static int ParseInt(string? value)
    {
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"'{value}' is not a valid integer.");

        return parsed;
    }

    public static int? ParseNullableInt(string? value)
        => IsSentinel(value) ? null : ParseInt(value);

    public static long ParseLong(string? value)
    {
        if (!long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"'{value}' is not a valid long integer.");

        return parsed;
    }

    public static long? ParseNullableLong(string? value)
        => IsSentinel(value) ? null : ParseLong(value);

    public static DateOnly? ParseNullableDate(string? value)
    {
        if (IsSentinel(value))
            return null;

        if (!DateOnly.TryParseExact(
                value!.Trim(),
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new FormatException($"'{value}' is not a valid date (expected dd/MM/yyyy).");
        }

        return parsed;
    }

    /// <summary>
    /// TSE coordinates use a comma as the decimal separator (for example "-19,0348876").
    /// </summary>
    public static decimal? ParseNullableCoordinate(string? value)
    {
        if (IsSentinel(value))
            return null;

        var normalised = value!.Trim().Replace(',', '.');

        if (!decimal.TryParse(
                normalised,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new FormatException($"'{value}' is not a valid coordinate.");
        }

        return parsed;
    }
}
