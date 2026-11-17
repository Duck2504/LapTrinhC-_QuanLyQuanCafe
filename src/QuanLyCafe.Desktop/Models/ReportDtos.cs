namespace QuanLyCafe.Desktop.Models;

// GET /api/reports/revenue?from=&to=&groupBy=day|month|shift
// Label là "2026-11-15", "2026-11" hoặc "Sáng"/"Chiều"/"Tối"
public record RevenuePointDto(string Label, decimal Revenue, int OrderCount);

// GET /api/reports/top-products
public record TopProductDto(string ProductName, int Quantity, decimal Revenue);

// GET /api/reports/inventory
public record InventoryReportDto(
    Guid MaterialId, string MaterialName, string Unit,
    decimal StockQuantity, decimal MinQuantity, decimal WastedQuantity);