using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Models;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public partial class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("UserType") != "NV")
                return RedirectToAction("Login", "Account");

            int role = HttpContext.Session.GetInt32("RoleID") ?? 0;
            if (role is 1 or 2)
            {
                ViewBag.TongDoanhThu = DatabaseHelper.ExecuteScalar(
                    "SELECT ISNULL(SUM(TongTien), 0) FROM HoaDon WHERE TrangThai = N'Đã thanh toán'");
                ViewBag.TongVeDaBan = DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM VeMayBay WHERE TrangThai IN (N'Đã xuất vé', N'Đã Check-in')");
                ViewBag.SoChuyenMoBan = DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM dbo.vw_DanhSachChuyenBayChiTiet WHERE TrangThaiChuyenBay = N'Đang mở bán' AND NgayGioBay > DATEADD(HOUR, 2, GETDATE())");
                ViewBag.TongKhachHang = DatabaseHelper.ExecuteScalar("SELECT COUNT(*) FROM KhachHang");
                ViewBag.DoanhThuTheoHang = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_BaoCaoDoanhThuTheoHang ORDER BY TongDoanhThuVe DESC");
                ViewBag.TopTuyenBay = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_TopTuyenBayDatNhieuNhat");
            }

            ViewBag.DanhSachDonHang = DatabaseHelper.ExecuteQuery("SELECT TOP 20 * FROM vw_LichSuDatVeKhachHang ORDER BY NgayDat DESC");
            if (role is 1 or 4)
            {
                ViewBag.DanhSachHuyDoi = DatabaseHelper.ExecuteQuery("SELECT TOP 20 * FROM YeuCauHuyDoiVe ORDER BY NgayYeuCau DESC");
                ViewBag.DanhSachDanhGia = DatabaseHelper.ExecuteQuery(@"
                    SELECT dg.*, kh.TenKH FROM DanhGiaChuyenBay dg JOIN KhachHang kh ON dg.MaKH = kh.MaKH ORDER BY dg.NgayDanhGia DESC");
            }

            if (role == 1)
            {
                ViewBag.DanhSachChuyenBay = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_DanhSachChuyenBayChiTiet ORDER BY NgayGioBay ASC");
                ViewBag.DanhSachKhuyenMai = DatabaseHelper.ExecuteQuery("SELECT * FROM KhuyenMai ORDER BY NgayKetThuc DESC");
                ViewBag.DanhSachNhanVien = DatabaseHelper.ExecuteQuery(@"
                    SELECT nv.MaNV, nv.TenNV, nv.TaiKhoan, nv.ChucVu, nv.TrangThai, vt.TenRole
                    FROM NhanVien nv JOIN VaiTro vt ON nv.RoleID = vt.RoleID");
                ViewBag.TuyenBays = DatabaseHelper.ExecuteQuery("SELECT MaTuyenBay FROM TuyenBay");
                ViewBag.MayBays = DatabaseHelper.ExecuteQuery("SELECT MaMayBay, TenMayBay, MaHang FROM MayBay WHERE TrangThai = N'Sẵn sàng'");
                ViewBag.HangGhes = DatabaseHelper.ExecuteQuery("SELECT MaHangGhe, TenHangGhe, HeSoGia FROM HangGhe");
            }

            return View();
        }

        [HttpPost]
        public IActionResult AddFlight(string maChuyenBay, string maTuyenBay, string maMayBay, DateTime ngayGioBay, DateTime ngayGioDenDK, decimal giaVeCoBan)
        {
            if (!IsAdmin)
                return StatusCode(StatusCodes.Status403Forbidden);
            var form = new FlightForm
            {
                MaChuyenBay = maChuyenBay,
                MaTuyenBay = maTuyenBay,
                MaMayBay = maMayBay,
                NgayGioBay = ngayGioBay,
                NgayGioDenDK = ngayGioDenDK,
                GiaVeCoBan = giaVeCoBan
            };
            if (!TryValidateModel(form))
                TempData["Error"] = "Thông tin lịch bay không hợp lệ.";
            else if (SaveFlight(form) is { } error)
                TempData["Error"] = error;
            else
                TempData["Success"] = $"Đã thêm lịch chuyến bay {maChuyenBay.ToUpperInvariant()}.";
            return RedirectToAction(nameof(Dashboard), null, null, "tab-flights");
        }

        [HttpPost]
        public IActionResult AddPromo(string maKM, string tenKM, decimal phanTramGiam, decimal giamToiDa, decimal donHangToiThieu, int soLuong, DateTime ngayKetThuc, DateTime? ngayBatDau)
        {
            if (HttpContext.Session.GetString("UserType") != "NV" || HttpContext.Session.GetInt32("RoleID") != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền tạo khuyến mãi!";
                return RedirectToAction("Dashboard");
            }

            DateTime startDate = (ngayBatDau ?? DateTime.Today).Date;
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(maKM) || maKM.Trim().Length > 20 ||
                string.IsNullOrWhiteSpace(tenKM) || tenKM.Trim().Length > 100 || phanTramGiam is < 0 or > 100 ||
                giamToiDa is < 0 or > 999999999999 || donHangToiThieu is < 0 or > 999999999999 ||
                soLuong < 0 || ngayKetThuc.Date < startDate)
            {
                TempData["Error"] = "Kiểm tra mã, mức giảm, số lượt và thời gian khuyến mãi.";
                return RedirectToAction(nameof(Dashboard), null, null, "tab-promos");
            }

            try
            {
                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO KhuyenMai (MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu, SoLuong, NgayBatDau, NgayKetThuc, TrangThai)
                    VALUES (@MaKM, @TenKM, @PT, @Max, @Min, @SL, @Start, @End, 1)",
                    CommandType.Text,
                    new SqlParameter("@MaKM", maKM.Trim().ToUpper()),
                    new SqlParameter("@TenKM", tenKM.Trim()),
                    new SqlParameter("@PT", phanTramGiam),
                    new SqlParameter("@Max", giamToiDa),
                    new SqlParameter("@Min", donHangToiThieu),
                    new SqlParameter("@SL", soLuong),
                    new SqlParameter("@Start", SqlDbType.Date) { Value = startDate },
                    new SqlParameter("@End", SqlDbType.Date) { Value = ngayKetThuc.Date });

                TempData["Success"] = $"Đã phát hành mã khuyến mãi {maKM.ToUpper()}!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi tạo khuyến mãi: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult UpdateFare(string? maChuyenBay, decimal? giaVeCoBan, string? maHangGhe, decimal? heSoGia)
        {
            if (HttpContext.Session.GetString("UserType") != "NV" || HttpContext.Session.GetInt32("RoleID") != 1)
                return StatusCode(StatusCodes.Status403Forbidden);

            int rows = 0;
            if (ModelState.IsValid && maChuyenBay?.Length is > 0 and <= 20 && giaVeCoBan is >= 1000 and <= 999999999)
            {
                rows = DatabaseHelper.ExecuteNonQuery(@"UPDATE ChuyenBay SET GiaVeCoBan = @Gia
                    WHERE MaChuyenBay = @Ma AND NgayGioBay > GETDATE()
                      AND TrangThai IN (N'Đang mở bán', N'Hết vé')",
                    CommandType.Text, new SqlParameter("@Gia", giaVeCoBan), new SqlParameter("@Ma", maChuyenBay));
            }
            else if (ModelState.IsValid && maHangGhe?.Length is > 0 and <= 10 && heSoGia is >= 1 and <= 99.99m)
            {
                rows = DatabaseHelper.ExecuteNonQuery("UPDATE HangGhe SET HeSoGia = @HeSo WHERE MaHangGhe = @Ma",
                    CommandType.Text, new SqlParameter("@HeSo", heSoGia), new SqlParameter("@Ma", maHangGhe));
            }

            if (rows == 1)
                TempData["Success"] = "Đã áp dụng giá mới cho các giao dịch tiếp theo.";
            else
                TempData["Error"] = "Không cập nhật được giá. Kiểm tra chuyến bay, giá hoặc hệ số.";

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public IActionResult TogglePromo(string maKM)
        {
            if (HttpContext.Session.GetString("UserType") != "NV" || HttpContext.Session.GetInt32("RoleID") != 1)
                return StatusCode(StatusCodes.Status403Forbidden);

            DatabaseHelper.ExecuteNonQuery("UPDATE KhuyenMai SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END WHERE MaKM = @MaKM",
                CommandType.Text, new SqlParameter("@MaKM", maKM));
            TempData["Success"] = $"Đã cập nhật trạng thái mã khuyến mãi {maKM}!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult ToggleStaff(string maNV)
        {
            if (!IsAdmin)
                return StatusCode(StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(maNV))
                return NotFound();

            int changed = DatabaseHelper.ExecuteNonQuery(@"
                UPDATE NhanVien SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END
                WHERE MaNV = @MaNV AND MaNV <> @Actor", CommandType.Text,
                new SqlParameter("@MaNV", maNV), new SqlParameter("@Actor", HttpContext.Session.GetString("UserId")));
            if (changed > 0)
                TempData["Success"] = "Đã cập nhật trạng thái tài khoản nhân viên.";
            else
                TempData["Error"] = "Không thể khóa hoặc mở lại tài khoản đang đăng nhập.";

            return RedirectToAction(nameof(Dashboard), null, null, "tab-staff");
        }

        [HttpPost]
        public IActionResult RunCleanupCursor()
        {
            if (HttpContext.Session.GetString("UserType") != "NV" || HttpContext.Session.GetInt32("RoleID") != 1)
                return StatusCode(StatusCodes.Status403Forbidden);

            try
            {
                DatabaseHelper.ExecuteNonQuery("sp_TuDongHuyDonGiuChoHetHan_Cursor", CommandType.StoredProcedure);
                TempData["Success"] = "Đã chạy Cursor quét và giải phóng toàn bộ đơn đặt chỗ quá hạn thanh toán!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Dashboard");
        }
    }
}