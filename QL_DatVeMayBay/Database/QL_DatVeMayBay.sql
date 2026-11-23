-- Khởi tạo cơ sở dữ liệu mới. Nếu tên đã tồn tại, script dừng để bảo toàn dữ liệu.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE master;
GO
IF DB_ID(N'QL_DatVeMayBay') IS NOT NULL
BEGIN
    RAISERROR(N'Cơ sở dữ liệu QL_DatVeMayBay đã tồn tại. Hãy chọn tên khác để chạy bản mẫu.', 16, 1);
    SET NOEXEC ON;
END;
GO
CREATE DATABASE QL_DatVeMayBay;
GO
USE QL_DatVeMayBay;
GO

CREATE TABLE HangHangKhong
(
    MaHang      varchar(10)   NOT NULL,
    TenHang     nvarchar(100) NOT NULL UNIQUE,
    QuocGia     nvarchar(50)  DEFAULT N'Việt Nam',
    Logo        varchar(255)  NULL,
    TrangThai   bit           DEFAULT 1, -- 1: Đang hoạt động, 0: Tạm ngưng
    CONSTRAINT PK_HangHangKhong PRIMARY KEY (MaHang)
);
GO

-- 2. Bảng Sân Bay
CREATE TABLE SanBay
(
    MaSanBay    varchar(10)   NOT NULL, -- Mã IATA (VD: SGN, HAN, DAD)
    TenSanBay   nvarchar(100) NOT NULL,
    ThanhPho    nvarchar(100) NOT NULL,
    QuocGia     nvarchar(50)  NOT NULL DEFAULT N'Việt Nam',
    TrangThai   bit           DEFAULT 1,
    CONSTRAINT PK_SanBay PRIMARY KEY (MaSanBay)
);
GO

-- 3. Bảng Tuyến Bay (Nối giữa Sân bay đi và Sân bay đến)
CREATE TABLE TuyenBay
(
    MaTuyenBay   varchar(15) NOT NULL,
    MaSanBayDi   varchar(10) NOT NULL,
    MaSanBayDen  varchar(10) NOT NULL,
    KhoangCachKm decimal(8,2) NULL CHECK (KhoangCachKm > 0),
    CONSTRAINT PK_TuyenBay PRIMARY KEY (MaTuyenBay),
    CONSTRAINT FK_TuyenBay_SBDi  FOREIGN KEY (MaSanBayDi)  REFERENCES SanBay(MaSanBay),
    CONSTRAINT FK_TuyenBay_SBDen FOREIGN KEY (MaSanBayDen) REFERENCES SanBay(MaSanBay),
    CONSTRAINT CHK_TuyenBay_KhacNhau CHECK (MaSanBayDi <> MaSanBayDen)
);
GO

-- 4. Bảng Máy Bay (Aircraft)
CREATE TABLE MayBay
(
    MaMayBay    varchar(15)  NOT NULL,
    TenMayBay   nvarchar(50) NOT NULL,
    MaHang      varchar(10)  NOT NULL,
    TongSoGhe   int          NOT NULL CHECK (TongSoGhe > 0),
    TrangThai   nvarchar(30) DEFAULT N'Sẵn sàng' CHECK (TrangThai IN (N'Sẵn sàng', N'Đang bay', N'Bảo trì')),
    CONSTRAINT PK_MayBay PRIMARY KEY (MaMayBay),
    CONSTRAINT FK_MayBay_Hang FOREIGN KEY (MaHang) REFERENCES HangHangKhong(MaHang)
);
GO

-- 5. Bảng Hạng Ghế (ECO: Phổ thông, BUS: Thương gia, FIR: Hạng nhất)
CREATE TABLE HangGhe
(
    MaHangGhe  varchar(10)   NOT NULL,
    TenHangGhe nvarchar(50)  NOT NULL UNIQUE,
    HeSoGia    decimal(4,2)  NOT NULL DEFAULT 1.0 CHECK (HeSoGia >= 1.0),
    MoTa       nvarchar(200) NULL,
    CONSTRAINT PK_HangGhe PRIMARY KEY (MaHangGhe)
);
GO

-- 6. Bảng Sơ Đồ Ghế Ngồi
CREATE TABLE GheNgoi
(
    MaGhe      int IDENTITY(1,1) NOT NULL,
    MaMayBay   varchar(15)       NOT NULL,
    SoGhe      varchar(10)       NOT NULL, -- VD: 01A, 02B, 10C
    MaHangGhe  varchar(10)       NOT NULL,
    TrangThai  bit               DEFAULT 1, -- 1: Tốt, 0: Hỏng
    CONSTRAINT PK_GheNgoi PRIMARY KEY (MaGhe),
    CONSTRAINT FK_GheNgoi_MayBay  FOREIGN KEY (MaMayBay)  REFERENCES MayBay(MaMayBay) ON DELETE CASCADE,
    CONSTRAINT FK_GheNgoi_HangGhe FOREIGN KEY (MaHangGhe) REFERENCES HangGhe(MaHangGhe),
    CONSTRAINT UQ_MayBay_SoGhe UNIQUE (MaMayBay, SoGhe)
);
GO

-- 7. Bảng Chuyến Bay (Lịch chuyến bay)
CREATE TABLE ChuyenBay
(
    MaChuyenBay   varchar(20)  NOT NULL,
    MaTuyenBay    varchar(15)  NOT NULL,
    MaMayBay      varchar(15)  NOT NULL,
    NgayGioBay    datetime     NOT NULL,
    NgayGioDenDK  datetime     NOT NULL,
    ThoiGianBay   int          NULL,
    GiaVeCoBan    money        NOT NULL CHECK (GiaVeCoBan > 0),
    SoGheTrong    int          NOT NULL CHECK (SoGheTrong >= 0),
    TrangThai     nvarchar(30) NOT NULL DEFAULT N'Đang mở bán' 
                  CHECK (TrangThai IN (N'Đang mở bán', N'Hết vé', N'Đã cất cánh', N'Đã hủy', N'Hoãn chuyến')),
    CONSTRAINT PK_ChuyenBay PRIMARY KEY (MaChuyenBay),
    CONSTRAINT FK_ChuyenBay_Tuyen  FOREIGN KEY (MaTuyenBay) REFERENCES TuyenBay(MaTuyenBay),
    CONSTRAINT FK_ChuyenBay_MayBay FOREIGN KEY (MaMayBay)   REFERENCES MayBay(MaMayBay),
    CONSTRAINT CHK_ThoiGianBay CHECK (NgayGioDenDK > NgayGioBay)
);
GO

-- 8. Bảng Chi Tiết Sân Bay Trung Gian (Transit/Stopover)
CREATE TABLE ChiTietChuyenBay
(
    MaCTCB        int IDENTITY(1,1) NOT NULL,
    MaChuyenBay   varchar(20)       NOT NULL,
    MaSanBayTG    varchar(10)       NOT NULL,
    ThoiGianDung  int               NOT NULL CHECK (ThoiGianDung > 0),
    ThuTuDung     int               NOT NULL DEFAULT 1,
    GhiChu        nvarchar(200)     NULL,
    CONSTRAINT PK_ChiTietChuyenBay PRIMARY KEY (MaCTCB),
    CONSTRAINT FK_CTCB_ChuyenBay FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay) ON DELETE CASCADE,
    CONSTRAINT FK_CTCB_SanBay    FOREIGN KEY (MaSanBayTG)  REFERENCES SanBay(MaSanBay),
    CONSTRAINT UQ_ChuyenBay_SanBayTG UNIQUE (MaChuyenBay, MaSanBayTG)
);
GO

-- Phân quyền, nhân viên và khách hàng (tích hợp tài khoản & mật khẩu)

-- 9. Bảng Vai Trò
CREATE TABLE VaiTro
(
    RoleID  int          NOT NULL,
    TenRole nvarchar(50) NOT NULL UNIQUE,
    MoTa    nvarchar(150) NULL,
    CONSTRAINT PK_VaiTro PRIMARY KEY (RoleID)
);
GO

-- 10. Bảng Nhân Viên
CREATE TABLE NhanVien
(
    MaNV      varchar(10)   NOT NULL,
    TenNV     nvarchar(50)  NOT NULL,
    SDT       varchar(15)   NULL UNIQUE,
    Email     varchar(100)  NULL UNIQUE,
    ChucVu    nvarchar(50)  NULL,
    Luong     money         DEFAULT 0 CHECK (Luong >= 0),
    TaiKhoan  varchar(50)   NOT NULL UNIQUE,
    MatKhau   varchar(255)  NOT NULL, -- Lưu Hash Password
    RoleID    int           NOT NULL DEFAULT 2,
    TrangThai bit           NOT NULL DEFAULT 1, -- 1: Đang làm việc, 0: Khóa
    NgayTao   datetime      DEFAULT GETDATE(),
    CONSTRAINT PK_NhanVien PRIMARY KEY (MaNV),
    CONSTRAINT FK_NhanVien_VaiTro FOREIGN KEY (RoleID) REFERENCES VaiTro(RoleID)
);
GO

-- 11. Bảng Khách Hàng (tích hợp trực tiếp mật khẩu và trạng thái khóa vào bảng Khách Hàng)
CREATE TABLE KhachHang
(
    MaKH            varchar(10)   NOT NULL,
    TenKH           nvarchar(50)  NOT NULL,
    GioiTinh        nvarchar(10)  NOT NULL CHECK (GioiTinh IN (N'Nam', N'Nữ', N'Khác')),
    NgaySinh        date          NOT NULL,
    SDT             varchar(15)   NOT NULL UNIQUE,
    Email           varchar(100)  NULL UNIQUE,
    MatKhau         varchar(255)  NOT NULL, -- Mật khẩu băm bằng ASP.NET Core PasswordHasher
    DiaChi          nvarchar(200) NULL,
    LoaiKhachHang   nvarchar(30)  NOT NULL DEFAULT N'Việt Nam' 
                    CHECK (LoaiKhachHang IN (N'Việt Nam', N'Nước ngoài', N'Doanh nghiệp')),
    -- Thông tin theo nhóm đối tượng:
    CCCD            varchar(20)   NULL,
    HoChieu         varchar(30)   NULL,
    QuocTich        nvarchar(50)  DEFAULT N'Việt Nam',
    CoQuanCongTac   nvarchar(150) NULL,
    SoTK_DoanhNghiep varchar(30)  NULL,
    TrangThaiKhoa   bit           NOT NULL DEFAULT 0, -- 0: Hoạt động, 1: Bị khóa
    NgayDangKy      datetime      DEFAULT GETDATE(),
    CONSTRAINT PK_KhachHang PRIMARY KEY (MaKH)
);
GO

CREATE UNIQUE INDEX UX_KhachHang_CCCD ON KhachHang(CCCD) WHERE CCCD IS NOT NULL;
CREATE UNIQUE INDEX UX_KhachHang_HoChieu ON KhachHang(HoChieu) WHERE HoChieu IS NOT NULL;
GO

-- Đặt chỗ, vé máy bay, hóa đơn & khuyến mãi (tích hợp thanh toán và vào hóa đơn)

-- 12. Bảng Mã Khuyến Mãi / Voucher
CREATE TABLE KhuyenMai
(
    MaKM             varchar(20)   NOT NULL,
    TenKM            nvarchar(100) NOT NULL,
    PhanTramGiam     decimal(5,2)  DEFAULT 0 CHECK (PhanTramGiam >= 0 AND PhanTramGiam <= 100),
    GiamToiDa        money         DEFAULT 0 CHECK (GiamToiDa >= 0),
    DonHangToiThieu  money         DEFAULT 0 CHECK (DonHangToiThieu >= 0),
    NgayBatDau       date          NOT NULL,
    NgayKetThuc      date          NOT NULL,
    SoLuong          int           DEFAULT 100 CHECK (SoLuong >= 0),
    TrangThai        bit           DEFAULT 1,
    CONSTRAINT PK_KhuyenMai PRIMARY KEY (MaKM),
    CONSTRAINT CHK_KhuyenMai_Ngay CHECK (NgayKetThuc >= NgayBatDau)
);
GO

-- 13. Bảng Phiếu Đặt Chỗ / Đơn Đặt Vé
CREATE TABLE PhieuDatCho
(
    MaDatCho      varchar(15)   NOT NULL, -- Mã PNR
    MaKH          varchar(10)   NOT NULL,
    NgayDat       datetime      DEFAULT GETDATE(),
    TongTienGoc   money         DEFAULT 0 CHECK (TongTienGoc >= 0),
    MaKM          varchar(20)   NULL,
    TienGiam      money         DEFAULT 0 CHECK (TienGiam >= 0),
    TongTienSauGiam money       DEFAULT 0 CHECK (TongTienSauGiam >= 0),
    HanThanhToan  datetime      NOT NULL, -- Thời hạn giữ chỗ
    TrangThai     nvarchar(30)  NOT NULL DEFAULT N'Chờ thanh toán'
                  CHECK (TrangThai IN (N'Chờ thanh toán', N'Đã thanh toán', N'Đã hủy', N'Hết hạn giữ chỗ', N'Hoàn tất')),
    GhiChu        nvarchar(255) NULL,
    CONSTRAINT PK_PhieuDatCho PRIMARY KEY (MaDatCho),
    CONSTRAINT FK_PhieuDatCho_KH FOREIGN KEY (MaKH) REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_PhieuDatCho_KM FOREIGN KEY (MaKM) REFERENCES KhuyenMai(MaKM)
);
GO

