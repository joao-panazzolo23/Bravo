using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class PollingPlace : Entity
{
    public Guid StateId { get; private set; }
    public Guid? MunicipalityId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public int TsePollingPlaceNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? Neighborhood { get; private set; }
    public string? ZipCode { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    private PollingPlace() { }

    public static PollingPlace Create(
        Guid stateId,
        Guid? municipalityId,
        Guid? zoneId,
        int tsePollingPlaceNumber,
        string name,
        string? address,
        string? neighborhood,
        string? zipCode,
        decimal? latitude,
        decimal? longitude)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tsePollingPlaceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var place = new PollingPlace
        {
            StateId = stateId,
            MunicipalityId = municipalityId,
            ZoneId = zoneId,
            TsePollingPlaceNumber = tsePollingPlaceNumber,
            Name = name,
            Address = address,
            Neighborhood = neighborhood,
            ZipCode = zipCode,
            Latitude = latitude,
            Longitude = longitude
        };

        var now = DateTime.UtcNow;
        place.SetCreatedAt(now);
        place.SetUpdatedAt(now);

        return place;
    }
}
