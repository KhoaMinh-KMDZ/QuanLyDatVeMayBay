using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index(string? maSanBayDi, string? maSanBayDen, DateTime? ngayBay, string? maHangGhe, string? maHang, decimal? giaMax)
        {
            ViewBag.SanBays = DatabaseHelper.ExecuteQuery("SELECT MaSanBay, TenSanBay, ThanhPho FROM SanBay WHERE TrangThai = 1 ORDER BY ThanhPho");
            ViewBag.HangHangKhongs = DatabaseHelper.ExecuteQuery("SELECT MaHang, TenHang FROM HangHangKhong WHERE TrangThai = 1");
            ViewBag.HangGhes = DatabaseHelper.ExecuteQuery("SELECT MaHangGhe, TenHangGhe, HeSoGia FROM HangGhe ORDER BY HeSoGia ASC");

            ViewBag.SelectedDi = maSanBayDi ?? "SGN";
            ViewBag.SelectedDen = maSanBayDen ?? "HAN";
            ViewBag.SelectedNgay = (ngayBay ?? new DateTime(2026, 9, 1)).ToString("yyyy-MM-dd");
            ViewBag.SelectedHangGhe = maHangGhe ?? "ECO";
            ViewBag.SelectedHang = maHang ?? "";
            ViewBag.SelectedGiaMax = giaMax;

            DataTable dtChuyenBay = new DataTable();

            if (!string.IsNullOrEmpty(maSanBayDi) && !string.IsNullOrEmpty(maSanBayDen) && ngayBay.HasValue)
            {
                ViewBag.DaTimKiem = true;
                dtChuyenBay = DatabaseHelper.ExecuteQuery(
                    "sp_TimKiemChuyenBay",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaSanBayDi", maSanBayDi),
                    new SqlParameter("@MaSanBayDen", maSanBayDen),
                    new SqlParameter("@NgayBay", ngayBay.Value.Date),
                    new SqlParameter("@MaHangGhe", string.IsNullOrEmpty(maHangGhe) ? DBNull.Value : maHangGhe),
                    new SqlParameter("@GiaMax", giaMax.HasValue ? giaMax.Value : DBNull.Value),
                    new SqlParameter("@MaHang", string.IsNullOrEmpty(maHang) ? DBNull.Value : maHang)
                );
            }
            else
            {
                ViewBag.DaTimKiem = false;
                dtChuyenBay = DatabaseHelper.ExecuteQuery(@"
                    SELECT TOP 8 
                        MaChuyenBay, TenHangHangKhong AS TenHang, TenMayBay,
                        TenSanBayDi AS SanBayDi, ThanhPhoDi,
                        TenSanBayDen AS SanBayDen, ThanhPhoDen,
                        NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThaiChuyenBay AS TrangThai
                    FROM vw_DanhSachChuyenBayChiTiet
                    WHERE TrangThaiChuyenBay = N'Đang mở bán' AND SoGheTrong > 0
                    ORDER BY NgayGioBay ASC");
            }

            ViewBag.DiemDung = DatabaseHelper.ExecuteQuery(@"
                SELECT ct.MaChuyenBay, sb.ThanhPho, sb.MaSanBay, ct.ThoiGianDung, ct.GhiChu
                FROM ChiTietChuyenBay ct
                JOIN SanBay sb ON ct.MaSanBayTG = sb.MaSanBay");

            return View(dtChuyenBay);
        }
    }
}