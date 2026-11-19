namespace QuanLyCafe.Application.Reporting;

public record RevenuePointDto(string Label, decimal Revenue, int OrderCount);

public record TopProductDto(string ProductName, int Quantity, decimal Revenue);

public record InventoryReportDto(
    Guid MaterialId, string MaterialName, string Unit,
    decimal StockQuantity, decimal MinQuantity, decimal WastedQuantity);
