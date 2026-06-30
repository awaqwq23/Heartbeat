namespace Heartbeat.Core
{
    /// <summary>
    /// 合成（伪应用）使用段的标识名。这些不是真实应用，而是采集层用来在时间轴上
    /// 显式占位的特殊段；前端在应用排行等视图中应过滤掉它们。详见 ADR-014。
    /// </summary>
    public static class SyntheticApps
    {
        /// <summary>
        /// "离开"段：人不在（息屏 / 睡眠），以及被归一化的锁屏宿主（如 LockApp）。
        /// 因应用名独特，服务端 merge 不会与前后真实应用合并，但相邻 away 段会彼此合并。
        /// </summary>
        public const string Away = "__away__";
    }
}
