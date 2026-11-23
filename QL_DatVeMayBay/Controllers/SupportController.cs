using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;

namespace QL_DatVeMayBay.Controllers;

public class SupportController : Controller
{
    private string? CustomerId => HttpContext.Session.GetString("UserType") == "KH"
        ? HttpContext.Session.GetString("UserId") : null;

    private bool CanResolve => HttpContext.Session.GetString("UserType") == "NV"
        && HttpContext.Session.GetInt32("RoleID") is 1 or 4;

    [HttpGet]
    public IActionResult Index()
    {
        if (CanResolve)
        {
            var queue = DatabaseHelper.ExecuteQuery(@"
                SELECT kn.*, kh.TenKH FROM KhieuNai kn JOIN KhachHang kh ON kh.MaKH = kn.MaKH
                ORDER BY CASE WHEN kn.TrangThai = N'Chờ tiếp nhận' THEN 0 ELSE 1 END, kn.NgayGui DESC");
            ViewBag.IsStaff = true;
            return View(queue);
        }

        if (CustomerId is not { } customer)
            return RedirectToAction("Login", "Account");
        return View(DatabaseHelper.ExecuteQuery(@"
            SELECT * FROM KhieuNai WHERE MaKH = @MaKH ORDER BY NgayGui DESC",
            CommandType.Text, new SqlParameter("@MaKH", customer)));
    }

    [HttpPost]
    public IActionResult Send(string tieuDe, string phanLoai, string noiDung, string? maDatCho, string? maVe)
    {
        if (CustomerId is not { } customer)
            return StatusCode(StatusCodes.Status403Forbidden);

        maDatCho = string.IsNullOrWhiteSpace(maDatCho) ? null : maDatCho.Trim();
        maVe = string.IsNullOrWhiteSpace(maVe) ? null : maVe.Trim();
        if (!ModelState.IsValid || Invalid(tieuDe, 120) || Invalid(noiDung, 1000)
            || phanLoai is not ("Chuyến bay" or "Thanh toán" or "Dịch vụ")
            || maDatCho?.Length > 15 || maVe?.Length > 20 || maVe != null && maDatCho == null)
        {
            TempData["Error"] = "Kiểm tra tiêu đề, nội dung và mã vé. Khi nhập mã vé, cần nhập mã PNR tương ứng.";
            return RedirectToAction(nameof(Index));
        }

        object? id = DatabaseHelper.ExecuteScalar(@"
            INSERT INTO KhieuNai (MaKH, MaDatCho, MaVe, TieuDe, PhanLoai, NoiDung)
            OUTPUT INSERTED.MaKhieuNai
            SELECT @MaKH, @PNR, @MaVe, @TieuDe, @PhanLoai, @NoiDung
            WHERE (@PNR IS NULL OR EXISTS (SELECT 1 FROM PhieuDatCho WHERE MaDatCho = @PNR AND MaKH = @MaKH))
                AND (@MaVe IS NULL OR EXISTS (SELECT 1 FROM VeMayBay WHERE MaDatCho = @PNR AND MaVe = @MaVe))",
            CommandType.Text, new SqlParameter("@MaKH", customer), new SqlParameter("@PNR", (object?)maDatCho ?? DBNull.Value),
            new SqlParameter("@MaVe", (object?)maVe ?? DBNull.Value), new SqlParameter("@TieuDe", tieuDe.Trim()),
            new SqlParameter("@PhanLoai", phanLoai), new SqlParameter("@NoiDung", noiDung.Trim()));
        if (id == null)
            TempData["Error"] = "Mã đặt chỗ hoặc mã vé không thuộc tài khoản hiện tại.";
        else
            TempData["Success"] = $"Đã gửi khiếu nại. Mã theo dõi #{id}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Resolve(int maKhieuNai, string ketQua, string phanHoi, string? maKM)
    {
        if (!CanResolve)
            return StatusCode(StatusCodes.Status403Forbidden);

        string? voucher = ketQua == "Đã giải quyết" && !string.IsNullOrWhiteSpace(maKM) ? maKM.Trim().ToUpperInvariant() : null;
        if (!ModelState.IsValid || Invalid(phanHoi, 1000) || voucher?.Length > 20
            || ketQua is not ("Đã giải quyết" or "Từ chối"))
        {
            TempData["Error"] = "Chọn kết quả và nhập nội dung phản hồi tối đa 1.000 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        int rows = DatabaseHelper.ExecuteNonQuery(@"
            UPDATE KhieuNai
            SET TrangThai = @KetQua, PhanHoi = @PhanHoi, NgayXuLy = SYSDATETIME(), MaNVXuLy = @MaNV, MaKM = @MaKM
            WHERE MaKhieuNai = @Ma AND TrangThai = N'Chờ tiếp nhận'
                AND (@MaKM IS NULL OR EXISTS (SELECT 1 FROM KhuyenMai WHERE MaKM = @MaKM AND TrangThai = 1
                    AND SoLuong > 0 AND CONVERT(date, GETDATE()) BETWEEN NgayBatDau AND NgayKetThuc))",
            CommandType.Text, new SqlParameter("@Ma", maKhieuNai), new SqlParameter("@KetQua", ketQua),
            new SqlParameter("@PhanHoi", phanHoi.Trim()), new SqlParameter("@MaNV", HttpContext.Session.GetString("UserId")),
            new SqlParameter("@MaKM", (object?)voucher ?? DBNull.Value));
        if (rows == 0)
            TempData["Error"] = "Hồ sơ đã được xử lý hoặc mã khuyến mãi không còn hợp lệ.";
        else
            TempData["Success"] = $"Đã lưu phản hồi cho hồ sơ #{maKhieuNai}.";
        return RedirectToAction(nameof(Index));
    }

    private static bool Invalid(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength;
}
