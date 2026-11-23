using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Models;

namespace QL_DatVeMayBay.Controllers;

public partial class AdminController
{
    private bool IsAdmin => HttpContext.Session.GetString("UserType") == "NV"
        && HttpContext.Session.GetInt32("RoleID") == 1;

    [HttpGet]
    public IActionResult Staff(string? id)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (string.IsNullOrEmpty(id))
            return StaffView(new StaffForm());

        var staff = DatabaseHelper.ExecuteQuery(@"
            SELECT MaNV, TenNV, ChucVu, SDT, Email, Luong, TaiKhoan, RoleID
            FROM NhanVien WHERE MaNV = @MaNV", CommandType.Text, new SqlParameter("@MaNV", id));
        if (staff.Rows.Count == 0)
            return NotFound();

        DataRow row = staff.Rows[0];
        return StaffView(new StaffForm
        {
            MaNV = row.Field<string>("MaNV"),
            TenNV = row.Field<string>("TenNV")!,
            ChucVu = row.Field<string>("ChucVu"),
            SDT = row.Field<string>("SDT") ?? "",
            Email = row.Field<string>("Email") ?? "",
            Luong = row.Field<decimal?>("Luong") ?? 0,
            TaiKhoan = row.Field<string>("TaiKhoan")!,
            RoleID = row.Field<int>("RoleID")
        });
    }

    [HttpPost]
    public IActionResult Staff(StaffForm form)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        bool create = string.IsNullOrEmpty(form.MaNV);
        if (create && string.IsNullOrWhiteSpace(form.MatKhau))
            ModelState.AddModelError(nameof(form.MatKhau), "Nhập mật khẩu từ 8 ký tự cho nhân viên mới.");
        if (string.Equals(form.MaNV, HttpContext.Session.GetString("UserId"), StringComparison.OrdinalIgnoreCase) && form.RoleID != 1)
            ModelState.AddModelError(nameof(form.RoleID), "Tài khoản đang thao tác phải giữ vai trò quản trị.");
        if (!ModelState.IsValid)
            return StaffView(form);

        string id = form.MaNV ?? ("NV" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
        string? password = string.IsNullOrWhiteSpace(form.MatKhau)
            ? null : PasswordStorage.Hash(id, form.MatKhau);
        string sql = create
            ? @"INSERT INTO NhanVien (MaNV, TenNV, ChucVu, SDT, Email, Luong, TaiKhoan, MatKhau, RoleID)
                VALUES (@Id, @Name, @Position, @Phone, @Email, @Salary, @Username, @Password, @Role)"
            : @"UPDATE NhanVien SET TenNV = @Name, ChucVu = @Position, SDT = @Phone, Email = @Email,
                    Luong = @Salary, TaiKhoan = @Username, RoleID = @Role, MatKhau = COALESCE(@Password, MatKhau)
                WHERE MaNV = @Id
                  AND (@Role = 1 OR MaNV <> @Actor)";

        try
        {
            int rows = DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text,
                new SqlParameter("@Id", id), new SqlParameter("@Name", form.TenNV.Trim()),
                new SqlParameter("@Position", (object?)form.ChucVu?.Trim() ?? DBNull.Value),
                new SqlParameter("@Phone", form.SDT), new SqlParameter("@Email", form.Email.Trim()),
                new SqlParameter("@Salary", form.Luong), new SqlParameter("@Username", form.TaiKhoan),
                new SqlParameter("@Password", (object?)password ?? DBNull.Value),
                new SqlParameter("@Role", form.RoleID),
                new SqlParameter("@Actor", HttpContext.Session.GetString("UserId")));
            if (rows != 1)
                return NotFound();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 or 547)
        {
            ModelState.AddModelError("", ex.Number == 547
                ? "Vai trò hoặc thông tin nhân viên không hợp lệ."
                : "Tài khoản, số điện thoại hoặc email đã được sử dụng.");
            return StaffView(form);
        }

        TempData["Success"] = create ? $"Đã thêm nhân viên {id}." : "Đã cập nhật hồ sơ nhân viên.";
        return RedirectToAction(nameof(Dashboard), null, null, "tab-staff");
    }

    private IActionResult StaffView(StaffForm form)
    {
        ViewBag.Roles = DatabaseHelper.ExecuteQuery("SELECT RoleID, TenRole FROM VaiTro ORDER BY RoleID");
        return View("Staff", form);
    }

    [HttpGet]
    public IActionResult Promotion(string id)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(id) || id.Length > 20)
            return NotFound();

        var vouchers = DatabaseHelper.ExecuteQuery("SELECT * FROM KhuyenMai WHERE MaKM = @Code",
            CommandType.Text, new SqlParameter("@Code", id));
        if (vouchers.Rows.Count == 0)
            return NotFound();

        DataRow row = vouchers.Rows[0];
        return View(new PromoForm
        {
            MaKM = row.Field<string>("MaKM")!,
            TenKM = row.Field<string>("TenKM")!,
            PhanTramGiam = row.Field<decimal?>("PhanTramGiam") ?? 0,
            GiamToiDa = row.Field<decimal?>("GiamToiDa") ?? 0,
            DonHangToiThieu = row.Field<decimal?>("DonHangToiThieu") ?? 0,
            SoLuong = row.Field<int?>("SoLuong") ?? 0,
            OriginalUses = row.Field<int?>("SoLuong") ?? 0,
            NgayBatDau = row.Field<DateTime>("NgayBatDau"),
            NgayKetThuc = row.Field<DateTime>("NgayKetThuc")
        });
    }

    [HttpPost]
    public IActionResult Promotion(PromoForm form)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);
        if (form.NgayKetThuc < form.NgayBatDau)
            ModelState.AddModelError(nameof(form.NgayKetThuc), "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (!ModelState.IsValid)
            return View(form);

        int changed = DatabaseHelper.ExecuteNonQuery(@"
            UPDATE KhuyenMai SET TenKM = @Name, PhanTramGiam = @Rate, GiamToiDa = @Max,
                DonHangToiThieu = @Min, SoLuong = @Uses, NgayBatDau = @Start, NgayKetThuc = @End
            WHERE MaKM = @Code AND ISNULL(SoLuong, 0) = @OriginalUses", CommandType.Text,
            new SqlParameter("@Name", form.TenKM.Trim()), new SqlParameter("@Rate", form.PhanTramGiam),
            new SqlParameter("@Max", form.GiamToiDa), new SqlParameter("@Min", form.DonHangToiThieu),
            new SqlParameter("@Uses", form.SoLuong), new SqlParameter("@Start", SqlDbType.Date) { Value = form.NgayBatDau! },
            new SqlParameter("@End", SqlDbType.Date) { Value = form.NgayKetThuc! }, new SqlParameter("@Code", form.MaKM),
            new SqlParameter("@OriginalUses", (object?)form.OriginalUses ?? DBNull.Value));
        if (changed != 1)
        {
            TempData["Error"] = "Số lượt voucher đã thay đổi. Kiểm tra số lượt mới trước khi lưu.";
            return RedirectToAction(nameof(Promotion), new { id = form.MaKM });
        }

        TempData["Success"] = "Đã cập nhật khuyến mãi cho các lần áp dụng tiếp theo.";
        return RedirectToAction(nameof(Dashboard), null, null, "tab-promos");
    }
}
