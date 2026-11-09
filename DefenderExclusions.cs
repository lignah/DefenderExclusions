// Windows Defender 제외 항목 관리자 (WinForms) — WMI: root\Microsoft\Windows\Defender, MSFT_MpPreference
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("Defender 제외 항목 관리")]
[assembly: AssemblyProduct("DefenderExclusions")]
[assembly: AssemblyCopyright("lignah")]
[assembly: AssemblyVersion("0.1")]
[assembly: AssemblyFileVersion("0.1")]

namespace DefenderExclusions
{
    static class Defender
    {
        const string NS = @"\\.\root\Microsoft\Windows\Defender";

        public static string[] Get(string prop)
        {
            var scope = new ManagementScope(NS);
            using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT " + prop + " FROM MSFT_MpPreference")))
            {
                foreach (ManagementObject mo in s.Get())
                {
                    var v = mo[prop] as string[];
                    if (v == null) return new string[0];
                    return v.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                }
            }
            return new string[0];
        }

        static void Invoke(string method, string prop, string[] values)
        {
            using (var mc = new ManagementClass(new ManagementScope(NS), new ManagementPath("MSFT_MpPreference"), null))
            {
                var inP = mc.GetMethodParameters(method);
                inP[prop] = values;
                var outP = mc.InvokeMethod(method, inP, null);
                if (outP != null && outP["ReturnValue"] != null)
                {
                    uint rv = Convert.ToUInt32(outP["ReturnValue"]);
                    if (rv != 0) throw new Exception(method + " 실패 (ReturnValue=0x" + rv.ToString("X") + ")");
                }
            }
        }

        public static void Add(string prop, string[] values) { Invoke("Add", prop, values); }
        public static void Remove(string prop, string[] values) { Invoke("Remove", prop, values); }
    }

    static class Theme
    {
        public static readonly Color BG = Color.FromArgb(0x14, 0x14, 0x15);              // #141415
        public static readonly Color PANEL = Color.FromArgb(0x19, 0x19, 0x1B);
        public static readonly Color ALT = Color.FromArgb(0x1F, 0x1F, 0x21);             // 홀짝 행
        public static readonly Color CTRL = Color.FromArgb(0x23, 0x23, 0x25);           // raised / 입력 / 버튼
        public static readonly Color CTRL_HOVER = Color.FromArgb(0x2C, 0x2C, 0x2E);
        public static readonly Color CTRL_DOWN = Color.FromArgb(0x35, 0x35, 0x38);
        public static readonly Color BORDER = Color.FromArgb(0x2A, 0x2A, 0x2C);     // quiet hairline
        public static readonly Color FG = Color.FromArgb(0xED, 0xED, 0xED);            // primary text
        public static readonly Color SECONDARY = Color.FromArgb(0xA1, 0xA1, 0xA1);    // captions / shortcut
        public static readonly Color DIM = Color.FromArgb(0x85, 0x85, 0x85);           // muted
        public static readonly Color PRIMARY = Color.FromArgb(0x14, 0xC3, 0x70);    // 강조색 (주 버튼 채움)
        public static readonly Color PRIMARY_HOVER = Color.FromArgb(0x2B, 0xD4, 0x83); 
        public static readonly Color PRIMARY_DOWN = Color.FromArgb(0x10, 0xA5, 0x5F);
        public static readonly Color PRIMARY_FG = Color.FromArgb(0x0B, 0x1F, 0x15);   // 주 버튼 글자 (초록 위 어두운 글자)
        public static readonly Color SELECTED = Color.FromArgb(0x3A, 0x3A, 0x3E);      // 목록 선택 행 (줄무늬보다 확실히 밝게)
        public static readonly Color EMPHASIS = Color.FromArgb(0x14, 0xC3, 0x70);     // 선택 막대 / 탭 밑줄
        public static readonly Color FOCUS = Color.FromArgb(0x14, 0xC3, 0x70);          // 포커스 링
        public static readonly Color DANGER = Color.FromArgb(0xE5, 0x48, 0x4D);       // 삭제 hover
        public static readonly Color DANGER_FG = Color.FromArgb(0xFF, 0x9A, 0x9C);

