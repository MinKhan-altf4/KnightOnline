# TÀI LIỆU LUỒNG HỆ THỐNG GAME

## Phạm vi tài liệu

Tài liệu này tổng hợp các luồng:

1. Tiếp tục bằng phiên đăng nhập đã lưu.
2. Người chơi mới và chế độ chơi thử.
3. Đăng nhập và đăng ký tài khoản.
4. Chọn và tạo nhân vật.
5. Cơ chế safe zone và xuất hiện Ingame.
6. Đăng xuất và lưu trạng thái nhân vật.
7. Hệ thống Heads-Up Display (HUD) Ingame.
8. HUD khi target người chơi, NPC, quái vật và vật thể tương tác.
9. Định hướng luồng nhiệm vụ tân thủ đến cấp 10.

---

# I. QUY ĐỊNH BỔ SUNG

## 1. Quy định tên gọi safe zone

Từ tài liệu này trở đi, mọi thuật ngữ liên quan đến điểm lưu an toàn, bao gồm:

- map spawn safe;
- Spawn Save;
- spawn save;

đều được quy định thống nhất tên gọi là:

> **safe zone**

---

## 2. Component dùng chung: Pop-up Loading Hệ thống

Pop-up Loading được sử dụng chung trong toàn bộ quá trình giao tiếp giữa Client và Server.

### 2.1. Giao diện

- Hiển thị dưới dạng khung thông báo nổi (Modal).
- Làm tối nền phía sau.
- Vô hiệu hóa các nút bấm phía sau để tránh thao tác đúp.

### 2.2. Nội dung hiển thị

- Vòng tròn xoay (Loading spinner).
- Dòng chữ mô tả trạng thái.

Ví dụ:

> “Đang kết nối đến máy chủ. Vui lòng chờ...”

### 2.3. Nút [Đóng] khi request chưa được Server tiếp nhận xử lý

Khi người chơi nhấn **Đóng** trước thời điểm Server tiếp nhận request để xử lý:

- Client gửi yêu cầu hủy chờ kết quả của request hiện tại.
- Pop-up Loading được tắt.
- Người chơi được giữ nguyên tại scene và màn hình hiện tại.
- Người chơi được phép thao tác lại.
- Server không được tạo thay đổi nghiệp vụ nếu xác nhận hủy trước khi bắt đầu xử lý.

### 2.4. Nút [Đóng] khi Server đã tiếp nhận request

Nếu Server đã tiếp nhận request và bắt đầu xử lý hoặc transaction đã hoàn tất:

- Nút **Đóng** không có tác dụng hủy kết quả nghiệp vụ.
- Server vẫn hoàn tất xử lý theo quy tắc của request.
- Server vẫn trả kết quả chính thức cho Client.
- Client bắt buộc tiếp nhận và thực thi kết quả:
  - Chuyển màn hình nếu thành công.
  - Hiển thị lỗi nếu thất bại.
- Nếu Client đã tắt Pop-up Loading trước khi kết quả đến, Client vẫn phải xử lý
  kết quả khi nhận được và không được giữ lại trạng thái cũ trái với Server.

Mỗi request quan trọng phải có `RequestId` để Server phân biệt request mới,
request trùng và request đã xử lý. Việc đóng Pop-up Loading chỉ hủy thao tác chờ
tại Client khi Server xác nhận request chưa bắt đầu; không được mặc định xem là
rollback transaction.

---

# II. LUỒNG 1: NGƯỜI CHƠI CŨ MỞ GAME — TIẾP TỤC BẰNG PHIÊN ĐÃ LƯU

## 1. Điểm bắt đầu

Người chơi đã từng đăng nhập thành công trước đó và thiết bị vẫn còn lưu Refresh Token hợp lệ.

## 2. Giao diện hiển thị

Màn hình khởi động (Entry Screen) gồm:

- Nút **Chơi tiếp: [Tên tài khoản]**.
- Nút **Đổi tài khoản**.
- Nút **Chọn máy chủ (Server)**.

Nút **Đổi tài khoản** chuyển sang Luồng 3.

## 3. Thao tác của người chơi

Người chơi nhấn nút **Chơi tiếp**.

## 4. Phản hồi của hệ thống

- Hiển thị Pop-up Loading.
- Khóa tương tác màn hình.
- Server xác thực Refresh Token.
- Server kiểm tra trạng thái hoạt động (Active) của tài khoản.

## 5. Điểm kết thúc thành công

Nếu tài khoản chưa Active ở nơi khác:

- Đóng Pop-up Loading.
- Server chiếm Active lease cho connection hiện tại.
- Chuyển người chơi đến màn hình Chọn nhân vật (Character Select).

Character Select được tính là trạng thái **Active** của tài khoản.

- Một tài khoản chỉ có một Active lease hợp lệ tại cùng thời điểm.
- Thiết bị khác không được chiếm Active lease và không được làm mất kết nối
  thiết bị đang Active.
- Khi connection tại Character Select mất kết nối, quay lại Entry hoặc đăng
  xuất hợp lệ, Server giải phóng đúng Active lease mà connection đó sở hữu.
- Active lease phải có heartbeat, thời hạn và connection/session generation để
  connection cũ không thể giải phóng lease của connection mới.

## 6. Xử lý ngoại lệ

### 6.1. Tài khoản Active ở nơi khác

- Đóng Pop-up Loading.
- Giữ người chơi tại Entry Screen.
- Hiển thị Pop-up thông báo tài khoản đang được sử dụng.

### 6.2. Token hết hạn

- Đóng Pop-up Loading.
- Yêu cầu người chơi đăng nhập lại.

---

# III. LUỒNG 2: NGƯỜI CHƠI MỚI MỞ GAME LẦN ĐẦU — CHƠI THỬ

## 1. Điểm bắt đầu

Người chơi vừa tải ứng dụng và mở game lần đầu.

Tại thời điểm này:

- Hệ thống ở trạng thái chờ.
- Chưa gửi request lên Server.

## 2. Giao diện hiển thị

Entry Screen gồm:

- Nút **Chơi mới**.
- Nút **Có tài khoản**.
- Nút **Chọn máy chủ (Server)**.

Nút **Có tài khoản** chuyển sang Luồng 3.

## 3. Thao tác của người chơi

Người chơi nhấn **Chơi mới**.

## 4. Phản hồi của hệ thống

- Hiển thị Pop-up Loading.
- Server cấp và xác thực Refresh Token dành cho tài khoản khách.
- Server cấp quyền chơi thử tối đa đến cấp độ 10.

## 5. Điểm kết thúc thành công

- Đóng Pop-up Loading.
- Chuyển người chơi đến màn hình Chọn nhân vật.

## 6. Xử lý ngoại lệ

Nếu mất kết nối hoặc Token chơi mới gặp lỗi:

- Đóng Pop-up Loading.
- Trả người chơi về Entry Screen.
- Hiển thị Pop-up:

> “Bạn mất kết nối server.”

---

