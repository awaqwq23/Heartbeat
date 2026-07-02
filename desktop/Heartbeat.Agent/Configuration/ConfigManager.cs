using Heartbeat.Agent.Models;
using Serilog;
using System.Text.Json;

namespace Heartbeat.Agent.Configuration
{
    /// <summary>
    /// 管理位于 %LOCALAPPDATA%/Heartbeat/config.json 的用户配置。
    /// 线程安全，支持原子写入和变更通知。
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private readonly object _lock = new();
        private AgentConfig _current;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// 配置变更事件，参数为新的配置快照
        /// </summary>
        public event Action<AgentConfig>? ConfigChanged;

        public ConfigManager() : this(null) { }

        public ConfigManager(string? configPath)
        {
            _configPath = configPath ?? GetDefaultConfigPath();
            _current = LoadOrCreateDefault();
        }

        /// <summary>
        /// 环境变量：若设置，覆盖 ApiBaseUrl（仅本地端到端验证用，不落盘）。
        /// 只覆盖上传目标；AuthServiceBaseUrl 不动，鉴权仍走真实 Auth 平台。详见 README「本地端到端验证」。
        /// </summary>
        public const string ApiBaseUrlOverrideEnv = "HEARTBEAT_API_BASE_URL";

        /// <summary>
        /// 获取当前配置快照（返回副本，防止外部修改）
        /// </summary>
        public AgentConfig Current
        {
            get
            {
                AgentConfig snapshot;
                lock (_lock)
                {
                    snapshot = Clone(_current);
                }

                var overrideUrl = Environment.GetEnvironmentVariable(ApiBaseUrlOverrideEnv);
                if (!string.IsNullOrWhiteSpace(overrideUrl))
                    snapshot.ApiBaseUrl = overrideUrl.TrimEnd('/');

                return snapshot;
            }
        }

        /// <summary>
        /// 更新配置并持久化（原子写入）
        /// </summary>
        public void Update(Action<AgentConfig> modifier)
        {
            AgentConfig snapshot;

            lock (_lock)
            {
                modifier(_current);
                Normalize(_current);
                SaveAtomic(_current);
                snapshot = Clone(_current);
            }

            // 在锁外触发事件，避免死锁
            ConfigChanged?.Invoke(snapshot);
            Log.Information("配置已更新并保存");
        }

        /// <summary>
        /// 替换整个配置
        /// </summary>
        public void Replace(AgentConfig newConfig)
        {
            AgentConfig snapshot;

            lock (_lock)
            {
                _current = Clone(newConfig);
                Normalize(_current);
                SaveAtomic(_current);
                snapshot = Clone(_current);
            }

            ConfigChanged?.Invoke(snapshot);
            Log.Information("配置已替换并保存");
        }

        /// <summary>
        /// 从磁盘重新加载配置
        /// </summary>
        public void Reload()
        {
            AgentConfig snapshot;

            lock (_lock)
            {
                _current = LoadOrCreateDefault();
                snapshot = Clone(_current);
            }

            ConfigChanged?.Invoke(snapshot);
            Log.Information("配置已从磁盘重新加载");
        }

        private AgentConfig LoadOrCreateDefault()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        Normalize(config);
                        Log.Information("已加载配置: {Path}", _configPath);
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "读取配置失败，将使用默认配置: {Path}", _configPath);
            }

            var defaultConfig = new AgentConfig();
            SaveAtomic(defaultConfig);
            Log.Information("已创建默认配置: {Path}", _configPath);
            return defaultConfig;
        }

        /// <summary>
        /// 归一化配置：去掉 BaseUrl 末尾的 '/'，避免与拼接逻辑产生 '//'。
        /// </summary>
        private static void Normalize(AgentConfig config)
        {
            config.ApiBaseUrl = config.ApiBaseUrl.TrimEnd('/');
            config.AuthServiceBaseUrl = config.AuthServiceBaseUrl.TrimEnd('/');
            config.AwayProcessNames ??= [];
        }

        /// <summary>
        /// 原子写入：先写临时文件，再替换目标文件
        /// </summary>
        private void SaveAtomic(AgentConfig config)
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath)!;
                Directory.CreateDirectory(dir);

                var tempPath = _configPath + ".tmp";
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存配置失败: {Path}", _configPath);
            }
        }

        private static AgentConfig Clone(AgentConfig source)
        {
            return new AgentConfig
            {
                ApiBaseUrl = source.ApiBaseUrl,
                ApiKey = source.ApiKey,
                AuthServiceBaseUrl = source.AuthServiceBaseUrl,
                DeviceName = source.DeviceName,
                UploadIntervalMinutes = source.UploadIntervalMinutes,
                StatusUploadIntervalSeconds = source.StatusUploadIntervalSeconds,
                AwayProcessNames = [.. source.AwayProcessNames ?? []],
                IngestPort = source.IngestPort,
            };
        }

        private static string GetDefaultConfigPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Heartbeat", "config.json");
        }
    }
}
