using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NineRouter
{
    internal sealed class ModelInfo
    {
        public string Id;
        public string Owner;
        public bool IsCombo;
        public int Ctx;
        public string Lower;
    }

    internal sealed class Hit
    {
        public ModelInfo M;
        public int Score;
        public int[] Pos;
    }

    // ------------------------------------------------------------------
    // Bo so khop mo: moi ky tu truy van phai xuat hien trong ten model
    // theo dung thu tu. Diem cao khi ky tu lien ke / bat dau chuoi /
    // ngay sau dau phan cach; tru khi co ky tu thua.
    // ------------------------------------------------------------------
    internal static class Matcher
    {
        public static Hit Score(string textLower, string queryLower)
        {
            int tn = textLower.Length, qn = queryLower.Length;
            if (qn == 0) return new Hit { M = null, Score = 0, Pos = new int[0] };
            if (qn > tn) return null;

            int[] pos = new int[qn];
            int ti = 0;
            for (int qi = 0; qi < qn; qi++)
            {
                char qc = queryLower[qi];
                int found = -1;
                while (ti < tn)
                {
                    if (textLower[ti] == qc) { found = ti; break; }
                    ti++;
                }
                if (found < 0) return null;
                pos[qi] = found;
                ti = found + 1;
            }

            int score = 0;
            for (int i = 0; i < qn; i++)
            {
                int p = pos[i];
                score += 16;
                if (p == 0)
                {
                    score += 40;
                }
                else
                {
                    char c = textLower[p - 1];
                    if (c == '/' || c == '-' || c == '_' || c == '.' || c == ':' || c == ' ' || c == '\\')
                        score += 28;
                    else if (char.IsLower(c) && char.IsUpper(textLower[p]))
                        score += 10;
                }
                if (i > 0)
                {
                    int gap = p - pos[i - 1] - 1;
                    if (gap == 0) score += 26;
                    else score -= Math.Min(gap, 14);
                }
            }
            score -= Math.Min(tn - qn, 60);

            if (textLower == queryLower) score += 600;
            else if (textLower.StartsWith(queryLower, StringComparison.Ordinal)) score += 220;
            else if (textLower.IndexOf(queryLower, StringComparison.Ordinal) >= 0) score += 90;

            return new Hit { Score = score, Pos = pos };
        }

        public static List<Hit> Search(List<ModelInfo> all, string query)
        {
            string q = (query == null ? "" : query).Trim().ToLowerInvariant();
            List<Hit> hits = new List<Hit>();
            for (int i = 0; i < all.Count; i++)
            {
                Hit h = Score(all[i].Lower, q);
                if (h != null) hits.Add(new Hit { M = all[i], Score = h.Score, Pos = h.Pos });
            }
            if (q.Length == 0)
                hits.Sort(delegate (Hit a, Hit b) {
                    if (a.M.IsCombo != b.M.IsCombo) return a.M.IsCombo ? -1 : 1;
                    return string.Compare(a.M.Id, b.M.Id, StringComparison.OrdinalIgnoreCase);
                });
            else
                hits.Sort(delegate (Hit a, Hit b) {
                    if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                    return string.Compare(a.M.Id, b.M.Id, StringComparison.OrdinalIgnoreCase);
                });
            return hits;
        }

        public static bool IsSubsequence(string text, string query)
        {
            string t = text.ToLowerInvariant(), q = query.ToLowerInvariant();
            int i = 0;
            for (int k = 0; k < q.Length; k++)
            {
                int idx = t.IndexOf(q[k], i);
                if (idx < 0) return false;
                i = idx + 1;
            }
            return true;
        }
    }

    internal static class Router
    {
        public static string Url = "http://localhost:20128";

        public static List<ModelInfo> Load()
        {
            string json;
            System.Net.WebClient wc = new System.Net.WebClient();
            wc.Encoding = Encoding.UTF8;
            try { json = wc.DownloadString(Url.TrimEnd('/') + "/v1/models"); }
            finally { wc.Dispose(); }

            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 32 * 1024 * 1024;
            Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
            System.Collections.IList arr = root["data"] as System.Collections.IList;
            if (arr == null) throw new InvalidDataException("Phần dữ liệu mô hình có định dạng lạ");

            List<ModelInfo> list = new List<ModelInfo>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                Dictionary<string, object> d = arr[i] as Dictionary<string, object>;
                if (d == null) continue;
                string id = Convert.ToString(d["id"]);
                if (id == null) continue;
                string owner = Convert.ToString(d["owned_by"]) ?? "";
                int ctx = 0;
                object c;
                if (d.TryGetValue("context_length", out c) && c != null)
                    ctx = Convert.ToInt32(c);
                list.Add(new ModelInfo {
                    Id = id, Owner = owner, IsCombo = (owner == "combo"),
                    Ctx = ctx, Lower = id.ToLowerInvariant()
                });
            }
            return list;
        }
    }

    internal sealed class BackupInfo
    {
        public string Path;
        public string FileName;
        public DateTime Time;
        public long Size;
        public string Model;   // model ghi trong ban sao do
    }
    // ==================================================================
    // Tu dong noi 9Router voi Codex tren may moi
    // ==================================================================
    internal static class Connector
    {
        public const string ProviderId = "9router";

        // 1. 9Router co chay khong?
        public static bool RouterAlive(string url)
        {
            // CHI HEAD, KHONG tai ve /v1/models: file do ~580 KB / 1295 model va mat
            // 5+ giay. Ham nay chay DONG BO trong constructor cua ConnectForm nen
            // tai no se lam cua so "Ket noi 9Router" dung hinh.
            string baseUrl = (url ?? "").TrimEnd('/');
            try {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)
                    System.Net.WebRequest.Create(baseUrl + "/v1/models");
                req.Method = "HEAD";
                req.Timeout = 3000;
                req.ReadWriteTimeout = 3000;
                using (System.Net.HttpWebResponse r = (System.Net.HttpWebResponse)req.GetResponse())
                    return (int)r.StatusCode == 200;
            } catch (System.Net.WebException we) {
                // May chu tra 405/501 cho HEAD -> thu GET "/" va bo qua body.
                System.Net.HttpWebResponse resp = we.Response as System.Net.HttpWebResponse;
                if (resp == null) return false;
                int code = (int)resp.StatusCode;
                resp.Close();
                if (code != 405 && code != 501) return false;
            } catch { return false; }
            try {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)
                    System.Net.WebRequest.Create(baseUrl + "/");
                req.Method = "GET";
                req.Timeout = 3000;
                using (System.Net.HttpWebResponse r = (System.Net.HttpWebResponse)req.GetResponse())
                    return (int)r.StatusCode < 400;
            } catch { return false; }
        }

        // 2. Node co san de doc file luu API key cua 9Router khong? (9Router la app npm)
        static bool NodeReady()
        {
            try {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "9router", "runtime", "node_modules", "better-sqlite3", "build", "Release");
                bool module = Directory.Exists(Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "9router", "runtime", "node_modules", "better-sqlite3"));
                bool db = File.Exists(Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "9router", "db", "data.sqlite"));
                return module && db;
            } catch { return false; }
        }

        // 3. Tu dong lay API key tu co so du lieu cua 9Router (dung node + better-sqlite3)
        public static string AutoDetectKey()
        {
            if (!NodeReady()) return null;
            try {
                string dir = Path.Combine(Path.GetTempPath(), "9r-key-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".js");
                string js =
                    "const D=require(process.argv[2]);const db=new D(process.argv[3],{readonly:true});" +
                    "const r=db.prepare(\"SELECT key FROM apiKeys WHERE isActive=1 LIMIT 1\").get();" +
                    "if(r)process.stdout.write(r.key);";
                System.IO.File.WriteAllText(dir, js, new UTF8Encoding(false));
                ProcessStartInfo psi = new ProcessStartInfo("node", "\"" + dir + "\" \"" +
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "9router", "runtime", "node_modules", "better-sqlite3") + "\" \"" +
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "9router", "db", "data.sqlite") + "\"");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process pr = Process.Start(psi))
                {
                    // PHẢI đọc cả hai luồng bằng bất đồng bộ, nếu không node sẽ kẹt
                    // khi ống stderr đầy và ReadToEnd chờ mãi -> treo cửa sổ.
                    Task<string> tOut = pr.StandardOutput.ReadToEndAsync();
                    Task<string> tErr = pr.StandardError.ReadToEndAsync();
                    if (!pr.WaitForExit(20000)) { try { pr.Kill(); } catch { } }
                    string outp = "";
                    try { outp = tOut.Result; } catch { }
                    try { tErr.Wait(2000); } catch { }
                    try { File.Delete(dir); } catch { }
                    outp = (outp ?? "").Trim();
                    return (outp.Length > 20) ? outp : null;
                }
            } catch { return null; }
        }

        // 4. config.toml da co khoi [model_providers.9router] chua?
        public static bool HasProvider(string cfgPath)
        {
            if (!File.Exists(cfgPath)) return false;
            foreach (string line in File.ReadAllLines(cfgPath))
                if (line.TrimStart().StartsWith("[model_providers." + ProviderId + "]")) return true;
            return false;
        }

        // 5. Da co san provider + co dung dia chi 9Router chua?
        // localhost và 127.0.0.1 là cùng một máy, không được so khác nhau
        public static string NormalizeUrl(string u)
        {
            return (u ?? "").Trim().TrimEnd('/').Replace("localhost", "127.0.0.1").ToLowerInvariant();
        }

        public static bool IsFullyWired(string cfgPath, string url)
        {
            if (!HasProvider(cfgPath)) return false;
            string b = BaseUrlOf(cfgPath);
            if (string.IsNullOrEmpty(b)) return false;
            return NormalizeUrl(b).EndsWith(NormalizeUrl(url) + "/v1", StringComparison.Ordinal);
        }

        // Doc base_url trong khoi [model_providers.9router].
        // KHONG quet ca file: mot comment cu chua URL se lam "da ket noi" sai.
        static string BaseUrlOf(string cfgPath)
        {
            try
            {
                if (!File.Exists(cfgPath)) return null;
                bool inBlock = false;
                foreach (string line in File.ReadAllLines(cfgPath))
                {
                    string tr = line.Trim();
                    if (tr.StartsWith("["))
                    {
                        string name = tr.TrimStart('[').TrimEnd(']').Trim();
                        inBlock = name.Equals("model_providers." + ProviderId, StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inBlock) continue;
                    if (TomlKeyOf(tr) != "base_url") continue;
                    return ReadTomlString(tr);
                }
            }
            catch { }
            return null;
        }

        // 6. Ghi khoi provider vao config.toml (co backup)
        public static string Wire(string cfgPath, string baseUrl, string key)
        {
            if (!File.Exists(cfgPath)) throw new FileNotFoundException("Không tìm thấy config.toml: " + cfgPath);
            if (HasProvider(cfgPath)) return "provider đã có sẵn";
            string backup = cfgPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(cfgPath, backup, true);

            string block =
                Environment.NewLine +
                Marker + Environment.NewLine +
                "[model_providers." + ProviderId + "]" + Environment.NewLine +
                "name = \"9Router\"" + Environment.NewLine +
                "base_url = \"" + baseUrl.TrimEnd('/') + "/v1\"" + Environment.NewLine +
                "wire_api = \"responses\"" + Environment.NewLine + Environment.NewLine +
                "[model_providers." + ProviderId + ".http_headers]" + Environment.NewLine +
                "Authorization = \"Bearer " + key + "\"" + Environment.NewLine;

            File.AppendAllText(cfgPath, block, new UTF8Encoding(false));
            return backup;
        }

        // ==================================================================
        // 7. NGAT KET NOI: xoa khoi provider 9Router va dua Codex ve mac dinh
        //
        // Chi xoa DUNG phan tool da them. Khong dung "xoa moi dong" hay
        // "ghi de ca file", de moi cau hinh nguoi dung dung sau do
        // (sandbox_mode, plugins, projects, notify...) deu con nguyen.
        // ==================================================================
        // Ten ban sao luu vua tao boi Unwire (de hien thi cho nguoi dung).
        public static string LastBackup;

        public const string Marker = "# --- Thêm bởi 9Router · Codex · Đổi model ---";

        // Provider Codex tu dung khi khong con khai bao 9Router.
        // "codex doctor" hien provider nay o muc "default model provider".
        public const string DefaultProvider = "openai";

        // Config dang tro ve 9Router hay khong.
        public static bool IsWired(string cfgPath)
        {
            return HasProvider(cfgPath);
        }

        // Gia tri cua dong "model_provider" ("" = khong khai bao gi).
        // PHẢI so đúng tên khoa: "model_providers.9router" cũng bắt đầu
        // bằng chuỗi "model_provider", nen StartsWith se ra sai.
        public static string ProviderNameOf(string cfgPath)
        {
            try
            {
                if (!File.Exists(cfgPath)) return "";
                foreach (string line in File.ReadAllLines(cfgPath))
                {
                    string tr = line.Trim();
                    if (TomlKeyOf(tr) != "model_provider") continue;
                    string v = ReadTomlString(tr);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch { }
            return "";
        }

        // Model trong config co phai cua 9Router hay khong.
        public static bool ModelIsFrom9Router(string cfgPath)
        {
            return ProviderNameOf(cfgPath).IndexOf(ProviderId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Xoa khoi [model_providers.9router] va cac dong tro toi no.
        /// <paramref name="fallbackModel"/> rong = xoa dong model de Codex
        /// TU CHON model mac dinh cua no (doctor se hien "&lt;default&gt;").
        /// Tra ve 1 neu da co thay doi, 0 neu khong.
        /// </summary>
        public static int Unwire(string cfgPath, string fallbackModel)
        {
            return Unwire(cfgPath, fallbackModel, null);
        }

        public static int Unwire(string cfgPath, string fallbackModel, List<string> log)
        {
            if (!File.Exists(cfgPath)) throw new FileNotFoundException("Không tìm thấy file config: " + cfgPath);
            string[] lines = File.ReadAllLines(cfgPath);
            List<string> keep = new List<string>(lines.Length);
            bool inBlock = false;
            bool topLevel = true;      // con o khoi goc chua vao bang nao
            bool changed = false;
            string fb = (fallbackModel ?? "").Trim();

            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                string tr = ln.Trim();

                // Bat dau / ket thuc khoi: [model_providers.9router]
                // va moi khoi con dang [model_providers.9router.xxx]
                if (tr.StartsWith("["))
                {
                    topLevel = false;
                    string name = tr.TrimStart('[').TrimEnd(']').Trim();
                    bool isOurs = name.Equals("model_providers." + ProviderId, StringComparison.OrdinalIgnoreCase)
                               || name.StartsWith("model_providers." + ProviderId + ".", StringComparison.OrdinalIgnoreCase);
                    if (isOurs) { inBlock = true; changed = true; if (log != null) log.Add("xoá khối [" + name + "]"); continue; }
                    inBlock = false;
                    keep.Add(ln);
                    continue;
                }

                if (inBlock) continue;   // noi dung ben trong khoi 9router -> bo

                // Comment danh dau do tool ghi ra
                if (tr.StartsWith(Marker))
                {
                    changed = true;
                    if (log != null) log.Add("xoá dòng chú thích của tool");
                    continue;
                }

                // CHI khop dung ten khoa. Dung StartsWith("model") se lam
                // model_reasoning_effort va model_verbosity cung bi doi nham.
                if (tr.Contains("="))
                {
                    string key = TomlKeyOf(tr);

                    // Dong model_provider tro toi 9router -> ve mac dinh cua Codex
                    if (key == "model_provider")
                    {
                        string v = ReadTomlString(ln);
                        if (v != null && v.IndexOf(ProviderId, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            changed = true;
                            if (fb.Length > 0) keep.Add("model_provider = \"" + DefaultProvider + "\"");
                            if (log != null) log.Add(fb.Length > 0
                                ? ("model_provider: " + v + " → " + DefaultProvider)
                                : "xoá dòng model_provider (Codex tự dùng nhà cung cấp mặc định)");
                            continue;
                        }
                    }

                    // Dong model / default_subagent_model dang tro ve model cua 9Router.
                    // "model" chi dung o khoi goc - trong bang khac co the la
                    // khoa trùng tên của nguoi dung, khong duoc dong vao.
                    if (key == "default_subagent_model" || (key == "model" && topLevel))
                    {
                        string v = ReadTomlString(ln);
                        if (LooksLike9RouterModel(v))
                        {
                            changed = true;
                            if (fb.Length > 0) keep.Add(key + " = \"" + fb + "\"");
                            if (log != null) log.Add(fb.Length > 0
                                ? (key + ": " + v + " → " + fb)
                                : ("xoá dòng " + key + " (Codex tự chọn model mặc định)"));
                            continue;
                        }
                    }
                }

                keep.Add(ln);
            }

            if (!changed) return 0;

            string backup = cfgPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(cfgPath, backup, true);
            File.WriteAllLines(cfgPath, TrimTrailingBlankRuns(keep).ToArray());
            LastBackup = Path.GetFileName(backup);
            return 1;
        }

        // Goi y model Codex nen dung sau khi ngat ket noi: uu tien model
        // trong ban sao luu gan nhat ma KHONG phai cua 9Router.
        public static string SuggestDefaultModel(string cfgPath)
        {
            try
            {
                if (!File.Exists(cfgPath)) return "";
                List<BackupInfo> all = Cfg.ListBackups(cfgPath);
                for (int i = 0; i < all.Count; i++)
                {
                    string m = all[i].Model;
                    if (!string.IsNullOrEmpty(m) && !LooksLike9RouterModel(m)) return m;
                }
            }
            catch { }
            return "";
        }        // Codex co dung OpenAI de goi API truc tiep khong. Neu thieu,
        // nguoi dung phai "codex login" lai truoc khi chay duoc.
        public static bool HasOpenAiAuth()
        {
            try
            {
                string home = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (string.IsNullOrEmpty(home))
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.codex";
                string f = Path.Combine(home, "auth.json");
                if (!File.Exists(f)) return false;
                string t2 = File.ReadAllText(f);
                if (t2.IndexOf("\"auth_mode\"", StringComparison.OrdinalIgnoreCase) < 0) return false;
                // "none" / "apikey" deu chay duoc; chi "chatgai" moi can mo cua so.
                return true;
            }
            catch { return true; }   // khong doc duoc thi coi nhu du, khong chan nguoi dung
        }

        // Lay ten khoa TOML truoc dau '='. Tra ve "" neu dong khong phai
        // khoa - gia tri (vi du: mang, comment, dong rong).
        static string TomlKeyOf(string trimmedLine)
        {
            int eq = trimmedLine.IndexOf('=');
            if (eq <= 0) return "";
            string k = trimmedLine.Substring(0, eq).Trim();
            foreach (char c in k)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return "";
            return k;
        }
        // Doc gia tri chuoi trong 'key = "gia tri"'.
        static string ReadTomlString(string line)
        {
            int a = line.IndexOf('"');
            if (a < 0) return null;
            int b = line.IndexOf('"', a + 1);
            if (b < 0) return null;
            return line.Substring(a + 1, b - a - 1);
        }

        // Model cua 9Router thuong KHONG phai ten model goc cua Codex:
        // "Openrouter", "Hung", "Nvidia/..."... deu la ten tuy chon cua 9Router.
        static bool LooksLike9RouterModel(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            if (v.IndexOf('/') >= 0) return true;                    // Nvidia/..., meta/...
            string lv = v.ToLowerInvariant();
            if (lv == "openrouter" || lv == "combo" || lv == "auto") return true;
            if (v.Length <= 12 && v.IndexOf('-') < 0 && v.IndexOf('_') < 0 && v.IndexOf('.') < 0
                && v.IndexOf(' ') < 0
                && !lv.StartsWith("gpt") && !lv.StartsWith("o1") && !lv.StartsWith("o3")
                && !lv.StartsWith("o4") && !lv.StartsWith("codex"))
                return true;                                          // ten ngan, tuy chon
            return false;
        }

        static List<string> TrimTrailingBlankRuns(List<string> lines)
        {
            List<string> r = new List<string>(lines);
            while (r.Count > 0 && r[r.Count - 1].Trim().Length == 0) r.RemoveAt(r.Count - 1);
            if (r.Count > 0) r.Add("");
            return r;
        }
    }
    // ==================================================================
    // Bo nho dem danh sach mo hinh: app mo len co du lieu ngay,
    // khong phai cho vai giay trong khi cho 9Router.
    // ==================================================================
    internal static class ModelCache
    {
        static string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "9RouterSwitcher");
        static string File_ { get { return Path.Combine(Dir, "models-cache.tsv"); } }

        public static void Save(List<ModelInfo> list)
        {
            try {
                if (list == null || list.Count == 0) return;
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                foreach (ModelInfo m in list)
                    sb.Append(m.Id.Replace("\t", " ").Replace("\n", " ")).Append('\t')
                      .Append(m.Owner.Replace("\t", " ")).Append('\t')
                      .Append(m.Ctx.ToString()).Append('\n');
                File.WriteAllText(File_ + ".tmp", sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(File_)) File.Delete(File_);
                File.Move(File_ + ".tmp", File_);
            } catch { }
        }

        public static List<ModelInfo> Load()
        {
            try {
                if (!File.Exists(File_)) return null;
                List<ModelInfo> list = new List<ModelInfo>();
                foreach (string line in File.ReadAllLines(File_))
                {
                    if (line.Length == 0) continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 2) continue;
                    int ctx = 0; int.TryParse(p[2], out ctx);
                    list.Add(new ModelInfo {
                        Id = p[0], Owner = p[1], IsCombo = (p[1] == "combo"),
                        Ctx = ctx, Lower = p[0].ToLowerInvariant() });
                }
                return (list.Count > 0) ? list : null;
            } catch { return null; }
        }
    }
    internal static class Cfg
    {
        public static string DefaultPath()
        {
            string home = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrEmpty(home))
                home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.codex";
            return home + "\\config.toml";
        }

        public static string Current(string path)
        {
            if (!File.Exists(path)) return null;
            foreach (string line in File.ReadAllLines(path))
            {
                Match mm = System.Text.RegularExpressions.Regex.Match(line, "^model\\s*=\\s*\"([^\"]+)\"");
                if (mm.Success) return mm.Groups[1].Value;
            }
            return null;
        }

        public static string SetModel(string path, string model, out string backup)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file config: " + path);
            string[] lines = File.ReadAllLines(path);
            List<string> outp = new List<string>(lines.Length);
            List<string> keys = new List<string>();
            var rxModel = new System.Text.RegularExpressions.Regex(@"^model\s*=");
            var rxAgent = new System.Text.RegularExpressions.Regex(@"^\s*default_subagent_model\s*=");
            // Che do nhieu agent: moi vai tro co model rieng trong file .toml
            // cua no. Keo "default_subagent_model" ve bang model Leader se
            // pha cau hinh da chon - nen bo qua dong nay.
            bool keepSubagent = Roles.Exists(path);
            // CHI dong "model" o khoi goc moi la model cua Codex. Trong [profile.x]
            // nguoi dung co the tu dat "model" rieng - KHONG duoc dong vao.
            // (Unwire da coi chung khuon nay, day la phan doi chieu cua no.)
            bool topLevel = true;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("["))
                {
                    topLevel = false;
                    outp.Add(lines[i]);
                    continue;
                }
                if (topLevel && rxModel.IsMatch(lines[i]))
                {
                    outp.Add("model = \"" + model + "\"");
                    keys.Add("model");
                }
                else if (rxAgent.IsMatch(lines[i]))
                {
                    // Da co vai tro -> giu nguyen dong cua nguoi dung, khong keo ve model leader.
                    if (keepSubagent) outp.Add(lines[i]);
                    else
                    {
                        outp.Add("default_subagent_model = \"" + model + "\"");
                        keys.Add("default_subagent_model");
                    }
                }
                else outp.Add(lines[i]);
            }
            if (keys.Count == 0) throw new InvalidOperationException("Không tìm thấy dòng mô hình trong " + path);
            backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(path, backup, true);
            File.WriteAllLines(path, outp.ToArray());
            return string.Join(", ", keys.ToArray());
        }

        // Sua model_reasoning_effort O KHOI GOC cua config.toml.
        // Chi dong o khoi goc - trong [profile.x] va trong file vai tro
        // nguoi dung tu dat, khong duoc dong vao.
        public static string SetReasoning(string path, string effort, out string backup)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file config: " + path);
            string[] lines = File.ReadAllLines(path);
            var rx = new System.Text.RegularExpressions.Regex(@"^model_reasoning_effort\s*=");
            List<string> outp = new List<string>(lines.Length);
            bool topLevel = true, done = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("["))
                {
                    topLevel = false;
                    outp.Add(lines[i]);
                    continue;
                }
                if (topLevel && rx.IsMatch(lines[i]))
                {
                    outp.Add("model_reasoning_effort = \"" + effort + "\"");
                    done = true;
                    continue;
                }
                outp.Add(lines[i]);
            }
            if (!done)
            {
                // Chua co dong -> them ngay sau dong model o khoi goc.
                int at = outp.Count;
                for (int i = 0; i < outp.Count; i++)
                {
                    if (outp[i].StartsWith("model ")) { at = i + 1; break; }
                }
                outp.Insert(at, "model_reasoning_effort = \"" + effort + "\"");
            }
            backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(path, backup, true);
            File.WriteAllLines(path, outp.ToArray());
            return done ? "model_reasoning_effort" : "model_reasoning_effort (them moi)";
        }

        // ---------- quan ly ban sao luu ----------

        public static string BackupPrefix(string cfgPath)
        {
            return Path.GetFileName(cfgPath) + ".bak-";
        }

        public static string FolderOf(string cfgPath)
        {
            return Path.GetDirectoryName(Path.GetFullPath(cfgPath));
        }

        // Doc ten model trong mot tep bat ky (khong phai config dang chay).
        public static string ModelIn(string file)
        {
            try
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    Match mm = System.Text.RegularExpressions.Regex.Match(line, "^model\\s*=\\s*\"([^\"]+)\"");
                    if (mm.Success) return mm.Groups[1].Value;
                }
            }
            catch { }
            return null;
        }

        // Danh sach ban sao, moi nhat truoc.
        public static List<BackupInfo> ListBackups(string cfgPath)
        {
            List<BackupInfo> list = new List<BackupInfo>();
            string dir = FolderOf(cfgPath);
            string prefix = BackupPrefix(cfgPath);
            if (!Directory.Exists(dir)) return list;
            foreach (string f in Directory.GetFiles(dir, prefix + "*"))
            {
                FileInfo fi = new FileInfo(f);
                DateTime t;
                string stamp = fi.Name.Substring(prefix.Length);
                if (!DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", null,
                        System.Globalization.DateTimeStyles.None, out t))
                    t = fi.LastWriteTime;
                list.Add(new BackupInfo {
                    Path = fi.FullName, FileName = fi.Name, Time = t,
                    Size = fi.Length, Model = ModelIn(fi.FullName)
                });
            }
            list.Sort(delegate (BackupInfo a, BackupInfo b) { return b.Time.CompareTo(a.Time); });
            return list;
        }

        // Khoi phuc: luon tao mot ban sao cua trang thai hien tai truoc khi ghi de.
        public static string Restore(string cfgPath, string backupPath)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("Không tìm thấy bản sao: " + backupPath);
            if (!File.Exists(cfgPath)) throw new FileNotFoundException("Không tìm thấy file config: " + cfgPath);
            string safety = cfgPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(cfgPath, safety, true);
            File.Copy(backupPath, cfgPath, true);
            return safety;
        }

        // Khoa an toan: chi xoa tep cung ten ban sao cua config dang thao tac,
        // va tep do phai nam cung thu muc voi config do.
        public static void DeleteBackup(string backupPath, string cfgPath)
        {
            if (!File.Exists(backupPath)) return;
            string dir = FolderOf(cfgPath);
            string full = Path.GetFullPath(backupPath);
            // Phai kem dau phan cach o cuoi, neu khong thi thu muc cung tien to
            // (vi du "...\.codex-x") se lot qua.
            string root = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chỉ cho phép xoá tệp nằm trong " + dir);
            if (!Path.GetFileName(full).StartsWith(BackupPrefix(cfgPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tệp này không phải bản sao của config này.");
            File.Delete(full);
        }
    }
}
