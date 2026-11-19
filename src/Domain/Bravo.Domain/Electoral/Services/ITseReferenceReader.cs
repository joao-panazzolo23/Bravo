using Bravo.Domain.Electoral.Models;

namespace Bravo.Domain.Electoral.Services;

public interface ITseReferenceReader
{
    TseReferenceSnapshot Read(Stream stream, string layoutVersion);
}
