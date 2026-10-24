namespace Nudge.Api.Infrastructure;

// Services throw these. AppExceptionHandler turns them into HTTP responses,
// so endpoints never need try/catch blocks.
public abstract class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class BadRequestException(string message) : AppException(StatusCodes.Status400BadRequest, message);
public class UnauthorizedException(string message) : AppException(StatusCodes.Status401Unauthorized, message);
public class ForbiddenException(string message) : AppException(StatusCodes.Status403Forbidden, message);
public class NotFoundException(string message) : AppException(StatusCodes.Status404NotFound, message);
public class ConflictException(string message) : AppException(StatusCodes.Status409Conflict, message);
