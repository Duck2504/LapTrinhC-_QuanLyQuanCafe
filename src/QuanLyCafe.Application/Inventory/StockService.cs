using QuanLyCafe.Application.Common;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Application.Inventory;

public class StockService(
    IMaterialRepository materials,
    IStockMovementRepository movements,
    IRealtimeNotifier notifier)
{
    private const decimal MaxStockQuantity = 999_999_999_999_999.999m;

    public Task<StockMovementResult> ReceiveAsync(
        SaveStockMovementRequest request, Guid userId, CancellationToken ct) =>
        SaveAsync(request, userId, MovementType.Receipt, ct);

    public Task<StockMovementResult> IssueAsync(
        SaveStockMovementRequest request, Guid userId, CancellationToken ct) =>
        SaveAsync(request, userId, MovementType.Issue, ct);

    public Task<PagedResult<StockMovementDto>> GetMovementsAsync(
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            throw new ValidationException(
                ErrorCodes.ValidationFailed,
                "Trang phải từ 1 và kích thước trang phải từ 1 đến 100.");

        return movements.GetMovementsAsync(page, pageSize, ct);
    }

    private async Task<StockMovementResult> SaveAsync(
        SaveStockMovementRequest request, Guid userId,
        MovementType type, CancellationToken ct)
    {
        var note = request.Note?.Trim();

        if (request.MaterialId == Guid.Empty || request.Quantity <= 0 ||
            request.Quantity > MaxStockQuantity ||
            decimal.Round(request.Quantity, 3) != request.Quantity ||
            note?.Length > 200)
        {
            throw new ValidationException(
                ErrorCodes.ValidationFailed,
                "Kiểm tra mã nguyên liệu, số lượng dương với tối đa 3 chữ số thập phân và ghi chú tối đa 200 ký tự.");
        }

        var material = await materials.GetAsync(request.MaterialId, ct)
            ?? throw new NotFoundException("Không tìm thấy nguyên liệu.");

        if (type == MovementType.Issue && request.Quantity > material.StockQuantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.StockIssueExceeds,
                $"Chỉ còn {material.StockQuantity} {material.Unit} trong kho.");
        }

        var newStock = type == MovementType.Receipt
            ? material.StockQuantity + request.Quantity
            : material.StockQuantity - request.Quantity;

        if (newStock > MaxStockQuantity)
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Tồn kho vượt giới hạn lưu trữ.");

        material.StockQuantity = newStock;

        var movement = new StockMovement
        {
            MaterialID = material.MaterialID,
            UserID = userId,
            MovementType = type,
            Quantity = request.Quantity,
            Note = note
        };

        await movements.SaveMovementAsync(movement, ct);
        if (type == MovementType.Issue)
            await notifier.NotifyLowStockAsync([material], CancellationToken.None); // sau commit: client hủy thì vẫn phải báo
        return new(movement.MovementID, movement.UserID, newStock);
    }

    public async Task<StocktakeResultDto> StocktakeAsync(
    StocktakeRequest request, Guid userId, CancellationToken ct)
    {
        var note = request.Note?.Trim();

        if (request.MaterialId == Guid.Empty ||
            request.ActualQuantity is not decimal actualQuantity ||
            actualQuantity < 0 ||
            actualQuantity > MaxStockQuantity ||
            decimal.Round(actualQuantity, 3) != actualQuantity ||
            note?.Length > 200)
        {
            throw new ValidationException(
                ErrorCodes.ValidationFailed,
                "Chọn nguyên liệu và nhập tồn thực tế từ 0, tối đa 3 chữ số thập phân. "
                + "Ghi chú tối đa 200 ký tự.");
        }

        var material = await materials.GetAsync(request.MaterialId, ct)
            ?? throw new NotFoundException("Không tìm thấy nguyên liệu.");

        var systemQuantity = material.StockQuantity;
        var difference = actualQuantity - systemQuantity;

        if (difference < 0)
        {
            material.StockQuantity = actualQuantity;

            await movements.SaveMovementAsync(new StockMovement
            {
                MaterialID = material.MaterialID,
                UserID = userId,
                MovementType = MovementType.Shrinkage,
                Quantity = -difference,
                Note = note
            }, ct);
            await notifier.NotifyLowStockAsync([material], CancellationToken.None); // sau commit: client hủy thì vẫn phải báo
        }

        return new(material.MaterialID, systemQuantity, actualQuantity, difference);
    }
}