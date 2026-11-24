using Microsoft.Extensions.DependencyInjection;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Infrastructure.Repositories.Inventory;
using QuanLyCafe.Application.Inventory;


namespace QuanLyCafe.Api;

public static class CatalogAndInventoryRegistration
{
    public static IServiceCollection AddCatalogAndInventory(
        this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, QuanLyCafe.Infrastructure.Repositories.Catalog.CategoryRepository>();
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<CatalogService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<StockService>();

        return services;
    }
}