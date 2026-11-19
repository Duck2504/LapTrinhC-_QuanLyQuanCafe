using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Application.Payment;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Infrastructure.Persistence;
using QuanLyCafe.Infrastructure.Repositories.Inventory;
using QuanLyCafe.Infrastructure.Repositories.Ordering;
using QuanLyCafe.Infrastructure.Repositories.Payment;

namespace QuanLyCafe.Tests.Payment;

public class PaymentRollbackTests
{
    [SqlServerFact]
    public async Task CashPayment_WhenSavingFailsBeforeCommit_RollsBackPaymentAndStock()
    {
        var connection = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable))
        {
            InitialCatalog = $"QuanLyCafe_PaymentTest_{Guid.NewGuid():N}",
            AttachDBFilename = ""
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.EnsureCreatedAsync();
        try
        {
            var scenario = new CashPaymentScenario();
            setup.AddRange(scenario.Material, scenario.Order);
            await setup.SaveChangesAsync();

            var paymentOptions = new DbContextOptionsBuilder<AppDbContext>(options)
                .AddInterceptors(new FailAfterWritingPayment())
                .Options;
            await using (var db = new AppDbContext(paymentOptions))
            {
                var orders = new OrderRepository(db);
                var inventory = new InventoryService(new ProductRepository(db), new MaterialRepository(db));
                var service = new PaymentService(orders, inventory, new PaymentRepository(db, orders),
                    new CashPaymentScenario.UnusedGateway());

                await Assert.ThrowsAsync<SimulatedPaymentSaveFailure>(() => scenario.PayAsync(service));
            }

            await using var verify = new AppDbContext(options);
            var order = await verify.Orders.SingleAsync(o => o.OrderID == scenario.Order.OrderID);
            var table = await verify.Tables.SingleAsync(t => t.TableID == scenario.Table.TableID);
            var material = await verify.Materials.SingleAsync(m => m.MaterialID == scenario.Material.MaterialID);

            Assert.Equal(OrderStatus.Open, order.Status);
            Assert.Null(order.PaymentMethod);
            Assert.Null(order.PaidAt);
            Assert.Null(order.CashierID);
            Assert.Null(order.PaymentResultJson);
            Assert.Equal(TableStatus.Serving, table.Status);
            Assert.Equal(100m, material.StockQuantity);
            Assert.False(await verify.StockMovements.AnyAsync(m => m.OrderID == order.OrderID));
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FailAfterWritingPayment : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            // EF has written the changes, but the order transaction has not committed.
            Assert.NotNull(eventData.Context?.Database.CurrentTransaction);
            Assert.True(result > 0);
            throw new SimulatedPaymentSaveFailure();
        }
    }

    private sealed class SimulatedPaymentSaveFailure : Exception;
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "QUANLYCAFE_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            Skip = $"Set {ConnectionVariable} to a SQL Server connection string that can create test databases.";
    }
}