-- 14. Bảng Vé Máy Bay / Chi Tiết Đặt Chỗ
CREATE TABLE VeMayBay
(
    MaVe            varchar(20)   NOT NULL, -- Mã E-Ticket
    MaDatCho        varchar(15)   NOT NULL,
    MaChuyenBay     varchar(20)   NOT NULL,
    MaGhe           int           NOT NULL,
    MaHangGhe       varchar(10)   NOT NULL,
    TenHanhKhach    nvarchar(50)  NOT NULL,
    CCCD_HanhKhach  varchar(30)   NULL,
    LoaiHanhKhach   nvarchar(20)  DEFAULT N'Người lớn' CHECK (LoaiHanhKhach IN (N'Người lớn', N'Trẻ em', N'Em bé')),
    GiaVe           money         NOT NULL CHECK (GiaVe > 0),
    DichVuPhuTro    varchar(12)   NULL,
    PhiPhuTro       money         NOT NULL DEFAULT 0 CHECK (PhiPhuTro >= 0),
    TrangThai       nvarchar(30)  NOT NULL DEFAULT N'Đã đặt chỗ'
                    CHECK (TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in', N'Đã hủy', N'Đã đổi')),
    CONSTRAINT PK_VeMayBay PRIMARY KEY (MaVe),
    CONSTRAINT FK_VeMayBay_PhieuDat FOREIGN KEY (MaDatCho)    REFERENCES PhieuDatCho(MaDatCho) ON DELETE CASCADE,
    CONSTRAINT FK_VeMayBay_Chuyen   FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay),
    CONSTRAINT FK_VeMayBay_Ghe      FOREIGN KEY (MaGhe)       REFERENCES GheNgoi(MaGhe),
    CONSTRAINT FK_VeMayBay_HangGhe  FOREIGN KEY (MaHangGhe)   REFERENCES HangGhe(MaHangGhe)
);
GO

-- Một ghế chỉ có một vé hiệu lực trên cùng chuyến bay, nhưng giữ lịch sử vé hủy/đổi.
CREATE UNIQUE INDEX UX_VeMayBay_GheDangSuDung ON VeMayBay(MaChuyenBay, MaGhe)
WHERE TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in');
GO

-- 15. Bảng Hóa Đơn & Thanh Toán (tích hợp toàn bộ phương thức và giao dịch vào hoá đơn)
CREATE TABLE HoaDon
(
    MaHD                varchar(15)   NOT NULL,
    MaDatCho            varchar(15)   NOT NULL UNIQUE,
    MaNV                varchar(10)   NULL, -- NULL nếu thanh toán online
    NgayLap             datetime      DEFAULT GETDATE(),
    TongTien            money         NOT NULL CHECK (TongTien >= 0),
    PhuongThuc          nvarchar(50)  NOT NULL DEFAULT N'Tiền mặt'
                        CHECK (PhuongThuc IN (N'Tiền mặt', N'Ví MoMo', N'Ví ZaloPay', N'Thẻ ATM nội địa', N'VISA/MasterCard', N'Internet Banking')),
    MaGiaoDichNganHang  varchar(100)  NULL,
    NgayThanhToan       datetime      DEFAULT GETDATE(),
    TrangThai           nvarchar(30)  NOT NULL DEFAULT N'Đã thanh toán' 
                        CHECK (TrangThai IN (N'Đã thanh toán', N'Đã hủy', N'Đã hoàn tiền')),
    GhiChu              nvarchar(200) NULL,
    ChiTietVe           nvarchar(max) NULL,
    CONSTRAINT PK_HoaDon PRIMARY KEY (MaHD),
    CONSTRAINT FK_HoaDon_DatCho   FOREIGN KEY (MaDatCho) REFERENCES PhieuDatCho(MaDatCho),
    CONSTRAINT FK_HoaDon_NhanVien FOREIGN KEY (MaNV)     REFERENCES NhanVien(MaNV)
);
GO

-- 16. Bảng Yêu Cầu Hủy / Đổi Vé
CREATE TABLE YeuCauHuyDoiVe
(
    MaYeuCau       int IDENTITY(1,1) NOT NULL,
    MaVe           varchar(20)       NOT NULL,
    LoaiYeuCau     nvarchar(20)      NOT NULL CHECK (LoaiYeuCau IN (N'Hủy vé', N'Đổi vé')),
    NgayYeuCau     datetime          DEFAULT GETDATE(),
    LyDo           nvarchar(255)     NULL,
    -- Dành cho đổi vé:
    MaChuyenBayMoi varchar(20)       NULL,
    MaGheMoi       int               NULL,
    -- Dành cho hoàn tiền theo Luật 72h:
    PhanTramHoan   decimal(5,2)      DEFAULT 0,
    SoTienGoc      money             DEFAULT 0,
    PhiPhat        money             DEFAULT 0,
    SoTienHoan     money             DEFAULT 0,
    PhiChenhLech   money             DEFAULT 0,
    -- Xử lý:
    TrangThai      nvarchar(30)      DEFAULT N'Đang chờ duyệt' 
                   CHECK (TrangThai IN (N'Đang chờ duyệt', N'Đã chấp thuận', N'Bị từ chối', N'Đã hoàn tất')),
    MaNVXuLy       varchar(10)       NULL,
    NgayXuLy       datetime          NULL,
    GhiChu         nvarchar(200)     NULL,
    CONSTRAINT PK_YeuCauHuyDoiVe PRIMARY KEY (MaYeuCau),
    CONSTRAINT FK_YeuCau_VeBay      FOREIGN KEY (MaVe)           REFERENCES VeMayBay(MaVe),
    CONSTRAINT FK_YeuCau_ChuyenMoi  FOREIGN KEY (MaChuyenBayMoi) REFERENCES ChuyenBay(MaChuyenBay),
    CONSTRAINT FK_YeuCau_GheMoi     FOREIGN KEY (MaGheMoi)       REFERENCES GheNgoi(MaGhe),
    CONSTRAINT FK_YeuCau_NV         FOREIGN KEY (MaNVXuLy)       REFERENCES NhanVien(MaNV)
);
GO

-- 17. Bảng Đánh Giá Chuyến Bay
CREATE TABLE DanhGiaChuyenBay
(
    MaDanhGia   int IDENTITY(1,1) NOT NULL,
    MaKH        varchar(10)       NOT NULL,
    MaChuyenBay varchar(20)       NOT NULL,
    SoSao       int               NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NoiDung     nvarchar(500)     NULL,
    NgayDanhGia datetime          DEFAULT GETDATE(),
    CONSTRAINT PK_DanhGia PRIMARY KEY (MaDanhGia),
    CONSTRAINT UQ_DanhGia_Khach_Chuyen UNIQUE (MaKH, MaChuyenBay),
    CONSTRAINT FK_DanhGia_KH     FOREIGN KEY (MaKH)        REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_DanhGia_Chuyen FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay)
);
GO

-- UC10/UC11: hồ sơ khiếu nại tách khỏi đánh giá và yêu cầu hủy/đổi.
CREATE TABLE KhieuNai
(
    MaKhieuNai int IDENTITY(1,1) PRIMARY KEY,
    MaKH varchar(10) NOT NULL REFERENCES KhachHang(MaKH),
    MaDatCho varchar(15) NULL REFERENCES PhieuDatCho(MaDatCho),
    MaVe varchar(20) NULL REFERENCES VeMayBay(MaVe),
    TieuDe nvarchar(120) NOT NULL,
    PhanLoai nvarchar(30) NOT NULL CHECK (PhanLoai IN (N'Chuyến bay', N'Thanh toán', N'Dịch vụ')),
    NoiDung nvarchar(1000) NOT NULL,
    TrangThai nvarchar(25) NOT NULL DEFAULT N'Chờ tiếp nhận'
        CHECK (TrangThai IN (N'Chờ tiếp nhận', N'Đã giải quyết', N'Từ chối')),
    NgayGui datetime2 NOT NULL DEFAULT SYSDATETIME(),
    MaNVXuLy varchar(10) NULL REFERENCES NhanVien(MaNV),
    NgayXuLy datetime2 NULL,
    PhanHoi nvarchar(1000) NULL,
    MaKM varchar(20) NULL REFERENCES KhuyenMai(MaKM)
);
GO

-- ====================================================================================================
-- PHẦN 4: USER-DEFINED TABLE TYPES (TRUYỀN DANH SÁCH VÉ TỪ C# VÀO STORED PROCEDURES)
-- ====================================================================================================

CREATE TYPE ChiTietDatVe_Type AS TABLE
(
    MaGhe          int,
    MaHangGhe      varchar(10),
    TenHanhKhach   nvarchar(50),
    CCCD_HanhKhach varchar(30),
    LoaiHanhKhach  nvarchar(20),
    GiaVe          money,
    DichVuPhuTro   varchar(12)
);
GO

-- ====================================================================================================
-- PHẦN 5: CÁC HÀM TÍNH TOÁN NGHIỆP VỤ VÀ HOÀN TIỀN (USER-DEFINED FUNCTIONS - UDF)
-- ====================================================================================================

-- Danh mục gói mẫu cho UC17, dùng chung khi hiển thị và tính giá ở SQL.
CREATE FUNCTION fn_DichVuPhuTro()
RETURNS TABLE
AS RETURN
    SELECT MaDichVu, TenDichVu, CAST(DonGia AS money) AS DonGia
    FROM (VALUES ('HL15', N'Hành lý ký gửi 15 kg', 180000),
                 ('HL20', N'Hành lý ký gửi 20 kg', 240000),
                 ('HL30', N'Hành lý ký gửi 30 kg', 360000),
                 ('SUATAN', N'Suất ăn', 100000),
                 ('BAOHIEM', N'Bảo hiểm chuyến bay', 80000)) d(MaDichVu, TenDichVu, DonGia);
GO

-- 1. Hàm tính giá vé thực tế theo hệ số hạng ghế
CREATE FUNCTION fn_TinhGiaVeThucTe
(
    @GiaCoBan money,
    @MaHangGhe varchar(10)
)
RETURNS money
AS
BEGIN
    DECLARE @HeSo decimal(4,2) = 1.0;

    SELECT @HeSo = ISNULL(HeSoGia, 1.0) FROM HangGhe WHERE MaHangGhe = @MaHangGhe;

    DECLARE @GiaCuoi money = @GiaCoBan * @HeSo;

    RETURN ROUND(@GiaCuoi, -3);
END;
GO

-- 2. Hàm tính tiền hoàn trả hủy vé theo quy tắc UC18 (>72h hoàn 80%, 24–72h hoàn 50%, dưới 24h hoàn 0%)
CREATE FUNCTION fn_TinhTienHoanTraHuyVe
(
    @MaVe varchar(20)
)
RETURNS @KetQua TABLE
(
    GiaVeGoc money,
    GioConLai decimal(10,2),
    PhanTramHoan decimal(5,2),
    SoTienHoan money,
    PhiPhat money
)
AS
BEGIN
    DECLARE @GiaVe money, @NgayGioBay datetime;
    SELECT @GiaVe = v.GiaVe, @NgayGioBay = cb.NgayGioBay
    FROM VeMayBay v
    JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
    WHERE v.MaVe = @MaVe;

    IF @GiaVe IS NOT NULL
    BEGIN
        DECLARE @HienTai datetime = GETDATE();
        DECLARE @GioConLai decimal(10,2) = DATEDIFF(SECOND, @HienTai, @NgayGioBay) / 3600.0;
        DECLARE @PhanTram decimal(5,2);

        IF @NgayGioBay > DATEADD(HOUR, 72, @HienTai)
            SET @PhanTram = 80.00;
        ELSE IF @NgayGioBay >= DATEADD(HOUR, 24, @HienTai)
            SET @PhanTram = 50.00;
        ELSE
            SET @PhanTram = 0.00;

        DECLARE @TienHoan money = @GiaVe * (@PhanTram / 100.0);
        DECLARE @Phat money = @GiaVe - @TienHoan;

        INSERT INTO @KetQua VALUES (@GiaVe, @GioConLai, @PhanTram, @TienHoan, @Phat);
    END
    RETURN;
END;
GO

-- 3. Hàm đếm số ghế còn trống theo chuyến bay và hạng ghế
CREATE VIEW vw_VeMayBayChiemGhe AS
SELECT v.MaChuyenBay, v.MaGhe
FROM dbo.VeMayBay v
JOIN dbo.PhieuDatCho p ON p.MaDatCho = v.MaDatCho
WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
   OR (v.TrangThai = N'Đã đặt chỗ' AND p.TrangThai = N'Chờ thanh toán'
       AND p.HanThanhToan > GETDATE());
GO

CREATE FUNCTION fn_DemSoGheTrongTheoHang
(
    @MaChuyenBay varchar(20),
    @MaHangGhe varchar(10)
)
RETURNS int
AS
BEGIN
    DECLARE @SoGheTrong int = 0;
    DECLARE @MaMayBay varchar(15);

    SELECT @MaMayBay = MaMayBay FROM ChuyenBay WHERE MaChuyenBay = @MaChuyenBay;

    SELECT @SoGheTrong = COUNT(*)
    FROM GheNgoi g
    WHERE g.MaMayBay = @MaMayBay 
      AND g.MaHangGhe = @MaHangGhe
      AND g.TrangThai = 1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.vw_VeMayBayChiemGhe v
          WHERE v.MaChuyenBay = @MaChuyenBay AND v.MaGhe = g.MaGhe
      );

    RETURN ISNULL(@SoGheTrong, 0);
END;
GO

-- 4. Hàm thống kê tổng doanh thu theo tháng và năm
CREATE FUNCTION fn_DoanhThuThangNam
(
    @Thang int,
    @Nam int
)
RETURNS money
AS
BEGIN
    DECLARE @TongDoanhThu money = 0;
    SELECT @TongDoanhThu = SUM(hd.TongTien)
    FROM HoaDon hd
    WHERE MONTH(hd.NgayThanhToan) = @Thang 
      AND YEAR(hd.NgayThanhToan) = @Nam
      AND hd.TrangThai = N'Đã thanh toán';
    RETURN ISNULL(@TongDoanhThu, 0);
END;
GO

-- ====================================================================================================
-- PHẦN 6: CÁC TRIGGERS TỰ ĐỘNG KIỂM SOÁT DỮ LIỆU & GHẾ TRỐNG
-- ====================================================================================================

-- Đồng bộ tồn ghế theo phần chênh lệch số vé hiệu lực của từng chuyến.
CREATE TRIGGER trg_VeMayBay_CapNhatGheTrong
ON VeMayBay AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    WITH ThayDoi AS
    (
        SELECT MaChuyenBay, 1 AS SoVe FROM inserted
        WHERE TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        UNION ALL
        SELECT MaChuyenBay, -1 FROM deleted
        WHERE TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
    ),
    Tong AS
    (
        SELECT MaChuyenBay, SUM(SoVe) AS SoVe FROM ThayDoi
        GROUP BY MaChuyenBay HAVING SUM(SoVe) <> 0
    )
    UPDATE cb
    SET SoGheTrong = cb.SoGheTrong - t.SoVe,
        TrangThai = CASE
            WHEN cb.TrangThai = N'Đang mở bán' AND cb.SoGheTrong - t.SoVe = 0 THEN N'Hết vé'
            WHEN cb.TrangThai = N'Hết vé' AND cb.SoGheTrong - t.SoVe > 0 THEN N'Đang mở bán'
            ELSE cb.TrangThai END
    FROM ChuyenBay cb JOIN Tong t ON t.MaChuyenBay = cb.MaChuyenBay;
