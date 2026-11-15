// Admin/AdminConsole.cs
// Admin console wiring + layout: dark sidebar, topbar, page shell,
// the access-denied page and the audit log page.
using System.Text;

public static class AdminConsole
{
    private static readonly (string group, string section, string label, string url)[] Nav =
    {
        ("", "dashboard", "Dashboard", "/admin/dashboard"),
        ("", "reservations", "Reservations", "/admin/reservations"),
        ("", "rooms", "Rooms & Inventory", "/admin/rooms"),
        ("", "calendar", "Availability Calendar", "/admin/calendar"),
        ("", "guests", "Guests / Users", "/admin/guests"),
        ("Financial", "payments", "Payments", "/admin/payments"),
        ("Financial", "billing", "Billing / Invoicing", "/admin/billing"),
        ("Operational", "staff", "Staff Management", "/admin/staff"),
        ("Operational", "housekeeping", "Housekeeping", "/admin/housekeeping"),
        ("Operational", "offers", "Offers / Promotions", "/admin/offers"),
        ("Settings & Analytics", "rates", "Rate Management", "/admin/rates"),
        ("Settings & Analytics", "reports", "Reports", "/admin/reports"),
        ("Settings & Analytics", "emails", "Email Templates", "/admin/emails"),
        ("Settings & Analytics", "settings", "System Settings", "/admin/settings"),
        ("Security", "accounts", "Accounts Database", "/admin/accounts"),
        ("Security", "audit", "Audit Logs", "/admin/audit"),
    };

