using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Tests.Inventory;

public class StockServiceTests
{
    private readonly Material _material = new() { StockQuantity = 100m };
    private readonly Guid _userId = Guid.NewGuid();
    private readonly RecordingNotifier _notifier = new();

    [Fact]
    public async Task Stocktake_WithShortage_RecordsShrinkageAndUpdatesStock()
    {
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        var result = await service.StocktakeAsync(
            new(_material.MaterialID, 70m, "Stock count"), _userId, CancellationToken.None);

        Assert.Equal(100m, result.SystemQuantity);
        Assert.Equal(70m, result.ActualQuantity);
        Assert.Equal(-30m, result.Difference);
        Assert.Equal(70m, _material.StockQuantity);
        var movement = Assert.Single(repository.Movements);
        Assert.Equal(MovementType.Shrinkage, movement.MovementType);
        Assert.Equal(30m, movement.Quantity);
        Assert.Equal(_material.MaterialID, movement.MaterialID);
        Assert.Equal(_userId, movement.UserID);
    }

    [Fact]
    public async Task Stocktake_WithMatchingCount_DoesNotRecordMovement()
    {
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        var result = await service.StocktakeAsync(
            new(_material.MaterialID, 100m, null), _userId, CancellationToken.None);

        Assert.Equal(0m, result.Difference);
        Assert.Equal(100m, _material.StockQuantity);
        Assert.Empty(repository.Movements);
    }

    [Fact]
    public async Task Stocktake_WithNegativeCount_IsRejected()
    {
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.StocktakeAsync(
            new(_material.MaterialID, -1m, null), _userId, CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Equal(100m, _material.StockQuantity);
        Assert.Empty(repository.Movements);
    }

    [Fact]
    public async Task Issue_ExceedingAvailableStock_IsRejected()
    {
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueAsync(
            new(_material.MaterialID, 120m, null), _userId, CancellationToken.None));

        Assert.Equal(ErrorCodes.StockIssueExceeds, error.Code);
        Assert.Equal(100m, _material.StockQuantity);
        Assert.Empty(repository.Movements);
    }

    [Fact]
    public async Task Issue_DroppingToMinimum_AlertsManager()
    {
        _material.MinQuantity = 20m;
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        await service.IssueAsync(new(_material.MaterialID, 79m, null), _userId, CancellationToken.None);
        Assert.Empty(_notifier.LowStock); // 21 > 20

        await service.IssueAsync(new(_material.MaterialID, 1m, null), _userId, CancellationToken.None);
        var alert = Assert.Single(_notifier.LowStock);
        Assert.Equal((_material.MaterialID, 20m), (alert.MaterialId, alert.StockQuantity));
    }

    [Fact]
    public async Task Stocktake_ShortageBelowMinimum_AlertsManager()
    {
        _material.MinQuantity = 50m;
        var repository = new StockRepository(_material);
        var service = new StockService(repository, repository, _notifier);

        await service.StocktakeAsync(new(_material.MaterialID, 40m, null), _userId, CancellationToken.None);

        Assert.Equal(40m, Assert.Single(_notifier.LowStock).StockQuantity);
    }

    private sealed class StockRepository(Material material) : IMaterialRepository, IStockMovementRepository
    {
        public List<StockMovement> Movements { get; } = [];

        public Task<Material?> GetAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == material.MaterialID ? material : null);

        public Task SaveMovementAsync(StockMovement movement, CancellationToken ct)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task<List<Material>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<Guid, Material>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task AddAsync(Material item, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<StockMovementDto>> GetMovementsAsync(int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
