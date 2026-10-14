using System;
using System.Drawing;
using System.Windows.Forms;
using LiteMonitor.src.Core;
using LiteMonitor.src.SystemServices.InfoService; // [New]

namespace LiteMonitor.src.UI.Controls
{
    public static class MonitorLayout
    {
        public static readonly int H_ROW = UIUtils.S(44);
        
        public static readonly int X_COL1 = UIUtils.S(20);  
        public static readonly int X_COL2 = UIUtils.S(140);
        
        // ★★★ [新增] 单位列 (位于 Name Input 和 Checkbox 之间) ★★★
        public static readonly int X_COL_UNIT = UIUtils.S(300); 

        // [需求4] 原有列全部右移
        public static readonly int X_COL3 = UIUtils.S(390); 
        public static readonly int X_COL4 = UIUtils.S(510); 

        // 兼容
        public static readonly int X_ID = X_COL1;
        public static readonly int X_NAME = X_COL2;
        public static readonly int X_SWITCH = X_COL3;
        public static readonly int X_SORT = X_COL4;
    }

    /// <summary>
    /// 抓手（行 / 组头左边缘的 6 点）：命中区与画法集中在这里，尺寸一律经 UIUtils.S 缩放。
    /// 命中区落在现有 ID 列（MonitorLayout.X_COL1）左侧空档，不动任何现有列坐标。
    /// </summary>
    public static class MonitorGrip
    {
        public static readonly Color GripColor = Color.FromArgb(150, 150, 150);

        public static int HitLeft => UIUtils.S(ListReorder.GripX0);
        public static int HitRight => UIUtils.S(ListReorder.GripX1);
        public static bool Hit(Point p) => p.X >= HitLeft && p.X <= HitRight;

        /// <summary>自绘 6 点（2 列 × 3 行）。禁用字体符号：避免 YaHei 缺字与 DPI 差异。</summary>
        public static void Paint(Graphics g, int height, Color color)
        {
            int dot = UIUtils.S(3);
            int gap = UIUtils.S(4);
            int gw = dot * 2 + gap;
            int gh = dot * 3 + gap * 2;
            int x0 = HitLeft + (HitRight - HitLeft + 1 - gw) / 2;
            int y0 = (height - gh) / 2;
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;

            using var b = new SolidBrush(color);
            for (int col = 0; col < 2; col++)
                for (int row = 0; row < 3; row++)
                    g.FillEllipse(b, x0 + col * (dot + gap), y0 + row * (dot + gap), dot, dot);
        }
    }

    /// <summary>
    /// 行 / 组头共用的「显示顺序」支撑：右键菜单（置顶 / 上移 / 下移 / 置底 ＋ 恢复默认顺序）与拖拽接线。
    /// 拖拽 / 菜单 / ▲▼ 三条通道最终都落到 ListReorder.MoveSlot 这一个出口，行为不可能分叉。
    /// </summary>
    internal sealed class ReorderSupport : IDisposable
    {
        private readonly Control _host;                               // 挂菜单 / 拖拽期持有鼠标的控件（行 / 组头）
        private readonly Func<Control?> _ownerOf;                     // 可移动项：行 = 自己，组头 = 自己的组块
        private readonly Func<Control?> _containerOf;                 // 兄弟容器：行 = Parent，组头 = 组块的 Parent
        private readonly Func<Func<Control, bool>?> _slotFilterOf;    // 哪些兄弟算「槽位」
        private readonly Func<int> _slotHeight;                       // >0 等高（行）；0 = 按各槽中心（组块）
        private readonly Func<int> _minTargetIndex;                   // 槽位表内最小下标（DASH 冻结 = 1）
        private readonly Func<bool> _positionEnabled;                 // DASH 组头 = false（位置项全禁用）

        private readonly ToolStripMenuItem _itemTop;
        private readonly ToolStripMenuItem _itemUp;
        private readonly ToolStripMenuItem _itemDown;
        private readonly ToolStripMenuItem _itemBottom;

        private ListReorder.DragSession? _session;

        public event EventHandler? RestoreDefaultOrderRequested;
        public ContextMenuStrip Menu { get; }
        public bool IsDragging => _session != null && _session.IsActive;
        public bool IsArmed => _session != null && _session.IsArmed;

