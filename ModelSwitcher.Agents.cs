// ==================================================================
// CHE DO NHIEU AGENT
//
// Codex 0.155.0 doc [agents] theo 2 kieu khac nhau:
//
//   1. Khoa phang trong [agents]:
//        default_subagent_model, default_subagent_reasoning_effort,
//        max_concurrent_threads_per_session, max_depth,
//        job_max_runtime_seconds, interrupt_message
//
//   2. Bang con [agents.<ten>] : AgentRoleToml co DUNG 3 truong
//        description, config_file, nickname_candidates
//
// MODEL cua moi vai tro NAM TRONG file config_file, KHONG nam trong
// [agents.<ten>] - dat `model` o do bi bo qua im lang.
//
// File nay KHONG doi hanh vi hien tai. No chi:
//   1. them 2 nut chon che do vao dai "DANG DUNG" cua cua so chinh,
//   2. them cua so cau hinh vai tro.
//
// Che do duoc SUY RA tu config.toml (co [agents.<ten>] hay khong),
// khong ghi them khoa gi - tranh "unknown configuration field" cua codex.
// ==================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NineRouter
{

internal sealed class RoleDef
{
    public string Name;
    public string Description = "";
    public string Model = "";
    // Reasoning effort rieng cua vai tro nay. Codex doc khoa
    // model_reasoning_effort nam trong file config_file.
    public string Reasoning = Roles.DefaultReasoning;
    public List<string> Nick = new List<string>();
    public bool IsLeader { get { return Roles.LeaderName.Equals(Name, StringComparison.OrdinalIgnoreCase); } }
}


    // ==================================================================
    // BO KIEM TRA BO CUC
    //
    // Do moi control co bi TRAN ra ngoai khung cha, hoac bi control khac
    // de len khong. Day la thu do truc tiep - khong doan theo mat.
    // ==================================================================
    internal static class LayoutCheck
    {
        public class Problem
        {
            public string Form, Kind, What;
            public Rectangle A, B;
            public Problem(string f, string k, string w, Rectangle a, Rectangle b)
            { Form = f; Kind = k; What = w; A = a; B = b; }
            public override string ToString()
            {
                if (Kind == "tran-ra-ngoai") return Form + " :: " + What + " tran ra ngoai " + A;
                return Form + " :: " + What + " " + A + " bi de boi " + B;
            }
        }

        // In moi control cua form - dung khi can nhin toan bo bo cuc.
        public static void Dump(Control root, int depth)
        {
            if (depth > 3) return;
            foreach (Control c in root.Controls)
            {
                Console.WriteLine(new string(' ', depth * 2)
                    + N(c) + " vis=" + c.Visible + " loc=" + c.Location + " size=" + c.Size);
                if (c.HasChildren) Dump(c, depth + 1);
            }
        }
        public static List<Problem> Scan(Form f)
        {
            // Ep hien + dung layout truoc, neu khong control nao co kich thuoc that.
            Show2(f);
            List<Problem> bad = new List<Problem>();
            // Do theo khung chua control, KHONG theo khung form: form chua Show
            // thi ClientRectangle chua dung, do se bao loi o toan bo cay.
            Walk(f, Rectangle.Empty, bad, 0);
            return bad;
        }

        public static void Show2(Control c)
        {
            try { c.Visible = true; } catch { }
            try { c.PerformLayout(); } catch { }
            foreach (Control k in c.Controls) Show2(k);
        }

        static string N(Control c)
        {
            string s = c.Name;
            if (string.IsNullOrEmpty(s)) s = c.GetType().Name + "@" + c.GetHashCode();
            return s;
        }

        static void Walk(Control parent, Rectangle parentRect, List<Problem> bad, int depth)
        {
            if (depth > 4) return;
            if (parentRect.Width <= 0 || parentRect.Height <= 0)
                parentRect = parent.ClientRectangle;
            // Panel/FlowLayoutPanel tu can bo cuc: de-len-nhau giua cac panel
            // khong phai loi. NHUNG van phai kiem tra tran-ra-ngoai va quet xuong.
            bool loose = (parent is Panel || parent is FlowLayoutPanel);
            // KHONG duoc loc theo Visible: form chung ta do truoc khi Show,
            // moi control co Visible=false nen bo do se bo qua het.
            List<Control> kids = new List<Control>();
            foreach (Control c in parent.Controls) kids.Add(c);
            foreach (Control c in kids)
            {
                Rectangle r = c.Bounds;
                if (r.Width <= 0 || r.Height <= 0) continue;
                // Control co Dock, hoac la con truc tiep cua Form: vi tri do WinForms
                // tinh lai, form chua Show thi do sai. Chi kiem tra control
                // dat vi tri tuyet doi trong panel.
                bool isPinned = (c.Dock == DockStyle.None && !(parent is Form));
                bool outside = isPinned && (r.Left < parentRect.Left || r.Top < parentRect.Top
                    || r.Right > parentRect.Right || r.Bottom > parentRect.Bottom);
                if (outside)
                    bad.Add(new Problem(parent.FindForm() == null ? "?" : parent.FindForm().Text,
                        "tran-ra-ngoai", c.Name.Length > 0 ? c.Name : c.GetType().Name,
                        r, parentRect));

                // hai control cung thuoc control cha -> de len nhau.
                // PHAI so bang reference, KHONG so Name: phan lon control ten rong.
                for (int i = 0; i < kids.Count; i++)
                {
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        if (loose) continue;
                        Control c1 = kids[i], c2 = kids[j];
                        // Bo qua khi mot trong hai nam tren control moi (popup).
                        if (c1 is Label || c2 is Label) continue;
                        if (c1.Parent != c2.Parent) continue;
                        // Control co Dock: vi tri do WinForms quyet dinh, khong phai loi.
                        if (c1.Dock != DockStyle.None || c2.Dock != DockStyle.None) continue;
                        Rectangle a = c1.Bounds, b = c2.Bounds;
                        if (a.IntersectsWith(b))
                            bad.Add(new Problem(parent.FindForm() == null ? "?" : parent.FindForm().Text,
                                "de-len-nhau",
                                N(c1) + " / " + N(c2), a, b));
                    }
                }
                // Bounds cua control la tuong doi voi cha -> so voi ClientRectangle cha.
                // Composite control (NumericUpDown) co phan ta ben trong: bo qua.
                if (c.HasChildren && !(c is NumericUpDown))
                    Walk(c, Rectangle.Empty, bad, depth + 1);
            }
        }

        // Nhan co bi cat chu khong: do chieu rong chu that so voi khung.
        public static List<string> Clipped(Form f)
        {
            Show2(f);
            List<string> r = new List<string>();
            foreach (Control c in AllText(f))
            {
                if (c.AutoSize) continue;
                // Nut/Combo khong xuong dong -> do 1 dong, khong dung WordBreak.
                bool wrap = (c is Label && c.Height > 26);
                Size need = TextRenderer.MeasureText(c.Text, c.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
                if (need.Height > c.Height + 2 || need.Width > c.Width + 2)
                    r.Add((c.Name.Length > 0 ? c.Name : c.GetType().Name) + ": can "
                        + need.Width + "x" + need.Height + ", co " + c.Width + "x" + c.Height
                        + "  \"" + Shorten(c.Text, 40) + "\"");
            }
            return r;
        }

        static string Shorten(string s, int n)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }

        // Moi control co chu deu phai kiem tra: Label, Button, ComboBox, CheckBox.
        // Chi quet Label thi bo qua chinh cho "mat chu" tren nut bam.
        public static List<Control> AllText(Control root)
        {
            List<Control> r = new List<Control>();
            foreach (Control c in root.Controls)
            {
                bool hasText = c is Label || c is Button || c is ComboBox || c is CheckBox;
                if (hasText && !string.IsNullOrEmpty(c.Text)) r.Add(c);
                if (c.HasChildren) r.AddRange(AllText(c));
            }
            return r;
        }

        public static List<Label> AllLabels(Control root)
        {
            List<Label> r = new List<Label>();
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && !string.IsNullOrEmpty(l.Text)) r.Add(l);
                if (c.HasChildren) r.AddRange(AllLabels(c));
            }
            return r;
        }
    }

