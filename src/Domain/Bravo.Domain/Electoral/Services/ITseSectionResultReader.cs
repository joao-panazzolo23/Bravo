using Bravo.Domain.Electoral.Models;

namespace Bravo.Domain.Electoral.Services;

public interface ITseSectionResultReader
{
    IReadOnlyList<TseSectionResultRow> Read(Stream stream, string layoutVersion);
}
