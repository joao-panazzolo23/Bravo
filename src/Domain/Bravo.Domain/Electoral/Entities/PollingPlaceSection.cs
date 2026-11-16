using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class PollingPlaceSection : Entity
{
    public Guid ElectionRoundId { get; private set; }
    public Guid PollingPlaceId { get; private set; }
    public Guid SectionId { get; private set; }

    private PollingPlaceSection() { }

    public static PollingPlaceSection Create(
        Guid electionRoundId,
        Guid pollingPlaceId,
        Guid sectionId)
    {
        var assignment = new PollingPlaceSection
        {
            ElectionRoundId = electionRoundId,
            PollingPlaceId = pollingPlaceId,
            SectionId = sectionId
        };

        var now = DateTime.UtcNow;
        assignment.SetCreatedAt(now);
        assignment.SetUpdatedAt(now);

        return assignment;
    }
}
