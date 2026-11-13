using Bravo.Application.Authentication.Commands;
using Bravo.Domain.Auth.Enums;
using Bravo.Domain.Auth.Services;
using Bravo.Domain.Auth.ViewModel;
using Bravo.Domain.Users.Repositories;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Handlers;

public class LoginHandler(
    ITokenService tokenService,
    IPasswordService passwordService,
    IUserRepository userRepository,
    IUnityOfWork unityOfWork
        ) : ICommandHandler<LoginCommand, Result<LoginViewModel>>
{
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    public async ValueTask<Result<LoginViewModel>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmail(command.Email!);

        if (user is null || !user.IsActive)
        {
            // Same cost as a real verification, otherwise response time reveals which e-mails exist.
            passwordService.Hash(command.Password!);
            return Result<LoginViewModel>.Unauthorized(message: InvalidCredentials);
        }

        var verification = passwordService.Verify(user.Password, command.Password!);

        if (verification is PasswordVerification.Failed)
            return Result<LoginViewModel>.Unauthorized(message: InvalidCredentials);

        if (verification is PasswordVerification.SuccessRehashNeeded)
        {
            user.RehashPassword(passwordService.Hash(command.Password!));
            await userRepository.Update(user);
            await unityOfWork.Commit();
        }

        return Result<LoginViewModel>.OK(new LoginViewModel(
            tokenService.GenerateToken(user),
            tokenService.GenerateRefreshToken(user)));
    }
}
