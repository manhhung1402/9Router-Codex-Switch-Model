using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NineRouter
{
    // csc cua .NET Framework khong chap nhan khai bao P/Invoke o cap namespace,
    // nen dat trong mot static class.
    internal static class NativeWin
    {
        static Icon appIcon;
        // Icon trich tu EXE moi lan se tao doi tuong GDI moi; dung chung mot lan.
        public static Icon AppIcon
        {
            get {
                if (appIcon == null) {
                    try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                    catch { appIcon = null; }
                }
                return appIcon;
            }
        }

        [DllImport("user32.dll")]
        internal static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int L, T, R, B; }

        // Chup that vung cua so tren MAN HINH that (CopyFromScreen).
        // DrawToBitmap khong ve duoc RowListView vi no tu ve trong OnPaint.
        public static void CaptureScreen(Bitmap bmp, IntPtr hWnd)
        {
            RECT r;
            if (!GetWindowRect(hWnd, out r)) return;
            int w = r.R - r.L, h = r.B - r.T;
            if (w <= 0 || h <= 0) return;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(r.L, r.T, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
            }
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

    // ==================================================================
    // Bang mau sang / toi
    // ==================================================================
    // ==================================================================
    // Ve dong danh sach - DUNG CHUNG cho danh sach mo hinh va danh sach
    // ban sao luu, de hai noi giong het nhau.
    // ==================================================================
    internal static class RowPainter
    {
        public const int RowH = 40;

        public static void WriteHighlight(Graphics g, string text, int[] pos, float x, float y, float maxW,
            Font f, Font fb, Brush br, Brush brb)
        {
            if (text == null) return;
            int len = text.Length;
            if (len == 0) return;
            // Khong con cho thi van ve vai chu dau, thay vi bo trang ca dong.
            if (maxW < 24f) {
                g.DrawString(len > 8 ? text.Substring(0, 8) : text, f, br, x, y);
                return;
            }
            HashSet<int> set = new HashSet<int>();
            if (pos != null) foreach (int p in pos) if (p >= 0 && p < len) set.Add(p);
            float cx = x;
            int i = 0;
            while (i < len) {
                bool hit = set.Contains(i);
                int j = i;
                while (j < len && set.Contains(j) == hit) j++;
                string seg = text.Substring(i, j - i);
                Font ff = hit ? fb : f;
                Brush bb = hit ? brb : br;
                float w = g.MeasureString(seg, ff).Width;
                if ((cx - x) + w > maxW) {
                    // Cat bang DO RONG THAT. Truoc day lay so pixel lam so ky tu
                    // nen cua so hep se ve tran ra ngoai dong.
                    float room = maxW - (cx - x);
                    int fit = FitChars(g, seg, ff, room);
                    if (fit > 0) {
                        string head = seg.Substring(0, fit);
                        g.DrawString(head, ff, bb, cx, y);
                        float used = g.MeasureString(head, ff).Width;
                        if (room - used >= 12) g.DrawString("...", ff, br, cx + used, y);
                    }
                    return;
                }
                g.DrawString(seg, ff, bb, cx, y);
                cx += w;
                i = j;
            }
        }

        // So ky tu ve duoc trong chieu rong con lai (do bang pixel that).
        static int FitChars(Graphics g, string s, Font f, float maxW)
        {
            if (maxW <= 0 || s.Length == 0) return 0;
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (g.MeasureString(s.Substring(0, mid), f).Width <= maxW) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public static void Draw(Graphics g, Rectangle r, Palette P, string title, int[] pos,
            string detail, bool selected, bool focused, bool accent, bool current, string tagText)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.FillRectangle(P.B[selected ? "Sel" : (accent ? "RowA" : "Row")], r);
            float x = r.X + 12;
            float maxW = r.Width - 30;
            if (accent)
                g.FillRectangle(P.B["Acc"], new Rectangle(r.X + 3, r.Y + 7, 3, r.Height - 14));
            if (selected) { x = r.X + 18; maxW -= 6; }
            if (accent && !string.IsNullOrEmpty(tagText)) {
                // Do rong tag do bang thuc do, khong hard-code 50 px nen ten model
                // dai bi cat som hon can thiet.
                float tw = g.MeasureString(tagText, P.FSmallB).Width;
                g.DrawString(tagText, P.FSmallB, P.B["Acc"], new PointF(r.Right - 12 - tw, r.Y + 6));
                maxW -= tw + 18;
            }
            if (current) {
                using (SolidBrush br = new SolidBrush(P.Cur)) g.FillEllipse(br, r.X + 12, r.Y + 14, 8, 8);
                x = r.X + 27; maxW -= 15;
            }
            WriteHighlight(g, title, pos, x, r.Y + 5, maxW, P.FId, P.FIdB, P.B["Fg"], P.B["Hi"]);
            g.DrawString(detail, P.FSmall, P.B["Dim"], new PointF(r.X + 12, r.Y + 24));
            // Dong dang chon: thanh ben trai. Truoc day chi dong combo moi co
            // thanh accent nen dong chon khot tren nen trang rat mo.
            if (selected)
                g.FillRectangle(P.B["Acc"], new Rectangle(r.X + 1, r.Y + 2, 3, r.Height - 4));
            // Vien focus: nguoi chi dung ban phim can biet con tro o dong nao.
            if (focused)
                using (Pen p = new Pen(P.B["Acc"]))
                    g.DrawRectangle(p, new Rectangle(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3));
        }
    }
    // ==================================================================
    // Panel co vien + bo goc bo tron + bong do nhe.
    // Dung cho moi "hop" trong giao dien de nguoi dung nhin ra
    // dau la vung nhap, dau la danh sach, dau la nut.
    // ==================================================================
    internal sealed class CardPanel : Panel
    {
        public Color Border = Color.FromArgb(200, 204, 212);
        public Color Fill = Color.White;
        public int Radius = 8;
        public bool Shadow = true;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Opaque, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = ClientRectangle;
            r.Width -= 1; r.Height -= 1;
            if (r.Width < 8 || r.Height < 8) return;

            using (GraphicsPath path = Round(r, Radius))
            {
                // Bong do: ve truoc, lech xuong phai 1px, nen no khong de
                // lop trong cua card len tren bong.
                if (Shadow) {
                    GraphicsPath sp = Shift(path, 1, 2);
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
                        g.FillPath(sh, sp);
                    sp.Dispose();
                }
                using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, path);
                using (Pen p = new Pen(Border)) g.DrawPath(p, path);
            }
        }

        // Dich ca duong cong de ve bong, giu nguyen hinh dang bo goc bo tron.
        public static GraphicsPath Shift(GraphicsPath p, int dx, int dy)
        {
            GraphicsPath q = new GraphicsPath();
            q.FillMode = p.FillMode;
            foreach (PointF pt in p.PathPoints)
                q.AddLine(pt.X + dx, pt.Y + dy, pt.X + dx, pt.Y + dy);
            return q;
        }

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 1) { p.AddRectangle(r); return p; }
            int d = rad * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
    // Mot bo font/brush duy nhat cho ca chuong trinh. Truoc day moi cua so tu tao
    // bo rieng nen mo/dong nhieu lan se lam ro ri GDI; het GDI thi Windows KHONG
    // ve duoc chu nua (nhin nhu chu bien mat).
    internal sealed class Palette
    {
        static Palette shared;
        public static Palette Shared { get { if (shared == null) shared = new Palette(); return shared; } }
        public bool Dark;
        public Color Form, Row, RowA, Sel, Fg, Dim, Acc, Hi, Cur, Box, Line;
        public Dictionary<string, SolidBrush> B = new Dictionary<string, SolidBrush>();
        public Font FId, FIdB, FSmall, FSmallB, FHead, FUi, FUiB;

        public Palette()
        {
            Color wc = SystemColors.Window;
            double lum = 0.299 * wc.R + 0.587 * wc.G + 0.114 * wc.B;
            Dark = lum < 140;
            if (Dark)
            {
                Form = Color.FromArgb(24, 24, 24);   Row  = Color.FromArgb(30, 30, 30);
                RowA = Color.FromArgb(36, 36, 36);   Sel  = Color.FromArgb(43, 74, 112);
                Fg   = Color.FromArgb(232, 232, 232); Dim = Color.FromArgb(140, 140, 140);
                Acc  = Color.FromArgb(88, 166, 255); Hi  = Color.FromArgb(255, 214, 102);
                Cur  = Color.FromArgb(112, 214, 140); Box = Color.FromArgb(44, 44, 44);
                Line = Color.FromArgb(72, 76, 84);
            }
            else
            {
                Form = Color.FromArgb(252, 252, 252); Row  = Color.White;
                RowA = Color.FromArgb(240, 244, 250); Sel  = Color.FromArgb(205, 228, 255);
                Fg   = Color.FromArgb(26, 26, 26);    Dim  = Color.FromArgb(112, 112, 112);
                Acc  = Color.FromArgb(11, 98, 214);  Hi  = Color.FromArgb(184, 88, 0);
                Cur  = Color.FromArgb(20, 138, 68);  Box = Color.White;
                Line = Color.FromArgb(206, 210, 218);
            }
            B["Fg"] = new SolidBrush(Fg);   B["Dim"] = new SolidBrush(Dim);
            B["Acc"] = new SolidBrush(Acc); B["Hi"] = new SolidBrush(Hi);
            B["Cur"] = new SolidBrush(Cur); B["Sel"] = new SolidBrush(Sel);
            B["Row"] = new SolidBrush(Row); B["RowA"] = new SolidBrush(RowA);
            FId    = new Font("Segoe UI", 10f, FontStyle.Regular);
            FIdB   = new Font("Segoe UI", 10f, FontStyle.Bold);
            FSmall = new Font("Segoe UI", 8f, FontStyle.Regular);
            FSmallB= new Font("Segoe UI", 8f, FontStyle.Bold);
            FHead  = new Font("Segoe UI", 13f, FontStyle.Bold);
            FUi    = new Font("Segoe UI", 9f, FontStyle.Regular);
            FUiB   = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        public void Dispose()
        {
            foreach (SolidBrush b in B.Values) b.Dispose();
            FId.Dispose(); FIdB.Dispose(); FSmall.Dispose(); FSmallB.Dispose();
            FHead.Dispose(); FUi.Dispose(); FUiB.Dispose();
        }
    }
    // ==================================================================
    // Cua so quan ly ban sao luu: xem / khoi phuc / xoa / mo thu muc
    // Danh sach ve bang ListBox tu ve. KHONG dung ListView owner-draw:
    // ListView bo qua su kien ve o lan ve dau tien nen chu chi hien
    // ra sau khi duoc chon lai.
    // ==================================================================
    // ==================================================================
    // Cua so quan ly ban sao luu: dung cung kieu dong voi danh sach mo hinh,
    // van chon duoc nhieu ban sao de khoi phuc hoac xoa.
    // ==================================================================
    internal sealed class BackupForm : Form
    {
        readonly Palette P = Palette.Shared;
        TextBox filter;
        RowListView lb;
        CardPanel filterCard, listCard;
        string cfgPath;
        List<BackupInfo> all = new List<BackupInfo>();
        List<BackupInfo> rows = new List<BackupInfo>();
        List<string> selected = new List<string>();
        Button btnRestore, btnDelete;

        const int RowH = RowPainter.RowH;

        public BackupForm(string cfgPath)
        {
            this.cfgPath = cfgPath;
            Text = "9Router · Bản sao lưu";
            ClientSize = new Size(840, 560);
            MinimumSize = new Size(660, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = P.Form;
            Font = P.FUi;
            try { Icon = NativeWin.AppIcon; } catch { }
            KeyPreview = true;

            Label tip = new Label { AutoSize = true, ForeColor = P.Dim, Location = new Point(16, 10) };
            tip.Text = "Mỗi lần đổi mô hình, tool tự tạo một bản sao config.toml trước khi ghi.";


            lb = new RowListView(delegate (Graphics g, Rectangle r, int i, bool isSel, bool isFoc) {
                if (i < 0 || i >= rows.Count) return;
                DrawRow(g, r, rows[i], isSel, isFoc);
            }, RowH);
            lb.EmptyText = "Chưa có bản sao nào.\nMỗi lần đổi mô hình, tool tự tạo một bản sao config.toml trước khi ghi.";
            lb.Dock = DockStyle.Fill;
            lb.BackColor = P.Box;
            lb.SelectionChanged += delegate { UpdateButtons(); };

            filterCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Dock = DockStyle.Top, Height = 34, Padding = new Padding(1) };
            filter = new TextBox { Dock = DockStyle.Fill, Font = P.FUi, BorderStyle = BorderStyle.None,
                BackColor = P.Box, ForeColor = P.Fg, Margin = new Padding(10, 0, 10, 0) };
            filterCard.Controls.Add(filter);
            listCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Dock = DockStyle.Fill, Padding = new Padding(1) };
            listCard.Controls.Add(lb);
            filter.TextChanged += delegate { UpdateRows(); };
            filter.KeyDown += delegate (object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; lb.Focus(); }
            };
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = P.Form, Padding = new Padding(16, 70, 16, 4) };
            body.Controls.Add(listCard);
            body.Controls.Add(filterCard);

            Label status = new Label { Dock = DockStyle.Fill, ForeColor = P.Dim,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            btnRestore = Mk("Khôi phục", P.Acc, Color.White, true);
            btnDelete  = Mk("Xoá bản sao", P.RowA, P.Fg, false);
            Button btnFolder  = Mk("Mở thư mục", P.RowA, P.Fg, false);
            Button btnRefresh = Mk("Làm mới", P.RowA, P.Fg, false);
            Button btnClose   = Mk("Đóng", P.RowA, P.Fg, false);
            FlowLayoutPanel box = new FlowLayoutPanel {
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(16, 8, 16, 8), BackColor = P.Form };
            box.Controls.AddRange(new Control[] { btnRestore, btnDelete, btnFolder, btnRefresh, btnClose });
            status.Dock = DockStyle.Top;
            status.Height = 26;
            status.Padding = new Padding(16, 0, 16, 0);
            Panel footer = new Panel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = P.Form };
            footer.Controls.Add(box);
            footer.Controls.Add(status);

            Controls.Add(body); Controls.Add(footer); Controls.Add(tip);
            tip.BringToFront();

            btnRestore.Click += delegate { DoRestore(); };
            btnDelete.Click += delegate { DoDelete(); };
            btnFolder.Click += delegate { OpenFolder(); };
            btnRefresh.Click += delegate { Reload(); };
            btnClose.Click += delegate { Close(); };

            // Nap du lieu o Load, tuc la TRUOC khi cua so duoc ve lan dau.
            // Neu nap o Shown thi danh sach rong, va mot so may se bo qua su kien
            // ve o frame dau tien => man hinh trong, phai bam chuot moi hien chu.
            Load += delegate { Reload(); lb.Update(); };
            Shown += delegate { lb.Update(); };
        }

        // Dong ve dung kieu voi danh sach mo hinh: dong lon ten, dong nho thong tin.
        void DrawRow(Graphics g, Rectangle r, BackupInfo b, bool isSel, bool isFoc)
        {
            string title = (b.Model == null) ? "(không đọc được)" : b.Model;
            string size = (b.Size >= 1024) ? (b.Size / 1024.0).ToString("0.#") + " KB" : b.Size + " B";
            string detail = b.Time.ToString("dd/MM/yyyy HH:mm:ss") + "   ·   " + size + "   ·   " + b.FileName;
            int[] pos = MatchPos(title + " " + b.FileName, filter.Text);
            RowPainter.Draw(g, r, P, title, pos, detail, isSel, isFoc, false, false, "");
        }

        // So khop mo tren "ten mo hinh + ten tep" de tim ban sao.
        static int[] MatchPos(string text, string q)
        {
            string s = (q == null) ? "" : q.Trim().ToLowerInvariant();
            if (s.Length == 0) return new int[0];
            string t = text.ToLowerInvariant();
            List<int> hits = new List<int>();
            int i = 0;
            foreach (char c in s) {
                int idx = t.IndexOf(c, i);
                if (idx < 0) return new int[0];
                hits.Add(idx);
                i = idx + 1;
            }
            return hits.ToArray();
        }

        static bool TestSubsequence(string text, string q)
        {
            return MatchPos(text ?? "", q).Length > 0 || (q != null && q.Trim().Length == 0);
        }

        Button Mk(string text, Color back, Color fore, bool primary)
        {
            Button b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 4, 12, 4), FlatStyle = FlatStyle.Flat,
                BackColor = back, ForeColor = fore, UseVisualStyleBackColor = false,
                MinimumSize = new Size(0, 32) };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.12f);
                        // FlatStyle.Flat KHONG tu mo nen khi nut bi disable, nen "Khoi phuc"
            // van nhu bam duoc. Tu lam mau cho ro rang.
            b.EnabledChanged += delegate {
                bool on = b.Enabled;
                b.BackColor = on ? back : ControlPaint.Light(back, 0.55f);
                b.ForeColor = on ? fore : ControlPaint.Light(fore, 0.45f);
            };            return b;
        }

        void UpdateButtons()
        {
            selected = new List<string>();
            foreach (int i in lb.GetSelected())
                if (i >= 0 && i < rows.Count) selected.Add(rows[i].Path);
            selected.Sort(delegate (string a, string b) {
                return File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b)); });
            btnRestore.Enabled = selected.Count > 0;
            btnDelete.Enabled = selected.Count > 0;
            btnRestore.Text = (selected.Count <= 1) ? "Khôi phục" : "Khôi phục (" + selected.Count + ")";
            btnDelete.Text = (selected.Count <= 1) ? "Xoá bản sao" : "Xoá " + selected.Count + " bản sao";
        }

        public void ScrollList(int delta)
        {
            lb.ScrollBy(delta);
            Application.DoEvents();
        }

        public void SetFilterForTest(string q)
        {
            filter.Text = q ?? "";
            Application.DoEvents();
        }

        public int RowCount { get { return rows.Count; } }
        // Vung danh sach tren MAN HINH that. Luu y: lb.Left/lb.Top la toa do
        // trong Panel cha, dung chung voi toa do cua Form se do sai vung,
        // do la ly do bo tu kiem tra cu truoc bao gio PASS gia.
        public Rectangle ListScreenRect
        {
            get
            {
                Point p = lb.PointToScreen(new Point(0, 0));
                return new Rectangle(p.X, p.Y, lb.Width, lb.Height);
            }
        }
        public int TotalCount { get { return all.Count; } }
        public int RowHeight { get { return RowH; } }
        public Color BoxColor { get { return P.Box; } }

        public void Reload()
        {
            if (lb == null) return;
            List<string> keep = new List<string>(selected);
            all = Cfg.ListBackups(cfgPath);
            UpdateRows();
            for (int i = 0; i < rows.Count; i++)
                if (keep.Contains(rows[i].Path)) lb.SetSelected(i, true);
            UpdateButtons();
        }

        void UpdateRows()
        {
            string q = filter.Text.Trim();
            rows = new List<BackupInfo>();
            foreach (BackupInfo b in all)
                if (q.Length == 0 || TestSubsequence(b.Model, q) || TestSubsequence(b.FileName, q)) rows.Add(b);
            lb.SetItems(rows.Count, rows.Count > 0 ? 0 : -1);
            if (rows.Count == 0 && q.Length > 0)
                lb.EmptyText = "Không có bản sao nào khớp với từ khóa.";
            UpdateButtons();
            // Ve lai ngay (dong bo) de chu co chieu hien dau tien,
            // khong phai doi den lan ve tiep theo cua Windows moi co chu.
            lb.Update();
        }

        // Kich thuoc thuc te cua ListBox sau khi form da hien - dung de phat hien loi bo cuc.
        public int ListW { get { return lb.Width; } }
        public int ListH { get { return lb.Height; } }
        public int ListX { get { return lb.Left; } }
        public int ListY { get { return lb.Top; } }
        public void SimulateClick(int index)
        {
            lb.ClickRow(index);
            Application.DoEvents();
        }

        // Do "so mu" de kiem tra danh sach da ve chu that hay chua.
        // Van dung ham ve cua chinh control, nen phan ánh dung hinh anh that.
        public int InkInList()
        {
            if (lb.Width <= 0 || lb.Height <= 0) return 0;
            using (Bitmap bmp = new Bitmap(lb.Width, lb.Height))
            {
                lb.DrawToBitmap(bmp, new Rectangle(0, 0, lb.Width, lb.Height));
                int ink = 0;
                for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (Math.Abs(c.R - P.Box.R) + Math.Abs(c.G - P.Box.G) + Math.Abs(c.B - P.Box.B) > 60) ink++;
                }
                return ink;
            }
        }

        public void SelectFirstRows(int n)
        {
            for (int i = 0; i < n && i < lb.ItemCount; i++) lb.SetSelected(i, true);
            if (lb.ItemCount > 0) lb.SelectedIndex = 0;
            UpdateButtons();
            Application.DoEvents();
        }

        public void RenderSelf(Bitmap bmp)
        {
            Application.DoEvents();
            DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
        }

        void OpenFolder()
        {
            try {
                string target = (selected.Count > 0) ? selected[selected.Count - 1] : cfgPath;
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + target + "\"");
            } catch (Exception ex) {
                MessageBox.Show("Không mở được thư mục:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void DoDelete()
        {
            if (selected.Count == 0) return;
            string q = (selected.Count == 1)
                ? "Xoá bản sao này?" + Environment.NewLine + Environment.NewLine + Path.GetFileName(selected[0])
                : "Xoá " + selected.Count + " bản sao?";
            q += Environment.NewLine + Environment.NewLine + "Tệp sẽ bị xoá THẬT trên ổ đĩa. Không thể hoàn tác.";
            if (MessageBox.Show(q, "Xác nhận xoá", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int ok = 0, loi = 0;
            foreach (string p in selected)
            {
                try { Cfg.DeleteBackup(p, cfgPath); ok++; } catch { loi++; }
            }
            Reload();
            MessageBox.Show(ok + " bản sao đã xoá." + (loi > 0 ? ("\n" + loi + " bản sao không xoá được.") : ""),
                "9Router", MessageBoxButtons.OK, loi > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        void DoRestore()
        {
            if (selected.Count == 0) return;
            string nhat = selected[selected.Count - 1];
            string q = (selected.Count == 1)
                ? "Khôi phục config.toml từ bản sao này?"
                : "Khôi phục từ " + selected.Count + " bản sao (theo thứ tự cũ → mới, bản mới nhất thắng)?";
            q += Environment.NewLine + Environment.NewLine
                + "  bản dùng cuối : " + Path.GetFileName(nhat) + Environment.NewLine
                + "  mô hình cuối  : " + (Cfg.ModelIn(nhat) ?? "?") + Environment.NewLine + Environment.NewLine
                + "Trạng thái config.toml hiện tại sẽ được lưu thành một bản sao mới trước khi ghi đè.";
            if (MessageBox.Show(q, "Xác nhận khôi phục", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int loi = 0;
            foreach (string p in selected)
            {
                try { Cfg.Restore(cfgPath, p); } catch { loi++; }
            }
            Reload();
            MessageBox.Show("Đã khôi phục config.toml." + (loi > 0 ? ("\n" + loi + " bản sao không áp dụng được.") : "")
                + "\n\nMở lại Codex Desktop App để áp dụng.",
                "9Router", MessageBoxButtons.OK, loi > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }

    internal sealed class ConnectForm : Form
    {
        readonly Palette P = Palette.Shared;
        string cfgPath, routerUrl;
        bool routerOk, providerOk, wiredOk;
        Label s1, s2, s3;
        TextBox boxKey;
        Button btnDetect, btnWire, btnOpenRouter, btnClose;
        Button btnUnwire;
        TextBox boxModel;
        Label lblModel, lblUnwire;
        CheckBox chkAuto;
        CardPanel keyCard, modelCard;

        public ConnectForm(string cfgPath, string routerUrl)
        {
            this.cfgPath = cfgPath;
            this.routerUrl = routerUrl;

            Text = "Kết nối 9Router với Codex";
            ClientSize = new Size(700, 620);
            MinimumSize = new Size(560, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = P.Form;
            Font = P.FUi;
            try { Icon = NativeWin.AppIcon; } catch { }

            Label t1 = new Label { Text = "Ghép 9Router vào Codex chỉ cần làm một lần cho mỗi máy.",
                Font = P.FHead, ForeColor = P.Fg, AutoSize = true, Location = new Point(16, 14) };
            s1 = Step(16, 56, "1.  9Router đang chạy");
            s2 = Step(16, 90, "2.  Codex đã biết tới 9Router");
            s3 = Step(16, 124, "3.  Đã cấu hình xong");

            Label lblKey = new Label { Text = "Khoá API của 9Router", ForeColor = P.Fg,
                AutoSize = true, Location = new Point(16, 162) };
            keyCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 8,
                Location = new Point(16, 180), Size = new Size(668, 30), Padding = new Padding(1) };
            boxKey = new TextBox { Dock = DockStyle.Fill, Font = P.FUi, BorderStyle = BorderStyle.None,
                BackColor = P.Box, ForeColor = P.Fg, Margin = new Padding(8, 0, 8, 0) };
            keyCard.Controls.Add(boxKey);
            btnDetect = new Button { Text = "Tự tìm khoá", AutoSize = true, Location = new Point(16, 214),
                FlatStyle = FlatStyle.Flat, BackColor = P.RowA, ForeColor = P.Fg };
            btnDetect.FlatAppearance.BorderSize = 0;
            Label hint = new Label { AutoSize = true, ForeColor = P.Dim, Location = new Point(112, 221) };
            hint.Text = "Tool tự đọc khoá từ cơ sở dữ liệu của 9Router. Nếu không lấy được, mở trang quản trị 9Router → Keys để sao chép.";

            btnWire = Mk("Kết nối ngay", P.Acc, Color.White, true);
            btnWire.Location = new Point(16, 254);
            btnOpenRouter = Mk("Mở trang quản trị 9Router", P.RowA, P.Fg, false);
            btnOpenRouter.Location = new Point(150, 254);
            btnClose = Mk("Đóng", P.RowA, P.Fg, false);
            btnClose.Location = new Point(360, 254);
            Label foot = new Label { AutoSize = true, ForeColor = P.Dim, Location = new Point(16, 306) };
            foot.Text = "Trước khi ghi, tool tự tạo một bản sao config.toml. Mở lại Codex sau khi kết nối.";

            Label sep = new Label { Text = "Không dùng 9Router nữa?",
                Font = P.FSmallB, ForeColor = P.Dim, AutoSize = true, Location = new Point(16, 348) };
            lblUnwire = new Label { Text = "Xoá khai báo 9Router khỏi config.toml, đưa Codex về dùng nhà cung cấp mặc định.",
                ForeColor = P.Dim, AutoSize = true, Location = new Point(16, 368) };
            chkAuto = new CheckBox { Text = "Để Codex tự chọn model mặc định của nó",
                AutoSize = true, ForeColor = P.Fg, BackColor = P.Form,
                Location = new Point(16, 396), Checked = true };
            chkAuto.CheckedChanged += delegate { SyncAuto(); };
            lblModel = new Label { Text = "Hoặc chỉ định model:", ForeColor = P.Fg,
                AutoSize = true, Location = new Point(16, 424) };
            modelCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 8,
                Location = new Point(16, 442), Size = new Size(300, 30), Padding = new Padding(1) };
            boxModel = new TextBox { Dock = DockStyle.Fill, Font = P.FUi, BorderStyle = BorderStyle.None,
                BackColor = P.Box, ForeColor = P.Fg, Margin = new Padding(8, 0, 8, 0) };
            modelCard.Controls.Add(boxModel);
            btnUnwire = Mk("Ngắt kết nối 9Router", P.RowA, P.Fg, false);
            btnUnwire.Location = new Point(328, 440);
            Label unHint = new Label { Text = "Mọi thay đổi đều có bản sao lưu, mở lại Codex sau khi ngắt.",
                AutoSize = true, ForeColor = P.Dim, Location = new Point(16, 486) };
            btnUnwire.Click += delegate { Unwire(); };
            btnDetect.Click += delegate { Detect(); };
            btnWire.Click += delegate { Wire(); };
            btnOpenRouter.Click += delegate { OpenRouter(); };
            btnClose.Click += delegate { Close(); };

            Controls.AddRange(new Control[] { t1, s1, s2, s3, lblKey, keyCard, boxKey, btnDetect, hint,
                btnWire, btnOpenRouter, btnClose, foot,
                sep, lblUnwire, chkAuto, lblModel, modelCard, boxModel, btnUnwire, unHint });
            Detect();
            SyncAuto();
        }

        Button Mk(string text, Color back, Color fore, bool primary)
        {
            Button b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 5, 14, 5), FlatStyle = FlatStyle.Flat,
                BackColor = back, ForeColor = fore, UseVisualStyleBackColor = false,
                MinimumSize = new Size(0, 32) };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Palette.Shared.Line;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.12f);
            if (primary) {
                b.Font = Palette.Shared.FUiB;
                b.FlatAppearance.BorderColor = ControlPaint.Dark(back, 0.15f);
            }
            // FlatStyle.Flat KHONG tu mo nen khi nut bi disable, nen "Khoi phuc"
            // van nhu bam duoc. Tu lam mau cho ro rang.
            b.EnabledChanged += delegate {
                bool on = b.Enabled;
                b.BackColor = on ? back : ControlPaint.Light(back, 0.55f);
                b.ForeColor = on ? fore : ControlPaint.Light(fore, 0.45f);
            };
            return b;
        }

        Label Step(int x, int y, string text)
        {
            return new Label { Text = text, ForeColor = P.Dim, AutoSize = true, Location = new Point(x, y) };
        }

        public void Detect()
        {
            routerOk = Connector.RouterAlive(routerUrl);
            providerOk = Connector.HasProvider(cfgPath);
            wiredOk = Connector.IsFullyWired(cfgPath, routerUrl);
            s1.Text = routerOk ? "✔  1.  9Router đang chạy" : "✖  1.  9Router chưa chạy";
            s1.ForeColor = routerOk ? P.Cur : P.Dim;
            s2.Text = providerOk ? "✔  2.  Codex đã biết tới 9Router" : "✖  2.  Codex chưa có khai báo 9Router";
            s2.ForeColor = providerOk ? P.Cur : P.Dim;
            s3.Text = wiredOk ? "✔  3.  Đã kết nối xong" : "✖  3.  Chưa kết nối";
            s3.ForeColor = wiredOk ? P.Cur : P.Dim;

            if (boxKey.Text.Trim().Length == 0) {
                string k = Connector.AutoDetectKey();
                if (k != null) boxKey.Text = k;
            }
            btnWire.Enabled = routerOk && (wiredOk || boxKey.Text.Trim().Length > 8);
            btnWire.Text = wiredOk ? "Đã kết nối" : "Kết nối ngay";
            // Nut ngat chi bat khi config that su con tro ve 9Router.
            bool canUnwire = providerOk || Connector.ModelIsFrom9Router(cfgPath);
            btnUnwire.Enabled = canUnwire;
            lblUnwire.ForeColor = canUnwire ? P.Fg : P.Dim;
            lblUnwire.Text = canUnwire
                ? "Xoá khai báo 9Router khỏi config.toml, đưa Codex về dùng nhà cung cấp mặc định."
                : "Codex đang dùng nhà cung cấp mặc định — không cần ngắt gì thêm.";
            // Chi goi y model khi nguoi dung CHON chinh no (khong tick "tu chon").
            if (!chkAuto.Checked && boxModel.Text.Trim().Length == 0 && canUnwire)
                boxModel.Text = Connector.SuggestDefaultModel(cfgPath);
        }

        // Bat/tat o nhap model khi tick "de Codex tu chon".
        void SyncAuto()
        {
            if (chkAuto == null) return;
            boxModel.Enabled = !chkAuto.Checked;
            modelCard.Border = chkAuto.Checked
                ? ControlPaint.Light(P.Line, 0.45f) : P.Line;
            lblModel.ForeColor = chkAuto.Checked ? P.Dim : P.Fg;
            if (chkAuto.Checked) boxModel.Text = "";
        }

        // XOA THAT config. Day la ham quan trong nhat cua man hinh nay -
        // truoc do no chi hien hop thoai xac nhan ma KHONG goi ham nao,
        // nen bam nut khong thay doi gi.
        void Unwire()
        {
            if (!Connector.IsWired(cfgPath) && !Connector.ModelIsFrom9Router(cfgPath))
            {
                MessageBox.Show("Config hiện tại không có khai báo 9Router, không cần ngắt.", "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Detect();
                return;
            }
            string fb = chkAuto.Checked ? "" : boxModel.Text.Trim();
            string modelLine = (fb.Length == 0)
                ? "Codex tự chọn model mặc định của nó"
                : fb;

            string q =
                "Ngắt kết nối 9Router?" + Environment.NewLine + Environment.NewLine +
                "Tool sẽ:" + Environment.NewLine +
                "  ·  xoá khai báo [model_providers.9router] khỏi config.toml" + Environment.NewLine +
                "  ·  trả Codex về nhà cung cấp mặc định (openai)" + Environment.NewLine +
                "  ·  model sau khi ngắt: " + modelLine + Environment.NewLine +
                "  ·  giữ nguyên mọi cấu hình khác của bạn" + Environment.NewLine + Environment.NewLine +
                "Trạng thái config hiện tại sẽ được lưu thành một bản sao trước khi ghi." + Environment.NewLine +
                Environment.NewLine + "Mở lại Codex sau khi ngắt.";
            if (MessageBox.Show(q, "Xác nhận ngắt kết nối", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                List<string> log = new List<string>();
                int n = Connector.Unwire(cfgPath, fb, log);
                if (n == 0)
                {
                    MessageBox.Show("Không tìm thấy gì để xoá — config có thể đã được sửa tay.", "9Router",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    string detail = string.Join(Environment.NewLine, log.ToArray());
                    MessageBox.Show("Đã ngắt kết nối 9Router." + Environment.NewLine + Environment.NewLine
                        + detail + Environment.NewLine + Environment.NewLine
                        + "Bản sao lưu: " + Connector.LastBackup + Environment.NewLine + Environment.NewLine
                        + "Mở lại Codex để áp dụng.", "9Router",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                boxKey.Clear();
                Detect();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không ngắt được: " + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void RenderSelf(Bitmap bmp)
        {
            Application.DoEvents();
            DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
        }

        void OpenRouter()
        {
            try { System.Diagnostics.Process.Start(routerUrl.TrimEnd('/') + "/"); } catch { }
        }

        void Wire()
        {
            string key = boxKey.Text.Trim();
            if (key.Length < 8) {
                MessageBox.Show("Cần khoá API của 9Router trước khi kết nối.", "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("Ghi cấu hình 9Router vào config.toml?" + Environment.NewLine + Environment.NewLine
                + "  file : " + cfgPath + Environment.NewLine
                + "  địa chỉ : " + routerUrl.TrimEnd('/') + "/v1" + Environment.NewLine + Environment.NewLine
                + "Sẽ tự tạo bản sao trước khi ghi.",
                "Xác nhận kết nối", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try {
                string bak = Connector.Wire(cfgPath, routerUrl, key);
                boxKey.Text = "";
                Detect();
                MessageBox.Show("Đã kết nối 9Router với Codex." + Environment.NewLine + Environment.NewLine
                    + ((bak != null) ? ("Bản sao lưu: " + Path.GetFileName(bak) + Environment.NewLine) : "")
                    + "Mở lại Codex Desktop App để dùng được.", "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Không kết nối được:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    // ==================================================================
    // Cua so chinh
    // ==================================================================
    internal sealed class SwitcherForm : Form
    {
        readonly Palette P = Palette.Shared;
        Button btnBackups, btnFolder, btnConnect, btnDefault;
        Button btnMode1, btnMode2, btnAgents;
        bool multiMode;
        Panel currentBar; Label currentTag, currentName, lblState;
        TextBox filter; RowListView list; Label title, status;
        CardPanel filterCard, listCard;
        CheckBox chkCombo;
        List<ModelInfo> all;
        List<Hit> rows = new List<Hit>();
        string current;
        bool comboOnly;
        int total, pool;
        string configPath;
        const int RowH = 40;

        public SwitcherForm(string cfgPath)
        {
            configPath = cfgPath;
            Text = "9Router · Codex · Đổi mô hình";
            ClientSize = new Size(840, 620);
            // 4 nut chuc nang ben phai + 3 nut che do + ten model can ~560px.
        // Hep hon se lam o ten model de len nut.
        MinimumSize = new Size(860, 470);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = P.Form;
            Font = P.FUi;
            try { Icon = NativeWin.AppIcon; } catch { }
            KeyPreview = true;

            // CardPanel de dai nay co vien + bo goc, noi bat hon nen cua so.
            currentBar = new CardPanel { Border = P.Line, Fill = P.RowA, Radius = 10,
                Location = new Point(16, 42), Height = 52 };
            currentTag = new Label { Text = "●  ĐANG DÙNG", Font = P.FSmallB, ForeColor = P.Cur,
                AutoSize = true, Location = new Point(14, 9) };
            currentTag.AutoEllipsis = true;
            currentName = new Label { Font = P.FHead, ForeColor = P.Fg, AutoSize = false,
                Location = new Point(14, 23), Size = new Size(500, 24), AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft };
            currentBar.Controls.Add(currentTag); currentBar.Controls.Add(currentName);
            // --- Chon che do agent, dat ben phai dai "DANG DUNG" ---
            btnMode1 = MakeButton("1 agent", P.Row, P.Fg, false);
            btnMode1.Tag = false;
            btnMode2 = MakeButton("Nhiều agent", P.Row, P.Fg, false);
            btnMode2.Tag = true;
            // Luon hien. Khi bat "Nhieu agent" thi nut nay sang len va bat duoc bam.
            btnAgents = MakeButton("⚙ Cấu hình vai trò", P.Row, P.Dim, false);
            foreach (Button mb in new Button[] { btnMode1, btnMode2, btnAgents })
            {
                mb.AutoSize = false;
                mb.TextAlign = ContentAlignment.MiddleCenter;
                mb.MinimumSize = Size.Empty;
                mb.Padding = new Padding(0);
            }
            currentBar.Controls.Add(btnMode1); currentBar.Controls.Add(btnMode2);
            currentBar.Controls.Add(btnAgents);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 160, BackColor = P.Form };
            title = new Label { Text = "9Router · Codex · Đổi mô hình", Font = P.FHead, ForeColor = P.Fg,
                AutoSize = true, Location = new Point(16, 10) };

            btnConnect = MakeButton("Kết nối 9Router", P.Acc, Color.White, true);
            btnDefault = MakeButton("Về mặc định", P.RowA, P.Fg, false);
            btnBackups = MakeButton("Lịch sử sao lưu", P.RowA, P.Fg, false);
            btnFolder  = MakeButton("Mở thư mục sao lưu", P.RowA, P.Fg, false);
            header.Controls.Add(title); header.Controls.Add(currentBar);
            header.Controls.Add(btnConnect); header.Controls.Add(btnDefault);
            header.Controls.Add(btnBackups); header.Controls.Add(btnFolder);

            // O loc: khung co vien bo tron, ben trong la TextBox + tick "Chi combo".
            filterCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Dock = DockStyle.Top, Height = 40, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 8) };
            filter = new TextBox { Dock = DockStyle.Fill, Font = P.FUiB, BorderStyle = BorderStyle.None,
                BackColor = P.Box, ForeColor = P.Fg, Margin = new Padding(10, 0, 0, 0) };
            chkCombo = new CheckBox { Text = "Chỉ combo", AutoSize = true, ForeColor = P.Fg,
                BackColor = P.Box, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleLeft };
            filterCard.Controls.Add(filter);
            filterCard.Controls.Add(chkCombo);

            list = new RowListView(delegate (Graphics g, Rectangle r, int i, bool isSel, bool isFoc) {
                if (i < 0 || i >= rows.Count) return;
                DrawModelRow(g, r, rows[i], isSel, isFoc);
            }, RowH);
            list.Dock = DockStyle.Fill;
            list.BackColor = P.Box;
            // Bo vien danh sach: RowListView tu ve khung roi, them vien CardPanel
            // de danh sach noi bat hon nen cua so.
            listCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Dock = DockStyle.Fill, Padding = new Padding(1) };
            listCard.Controls.Add(list);

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = P.Form, Padding = new Padding(16, 10, 16, 4) };
            body.Controls.Add(listCard);
            lblState = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = P.Dim, Font = P.FUi, BackColor = P.Box, Text = "Đang tải…" };
            body.Controls.Add(lblState);
            body.Controls.Add(filterCard);
            // Z-order mac dinh de danh sach che phu "Dang tai",
            // nguoi dung chi thay mot o trang trong khong ro nguyen nhan.
            lblState.BringToFront();

            status = new Label { Dock = DockStyle.Fill, ForeColor = P.Dim, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            Button btnSwitch = MakeButton("Chuyển mô hình", P.Acc, Color.White, true);
            Button btnCli    = MakeButton("Mở Codex CLI", P.Sel, P.Fg, false);
            Button btnReload = MakeButton("Làm mới", P.RowA, P.Fg, false);
            FlowLayoutPanel btnBox = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 430,
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                Padding = new Padding(0, 13, 0, 4), BackColor = P.Form };
            btnBox.Controls.Add(btnSwitch); btnBox.Controls.Add(btnCli); btnBox.Controls.Add(btnReload);
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = P.Form, Padding = new Padding(16, 0, 16, 10) };
            footer.Controls.Add(status); footer.Controls.Add(btnBox);

            Controls.Add(body); Controls.Add(footer); Controls.Add(header);

            filter.TextChanged += delegate { UpdateResults(); };
            list.SelectionChanged += delegate { UpdateStatus(); };
            list.RowDoubleClicked += delegate { InvokeSwitch(); };
            chkCombo.CheckedChanged += delegate { comboOnly = chkCombo.Checked; UpdateResults(); };
            btnSwitch.Click += delegate { InvokeSwitch(); };
            btnCli.Click += delegate { InvokeCli(); };
            btnReload.Click += delegate { LoadModels(); };
            btnConnect.Click += delegate { OpenConnectForm(); };
            btnDefault.Click += delegate { InvokeDefault(); };
            btnBackups.Click += delegate { OpenBackupForm(); };
            btnFolder.Click += delegate { OpenBackupFolder(); };
            btnMode1.Click += delegate { SetMultiMode(false); };
            btnMode2.Click += delegate { SetMultiMode(true); };
            btnAgents.Click += delegate { OpenAgentForm(); };
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            filter.KeyDown += delegate (object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; InvokeSwitch(); }
                else if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; list.Focus(); }
            };
            list.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) InvokeSwitch(); };
            Resize += delegate { LayoutHeaderButtons(); };
            Shown += delegate { LayoutHeaderButtons(); LoadModels(); };
            Shown += delegate { UpdateMode(); LoadModels(); };
        }

        static Button MakeButton(string text, Color back, Color fore, bool primary)
        {
            // Flat + FlatAppearance.BorderSize de ve duoc vien mau tuy chon.
            // FlatStyle.System bo qua FlatAppearance nen se khong co vien.
            Button b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 4, 12, 4), FlatStyle = FlatStyle.Flat,
                BackColor = back, ForeColor = fore, UseVisualStyleBackColor = false,
                MinimumSize = new Size(0, 32) };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Palette.Shared.Line;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.12f);
            if (primary) {
                b.Font = Palette.Shared.FUiB;
                b.FlatAppearance.BorderColor = ControlPaint.Dark(back, 0.15f);
                b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.18f);
            }
            // FlatStyle.Flat KHONG tu mo nen khi nut bi disable, nen "Khoi phuc"
            // van nhu bam duoc. Tu lam mau cho ro rang.
            b.EnabledChanged += delegate {
                bool on = b.Enabled;
                b.BackColor = on ? back : ControlPaint.Light(back, 0.55f);
                b.ForeColor = on ? fore : ControlPaint.Light(fore, 0.45f);
            };
            return b;
        }

        public void SetFilterForShot(string q) { filter.Text = q ?? ""; UpdateResults(); }

        public void RenderSelf(Bitmap bmp)
        {
            LayoutHeaderButtons();
            Application.DoEvents();
            DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
        }

        public void PrepareForRender()
        {
            all = Router.Load();
            current = Cfg.Current(configPath);
            // Phai nap that danh sach + chu tran: --shot chup form o trang thai
            // ban giao, khong thi moi anh ra danh sach rong va khong bat duoc loi ve.
            UpdateHeader();
            UpdateResults();
        }

        public bool RenderRealList(Bitmap bmp, string q, int w, int h)
        {
            if (all == null) return false;
            filter.Text = q ?? "";
            UpdateResults();
            list.Width = w; list.Height = h;
            list.CreateControl();
            Application.DoEvents();
            list.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
            return rows.Count > 0;
        }

        // Mo phong danh sach len bitmap bang DUNG ham ve cua ListBox (khong dung control that).
        public int RenderTo(Bitmap bmp, string q, int rowCount)
        {
            List<Hit> hits = Matcher.Search(all == null ? new List<ModelInfo>() : all, q);
            List<Hit> show = hits.Count > rowCount ? hits.GetRange(0, rowCount) : hits;
            int headH = 112, rowH = RowH;
            g_Clear(bmp);
            g_Text(bmp, "9Router · Codex · Đổi mô hình", HeadFont, 16, 10, HeadBrush);
            g_Text(bmp, string.IsNullOrEmpty(current) ? "Không đọc được mô hình hiện tại" : "Mô hình đang dùng: " + current,
                UiFont, 18, 42, CurBrush);
            g_Text(bmp, q ?? "", UiBoldFont, 21, 76, FgBrush);
            for (int i = 0; i < show.Count; i++)
                DrawModelRow(g_Of(bmp), new Rectangle(16, headH + i * rowH, 808, rowH), show[i], i == 0, false);
            g_Text(bmp, "hien " + hits.Count + " / " + AllCount + " mô hình   ·   sẽ chuyển sang:  "
                + (show.Count > 0 ? show[0].M.Id : ""), UiFont, 16, bmp.Height - 32, DimBrush);
            return hits.Count;
        }

        Graphics g_Of(Bitmap bmp) { return Graphics.FromImage(bmp); }
        void g_Clear(Bitmap bmp) { using (Graphics g = Graphics.FromImage(bmp)) g.Clear(P.Form); }
        void g_Text(Bitmap bmp, string t, Font f, int x, int y, Brush col)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(t, f, col, new PointF(x, y), StringFormat.GenericTypographic);

            }
        }

        public int AllCount { get { return all == null ? 0 : all.Count; } }
        public int ListItemCount { get { return list.ItemCount; } }
        public int RowCount { get { return rows == null ? 0 : rows.Count; } }
        public int RowHeight { get { return RowH; } }
        public string CurrentModel { get { return current; } }
        public string FirstId { get { return (rows != null && rows.Count > 0) ? rows[0].M.Id : ""; } }
        public int LastSearchMs, LastFillMs;
        public int LastLoadMs;   // thoi gian tai danh sach tu 9Router
        public Color BoxColor { get { return P.Box; } }
        public Color FormColor { get { return P.Form; } }
        public Color DimColor { get { return P.Dim; } }
        public Font HeadFont { get { return P.FHead; } }
        public Font UiFont { get { return P.FUi; } }
        public Font UiBoldFont { get { return P.FUiB; } }
        public Brush HeadBrush { get { return P.B["Fg"]; } }
        public Brush FgBrush { get { return P.B["Fg"]; } }
        public Brush DimBrush { get { return P.B["Dim"]; } }
        public Brush CurBrush { get { return P.B["Cur"]; } }
        // ---------- tai du lieu ----------
        void LoadModels()
        {
            System.Diagnostics.Stopwatch swLoad = System.Diagnostics.Stopwatch.StartNew();


            // Hien danh sach trong bo nho dem ngay neu co, roi moi lam moi tu 9Router.
            List<ModelInfo> cache = ModelCache.Load();
            if (cache != null && all == null) {
                all = cache;
                current = Cfg.Current(configPath);
                UpdateHeader();
                UpdateResults();
                lblState.Visible = false;
                status.Text = "đang cập nhật danh sách từ 9Router…";
            } else {
                lblState.Visible = true;
                lblState.Text = "Đang tải danh sách mô hình từ 9Router…";
                status.Text = "Đang tải…";
            }
            Task.Factory.StartNew(new Func<object>(delegate () {
                try { return (object)Router.Load(); }
                catch (Exception ex) { return (object)ex; }
            })).ContinueWith(delegate (Task<object> t) {
                object r = t.Result;
                LastLoadMs = (int)swLoad.ElapsedMilliseconds;
                BeginInvoke((MethodInvoker)delegate {
                    if (r is Exception) {
                        status.Text = "Tải thất bại.";
                        lblState.Visible = true;
                        lblState.Text = "Không lấy được danh sách mô hình từ 9Router."
                            + Environment.NewLine + Environment.NewLine + "Hãy bật 9Router rồi bấm Làm mới.";
                        return;
                    }
                    all = (List<ModelInfo>)r;
                    ModelCache.Save(all);
                    current = Cfg.Current(configPath);
                    UpdateHeader();
                    UpdateResults();
                    // QUAN TRONG: an nhan "Dang tai" de danh sach khong bi phu trang
                    lblState.Visible = false;
                    // Ve lai dong bo de danh sach co chu ngay o frame dau tien.
                    list.Update();
                });
            });
        }

        void UpdateHeader()
        {
            // Ten nha cung cap luon hien tren dai "DANG DUNG" de nguoi dung
            // biet dang qua 9Router hay da ve mac dinh cua Codex.
            string prov = CurProvider();
            string tag = (prov == "(mặc định của Codex)") ? "●  ĐANG DÙNG" : "●  ĐANG DÙNG  ·  " + prov;
            if (string.IsNullOrEmpty(current)) {
                currentName.Text = "Không đọc được mô hình hiện tại";
                currentName.ForeColor = P.Dim; currentTag.ForeColor = P.Dim;
                currentBar.BackColor = P.Row;
            } else {
                currentName.Text = current;
                currentName.ForeColor = P.Fg;
                currentTag.ForeColor = P.Cur;
                currentBar.BackColor = P.RowA;
            }
            currentTag.Text = tag;
        }

        // Che do nhieu agent duoc SUY RA tu config.toml co [agents.<ten>] hay khong,
        // khong ghi them khoa la - tranh "unknown configuration field" cua codex.
        void UpdateMode()
        {
            multiMode = Roles.Exists(configPath);
            PaintMode(btnMode1, !multiMode);
            PaintMode(btnMode2, multiMode);
            // Nut cau hinh chi sang + bat duoc bam khi dang o che do nhieu agent.
            if (multiMode) PaintMode(btnAgents, true);
            else
            {
                btnAgents.BackColor = P.Row;
                btnAgents.ForeColor = P.Dim;
                btnAgents.FlatAppearance.BorderColor = P.Line;
                btnAgents.FlatAppearance.MouseOverBackColor = P.Row;
                btnAgents.Enabled = false;
            }
            LayoutHeaderButtons();
        }

        // Ve trang thai "bat": nen xanh, chu trang. Tat: nen trang, chu xam.
        static void PaintMode(Button b, bool on)
        {
            Palette p = Palette.Shared;
            // Bat truoc: su kien EnabledChanged cua MakeButton tu ve lai mau,
            // phai de no chay xong roi moi to mau trang thai "bat".
            b.Enabled = true;
            b.BackColor = on ? p.Acc : p.Row;
            b.ForeColor = on ? Color.White : p.Fg;
            b.FlatAppearance.BorderColor = on ? p.Acc : p.Line;
            b.FlatAppearance.MouseOverBackColor = on ? p.Acc : p.RowA;
        }

        public void LayoutHeaderButtons()
        {
            if (btnBackups == null) return;
            // LUOI 2x2 thay vi 4 nut xep chong doc: bo cuc gon hon mot nua,
            // va dai "DANG DUNG" khong bi thu nho khi cua so hep.
            int w = ClientSize.Width;
            int bw = Math.Max(btnConnect.Width, Math.Max(btnDefault.Width,
                        Math.Max(btnBackups.Width, btnFolder.Width))) + 14;
            const int gap = 8, top = 8, rowH = 38;
            int x1 = w - 16 - bw;
            int x0 = x1 - bw - gap;
            btnConnect.Location = new Point(x0, top);
            btnBackups.Location = new Point(x0, top + rowH);
            btnDefault.Location = new Point(x1, top);
            btnFolder.Location = new Point(x1, top + rowH);
            int avail = Math.Max(180, x0 - 24);
            currentBar.Location = new Point(16, top + rowH * 2 + 8);
            currentBar.Width = avail;
            // 3 nut ben phai: Chay 2 nut che do + (neu dang o che do nhieu agent) nut mo cua so vai tro.
            // Thu tu: [1 agent] [Nhieu agent] [⚙ Cau hinh vai tro]
            // -> nut cau hinh nam NGAY BEN CANH nut da bat, de mat kiem tra 1 nhin.
            const int bh = 26, mw = 78, mg = 4, aw = 132;
            int by = (currentBar.Height - bh) / 2;
            int rx = currentBar.Width - 12 - aw;
            btnAgents.Location = new Point(rx, by);
            btnAgents.Size = new Size(aw, bh);
            int lx = rx - mg - mw;
            btnMode2.Location = new Point(lx, by);
            btnMode1.Location = new Point(lx - mg - mw, by);
            btnAgents.Visible = true;
            // Ten model PHAI kiem soat boi nut che do. Khong gan width thi
            // no giu nguyen 500px cua khoi tao va tran ra ngoai thanh.
            int nameRight = btnMode1.Left - 10;
            currentName.Width = Math.Max(120, nameRight - 14);
            currentTag.Width = Math.Max(60, nameRight - 28);
        }

        // Mo cua so cau hinh vai tro. Tra true neu co thay doi de tai lai danh sach.
        bool OpenAgentForm()
        {
            try
            {
                bool changed = false;
                using (AgentForm af = new AgentForm(configPath, all))
                {
                    changed = (af.ShowDialog(this) == DialogResult.OK);
                }
                current = Cfg.Current(configPath);
                UpdateHeader();
                UpdateMode();
                UpdateResults();
                return changed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được cửa sổ vai trò:" + Environment.NewLine + ex.Message,
                    "9Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Bam nut "Nhieu agent": mo cua so cau hinh vai tro ngay.
        // Hai nut loat nhau: bam "1 agent" -> goi het vai tro, quay ve 1 agent.
        // Bam "Nhieu agent" -> sang che do nhieu agent + mo cua so cau hinh.
        void SetMultiMode(bool on)
        {
            if (on)
            {
                if (Roles.Exists(configPath))
                {
                    multiMode = true;
                    UpdateMode();
                    OpenAgentForm();
                    return;
                }
                // Chua co vai tro nao -> thang mo cua so de nguoi dung tao.
                multiMode = true;
                UpdateMode();
                OpenAgentForm();
                return;
            }

            if (!Roles.Exists(configPath))
            {
                multiMode = false;
                UpdateMode();
                return;
            }

            int n = Roles.Read(configPath).Count - 1;
            if (MessageBox.Show("Tắt chế độ nhiều agent?" + Environment.NewLine + Environment.NewLine
                    + "Sẽ xoá " + n + " vai trò khỏi config.toml." + Environment.NewLine
                    + "Model của leader và các cấu hình khác giữ nguyên." + Environment.NewLine + Environment.NewLine
                    + "Codex sẽ dùng chung một model cho người và subagent.",
                    "9Router", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try { Roles.Disable(configPath); }
            catch (Exception ex)
            {
                MessageBox.Show("Không tắt được:" + Environment.NewLine + ex.Message,
                    "9Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            multiMode = false;
            current = Cfg.Current(configPath);
            UpdateHeader();
            UpdateMode();
            UpdateResults();
        }

        void UpdateResults()
        {
            if (all == null) return;
            List<ModelInfo> src = all;
            if (comboOnly) { src = new List<ModelInfo>(); foreach (ModelInfo m in all) if (m.IsCombo) src.Add(m); }
            var swS = System.Diagnostics.Stopwatch.StartNew();
            List<Hit> hits = Matcher.Search(src, filter.Text);
            LastSearchMs = (int)swS.ElapsedMilliseconds;
            total = hits.Count;
            pool = src.Count;
            rows = hits;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            list.SetItems(rows.Count, rows.Count > 0 ? 0 : -1);
            list.EmptyText = (rows.Count == 0 && filter.Text.Trim().Length > 0)
                ? "Không có mô hình nào khớp với từ khóa."
                : "Danh sách mô hình đang trống.";
            LastFillMs = (int)sw.ElapsedMilliseconds;
            UpdateStatus();
            if (LastLoadMs > 0) status.Text += "   ·   tải xong trong " + LastLoadMs + " ms";
        }

        void UpdateStatus()
        {
            if (rows == null) return;
            if (list.SelectedIndex >= 0 && list.SelectedIndex < rows.Count)
                status.Text = "hiện " + total + " / " + pool + " mô hình   ·   sẽ chuyển sang:  " + rows[list.SelectedIndex].M.Id;
            else
                status.Text = "hiện " + total + " / " + pool + " mô hình";
        }

        // ---------- ve dong ----------
        void DrawModelRow(Graphics g, Rectangle r, Hit row, bool isSel, bool isFoc)
        {
            string ctx = "";
            if (row.M.Ctx > 0)
                ctx = (row.M.Ctx >= 1000000) ? (row.M.Ctx / 1000000.0).ToString("0.#") + "M"
                                             : (row.M.Ctx / 1000.0).ToString("0.#") + "k";
            string detail = row.M.IsCombo ? "combo" : row.M.Owner;
            if (ctx.Length > 0) detail += "   ·   " + ctx + " context";
            if (row.M.Id == current) detail = "● đang dùng   ·   " + detail;
            RowPainter.Draw(g, r, P, row.M.Id, row.Pos, detail, isSel, isFoc, row.M.IsCombo, row.M.Id == current, "COMBO");
        }
        // ---------- hanh dong ----------
        public void OpenConnectForm()
        {
            try {
                using (ConnectForm cf = new ConnectForm(configPath, Router.Url)) {
                    cf.ShowDialog(this);
                }
                current = Cfg.Current(configPath);
                UpdateHeader();
                UpdateResults();
            } catch (Exception ex) {
                MessageBox.Show("Không mở được:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Ngat 9Router ngay tren cua so chinh. Mot lan bam, khong can
        // mo them cua so nao. Tra ve true neu da thay doi config.
        public bool InvokeDefault()
        {
            if (!Connector.IsWired(configPath) && !Connector.ModelIsFrom9Router(configPath))
            {
                MessageBox.Show("Codex đang dùng nhà cung cấp mặc định rồi — không cần ngắt gì thêm."
                    + Environment.NewLine + Environment.NewLine
                    + "Nhà cung cấp hiện tại: " + CurProvider(), "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            string fb = Connector.SuggestDefaultModel(configPath);
            string cur = string.IsNullOrEmpty(current) ? "(không có)" : current;

            string body =
                "Tool sẽ xoá khai báo 9Router khỏi config.toml" + Environment.NewLine +
                "và đưa Codex về nhà cung cấp mặc định của OpenAI." + Environment.NewLine + Environment.NewLine +
                "  model hiện tại  : " + cur + Environment.NewLine +
                "  model sau khi đó: " + (fb.Length == 0 ? "Codex tự chọn" : fb) + Environment.NewLine + Environment.NewLine +
                "Mọi cấu hình khác của bạn được giữ nguyên." + Environment.NewLine +
                "Config hiện tại sẽ được sao lưu trước khi ghi." + Environment.NewLine + Environment.NewLine +
                "Mở lại Codex sau khi ngắt.";
            if (!Connector.HasOpenAiAuth())
                body += Environment.NewLine + Environment.NewLine +
                    "Lưu ý: máy này chưa đăng nhập OpenAI. Sau khi ngắt, hãy chạy" + Environment.NewLine +
                    "codex login trong terminal để Codex dùng được nhà cung cấp mặc định.";
            if (MessageBox.Show(body, "Ngắt kết nối 9Router", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return false;

            try
            {
                List<string> log = new List<string>();
                int n = Connector.Unwire(configPath, fb, log);
                if (n == 0)
                {
                    MessageBox.Show("Không tìm thấy gì để xoá — config có thể đã được sửa tay.", "9Router",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                current = Cfg.Current(configPath);
                UpdateHeader();
                UpdateResults();
                status.Text = "Đã ngắt 9Router. Codex sẽ dùng nhà cung cấp mặc định.";
                string done = "Đã ngắt kết nối 9Router." + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, log.ToArray())
                    + Environment.NewLine + Environment.NewLine
                    + "Bản sao lưu: " + Connector.LastBackup + Environment.NewLine + Environment.NewLine
                    + "Mở lại Codex để áp dụng.";
                if (!Connector.HasOpenAiAuth())
                    done += Environment.NewLine + Environment.NewLine +
                        "Máy này chưa đăng nhập OpenAI — hãy chạy codex login trong terminal.";
                MessageBox.Show(done, "9Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không ngắt được: " + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Nha cung cap dang dung, de hien thi cho nguoi dung.
        public string CurProvider()
        {
            string p = Connector.ProviderNameOf(configPath);
            if (p.Length == 0) return "(mặc định của Codex)";
            if (p.IndexOf("9router", StringComparison.OrdinalIgnoreCase) >= 0) return "9Router";
            return p;
        }

        public void OpenBackupForm()
        {
            try {
                using (BackupForm bf = new BackupForm(configPath)) {
                    bf.ShowDialog(this);
                }
                current = Cfg.Current(configPath);
                UpdateHeader();
                UpdateResults();
            } catch (Exception ex) {
                MessageBox.Show("Không mở được danh sách bản sao:" + Environment.NewLine + ex.Message,
                    "9Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenBackupFolder()
        {
            try {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + configPath + "\"");
            } catch (Exception ex) {
                MessageBox.Show("Không mở được thư mục:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void InvokeSwitch()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= rows.Count) return;
            string nw = rows[list.SelectedIndex].M.Id;
            string old = string.IsNullOrEmpty(current) ? "(không có)" : current;

            // Sau khi ngat 9Router, config khong con khai bao provider.
            // Chuyen sang ten model cua 9Router luc do se BRO - nen tu
            // noi lai truoc, key doc truc tiep tu 9Router.
            bool needWire = !Connector.IsWired(configPath);
            string autoKey = null;
            if (needWire) {
                autoKey = Connector.AutoDetectKey();
                if (autoKey != null) {
                    DialogResult r = MessageBox.Show("Codex đang dùng nhà cung cấp mặc định (đã ngắt 9Router)." + Environment.NewLine
                        + Environment.NewLine + "Chuyển sang \"" + nw + "\" cần nối lại 9Router." + Environment.NewLine
                        + "Tool tự đọc khoá từ 9Router và kết nối lại giúp bạn." + Environment.NewLine + Environment.NewLine
                        + "Nối lại 9Router ngay?", "Cần kết nối 9Router", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (r != DialogResult.Yes) return;
                } else {
                    MessageBox.Show("Chuyển sang \"" + nw + "\" cần khai báo 9Router trong config.toml." + Environment.NewLine
                        + Environment.NewLine + "Bấm \"Kết nối 9Router\" ở góc phải để làm việc này.", "9Router",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            if (MessageBox.Show("Chuyển mô hình cho Codex?" + Environment.NewLine + Environment.NewLine
                + "  hiện tại : " + old + Environment.NewLine + "  mới     : " + nw + Environment.NewLine + Environment.NewLine
                + "File sẽ được sao lưu trước khi ghi.", "Xác nhận", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
            try {
                if (needWire) Connector.Wire(configPath, Router.Url, autoKey);
                string backup;
                Cfg.SetModel(configPath, nw, out backup);
                current = nw;
                UpdateHeader();
                UpdateResults();
                status.Text = "Đã chuyển sang " + nw + "  ·  bản sao lưu: " + Path.GetFileName(backup);
                MessageBox.Show("Đã chuyển mô hình sang:" + Environment.NewLine + Environment.NewLine + nw
                    + Environment.NewLine + Environment.NewLine + "Mở lại Codex Desktop App để áp dụng."
                    + Environment.NewLine + "Bản sao lưu: " + Path.GetFileName(backup), "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Không chuyển được:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void InvokeCli()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= rows.Count) return;
            string m = rows[list.SelectedIndex].M.Id;
            try {
                ProcessStartInfo psi = new ProcessStartInfo("codex.exe", "-m \"" + m + "\"");
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Process.Start(psi);
                status.Text = "Đã mở Codex CLI với mô hình " + m;
            } catch (Exception ex) {
                MessageBox.Show("Không mở được Codex CLI:" + Environment.NewLine + ex.Message, "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

    }
}
