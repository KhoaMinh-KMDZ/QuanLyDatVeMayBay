using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("UserType") != "NV")
                return RedirectToAction("Login", "Account");

            // 1. Chỉ số KPI tổng quan
            ViewBag.TongDoanhThu = Convert.ToDecimal(DatabaseHelper.ExecuteScalar(
                "SELECT ISNULL(SUM(TongTien), 0) FROM HoaDon WHERE TrangThai = N'Thành công'"));
            ViewBag.TongVeDaBan = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM VeMayBay WHERE TrangThai IN (N'Đã xuất vé', N'Đã Check-in')"));
            ViewBag.SoChuyenMoBan = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM ChuyenBay WHERE TrangThai = N'Đang mở bán'"));
            ViewBag.TongKhachHang = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM KhachHang"));

            // 2. Dữ liệu bảng & biểu đồ
            ViewBag.DoanhThuTheoHang = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_BaoCaoDoanhThuTheoHang ORDER BY TongDoanhThuVe DESC");
            ViewBag.TopTuyenBay = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_TopTuyenBayDatNhieuNhat");
            ViewBag.DanhSachChuyenBay = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_DanhSachChuyenBayChiTiet ORDER BY NgayGioBay ASC");
            ViewBag.DanhSachDonHang = DatabaseHelper.ExecuteQuery("SELECT TOP 20 * FROM vw_LichSuDatVeKhachHang ORDER BY NgayDat DESC");
            ViewBag.DanhSachHuyDoi = DatabaseHelper.ExecuteQuery("SELECT TOP 20 * FROM YeuCauHuyDoiVe ORDER BY NgayYeuCau DESC");
            ViewBag.DanhSachKhuyenMai = DatabaseHelper.ExecuteQuery("SELECT * FROM KhuyenMai ORDER BY NgayKetThuc DESC");
            ViewBag.DanhSachNhanVien = DatabaseHelper.ExecuteQuery(@"
                SELECT nv.*, vt.TenRole FROM NhanVien nv JOIN VaiTro vt ON nv.RoleID = vt.RoleID");
            ViewBag.DanhSachDanhGia = DatabaseHelper.ExecuteQuery(@"
                SELECT dg.*, kh.TenKH FROM DanhGiaChuyenBay dg JOIN KhachHang kh ON dg.MaKH = kh.MaKH ORDER BY dg.NgayDanhGia DESC");

            ViewBag.TuyenBays = DatabaseHelper.ExecuteQuery("SELECT MaTuyenBay FROM TuyenBay");
            ViewBag.MayBays = DatabaseHelper.ExecuteQuery("SELECT MaMayBay, TenMayBay, MaHang FROM MayBay WHERE TrangThai = N'Sẵn sàng'");

            return View();
        }

        [HttpPost]
        public IActionResult AddFlight(string maChuyenBay, string maTuyenBay, string maMayBay, DateTime ngayGioBay, DateTime ngayGioDenDK, decimal giaVeCoBan)
        {
            if (HttpContext.Session.GetInt32("RoleID") != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền thêm lịch bay!";
                return RedirectToAction("Dashboard");
            }

            try
            {
                int thoiGianBay = (int)(ngayGioDenDK - ngayGioBay).TotalMinutes;
                int tongGhe = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT TongSoGhe FROM MayBay WHERE MaMayBay = @MaMB",
                    CommandType.Text,
                    new SqlParameter("@MaMB", maMayBay)));

                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
                    VALUES (@MaCB, @MaTB, @MaMB, @GioBay, @GioDen, @ThoiGian, @Gia, @SoGhe, N'Đang mở bán')",
                    CommandType.Text,
                    new SqlParameter("@MaCB", maChuyenBay.Trim().ToUpper()),
                    new SqlParameter("@MaTB", maTuyenBay),
                    new SqlParameter("@MaMB", maMayBay),
                    new SqlParameter("@GioBay", ngayGioBay),
                    new SqlParameter("@GioDen", ngayGioDenDK),
                    new SqlParameter("@ThoiGian", thoiGianBay),
                    new SqlParameter("@Gia", giaVeCoBan),
                    new SqlParameter("@SoGhe", tongGhe));

                TempData["Success"] = $"Đã thêm lịch chuyến bay {maChuyenBay.ToUpper()} thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi thêm chuyến bay: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult AddPromo(string maKM, string tenKM, decimal phanTramGiam, decimal giamToiDa, decimal donHangToiThieu, int soLuong, DateTime ngayKetThuc)
        {
            if (HttpContext.Session.GetInt32("RoleID") != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền tạo khuyến mãi!";
                return RedirectToAction("Dashboard");
            }

            try
            {
                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO KhuyenMai (MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu, SoLuong, NgayBatDau, NgayKetThuc, TrangThai)
                    VALUES (@MaKM, @TenKM, @PT, @Max, @Min, @SL, GETDATE(), @End, 1)",
                    CommandType.Text,
                    new SqlParameter("@MaKM", maKM.Trim().ToUpper()),
                    new SqlParameter("@TenKM", tenKM),
                    new SqlParameter("@PT", phanTramGiam),
                    new SqlParameter("@Max", giamToiDa),
                    new SqlParameter("@Min", donHangToiThieu),
                    new SqlParameter("@SL", soLuong),
                    new SqlParameter("@End", ngayKetThuc));

                TempData["Success"] = $"Đã phát hành mã khuyến mãi {maKM.ToUpper()}!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi tạo khuyến mãi: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult TogglePromo(string maKM)
        {
            DatabaseHelper.ExecuteNonQuery("UPDATE KhuyenMai SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END WHERE MaKM = @MaKM",
                CommandType.Text, new SqlParameter("@MaKM", maKM));
            TempData["Success"] = $"Đã cập nhật trạng thái mã khuyến mãi {maKM}!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult ToggleStaff(string maNV)
        {
            if (HttpContext.Session.GetInt32("RoleID") == 1 && maNV != "NV01")
            {
                DatabaseHelper.ExecuteNonQuery("UPDATE NhanVien SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END WHERE MaNV = @MaNV",
                    CommandType.Text, new SqlParameter("@MaNV", maNV));
                TempData["Success"] = $"Đã cập nhật trạng thái tài khoản nhân viên {maNV}!";
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult RunCleanupCursor()
        {
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