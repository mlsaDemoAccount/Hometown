using System.Text.RegularExpressions;
using UnityEngine;

/// What a joining player needs to find the host's room: a 4-digit room code (easy to type), plus
/// optionally the Photon region the host is in. Players read out / type just the digits ("4821");
/// the QR code also carries the region ("hometown://join/4821-eu") so a scan goes straight to the
/// right place, while a typed code searches the regions (see NetworkSessionManager).
/// (Phase 3 will switch the QR to the https invite link; TryParse already reads the last path
/// segment of any link, so both forms keep working.)
public struct JoinCode
{
    public string room;    // Photon session name: 4 digits, e.g. "4821"
    public string region;  // Photon region token, e.g. "eu" (may be empty)

    private static readonly Regex RoomPattern = new Regex("^[0-9]{4}$");
    private static readonly Regex RegionPattern = new Regex("^[a-z]{2,5}$");

    /// What players see and type: just the digits.
    public string ToCode() => room;

    /// What the QR carries: the digits plus the region, when known.
    public string ToPayload() => "hometown://join/" + (string.IsNullOrEmpty(region) ? room : room + "-" + region);

    public static string GenerateRoom() => Random.Range(1000, 10000).ToString(); // no leading zero

    /// Accepts "4821", "4821-eu", or any link / path ending in one of those.
    public static bool TryParse(string text, out JoinCode info)
    {
        info = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string value = text.Trim();
        int query = value.IndexOfAny(new[] { '?', '#' });
        if (query >= 0) value = value.Substring(0, query);
        value = value.TrimEnd('/');
        int slash = value.LastIndexOf('/');
        if (slash >= 0) value = value.Substring(slash + 1);

        string room = value;
        string region = "";
        int dash = value.IndexOf('-');
        if (dash >= 0)
        {
            room = value.Substring(0, dash);
            region = value.Substring(dash + 1).ToLowerInvariant();
        }

        if (!RoomPattern.IsMatch(room)) return false;
        if (region.Length > 0 && !RegionPattern.IsMatch(region)) return false;

        info = new JoinCode { room = room, region = region };
        return true;
    }

    /// Photon reports regions like "eu" or "eu/*" - keep just the letters.
    public static string CleanRegion(string region)
    {
        if (string.IsNullOrEmpty(region)) return "";
        var match = Regex.Match(region.ToLowerInvariant(), "^[a-z]+");
        return match.Success ? match.Value : "";
    }
}
