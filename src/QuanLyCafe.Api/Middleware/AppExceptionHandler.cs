using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Common;

namespace QuanLyCafe.Api.Middleware;

// Exception → ProblemDetails có "code" (ERR-*). Lỗi không lường trước → 500, không lộ chi tiết.
public class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var status = ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            BusinessRuleException => StatusCodes.Status422UnprocessableEntity,
            ConflictException => StatusCodes.Status409Conflict,
            PaymentGatewayException => StatusCodes.Status502BadGateway,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            BadHttpRequestException bad => bad.StatusCode,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Lỗi không xử lý: {Path}", http.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status500InternalServerError ? "Đã xảy ra lỗi hệ thống." : ex.Message,
        };
        if (ex is AppException app) problem.Extensions["code"] = app.Code;
        if (ex is DbUpdateConcurrencyException) problem.Extensions["code"] = ErrorCodes.ConcurrencyConflict;

        http.Response.StatusCode = status;
        // WriteAsync (không phải TryWriteAsync) để vẫn trả đúng mã khi client không gửi Accept: JSON.
        await problemDetails.WriteAsync(new() { HttpContext = http, ProblemDetails = problem, Exception = ex });
        return true;
    }
}
