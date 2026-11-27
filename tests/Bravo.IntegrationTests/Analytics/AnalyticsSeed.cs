using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Dapper;
using Npgsql;

namespace Bravo.IntegrationTests.Analytics;

/// <summary>
/// Builds a small, fully known electoral dataset for the analytics tests.
/// </summary>
/// <remarks>
/// The numbers are chosen so every expected value can be computed by hand:
///
/// Municipality ONE (2 zones, 3 sections), 2022 round 1, office TEST, valid votes 100:
///   section 101: A=60, B=30, party=10   -> valid 100, A=60%
///   section 102: A=45, B=45, blank=10   -> valid 90,  A=50%
///   section 103: A=0,  B=0,  null=5     -> valid 0   (zero-valid-vote edge case)
/// Municipality TWO (1 zone, 2 sections), 2022 round 1, valid votes 200:
///   section 201: A=0,  B=150            -> valid 150, A=0%
///   section 202: A=50, C=0              -> valid 50,  A=100%
/// Total for A: 155 of 390 valid = 39.74%.
///
/// Round 2 keeps only A and B, so the candidate composition differs from round 1: A=200 of 260
/// valid and B=60, which exercises round separation without assuming transfers.
///
/// 2018 round 1 (same office) has A=100 of 300 valid, giving a year comparison.
///
/// Candidate A has no identity; candidates B and C share one identity, which lets a test prove
/// identity aggregation works without inventing a party affiliation.
/// </remarks>
internal sealed class AnalyticsSeed
{
    public static async Task<AnalyticsSeedIds> CreateAsync(
        NpgsqlConnection connection,
        string suffix)
    {
        var ids = new AnalyticsSeedIds(
            StateId: Guid.CreateVersion7(),
            MunicipalityOneId: Guid.CreateVersion7(),
            MunicipalityTwoId: Guid.CreateVersion7(),
            ZoneOneId: Guid.CreateVersion7(),
            ZoneTwoId: Guid.CreateVersion7(),
            Section101Id: Guid.CreateVersion7(),
            Section102Id: Guid.CreateVersion7(),
            Section103Id: Guid.CreateVersion7(),
            Section201Id: Guid.CreateVersion7(),
            Section202Id: Guid.CreateVersion7(),
            Election2022Id: Guid.CreateVersion7(),
            RoundOneId: Guid.CreateVersion7(),
            RoundTwoId: Guid.CreateVersion7(),
            Election2018Id: Guid.CreateVersion7(),
            Round2018Id: Guid.CreateVersion7(),
            OfficeId: Guid.CreateVersion7(),
            PartyOneId: Guid.CreateVersion7(),
            PartyTwoId: Guid.CreateVersion7(),
            IdentityId: Guid.CreateVersion7(),
            CandidateA2022Id: Guid.CreateVersion7(),
            CandidateB2022Id: Guid.CreateVersion7(),
            CandidateC2022Id: Guid.CreateVersion7(),
            CandidateA2018Id: Guid.CreateVersion7(),
            CandidateB2018Id: Guid.CreateVersion7(),
            ActiveVersionId: Guid.CreateVersion7(),
            DatasetId: Guid.CreateVersion7());

        await InsertReferenceAsync(connection, ids, suffix);
        await InsertStructureAsync(connection, ids);
        await InsertCandidatesAsync(connection, ids, suffix);
        await InsertResultsAsync(connection, ids);

        return ids;
    }

    private static async Task InsertReferenceAsync(
        NpgsqlConnection connection,
        AnalyticsSeedIds ids,
        string suffix)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO "States" ("Id","IbgeCode","Abbreviation","Name","Region","CreatedAt","UpdatedAt")
            VALUES (@StateId, @StateIbge, @StateAcronym, 'TEST STATE', 'SUDESTE', now(), now());

            INSERT INTO "Municipalities" ("Id","IbgeCode","Name","StateId","CreatedAt","UpdatedAt")
            VALUES (@MunicipalityOneId, @MunicipalityOneIbge, 'MUNICIPIO UM', @StateId, now(), now()),
                   (@MunicipalityTwoId, @MunicipalityTwoIbge, 'MUNICIPIO DOIS', @StateId, now(), now());

            INSERT INTO "ElectoralZones" ("Id","StateId","TseZoneNumber","MunicipalityId","CreatedAt","UpdatedAt")
            VALUES (@ZoneOneId, @StateId, 1, @MunicipalityOneId, now(), now()),
                   (@ZoneTwoId, @StateId, 2, @MunicipalityTwoId, now(), now());

            INSERT INTO "ElectoralOffices" ("Id","TseOfficeCode","Name","CreatedAt","UpdatedAt")
            VALUES (@OfficeId, @OfficeCode, 'CARGO TESTE', now(), now());

