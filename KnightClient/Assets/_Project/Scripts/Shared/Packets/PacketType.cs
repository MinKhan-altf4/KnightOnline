namespace KnightOnline.Client.Shared.Packets
{
    /// <summary>
    /// Định danh loại packet, dùng để Client/Server biết cách deserialize
    /// dữ liệu nhận được. Mở rộng dần khi có thêm packet mới — KHÔNG xóa
    /// hoặc đổi số thứ tự các giá trị cũ khi đã có dữ liệu thật chạy,
    /// vì sẽ phá vỡ khả năng tương thích ngược.
    /// </summary>
    public enum PacketType : byte
    {
        Unknown = 0,
        ConnectRequest = 1,
        ConnectResponse = 2,
    }
}