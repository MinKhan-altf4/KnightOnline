Kiến Trúc Mạng KnightOnline: Cách Server \& Client Hoạt Động

1\. Tổng Quan

Server (Program.cs): TCP Listener chạy trên port 7777, chấp nhận clients, xử lý packets

Client (NetworkClient.cs): Unity MonoBehaviour, kết nối tới server, gửi/nhận packets

Giao thức: Length-prefixed framing (4 byte độ dài + JSON payload)

2\. Server (Program.cs) Hoạt Động

**Vòng Lặp Chính (Main)**

Server start → TcpListener(IPAddress.Any, 7777)

↓

while (true)

&#x20; ├─ Chờ client kết nối: AcceptTcpClientAsync()

&#x20; └─ Fire-and-forget: \_ = HandleClientAsync(tcpClient)

&#x20;    (Không await, vòng lặp tiếp tục nhận client khác)





**Xử Lý Mỗi Client (HandleClientAsync)**

Client kết nối → HandleClientAsync(tcpClient)

↓

using (tcpClient) {

&#x20; while (tcpClient.Connected) {

&#x20;   ├─ Đọc packet: ReadEnvelopeAsync(stream)

&#x20;   │  → 4 byte độ dài + JSON payload

&#x20;   ├─ Xử lý packet: HandlePacketAsync(stream, envelope)

&#x20;   │  → Switch theo PacketType

&#x20;   │  → Nếu ConnectRequest → gửi ConnectResponse

&#x20;   └─ Lặp lại cho packet tiếp theo

&#x20; }

}





**Length-Prefixed Framing**

\[4 byte length] \[JSON payload]

&#x20;     ↓              ↓

&#x20; "00 00 00 5C"  {...packet...}

&#x20; (92 bytes)

Đọc 4 byte đầu → biết payload dài bao nhiêu

Sau đó đọc đúng số byte đó → parse JSON

Cách này giải quyết vấn đề TCP không có khái niệm "message", chỉ có byte stream



3. Client (NetworkClient.cs) Hoạt Động

**Khởi Tạo Kết Nối (ConnectAsync)**

GameBootstrap tạo GameObject → thêm NetworkClient component

↓

ConnectAsync()

&#x20; ├─ TcpClient.ConnectAsync("127.0.0.1", 7777)

&#x20; ├─ Khởi tạo CancellationTokenSource (\_cts)

&#x20; ├─ Bắt đầu ReceiveLoopAsync (fire-and-forget)

&#x20; │  → Lắng nghe packets từ server liên tục

&#x20; └─ Gửi ConnectRequestPacket("1.0.0")


**Vòng Lặp Nhận Packet (ReceiveLoopAsync)** 

while (tcpClient.Connected \&\& !ct.IsCancellationRequested) {

&#x20; ├─ ReadEnvelopeAsync(ct)

&#x20; │  → Đọc 4 byte length

&#x20; │  → Đọc payload JSON

&#x20; │  → Deserialize thành PacketEnvelope

&#x20; ├─ HandlePacket(envelope)

&#x20; │  → Switch theo PacketType

&#x20; │  → Nếu ConnectResponse → log message

&#x20; └─ Lặp lại

}





**Gửi Packet (SendPacketAsync)** 

SendPacketAsync(PacketType.ConnectRequest, request)

&#x20; ├─ Serialize payload thành JSON

&#x20; ├─ Wrap vào PacketEnvelope { Type, Payload }

&#x20; ├─ Serialize envelope thành JSON

&#x20; ├─ Tính độ dài

&#x20; ├─ Gửi: \[4 byte length] + \[JSON]

&#x20; └─ Await để đảm bảo dữ liệu được gửi hết



**4. Packet Flow: Từ Client → Server → Client**

CLIENT                              SERVER

&#x20; |                                   |

&#x20; |-- ConnectRequest (length + JSON)->|

&#x20; |                                   |

&#x20; |                         ReadEnvelopeAsync

&#x20; |                         HandlePacket(ConnectRequest)

&#x20; |                         ├─ Deserialize request

&#x20; |                         ├─ Validate

&#x20; |                         └─ Gửi ConnectResponse

&#x20; |                                   |

&#x20; |<---(length + JSON) ConnectResponse|

&#x20; |                                   |

&#x20; | ReceiveLoopAsync                  |

&#x20; | ├─ ReadEnvelopeAsync              |

&#x20; | ├─ HandlePacket(ConnectResponse)  |

&#x20; | └─ Log: "Server phản hồi: ..."    |

&#x20; |                                   |


5. Đặc Điểm Chính

Đặc Điểm	Server	Client

Thread Model	Async/await, mỗi client 1 Task	MonoBehaviour trên Main thread, async/await

Lifecycle	Console app, chạy vô thời hạn	Unity MonoBehaviour, gọi OnDestroy() khi tắt

Error Handling	Try-catch ở mỗi client handler, 1 client lỗi không ảnh hưởng server	Try-catch + CancellationToken, supppress expected I/O errors on shutdown

Resource Cleanup	using (tcpClient) tự động dispose	OnDestroy() → Disconnect() → dispose resources + cancel token

Cancellation	N/A	CancellationTokenSource để graceful shutdown

6\. Cách Mở Rộng (Thêm Packet Type Mới)

Bước 1: Định nghĩa packet class trong Shared/Packets
public class MyCustomPacket

{

&#x20;   public string Data { get; set; }

}


Bước 2: Thêm vào PacketType Enum

public enum PacketType

{

&#x20;   ConnectRequest,

&#x20;   ConnectResponse,

&#x20;   MyCustomType  // ← Thêm ở đây

}

Bước 3: Server xử lý



case PacketType.MyCustomType:

&#x20;   var packet = JsonSerializer.Deserialize<MyCustomPacket>(envelope.Payload);

&#x20;   // Xử lý logic

&#x20;   break;

Bước 4: Client gửi

var packet = new MyCustomPacket { Data = "Hello" };

await SendPacketAsync(PacketType.MyCustomType, packet);
Tóm tắt: Server chờ clients, nhận packets, xử lý, gửi response. Client kết nối, gửi packets, lắng nghe responses. Cả hai dùng length-prefixed framing để đồng bộ dữ liệu trên TCP stream.







