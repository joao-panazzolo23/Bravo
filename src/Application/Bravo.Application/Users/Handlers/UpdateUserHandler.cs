using Mediator;
using Bravo.Shared.Result;
using Bravo.Shared.Data;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Services.Email;
using Bravo.Application.Users.Commands;

namespace Bravo.Application.Users.Handlers;

public class UpdateUserHandler(
    IUnityOfWork unityOfWork, 
    IUserRepository userRepository, 
    IEmailService emailService
    ) : ICommandHandler<UpdateUserCommand, Result<Unit>>
{
    async ValueTask<Result<Unit>> ICommandHandler<UpdateUserCommand, Result<Unit>>.Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var entity = await userRepository.GetById(command.Id);
        if (entity == null)
        {
            return Result<Unit>.NotFound();
        }

        entity.Update(
            name: command.Name,
            email: command.Email,
            document: command.Document,
            phone: command.Phone,
            isActive: command.IsActive
        );

        await userRepository.Update(entity);
        await unityOfWork.Commit();

        return Result<Unit>.OK();
    }
}
