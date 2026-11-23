using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class CheckInController : Controller
    {
        [HttpGet]
        public IActionResult Index(string? maDatCho, string? tenHanhKhach)
        {
            if (maDatCho is null && tenHanhKhach is null)
                return View();

            if (!string.IsNullOrWhiteSpace(maDatCho) && !string.IsNullOrWhiteSpace(tenHanhKhach))
            {
                var tickets = DatabaseHelper.ExecuteQuery(@"
                    SELECT v.*,
                           CASE WHEN GETDATE() BETWEEN DATEADD(HOUR, -24, v.NgayGioBay) AND DATEADD(HOUR, -1, v.NgayGioBay)
                                AND cb.TrangThai NOT IN (N'Đã hủy', N'Hoãn chuyến', N'Đã cất cánh')
                                AND p.TrangThai = N'Đã thanh toán' THEN 1 ELSE 0 END AS CoTheCheckIn
                    FROM vw_ChiTietVeVaHanhKhach v
                    JOIN ChuyenBay cb ON cb.MaChuyenBay = v.MaChuyenBay
                    JOIN PhieuDatCho p ON p.MaDatCho = v.MaDatCho
                    WHERE v.MaDatCho = @PNR AND v.TenHanhKhach = @TenHK",
                    CommandType.Text, new SqlParameter("@PNR", maDatCho.Trim()),
                    new SqlParameter("@TenHK", tenHanhKhach.Trim()));
                if (tickets.Rows.Count > 0)
                {
                    ViewBag.DanhSachVe = tickets;
                    return View();
                }
            }

            ViewBag.Error = "Vui lòng nhập đúng mã PNR và họ tên đầy đủ của hành khách.";
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmCheckIn(string maVe, string maDatCho, string tenHanhKhach, bool chapNhanAnToan)
        {
            if (!ModelState.IsValid || !chapNhanAnToan || string.IsNullOrWhiteSpace(maVe) ||
                string.IsNullOrWhiteSpace(maDatCho) || string.IsNullOrWhiteSpace(tenHanhKhach))
            {
                TempData["Error"] = "Vui lòng xác thực vé và đồng ý với quy định an toàn hành lý.";
                return RedirectToAction(nameof(Index), new { maDatCho, tenHanhKhach });
            }

            var changed = DatabaseHelper.ExecuteScalar(@"
                DECLARE @Now datetime = GETDATE();
                UPDATE v SET TrangThai = N'Đã Check-in'
                FROM VeMayBay v JOIN ChuyenBay cb ON cb.MaChuyenBay = v.MaChuyenBay
                JOIN PhieuDatCho p ON p.MaDatCho = v.MaDatCho
                WHERE v.MaVe = @MaVe AND v.MaDatCho = @PNR AND v.TenHanhKhach = @TenHK
                    AND v.TrangThai = N'Đã xuất vé' AND p.TrangThai = N'Đã thanh toán'
                    AND cb.TrangThai NOT IN (N'Đã hủy', N'Hoãn chuyến', N'Đã cất cánh')
                    AND @Now BETWEEN DATEADD(HOUR, -24, cb.NgayGioBay) AND DATEADD(HOUR, -1, cb.NgayGioBay);
                SELECT @@ROWCOUNT;",
                CommandType.Text, new SqlParameter("@MaVe", maVe),
                new SqlParameter("@PNR", maDatCho.Trim()), new SqlParameter("@TenHK", tenHanhKhach.Trim()));

            if (Convert.ToInt32(changed) == 1)
                TempData["Success"] = "Đã check-in. Thẻ lên máy bay được hiển thị bên dưới.";
            else
                TempData["Error"] = "Vé chưa đủ điều kiện check-in hoặc đã ngoài khung 24 giờ đến 60 phút trước giờ bay.";

            return RedirectToAction(nameof(Index), new { maDatCho, tenHanhKhach });
        }
    }
}
