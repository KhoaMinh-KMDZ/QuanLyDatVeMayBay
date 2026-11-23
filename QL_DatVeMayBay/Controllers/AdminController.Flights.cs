using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Models;

namespace QL_DatVeMayBay.Controllers;

public partial class AdminController
{
    [HttpGet]
    public IActionResult Flight(string? id)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(id))
            return FlightView(new FlightForm());

        var flight = DatabaseHelper.ExecuteQuery(@"
            SELECT MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, GiaVeCoBan, TrangThai
            FROM ChuyenBay WHERE MaChuyenBay = @Id", CommandType.Text, new SqlParameter("@Id", id));
        if (flight.Rows.Count == 0)
            return NotFound();

        DataRow row = flight.Rows[0];
        var stops = DatabaseHelper.ExecuteQuery(@"
            SELECT MaSanBayTG, ThoiGianDung, GhiChu
            FROM ChiTietChuyenBay WHERE MaChuyenBay = @Id ORDER BY ThuTuDung",
            CommandType.Text, new SqlParameter("@Id", id));
        return FlightView(new FlightForm
        {
            Update = true,
            MaChuyenBay = row.Field<string>("MaChuyenBay")!,
            MaTuyenBay = row.Field<string>("MaTuyenBay")!,
            MaMayBay = row.Field<string>("MaMayBay")!,
            NgayGioBay = row.Field<DateTime>("NgayGioBay"),
            NgayGioDenDK = row.Field<DateTime>("NgayGioDenDK"),
            GiaVeCoBan = row.Field<decimal>("GiaVeCoBan"),
            TrangThai = row.Field<string>("TrangThai")!,
            Stops = stops.AsEnumerable().Select(stop => new FlightStopForm
            {
                MaSanBayTG = stop.Field<string>("MaSanBayTG"),
                ThoiGianDung = stop.Field<int>("ThoiGianDung"),
                GhiChu = stop.Field<string>("GhiChu")
            }).ToList()
        });
    }

    [HttpPost]
    public IActionResult Flight(FlightForm form)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        string? error = SaveFlight(form);
        if (error != null)
            ModelState.AddModelError("", error);
        if (!ModelState.IsValid)
            return FlightView(form);

        TempData["Success"] = $"Đã lưu lịch chuyến {form.MaChuyenBay}.";
        return RedirectToAction(nameof(Dashboard), null, null, "tab-flights");
    }

    private IActionResult FlightView(FlightForm form)
    {
        ViewBag.Routes = DatabaseHelper.ExecuteQuery("SELECT MaTuyenBay FROM TuyenBay ORDER BY MaTuyenBay");
        ViewBag.Aircraft = DatabaseHelper.ExecuteQuery(@"
            SELECT MaMayBay, TenMayBay FROM MayBay
            WHERE TrangThai = N'Sẵn sàng' OR MaMayBay = @MaMayBay ORDER BY MaMayBay",
            CommandType.Text, new SqlParameter("@MaMayBay", form.MaMayBay));
        ViewBag.Airports = DatabaseHelper.ExecuteQuery(@"
            SELECT MaSanBay, TenSanBay FROM SanBay WHERE TrangThai = 1 ORDER BY MaSanBay");
        ViewBag.CanEdit = !form.Update || DatabaseHelper.ExecuteScalar(@"
            SELECT 1 FROM ChuyenBay cb
            WHERE cb.MaChuyenBay = @Id AND cb.NgayGioBay > GETDATE() AND cb.TrangThai <> N'Đã cất cánh'
                AND NOT EXISTS (SELECT 1 FROM VeMayBay v WHERE v.MaChuyenBay = cb.MaChuyenBay)",
            CommandType.Text, new SqlParameter("@Id", form.MaChuyenBay)) != null;
        return View("Flight", form);
    }

    private string? SaveFlight(FlightForm form)
    {
        if (!ModelState.IsValid)
            return "Kiểm tra mã chuyến, tuyến, tàu bay, thời gian và giá vé.";
        if (form.NgayGioBay is not { } departure || form.NgayGioDenDK is not { } arrival
            || departure.Year < 1753 || arrival <= departure
            || form.TrangThai is not ("Đang mở bán" or "Hoãn chuyến" or "Đã hủy"))
            return "Giờ đến phải sau giờ khởi hành. Chọn trạng thái mở bán, hoãn hoặc hủy.";

        var stops = form.Stops.Where(stop => !string.IsNullOrWhiteSpace(stop.MaSanBayTG)
            || stop.ThoiGianDung.HasValue || !string.IsNullOrWhiteSpace(stop.GhiChu)).ToArray();
        if (stops.Any(stop => string.IsNullOrWhiteSpace(stop.MaSanBayTG) || stop.ThoiGianDung is not (>= 45 and <= 120))
            || stops.Select(stop => stop.MaSanBayTG).Distinct(StringComparer.OrdinalIgnoreCase).Count() != stops.Length)
            return "Mỗi điểm quá cảnh cần sân bay khác nhau và thời gian dừng từ 45 đến 120 phút.";
        if (stops.Sum(stop => stop.ThoiGianDung!.Value) >= (arrival - departure).TotalMinutes)
            return "Tổng thời gian dừng phải ngắn hơn toàn bộ thời gian hành trình.";

        try
        {
            DatabaseHelper.ExecuteNonQuery(@"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                IF @Departure <= GETDATE()
                    THROW 51023, N'Giờ khởi hành phải ở tương lai.', 1;

                DECLARE @From varchar(10), @To varchar(10);
                SELECT @From = MaSanBayDi, @To = MaSanBayDen FROM TuyenBay
                WHERE MaTuyenBay = @Route;
                IF @From IS NULL
                    THROW 51026, N'Tuyến bay không hợp lệ.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM MayBay WITH (UPDLOCK, HOLDLOCK)
                    WHERE MaMayBay = @Aircraft AND TrangThai = N'Sẵn sàng'
                )
                    THROW 51020, N'Tàu bay không sẵn sàng.', 1;

                IF @Update = 1
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM ChuyenBay WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaChuyenBay = @Id AND NgayGioBay > GETDATE() AND TrangThai <> N'Đã cất cánh'
                    )
                        THROW 51024, N'Không tìm thấy chuyến có thể điều chỉnh.', 1;

                    IF EXISTS (SELECT 1 FROM VeMayBay WHERE MaChuyenBay = @Id)
                        THROW 51025, N'Chuyến đã có hồ sơ vé. Cần xử lý hành khách trước khi đổi lịch.', 1;
                END;

                IF @Status <> N'Đã hủy' AND EXISTS (
                    SELECT 1 FROM ChuyenBay
                    WHERE MaMayBay = @Aircraft AND (@Update = 0 OR MaChuyenBay <> @Id)
                        AND TrangThai <> N'Đã hủy' AND NgayGioBay < @Arrival AND NgayGioDenDK > @Departure
                )
                    THROW 51021, N'Tàu bay đã có lịch trong khoảng giờ này.', 1;

                DECLARE @Seats int = (SELECT COUNT(*) FROM GheNgoi WHERE MaMayBay = @Aircraft AND TrangThai = 1);
                IF @Seats = 0
                    THROW 51022, N'Tàu bay chưa có ghế khả dụng.', 1;

                DECLARE @StopsTable TABLE(ThuTu int, Airport varchar(10), Minutes int, Note nvarchar(200));
                INSERT INTO @StopsTable
                SELECT CONVERT(int, entry.[key]) + 1, stop.Airport, stop.Minutes, stop.Note
                FROM OPENJSON(@Stops) entry
                CROSS APPLY OPENJSON(entry.[value]) WITH (
                    Airport varchar(10) '$.MaSanBayTG', Minutes int '$.ThoiGianDung', Note nvarchar(200) '$.GhiChu'
                ) stop;

                IF EXISTS (
                    SELECT 1 FROM @StopsTable stop
                    WHERE stop.Airport IN (@From, @To) OR NOT EXISTS (
                        SELECT 1 FROM SanBay WHERE MaSanBay = stop.Airport AND TrangThai = 1
                    )
                )
                    THROW 51027, N'Điểm quá cảnh phải hoạt động và khác sân bay đi, sân bay đến.', 1;

                IF @Update = 1
                BEGIN
                    UPDATE ChuyenBay SET MaTuyenBay = @Route, MaMayBay = @Aircraft, NgayGioBay = @Departure, NgayGioDenDK = @Arrival,
                        ThoiGianBay = DATEDIFF(MINUTE,@Departure,@Arrival), GiaVeCoBan = @Fare, SoGheTrong = @Seats, TrangThai = @Status
                    WHERE MaChuyenBay = @Id;
                    DELETE FROM ChiTietChuyenBay WHERE MaChuyenBay = @Id;
                END
                ELSE
                BEGIN
                    INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
                    VALUES (@Id, @Route, @Aircraft, @Departure, @Arrival, DATEDIFF(MINUTE,@Departure,@Arrival), @Fare, @Seats, @Status);
                END;

                INSERT INTO ChiTietChuyenBay (MaChuyenBay, MaSanBayTG, ThoiGianDung, ThuTuDung, GhiChu)
                SELECT @Id, Airport, Minutes, ThuTu, Note FROM @StopsTable;
                COMMIT TRANSACTION;", CommandType.Text,
                new SqlParameter("@Update", form.Update), new SqlParameter("@Id", form.MaChuyenBay.Trim().ToUpperInvariant()),
                new SqlParameter("@Route", form.MaTuyenBay), new SqlParameter("@Aircraft", form.MaMayBay),
                new SqlParameter("@Departure", departure), new SqlParameter("@Arrival", arrival),
                new SqlParameter("@Fare", form.GiaVeCoBan), new SqlParameter("@Status", form.TrangThai),
                new SqlParameter("@Stops", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(stops) });
        }
        catch (SqlException ex) when (ex.Number is >= 51020 and <= 51027)
        {
            return ex.Message;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return "Mã chuyến hoặc điểm quá cảnh đã tồn tại.";
        }

        return null;
    }
}
