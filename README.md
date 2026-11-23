# SkyVN Airlines

Đồ án Nhóm 10 môn Phân tích và thiết kế hệ thống thông tin. Ứng dụng ASP.NET Core 8 MVC dùng ADO.NET và SQL Server để quản lý đặt vé máy bay nội địa, bán vé tại quầy và hỗ trợ sau bán.

## Chạy bản đồ án

Cần .NET 8 và SQL Server. Đường dẫn mặc định trong `appsettings.json` dùng SQL Server Express. Để dùng LocalDB trên Windows, chạy các lệnh PowerShell sau từ thư mục chứa README.

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -f 65001 -i '.\QL_DatVeMayBay\Database\QL_DatVeMayBay.sql'

$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=QL_DatVeMayBay;Integrated Security=True;TrustServerCertificate=True;'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project '.\QL_DatVeMayBay\QL_DatVeMayBay.csproj' --no-launch-profile --urls 'http://localhost:5189'
```

Mở `http://localhost:5189`. Kịch bản SQL khởi tạo mới và dừng khi đã có cơ sở dữ liệu cùng tên. Để chạy song song với dữ liệu đang dùng, sửa tên `QL_DatVeMayBay` trong **bản sao** của script trước khi chạy rồi khớp lại trường `Database` trong kết nối. Những thay đổi lược đồ trong bản này cần một cơ sở mới hoặc kịch bản nâng cấp riêng cho dữ liệu hiện hữu.

Lịch mẫu bắt đầu khoảng hai ngày sau khi cài, cùng thời điểm với voucher mẫu. Trang chủ liệt kê các chuyến còn mở bán. Có thể lấy ngày chuyến `VN120` từ trang chủ để thử tìm kiếm tuyến SGN → HAN.

| Vai trò mẫu | Tên đăng nhập |
|---|---|
| Hành khách | `duyenan@gmail.com` hoặc `ngocnhu@gmail.com` |
| Ban Giám đốc | `admin` |
| Thu ngân | `thungan` |
| Bán vé | `banve` |
| Chăm sóc khách hàng | `cskh` |

Mật khẩu mẫu là `123456`. Các tài khoản mẫu được lưu dạng băm. Khi chạy cấu hình Development, thanh toán trực tuyến và thu phí đổi vé dùng dữ liệu mô phỏng. Thu ngân có thể ghi nhận tiền mặt cho đơn đã giữ tại quầy.

Nhân viên bán vé tìm chuyến, chọn ghế và nhập mã khách hàng đã có hồ sơ để lập PNR. Thu ngân mở PNR đó để thu tiền, xuất vé và in hóa đơn mẫu. Hành khách đăng nhập tài khoản của mình để tự giữ chỗ, xem hóa đơn, hủy hoặc đổi vé. Check-in yêu cầu đúng PNR, họ tên đầy đủ và xác nhận an toàn hành lý.

## Đối chiếu với đặc tả Nhóm 10

Bản kiểm tra lấy commit GitHub `0a089e771a35747e0f828dc564df6c553ed2d167` làm mốc. Trước khi sửa, 93 tệp theo dõi trên GitHub và bản trong máy có cùng nội dung khi chuẩn hóa xuống dòng. Hai bản SQL sau khi sửa được đồng bộ.

Tiêu chí đối chiếu lấy từ bảy chương bài giảng. Chương 1–2 dùng để xác định ranh giới và yêu cầu. Chương 3–4 dùng để đối chiếu tác nhân, sơ đồ hoạt động và đặc tả use case. Chương 5–6 dùng để truy lớp phân tích đến khóa, quan hệ và bội số trong SQL. Chương 7 dùng để kiểm tra sự kiện, lớp thực hiện, thông điệp tuần tự và phạm vi giao tác.

