namespace LiteMonitor.src.Core
{
    /// <summary>
    /// 数据量单位换算（本主题唯一出处）。
    /// 背景：LHM 0.9.7 起 <c>SensorType.Data</c> 类传感器一律以**字节**为单位（上游提交 #2533；
    /// 此前 Data=GB、SmallData=MB）。凡把 Data 类取值换算为 GB 的地方都调用本类，
    /// 不要再按数值量级猜单位——那曾造成两次容量显示放大事故（显存 500+ TB / 内存 PB）。
    /// </summary>
    public static class DataUnits
    {
        /// <summary>1 GiB 的字节数。</summary>
        public const double BytesPerGB = 1073741824.0;

        /// <summary>字节 → GB。</summary>
        public static float BytesToGB(double bytes) => (float)(bytes / BytesPerGB);
    }
}
