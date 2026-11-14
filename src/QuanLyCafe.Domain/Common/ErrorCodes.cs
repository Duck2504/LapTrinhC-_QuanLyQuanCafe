namespace QuanLyCafe.Domain.Common;

// Mã lỗi theo mục 4.5.2 báo cáo; trả về trong ProblemDetails.code.
public static class ErrorCodes
{
    public const string AuthInvalidCredentials = "ERR-AUTH-001";
    public const string AuthAccountLocked = "ERR-AUTH-002";
    public const string AuthInvalidToken = "ERR-AUTH-003";
    public const string StockInsufficient = "ERR-STOCK-001";
    public const string StockIssueExceeds = "ERR-STOCK-002";
    public const string BomMissing = "ERR-BOM-001";
    public const string PriceNegative = "ERR-PRICE-001";
    public const string OrderAlreadyPaid = "ERR-ORDER-001";
    public const string OrderTableHasOpenOrder = "ERR-ORDER-002";
    public const string PaymentGatewayFailed = "ERR-PAY-001";
    public const string PaymentInsufficientCash = "ERR-PAY-002";
    public const string ConcurrencyConflict = "ERR-CON-001";
}