        public ReorderSupport(Control host,
                              Func<Control?> ownerOf,
                              Func<Control?> containerOf,
                              Func<Func<Control, bool>?> slotFilterOf,
                              Func<int> slotHeight,
                              Func<int> minTargetIndex,
                              Func<bool> positionEnabled)
        {
            _host = host;
            _ownerOf = ownerOf;
            _containerOf = containerOf;
            _slotFilterOf = slotFilterOf;
            _slotHeight = slotHeight;
            _minTargetIndex = minTargetIndex;
            _positionEnabled = positionEnabled;

            Menu = new ContextMenuStrip();
            _itemTop = new ToolStripMenuItem(L("置顶", "Move to Top"), null, (s, e) => MoveTop());
            _itemUp = new ToolStripMenuItem(L("上移", "Move Up"), null, (s, e) => MoveBy(-1));
            _itemDown = new ToolStripMenuItem(L("下移", "Move Down"), null, (s, e) => MoveBy(1));
            _itemBottom = new ToolStripMenuItem(L("置底", "Move to Bottom"), null, (s, e) => MoveBottom());
            var itemRestore = new ToolStripMenuItem(L("恢复默认顺序", "Restore Default Order"), null,
                                                    (s, e) => RestoreDefaultOrderRequested?.Invoke(this, EventArgs.Empty));

            Menu.Items.AddRange(new ToolStripItem[]
            {
                _itemTop, _itemUp, _itemDown, _itemBottom,
                new ToolStripSeparator(),
                itemRestore,
            });
            Menu.Opening += (s, e) => RefreshEnabled();

            // 宿主被释放（列表刷新 / 窗口关闭）：会话立即收尾，定时器不停留在后台
            _host.Disposed += (s, e) => _session?.Abort();
        }

        /// <summary>把菜单挂到该控件及其所有子控件（右键落在行内标签 / 空白区也能弹出）。</summary>
        public void AttachMenu(Control root)
        {
            // 输入框自带剪切 / 复制 / 粘贴菜单：挂上本菜单会把它顶掉 → 跳过输入框本身，其余子控件照挂
            if (root is TextBoxBase) return;
            root.ContextMenuStrip = Menu;
            foreach (Control c in root.Controls) AttachMenu(c);
        }

        /// <summary>释放菜单：ContextMenuStrip 不随宿主控件自动释放 → 每次 ReloadList 每行都会漏一个。</summary>
        public void Dispose()
        {
            Menu.Dispose();
        }

        // ===== 鼠标（左键落在抓手区才起拖；拖拽期由 Capture 吞事件，不改输入框 Enabled）=====
        public void MouseDown(Point location, MouseButtons button)
        {
            if (button != MouseButtons.Left) return;
            if (!_positionEnabled() || !MonitorGrip.Hit(location)) return;

            var container = _containerOf();
            var owner = _ownerOf();
            if (container == null || owner == null || _host.IsDisposed) return;

            // source = 可移动项（组层是组块）；captureTarget = 持有鼠标的控件（组层是组头）
            _session = new ListReorder.DragSession(owner, _host, container, _slotFilterOf(),
                                                  _slotHeight(), _minTargetIndex(),
                                                  UIUtils.S(12), UIUtils.S(28), UIColors.Primary);
            _session.OnMouseDown(Cursor.Position);
        }

        public void MouseMove()
        {
            if (_session != null && _session.IsBusy) _session.OnMouseMove();
        }

        public void MouseUp(MouseButtons button)
        {
            if (button != MouseButtons.Left) return;
            _session?.OnMouseUp();
        }

        public void CaptureLost()
        {
            if (_session != null && _session.IsActive && !_host.Capture) _session.Abort();
        }

        /// <summary>鼠标移出宿主：只清理「已按下、未起拖」的会话——此时无 Capture，
        /// MouseUp 送不回来，留着会让鼠标再移入时凭空起拖。</summary>
        public void MouseLeave()
        {
            if (_session != null && _session.IsArmed && !_session.IsActive) _session.Abort();
        }

