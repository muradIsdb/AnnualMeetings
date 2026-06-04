using System.Text.RegularExpressions;

namespace IsDB.Hospitality.Application.Common.Helpers;

/// <summary>
/// Shared utility for normalising IATA flight numbers to a canonical form.
/// Canonical form: uppercase airline code (2-3 letters) + numeric suffix with no leading zeros.
/// Examples: "TK 0334" → "TK334", "EK0583" → "EK583", " LH612" → "LH612", "FZ 707" → "FZ707".
/// </summary>
public static class FlightNumberHelper
{
    private static readonly Regex _leadingZeroPattern =
        new(@"^([A-Za-z]{1,3})0+(\d+.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Normalises an IATA flight number to canonical form.
    /// Returns the original string (trimmed, uppercased) if it does not match the expected pattern.
    /// </summary>
    public static string Normalise(string? flightNumber)
    {
        if (string.IsNullOrWhiteSpace(flightNumber))
            return string.Empty;

        // Remove all spaces (including leading/trailing)
        var s = flightNumber.Replace(" ", "").Trim().ToUpperInvariant();

        // Strip leading zeros from the numeric suffix
        var match = _leadingZeroPattern.Match(s);
        if (match.Success)
            return (match.Groups[1].Value + match.Groups[2].Value).ToUpperInvariant();

        return s;
    }
}
