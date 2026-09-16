namespace Felion.Application.Members;

public sealed class MemberAccessDeniedException : Exception
{
    public MemberAccessDeniedException()
        : base("Only active Core or Admin members may manage members.")
    {
    }
}

public sealed class MemberNotFoundException(Guid memberId)
    : Exception($"Member '{memberId}' was not found.");

public sealed class MemberConflictException(string message)
    : Exception(message);

public sealed class MemberValidationException(string message)
    : Exception(message);