        public static Font UiFont(float pt, FontStyle style = FontStyle.Regular)
        {
            // 한국어 Windows에서 FontFamily.Name: "맑은 고딕". 영문 이름(1033)으로 확인
            foreach (var name in new[] { "Malgun Gothic", "Segoe UI" })
            {
                try
                {
                    using (var ff = new FontFamily(name))
                        if (string.Equals(ff.GetName(1033), name, StringComparison.OrdinalIgnoreCase))
                            return new Font(name, pt, style, GraphicsUnit.Point);
                }
                catch (ArgumentException) { }
            }
            return new Font(SystemFonts.MessageBoxFont.FontFamily, pt, style, GraphicsUnit.Point);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // 직접 그리는 평면 버튼: 주 버튼은 밝은 채움+어두운 글자, 탭 선택은 밑줄, 단축키는 흐린 색
    class FlatBtn : Button
    {
        public bool Accent;      // 주 버튼 (밝은 무채색 채움)
        public bool Selected;    // 탭 선택 상태 (하단 밑줄)
        public bool Danger;      // 파괴적 동작 (hover 시 빨간 테두리)
        bool hover, down;
        public int Radius = 6;

        public FlatBtn()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            AutoSize = false;
        }

        protected override bool ShowFocusCues { get { return base.ShowFocusCues; } }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.BG);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool primary = Accent && Enabled;
            bool hot = hover && Enabled;
            Color bg, border, fg;
            if (primary)
            {
                bg = down ? Theme.PRIMARY_DOWN : hot ? Theme.PRIMARY_HOVER : Theme.PRIMARY;
                border = bg;
                fg = Theme.PRIMARY_FG;
            }
            else
            {
                Color rest = Selected ? Theme.CTRL_HOVER : Theme.CTRL;
                bg = down ? Theme.CTRL_DOWN : hot ? (Selected ? Theme.CTRL_DOWN : Theme.CTRL_HOVER) : rest;
                border = hot && Danger ? Theme.DANGER : Theme.BORDER;
                fg = !Enabled ? Theme.DIM : (Danger && hot) ? Theme.DANGER_FG : Theme.FG;
            }
            if (Focused && ShowFocusCues) border = primary ? Theme.CTRL_DOWN : Theme.FOCUS;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Rounded(r, Radius))
            using (var b = new SolidBrush(bg))
            using (var p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            // Signature: 활성 탭 하단 2px 밝은 회색 밑줄
            if (Selected && Enabled)
            {
                int y = Height - 2;
                int x0 = Math.Max(Radius, 4);
                int x1 = Math.Max(x0, Width - 1 - Math.Max(Radius, 4));
                using (var p = new Pen(Theme.EMPHASIS, 2)) g.DrawLine(p, x0, y, x1, y);
            }
            DrawLabel(g, fg, primary && !Enabled ? Theme.DIM : fg);
        }

