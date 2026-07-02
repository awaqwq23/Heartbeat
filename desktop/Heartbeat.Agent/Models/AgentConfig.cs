namespace Heartbeat.Agent.Models
{
    public class AgentConfig
    {
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string AuthServiceBaseUrl { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;

        private int _uploadIntervalMinutes = 1;
        public int UploadIntervalMinutes
        {
            get => _uploadIntervalMinutes;
            set => _uploadIntervalMinutes = value < 1 ? 1 : value;
        }

        private int _statusUploadIntervalSeconds = 30;
        public int StatusUploadIntervalSeconds
        {
            get => _statusUploadIntervalSeconds;
            set => _statusUploadIntervalSeconds = value < 5 ? 5 : value;
        }

        /// <summary>
        /// 前台进程名命中此列表时，该使用段被归一化为 away 段（仅改名，不驱动 away 状态机）。
        /// 默认包含锁屏宿主 LockApp。详见 ADR-014。
        /// </summary>
        public List<string> AwayProcessNames { get; set; } = ["LockApp"];

        /// <summary>
        /// 本地 ingest 枢纽监听端口（loopback），插件采集器往此推段（ADR-017）。
        /// ≤0 表示禁用。
        /// </summary>
        public int IngestPort { get; set; } = 48200;
    }
}
