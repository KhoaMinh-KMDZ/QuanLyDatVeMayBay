using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index(string? maSanBayDi, string? maSanBayDen, DateTime? ngayBay, string? maHangGhe, string? maHang, decimal? giaMax, int soHanhKhach = 1)
        {
            ViewBag.SanBays = DatabaseHelper.ExecuteQuery("SELECT MaSanBay, TenSanBay, ThanhPho FROM SanBay WHERE TrangThai = 1 ORDER BY ThanhPho");
            ViewBag.HangHangKhongs = DatabaseHelper.ExecuteQuery("SELECT MaHang, TenHang FROM HangHangKhong WHERE TrangThai = 1");
            ViewBag.HangGhes = DatabaseHelper.ExecuteQuery("SELECT MaHangGhe, TenHangGhe, HeSoGia FROM HangGhe ORDER BY HeSoGia ASC");

            ViewBag.SelectedDi = maSanBayDi ?? "SGN";
            ViewBag.SelectedDen = maSanBayDen ?? "HAN";
            ViewBag.SelectedNgay = (ngayBay ?? DateTime.Today).ToString("yyyy-MM-dd");
            ViewBag.SoHanhKhach = soHanhKhach;
            ViewBag.SelectedHangGhe = maHangGhe ?? "ECO";
            ViewBag.SelectedHang = maHang ?? "";
            ViewBag.SelectedGiaMax = giaMax;

            DataTable dtChuyenBay = new DataTable();

            if (Request.Query.ContainsKey(nameof(maSanBayDi)) ||
                Request.Query.ContainsKey(nameof(maSanBayDen)) || Request.Query.ContainsKey(nameof(ngayBay)))
            {
                ViewBag.DaTimKiem = true;
                ViewBag.SelectedHangGhe = maHangGhe ?? "";
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(maSanBayDi) ||
                    string.IsNullOrWhiteSpace(maSanBayDen) ||
                    string.Equals(maSanBayDi, maSanBayDen, StringComparison.OrdinalIgnoreCase) ||
                    ngayBay is null || ngayBay.Value.Date < DateTime.Today || giaMax < 0 || soHanhKhach < 1)
                {
                    ViewBag.SearchError = "Chọn hai sân bay khác nhau, ngày bay hợp lệ và ít nhất một hành khách.";
                    return View(dtChuyenBay);
                }

                DataTable SearchFlights(int nearbyDays) => DatabaseHelper.ExecuteQuery(
                    "sp_TimKiemChuyenBay",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaSanBayDi", maSanBayDi),
                    new SqlParameter("@MaSanBayDen", maSanBayDen),
                    new SqlParameter("@NgayBay", ngayBay.Value.Date),
                    new SqlParameter("@MaHangGhe", string.IsNullOrEmpty(maHangGhe) ? DBNull.Value : maHangGhe),
                    new SqlParameter("@GiaMax", giaMax.HasValue ? giaMax.Value : DBNull.Value),
                    new SqlParameter("@MaHang", string.IsNullOrEmpty(maHang) ? DBNull.Value : maHang),
                    new SqlParameter("@SoHanhKhach", soHanhKhach),
                    new SqlParameter("@SoNgayLanCan", nearbyDays)
                );

                dtChuyenBay = SearchFlights(0);
                if (dtChuyenBay.Rows.Count == 0)
                {
                    ViewBag.NgayGoiY = SearchFlights(3).AsEnumerable()
                        .Select(row => row.Field<DateTime>("NgayGioBay").Date)
                        .Distinct()
                        .OrderBy(date => Math.Abs((date - ngayBay.Value.Date).Days))
                        .ThenBy(date => date)
                        .ToArray();
                }
            }
            else
            {
                ViewBag.DaTimKiem = false;
                object? nextFlight = DatabaseHelper.ExecuteScalar(@"
                    SELECT MIN(NgayGioBay)
                    FROM vw_DanhSachChuyenBayChiTiet
                    WHERE MaSanBayDi = 'SGN' AND MaSanBayDen = 'HAN'
                        AND TrangThaiChuyenBay = N'Đang mở bán'
                        AND NgayGioBay > DATEADD(HOUR, 2, GETDATE())
                        AND dbo.fn_DemSoGheTrongTheoHang(MaChuyenBay, 'ECO') > 0");
                if (nextFlight is DateTime nextDeparture)
                    ViewBag.SelectedNgay = nextDeparture.ToString("yyyy-MM-dd");
                dtChuyenBay = DatabaseHelper.ExecuteQuery(@"
                    SELECT TOP 8 
                        MaChuyenBay, TenHangHangKhong AS TenHang, TenMayBay,
                        TenSanBayDi AS SanBayDi, ThanhPhoDi,
                        TenSanBayDen AS SanBayDen, ThanhPhoDen,
                        NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThaiChuyenBay AS TrangThai
                    FROM vw_DanhSachChuyenBayChiTiet
                    WHERE TrangThaiChuyenBay = N'Đang mở bán' AND SoGheTrong > 0
                      AND NgayGioBay > DATEADD(HOUR, 2, GETDATE())
                    ORDER BY NgayGioBay ASC");
            }

            ViewBag.DiemDung = DatabaseHelper.ExecuteQuery(@"
                SELECT ct.MaChuyenBay, sb.ThanhPho, sb.MaSanBay, ct.ThoiGianDung, ct.GhiChu
                FROM ChiTietChuyenBay ct
                JOIN SanBay sb ON ct.MaSanBayTG = sb.MaSanBay
                ORDER BY ct.MaChuyenBay, ct.ThuTuDung");

            return View(dtChuyenBay);
        }
    }
}