# IV. LUỒNG 3: ĐĂNG NHẬP VÀ ĐĂNG KÝ TÀI KHOẢN

## 1. Điểm bắt đầu

Người chơi thực hiện một trong hai thao tác:

- Nhấn **Có tài khoản** từ Luồng 2.
- Nhấn **Đổi tài khoản** từ Luồng 1.

## 2. Giao diện hiển thị

Hệ thống hiển thị Form gồm:

- Ô Tài khoản.
- Ô Mật khẩu.
- Nút **Xác nhận đăng nhập**.
- Nút **Đăng ký**.

## 3. Thao tác đăng nhập

Người chơi nhập thông tin và nhấn **Xác nhận đăng nhập**.

Hệ thống:

- Hiển thị Pop-up Loading.
- Thực hiện quy trình xác thực tương ứng với Luồng 1.

## 4. Thao tác đăng ký

Khi người chơi nhấn **Đăng ký**:

- Ứng dụng chuyển hướng đến website hoặc forum chính thức của game.

## 5. Xử lý sau đăng ký

Sau khi đăng ký thành công trên web và quay lại ứng dụng:

- Trò chơi yêu cầu làm mới trạng thái.
- Người chơi phải nhập lại Tài khoản và Mật khẩu vừa tạo.
- Hệ thống chạy lại quy trình đăng nhập.

## 6. Điểm kết thúc hợp lệ

- Đóng Pop-up Loading.
- Server xác thực thành công.
- Chuyển người chơi đến màn hình Chọn nhân vật.

## 7. Xử lý ngoại lệ

Nếu sai thông tin:

- Đóng Pop-up Loading.
- Giữ người chơi tại màn hình hiện tại.
- Hiển thị Pop-up:

> “Sai tài khoản hoặc mật khẩu.”

---

# V. LUỒNG GIAO ĐIỂM: CHỌN VÀ TẠO NHÂN VẬT

## 1. Điểm bắt đầu

Người chơi xác thực đăng nhập thành công từ Luồng 1, Luồng 2 hoặc Luồng 3.

## 2. Giao diện danh sách nhân vật

- Mỗi tài khoản có tối đa 3 slot nhân vật.
- Mỗi slot được thể hiện bằng một bục đứng.
- Slot đã có nhân vật hiển thị nhân vật tương ứng.
- Slot trống cho phép tạo nhân vật mới.

## 3. Nút tương tác

- Chọn nhân vật đã có: hiển thị nút **Bắt đầu chơi**.
- Chọn slot trống: hiển thị nút **Tạo nhân vật**.

## 4. Giao diện tạo nhân vật

Bao gồm:

- Lớp nhân vật (Class).
- Giới tính Nam/Nữ.
- Ngoại hình:
  - Tóc.
  - Khuôn mặt.
- Ô nhập Tên nhân vật.
- Giới hạn tên từ 6 đến 20 ký tự.
- Nút **Xác nhận tạo**.
- Nút **Quay lại**.

## 5. Giới hạn tạo mới

Mỗi tài khoản được tạo tối đa 3 nhân vật.

## 6. Thao tác tạo mới

Người chơi:

1. Chọn slot trống.
2. Chọn Class.
3. Chọn giới tính.
4. Chọn ngoại hình.
5. Nhập tên.
6. Nhấn **Xác nhận tạo**.

## 7. Thao tác chọn nhân vật có sẵn

Người chơi:

1. Chọn nhân vật.
2. Nhấn **Bắt đầu chơi**.
3. Chuyển sang Luồng Vào Ingame.

## 8. Phản hồi hệ thống khi tạo nhân vật

### 8.1. Hiển thị Loading

Hiển thị Pop-up Loading:

> “Đang khởi tạo nhân vật...”

### 8.2. Kiểm tra tại Client

- Kiểm tra độ dài tên.
- Nếu tên dưới 6 hoặc trên 20 ký tự, không gửi request.

### 8.3. Kiểm tra tại Gateway

Tên hợp lệ về độ dài được đưa qua Profanity Filter để kiểm tra:

- Từ cấm.
- Từ văng tục.
- Nội dung vi phạm chuẩn mực game.

### 8.4. Kiểm tra Database

Server đối chiếu tên với cơ sở dữ liệu toàn Server để bảo đảm tên chưa được sử dụng.

### 8.5. Khởi tạo dữ liệu

Khi vượt qua toàn bộ kiểm tra:

- Server lưu Class.
- Server lưu giới tính.
- Server lưu ngoại hình.
- Server lưu tên.
- Server tạo hồ sơ dữ liệu nhân vật trong slot tương ứng.

## 9. Điểm kết thúc thành công

- Đóng Pop-up Loading.
- Server tự chọn nhân vật vừa được tạo cho connection hiện tại.
- Hiển thị Loading tải map.
- Chuyển thẳng người chơi vào map tân thủ.
- Nhân vật xuất hiện tại safe zone mặc định của map tân thủ.

Luồng này bỏ thao tác quay lại Character Select và nhấn **Bắt đầu chơi** đối với
nhân vật vừa tạo. Khi người chơi đăng nhập ở những lần sau, nhân vật vẫn xuất
hiện bình thường tại slot tương ứng trong Character Select.

## 10. Xử lý ngoại lệ

Thông báo phải nêu rõ nguyên nhân:

- “Tên nhân vật phải từ 6 đến 20 ký tự.”
- “Tên nhân vật chứa từ ngữ không hợp lệ. Vui lòng đặt tên khác.”
- “Tên nhân vật đã tồn tại. Vui lòng chọn tên khác.”

---

# VI. LUỒNG 4: CƠ CHẾ SAFE ZONE VÀ XUẤT HIỆN INGAME

## 1. Điểm bắt đầu

Người chơi hoàn tất chọn nhân vật và bắt đầu tải vào thế giới game.

## 2. Điểm lưu mặc định

Nhân vật mới luôn xuất hiện tại:

> **Làng tân thủ — safe zone đầu tiên**

## 3. Cập nhật safe zone

Khi nhân vật đi qua một bản đồ hỗ trợ safe zone:

- Server tự động ghi nhớ.
- Safe zone mới nhất trở thành Last Save Point.
- Lần đăng nhập sau có thể xuất hiện tại khu vực an toàn của map đó.

## 4. Đăng xuất tại map thường dưới 10 phút

Nếu người chơi thoát game ở map không có safe zone và quay lại trong thời gian dưới 10 phút:

- Giữ nguyên Map ID.
- Giữ nguyên tọa độ cũ.
- Nhân vật xuất hiện tại vị trí trước khi thoát.

## 5. Đăng xuất tại map thường quá 10 phút

Nếu thời gian offline vượt quá 10 phút:

- Không giữ nguyên tọa độ map thường.
- Đưa nhân vật về safe zone gần nhất đã đi qua.
- Vị trí xuất hiện được chọn hợp lệ trong vùng an toàn.

## 6. Phục hồi trạng thái

