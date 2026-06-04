using System.Text.RegularExpressions;

namespace IsDB.Hospitality.Application.Common.Helpers;

/// <summary>
/// Shared utility for normalising IATA flight numbers to a canonical form.
///
/// Rules applied in order:
///   1. Remove all spaces (including leading/trailing)          "TK 334"  → "TK334"
///   2. Uppercase everything                                    "Tk334"   → "TK334"
///   3. Remove hyphens between the letter code and digits       "EK-585"  → "EK585"
///   4. Strip leading zeros from the numeric suffix             "TK0334"  → "TK334"
///   5. Strip trailing non-alphanumeric characters              "TK8440*" → "TK8440"
///
/// Free-text entries like "Flydubai FZ 707" cannot be safely auto-fixed and
/// are returned as-is after rules 1–2 (spaces removed, uppercased).
/// Those must be corrected directly in EventsAir.
/// </summary>
public static class FlightNumberHelper
{
    // Matches: 1–3 letters, optional leading zeros, then digits (captures code + number)
    private static readonly Regex _leadingZeroPattern =
        new(@"^([A-Za-z]{1,3})0+(\d+.*)$", RegexOptions.Compiled);

    // Matches a hyphen between the airline code and the numeric part.
    // Airline codes can be 2-3 chars and may contain digits (e.g. J2, B6, 9W).
    private static readonly Regex _hyphenPattern =
        new(@"^([A-Za-z0-9]{1,3})-([0-9].*)$", RegexOptions.Compiled);

    // Matches trailing non-alphanumeric characters (e.g. asterisk, dot)
    private static readonly Regex _trailingGarbagePattern =
        new(@"[^A-Za-z0-9]+$", RegexOptions.Compiled);

    /// <summary>
    /// Normalises an IATA flight number to canonical form.
    /// Returns an empty string for null/whitespace input.
    /// Returns the cleaned string (spaces removed, uppercased) for entries that
    /// do not match the expected IATA pattern (e.g. "Flydubai FZ 707").
    /// </summary>
    public static string Normalise(string? flightNumber)
    {
        if (string.IsNullOrWhiteSpace(flightNumber))
            return string.Empty;

        // Rule 1 + 2: Remove all spaces and uppercase
        var s = flightNumber.Replace(" ", "").Trim().ToUpperInvariant();

        // Rule 3: Remove hyphen between letter code and digits (e.g. "EK-585" → "EK585")
        var hyphenMatch = _hyphenPattern.Match(s);
        if (hyphenMatch.Success)
            s = hyphenMatch.Groups[1].Value + hyphenMatch.Groups[2].Value;

        // Rule 4: Strip leading zeros from the numeric suffix (e.g. "TK0334" → "TK334")
        var zeroMatch = _leadingZeroPattern.Match(s);
        if (zeroMatch.Success)
            s = zeroMatch.Groups[1].Value + zeroMatch.Groups[2].Value;

        // Rule 5: Strip trailing non-alphanumeric characters (e.g. "TK8440*" → "TK8440")
        s = _trailingGarbagePattern.Replace(s, "");

        return s.ToUpperInvariant();
    }
}
