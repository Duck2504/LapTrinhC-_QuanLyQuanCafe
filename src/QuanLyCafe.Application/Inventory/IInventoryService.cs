namespace QuanLyCafe.Application.Inventory;

public record StockCheckItem(Guid ProductId, int Quantity);

public interface IInventoryService
{
    Task CheckStockAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken ct = default);
}
public class StubInventoryService : IInventoryService
{
    public Task CheckStockAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken ct = default) => Task.CompletedTask;
}
    Task CheckStockAsync(
        IEnumerable<StockCheckItem> items,
        CancellationToken ct);
}
