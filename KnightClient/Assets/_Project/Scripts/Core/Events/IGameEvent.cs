namespace KnightOnline.Client.Core.Events
{
    /// <summary>
    /// Marker interface cho mọi event payload trong hệ thống.
    /// Không chứa method — chỉ ràng buộc type tại compile-time để EventBus
    /// chỉ nhận các kiểu được thiết kế làm event.
    /// </summary>
    public interface IGameEvent
    {
    }
}