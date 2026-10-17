namespace LiteMonitor.src.Core
{
    /// <summary>
    /// 显卡读数键的判定（纯函数，便于单测）。
    /// 用途：取值层在“该键已不在映射中”（显卡被禁用/移除）时明确回 NaN，
    /// 让显示层呈现 `—`，而不是继续吐上次的旧值。非 GPU 键不受影响。
    /// 调用方：HardwareValueProvider 兜底分支。
    /// 注：类名保留了历史名（先前曾承载“最后有效值”策略），实际职责以本注释为准。
    /// </summary>
    public static class LastValidPolicy
    {
        /// <summary>显卡读数键（占用/显存/温度/功耗…）。</summary>
        public static bool IsGpuKey(string key) => key.StartsWith("GPU", System.StringComparison.Ordinal);

        /// <summary>
        /// 该键是否应报告为“无值”（→ 显示 `—`）。
        /// 判据（二者同时成立）：① 该键当前不在映射缓存中；② 没有任何显卡被映射（CachedGpu == null）。
        /// 只看①会把“本机没有这个传感器”（核显机的 GPU.Fan 等）误判为设备缺席；
        /// 只看②会漏判“设备被禁用但引用仍在”（首版缺陷）。调用方：HardwareValueProvider 兜底分支。
        /// </summary>
        public static bool ShouldReportMissing(string key, bool keyInCache, bool mappedGpuPresent)
            => IsGpuKey(key) && !keyInCache && !mappedGpuPresent;
    }
}
