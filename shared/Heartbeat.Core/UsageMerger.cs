using Heartbeat.Core.DTOs.Usage;

namespace Heartbeat.Core
{
    /// <summary>
    /// 合并相邻同应用使用记录（处理上传截断产生的碎片）
    /// </summary>
    public static class UsageMerger
    {
        /// <summary>
        /// 合并容差：同应用首尾相连在此范围内的记录合并
        /// </summary>
        public static readonly TimeSpan MergeTolerance = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 两段是否可合并的判据（唯一真源，客户端与服务端共用）。
        /// 规则：同 AppName（不区分大小写）且同 Title，且时间首尾相连/重叠（≤容差）。
        /// Title 的段级维度详见 ADR-015 —— 标题不同则视为不同活动，不合并。
        /// </summary>
        public static bool CanMerge(
            string prevAppName, string? prevTitle, DateTimeOffset prevEnd,
            string currAppName, string? currTitle, DateTimeOffset currStart)
        {
            return string.Equals(prevAppName, currAppName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(prevTitle, currTitle, StringComparison.Ordinal)
                && currStart <= prevEnd + MergeTolerance;
        }

        /// <summary>
        /// 将连续或重叠的同应用+同标题记录合并为一条（中间有其他应用/标题则不合并）。
        /// 不会修改传入的对象，返回全新的列表和对象。
        /// </summary>
        public static List<AppUsageItem> Merge(List<AppUsageItem> usages)
        {
            if (usages.Count <= 1) return usages;

            var sorted = usages.OrderBy(u => u.StartTime).ToList();

            var result = new List<AppUsageItem>
            {
                new() { AppName = sorted[0].AppName, Title = sorted[0].Title, StartTime = sorted[0].StartTime, EndTime = sorted[0].EndTime }
            };

            for (var i = 1; i < sorted.Count; i++)
            {
                var prev = result[^1];
                var curr = sorted[i];

                if (CanMerge(prev.AppName, prev.Title, prev.EndTime, curr.AppName, curr.Title, curr.StartTime))
                {
                    // 同应用+同标题且重叠或首尾相连 → 扩展上一条的结束时间
                    if (curr.EndTime > prev.EndTime)
                        prev.EndTime = curr.EndTime;
                }
                else
                {
                    result.Add(new() { AppName = curr.AppName, Title = curr.Title, StartTime = curr.StartTime, EndTime = curr.EndTime });
                }
            }

            return result;
        }
    }
}
