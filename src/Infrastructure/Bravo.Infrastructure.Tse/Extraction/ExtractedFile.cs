namespace Bravo.Infrastructure.Tse.Extraction;

internal sealed record ExtractedFile(
    string FilePath,
     string EntryName,
      long SizeBytes
      );