END;
GO

-- 3. Trigger cập nhật tổng tiền Phiếu Đặt Chỗ khi thêm/xóa/sửa vé
CREATE TRIGGER trg_CapNhatTongTienPhieuDatCho
ON VeMayBay AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @BangMaDatCho TABLE (MaDatCho varchar(15));
    INSERT INTO @BangMaDatCho
    SELECT DISTINCT MaDatCho FROM inserted
    UNION
    SELECT DISTINCT MaDatCho FROM deleted;

    UPDATE PhieuDatCho
    SET TongTienGoc = ISNULL((
            SELECT SUM(v.GiaVe + v.PhiPhuTro) FROM VeMayBay v
            WHERE v.MaDatCho = PhieuDatCho.MaDatCho AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        ), 0)
    WHERE MaDatCho IN (SELECT MaDatCho FROM @BangMaDatCho);

    UPDATE PhieuDatCho
    SET TongTienSauGiam = CASE 
            WHEN TongTienGoc - TienGiam < 0 THEN 0 
            ELSE TongTienGoc - TienGiam 
        END
    WHERE MaDatCho IN (SELECT MaDatCho FROM @BangMaDatCho);
END;
GO

-- ====================================================================================================
-- PHẦN 7: CÁC THỦ TỤC LƯU TRỮ STORED PROCEDURES (XỬ LÝ GIAO DỊCH AN TOÀN)
-- ====================================================================================================

-- 1. SP Tìm kiếm chuyến bay đa tiêu chí (Thanh lọc tìm kiếm)
CREATE PROCEDURE sp_TimKiemChuyenBay
    @MaSanBayDi varchar(10),
    @MaSanBayDen varchar(10),
    @NgayBay date,
    @MaHangGhe varchar(10) = NULL,
    @GiaMax money = NULL,
    @MaHang varchar(10) = NULL,
    @SoHanhKhach int = 1,
    @SoNgayLanCan int = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cb.MaChuyenBay, cb.TenHangHangKhong AS TenHang, cb.Logo, cb.TenMayBay,
        cb.TenSanBayDi AS SanBayDi, cb.ThanhPhoDi, cb.TenSanBayDen AS SanBayDen, cb.ThanhPhoDen,
        cb.NgayGioBay, cb.NgayGioDenDK, cb.ThoiGianBay,
        CASE WHEN @MaHangGhe IS NULL THEN cb.GiaVeCoBan
             ELSE dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, @MaHangGhe) END AS GiaVeCoBan,
        CASE WHEN @MaHangGhe IS NULL THEN cb.SoGheTrong ELSE seats.SoGheTheoHang END AS SoGheTrong,
        cb.TrangThaiChuyenBay AS TrangThai
    FROM dbo.vw_DanhSachChuyenBayChiTiet cb
    CROSS APPLY (VALUES (dbo.fn_DemSoGheTrongTheoHang(cb.MaChuyenBay, @MaHangGhe))) seats(SoGheTheoHang)
    WHERE cb.MaSanBayDi = @MaSanBayDi AND cb.MaSanBayDen = @MaSanBayDen
      AND cb.NgayGioBay >= DATEADD(DAY, -@SoNgayLanCan, @NgayBay)
      AND cb.NgayGioBay < DATEADD(DAY, 1 + @SoNgayLanCan, @NgayBay)
      AND cb.TrangThaiChuyenBay = N'Đang mở bán'
      AND cb.NgayGioBay > DATEADD(HOUR, 2, GETDATE())
      AND cb.SoGheTrong >= @SoHanhKhach
      AND (@GiaMax IS NULL OR CASE WHEN @MaHangGhe IS NULL THEN cb.GiaVeCoBan
           ELSE dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, @MaHangGhe) END <= @GiaMax)
      AND (@MaHang IS NULL OR cb.MaHang = @MaHang)
      AND (@MaHangGhe IS NULL OR seats.SoGheTheoHang >= @SoHanhKhach)
    ORDER BY cb.GiaVeCoBan ASC, cb.NgayGioBay ASC;
END;
GO

