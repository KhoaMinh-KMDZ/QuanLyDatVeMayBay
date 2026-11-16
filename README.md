# Hệ Thống Quản Lý Đặt Vé Máy Bay Nội Địa (SkyVN Airlines)

Ứng dụng web quản lý bán vé máy bay nội địa Việt Nam phục vụ đồng thời hai nhóm đối tượng: **Hành khách đặt vé trực tuyến** và **Nhân viên nghiệp vụ tại đại lý**. Hệ thống được xây dựng trên nền tảng **ASP.NET Core 8.0 MVC**, giao tiếp cơ sở dữ liệu bằng **ADO.NET** kết hợp bộ **Stored Procedures, Triggers, Functions và Views** trên **SQL Server**.

---

## 1. Công nghệ sử dụng

* **Backend:** C#, ASP.NET Core 8.0 (Model-View-Controller), Session Authentication
* **Data Access:** Thuần ADO.NET (`Microsoft.Data.SqlClient`, `DataTable`, `SqlCommand`, Table-Valued Parameters)
* **Database:** Microsoft SQL Server (17 bảng chuẩn hóa, Stored Procedures xử lý giao dịch đồng thời, Triggers tự động cập nhật tồn kho ghế & tổng tiền, SQL Cursor)
* **Frontend:** Razor Views, HTML5, CSS3, Bootstrap 5, Bootstrap Icons, Chart.js

---

## 2. Chức năng chính

### Phân hệ Hành khách
* **Tìm kiếm chuyến bay:** Lọc theo sân bay đi/đến (15 cảng hàng không nội địa), ngày khởi hành, hạng ghế, hãng hàng không, mức giá tối đa; hiển thị thông tin bay thẳng hoặc điểm dừng quá cảnh.
* **Chọn ghế trực quan & Đặt vé:** Hiển thị sơ đồ khoang ghế theo từng loại tàu bay (Thương gia / Phổ thông / Đã đặt). Sử dụng `UPDLOCK, HOLDLOCK` trong SQL Server để khóa ghế và chống đặt trùng khi nhiều người thao tác cùng lúc.
* **Thanh toán & Hóa đơn VAT:** Hỗ trợ nhiều phương thức thanh toán (MoMo, ZaloPay, thẻ nội địa, thẻ quốc tế, tiền mặt), áp dụng mã giảm giá và xuất hóa đơn điện tử chuẩn bản in PDF.
* **Check-in Online & Boarding Pass:** Làm thủ tục trực tuyến theo mã đặt chỗ (PNR) và cấp thẻ lên máy bay điện tử kèm mã QR.
* **Quản lý đặt chỗ (PNR):**
  * Xem lịch sử đơn đặt vé cá nhân tại mục *Chuyến bay của tôi*.
  * **Hủy vé & Hoàn tiền:** Tự động tính tỷ lệ hoàn trả và phí phạt dựa trên mốc thời gian trước giờ khởi hành (Luật 72h).
  * **Đổi chuyến bay:** Chọn chuyến mới, tải danh sách ghế còn trống theo thời gian thực và tự động tính chênh lệch giá vé + phí đổi.
  * **Đánh giá chuyến bay:** Gửi nhận xét và chấm điểm (1–5 sao) sau khi hoàn tất thủ tục.

### Phân hệ Nhân viên & Quản trị (Phân quyền RBAC)
Hệ thống phân chia quyền hạn theo 4 vai trò nghiệp vụ:
* **Ban Giám Đốc (`RoleID = 1`):** Xem toàn bộ thống kê doanh thu (biểu đồ Chart.js theo hãng bay và tuyến bay), mở lịch chuyến bay mới, phát hành/khóa mã khuyến mãi, quản lý tài khoản nhân sự.
* **Thu Ngân (`RoleID = 2`):** Xác nhận thu tiền tại quầy, đối soát giao dịch và in hóa đơn VAT.
* **Bán Vé (`RoleID = 3`):** Hỗ trợ đặt giữ chỗ, tra cứu hồ sơ PNR và xuất thẻ lên máy bay cho khách.
* **Chăm sóc Khách hàng (`RoleID = 4`):** Tiếp nhận yêu cầu đổi chuyến, hủy vé hoàn tiền và theo dõi phản hồi của hành khách.
* **Tác vụ tự động:** Chạy SQL Cursor quét và giải phóng ghế cho các đơn giữ chỗ quá hạn thanh toán.

