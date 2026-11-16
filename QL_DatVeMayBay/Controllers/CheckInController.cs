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
            if (!string.IsNullOrWhiteSpace(maDatCho))
            {
                string query = @"
                    SELECT * FROM vw_ChiTietVeVaHanhKhach 
                    WHERE MaDatCho = @PNR 
                      AND (@TenHK IS NULL OR TenHanhKhach LIKE N'%' + @TenHK + N'%')";

                DataTable dt = DatabaseHelper.ExecuteQuery(
                    query,
                    CommandType.Text,
                    new SqlParameter("@PNR", maDatCho.Trim().ToUpper()),
                    new SqlParameter("@TenHK", string.IsNullOrWhiteSpace(tenHanhKhach) ? DBNull.Value : tenHanhKhach.Trim())
                );

                if (dt.Rows.Count > 0)
                    ViewBag.DanhSachVe = dt;
                else
                    ViewBag.Error = "Không tìm thấy vé phù hợp với mã PNR và tên hành khách đã nhập!";
            }
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmCheckIn(string maVe, string maDatCho, string? tenHanhKhach)
        {
            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "SELECT TrangThaiVe, NgayGioBay FROM vw_ChiTietVeVaHanhKhach WHERE MaVe = @MaVe",
                    CommandType.Text,
                    new SqlParameter("@MaVe", maVe));

                if (dt.Rows.Count == 0)
                    throw new Exception("Không tìm thấy thông tin vé!");

                string trangThai = dt.Rows[0]["TrangThaiVe"].ToString()!;
                if (trangThai != "Đã xuất vé")
                    throw new Exception($"Vé đang ở trạng thái '{trangThai}', không thể thực hiện Check-in!");

                // Cập nhật trạng thái vé sang Đã Check-in (UC07)
                DatabaseHelper.ExecuteNonQuery(
                    "UPDATE VeMayBay SET TrangThai = N'Đã Check-in' WHERE MaVe = @MaVe",
                    CommandType.Text,
                    new SqlParameter("@MaVe", maVe));

                TempData["Success"] = $"Check-in thành công cho vé {maVe}! Thẻ lên máy bay điện tử (Boarding Pass) đã được khởi tạo bên dưới.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { maDatCho, tenHanhKhach });
        }
    }
}