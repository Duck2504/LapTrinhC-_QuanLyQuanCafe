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
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<CatalogService>();
        services.AddScoped<IInventoryService, InventoryService>();

        return services;
    }
}