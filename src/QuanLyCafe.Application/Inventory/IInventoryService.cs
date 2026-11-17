namespace QuanLyCafe.Application.Inventory;

public record StockCheckItem(Guid ProductId, int Quantity);

public interface IInventoryService
{
    Task CheckStockAsync(
        IEnumerable<StockCheckItem> items,
        CancellationToken ct);
}