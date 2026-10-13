using LiteMonitor.src.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;

namespace LiteMonitor
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            // === 1. 语言判断与文案准备 ===
            bool isZh = LanguageManager.CurrentLang == "zh";

            string strTitle = LanguageManager.T("Menu.About");

            // 版权行年份动态：起始年 2025（项目首发年）+ 当前年，跨年自动跟随
            const int startYear = 2025;
            int yearNow = DateTime.Now.Year;
            string strCopyright = yearNow > startYear
                ? $"© {startYear}–{yearNow} Diorser / LiteMonitor Project"
                : $"© {startYear} Diorser / LiteMonitor Project";

            string strDesc = isZh ? $"一款开源的轻量级硬件监控软件。\n{strCopyright}"
                                  : $"A lightweight desktop hardware monitor.\n{strCopyright}";

            // 署名三行：原作者/原仓库、原官网 与 本源（fork）并列表述，互不覆盖
            string sep = isZh ? "：" : ": ";
            string lblOriginal = isZh ? "原项目" : "Original project";
            string lblWebsite = isZh ? "原官网" : "Original website";
            string lblFork = isZh ? "本版本（Fork）" : "This build (fork)";
            string strOriginalLine = $"{lblOriginal}{sep}Diorser — github.com/Diorser/LiteMonitor";
            string strWebLine = $"{lblWebsite}{sep}litemonitor.cn";
            string strForkLine = $"{lblFork}{sep}azhuge233 — github.com/azhuge233/LiteMonitor";
            string strUpdate = LanguageManager.T("Menu.CheckUpdate");
            string strClose = LanguageManager.T("Menu.OK");
            string strBug = LanguageManager.T("Menu.Feedback");

            // === 基础外观 ===
            Text = strTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;   // ✅ 居中在屏幕（不会被主窗体挡住）
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;                                   // ✅ 确保显示在最前
            
            // ★★★ DPI 修复：计算缩放系数并应用 ★★★
            UIUtils.ScaleFactor = this.DeviceDpi / 96f;
            ClientSize = UIUtils.S(new Size(360, 280));   // 兜底尺寸；构造函数末尾按最宽内容行自适应

            var theme = ThemeManager.Current;
            BackColor = ThemeManager.ParseColor(theme.Color.Background);

            // === 标题 ===
            var lblTitle = new Label
            {
                Text = "⚡️LiteMonitor",
                Font = new Font(theme.Font.Family, 14, FontStyle.Bold),
                ForeColor = ThemeManager.ParseColor(theme.Color.TextTitle),
                AutoSize = true,
                Location = new Point(UIUtils.S(22), UIUtils.S(24))
            };

            // === 简洁版本号 ===
            string version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "1.0.2";
            // ✅ 自动清理 Git 哈希后缀（如 1.0+abc123 → 1.0）

            int plus = version.IndexOf('+');
            if (plus > 0) version = version[..plus];

            var lblVer = new Label
            {
                Text = $"Version {version}",
                ForeColor = ThemeManager.ParseColor(theme.Color.TextPrimary),
                Location = new Point(UIUtils.S(32), UIUtils.S(62)),
                AutoSize = true
            };

            // === 简介 ===
            var lblDesc = new Label
            {
                Text = strDesc,
                ForeColor = ThemeManager.ParseColor(theme.Color.TextPrimary),
                Location = new Point(UIUtils.S(32), UIUtils.S(92)),
                AutoSize = true
            };

            // === 官网链接 ===
            var websiteLink = new LinkLabel
            {
                Text = strWebLine,
                LinkColor = Color.SkyBlue,
                ActiveLinkColor = Color.LightSkyBlue,
                VisitedLinkColor = Color.DeepSkyBlue,
                Location = new Point(UIUtils.S(32), UIUtils.S(167)),
                AutoSize = true
            };
            websiteLink.LinkClicked += (_, __) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://LiteMonitor.cn")
                    { UseShellExecute = true });
                }
                catch { }
            };

            // === GitHub 链接 ===
            var githubLink = new LinkLabel
            {
                Text = strOriginalLine,
                LinkColor = Color.SkyBlue,
                ActiveLinkColor = Color.LightSkyBlue,
                VisitedLinkColor = Color.DeepSkyBlue,
                Location = new Point(UIUtils.S(32), UIUtils.S(142)),
                AutoSize = true
            };
            githubLink.LinkClicked += (_, __) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/Diorser/LiteMonitor")
                    { UseShellExecute = true });
                }
                catch { }
            };

            // === 本源（fork）仓库链接 ===
            var forkLink = new LinkLabel
            {
                Text = strForkLine,
                LinkColor = Color.SkyBlue,
                ActiveLinkColor = Color.LightSkyBlue,
                VisitedLinkColor = Color.DeepSkyBlue,
                Location = new Point(UIUtils.S(32), UIUtils.S(192)),
                AutoSize = true
            };
            forkLink.LinkClicked += (_, __) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/azhuge233/LiteMonitor")
                    { UseShellExecute = true });
                }
                catch { }
            };

            // === BUG 反馈按钮 (新增) ===
            var btnBug = new Button
            {
                Text = strBug,
                Size = UIUtils.S(new Size(100, 30)),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                // ✅ 与左侧文字(x=32)对齐
                Location = new Point(UIUtils.S(32), UIUtils.S(235)),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.ParseColor(theme.Color.GroupBackground),
                ForeColor = ThemeManager.ParseColor(theme.Color.TextPrimary),
                Font = new Font(theme.Font.Family, 9.5f, FontStyle.Regular)
            };
            btnBug.FlatAppearance.BorderSize = 0;
            btnBug.FlatAppearance.MouseOverBackColor = ThemeManager.ParseColor(theme.Color.BarBackground);
            btnBug.Click += (_, __) => 
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/azhuge233/LiteMonitor/issues")
                    { UseShellExecute = true });
                }
                catch { }
            };

            // === 检查更新按钮 ===
            var btnCheckUpdate = new Button
            {
                Text = strUpdate,
                Size = UIUtils.S(new Size(100, 30)),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                // 稍微往右移一点，给 Bug 按钮腾出空间 (原 150 保持不变，刚好合适)
                Location = new Point(UIUtils.S(150), UIUtils.S(235)),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.ParseColor(theme.Color.GroupBackground),
                ForeColor = ThemeManager.ParseColor(theme.Color.TextPrimary),
                Font = new Font(theme.Font.Family, 9.5f, FontStyle.Regular)
            };
            btnCheckUpdate.FlatAppearance.BorderSize = 0;
            btnCheckUpdate.FlatAppearance.MouseOverBackColor = ThemeManager.ParseColor(theme.Color.BarBackground);
            btnCheckUpdate.Click += async (_, __) => await UpdateChecker.CheckAsync(showMessage: true);

            // === 关闭按钮（扁平风格） ===
            var btnClose = new Button
            {
                Text = strClose,
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Size = UIUtils.S(new Size(70, 30)),
                Location = new Point(UIUtils.S(270), UIUtils.S(235)),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.ParseColor(theme.Color.GroupBackground),
                ForeColor = ThemeManager.ParseColor(theme.Color.TextPrimary),
                Font = new Font(theme.Font.Family, 9.5f, FontStyle.Regular)
            };
            btnClose.FlatAppearance.BorderSize = 0;           // ✅ 移除白边框
            btnClose.FlatAppearance.MouseOverBackColor = ThemeManager.ParseColor(theme.Color.BarBackground);

            // 别忘了把新按钮 btnBug 加入集合（forkLink 同理）
            Controls.AddRange([lblTitle, lblVer, lblDesc, websiteLink, githubLink, forkLink, btnBug, btnCheckUpdate, btnClose]);

            // === 版式自适应：窗口宽度按最宽文本行取值（新增署名行不会截断文字）===
            int maxTextW = 0;
            foreach (Control c in Controls)
            {
                if (c is Label lbl)   // LinkLabel 继承自 Label，一并覆盖
                    maxTextW = Math.Max(maxTextW, lbl.PreferredWidth);
            }
            ClientSize = new Size(Math.Max(UIUtils.S(360), maxTextW + UIUtils.S(64)), UIUtils.S(280));
        }
    }
}