        // ===== 右键菜单 =====
        private List<Control> Slots()
        {
            var container = _containerOf();
            if (container == null) return new List<Control>();
            var filter = _slotFilterOf();
            return ListReorder.VisualChildren(container).Where(c => filter == null || filter(c)).ToList();
        }

        private void MoveTop() { var slots = Slots(); MoveToSlot(slots, Math.Max(0, _minTargetIndex())); }
        private void MoveBottom() { var slots = Slots(); MoveToSlot(slots, slots.Count - 1); }
        private void MoveBy(int delta)
        {
            var owner = _ownerOf();
            if (owner == null) return;
            var slots = Slots();
            MoveToSlot(slots, slots.IndexOf(owner) + delta);
        }

        private void MoveToSlot(List<Control> slots, int slotIndex)
        {
            var container = _containerOf();
            var owner = _ownerOf();
            if (container == null || owner == null || slots.Count == 0) return;

            int min = Math.Max(0, _minTargetIndex());
            if (slotIndex < min) slotIndex = min;
            if (slotIndex > slots.Count - 1) slotIndex = slots.Count - 1;
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            // 唯一的 z 序落地
            ListReorder.MoveSlot(container, owner, ListReorder.GetLogicalIndex(container, slots[slotIndex]));
        }

        /// <summary>首项禁用「置顶 / 上移」、末项禁用「下移 / 置底」；DASH 组头位置项全禁用。</summary>
        private void RefreshEnabled()
        {
            var owner = _ownerOf();
            var slots = owner == null ? new List<Control>() : Slots();
            int idx = owner == null ? -1 : slots.IndexOf(owner);
            bool on = _positionEnabled();
            bool canUp = on && idx > Math.Max(0, _minTargetIndex());
            bool canDown = on && idx >= 0 && idx < slots.Count - 1;

            _itemTop.Enabled = canUp;
            _itemUp.Enabled = canUp;
            _itemDown.Enabled = canDown;
            _itemBottom.Enabled = canDown;
        }

        /// <summary>菜单文案：与 HardwareInfoForm 同一约定（无语言键的局部文案走中英双写）。</summary>
        private static string L(string zh, string en)
            => LanguageManager.CurrentLang == "zh" ? zh : en;
    }

    public class MonitorItemRow : Panel
    {
        public MonitorItemConfig Config { get; private set; }

        private Label _lblId;           
        private Label _lblName;         
        
        private LiteUnderlineInput _inputName;  
        private LiteUnderlineInput _inputShort; 
        
        // ★★★ [新增] 单位输入框 ★★★
        private LiteUnderlineInput _inputUnit;

        private LiteCheck _chkPanel;            
        private LiteCheck _chkTaskbar;          
        
        private LiteSortBtn _btnUp;
        private LiteSortBtn _btnDown;

        private bool _isTaskbarMode = false; // 记录当前模式

        public event EventHandler MoveUp;
        public event EventHandler MoveDown;

        /// <summary>右键菜单「恢复默认顺序」（由 MonitorPage 接住：只作用于当前页签）。</summary>
        public event EventHandler? RestoreDefaultOrderRequested;

        /// <summary>本行可落到的最小槽位下标（行恒为 0；组层用 1 实现 DASH 位次冻结）。</summary>
        public int MinTargetIndex { get; set; } = 0;

        /// <summary>▲/▼ 是否可见（DASH 组隐藏：位次冻结时留着按钮＝点了没反应）。
        /// 记的是「请求状态」——Control.Visible 读的是含父链的有效可见性，窗口未显示时一律为 false。</summary>
        public bool SortButtonsVisible
        {
            get => _sortButtonsVisible;
            set { _sortButtonsVisible = value; _btnUp.Visible = value; _btnDown.Visible = value; }
        }
        private bool _sortButtonsVisible = true;

        // 抓手 + 右键菜单 + 拖拽接线
        private readonly ReorderSupport _reorder;

