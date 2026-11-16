using Bravo.Application.Electoral.Models;

namespace Bravo.Application.Electoral.Contracts;

public interface ITseCandidateReader
{
    IReadOnlyList<TseCandidateRow> Read(Stream stream, string layoutVersion);
}