-- 2. SP Đặt vé theo nhóm hành khách
CREATE PROCEDURE sp_DatVeMayBay_Batch
    @MaDatCho        varchar(15),
    @MaKH            varchar(10),
    @MaChuyenBay     varchar(20),
    @MaKM            varchar(20) = NULL,
    @GhiChu          nvarchar(255) = NULL,
    @DSVe            ChiTietDatVe_Type READONLY
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Dọn đúng ghế được yêu cầu trước khi khóa chuyến. Tác vụ dọn khóa đơn trước.
        IF @MaChuyenBay IS NOT NULL AND EXISTS (SELECT 1 FROM @DSVe)
            EXEC dbo.sp_TuDongHuyDonGiuChoHetHan_Cursor @MaChuyenBay = @MaChuyenBay, @GheYeuCau = @DSVe;

        BEGIN TRANSACTION;
        DECLARE @HienTai datetime, @NgayBay datetime, @MaMayBay varchar(15), @GiaCoBan money;
        SELECT @NgayBay = NgayGioBay, @MaMayBay = MaMayBay, @GiaCoBan = GiaVeCoBan
        FROM ChuyenBay WITH (UPDLOCK, HOLDLOCK)
        WHERE MaChuyenBay = @MaChuyenBay AND TrangThai = N'Đang mở bán';
        SET @HienTai = GETDATE();
        IF @NgayBay IS NULL OR @NgayBay <= DATEADD(HOUR, 2, @HienTai)
            THROW 51001, N'Chuyến bay đã dừng nhận đặt vé.', 1;

        IF NOT EXISTS (SELECT 1 FROM @DSVe)
            OR EXISTS (SELECT 1 FROM @DSVe GROUP BY MaGhe HAVING COUNT(*) > 1)
            THROW 51002, N'Danh sách ghế trống hoặc có ghế bị lặp.', 1;

        IF EXISTS (
            SELECT 1 FROM @DSVe d
            LEFT JOIN GheNgoi g ON g.MaGhe = d.MaGhe AND g.MaMayBay = @MaMayBay AND g.TrangThai = 1
            WHERE g.MaGhe IS NULL OR NULLIF(LTRIM(RTRIM(d.TenHanhKhach)), N'') IS NULL
                OR d.LoaiHanhKhach IS NULL
                OR (d.DichVuPhuTro IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM dbo.fn_DichVuPhuTro() dv WHERE dv.MaDichVu = d.DichVuPhuTro
                ))
        )
            THROW 51003, N'Ghế hoặc thông tin hành khách không hợp lệ.', 1;

        -- Kiểm tra và khóa độc quyền (UPDLOCK, HOLDLOCK) để chống tranh chấp đồng thời (Race Condition)
        IF EXISTS (
            SELECT 1 FROM @DSVe ds
            JOIN VeMayBay v WITH (UPDLOCK, HOLDLOCK) ON v.MaChuyenBay = @MaChuyenBay AND v.MaGhe = ds.MaGhe
            WHERE v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
        BEGIN
            RAISERROR(N'Một trong các ghế bạn chọn đã có người đặt. Vui lòng chọn ghế khác!', 16, 1);
        END

        DECLARE @TongTienGoc money = 0;
        SELECT @TongTienGoc = SUM(dbo.fn_TinhGiaVeThucTe(@GiaCoBan, g.MaHangGhe) + ISNULL(dv.DonGia, 0))
        FROM @DSVe d JOIN GheNgoi g ON g.MaGhe = d.MaGhe
        LEFT JOIN dbo.fn_DichVuPhuTro() dv ON dv.MaDichVu = d.DichVuPhuTro;

        DECLARE @TienGiam money = 0;
        IF @MaKM IS NOT NULL
        BEGIN
            DECLARE @PhanTram decimal(5,2), @GiamMax money, @ToiThieu money, @ConLai int;
            SELECT @PhanTram = PhanTramGiam, @GiamMax = GiamToiDa, @ToiThieu = DonHangToiThieu, @ConLai = SoLuong
            FROM KhuyenMai WITH (UPDLOCK, HOLDLOCK) WHERE MaKM = @MaKM AND TrangThai = 1 AND CAST(GETDATE() AS date) BETWEEN NgayBatDau AND NgayKetThuc;

            IF @ConLai > 0 AND @TongTienGoc >= @ToiThieu
            BEGIN
                SET @TienGiam = @TongTienGoc * (@PhanTram / 100.0);
                IF @TienGiam > @GiamMax AND @GiamMax > 0 SET @TienGiam = @GiamMax;
                UPDATE KhuyenMai SET SoLuong = SoLuong - 1 WHERE MaKM = @MaKM;
            END
            ELSE
            BEGIN
                THROW 51004, N'Mã khuyến mãi hết hiệu lực, hết lượt hoặc đơn chưa đạt giá trị tối thiểu.', 1;
            END
        END

        DECLARE @TongSauGiam money = @TongTienGoc - @TienGiam;
        IF @TongSauGiam < 0 SET @TongSauGiam = 0;

        DECLARE @HanTT datetime = DATEADD(MINUTE, 15, @HienTai);
        IF @HanTT > DATEADD(HOUR, -2, @NgayBay)
            SET @HanTT = DATEADD(HOUR, -2, @NgayBay);

        INSERT INTO PhieuDatCho(MaDatCho, MaKH, NgayDat, TongTienGoc, MaKM, TienGiam, TongTienSauGiam, HanThanhToan, TrangThai, GhiChu)
        VALUES (@MaDatCho, @MaKH, GETDATE(), @TongTienGoc, @MaKM, @TienGiam, @TongSauGiam, @HanTT, N'Chờ thanh toán', @GhiChu);

        INSERT INTO VeMayBay (MaVe, MaDatCho, MaChuyenBay, MaGhe, MaHangGhe, TenHanhKhach, CCCD_HanhKhach, LoaiHanhKhach, GiaVe, DichVuPhuTro, PhiPhuTro, TrangThai)
        SELECT 
            @MaDatCho + '-' + CAST(ROW_NUMBER() OVER (ORDER BY g.MaGhe) AS varchar(5)),
            @MaDatCho,
            @MaChuyenBay,
            g.MaGhe,
            g.MaHangGhe,
            d.TenHanhKhach,
            d.CCCD_HanhKhach,
            d.LoaiHanhKhach,
            dbo.fn_TinhGiaVeThucTe(@GiaCoBan, g.MaHangGhe),
            d.DichVuPhuTro,
            ISNULL(dv.DonGia, 0),
            N'Đã đặt chỗ'
        FROM @DSVe d JOIN GheNgoi g ON g.MaGhe = d.MaGhe
        LEFT JOIN dbo.fn_DichVuPhuTro() dv ON dv.MaDichVu = d.DichVuPhuTro;

        COMMIT TRANSACTION;
        SELECT @MaDatCho AS MaDatCho, @TongSauGiam AS TongTienThanhToan, @HanTT AS HanThanhToan, N'Đặt vé thành công!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- UC20: áp dụng hoặc tháo voucher tại bước thanh toán, giữ lượt đến hạn giữ chỗ.
CREATE PROCEDURE sp_ApDungKhuyenMai
    @MaDatCho varchar(15),
    @MaKM varchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @MaCu varchar(20), @Tong money, @TienGiam money = 0;
        SELECT @MaCu = MaKM, @Tong = TongTienGoc
        FROM PhieuDatCho WITH (UPDLOCK, HOLDLOCK)
        WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ thanh toán' AND HanThanhToan > GETDATE();
        IF @Tong IS NULL
            THROW 51009, N'Đơn không còn thời hạn giữ chỗ.', 1;

        IF @MaKM IS NOT NULL AND ISNULL(@MaCu, '') <> @MaKM
        BEGIN
            SELECT @TienGiam =
                CASE WHEN GiamToiDa > 0 AND @Tong * PhanTramGiam / 100.0 > GiamToiDa
                     THEN GiamToiDa ELSE @Tong * PhanTramGiam / 100.0 END
            FROM KhuyenMai WITH (UPDLOCK, HOLDLOCK)
            WHERE MaKM = @MaKM AND TrangThai = 1 AND SoLuong > 0 AND @Tong >= DonHangToiThieu
                AND CONVERT(date, GETDATE()) BETWEEN NgayBatDau AND NgayKetThuc;
            IF @@ROWCOUNT = 0
                THROW 51004, N'Mã khuyến mãi hết hiệu lực, hết lượt hoặc đơn chưa đạt giá trị tối thiểu.', 1;
            UPDATE KhuyenMai SET SoLuong = SoLuong - 1 WHERE MaKM = @MaKM;
        END;
        IF ISNULL(@MaCu, '') <> ISNULL(@MaKM, '')
        BEGIN
            UPDATE KhuyenMai SET SoLuong = SoLuong + 1 WHERE MaKM = @MaCu;
            UPDATE PhieuDatCho SET MaKM = @MaKM, TienGiam = @TienGiam, TongTienSauGiam = @Tong - @TienGiam
            WHERE MaDatCho = @MaDatCho;
        END;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- 3. SP Thanh toán & Xuất hóa đơn tích hợp
CREATE PROCEDURE sp_ThanhToanDonVe
    @MaHD             varchar(15),
    @MaDatCho         varchar(15),
    @MaNV             varchar(10) = NULL,
    @PhuongThuc       nvarchar(50) = N'Thẻ ATM nội địa',
    @MaGiaoDich       varchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @TongTien money, @TrangThaiDatCho nvarchar(30), @HanThanhToan datetime;
        SELECT @TongTien = TongTienSauGiam, @TrangThaiDatCho = TrangThai, @HanThanhToan = HanThanhToan
        FROM PhieuDatCho WITH (UPDLOCK, HOLDLOCK) WHERE MaDatCho = @MaDatCho;

        IF EXISTS (SELECT 1 FROM HoaDon WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã thanh toán')
        BEGIN
            SELECT MaHD AS MaHoaDon, TongTien AS SoTienThanhToan FROM HoaDon WHERE MaDatCho = @MaDatCho;
            COMMIT TRANSACTION;
            RETURN;
        END;
        IF @HanThanhToan <= GETDATE()
            THROW 51005, N'Đơn đã hết thời hạn giữ chỗ.', 1;
        IF NOT EXISTS (SELECT 1 FROM VeMayBay WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã đặt chỗ')
            THROW 51006, N'Đơn không có vé đang giữ chỗ.', 1;

        IF @TongTien IS NULL
        BEGIN
            RAISERROR(N'Không tìm thấy thông tin đơn đặt vé!', 16, 1);
        END

        IF @TrangThaiDatCho <> N'Chờ thanh toán'
        BEGIN
            RAISERROR(N'Đơn đặt chỗ không ở trạng thái chờ thanh toán!', 16, 1);
        END

        -- 1. Tạo Hóa Đơn kèm thông tin Thanh Toán trực tiếp
        DECLARE @ChiTietVe nvarchar(max) = (
            SELECT v.MaVe, v.TenHanhKhach, v.MaChuyenBay, v.ThanhPhoDi, v.ThanhPhoDen,
                   v.TenHangGhe, v.SoGhe, v.GiaVe, v.PhiPhuTro
            FROM vw_ChiTietVeVaHanhKhach v
            WHERE v.MaDatCho = @MaDatCho AND v.TrangThaiVe = N'Đã đặt chỗ'
            FOR JSON PATH
        );
        INSERT INTO HoaDon (MaHD, MaDatCho, MaNV, NgayLap, TongTien, PhuongThuc, MaGiaoDichNganHang, NgayThanhToan, TrangThai, GhiChu, ChiTietVe)
        VALUES (@MaHD, @MaDatCho, @MaNV, GETDATE(), @TongTien, @PhuongThuc, @MaGiaoDich, GETDATE(), N'Đã thanh toán', N'Thanh toán mã đặt chỗ ' + @MaDatCho, @ChiTietVe);

        -- 2. Cập nhật trạng thái Đơn đặt chỗ & Vé máy bay
        UPDATE PhieuDatCho SET TrangThai = N'Đã thanh toán' WHERE MaDatCho = @MaDatCho;
        UPDATE VeMayBay    SET TrangThai = N'Đã xuất vé'     WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã đặt chỗ';

        COMMIT TRANSACTION;
        SELECT @MaHD AS MaHoaDon, @TongTien AS SoTienThanhToan, N'Thanh toán hoàn tất, vé đã được xuất điện tử!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 4. SP Xử lý Hủy vé & Hoàn tiền theo Quy định 72 giờ
CREATE PROCEDURE sp_XuLyHuyVeVaHoanTien
    @MaVe        varchar(20),
    @LyDo        nvarchar(255),
    @MaNVXuLy    varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @MaDatCho varchar(15), @NgayBay datetime, @TrangThaiVe nvarchar(30);
        SELECT @MaDatCho = MaDatCho FROM VeMayBay WHERE MaVe = @MaVe;
        DECLARE @TrangThaiDon nvarchar(30);
        SELECT @TrangThaiDon = TrangThai FROM PhieuDatCho WITH (UPDLOCK, HOLDLOCK) WHERE MaDatCho = @MaDatCho;
        SELECT @NgayBay = cb.NgayGioBay, @TrangThaiVe = v.TrangThai
        FROM VeMayBay v WITH (UPDLOCK) JOIN ChuyenBay cb ON cb.MaChuyenBay = v.MaChuyenBay
        WHERE v.MaVe = @MaVe AND cb.TrangThai NOT IN (N'Đã cất cánh', N'Đã hủy');

        IF @TrangThaiVe IS NULL OR @TrangThaiVe <> N'Đã xuất vé'
           OR @TrangThaiDon <> N'Đã thanh toán' OR @NgayBay <= GETDATE()
            THROW 51007, N'Chỉ hủy được vé đã xuất và chưa khởi hành, chưa check-in.', 1;

        DECLARE @GiaGoc money, @GioConLai decimal(10,2), @PhanTram decimal(5,2), @TienHoan money, @PhiPhat money;
        SELECT @GiaGoc = GiaVeGoc, @GioConLai = GioConLai, @PhanTram = PhanTramHoan, @TienHoan = SoTienHoan, @PhiPhat = PhiPhat
        FROM fn_TinhTienHoanTraHuyVe(@MaVe);

        UPDATE VeMayBay SET TrangThai = N'Đã hủy' WHERE MaVe = @MaVe;

        IF NOT EXISTS (SELECT 1 FROM VeMayBay WHERE MaDatCho = @MaDatCho
                       AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in'))
            UPDATE PhieuDatCho SET TrangThai = N'Đã hủy' WHERE MaDatCho = @MaDatCho;

        INSERT INTO YeuCauHuyDoiVe (MaVe, LoaiYeuCau, NgayYeuCau, LyDo, PhanTramHoan, SoTienGoc, PhiPhat, SoTienHoan, TrangThai, MaNVXuLy, NgayXuLy, GhiChu)
        VALUES (@MaVe, N'Hủy vé', GETDATE(), @LyDo, @PhanTram, @GiaGoc, @PhiPhat, @TienHoan, N'Đã hoàn tất', @MaNVXuLy, GETDATE(), 
                N'Thời gian trước bay: ' + CAST(@GioConLai AS nvarchar) + N' giờ. Hoàn ' + CAST(@PhanTram AS nvarchar) + N'%');

        COMMIT TRANSACTION;
        SELECT @MaVe AS MaVeHuy, @GiaGoc AS GiaVeGoc, @PhanTram AS PhanTramHoan, @TienHoan AS SoTienHoanKhachNhan, @PhiPhat AS PhiPhatHuy;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 5. SP Đổi vé máy bay sang chuyến bay mới
CREATE PROCEDURE sp_DoiVeMayBay
    @MaVeCu          varchar(20),
    @MaChuyenBayMoi  varchar(20),
    @MaGheMoi        int,
    @MaHangGheMoi    varchar(10),
    @MaNVXuLy        varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF @MaChuyenBayMoi IS NOT NULL AND @MaGheMoi IS NOT NULL
        BEGIN
            DECLARE @Ghe ChiTietDatVe_Type;
            INSERT @Ghe (MaGhe) VALUES (@MaGheMoi);
            EXEC dbo.sp_TuDongHuyDonGiuChoHetHan_Cursor @MaChuyenBay = @MaChuyenBayMoi, @GheYeuCau = @Ghe;
        END;

        BEGIN TRANSACTION;
        DECLARE @GiaVeCu money, @MaDatCho varchar(15), @TenHK nvarchar(50), @CCCD varchar(30),
                @LoaiHK nvarchar(20), @MaDichVu varchar(12), @PhiPhuTro money;
        SELECT @MaDatCho = MaDatCho FROM VeMayBay WHERE MaVe = @MaVeCu;
        DECLARE @TrangThaiDon nvarchar(30);
        SELECT @TrangThaiDon = TrangThai FROM PhieuDatCho WITH (UPDLOCK, HOLDLOCK) WHERE MaDatCho = @MaDatCho;

        SELECT @GiaVeCu = v.GiaVe, @TenHK = v.TenHanhKhach, @CCCD = v.CCCD_HanhKhach, @LoaiHK = v.LoaiHanhKhach, @MaDichVu = v.DichVuPhuTro, @PhiPhuTro = v.PhiPhuTro
        FROM VeMayBay v WITH (UPDLOCK) JOIN ChuyenBay cb ON cb.MaChuyenBay = v.MaChuyenBay
        WHERE v.MaVe = @MaVeCu AND v.TrangThai = N'Đã xuất vé' AND @TrangThaiDon = N'Đã thanh toán'
          AND cb.TrangThai IN (N'Đang mở bán', N'Hết vé', N'Hoãn chuyến')
          AND cb.NgayGioBay >= DATEADD(HOUR, 24, GETDATE());

        IF @GiaVeCu IS NULL
        BEGIN
            RAISERROR(N'Vé phải đã xuất, chưa check-in và còn ít nhất 24 giờ trước khởi hành.', 16, 1);
        END

        DECLARE @GiaCoBanMoi money, @HangGheThucTe varchar(10);
        SELECT @GiaCoBanMoi = cb.GiaVeCoBan, @HangGheThucTe = g.MaHangGhe
        FROM ChuyenBay cb WITH (UPDLOCK, HOLDLOCK)
        JOIN GheNgoi g ON g.MaMayBay = cb.MaMayBay AND g.MaGhe = @MaGheMoi AND g.TrangThai = 1
        WHERE cb.MaChuyenBay = @MaChuyenBayMoi AND cb.TrangThai = N'Đang mở bán'
          AND cb.NgayGioBay > DATEADD(HOUR, 2, GETDATE());
        IF @GiaCoBanMoi IS NULL OR @MaHangGheMoi IS NULL OR @HangGheThucTe <> @MaHangGheMoi
            THROW 51008, N'Chuyến bay hoặc ghế mới không hợp lệ.', 1;

        IF EXISTS (
            SELECT 1 FROM VeMayBay WITH (UPDLOCK, HOLDLOCK)
            WHERE MaChuyenBay = @MaChuyenBayMoi AND MaGhe = @MaGheMoi AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
        BEGIN
            RAISERROR(N'Ghế mới trên chuyến bay đã có người chọn!', 16, 1);
        END

        DECLARE @GiaVeMoi money = dbo.fn_TinhGiaVeThucTe(@GiaCoBanMoi, @HangGheThucTe);
        DECLARE @PhiChenhLech money = @GiaVeMoi - @GiaVeCu;
        IF @PhiChenhLech < 0 SET @PhiChenhLech = 0;

        DECLARE @PhiDoiVe money = 150000; -- Phí dịch vụ cố định
        DECLARE @TongPhiPhaiDong money = @PhiChenhLech + @PhiDoiVe;

        UPDATE VeMayBay SET TrangThai = N'Đã đổi' WHERE MaVe = @MaVeCu;

        DECLARE @MaVeMoi varchar(20) = 'RE' + LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 18);
        INSERT INTO VeMayBay (MaVe, MaDatCho, MaChuyenBay, MaGhe, MaHangGhe, TenHanhKhach, CCCD_HanhKhach, LoaiHanhKhach, GiaVe, DichVuPhuTro, PhiPhuTro, TrangThai)
        VALUES (@MaVeMoi, @MaDatCho, @MaChuyenBayMoi, @MaGheMoi, @MaHangGheMoi, @TenHK, @CCCD, @LoaiHK,
                @GiaVeMoi, @MaDichVu, @PhiPhuTro, N'Đã xuất vé');

        INSERT INTO YeuCauHuyDoiVe (MaVe, LoaiYeuCau, NgayYeuCau, LyDo, MaChuyenBayMoi, MaGheMoi, PhiChenhLech, PhiPhat, TrangThai, MaNVXuLy, NgayXuLy)
        VALUES (@MaVeCu, N'Đổi vé', GETDATE(), N'Đổi sang chuyến bay ' + @MaChuyenBayMoi, @MaChuyenBayMoi, @MaGheMoi, @PhiChenhLech, @PhiDoiVe, N'Đã hoàn tất', @MaNVXuLy, GETDATE());

        COMMIT TRANSACTION;
        SELECT @MaVeMoi AS MaVeMoi, @PhiChenhLech AS TienChenhLechGia, @PhiDoiVe AS PhiDichVuDoi, @TongPhiPhaiDong AS TongTienThanhToanThem;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 6. SP Đăng ký & Đăng nhập Khách hàng (Thao tác trực tiếp trên bảng KhachHang)
CREATE FUNCTION fn_ChuanHoaSoDienThoai(@SoDienThoai varchar(100))
RETURNS varchar(100)
AS
BEGIN
    DECLARE @So varchar(100) = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@SoDienThoai,
        ' ', ''), '-', ''), '.', ''), '(', ''), ')', '');
    SET @So = REPLACE(REPLACE(REPLACE(@So, CHAR(9), ''), CHAR(10), ''), CHAR(13), '');

    DECLARE @SoSauMaVN varchar(100);
    IF LEFT(@So, 3) = '+84'
        SET @SoSauMaVN = SUBSTRING(@So, 4, 100);
    ELSE IF LEFT(@So, 4) = '0084'
        SET @SoSauMaVN = SUBSTRING(@So, 5, 100);

    IF LEN(@SoSauMaVN) IN (9, 10) AND LEFT(@SoSauMaVN, 1) <> '0'
        AND @SoSauMaVN COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%'
        RETURN '0' + @SoSauMaVN;

    RETURN @So;
END;
GO

CREATE PROCEDURE sp_DangKyKhachHang
    @TenKH            nvarchar(50),
    @GioiTinh         nvarchar(10)  = N'Nam',
    @NgaySinh         date          = '2000-01-01',
    @SDT              varchar(15),
    @Email            varchar(100),
    @MatKhau          varchar(255),
    @DiaChi           nvarchar(200) = NULL,
    @LoaiKhachHang    nvarchar(30)  = N'Việt Nam',
    @CCCD             varchar(20)   = NULL,
    @HoChieu          varchar(30)   = NULL,
    @QuocTich         nvarchar(50)  = N'Việt Nam',
    @CoQuanCongTac    nvarchar(150) = NULL,
    @SoTK_DoanhNghiep varchar(30)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        SET @SDT = dbo.fn_ChuanHoaSoDienThoai(@SDT);
        IF EXISTS (SELECT 1 FROM dbo.KhachHang
                   WHERE Email = @Email OR dbo.fn_ChuanHoaSoDienThoai(SDT) = @SDT)
        BEGIN
            RAISERROR(N'Email hoặc Số điện thoại này đã được đăng ký trong hệ thống!', 16, 1);
        END

        IF @CCCD IS NOT NULL AND EXISTS (SELECT 1 FROM KhachHang WHERE CCCD = @CCCD)
        BEGIN
            RAISERROR(N'Số CCCD này đã tồn tại trong hệ thống!', 16, 1);
        END

        DECLARE @MaKH varchar(10) = 'KH' + LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 8);

        INSERT INTO KhachHang (MaKH, TenKH, GioiTinh, NgaySinh, SDT, Email, MatKhau, DiaChi, LoaiKhachHang, CCCD, HoChieu, QuocTich, CoQuanCongTac, SoTK_DoanhNghiep, TrangThaiKhoa, NgayDangKy)
        VALUES (@MaKH, @TenKH, @GioiTinh, @NgaySinh, @SDT, @Email, @MatKhau, @DiaChi, @LoaiKhachHang, @CCCD, @HoChieu, @QuocTich, @CoQuanCongTac, @SoTK_DoanhNghiep, 0, GETDATE());

        COMMIT TRANSACTION;
        SELECT @MaKH AS MaKH, N'Đăng ký tài khoản thành công!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE sp_DangNhapKhachHang
    @TaiKhoan varchar(100) -- Email hoặc SĐT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaKH varchar(10);
    SELECT TOP (1) @MaKH = MaKH
    FROM dbo.KhachHang
    WHERE Email = @TaiKhoan OR SDT = @TaiKhoan
    ORDER BY CASE WHEN Email = @TaiKhoan THEN 0 ELSE 1 END;

    IF @MaKH IS NULL AND CHARINDEX('@', @TaiKhoan) = 0
    BEGIN
        DECLARE @So varchar(100) = dbo.fn_ChuanHoaSoDienThoai(@TaiKhoan);
        -- Với hồ sơ cũ trùng sau chuẩn hóa, chỉ nhận số đã lưu chính xác hoặc email.
        SELECT @MaKH = MIN(MaKH)
        FROM dbo.KhachHang
        WHERE dbo.fn_ChuanHoaSoDienThoai(SDT) = @So
        HAVING COUNT(*) = 1;
    END;

    SELECT 
        kh.MaKH,
        kh.TenKH,
        kh.GioiTinh,
        kh.NgaySinh,
        kh.Email,
        kh.SDT,
        kh.MatKhau,
        kh.DiaChi,
        kh.LoaiKhachHang,
        kh.CCCD,
        kh.HoChieu,
        kh.QuocTich,
        kh.CoQuanCongTac,
        kh.SoTK_DoanhNghiep,
        kh.TrangThaiKhoa,
        kh.NgayDangKy
    FROM KhachHang kh
    WHERE kh.MaKH = @MaKH AND kh.TrangThaiKhoa = 0;