| Use case | Hiện trạng sau sửa | Phần còn thiếu so với báo cáo |
|---|---|---|
| UC01 Tra cứu | Lọc tuyến, ngày, hãng, hạng ghế, số khách và giá đúng hạng. Nếu ngày đã chọn hết chuyến, gợi ý ngày trong khoảng ±3 ngày với cùng bộ lọc. | |
| UC02 Đặt trực tuyến | Gắn PNR với khách đăng nhập. SQL kiểm tra ghế thuộc tàu bay và tính giá theo dữ liệu gốc. | Luồng khách vãng lai xác minh thông tin liên hệ. |
| UC03 Bán tại quầy | Nhân viên bán vé giữ chỗ theo mã khách đã có. Thu ngân thu tiền mặt và in hóa đơn mẫu. | Nhập mới hoặc cập nhật hồ sơ hành khách ngay trong màn hình bán tại quầy. |
| UC04 Thanh toán | Chặn thanh toán quá hạn. Lặp xác nhận trả về hóa đơn cũ. Có ghi nhận tiền mặt và mô phỏng trực tuyến. | Cổng trực tuyến thật, xác nhận giao dịch từ nhà cung cấp và gửi vé. |
| UC05 Hủy vé | Hủy vé đã xuất, trước giờ bay và chưa check-in. Ghi kết quả thẩm định và trả ghế trong cùng giao tác. | Chuyển khoản hoàn tiền và đối soát số tiền thực trả. |
| UC06 Đổi vé | Vé còn ít nhất 24 giờ, ghế mới hợp lệ, ghi phí đổi và trả ghế cũ. Phát hành vé mới trong giao tác mô phỏng. | Thu phí qua kênh thật và lập chứng từ thu bổ sung. |
| UC07 Check-in | Đối chiếu họ tên đầy đủ, PNR, vé, trạng thái phiếu, giờ bay và xác nhận an toàn hành lý. | Đồng bộ thủ tục với hãng. |
| UC08 Đánh giá | Ghi nhận cột `NoiDung`, giới hạn một lời đánh giá mỗi khách mỗi chuyến. Yêu cầu vé đã check-in và đã qua giờ đến dự kiến. | Lấy giờ đến thực tế từ hãng. |
| UC09 Thống kê | Doanh số hóa đơn theo trạng thái thanh toán, tổng vé, bảng theo hãng và tuyến. Giới hạn cho Ban Giám đốc và Thu ngân. | Báo cáo doanh thu thuần theo kỳ sau đối soát, lọc và xuất tệp theo đặc tả đầy đủ. |
| UC10 Gửi khiếu nại | Tạo hồ sơ và mã theo dõi. Nếu ghi PNR hoặc vé, hệ thống kiểm tra hồ sơ thuộc khách. | Nhận hồ sơ qua xác minh email và gửi thông báo ngoài ứng dụng. |
| UC11 Xử lý khiếu nại | CSKH hoặc Ban Giám đốc phản hồi trong app, đóng hoặc từ chối hồ sơ và có thể gắn voucher đang hiệu lực. | Phân công nội bộ theo hàng đợi và cấp mã bồi hoàn riêng. |
| UC12 Quản lý lịch | Thêm và chỉnh lịch với nhiều điểm quá cảnh, kiểm tra 45–120 phút mỗi điểm, tàu bay sẵn sàng, xung đột và số ghế. Có thể hoãn/hủy chuyến tương lai chưa có hồ sơ vé. | Điều phối hoãn/hủy khi đã có vé và hỗ trợ hành khách bị ảnh hưởng. |
| UC13 Bảng giá | Cập nhật giá chuyến, hệ số hạng, tạo, sửa và khóa/mở voucher. Kiểm tra kỳ hiệu lực và từ chối ghi đè số lượt đã thay đổi. | |
| UC14 Nhân sự | Tạo và sửa hồ sơ, gán vai trò, khóa/mở tài khoản. Kiểm tra trùng tài khoản, điện thoại, email và thu hồi phiên cũ khi đổi vai trò. | |
| UC15 Thẻ lên máy bay | Thẻ mẫu tạo QR cục bộ sau check-in và hỗ trợ in từ trình duyệt. | Cửa khởi hành từ hãng, gửi thẻ và tích hợp hệ thống kiểm soát. |
| UC16 Giữ ghế | Giữ 15 phút, hiện thời gian đếm ngược khi thanh toán, chỉ một vé hiệu lực trên mỗi ghế/chuyến. Tác vụ chạy mỗi phút để giải phóng đơn hết hạn. | Triển khai tác vụ độc lập nếu cần xử lý lúc web ngừng chạy. |
| UC17 Dịch vụ | Chọn một gói mẫu cho từng khách. SQL tính và lưu phí, tổng tiền và chi tiết hóa đơn cập nhật theo gói. | Danh mục quản trị và phối hợp nhiều gói cho một hành khách. |
| UC18 Thẩm định | Trên 72 giờ hoàn 80%, từ 24 đến 72 giờ hoàn 50%, dưới 24 giờ hoàn 0%. | Chốt cách phân bổ voucher và phụ phí vào khoản hoàn thực tế. |
| UC19 Hóa đơn | Một hóa đơn trên mỗi PNR. Lưu ảnh chụp dữ liệu giao dịch dạng JSON để giữ nguyên số liệu khi hủy hoặc đổi. | Phát hành hóa đơn VAT, ký số và gửi email. |
| UC20 Voucher | Áp dụng hoặc bỏ voucher khi thanh toán, kiểm tra ngày, số lượt, ngưỡng và trần giảm. Giữ lượt khi áp dụng, hoàn lượt khi bỏ hoặc hết hạn. | Đặc tả mô tả trừ lượt khi thanh toán xong. Bản chạy giữ lượt trước để đảm bảo đơn còn hạn giữ được mức giảm. |