        // ★★★ 修改构造函数：增加 isTaskbarMode 参数 ★★★
        public MonitorItemRow(MonitorItemConfig item, bool isTaskbarMode)
        {
            this.Config = item;
            this._isTaskbarMode = isTaskbarMode; // 记录模式
            this.Dock = DockStyle.Top;
            this.Height = MonitorLayout.H_ROW;
            this.BackColor = Color.White;

            // 1. ID Label (主界面模式)
            _lblId = new Label
            {
                Text = item.Key,
                Location = new Point(MonitorLayout.X_COL1, UIUtils.S(14)),
                Size = new Size(UIUtils.S(110), UIUtils.S(20)),
                AutoEllipsis = true,
                ForeColor = UIColors.TextSub,
                Font = UIFonts.Regular(8F)
            };

            // 2. Name Label (任务栏模式)
            // [Refactor] 使用统一解析器
            string labelResolved = MetricLabelResolver.ResolveLabel(item);
            
            string defName = LanguageManager.T(UIUtils.Intern("Items." + item.Key));
            string valName = !string.IsNullOrEmpty(labelResolved) ? labelResolved : defName;
            
            _lblName = new Label
            {
                Text = valName, 
                Location = new Point(MonitorLayout.X_COL1, UIUtils.S(14)),
                Size = new Size(UIUtils.S(110), UIUtils.S(20)),
                AutoEllipsis = true,
                // [需求2] 字体样式与主界面Tab(_lblId)保持一致
                ForeColor = UIColors.TextSub, 
                Font = UIFonts.Regular(8F),
                Visible = false 
            };

            // 3. Name Input
            _inputName = new LiteUnderlineInput(valName, "", "", 100, UIColors.TextMain) 
            { Location = new Point(MonitorLayout.X_COL2, UIUtils.S(8)) };

            // 4. Short Input
            // [Refactor] 使用统一解析器
            string shortResolved = MetricLabelResolver.ResolveShortLabel(item);

            string defShortKey = UIUtils.Intern("Short." + item.Key);
            string defShort = LanguageManager.T(defShortKey);
            if (defShort.StartsWith("Short.")) defShort = item.Key.Split('.')[1]; 
            
            string valShort = !string.IsNullOrEmpty(shortResolved) ? shortResolved : defShort;
            
            _inputShort = new LiteUnderlineInput(valShort, "", "", 80, UIColors.TextMain) 
            { Location = new Point(MonitorLayout.X_COL2, UIUtils.S(8)), Visible = false };

            // ★★★ 5. [新增] Unit Input 初始化逻辑 ★★★
            // 1. 根据传入的模式，获取正确的默认单位 (如 Panel="{u}/s", Taskbar="{u}")
            // 使用 Settings 上下文，确保用户能看到 {u} 占位符
            var unitCtx = isTaskbarMode ? MetricUtils.UnitContext.SettingsTaskbar : MetricUtils.UnitContext.SettingsPanel;
            string defUnit = MetricUtils.GetUnitStr(item.Key, 0, unitCtx);
            
            // 2. 获取当前配置值
            string userConfig = isTaskbarMode ? item.UnitTaskbar : item.UnitPanel;
            
            // 3. 决定显示文本 (null显示默认值, ""显示空格)
            // 注意：如果配置是""(隐藏)，我们给输入框显示一个空格" "，让用户知道这里不是空的(默认)，而是特意留空的。
            string displayUnit;
            if (userConfig == null) displayUnit = defUnit;   // Auto -> 显示默认
            else if (userConfig == "") displayUnit = " ";    // Hide -> 显示空格
            else displayUnit = userConfig;                   // Custom -> 显示自定义

            // 4. 初始化输入框 (Placeholder 设为 defUnit)
            _inputUnit = new LiteUnderlineInput(displayUnit, "", "", 50, UIColors.TextSub)
            { Location = new Point(MonitorLayout.X_COL_UNIT, UIUtils.S(8)) };
            _inputUnit.Inner.Font = UIFonts.Regular(8F);

            // 6. Checkboxes
            _chkPanel = new LiteCheck(item.VisibleInPanel, LanguageManager.T("Menu.MainForm")) 
            { Location = new Point(MonitorLayout.X_COL3, UIUtils.S(10)) };
            
            _chkTaskbar = new LiteCheck(item.VisibleInTaskbar, LanguageManager.T("Menu.Taskbar")) 
            { Location = new Point(MonitorLayout.X_COL3, UIUtils.S(10)), Visible = false };

            // 7. Sort Buttons
            _btnUp = new LiteSortBtn("▲") { Location = new Point(MonitorLayout.X_COL4, UIUtils.S(10)) };
            _btnDown = new LiteSortBtn("▼") { Location = new Point(MonitorLayout.X_COL4 + UIUtils.S(36), UIUtils.S(10)) };
            
            _btnUp.Click += (s, e) => MoveUp?.Invoke(this, EventArgs.Empty);
            _btnDown.Click += (s, e) => MoveDown?.Invoke(this, EventArgs.Empty);

            this.Controls.AddRange(new Control[] { 
                _lblId, _lblName, 
                _inputName, _inputShort, _inputUnit, // 加在这里
                _chkPanel, _chkTaskbar, 
                _btnUp, _btnDown 
            });

            // ★★★ 立即应用模式可见性 ★★★
            ApplyModeVisibility();

            // 抓手 + 右键菜单 + 拖拽：行的兄弟槽位 = 同一容器内的其他行（主界面页限本组 RowsPanel）
            _reorder = new ReorderSupport(this,
                                          () => this,
                                          () => Parent,
                                          () => (Func<Control, bool>)(c => c is MonitorItemRow),
                                          () => MonitorLayout.H_ROW,
                                          () => MinTargetIndex,
                                          () => true);
            _reorder.RestoreDefaultOrderRequested += (s, e) => RestoreDefaultOrderRequested?.Invoke(this, EventArgs.Empty);
            _reorder.AttachMenu(this);
        }