            INSERT INTO "PoliticalParties" ("Id","TsePartyNumber","Acronym","Name","CreatedAt","UpdatedAt")
            VALUES (@PartyOneId, @PartyOneNumber, 'P1', 'PARTIDO UM', now(), now()),
                   (@PartyTwoId, @PartyTwoNumber, 'P2', 'PARTIDO DOIS', now(), now());

            INSERT INTO "CandidateIdentities" ("Id","Cpf","FullName","BirthDate","CreatedAt","UpdatedAt")
            VALUES (@IdentityId, NULL, 'IDENTIDADE COMPARTILHADA', '1980-01-01', now(), now());

            INSERT INTO "ImportDatasets"
                ("Id","Provider","Dataset","ElectionYear","LayoutVersion","SourceUrl",
                 "SourceFileName","Checksum","SizeBytes","AcquiredAt","CreatedAt","UpdatedAt")
            VALUES (@DatasetId,'TSE','votacao_secao',2022,'v1','url','file',@Checksum,1,now(),now(),now());

            INSERT INTO "DatasetVersions"
                ("Id","ImportDatasetId","Scope","Status","IsActive","CreatedAt","UpdatedAt")
            VALUES (@ActiveVersionId,@DatasetId,@Scope,'Completed',true,now(),now());
            """,
            new
            {
                ids.StateId,
                StateIbge = Hash(suffix, 1),
                StateAcronym = $"X{suffix}",
                ids.MunicipalityOneId,
                MunicipalityOneIbge = Hash(suffix, 2),
                ids.MunicipalityTwoId,
                MunicipalityTwoIbge = Hash(suffix, 3),
                ids.ZoneOneId,
                ids.ZoneTwoId,
                ids.OfficeId,
                OfficeCode = Hash(suffix, 4),
                ids.PartyOneId,
                PartyOneNumber = Hash(suffix, 5),
                ids.PartyTwoId,
                PartyTwoNumber = Hash(suffix, 6),
                ids.IdentityId,
                ids.DatasetId,
                Checksum = Guid.NewGuid().ToString("N"),
                ids.ActiveVersionId,
                Scope = $"TSE:votacao_secao:2022:{suffix}:{Guid.NewGuid():N}"
            });
    }

    private static async Task InsertStructureAsync(NpgsqlConnection connection, AnalyticsSeedIds ids)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO "ElectoralSections" ("Id","MunicipalityId","ZoneId","TseSectionNumber","CreatedAt","UpdatedAt")
            VALUES (@Section101Id, @MunicipalityOneId, @ZoneOneId, 101, now(), now()),
                   (@Section102Id, @MunicipalityOneId, @ZoneOneId, 102, now(), now()),
                   (@Section103Id, @MunicipalityOneId, @ZoneOneId, 103, now(), now()),
                   (@Section201Id, @MunicipalityTwoId, @ZoneTwoId, 201, now(), now()),
                   (@Section202Id, @MunicipalityTwoId, @ZoneTwoId, 202, now(), now());
            """,
            ids);
    }

    private static async Task InsertCandidatesAsync(
        NpgsqlConnection connection,
        AnalyticsSeedIds ids,
        string suffix)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO "Elections" ("Id","TseElectionCode","Year","TypeCode","TypeName","Description","CreatedAt","UpdatedAt")
            VALUES (@Election2022Id, @Code2022, 2022, 1, 'ORDINARIA', 'TEST 2022', now(), now()),
                   (@Election2018Id, @Code2018, 2018, 1, 'ORDINARIA', 'TEST 2018', now(), now());

            INSERT INTO "ElectionRounds" ("Id","ElectionId","Round","IsRunoff","CreatedAt","UpdatedAt")
            VALUES (@RoundOneId, @Election2022Id, 1, false, now(), now()),
                   (@RoundTwoId, @Election2022Id, 2, true, now(), now()),
                   (@Round2018Id, @Election2018Id, 1, false, now(), now());

            INSERT INTO "Candidates"
                ("Id","ElectionId","OfficeId","StateId","CandidateIdentityId","TseCandidateSequential",
                 "BallotNumber","Name","UrnName","PartyId","CreatedAt","UpdatedAt")
            VALUES (@CandidateA2022Id, @Election2022Id, @OfficeId, @StateId, NULL, 'A22', 10, 'CANDIDATO A', 'A', @PartyOneId, now(), now()),
                   (@CandidateB2022Id, @Election2022Id, @OfficeId, @StateId, @IdentityId, 'B22', 20, 'CANDIDATO B', 'B', @PartyTwoId, now(), now()),
                   (@CandidateC2022Id, @Election2022Id, @OfficeId, @StateId, @IdentityId, 'C22', 30, 'CANDIDATO C', 'C', @PartyTwoId, now(), now()),
                   (@CandidateA2018Id, @Election2018Id, @OfficeId, @StateId, NULL, 'A18', 10, 'CANDIDATO A', 'A', @PartyOneId, now(), now()),
                   (@CandidateB2018Id, @Election2018Id, @OfficeId, @StateId, @IdentityId, 'B18', 20, 'CANDIDATO B', 'B', @PartyTwoId, now(), now());
            """,
            new
            {
                ids.Election2022Id,
                Code2022 = Hash(suffix, 7),
                ids.Election2018Id,
                Code2018 = Hash(suffix, 8),
                ids.RoundOneId,
                ids.RoundTwoId,
                ids.Round2018Id,
                ids.CandidateA2022Id,
                ids.CandidateB2022Id,
                ids.CandidateC2022Id,
                ids.CandidateA2018Id,
                ids.CandidateB2018Id,
                ids.OfficeId,
                ids.StateId,
                ids.IdentityId,
                ids.PartyOneId,
                ids.PartyTwoId
            });
    }

    private static Task InsertResultsAsync(NpgsqlConnection connection, AnalyticsSeedIds ids)
    {
        var rows = new List<object>();

        void Add(Guid section, Guid round, Guid? candidate, Guid? party, VoteType type, int ballot, long votes)
            => rows.Add(new
            {
                Id = Guid.CreateVersion7(),
                SectionId = section,
                RoundId = round,
                CandidateId = candidate,
                PartyId = party,
                VoteType = (short)type,
                Ballot = ballot,
                Votes = votes,
                VersionId = ids.ActiveVersionId,
                OfficeId = ids.OfficeId
            });

        Add(ids.Section101Id, ids.RoundOneId, ids.CandidateA2022Id, null, VoteType.Nominal, 10, 60);
        Add(ids.Section101Id, ids.RoundOneId, ids.CandidateB2022Id, null, VoteType.Nominal, 20, 30);
        Add(ids.Section101Id, ids.RoundOneId, null, ids.PartyOneId, VoteType.Party, 10, 10);

        Add(ids.Section102Id, ids.RoundOneId, ids.CandidateA2022Id, null, VoteType.Nominal, 10, 45);
        Add(ids.Section102Id, ids.RoundOneId, ids.CandidateB2022Id, null, VoteType.Nominal, 20, 45);
        Add(ids.Section102Id, ids.RoundOneId, null, null, VoteType.Blank, 95, 10);

        Add(ids.Section103Id, ids.RoundOneId, null, null, VoteType.Null, 96, 5);

        Add(ids.Section201Id, ids.RoundOneId, ids.CandidateB2022Id, null, VoteType.Nominal, 20, 150);
        Add(ids.Section202Id, ids.RoundOneId, ids.CandidateA2022Id, null, VoteType.Nominal, 10, 50);
        Add(ids.Section202Id, ids.RoundOneId, ids.CandidateC2022Id, null, VoteType.Nominal, 30, 0);

        Add(ids.Section101Id, ids.RoundTwoId, ids.CandidateA2022Id, null, VoteType.Nominal, 10, 100);
        Add(ids.Section101Id, ids.RoundTwoId, ids.CandidateB2022Id, null, VoteType.Nominal, 20, 30);
        Add(ids.Section201Id, ids.RoundTwoId, ids.CandidateA2022Id, null, VoteType.Nominal, 10, 100);
        Add(ids.Section201Id, ids.RoundTwoId, ids.CandidateB2022Id, null, VoteType.Nominal, 20, 30);

        Add(ids.Section101Id, ids.Round2018Id, ids.CandidateA2018Id, null, VoteType.Nominal, 10, 100);
        Add(ids.Section201Id, ids.Round2018Id, ids.CandidateB2018Id, null, VoteType.Nominal, 20, 200);

        return connection.ExecuteAsync(
            """
            INSERT INTO "ElectionResults"
                ("Id","ElectionRoundId","SectionId","OfficeId","VoteType","CandidateId","PartyId",
                 "TseBallotNumber","Votes","DatasetVersionId","CreatedAt","UpdatedAt")
            VALUES (@Id,@RoundId,@SectionId,@OfficeId,@VoteType,@CandidateId,@PartyId,
                    @Ballot,@Votes,@VersionId,now(),now());
            """,
            rows);
    }

    private static int Hash(string suffix, int salt)
        => Math.Abs((suffix.GetHashCode() * 31) + salt) % 80_000_000 + 10_000_000;
}
