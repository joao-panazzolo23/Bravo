using Bravo.Application.Authentication.Commands;
using Bravo.Domain.Auth.Services;
using Bravo.Domain.Services.Email;
using Bravo.Domain.Users.Repositories;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Handlers;

internal class ResetPasswordHandler(
    IUserRepository userRepository,
    IUserRecoveryRepository userRecoveryRepository,
    IRecoveryService recoveryService,
    IPasswordService passwordService,
    IAccountEmailService accountEmailService,
    TimeProvider timeProvider,
    IUnityOfWork unityOfWork
    ) : ICommandHandler<ResetPasswordCommand, Result<Unit>>
{
    private const string InvalidToken = "Token de recuperação inválido ou expirado.";

    public async ValueTask<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var recovery = await userRecoveryRepository.GetByTokenHash(recoveryService.Hash(command.Token));

        if (recovery is null || !recovery.IsValid(now))
            return Result<Unit>.BadRequest(message: InvalidToken);

        var user = await userRepository.GetById(recovery.UserId);

        if (user is null || !user.IsActive)
            return Result<Unit>.BadRequest(message: InvalidToken);

        recovery.Consume(now);

        foreach (var pending in await userRecoveryRepository.ListPendingByUser(user.Id, now))
            pending.Revoke(now);

        user.ChangePassword(passwordService.Hash(command.NewPassword));
        await userRepository.Update(user);
        await unityOfWork.Commit();

        await accountEmailService.SendPasswordChanged(user, cancellationToken);

        return Result<Unit>.NoContent();
    }
}
