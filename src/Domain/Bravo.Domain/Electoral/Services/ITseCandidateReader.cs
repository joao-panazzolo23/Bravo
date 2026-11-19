using Bravo.Domain.Electoral.Models;

namespace Bravo.Domain.Electoral.Services;

public interface ITseCandidateReader
{
    IReadOnlyList<TseCandidateRow> Read(Stream stream, string layoutVersion);
}
