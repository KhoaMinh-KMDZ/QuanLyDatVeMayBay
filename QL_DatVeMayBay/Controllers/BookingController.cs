using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class BookingController(IConfiguration configuration) : Controller
    {
        private string? CustomerId => HttpContext.Session.GetString("UserType") == "KH"
            ? HttpContext.Session.GetString("UserId") : null;

        private int StaffRole => HttpContext.Session.GetString("UserType") == "NV"
            ? HttpContext.Session.GetInt32("RoleID") ?? 0 : 0;

        private bool CanAccessBooking(string? pnr, params int[] staffRoles)
        {
            if (string.IsNullOrWhiteSpace(pnr))
                return false;
            if (staffRoles.Contains(StaffRole))
                return true;
            return CustomerId is { } customer && DatabaseHelper.ExecuteScalar(
                "SELECT 1 FROM PhieuDatCho WHERE MaDatCho = @PNR AND MaKH = @MaKH",
                CommandType.Text, new SqlParameter("@PNR", pnr), new SqlParameter("@MaKH", customer)) != null;
        }

        private static bool BelongsToBooking(string? ticket, string pnr) =>
            !string.IsNullOrWhiteSpace(ticket) && DatabaseHelper.ExecuteScalar(
                "SELECT 1 FROM VeMayBay WHERE MaVe = @MaVe AND MaDatCho = @PNR",
                CommandType.Text, new SqlParameter("@MaVe", ticket), new SqlParameter("@PNR", pnr)) != null;

        // ============================================================
        // 1. SƠ ĐỒ GHẾ TRỰC QUAN & NHẬP THÔNG TIN HÀNH KHÁCH (UC02)
        // ============================================================
        [HttpGet]
        public IActionResult SelectSeats(string maChuyenBay, string? maHangGhe)
        {
            if (string.IsNullOrEmpty(maChuyenBay))
                return RedirectToAction("Index", "Home");

            DataTable dtCB = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_DanhSachChuyenBayChiTiet WHERE MaChuyenBay = @MaCB AND TrangThaiChuyenBay = N'Đang mở bán' AND NgayGioBay > DATEADD(HOUR, 2, GETDATE())",
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
                            SELECT 1 FROM dbo.vw_VeMayBayChiemGhe v
                            WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe
                        ) THEN N'Đã đặt'
                        ELSE N'Trống'
                    END AS TinhTrang
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                WHERE g.MaMayBay = @MaMayBay AND (@MaHangGhe IS NULL OR g.MaHangGhe = @MaHangGhe)
                ORDER BY g.SoGhe ASC",
                CommandType.Text,
                new SqlParameter("@GiaCoBan", giaCoBan),
                new SqlParameter("@MaCB", maChuyenBay),
                new SqlParameter("@MaMayBay", maMayBay),
                new SqlParameter("@MaHangGhe", (object?)maHangGhe ?? DBNull.Value));

            ViewBag.ChuyenBay = cb;
            ViewBag.DanhSachGhe = dtGhe;
            ViewBag.DichVu = DatabaseHelper.ExecuteQuery("SELECT * FROM dbo.fn_DichVuPhuTro() ORDER BY TenDichVu");

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
            string?[] cccdHanhKhach,
            string[] loaiHanhKhach,
            string?[] dichVuPhuTro,
            string? ghiChuThem,
            string? maKhachTaiQuay)
        {
            string? maKH = StaffRole == 3 ? maKhachTaiQuay?.Trim() : CustomerId;
            if (CustomerId == null && StaffRole != 3)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("SelectSeats", new { maChuyenBay }) });
            if (string.IsNullOrWhiteSpace(maKH) || maKH.Length > 10 || !ModelState.IsValid ||
                selectedMaGhe.Length == 0 || selectedMaGhe.Length != tenHanhKhach.Length ||
                selectedMaGhe.Length != cccdHanhKhach.Length || selectedMaGhe.Length != loaiHanhKhach.Length ||
                selectedMaGhe.Length != dichVuPhuTro.Length ||
                tenHanhKhach.Any(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50) ||
                cccdHanhKhach.Any(id => string.IsNullOrWhiteSpace(id) || id.Trim().Length > 30) ||
                dichVuPhuTro.Any(code => code?.Length > 12))
            {
                TempData["Error"] = "Vui lòng nhập mã khách hàng, danh sách ghế và thông tin hành khách hợp lệ.";
                return RedirectToAction("SelectSeats", new { maChuyenBay });
            }

            string ghiChuFull = ghiChuThem?.Trim() ?? "";
            if (ghiChuFull.Length > 255)
            {
                TempData["Error"] = "Ghi chú đơn đặt chỗ tối đa 255 ký tự.";
                return RedirectToAction("SelectSeats", new { maChuyenBay });
            }

            var dtVe = new DataTable();
            dtVe.Columns.Add("MaGhe", typeof(int));
            dtVe.Columns.Add("MaHangGhe", typeof(string));
            dtVe.Columns.Add("TenHanhKhach", typeof(string));
            dtVe.Columns.Add("CCCD_HanhKhach", typeof(string));
            dtVe.Columns.Add("LoaiHanhKhach", typeof(string));
            dtVe.Columns.Add("GiaVe", typeof(decimal));
            dtVe.Columns.Add("DichVuPhuTro", typeof(string));
            for (int i = 0; i < selectedMaGhe.Length; i++)
            {
                dtVe.Rows.Add(selectedMaGhe[i], DBNull.Value, tenHanhKhach[i].Trim(),
                    string.IsNullOrWhiteSpace(cccdHanhKhach[i]) ? DBNull.Value : cccdHanhKhach[i]!.Trim(),
                    loaiHanhKhach[i], DBNull.Value, (object?)dichVuPhuTro[i] ?? DBNull.Value);
            }

            string maDatCho = "PNR" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            try
            {
                DatabaseHelper.ExecuteQuery(
                    "sp_DatVeMayBay_Batch",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaDatCho", maDatCho),
                    new SqlParameter("@MaKH", maKH),
                    new SqlParameter("@MaChuyenBay", maChuyenBay),
                    new SqlParameter("@GhiChu", string.IsNullOrWhiteSpace(ghiChuFull) ? DBNull.Value : ghiChuFull),
                    DatabaseHelper.CreateTableParameter("@DSVe", "ChiTietDatVe_Type", dtVe)
                );

                return StaffRole == 3
                    ? RedirectToAction("Lookup", new { maDatCho })
                    : RedirectToAction("Payment", new { maDatCho });
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
            if (!CanAccessBooking(maDatCho, 1, 2))
                return NotFound();

            DataTable dtDon = DatabaseHelper.ExecuteQuery(
                @"SELECT *, DATEDIFF(SECOND, GETDATE(), HanThanhToan) AS SoGiayGiuCho
                  FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @MaDatCho",
                CommandType.Text,
                new SqlParameter("@MaDatCho", maDatCho));

            if (dtDon.Rows.Count == 0)
                return RedirectToAction("Index", "Home");

            DataTable dtVes = DatabaseHelper.ExecuteQuery(
                "SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @MaDatCho",
                CommandType.Text,
                new SqlParameter("@MaDatCho", maDatCho));

            if (dtDon.Rows[0]["TrangThaiDonHang"].ToString() != "Chờ thanh toán" ||
                dtDon.Rows[0].Field<int>("SoGiayGiuCho") <= 0)
                return RedirectToAction("Lookup", new { maDatCho });

            ViewBag.DonDatCho = dtDon.Rows[0];
            ViewBag.DanhSachVe = dtVes;
            ViewBag.AllowDemoPayments = configuration.GetValue<bool>("AllowDemoPayments");
            ViewBag.Vouchers = DatabaseHelper.ExecuteQuery(@"
                SELECT MaKM, TenKM, PhanTramGiam, DonHangToiThieu FROM KhuyenMai
                WHERE TrangThai = 1 AND SoLuong > 0 AND CAST(GETDATE() AS date) BETWEEN NgayBatDau AND NgayKetThuc
                ORDER BY MaKM");
            return View();
        }

        [HttpPost]
        public IActionResult ApplyVoucher(string maDatCho, string? maKM)
        {
            if (!CanAccessBooking(maDatCho, 1, 2))
                return NotFound();

            try
            {
                string? voucher = maKM?.Trim().ToUpperInvariant();
                if (voucher?.Length > 20)
                    TempData["Error"] = "Mã khuyến mãi tối đa 20 ký tự.";
                else
                {
                    DatabaseHelper.ExecuteNonQuery("sp_ApDungKhuyenMai", CommandType.StoredProcedure,
                        new SqlParameter("@MaDatCho", maDatCho),
                        new SqlParameter("@MaKM", string.IsNullOrEmpty(voucher) ? DBNull.Value : voucher));
                    TempData["Success"] = string.IsNullOrEmpty(voucher) ? "Đã bỏ voucher." : "Đã áp dụng voucher.";
                }
            }
            catch (SqlException ex) when (ex.Number is 51004 or 51009)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Payment", new { maDatCho });
        }

        [HttpPost]
        public IActionResult ConfirmPayment(string maDatCho, string phuongThuc)
        {
            if (!CanAccessBooking(maDatCho, 1, 2))
                return NotFound();

            bool cash = phuongThuc == "Tiền mặt";
            if (cash && StaffRole is not (1 or 2) ||
                !cash && !configuration.GetValue<bool>("AllowDemoPayments"))
            {
                TempData["Error"] = "Phương thức này chưa sẵn sàng cho tài khoản hiện tại.";
                return RedirectToAction("Payment", new { maDatCho });
            }

            string maHD = "HD" + Guid.NewGuid().ToString("N")[..13].ToUpperInvariant();
            string maGiaoDich = (cash ? "QUAY-" : "MO-PHONG-") + Guid.NewGuid().ToString("N");
            string? maNV = HttpContext.Session.GetString("UserType") == "NV"
                ? HttpContext.Session.GetString("UserId")
                : null;

            try
            {
                DataTable payment = DatabaseHelper.ExecuteQuery(
                    "sp_ThanhToanDonVe",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaHD", maHD),
                    new SqlParameter("@MaDatCho", maDatCho),
                    new SqlParameter("@MaNV", (object?)maNV ?? DBNull.Value),
                    new SqlParameter("@PhuongThuc", phuongThuc),
                    new SqlParameter("@MaGiaoDich", maGiaoDich)
                );

                TempData["Success"] = $"Đã ghi nhận thanh toán mã {payment.Rows[0]["MaHoaDon"]} và xuất vé.";
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
            if (!CanAccessBooking(maDatCho, 1, 2))
                return NotFound();

            DataTable dtHD = DatabaseHelper.ExecuteQuery(@"
                SELECT hd.*, pdc.MaKH, kh.TenKH, kh.SDT, kh.Email, kh.DiaChi, kh.CCCD,
                       hd.TongTien + pdc.TienGiam AS TongTienGoc, pdc.TienGiam, pdc.GhiChu
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
                @"SELECT v.* FROM HoaDon h CROSS APPLY OPENJSON(h.ChiTietVe)
                  WITH (MaVe varchar(20), TenHanhKhach nvarchar(50), MaChuyenBay varchar(20),
                        ThanhPhoDi nvarchar(100), ThanhPhoDen nvarchar(100), TenHangGhe nvarchar(100), SoGhe varchar(10),
                        GiaVe decimal(19, 4), PhiPhuTro decimal(19, 4)) v
                  WHERE h.MaDatCho = @PNR",
                CommandType.Text,
                new SqlParameter("@PNR", maDatCho.Trim().ToUpperInvariant()));

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
                string pnr = maDatCho.Trim().ToUpperInvariant();
                if (!CanAccessBooking(pnr, 1, 2, 3, 4))
                    return NotFound();
                DataTable dtDon = DatabaseHelper.ExecuteQuery(
                    "SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @PNR",
                    CommandType.Text,
                    new SqlParameter("@PNR", pnr));

                if (dtDon.Rows.Count > 0)
                {
                    ViewBag.DonDatCho = dtDon.Rows[0];
                    ViewBag.AllowDemoPayments = configuration.GetValue<bool>("AllowDemoPayments");
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
                        SELECT TOP 20 cb.MaChuyenBay, cb.TenHangHangKhong AS TenHang, cb.MaTuyenBay, cb.NgayGioBay, cb.GiaVeCoBan
                        FROM dbo.vw_DanhSachChuyenBayChiTiet cb
                        WHERE cb.TrangThaiChuyenBay = N'Đang mở bán' AND cb.SoGheTrong > 0
                          AND cb.NgayGioBay > DATEADD(HOUR, 2, GETDATE())
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
                  AND cb.TrangThai IN (N'Đang mở bán', N'Hết vé') AND cb.NgayGioBay > DATEADD(HOUR, 2, GETDATE())
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.vw_VeMayBayChiemGhe v
                            WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe
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
            if (!CanAccessBooking(maDatCho, 1, 4) || !BelongsToBooking(maVe, maDatCho))
                return NotFound();
            string? reason = lyDo?.Trim();
            if (reason?.Length > 255)
            {
                TempData["Error"] = "Lý do hủy tối đa 255 ký tự.";
                return RedirectToAction("Lookup", new { maDatCho });
            }

            string? maNV = HttpContext.Session.GetString("UserType") == "NV"
                ? HttpContext.Session.GetString("UserId")
                : null;

            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_XuLyHuyVeVaHoanTien",
                    CommandType.StoredProcedure,
                    new SqlParameter("@MaVe", maVe),
                    new SqlParameter("@LyDo", string.IsNullOrWhiteSpace(reason) ? "Khách yêu cầu hủy vé trực tuyến" : reason),
                    new SqlParameter("@MaNVXuLy", (object?)maNV ?? DBNull.Value)
                );

                DataRow r = dt.Rows[0];
                TempData["Success"] = $"Đã hủy vé {maVe}! Tỷ lệ hoàn theo Luật 72h: {r["PhanTramHoan"]}% — Số tiền hoàn dự kiến: {Convert.ToDecimal(r["SoTienHoanKhachNhan"]):N0} VNĐ.";
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
            if (!CanAccessBooking(maDatCho, 1, 4) || !BelongsToBooking(maVeCu, maDatCho))
                return NotFound();
            if (!configuration.GetValue<bool>("AllowDemoPayments"))
            {
                TempData["Error"] = "Chưa cấu hình xác nhận thu phí đổi vé.";
                return RedirectToAction("Lookup", new { maDatCho });
            }
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
                TempData["Success"] = $"Đổi vé thành công sang mã vé mới {r["MaVeMoi"]}! Chênh lệch giá: {Convert.ToDecimal(r["TienChenhLechGia"]):N0}đ + Phí đổi: {Convert.ToDecimal(r["PhiDichVuDoi"]):N0}đ = Phí thu mô phỏng: {Convert.ToDecimal(r["TongTienThanhToanThem"]):N0} VNĐ.";
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
            if (CustomerId is not { } customer || !CanAccessBooking(maDatCho))
                return NotFound();

            if (soSao is < 1 or > 5 || string.IsNullOrWhiteSpace(binhLuan) || binhLuan.Trim().Length > 500)
            {
                TempData["Error"] = "Vui lòng chọn 1–5 sao và nhập nhận xét tối đa 500 ký tự.";
                return RedirectToAction("Lookup", new { maDatCho });
            }

            try
            {
                int rows = DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO DanhGiaChuyenBay (MaKH, MaChuyenBay, SoSao, NoiDung)
                    SELECT @MaKH, @MaCB, @SoSao, @NoiDung
                    WHERE EXISTS (
                        SELECT 1 FROM VeMayBay v JOIN ChuyenBay cb ON cb.MaChuyenBay = v.MaChuyenBay
                        WHERE v.MaDatCho = @PNR AND v.MaChuyenBay = @MaCB
                          AND v.TrangThai = N'Đã Check-in' AND cb.NgayGioDenDK <= GETDATE()
                          AND cb.TrangThai NOT IN (N'Đã hủy', N'Hoãn chuyến')
                    ) AND NOT EXISTS (
                        SELECT 1 FROM DanhGiaChuyenBay WHERE MaKH = @MaKH AND MaChuyenBay = @MaCB
                    )",
                    CommandType.Text,
                    new SqlParameter("@MaKH", customer),
                    new SqlParameter("@PNR", maDatCho),
                    new SqlParameter("@MaCB", maChuyenBay),
                    new SqlParameter("@SoSao", soSao),
                    new SqlParameter("@NoiDung", binhLuan.Trim()));
                if (rows > 0)
                    TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá trải nghiệm chuyến bay!";
                else
                    TempData["Error"] = "Chỉ đánh giá một lần sau khi hoàn thành chuyến bay đã check-in.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                TempData["Error"] = "Bạn đã đánh giá chuyến bay này.";
            }

            return RedirectToAction("Lookup", new { maDatCho });
        }
    }
}
