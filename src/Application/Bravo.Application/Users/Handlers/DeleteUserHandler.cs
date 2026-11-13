using Bravo.Application.Users.Commands;
using Bravo.Domain.Users.Repositories;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Users.Handlers;

public class DeleteUserHandler(IUnityOfWork unityOfWork,
                               IUserRepository userRepository) : IRequestHandler<DeleteUserCommand, Result<Unit>>
{
    private readonly IUnityOfWork _unityOfWork = unityOfWork;
    private readonly IUserRepository _userRepository = userRepository;


    async ValueTask<Result<Unit>> IRequestHandler<DeleteUserCommand, Result<Unit>>.Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var entity = await _userRepository.GetById(request.Id);

        if (entity == null)
        {
            return Result<Unit>.NotFound();
        }

        await _userRepository.Delete(entity);
        await _unityOfWork.Commit();

        return Result<Unit>.OK();
    }
}