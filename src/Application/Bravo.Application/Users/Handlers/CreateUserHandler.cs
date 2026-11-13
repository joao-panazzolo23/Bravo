using Bravo.Application.Users.Commands;
using Bravo.Domain.Auth.Services;
using Bravo.Domain.Services.Email;
using Bravo.Domain.Users.Entities;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Users.ViewModel;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Users.Handlers;

public class CreateUserHandler(
    IUnityOfWork unityOfWork,
    IUserRepository userRepository,
    IUserQueryRepository userQueryRepository,
    IEmailService emailService,
    IPasswordService passwordService
    ) : ICommandHandler<CreateUserCommand, Result<Unit>>
{
    async ValueTask<Result<Unit>> ICommandHandler<CreateUserCommand, Result<Unit>>.Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        GetUserViewModel? userViewModel = await userQueryRepository.GetByEmail(command.Email);

        if (userViewModel is not null)
        {
            return Result<Unit>.Conflict(message: "O e-mail já está cadastrado.");
        }

        var entity = new User(
            name: command.Name,
            email: command.Email,
            document: command.Document,
            phone: command.Phone,
            password: passwordService.Hash(command.Password),
            roleType: command.Role
        );

        await userRepository.Create(entity);
        await unityOfWork.Commit();

        return Result<Unit>.Created();
    }
}