Khi người chơi vào lại game, hệ thống tiếp tục xử lý:

- HP.
- MP hoặc năng lượng.
- Cooldown.
- Buff.
- Debuff.

## 7. Loading tải map

Khi chuyển từ Character Select sang Ingame:

- Hiển thị Loading Bar hoặc Loading Spinner.
- Tải tài nguyên bản đồ.
- Chờ Server hoàn tất xác nhận dữ liệu.

## 8. Cập nhật điểm lưu ngầm

Server:

- Gắn Làng tân thủ làm safe zone mặc định cho nhân vật mới.
- Ghi đè Last Save Point khi nhân vật bước vào safe zone mới.

## 9. Kiểm tra điều kiện xuất hiện

Server so sánh Timestamp đăng xuất:

- Offline dưới 10 phút: gọi tọa độ chính xác tại map hiện tại.
- Offline trên 10 phút: gọi tọa độ hợp lệ tại Last Save Point.

## 10. Tải dữ liệu nhân vật

Server truy xuất:

- HP.
- MP.
- Cooldown còn lại.
- Buff/Debuff còn lại.
- Map ID.
- Tọa độ xuất hiện.

## 11. Điểm kết thúc

- Loading kết thúc.
- Nhân vật xuất hiện tại tọa độ hợp lệ.
- Chỉ số và trạng thái được đồng bộ.
- Người chơi có thể bắt đầu điều khiển.

## 12. Thông báo hệ thống

Có thể hiển thị:

> “Bạn đã rời mạng quá lâu, tự động đưa về khu vực an toàn.”

Hoặc:

> “Đã cập nhật điểm lưu mới.”

## 13. Quy tắc di chuyển Ingame

### 13.1. Dữ liệu Client gửi

Client không tự quyết định tọa độ chính thức của nhân vật.

Khi người chơi thao tác di chuyển:

- Client gửi **ý định di chuyển** (`MoveIntent`).
- `MoveIntent` chứa tọa độ đích mà người chơi muốn đi tới.
- Client không gửi tọa độ hiện tại như một kết quả đã được tự xác nhận.
- Server kiểm tra trạng thái nhân vật, map, điểm đích, đường đi và tốc độ trước
  khi chấp nhận.
- Server mô phỏng hoặc quyết định quá trình di chuyển và cập nhật tọa độ chính thức.
- Client chỉ hiển thị dự đoán khi cần để thao tác mượt và phải đồng bộ lại theo
  trạng thái Server.

Không tin tọa độ đích do Client gửi chỉ vì dữ liệu có đúng định dạng. Điểm đích
vẫn phải được Server kiểm tra để hạn chế speed hack, teleport hack, đi xuyên vật
thể và các công cụ sửa gói tin.

### 13.2. Tốc độ di chuyển

Tốc độ được biểu diễn theo hệ số cấu hình:

- Đi bộ: hệ số tốc độ cơ sở `1x`.
- Cưỡi thú cưỡi: hệ số tốc độ cơ sở `2x`.

Các hệ số này là giá trị thiết kế ban đầu, không phải giá trị hardcode cố định
trong packet hoặc Client. Server lấy tốc độ cuối cùng từ trạng thái nhân vật,
thú cưỡi, buff/debuff, map và luật của chế độ chơi.

Client không được tự khai báo rằng nhân vật đang cưỡi thú hoặc tự gửi tốc độ
cuối cùng.

### 13.3. Va chạm và giới hạn bản đồ

Nhân vật không được đi xuyên qua:

- Vật thể có va chạm.
- NPC.
- Người chơi khác.
- Quái vật.
- Ranh giới và vùng nằm ngoài map.
- Khu vực bị Server xác định là không thể đi vào.

Quái vật và người chơi có thể chặn đường theo quy tắc va chạm vật lý của thế
giới game. Server là nơi quyết định kết quả va chạm cuối cùng.

Hệ thống phải chừa khả năng bổ sung luật chống lợi dụng việc chặn đường, ví dụ
safe zone, điểm spawn, cổng dịch chuyển hoặc khu vực NPC quan trọng.

### 13.4. Dịch chuyển

Teleport không được xử lý như di chuyển thông thường.

- Server xác định và đặt tọa độ dịch chuyển chính thức.
- Điểm đến phải thuộc danh sách hoặc vùng dịch chuyển hợp lệ.
- Sau khi teleport, Server gửi trạng thái vị trí mới cho Client.
- Client đặt lại dự đoán di chuyển theo tọa độ Server để tránh sai lệch.
- MoveIntent cũ trước thời điểm teleport không được tiếp tục áp dụng.

### 13.5. Di chuyển khi sử dụng kỹ năng

Trong combat PvE giai đoạn hiện tại:

- Khi bắt đầu sử dụng skill yêu cầu đứng yên, nhân vật dừng di chuyển.
- MoveIntent đang thực hiện bị dừng hoặc hủy theo luật của skill.
- Client không được tiếp tục dịch chuyển nhân vật trong lúc Server xác định
  skill đang khóa di chuyển.

Thiết kế phải chừa khả năng mở rộng cho PvP/Ranked sau này:

- Nhân vật có thể vừa di chuyển vừa thi triển một số kỹ năng.
- Hỗ trợ kỹ năng định hướng và combat non-target.
- Khả năng di chuyển trong lúc dùng skill được cấu hình theo từng skill và chế
  độ chơi, không hardcode chung cho mọi class.

### 13.6. Mất kết nối khi đang di chuyển

Khi mất kết nối:

- Server dừng tiếp nhận MoveIntent ngay lập tức.
- Connection bị disconnect và chuyển sang Luồng 4 để xác định trạng thái xuất
  hiện ở lần kết nối tiếp theo.
- Tọa độ chính thức cuối cùng do Server ghi nhận được sử dụng; không sử dụng tọa
  độ chưa được Server xác nhận từ Client.
- Nếu nhân vật đang trong combat, áp dụng quy tắc thua do disconnect tại Luồng 5.

---

# VII. LUỒNG 5: ĐĂNG XUẤT VÀ LƯU TRẠNG THÁI NHÂN VẬT

## 1. Điểm bắt đầu

Người chơi đang Ingame và xảy ra một trong các trường hợp:

- Chọn Đăng xuất.
- Đóng ứng dụng.
- Mất mạng.
- Force Close.

## 2. Đăng xuất chủ động

Nút **Đăng xuất** nằm trong Cài đặt.

Khi nhấn:

- Hiển thị Pop-up xác nhận:

> “Bạn có chắc chắn muốn đăng xuất?”

## 3. Thao tác của người chơi

Người chơi nhấn **Đồng ý**.

## 4. Phản hồi hệ thống

Hiển thị Pop-up Loading:

> “Đang lưu dữ liệu...”

## 5. Xử lý dữ liệu

Server đóng băng trạng thái hiện tại của nhân vật và ghi dữ liệu vào hệ thống.

