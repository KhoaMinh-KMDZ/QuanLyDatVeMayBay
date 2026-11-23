using System.ComponentModel.DataAnnotations;

namespace QL_DatVeMayBay.Models;

public sealed class StaffForm
{
    [StringLength(10)] public string? MaNV { get; set; }
    [Required, StringLength(50)] public string TenNV { get; set; } = "";
    [StringLength(50)] public string? ChucVu { get; set; }
    [Required, RegularExpression(@"^\+?[0-9]{8,14}$")] public string SDT { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Range(typeof(decimal), "0", "999999999999")] public decimal Luong { get; set; }
    [Required, StringLength(50), RegularExpression(@"^[a-zA-Z0-9_.-]+$")]
    public string TaiKhoan { get; set; } = "";
    [StringLength(128, MinimumLength = 8)] public string? MatKhau { get; set; }
    [Range(1, int.MaxValue)] public int RoleID { get; set; }
}

public sealed class PromoForm
{
    [Required, StringLength(20)] public string MaKM { get; set; } = "";
    [Required, StringLength(100)] public string TenKM { get; set; } = "";
    [Range(typeof(decimal), "0", "100")] public decimal PhanTramGiam { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal GiamToiDa { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal DonHangToiThieu { get; set; }
    [Range(0, int.MaxValue)] public int SoLuong { get; set; }
    public int? OriginalUses { get; set; }
    [Required, DataType(DataType.Date)] public DateTime? NgayBatDau { get; set; }
    [Required, DataType(DataType.Date)] public DateTime? NgayKetThuc { get; set; }
}

public sealed class FlightForm
{
    public bool Update { get; set; }
    [Required, StringLength(20), RegularExpression(@"^[a-zA-Z0-9-]+$")]
    public string MaChuyenBay { get; set; } = "";
    [Required, StringLength(15)] public string MaTuyenBay { get; set; } = "";
    [Required, StringLength(15)] public string MaMayBay { get; set; } = "";
    [Required] public DateTime? NgayGioBay { get; set; }
    [Required] public DateTime? NgayGioDenDK { get; set; }
    [Range(typeof(decimal), "1000", "999999999")] public decimal GiaVeCoBan { get; set; }
    [Required] public string TrangThai { get; set; } = "Đang mở bán";
    public List<FlightStopForm> Stops { get; set; } = [];
}

public sealed class FlightStopForm
{
    [StringLength(10)] public string? MaSanBayTG { get; set; }
    [Range(45, 120)] public int? ThoiGianDung { get; set; }
    [StringLength(200)] public string? GhiChu { get; set; }
}
