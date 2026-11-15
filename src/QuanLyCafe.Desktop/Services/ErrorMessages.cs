namespace QuanLyCafe.Desktop.Services;

public static class ErrorMessages
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["ERR-AUTH-001"]  = "Sai tài khoản hoặc mật khẩu",
        ["ERR-AUTH-002"]  = "Tài khoản đã bị vô hiệu hóa, vui lòng liên hệ Quản lý",
        ["ERR-AUTH-003"]  = "Phiên làm việc đã hết hạn, vui lòng đăng nhập lại",
        ["ERR-STOCK-001"] = "Món tạm hết nguyên liệu",
        ["ERR-STOCK-002"] = "Số lượng xuất vượt tồn kho",
        ["ERR-BOM-001"]   = "Sản phẩm chưa thiết lập định lượng",
        ["ERR-PRICE-001"] = "Giá bán phải lớn hơn hoặc bằng 0",
        ["ERR-ORDER-001"] = "Hóa đơn đã thanh toán, không thể sửa",
        ["ERR-ORDER-002"] = "Bàn này đang có order",
        ["ERR-PAY-001"]   = "Cổng thanh toán chưa phản hồi, vui lòng chọn tiền mặt",
        ["ERR-PAY-002"]   = "Số tiền khách đưa chưa đủ",
        ["ERR-CON-001"]   = "Dữ liệu vừa được cập nhật, vui lòng thử lại",
        ["ERR-NET"]       = "Không kết nối được máy chủ",
    };

    public static string For(ApiException ex)
        => ex.Code is not null && Map.TryGetValue(ex.Code, out var m) ? m : ex.Message;
}

public static class ErrorPresenter
{
    private static readonly ErrorProvider _provider = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public static void Show(IWin32Window? owner, Exception ex, Control? field = null)
    {
        var msg = ex is ApiException api ? ErrorMessages.For(api) : "Lỗi không xác định: " + ex.Message;
        if (field is not null) _provider.SetError(field, msg);
        MessageBox.Show(owner, msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Clear(Control field) => _provider.SetError(field, "");
}