---

## 3. Cấu trúc thư mục dự án

```text
QuanLyDatVeMayBay/
├── QL_DatVeMayBay/
│   ├── Controllers/          # Điều hướng nghiệp vụ (Home, Account, Booking, CheckIn, Admin)
│   ├── Data/                 # DatabaseHelper.cs (Lớp kết nối & thực thi ADO.NET)
│   ├── Database/             # QL_DatVeMayBay.sql (Script khởi tạo CSDL & dữ liệu mẫu)
│   ├── Views/                # Giao diện Razor (.cshtml) cho từng phân hệ
│   ├── wwwroot/              # Tài nguyên tĩnh (CSS, JS, thư viện)
│   ├── appsettings.json      # Cấu hình chuỗi kết nối SQL Server
│   └── Program.cs            # Khởi tạo ứng dụng và cấu hình Session
└── .gitignore
```

---

## 4. Hướng dẫn cài đặt và chạy dự án

### Yêu cầu môi trường
* Visual Studio 2022 (đã cài workload *ASP.NET and web development*)
* .NET 8.0 SDK
* SQL Server 2019 trở lên & SQL Server Management Studio (SSMS)

### Các bước thực hiện
1. **Clone repository về máy:**
   ```bash
   git clone [https://github.com/xXHvngXx/QuanLyDatVeMayBay.git](https://github.com/xXHvngXx/QuanLyDatVeMayBay.git)
   ```
2. **Khởi tạo Cơ sở dữ liệu:**
   * Mở SSMS, mở file `QL_DatVeMayBay/Database/QL_DatVeMayBay.sql` và nhấn **Execute (`F5`)** để tạo database `QL_DatVeMayBay` cùng dữ liệu mẫu.
   * *(Tùy chọn)* Nếu ngày bay trong dữ liệu mẫu đã qua so với ngày hiện tại trên máy, chạy thêm lệnh sau để dời lịch bay sang tương lai phục vụ kiểm thử Luật hoàn vé 72h:
     ```sql
     UPDATE ChuyenBay
     SET NgayGioBay = DATEADD(MONTH, 4, NgayGioBay),
         NgayGioDenDK = DATEADD(MONTH, 4, NgayGioDenDK);
     ```
3. **Cấu hình chuỗi kết nối:**
   * Mở file `QL_DatVeMayBay/appsettings.json` và chỉnh lại `Server` cho khớp với SQL Server trên máy (mặc định là `.\SQLEXPRESS`):
     ```json
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=QL_DatVeMayBay;Trusted_Connection=True;TrustServerCertificate=True;"
     }
     ```
4. **Chạy ứng dụng:**
   * Mở file `QL_DatVeMayBay/QL_DatVeMayBay.sln` bằng Visual Studio 2022 và nhấn **`F5`** (hoặc `Ctrl + F5`).

---

## 5. Tài khoản kiểm thử

Tất cả tài khoản mẫu sử dụng chung mật khẩu: **`123456`**

| Phân hệ | Vai trò | Tài khoản đăng nhập | Mật khẩu |
| :--- | :--- | :--- | :--- |
| **Khách hàng** | Hành khách thành viên | `duyenan@gmail.com` | `123456` |
| **Khách hàng** | Hành khách thành viên | `ngocnhu@gmail.com` | `123456` |
| **Nhân viên** | Ban Giám Đốc (Full quyền) | `admin` | `123456` |
| **Nhân viên** | Thu Ngân | `thungan` | `123456` |
| **Nhân viên** | Nhân viên Bán Vé | `banve` | `123456` |
| **Nhân viên** | Chăm sóc Khách hàng | `cskh` | `123456` |

---

## 6. Thông tin nhóm thực hiện

* **Đề tài:** Phân tích và Thiết kế Hệ thống Quản lý Đặt vé Máy bay Nội địa Việt Nam
* **Nhóm thực hiện:** Nhóm 10 — Khoa Công nghệ Thông tin, Trường Đại học Công Thương TP.HCM (HUIT)
