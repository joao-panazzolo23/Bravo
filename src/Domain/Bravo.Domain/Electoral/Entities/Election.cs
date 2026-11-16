using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class Election : Entity
{
    public int TseElectionCode { get; private set; }
    public int Year { get; private set; }
    public int TypeCode { get; private set; }
    public string TypeName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly? ElectionDate { get; private set; }

    private Election() { }

    public static Election Create(
        int tseElectionCode,
        int year,
        int typeCode,
        string typeName,
        string description,
        DateOnly? electionDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tseElectionCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year);

        var election = new Election
        {
            TseElectionCode = tseElectionCode,
            Year = year,
            TypeCode = typeCode,
            TypeName = typeName,
            Description = description,
            ElectionDate = electionDate
        };

        var now = DateTime.UtcNow;
        election.SetCreatedAt(now);
        election.SetUpdatedAt(now);

        return election;
    }
}