## Các điểm cần đồng bộ khi cập nhật báo cáo

- Chương 2 nêu giữ chỗ 15 phút và hoàn 80%/50%/0%. Bản Chương 3 ban đầu có chỗ ghi giữ 24 giờ và hoàn 50% khi còn dưới 24 giờ. Mã đã theo các mốc ở Chương 2. Cập nhật bảng sự kiện–hành động, giả mã, sơ đồ tuần tự và ảnh màn hình tương ứng.
- Chương 3 bản cập nhật mô tả 17 bảng, 24 khóa ngoại và khóa duy nhất theo mọi trạng thái của vé. Bản SQL hiện tại có thêm bảng `KhieuNai` với năm khóa ngoại và chỉ mục duy nhất có bộ lọc theo trạng thái vé. Thuộc tính dịch vụ nằm trên `VeMayBay` và dữ liệu lúc thanh toán nằm trên `HoaDon`.
- Hình use case hệ thống thể hiện cổng thanh toán riêng. Trong sơ đồ triển khai hiện tại, ghi rõ nhánh mô phỏng và nhánh thu tiền mặt tại quầy. Sơ đồ lớp triển khai cần bổ sung `SupportController` và tác vụ giải phóng đơn hết hạn.
- Bản báo cáo Chương 3 đã sửa có các bảng kiểm thử mô tả lỗi thấy trong bản cũ. Đổi trạng thái các ca đã sửa, thực hiện lại ca còn thiếu và cập nhật sơ đồ SQL dựa trên catalog của bản mới.

## Kết quả kiểm thử

Bản Release biên dịch với 0 lỗi và 0 cảnh báo. Các luồng web được chạy bằng Edge trên cấu hình Debug, kết nối với SQL Server LocalDB qua cơ sở `QL_DatVeMayBay_ReviewFinal`. Máy kiểm thử chặn tiến trình Release theo chính sách Windows Application Control.

- Ca SQL xác nhận giới hạn 15 phút, tính giá và phụ phí từ dữ liệu gốc, từ chối ghế sai máy bay, chặn thanh toán quá hạn, hoàn lượt voucher một lần, ngăn tạo trùng hóa đơn và đối soát tồn ghế. Hai phiên giữ cùng ghế chạy song song cho đúng một phiếu thành công.
- Mốc hoàn 80%/50%/0% được thử sát trên và sát dưới 72 giờ, 24 giờ. Luồng hủy rồi đặt lại cùng ghế thành công.
- Ca web đi qua tìm chuyến, thanh toán mô phỏng, hóa đơn sau hủy, đổi vé, thu tiền mặt, khiếu nại và phản hồi, phân vai, cập nhật giá, chặn lịch trùng, check-in trong và ngoài khung giờ, phát QR cục bộ và từ chối lời đánh giá trùng.

Trong cả hai bản PDF, Chương 4 và Chương 5 còn ở mức đề mục. Nhóm có thể dùng kết quả kiểm thử và bảng đối chiếu trên để viết phần cài đặt và kết luận.

### Lần rà soát giao diện sau kiểm thử tích hợp

Bản cập nhật bổ sung gợi ý ngày bay theo UC01, đồng hồ đếm ngược theo UC16 và giữ đường dẫn quay lại màn hình đặt vé sau khi đăng nhập sai. Bản Debug biên dịch với 0 lỗi và 0 cảnh báo. SQL Server LocalDB xác nhận kết quả đúng ngày, giá theo hạng và gợi ý ±3 ngày giữ nguyên bộ lọc. JavaScript qua kiểm tra đếm từ 15:00 về 0 và khóa nút áp voucher, thanh toán.

