namespace Heartbeat.Core.DTOs.Input
{
    /// <summary>
    /// 输入事件类型。详见 ADR-012。
    /// </summary>
    public enum InputEventType : short
    {
        KeyDown = 1,
        MouseButton = 2,
        MouseScroll = 3,
    }

    public class InputEventUploadRequest
    {
        public List<InputEventItem> Events { get; set; } = [];
    }

    public class InputEventItem
    {
        /// <summary>客户端生成的 UUIDv7，兼作主键与去重键。</summary>
        public Guid Id { get; set; }

        public InputEventType EventType { get; set; }

        /// <summary>键盘=VK 码；鼠标按钮=1左/2右/3中；滚轮=1上/2下。</summary>
        public short Code { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
