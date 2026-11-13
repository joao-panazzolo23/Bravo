using Bravo.Application.Authentication.Commands;
using Bravo.Domain.Auth.Services;
using Bravo.Domain.Auth.ViewModel;
using Bravo.Domain.Users.Repositories;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Handlers;

public class RefreshTokenHandler(
    ITokenService _tokenService,
    IUserRepository _userRepository
    ) : ICommandHandler<RefreshTokenCommand, Result<LoginViewModel>>
{
    public async ValueTask<Result<LoginViewModel>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var payload = _tokenService.ValidateRefresh(command.RefreshToken);

        if (payload is null)
            return Result<LoginViewModel>.Unauthorized();

        var user = await _userRepository.GetById(payload.UserId);

        if (user is null || !user.IsActive || user.SecurityStamp != payload.SecurityStamp)
            return Result<LoginViewModel>.Unauthorized();

        return Result<LoginViewModel>.OK(new LoginViewModel(
            _tokenService.GenerateToken(user),
            _tokenService.GenerateRefreshToken(user)));
    }
}
