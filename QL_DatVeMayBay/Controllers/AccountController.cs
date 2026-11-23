using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Models;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class AccountController : Controller
    {
        private static void UpgradePassword(string userId, string stored, string password, bool staff)
        {
            if (PasswordStorage.IsHashed(stored))
                return;

            string query = staff
                ? "UPDATE NhanVien SET MatKhau = @Hash WHERE MaNV = @Id AND MatKhau = @Old"
                : "UPDATE KhachHang SET MatKhau = @Hash WHERE MaKH = @Id AND MatKhau = @Old";
            DatabaseHelper.ExecuteNonQuery(query, CommandType.Text,
                new SqlParameter("@Hash", PasswordStorage.Hash(userId, password)),
                new SqlParameter("@Id", userId), new SqlParameter("@Old", stored));
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl) =>
            View(new LoginForm { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

        [HttpPost]
        public IActionResult Login(LoginForm form)
        {
            form.ReturnUrl = Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : null;
            if (form.LoginType is not ("NV" or "KH"))
                ModelState.AddModelError(nameof(form.LoginType), "Chọn đối tượng đăng nhập phù hợp.");
            if (!ModelState.IsValid)
                return View(form);

            string loginType = form.LoginType;
            string taiKhoan = form.TaiKhoan.Trim();
            string matKhau = form.MatKhau;
            string? returnUrl = form.ReturnUrl;
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

                if (dt.Rows.Count > 0 && PasswordStorage.Verify(dt.Rows[0]["MaNV"].ToString()!,
                    dt.Rows[0]["MatKhau"].ToString()!, matKhau))
                {
                    DataRow r = dt.Rows[0];
                    UpgradePassword(r["MaNV"].ToString()!, r["MatKhau"].ToString()!, matKhau, staff: true);
                    HttpContext.Session.Clear();
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
                    "sp_DangNhapKhachHang", CommandType.StoredProcedure,
                    new SqlParameter("@TaiKhoan", taiKhoan));

                if (dt.Rows.Count > 0 && PasswordStorage.Verify(dt.Rows[0]["MaKH"].ToString()!,
                    dt.Rows[0]["MatKhau"].ToString()!, matKhau))
                {
                    DataRow r = dt.Rows[0];
                    UpgradePassword(r["MaKH"].ToString()!, r["MatKhau"].ToString()!, matKhau, staff: false);
                    HttpContext.Session.Clear();
                    HttpContext.Session.SetString("UserType", "KH");
                    HttpContext.Session.SetString("UserId", r["MaKH"].ToString()!);
                    HttpContext.Session.SetString("UserName", r["TenKH"].ToString()!);
                    HttpContext.Session.SetString("UserEmail", r["Email"]?.ToString() ?? "");
                    HttpContext.Session.SetString("UserCCCD", r.Field<string>("CCCD") ?? r.Field<string>("HoChieu") ?? "");

                    if (Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);
                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.");
            return View(form);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl) =>
            View(new RegisterForm { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

        [HttpPost]
        public IActionResult Register(RegisterForm form)
        {
            form.ReturnUrl = Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : null;
            if (form.NgaySinh?.Date > DateTime.Today)
                ModelState.AddModelError(nameof(form.NgaySinh), "Ngày sinh không được ở tương lai.");
            if (form.GioiTinh is not ("Nam" or "Nữ" or "Khác"))
                ModelState.AddModelError(nameof(form.GioiTinh), "Chọn giới tính phù hợp.");
            if (form.LoaiKhachHang is not ("Việt Nam" or "Nước ngoài" or "Doanh nghiệp"))
                ModelState.AddModelError(nameof(form.LoaiKhachHang), "Chọn đối tượng khách phù hợp.");

            string phone = NormalizePhone(form.Sdt ?? "");
            string digits = phone.StartsWith('+') ? phone[1..] : phone;
            if (digits.Length < 8 || phone.Length > 15 || digits.Any(digit => !char.IsAsciiDigit(digit)))
                ModelState.AddModelError(nameof(form.Sdt), "Số điện thoại không hợp lệ.");
            if (!ModelState.IsValid)
                return View(form);

            try
            {
                string email = form.Email.Trim();
                DataTable customers = DatabaseHelper.ExecuteQuery(
                    "sp_DangKyKhachHang", CommandType.StoredProcedure,
                    new SqlParameter("@TenKH", form.TenKH.Trim()),
                    new SqlParameter("@GioiTinh", form.GioiTinh),
                    new SqlParameter("@NgaySinh", SqlDbType.Date) { Value = form.NgaySinh!.Value.Date },
                    new SqlParameter("@SDT", phone),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@MatKhau", PasswordStorage.Hash(email, form.MatKhau)),
                    new SqlParameter("@DiaChi", (object?)form.DiaChi?.Trim() ?? DBNull.Value),
                    new SqlParameter("@LoaiKhachHang", form.LoaiKhachHang),
                    new SqlParameter("@CCCD", (object?)form.Cccd?.Trim() ?? DBNull.Value),
                    new SqlParameter("@HoChieu", (object?)form.HoChieu?.Trim() ?? DBNull.Value),
                    new SqlParameter("@QuocTich", string.IsNullOrWhiteSpace(form.QuocTich) ? "Việt Nam" : form.QuocTich.Trim()));

                TempData["Success"] = $"Đăng ký thành công. Mã khách hàng là {customers.Rows[0]["MaKH"]}.";
                return RedirectToAction(nameof(Login), new { returnUrl = form.ReturnUrl });
            }
            catch (SqlException ex) when (ex.Number is 50000 or 2601 or 2627)
            {
                ModelState.AddModelError("", "Email, số điện thoại hoặc giấy tờ đã được đăng ký.");
                return View(form);
            }
        }

        private static string NormalizePhone(string phone) =>
            string.Concat(phone.Where(character => !char.IsWhiteSpace(character) && character is not ('-' or '.' or '(' or ')')));

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}