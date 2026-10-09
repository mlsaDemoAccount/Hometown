using System;
using System.Collections.Generic;

// JSON shapes for the memory endpoints (GET /api/memories/{landmarkId}, POST /api/memories).

[Serializable]
public class MemoryEntry
{
    public string id;
    public string landmarkId;
    public string title;
    public string description;
    public string imageUrl;
    public string createdUtc;

    /// "12 Oct 2026" - or empty if the server date can't be read.
    public string DisplayDate
    {
        get
        {
            return DateTime.TryParse(createdUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d.ToLocalTime().ToString("dd MMM yyyy")
                : "";
        }
    }
}

/// JsonUtility can't read a bare JSON array, so the list response is wrapped as {"items": [...]}.
[Serializable]
public class MemoryEntryList
{
    public List<MemoryEntry> items = new List<MemoryEntry>();
}