    public static void Map(WebApplication app, List<Room> rooms, List<Booking> bookings,
        List<Transaction> transactions, List<Ticket> tickets, List<string> paymentMethods)
    {
        AdminData.Initialize(app.Environment.ContentRootPath, rooms, bookings, transactions, tickets, paymentMethods);

        AdminAuth.Map(app);
        AdminDashboard.Map(app);
        AdminReservations.Map(app);
        AdminPayments.Map(app);
        AdminBilling.Map(app);
        AdminRooms.Map(app);
        AdminCalendar.Map(app);
        AdminHousekeeping.Map(app);
        AdminGuests.Map(app);
        AdminStaff.Map(app);
        AdminOffers.Map(app);
        AdminRates.Map(app);
        AdminReports.Map(app);
        AdminEmail.Map(app);
        AdminSettings.Map(app);
        MapAudit(app);

        app.MapGet("/admin", (HttpContext ctx) =>
        {
            AdminUser? u = AdminAuth.UserOf(ctx);
            return Results.Redirect(u == null ? "/admin/login" : "/admin/dashboard");
        });

        // theme toggle: flips the account's dark mode preference and sends the
        // user back to the page they were on (same host, /admin only)
        app.MapPost("/admin/theme", (HttpContext ctx) =>
        {
            AdminUser? u = AdminAuth.UserOf(ctx);
            if (u == null) return Results.Redirect("/admin/login");
            u.Theme = u.Theme == "dark" ? "light" : "dark";
            AdminData.Save();

            string target = "/admin/dashboard";
            string referer = ctx.Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out Uri? r) && r.PathAndQuery.StartsWith("/admin"))
                target = r.PathAndQuery;
            return Results.Redirect(target);
        });
    }

    // ---------- page shell ----------

    public static IResult Page(AdminUser user, string section, string title, string body)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='shell'><aside class='sidebar'>");
        sb.Append("<div class='sbrand'><span class='sstar'>&#10022;</span><span class='slogo'>Azure</span><span class='ssub'><i></i>Cove Hotel<i></i></span><span class='sadmin'>Admin console</span></div>");
        string currentGroup = "\u0000";
        foreach (var item in Nav)
        {
            if (!AdminAuth.Can(user.Role, item.section)) continue;
            if (item.group != currentGroup)
            {
                sb.Append("<div class='sgroup'>" + AdminData.H(item.group) + "</div>");
                currentGroup = item.group;
            }
            sb.Append("<a class='sitem" + (item.section == section ? " active" : "") + "' href='" + item.url + "'>" + item.label + "</a>");
        }
        sb.Append("</aside><main class='main'><header class='topbar'>");
        sb.Append("<h1>" + AdminData.H(title) + "</h1>");
        sb.Append("<div class='user'><span class='uchip'>" + AdminData.H(user.Username) + " &middot; " + AdminData.H(user.Role) + "</span>");
        sb.Append("<a class='btn small gray' href='/admin/account'>My account</a>");
        sb.Append("<form method='post' action='/admin/theme'><button class='btn small gray' title='Toggle dark mode'>" + (user.Theme == "dark" ? "&#9788; Light mode" : "&#127769; Dark mode") + "</button></form>");
        sb.Append("<form method='post' action='/admin/logout'><button class='btn small gray'>Log out</button></form></div></header>");
        sb.Append("<div class='content'>" + body + "</div></main></div>");
        return Results.Content(FullHtml(title, sb.ToString(), user.Theme == "dark"), "text/html; charset=utf-8");
    }

    public static IResult DenyPage(AdminUser user, string section)
    {
        string body = "<div class='acard slim'><h2>Access denied</h2>"
            + "<p>Your role (<b>" + AdminData.H(user.Role) + "</b>) does not have access to this section.</p>"
            + "<a class='btn' href='/admin/dashboard'>Back to dashboard</a></div>";
        return Page(user, "dashboard", "Access denied", body);
    }

    public static string BarePage(string title, string body)
    {
        string wrap = "<div class='bare'>" + body + "</div>";
        return FullHtml(title, wrap, false);
    }

    private static string FullHtml(string title, string inner, bool dark)
    {
        return "<!DOCTYPE html><html><head><meta charset='utf-8'>"
            + "<meta name='viewport' content='width=device-width,initial-scale=1'>"
            + "<link rel='icon' type='image/jpeg' href='/photo/azure-icon.jpeg'>"
            + "<link rel='preconnect' href='https://fonts.googleapis.com'><link href='https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Poppins:wght@400;600;700&display=swap' rel='stylesheet'>"
            + "<title>" + AdminData.H(title) + " - Admin - Azure Cove Hotel</title><style>" + AdminCss + "</style></head><body" + (dark ? " class='dark'" : "") + ">" + inner + "</body></html>";
    }

    // ---------- audit log page ----------

    private static void MapAudit(WebApplication app)
    {
        app.MapGet("/admin/audit", (HttpContext ctx, string? user, string? action, string? from, string? to) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "audit");
            if (deny != null) return deny;
            AdminUser me = u!;

            DateTime fromD = DateTime.TryParse(from, out DateTime f) ? f.Date : DateTime.Today.AddDays(-30);
            DateTime toD = DateTime.TryParse(to, out DateTime t) ? t.Date : DateTime.Today;

            var sb = new StringBuilder();
            sb.Append("<form class='acard filt' method='get' action='/admin/audit'>");
            sb.Append("<label>User<input class='t' name='user' value='" + AdminData.H(user ?? "") + "' placeholder='any'></label>");
            sb.Append("<label>Action contains<input class='t' name='action' value='" + AdminData.H(action ?? "") + "' placeholder='any'></label>");
            sb.Append("<label>From<input class='t' type='date' name='from' value='" + AdminData.Ds(fromD) + "'></label>");
            sb.Append("<label>To<input class='t' type='date' name='to' value='" + AdminData.Ds(toD) + "'></label>");
            sb.Append("<button class='btn'>Filter</button></form>");

            sb.Append("<div class='acard'><table class='atable'><tr><th>Time</th><th>User</th><th>Action</th><th>Details</th><th>IP</th></tr>");
            int shown = 0;
            foreach (AuditEntry e in AdminData.AuditLog.OrderByDescending(x => x.Time))
            {
                if (e.Time.Date < fromD || e.Time.Date > toD) continue;
                if (user != null && user.Trim() != "" && !e.Username.Contains(user.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (action != null && action.Trim() != "" && !e.Action.Contains(action.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append("<tr><td class='nowrap'>" + e.Time.ToString("MMM d, HH:mm:ss") + "</td><td>" + AdminData.H(e.Username) + " <span class='muted'>(" + AdminData.H(e.Role) + ")</span></td>"
                    + "<td>" + AdminData.H(e.Action) + "</td><td>" + AdminData.H(e.Details) + "</td><td class='muted'>" + AdminData.H(e.Ip) + "</td></tr>");
                shown++;
                if (shown >= 300) { sb.Append("<tr><td colspan='5' class='muted'>Showing the 300 newest entries only.</td></tr>"); break; }
            }
            if (shown == 0) sb.Append("<tr><td colspan='5' class='muted'>No audit entries for this filter.</td></tr>");
            sb.Append("</table></div>");
            sb.Append("<p class='muted'>Every admin action is recorded automatically: logins, edits, refunds, status changes and account changes.</p>");

            return Page(me, "audit", "Audit Logs", sb.ToString());
        });
    }

    // ---------- admin stylesheet ----------

    public static string AdminCss = """
*{box-sizing:border-box}
body{margin:0;font-family:'Poppins','Segoe UI',Arial,sans-serif;background:var(--bg);color:var(--text);font-size:15px;color-scheme:light;--bg:#eef2f7;--card:#ffffff;--border:#e3e8f0;--line:#eef1f6;--text:#1a2233;--muted:#8a97ab;--muted2:#5a6b85;--thbg:#f4f6fa;--inputbg:#ffffff;--inputborder:#c9d2e0;--link:#0071c2;--btn:#0071c2;--btnhover:#00588f;--shadow:0 1px 2px #00000008;--okbg:#e6f4ea;--okfg:#0b7a2a;--warnbg:#fff3d6;--warnfg:#946200;--badbg:#fdecec;--badfg:#b02a2a;--infobg:#e3effb;--infofg:#0b5aa7;--mutbg:#eef1f6;--mutfg:#5a6b85;--calborder:#e8edf4;--cfree:#d3f9d8;--cpartial:#ffe8a3;--cfull:#ffb3b0;--cblack:#cfd4dd;--cpast:#f4f6fa;--stripe:#ffffff88}
body.dark{--bg:#0e1622;--card:#182234;--border:#2a3750;--line:#232f47;--text:#e2e8f4;--muted:#8fa0b9;--muted2:#a9b6cb;--thbg:#1f2b40;--inputbg:#111a29;--inputborder:#33415e;--link:#4da3ff;--btn:#1c7ed6;--btnhover:#1874c2;--shadow:none;--okbg:#12351f;--okfg:#69db7c;--warnbg:#3a2c0a;--warnfg:#ffd43b;--badbg:#3b1515;--badfg:#ff8787;--infobg:#12294a;--infofg:#74c0fc;--mutbg:#24304a;--mutfg:#aab6cc;--calborder:#2a3750;--cfree:#1d4027;--cpartial:#4d3a0d;--cfull:#5c201d;--cblack:#2a3750;--cpast:#161f30;--stripe:#00000055;color-scheme:dark}
a{color:var(--link);text-decoration:none}
button,input,select,textarea{font-family:inherit}
.shell{display:flex;min-height:100vh}
.sidebar{width:242px;flex-shrink:0;background:#0b2e59;color:#cdd8ee;padding:18px 0 26px}
.sbrand{padding:2px 18px 14px;border-bottom:1px solid #ffffff22;margin-bottom:10px;text-align:center;position:relative}
.sstar{position:absolute;top:2px;right:20px;color:#d4af37;font-size:14px}
.slogo{display:block;font-family:'Cormorant Garamond',serif;font-weight:600;font-size:31px;letter-spacing:6px;text-transform:uppercase;color:#fff;line-height:1.1}
.ssub{display:flex;align-items:center;gap:8px;font-size:10px;letter-spacing:3px;text-transform:uppercase;color:#e3c46c;margin-top:3px}
.ssub i{flex:1;height:1px;background:#c9a22766}
.sadmin{display:block;font-size:9px;letter-spacing:2.4px;text-transform:uppercase;color:#8fa9d4;margin-top:7px}
.sgroup{padding:14px 18px 4px;font-size:10px;letter-spacing:1.6px;text-transform:uppercase;color:#7f9ac7}
.sitem{display:block;padding:9px 18px;color:#cdd8ee;font-size:13.5px;border-left:3px solid transparent}
.sitem:hover{background:#ffffff10;color:#fff}
.sitem.active{background:#ffffff14;color:#fff;border-left-color:#febb02;font-weight:600}
.main{flex:1;min-width:0;display:flex;flex-direction:column}
.topbar{background:var(--card);border-bottom:1px solid var(--border);padding:14px 24px;display:flex;justify-content:space-between;align-items:center;gap:12px}
.topbar h1{margin:0;font-size:22px;color:var(--text);font-family:'Cormorant Garamond',serif;font-weight:600}
.user{display:flex;align-items:center;gap:10px}
.uchip{background:var(--bg);border:1px solid var(--border);color:var(--text);border-radius:999px;padding:5px 12px;font-size:13px}
.content{padding:22px;max-width:1200px}
.acard{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:18px 20px;margin-bottom:18px;box-shadow:var(--shadow)}
.acard.slim{max-width:520px}
.acard h2{margin:0 0 10px;font-size:20px;font-family:'Cormorant Garamond',serif;font-weight:600}
.acard h3{margin:16px 0 8px;font-size:15px;text-transform:uppercase;letter-spacing:1px;color:var(--muted2);font-family:'Cormorant Garamond',serif;font-weight:700}
.stats{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:14px;margin-bottom:18px}
.stat{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:14px 16px}
.stat .lbl{font-size:12px;color:var(--muted2);text-transform:uppercase;letter-spacing:.8px}
.stat .val{font-size:26px;font-weight:700;margin-top:2px}
.stat .sub{font-size:12px;color:var(--muted);margin-top:2px}
.atable{width:100%;border-collapse:collapse;font-size:14px}
.atable th{text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:1px;color:var(--muted2);border-bottom:2px solid var(--calborder);padding:8px 8px}
.atable td{padding:9px 8px;border-bottom:1px solid var(--line);vertical-align:top}
.atable tr:last-child td{border-bottom:0}
.chip{display:inline-block;border-radius:999px;padding:2px 10px;font-size:12px;font-weight:600;white-space:nowrap}
.chip.ok{background:var(--okbg);color:var(--okfg)}
.chip.warn{background:var(--warnbg);color:var(--warnfg)}
.chip.bad{background:var(--badbg);color:var(--badfg)}
.chip.info{background:var(--infobg);color:var(--infofg)}
.chip.mut{background:var(--mutbg);color:var(--mutfg)}
.btn{background:var(--btn);color:#fff;border:0;border-radius:6px;padding:10px 16px;font-size:14px;font-weight:600;cursor:pointer;display:inline-block}
.btn:hover{background:var(--btnhover)}
.btn.red{background:#c0392b}.btn.red:hover{background:#9c2e23}
.btn.gray{background:#64748b}.btn.gray:hover{background:#4b5a6e}
.btn.small{padding:5px 10px;font-size:12.5px}
label.f{display:block;font-weight:600;margin:12px 0 4px;font-size:13.5px}
input.t,select.t,textarea.t{width:100%;padding:9px 10px;border:1px solid var(--inputborder);border-radius:6px;font-size:14px;background:var(--inputbg);color:var(--text)}
textarea.t{resize:vertical}
.filt{display:flex;gap:12px;flex-wrap:wrap;align-items:end}
.filt label{font-size:12px;font-weight:600;color:var(--muted2);display:flex;flex-direction:column;gap:4px}
.filt .t{width:auto;min-width:130px}
.alert{border-radius:6px;padding:12px 14px;margin:10px 0;font-size:14px}
.alert.ok{background:var(--okbg);border:1px solid var(--okfg);color:var(--okfg)}
.alert.bad{background:var(--badbg);border:1px solid var(--badfg);color:var(--badfg)}
.alert.warn{background:var(--warnbg);border:1px solid var(--warnfg);color:var(--warnfg)}
.acct{display:flex;justify-content:space-between;gap:12px;padding:7px 0;border-bottom:1px solid var(--line);font-size:14px}
.acct:last-child{border-bottom:0}
.acct span{color:var(--muted2)}
.cols{display:grid;grid-template-columns:2fr 1fr;gap:18px;align-items:start}
.cols3{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:14px}
.muted{color:var(--muted);font-size:13px}
.nowrap{white-space:nowrap}
.rowflex{display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.money{font-weight:700;white-space:nowrap}
.hkcard{border:1px solid var(--border);border-radius:10px;padding:14px}
.hkcard img{width:100%;height:90px;object-fit:cover;border-radius:6px}
.calwrap{overflow-x:auto}
.cal{border-collapse:collapse;font-size:11px}
.cal th{border:1px solid var(--calborder);padding:4px;background:var(--thbg);font-weight:600;color:var(--muted2)}
.cal td{border:1px solid var(--calborder);width:26px;height:26px;text-align:center;padding:0;color:var(--text)}
.c-free{background:var(--cfree)}
.c-partial{background:var(--cpartial)}
.c-full{background:var(--cfull)}
.c-blackout{background:var(--cblack);background-image:repeating-linear-gradient(45deg,transparent 0 4px,var(--stripe) 4px 8px)}
.c-past{background:var(--cpast);color:var(--muted)}
.legend{display:flex;gap:14px;flex-wrap:wrap;font-size:13px;margin:8px 0}
.legend span{display:inline-flex;align-items:center;gap:6px;color:var(--text)}
.legend i{width:16px;height:16px;border-radius:4px;display:inline-block;border:1px solid rgba(0,0,0,0.35);flex-shrink:0}
/* solid legend swatches: same vivid colors in light and dark mode */
.lg-free{background:#2f9e44}
.lg-partial{background:#f59f00}
.lg-full{background:#e03131}
.lg-blackout{background:#5c677d;background-image:repeating-linear-gradient(45deg,transparent 0 4px,#ffffffaa 4px 8px)}
.lg-past{background:#868e96}
.bare{min-height:100vh;display:flex;align-items:center;justify-content:center;background:#0b2e59;padding:20px}
.logincard{background:#fff;border-radius:12px;padding:34px 30px;width:380px;max-width:100%;position:relative}
.llogo{font-family:'Cormorant Garamond',serif;font-weight:600;font-size:46px;letter-spacing:10px;text-transform:uppercase;color:#16305e;line-height:1.05;text-align:center}
.lstar{position:absolute;top:20px;right:24px;color:#c9a227;font-size:14px}
.lsub{display:flex;align-items:center;gap:10px;font-size:11px;letter-spacing:4px;text-transform:uppercase;color:#9a7b1e;margin:4px 0 0}
.lsub i{flex:1;height:1px;background:#c9a227}
.ladmin{font-size:10px;letter-spacing:2.4px;text-transform:uppercase;color:#5a6b85;text-align:center;margin-bottom:14px}
/* paper (receipts, invoices, e-mail preview) stays white in dark mode */
.paper{max-width:820px;margin:auto;background:#fff;color:#1a2233;border:1px solid #dfe5ee;padding:36px 40px;border-radius:8px}
.paper .muted{color:#6b6b6b}
.paper .atable th{color:#5a6b85;border-bottom-color:#e8edf4}
.paper .atable td{color:#1a2233;border-bottom:1px solid #eef1f6}
.phead{display:flex;justify-content:space-between;gap:20px;border-bottom:3px solid #003b95;padding-bottom:14px;margin-bottom:14px}
.phead .pl{font-family:'Cormorant Garamond',serif;font-weight:600;font-size:34px;letter-spacing:4px;text-transform:uppercase;color:#16305e;line-height:1.05}
.stamp{display:inline-block;border:3px solid #0b7a2a;color:#0b7a2a;border-radius:6px;padding:6px 16px;font-weight:800;letter-spacing:2px;transform:rotate(-5deg);font-size:18px}
.stamp.due{border-color:#946200;color:#946200}
.stamp.refund{border-color:#b02a2a;color:#b02a2a}
.stamp.void{border-color:#8a97ab;color:#8a97ab}
.ptotal{display:flex;justify-content:flex-end;gap:30px;font-size:18px;font-weight:700;border-top:2px solid #1a2233;padding-top:10px;margin-top:10px}
@media(max-width:920px){.shell{flex-direction:column}.sidebar{width:100%;padding:8px 0}.sbrand{border:0;padding:6px 18px}.content{padding:14px}.cols{grid-template-columns:1fr}}
@media print{.sidebar,.topbar,.no-print,.alert{display:none!important}.shell{display:block}.content{padding:0;max-width:none}.acard{border:0;box-shadow:none;margin:0;padding:0}.paper{border:0;max-width:none}body{background:#fff}}
""";
}
