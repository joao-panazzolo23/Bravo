using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// An electoral event as published by the source (for example, the 2022 general election).
/// The TSE code is stored as a string because official identifiers must preserve their
/// original representation.
/// </summary>
public class Election : Entity
{
    public string TseCode { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int TypeCode { get; private set; }
    public string TypeName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTime ElectionDate { get; private set; }

    public ICollection<ElectionRound> Rounds { get; private set; } = [];

    private Election() { }

    public static Election Create(
        string tseCode,
        int year,
        int typeCode,
        string typeName,
        string name,
        DateTime electionDate)
    {
        var election = new Election
        {
            TseCode = tseCode,
            Year = year,
            TypeCode = typeCode,
            TypeName = typeName,
            Name = name,
            ElectionDate = electionDate
        };
        election.SetCreatedAt(DateTime.UtcNow);
        election.SetUpdatedAt(DateTime.UtcNow);
        return election;
    }

    public void Update(int typeCode, string typeName, string name, DateTime electionDate)
    {
        TypeCode = typeCode;
        TypeName = typeName;
        Name = name;
        ElectionDate = electionDate;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