internal static class Roles
{
    public const string LeaderName = "leader";

    // Cac muc reasoning Codex chap nhan.
    public static readonly string[] ReasoningLevels =
        { "minimal", "low", "medium", "high", "max" };
    public const string DefaultReasoning = "medium";

    public static string NormalReasoning(string v)
    {
        if (string.IsNullOrEmpty(v)) return DefaultReasoning;
        foreach (string s in ReasoningLevels)
            if (s.Equals(v, StringComparison.OrdinalIgnoreCase)) return s;
        return DefaultReasoning;
    }

    // Doc model_reasoning_effort tu file .toml cua vai tro.
    public static string ReasoningIn(string file)
    {
        try
        {
            foreach (string line in File.ReadAllLines(file))
            {
                string tr = line.Trim();
                if (KeyOf(tr) != "model_reasoning_effort") continue;
                return NormalReasoning(TomlStr(tr));
            }
        }
        catch { }
        return DefaultReasoning;
    }

    // Cac khoa phang cua [agents]. KHONG phai vai tro - Codex doc chung cho
    // moi subagent. Chi dung de tach bao con nao la "khoa chung", con lai
    // duoc doc nhu vai tro.
    static readonly string[] FlatKeys = {
        "default_subagent_model", "default_subagent_reasoning_effort",
        "max_concurrent_threads_per_session", "max_depth",
        "job_max_runtime_seconds", "interrupt_message"
    };

    public static bool IsFlatKey(string k)
    {
        foreach (string f in FlatKeys) if (f == k) return true;
        return false;
    }

