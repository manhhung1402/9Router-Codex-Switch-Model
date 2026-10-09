using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NineRouter
{
    // ==================================================================
    // Danh sach tu ve hoan toan.
    //
    // KHONG dung ListBox / ListView o che do owner-draw. Windows chi gui
    // WM_DRAWITEM cho ListBox khi ListBox da ve mot lan, nen frame ve
    // dau tien ra danh sach trong, chi khi bam chuot hoac bam "Lam moi"
    // thi chu moi hien. Control nay ve thang trong OnPaint, nen chu
    // hien dung ngay o frame dau tien.
    // ==================================================================
    internal sealed class RowListView : Control
    {
        public delegate void DrawRowDelegate(Graphics g, Rectangle r, int index,
            bool selected, bool focused);

        readonly DrawRowDelegate drawRow;
        readonly List<int> sel = new List<int>();

        int itemCount;
        readonly int rowH;
        int top;                 // dong dau tien dang hien
        int anchor = -1;         // dong neo cho chon nhieu (Shift)
        bool trackDown;          // dang keo thanh cuon
        int trackOffset;

        // Chu hien o giua danh sach khi khong co dong nao.
        // \n xuong dong. Rong ma khong co gi thi nguoi dung khong hieu vi sao.
        public string EmptyText = "";

        const int BarW = 13;

        public event EventHandler SelectionChanged;
        public event EventHandler RowDoubleClicked;

        public RowListView(DrawRowDelegate painter, int rowHeight)
        {
            drawRow = painter;
            rowH = rowHeight < 10 ? 10 : rowHeight;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = SystemColors.Window;
            TabStop = true;
        }

        // ---------- trang thai ----------
        public int ItemCount { get { return itemCount; } }
        public int RowHeight { get { return rowH; } }
        public int TopIndex { get { return top; } }

        public int PageSize
        {
            get
            {
                int h = ClientSize.Height;
                if (HasBar) h -= BarW;
                if (h < rowH) h = rowH;
                return h / rowH;
            }
        }

        public bool HasBar { get { return itemCount * rowH > ClientSize.Height; } }

        public int ViewWidth
        {
            get
            {
                int w = ClientSize.Width - 1 - (HasBar ? BarW : 0);
                return w < 20 ? 20 : w;
            }
        }

        public int MaxTop
        {
            get
            {
                int m = itemCount - PageSize;
                return m < 0 ? 0 : m;
            }
        }

        int cursorIndex = -1;
        public int SelectedIndex
        {
            get { return cursorIndex; }
            set
            {
                if (itemCount <= 0) { cursorIndex = -1; return; }
                if (value < 0) value = 0;
                if (value >= itemCount) value = itemCount - 1;
                cursorIndex = value;
                anchor = value;
            }
        }

        public int SelectedCount { get { return sel.Count; } }

        public List<int> GetSelected()
        {
            List<int> r = new List<int>(sel);
            r.Sort();
            return r;
        }

        public bool IsSelected(int i) { return sel.Contains(i); }

        public void ClearSelection()
        {
            sel.Clear();
            cursorIndex = itemCount > 0 ? 0 : -1;
            anchor = cursorIndex;
            Invalidate();
            RaiseSelectionChanged();
        }

        public void SetSelected(int i, bool on)
        {
            if (i < 0 || i >= itemCount) return;
            if (on) { if (!sel.Contains(i)) sel.Add(i); }
            else sel.Remove(i);
            Invalidate();
            RaiseSelectionChanged();
        }

        // Nap lai danh sach. selectedIndex = -1 de khong chon gi.
        public void SetItems(int count, int selectedIndex)
        {
            itemCount = count < 0 ? 0 : count;
            sel.Clear();
            if (itemCount == 0)
            {
                cursorIndex = -1;
                anchor = -1;
            }
            else
            {
                if (selectedIndex < 0) selectedIndex = 0;
                if (selectedIndex >= itemCount) selectedIndex = itemCount - 1;
                cursorIndex = selectedIndex;
                anchor = selectedIndex;
                sel.Add(selectedIndex);
            }
            top = 0;
            Invalidate();
        }

        void RaiseSelectionChanged()
        {
            EventHandler h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        void ClampTop()
        {
            if (top < 0) top = 0;
            if (top > MaxTop) top = MaxTop;
        }

        public void EnsureVisible(int i)
        {
            if (i < 0 || i >= itemCount) return;
            int page = PageSize;
            if (i < top) top = i;
            else if (i >= top + page) top = i - page + 1;
            ClampTop();
            Invalidate();
        }

        public void ScrollBy(int rows)
        {
            top += rows;
            ClampTop();
            Invalidate();
        }

        public void ScrollToTop()
        {
            top = 0;
            Invalidate();
        }

        // ---------- ve ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle clip = ClientRectangle;
            using (SolidBrush bg = new SolidBrush(BackColor)) g.FillRectangle(bg, clip);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            ClampTop();
            bool bar = HasBar;
            int viewW = ViewWidth;
            int first = top;
            int last = first + (clip.Height / rowH) + 1;
            if (last > itemCount) last = itemCount;

            if (drawRow != null)
            {
                bool foc = Focused;
                for (int i = first; i < last; i++)
                {
                    Rectangle row = new Rectangle(1, (i - top) * rowH, viewW, rowH);
                    drawRow(g, row, i, sel.Contains(i), foc && i == cursorIndex);
                }
            }

            if (itemCount == 0 && !string.IsNullOrEmpty(EmptyText))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush eb = new SolidBrush(SystemColors.GrayText))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    string[] parts = EmptyText.Split('\n');
                    int lh = 18;
                    int y0 = clip.Height / 2 - (parts.Length * lh) / 2;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (y0 + i * lh + lh > clip.Height) break;
                        Rectangle line = new Rectangle(1, y0 + i * lh, clip.Width - 2, lh);
                        g.DrawString(parts[i].Trim(), Font, eb, line, sf);
                    }
                }
            }

            using (Pen p = new Pen(SystemColors.ControlDark))
                g.DrawRectangle(p, 0, 0, clip.Width - 1, clip.Height - 1);

            if (bar) PaintBar(g, clip);
        }

        void PaintBar(Graphics g, Rectangle clip)
        {
            Rectangle track = new Rectangle(clip.Width - BarW, 1, BarW - 1, clip.Height - 2);
            using (SolidBrush b = new SolidBrush(SystemColors.Control))
                g.FillRectangle(b, track);
            int page = PageSize;
            int th = (int)((double)track.Height * page / Math.Max(1, itemCount));
            if (th < 24) th = 24;
            if (th > track.Height) th = track.Height;
            int span = itemCount - page;
            int ty = span <= 0 ? 0
                : (int)((double)(track.Height - th) * top / span);
            Rectangle thumb = new Rectangle(track.X, track.Y + ty, track.Width, th);
            using (SolidBrush b = new SolidBrush(SystemColors.ScrollBar))
                g.FillRectangle(b, thumb);
        }

        // ---------- chuot ----------
        int IndexAt(int y)
        {
            int i = top + y / rowH;
            if (i < 0) i = 0;
            if (i >= itemCount) i = itemCount - 1;
            return i;
        }

        Rectangle ThumbRect()
        {
            Rectangle clip = ClientRectangle;
            Rectangle track = new Rectangle(clip.Width - BarW, 1, BarW - 1, clip.Height - 2);
            int page = PageSize;
            int th = (int)((double)track.Height * page / Math.Max(1, itemCount));
            if (th < 24) th = 24;
            if (th > track.Height) th = track.Height;
            int span = itemCount - page;
            int ty = span <= 0 ? 0
                : (int)((double)(track.Height - th) * top / span);
            return new Rectangle(track.X, track.Y + ty, track.Width, th);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (HasBar && e.X >= ClientSize.Width - BarW)
            {
                trackDown = true;
                Rectangle th = ThumbRect();
                trackOffset = e.Y - th.Y;
                if (e.Y < th.Y) { top = Math.Max(0, top - PageSize); Invalidate(); }
                else if (e.Y > th.Bottom) { top = Math.Min(MaxTop, top + PageSize); Invalidate(); }
                return;
            }
            if (itemCount == 0) return;
            int i = IndexAt(e.Y);

            if ((ModifierKeys & Keys.Shift) != 0 && anchor >= 0)
            {
                sel.Clear();
                int a = Math.Min(anchor, i);
                int b2 = Math.Max(anchor, i);
                for (int k = a; k <= b2; k++) sel.Add(k);
                cursorIndex = i;
            }
            else if ((ModifierKeys & Keys.Control) != 0)
            {
                if (sel.Contains(i)) sel.Remove(i); else sel.Add(i);
                cursorIndex = i;
                anchor = i;
            }
            else
            {
                sel.Clear();
                sel.Add(i);
                cursorIndex = i;
                anchor = i;
            }
            EnsureVisible(i);
            Invalidate();
            RaiseSelectionChanged();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!trackDown) return;
            Rectangle track = new Rectangle(ClientSize.Width - BarW, 1, BarW - 1, ClientSize.Height - 2);
            int page = PageSize;
            int th = (int)((double)track.Height * page / Math.Max(1, itemCount));
            if (th < 24) th = 24;
            if (th > track.Height) th = track.Height;
            int span = itemCount - page;
            if (span <= 0 || track.Height - th <= 0) return;
            double f = (double)(e.Y - trackOffset - track.Y) / (track.Height - th);
            top = (int)(f * span);
            ClampTop();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            trackDown = false;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (HasBar && e.X >= ClientSize.Width - BarW) return;
            if (itemCount == 0) return;
            EventHandler h = RowDoubleClicked;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = e.Delta / 120;
            if (step == 0) step = e.Delta > 0 ? 1 : -1;
            top -= step * 3;
            ClampTop();
            Invalidate();
        }

        // ---------- ban phim ----------
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End:
                case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (itemCount == 0) { base.OnKeyDown(e); return; }
            int page = PageSize;
            int i = cursorIndex < 0 ? 0 : cursorIndex;

            switch (e.KeyCode)
            {
                case Keys.Up: i--; break;
                case Keys.Down: i++; break;
                case Keys.PageUp: i -= page; break;
                case Keys.PageDown: i += page; break;
                case Keys.Home: i = 0; break;
                case Keys.End: i = itemCount - 1; break;
                case Keys.Space:
                    if (sel.Contains(i)) sel.Remove(i); else sel.Add(i);
                    Invalidate();
                    RaiseSelectionChanged();
                    e.Handled = true;
                    return;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            if (i < 0) i = 0;
            if (i >= itemCount) i = itemCount - 1;
            sel.Clear();
            sel.Add(i);
            cursorIndex = i;
            anchor = i;
            EnsureVisible(i);
            Invalidate();
            RaiseSelectionChanged();
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        // Mo khoa nhan giong thao tac chuot that cua nguoi dung.
        public void ClickRow(int index)
        {
            if (itemCount == 0) return;
            if (index < 0) index = 0;
            if (index >= itemCount) index = itemCount - 1;
            int y = (index - top) * rowH + rowH / 2;
            if (y < 0 || y >= ClientSize.Height)
            {
                top = Math.Max(0, index);
                ClampTop();
                y = rowH / 2;
            }
            if (!IsHandleCreated) CreateControl();
            IntPtr lp = (IntPtr)((y << 16) | (30 & 0xFFFF));
            NativeWin.SendMessage(Handle, 0x0201, (IntPtr)1, lp);
            NativeWin.SendMessage(Handle, 0x0202, IntPtr.Zero, lp);
            Invalidate();
        }
    }
}
