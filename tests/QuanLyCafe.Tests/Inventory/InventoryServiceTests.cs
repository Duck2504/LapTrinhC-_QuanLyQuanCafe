using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Tests.Inventory;

public class InventoryServiceTests
{
    private readonly Material _milk = new()
    {
        MaterialName = "Sữa",
        Unit = "ml",
        StockQuantity = 50m
    };

    [Fact]
    public async Task SanPhamChuaCoBom_BaoErrBom001()
    {
        var product = new Product { ProductName = "Cà phê sữa" };
        var service = CreateService(product);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CheckStockAsync(
                [new StockCheckItem(product.ProductID, 1)],
                CancellationToken.None));

        Assert.Equal(ErrorCodes.BomMissing, ex.Code);
    }

    [Fact]
    public async Task HaiPhanCaPheSua_KhoKhongDu_BaoErrStock001()
    {
        var product = CreateProduct("Cà phê sữa", 30m);
        var service = CreateService(product);

        // Hai phần cần 60 ml, kho có 50 ml.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CheckStockAsync(
                [new StockCheckItem(product.ProductID, 2)],
                CancellationToken.None));

        Assert.Equal(ErrorCodes.StockInsufficient, ex.Code);
    }

    [Fact]
    public async Task HaiMonDungChungSua_PhaiCongDonTruocKhiSoTon()
    {
        var coffee = CreateProduct("Cà phê sữa", 30m);
        var bacXiu = CreateProduct("Bạc xỉu", 15m);
        var service = CreateService(coffee, bacXiu);

        // Mỗi món cần 30 ml riêng. Tổng cần 60 ml, kho có 50 ml.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CheckStockAsync(
                [
                    new StockCheckItem(coffee.ProductID, 1),
                    new StockCheckItem(bacXiu.ProductID, 2)
                ],
                CancellationToken.None));

        Assert.Equal(ErrorCodes.StockInsufficient, ex.Code);
    }

    [Fact]
    public async Task TonVuaDu_KiemTraThanhCongVaKhongTruKho()
    {
        _milk.StockQuantity = 60m;
        var product = CreateProduct("Cà phê sữa", 30m);
        var service = CreateService(product);

        await service.CheckStockAsync(
            [new StockCheckItem(product.ProductID, 2)],
            CancellationToken.None);

        Assert.Equal(60m, _milk.StockQuantity);
    }

    private Product CreateProduct(string name, decimal milkPerServing)
    {
        var product = new Product { ProductName = name };

        product.ProductBOMs.Add(new ProductBOM
        {
            ProductID = product.ProductID,
            MaterialID = _milk.MaterialID,
            QuantityRequired = milkPerServing
        });

        return product;
    }

    private InventoryService CreateService(params Product[] products)
    {
        var repository = new FakeInventoryRepository(products, _milk);
        return new InventoryService(repository, repository);
    }

    private sealed class FakeInventoryRepository(
        Product[] products,
        Material material) : IProductRepository, IMaterialRepository
    {
        public Task<Product?> GetWithBomAsync(Guid id, CancellationToken ct)
        {
            var product = products.SingleOrDefault(p => p.ProductID == id);
            return Task.FromResult(product);
        }

        public Task<Dictionary<Guid, Material>> GetByIdsAsync(
            IEnumerable<Guid> ids,
            CancellationToken ct)
        {
            var result = new Dictionary<Guid, Material>();

            if (ids.Contains(material.MaterialID))
                result.Add(material.MaterialID, material);

            return Task.FromResult(result);
        }

        public Task<List<Product>> GetAllAsync(bool onlyActive, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
            => throw new NotSupportedException();

        public Task AddAsync(Product product, CancellationToken ct)
            => throw new NotSupportedException();

        public void RemoveBomLines(IEnumerable<ProductBOM> lines)
            => throw new NotSupportedException();

        public Task<List<Material>> GetAllAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Material?> GetAsync(Guid id, CancellationToken ct)
            => throw new NotSupportedException();

        public Task AddAsync(Material item, CancellationToken ct)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}