    public static bool IsValidName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        foreach (char c in n)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
        return true;
    }

    // Thu muc chua file cau hinh tung vai tro.
    public static string Dir()
    {
        string home = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrEmpty(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.codex";
        return Path.Combine(home, "roles");
    }

    public static string FileOf(string name) { return Path.Combine(Dir(), name + ".toml"); }

    // ---------- doc config.toml ----------

    // Co [agents.<ten>] nao khong? -> dang o che do nhieu agent.
    public static bool Exists(string cfgPath)
    {
        try
        {
            foreach (string line in File.ReadAllLines(cfgPath))
            {
                string tr = line.Trim();
                if (!tr.StartsWith("[agents.")) continue;
                string inner = tr.Substring(8, tr.Length - 9);
                if (inner.Length == 0) continue;
                if (inner.EndsWith(".")) continue;              // [agents.x.y]
                if (IsFlatKey(inner)) continue;                 // [agents.model] - khoa chung
                return true;
            }
        }
        catch { }
        return false;
    }

    // Danh sach vai tro hien co. Leader luon o dau.
    // Doc model_reasoning_effort o khoi goc config.toml - dung cho leader.
    public static string LeaderReasoningIn(string cfgPath)
    {
        try
        {
            bool top = true;
            foreach (string line in File.ReadAllLines(cfgPath))
            {
                string tr = line.Trim();
                if (tr.StartsWith("[")) { top = false; continue; }
                if (!top) continue;
                if (KeyOf(tr) != "model_reasoning_effort") continue;
                int eq = tr.IndexOf('=');
                if (eq < 0) continue;
                return NormalReasoning(tr.Substring(eq + 1).Trim().Trim('"'));
            }
        }
        catch { }
        return DefaultReasoning;
    }

    public static List<RoleDef> Read(string cfgPath)
    {
        List<RoleDef> list = new List<RoleDef>();
        RoleDef leader = new RoleDef { Name = LeaderName, Model = Cfg.Current(cfgPath),
            Reasoning = LeaderReasoningIn(cfgPath) };
        leader.Description = "Model goc cua Codex. Chon model o day = dung nut Chuyen mo hinh.";
        list.Add(leader);
        if (!File.Exists(cfgPath)) return list;

        RoleDef cur = null;
        foreach (string line in File.ReadAllLines(cfgPath))
        {
            string tr = line.Trim();
            if (tr.StartsWith("["))
            {
                RoleDef h = RoleFromHeader(tr);
                if (h != null)
                {
                    bool dup = false;
                    foreach (RoleDef x in list)
                        if (x.Name == h.Name) { dup = true; h = x; break; }
                    if (!dup) list.Add(h);
                }
                cur = h;
                continue;
            }
            if (cur == null) continue;
            string k = KeyOf(tr);
            if (k == "description") cur.Description = TomlStr(tr) ?? "";
            else if (k == "config_file")
            {
                string f = TomlStr(tr);
                // Model thuoc ve file rieng cua vai tro, khong nam trong [agents.x].
                string m = (string.IsNullOrEmpty(f)) ? null : Cfg.ModelIn(f);
                if (!string.IsNullOrEmpty(f)) cur.Reasoning = ReasoningIn(f);
                if (!string.IsNullOrEmpty(m)) cur.Model = m;
            }
            else if (k == "nickname_candidates") cur.Nick = ParseList(tr);
        }
        return list;
    }

    // "[agents.architect]" -> RoleDef. Tra null neu khong phai vai tro.
    static RoleDef RoleFromHeader(string tr)
    {
        if (!tr.StartsWith("[agents.")) return null;
        if (!tr.EndsWith("]")) return null;
        string inner = tr.Substring(8, tr.Length - 9);
        if (inner.Length == 0 || inner.IndexOf('.') >= 0) return null;
        if (IsFlatKey(inner)) return null;
        RoleDef r = new RoleDef { Name = inner, Nick = new List<string>() };
        r.Nick.Add(inner);
        return r;
    }

    // Tat che do nhieu agent: goi het [agents.<ten>] khoi config.toml.
    // Don gian la ghi lai danh sach chi con leader.
    public static void Disable(string cfgPath)
    {
        List<RoleDef> only = new List<RoleDef>();
        only.Add(Read(cfgPath)[0]);
        Write(cfgPath, only);
    }

    // ---------- max_concurrent_threads_per_session ----------

    public static int ClampParallel(int n)
    {
        if (n < 1) return 1;
        if (n > 32) return 32;
        return n;
    }

    public static int MaxParallelIn(string cfgPath)
    {
        try
        {
            foreach (string line in File.ReadAllLines(cfgPath))
            {
                string tr = line.Trim();
                if (KeyOf(tr) != "max_concurrent_threads_per_session") continue;
                int eq = tr.IndexOf('=');
                int n;
                if (eq > 0 && int.TryParse(tr.Substring(eq + 1).Trim(), out n))
                    return ClampParallel(n);
            }
        }
        catch { }
        return 4;
    }

    // Gho max_concurrent_threads_per_session vao khoi [agents], tao neu chua co.
    public static void WriteMaxParallel(string cfgPath, int n)
    {
        n = ClampParallel(n);
        List<string> lines = new List<string>(File.ReadAllLines(cfgPath));
        string key = "max_concurrent_threads_per_session";
        bool inAgents = false;
        for (int i = 0; i < lines.Count; i++)
        {
            string tr = lines[i].Trim();
            if (tr.StartsWith("[")) { inAgents = (tr == "[agents]"); continue; }
            if (inAgents && KeyOf(tr) == key)
            {
                lines[i] = key + " = " + n;
                File.WriteAllLines(cfgPath, lines.ToArray(), new UTF8Encoding(false));
                return;
            }
        }
        int at = -1;
        bool found = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() != "[agents]") continue;
            found = true;
            int j = i + 1;
            while (j < lines.Count && !lines[j].TrimStart().StartsWith("[")) j++;
            at = j;
            break;
        }
        if (!found)
        {
            lines.Add("");
            lines.Add("[agents]");
            at = lines.Count;
        }
        lines.Insert(at, key + " = " + n);
        File.WriteAllLines(cfgPath, lines.ToArray(), new UTF8Encoding(false));
    }

    // ---------- preset team (van ban thuan, khong can escape) ----------

    public static string PresetDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "9RouterSwitcher");
    }

    public static List<string> ListPresets()
    {
        List<string> r = new List<string>();
        string dir = PresetDir();
        if (!Directory.Exists(dir)) return r;
        foreach (string f in Directory.GetFiles(dir, "team-*"))
            r.Add(Path.GetFileName(f).Substring(5));
        r.Sort(StringComparer.CurrentCultureIgnoreCase);
        return r;
    }

    public static string PresetPath(string name)
    {
        string dir = PresetDir();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, "team-" + name);
    }

    static string Tail(string line)
    {
        int eq = line.IndexOf('=');
        return eq < 0 ? "" : line.Substring(eq + 1).Trim();
    }

    public static void SavePreset(string name, int maxParallel, List<RoleDef> roles)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Ten preset rong.");
        string nl = Environment.NewLine;
        StringBuilder sb = new StringBuilder();
        sb.Append("name = ").Append(name).Append(nl);
        sb.Append("maxParallel = ").Append(ClampParallel(maxParallel)).Append(nl);
        foreach (RoleDef r in roles)
        {
            if (r.IsLeader) continue;
            sb.Append("role = ").Append(r.Name).Append(nl);
            sb.Append("  model = ").Append(r.Model).Append(nl);
            sb.Append("  reasoning = ").Append(NormalReasoning(r.Reasoning)).Append(nl);
            sb.Append("  desc = ").Append((r.Description ?? "").Replace(nl, " ")).Append(nl);
        }
        File.WriteAllText(PresetPath(name), sb.ToString(), new UTF8Encoding(false));
    }

    public static List<RoleDef> LoadPreset(string name, out int maxParallel)
    {
        maxParallel = 4;
        List<RoleDef> list = new List<RoleDef>();
        string p = PresetPath(name);
        if (!File.Exists(p)) return list;
        RoleDef cur = null;
        foreach (string line in File.ReadAllLines(p))
        {
            string t = line.Trim();
            if (t.StartsWith("maxParallel"))
            {
                int n;
                if (int.TryParse(Tail(t), out n)) maxParallel = ClampParallel(n);
                continue;
            }
            if (t.StartsWith("role ="))
            {
                cur = new RoleDef { Name = Tail(t) };
                list.Add(cur);
                continue;
            }
            if (cur == null) continue;
            if (t.StartsWith("model =")) cur.Model = Tail(t);
            else if (t.StartsWith("reasoning =")) cur.Reasoning = NormalReasoning(Tail(t));
            else if (t.StartsWith("desc =")) cur.Description = Tail(t);
        }
        return list;
    }

    // ---------- ghi config.toml ----------

    // Xoa het cac bang [agents.<ten>] cu, ghi lai bang danh sach moi.
    // Chi dong vao bang con; dong [agents] va khoa phang cua no giu nguyen.
    public static void Write(string cfgPath, List<RoleDef> roles)
    {
        if (!File.Exists(cfgPath)) throw new FileNotFoundException("Khong tim thay config.toml: " + cfgPath);
        string[] src = File.ReadAllLines(cfgPath);
        List<string> outp = new List<string>(src.Length);
        bool skip = false;
        foreach (string line in src)
        {
            if (skip)
            {
                if (line.TrimStart().StartsWith("[")) skip = false;   // het bang con
                else continue;                                        // van con trong bang
            }
            string tr = line.Trim();
            if (tr.StartsWith("[agents.") && tr.EndsWith("]")
                && tr.IndexOf('.', 8) < 0)
            {
                string inner = tr.Substring(8, tr.Length - 9);
                if (inner.Length > 0 && !IsFlatKey(inner)) { skip = true; continue; }
            }
            outp.Add(line);
        }

        List<string> tail = new List<string>();
        foreach (RoleDef r in roles)
        {
            if (r.IsLeader) continue;
            tail.Add("");
            tail.Add(Connector.Marker);
            tail.Add("[agents." + r.Name + "]");
            tail.Add("description = \"" + Esc(r.Description) + "\"");
            tail.Add("config_file = \"" + Esc(FileOf(r.Name)) + "\"");
            tail.Add("nickname_candidates = " + ListLit(r.Nick, r.Name));
        }
        if (tail.Count > 0) outp.AddRange(tail);

        string backup = cfgPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(cfgPath, backup, true);
        File.WriteAllLines(cfgPath, outp.ToArray(), new UTF8Encoding(false));
    }

    // ---------- ghi file vai tro ----------

    // File .toml rieng cua vai tro: model + provider + mo ta.
    public static void WriteFile(string cfgPath, RoleDef r)
    {
        string dir = Dir();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string nl = Environment.NewLine;
        StringBuilder sb = new StringBuilder();
        sb.Append(Connector.Marker).Append(nl);
        sb.Append("model = \"").Append(Esc(r.Model)).Append("\"").Append(nl);
        sb.Append("model_provider = \"").Append(Connector.ProviderId).Append("\"").Append(nl);
        sb.Append("model_reasoning_effort = \"").Append(Esc(NormalReasoning(r.Reasoning))).Append("\"").Append(nl);
        sb.Append("sandbox_mode = \"workspace-write\"").Append(nl);
        sb.Append(nl);
        sb.Append("developer_instructions = \"\"\"").Append(nl);
        sb.Append(Instructions(r)).Append(nl);
        sb.Append("\"\"\"").Append(nl);
        sb.Append(nl);
        sb.Append(ProviderBlock(cfgPath));
        File.WriteAllText(FileOf(r.Name), sb.ToString(), new UTF8Encoding(false));
    }

    // Codex BO QUA vai tro neu description rong:
    //   "Ignoring malformed agent role definition: agents.X.description cannot be blank"
    // Vai tro bien mat, chi con leader chay. Mo ta khong duoc de trong.
    public static string DefaultDescription(string role)
    {
        string n = (role ?? "").ToLowerInvariant();
        if (n == "leader") return "Dieu phoi toan bo team: doc yeu cau, lap ke hoach, chia viec va kiem tra ket qua.";
        if (n == "architect") return "Thiet ke kien truc, chot interface va tach thanh task cho tung vai tro.";
        if (n.StartsWith("coder")) return "Cai dat ma theo dac ta da chot. Khong tu doi kien truc.";
        if (n == "qa" || n == "reviewer" || n.StartsWith("qc")) return "Kiem tra ket qua, chay test, tra ve PASS hoac FAIL kem ly do.";
        if (n == "frontend") return "Lam giao dien nguoi dung theo dac ta.";
        if (n == "backend") return "Lam phia may chu va API theo dac ta.";
        if (n == "researcher") return "Nghiem cuu, tim hieu va tom tat thong tin can thiet.";
        if (n == "tester") return "Viet va chay kiem thu, tim loi va hien loi cu the.";
        if (n == "security") return "Ra soat bao mat, tim lo hong va de xuat cach sua.";
        return "Vai tro " + role + ". Nhan cong viec duoc giao va bao cao ket qua.";
    }

    static string Instructions(RoleDef r)
    {
        string d = (r.Description ?? "").Trim();
        if (d.Length == 0) return "Ban la " + r.Name + ".";
        return d;
    }

    // Chep khoi [model_providers.9router] cua config chinh. Subagent can
    // resolve provider rieng nen file vai tro phai co day khoa + bearer.
    public static string ProviderBlock(string cfgPath)
    {
        string nl = Environment.NewLine;
        string baseUrl = "http://127.0.0.1:20128/v1";
        string auth = "";
        try
        {
            string[] lines = File.ReadAllLines(cfgPath);
            baseUrl = TableValue(lines, "[model_providers." + Connector.ProviderId + "]", "base_url", baseUrl);
            auth = TableValue(lines, "[model_providers." + Connector.ProviderId + ".http_headers]", "Authorization", "");
        }
        catch { }
        StringBuilder sb = new StringBuilder();
        sb.Append("[model_providers.").Append(Connector.ProviderId).Append("]").Append(nl);
        sb.Append("name = \"9Router\"").Append(nl);
        sb.Append("base_url = \"").Append(Esc(baseUrl)).Append("\"").Append(nl);
        sb.Append("wire_api = \"responses\"").Append(nl);
        sb.Append(nl);
        sb.Append("[model_providers.").Append(Connector.ProviderId).Append(".http_headers]").Append(nl);
        sb.Append("Authorization = \"").Append(Esc(auth)).Append("\"").Append(nl);
        return sb.ToString();
    }

    // ---------- tien ich TOML nho ----------

    static string KeyOf(string tr)
    {
        int eq = tr.IndexOf('=');
        if (eq <= 0) return "";
        string k = tr.Substring(0, eq).Trim();
        foreach (char c in k)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return "";
        return k;
    }

    static string TomlStr(string line)
    {
        int a = line.IndexOf('"');
        if (a < 0) return null;
        int b = line.IndexOf('"', a + 1);
        if (b < 0) return null;
        return line.Substring(a + 1, b - a - 1).Replace("\\\\", "\\");
    }

    static string TableValue(string[] lines, string header, string key, string fallback)
    {
        bool inTable = false;
        foreach (string line in lines)
        {
            string tr = line.Trim();
            if (tr.StartsWith("[")) { inTable = (tr == header); continue; }
            if (!inTable) continue;
            if (KeyOf(tr) == key)
            {
                string v = TomlStr(tr);
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }
        return fallback;
    }

    static List<string> ParseList(string line)
    {
        List<string> r = new List<string>();
        int a = line.IndexOf('"'), b = line.IndexOf('"', a + 1);
        while (a >= 0 && b > a)
        {
            r.Add(line.Substring(a + 1, b - a - 1));
            a = line.IndexOf('"', b + 1);
            b = (a < 0) ? -1 : line.IndexOf('"', a + 1);
        }
        return r;
    }

    static string ListLit(List<string> items, string fallback)
    {
        if (items == null || items.Count == 0) return "[\"" + Esc(fallback) + "\"]";
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Esc(items[i])).Append('"');
        }
        return sb.Append("]").ToString();
    }

    // TOML chuoi co dau nhay phai escape "\" va ".
    public static string Esc(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
    }
}

    // ==================================================================
    // CUA SO CAU HINH TEAM NHIEU AGENT
    //
    // Trai : vai tro (ten - model - reasoning) + mo ta
    // Phai : o loc + danh sach model cua 9Router (dung lai bo khop mo)
    // Duoi : so do workflow + max song song + nut AP DUNG
    // ==================================================================
    internal sealed class AgentForm : Form
    {
        readonly Palette P = Palette.Shared;
        readonly string configPath;
        List<ModelInfo> all;
        List<RoleDef> roles = new List<RoleDef>();
        List<Hit> rows = new List<Hit>();
        bool comboOnly;
        int maxParallel = 4;
        Label lblParallel;
        TextBox tbPreset;
        ComboBox cbPreset;

        Panel leftBox, rightBox;
        Label lblTeam, lblDesc, lblPick, lblState, lblReasoning, lblFlow;
        ListBox lbRoles;
        TextBox tbNew, tbDesc, tbFilter;
        Button btnAdd, btnDel, btnRename, btnAssign, btnSave, btnClose;
        ComboBox cbReasoning;
        NumericUpDown nudParallel;
        RowListView list;
        CardPanel filterCard, listCard;

        const int RowH = 40;
        const int PadL = 16, PadR = 16, FootH = 64;

        // 9 mau vai tro co san - dung khi bam "+ Them vai tro"
        static readonly string[] Templates = {
            "architect", "coder", "coder2", "frontend", "backend",
            "researcher", "tester", "security", "reviewer"
        };

        public AgentForm(string cfgPath, List<ModelInfo> models)
        {
            this.configPath = cfgPath;
            roles = Roles.Read(cfgPath);
            all = models;
            if (all == null) all = ModelCache.Load();

            Text = "9Router \u00b7 Codex \u00b7 Nhi\u1ec7u agent";
            ClientSize = new Size(1000, 660);
            MinimumSize = new Size(840, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = P.Form;
            Font = P.FUi;
            try { Icon = NativeWin.AppIcon; } catch { }
            KeyPreview = true;

            BuildLeft();
            BuildRight();
            BuildFooter();

            Resize += delegate { LayoutAll(); };
            Shown += delegate { LayoutAll(); FillRoles(0); tbFilter.Text = ""; UpdateResults(); };
            KeyDown += delegate (object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
            };
        }

        void BuildLeft()
        {
            leftBox = new Panel { Dock = DockStyle.Left, Width = 310, BackColor = P.Form };
            lblTeam = new Label { Text = "MULTI-AGENT TEAM", Font = P.FSmallB, ForeColor = P.Dim,
                AutoSize = true, Location = new Point(PadL, 10) };
            lbRoles = new ListBox { Location = new Point(PadL, 30), Size = new Size(270, 190),
                Font = P.FUi, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
            lbRoles.SelectedIndexChanged += delegate { OnRoleChanged(); };

            tbNew = new TextBox { Location = new Point(PadL, 230), Size = new Size(170, 24),
                Font = P.FUi, BorderStyle = BorderStyle.FixedSingle };
            btnAdd = AgentBtn("+ Th\u00eam", P.RowA, P.Fg, false);
            btnAdd.Location = new Point(PadL + 176, 229); btnAdd.Size = new Size(94, 26);
            btnAdd.Click += delegate { AddRole(); };

            btnRename = AgentBtn("\u0110\u1ed5i t\u00ean", P.RowA, P.Fg, false);
            btnRename.Location = new Point(PadL, 264); btnRename.Size = new Size(131, 26);
            btnRename.Click += delegate { RenameRole(); };
            btnDel = AgentBtn("\u2212 Xo\u00e1", P.RowA, P.Fg, false);
            btnDel.Location = new Point(PadL + 137, 264); btnDel.Size = new Size(133, 26);
            btnDel.Click += delegate { DelRole(); };

            lblReasoning = new Label { Text = "Reasoning", Font = P.FSmallB, ForeColor = P.Dim,
                Location = new Point(PadL, 299), Size = new Size(70, 18) };
            cbReasoning = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = P.FUi,
                Location = new Point(PadL + 74, 296), Width = 110 };
            foreach (string s in Roles.ReasoningLevels) cbReasoning.Items.Add(s);
            cbReasoning.SelectedIndexChanged += delegate {
                RoleDef r = Cur;
                if (r != null && cbReasoning.SelectedIndex >= 0 && !r.IsLeader)
                    r.Reasoning = (string)cbReasoning.SelectedItem;
            };

            lblDesc = new Label { Text = "M\u00d4 T\u1e2A CHO AGENT", Font = P.FSmallB, ForeColor = P.Dim,
                Location = new Point(PadL, 330), Size = new Size(270, 16) };
            tbDesc = new TextBox { Location = new Point(PadL, 348), Size = new Size(270, 120),
                Multiline = true, ScrollBars = ScrollBars.Vertical, Font = P.FUi,
                BackColor = P.Box, ForeColor = P.Fg, BorderStyle = BorderStyle.FixedSingle };

            leftBox.Controls.Add(lblTeam); leftBox.Controls.Add(lbRoles);
            leftBox.Controls.Add(tbNew); leftBox.Controls.Add(btnAdd);
            leftBox.Controls.Add(btnRename); leftBox.Controls.Add(btnDel);
            leftBox.Controls.Add(lblReasoning); leftBox.Controls.Add(cbReasoning);
            leftBox.Controls.Add(lblDesc); leftBox.Controls.Add(tbDesc);
        }

        void BuildRight()
        {
            rightBox = new Panel { Dock = DockStyle.Fill, BackColor = P.Form };
            lblPick = new Label { Text = "CH\u1ec6N MODEL CHO VAI TR\u00d2", Font = P.FSmallB,
                ForeColor = P.Dim, Location = new Point(8, 10), Size = new Size(300, 16) };

            filterCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Location = new Point(8, 30), Size = new Size(500, 40), Padding = new Padding(1) };
            tbFilter = new TextBox { Dock = DockStyle.Fill, Font = P.FUiB, BorderStyle = BorderStyle.None,
                BackColor = P.Box, ForeColor = P.Fg, Margin = new Padding(10, 0, 0, 0) };
            CheckBox chk = new CheckBox { Text = "Ch\u1ec9 combo", AutoSize = true, ForeColor = P.Fg,
                BackColor = P.Box, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleLeft };
            chk.CheckedChanged += delegate { comboOnly = chk.Checked; UpdateResults(); };
            filterCard.Controls.Add(tbFilter); filterCard.Controls.Add(chk);

            list = new RowListView(delegate (Graphics g, Rectangle r, int i, bool isSel, bool isFoc) {
                if (i < 0 || i >= rows.Count) return;
                DrawModelRow(g, r, rows[i], isSel, isFoc);
            }, RowH);
            list.BackColor = P.Box;
            list.Dock = DockStyle.Fill;
            list.RowDoubleClicked += delegate { Assign(); };
            listCard = new CardPanel { Border = P.Line, Fill = P.Box, Radius = 10,
                Dock = DockStyle.Fill, Padding = new Padding(1) };
            listCard.Controls.Add(list);

            btnAssign = AgentBtn("G\u00e1n model n\u00e0y cho vai tr\u00f2", P.Sel, P.Fg, true);
            btnAssign.Location = new Point(8, 500); btnAssign.Size = new Size(240, 32);
            btnAssign.Click += delegate { Assign(); };

            lblFlow = new Label { Font = P.FSmallB, ForeColor = P.Dim, AutoSize = false,
                Location = new Point(8, 540), Size = new Size(500, 18), AutoEllipsis = true,
                Text = "USER > LEADER (plan + delegate) > WORKERS > QC / REVIEW > LEADER FINAL REVIEW" };
            lblParallel = new Label { Font = P.FSmallB, ForeColor = P.Dim, AutoSize = true,
                Location = new Point(8, 560) };
            nudParallel = new NumericUpDown { Minimum = 1, Maximum = 32, Font = P.FUi,
                Location = new Point(120, 556), Width = 66 };
            nudParallel.ValueChanged += delegate { maxParallel = (int)nudParallel.Value; };

            lblState = new Label { Font = P.FSmall, ForeColor = P.Dim, AutoSize = false,
                Location = new Point(8, 590), Size = new Size(500, 18), AutoEllipsis = true };

            rightBox.Controls.Add(listCard); rightBox.Controls.Add(lblPick);
            rightBox.Controls.Add(filterCard); rightBox.Controls.Add(btnAssign);
            rightBox.Controls.Add(lblFlow); rightBox.Controls.Add(lblParallel);
            rightBox.Controls.Add(nudParallel); rightBox.Controls.Add(lblState);
        }

        void BuildFooter()
        {
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = FootH, BackColor = P.Form };
            Button btnSavePreset = AgentBtn("L\u01b0u preset", P.RowA, P.Fg, false);
            btnSavePreset.Location = new Point(PadL, 16); btnSavePreset.Size = new Size(100, 28);
            btnSavePreset.Click += delegate { SavePreset(); };
            tbPreset = new TextBox { Location = new Point(PadL + 106, 18),
                Size = new Size(140, 24), Font = P.FUi, BorderStyle = BorderStyle.FixedSingle };
            cbPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = P.FUi,
                Location = new Point(PadL + 252, 18), Size = new Size(160, 24) };
            Button btnLoadPreset = AgentBtn("N\u1ea1p preset", P.RowA, P.Fg, false);
            btnLoadPreset.Location = new Point(PadL + 418, 16); btnLoadPreset.Size = new Size(100, 28);
            btnLoadPreset.Click += delegate { LoadPreset(); };

            btnClose = AgentBtn("\u0110\u00f3ng", P.RowA, P.Fg, false);
            btnClose.Size = new Size(90, 30); btnClose.Dock = DockStyle.Right;
            btnClose.Click += delegate { Close(); };
            btnSave = AgentBtn("\u00c1P D\u1ee4NG MULTI-AGENT", P.Acc, Color.White, true);
            btnSave.Size = new Size(210, 30); btnSave.Dock = DockStyle.Right;
            btnSave.Click += delegate { Save(); };

            footer.Controls.Add(tbPreset); footer.Controls.Add(btnSavePreset);
            footer.Controls.Add(cbPreset); footer.Controls.Add(btnLoadPreset);
            footer.Controls.Add(btnSave); footer.Controls.Add(btnClose);
            Controls.Add(rightBox); Controls.Add(leftBox); Controls.Add(footer);
        }

        void LayoutAll()
        {
            if (leftBox == null || rightBox == null) return;
            int W = ClientSize.Width, H = ClientSize.Height;
            int bodyH = H - FootH - 12;
            int LW = Math.Max(300, Math.Min(360, W / 3));
            leftBox.Width = LW;
            int iw = LW - PadL - 18;

            int y = 10;
            lblTeam.Location = new Point(PadL, y);
            lbRoles.Location = new Point(PadL, y + 20);
            lbRoles.Width = iw;
            int rolesH = Math.Max(100, Math.Min(200, (int)(bodyH * 0.34)));
            lbRoles.Height = rolesH;
            y += 22 + rolesH;

            tbNew.Location = new Point(PadL, y);
            tbNew.Width = Math.Max(90, iw - 98);
            btnAdd.Location = new Point(PadL + tbNew.Width + 4, y - 1);
            btnAdd.Width = iw - tbNew.Width - 4;
            btnAdd.Height = 26;
            y += 32;

            btnRename.Location = new Point(PadL, y);
            btnRename.Width = (iw - 4) / 2;
            btnDel.Location = new Point(PadL + btnRename.Width + 4, y);
            btnDel.Width = iw - btnRename.Width - 4;
            btnRename.Height = btnDel.Height = 26;
            y += 30;

            lblReasoning.Location = new Point(PadL, y + 5);
            cbReasoning.Location = new Point(PadL + 68, y);
            y += 26;

            lblDesc.Location = new Point(PadL, y);
            lblDesc.Width = iw;
            int descH = Math.Max(60, bodyH - (y + 16 - 10) - 4);
            tbDesc.Location = new Point(PadL, y + 18);
            tbDesc.Width = iw;
            tbDesc.Height = descH;

            // ---- phai ----
            int rx = 10, rw = W - LW - PadR - 4;
            lblPick.Location = new Point(rx, 10);
            filterCard.Location = new Point(rx, 30);
            filterCard.Width = rw; filterCard.Height = 40;

            int bottomH = 74;
            int listTop = 78;
            int listH = Math.Max(80, bodyH - listTop - bottomH - 24);
            listCard.Location = new Point(rx, listTop);
            listCard.Width = rw; listCard.Height = listH;
            listCard.PerformLayout();
            if (list.Size.Width <= 0 || list.Size.Height <= 0)
                list.Bounds = new Rectangle(1, 1, Math.Max(10, rw - 2), Math.Max(10, listH - 2));

            btnAssign.Location = new Point(rx, listTop + listH + 4);
            btnAssign.Size = new Size(Math.Min(260, rw), 32);

            int fy = listTop + listH + 40;
            lblFlow.Location = new Point(rx, fy);
            lblFlow.Width = rw; lblFlow.Height = 16;
            lblParallel.Text = "Max song song:";
            lblParallel.Location = new Point(rx, fy + 24);
            nudParallel.Location = new Point(rx + 96, fy + 20);
            lblState.Location = new Point(rx, fy + 24);
            lblState.Width = Math.Max(120, rw - 120);
        }

        static Button AgentBtn(string text, Color back, Color fore, bool primary)
        {
            Button b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = back,
                ForeColor = fore, Font = Palette.Shared.FUiB, UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Palette.Shared.Line;
            b.FlatAppearance.MouseOverBackColor = primary ? Palette.Shared.Sel : Palette.Shared.RowA;
            b.FlatAppearance.MouseDownBackColor = Palette.Shared.Sel;
            Color dim = Palette.Shared.Dim;
            b.EnabledChanged += delegate { b.ForeColor = b.Enabled ? fore : dim; };
            return b;
        }

        // ---------- danh sach vai tro ----------
        void FillRoles(int sel)
        {
            lbRoles.BeginUpdate();
            lbRoles.Items.Clear();
            foreach (RoleDef r in roles)
            {
                string m = string.IsNullOrEmpty(r.Model) ? "(ch\u01b0a ch\u1ecdn model)" : r.Model;
                lbRoles.Items.Add(r.Name + "   \u00b7   " + m + "   \u00b7   "
                    + Roles.NormalReasoning(r.Reasoning));
            }
            lbRoles.EndUpdate();
            if (roles.Count > 0) lbRoles.SelectedIndex = Math.Max(0, Math.Min(sel, roles.Count - 1));
        }

        RoleDef Cur { get { int i = lbRoles.SelectedIndex; return (i >= 0 && i < roles.Count) ? roles[i] : null; } }

        void OnRoleChanged()
        {
            RoleDef r = Cur;
            bool leader = (r != null && r.IsLeader);
            tbDesc.Enabled = (r != null && !leader);
            tbDesc.Text = (r == null) ? "" : r.Description;
            if (cbReasoning != null)
            {
                cbReasoning.Enabled = (r != null && !leader);
                cbReasoning.SelectedItem = (r == null) ? null : Roles.NormalReasoning(r.Reasoning);
            }
            if (btnDel != null) btnDel.Enabled = (r != null && !leader);
            if (btnRename != null) btnRename.Enabled = (r != null && !leader);
            lblPick.Text = (r == null) ? "CH\u1ec6N MODEL CHO VAI TR\u00d2"
                : "CH\u1ec6N MODEL CHO: " + r.Name.ToUpperInvariant();
            UpdateState();
        }

        void AddRole()
        {
            string n = tbNew.Text.Trim();
            if (n.Length == 0)
            {
                // chua go ten -> gan mau co san, tranh de nguoi dung ngon
                foreach (string t in Templates)
                {
                    bool used = false;
                    foreach (RoleDef r in roles)
                        if (r.Name.Equals(t, StringComparison.OrdinalIgnoreCase)) used = true;
                    if (!used) { n = t; break; }
                }
                if (n.Length == 0) { Warn("Khong con mau trong. Go ten vai tro."); return; }
            }
            if (!Roles.IsValidName(n)) { Warn("Ten chi gom chu, so, gach duoi va gach ngang."); return; }
            foreach (RoleDef r in roles)
                if (r.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) { Warn("Da co vai tro \"" + n + "\"."); return; }
            roles.Add(new RoleDef { Name = n, Description = Roles.DefaultDescription(n) });
            tbNew.Clear();
            FillRoles(roles.Count - 1);
        }

        void RenameRole()
        {
            int i = lbRoles.SelectedIndex;
            if (i < 0 || roles[i].IsLeader) { Warn("Chi doi ten duoc vai tro, khong doi ten leader."); return; }
            string n = tbNew.Text.Trim();
            if (n.Length == 0) { Warn("Go ten moi vao o ben tren roi bam Đổi tên."); return; }
            if (!Roles.IsValidName(n)) { Warn("Ten chi gom chu, so, gach duoi va gach ngang."); return; }
            foreach (RoleDef r in roles)
                if (!ReferenceEquals(r, roles[i]) && r.Name.Equals(n, StringComparison.OrdinalIgnoreCase))
                { Warn("Da co vai tro \"" + n + "\"."); return; }
            roles[i].Name = n;
            tbNew.Clear();
            FillRoles(i);
        }

        void DelRole()
        {
            int i = lbRoles.SelectedIndex;
            if (i < 0) return;
            if (roles[i].IsLeader) { Warn("Khong xoa duoc leader."); return; }
            if (MessageBox.Show("Xoá vai trò \"" + roles[i].Name + "\"?", "9Router",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            roles.RemoveAt(i);
            FillRoles(Math.Max(0, i - 1));
        }

        // ---------- danh sach model ----------
        void UpdateResults()
        {
            if (all == null)
            {
                rows = new List<Hit>();
                list.SetItems(0, -1);
                list.EmptyText = "Chua co danh sach model.\nBat 9Router roi bam Làm mới o cua so chinh.";
                return;
            }
            List<ModelInfo> src = all;
            if (comboOnly)
            {
                src = new List<ModelInfo>();
                foreach (ModelInfo m in all) if (m.IsCombo) src.Add(m);
            }
            rows = Matcher.Search(src, tbFilter.Text);
            list.SetItems(rows.Count, rows.Count > 0 ? 0 : -1);
            list.EmptyText = (rows.Count == 0 && tbFilter.Text.Trim().Length > 0)
                ? "Khong co mo hinh nao khop voi tu khoa."
                : "Danh sach mo hinh dang trong.";
            UpdateState();
        }

        void DrawModelRow(Graphics g, Rectangle r, Hit row, bool isSel, bool isFoc)
        {
            string ctx = "";
            if (row.M.Ctx > 0)
                ctx = (row.M.Ctx >= 1000000) ? (row.M.Ctx / 1000000.0).ToString("0.#") + "M"
                                             : (row.M.Ctx / 1000.0).ToString("0.#") + "k";
            string detail = row.M.IsCombo ? "combo" : row.M.Owner;
            if (ctx.Length > 0) detail += "   ·   " + ctx + " context";
            RoleDef cur = Cur;
            bool isCur = (cur != null && row.M.Id == cur.Model);
            if (isCur) detail = "● vai trò này đang dùng   ·   " + detail;
            RowPainter.Draw(g, r, P, row.M.Id, row.Pos, detail, isSel, isFoc, row.M.IsCombo, isCur, "COMBO");
        }

        void Assign()
        {
            RoleDef r = Cur;
            if (r == null) return;
            int i = list.SelectedIndex;
            if (i < 0 || i >= rows.Count) { Warn("Chọn một mô hình ở danh sách bên phải trước."); return; }
            r.Model = rows[i].M.Id;
            FillRoles(lbRoles.SelectedIndex);
        }

        void UpdateState()
        {
            RoleDef r = Cur;
            if (r == null) { lblState.Text = ""; return; }
            lblState.Text = string.IsNullOrEmpty(r.Model)
                ? "Chưa chọn model cho vai trò này."
                : "Đang gán: " + r.Model + "  ·  " + Roles.NormalReasoning(r.Reasoning);
        }

        // ---------- preset ----------
        void SavePreset()
        {
            string n = tbPreset == null ? "" : tbPreset.Text.Trim();

            if (n.Length == 0) { Warn("Gõ tên preset trước đã."); return; }
            try { Roles.SavePreset(n, maxParallel, roles); FillPresetList(n); }
            catch (Exception ex) { Warn("Không lưu được preset: " + ex.Message); }
        }

        void LoadPreset()
        {
            string n = (cbPreset.SelectedItem != null) ? cbPreset.SelectedItem.ToString()
                  : (tbPreset == null ? "" : tbPreset.Text.Trim());
            if (n.Length == 0) { Warn("Chọn một preset trước đã."); return; }
            try
            {
                int mp;
                List<RoleDef> got = Roles.LoadPreset(n, out mp);
                roles = Roles.Read(configPath);
                foreach (RoleDef g in got)
                {
                    RoleDef t = null;
                    foreach (RoleDef x in roles)
                        if (x.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase)) t = x;
                    if (t == null) { roles.Add(g); continue; }
                    t.Model = g.Model; t.Reasoning = g.Reasoning; t.Description = g.Description;
                }
                maxParallel = mp;
                nudParallel.Value = Math.Max(1, Math.Min(32, mp));
                FillRoles(0); UpdateResults();
            }
            catch (Exception ex) { Warn("Không nạp được preset: " + ex.Message); }
        }

        void FillPresetList(string sel)
        {
            if (cbPreset == null) return;
            cbPreset.Items.Clear();
            foreach (string p in Roles.ListPresets()) cbPreset.Items.Add(p);
            cbPreset.SelectedItem = sel;
            tbPreset.Text = sel;
        }

        // ---------- ap dung ----------
        void Save()
        {
            int workers = 0;
            foreach (RoleDef r in roles) if (!r.IsLeader) workers++;
            for (int i = 0; i < roles.Count; i++)
                if (!roles[i].IsLeader && string.IsNullOrEmpty(roles[i].Model))
                { Warn("Vai trò \"" + roles[i].Name + "\" chưa có model."); lbRoles.SelectedIndex = i; return; }
            if (workers == 0) { Warn("Cần ít nhất 1 vai trò ngoài leader."); return; }
            if (string.IsNullOrEmpty(roles[0].Model)) { Warn("Chưa chọn model cho leader."); return; }
            for (int i = 0; i < roles.Count; i++)
                if (!roles[i].IsLeader && string.IsNullOrEmpty((roles[i].Description ?? "").Trim()))
                    roles[i].Description = Roles.DefaultDescription(roles[i].Name);

            StringBuilder sb = new StringBuilder();
            sb.Append("Mode: MULTI-AGENT").Append(Environment.NewLine).Append(Environment.NewLine);
            foreach (RoleDef r in roles)
                sb.Append("  ").Append(r.Name).Append("  →  ")
                  .Append(string.IsNullOrEmpty(r.Model) ? "(chưa chọn)" : r.Model)
                  .Append("   (").Append(Roles.NormalReasoning(r.Reasoning)).Append(")")
                  .Append(Environment.NewLine);
            sb.Append(Environment.NewLine).Append("  Max song song: ").Append(maxParallel).Append(Environment.NewLine);
            sb.Append(Environment.NewLine).Append("Tool sẽ tự tạo bản sao config.toml trước khi ghi.");
            if (MessageBox.Show("Áp dụng cấu hình này?" + Environment.NewLine + Environment.NewLine + sb.ToString(),
                    "9Router", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                Roles.Write(configPath, roles);
                foreach (RoleDef r in roles) if (!r.IsLeader) Roles.WriteFile(configPath, r);
                string cur = Cfg.Current(configPath);
                if (cur != roles[0].Model)
                {
                    string bak;
                    Cfg.SetModel(configPath, roles[0].Model, out bak);
                }
                string curR = Roles.LeaderReasoningIn(configPath);
                if (curR != Roles.NormalReasoning(roles[0].Reasoning))
                {
                    string bakR;
                    Cfg.SetReasoning(configPath, Roles.NormalReasoning(roles[0].Reasoning), out bakR);
                }
                Roles.WriteMaxParallel(configPath, maxParallel);
                MessageBox.Show("Đã áp dụng " + workers + " vai trò vào config.toml." + Environment.NewLine
                    + "Mở lại Codex App để nạp cấu hình mới.", "9Router",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { Warn("Không ghi được: " + ex.Message); }
        }

        void Warn(string m)
        {
            MessageBox.Show(m, "9Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- tien ich cho --selftest va --shotagents ----------
        public int TotalModels { get { return (all == null) ? 0 : all.Count; } }
        public int VisibleRows { get { return rows.Count; } }
        public Size ModelListSize { get { return (list == null) ? Size.Empty : list.Size; } }
        public int ModelListRows { get { return (list == null || RowH <= 0) ? 0 : list.ClientSize.Height / RowH; } }
        public int TestFilter(string text)
        {
            tbFilter.Text = text ?? "";
            UpdateResults();
            return rows.Count;
        }
        public void PrepareForShot()
        {
            LayoutAll();
            FillRoles(0);
            tbFilter.Text = "";
            UpdateResults();
            LayoutAll();
        }
        public void RenderSelf(Bitmap bmp) { NativeWin.CaptureScreen(bmp, Handle); }
        // Do dong danh sach co ve duoc chu khong (RowListView tu ve trong OnPaint).
        public static int MeasureRowInk(string modelId)
        {
            Palette p = Palette.Shared;
            using (Bitmap bmp = new Bitmap(560, 40))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.FillRectangle(p.B["Row"], 0, 0, bmp.Width, bmp.Height);
                Hit h = new Hit { M = new ModelInfo { Id = modelId, Owner = "test",
                    IsCombo = false, Ctx = 128000, Lower = modelId.ToLowerInvariant() } };
                RowPainter.Draw(g, new Rectangle(0, 0, bmp.Width, 40), p, modelId,
                    new int[modelId.Length], "test   -   128k context", false, false,
                    false, false, "COMBO");
                int n = 0;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R < 200 || c.G < 200 || c.B < 200) n++;
                    }
                return n;
            }
        }
    }

}
