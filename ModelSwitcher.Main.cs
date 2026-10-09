using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NineRouter
{

    internal static class Program
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        static int pass = 0, fail = 0;

        static void Log(string where, Exception ex)
        {
            try {
                string p = Path.Combine(Path.GetTempPath(), "9router-switcher-error.log");
                File.AppendAllText(p, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + where
                    + Environment.NewLine + ex + Environment.NewLine + "----" + Environment.NewLine);
            } catch { }
        }

        [STAThread]
        static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e) { Log("ThreadException", e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, System.UnhandledExceptionEventArgs e) { Log("Unhandled", e.ExceptionObject as Exception); };
            return Run(args);
        }

        static int Run(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            string cfgPath = Cfg.DefaultPath();
            string query = "";
            string renderOut = null;
            string renderCtl = null;
            string shot = null;
            string shotBackup = null;
        string shotAgents = null;
            string shotConnect = null;
            bool multiSel = false;
            string switchTo = null;
            string defaultTo = null;
            string outFile = null;
            int renderRows = 12;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--url" && i + 1 < args.Length) Router.Url = args[++i];
                else if (a == "--config" && i + 1 < args.Length) cfgPath = args[++i];
                else if (a == "--rows" && i + 1 < args.Length) int.TryParse(args[++i], out renderRows);
                else if (a == "--render") renderOut = i + 1 < args.Length ? args[++i] : null;
                else if (a == "--rendercontrol") renderCtl = i + 1 < args.Length ? args[++i] : null;
                else if (a == "--shot") shot = i + 1 < args.Length ? args[++i] : null;
                else if (a == "--shotbackup") shotBackup = i + 1 < args.Length ? args[++i] : null;
            else if (a == "--shotagents") shotAgents = i + 1 < args.Length ? args[++i] : null;
                else if (a == "--shotconnect") shotConnect = i + 1 < args.Length ? args[++i] : null;
                else if (a == "--multi") multiSel = true;
                else if (a == "--switch" && i + 1 < args.Length) switchTo = args[++i];
                else if (a == "--default") { mode = "--default"; defaultTo = args.Length > i + 1 && !args[i + 1].StartsWith("-") ? args[++i] : ""; }
                else if (a == "--out" && i + 1 < args.Length) outFile = args[++i];
                else if (a == "--selftest") mode = "--selftest";
                else if (a == "--check") mode = "--check";
                else if (a == "--help" || a == "-h" || a == "/?") mode = "--help";
                else if (!a.StartsWith("-")) query = args[i];
            }

            if (mode == "" && switchTo == null) mode = "--gui";

            if (mode != "--gui")
            {
                AttachConsole(-1);
                if (!string.IsNullOrEmpty(outFile))
                    Console.SetOut(new StreamWriter(new FileStream(outFile, FileMode.Create, FileAccess.Write)) { AutoFlush = true });
            }

            try
            {
                if (mode == "--help") { Usage(); return 0; }
                if (mode == "--selftest") { int rc = SelfTest(); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, null); return rc; }
                if (mode == "--backuptest") return DoBackupTest(cfgPath);
                if (mode == "--check") { int rc = DoCheck(cfgPath); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Kiểm tra môi trường"); return rc; }
                if (mode == "--default") { int rc = DoDefault(cfgPath, defaultTo); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Chuyen ve mac dinh"); return rc; }
                if (switchTo != null) { int rc = DoSwitch(cfgPath, switchTo); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Da chuyen model thanh cong"); return rc; }
                if (renderOut != null) { int rc = DoRender(cfgPath, query, renderOut, renderRows); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Da render xong"); return rc; }
                if (renderCtl != null) { int rc = DoRenderControl(cfgPath, query, renderCtl, renderRows); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Da render control"); return rc; }
                if (shot != null) { int rc = DoShot(cfgPath, query, shot); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Da chup cua so"); return rc; }
                if (shotBackup != null) { int rc = DoShotBackup(cfgPath, shotBackup, multiSel); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Đã chụp cửa sổ bản sao lưu"); return rc; }
                if (shotConnect != null) { int rc = DoShotConnect(cfgPath, Router.Url, shotConnect); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Đã chụp cửa sổ kết nối"); return rc; }
                if (shotAgents != null) { int rc = DoShotAgents(cfgPath, shotAgents); if (string.IsNullOrEmpty(outFile)) ShowSummary(rc, "Đã chụp cửa sổ cấu hình vai trò"); return rc; }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOI: " + ex.Message);
                return 2;
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SwitcherForm(cfgPath));
            return 0;
        }

        static void ShowSummary(int code, string title)
        {
            string msg = code == 0 ? "OK" : "Co loi (ma " + code + ")";
            // KHONG bat MessageBox khi console bi chuyen huong: nguoi goi tool
            // tu dong lenh se bi treo vo hanh choi mot hop thoai.
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine((title == null ? "" : title + ": ") + msg);
                return;
            }
            MessageBox.Show(msg, title ?? "9Router", MessageBoxButtons.OK,
                code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        static void Usage()
        {
            Console.WriteLine("9Router Model Switcher");
            Console.WriteLine();
            Console.WriteLine("  ModelSwitcher.exe                       mo cua so chuyen model");
            Console.WriteLine("  ModelSwitcher.exe --switch <model>      chuyen model khong mo cua so");
            Console.WriteLine("  ModelSwitcher.exe --selftest            kiem tra bo so khop mo");
            Console.WriteLine("  ModelSwitcher.exe --check               kiem tra moi truong (chay sau khi Codex cap nhat)");
            Console.WriteLine("  ModelSwitcher.exe --default [model]     ngat 9Router, ve mac dinh cua Codex");
            Console.WriteLine("  ModelSwitcher.exe --render <a.png> [q]  ve danh sach ra file PNG");
            Console.WriteLine();
            Console.WriteLine("  --url <dia chi 9Router>   mac dinh http://localhost:20128");
            Console.WriteLine("  --config <file config>    mac dinh %CODEX_HOME%\\config.toml");
        }

        static int DoSwitch(string cfgPath, string model)
        {
            string backup;
            string keys = Cfg.SetModel(cfgPath, model, out backup);
            Console.WriteLine("Da chuyen model sang: " + model);
            Console.WriteLine("Cap nhat: " + keys);
            Console.WriteLine("Backup  : " + backup);
            Console.WriteLine("File    : " + cfgPath);
            return 0;
        }

        static int DoDefault(string cfgPath, string model)
        {
            string fb = (model == null ? "" : model.Trim());
            if (fb.Length == 0) fb = Connector.SuggestDefaultModel(cfgPath);
            List<string> log = new List<string>();
            int n = Connector.Unwire(cfgPath, fb, log);
            if (n == 0)
            {
                Console.WriteLine("Config khong con tro toi 9Router - khong co gi de lam.");
                return 0;
            }
            Console.WriteLine("Da ngat 9Router. Thay doi:");
            foreach (string s in log) Console.WriteLine("  - " + s);
            Console.WriteLine("Backup: " + Connector.LastBackup);
            Console.WriteLine("File  : " + cfgPath);
            return 0;
        }

        static int DoRender(string cfgPath, string query, string outPath, int rowCount)
        {
            if (rowCount < 1) rowCount = 1;
            SwitcherForm f = new SwitcherForm(cfgPath);
            f.PrepareForRender();
            int headH = 112, rowH = 40;
            int hits;
            using (Bitmap bmp = new Bitmap(840, headH + rowCount * rowH + 46))
            {
                hits = f.RenderTo(bmp, query, rowCount);
                bmp.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("Da render " + Math.Min(hits, rowCount) + " dong (" + hits + " ket qua / " + f.AllCount + " model) ra " + outPath);
            return 0;
        }
        static int DoShot(string cfgPath, string query, string outPath)
        {
            SwitcherForm f = new SwitcherForm(cfgPath);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.Show();
            f.BringToFront();
            f.Refresh();
            for (int k = 0; k < 12; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            Application.DoEvents();
            f.PrepareForRender();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                if (query != null) f.SetFilterForShot(query);
                f.RenderSelf(bmp);
                bmp.Save(outPath, ImageFormat.Png);
            }
            f.Close();
            Console.WriteLine("Da chup cua so chinh ra " + outPath);
            return 0;
        }

        static int DoShotConnect(string cfgPath, string url, string outPath)
        {
            ConnectForm f = new ConnectForm(cfgPath, url);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.Show();
            f.BringToFront();
            f.Refresh();
            for (int k = 0; k < 20; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.RenderSelf(bmp);
                bmp.Save(outPath, ImageFormat.Png);
            }
            f.Close();
            Console.WriteLine("Đã chụp cửa sổ kết nối ra " + outPath);
            return 0;
        }
        static int DoShotAgents(string cfgPath, string outPath)
        {
            List<ModelInfo> models = ModelCache.Load();
            if (models == null || models.Count == 0)
            {
                Console.WriteLine("LOI: chua co danh sach model. Bat 9Router roi chay --shot.");
                return 2;
            }
            AgentForm f = new AgentForm(cfgPath, models);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.Show();
            f.BringToFront();
            f.Refresh();
            for (int k = 0; k < 120; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); f.Refresh(); }
            Application.DoEvents();
            f.PrepareForShot();
            for (int k = 0; k < 120; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); f.Refresh(); }
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.RenderSelf(bmp);
                bmp.Save(outPath, ImageFormat.Png);
            }
            f.Close();
            Console.WriteLine("Da chup cua so cau hinh vai tro ra " + outPath
                + "   (" + models.Count + " model)");
            return 0;
        }


        static int DoShotBackup(string cfgPath, string outPath, bool multiSel)
        {
            BackupForm f = new BackupForm(cfgPath);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.Show();
            f.BringToFront();
            f.Refresh();
            for (int k = 0; k < 20; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            Application.DoEvents();
            if (multiSel) f.SelectFirstRows(5);
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.RenderSelf(bmp);
                bmp.Save(outPath, ImageFormat.Png);
            }
            f.Close();
            Console.WriteLine("Da chup cua so ban sao luu ra " + outPath);
            return 0;
        }
        // Kiem tra moi truong: dung sau khi Codex cap nhat de phat hien nghet dung gi.
        static int DoBackupTest(string cfgPath)
        {
            // Tao nhieu ban sao gia de danh sach du dong va co thanh cuon.
            string dir = Path.Combine(Path.GetTempPath(), "ms-bt-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            string cfg = Path.Combine(dir, "config.toml");
            File.WriteAllText(cfg, "model = \"gpt-5\"\n");
            string[] names = { "openai/gpt-5", "anthropic/claude-opus-5", "google/gemini-3-pro",
                "xai/grok-5", "deepseek/deepseek-v4", "meta/llama-5", "openai/gpt-5-mini",
                "anthropic/claude-sonnet-5", "google/gemini-3-flash", "mistral/mistral-large",
                "qwen/qwen-4-max", "openai/o5-pro" };
            for (int i = 0; i < names.Length; i++)
                File.WriteAllText(cfg + ".bak-2026090" + ((i % 9) + 1) + "-12000" + i, "model = \"" + names[i] + "\"\n");

            Console.WriteLine("=== Kiem tra danh sach ban sao (do tren MAN HINH that) ===");
            int rc = 0;
            try
            {
                SwitcherForm main = new SwitcherForm(cfg);
                main.StartPosition = FormStartPosition.Manual;
                main.Location = new Point(30, 30);
                main.Show();
                for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(120); }

                BackupForm bf = new BackupForm(cfg);
                bf.StartPosition = FormStartPosition.Manual;
                bf.Location = new Point(30, 30);
                bf.Show();
                // Do ngay o frame ve DAU TIEN, chua bam chuot, chua bam Lam moi.
                for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(120); }
                bf.BringToFront();
                Application.DoEvents();
                // Cho den khi cua so da bo cuc xong (danh sach co chieu cao that).
                // Neu do qua som, CHI DO CHUA BOC CUC chu chua ve, va bao LOI nham.
                for (int i = 0; i < 40 && bf.ListH < bf.RowHeight * 3; i++) {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                System.Threading.Thread.Sleep(200);

                int rows = bf.RowCount;
                int visible = Math.Min(rows, Math.Max(1, bf.ListH / bf.RowHeight));
                // Dem so dong CO THAT CHU (bo qua dong nen + dong dang chon).
                int textFirst = RowsWithText(bf, visible);
                Console.WriteLine("  so dong trong danh sach = " + rows);
                Console.WriteLine("  so dong dang hien       = " + visible);
                Console.WriteLine("  so dong co that chu     = " + textFirst);
                if (rows <= 0) { Console.WriteLine("  [LOI] danh sach rong"); rc |= 1; }
                if (textFirst < Math.Max(1, visible - 1)) {
                    Console.WriteLine("  [LOI] khong thay chu ngay khi moi mo canh sach");
                    rc |= 2;
                }

                // Cuon xuong: phai van thay chu
                if (rows > visible + 1) {
                    bf.ScrollList(visible);
                    for (int i = 0; i < 4; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(80); }
                    int textScroll = RowsWithText(bf, visible);
                    Console.WriteLine("  so dong co chu sau khi cuon = " + textScroll);
                    if (textScroll < Math.Max(1, visible - 1)) { Console.WriteLine("  [LOI] cuon xong khong thay chu"); rc |= 4; }
                    bf.ScrollList(-visible);
                    for (int i = 0; i < 3; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
                }

                // Chon nhieu dong: phai giu nguyen chu
                bf.SelectFirstRows(3);
                for (int i = 0; i < 3; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(80); }
                int textMulti = RowsWithText(bf, visible);
                Console.WriteLine("  so dong co chu khi chon nhieu = " + textMulti);
                if (textMulti < Math.Max(1, visible - 1)) { Console.WriteLine("  [LOI] chon nhieu dong lam mat chu"); rc |= 8; }

                // Loc theo ky tu: phai ra ket qua va van thay chu
                bf.SetFilterForTest("gpt");
                for (int i = 0; i < 3; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(80); }
                int shownFilter = Math.Min(bf.RowCount, Math.Max(1, bf.ListH / bf.RowHeight));
                int textFilter = RowsWithText(bf, shownFilter);
                Console.WriteLine("  so dong sau khi loc  = " + bf.RowCount
                    + "   co chu = " + textFilter);
                if (bf.RowCount <= 0) { Console.WriteLine("  [LOI] loc khong ra ket qua"); rc |= 16; }
                else if (textFilter < Math.Max(1, shownFilter - 1)) {
                    Console.WriteLine("  [LOI] loc xong khong thay chu"); rc |= 32;
                }
                bf.SetFilterForTest("");
                for (int i = 0; i < 3; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(80); }

                bf.Close();
                main.Close();
            }
            catch (Exception ex) { Console.WriteLine("  [LOI] " + ex.Message); rc |= 64; }
            finally { try { Directory.Delete(dir, true); } catch { } }
            Console.WriteLine(rc == 0 ? "  KET QUA: DUNG" : "  KET QUA: CO LOI (" + rc + ")");
            return rc;
        }
        // Dem so dong nao tren MAN HINH that co that chu.
        // KHONG dung DrawToBitmap: no ep ve lai nen luon thay chu va khong
        // bat duoc loi that. KHONG dem "so diem toi" ca vung: dong dang chon
        // co nen mau, dem nho do se bao PASS gia.
        static int RowsWithText(BackupForm f, int maxRows)
        {
            if (maxRows < 1) maxRows = 1;
            Rectangle list = f.ListScreenRect;      // toa do MAN HINH that
            if (list.Width <= 0 || list.Height <= 0) return 0;
            int rowH = Math.Max(8, f.RowHeight);
            int found = 0;
            for (int i = 0; i < maxRows; i++)
            {
                Rectangle row = new Rectangle(list.X + 3, list.Y + 2 + i * rowH,
                    Math.Min(list.Width - 8, 520), rowH - 3);
                row.Intersect(list);
                if (row.Width < 20 || row.Height < 8) break;
                using (Bitmap bmp = new Bitmap(row.Width, row.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(row.Location, Point.Empty, row.Size);
                    int n = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (255 - c.R > 60 || 255 - c.G > 60 || 255 - c.B > 60) n++;
                    }
                    if (n > 200) found++;              // it nhat 200 diem toi = co chu
                }
            }
            return found;
        }
        static int DoCheck(string cfgPath)
        {
            int ok = 0, bad = 0;
            Console.WriteLine();
            Console.WriteLine("  Kiểm tra môi trường — chạy sau mỗi lần Codex cập nhật");
            Console.WriteLine("  --------------------------------------------------------");
            Console.WriteLine();

            // 1) 9Router
            try {
                List<ModelInfo> all = Router.Load();
                int nc = 0; foreach (ModelInfo m in all) if (m.IsCombo) nc++;
                Console.WriteLine("  [OK]   9Router tai " + Router.Url + " - " + all.Count + " model, " + nc + " combo");
                ok++;
            } catch (Exception ex) {
                Console.WriteLine("  [LỖI]  Không gọi được 9Router tại " + Router.Url);
                Console.WriteLine("         Hãy bật 9Router trước khi mở tool này.");
                Console.WriteLine("         " + ex.Message);
                bad++;
            }

            // 2) config.toml
            if (File.Exists(cfgPath)) {
                Console.WriteLine("  [OK]   config.toml: " + cfgPath);
                ok++;
                string cur = Cfg.Current(cfgPath);
                if (!string.IsNullOrEmpty(cur)) {
                    Console.WriteLine("  [OK]     co dong model = \"" + cur + "\"");
                    ok++;
                } else {
                    Console.WriteLine("  [LOI]   KHONG tim thay dong 'model' trong config.toml");
                    Console.WriteLine("         Tool sẽ không chuyển được model. Nếu Codex đổi cấu hình, báo lại để sửa code.");
                    bad++;
                }
                bool hasAgent = false;
                foreach (string ln in File.ReadAllLines(cfgPath))
                    if (ln.TrimStart().StartsWith("default_subagent_model")) { hasAgent = true; break; }
                if (hasAgent) { Console.WriteLine("  [OK]     có dòng default_subagent_model"); ok++; }
                else { Console.WriteLine("  [--]   không có dòng default_subagent_model (bỏ qua, vẫn chuyển được)"); }
            } else {
                Console.WriteLine("  [LỖI]  Không tìm thấy config.toml: " + cfgPath);
                Console.WriteLine("         Hãy chạy Codex một lần để tạo file này, hoặc dùng --config để chỉ định đường dẫn.");
                bad++;
            }

            // 3) codex.exe
            string codexPath = null;
            try {
                foreach (string pth in Environment.GetEnvironmentVariable("PATH").Split(';'))
                    if (!string.IsNullOrEmpty(pth) && File.Exists(Path.Combine(pth, "codex.exe"))) { codexPath = Path.Combine(pth, "codex.exe"); break; }
            } catch { }
            if (codexPath != null) { Console.WriteLine("  [OK]   codex.exe: " + codexPath); ok++; }
            else { Console.WriteLine("  [--]   không tìm thấy codex.exe trong PATH — nút 'Mở Codex CLI' sẽ không dùng được"); }

            // 4) .NET Framework
            string v = Environment.Version.ToString();
            Console.WriteLine("  [OK]   .NET Framework " + v);

            Console.WriteLine();
            Console.WriteLine("  Kết quả: " + ok + " OK, " + bad + " lỗi");
            return bad;
        }
        // Ve danh sach bang CHINH control ListBox (DrawToBitmap) de bat loi "khong gan DrawItem".
        static int DoRenderControl(string cfgPath, string query, string outPath, int rowCount)
        {
            if (rowCount < 1) rowCount = 6;
            SwitcherForm f = new SwitcherForm(cfgPath);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.Show();
            f.BringToFront();
            f.Refresh();
            for (int k = 0; k < 20; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            Application.DoEvents();
            f.PrepareForRender();
            int listW = 800;
            int showRows = Math.Min(rowCount, 12);
            int listH = showRows * 40;
            int headH = 112;
            int ink; int nrows; Color box;
            Bitmap listBmp = new Bitmap(listW, listH);
            f.RenderRealList(listBmp, query, listW, listH);
            nrows = f.RowCount; box = f.BoxColor;
            ink = CountInk(listBmp, box);
            using (Bitmap bmp = new Bitmap(listW + 32, headH + listH + 46))
            {
                Graphics g = Graphics.FromImage(bmp);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(f.FormColor);
                g.DrawString("9Router  ·  Chuyen model", f.HeadFont, f.HeadBrush, new PointF(16, 10));
                g.DrawString(f.CurrentModel == null ? "Khong doc duoc model hien tai" : "Model dang dung: " + f.CurrentModel, f.UiFont, f.CurBrush, new PointF(18, 42));
                using (SolidBrush fb = new SolidBrush(f.BoxColor)) g.FillRectangle(fb, new Rectangle(16, 70, listW, 30));
                using (Pen pen = new Pen(f.DimColor)) g.DrawRectangle(pen, new Rectangle(16, 70, listW, 30));
                g.DrawString(query ?? "", f.UiBoldFont, f.FgBrush, new PointF(21, 76));
                g.DrawImage(listBmp, new Rectangle(16, headH, listW, listH));
                g.DrawString("hien " + nrows + " ket qua   ·   se chuyen sang:  " + f.FirstId, f.UiFont, f.DimBrush, new PointF(16, headH + listH + 8));
                g.Dispose();
                bmp.Save(outPath, ImageFormat.Png);
            }
            listBmp.Dispose();
            f.Close();
            int itemCount = f.ListItemCount;
            Console.WriteLine("Danh sach that cua ListBox: " + nrows + " dong, " + itemCount + " dong trong control, " + ink + " pixel chu");
            Console.WriteLine("Thoi gian: tim kiem " + f.LastSearchMs + " ms, nap danh sach " + f.LastFillMs + " ms");
            if (itemCount != nrows) { Console.WriteLine("LOI: ListBox chi chua " + itemCount + "/" + nrows + " dong"); return 5; }
            if (nrows == 0) { Console.WriteLine("LOI: khong co dong nao trong danh sach"); return 3; }
            if (ink < 50) { Console.WriteLine("LOI: ListBox ve ra TRONG - thieu su kien DrawItem hoac khong ve duoc chu"); return 4; }
            Console.WriteLine("OK: da ve danh sach that cua ListBox ra " + outPath);
            return 0;
        }

        // Dem pixel khac mau nen: 0 nghia la khong co gi duoc ve.
        static int CountInk(Bitmap bmp, Color bg)
        {
            int ink = 0;
            for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x += 1)
            {
                Color c = bmp.GetPixel(x, y);
                if (Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B) > 60) ink++;
            }
            return ink;
        }

        // ------------------ kiem tra bo so khop mo ------------------
        static void Check(bool ok, string label, string detail)
        {
            if (ok) { pass++; Console.WriteLine("  [PASS] " + label); }
            else { fail++; Console.WriteLine("  [FAIL] " + label); }
            if (!string.IsNullOrEmpty(detail)) Console.WriteLine("         " + detail);
        }

        static int SelfTest()
        {
            Console.WriteLine();
            Console.WriteLine("  Kiem tra bo so khop mo");
            Console.WriteLine("  ----------------------");
            List<ModelInfo> all = Router.Load();
            int nCombo = 0;
            foreach (ModelInfo m in all) if (m.IsCombo) nCombo++;
            Console.WriteLine("  Tai duoc " + all.Count + " model, gom " + nCombo + " combo");
            Console.WriteLine();

            Check(all.Count > 0, "Tai duoc danh sach model", all.Count + " model tu " + Router.Url);
            Check(nCombo >= 1, "Danh sach co chua combo", nCombo + " combo: " + JoinCombo(all));

            string[] queries = { "o", "op", "ope", "openr", "bunny", "hung", "qwen3.8", "antigra", "deepseek", "zzz", "sb", "gpt-5", "nemo" };
            int badSub = 0, badPos = 0, nChecked = 0;
            foreach (string q in queries)
            {
                foreach (Hit h in Matcher.Search(all, q))
                {
                    nChecked++;
                    if (!Matcher.IsSubsequence(h.M.Id, q)) badSub++;
                    if (h.Pos.Length != q.Length) { badPos++; continue; }
                    string il = h.M.Lower, ql = q.ToLowerInvariant();
                    for (int i = 0; i < ql.Length; i++) {
                        if (h.Pos[i] >= il.Length || il[h.Pos[i]] != ql[i]) { badPos++; break; }
                        if (i > 0 && h.Pos[i] <= h.Pos[i - 1]) { badPos++; break; }
                    }
                }
            }
            Check(badSub == 0, "Moi ket qua deu khop ky tu theo dung thu tu",
                "kiem tra " + nChecked + " ket qua tren " + queries.Length + " truy van, sai: " + badSub);
            Check(badPos == 0, "Vi tri highlight dung va tang dan", "kiem tra " + nChecked + " ket qua, sai: " + badPos);

            string[] grow = { "s", "sp", "spa", "spac", "space", "space-b", "space-bu", "space-bun" };
            int prev = int.MaxValue; bool narrow = true;
            StringBuilder sb = new StringBuilder();
            foreach (string q in grow) {
                int c = Matcher.Search(all, q).Count;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append("'" + q + "' -> " + c);
                if (c > prev) narrow = false;
                prev = c;
            }
            Check(narrow, "Ket qua thu hep dan khi giao them ky tu", sb.ToString());

            string[,] top = {
                { "hung", "Hung" }, { "antigra", "Antigra" }, { "openrouter", "Openrouter" },
                { "deepseek", "Deepseek" }, { "bunny", "*bunny*" }, { "qwen3.8", "*qwen3.8*" }
            };
            int skip = 0;
            for (int i = 0; i < top.GetLength(0); i++) {
                string want = top[i, 1];
                bool fuzzy = want.StartsWith("*") && want.EndsWith("*");
                // Moi tai khoan 9Router co bo model khac nhau. Neu tai khoan nay
                // khong co dung model thi BO QUA - bao loi gia thi lam test vo dung.
                bool have = false;
                if (fuzzy)
                {
                    have = Matcher.Search(all, top[i, 0]).Count > 0;
                }
                else
                {
                    foreach (ModelInfo m in all)
                        if (string.Equals(m.Id, want, StringComparison.OrdinalIgnoreCase)) { have = true; break; }
                }
                if (!have)
                {
                    skip++;
                    Console.WriteLine("  [SKIP] " + top[i, 0] + ": tai khoan nay khong co model nay");
                    continue;
                }
                List<Hit> r = Matcher.Search(all, top[i, 0]);
                string best = r.Count > 0 ? r[0].M.Id : "(rong)";
                bool ok = r.Count > 0 && Wild(best, want);
                Check(ok, "Xep hang dung cho '" + top[i, 0] + "'", "top1 = " + best + "  (" + r.Count + " ket qua)");
            }
            if (skip > 0) Console.WriteLine("         bo qua " + skip + " muc xep hang vi tai khoan nay khong co model tuong ung");

            int miss = 0;
            foreach (ModelInfo cb in all) {
                if (!cb.IsCombo) continue;
                bool found = false;
                foreach (Hit h in Matcher.Search(all, cb.Id)) if (h.M.Id == cb.Id) { found = true; break; }
                if (!found) miss++;
            }
            Check(miss == 0, "Tim theo ten combo luon ra chinh combo do", "thieu: " + miss);

            // ---- Kiem tra ListBox that su (co bao ve finally de khong treo) ----
            int listAll = 0;
            SwitcherForm chk2 = null;
            try {
                chk2 = new SwitcherForm(Cfg.DefaultPath());
                chk2.Show(); Application.DoEvents(); chk2.PrepareForRender();
                using (Bitmap bb2 = new Bitmap(700, 200))
                {
                    chk2.RenderRealList(bb2, "", 700, 200);
                    listAll = chk2.ListItemCount;
                }
            } finally { if (chk2 != null) chk2.Dispose(); }
            Check(listAll == all.Count, "Không còn giới hạn 300 dòng khi không lọc",
                "ListBox chứa " + listAll + " / " + all.Count + " dòng");

            // Bắt lỗi thực tế: ListBox đặt OwnerDrawFixed nhưng thiếu DrawItem sẽ vẽ ra trống.
            int inkReal = 0, rowsReal = 0;
            SwitcherForm chk = null;
            try {
                chk = new SwitcherForm(Cfg.DefaultPath());
                chk.Show(); Application.DoEvents(); chk.PrepareForRender();
                using (Bitmap bb = new Bitmap(700, 120))
                {
                    chk.RenderRealList(bb, "qwen3", 700, 120);
                    rowsReal = chk.RowCount; inkReal = CountInk(bb, chk.BoxColor);
                }
            } finally { if (chk != null) chk.Dispose(); }
            Check(rowsReal > 0 && inkReal >= 50, "ListBox thật sự vẽ tên model (không trống)",
                rowsReal + " dòng, " + inkReal + " pixel chữ");
            List<Hit> empty = Matcher.Search(all, "");
            int emptyCombo = 0;
            foreach (Hit h in empty) if (h.M.IsCombo) emptyCombo++;
            Check(emptyCombo == nCombo, "Loc rong van hien day du combo", nCombo + " combo dung dau: " + TopIds(empty, 4));

            int lost = 0, sample = 0;
            for (int i = 0; i < all.Count; i += 4) {
                sample++;
                bool found = false;
                foreach (Hit h in Matcher.Search(all, all[i].Id)) if (h.M.Id == all[i].Id) { found = true; break; }
                if (!found) lost++;
            }
            Check(lost == 0, "Go dung ten model thi luon tim thay chinh no", "mau " + sample + " model, khong tim thay: " + lost);

            // ---- Kiem tra chuc nang TU DONG NOI 9Router voi Codex ----
            // config.toml moi: chua co gi, phai duoc them provider + header
            string t2 = Path.Combine(Path.GetTempPath(), "ms-conn-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(t2);
            string c2 = Path.Combine(t2, "config.toml");
            int hasBefore = 0, wireOk = 0, hasAfter = 0, fullOk = 0, hasBackup = 0, hasHeader = 0, idem = 0;
            string keyAuto = null;
            try {
                File.WriteAllText(c2, "model = \"Alpha\"\nmodel_provider = \"9router\"\n");
                hasBefore = Connector.HasProvider(c2) ? 1 : 0;
                string url2 = "http://127.0.0.1:20128";
                string key2 = "sk-test-1234567890abcdefghij";
                string bak2 = Connector.Wire(c2, url2, key2);
                string txt2 = File.ReadAllText(c2);
                hasAfter = Connector.HasProvider(c2) ? 1 : 0;
                fullOk = Connector.IsFullyWired(c2, url2) ? 1 : 0;
                hasHeader = txt2.Contains("[model_providers.9router.http_headers]") ? 1 : 0;
                hasBackup = File.Exists(bak2) ? 1 : 0;
                if (bak2 != null && bak2.EndsWith(".bak-20260101-101010") == false) hasBackup = 1;
                idem = (Connector.Wire(c2, url2, key2) == "provider đã có sẵn") ? 1 : 0;
                if (!txt2.Contains("model = \"Alpha\"")) wireOk = 0; else wireOk = 1;
            } catch { }
            finally { try { System.IO.Directory.Delete(t2, true); } catch { } }
            keyAuto = Connector.AutoDetectKey();
            Check(hasBefore == 0 && hasAfter == 1 && fullOk == 1 && hasHeader == 1 && hasBackup == 1,
                "Tự nối: thêm provider + http_headers + url, có backup",
                "trước=" + hasBefore + " sau=" + hasAfter + " đầy đủ=" + fullOk + " header=" + hasHeader + " backup=" + hasBackup);
            Check(wireOk == 1 && idem == 1, "Tự nối: giữ nguyên model cũ, gọi lại không nhân bản",
                "giữ model=" + wireOk + " idempotent=" + idem);
            Check(keyAuto != null && keyAuto.Length > 20, "Tự dò API key từ cơ sở dữ liệu 9Router",
                (keyAuto == null) ? "không lấy được (máy này không có 9Router)" : "đã lấy " + keyAuto.Length + " ký tự");

            // ---- Máy này đã nối chưa (kiểm tra trên config thật, chỉ đọc) ----
            string realCfg = Cfg.DefaultPath();
            bool realHas = File.Exists(realCfg) && Connector.HasProvider(realCfg);
            bool realFull = File.Exists(realCfg) && Connector.IsFullyWired(realCfg, Router.Url);
            Check(realHas, "Máy này đã có khai báo 9Router trong config.toml",
                realCfg + "  ->  hasProvider=" + realHas + " wired=" + realFull);

            // localhost và 127.0.0.1 là cùng một máy — phải coi là giống nhau
            string tmpN = Path.Combine(Path.GetTempPath(), "ms-norm-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".toml");
            int normOk = 0;
            try {
                File.WriteAllText(tmpN, "model = \"X\"\n[model_providers.9router]\nbase_url = \"http://127.0.0.1:20128/v1\"\n");
                normOk = (Connector.IsFullyWired(tmpN, "http://localhost:20128")
                         && Connector.IsFullyWired(tmpN, "http://127.0.0.1:20128")) ? 1 : 0;
            } catch { }
            finally { try { File.Delete(tmpN); } catch { } }
            Check(normOk == 1, "Nhận cả localhost lẫn 127.0.0.1 là đã kết nối", "normOk=" + normOk);

            // ---- Danh sach mo hinh va danh sach ban sao dung CHUNG kieu dong ----
            int rowMismatch = 0, inkMo = 0, inkSauClick = 0, multiErr = 0;
            try {
                SwitcherForm sw = new SwitcherForm(Cfg.DefaultPath());
                sw.PrepareForRender();
                if (sw.RowHeight != RowPainter.RowH) rowMismatch++;

                BackupForm bk = new BackupForm(Cfg.DefaultPath());
                bk.Show(); Application.DoEvents();
                if (bk.RowHeight != RowPainter.RowH) rowMismatch++;

                inkMo = bk.InkInList();
                bk.SimulateClick(0);
                inkSauClick = bk.InkInList();
                bk.SelectFirstRows(4);
                if (bk.RowCount > 0) bk.SimulateClick(1);
                if (bk.InkInList() < 400) multiErr++;
                bk.Close(); sw.Dispose();
            } catch { multiErr++; }
            // Bo cuc thuc cua danh sach: phai lap day khung, khong duoc de o kich thuoc mac dinh 120x96.
            int layoutBad = 0;
            string layoutInfo = "khong do duoc";
            BackupForm bl = null;
            try {
                bl = new BackupForm(Cfg.DefaultPath());
                bl.Show();
                for (int k = 0; k < 12; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
                if (bl.ListW < 400 || bl.ListH < 200) layoutBad++;
                layoutInfo = "ListBox = " + bl.ListW + "x" + bl.ListH + " tai (" + bl.ListX + "," + bl.ListY + ")";
            } catch { layoutBad++; layoutInfo = "khong mo duoc cua so"; }
            finally { if (bl != null) bl.Dispose(); }
            Check(layoutBad == 0, "Danh sach bản sao lấp đầy khung cửa sổ", layoutInfo);

            Check(rowMismatch == 0,
                "Danh sach ban sao dung chung kieu dong voi danh sach mo hinh",
                "chieu cao dong khop nhau, RowH=" + RowPainter.RowH);
            Check(inkMo >= 400 && inkSauClick >= 400,
                "Chu van hien sau khi bam chuot that vao dong dau",
                "luc mo=" + inkMo + " px, sau khi bam=" + inkSauClick + " px");
            Check(multiErr == 0, "Chon nhieu ban sao van giu nguyen chu", "loi=" + multiErr);



            // ---- Kiem tra chuc nang quan ly ban sao (chay tren ban sao tam) ----
            string tmpDir = Path.Combine(Path.GetTempPath(), "ms-bak-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(tmpDir);
            string tmpCfg = Path.Combine(tmpDir, "config.toml");
            int bakCount = 0, listOk = 0, restoreOk = 0, deleteOk = 0, safetyNew = 0;
            try {
                File.WriteAllText(tmpCfg, "model = \"Alpha\"\ndefault_subagent_model = \"Alpha\"\n");
                string b1 = tmpCfg + ".bak-20260101-101010";
                string b2 = tmpCfg + ".bak-20260102-202020";
                File.WriteAllText(b1, "model = \"Beta\"\n");
                File.WriteAllText(b2, "model = \"Gamma\"\n");
                File.WriteAllText(Path.Combine(tmpDir, "khong-phai-backup.txt"), "x");

                List<BackupInfo> bl2 = Cfg.ListBackups(tmpCfg);
                bakCount = bl2.Count;
                if (bakCount == 2 && bl2[0].Model == "Gamma" && bl2[1].Model == "Beta") listOk = 1;

                string safety = Cfg.Restore(tmpCfg, b1);
                if (Cfg.Current(tmpCfg) == "Beta" && File.Exists(safety)) restoreOk = 1;
                if (!safety.EndsWith(".bak-20260101-101010")) safetyNew = 1;

                Cfg.DeleteBackup(b1, tmpCfg);
                if (!File.Exists(b1)) deleteOk = 1;
            } catch { }
            finally { try { System.IO.Directory.Delete(tmpDir, true); } catch { } }

            Check(listOk == 1, "Doc danh sach ban sao (chi .bak-, moi nhat truoc)", "thay " + bakCount + " ban sao");
            Check(restoreOk == 1 && safetyNew == 1, "Khoi phuc: ghi de config + luu ban sao an toan", "restoreOk=" + restoreOk + " safetyMoi=" + safetyNew);
            Check(deleteOk == 1, "Xoa ban sao that tren dia", "deleteOk=" + deleteOk);

            // ---- NGAT KET NOI: xoa 9Router, ve mac dinh cua Codex ----
            // Dung file config GIA trong thu muc tam, khong cham file that.
            string udir = Path.Combine(Path.GetTempPath(), "ms-uw-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(udir);
            try
            {
                string ucfg = Path.Combine(udir, "config.toml");
                File.WriteAllLines(ucfg, new string[] {
                    "model = \"Openrouter\"",
                    "model_provider = \"9router\"",
                    "model_reasoning_effort = \"minimal\"",
                    "sandbox_mode = \"danger-full-access\"",
                    "",
                    "[desktop]",
                    "localeOverride = \"vi-VN\"",
                    "",
                    "[mcp_servers.node_repl]",
                    "command = 'x'",
                    "",
                    "[model_providers.9router]",
                    "name = \"9Router\"",
                    "base_url = \"http://127.0.0.1:20128/v1\"",
                    "wire_api = \"responses\"",
                    "",
                    "[model_providers.9router.http_headers]",
                    "Authorization = \"Bearer sk-test\"",
                    "",
                    "[agents]",
                    "default_subagent_model = \"Openrouter\"",
                    "" });

                Check(Connector.IsWired(ucfg), "Ngat: config mau dang tro toi 9Router", null);
                Check(Connector.ProviderNameOf(ucfg) == "9router", "Ngat: doc dung ten provider",
                    "provider=" + Connector.ProviderNameOf(ucfg));

                List<string> ulog = new List<string>();
                int un = Connector.Unwire(ucfg, "", ulog);
                string ua = File.ReadAllText(ucfg);
                Check(un == 1, "Ngat: bao co da thay doi", "Unwire=" + un);
                Check(ua.IndexOf("[model_providers.9router", StringComparison.OrdinalIgnoreCase) < 0
                      && ua.IndexOf("9router", StringComparison.OrdinalIgnoreCase) < 0,
                    "Ngat: xoa sach khoi 9Router", null);
                Check(ua.IndexOf("model_provider", StringComparison.Ordinal) < 0
                      && ua.IndexOf("model = ", StringComparison.Ordinal) < 0
                      && ua.IndexOf("default_subagent_model", StringComparison.Ordinal) < 0,
                    "Ngat: bo model de Codex tu chon", null);
                Check(ua.Contains("sandbox_mode = \"danger-full-access\"")
                      && ua.Contains("model_reasoning_effort = \"minimal\"")
                      && ua.Contains("localeOverride = \"vi-VN\"")
                      && ua.Contains("mcp_servers.node_repl"),
                    "Ngat: giu nguyen cau hinh khac cua nguoi dung", null);
                Check(!Connector.IsWired(ucfg) && Connector.ProviderNameOf(ucfg).Length == 0,
                    "Ngat: config da ve mac dinh", null);
                Check(Directory.GetFiles(udir, "config.toml.bak-*").Length == 1,
                    "Ngat: co tao ban sao luu truoc khi ghi", null);
                Check(Connector.Unwire(ucfg, "", null) == 0,
                    "Ngat: goi lai khong lam gi", null);

                // Chi dinh model cu the -> gan model_provider = openai
                string ucfg2 = Path.Combine(udir, "config2.toml");
                File.WriteAllLines(ucfg2, new string[] {
                    "model = \"Nvidia/nemotron\"", "model_provider = \"9router\"", "",
                    "[model_providers.9router]", "name = \"9Router\"", "",
                    "[agents]", "default_subagent_model = \"Nvidia/nemotron\"", "" });
                Connector.Unwire(ucfg2, "gpt-5.1-codex", null);
                string u2 = File.ReadAllText(ucfg2);
                Check(u2.Contains("model = \"gpt-5.1-codex\"")
                      && u2.Contains("model_provider = \"openai\"")
                      && u2.Contains("default_subagent_model = \"gpt-5.1-codex\"")
                      && u2.IndexOf("[model_providers.9router", StringComparison.OrdinalIgnoreCase) < 0,
                    "Ngat: chi dinh model cu the cua Codex", null);

                // Khoa "model" trong bang con phai giu nguyen
                string ucfg3 = Path.Combine(udir, "config3.toml");
                File.WriteAllLines(ucfg3, new string[] {
                    "model = \"Openrouter\"", "model_provider = \"9router\"", "",
                    "[profile.work]", "model = \"gpt-5.1-codex\"", "",
                    "[model_providers.9router]", "name = \"9Router\"", "" });
                Connector.Unwire(ucfg3, "gpt-5.1-codex", null);
                string u3 = File.ReadAllText(ucfg3);
                Check(u3.Contains("[profile.work]") && u3.Contains("model = \"gpt-5.1-codex\"")
                      && u3.Contains("model_provider = \"openai\""),
                    "Ngat: khong dong vao khoa 'model' trong bang con", null);
            }
            finally { try { Directory.Delete(udir, true); } catch { } }



            // ---- Kiem tra che do nhieu agent (chay tren config tam) ----
            string rdir = Path.Combine(Path.GetTempPath(), "ms-role-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(rdir);
            string rcfg = Path.Combine(rdir, "config.toml");
            try
            {
                File.WriteAllLines(rcfg, new string[] {
                    "model = \"Leader-A\"", "model_provider = \"9router\"", "",
                    "[agents]", "default_subagent_model = \"Leader-A\"", "",
                    "[mcp_servers.node_repl]", "command = \"node\"", "",
                    "[model_providers.9router]", "name = \"9Router\"",
                    "base_url = \"http://127.0.0.1:20128/v1\"", "wire_api = \"responses\"", "",
                    "[model_providers.9router.http_headers]", "Authorization = \"Bearer test-key\"", "" });
                string bakR;

                Check(!Roles.Exists(rcfg),
                    "Nhieu agent: config chua co vai tro = che do 1 agent", null);
                Check(Roles.Read(rcfg)[0].Model == "Leader-A",
                    "Nhieu agent: doc duoc model cua leader", Roles.Read(rcfg)[0].Model);
                Check(!Roles.IsFlatKey("architect") && Roles.IsFlatKey("max_depth")
                       && Roles.IsFlatKey("default_subagent_model"),
                    "Nhieu agent: phan biet khoa chung voi ten vai tro", null);

                // Leader + 2 vai tro, moi cai mot model rieng
                List<RoleDef> rr = Roles.Read(rcfg);
                rr.Add(new RoleDef { Name = "architect", Model = "Arch-B", Description = "Thiet ke" });
                rr.Add(new RoleDef { Name = "qa", Model = "QA-C" });

                // [agents.x] chi tro toi file .toml -> doi CODEX_HOME TRUOC khi ghi
                // de khong bao gio chi vao ~/.codex that.
                string oldHome = Environment.GetEnvironmentVariable("CODEX_HOME");
                Environment.SetEnvironmentVariable("CODEX_HOME", rdir);
                try
                {
                    Roles.Write(rcfg, rr);
                    Roles.WriteFile(rcfg, rr[1]);
                    Roles.WriteFile(rcfg, rr[2]);
                }
                finally { Environment.SetEnvironmentVariable("CODEX_HOME", oldHome); }

                string rw = File.ReadAllText(rcfg);
                Check(Roles.Exists(rcfg), "Nhieu agent: ghi xong co vai tro", null);
                Check(rw.Contains("[agents.architect]") && rw.Contains("[agents.qa]")
                       && rw.Contains("description = \"Thiet ke\"") && rw.Contains("config_file = "),
                    "Nhieu agent: ghi duoc bang [agents.<ten>]", null);
                Check(rw.Contains("[mcp_servers.node_repl]") && rw.Contains("[model_providers.9router]")
                       && rw.Contains("Bearer test-key") && rw.Contains("default_subagent_model"),
                    "Nhieu agent: giu nguyen phan cau hinh khac cua nguoi dung", null);

                List<RoleDef> r2 = Roles.Read(rcfg);
                Check(r2.Count == 3 && r2[1].Name == "architect" && r2[2].Name == "qa"
                       && r2[1].Description == "Thiet ke"
                       && r2[1].Model == "Arch-B" && r2[2].Model == "QA-C",
                    "Nhieu agent: doc lai dung so vai tro", "doc=" + r2.Count);

                string rf = File.ReadAllText(Path.Combine(rdir, "roles", "architect.toml"));
                Check(rf.Contains("model = \"Arch-B\"")
                       && rf.Contains("model_provider = \"9router\"")
                       && rf.Contains("wire_api = \"responses\"")
                       && rf.Contains("Bearer test-key")
                       && rf.Contains("127.0.0.1:20128")
                       && rf.Contains("developer_instructions"),
                    "Nhieu agent: file vai tro co model + provider + bearer", null);

                // Doi model leader: KHONG duoc keo default_subagent_model ve bang model leader
                Cfg.SetModel(rcfg, "Leader-Moi", out bakR);
                string r3 = File.ReadAllText(rcfg);
                Check(r3.Contains("model = \"Leader-Moi\"")
                       && r3.Contains("default_subagent_model = \"Leader-A\"")
                       && r3.Contains("[agents.architect]"),
                    "Nhieu agent: doi model leader khong pha vai tro", null);

                // Xoa het vai tro -> quay ve che do 1 agent
                List<RoleDef> r4 = new List<RoleDef>();
                r4.Add(r2[0]); r4[0].Model = "Leader-Moi";
                Roles.Write(rcfg, r4);
                Check(!Roles.Exists(rcfg) && !File.ReadAllText(rcfg).Contains("[agents.qa]"),
                    "Nhieu agent: xoa het vai tro = tro ve che do 1 agent", null);

                // Nut "1 agent": goi het vai tro nhung giu model leader + cau hinh khac
                List<RoleDef> r5 = Roles.Read(rcfg);
                r5.Add(new RoleDef { Name = "coder", Model = "Coder-D" });
                Roles.Write(rcfg, r5);
                Check(Roles.Exists(rcfg), "Nhieu agent: co vai tro truoc khi tat", null);
                Roles.Disable(rcfg);
                string r6 = File.ReadAllText(rcfg);
                Check(!Roles.Exists(rcfg) && r6.Contains("model = \"Leader-Moi\"")
                       && r6.Contains("default_subagent_model = \"Leader-A\"")
                       && r6.Contains("[mcp_servers.node_repl]")
                       && !r6.Contains("[agents.coder]") && !r6.Contains("[agents.architect]"),
                    "Nhieu agent: nut 1 agent goi het vai tro, giu nguyen phan khac", null);

                // Che do 1 agent: giu nguyen hanh vi cu
                File.WriteAllLines(rcfg, new string[] {
                    "model = \"Alpha\"", "default_subagent_model = \"Alpha\"", "" });
                Cfg.SetModel(rcfg, "Beta", out bakR);
                Check(File.ReadAllText(rcfg).Contains("default_subagent_model = \"Beta\""),
                    "Nhieu agent: che do 1 agent giu nguyen hanh vi cu", null);

                // ---- Cua so cau hinh vai tro: phai hien HET model + loc nhu cua so "1 agent" ----
                AgentForm af = null;
                try
                {
                    List<ModelInfo> fake = new List<ModelInfo>();
                    string[] mn = { "openrouter/free/model-alpha", "openrouter/free/model-beta",
                        "orca/anthropic/claude-opus-5", "gemini/gemini-3.1-pro",
                        "unofree/glm-5.3-flash-thinking:free", "openai/gpt-5" };
                    for (int i = 0; i < mn.Length; i++)
                        fake.Add(new ModelInfo { Id = mn[i], Owner = "test", IsCombo = false,
                            Ctx = 128000, Lower = mn[i].ToLowerInvariant() });
                    af = new AgentForm(rcfg, fake);

                    Check(af.TotalModels == 6,
                        "Cu so vai tro: nap duoc danh sach model", "n=" + af.TotalModels);
                    Check(af.TestFilter("") == 6,
                        "Cu so vai tro: o loc rong = hien HET model", "hien=" + af.TestFilter(""));
                    int nAlpha = af.TestFilter("alpha");
                    Check(nAlpha == 1 && nAlpha < 6,
                        "Cu so vai tro: go chu loc ra duy nhat 1 dong", "hien=" + nAlpha);
                    Check(af.TestFilter("gemini") == 1 && af.TestFilter("openai") == 1,
                        "Cu so vai tro: loc theo ten model", null);
                    Check(af.TestFilter("") == 6,
                        "Cu so vai tro: xoa o loc thi thay lai HET model", null);
                    Check(af.TestFilter("khong-ton-tai") == 0,
                        "Cu so vai tro: loc khong khop = danh sach rong", null);

                    int ink = AgentForm.MeasureRowInk("openai/gpt-5");
                    Size ls = af.ModelListSize;
                    Check(ls.Width > 300 && ls.Height > 100,
                        "Cu so vai tro: khung danh sach model co kich thuoc that",
                        ls.Width + "x" + ls.Height);
                    Check(af.ModelListRows >= 3,
                        "Cu so vai tro: danh sach model hien duoc it nhat 3 dong",
                        "rows=" + af.ModelListRows);

                    // ---- Regex phai la verbatim @"..." : "\s" trong chuoi "..."
                //    la dau gach cheo, KHONG phai khoang trang -> quet sai.
                string rxCfg = Path.Combine(Path.GetTempPath(),
                    "ms-rx-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".toml");
                File.WriteAllLines(rxCfg, new string[] {
                    "model = \"Alpha\"", "model_provider = \"9router\"",
                    "model_reasoning_effort = \"low\"", "",
                    "[agents]", "default_subagent_model = \"Alpha\"", "" });
                Cfg.SetModel(rxCfg, "Beta", out bakR);
                string rxTxt = File.ReadAllText(rxCfg);
                Check(rxTxt.Contains("model = \"Beta\"")
                       && rxTxt.Contains("default_subagent_model = \"Beta\""),
                    "Che do 1 agent: doi model keo ca default_subagent_model theo",
                    rxTxt.Replace("\r", "").Replace("\n", " | "));
                Cfg.SetReasoning(rxCfg, "max", out bakR);
                string rxTxt2 = File.ReadAllText(rxCfg);
                Check(rxTxt2.Contains("model_reasoning_effort = \"max\"")
                       && rxTxt2.Contains("model = \"Beta\""),
                    "Che do 1 agent: doi reasoning giu nguyen dong model",
                    rxTxt2.Replace("\r", "").Replace("\n", " | "));
                try { File.Delete(rxCfg); } catch { }

                // ---- Reasoning cua LEADER: doc + ghi vao khoi goc config ----
                    string lrcfg = Path.Combine(Path.GetTempPath(),
                        "ms-lead-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".toml");
                    File.WriteAllLines(lrcfg, new string[] {
                        "model = \"M-Lead\"", "model_reasoning_effort = \"max\"", "",
                        "[profile.work]", "model_reasoning_effort = \"low\"", "",
                        "[agents.qa]", "description = \"x\"", "config_file = \"q.toml\"" });
                    Check(Roles.LeaderReasoningIn(lrcfg) == "max",
                        "Leader: doc duoc muc suy luan o khoi goc config",
                        Roles.LeaderReasoningIn(lrcfg));
                    Check(Roles.Read(lrcfg)[0].Reasoning == "max",
                        "Leader: Read() gan dung muc suy luan", null);

                    string bakL;
                    Cfg.SetReasoning(lrcfg, "high", out bakL);
                    string lt = File.ReadAllText(lrcfg);
                    Check(lt.Contains("model_reasoning_effort = \"high\"")
                           && lt.Contains("model = \"M-Lead\"")
                           && lt.Contains("[profile.work]")
                           && lt.Contains("model_reasoning_effort = \"low\""),
                        "Leader: sua muc suy luan ma giu nguyen khoa trong [profile.x]", null);
                    Check(Roles.LeaderReasoningIn(lrcfg) == "high",
                        "Leader: doc lai sau khi sua", Roles.LeaderReasoningIn(lrcfg));

                    // [profile.x] phai giu nguyen gia tri rieng
                    File.WriteAllLines(lrcfg, new string[] {
                        "model = \"M-Lead\"", "", "[profile.work]",
                        "model_reasoning_effort = \"low\"", "" });
                    Cfg.SetReasoning(lrcfg, "max", out bakL);
                    Check(File.ReadAllText(lrcfg).Contains("model_reasoning_effort = \"max\"")
                           && File.ReadAllText(lrcfg).Contains("model_reasoning_effort = \"low\""),
                        "Leader: them moi dong khi config chua co", null);
                    try { File.Delete(lrcfg); } catch { }

                    // ---- Mo ta rong: Codex bo qua vai tro ----
                    Check(Roles.DefaultDescription("coder").Length > 10,
                        "Mo ta vai tro: tu dien mo ta mac dinh khi de trong", null);
                    Check(Roles.DefaultDescription("QC").Contains("PASS")
                           && Roles.DefaultDescription("architect").Contains("kien truc"),
                        "Mo ta vai tro: noi dung dung nhiem vu", null);
                    Check(!string.IsNullOrEmpty(Roles.DefaultDescription("vai-la")),
                        "Mo ta vai tro: vai tro la ten bat ky van co mo ta", null);

                    Check(Roles.NormalReasoning("HIGH") == "high",
                        "Cu so vai tro: reasoning chuan hoa chu hoa/thuong",
                        Roles.NormalReasoning("HIGH"));
                    Check(Roles.NormalReasoning("khong-co") == Roles.DefaultReasoning,
                        "Cu so vai tro: reasoning la gia tri la -> ve mac dinh", null);
                    Check(Roles.ReasoningLevels.Length == 5,
                        "Cu so vai tro: co day du 5 muc reasoning",
                        "n=" + Roles.ReasoningLevels.Length);

                    // max_concurrent_threads_per_session
                    Roles.WriteMaxParallel(rcfg, 7);
                    Check(Roles.MaxParallelIn(rcfg) == 7,
                        "Cu so vai tro: doc max song song", "" + Roles.MaxParallelIn(rcfg));
                    Roles.WriteMaxParallel(rcfg, 2);
                    Check(File.ReadAllText(rcfg).Contains("max_concurrent_threads_per_session = 2")
                           && Roles.MaxParallelIn(rcfg) == 2,
                        "Cu so vai tro: sua max song song khong them dong trung", null);
                    Check(Roles.ClampParallel(0) == 1 && Roles.ClampParallel(99) == 32,
                        "Cu so vai tro: gioi han max song song 1..32", null);

                    // preset: luu roi nap lai
                    string oldHome2 = Environment.GetEnvironmentVariable("CODEX_HOME");
                    string oldLocal = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                    string tmpPreset = Path.Combine(Path.GetTempPath(),
                        "ms-preset-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", tmpPreset);
                    try
                    {
                        List<RoleDef> pr = new List<RoleDef>();
                        pr.Add(new RoleDef { Name = "leader", Model = "M1" });
                        pr.Add(new RoleDef { Name = "coder", Model = "M2", Reasoning = "high" });
                        Roles.SavePreset("Balanced", 5, pr);
                        Check(Roles.ListPresets().Contains("Balanced"),
                            "Cu so vai tro: luu preset", null);
                        int mp2;
                        List<RoleDef> got2 = Roles.LoadPreset("Balanced", out mp2);
                        Check(got2.Count == 1 && got2[0].Name == "coder"
                               && got2[0].Model == "M2" && got2[0].Reasoning == "high"
                               && mp2 == 5,
                            "Cu so vai tro: nap preset dung agents + reasoning + max",
                            "n=" + got2.Count + " mp=" + mp2);
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocal);
                        Environment.SetEnvironmentVariable("CODEX_HOME", oldHome2);
                        try { Directory.Delete(tmpPreset, true); } catch { }
                    }

                    Check(ink > 200,
                        "Cu so vai tro: dong model ve duoc chu (RowPainter)", "ink=" + ink);
                }
                catch (Exception ex)
                {
                    Check(false, "Cu so vai tro: khong mo duoc", ex.Message);
                }
                finally { if (af != null) af.Dispose(); }

                // ---- Do bo cuc that cua ca 2 cua so ----
                AuditLayouts(rcfg);
            }
            finally { try { Directory.Delete(rdir, true); } catch { } }


            Console.WriteLine();
            Console.WriteLine("  Ket qua: " + pass + " pass, " + fail + " fail");
            return fail;
        }

        // Do bo cuc that: bao control tran ra ngoai, de len nhau, nhan bi cat.
        static void AuditLayouts(string cfgPath)
        {
            List<ModelInfo> fake = new List<ModelInfo>();
            string[] mn = { "openrouter/free/model-alpha", "orca/anthropic/claude-opus-5",
                "gemini/gemini-3.1-pro", "unofree/glm-5.3-flash-thinking:free" };
            for (int i = 0; i < mn.Length; i++)
                fake.Add(new ModelInfo { Id = mn[i], Owner = "test", IsCombo = false,
                    Ctx = 128000, Lower = mn[i].ToLowerInvariant() });

            int[] w = { 1400, 1000, 900, 820 };
            int[] h = { 800, 640, 600, 580 };
            int totalBad = 0, totalClip = 0, totalBadMain = 0, totalClipMain = 0;
            int[] wm = { 1400, 1000, 900, 860 };
            int[] hm = { 800, 640, 600, 470 };
            for (int k = 0; k < wm.Length; k++)
            {
                SwitcherForm mf = null;
                try
                {
                    mf = new SwitcherForm(cfgPath);
                    mf.Size = new Size(wm[k], hm[k]);
                    LayoutCheck.Show2(mf);
                    mf.LayoutHeaderButtons();
                    LayoutCheck.Show2(mf);
                    foreach (LayoutCheck.Problem pr in LayoutCheck.Scan(mf))
                    {
                        if (totalBadMain < 6) Console.WriteLine("    LOI-CTA " + pr.ToString());
                        totalBadMain++;
                    }
                    foreach (string s in LayoutCheck.Clipped(mf))
                    {
                        if (totalClipMain < 8) Console.WriteLine("    CAT-CTA " + s);
                        totalClipMain++;
                    }
                }
                finally { if (mf != null) mf.Dispose(); }
            }
            Check(totalBadMain == 0, "Layout cua so chinh: khong control tran ra ngoai hay de len nhau",
                totalBadMain == 0 ? "0 loi o 4 kich thuoc" : totalBadMain + " loi");
            Check(totalClipMain == 0, "Layout cua so chinh: khong nhan bi cat chu",
                totalClipMain == 0 ? "0 nhan cat" : totalClipMain + " nhan cat");
            for (int k = 0; k < w.Length; k++)
            {
                AgentForm af = null;
                try
                {
                    af = new AgentForm(cfgPath, fake);
                    af.Size = new Size(w[k], h[k]);
                    af.PrepareForShot();
                    if (w[k] == 1400 && h[k] == 800) LayoutCheck.Dump(af, 0);
                    foreach (LayoutCheck.Problem pr in LayoutCheck.Scan(af))
                    {
                        if (totalBad < 12) Console.WriteLine("    LOI " + pr.ToString());
                        totalBad++;
                    }
                    foreach (string s in LayoutCheck.Clipped(af))
                    {
                        if (totalClip < 6) Console.WriteLine("    CAT " + s);
                        totalClip++;
                    }
                }
                finally { if (af != null) af.Dispose(); }
            }
            Console.WriteLine();
            Check(totalBad == 0, "Layout cua so vai tro: khong control tran ra ngoai hay de len nhau",
                totalBad == 0 ? "0 loi o 4 kich thuoc" : totalBad + " loi");
            Check(totalClip == 0, "Layout cua so vai tro: khong nhan bi cat chu",
                totalClip == 0 ? "0 nhan cat" : totalClip + " nhan cat");
            Console.WriteLine();
        }

        static bool Wild(string s, string p)
        {
            if (p.StartsWith("*") && p.EndsWith("*"))
                return s.IndexOf(p.Substring(1, p.Length - 2), StringComparison.OrdinalIgnoreCase) >= 0;
            return string.Equals(s, p, StringComparison.OrdinalIgnoreCase);
        }

        static string JoinCombo(List<ModelInfo> all)
        {
            List<string> l = new List<string>();
            foreach (ModelInfo m in all) if (m.IsCombo) l.Add(m.Id);
            return string.Join(", ", l.ToArray());
        }

        static string TopIds(List<Hit> hits, int n)
        {
            List<string> l = new List<string>();
            for (int i = 0; i < hits.Count && i < n; i++) l.Add(hits[i].M.Id);
            return string.Join(", ", l.ToArray());
        }
    }
}
