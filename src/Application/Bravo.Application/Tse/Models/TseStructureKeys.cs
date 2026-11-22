using Bravo.Domain.Electoral.Models;

namespace Bravo.Application.Tse.Models;

/// <summary>
/// The distinct geographic and electoral keys a results file references, collected during the
/// structure pass.
/// </summary>
/// <remarks>
/// Only distinct compact keys are kept: municipality code to name, and per (municipality, zone)
/// the section numbers and polling place numbers it contains. An 18-million-row file collapses
/// to a few thousand entries, so this replaces the multi-gigabyte row list the import used to
/// hold while preserving exactly the information structure preparation needs.
/// </remarks>
public sealed class TseStructureKeys
{
    private readonly Dictionary<(string Code, int Zone), ZoneKeys> _zones = [];
    private readonly Dictionary<string, string> _municipalityNames = new(StringComparer.Ordinal);
    private readonly HashSet<(int ElectionCode, short Round)> _elections = [];

    public void Observe(TseSectionResultRow row)
    {
        _municipalityNames[row.TseMunicipalityCode] = row.MunicipalityName;
        _elections.Add((row.ElectionCode, row.Round));

        var key = (row.TseMunicipalityCode, row.ZoneNumber);

        if (!_zones.TryGetValue(key, out var zone))
        {
            zone = new ZoneKeys();
            _zones[key] = zone;
        }

        zone.Sections.Add(row.SectionNumber);

        // A non-positive polling place number is a source sentinel ("not published"), not a
        // real location, so it must not become a PollingPlaces row.
        if (row.PollingPlaceNumber > 0)
            zone.Places.Add(row.PollingPlaceNumber);
    }

    public IReadOnlyCollection<string> MunicipalityCodes => _municipalityNames.Keys;

    public IReadOnlyCollection<(int ElectionCode, short Round)> Elections => _elections;

    public string MunicipalityName(string code)
        => _municipalityNames.TryGetValue(code, out var name)
            ? name
            : throw new KeyNotFoundException($"Municipality code '{code}' was not observed.");

    public IEnumerable<int> ZoneNumbers()
        => _zones.Keys.Select(key => key.Zone).Distinct();

    public IEnumerable<(string Code, int Zone, int Section)> Sections()
    {
        foreach (var (key, zone) in _zones)
        {
            foreach (var section in zone.Sections)
                yield return (key.Code, key.Zone, section);
        }
    }

    public IEnumerable<(string Code, int Zone, int Place)> PollingPlaces()
    {
        foreach (var (key, zone) in _zones)
        {
            foreach (var place in zone.Places)
                yield return (key.Code, key.Zone, place);
        }
    }

    /// <summary>Sections that belong to one (municipality, zone).</summary>
    public IReadOnlyCollection<int> SectionsFor(string code, int zone)
        => _zones.TryGetValue((code, zone), out var keys) ? keys.Sections : [];

    public int MunicipalityCount => _municipalityNames.Count;

    public int SectionCount => _zones.Values.Sum(zone => zone.Sections.Count);

    public int PollingPlaceCount => _zones.Values.Sum(zone => zone.Places.Count);

    /// <summary>
    /// Set once a sentinel (non-positive) polling place number is observed, so the caller can
    /// warn a single time per scope instead of once per row.
    /// </summary>
    public bool SawSentinelPollingPlace { get; set; }

    private sealed class ZoneKeys
    {
        public HashSet<int> Sections { get; } = [];

        public HashSet<int> Places { get; } = [];
    }
}