Server không phụ thuộc vào một request lưu cuối cùng từ Client. Trạng thái nhân
vật authoritative phải được Server duy trì trong suốt phiên chơi và lưu bằng:

- Checkpoint định kỳ.
- Ghi nhận khi có thay đổi quan trọng.
- Transaction phù hợp với dữ liệu cần bảo toàn.

Disconnect chỉ kết thúc connection/session và chốt trạng thái Server đã xác
nhận. Dữ liệu cuối cùng do Client tự khai báo không được dùng làm nguồn sự thật.

## 6. Dữ liệu vị trí và sinh tồn

Server lưu:

- Map ID.
- Tọa độ X, Y, Z.
- HP hiện tại.
- MP hiện tại.

## 7. Dữ liệu thời gian

Server lưu:

- Cooldown kỹ năng còn lại.
- Thời gian Buff còn lại.
- Thời gian Debuff còn lại.

Đăng xuất không hủy các thông số này.

## 8. Timestamp

Server lưu mốc thời gian đăng xuất để Luồng 4 sử dụng khi người chơi đăng nhập lại.

## 9. Đăng xuất chủ động thành công

- Đóng Pop-up Loading.
- Rời Ingame.
- Trở về Entry Screen.

## 10. Đăng xuất bị động

Khi mất mạng hoặc Force Close:

- Server tự động xử lý dữ liệu ngầm.
- Không phụ thuộc vào Client gửi lệnh lưu hoàn chỉnh.
- Bảo đảm nhân vật không bị kẹt trong game.

## 11. Đăng xuất hoặc mất kết nối khi đang combat

Nếu nhân vật đang trong trạng thái combat mà xảy ra bất kỳ trường hợp nào:

- Người chơi chọn đăng xuất.
- Đóng ứng dụng.
- Force Close.
- Mất mạng.
- Connection bị gián đoạn hoặc bị disconnect vì nguyên nhân khác.

Thì phiên combat được tính là **thua**.

- Trong PvE, nhân vật được xử lý như bị hạ gục bởi combat hiện tại.
- Trong PvP/PK/Ranked sau này, đối thủ hoặc bên chiến thắng được xác định theo
  luật của chế độ.
- Server quyết định kết quả, cập nhật trạng thái chết/thua và lưu kết quả.
- Client không thể tránh kết quả bằng cách ngắt mạng hoặc đóng ứng dụng.
- Khi vào lại, người chơi tiếp tục từ trạng thái sau thất bại do Server ghi nhận,
  không quay lại trạng thái trước combat.

Nhà phát hành có trách nhiệm tối ưu chất lượng kết nối và cơ chế vận hành để
giảm trường hợp mất mạng ngoài ý muốn. Tuy nhiên, luật gameplay không phân biệt
disconnect chủ động với sự cố mạng vì Server không thể tin cậy xác định ý định
thật của Client.

---

# VIII. LUỒNG 6: HỆ THỐNG HEADS-UP DISPLAY — HUD INGAME

## 1. Mục đích

HUD là lớp giao diện hiển thị trong lúc người chơi đang Ingame.

HUD có nhiệm vụ:

- Hiển thị trạng thái nhân vật.
- Hiển thị trạng thái mục tiêu.
- Cung cấp điều khiển di chuyển và chiến đấu.
- Cho phép truy cập nhanh các chức năng cần thiết.
- Gom chức năng để tiết kiệm diện tích màn hình.
- Hạn chế hiển thị thường trực các thông tin không cần thiết.

HUD không tự quyết định dữ liệu gameplay. Dữ liệu hiển thị phải dựa trên trạng thái được Server xác nhận.

---

## 2. Cấu trúc tổng thể

```text
HUD Ingame
├── Khung Trạng thái Nhân vật
├── Khung Thông tin Mục tiêu
├── Dấu Chỉ Mục tiêu
├── Khu vực điều khiển chiến đấu
├── Ô Tương tác Ngữ cảnh
├── Minimap
├── Thanh Chức năng Thu gọn
├── Hệ thống Thông báo
└── Các cửa sổ Pop-up
```

---

# IX. KHUNG TRẠNG THÁI NHÂN VẬT

## 1. Quy định tên gọi

Các tên gọi cũ như:

- Khung HP/MP.
- Thanh máu góc trái.
- Khung nhân vật góc trên.
- Khung thông tin người chơi.

được thống nhất gọi là:

> **Khung Trạng thái Nhân vật**

Tên kỹ thuật:

```text
PlayerStatusPanel
```

Không dùng tên `CharacterPanel` để tránh nhầm với cửa sổ thông tin chi tiết nhân vật.

## 2. Vị trí

- Nằm ở góc trên bên trái màn hình.
- Luôn hiển thị khi Ingame, trừ cảnh đặc biệt ẩn toàn bộ HUD.

## 3. Nội dung hiển thị

- Avatar nếu có.
- HP hiện tại / HP tối đa.
- MP hoặc năng lượng hiện tại / tối đa.
- Level.
- Tiến độ EXP.

```text
PlayerStatusPanel
├── Avatar
├── HpBar
├── MpBar
├── LevelText
├── ExpBar
└── MenuInteractionArea
```

## 4. Thao tác

Toàn bộ Khung Trạng thái Nhân vật là vùng có thể nhấn.

Khi nhấn:

- Mở Menu nhân vật.
- Không cần icon Hành trang riêng ngoài HUD.
- Tiết kiệm diện tích màn hình.

Trong lần đầu, game có thể hướng dẫn:

> “Chạm vào Khung Trạng thái Nhân vật để mở Menu nhân vật.”

---

# X. MENU NHÂN VẬT

## 1. Cách hiển thị

- Mở dưới dạng cửa sổ nổi trên Ingame.
- Không chuyển scene.
- Khóa thao tác chạm xuống bản đồ để tránh di chuyển nhầm.
- Đóng bằng nút `X` hoặc nhấn lại Khung Trạng thái Nhân vật.

## 2. Các tab

```text
Menu nhân vật
├── Hành trang
├── Trang bị
├── Kỹ năng
├── Chỉ số nhân vật
├── Nhiệm vụ
└── Cài đặt
```

## 3. Hành trang

Hiển thị:

- Vật phẩm.
- Nguyên liệu.
- Vật phẩm tiêu hao.
- Vật phẩm nhiệm vụ.
- Số lượng.
- Bộ lọc hoặc tab phân loại.

## 4. Trang bị

Hiển thị:

- Vũ khí.
- Áo giáp.
- Phụ kiện.
- Các slot trang bị.
- Thông tin trang bị đang sử dụng.

## 5. Kỹ năng

Hiển thị:

- Kỹ năng đã học.
- Cấp kỹ năng.
- Mô tả kỹ năng.
- Bố trí kỹ năng vào các ô sử dụng nhanh.

## 6. Chỉ số nhân vật

Hiển thị:

- HP.
- MP.
- Công.
- Thủ.
- Chí mạng.
- Tốc độ.
- Các chỉ số khác được bổ sung sau.

