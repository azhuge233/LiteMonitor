namespace LiteMonitor.src.Core
{
    /// <summary>
    /// 「首选显卡」下拉框的填充策略（纯函数，便于单测）。
    /// 背景：下拉列表只用“当前检测到的显卡”填充，保存的卡被禁用时会回落到“自动”，
    /// 用户此时保存就把偏好覆盖成空（丢设置）。策略：给“当前不可用但已保存”的卡
    /// 注入一个占位项并保持选中，使保存不改写配置值。
    /// 调用方：SystemHardwarPage.FillGpuSync。
    /// </summary>
    public static class GpuChoicePolicy
    {
        /// <summary>保存的显卡不在当前列表、且保存值非空时，是否注入占位项。</summary>
        public static bool ShouldInjectPlaceholder(bool hasResolvedCurrent, string savedValue)
            => !hasResolvedCurrent && !string.IsNullOrWhiteSpace(savedValue);

        /// <summary>占位项标签（用保存值本身作值，保证保存时回写原值）。</summary>
        public static string PlaceholderLabel(string savedValue) => $"（当前不可用）{savedValue}";
    }
}
