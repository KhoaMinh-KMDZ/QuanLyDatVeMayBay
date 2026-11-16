using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string loginType, string taiKhoan, string matKhau)
        {
            if (string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu!";
                return View();
            }

            if (loginType == "NV")
            {
                // Đăng nhập Nhân viên (Ban Giám Đốc, Thu Ngân, Bán Vé, CSKH)
                DataTable dt = DatabaseHelper.ExecuteQuery(@"
                    SELECT nv.MaNV, nv.TenNV, nv.TaiKhoan, nv.MatKhau, nv.RoleID, vt.TenRole
                    FROM NhanVien nv
                    JOIN VaiTro vt ON nv.RoleID = vt.RoleID
                    WHERE nv.TaiKhoan = @TaiKhoan AND nv.TrangThai = 1",
                    CommandType.Text,
                    new SqlParameter("@TaiKhoan", taiKhoan.Trim()));

                if (dt.Rows.Count > 0 && dt.Rows[0]["MatKhau"].ToString() == matKhau)
                {
                    DataRow r = dt.Rows[0];
                    HttpContext.Session.SetString("UserType", "NV");
                    HttpContext.Session.SetString("UserId", r["MaNV"].ToString()!);
                    HttpContext.Session.SetString("UserName", $"{r["TenNV"]} ({r["TenRole"]})");
                    HttpContext.Session.SetInt32("RoleID", Convert.ToInt32(r["RoleID"]));
                    HttpContext.Session.SetString("RoleName", r["TenRole"].ToString()!);

                    return RedirectToAction("Dashboard", "Admin");
                }
            }
            else
            {
                // Đăng nhập Khách hàng qua SP sp_DangNhapKhachHang
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_DangNhapKhachHang",
                    CommandType.StoredProcedure,
                    new SqlParameter("@TaiKhoan", taiKhoan.Trim()));

                if (dt.Rows.Count > 0 && dt.Rows[0]["MatKhau"].ToString() == matKhau)
                {
                    DataRow r = dt.Rows[0];
                    HttpContext.Session.SetString("UserType", "KH");
                    HttpContext.Session.SetString("UserId", r["MaKH"].ToString()!);
                    HttpContext.Session.SetString("UserName", r["TenKH"].ToString()!);
                    HttpContext.Session.SetString("UserEmail", r["Email"]?.ToString() ?? "");
                    HttpContext.Session.SetString("UserCCCD", r["CCCD"]?.ToString() ?? r["HoChieu"]?.ToString() ?? "");

                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Tài khoản hoặc mật khẩu không chính xác (hoặc tài khoản đã bị khóa)!";
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string tenKH, string gioiTinh, DateTime ngaySinh, string sdt, string email, string matKhau, string diaChi, string loaiKhachHang, string? cccd, string? hoChieu, string quocTich)
        {
            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_DangKyKhachHang",
                    CommandType.StoredProcedure,
                    new SqlParameter("@TenKH", tenKH),
                    new SqlParameter("@GioiTinh", gioiTinh),
                    new SqlParameter("@NgaySinh", ngaySinh),
                    new SqlParameter("@SDT", sdt),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@MatKhau", matKhau),
                    new SqlParameter("@DiaChi", string.IsNullOrWhiteSpace(diaChi) ? DBNull.Value : diaChi),
                    new SqlParameter("@LoaiKhachHang", loaiKhachHang),
                    new SqlParameter("@CCCD", string.IsNullOrWhiteSpace(cccd) ? DBNull.Value : cccd),
                    new SqlParameter("@HoChieu", string.IsNullOrWhiteSpace(hoChieu) ? DBNull.Value : hoChieu),
                    new SqlParameter("@QuocTich", string.IsNullOrWhiteSpace(quocTich) ? "Việt Nam" : quocTich)
                );

                TempData["Success"] = $"Đăng ký thành công! Mã khách hàng của bạn là {dt.Rows[0]["MaKH"]}. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            catch (SqlException ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}