        // 提取出来的可见性设置方法
        private void ApplyModeVisibility()
        {
            if (_isTaskbarMode)
            {
                _lblId.Visible = false; _lblName.Visible = true; 
                _inputName.Visible = false; _inputShort.Visible = true;
                _chkPanel.Visible = false; _chkTaskbar.Visible = true;
            }
            else
            {
                _lblId.Visible = true; _lblName.Visible = false;
                _inputName.Visible = true; _inputShort.Visible = false;
                _chkPanel.Visible = true; _chkTaskbar.Visible = false;
            }
        }

        // [需求2] 暴露方法供组开关调用
        public void SetPanelChecked(bool check) 
        {
            _chkPanel.Checked = check;
        }

        public void SetMode(bool isTaskbarMode)
        {
            // 如果模式没变，直接返回
            if (_isTaskbarMode == isTaskbarMode) return;
            
            _isTaskbarMode = isTaskbarMode;

            // 重新获取默认值和配置
            // 使用 Settings 上下文，确保切换模式时，Placeholder 依然正确显示带 {u} 的格式
            // [Fix] 根据模式动态选择上下文
            var unitCtx = isTaskbarMode ? MetricUtils.UnitContext.SettingsTaskbar : MetricUtils.UnitContext.SettingsPanel;
            string defUnit = MetricUtils.GetUnitStr(Config.Key, 0, unitCtx);
            string userConfig = isTaskbarMode ? Config.UnitTaskbar : Config.UnitPanel;

            // 更新 Input 状态
            // null -> 默认, "" -> 空格
            string display = (userConfig == null) ? defUnit : (userConfig == "" ? " " : userConfig);
            _inputUnit.Inner.Text = display;
            
            ApplyModeVisibility();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(UIColors.Border))
                e.Graphics.DrawLine(p, MonitorLayout.X_COL1, Height - 1, Width - UIUtils.S(20), Height - 1);

            MonitorGrip.Paint(e.Graphics, Height, MonitorGrip.GripColor);   // 抓手（6 点自绘）

            // 拖拽期反馈：源行左缘高亮；目标槽位画插入线（线随内容滚动，所以由槽位自己画）
            var drag = ListReorder.Active;
            if (drag == null) return;

            if (drag.IsSource(this))
            {
                using var b = new SolidBrush(UIColors.Primary);
                e.Graphics.FillRectangle(b, 0, 0, UIUtils.S(3), Height);
            }
            else if (drag.ShouldPaintLineFor(this))
            {
                ListReorder.PaintInsertionLine(e.Graphics, ClientRectangle, drag.LineColor,
                                               UIUtils.S(ListReorder.LineThickness));
            }
        }