        void DrawLabel(Graphics g, Color mainFg, Color fallbackFg)
        {
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
            string text = Text ?? "";
            int split = text.LastIndexOf(" (", StringComparison.Ordinal);
            if (split > 0 && text.EndsWith(")", StringComparison.Ordinal))
            {
                string main = text.Substring(0, split);
                string shortcut = text.Substring(split); // " (F5)"
                Size szMain = TextRenderer.MeasureText(g, main, Font, new Size(int.MaxValue, Height), flags);
                Size szSc = TextRenderer.MeasureText(g, shortcut, Font, new Size(int.MaxValue, Height), flags);
                int total = szMain.Width + szSc.Width;
                int x = Math.Max(0, (Width - total) / 2);
                var mainRc = new Rectangle(x, 0, szMain.Width, Height);
                var scRc = new Rectangle(x + szMain.Width, 0, szSc.Width, Height);
                Color scFg = !Enabled ? Theme.DIM : (Accent ? Color.FromArgb(0x66, Theme.PRIMARY_FG) : Theme.SECONDARY);
                TextRenderer.DrawText(g, main, Font, mainRc, !Enabled ? Theme.DIM : mainFg, flags);
                TextRenderer.DrawText(g, shortcut, Font, scRc, scFg, flags);
            }
            else
            {
                TextRenderer.DrawText(g, text, Font, new Rectangle(0, 0, Width, Height), !Enabled ? Theme.DIM : fallbackFg,
                    flags | TextFormatFlags.HorizontalCenter);
            }
        }
    }

    // 테두리를 직접 그리고 안쪽의 테두리 없는 TextBox를 세로 가운데 정렬하는 입력 상자
    class InputBox : Panel
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public readonly TextBox Box;
        string hint = "";
        public int PadX = 8;

        public InputBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CTRL;
            Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.CTRL, ForeColor = Theme.FG };
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
            Box.HandleCreated += (s, e) => ApplyHint();
            Controls.Add(Box);
            Cursor = Cursors.IBeam;
            Click += (s, e) => Box.Focus();
        }

        public string Hint { get { return hint; } set { hint = value ?? ""; ApplyHint(); } }
        void ApplyHint() { if (Box.IsHandleCreated) SendMessage(Box.Handle, 0x1501, (IntPtr)1, hint); }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Box.Font = Font; PerformLayout(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int h = Box.PreferredHeight;
            Box.SetBounds(PadX, Math.Max(1, (Height - h) / 2), Math.Max(10, Width - PadX * 2), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.BG);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Rounded(r, 6))
            using (var b = new SolidBrush(Theme.CTRL))
            using (var p = new Pen(Box.Focused ? Theme.FOCUS : Theme.BORDER))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            if (Box.Focused)
                using (var p = new Pen(Theme.EMPHASIS, 1)) g.DrawLine(p, 6, Height - 1, Width - 7, Height - 1);
        }
    }

    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.CTRL; } }
        public override Color MenuBorder { get { return Theme.BORDER; } }
        public override Color MenuItemBorder { get { return Theme.CTRL_HOVER; } }
        public override Color MenuItemSelected { get { return Theme.CTRL_HOVER; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CTRL_HOVER; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CTRL_HOVER; } }
        public override Color SeparatorDark { get { return Theme.BORDER; } }
        public override Color SeparatorLight { get { return Theme.BORDER; } }
        public override Color ImageMarginGradientBegin { get { return Theme.CTRL; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.CTRL; } }
        public override Color ImageMarginGradientEnd { get { return Theme.CTRL; } }
    }

    class Category
    {
        public string Title, Prop, Hint;
        public bool Folder, File;
        public string FileFilter;
    }

    class MainForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string id);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint n);
        [DllImport("shell32.dll")] static extern void DragAcceptFiles(IntPtr h, bool accept);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint DragQueryFile(IntPtr hDrop, uint i, StringBuilder sb, uint cch);
        [DllImport("shell32.dll")] static extern void DragFinish(IntPtr hDrop);
        [DllImport("user32.dll")] static extern bool ChangeWindowMessageFilterEx(IntPtr h, uint msg, uint action, IntPtr p);
        [DllImport("ole32.dll")] static extern int RevokeDragDrop(IntPtr h);
        const int WM_SETICON = 0x0080, WM_DROPFILES = 0x0233;
        const uint MSGFLT_ALLOW = 1;
        IntPtr hIconLarge, hIconSmall;

        readonly Category[] cats = new[] {
            new Category { Title = "폴더 / 파일", Prop = "ExclusionPath", Folder = true, File = true, FileFilter = "모든 파일 (*.*)|*.*",
                Hint = "제외할 폴더 또는 파일 경로 (예: C:\\Tools)" },
            new Category { Title = "프로세스", Prop = "ExclusionProcess", File = true, FileFilter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
                Hint = "프로세스 경로 또는 이름 (예: C:\\App\\app.exe 또는 app.exe)" },
            new Category { Title = "확장자", Prop = "ExclusionExtension",
                Hint = "확장자 (예: .vmdk)" },
        };

        int current = 0;
        readonly List<FlatBtn> tabButtons = new List<FlatBtn>();
        readonly float scale;
        readonly int H;          // 공통 컨트롤 높이 (글꼴 기준)
        ListBox list;
        InputBox input;
        FlatBtn btnFolder, btnFile, btnAdd, btnDel, btnCopy, btnRefresh;
        Label status, inputCaption;

        int S(int px) { return (int)Math.Round(px * scale); }

        public MainForm()
        {
            // 시스템 DPI 인식: 글꼴은 pt 단위라 자동으로 DPI에 맞게 커지고, 나머지 크기는 글꼴 높이/배율로 계산
            AutoScaleMode = AutoScaleMode.None;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f; // 화면 DC 사용 (폼 핸들을 일찍 만들지 않음)
            Font = Theme.UiFont(10f);
            H = Math.Max(Font.Height + S(12), S(36));

            Text = "Defender 제외 항목 관리";
            BackColor = Theme.BG; ForeColor = Theme.FG;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            // AllowDrop 은 켜지 않음: OLE 드롭 대상으로 등록되면 탐색기가 OLE 드래그를 쓰는데, 관리자 창에는 UIPI 때문에 막힘.
            // 대신 WM_DROPFILES(DragAcceptFiles) 방식 사용 → OnHandleCreated / WndProc 참고
            // 아이콘: exe 안의 Win32 아이콘(/win32icon)을 큰/작은 크기로 각각 불러옴 (리소스 중복 포함 안 함)
            try
            {
                if (ExtractIconEx(Application.ExecutablePath, 0, out hIconLarge, out hIconSmall, 1) > 0 && hIconLarge != IntPtr.Zero)
                    Icon = Icon.FromHandle(hIconLarge);
            }
            catch { }

            // ── 레이아웃 ───────────────────────────────────────────────
            // 위→아래: [분류 탭] [목록] [새 항목 추가 + 개수]
            // 모든 행은 AutoSize(목록만 Percent) → 글꼴/DPI가 커져도 행 높이가 내용에 맞게 늘어남
            int gap = S(8);      // 같은 그룹 안 컨트롤 간격
            int section = S(16); // 그룹 사이 간격
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(S(16), S(16), S(16), S(16)), Margin = new Padding(0), BackColor = Theme.BG };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 탭
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 목록
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 추가 영역
            Controls.Add(root);

            // 1) 분류 탭: 버튼 폭 통일
            var tabs = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 0, 0, section), Padding = new Padding(0), BackColor = Theme.BG };
            for (int i = 0; i < cats.Length; i++)
            {
                int idx = i;
                var b = MakeButton(cats[i].Title, S(16));
                b.Margin = new Padding(0, 0, i < cats.Length - 1 ? gap : 0, 0);
                b.Click += (s, e) => SwitchTo(idx);
                tabButtons.Add(b);
                tabs.Controls.Add(b);
            }
            UniformWidth(tabButtons.ToArray());
            root.Controls.Add(tabs, 0, 0);

            // 2) 목록 동작은 화면에서 빼고 단축키(F5 · Ctrl+C · Del)와 오른쪽 클릭 메뉴로 제공
            status = new Label { AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.DIM, Margin = new Padding(gap, 0, 0, gap), UseMnemonic = false };
            btnRefresh = MakeButton("새로고침 (F5)"); btnRefresh.Click += (s, e) => Reload();
            btnCopy = MakeButton("복사 (Ctrl+C)"); btnCopy.Click += (s, e) => DoCopy();
            btnDel = MakeButton("삭제 (Del)"); btnDel.Danger = true; btnDel.Click += (s, e) => DoDelete();
            var menu = new ContextMenuStrip { ShowImageMargin = false, BackColor = Theme.CTRL, ForeColor = Theme.FG, Font = Font, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
            var miRefresh = new ToolStripMenuItem("새로고침", null, (s, e) => Reload()) { ShortcutKeyDisplayString = "F5", ForeColor = Theme.FG };
            var miCopy = new ToolStripMenuItem("복사", null, (s, e) => DoCopy()) { ShortcutKeyDisplayString = "Ctrl+C", ForeColor = Theme.FG };
            var miDel = new ToolStripMenuItem("삭제", null, (s, e) => DoDelete()) { ShortcutKeyDisplayString = "Del", ForeColor = Theme.DANGER_FG };
            menu.Items.AddRange(new ToolStripItem[] { miCopy, miDel, new ToolStripSeparator(), miRefresh });
            menu.Opening += (s, e) => { bool any = list.SelectedItems.Count > 0; miCopy.Enabled = any; miDel.Enabled = any; };

            // 3) 목록 (1px 테두리 패널 안에 꽉 채움)
            var listHost = new Panel { Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(1), BackColor = Theme.BORDER, Margin = new Padding(0, 0, 0, section) };
            list = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.PANEL, ForeColor = Theme.FG,
                SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false, HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerDrawFixed, Font = Font
            };
            list.ItemHeight = Math.Min(255, Font.Height + S(12));
            list.DrawItem += DrawListItem;
            list.SelectedIndexChanged += (s, e) => UpdateStatus();
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) { DoDelete(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.C) { DoCopy(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.A) { for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true); e.Handled = true; }
            };
            list.DoubleClick += (s, e) => { if (list.SelectedItem != null) OpenInExplorer(list.SelectedItem.ToString()); };
            list.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int i = list.IndexFromPoint(e.Location);
                if (i >= 0 && !list.GetSelected(i)) { list.ClearSelected(); list.SetSelected(i, true); }
            };
            list.ContextMenuStrip = menu;
            listHost.Controls.Add(list);
            root.Controls.Add(listHost, 0, 1);

            // 4) 새 항목 추가: 제목 + [입력란 | 폴더 선택 | 파일 선택 | 추가]
            var addGroup = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0), BackColor = Theme.BG };
            addGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            addGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inputCaption = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.UiFont(11f), ForeColor = Theme.SECONDARY, Margin = new Padding(0, 0, 0, gap), UseMnemonic = false, Text = "새 항목 추가" };
            var inRow = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, ColumnCount = 4, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0), BackColor = Theme.BG };
            inRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            input = new InputBox { Height = H, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, gap, 0), Font = Font, PadX = S(8) };
            input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { DoAdd(); e.Handled = true; e.SuppressKeyPress = true; } };
            btnFolder = MakeButton("폴더 선택…"); btnFolder.Margin = new Padding(0, 0, gap, 0); btnFolder.Click += (s, e) => BrowseFolder();
            btnFile = MakeButton("파일 선택…"); btnFile.Margin = new Padding(0, 0, gap, 0); btnFile.Click += (s, e) => BrowseFile();
            btnAdd = MakeButton("추가", S(20)); btnAdd.Accent = true; btnAdd.Margin = new Padding(0); btnAdd.Click += (s, e) => DoAdd();
            UniformWidth(btnFolder, btnFile);
            inRow.Controls.Add(input, 0, 0);
            inRow.Controls.Add(btnFolder, 1, 0);
            inRow.Controls.Add(btnFile, 2, 0);
            inRow.Controls.Add(btnAdd, 3, 0);
            var capRow = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0), BackColor = Theme.BG };
            capRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            capRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            capRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            capRow.Controls.Add(inputCaption, 0, 0);
            capRow.Controls.Add(status, 1, 0);
            addGroup.Controls.Add(capRow, 0, 0);
            addGroup.Controls.Add(inRow, 0, 1);
            root.Controls.Add(addGroup, 0, 2);

            // 탭 순서: 탭 → 입력 → 찾아보기/추가 → 목록 동작 → 목록
            tabs.TabIndex = 0; addGroup.TabIndex = 1; listHost.TabIndex = 2;
            input.TabIndex = 0; btnFolder.TabIndex = 1; btnFile.TabIndex = 2; btnAdd.TabIndex = 3;

            // 최소 크기: 도구줄·입력줄 버튼이 잘리지 않는 폭 + 목록이 몇 줄 보이는 높이
            int minW = root.Padding.Horizontal + Math.Max(
                S(160),
                S(200) + gap + btnFolder.Width + gap + btnFile.Width + gap + btnAdd.Width);
            int minH = root.Padding.Vertical + H * 2 + inputCaption.PreferredHeight + gap * 3 + section * 2 + list.ItemHeight * 4 + S(2);
            MinimumSize = SizeFromClientSize(new Size(minW, minH));
            Size = new Size(Math.Max(S(740), MinimumSize.Width), Math.Max(S(570), MinimumSize.Height)); // 기본 창 크기 740x570 (테두리 포함)

            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { Reload(); e.Handled = true; } };

            Shown += (s, e) => SwitchTo(0);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4); } catch { }
            try
            {
                if (hIconSmall != IntPtr.Zero) SendMessage(Handle, WM_SETICON, (IntPtr)0, hIconSmall);   // ICON_SMALL (타이틀바)
                if (hIconLarge != IntPtr.Zero) SendMessage(Handle, WM_SETICON, (IntPtr)1, hIconLarge);   // ICON_BIG (Alt+Tab/작업표시줄)
            }
            catch { }
            // 관리자 창 드래그&드롭: OLE 드롭 대상 해제 + WM_DROPFILES 관련 메시지를 UIPI 필터에서 허용 + WS_EX_ACCEPTFILES
            try
            {
                RevokeDragDrop(Handle);
                ChangeWindowMessageFilterEx(Handle, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
                ChangeWindowMessageFilterEx(Handle, 0x004A, MSGFLT_ALLOW, IntPtr.Zero); // WM_COPYDATA
                ChangeWindowMessageFilterEx(Handle, 0x0049, MSGFLT_ALLOW, IntPtr.Zero); // WM_COPYGLOBALDATA
                DragAcceptFiles(Handle, true);
            }
            catch { }
        }

        // WinForms는 UpdateStyles 때 확장 스타일을 CreateParams로 다시 덮어쓰므로 WS_EX_ACCEPTFILES를 여기서 고정
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000010; // WS_EX_ACCEPTFILES
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DROPFILES)
            {
                string[] files = GetDroppedFiles(m.WParam);
                m.Result = IntPtr.Zero;
                BeginInvoke(new Action(() => OnFilesDropped(files)));
                return;
            }
            base.WndProc(ref m);
        }

        static string[] GetDroppedFiles(IntPtr hDrop)
        {
            var result = new List<string>();
            try
            {
                uint n = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < n; i++)
                {
                    uint len = DragQueryFile(hDrop, i, null, 0);
                    var sb = new StringBuilder((int)len + 1);
                    if (DragQueryFile(hDrop, i, sb, (uint)sb.Capacity) > 0) result.Add(sb.ToString());
                }
            }
            finally { DragFinish(hDrop); }
            return result.ToArray();
        }

        // 드롭 처리: 1개면 입력란에 채움, 여러 개면 모두 추가할지 물어봄 (아니요 → 첫 번째만 입력란에)
        void OnFilesDropped(string[] files)
        {
            if (files == null || files.Length == 0) return;
            Activate();
            var c = cats[current];
            var values = (current == 2
                    ? files.Select(f => Path.GetExtension(f)).Where(x => !string.IsNullOrEmpty(x))
                    : files.AsEnumerable())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (values.Length == 0)
            {
                MessageBox.Show(this, "확장자가 있는 파일을 드롭하십시오.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (values.Length > 1)
            {
                var existing = new HashSet<string>(list.Items.Cast<object>().Select(x => x.ToString()), StringComparer.OrdinalIgnoreCase);
                var fresh = values.Where(v => !existing.Contains(v)).ToArray();
                if (fresh.Length > 0)
                {
                    var preview = string.Join("\n", fresh.Take(15)) + (fresh.Length > 15 ? "\n… 외 " + (fresh.Length - 15) + "개" : "");
                    var skipped = values.Length - fresh.Length;
                    var msg = "드롭한 항목 " + values.Length + "개 중 새 항목 " + fresh.Length + "개를 " + c.Title + " 제외 목록에 모두 추가할까요?"
                        + (skipped > 0 ? "\n(이미 있는 " + skipped + "개는 건너뜀)" : "")
                        + "\n\n" + preview + "\n\n[예] 모두 추가    [아니요] 첫 번째만 입력란에 넣기";
                    var r = MessageBox.Show(this, msg, "여러 항목 추가", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (r == DialogResult.Cancel) return;
                    if (r == DialogResult.Yes)
                    {
                        Cursor = Cursors.WaitCursor;
                        try { Defender.Add(c.Prop, fresh); }
                        catch (Exception ex) { ShowError("제외 항목을 추가하지 못했습니다.", ex); }
                        finally { Cursor = Cursors.Default; }
                        Reload();
                        list.ClearSelected();
                        for (int i = 0; i < list.Items.Count; i++)
                            if (fresh.Contains(list.Items[i].ToString(), StringComparer.OrdinalIgnoreCase)) list.SetSelected(i, true);
                        return;
                    }
                }
            }
            input.Box.Text = values[0];
            input.Box.Focus();
            input.Box.SelectionStart = input.Box.Text.Length;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try { SetWindowTheme(list.Handle, "DarkMode_Explorer", null); } catch { }
        }

        FlatBtn MakeButton(string text, int padX = -1)
        {
            if (padX < 0) padX = S(16);
            var b = new FlatBtn { Text = text, Font = Font, ForeColor = Theme.FG, BackColor = Theme.CTRL, Margin = new Padding(0, 0, S(8), 0) };
            int w = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, H), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + padX * 2;
            b.Size = new Size(w, H);
            b.Radius = S(6);
            return b;
        }

        // 같은 그룹 버튼 폭을 가장 긴 텍스트 기준으로 맞춤 (텍스트 잘림 없음)
        static void UniformWidth(params FlatBtn[] buttons)
        {
            int w = buttons.Max(b => b.Width);
            foreach (var b in buttons) b.Width = w;
        }

        void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color back = sel ? Theme.SELECTED : (e.Index % 2 == 0 ? Theme.PANEL : Theme.ALT);
            using (var bg = new SolidBrush(back)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (sel)
            {
                int bar = Math.Max(3, S(4));
                using (var mark = new SolidBrush(Theme.EMPHASIS))
                    e.Graphics.FillRectangle(mark, e.Bounds.X, e.Bounds.Y, bar, e.Bounds.Height);
            }
            int pad = S(12);
            var r = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - pad - S(8)), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), list.Font, r, sel ? Color.White : Theme.FG,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        void SwitchTo(int idx)
        {
            current = idx;
            for (int i = 0; i < tabButtons.Count; i++) { tabButtons[i].Selected = i == idx; tabButtons[i].Invalidate(); }
            var c = cats[idx];
            btnFolder.Visible = c.Folder;
            btnFile.Visible = c.File;
            input.Box.Text = "";
            input.Hint = c.Hint;
            inputCaption.Text = c.Title + " 제외 항목 추가";
            Reload();
        }

        void Reload()
        {
            var c = cats[current];
            Cursor = Cursors.WaitCursor;
            try
            {
                var items = Defender.Get(c.Prop).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                list.BeginUpdate();
                list.Items.Clear();
                foreach (var it in items) list.Items.Add(it);
                list.EndUpdate();
                int w = 0;
                foreach (var it in items) w = Math.Max(w, TextRenderer.MeasureText(it, list.Font).Width + S(24));
                list.HorizontalExtent = w;
            }
            catch (Exception ex) { ShowError("목록을 불러오지 못했습니다.", ex); }
            finally { Cursor = Cursors.Default; }
            UpdateStatus();
        }

        void UpdateStatus()
        {
            status.Text = cats[current].Title + ": " + list.Items.Count + "개" + (list.SelectedItems.Count > 0 ? "  ·  선택 " + list.SelectedItems.Count + "개" : "");
            btnDel.Enabled = list.SelectedItems.Count > 0;
        }

        void BrowseFolder()
        {
            using (var d = new FolderBrowserDialog { Description = "제외할 폴더를 선택하세요", ShowNewFolderButton = false })
                if (d.ShowDialog(this) == DialogResult.OK) input.Box.Text = d.SelectedPath;
        }

        void BrowseFile()
        {
            using (var d = new OpenFileDialog { Filter = cats[current].FileFilter, Title = "제외할 파일 선택", CheckFileExists = true })
                if (d.ShowDialog(this) == DialogResult.OK) input.Box.Text = d.FileName;
        }

        // 더블클릭: 파일이면 탐색기에서 선택된 상태로, 폴더면 그 폴더를 엶. 없는 경로는 가장 가까운 상위 폴더를 엶.
        void OpenInExplorer(string item)
        {
            if (current == 2) return; // 확장자는 위치가 없음
            string path = Environment.ExpandEnvironmentVariables(item.Trim().Trim('"'));
            int wc = path.IndexOfAny(new[] { '*', '?' });
            if (wc >= 0) path = path.Substring(0, wc);
            if (!path.Contains("\\")) { status.Text = "경로가 아닌 항목이라 열 수 없습니다"; return; }
            try
            {
                if (File.Exists(path)) { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); return; }
                string dir = path.TrimEnd('\\');
                while (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) dir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir)) { status.Text = "위치를 찾을 수 없습니다"; return; }
                if (dir.Length == 2 && dir[1] == ':') dir += "\\";
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                if (!string.Equals(dir.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    status.Text = "경로가 없어 상위 폴더를 열었습니다";
            }
            catch (Exception ex) { ShowError("탐색기를 열지 못했습니다.", ex); }
        }

        void DoAdd()
        {
            var c = cats[current];
            var v = input.Box.Text.Trim().Trim('"').Trim();
            if (v.Length == 0) { input.Box.Focus(); return; }
            if (current == 2 && !v.StartsWith(".") && !v.Contains("\\")) v = "." + v;
            if (list.Items.Cast<object>().Any(x => string.Equals(x.ToString(), v, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "이미 목록에 있습니다:\n" + v, "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (current != 2 && v.Contains("\\") && !v.Contains("*") && !v.Contains("%") && !File.Exists(v) && !Directory.Exists(v))
            {
                if (MessageBox.Show(this, "이 경로가 존재하지 않습니다. 그래도 추가할까요?\n\n" + v, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            }
            Cursor = Cursors.WaitCursor;
            try { Defender.Add(c.Prop, new[] { v }); input.Box.Text = ""; }
            catch (Exception ex) { ShowError("제외 항목을 추가하지 못했습니다.", ex); }
            finally { Cursor = Cursors.Default; }
            Reload();
            int i = list.Items.Cast<object>().ToList().FindIndex(x => string.Equals(x.ToString(), v, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { list.ClearSelected(); list.SelectedIndex = i; }
        }

        void DoDelete()
        {
            var c = cats[current];
            var sel = list.SelectedItems.Cast<object>().Select(x => x.ToString()).ToArray();
            if (sel.Length == 0) { MessageBox.Show(this, "삭제할 항목을 선택하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var preview = string.Join("\n", sel.Take(15)) + (sel.Length > 15 ? "\n… 외 " + (sel.Length - 15) + "개" : "");
            if (MessageBox.Show(this, c.Title + " 제외 항목 " + sel.Length + "개를 삭제할까요?\n\n" + preview, "삭제 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Cursor = Cursors.WaitCursor;
            try { Defender.Remove(c.Prop, sel); }
            catch (Exception ex) { ShowError("제외 항목을 삭제하지 못했습니다.", ex); }
            finally { Cursor = Cursors.Default; }
            Reload();
        }

        void DoCopy()
        {
            var sel = list.SelectedItems.Cast<object>().Select(x => x.ToString()).ToArray();
            if (sel.Length == 0) sel = list.Items.Cast<object>().Select(x => x.ToString()).ToArray();
            if (sel.Length == 0) return;
            try { Clipboard.SetText(string.Join("\r\n", sel)); status.Text = sel.Length + "개 복사됨"; }
            catch (Exception ex) { ShowError("클립보드에 복사하지 못했습니다.", ex); }
        }

        void ShowError(string msg, Exception ex)
        {
            MessageBox.Show(this, msg + "\n\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--selftest") return SelfTest(args[1]);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        // 백엔드 검증용: 임시 항목만 추가/삭제하고 전체 목록이 그대로인지 비교
        static int SelfTest(string log)
        {
            var sb = new StringBuilder();
            bool ok = true;
            var tests = new[] {
                new[] { "ExclusionPath", @"C:\__excl_test_tmp" },
                new[] { "ExclusionProcess", @"C:\__excl_test_tmp\__excl_test.exe" },
                new[] { "ExclusionExtension", ".__excltest" },
            };
            try
            {
                foreach (var t in tests)
                {
                    string prop = t[0], val = t[1];
                    var before = Defender.Get(prop);
                    sb.AppendLine(prop + " before=" + before.Length);
                    if (before.Contains(val, StringComparer.OrdinalIgnoreCase)) { sb.AppendLine("  test value already present, skip"); ok = false; continue; }
                    Defender.Add(prop, new[] { val });
                    var mid = Defender.Get(prop);
                    bool added = mid.Contains(val, StringComparer.OrdinalIgnoreCase);
                    sb.AppendLine("  after add=" + mid.Length + " present=" + added);
                    Defender.Remove(prop, new[] { val });
                    var after = Defender.Get(prop);
                    bool gone = !after.Contains(val, StringComparer.OrdinalIgnoreCase);
                    bool same = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase).SetEquals(after);
                    sb.AppendLine("  after remove=" + after.Length + " removed=" + gone + " identicalToBefore=" + same);
                    if (!(added && gone && same)) ok = false;
                }
            }
            catch (Exception ex) { sb.AppendLine("ERROR: " + ex); ok = false; }
            sb.AppendLine(ok ? "RESULT=PASS" : "RESULT=FAIL");
            File.WriteAllText(log, sb.ToString(), new UTF8Encoding(false));
            return ok ? 0 : 1;
        }
    }
}
