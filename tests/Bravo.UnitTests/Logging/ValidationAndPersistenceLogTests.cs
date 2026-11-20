using Bravo.Application.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed class ValidationAndPersistenceLogTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void ValidationEventsEmitExpectedProperties()
    {
        ValidationLog.RecordRejected(_logger, "TSE:cand:2022", "Invalid CPF format");
        ValidationLog.UnresolvedReference(_logger, "TSE:cand:2022", "Party", "99");
        ValidationLog.ValidationCompleted(_logger, "TSE:cand:2022", 500, 2);
        ValidationLog.SyntheticScopeCreated(_logger, "ZZ");
        ValidationLog.MissingColumnsDetected(_logger, "file.csv", "v1", "colA, colB");
        ValidationLog.GeographyResolutionSkipped(_logger, "candidates", 2022, "AC");
        ValidationLog.IbgeRecordDiscarded(_logger, "state", 11, "Missing sail");

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(7);

        entries[0].EventId.Id.Should().Be(1301);
        entries[0].Properties["Reason"].Should().Be("Invalid CPF format");

        entries[1].EventId.Id.Should().Be(1302);
        entries[1].Properties["ReferenceType"].Should().Be("Party");
        entries[1].Properties["ReferenceValue"].Should().Be("99");

        entries[2].EventId.Id.Should().Be(1303);
        entries[2].Properties["ValidCount"].Should().Be(500);
        entries[2].Properties["InvalidCount"].Should().Be(2);

        entries[3].EventId.Id.Should().Be(1304);
        entries[3].Properties["Scope"].Should().Be("ZZ");

        entries[4].EventId.Id.Should().Be(1305);
        entries[4].Properties["MissingColumns"].Should().Be("colA, colB");

        entries[5].EventId.Id.Should().Be(1306);
        entries[5].Properties["EntityType"].Should().Be("candidates");

        entries[6].EventId.Id.Should().Be(1307);
        entries[6].Properties["EntityType"].Should().Be("state");
    }

    [Fact]
    public void PersistenceEventsEmitExpectedProperties()
    {
        var jobId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var dbEx = new InvalidOperationException("PostgreSQL deadlocked");

        PersistenceLog.BatchPersisting(_logger, "Candidate", 1000);
        PersistenceLog.BatchPersisted(_logger, "Candidate", 1000, 45.2);
        PersistenceLog.PersistenceFailed(_logger, "Candidate", dbEx);
        PersistenceLog.CheckpointPersisted(_logger, jobId, unitId, "pos-500");
        PersistenceLog.ProgressPersistFailed(_logger, jobId, dbEx);
        PersistenceLog.IntegrityViolation(_logger, "Candidate", "FK constraint violation", dbEx);
        PersistenceLog.TransactionFailed(_logger, "Candidate", dbEx);
        PersistenceLog.ReferenceDataUpserted(_logger, 2022, 2, 7, 27, 3);
        PersistenceLog.CandidateRegistrationsUpserted(_logger, 542, 2022, 27);
        PersistenceLog.SectionResultsUpserted(_logger, 10000, 2022, "AC");
        PersistenceLog.CandidatesUpsertedForState(_logger, 542, 2022, "AC");
        PersistenceLog.IbgeImportCompleted(_logger, 27, 5570);

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(12);

        entries[0].EventId.Id.Should().Be(1401);
        entries[0].Properties["EntityName"].Should().Be("Candidate");
        entries[0].Properties["Count"].Should().Be(1000);

        entries[1].EventId.Id.Should().Be(1402);
        entries[1].Properties["ElapsedMs"].Should().Be(45.2);

        entries[2].EventId.Id.Should().Be(1403);
        entries[2].Exception.Should().BeSameAs(dbEx);

        entries[3].EventId.Id.Should().Be(1404);
        entries[3].Properties["Position"].Should().Be("pos-500");

        entries[4].EventId.Id.Should().Be(1405);
        entries[4].Exception.Should().BeSameAs(dbEx);

        entries[5].EventId.Id.Should().Be(1406);
        entries[5].Exception.Should().BeSameAs(dbEx);

        entries[6].EventId.Id.Should().Be(1407);
        entries[6].Exception.Should().BeSameAs(dbEx);

        entries[7].EventId.Id.Should().Be(1408);
        entries[7].Properties["Elections"].Should().Be(2);

        entries[8].EventId.Id.Should().Be(1409);
        entries[8].Properties["States"].Should().Be(27);

        entries[9].EventId.Id.Should().Be(1410);
        entries[9].Properties["State"].Should().Be("AC");

        entries[10].EventId.Id.Should().Be(1411);
        entries[10].Properties["Count"].Should().Be(542);

        entries[11].EventId.Id.Should().Be(1412);
        entries[11].Properties["States"].Should().Be(27);
        entries[11].Properties["Municipalities"].Should().Be(5570);
    }
}
