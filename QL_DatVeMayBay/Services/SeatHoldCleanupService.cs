using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Hubs;
using System.Data;

namespace QL_DatVeMayBay.Services
{
    public class SeatHoldCleanupService : BackgroundService
    {
        private readonly string _connStr;
        private readonly IHubContext<SeatHub> _hubContext;

        public SeatHoldCleanupService(IConfiguration config, IHubContext<SeatHub> hubContext)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ReleaseExpiredBookingsAsync();
                }
                catch
                {
                    // Tránh ngắt luồng nền khi DB bận
                }
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ReleaseExpiredBookingsAsync()
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // 1. Lấy danh sách các ghế sắp được giải phóng để báo Realtime cho các trình duyệt khác
            string queryExpired = @"
                SELECT v.MaChuyenBay, v.MaGhe
                FROM VeMayBay v
                JOIN PhieuDatCho p ON v.MaDatCho = p.MaDatCho
                WHERE p.TrangThai = N'Chờ thanh toán' AND p.HanThanhToan < GETDATE();";

            var releasedSeats = new List<(string MaCB, int MaGhe)>();
            using (var cmd = new SqlCommand(queryExpired, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    releasedSeats.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }

            if (releasedSeats.Count == 0) return;

            // 2. Gọi Stored Procedure có sẵn của nhóm để hủy đơn quá hạn & Trigger tự hoàn ghế trống
            using (var spCmd = new SqlCommand("sp_TuDongHuyDonGiuChoHetHan_Cursor", conn))
            {
                spCmd.CommandType = CommandType.StoredProcedure;
                await spCmd.ExecuteNonQueryAsync();
            }

            // 3. Bắn SignalR cập nhật sơ đồ ghế cho tất cả trình duyệt đang mở
            foreach (var group in releasedSeats.GroupBy(x => x.MaCB))
            {
                await _hubContext.Clients.Group($"FLIGHT_{group.Key}")
                    .SendAsync("SeatStatusChanged", new
                    {
                        maChuyenBay = group.Key,
                        seatIds = group.Select(x => x.MaGhe).ToList(),
                        isOccupied = false,
                        message = "Một số ghế hết hạn giữ chỗ 15 phút đã trống trở lại!"
                    });
            }
        }
    }
}