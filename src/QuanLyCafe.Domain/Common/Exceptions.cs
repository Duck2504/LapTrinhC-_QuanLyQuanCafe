namespace QuanLyCafe.Domain.Common;

// Middleware ở Api ánh xạ từng loại sang HTTP: 400/401/404/422/409/502.
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public class ValidationException(string code, string message) : AppException(code, message);

public class UnauthorizedException(string code, string message) : AppException(code, message);

public class NotFoundException(string message) : AppException("ERR-NOTFOUND", message);

public class BusinessRuleException(string code, string message) : AppException(code, message);

public class ConflictException(string code, string message) : AppException(code, message);

public class PaymentGatewayException(string message) : AppException(ErrorCodes.PaymentGatewayFailed, message);