END;
GO

-- 7. SP Tự động hủy đơn quá hạn giữ chỗ (Quét bằng CURSOR)
CREATE PROCEDURE sp_TuDongHuyDonGiuChoHetHan_Cursor
    @MaChuyenBay varchar(20) = NULL,
    @GheYeuCau ChiTietDatVe_Type READONLY
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @MaDatCho varchar(15), @MaKM varchar(20), @SoLoi int = 0, @LoiDau int,
            @HanHetHan datetime = GETDATE(), @TranBanDau int = @@TRANCOUNT;

    DECLARE cur_DonHetHan CURSOR LOCAL FAST_FORWARD FOR
        SELECT p.MaDatCho
        FROM dbo.PhieuDatCho p
        WHERE p.TrangThai = N'Chờ thanh toán' AND p.HanThanhToan <= @HanHetHan
          AND (@MaChuyenBay IS NULL OR EXISTS (
              SELECT 1 FROM dbo.VeMayBay v
              WHERE v.MaDatCho = p.MaDatCho AND v.MaChuyenBay = @MaChuyenBay AND v.TrangThai = N'Đã đặt chỗ'
                AND EXISTS (SELECT 1 FROM @GheYeuCau g WHERE g.MaGhe = v.MaGhe)
          ))
        ORDER BY p.MaDatCho;

    OPEN cur_DonHetHan;
    FETCH NEXT FROM cur_DonHetHan INTO @MaDatCho;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            SET @MaKM = NULL;
            SELECT @MaKM = MaKM FROM dbo.PhieuDatCho WITH (UPDLOCK, HOLDLOCK)
            WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ thanh toán' AND HanThanhToan <= @HanHetHan;

            UPDATE dbo.PhieuDatCho SET TrangThai = N'Hết hạn giữ chỗ' WHERE MaDatCho = @MaDatCho
                AND TrangThai = N'Chờ thanh toán' AND HanThanhToan <= @HanHetHan;
            IF @@ROWCOUNT > 0
            BEGIN
                UPDATE dbo.VeMayBay SET TrangThai = N'Đã hủy'
                WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã đặt chỗ';
                UPDATE dbo.KhuyenMai SET SoLuong = SoLuong + 1 WHERE MaKM = @MaKM;
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            IF @TranBanDau > 0 OR @MaChuyenBay IS NOT NULL
                THROW;

            SET @SoLoi += 1;
            SET @LoiDau = ISNULL(@LoiDau, ERROR_NUMBER());
        END CATCH;

        FETCH NEXT FROM cur_DonHetHan INTO @MaDatCho;
    END;

    CLOSE cur_DonHetHan;
    DEALLOCATE cur_DonHetHan;

    IF @SoLoi > 0
    BEGIN
        DECLARE @ThongBao nvarchar(2048) = CONCAT(N'Không giải phóng được ', @SoLoi,
            N' đơn hết hạn. Mã lỗi SQL đầu tiên ', @LoiDau, N'.');
        THROW 51028, @ThongBao, 1;
    END;
END;
GO

-- ====================================================================================================
-- PHẦN 8: CÁC KHUNG NHÌN DỮ LIỆU (VIEWS)
-- ====================================================================================================

-- 1. View Danh sách chuyến bay chi tiết
CREATE VIEW vw_DanhSachChuyenBayChiTiet AS
SELECT 
    cb.MaChuyenBay,
    hhk.MaHang,
    hhk.TenHang AS TenHangHangKhong,
    hhk.Logo,
    mb.MaMayBay,
    mb.TenMayBay,
    tb.MaTuyenBay,
    sbDi.MaSanBay  AS MaSanBayDi,
    sbDi.TenSanBay AS TenSanBayDi,
    sbDi.ThanhPho  AS ThanhPhoDi,
    sbDen.MaSanBay  AS MaSanBayDen,
    sbDen.TenSanBay AS TenSanBayDen,
    sbDen.ThanhPho  AS ThanhPhoDen,
    cb.NgayGioBay,
    cb.NgayGioDenDK,
    cb.ThoiGianBay,
    cb.GiaVeCoBan,
    available.SoGheTrong,
    mb.TongSoGhe,
    CASE WHEN cb.TrangThai IN (N'Đang mở bán', N'Hết vé')
         THEN CASE WHEN available.SoGheTrong > 0 THEN N'Đang mở bán' ELSE N'Hết vé' END
         ELSE cb.TrangThai END AS TrangThaiChuyenBay
FROM ChuyenBay cb
JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
JOIN HangHangKhong hhk ON mb.MaHang = hhk.MaHang
CROSS APPLY (
    SELECT COUNT(*) AS SoGheTrong
    FROM dbo.GheNgoi g
    WHERE g.MaMayBay = cb.MaMayBay AND g.TrangThai = 1 AND NOT EXISTS (
        SELECT 1 FROM dbo.vw_VeMayBayChiemGhe v
        WHERE v.MaChuyenBay = cb.MaChuyenBay AND v.MaGhe = g.MaGhe
    )
) available;
GO

-- 2. View Lịch sử đặt vé và trạng thái thanh toán
CREATE VIEW vw_LichSuDatVeKhachHang AS
SELECT 
    pdc.MaDatCho,
    pdc.NgayDat,
    kh.MaKH,
    kh.TenKH,
    kh.SDT,
    kh.Email,
    pdc.TongTienGoc,
    pdc.TienGiam,
    pdc.MaKM,
    pdc.TongTienSauGiam,
    pdc.HanThanhToan,
    pdc.TrangThai AS TrangThaiDonHang,
    hd.MaHD,
    hd.PhuongThuc AS PhuongThucThanhToan,
    hd.NgayThanhToan,
    hd.MaGiaoDichNganHang
FROM PhieuDatCho pdc
JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
LEFT JOIN HoaDon hd ON pdc.MaDatCho = hd.MaDatCho;
GO

-- 3. View Chi tiết vé & Thẻ lên máy bay
CREATE VIEW vw_ChiTietVeVaHanhKhach AS
SELECT 
    v.MaVe,
    v.MaDatCho,
    v.TenHanhKhach,
    v.CCCD_HanhKhach,
    v.LoaiHanhKhach,
    v.GiaVe,
    v.DichVuPhuTro,
    v.PhiPhuTro,
    v.TrangThai AS TrangThaiVe,
    hg.TenHangGhe,
    gn.SoGhe,
    cb.MaChuyenBay,
    hhk.TenHang AS HangBay,
    sbDi.TenSanBay  AS SanBayDi,
    sbDi.ThanhPho   AS ThanhPhoDi,
    sbDen.TenSanBay AS SanBayDen,
    sbDen.ThanhPho  AS ThanhPhoDen,
    cb.NgayGioBay,
    cb.NgayGioDenDK
FROM VeMayBay v
JOIN HangGhe hg ON v.MaHangGhe = hg.MaHangGhe
JOIN GheNgoi gn ON v.MaGhe = gn.MaGhe
JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
JOIN HangHangKhong hhk ON mb.MaHang = hhk.MaHang
JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay;
GO

-- 4. View Thống kê Doanh thu theo Hãng Hàng Không
CREATE VIEW vw_BaoCaoDoanhThuTheoHang AS
SELECT 
    hhk.MaHang,
    hhk.TenHang,
    COUNT(DISTINCT v.MaVe) AS TongSoVeDaBan,
    SUM(v.GiaVe)           AS TongDoanhThuVe
FROM HangHangKhong hhk
JOIN MayBay mb ON hhk.MaHang = mb.MaHang
JOIN ChuyenBay cb ON mb.MaMayBay = cb.MaMayBay
JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay
WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
GROUP BY hhk.MaHang, hhk.TenHang;
GO

-- 5. View Top các Tuyến Bay đặt nhiều nhất
CREATE VIEW vw_TopTuyenBayDatNhieuNhat AS
SELECT TOP 10
    tb.MaTuyenBay,
    sbDi.ThanhPho + N' -> ' + sbDen.ThanhPho AS ChangBay,
    COUNT(v.MaVe) AS SoLuongVeBan,
    SUM(v.GiaVe)  AS TongDoanhThu
FROM TuyenBay tb
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
JOIN ChuyenBay cb ON tb.MaTuyenBay = cb.MaTuyenBay
JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay
WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
GROUP BY tb.MaTuyenBay, sbDi.ThanhPho, sbDen.ThanhPho
ORDER BY SoLuongVeBan DESC;
GO

-- ====================================================================================================
-- PHẦN 9: NẠP DỮ LIỆU MẪU TOÀN DIỆN (SEED DATA - 100% HÀNG KHÔNG NỘI ĐỊA VIỆT NAM)
-- ====================================================================================================

-- 1. Hãng Hàng Không (5 Hãng Hàng Không Nội Địa Việt Nam)
INSERT INTO HangHangKhong (MaHang, TenHang, QuocGia, Logo, TrangThai) VALUES
('VN', N'Vietnam Airlines',               N'Việt Nam', '/images/vietnam_airlines.png', 1),
('VJ', N'Vietjet Air',                     N'Việt Nam', '/images/vietjet_air.png',     1),
('QH', N'Bamboo Airways',                  N'Việt Nam', '/images/bamboo_airways.png',  1),
('VU', N'Vietravel Airlines',              N'Việt Nam', '/images/vietravel.png',       1),
('0V', N'VASCO (Hàng không Côn Đảo/Vùng)', N'Việt Nam', '/images/vasco.png',           1);
GO

-- 2. Sân Bay (15 Cảng Hàng Không Nội Địa Việt Nam)
INSERT INTO SanBay (MaSanBay, TenSanBay, ThanhPho, QuocGia, TrangThai) VALUES
('SGN', N'Sân bay Quốc tế Tân Sơn Nhất', N'TP. Hồ Chí Minh',   N'Việt Nam', 1),
('HAN', N'Sân bay Quốc tế Nội Bài',      N'Hà Nội',             N'Việt Nam', 1),
('DAD', N'Sân bay Quốc tế Đà Nẵng',      N'Đà Nẵng',            N'Việt Nam', 1),
('CXR', N'Sân bay Quốc tế Cam Ranh',     N'Khánh Hòa',          N'Việt Nam', 1),
('PQC', N'Sân bay Quốc tế Phú Quốc',     N'Kiên Giang',         N'Việt Nam', 1),
('HPH', N'Sân bay Quốc tế Cát Bi',       N'Hải Phòng',          N'Việt Nam', 1),
('VCA', N'Sân bay Quốc tế Cần Thơ',      N'Cần Thơ',            N'Việt Nam', 1),
('VII', N'Sân bay Quốc tế Vinh',         N'Nghệ An',            N'Việt Nam', 1),
('HUI', N'Sân bay Quốc tế Phú Bài',      N'Thừa Thiên Huế',     N'Việt Nam', 1),
('UIH', N'Sân bay Phù Cát',              N'Bình Định',          N'Việt Nam', 1),
('DLI', N'Sân bay Liên Khương',          N'Lâm Đồng',           N'Việt Nam', 1),
('VCS', N'Sân bay Côn Đảo',              N'Bà Rịa - Vũng Tàu',  N'Việt Nam', 1),
('DIN', N'Sân bay Điện Biên Phủ',        N'Điện Biên',          N'Việt Nam', 1),
('BMV', N'Sân bay Buôn Ma Thuột',        N'Đắk Lắk',            N'Việt Nam', 1),
('THD', N'Sân bay Thọ Xuân',             N'Thanh Hóa',          N'Việt Nam', 1);
GO

-- 3. Tuyến Bay (20 Tuyến bay nội địa kết nối 3 miền Bắc - Trung - Nam)
INSERT INTO TuyenBay (MaTuyenBay, MaSanBayDi, MaSanBayDen, KhoangCachKm) VALUES
('SGN-HAN', 'SGN', 'HAN', 1160.00),
('HAN-SGN', 'HAN', 'SGN', 1160.00),
('SGN-DAD', 'SGN', 'DAD', 605.00),
('DAD-SGN', 'DAD', 'SGN', 605.00),
('HAN-DAD', 'HAN', 'DAD', 630.00),
('DAD-HAN', 'DAD', 'HAN', 630.00),
('SGN-PQC', 'SGN', 'PQC', 300.00),
('PQC-SGN', 'PQC', 'SGN', 300.00),
('HAN-PQC', 'HAN', 'PQC', 1230.00),
('PQC-HAN', 'PQC', 'HAN', 1230.00),
('SGN-CXR', 'SGN', 'CXR', 310.00),
('CXR-SGN', 'CXR', 'SGN', 310.00),
('HAN-CXR', 'HAN', 'CXR', 1050.00),
('SGN-DLI', 'SGN', 'DLI', 220.00),
('HAN-HUI', 'HAN', 'HUI', 540.00),
('SGN-VCS', 'SGN', 'VCS', 230.00),
('VCS-SGN', 'VCS', 'SGN', 230.00),
('HAN-DIN', 'HAN', 'DIN', 290.00),
('HPH-VCS', 'HPH', 'VCS', 1370.00),
('DIN-PQC', 'DIN', 'PQC', 1520.00);
GO

