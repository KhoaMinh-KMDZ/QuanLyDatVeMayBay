using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Hubs;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class BookingController : Controller
    {
        private readonly string _connStr;
        private readonly IHubContext<SeatHub> _hubContext;

        public BookingController(IConfiguration config, IHubContext<SeatHub> hubContext)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _hubContext = hubContext;
        }

        // =========================================================================
        // 1. CHỌN GHẾ NGỒI (MỘT CHIỀU / KHỨ HỒI + DỊCH VỤ BỔ SUNG)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> SelectSeats(string maChuyenBayDi, string? maChuyenBayVe, int soHanhKhach = 1)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            ViewBag.ChuyenDi = await GetFlightDetailAsync(conn, maChuyenBayDi);
            ViewBag.GheChuyenDi = await GetSeatsForFlightAsync(conn, maChuyenBayDi);

            if (!string.IsNullOrEmpty(maChuyenBayVe))
            {
                ViewBag.ChuyenVe = await GetFlightDetailAsync(conn, maChuyenBayVe);
                ViewBag.GheChuyenVe = await GetSeatsForFlightAsync(conn, maChuyenBayVe);
            }

            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);
            ViewBag.SoHanhKhach = soHanhKhach;
            return View();
        }

        // =========================================================================
        // 2. GIỮ GHẾ 15 PHÚT ĐA LUỒNG + BẮN SIGNALR REALTIME
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> HoldSeatsAndProceed(
            string maChuyenBayDi,
            string? maChuyenBayVe,
            List<int> gheDiIds,
            List<int>? gheVeIds,
            List<string> tenHanhKhachs,
            List<string> cccdHanhKhachs,
            List<string>? selectedServiceIds,
            string? maKM)
        {
            string maKH = HttpContext.Session.GetString("MaKH") ?? "KH00001";
            if (gheDiIds == null || gheDiIds.Count < tenHanhKhachs.Count)
            {
                TempData["Error"] = $"Vui lòng chọn đủ {tenHanhKhachs.Count} ghế cho chuyến đi!";
                return RedirectToAction("SelectSeats", new { maChuyenBayDi, maChuyenBayVe, soHanhKhach = tenHanhKhachs.Count });
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            string pnrDi = "PNR" + Random.Shared.Next(100000, 999999);
            string? pnrVe = !string.IsNullOrEmpty(maChuyenBayVe) ? "PNR" + Random.Shared.Next(100000, 999999) : null;
            string loaiHanhTrinh = pnrVe != null ? "Khứ hồi" : "Một chiều";

            try
            {
                await ExecuteBatchBookingAsync(conn, pnrDi, maKH, maChuyenBayDi, gheDiIds, tenHanhKhachs, cccdHanhKhachs, maKM, loaiHanhTrinh, pnrVe);

                await _hubContext.Clients.Group($"FLIGHT_{maChuyenBayDi}")
                    .SendAsync("SeatStatusChanged", new { maChuyenBay = maChuyenBayDi, seatIds = gheDiIds, isOccupied = true });

                if (pnrVe != null && gheVeIds != null && gheVeIds.Count > 0)
                {
                    await ExecuteBatchBookingAsync(conn, pnrVe, maKH, maChuyenBayVe!, gheVeIds, tenHanhKhachs, cccdHanhKhachs, null, loaiHanhTrinh, pnrDi);

                    await _hubContext.Clients.Group($"FLIGHT_{maChuyenBayVe}")
                        .SendAsync("SeatStatusChanged", new { maChuyenBay = maChuyenBayVe, seatIds = gheVeIds, isOccupied = true });
                }

                if (selectedServiceIds != null && selectedServiceIds.Count > 0)
                {
                    await InsertAddonServicesAsync(conn, pnrDi, selectedServiceIds, "Chờ thanh toán", null);
                }

                return RedirectToAction("Payment", new { maDatCho = pnrDi, maDatChoVe = pnrVe });
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("SelectSeats", new { maChuyenBayDi, maChuyenBayVe, soHanhKhach = tenHanhKhachs.Count });
            }
        }

        // =========================================================================
        // 3. THANH TOÁN ONLINE (VIETQR / MOMO / ZALOPAY / THẺ)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Payment(string maDatCho, string? maDatChoVe)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var donDi = await GetBookingHeaderDictAsync(conn, maDatCho);
            if (donDi == null) return NotFound();

            decimal tongThanhToan = Convert.ToDecimal(donDi["TongTienSauGiam"]);
            if (!string.IsNullOrEmpty(maDatChoVe))
            {
                var donVe = await GetBookingHeaderDictAsync(conn, maDatChoVe);
                if (donVe != null)
                {
                    ViewBag.DonVe = donVe;
                    tongThanhToan += Convert.ToDecimal(donVe["TongTienSauGiam"]);
                }
            }

            using (var cmdDV = new SqlCommand("SELECT ISNULL(SUM(ThanhTien), 0) FROM ChiTietDichVuDatVe WHERE MaDatCho = @Ma AND TrangThaiThanhToan = N'Chờ thanh toán'", conn))
            {
                cmdDV.Parameters.AddWithValue("@Ma", maDatCho);
                tongThanhToan += Convert.ToDecimal(await cmdDV.ExecuteScalarAsync());
            }

            ViewBag.DonDi = donDi;
            ViewBag.TongThanhToan = tongThanhToan;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmOnlinePayment(string maDatCho, string? maDatChoVe, string phuongThuc)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            string maGD = "ONLINE_" + DateTime.Now.ToString("yyyyMMddHHmmss");

            try
            {
                await CallThanhToanSPAsync(conn, "HD" + Random.Shared.Next(10000, 99999), maDatCho, phuongThuc, maGD);

                if (!string.IsNullOrEmpty(maDatChoVe))
                {
                    await CallThanhToanSPAsync(conn, "HD" + Random.Shared.Next(10000, 99999), maDatChoVe, phuongThuc, maGD + "_RET");
                }

                using var cmdDV = new SqlCommand(@"
                    UPDATE ChiTietDichVuDatVe 
                    SET TrangThaiThanhToan = N'Đã thanh toán', PhuongThucTT = @PT, MaGiaoDich = @GD, NgayThanhToan = GETDATE()
                    WHERE MaDatCho = @Ma AND TrangThaiThanhToan = N'Chờ thanh toán';", conn);
                cmdDV.Parameters.AddWithValue("@PT", phuongThuc);
                cmdDV.Parameters.AddWithValue("@GD", maGD);
                cmdDV.Parameters.AddWithValue("@Ma", maDatCho);
                await cmdDV.ExecuteNonQueryAsync();

                TempData["Success"] = $"Thanh toán thành công qua {phuongThuc}! Mã đặt chỗ: {maDatCho}";
                return RedirectToAction("MyBookings");
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("MyBookings");
            }
        }

        // =========================================================================
        // 4. VÉ CỦA TÔI & MUA THÊM DỊCH VỤ SAU KHI ĐẶT VÉ THÀNH CÔNG
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            string maKH = HttpContext.Session.GetString("MaKH") ?? "KH00001";
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            string sql = @"
                SELECT p.MaDatCho, p.NgayDat, p.TongTienSauGiam, p.TrangThai, p.LoaiHanhTrinh, p.MaDatChoLienKet,
                       hd.MaHD, hd.NgayThanhToan, hd.DaXuatHoaDon,
                       CASE WHEN hd.NgayThanhToan IS NOT NULL AND DATEDIFF(MINUTE, hd.NgayThanhToan, GETDATE()) <= 4320 THEN 1 ELSE 0 END AS DuocXuatHoaDon72h,
                       (SELECT ISNULL(SUM(ThanhTien), 0) FROM ChiTietDichVuDatVe d WHERE d.MaDatCho = p.MaDatCho AND d.TrangThaiThanhToan = N'Đã thanh toán') AS TienDichVuThem
                FROM PhieuDatCho p
                LEFT JOIN HoaDon hd ON p.MaDatCho = hd.MaDatCho
                WHERE p.MaKH = @MaKH
                ORDER BY p.NgayDat DESC;";

            var list = new List<Dictionary<string, object>>();
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MaKH", maKH);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < rd.FieldCount; i++) row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                    list.Add(row);
                }
            }

            ViewBag.Bookings = list;
            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BuyAddonAfterBooking(string maDatCho, List<string> serviceIds, string? returnUrl)
        {
            if (serviceIds == null || serviceIds.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 dịch vụ bổ sung.";
                return returnUrl == "Lookup" ? RedirectToAction("Lookup", new { maDatCho }) : RedirectToAction("MyBookings");
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            string maGD = "ADDON_QR_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            await InsertAddonServicesAsync(conn, maDatCho, serviceIds, "Đã thanh toán", maGD);

            TempData["Success"] = $"Đã mua thêm dịch vụ cho mã vé {maDatCho} và thanh toán chuyển khoản VietQR thành công!";
            return returnUrl == "Lookup" ? RedirectToAction("Lookup", new { maDatCho }) : RedirectToAction("MyBookings");
        }

        // =========================================================================
        // 5. HÓA ĐƠN ĐIỆN TỬ VAT (CHỈ CHO PHÉP TRONG VÒNG 72 TIẾNG SAU KHI MUA VÉ)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Invoice(string maDatCho)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            try
            {
                // 1. Gọi SP kiểm tra quy định 72 tiếng
                using (var cmdCheck = new SqlCommand("sp_XuatHoaDonTrong72Gio", conn))
                {
                    cmdCheck.CommandType = CommandType.StoredProcedure;
                    cmdCheck.Parameters.AddWithValue("@MaDatCho", maDatCho);
                    await cmdCheck.ExecuteNonQueryAsync();
                }

                // 2. Lấy DataRow HoaDon đầy đủ thông tin khách hàng & phiếu đặt chỗ cho Invoice.cshtml
                string sqlHD = @"
                    SELECT hd.MaHD, hd.MaDatCho, hd.NgayLap, hd.TongTien, hd.PhuongThuc,
                           ISNULL(hd.MaGiaoDichNganHang, 'TXN_ONLINE') AS MaGiaoDich, hd.TrangThai,
                           pdc.TongTienGoc, pdc.TienGiam, pdc.TongTienSauGiam,
                           kh.TenKH, kh.CCCD, kh.SDT, kh.Email, kh.DiaChi
                    FROM HoaDon hd
                    JOIN PhieuDatCho pdc ON hd.MaDatCho = pdc.MaDatCho
                    JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
                    WHERE hd.MaDatCho = @Ma;";
                var dtHD = new DataTable();
                using (var da = new SqlDataAdapter(sqlHD, conn))
                {
                    da.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                    da.Fill(dtHD);
                }
                if (dtHD.Rows.Count == 0) return NotFound();
                ViewBag.HoaDon = dtHD.Rows[0];

                // 3. Lấy danh sách vé
                var dtVe = new DataTable();
                using (var daVe = new SqlDataAdapter("SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @Ma", conn))
                {
                    daVe.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                    daVe.Fill(dtVe);
                }
                ViewBag.DanhSachVe = dtVe;

                // 4. Lấy danh sách dịch vụ mua thêm (Hành lý, Suất ăn, Chọn ghế)
                var dtDV = new DataTable();
                string sqlDV = @"
                    SELECT dv.TenDV, dv.LoaiDV, ct.SoLuong, ct.ThanhTien, ct.MaGiaoDich
                    FROM ChiTietDichVuDatVe ct
                    JOIN DichVuBoSung dv ON ct.MaDV = dv.MaDV
                    WHERE ct.MaDatCho = @Ma AND ct.TrangThaiThanhToan = N'Đã thanh toán';";
                using (var daDV = new SqlDataAdapter(sqlDV, conn))
                {
                    daDV.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                    daDV.Fill(dtDV);
                }
                ViewBag.DanhSachDichVu = dtDV;

                return View();
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message; // Quá 72 giờ kể từ lúc thanh toán
                return RedirectToAction("MyBookings");
            }
        }

        // =========================================================================
        // 6. TRA CỨU PNR / HỦY VÉ / ĐỔI VÉ / ĐÁNH GIÁ (GIỮ NGUYÊN LOGIC LOOKUP CỦA NHÓM)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Lookup(string? maDatCho)
        {
            if (string.IsNullOrWhiteSpace(maDatCho)) return View();

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var dtDon = new DataTable();
            string sqlDon = @"
                SELECT v.*,
                       CASE WHEN v.NgayThanhToan IS NOT NULL AND DATEDIFF(MINUTE, v.NgayThanhToan, GETDATE()) <= 4320 THEN 1 ELSE 0 END AS DuocXuatHoaDon72h
                FROM vw_LichSuDatVeKhachHang v
                WHERE v.MaDatCho = @Ma;";
            using (var da = new SqlDataAdapter(sqlDon, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho.Trim().ToUpper());
                da.Fill(dtDon);
            }

            if (dtDon.Rows.Count == 0)
            {
                ViewBag.NotFound = $"Không tìm thấy hồ sơ đặt chỗ với mã PNR: {maDatCho}";
                return View();
            }

            ViewBag.DonDatCho = dtDon.Rows[0];

            var dtVe = new DataTable();
            using (var daVe = new SqlDataAdapter("SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @Ma", conn))
            {
                daVe.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho.Trim().ToUpper());
                daVe.Fill(dtVe);
            }
            ViewBag.DanhSachVe = dtVe;

            // Tính trước tiền hoàn theo Luật 72h cho từng vé
            var refundMap = new Dictionary<string, DataRow>();
            foreach (DataRow v in dtVe.Rows)
            {
                string maVe = v["MaVe"].ToString()!;
                var dtRef = new DataTable();
                using var daRef = new SqlDataAdapter("SELECT * FROM dbo.fn_TinhTienHoanTraHuyVe(@MaVe)", conn);
                daRef.SelectCommand.Parameters.AddWithValue("@MaVe", maVe);
                daRef.Fill(dtRef);
                if (dtRef.Rows.Count > 0) refundMap[maVe] = dtRef.Rows[0];
            }
            ViewBag.RefundPreview = refundMap;

            // Lấy danh sách chuyến bay khác để đổi vé & dịch vụ bổ sung
            var dtCB = new DataTable();
            using (var daCB = new SqlDataAdapter("SELECT * FROM vw_DanhSachChuyenBayChiTiet WHERE TrangThaiChuyenBay = N'Đang mở bán' AND SoGheTrong > 0", conn))
            {
                daCB.Fill(dtCB);
            }
            ViewBag.ChuyenBayKhac = dtCB;
            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSeats(string maChuyenBay)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            string sql = @"
                SELECT g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe,
                       dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe) AS GiaThucTe
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB AND g.TrangThai = 1
                  AND g.MaGhe NOT IN (
                      SELECT v.MaGhe FROM VeMayBay v 
                      WHERE v.MaChuyenBay = @MaCB AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                  )
                ORDER BY g.SoGhe;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaCB", maChuyenBay);
            var list = new List<object>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new
                {
                    maGhe = rd.GetInt32(0),
                    soGhe = rd.GetString(1),
                    maHangGhe = rd.GetString(2),
                    tenHangGhe = rd.GetString(3),
                    giaThucTe = rd.GetDecimal(4)
                });
            }
            return Json(list);
        }

        [HttpPost]
        public async Task<IActionResult> CancelTicket(string maDatCho, string maVe, string lyDo)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                using var cmd = new SqlCommand("sp_XuLyHuyVeVaHoanTien", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaVe", maVe);
                cmd.Parameters.AddWithValue("@LyDo", lyDo);
                cmd.Parameters.AddWithValue("@MaNVXuLy", DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                TempData["Success"] = $"Đã hủy vé {maVe} và tự động giải phóng ghế trống!";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleTicket(string maDatCho, string maVeCu, string maChuyenBayMoi, int maGheMoi, string maHangGheMoi)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                using var cmd = new SqlCommand("sp_DoiVeMayBay", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaVeCu", maVeCu);
                cmd.Parameters.AddWithValue("@MaChuyenBayMoi", maChuyenBayMoi);
                cmd.Parameters.AddWithValue("@MaGheMoi", maGheMoi);
                cmd.Parameters.AddWithValue("@MaHangGheMoi", maHangGheMoi);
                cmd.Parameters.AddWithValue("@MaNVXuLy", DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                TempData["Success"] = $"Đổi vé {maVeCu} sang chuyến {maChuyenBayMoi} thành công!";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReview(string maDatCho, string maChuyenBay, int soSao, string binhLuan)
        {
            string maKH = HttpContext.Session.GetString("MaKH") ?? "KH00001";
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("INSERT INTO DanhGiaChuyenBay (MaKH, MaChuyenBay, SoSao, NoiDung) VALUES (@MaKH, @MaCB, @Sao, @ND)", conn);
            cmd.Parameters.AddWithValue("@MaKH", maKH);
            cmd.Parameters.AddWithValue("@MaCB", maChuyenBay);
            cmd.Parameters.AddWithValue("@Sao", soSao);
            cmd.Parameters.AddWithValue("@ND", binhLuan);
            await cmd.ExecuteNonQueryAsync();
            TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá chuyến bay!";
            return RedirectToAction("Lookup", new { maDatCho });
        }

        // ================= HELPER METHODS =================
        private async Task<Dictionary<string, object>> GetFlightDetailAsync(SqlConnection conn, string maCB)
        {
            using var cmd = new SqlCommand("SELECT * FROM vw_DanhSachChuyenBayChiTiet WHERE MaChuyenBay = @Ma", conn);
            cmd.Parameters.AddWithValue("@Ma", maCB);
            var row = new Dictionary<string, object>();
            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
                for (int i = 0; i < rd.FieldCount; i++) row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
            return row;
        }

        private async Task<List<Dictionary<string, object>>> GetSeatsForFlightAsync(SqlConnection conn, string maCB)
        {
            string sql = @"
                SELECT g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe, hg.HeSoGia,
                       CASE WHEN EXISTS (
                           SELECT 1 FROM VeMayBay v 
                           WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe 
                             AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                       ) THEN 1 ELSE 0 END AS DaBiDat
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB
                ORDER BY g.SoGhe ASC;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaCB", maCB);
            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var r = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.GetValue(i);
                list.Add(r);
            }
            return list;
        }

        private async Task<List<Dictionary<string, object>>> GetAddonServicesAsync(SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT * FROM DichVuBoSung WHERE TrangThai = 1", conn);
            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var r = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                list.Add(r);
            }
            return list;
        }

        private async Task ExecuteBatchBookingAsync(
            SqlConnection conn, string pnr, string maKH, string maCB,
            List<int> seatIds, List<string> names, List<string> cccds,
            string? maKM, string loaiHanhTrinh, string? pnrLienKet)
        {
            var dt = new DataTable();
            dt.Columns.Add("MaGhe", typeof(int));
            dt.Columns.Add("MaHangGhe", typeof(string));
            dt.Columns.Add("TenHanhKhach", typeof(string));
            dt.Columns.Add("CCCD_HanhKhach", typeof(string));
            dt.Columns.Add("LoaiHanhKhach", typeof(string));
            dt.Columns.Add("GiaVe", typeof(decimal));

            for (int i = 0; i < seatIds.Count; i++)
            {
                string sqlInfo = @"
                    SELECT g.MaHangGhe, dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe)
                    FROM GheNgoi g CROSS JOIN ChuyenBay cb
                    WHERE g.MaGhe = @MaGhe AND cb.MaChuyenBay = @MaCB;";
                using var cmdInfo = new SqlCommand(sqlInfo, conn);
                cmdInfo.Parameters.AddWithValue("@MaGhe", seatIds[i]);
                cmdInfo.Parameters.AddWithValue("@MaCB", maCB);
                using var rd = await cmdInfo.ExecuteReaderAsync();
                await rd.ReadAsync();
                string hangGhe = rd.GetString(0);
                decimal giaVe = rd.GetDecimal(1);
                await rd.CloseAsync();

                dt.Rows.Add(seatIds[i], hangGhe, names[i], cccds.ElementAtOrDefault(i) ?? "", "Người lớn", giaVe);
            }

            using var cmd = new SqlCommand("sp_DatVeMayBay_Batch", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@MaDatCho", pnr);
            cmd.Parameters.AddWithValue("@MaKH", maKH);
            cmd.Parameters.AddWithValue("@MaChuyenBay", maCB);
            cmd.Parameters.AddWithValue("@MaKM", string.IsNullOrWhiteSpace(maKM) ? DBNull.Value : maKM.Trim().ToUpper());
            cmd.Parameters.AddWithValue("@GhiChu", $"Đặt vé {loaiHanhTrinh}");
            cmd.Parameters.AddWithValue("@LoaiHanhTrinh", loaiHanhTrinh);
            cmd.Parameters.AddWithValue("@MaDatChoLienKet", (object?)pnrLienKet ?? DBNull.Value);

            var tvp = cmd.Parameters.AddWithValue("@DSVe", dt);
            tvp.SqlDbType = SqlDbType.Structured;
            tvp.TypeName = "ChiTietDatVe_Type";

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsertAddonServicesAsync(SqlConnection conn, string maDatCho, List<string> serviceIds, string trangThai, string? maGD)
        {
            foreach (var maDV in serviceIds)
            {
                string sql = @"
                    INSERT INTO ChiTietDichVuDatVe (MaDatCho, MaDV, SoLuong, DonGia, ThanhTien, TrangThaiThanhToan, MaGiaoDich, NgayThanhToan)
                    SELECT @MaDatCho, MaDV, 1, DonGia, DonGia, @TT, @GD, CASE WHEN @TT = N'Đã thanh toán' THEN GETDATE() ELSE NULL END
                    FROM DichVuBoSung WHERE MaDV = @MaDV;";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MaDatCho", maDatCho);
                cmd.Parameters.AddWithValue("@MaDV", maDV);
                cmd.Parameters.AddWithValue("@TT", trangThai);
                cmd.Parameters.AddWithValue("@GD", (object?)maGD ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<Dictionary<string, object>?> GetBookingHeaderDictAsync(SqlConnection conn, string maDatCho)
        {
            using var cmd = new SqlCommand("SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @Ma", conn);
            cmd.Parameters.AddWithValue("@Ma", maDatCho);
            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;
            var r = new Dictionary<string, object>();
            for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
            return r;
        }

        private async Task CallThanhToanSPAsync(SqlConnection conn, string maHD, string maDatCho, string phuongThuc, string maGD)
        {
            using var cmd = new SqlCommand("sp_ThanhToanDonVe", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@MaHD", maHD);
            cmd.Parameters.AddWithValue("@MaDatCho", maDatCho);
            cmd.Parameters.AddWithValue("@MaNV", DBNull.Value);
            cmd.Parameters.AddWithValue("@PhuongThuc", phuongThuc);
            cmd.Parameters.AddWithValue("@MaGiaoDich", maGD);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}