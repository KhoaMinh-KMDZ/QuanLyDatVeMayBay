using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace QL_DatVeMayBay.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connStr;

        public HomeController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IActionResult> Index(
            string? maSanBayDi,
            string? maSanBayDen,
            DateTime? ngayDi,
            DateTime? ngayVe,
            string loaiHanhTrinh = "MOT_CHIEU",
            string cheDoBay = "ALL", // ALL | THANG | QUA_CANH
            int soHanhKhach = 1)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // 1. Lấy danh sách Sân bay cho Dropdown
            var sanBays = new List<dynamic>();
            using (var cmdSB = new SqlCommand("SELECT MaSanBay, TenSanBay, ThanhPho, HinhAnhUrl FROM SanBay WHERE TrangThai = 1", conn))
            using (var rd = await cmdSB.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    sanBays.Add(new
                    {
                        MaSanBay = rd.GetString(0),
                        TenSanBay = rd.GetString(1),
                        ThanhPho = rd.GetString(2),
                        HinhAnhUrl = rd.IsDBNull(3) ? "" : rd.GetString(3)
                    });
                }
            }

            bool daLoc = !string.IsNullOrEmpty(maSanBayDi) || !string.IsNullOrEmpty(maSanBayDen) || ngayDi.HasValue || cheDoBay != "ALL";

            ViewBag.SanBays = sanBays;
            ViewBag.DaLoc = daLoc;
            ViewBag.LoaiHanhTrinh = loaiHanhTrinh;
            ViewBag.CheDoBay = cheDoBay;
            ViewBag.SoHanhKhach = soHanhKhach;
            ViewBag.MaSanBayDi = maSanBayDi ?? "SGN";
            ViewBag.MaSanBayDen = maSanBayDen ?? "HAN";
            ViewBag.NgayDi = (ngayDi ?? new DateTime(2027, 1, 1)).ToString("yyyy-MM-dd");
            ViewBag.NgayVe = (ngayVe ?? new DateTime(2027, 1, 3)).ToString("yyyy-MM-dd");

            // 2. Truy vấn trực tiếp vào danh sách Lịch chuyến bay đang mở bán (nếu chưa lọc thì lấy toàn bộ)
            if (daLoc)
            {
                ViewBag.LichChuyenBay = await QueryFlightsAsync(conn, maSanBayDi, maSanBayDen, ngayDi, cheDoBay);

                if (loaiHanhTrinh == "KHU_HOI")
                {
                    ViewBag.LichChuyenBayVe = await QueryFlightsAsync(conn, maSanBayDen, maSanBayDi, ngayVe, cheDoBay);
                }
            }
            else
            {
                ViewBag.LichChuyenBay = await QueryFlightsAsync(conn, null, null, null, "ALL");
            }

            return View();
        }

        private async Task<List<Dictionary<string, object>>> QueryFlightsAsync(
            SqlConnection conn, string? di, string? den, DateTime? ngay, string cheDoBay)
        {
            string sql = @"
                SELECT * FROM vw_DanhSachChuyenBayChiTiet
                WHERE TrangThaiChuyenBay = N'Đang mở bán' AND SoGheTrong > 0
                  AND (@Di IS NULL OR @Di = '' OR MaSanBayDi = @Di)
                  AND (@Den IS NULL OR @Den = '' OR MaSanBayDen = @Den)
                  AND (@Ngay IS NULL OR CAST(NgayGioBay AS date) = @Ngay)
                  AND (@CheDo = 'ALL' OR (@CheDo = 'THANG' AND LaChuyenQuaCanh = 0) OR (@CheDo = 'QUA_CANH' AND LaChuyenQuaCanh = 1))
                ORDER BY NgayGioBay ASC, GiaVeCoBan ASC;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Di", (object?)di ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Den", (object?)den ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ngay", ngay.HasValue ? ngay.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@CheDo", cheDoBay);

            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++)
                {
                    row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                }
                list.Add(row);
            }
            return list;
        }
    }
}