-- 4. Hạng Ghế
INSERT INTO HangGhe (MaHangGhe, TenHangGhe, HeSoGia, MoTa) VALUES
('ECO', N'Hạng Phổ Thông', 1.00, N'Hành lý xách tay 12kg, suất ăn nhẹ tiêu chuẩn'),
('BUS', N'Hạng Thương Gia', 2.00, N'Phòng chờ Bông Sen, ghế ngả 180 độ, ưu tiên check-in, hành lý 30kg'),
('FIR', N'Hạng Nhất',      3.50, N'Khoang riêng tư cao cấp, ẩm thực thượng hạng, đưa đón xe sang');
GO

-- 5. Máy Bay (10 Tàu bay phục vụ mạng lưới nội địa)
INSERT INTO MayBay (MaMayBay, TenMayBay, MaHang, TongSoGhe, TrangThai) VALUES
('VN-A888', N'Boeing 787-9 Dreamliner', 'VN', 28, N'Sẵn sàng'),
('VN-A350', N'Airbus A350-900',         'VN', 28, N'Sẵn sàng'),
('VN-A321', N'Airbus A321 Neo',         'VN', 28, N'Sẵn sàng'),
('VJ-A321', N'Airbus A321 Neo',         'VJ', 28, N'Sẵn sàng'),
('VJ-A330', N'Airbus A330-300',         'VJ', 28, N'Sẵn sàng'),
('QH-A320', N'Airbus A320 Neo',         'QH', 28, N'Sẵn sàng'),
('QH-A787', N'Boeing 787-9 Dreamliner', 'QH', 28, N'Sẵn sàng'),
('VU-A321', N'Airbus A321-200',         'VU', 28, N'Sẵn sàng'),
('VU-A320', N'Airbus A320-200',         'VU', 28, N'Sẵn sàng'),
('0V-A072', N'ATR 72-600 Côn Đảo',      '0V', 28, N'Sẵn sàng');
GO

-- 6. Tạo Sơ Đồ Ghế Ngồi (Ghế Thương gia: 01A-02D, Phổ thông: 10A-15D)
-- Ghế cho VN-A888 (IDs: 1 - 28)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai) VALUES
('VN-A888', '01A', 'BUS', 1), ('VN-A888', '01B', 'BUS', 1), ('VN-A888', '01C', 'BUS', 1), ('VN-A888', '01D', 'BUS', 1),
('VN-A888', '02A', 'BUS', 1), ('VN-A888', '02B', 'BUS', 1), ('VN-A888', '02C', 'BUS', 1), ('VN-A888', '02D', 'BUS', 1),
('VN-A888', '10A', 'ECO', 1), ('VN-A888', '10B', 'ECO', 1), ('VN-A888', '10C', 'ECO', 1), ('VN-A888', '10D', 'ECO', 1),
('VN-A888', '11A', 'ECO', 1), ('VN-A888', '11B', 'ECO', 1), ('VN-A888', '11C', 'ECO', 1), ('VN-A888', '11D', 'ECO', 1),
('VN-A888', '12A', 'ECO', 1), ('VN-A888', '12B', 'ECO', 1), ('VN-A888', '12C', 'ECO', 1), ('VN-A888', '12D', 'ECO', 1),
('VN-A888', '14A', 'ECO', 1), ('VN-A888', '14B', 'ECO', 1), ('VN-A888', '14C', 'ECO', 1), ('VN-A888', '14D', 'ECO', 1),
('VN-A888', '15A', 'ECO', 1), ('VN-A888', '15B', 'ECO', 1), ('VN-A888', '15C', 'ECO', 1), ('VN-A888', '15D', 'ECO', 1);

-- Ghế cho VN-A350 (IDs: 29 - 56)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai) VALUES
('VN-A350', '01A', 'BUS', 1), ('VN-A350', '01B', 'BUS', 1), ('VN-A350', '01C', 'BUS', 1), ('VN-A350', '01D', 'BUS', 1),
('VN-A350', '02A', 'BUS', 1), ('VN-A350', '02B', 'BUS', 1), ('VN-A350', '02C', 'BUS', 1), ('VN-A350', '02D', 'BUS', 1),
('VN-A350', '10A', 'ECO', 1), ('VN-A350', '10B', 'ECO', 1), ('VN-A350', '10C', 'ECO', 1), ('VN-A350', '10D', 'ECO', 1),
('VN-A350', '11A', 'ECO', 1), ('VN-A350', '11B', 'ECO', 1), ('VN-A350', '11C', 'ECO', 1), ('VN-A350', '11D', 'ECO', 1),
('VN-A350', '12A', 'ECO', 1), ('VN-A350', '12B', 'ECO', 1), ('VN-A350', '12C', 'ECO', 1), ('VN-A350', '12D', 'ECO', 1),
('VN-A350', '14A', 'ECO', 1), ('VN-A350', '14B', 'ECO', 1), ('VN-A350', '14C', 'ECO', 1), ('VN-A350', '14D', 'ECO', 1),
('VN-A350', '15A', 'ECO', 1), ('VN-A350', '15B', 'ECO', 1), ('VN-A350', '15C', 'ECO', 1), ('VN-A350', '15D', 'ECO', 1);

-- Ghế cho VJ-A321 (IDs: 57 - 84)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai) VALUES
('VJ-A321', '01A', 'BUS', 1), ('VJ-A321', '01B', 'BUS', 1), ('VJ-A321', '01C', 'BUS', 1), ('VJ-A321', '01D', 'BUS', 1),
('VJ-A321', '02A', 'BUS', 1), ('VJ-A321', '02B', 'BUS', 1), ('VJ-A321', '02C', 'BUS', 1), ('VJ-A321', '02D', 'BUS', 1),
('VJ-A321', '10A', 'ECO', 1), ('VJ-A321', '10B', 'ECO', 1), ('VJ-A321', '10C', 'ECO', 1), ('VJ-A321', '10D', 'ECO', 1),
('VJ-A321', '11A', 'ECO', 1), ('VJ-A321', '11B', 'ECO', 1), ('VJ-A321', '11C', 'ECO', 1), ('VJ-A321', '11D', 'ECO', 1),
('VJ-A321', '12A', 'ECO', 1), ('VJ-A321', '12B', 'ECO', 1), ('VJ-A321', '12C', 'ECO', 1), ('VJ-A321', '12D', 'ECO', 1),
('VJ-A321', '14A', 'ECO', 1), ('VJ-A321', '14B', 'ECO', 1), ('VJ-A321', '14C', 'ECO', 1), ('VJ-A321', '14D', 'ECO', 1),
('VJ-A321', '15A', 'ECO', 1), ('VJ-A321', '15B', 'ECO', 1), ('VJ-A321', '15C', 'ECO', 1), ('VJ-A321', '15D', 'ECO', 1);

-- Ghế cho 0V-A072 (IDs: 85 - 112)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai) VALUES
('0V-A072', '01A', 'BUS', 1), ('0V-A072', '01B', 'BUS', 1), ('0V-A072', '01C', 'BUS', 1), ('0V-A072', '01D', 'BUS', 1),
('0V-A072', '02A', 'BUS', 1), ('0V-A072', '02B', 'BUS', 1), ('0V-A072', '02C', 'BUS', 1), ('0V-A072', '02D', 'BUS', 1),
('0V-A072', '10A', 'ECO', 1), ('0V-A072', '10B', 'ECO', 1), ('0V-A072', '10C', 'ECO', 1), ('0V-A072', '10D', 'ECO', 1),
('0V-A072', '11A', 'ECO', 1), ('0V-A072', '11B', 'ECO', 1), ('0V-A072', '11C', 'ECO', 1), ('0V-A072', '11D', 'ECO', 1),
('0V-A072', '12A', 'ECO', 1), ('0V-A072', '12B', 'ECO', 1), ('0V-A072', '12C', 'ECO', 1), ('0V-A072', '12D', 'ECO', 1),
('0V-A072', '14A', 'ECO', 1), ('0V-A072', '14B', 'ECO', 1), ('0V-A072', '14C', 'ECO', 1), ('0V-A072', '14D', 'ECO', 1),
('0V-A072', '15A', 'ECO', 1), ('0V-A072', '15B', 'ECO', 1), ('0V-A072', '15C', 'ECO', 1), ('0V-A072', '15D', 'ECO', 1);

-- Ghế cho các máy bay còn lại (VN-A321, VJ-A330, QH-A320, QH-A787, VU-A321, VU-A320)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai) VALUES
('VN-A321', '01A', 'BUS', 1), ('VN-A321', '01B', 'BUS', 1), ('VN-A321', '01C', 'BUS', 1), ('VN-A321', '01D', 'BUS', 1),
('VN-A321', '02A', 'BUS', 1), ('VN-A321', '02B', 'BUS', 1), ('VN-A321', '02C', 'BUS', 1), ('VN-A321', '02D', 'BUS', 1),
('VN-A321', '10A', 'ECO', 1), ('VN-A321', '10B', 'ECO', 1), ('VN-A321', '10C', 'ECO', 1), ('VN-A321', '10D', 'ECO', 1),
('VN-A321', '11A', 'ECO', 1), ('VN-A321', '11B', 'ECO', 1), ('VN-A321', '11C', 'ECO', 1), ('VN-A321', '11D', 'ECO', 1),
('VN-A321', '12A', 'ECO', 1), ('VN-A321', '12B', 'ECO', 1), ('VN-A321', '12C', 'ECO', 1), ('VN-A321', '12D', 'ECO', 1),
('VN-A321', '14A', 'ECO', 1), ('VN-A321', '14B', 'ECO', 1), ('VN-A321', '14C', 'ECO', 1), ('VN-A321', '14D', 'ECO', 1),
('VN-A321', '15A', 'ECO', 1), ('VN-A321', '15B', 'ECO', 1), ('VN-A321', '15C', 'ECO', 1), ('VN-A321', '15D', 'ECO', 1),

('QH-A320', '01A', 'BUS', 1), ('QH-A320', '01B', 'BUS', 1), ('QH-A320', '01C', 'BUS', 1), ('QH-A320', '01D', 'BUS', 1),
('QH-A320', '02A', 'BUS', 1), ('QH-A320', '02B', 'BUS', 1), ('QH-A320', '02C', 'BUS', 1), ('QH-A320', '02D', 'BUS', 1),
('QH-A320', '10A', 'ECO', 1), ('QH-A320', '10B', 'ECO', 1), ('QH-A320', '10C', 'ECO', 1), ('QH-A320', '10D', 'ECO', 1),
('QH-A320', '11A', 'ECO', 1), ('QH-A320', '11B', 'ECO', 1), ('QH-A320', '11C', 'ECO', 1), ('QH-A320', '11D', 'ECO', 1),
('QH-A320', '12A', 'ECO', 1), ('QH-A320', '12B', 'ECO', 1), ('QH-A320', '12C', 'ECO', 1), ('QH-A320', '12D', 'ECO', 1),
('QH-A320', '14A', 'ECO', 1), ('QH-A320', '14B', 'ECO', 1), ('QH-A320', '14C', 'ECO', 1), ('QH-A320', '14D', 'ECO', 1),
('QH-A320', '15A', 'ECO', 1), ('QH-A320', '15B', 'ECO', 1), ('QH-A320', '15C', 'ECO', 1), ('QH-A320', '15D', 'ECO', 1);
GO

-- 7. Chuyến Bay (20 Chuyến bay nội địa)
INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, TrangThai)
SELECT mb.MaMayBay, mau.SoGhe, mau.MaHangGhe, 1
FROM MayBay mb CROSS JOIN GheNgoi mau
WHERE mau.MaMayBay = 'VN-A888'
    AND NOT EXISTS (SELECT 1 FROM GheNgoi g WHERE g.MaMayBay = mb.MaMayBay);
GO

INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai) VALUES
-- Tuyến SGN -> HAN
('VN120', 'SGN-HAN', 'VN-A888', '2026-09-01 08:00:00', '2026-09-01 10:15:00', 135, 1500000, 28, N'Đang mở bán'),
('VN122', 'SGN-HAN', 'VN-A350', '2026-09-01 14:00:00', '2026-09-01 16:15:00', 135, 1650000, 28, N'Đang mở bán'),
('VJ150', 'SGN-HAN', 'VJ-A321', '2026-09-01 06:30:00', '2026-09-01 08:45:00', 135, 1150000, 28, N'Đang mở bán'),
('QH202', 'SGN-HAN', 'QH-A320', '2026-09-01 18:00:00', '2026-09-01 20:15:00', 135, 1400000, 28, N'Đang mở bán'),
-- Tuyến HAN -> SGN
('VN121', 'HAN-SGN', 'VN-A888', '2026-09-02 11:30:00', '2026-09-02 13:45:00', 135, 1550000, 28, N'Đang mở bán'),
('VJ151', 'HAN-SGN', 'VJ-A321', '2026-09-02 09:15:00', '2026-09-02 11:30:00', 135, 1200000, 28, N'Đang mở bán'),
-- Tuyến SGN -> DAD
('VN214', 'SGN-DAD', 'VN-A321', '2026-09-01 07:15:00', '2026-09-01 08:35:00', 80,  1250000, 28, N'Đang mở bán'),
('VJ201', 'SGN-DAD', 'VJ-A321', '2026-09-01 09:30:00', '2026-09-01 10:50:00', 80,  950000,  28, N'Đang mở bán'),
('QH301', 'SGN-DAD', 'QH-A320', '2026-09-01 16:45:00', '2026-09-01 18:05:00', 80,  1100000, 28, N'Đang mở bán'),
-- Tuyến HAN -> DAD
('VN310', 'HAN-DAD', 'VN-A321', '2026-09-02 08:00:00', '2026-09-02 09:20:00', 80,  1300000, 28, N'Đang mở bán'),
('VJ312', 'HAN-DAD', 'VJ-A321', '2026-09-02 13:30:00', '2026-09-02 14:50:00', 80,  980000,  28, N'Đang mở bán'),
-- Tuyến Du Lịch: Phú Quốc, Nha Trang, Đà Lạt, Côn Đảo, Điện Biên
('VN450', 'SGN-PQC', 'VN-A321', '2026-09-03 07:00:00', '2026-09-03 08:00:00', 60,  1150000, 28, N'Đang mở bán'),
('VJ452', 'SGN-PQC', 'VJ-A321', '2026-09-03 10:20:00', '2026-09-03 11:20:00', 60,  850000,  28, N'Đang mở bán'),
('VN510', 'SGN-CXR', 'VN-A321', '2026-09-03 12:00:00', '2026-09-03 13:05:00', 65,  1050000, 28, N'Đang mở bán'),
('VJ620', 'SGN-DLI', 'VJ-A321', '2026-09-04 06:45:00', '2026-09-04 07:40:00', 55,  750000,  28, N'Đang mở bán'),
('0V101', 'SGN-VCS', '0V-A072', '2026-09-04 08:15:00', '2026-09-04 09:10:00', 55,  1850000, 28, N'Đang mở bán'),
('0V102', 'VCS-SGN', '0V-A072', '2026-09-04 14:30:00', '2026-09-04 15:25:00', 55,  1850000, 28, N'Đang mở bán'),
('VN720', 'HAN-DIN', 'VN-A321', '2026-09-05 09:30:00', '2026-09-05 10:30:00', 60,  1200000, 28, N'Đang mở bán'),
-- Tuyến Quá Cảnh / Nối Chuyến Nội Địa (Transit)
('VN850', 'HPH-VCS', 'VN-A321', '2026-09-05 06:00:00', '2026-09-05 10:30:00', 270, 2950000, 28, N'Đang mở bán'),
('QH500', 'DIN-PQC', 'QH-A320', '2026-09-06 07:30:00', '2026-09-06 12:45:00', 315, 2750000, 28, N'Đang mở bán');
GO

