using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LiteMonitor.src.UI.Controls
{
    /// <summary>
    /// 「显示顺序」重排原语：目标位次计算、z 序落地（唯一出口）、拖拽会话、抓手几何、插入线。
    ///
    /// 索引约定（与 MonitorPage.ReloadList 的「逆序添加」一致，已实测确认）：
    ///   逻辑位次 0 = 视觉最上；子控件索引与逻辑位次相反。
    ///   逻辑位次 = Controls.Count - 1 - GetChildIndex(control)
    ///
    /// 本文件刻意不引用项目内其他类型（UIUtils / UIColors / LanguageManager 等）：
    /// 纯逻辑因此可被单测工程直接 Compile Include 链接、无需任何 UI 实例。
    /// 缩放与配色由调用方（MonitorControls / MonitorPage）经 UIUtils 传入。
    /// </summary>
    public static class ListReorder
    {
        // ===== 抓手命中区（未缩放基准值 2..18；调用方经 UIUtils.S 缩放）=====
        // 落在现有 ID 列（X_COL1）左侧空档，不动任何现有列坐标。
        public const int GripX0 = 2;
        public const int GripX1 = 18;

        /// <summary>按下后位移达到该像素数才真正起拖（避免把点击/轻微抖动当拖拽）。</summary>
        public const int DragThresholdPx = 4;

        /// <summary>插入线厚度基准值（未缩放）。</summary>
        public const int LineThickness = 3;

        /// <summary>当前活动拖拽（同一时刻至多一个）；行 / 组头绘制插入线时查询。</summary>
        public static DragSession? Active { get; private set; }

        internal static void SetActive(DragSession? session) => Active = session;

        // =====================================================================
        // 1. 纯逻辑：视觉 y → 目标位次
        // =====================================================================

        /// <summary>
        /// 视觉 y → 目标位次（纯函数，可单测）。
        /// 语义：光标落在哪个槽内，该项就落到哪个位次（0 = 视觉最上）。
        /// 光标在首槽中心之上 → 0；在末槽中心之下 → slotCount-1（天然完成越界钳制）。
        /// </summary>
        public static int ComputeTargetIndex(int cursorY, int slotHeight, int slotCount, int currentIndex)
            => ComputeTargetIndex(cursorY, slotHeight, slotCount, currentIndex, 0);

        /// <summary>
        /// 同上，另带最小位次下限（minIndex）——用于 DASH 组位次冻结（任何组不得排到它之前，下限 = 1）。
        /// slotHeight 非法（≤0）或槽数为 0 时保持当前位次，不做移动。
        /// </summary>
        public static int ComputeTargetIndex(int cursorY, int slotHeight, int slotCount, int currentIndex, int minIndex)
        {
            if (slotCount <= 0) return 0;
            if (slotHeight <= 0) return Clamp(currentIndex, minIndex, slotCount - 1);

            var middles = new int[slotCount];
            for (int i = 0; i < slotCount; i++) middles[i] = slotHeight / 2 + i * slotHeight;
            return ComputeTargetIndex(cursorY, middles, currentIndex, minIndex);
        }

        /// <summary>
        /// 不等高槽位版（组块各自高低不同）：slotMiddles[i] = 第 i 槽的视觉中心 y（同一坐标系、升序）。
        /// </summary>
        public static int ComputeTargetIndex(int cursorY, IReadOnlyList<int>? slotMiddles, int currentIndex, int minIndex)
        {
            if (slotMiddles == null) return 0;
            int n = slotMiddles.Count;
            if (n <= 0) return 0;

            int idx = 0;
            for (int i = 1; i < n; i++)
            {
                if (cursorY >= slotMiddles[i]) idx = i;
                else break;
            }
            return Clamp(idx, minIndex, n - 1);
        }

        // =====================================================================
        // 2. 纯逻辑：恢复默认顺序（只决定顺序，不改动任何字段）
        // =====================================================================

        /// <summary>
        /// 按出厂默认名次重排（纯函数，可单测）：
        /// 命中默认表的项按其默认名次升序；未命中项（插件项等）保持相对顺序并整体排在默认项之后。
        /// 返回新列表，元素为原实例引用——本函数不写任何字段（可见性 / 别名 / 单位 / 标签一律不动）。
        /// defaultRanks 的键比较规则由调用方传入的字典自身决定（建议 OrdinalIgnoreCase）。
        /// </summary>
        public static List<T> ApplyDefaultOrder<T>(IList<T>? current, Func<T, string>? keyOf, IDictionary<string, int>? defaultRanks)
        {
            var result = new List<T>();
            if (current == null || current.Count == 0) return result;

            var pairs = new List<(int Rank, int Pos, T Item)>(current.Count);
            for (int i = 0; i < current.Count; i++)
            {
                int rank = int.MaxValue;    // 非默认项：整体居后
                string? key = keyOf == null ? null : keyOf(current[i]);
                if (!string.IsNullOrEmpty(key) && defaultRanks != null && defaultRanks.TryGetValue(key, out int r))
                    rank = r;
                pairs.Add((rank, i, current[i]));   // Pos 保底：同rank按原顺序（保持相对顺序）
            }

            pairs.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Pos.CompareTo(b.Pos));
            for (int i = 0; i < pairs.Count; i++) result.Add(pairs[i].Item);
            return result;
        }

        // =====================================================================
        // 3. 索引换算与 z 序落地
        // =====================================================================

        /// <summary>逻辑位次（0 = 视觉最上）；控件不在容器内返回 -1。</summary>
        public static int GetLogicalIndex(Control container, Control child)
        {
            if (container == null || child == null) return -1;
            // 注意：ControlCollection.GetChildIndex(child) 对非子控件会抛异常，必须用不抛的重载
            int i = container.Controls.GetChildIndex(child, false);
            return i < 0 ? -1 : container.Controls.Count - 1 - i;
        }

        /// <summary>容器内控件的视觉序（上→下）。</summary>
        public static List<Control> VisualChildren(Control container)
            => container == null ? new List<Control>() : container.Controls.Cast<Control>().Reverse().ToList();

        /// <summary>
        /// 唯一的 z 序落地（拖拽 / 右键菜单 / ▲▼ 三条通道都调用这里，行为不可能分叉）：
        /// SuspendLayout → SetChildIndex → 还原 AutoScrollPosition → ResumeLayout(false) + PerformLayout()。
        /// 只允许同一容器内重排；位次越界一律钳制。
        /// </summary>
        public static void MoveSlot(Control container, Control control, int newLogicalIndex)
        {
            if (container == null || control == null) return;
            if (!ReferenceEquals(control.Parent, container)) return;    // 不跨容器（不跨组）

            int count = container.Controls.Count;
            if (count <= 1) return;

            if (newLogicalIndex < 0) newLogicalIndex = 0;
            if (newLogicalIndex > count - 1) newLogicalIndex = count - 1;

            int newChildIndex = count - 1 - newLogicalIndex;
            if (container.Controls.GetChildIndex(control, false) == newChildIndex) return;   // 已在目标位次

            var scroll = container as ScrollableControl;
            Point pos = scroll != null ? scroll.AutoScrollPosition : Point.Empty;

            container.SuspendLayout();
            try
            {
                container.Controls.SetChildIndex(control, newChildIndex);
                if (scroll != null) scroll.AutoScrollPosition = new Point(Math.Abs(pos.X), Math.Abs(pos.Y));
            }
            finally
            {
                container.ResumeLayout(false);
                container.PerformLayout();
            }
        }

        /// <summary>
        /// 批量重排到给定视觉序（自底向上扫描，逐项调用 MoveSlot）。
        /// desiredTopToBottom 只需给出要排序的那批控件（容器内的其他控件如占位符 / 底部提示不参与）。
        /// </summary>
        public static void ApplyVisualOrder(Control container, IReadOnlyList<Control> desiredTopToBottom)
        {
            if (container == null || desiredTopToBottom == null) return;

            var items = new List<Control>();
            foreach (var c in desiredTopToBottom)
                if (c != null && ReferenceEquals(c.Parent, container)) items.Add(c);
            if (items.Count < 2) return;

            // 槽位 = 这批控件当前的逻辑位次（升序）；自底向上落位，可保证已落位的不被后续步骤扰动
            var slots = new List<int>();
            foreach (var c in items) slots.Add(GetLogicalIndex(container, c));
            slots.Sort();

            for (int i = items.Count - 1; i >= 0; i--) MoveSlot(container, items[i], slots[i]);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) return max < 0 ? 0 : max;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// 画插入线（由槽位控件的 OnPaint 调用：线随内容一起滚动）。
        /// 统一画在槽位的上边缘——线标记的就是该项将要占据的位次（落位后该项正落在该槽原位）。
        /// </summary>
        public static void PaintInsertionLine(Graphics g, Rectangle slotBounds, Color color, int thickness)
        {
            if (g == null || slotBounds.Width <= 0 || slotBounds.Height <= 0) return;
            int t = thickness <= 0 ? LineThickness : thickness;
            int y = slotBounds.Top;

            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, slotBounds.Left, y, slotBounds.Width, t);
                int d = t * 3;
                g.FillEllipse(brush, slotBounds.Left, y + t / 2 - d / 2, d, d);
                g.FillEllipse(brush, slotBounds.Right - d, y + t / 2 - d / 2, d, d);
            }
        }

        // =====================================================================
        // 4. 拖拽会话
        // =====================================================================

        /// <summary>
        /// 一次拖拽会话：按下后位移 ≥ 阈值才起拖 → 插入线跟随 + 边缘自动滚动 → 放下 / 取消（Esc、出界回位）。
        /// 落地下单 = <see cref="MoveSlot"/>（唯一出口）；拖拽期用 Capture 吞掉鼠标事件，
        /// **不改**行内输入框的 Enabled（避免视觉变灰）。
        /// </summary>
        public sealed class DragSession
        {
            private readonly Control _source;                   // 可移动项（行 / 组块）——z 序落地的对象
            private readonly Control _captureTarget;            // 拖拽期持有鼠标的控件（行 / 组头：鼠标事件在此被吞掉）
            private readonly Control _container;                // 兄弟容器（RowsPanel / _container）
            private readonly Func<Control, bool>? _slotFilter;  // 哪些兄弟算「槽位」（排除占位符 / 底部提示）
            private readonly int _slotHeight;                   // >0 = 等高槽（行）；≤0 = 按各槽中心算（组块）
            private readonly int _minTargetIndex;               // 槽位表内的最小下标（DASH 冻结：1）
            private readonly int _scrollStep;                   // 边缘自动滚动步长（像素，已缩放）
            private readonly int _edgeZone;                     // 触发自动滚动的边缘厚度（像素，已缩放）

            private bool _armed;        // 已在抓手按下
            private bool _active;       // 已起拖
            private Point _downScreen;
            private int _sourceIndex = -1;
            private int _targetIndex = -1;   // -1 = 无有效落点（出界）

            private System.Windows.Forms.Timer? _timer;
            private EscMessageFilter? _esc;

            public DragSession(Control source, Control captureTarget, Control container, Func<Control, bool>? slotFilter,
                               int slotHeight, int minTargetIndex, int scrollStep, int edgeZone, Color lineColor)
            {
                _source = source;
                _captureTarget = captureTarget ?? source;
                _container = container;
                _slotFilter = slotFilter;
                _slotHeight = slotHeight;
                _minTargetIndex = minTargetIndex < 0 ? 0 : minTargetIndex;
                _scrollStep = scrollStep <= 0 ? 12 : scrollStep;
                _edgeZone = edgeZone <= 0 ? 28 : edgeZone;
                LineColor = lineColor;
            }

            public Control Container => _container;
            public Control Source => _source;
            public Color LineColor { get; }
            public bool IsArmed => _armed;
            public bool IsActive => _active;
            public bool IsBusy => _armed || _active;
            public int SourceIndex => _sourceIndex;
            public int TargetIndex => _targetIndex;

            /// <summary>鼠标在抓手区按下（仅左键；调用方负责判定命中区与启用态）。</summary>
            public void OnMouseDown(Point screenPoint)
            {
                if (_container == null || _source == null) return;
                _armed = true;
                _active = false;
                _targetIndex = -1;
                _downScreen = screenPoint;
                _sourceIndex = GetLogicalIndex(_container, _source);
            }

            /// <summary>鼠标移动：位移够阈值才起拖，随后更新插入位。</summary>
            public void OnMouseMove()
            {
                if (!_armed || _container == null) return;

                Point cur = Cursor.Position;
                if (!_active)
                {
                    if (Math.Abs(cur.X - _downScreen.X) < DragThresholdPx &&
                        Math.Abs(cur.Y - _downScreen.Y) < DragThresholdPx) return;   // 位移不足 → 仍算点击
                    StartDrag();
                }
                UpdateTarget(cur);
            }

            /// <summary>松开鼠标：有效落点则落到该位次，否则回位（什么都不做）。</summary>
            public void OnMouseUp()
            {
                if (!_armed) return;

                if (!_active) { _armed = false; return; }

                UpdateTarget(Cursor.Position);
                int target = _targetIndex;
                int source = _sourceIndex;
                EndDrag();

                if (target >= 0 && target != source) MoveSlot(_container, _source, target);
            }

            /// <summary>取消（Esc / 失捕获）：无任何落位动作，回位。</summary>
            public void Abort()
            {
                if (!_armed && !_active) return;
                EndDrag();
            }

            /// <summary>
            /// 该槽位是否要画插入线（线画在槽的上边缘）。参数是「槽位控件」本身：
            /// 行层 = 行；组层 = 组块（由组头代为绘制，故组头传入自己的 Tag）。
            /// </summary>
            public bool ShouldPaintLineFor(Control slot)
            {
                if (!_active || _targetIndex < 0 || slot == null || _container == null) return false;
                if (!ReferenceEquals(slot.Parent, _container)) return false;
                if (_targetIndex == _sourceIndex) return false;                  // 原地：不画
                return GetLogicalIndex(_container, slot) == _targetIndex;
            }

            /// <summary>该控件是否就是本次拖拽的源（用于高亮提供反馈；组层传组块）。</summary>
            public bool IsSource(Control c) => _active && c != null && ReferenceEquals(c, _source);

            private void StartDrag()
            {
                _active = true;
                _sourceIndex = GetLogicalIndex(_container, _source);
                _targetIndex = _sourceIndex;
                SetActive(this);

                if (_captureTarget != null) _captureTarget.Capture = true;   // 吞掉鼠标事件：行内输入框不会被拖拽打扰

                _esc = new EscMessageFilter(Abort);
                Application.AddMessageFilter(_esc);

                _timer = new System.Windows.Forms.Timer { Interval = 30 };
                _timer.Tick += (s, e) => AutoScrollTick();
                _timer.Start();

                InvalidateSlots();
            }

            private void EndDrag()
            {
                _armed = false;
                _active = false;

                if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
                if (_esc != null) { Application.RemoveMessageFilter(_esc); _esc = null; }
                if (ReferenceEquals(Active, this)) SetActive(null);

                if (_captureTarget != null && !_captureTarget.IsDisposed && _captureTarget.Capture) _captureTarget.Capture = false;

                _targetIndex = -1;
                InvalidateSlots();
            }

            private void UpdateTarget(Point screenPoint)
            {
                if (_container == null || _container.IsDisposed) return;
                int target = ResolveTarget(_container.PointToClient(screenPoint));
                if (target == _targetIndex) return;
                _targetIndex = target;
                InvalidateSlots();
            }

            /// <summary>目标位次（容器逻辑位次）；出界（含拖出本组 / 拖出可视区）返回 -1。</summary>
            private int ResolveTarget(Point cursorInContainer)
            {
                if (_container == null || _source == null) return -1;
                if (!_container.ClientRectangle.Contains(cursorInContainer)) return -1;

                var slots = CollectSlots(_container, _slotFilter);
                if (slots.Count == 0) return -1;

                int current = _sourceIndex < 0 ? 0 : _sourceIndex;
                int slotIdx;
                if (_slotHeight > 0)
                {
                    int relY = cursorInContainer.Y - slots[0].Top;
                    slotIdx = ComputeTargetIndex(relY, _slotHeight, slots.Count, current, _minTargetIndex);
                }
                else
                {
                    var middles = new int[slots.Count];
                    for (int i = 0; i < slots.Count; i++) middles[i] = slots[i].Center;
                    slotIdx = ComputeTargetIndex(cursorInContainer.Y, middles, current, _minTargetIndex);
                }
                return slots[slotIdx].LogicalIndex;
            }

            /// <summary>边缘自动滚动：光标在容器内且贴着上 / 下边缘时持续滚动（跨可视区拖拽）；
            /// 光标在容器外（拖出窗口 / 容器）时不滚。</summary>
            private void AutoScrollTick()
            {
                if (!_active || _container == null || _container.IsDisposed) return;
                var sc = _container as ScrollableControl;
                if (sc == null || !sc.AutoScroll) return;

                Point cur = _container.PointToClient(Cursor.Position);
                if (!_container.ClientRectangle.Contains(cur)) return;   // 光标不在容器内 → 不滚

                int dy = 0;
                if (cur.Y < _edgeZone) dy = -_scrollStep;
                else if (cur.Y > _container.ClientSize.Height - _edgeZone) dy = _scrollStep;
                if (dy == 0) return;

                Point pos = sc.AutoScrollPosition;
                int newY = Math.Abs(pos.Y) + dy;
                if (newY < 0) newY = 0;                       // 上界由 WinForms 自行钳制
                sc.AutoScrollPosition = new Point(Math.Abs(pos.X), newY);

                UpdateTarget(Cursor.Position);
            }

            private void InvalidateSlots()
            {
                if (_container == null) return;
                // 连同子控件一起失效：组层的插入线由组头（孙级控件）绘制，只失效父级看不见重绘
                foreach (Control c in _container.Controls) c.Invalidate(true);
            }

            private readonly struct Slot
            {
                public readonly Control Control;
                public readonly int LogicalIndex;
                public readonly int Top;
                public readonly int Center;

                public Slot(Control c, int logicalIndex)
                {
                    Control = c;
                    LogicalIndex = logicalIndex;
                    Top = c.Top;
                    Center = c.Top + c.Height / 2;
                }
            }

            private static List<Slot> CollectSlots(Control container, Func<Control, bool>? filter)
            {
                var list = new List<Slot>();
                if (container == null) return list;

                int count = container.Controls.Count;
                for (int i = 0; i < count; i++)
                {
                    var c = container.Controls[i];
                    if (c == null) continue;
                    // 不按 Visible 过滤：Control.Visible 是「含父链」的有效可见性，页面未显示时全为 false
                    if (filter != null && !filter(c)) continue;
                    list.Add(new Slot(c, count - 1 - i));
                }
                list.Sort((a, b) => a.LogicalIndex.CompareTo(b.LogicalIndex));   // 视觉序 = 逻辑位次升序
                return list;
            }

            /// <summary>Esc 取消：拖拽期鼠标被 Capture，键盘不在行上，靠消息过滤器兜住。</summary>
            private sealed class EscMessageFilter : IMessageFilter
            {
                private const int WM_KEYDOWN = 0x0100;
                private readonly Action _onEsc;

                public EscMessageFilter(Action onEsc) { _onEsc = onEsc; }

                public bool PreFilterMessage(ref Message m)
                {
                    if (m.Msg == WM_KEYDOWN && m.WParam.ToInt32() == (int)Keys.Escape)
                    {
                        _onEsc?.Invoke();
                        return true;
                    }
                    return false;
                }
            }
        }
    }
}
