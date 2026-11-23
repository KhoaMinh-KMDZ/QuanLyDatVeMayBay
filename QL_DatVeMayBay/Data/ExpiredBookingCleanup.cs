using System.Data;
using Microsoft.Data.SqlClient;

namespace QL_DatVeMayBay.Data;

internal sealed class ExpiredBookingCleanup(ILogger<ExpiredBookingCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                DatabaseHelper.ExecuteNonQuery("sp_TuDongHuyDonGiuChoHetHan_Cursor", CommandType.StoredProcedure);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Không thể giải phóng đơn giữ chỗ hết hạn.");
            }
        }
    }
}
