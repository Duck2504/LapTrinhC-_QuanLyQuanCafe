using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using QuanLyCafe.Application.Payment;

namespace QuanLyCafe.Api.Controllers.Payment;

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Manager,Cashier")]
public class PaymentsController(PaymentService payments) : ControllerBase
{
    [HttpPost]
    public Task<PaymentResultDto> Pay(PaymentRequest request, CancellationToken ct)
    {
        var cashierId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        return payments.PayCashAsync(request, cashierId, User.Identity?.Name, ct);
    }
}