## 7. Nhiệm vụ

Hiển thị:

- Nhiệm vụ chính.
- Nhiệm vụ phụ.
- Nhiệm vụ hằng ngày.
- Tiến độ.
- Phần thưởng.
- Trạng thái.

## 8. Cài đặt

Tên **Cài đặt** được dùng thay cho **Chức năng** để tránh trùng vai trò với Thanh Chức năng Thu gọn.

### 8.1. Hiển thị

- Ẩn/hiện người chơi khác.
- Giới hạn số lượng người chơi được hiển thị.
- Ẩn/hiện tên người chơi.
- Tắt hiệu ứng kỹ năng của người chơi khác.
- Tắt hiệu ứng skin/thời trang của người chơi khác.
- Ẩn pet của người chơi khác.
- Ẩn thú cưỡi của người chơi khác.

### 8.2. Âm thanh

- Âm lượng tổng.
- Nhạc nền.
- Hiệu ứng âm thanh.
- Âm thanh thông báo.

### 8.3. Điều khiển

- Mở chế độ **Chỉnh sửa bố cục chiến đấu**.
- Thay đổi vị trí và kích thước joystick hoặc cụm nút di chuyển.
- Thay đổi vị trí và kích thước nút đánh thường.
- Thay đổi vị trí và kích thước từng nút kỹ năng.
- Thay đổi vị trí và kích thước nút **Đổi Mục tiêu**.
- Thay đổi vị trí và kích thước nút **Đổi Trang Kỹ năng**.
- Thay đổi vị trí và kích thước **Ô Tương tác Ngữ cảnh**.
- Chỉnh độ trong suốt của các nút điều khiển.
- Khóa bố cục HUD.
- Khôi phục bố cục mặc định.

Mục tiêu của chức năng này là giúp người chơi:

- Bố trí thao tác phù hợp với kích thước màn hình và cách cầm thiết bị.
- Đặt nút skill, nút đánh và nút Đổi Mục tiêu tại vị trí thuận tay.
- Chuyển target nhanh và sử dụng skill liên tục trong PK.
- Giảm thao tác nhầm khi nhiều nút chiến đấu xuất hiện cùng lúc.

#### 8.3.1. Chế độ Chỉnh sửa bố cục chiến đấu

Khi mở chế độ chỉnh sửa:

- Gameplay phía sau tạm ngừng nhận thao tác điều khiển.
- Các thành phần được phép chỉnh sửa hiển thị khung bao.
- Người chơi kéo thành phần để thay đổi vị trí.
- Người chơi dùng tay cầm hoặc thanh trượt để thay đổi kích thước.
- Thành phần đang chỉnh sửa phải hiển thị rõ trạng thái được chọn.
- Có thể xem trước bố cục trước khi lưu.

Các nút điều khiển trong chế độ này:

- **Lưu**: xác nhận bố cục hiện tại.
- **Hủy**: bỏ các thay đổi chưa lưu.
- **Khôi phục mặc định**: trả toàn bộ bố cục về thiết lập ban đầu.

#### 8.3.2. Thành phần được phép chỉnh sửa

```text
Bố cục điều khiển chiến đấu
├── Joystick hoặc cụm nút di chuyển
├── Nút đánh thường
├── Từng nút kỹ năng
├── Nút Đổi Mục tiêu
├── Nút Đổi Trang Kỹ năng
└── Ô Tương tác Ngữ cảnh
```

**Dấu Chỉ Mục tiêu** là mũi tên bám theo mục tiêu trong thế giới game nên không
thuộc thành phần kéo thả thủ công.

**Khung Thông tin Mục tiêu** cố định bên trái Minimap và không được thay đổi vị
trí hoặc kích thước. Quy tắc này giúp thông tin target luôn nằm tại một vị trí
nhất quán khi người chơi quan sát và PK.

#### 8.3.3. Quy tắc vị trí và kích thước

- Mỗi thành phần có giới hạn kích thước tối thiểu và tối đa để vẫn có thể thao tác.
- Không cho phép kéo toàn bộ thành phần ra ngoài vùng an toàn của màn hình.
- Cảnh báo khi các nút quan trọng chồng lấp làm cản trở thao tác.
- Bố cục phải thích ứng với tỷ lệ màn hình, vùng tai thỏ và cạnh bo của thiết bị.
- Khi đổi độ phân giải hoặc tỷ lệ màn hình, hệ thống giữ vị trí tương đối thay vì
  sử dụng tọa độ pixel cố định.
- Thay đổi bố cục không làm thay đổi phạm vi, cooldown, sức mạnh hoặc luật chọn
  mục tiêu của gameplay.

#### 8.3.4. Bố cục phục vụ PK

Người chơi được phép lưu bố cục chiến đấu thuận tiện cho PK, trong đó:

- Nút Đổi Mục tiêu có thể đặt gần cụm skill.
- Các skill cần phản ứng nhanh có thể được phóng to.
- Những nút ít dùng có thể thu nhỏ hoặc đặt xa vùng thao tác chính.
- Khung Thông tin Mục tiêu giữ cố định bên trái Minimap để người chơi luôn biết
  vị trí quan sát HP và trạng thái đối thủ.

Giai đoạn đầu tối thiểu phải có một bố cục tùy chỉnh. Hệ thống có thể mở rộng
thêm nhiều cấu hình như **Mặc định**, **PvE** và **PK** sau này mà không thay đổi
quy tắc điều khiển cốt lõi.

#### 8.3.5. Lưu bố cục

- Bố cục HUD được lưu riêng theo thiết bị vì kích thước và tỷ lệ màn hình khác nhau.
- Chỉ lưu dữ liệu trình bày như vị trí, kích thước và độ trong suốt.
- Không dùng dữ liệu bố cục HUD để quyết định kết quả gameplay.
- Khi dữ liệu bố cục bị thiếu, lỗi hoặc không còn tương thích, sử dụng bố cục mặc định.
- Gỡ ứng dụng sẽ xóa bố cục đã lưu tại thiết bị.

### 8.4. Hiệu năng

- Chất lượng đồ họa.
- Giới hạn FPS.
- Chế độ tiết kiệm pin.
- Giảm hiệu ứng.
- Giảm số đối tượng hiển thị.

### 8.5. Tài khoản

- Đổi nhân vật.
- Đăng xuất.
- Các lựa chọn tài khoản bổ sung sau này.

Đăng xuất tiếp tục sử dụng Luồng 5.

---

# XI. HIỂN THỊ TIỀN TỆ

## 1. Quy định chung

Trong giai đoạn HUD hiện tại:

- Vàng không hiển thị thường trực ngoài Ingame HUD.
- Ngọc không hiển thị thường trực ngoài Ingame HUD.
- Người chơi chỉ xem số dư khi mở Menu nhân vật.

## 2. Vị trí

Vàng và Ngọc nằm cố định trong Menu nhân vật, không thuộc riêng tab Hành trang.

