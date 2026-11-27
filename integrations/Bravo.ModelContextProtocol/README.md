# BRAVO MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io/) stdio server that exposes the BRAVO
electoral analytics read model to MCP clients. Every tool is a thin adapter over the Application
analytics queries, so an MCP client and the HTTP API answer with the same calculations, the same
valid filters and the same limitations.

## Tools

| Tool | Purpose |
| --- | --- |
| `find_vote_concentration` | Sections where a candidate reached a share of valid votes inside a window (paginated). |
| `rank_vote_concentration` | Sections ordered by a candidate's share of valid votes. |
| `get_municipality_performance` | A candidate's votes, valid votes, share and rank per municipality. |
| `get_municipality_margins` | Municipalities ordered by the margin between their two strongest candidates. |
| `get_regional_dominance` | Municipalities contested/won and share statistics per state. |
| `get_turnout_composition` | Votes cast, valid, blank and null at section/municipality/zone/state level. |
| `detect_outliers` | Sections deviating from a reference population by a Z-score threshold. |
| `compare_election_years` | One candidate's performance between two election years. |
| `compare_election_rounds` | One candidate's performance between two rounds of the same election. |
| `get_candidate_identity_history` | Every candidacy of a candidate identity across elections. |

## Analytical rules the tools follow

- **Rounds and offices are never mixed.** A result belongs to one office and one round; share-based
  tools require both.
- **Valid votes** are nominal plus party ballots. Blank and null ballots are reported separately and
  never enter a candidate denominator.
- **Only active dataset versions are read**, so a re-import is never double counted.
- **Turnout is not inferred.** The database stores no eligible-voter source, so attendance,
  abstention and turnout are returned as `null`; blank and null shares use votes cast.
- **Outliers are statistical only.** A deviation is never labelled fraud, manipulation or an
  irregularity.
- **Candidate identities are not parties.** An identity is a person, not a party, coalition or
  ideology.

## Configuration

The server reads the standard `PostgreSQL` options section, so a connection string can be passed as
an environment variable:

```bash
PostgreSQL__Connection="Host=localhost;Port=5432;Database=Bravo;Username=postgres;Password=postgres" \
  dotnet run --project integrations/Bravo.ModelContextProtocol
```

## Developing locally

```json
{
  "servers": {
    "Bravo.ModelContextProtocol": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "<PATH TO PROJECT DIRECTORY>"],
      "env": {
        "PostgreSQL__Connection": "Host=localhost;Port=5432;Database=Bravo;Username=postgres;Password=postgres"
      }
    }
  }
}
```

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package.
2. Publish with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`.
