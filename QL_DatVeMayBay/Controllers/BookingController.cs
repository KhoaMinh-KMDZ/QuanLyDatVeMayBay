using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class BookingController : Controller
    {
        // ============================================================
        // 1. SƠ ĐỒ GHẾ TRỰC QUAN & NHẬP THÔNG TIN HÀNH KHÁCH (UC02)
        // ============================================================
        [HttpGet]
        public IActionResult SelectSeats(string maChuyenBay, string? maHangGhe)
        {
            if (string.IsNullOrEmpty(maChuyenBay))
                return RedirectToAction("Index", "Home");

            DataTable dtCB = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_DanhSachChuyenBayChiTiet WHERE MaChuyenBay = @MaCB",
                CommandType.Text,
                new SqlParameter("@MaCB", maChuyenBay));

            if (dtCB.Rows.Count == 0)
                return RedirectToAction("Index", "Home");

            DataRow cb = dtCB.Rows[0];
            string maMayBay = cb["MaMayBay"].ToString()!;
            decimal giaCoBan = Convert.ToDecimal(cb["GiaVeCoBan"]);

            DataTable dtGhe = DatabaseHelper.ExecuteQuery(@"
                SELECT 
                    g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe, hg.HeSoGia,
                    dbo.fn_TinhGiaVeThucTe(@GiaCoBan, g.MaHangGhe) AS GiaThucTe,
                    CASE 
                        WHEN g.TrangThai = 0 THEN N'Hỏng'
                        WHEN EXISTS (
                            SELECT 1 FROM VeMayBay v 
                            WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe 
                              AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                        ) THEN N'Đã đặt'
                        ELSE N'Trống'
                    END AS TinhTrang
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                WHERE g.MaMayBay = @MaMayBay
                ORDER BY g.SoGhe ASC",
                CommandType.Text,
                new SqlParameter("@GiaCoBan", giaCoBan),
                new SqlParameter("@MaCB", maChuyenBay),
                new SqlParameter("@MaMayBay", maMayBay));

            ViewBag.ChuyenBay = cb;
            ViewBag.DanhSachGhe = dtGhe;
            ViewBag.KhuyenMais = DatabaseHelper.ExecuteQuery(
                "SELECT MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu FROM KhuyenMai WHERE TrangThai = 1 AND SoLuong > 0");

            return View();
        }

        // ============================================================
        // 2. KHÓA GHẾ & KHỞI TẠO MÃ PNR (sp_DatVeMayBay_Batch)
        // ============================================================
        [HttpPost]
        public IActionResult CreateBooking(
            string maChuyenBay,
            int[] selectedMaGhe,
            string[] tenHanhKhach,
            string[] cccdHanhKhach,
            string[] loaiHanhKhach,
            string? maKM,
            string? dichVuPhuTro,
            string? ghiChuThem)
        {
            if (selectedMaGhe == null || selectedMaGhe.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 vị trí ghế trên sơ đồ!";
                return RedirectToAction("SelectSeats", new { maChuyenBay });
            }

            string maKH = HttpContext.Session.GetString("UserType") == "KH"
                ? (HttpContext.Session.GetString("UserId") ?? "KH00001")
                : "KH00001";

            decimal giaCoBan = Convert.ToDecimal(DatabaseHelper.ExecuteScalar(
                "SELECT GiaVeCoBan FROM ChuyenBay WHERE MaChuyenBay = @MaCB",
                CommandType.Text,
                new SqlParameter("@MaCB", maChuyenBay)));

            DataTable dtVe = new DataTable();
            dtVe.Columns.Add("MaGhe", typeof(int));
            dtVe.Columns.Add("MaHangGhe", typeof(string));
            dtVe.Columns.Add("TenHanhKhach", typeof(string));
            dtVe.Columns.Add("CCCD_HanhKhach", typeof(string));
            dtVe.Columns.Add("LoaiHanhKhach", typeof(string));
            dtVe.Columns.Add("GiaVe", typeof(decimal));

            for (int i = 0; i < selectedMaGhe.Length; i++)
            {
                int maGhe = selectedMaGhe[i];
                DataTable dtInfoGhe = DatabaseHelper.ExecuteQuery(@"
                    SELECT g.MaHangGhe, dbo.fn_TinhGiaVeThucTe(@GiaCoBan, g.MaHangGhe) AS GiaVe
                    FROM GheNgoi g WHERE g.MaGhe = @MaGhe",
                    CommandType.Text,
                    new SqlParameter("@GiaCoBan", giaCoBan),
                    new SqlParameter("@MaGhe", maGhe));

                string maHangGhe = dtInfoGhe.Rows[0]["MaHangGhe"].ToString()!;
                decimal giaVe = Convert.ToDecimal(dtInfoGhe.Rows[0]["GiaVe"]);

                dtVe.Rows.Add(
                    maGhe,
                    maHangGhe,
                    tenHanhKhach[i].Trim(),
                    string.IsNullOrWhiteSpace(cccdHanhKhach[i]) ? "079204000000" : cccdHanhKhach[i].Trim(),
                    loaiHanhKhach[i],
                    giaVe
                );
            }

            string maDatCho = "PNR" + Random.Shared.Next(100000, 999999);
            string ghiChuFull = string.Join(" | ", new[] { dichVuPhuTro, ghiChuThem }.Where(s => !string.IsNullOrWhiteSpace(s)));

            try
            {
                DatabaseHelper.ExecuteQuery(
                    "sp_DatVeMayBay_Batch",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaDatCho", maDatCho),
                    new SqlParameter("@MaKH", maKH),
                    new SqlParameter("@MaChuyenBay", maChuyenBay),
                    new SqlParameter("@MaKM", string.IsNullOrWhiteSpace(maKM) ? DBNull.Value : maKM.Trim().ToUpper()),
                    new SqlParameter("@GhiChu", string.IsNullOrWhiteSpace(ghiChuFull) ? DBNull.Value : ghiChuFull),
                    DatabaseHelper.CreateTableParameter("@DSVe", "ChiTietDatVe_Type", dtVe)
                );

                return RedirectToAction("Payment", new { maDatCho });
            }
            catch (SqlException ex)
            {
                TempData["Error"] = "Không thể giữ chỗ: " + ex.Message;
                return RedirectToAction("SelectSeats", new { maChuyenBay });
            }
        }

        // ============================================================
        // 3. THANH TOÁN ĐA KÊNH & XUẤT HÓA ĐƠN ĐIỆN TỬ (UC04 + UC19)
        // ============================================================
        [HttpGet]
        public IActionResult Payment(string maDatCho)
        {
            if (string.IsNullOrEmpty(maDatCho))
                return RedirectToAction("Index", "Home");

            DataTable dtDon = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @MaDatCho",
                CommandType.Text,
                new SqlParameter("@MaDatCho", maDatCho));

            if (dtDon.Rows.Count == 0)
                return RedirectToAction("Index", "Home");

            DataTable dtVes = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @MaDatCho",
                CommandType.Text,
                new SqlParameter("@MaDatCho", maDatCho));

            ViewBag.DonDatCho = dtDon.Rows[0];
            ViewBag.DanhSachVe = dtVes;
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmPayment(string maDatCho, string phuongThuc)
        {
            string maHD = "HD" + Random.Shared.Next(100000, 999999);
            string maGiaoDich = "TXN_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            string? maNV = HttpContext.Session.GetString("UserType") == "NV"
                ? HttpContext.Session.GetString("UserId")
                : null;

            try
            {
                DatabaseHelper.ExecuteQuery(
                    "sp_ThanhToanDonVe",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaHD", maHD),
                    new SqlParameter("@MaDatCho", maDatCho),
                    new SqlParameter("@MaNV", (object?)maNV ?? DBNull.Value),
                    new SqlParameter("@PhuongThuc", phuongThuc),
                    new SqlParameter("@MaGiaoDich", maGiaoDich)
                );

                TempData["Success"] = $"Thanh toán thành công! Hóa đơn {maHD} đã được lập và vé điện tử đã xuất.";
                return RedirectToAction("Lookup", new { maDatCho });
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Payment", new { maDatCho });
            }
        }

        [HttpGet]
        public IActionResult Invoice(string maDatCho)
        {
            if (string.IsNullOrWhiteSpace(maDatCho))
                return RedirectToAction("Lookup");

            DataTable dtHD = DatabaseHelper.ExecuteQuery(@"
                SELECT hd.*, ('TXN-' + hd.MaHD) AS MaGiaoDich, pdc.MaKH, kh.TenKH, kh.SDT, kh.Email, kh.DiaChi, kh.CCCD,
                       pdc.TongTienGoc, pdc.TienGiam, pdc.TongTienSauGiam, pdc.GhiChu
                FROM HoaDon hd
                JOIN PhieuDatCho pdc ON hd.MaDatCho = pdc.MaDatCho
                JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
                WHERE hd.MaDatCho = @PNR",
                CommandType.Text,
                new SqlParameter("@PNR", maDatCho.Trim().ToUpper()));

            if (dtHD.Rows.Count == 0)
            {
                TempData["Error"] = "Đơn đặt chỗ này chưa phát sinh hóa đơn thanh toán!";
                return RedirectToAction("Lookup", new { maDatCho });
            }

            ViewBag.HoaDon = dtHD.Rows[0];
            ViewBag.DanhSachVe = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @PNR",
                CommandType.Text,
                new SqlParameter("@PNR", maDatCho.Trim().ToUpper()));

            return View();
        }

        // ============================================================
        // 4. CỔNG CHUYẾN BAY CỦA TÔI (DÀNH CHO KHÁCH ĐÃ ĐĂNG NHẬP)
        // ============================================================
        [HttpGet]
        public IActionResult MyBookings()
        {
            if (HttpContext.Session.GetString("UserType") != "KH")
                return RedirectToAction("Login", "Account");

            string maKH = HttpContext.Session.GetString("UserId")!;
            DataTable dt = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaKH = @MaKH ORDER BY NgayDat DESC",
                CommandType.Text,
                new SqlParameter("@MaKH", maKH));

            return View(dt);
        }

        // ============================================================
        // 5. TRA CỨU PNR, HỦY VÉ (LUẬT 72H), ĐỔI VÉ & ĐÁNH GIÁ SAO
        // ============================================================
        [HttpGet]
        public IActionResult Lookup(string? maDatCho)
        {
            if (!string.IsNullOrWhiteSpace(maDatCho))
            {
                string pnr = maDatCho.Trim().ToUpper();
                DataTable dtDon = DatabaseHelper.ExecuteQuery(
                    "SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @PNR",
                    CommandType.Text,
                    new SqlParameter("@PNR", pnr));

                if (dtDon.Rows.Count > 0)
                {
                    ViewBag.DonDatCho = dtDon.Rows[0];
                    DataTable dtVes = DatabaseHelper.ExecuteQuery(
                        "SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @PNR",
                        CommandType.Text,
                        new SqlParameter("@PNR", pnr));
                    ViewBag.DanhSachVe = dtVes;

                    var refundPreview = new Dictionary<string, DataRow>();
                    foreach (DataRow v in dtVes.Rows)
                    {
                        string maVe = v["MaVe"].ToString()!;
                        DataTable dtRef = DatabaseHelper.ExecuteQuery(
                            "SELECT * FROM fn_TinhTienHoanTraHuyVe(@MaVe)",
                            CommandType.Text,
                            new SqlParameter("@MaVe", maVe));
                        if (dtRef.Rows.Count > 0)
                            refundPreview[maVe] = dtRef.Rows[0];
                    }
                    ViewBag.RefundPreview = refundPreview;

                    ViewBag.ChuyenBayKhac = DatabaseHelper.ExecuteQuery(@"
                        SELECT TOP 20 cb.MaChuyenBay, hhk.TenHang, tb.MaTuyenBay, cb.NgayGioBay, cb.GiaVeCoBan
                        FROM ChuyenBay cb
                        JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
                        JOIN HangHangKhong hhk ON mb.MaHang = hhk.MaHang
                        JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
                        WHERE cb.TrangThai = N'Đang mở bán' AND cb.SoGheTrong > 0
                        ORDER BY cb.NgayGioBay ASC");
                }
                else
                {
                    ViewBag.NotFound = $"Không tìm thấy hồ sơ đặt chỗ với mã PNR: {pnr}";
                }
            }
            return View();
        }

        // API JSON trả về danh sách ghế trống của chuyến bay khi đổi vé
        [HttpGet]
        public IActionResult GetAvailableSeats(string maChuyenBay)
        {
            DataTable dt = DatabaseHelper.ExecuteQuery(@"
                SELECT g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe,
                       dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe) AS GiaThucTe
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB AND g.TrangThai = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM VeMayBay v 
                      WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe 
                        AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                  )
                ORDER BY g.SoGhe ASC",
                CommandType.Text,
                new SqlParameter("@MaCB", maChuyenBay));

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new
                {
                    maGhe = Convert.ToInt32(r["MaGhe"]),
                    soGhe = r["SoGhe"].ToString(),
                    maHangGhe = r["MaHangGhe"].ToString(),
                    tenHangGhe = r["TenHangGhe"].ToString(),
                    giaThucTe = Convert.ToDecimal(r["GiaThucTe"])
                });
            }
            return Json(list);
        }

        [HttpPost]
        public IActionResult CancelTicket(string maDatCho, string maVe, string lyDo)
        {
            string? maNV = HttpContext.Session.GetString("UserType") == "NV"
                ? HttpContext.Session.GetString("UserId")
                : null;

            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_XuLyHuyVeVaHoanTien",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaVe", maVe),
                    new SqlParameter("@LyDo", string.IsNullOrWhiteSpace(lyDo) ? "Khách yêu cầu hủy vé trực tuyến" : lyDo),
                    new SqlParameter("@MaNVXuLy", (object?)maNV ?? DBNull.Value)
                );

                // Đồng bộ trạng thái PhieuDatCho nếu tất cả vé trong đơn đều đã hủy
                DatabaseHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT 1 FROM VeMayBay WHERE MaDatCho = @PNR AND TrangThai <> N'Đã hủy')
                        UPDATE PhieuDatCho SET TrangThai = N'Đã hủy' WHERE MaDatCho = @PNR",
                    CommandType.Text,
                    new SqlParameter("@PNR", maDatCho));

                DataRow r = dt.Rows[0];
                TempData["Success"] = $"Đã hủy vé {maVe}! Tỷ lệ hoàn theo Luật 72h: {r["PhanTramHoan"]}% — Số tiền hoàn khách nhận: {Convert.ToDecimal(r["SoTienHoanKhachNhan"]):N0} VNĐ.";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        public IActionResult RescheduleTicket(string maDatCho, string maVeCu, string maChuyenBayMoi, int maGheMoi, string maHangGheMoi)
        {
            string? maNV = HttpContext.Session.GetString("UserType") == "NV"
                ? HttpContext.Session.GetString("UserId")
                : null;

            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_DoiVeMayBay",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaVeCu", maVeCu),
                    new SqlParameter("@MaChuyenBayMoi", maChuyenBayMoi),
                    new SqlParameter("@MaGheMoi", maGheMoi),
                    new SqlParameter("@MaHangGheMoi", maHangGheMoi),
                    new SqlParameter("@MaNVXuLy", (object?)maNV ?? DBNull.Value)
                );

                DataRow r = dt.Rows[0];
                TempData["Success"] = $"Đổi vé thành công sang mã vé mới {r["MaVeMoi"]}! Chênh lệch giá: {Convert.ToDecimal(r["TienChenhLechGia"]):N0}đ + Phí đổi: {Convert.ToDecimal(r["PhiDichVuDoi"]):N0}đ = Tổng thu thêm: {Convert.ToDecimal(r["TongTienThanhToanThem"]):N0} VNĐ.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        public IActionResult SubmitReview(string maDatCho, string maChuyenBay, int soSao, string binhLuan)
        {
            try
            {
                string maKH = HttpContext.Session.GetString("UserType") == "KH"
                    ? (HttpContext.Session.GetString("UserId") ?? "KH00001")
                    : (DatabaseHelper.ExecuteScalar("SELECT MaKH FROM PhieuDatCho WHERE MaDatCho = @PNR", CommandType.Text, new SqlParameter("@PNR", maDatCho))?.ToString() ?? "KH00001");

                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO DanhGiaChuyenBay (MaKH, MaChuyenBay, SoSao, BinhLuan)
                    VALUES (@MaKH, @MaCB, @SoSao, @BinhLuan)",
                    CommandType.Text,
                    new SqlParameter("@MaKH", maKH),
                    new SqlParameter("@MaCB", maChuyenBay),
                    new SqlParameter("@SoSao", soSao),
                    new SqlParameter("@BinhLuan", binhLuan));

                TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá trải nghiệm chuyến bay!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể gửi đánh giá: " + ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }
    }
}