Ví dụ:

```text
Vàng: 295.636
Ngọc: 99
```

## 3. Thông báo biến động

Khi nhận hoặc tiêu tiền, HUD có thể hiện thông báo ngắn:

```text
+1.500 vàng
-20 ngọc
```

Quy định:

- Không khóa thao tác.
- Tự biến mất.
- Không hiển thị toàn bộ số dư ngoài HUD.

---

# XII. KHU VỰC ĐIỀU KHIỂN CHIẾN ĐẤU

Các thành phần luôn hiển thị cơ bản:

- Joystick hoặc nút di chuyển.
- Nút đánh thường.
- Các nút kỹ năng.
- Nút Đổi Mục tiêu.
- Nút Đổi Trang Kỹ năng.
- Cooldown.
- Vật phẩm chiến đấu nếu có.
- Ô Tương tác Ngữ cảnh.

Người chơi được phép thay đổi vị trí và kích thước các nút điều khiển chiến đấu
trong Cài đặt, bao gồm joystick hoặc cụm nút di chuyển, nút đánh thường, từng
nút skill, nút Đổi Mục tiêu, nút Đổi Trang Kỹ năng và Ô Tương tác Ngữ cảnh.
Khung Thông tin Mục tiêu không thuộc nhóm tùy chỉnh và luôn cố định bên trái
Minimap.

Định hướng combat được chia làm hai giai đoạn riêng:

- Combat Target dành cho PvE và cày cuốc.
- Combat Non-target dành cho Ranked.

Chi tiết combat sẽ được thiết kế ở luồng riêng.

---


## 3. Nút Đổi Mục tiêu

Tên kỹ thuật đề xuất:

```text
TargetSwitchButton
```

Chức năng:

- Chuyển nhanh sang một mục tiêu hợp lệ khác trong phạm vi cho phép.
- Khi nhấn liên tục, hệ thống lần lượt duyệt qua các mục tiêu hợp lệ.
- Dấu Chỉ Mục tiêu và Khung Thông tin Mục tiêu phải cập nhật theo mục tiêu mới.
- Trong PvE, ưu tiên quái vật hoặc đối tượng nhiệm vụ phù hợp.
- Trong PK hoặc Ranked, ưu tiên người chơi đối địch hợp lệ theo luật của chế độ.
- Không chọn mục tiêu đã chết, ngoài phạm vi, không thể tương tác hoặc bị Server xác định là không hợp lệ.
- Mục tiêu cuối cùng phải được Server xác nhận.

## 4. Nút Đổi Trang Kỹ năng

Tên kỹ thuật đề xuất:

```text
SkillPageSwitchButton
```

Chức năng:

- Chuyển đổi giữa các trang kỹ năng đã được người chơi thiết lập.
- Trang 1 ưu tiên kỹ năng tấn công chính.
- Trang 2 dùng cho các kỹ năng Buff, Trói, Khống chế hoặc Hỗ trợ.
- Khi đổi trang, icon kỹ năng và trạng thái cooldown phải cập nhật ngay.
- Cooldown vẫn tiếp tục đếm dù kỹ năng đang nằm ở trang không hiển thị.
- Đổi trang không được reset cooldown hoặc bỏ qua điều kiện sử dụng kỹ năng.
- Người chơi sắp xếp kỹ năng vào từng trang trong tab Kỹ năng của Menu nhân vật.
- Giai đoạn đầu ưu tiên tối đa 2 trang để tránh gây rối thao tác.

Cấu trúc dự kiến:

```text
Skill Pages
├── Trang 1: Kỹ năng tấn công chính
└── Trang 2: Buff / Trói / Khống chế / Hỗ trợ
```


# XIII. THANH CHỨC NĂNG THU GỌN

## 1. Mục đích

Các tính năng không cần xuất hiện liên tục được gom trong một thanh mở bằng mũi tên.

## 2. Trạng thái đóng

- Chỉ hiện mũi tên.
- Các icon chức năng được ẩn.
- Tăng diện tích quan sát.

## 3. Trạng thái mở

- Các icon bung ra thành hàng hoặc cụm.
- Người chơi chọn chức năng cần sử dụng.

## 4. Danh sách chức năng dự kiến

- Auto.
- Hộp thư.
- Bang hội.
- Đội nhóm.
- Chat thế giới.
- Thú cưỡi.
- Cảm xúc.
- Hoạt động.
- Sự kiện.
- Các chức năng gameplay bổ sung.

Không đưa các mục sau vào thanh này vì đã nằm trong Menu nhân vật:

- Hành trang.
- Trang bị.
- Kỹ năng.
- Chỉ số nhân vật.
- Nhiệm vụ.
- Cài đặt.

---

# XIV. POP-UP AUTO

## 1. Cách mở

1. Nhấn mũi tên.
2. Mở Thanh Chức năng Thu gọn.
3. Nhấn icon Auto.
4. Hiển thị Pop-up Auto.

## 2. Thiết lập ban đầu

```text
Cài đặt Auto
├── Tự động đánh
├── Tự động nhặt đồ
├── Tự động bơm máu
├── Tự động sử dụng Buff
├── Tự động lên ngựa
└── Tự động xuống ngựa
```

## 3. Quy định trạng thái

- Mỗi tính năng có bật/tắt riêng.
- Nên dùng checkbox hoặc công tắc.
- Trạng thái phải nhìn thấy ngay.
- Không chỉ dùng câu lệnh kiểu “Tắt tự động đánh” vì dễ gây nhầm trạng thái.

Chi tiết vận hành Auto sẽ thuộc luồng Combat Target.

---

# XV. HỆ THỐNG THÔNG BÁO TIN NHẮN

## 1. Tên gọi

Hệ thống gồm:

- **Banner Thông báo Nhanh**.
- **Dấu Chưa xem**.
- **Hộp thư**.

## 2. Khi có tin nhắn mới

1. Banner trượt từ trên xuống.
2. Banner nhấp nháy nhẹ vài lần.
3. Banner tự thu lại.
4. Dấu Chưa xem xuất hiện trên mũi tên.
5. Người chơi vẫn tiếp tục điều khiển bình thường.

Banner không được:

- Khóa thao tác.
- Che Khung Trạng thái Nhân vật.
- Che Minimap.
- Che khu vực kỹ năng quan trọng.

## 3. Khi mở Thanh Chức năng Thu gọn

- Chấm đỏ trên mũi tên dẫn người chơi đến chức năng có nội dung mới.
- Icon Hộp thư hiển thị chấm đỏ hoặc số thư chưa đọc.
- Có thể hiển thị `9+` khi số lượng lớn.

## 4. Điều kiện xóa Dấu Chưa xem

Dấu Chưa xem không biến mất chỉ vì người chơi đã mở thanh chức năng.

Chỉ xóa khi:

- Người chơi mở Hộp thư.
- Nội dung mới đã được xem.
- Hệ thống ghi nhận trạng thái đã đọc.

---

# XVI. HUD MỤC TIÊU

