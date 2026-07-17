using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Client.Data.Events
{
    // Đổi từ IGameEvent thành IStickyGameEvent - đây là event đại diện
    // TRẠNG THÁI kết nối hiện tại, cần được EventBus lưu lại và phát ngay
    // cho subscriber đăng ký muộn (giải quyết race condition Network vs UI).
    public readonly struct ServerConnectionResultEvent : IStickyGameEvent
    {
        public readonly ConnectResult Result;
        public readonly string Message;

        public ServerConnectionResultEvent(ConnectResult result, string message)
        {
            Result = result;
            Message = message;
        }
    }

    // Giữ nguyên IGameEvent thường - đây là 1 sự kiện tức thời
    // ("vừa mất kết nối"), không nên phát lại mãi cho subscriber tới sau.
    public readonly struct ServerDisconnectedEvent : IGameEvent
    {
    }
}