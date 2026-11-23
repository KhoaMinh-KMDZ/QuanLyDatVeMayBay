:ON ERROR EXIT
-- Chạy với sqlcmd ở CSDL SkyVN đã có. Mỗi batch thuộc cùng một giao tác.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.PhieuDatCho', N'U') IS NULL
    OR OBJECT_ID(N'dbo.VeMayBay', N'U') IS NULL
    OR OBJECT_ID(N'dbo.sp_DatVeMayBay_Batch', N'P') IS NULL
    OR TYPE_ID(N'dbo.ChiTietDatVe_Type') IS NULL
    THROW 51998, N'Chọn cơ sở dữ liệu SkyVN đang dùng trước khi chạy nâng cấp.', 1;
BEGIN TRANSACTION;
GO

CREATE OR ALTER VIEW vw_VeMayBayChiemGhe AS
SELECT v.MaChuyenBay, v.MaGhe
FROM dbo.VeMayBay v
JOIN dbo.PhieuDatCho p ON p.MaDatCho = v.MaDatCho
WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
   OR (v.TrangThai = N'Đã đặt chỗ' AND p.TrangThai = N'Chờ thanh toán'
       AND p.HanThanhToan > GETDATE());
GO

CREATE OR ALTER FUNCTION fn_DemSoGheTrongTheoHang
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

CREATE OR ALTER VIEW vw_DanhSachChuyenBayChiTiet AS
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

CREATE OR ALTER PROCEDURE sp_TimKiemChuyenBay
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

CREATE OR ALTER PROCEDURE sp_TuDongHuyDonGiuChoHetHan_Cursor
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

CREATE OR ALTER PROCEDURE sp_DatVeMayBay_Batch
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

CREATE OR ALTER PROCEDURE sp_DoiVeMayBay
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

CREATE OR ALTER FUNCTION fn_ChuanHoaSoDienThoai(@SoDienThoai varchar(100))
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

CREATE OR ALTER PROCEDURE sp_DangKyKhachHang
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

CREATE OR ALTER PROCEDURE sp_DangNhapKhachHang
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

COMMIT TRANSACTION;
GO
