using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Application.Common;

// Đẩy sự kiện qua BarHub (mục 4.3.6). Chỉ gọi SAU khi transaction đã commit.
public interface IRealtimeNotifier
{
    // OrderPlaced → TicketCreated gửi nhóm Barista.
    Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default);
}
