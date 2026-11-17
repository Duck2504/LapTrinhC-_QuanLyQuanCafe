using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;

namespace QuanLyCafe.Application.Inventory;

public class InventoryService(
    IProductRepository products,
    IMaterialRepository materials) : IInventoryService
{
    public async Task CheckStockAsync(
        IEnumerable<StockCheckItem> items,
        CancellationToken ct)
    {
        var needed = new Dictionary<Guid, decimal>();

        foreach (var group in items.GroupBy(item => item.ProductId))
        {
            var product = await products.GetWithBomAsync(group.Key, ct)
                ?? throw new NotFoundException(
                    $"Không tìm thấy sản phẩm {group.Key}.");

            if (product.ProductBOMs.Count == 0)
            {
                throw new BusinessRuleException(
                    ErrorCodes.BomMissing,
                    $"Sản phẩm {product.ProductName} chưa thiết lập định lượng.");
            }

            var quantity = group.Sum(item => (decimal)item.Quantity);

            foreach (var line in product.ProductBOMs)
            {
                needed[line.MaterialID] =
                    needed.GetValueOrDefault(line.MaterialID)
                    + line.QuantityRequired * quantity;
            }
        }

        if (needed.Count == 0)
            return;

        var stock = await materials.GetByIdsAsync(needed.Keys, ct);

        foreach (var (materialId, required) in needed)
        {
            if (!stock.TryGetValue(materialId, out var material))
            {
                throw new BusinessRuleException(
                    ErrorCodes.StockInsufficient,
                    $"Không tìm thấy tồn kho của nguyên liệu {materialId}.");
            }

            if (material.StockQuantity < required)
            {
                throw new BusinessRuleException(
                    ErrorCodes.StockInsufficient,
                    $"Thiếu {material.MaterialName}: cần {required} {material.Unit}, "
                    + $"hiện có {material.StockQuantity} {material.Unit}.");
            }
        }
    }
}