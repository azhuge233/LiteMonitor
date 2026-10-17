using System;

namespace LiteMonitor.src.Core
{
    /// <summary>
    /// 设备变更触发的重载限流策略（纯函数，便于单测）。
    /// 显示设备事件可能成串到来（一次禁用/启用操作往往触发多条通知），
    /// 限流避免短时间内反复全量重载（每次重载会让界面短暂无数据）。
    /// 调用方：HardwareMonitor.UpdateAll 的消费点。
    /// </summary>
    public static class ReloadThrottle
    {
        /// <summary>
        /// 该事件是否应触发重载：启动静默期已过 且 距上次触发已达最小间隔。
        /// 静默期用于忽略“注册设备通知时系统对存量设备批量投递的到来事件”，
        /// 以及避开启动初始化阶段（避免重载与初始化交错）。
        /// </summary>
        public static bool ShouldTrigger(DateTime nowUtc, DateTime lastTriggerUtc, TimeSpan minInterval, DateTime suppressedUntilUtc)
            => nowUtc >= suppressedUntilUtc && nowUtc - lastTriggerUtc >= minInterval;
    }
}