## 1. Thành phần

Hệ thống target gồm:

```text
Target System
├── Dấu Chỉ Mục tiêu
├── Khung Thông tin Mục tiêu
└── Ô Tương tác Ngữ cảnh
```

## 2. Loại mục tiêu hỗ trợ

- Người chơi.
- NPC.
- Quái vật.
- Vật thể tương tác:
  - Bảng khu.
  - Cột đá.
  - Rương.
  - Cổng.
  - Công tắc.
  - Vật phẩm nhiệm vụ.
  - Vật thể đặc biệt khác.

---

# XVII. DẤU CHỈ MỤC TIÊU

## 1. Tên gọi

> **Dấu Chỉ Mục tiêu**

Tên kỹ thuật:

```text
TargetIndicator
```

## 2. Hiển thị

Khi target đúng một đối tượng hợp lệ:

- Một mũi tên trỏ xuống xuất hiện trên đầu mục tiêu.
- Mũi tên nhấp nháy.
- Chỉ mục tiêu hiện tại mới có mũi tên.

## 3. Cập nhật

- Đổi target: mũi tên chuyển sang mục tiêu mới.
- Mất target: mũi tên biến mất.
- Mục tiêu chết, bị phá hủy, rời map hoặc không hợp lệ: mũi tên tắt.
- Server xác nhận đổi target trước khi cập nhật trạng thái chính thức.

---

# XVIII. KHUNG THÔNG TIN MỤC TIÊU

## 1. Tên gọi

> **Khung Thông tin Mục tiêu**

Tên kỹ thuật:

```text
TargetStatusPanel
```

## 2. Vị trí

- Nằm bên trái Minimap.
- Minimap nằm sát góc trên bên phải màn hình.
- Tách khỏi Khung Trạng thái Nhân vật.
- Không đặt toàn bộ thông tin trên đầu mục tiêu để tránh rối màn hình.
- Vị trí và kích thước của Khung Thông tin Mục tiêu được cố định, không thuộc
  chế độ Chỉnh sửa bố cục chiến đấu.

## 3. Nội dung cơ bản

Tùy loại target, khung có thể hiển thị:

- Tên.
- Level.
- Thanh HP.
- Loại đối tượng.
- Vai trò.
- Trạng thái quan hệ.

---

# XIX. TARGET NGƯỜI CHƠI

## 1. Thông tin hiển thị

Khi target người chơi:

- Tên nhân vật.
- Level.
- Thanh HP.
- Thông tin quan hệ nếu cần:
  - Thân thiện.
  - Trung lập.
  - Đồng đội.
  - Cùng bang.
  - Đối địch.

## 2. Trạng thái thân thiện hoặc trung lập

Khi mục tiêu không ở trạng thái đối địch:

- Ô skill giữa chuyển thành nút **Trò chuyện**.
- Nhấn nút mở Pop-up Giao tiếp.

## 3. Pop-up Giao tiếp

Các lựa chọn dự kiến:

- Trò chuyện.
- Kết bạn.
- Thông tin.
- Mời vào nhóm.
- Giao dịch.

Chỉ hiển thị những hành động hợp lệ theo trạng thái hiện tại.

Ví dụ:

- Đã là bạn: có thể ẩn mục Kết bạn.
- Đã ở cùng nhóm: không hiện Mời vào nhóm.
- Đang ở chế độ cấm giao dịch: không hiện Giao dịch.

## 4. Trạng thái đối địch hoặc PK

Trong các trường hợp:

- Đang PK.
- Mục tiêu thuộc phe địch.
- Mục tiêu bị Server xác định là thù địch.
- Đang trong khu vực hoặc chế độ chiến đấu.

Thì:

- Không hiển thị nút Trò chuyện tại ô giữa.
- Ô giữa giữ nguyên chức năng skill hoặc combat.
- Không để giao tiếp chiếm vị trí thao tác chiến đấu.

---

# XX. TARGET QUÁI VẬT

## 1. Thông tin hiển thị

- Tên quái.
- Level.
- Thanh HP.
- Phân loại nếu có:
  - Quái thường.
  - Quái tinh anh.
  - Mini Boss.
  - Boss.
  - Quái nhiệm vụ.

## 2. Ô giữa

- Giữ nguyên skill hoặc thao tác chiến đấu.
- Không chuyển thành nút giao tiếp.

## 3. Khi quái chết

- Dấu Chỉ Mục tiêu biến mất.
- Khung Thông tin Mục tiêu biến mất.
- Nếu Auto đang bật, hệ thống có thể tìm target mới theo luật Auto.

---

# XXI. TARGET NPC

## 1. Thông tin hiển thị

- Tên NPC.
- Vai trò.
- Level nếu NPC có sử dụng.
- Không bắt buộc hiển thị HP nếu NPC không thể bị tấn công.

## 2. Ô Tương tác Ngữ cảnh

Ô giữa đổi thành hành động phù hợp:

- Nói chuyện.
- Mua bán.
- Rèn.
- Mở kho.
- Dịch chuyển.
- Nhận nhiệm vụ.
- Trả nhiệm vụ.

Nếu đứng ngoài phạm vi:

- Nút bị vô hiệu hóa.
- Có thể hiện thông báo:

> “Hãy tiến lại gần.”

---

# XXII. TARGET VẬT THỂ TƯƠNG TÁC

## 1. Thông tin hiển thị

- Tên vật thể.
- Loại vật thể.
- Thanh HP hoặc độ bền nếu vật thể có thể bị phá hủy.
- Không hiển thị HP nếu vật thể chỉ dùng để tương tác.

## 2. Ô Tương tác Ngữ cảnh

Ví dụ:

- Bảng khu → **Đọc**.
- Cột đá → **Kiểm tra**.
- Rương → **Mở**.
- Cổng → **Sử dụng**.
- Công tắc → **Kích hoạt**.

---

# XXIII. Ô TƯƠNG TÁC NGỮ CẢNH

## 1. Tên gọi

> **Ô Tương tác Ngữ cảnh**

Tên kỹ thuật:

```text
ContextActionSlot
```

## 2. Nguyên tắc

Ô giữa thay đổi chức năng dựa trên loại mục tiêu và quan hệ chiến đấu.

## 3. Chuyển thành nút tương tác khi

- Target người chơi thân thiện hoặc trung lập.
- Target NPC.
- Target vật thể tương tác.

## 4. Giữ nguyên skill/combat khi

- Target quái.
- Target người chơi đối địch.
- Đang PK.
- Đang trong trạng thái combat.
- Mục tiêu cần ưu tiên chiến đấu.

---

# XXIV. QUY TẮC CHỌN VÀ HỦY TARGET

## 1. Cách chọn target

- Chạm trực tiếp vào đối tượng.
- Chọn mục tiêu gần nhất.
- Chuyển target bằng nút hoặc thao tác vuốt.
- Auto chọn target trong PvE.
- Nhiệm vụ hoặc kỹ năng chỉ định mục tiêu hợp lệ.