        // ===== 拖拽接线：只有左键落在抓手区（命中区在 ID 列左侧空档）才起拖 =====
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _reorder.MouseDown(e.Location, e.Button);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _reorder.MouseMove();
            if (!_reorder.IsDragging && !_reorder.IsArmed)
                Cursor = MonitorGrip.Hit(e.Location) ? Cursors.SizeAll : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _reorder.MouseUp(e.Button);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _reorder.MouseLeave();   // 按下后移出：清掉未起拖的会话（无 Capture，MouseUp 送不回来）
            if (!_reorder.IsDragging) Cursor = Cursors.Default;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _reorder.CaptureLost();
        }

        /// <summary>行被释放（ReloadList 逐行 Dispose）：连右键菜单一起释放，避免菜单泄漏。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) _reorder?.Dispose();
            base.Dispose(disposing);
        }

        public void SyncToConfig()
        {
            // Name
            string valName = _inputName.Inner.Text; // 不使用 Trim，允许用户输入空格隐藏
            string originalName = LanguageManager.GetOriginal(UIUtils.Intern("Items." + Config.Key));
            
            // [Fix] Handle untranslated plugin keys (e.g. DASH.UniversalAPI.0.val)
            // If GetOriginal returns the full key (fallback), try to extract the last part as the field name
            if (Config.Key.StartsWith("DASH.") && originalName.Contains("DASH."))
            {
                int lastDot = originalName.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < originalName.Length - 1) 
                    originalName = originalName.Substring(lastDot + 1);
            }

            // 规范化：如果是纯空格，则转为一个空格
            if (valName.Length > 0 && string.IsNullOrWhiteSpace(valName))
            {
                 valName = " ";
                 // 即时反馈
                 if (_inputName.Inner.Text != valName) _inputName.Inner.Text = valName;
            }
            else
            {
                // 否则去掉首尾空格
                string trimmed = valName.Trim();
                if (trimmed != valName) 
                {
                    valName = trimmed;
                    _inputName.Inner.Text = valName;
                }
            }

            // [Improvement] Check against current runtime dynamic label as well
            string currentDynamicLabel = InfoService.Instance.GetValue("PROP.Label." + Config.Key);
            if (string.IsNullOrEmpty(currentDynamicLabel)) currentDynamicLabel = Config.DynamicLabel;

            // 如果等于默认值，或者等于当前的动态值，则存为空字符串 (恢复自动模式)
            // 否则存为用户自定义值 (锁定模式)
            if (valName != " " && (string.Equals(valName, originalName, StringComparison.OrdinalIgnoreCase) || valName == currentDynamicLabel))
                Config.UserLabel = "";
            else
                Config.UserLabel = valName;

            // Short (TaskbarLabel)
            // ★★★ 修复：允许用户输入空格 (" ") 来隐藏标签 ★★★
            string rawShort = _inputShort.Inner.Text;
            string valShort;
            
            // Handle space logic for short label
            if (rawShort.Length > 0 && string.IsNullOrWhiteSpace(rawShort))
            {
                valShort = " ";
            }
            else
            {
                valShort = rawShort.Trim();
            }
            
            // 同样应用动态值检查逻辑
            string defShortKey = UIUtils.Intern("Short." + Config.Key);
            // [Fix] Must use GetOriginal to ignore current user overrides, otherwise it will toggle back to default
            string defShort = LanguageManager.GetOriginal(defShortKey); 
            
            // 获取当前理论上的动态 Short (InfoService > DynamicShort > null)
            string currentDynamicShort = InfoService.Instance.GetValue("PROP.ShortLabel." + Config.Key);
            if (string.IsNullOrEmpty(currentDynamicShort)) currentDynamicShort = Config.DynamicTaskbarLabel;

            if (valShort == "")
            {
                Config.TaskbarLabel = "";
            }
            else if (valShort == " ")
            {
                Config.TaskbarLabel = " ";
            }
            else
            {
                 // 如果等于默认值，或者等于当前的动态简称，则存为空字符串 (恢复自动模式)
                if (valShort == defShort || valShort == currentDynamicShort)
                    Config.TaskbarLabel = "";
                else
                    Config.TaskbarLabel = valShort;
            }
            
            // 最终赋值 (逻辑已在上方处理完毕)
            // 无需再次检查，直接使用计算好的 valShort 即可
            // (注意：上方的 if/else 块已经给 Config.TaskbarLabel 赋值了，这里需要移除多余的旧代码)

            // ★★★ 核心修改：保存单位逻辑 ★★★
            string rawUnit = _inputUnit.Inner.Text; // 【关键】不使用 Trim()，保留用户输入的空格
            
            // 获取当前模式下的默认单位，用于对比
            // 保存时同样需要对比 Settings 上下文下的默认值 (因为输入框显示的是 Settings 格式)
            var unitCtx = _isTaskbarMode ? MetricUtils.UnitContext.SettingsTaskbar : MetricUtils.UnitContext.SettingsPanel;
            string defUnit = MetricUtils.GetUnitStr(Config.Key, 0, unitCtx);
            string finalVal;

            if (string.IsNullOrEmpty(rawUnit)) 
            {
                // 情况1：用户清空输入框 -> 恢复默认 (Auto/null)
                finalVal = null;
            }
            else if (string.IsNullOrWhiteSpace(rawUnit))
            {
                // 情况2：用户只输入了空格 (" ") -> 强制不显示 ("")
                // 这里的逻辑必须保留：纯空格意味着用户想隐藏单位
                finalVal = "";
            }
            else
            {
                string val = rawUnit; // 已经是原始值了

                // 情况3：用户输入了内容
                // 【关键】使用 Ordinal 区分大小写。
                // 如果用户输入 "{u}/S"，不等于默认的 "{u}/s"，会作为自定义值保存。
                if (string.Equals(val, defUnit, StringComparison.Ordinal))
                    finalVal = null;
                else
                    finalVal = val;
            }

            if (_isTaskbarMode) Config.UnitTaskbar = finalVal;
            else Config.UnitPanel = finalVal;

            Config.VisibleInPanel = _chkPanel.Checked;
            Config.VisibleInTaskbar = _chkTaskbar.Checked;
        }
    }

    public class MonitorGroupHeader : Panel
    {
        public string GroupKey { get; private set; }
        public LiteUnderlineInput InputAlias { get; private set; }
        private LiteCheck _chkAll; // [需求2] 增加组开关
        private LiteSortBtn _btnUp;
        private LiteSortBtn _btnDown;
        public event EventHandler MoveUp;
        public event EventHandler MoveDown;
        public event EventHandler<bool> ToggleGroup; // [需求2] 组开关事件

        /// <summary>右键菜单「恢复默认顺序」（由 MonitorPage 接住：只作用于当前页签）。</summary>
        public event EventHandler? RestoreDefaultOrderRequested;

        /// <summary>是否可参与重排（DASH 组 = false：无抓手、▲/▼ 隐藏、位置项禁用）。</summary>
        public bool ReorderEnabled { get; set; } = true;

        /// <summary>可落到的最小槽位下标（组层：DASH 存在时为 1，保证任何组不排到 DASH 之前）。</summary>
        public int MinTargetIndex { get; set; } = 0;

        /// <summary>兄弟槽位判定（组块类型由 MonitorPage 注入；不注入 = 所有兄弟）。</summary>
        public Func<Control, bool>? SlotFilter { get; set; }

        /// <summary>▲/▼ 是否可见（DASH 组隐藏）。记的是「请求状态」，理由同行。</summary>
        public bool SortButtonsVisible
        {
            get => _sortButtonsVisible;
            set { _sortButtonsVisible = value; _btnUp.Visible = value; _btnDown.Visible = value; }
        }
        private bool _sortButtonsVisible = true;

        // 抓手 + 右键菜单 + 拖拽接线
        private readonly ReorderSupport _reorder;

        public MonitorGroupHeader(string groupKey, string alias)
        {
            this.GroupKey = groupKey;
            this.Dock = DockStyle.Top;
            this.Height = UIUtils.S(45);
            this.BackColor = UIColors.GroupHeader; 

            var lblId = new Label { 
                Text = groupKey, 
                Location = new Point(MonitorLayout.X_COL1, UIUtils.S(12)), 
                AutoSize = true, 
                Font = UIFonts.Bold(9F), 
                ForeColor = Color.Gray 
            };

            string defGName = LanguageManager.T("Groups." + groupKey);
            if (defGName.StartsWith("Groups.")) defGName = groupKey;
            
            InputAlias = new LiteUnderlineInput(string.IsNullOrEmpty(alias) ? defGName : alias, "", "", 100) 
            { Location = new Point(MonitorLayout.X_COL2, UIUtils.S(8)) };
            InputAlias.SetBg(UIColors.GroupHeader); 
            InputAlias.Inner.Font = UIFonts.Bold(9F);

            // [需求2] 组开关复选框
            _chkAll = new LiteCheck(true, "") 
            { 
                Location = new Point(MonitorLayout.X_COL3, UIUtils.S(12)),
                // 默认不显示文字，只作为全选/全不选开关
            };
            // 点击事件：触发 ToggleGroup
            _chkAll.Click += (s, e) => ToggleGroup?.Invoke(this, _chkAll.Checked);

            // ▲/▼ 提升为字段：DASH 组需整组隐藏
            _btnUp = new LiteSortBtn("▲") { Location = new Point(MonitorLayout.X_COL4, UIUtils.S(10)) };
            _btnDown = new LiteSortBtn("▼") { Location = new Point(MonitorLayout.X_COL4 + UIUtils.S(36), UIUtils.S(10)) };

            _btnUp.Click += (s, e) => MoveUp?.Invoke(this, EventArgs.Empty);
            _btnDown.Click += (s, e) => MoveDown?.Invoke(this, EventArgs.Empty);

            this.Controls.AddRange(new Control[] { lblId, InputAlias, _chkAll, _btnUp, _btnDown });

            // 抓手 + 右键菜单 + 拖拽：可移动项 = Tag 里的组块；兄弟容器 = 组块所在的 _container
            _reorder = new ReorderSupport(this,
                                          () => Tag as Control,
                                          () => (Tag as Control)?.Parent,
                                          () => SlotFilter,
                                          () => 0,                       // 组块高矮不一 → 按各槽中心计算
                                          () => MinTargetIndex,
                                          () => ReorderEnabled);
            _reorder.RestoreDefaultOrderRequested += (s, e) => RestoreDefaultOrderRequested?.Invoke(this, EventArgs.Empty);
            _reorder.AttachMenu(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using(var p = new Pen(UIColors.Border)) 
                e.Graphics.DrawLine(p, 0, Height-1, Width, Height-1);

            if (!ReorderEnabled) return;   // DASH：位次冻结 → 无抓手

            MonitorGrip.Paint(e.Graphics, Height, MonitorGrip.GripColor);

            var drag = ListReorder.Active;
            if (drag == null) return;

            // 组层：槽位 = 组块；组块其余部分被卡片遮住，只有组头是可见表头 → 由组头代为绘制
            var block = Tag as Control;
            if (block == null) return;

            if (drag.IsSource(block))
            {
                using var b = new SolidBrush(UIColors.Primary);
                e.Graphics.FillRectangle(b, 0, 0, UIUtils.S(3), Height);
            }
            else if (drag.ShouldPaintLineFor(block))
            {
                ListReorder.PaintInsertionLine(e.Graphics, ClientRectangle, drag.LineColor,
                                               UIUtils.S(ListReorder.LineThickness));
            }
        }

        // ===== 拖拽接线（DASH 组头不进这里：ReorderEnabled = false）=====
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!ReorderEnabled) return;
            _reorder.MouseDown(e.Location, e.Button);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!ReorderEnabled) return;
            _reorder.MouseMove();
            if (!_reorder.IsDragging && !_reorder.IsArmed)
                Cursor = MonitorGrip.Hit(e.Location) ? Cursors.SizeAll : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!ReorderEnabled) return;
            _reorder.MouseUp(e.Button);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _reorder.MouseLeave();   // 按下后移出：清掉未起拖的会话（无 Capture，MouseUp 送不回来）
            if (!_reorder.IsDragging) Cursor = Cursors.Default;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _reorder.CaptureLost();
        }

        /// <summary>组头被释放（ReloadList 逐块 Dispose）：连右键菜单一起释放，避免菜单泄漏。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) _reorder?.Dispose();
            base.Dispose(disposing);
        }
    }
}