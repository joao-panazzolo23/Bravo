using Bravo.Application.Authentication.Commands;
using Bravo.Domain.Auth.Services;
using Bravo.Domain.Services.Email;
using Bravo.Domain.Users.Entities;
using Bravo.Domain.Users.Repositories;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Handlers;

public class RecoveryPasswordHandler(
    IUserRepository userRepository,
    IUserRecoveryRepository userRecoveryRepository,
    IRecoveryService recoveryService,
    IAccountEmailService accountEmailService,
    TimeProvider timeProvider,
    IUnityOfWork unityOfWork
    ) : ICommandHandler<RecoveryPasswordCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(RecoveryPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmail(command.Email);

        if (user is null || !user.IsActive)
            return Result<Unit>.NoContent();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var pending in await userRecoveryRepository.ListPendingByUser(user.Id, now))
            pending.Revoke(now);

        var token = recoveryService.Generate();
        await userRecoveryRepository.Create(new UserRecovery(user.Id, token.Hash, token.ExpiresAt));
        await unityOfWork.Commit();

        await accountEmailService.SendPasswordRecovery(user, token, cancellationToken);

        return Result<Unit>.NoContent();
    }
}