-- 8. Sân Bay Trung Gian Nội Địa (Transit / Stopover)
INSERT INTO ChiTietChuyenBay (MaChuyenBay, MaSanBayTG, ThoiGianDung, ThuTuDung, GhiChu) VALUES
('VN850', 'SGN', 75, 1, N'Quá cảnh tại Tân Sơn Nhất (SGN) để nối chuyến ra Côn Đảo bằng tàu bay ATR72'),
('QH500', 'HAN', 90, 1, N'Nghỉ quá cảnh tại Nội Bài (HAN) và trung chuyển hành lý chặng Điện Biên - Phú Quốc');
GO

-- 9. Vai Trò & 10 Nhân Viên
INSERT INTO VaiTro (RoleID, TenRole, MoTa) VALUES
(1, N'Ban Giám Đốc',       N'Toàn quyền quản trị cơ sở dữ liệu và hệ thống đại lý vé'),
(2, N'Nhân Viên Thu Ngân',  N'Quản lý lập hóa đơn, đối soát công nợ và duyệt thanh toán'),
(3, N'Nhân Viên Bán Vé',    N'Tra cứu chuyến bay, giữ chỗ và tạo đơn đặt vé máy bay'),
(4, N'Nhân Viên CSKH',      N'Hỗ trợ khách hàng, giải quyết khiếu nại, hủy vé và đổi chuyến bay');
GO

INSERT INTO NhanVien (MaNV, TenNV, SDT, Email, ChucVu, Luong, TaiKhoan, MatKhau, RoleID, TrangThai) VALUES
('NV001', N'Nguyễn Văn Quản Trị', '0901000001', 'admin@airline.vn',   N'Trưởng phòng CNTT', 25000000, 'admin',    'PBKDF2:AQAAAAIAAYagAAAAEGH0wleIpOvf7fhEz4UKiWbzVbHxhX4jnvDkXSYNXoe2lzVVn+ZqkpyY2t71+36oRA==', 1, 1),
('NV002', N'Lê Thị Thu Ngân',     '0901000002', 'thungan@airline.vn', N'Kế toán trưởng',    18000000, 'thungan',  'PBKDF2:AQAAAAIAAYagAAAAEM7zvU/4aeOHic2OEMGa8jXezhEXHFre0/+PS/U/rH7ZZQE9MgOsmJ3mTb7COsKwMg==', 2, 1),
('NV003', N'Trần Văn Bán Vé',     '0901000003', 'banve@airline.vn',   N'Tổ trưởng bán vé',  14000000, 'banve',    'PBKDF2:AQAAAAIAAYagAAAAEB6jD9fLG5alVLftujKQBu3+BLywTvQV58k9xDwar5uKPe1hfKE6uP1vClIkIB4eAw==', 3, 1),
('NV004', N'Phạm Thị CSKH',       '0901000004', 'cskh@airline.vn',    N'Chuyên viên CSKH',  12000000, 'cskh',     'PBKDF2:AQAAAAIAAYagAAAAEO8et7TIIEoJigziqYiZRDYo80dKUVq8Vql1POahaktujLr19+5YtWakqwgpBEkUCA==', 4, 1),
('NV005', N'Hoàng Đức Minh',      '0901000005', 'minh.hd@airline.vn', N'Nhân viên bán vé',  10000000, 'banve02',  'PBKDF2:AQAAAAIAAYagAAAAEFqrDcrmu6lDc/4EF3/SXWVa86zGg8SzyKtfNbCKZkWBimo+TmgWOllCikIT5vlF9g==', 3, 1),
('NV006', N'Đỗ Mai Phương',       '0901000006', 'phuong.dm@airline.vn',N'Nhân viên thu ngân',11000000, 'thungan02','PBKDF2:AQAAAAIAAYagAAAAEBwsO5lf3yXKalLEFMWZ2gc4d4PT7onOU5QJ/pZl5JmWAf2hXbCBivqfA2Alx3vPOA==', 2, 1),
('NV007', N'Vũ Quốc Tuấn',        '0901000007', 'tuan.vq@airline.vn', N'Nhân viên CSKH',    10500000, 'cskh02',   'PBKDF2:AQAAAAIAAYagAAAAEIZ+YNR8WSwm4H2pzDlnglDVDztT677JuXnRrreTu8nz7Vn5NC5SbHBHGT+rSdDWnQ==', 4, 1),
('NV008', N'Bùi Thanh Trúc',      '0901000008', 'truc.bt@airline.vn', N'Nhân viên bán vé',  10000000, 'banve03',  'PBKDF2:AQAAAAIAAYagAAAAELhMTgPxOJekdxNnTs7+QP8+71TOLGKIvaE9vGQg0/svJlzBVIwVmMLNzgiENYswIA==', 3, 1),
('NV009', N'Đinh Hữu Tài',        '0901000009', 'tai.dh@airline.vn',  N'Quản trị cơ sở dữ liệu', 22000000, 'dba', 'PBKDF2:AQAAAAIAAYagAAAAEEdh954loMHhu5y+Jdi6LUfMwRcXWxu+Nj59Rm4vB8ZQ3fskcHC6mu8LtXyZdHpGYQ==', 1, 1),
('NV010', N'Ngô Bảo Châu',        '0901000010', 'chau.nb@airline.vn', N'Nhân viên CSKH',    10500000, 'cskh03',   'PBKDF2:AQAAAAIAAYagAAAAEGgv/pnXA+VMRv40VJTXqA842EiUfacGyvUDLbVkPlXltCzqvo90CzpouHZHUO4+Hg==', 4, 1);
GO

