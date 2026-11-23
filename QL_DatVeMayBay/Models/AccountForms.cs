using System.ComponentModel.DataAnnotations;

namespace QL_DatVeMayBay.Models;

public sealed class LoginForm
{
    [Required(ErrorMessage = "Vui lòng nhập tài khoản.")]
    [StringLength(100, ErrorMessage = "Tài khoản tối đa 100 ký tự.")]
    public string TaiKhoan { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string MatKhau { get; set; } = "";

    public string LoginType { get; set; } = "KH";
    public string? ReturnUrl { get; set; }
}

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(50, ErrorMessage = "Họ và tên tối đa 50 ký tự.")]
    public string TenKH { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    public DateTime? NgaySinh { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    public string Sdt { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có từ 6 đến 128 ký tự.")]
    public string MatKhau { get; set; } = "";

    [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự.")]
    public string? DiaChi { get; set; }

    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "CCCD phải gồm 12 chữ số.")]
    public string? Cccd { get; set; }

    [StringLength(30, ErrorMessage = "Số hộ chiếu tối đa 30 ký tự.")]
    public string? HoChieu { get; set; }

    [StringLength(50, ErrorMessage = "Quốc tịch tối đa 50 ký tự.")]
    public string? QuocTich { get; set; }

    public string GioiTinh { get; set; } = "Nam";
    public string LoaiKhachHang { get; set; } = "Việt Nam";
    public string? ReturnUrl { get; set; }
}
