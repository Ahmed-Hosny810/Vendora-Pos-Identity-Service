namespace Pos.Identity.Application.Interfaces.Services;

public interface IUserNameLookupService
{
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}