-- 10. Khách Hàng (15 Khách hàng: Việt Nam + Du khách quốc tế bay nội địa)
INSERT INTO KhachHang (MaKH, TenKH, GioiTinh, NgaySinh, SDT, Email, MatKhau, DiaChi, LoaiKhachHang, CCCD, HoChieu, QuocTich, CoQuanCongTac, SoTK_DoanhNghiep, TrangThaiKhoa) VALUES
-- Khách Việt Nam
('KH00001', N'Lê Duyên An',        N'Nam', '2004-05-15', '0911000001', 'duyenan@gmail.com',    'PBKDF2:AQAAAAIAAYagAAAAEJEcvi7ldLtUoCKJrBALE2cUGGz0x2yvPqE/KeEP7P1N+JKZRub96HAtSYdTw5tKig==', N'123 Nguyễn Huệ, Q1, TP. HCM',     N'Việt Nam',     '079204000111', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00002', N'Lê Nguyễn Đăng Khoa',N'Nam', '2004-08-20', '0911000002', 'dangkhoa@gmail.com',   'PBKDF2:AQAAAAIAAYagAAAAEGE9AtIZDgxvF4gzOY490L8NFje18GdugQuyH+fS0Q4062j9WSmIsGgwDJee9PZlYw==', N'456 Lê Duẩn, Q3, TP. HCM',        N'Việt Nam',     '079204000222', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00003', N'Lâm Ngọc Như',       N'Nữ',  '2004-11-10', '0911000003', 'ngocnhu@gmail.com',    'PBKDF2:AQAAAAIAAYagAAAAEINHT9hmKzWYn+ZDGeyDTm8zOQ+4CPL6N6QhK5paKQjWDeUUBCjhtCkz1lY12hYK4w==', N'789 Cách Mạng T8, Tân Bình, HCM', N'Việt Nam',     '079304000333', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00004', N'Trần Hoàng Nam',     N'Nam', '1995-02-14', '0911000004', 'hoangnam@gmail.com',   'PBKDF2:AQAAAAIAAYagAAAAEH8LqEX3ySxTmslCiWrzzLgcvEy+m7N9f0PE6xBcmIoeAbndVm+aWWcPaiqxN+2M0g==', N'12 Tràng Tiền, Hoàn Kiếm, Hà Nội',N'Việt Nam',     '001095000444', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00005', N'Nguyễn Thúy Hằng',   N'Nữ',  '1998-07-22', '0911000005', 'thuyhang@gmail.com',   'PBKDF2:AQAAAAIAAYagAAAAEMpaqoFMOBd/FZpWxNctQ1mE/V4EXN/KWkI6PPMQJkDM/rvvApd6YlI4IsXfyejV+A==', N'34 Bạch Đằng, Hải Châu, Đà Nẵng', N'Việt Nam',     '048098000555', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00006', N'Phan Thanh Tùng',    N'Nam', '1992-10-05', '0911000006', 'thanhtung@gmail.com',  'PBKDF2:AQAAAAIAAYagAAAAEC1hInQCSLGSmB8OrZDR/fiJFb+MM/lGY+/HmuRAqP/y5KllZmGPV8xKQOouGZh9Ug==', N'56 Hùng Vương, Ninh Kiều, Cần Thơ',N'Việt Nam',    '092092000666', NULL,        N'Việt Nam', NULL,                     NULL,           0),
('KH00007', N'Vũ Bích Phương',     N'Nữ',  '2000-12-18', '0911000007', 'bichphuong@gmail.com', 'PBKDF2:AQAAAAIAAYagAAAAECjx9RjAkYN+4RlAoDfrevCjyUbr/WL43HffaEMzAsRwwGDkhNnBj5vHtrCgQOi02A==', N'88 Lạch Tray, Ngô Quyền, Hải Phòng',N'Việt Nam',   '031300000777', NULL,        N'Việt Nam', NULL,                     NULL,           0),
-- Khách Nước Ngoài Du Lịch Nội Địa Việt Nam
('KH00008', N'John Smith',         N'Nam', '1988-03-25', '0911000008', 'johnsmith@gmail.com',  'PBKDF2:AQAAAAIAAYagAAAAEM+MqVysxV6ZAJzxrtjwdSf3jNzxDAG0kFXcL1x1Fspp3nDJo4Jcaz9V2sh0Jhn4hA==', N'Khách sạn Rex, Q1, TP. HCM',      N'Nước ngoài',   NULL,           'A99887766', N'Úc',       NULL,                     NULL,           0),
('KH00009', N'Michael Brown',      N'Nam', '1985-09-12', '0911000009', 'mbrown@yahoo.com',     'PBKDF2:AQAAAAIAAYagAAAAEAMyMBDXW+QDI/lA8eYmAX6oGi0flKVlQ6YdDpoUQtiTO/ePwDt75RjXKr1ZZ4VKaA==', N'Khách sạn Metropole, Hà Nội',     N'Nước ngoài',   NULL,           'US5544332', N'Mỹ',       NULL,                     NULL,           0),
('KH00010', N'Kenji Sato',         N'Nam', '1990-06-30', '0911000010', 'kenji.sato@sony.jp',   'PBKDF2:AQAAAAIAAYagAAAAEOgL4Hur9KJ44TBq6tC9pWukpaNFN2DZudiPoQpSIbL3uyFUnV7NbU3czEF31uLQaw==', N'Khách sạn Daewoo, Ba Đình, Hà Nội',N'Nước ngoài',  NULL,           'JP8877112', N'Nhật Bản', NULL,                     NULL,           0),
('KH00011', N'Emma Watson',        N'Nữ',  '1996-04-15', '0911000011', 'emma.w@oxford.uk',     'PBKDF2:AQAAAAIAAYagAAAAEJMO2vGsGivd07TX1CaisZqlOoc6iQ+mx/wQXOm1yiOJY0s9OD0radkfPcTkGRUKmA==', N'Resort Vinpearl, Phú Quốc',       N'Nước ngoài',   NULL,           'UK1122334', N'Anh',      NULL,                     NULL,           0),
-- Khách Doanh Nghiệp VIP
('KH00012', N'Phạm Nhật Vượng',    N'Nam', '1968-08-05', '0911000012', 'corp.vin@vingroup.net', 'PBKDF2:AQAAAAIAAYagAAAAEKRBkiVzoIg2/iMQDyYwZkJGDNmcGWrc1yBLWF1iZstAugY4IbbCeVADNTVfk2dTUg==', N'Số 7 Đường Bằng Lăng 1, Long Biên, Hà Nội', N'Doanh nghiệp', '001068000888', NULL, N'Việt Nam', N'Tập đoàn Vingroup',       '110000123456', 0),
('KH00013', N'Trương Gia Bình',    N'Nam', '1956-05-19', '0911000013', 'corp.fpt@fpt.com.vn',   'PBKDF2:AQAAAAIAAYagAAAAEENz/ttYp7wEgmgtvOqp04iMaCME6MMW7vQPzY50DHUshxj+NwRnakmcqWPWCpw+Pg==', N'Tòa nhà FPT, Cầu Giấy, Hà Nội',   N'Doanh nghiệp', '001056000999', NULL,        N'Việt Nam', N'Tập đoàn FPT',            '002100078910', 0),
('KH00014', N'Mai Kiều Liên',      N'Nữ',  '1953-09-01', '0911000014', 'corp.vnm@vinamilk.com', 'PBKDF2:AQAAAAIAAYagAAAAEKmH5DQ1dVf6c9PaY6kQRUty9TzPeSGRyJj3l7kQxVkBa9myQ/vTCF5iSlPTwR4fGw==', N'10 Tân Trào, Tân Phú, Q7, TP. HCM',N'Doanh nghiệp', '079053000112', NULL,        N'Việt Nam', N'Công ty CP Sữa Vinamilk',  '007100055588', 0),
('KH00015', N'Nguyễn Đăng Quang',  N'Nam', '1963-08-23', '0911000015', 'corp.msn@masan.vn',    'PBKDF2:AQAAAAIAAYagAAAAEIcdvKQDseP6a2ON9eJ5hzh5vsT3nhIpW6uBZOAXXNyLaEVyj+L4hOWz+OGMWIddpg==', N'Masan Tower, Hai Bà Trưng, Q1, HCM',N'Doanh nghiệp','079063000334', NULL,        N'Việt Nam', N'Tập đoàn Masan',          '018100099911', 0);
GO

-- 11. Mã Khuyến Mãi (8 Mã Voucher)
INSERT INTO KhuyenMai (MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu, NgayBatDau, NgayKetThuc, SoLuong, TrangThai) VALUES
('HE2026',       N'Chào Hè Rực Rỡ 2026',            10.00, 500000,   1000000, '2026-05-01', '2026-09-30', 500,  1),
('VIPBAY',       N'Đặc Quyền Dành Cho Khách VIP',   15.00, 1000000,  2000000, '2026-01-01', '2026-12-31', 200,  1),
('NEWFLYER',     N'Ưu Đãi Chào Đón Khách Hàng Mới', 5.00,  200000,   500000,  '2026-01-01', '2026-12-31', 1000, 1),
('TET2027',      N'Tết Sum Vầy - Đặt Vé Sớm',       12.00, 800000,   3000000, '2026-08-01', '2027-02-28', 300,  1),
('STUDENTFLY',   N'Tiếp Bước Tân Sinh Viên',        8.00,  300000,   800000,  '2026-08-01', '2026-11-30', 500,  1),
('BLACKFRIDAY',  N'Siêu Sale Black Friday Hàng Không',20.00,1500000, 2500000, '2026-11-20', '2026-11-30', 100,  1),
('AUTUMN2026',   N'Mùa Thu Hà Nội - Khám Phá Tây Bắc',7.00, 400000,  1200000, '2026-09-01', '2026-11-30', 400,  1),
('CORPFIRST',    N'Chiết Khấu Khách Hàng Doanh Nghiệp',18.00,2000000,5000000, '2026-01-01', '2026-12-31', 150,  1);
GO

-- Đặt lịch mẫu tương đối so với ngày cài đặt.
DECLARE @DichNgay int = DATEDIFF(DAY, '20260901', DATEADD(DAY, 2, CONVERT(date, GETDATE())));
UPDATE ChuyenBay
SET NgayGioBay = DATEADD(DAY, @DichNgay, NgayGioBay), NgayGioDenDK = DATEADD(DAY, @DichNgay, NgayGioDenDK),
    SoGheTrong = (SELECT COUNT(*) FROM GheNgoi WHERE MaMayBay = ChuyenBay.MaMayBay AND TrangThai = 1);
UPDATE KhuyenMai
SET NgayBatDau = DATEADD(DAY, @DichNgay, NgayBatDau), NgayKetThuc = DATEADD(DAY, @DichNgay, NgayKetThuc);
GO

-- 12. Giao Dịch Đặt Vé & Xuất Hóa Đơn Thanh Toán
-- ĐƠN 1: PNR00001 (Khách VIP Lê Duyên An - Thanh toán Thẻ ATM)
DECLARE @DS1 ChiTietDatVe_Type;
INSERT INTO @DS1 VALUES
(1, 'BUS', N'Lê Duyên An',         '079204000111', N'Người lớn', 3000000, NULL),
(9, 'ECO', N'Lê Nguyễn Đăng Khoa', '079204000222', N'Người lớn', 1500000, NULL)
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00001', @MaKH='KH00001', @MaChuyenBay='VN120', @MaKM='HE2026', @GhiChu=N'Vé công tác VIP', @DSVe=@DS1;
EXEC sp_ThanhToanDonVe @MaHD='HD00001', @MaDatCho='PNR00001', @MaNV='NV002', @PhuongThuc=N'Thẻ ATM nội địa', @MaGiaoDich='VNPAY_TXN_001';

-- ĐƠN 2: PNR00002 (Khách Lâm Ngọc Như - Thanh toán MoMo)
DECLARE @DS2 ChiTietDatVe_Type;
INSERT INTO @DS2 VALUES
(65, 'ECO', N'Lâm Ngọc Như', '079304000333', N'Người lớn', 950000, NULL)
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00002', @MaKH='KH00003', @MaChuyenBay='VJ201', @MaKM='NEWFLYER', @GhiChu=N'Du lịch hè', @DSVe=@DS2;
EXEC sp_ThanhToanDonVe @MaHD='HD00002', @MaDatCho='PNR00002', @MaNV=NULL, @PhuongThuc=N'Ví MoMo', @MaGiaoDich='MOMO_TXN_002';

-- ĐƠN 3: PNR00003 (Khách Quốc tế John Smith du lịch Côn Đảo - Thanh toán VISA)
DECLARE @DS3 ChiTietDatVe_Type;
INSERT INTO @DS3 VALUES
(85, 'BUS', N'John Smith', 'A99887766', N'Người lớn', 3700000, NULL)
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00003', @MaKH='KH00008', @MaChuyenBay='0V101', @MaKM='VIPBAY', @GhiChu=N'Tour du lịch Côn Đảo', @DSVe=@DS3;
EXEC sp_ThanhToanDonVe @MaHD='HD00003', @MaDatCho='PNR00003', @MaNV=NULL, @PhuongThuc=N'VISA/MasterCard', @MaGiaoDich='VISA_INT_003';

-- ĐƠN 4: PNR00004 (Khách Doanh nghiệp FPT Trương Gia Bình - Chờ thanh toán)
DECLARE @DS4 ChiTietDatVe_Type;
INSERT INTO @DS4 VALUES
(2, 'BUS', N'Trương Gia Bình', '001056000999', N'Người lớn', 3000000, NULL),
(3, 'BUS', N'Nguyễn Thế Phương', '001060000888', N'Người lớn', 3000000, NULL),
(10, 'ECO', N'Hoàng Việt Anh',   '001075000777', N'Người lớn', 1500000, NULL)
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00004', @MaKH='KH00013', @MaChuyenBay='VN120', @MaKM='CORPFIRST', @GhiChu=N'Vé đoàn lãnh đạo FPT công tác', @DSVe=@DS4;

-- ĐƠN 5: PNR00005 (Khách Trần Hoàng Nam - Tiền mặt tại quầy)
DECLARE @DS5 ChiTietDatVe_Type;
INSERT INTO @DS5 VALUES
(29, 'BUS', N'Trần Hoàng Nam', '001095000444', N'Người lớn', 3300000, NULL)
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00005', @MaKH='KH00004', @MaChuyenBay='VN122', @MaKM='VIPBAY', @GhiChu=N'Mua tại phòng vé sân bay', @DSVe=@DS5;
EXEC sp_ThanhToanDonVe @MaHD='HD00005', @MaDatCho='PNR00005', @MaNV='NV006', @PhuongThuc=N'Tiền mặt', @MaGiaoDich='CASH_POS_005';

-- ĐƠN 6: PNR00006 (Khách Nguyễn Thúy Hằng - Thanh toán ZaloPay)
DECLARE @DS6 ChiTietDatVe_Type;
INSERT INTO @DS6 VALUES
(66, 'ECO', N'Nguyễn Thúy Hằng', '048098000555', N'Người lớn', 850000, NULL),
(67, 'ECO', N'Trần Bảo An',      '048098000666', N'Trẻ em',    850000, NULL)
UPDATE d SET MaGhe = g.MaGhe
FROM @DS6 d JOIN GheNgoi old ON old.MaGhe = d.MaGhe
JOIN ChuyenBay cb ON cb.MaChuyenBay = 'VJ452'
JOIN GheNgoi g ON g.MaMayBay = cb.MaMayBay AND g.SoGhe = old.SoGhe;
EXEC sp_DatVeMayBay_Batch @MaDatCho='PNR00006', @MaKH='KH00005', @MaChuyenBay='VJ452', @MaKM='HE2026', @GhiChu=N'Nghỉ dưỡng gia đình Phú Quốc', @DSVe=@DS6;
EXEC sp_ThanhToanDonVe @MaHD='HD00006', @MaDatCho='PNR00006', @MaNV=NULL, @PhuongThuc=N'Ví ZaloPay', @MaGiaoDich='ZALO_TXN_006';
GO

-- PHẦN 10: QUẢN TRỊ BẢO MẬT VÀ PHÂN QUYỀN TRÊN SQL SERVER (SECURITY & RBAC)
-- =====================================================================================================

-- 3. Tạo Database Roles
IF NOT EXISTS(SELECT name FROM sys.database_principals WHERE name='db_admin_role'   AND type='R') CREATE ROLE db_admin_role;
IF NOT EXISTS(SELECT name FROM sys.database_principals WHERE name='db_thungan_role' AND type='R') CREATE ROLE db_thungan_role;
IF NOT EXISTS(SELECT name FROM sys.database_principals WHERE name='db_banve_role'   AND type='R') CREATE ROLE db_banve_role;
IF NOT EXISTS(SELECT name FROM sys.database_principals WHERE name='db_cskh_role'    AND type='R') CREATE ROLE db_cskh_role;
GO

-- 4. Phân Quyền (Permissions)
-- Admin: Toàn quyền
GRANT CONTROL TO db_admin_role;

-- Thu ngân: Hóa Đơn, Phiếu Đặt Chỗ, Vé
GRANT SELECT, INSERT, UPDATE ON HoaDon     TO db_thungan_role;
GRANT SELECT, UPDATE         ON PhieuDatCho TO db_thungan_role;
GRANT SELECT                 ON VeMayBay   TO db_thungan_role;
GRANT EXECUTE ON sp_ThanhToanDonVe         TO db_thungan_role;
GRANT EXECUTE ON sp_ApDungKhuyenMai        TO db_thungan_role;

-- Nhân viên Bán vé: Tra cứu, Đặt vé, Voucher
GRANT SELECT                 ON ChuyenBay    TO db_banve_role;
GRANT SELECT                 ON GheNgoi      TO db_banve_role;
GRANT SELECT, INSERT, UPDATE ON PhieuDatCho  TO db_banve_role;
GRANT SELECT, INSERT, UPDATE ON VeMayBay    TO db_banve_role;
GRANT SELECT                 ON KhuyenMai    TO db_banve_role;
GRANT EXECUTE ON sp_TimKiemChuyenBay         TO db_banve_role;
GRANT EXECUTE ON sp_DatVeMayBay_Batch        TO db_banve_role;

-- Nhân viên CSKH: Khách hàng, Đổi/Hủy vé
GRANT SELECT                 ON KhachHang        TO db_cskh_role;
GRANT SELECT, UPDATE         ON KhieuNai         TO db_cskh_role;
GRANT SELECT, UPDATE         ON VeMayBay         TO db_cskh_role;
GRANT SELECT, INSERT, UPDATE ON YeuCauHuyDoiVe   TO db_cskh_role;
GRANT EXECUTE ON sp_XuLyHuyVeVaHoanTien          TO db_cskh_role;
GRANT EXECUTE ON sp_DoiVeMayBay                  TO db_cskh_role;
GO

-- DBA có thể gán tài khoản đã tạo vào vai trò tương ứng trên instance triển khai.
-- LocalDB/Express chạy tác vụ hết hạn trong ứng dụng, không cần SQL Server Agent.

-- 1. Kiểm tra số lượng bản ghi trong từng bảng
SELECT 'HangHangKhong'   AS TenBang, COUNT(*) AS TongBanGhi FROM HangHangKhong
UNION ALL SELECT 'SanBay',          COUNT(*) FROM SanBay
UNION ALL SELECT 'TuyenBay',        COUNT(*) FROM TuyenBay
UNION ALL SELECT 'MayBay',          COUNT(*) FROM MayBay
UNION ALL SELECT 'HangGhe',         COUNT(*) FROM HangGhe
UNION ALL SELECT 'GheNgoi',         COUNT(*) FROM GheNgoi
UNION ALL SELECT 'ChuyenBay',       COUNT(*) FROM ChuyenBay
UNION ALL SELECT 'ChiTietChuyenBay',COUNT(*) FROM ChiTietChuyenBay
UNION ALL SELECT 'VaiTro',          COUNT(*) FROM VaiTro
UNION ALL SELECT 'NhanVien',        COUNT(*) FROM NhanVien
UNION ALL SELECT 'KhachHang',       COUNT(*) FROM KhachHang
UNION ALL SELECT 'KhuyenMai',       COUNT(*) FROM KhuyenMai
UNION ALL SELECT 'PhieuDatCho',     COUNT(*) FROM PhieuDatCho
UNION ALL SELECT 'VeMayBay',        COUNT(*) FROM VeMayBay
UNION ALL SELECT 'HoaDon',          COUNT(*) FROM HoaDon
UNION ALL SELECT 'YeuCauHuyDoiVe',  COUNT(*) FROM YeuCauHuyDoiVe
UNION ALL SELECT 'DanhGiaChuyenBay',COUNT(*) FROM DanhGiaChuyenBay
UNION ALL SELECT 'KhieuNai',COUNT(*) FROM KhieuNai;
GO

-- 2. Kiểm tra dữ liệu qua các View báo cáo
SELECT TOP 5 * FROM vw_DanhSachChuyenBayChiTiet;
SELECT * FROM vw_BaoCaoDoanhThuTheoHang;
GO
SET NOEXEC OFF;
GO