Lần chạy tích hợp trên bản cập nhật này bị Windows Code Integrity chặn khi nạp DLL mới do yêu cầu chữ ký của máy. Các ca web ở mục trên thuộc lần kiểm thử tích hợp trước khi thêm các thay đổi này.

### Kiểm thử bản quản lý trên Docker

Bản Release được chạy với ASP.NET Core 8 và SQL Server 2022 trong Docker. Hai container dùng cùng một mạng riêng. Trang web được mở trên máy chạy Docker tại `http://127.0.0.1:5189/`. Cơ sở này dùng dữ liệu mẫu, độc lập với LocalDB.

- Kiểm thử web UC12 tạo lịch có điểm quá cảnh, chặn điểm trùng sân bay đi hoặc đến, đọc lại điểm dừng đã lưu, hoãn chuyến và kiểm tra hành vi khi chuyến đã có vé.
- Kiểm thử web UC13 sửa khuyến mãi và xử lý trường hợp số lượt thay đổi trước khi lưu.
- Kiểm thử web UC14 thêm nhân viên, đăng nhập bằng mật khẩu đã băm, chặn truy cập phần quản trị đối với thu ngân, chống hồ sơ trùng, đổi vai trò và thu hồi phiên cũ.
- Kiểm thử lại tra cứu, gợi ý ngày lân cận, quay về chọn ghế sau khi đăng nhập, giữ ghế có phụ phí và khóa nút khi đồng hồ hết hạn.

### Kiểm thử nhập liệu trên bản chạy

- Kiểm thử gõ qua ký tự thứ tư trên đăng nhập và đăng ký bằng Edge ở kích thước desktop và điện thoại. Nút hiện/ẩn giữ nguyên nội dung và đưa con trỏ về ô mật khẩu.
- Để trống địa chỉ và quốc tịch vẫn đăng ký được. Khi báo lỗi, biểu mẫu giữ thông tin đã nhập và để trống mật khẩu. Đăng nhập hỗ trợ số điện thoại có dấu cách hoặc dấu gạch.
- Kiểm thử nhập tên tiếng Việt, giữ số 0 đầu của giấy tờ khi thêm hoặc bỏ ghế khác, đổi chuyến liên tiếp và không để phản hồi cũ ghi đè danh sách ghế. Kiểm tra thêm ô PNR, nội dung hỗ trợ, mật khẩu nhân viên, lương, giá trị thập phân của voucher và ngày giờ chuyến bay.

### Nâng cấp ghế hết hạn và số điện thoại

Với cơ sở SkyVN đã cài, chạy script nâng cấp riêng trên tên cơ sở hiện có. Script cập nhật định nghĩa SQL trong một giao tác và giữ lại các bảng cùng dữ liệu. Với cơ sở mới, dùng script khởi tạo ở đầu README.

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -f 65001 -d QL_DatVeMayBay -i '.\QL_DatVeMayBay\Database\NangCap_GiuCho_Va_SoDienThoai.sql'
```

- Ghế hết hạn hiển thị trống ngay. Khi đặt hoặc đổi sang ghế đó, SQL giải phóng đơn cũ và hoàn lượt voucher trước khi xử lý giao dịch mới. Ca hai phiên đặt song song chỉ ghi nhận một phiếu mới và một lần hoàn voucher.
- Tác vụ dọn đơn ghi cảnh báo khi cạn pool. Ca kiểm thử đã tái hiện host cũ dừng và xác nhận bản mới dọn được một đơn ở lượt tiếp theo sau khi trả kết nối.
- Số Việt Nam dạng `+84`, `0084` và `0` được đối chiếu về cùng dạng khi đăng ký và đăng nhập. Hai hồ sơ cũ trùng sau chuẩn hóa giữ nguyên mã và lịch sử. Dùng email hoặc chính xác số đã lưu để phân biệt.
- Đã kiểm thử cài mới, nâng cấp lặp trên cơ sở thử đã có dữ liệu, đặt và đổi sang ghế hết hạn, thanh toán và hóa đơn mẫu, đăng ký trùng và đăng nhập qua các dạng số điện thoại.
