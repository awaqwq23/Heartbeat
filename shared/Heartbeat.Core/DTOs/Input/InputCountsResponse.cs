namespace Heartbeat.Core.DTOs.Input
{
    /// <summary>
    /// 某时间段内的键盘/鼠标操作计数。详见 ADR-012。
    /// </summary>
    public class InputCountsResponse
    {
        public long KeyboardTotal { get; set; }
        public long MouseLeft { get; set; }
        public long MouseRight { get; set; }
        public long MouseMiddle { get; set; }
        public long ScrollUp { get; set; }
        public long ScrollDown { get; set; }
    }
}