## 2. Khi nhiều đối tượng chồng nhau

Thứ tự ưu tiên có thể gồm:

1. Đối tượng nhiệm vụ.
2. Quái đang tấn công người chơi.
3. Quái gần nhất.
4. NPC.
5. Người chơi.
6. Vật thể tương tác.

Thứ tự cụ thể có thể thay đổi theo ngữ cảnh.

## 3. Điều kiện hủy target

- Người chơi bấm vào khoảng trống.
- Người chơi chọn bỏ mục tiêu.
- Mục tiêu chết.
- Mục tiêu bị phá hủy.
- Mục tiêu rời map.
- Mục tiêu vượt phạm vi cho phép.
- Mục tiêu trở nên không hợp lệ.
- Người chơi chuyển map hoặc scene.
- Server xác nhận mất target.

## 4. Target không hợp lệ

- Không tiếp tục hiển thị dữ liệu cũ.
- Không cho gửi lệnh tấn công hoặc tương tác.
- Dấu Chỉ Mục tiêu biến mất.
- Khung Thông tin Mục tiêu đóng.
- Auto tìm mục tiêu mới nếu luật cho phép.

---

# XXV. CÁC THÀNH PHẦN HUD LUÔN HIỂN THỊ

Trong trạng thái Ingame thông thường:

```text
HUD luôn hiển thị
├── Khung Trạng thái Nhân vật
├── Joystick hoặc nút di chuyển
├── Nút đánh thường
├── Các nút kỹ năng
├── Nút Đổi Mục tiêu
├── Nút Đổi Trang Kỹ năng
├── Ô Tương tác Ngữ cảnh
├── Minimap
├── Mũi tên mở Thanh Chức năng Thu gọn
└── Thông báo ngắn theo tình huống
```

Chỉ hiển thị khi có target:

- Dấu Chỉ Mục tiêu.
- Khung Thông tin Mục tiêu.

Không hiển thị thường trực:

- Vàng.
- Ngọc.
- Hành trang.
- Trang bị.
- Kỹ năng chi tiết.
- Chỉ số chi tiết.
- Nhiệm vụ chi tiết.
- Cài đặt.

---

# XXVI. QUY TẮC TRÁNH TRÙNG LẶP CHỨC NĂNG

## 1. Khung Trạng thái Nhân vật

Dùng để truy cập:

- Hành trang.
- Trang bị.
- Kỹ năng.
- Chỉ số nhân vật.
- Nhiệm vụ.
- Cài đặt.
- Vàng và Ngọc.

## 2. Thanh Chức năng Thu gọn

Dùng để truy cập:

- Auto.
- Hộp thư.
- Bang hội.
- Đội nhóm.
- Chat.
- Thú cưỡi.
- Cảm xúc.
- Hoạt động.
- Sự kiện.

## 3. HUD chiến đấu

Dùng để:

- Di chuyển.
- Đánh thường.
- Dùng kỹ năng.
- Tương tác theo ngữ cảnh.
- Quan sát trạng thái bản thân và mục tiêu.

Một chức năng không được xuất hiện đồng thời ở nhiều khu vực nếu không có lý do gameplay rõ ràng.

---

# XXVII. QUY TẮC DỮ LIỆU CLIENT–SERVER

HUD không tự quyết định:

- HP/MP.
- EXP.
- Vàng/Ngọc.
- Vật phẩm.
- Trang bị.
- Cooldown.
- Buff/Debuff.
- Trạng thái đã đọc của thư.
- Kết quả Auto.
- HP mục tiêu.
- Quan hệ thân thiện/đối địch.
- Mục tiêu có thể tương tác hay không.
- Kết quả giao tiếp, giao dịch, mời nhóm hoặc kết bạn.

Luồng dữ liệu cơ bản:

```text
Người chơi thao tác
        ↓
Client gửi request hoặc input
        ↓
Server kiểm tra và xác nhận
        ↓
Client cập nhật trạng thái
        ↓
HUD hiển thị kết quả
```

Khi mất đồng bộ:

- HUD không tự giả lập thành công.
- Hiển thị trạng thái chờ hoặc kết nối lại nếu cần.
- Cập nhật lại theo dữ liệu chính thức từ Server.

---

# XXVIII. ĐIỂM KẾT THÚC LUỒNG HUD

Luồng HUD được xem là hoạt động đúng khi:

- Khung Trạng thái Nhân vật hiển thị đúng dữ liệu.
- Nhấn khung mở đúng Menu nhân vật.
- Vàng và Ngọc chỉ hiện trong Menu nhân vật.
- Joystick, nút đánh và nút kỹ năng hoạt động.
- Nút Đổi Mục tiêu chuyển đúng sang mục tiêu hợp lệ.
- Nút Đổi Trang Kỹ năng chuyển đúng giữa trang chiến đấu và trang Buff/Trói/Hỗ trợ.
- Cooldown kỹ năng vẫn được giữ chính xác khi đổi trang.
- Người chơi có thể thay đổi vị trí và kích thước joystick hoặc cụm nút di
  chuyển, nút đánh thường, từng nút skill, nút Đổi Mục tiêu, nút Đổi Trang Kỹ
  năng và Ô Tương tác Ngữ cảnh.
- Khung Thông tin Mục tiêu giữ cố định bên trái Minimap và không thay đổi kích thước.
- Bố cục tùy chỉnh vẫn nằm trong vùng an toàn và không làm mất khả năng thao tác.
- Người chơi có thể lưu, hủy hoặc khôi phục bố cục điều khiển mặc định.
- Mũi tên mở/đóng Thanh Chức năng Thu gọn.
- Auto mở đúng Pop-up thiết lập.
- Tin nhắn mới kích hoạt Banner và Dấu Chưa xem.
- Target hợp lệ hiển thị mũi tên trên đầu.
- Khung Thông tin Mục tiêu hiển thị bên trái Minimap.
- Ô giữa đổi đúng theo ngữ cảnh.
- Target người chơi thân thiện mở được Pop-up Giao tiếp.
- Target đối địch hoặc PK giữ nguyên thao tác combat.
- Cài đặt và bố cục HUD được lưu cho lần chơi tiếp theo.

---

# XXIX. ĐỊNH HƯỚNG LUỒNG TIẾP THEO: NHIỆM VỤ TÂN THỦ ĐẾN CẤP 10

Luồng tiếp theo sẽ mô tả nhiệm vụ tân thủ đến cấp 10.

Quy tắc đã xác định:

- Sau khi hoàn thành nhiệm vụ đầu tiên, nhân vật lên cấp 2.
- Người chơi được đưa đến safe zone đầu tiên.
- Khi nhân vật đạt cấp 2 hoặc đã đi vào safe zone đầu tiên, hệ thống phải bảo đảm người chơi được chuyển khỏi map mở đầu.
- Mục đích là tránh trường hợp người chơi cố tình ở lại map đầu tiên của game.
