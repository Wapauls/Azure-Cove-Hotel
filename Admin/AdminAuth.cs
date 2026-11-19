// Admin/AdminAuth.cs
// Admin authentication: PBKDF2 password hashing, cookie sessions,
// role-based permissions and the login / logout / change-password pages.
using System.Security.Cryptography;

public class AdminSession
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime Expires { get; set; }
}

public static class AdminAuth
{
    public const string CookieName = "ac_admin";
    private static readonly Dictionary<string, AdminSession> Sessions = new();

    // Which roles may see which sidebar section.
    public static readonly Dictionary<string, string[]> CanSee = new()
    {
        ["account"] = new[] { "Admin", "Manager", "Front Desk", "Housekeeping" },
        ["dashboard"] = new[] { "Admin", "Manager", "Front Desk", "Housekeeping" },
        ["reservations"] = new[] { "Admin", "Manager", "Front Desk" },
        ["payments"] = new[] { "Admin", "Manager", "Front Desk" },
        ["billing"] = new[] { "Admin", "Manager", "Front Desk" },
        ["rooms"] = new[] { "Admin", "Manager", "Front Desk" },
        ["calendar"] = new[] { "Admin", "Manager", "Front Desk" },
        ["housekeeping"] = new[] { "Admin", "Manager", "Front Desk", "Housekeeping" },
        ["guests"] = new[] { "Admin", "Manager", "Front Desk" },
        ["staff"] = new[] { "Admin", "Manager" },
        ["accounts"] = new[] { "Admin" },
        ["offers"] = new[] { "Admin", "Manager" },
        ["rates"] = new[] { "Admin", "Manager" },
        ["reports"] = new[] { "Admin", "Manager" },
        ["emails"] = new[] { "Admin", "Manager" },
        ["settings"] = new[] { "Admin" },
        ["audit"] = new[] { "Admin" },
    };

    public static bool Can(string role, string section) => CanSee.TryGetValue(section, out var roles) && roles.Contains(role);

    // Drops every session of a user except keepToken - used after a password
    // change so other browsers/devices have to sign in again.
    public static void RevokeOtherSessions(string username, string keepToken)
    {
        List<string> dead = Sessions.Where(kv => kv.Value.Username == username && kv.Key != keepToken).Select(kv => kv.Key).ToList();
        foreach (string t in dead) Sessions.Remove(t);
    }

    // A reviewed-but-not-yet-confirmed account change (username, or the HASH
    // of a new password). Keyed by session token and kept for 10 minutes, so
    // no secret travels through the confirmation page.
    private sealed class Pending
    {
        public string Kind = "";
        public string Value = "";
        public DateTime Expires;
    }

    private static readonly Dictionary<string, Pending> PendingMap = new();

    private static void Pend(HttpContext ctx, string kind, string value)
    {
        PendingMap[ctx.Request.Cookies[CookieName] ?? ""] = new Pending { Kind = kind, Value = value, Expires = DateTime.Now.AddMinutes(10) };
    }

    private static Pending? TakePending(HttpContext ctx)
    {
        string token = ctx.Request.Cookies[CookieName] ?? "";
        List<string> expired = PendingMap.Where(kv => kv.Value.Expires < DateTime.Now).Select(kv => kv.Key).ToList();
        foreach (string k in expired) PendingMap.Remove(k);
        if (token == "" || !PendingMap.TryGetValue(token, out Pending? p)) return null;
        PendingMap.Remove(token);
        return p.Expires < DateTime.Now ? null : p;
    }


    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        return "pbkdf2-100000-" + Convert.ToBase64String(salt) + "-" + Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string stored)
    {
        string[] parts = (stored ?? "").Split('-');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        if (!int.TryParse(parts[1], out int iters)) return false;
        byte[] salt, hash;
        try { salt = Convert.FromBase64String(parts[2]); hash = Convert.FromBase64String(parts[3]); }
        catch { return false; }
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iters, HashAlgorithmName.SHA256);
        byte[] test = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(test, hash);
    }


    public static AdminUser? UserOf(HttpContext? ctx)
    {
        if (ctx == null) return null;
        string? token = ctx.Request.Cookies[CookieName];
        if (token == null || !Sessions.TryGetValue(token, out AdminSession? s) || s == null) return null;
        if (s.Expires < DateTime.Now) { Sessions.Remove(token); return null; }
        AdminUser? u = AdminData.Admins.Find(a => a.Active && a.Username == s.Username);
        if (u == null) { Sessions.Remove(token); return null; }
        s.Role = u.Role;
        s.Expires = DateTime.Now.AddHours(8); // sliding expiration
        return u;
    }

    // Returns the signed-in user, or an IResult to return immediately
    // (redirect to login, or an access-denied page).
    public static (AdminUser? user, IResult? deny) Check(HttpContext ctx, string section)
    {
        AdminUser? u = UserOf(ctx);
        if (u == null) return (null, Results.Redirect("/admin/login"));
        if (!Can(u.Role, section)) return (u, AdminConsole.DenyPage(u, section));
        return (u, null);
    }


    public static void Map(WebApplication app)
    {
        // Admin pages are never cached: after signing out, the back button
        // (and the disk cache) must show the sign-in page again instead of a
        // remembered console page with live data.
        app.Use(async (HttpContext ctx, Func<Task> next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/admin"))
            {
                ctx.Response.OnStarting(() =>
                {
                    ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                    ctx.Response.Headers["Pragma"] = "no-cache";
                    return Task.CompletedTask;
                });
            }
            await next();
        });

        app.MapGet("/admin/login", (HttpContext ctx) =>
        {
            if (UserOf(ctx) != null) return Results.Redirect("/admin/dashboard");
            return Results.Content(AdminConsole.BarePage("Sign in", LoginBody("")), "text/html; charset=utf-8");
        });

        app.MapPost("/admin/login", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            string username = form["username"].ToString().Trim();
            string password = form["password"].ToString();

            AdminUser? u = AdminData.Admins.Find(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            bool locked = u != null && u.LockoutUntil != null && u.LockoutUntil > DateTime.Now;
            if (u == null || !u.Active || locked)
            {
                AdminData.Audit(ctx, "Login failed", "unknown, inactive or locked account '" + username + "'");
                return Results.Content(AdminConsole.BarePage("Sign in", LoginBody("Invalid username or password, or the account is locked.")), "text/html; charset=utf-8");
            }

            if (!VerifyPassword(password, u.PasswordHash))
            {
                u.FailedLogins++;
                if (u.FailedLogins >= 5) u.LockoutUntil = DateTime.Now.AddMinutes(15);
                AdminData.Save();
                AdminData.Audit(ctx, "Login failed", "wrong password for '" + u.Username + "'");
                return Results.Content(AdminConsole.BarePage("Sign in", LoginBody("Invalid username or password.")), "text/html; charset=utf-8");
            }

            u.FailedLogins = 0;
            u.LockoutUntil = null;
            u.LastLoginAt = DateTime.Now;
            u.LastLoginIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";

            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            Sessions[token] = new AdminSession { Token = token, Username = u.Username, Role = u.Role, Expires = DateTime.Now.AddHours(8) };
            ctx.Response.Cookies.Append(CookieName, token, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/", IsEssential = true });

            AdminData.Save();
            AdminData.Audit(ctx, "Login", "signed in");
            // no forced password redirect - staff manage their own account
            // under "My account" in the top bar
            return Results.Redirect("/admin/dashboard");
        });

        app.MapPost("/admin/logout", (HttpContext ctx) =>
        {
            AdminData.Audit(ctx, "Logout", "signed out");
            string? token = ctx.Request.Cookies[CookieName];
            if (token != null) Sessions.Remove(token);
            // delete with the same Path the cookie was set with, otherwise the
            // browser keeps the original cookie around
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            // drop everything cached for this site, so no console page survives
            // the back button once the user has signed out
            ctx.Response.Headers["Clear-Site-Data"] = "\"cache\"";
            return Results.Redirect("/admin/login");
        });


        app.MapGet("/admin/account", (HttpContext ctx) =>
        {
            var (u, deny) = Check(ctx, "account");
            if (u == null) return deny ?? Results.Redirect("/admin/login");
            string saved = ctx.Request.Query["saved"].ToString();
            return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, saved, ""));
        });

        // Username change: authorize with the current password, validate, park
        // the request and show the confirmation prompt - nothing is saved until
        // the staff member confirms on the next page.
        app.MapPost("/admin/account/username", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var (u, deny) = Check(ctx, "account");
            if (u == null) return deny ?? Results.Redirect("/admin/login");

            if (!VerifyPassword(form["current"].ToString(), u.PasswordHash))
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "Your current password is wrong."));

            string newName = form["username"].ToString().Trim();
            if (newName.Length < 3 || newName.Length > 30 || newName.Contains(' '))
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The new username must be 3-30 characters with no spaces."));
            if (newName == u.Username)
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "That is already your username."));
            if (AdminData.Admins.Find(a => a.Username.Equals(newName, StringComparison.OrdinalIgnoreCase)) != null)
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "That username is already taken."));

            Pend(ctx, "username", newName);
            return AdminConsole.Page(u, "account", "Review username change", ConfirmUsernameBody(u.Username, newName));
        });

        app.MapPost("/admin/account/username/confirm", (HttpContext ctx) =>
        {
            var (u, deny) = Check(ctx, "account");
            if (u == null) return deny ?? Results.Redirect("/admin/login");

            Pending? p = TakePending(ctx);
            if (p == null || p.Kind != "username")
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The change request expired. Please start again."));

            string oldName = u.Username;
            string newName = p.Value;
            if (AdminData.Admins.Find(a => a.Id != u.Id && a.Username.Equals(newName, StringComparison.OrdinalIgnoreCase)) != null)
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "That username was taken in the meantime. Please choose another."));

            u.Username = newName;
            // keep every signed-in session of the account working under the new name
            foreach (var kv in Sessions.Where(kv => kv.Value.Username == oldName).ToList())
                kv.Value.Username = newName;

            AdminData.Save();
            AdminData.Audit(ctx, "Username changed", "'" + oldName + "' is now '" + newName + "'");
            return Results.Redirect("/admin/account?saved=username");
        });

        // Password change: same review-then-confirm pattern. Only the HASH of
        // the new password is parked, so no secret ever travels through the
        // confirmation page.
        app.MapPost("/admin/account/password", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var (u, deny) = Check(ctx, "account");
            if (u == null) return deny ?? Results.Redirect("/admin/login");

            string current = form["current"].ToString();
            string new1 = form["new1"].ToString();
            string new2 = form["new2"].ToString();

            if (!VerifyPassword(current, u.PasswordHash))
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "Your current password is wrong."));
            if (new1.Length < 8)
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The new password must be at least 8 characters."));
            if (new1 != new2)
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The two new passwords do not match."));
            if (VerifyPassword(new1, u.PasswordHash))
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The new password is the same as the current one."));

            Pend(ctx, "password", HashPassword(new1));
            return AdminConsole.Page(u, "account", "Review password change", ConfirmPasswordBody(u.Username, new1.Length));
        });

        app.MapPost("/admin/account/password/confirm", (HttpContext ctx) =>
        {
            var (u, deny) = Check(ctx, "account");
            if (u == null) return deny ?? Results.Redirect("/admin/login");

            Pending? p = TakePending(ctx);
            if (p == null || p.Kind != "password")
                return AdminConsole.Page(u, "account", "Account settings", AccountBody(u, "", "The change request expired. Please start again."));

            u.PasswordHash = p.Value;
            u.MustChangePassword = false;
            // sign out any other device/browser that is logged in as this user
            RevokeOtherSessions(u.Username, ctx.Request.Cookies[CookieName] ?? "");

            AdminData.Save();
            AdminData.Audit(ctx, "Password changed", "password changed for '" + u.Username + "' (own account)");
            return Results.Redirect("/admin/account?saved=password");
        });
    }

    private static string LoginBody(string error)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='loginwrap'><div class='logincard'><span class='lstar'>&#10022;</span>");
        sb.Append("<div class='llogo'>Azure</div><div class='lsub'><i></i>Cove Hotel<i></i></div><div class='ladmin'>Admin console</div>");
        if (error != "") sb.Append("<div class='alert bad'>" + AdminData.H(error) + "</div>");
        sb.Append("<form method='post' action='/admin/login'>");
        sb.Append("<label class='f'>Username</label><input class='t' name='username' required autofocus>");
        sb.Append("<label class='f'>Password</label><input class='t' type='password' name='password' required><br>");
        sb.Append("<button class='btn' style='width:100%'>Sign in</button></form>");
        sb.Append("</div></div>");
        return sb.ToString();
    }


    private static string AccountBody(AdminUser u, string saved, string error)
    {
        var sb = new System.Text.StringBuilder();
        if (error != "") sb.Append("<div class='alert bad'>" + AdminData.H(error) + "</div>");
        if (saved == "username") sb.Append("<div class='alert ok'>Username changed successfully.</div>");
        if (saved == "password") sb.Append("<div class='alert ok'>Password changed successfully. Other signed-in devices were logged out.</div>");
        if (u.MustChangePassword) sb.Append("<div class='alert warn'>You are still using the default password that was created for this account. Please change it below.</div>");

        sb.Append("<div class='acard slim'><h2>Your account</h2>");
        sb.Append("<div class='acct'><span>Username</span><b>" + AdminData.H(u.Username) + "</b></div>");
        sb.Append("<div class='acct'><span>Role</span><b>" + AdminData.H(u.Role) + "</b></div>");
        sb.Append("<div class='acct'><span>Last sign-in</span><b>" + (u.LastLoginAt == null ? "-" : AdminData.H(u.LastLoginAt.Value.ToString("MMM d, yyyy, h:mm tt"))) + "</b></div>");
        sb.Append("<div class='acct'><span>Sign-in IP</span><b>" + (u.LastLoginIp == "" ? "-" : AdminData.H(u.LastLoginIp)) + "</b></div>");
        sb.Append("</div>");

        sb.Append("<div class='acard slim'><h2>Change username</h2>");
        sb.Append("<form method='post' action='/admin/account/username'>");
        sb.Append("<label class='f'>Current password (authorizes this change)</label><input class='t' type='password' name='current' required>");
        sb.Append("<label class='f'>New username (3-30 characters, no spaces)</label><input class='t' name='username' required><br>");
        sb.Append("<button class='btn'>Review username change</button></form></div>");

        sb.Append("<div class='acard slim'><h2>Change password</h2>");
        sb.Append("<form method='post' action='/admin/account/password'>");
        sb.Append("<label class='f'>Current password</label><input class='t' type='password' name='current' required>");
        sb.Append("<label class='f'>New password (8+ characters)</label><input class='t' type='password' name='new1' required>");
        sb.Append("<label class='f'>Repeat new password</label><input class='t' type='password' name='new2' required><br>");
        sb.Append("<button class='btn'>Review password change</button></form></div>");
        return sb.ToString();
    }


    private static string ConfirmUsernameBody(string oldName, string newName)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='acard slim'><h2>Review username change</h2>");
        sb.Append("<p class='muted'>Please review the details below before saving. The change takes effect immediately.</p>");
        sb.Append("<div class='acct'><span>Current username</span><b>" + AdminData.H(oldName) + "</b></div>");
        sb.Append("<div class='acct'><span>New username</span><b>" + AdminData.H(newName) + "</b></div>");
        sb.Append("<p class='muted'>Your sign-in sessions keep working under the new username.</p><br>");
        sb.Append("<form method='post' action='/admin/account/username/confirm' style='display:inline'>");
        sb.Append("<button class='btn'>Confirm change</button></form> ");
        sb.Append("<a class='btn gray' href='/admin/account'>Cancel</a></div>");
        return sb.ToString();
    }

    private static string ConfirmPasswordBody(string username, int length)
    {
        // only the length travels to this page - the password itself stays
        // server-side (as a hash) until confirmed
        string masked = new string('\u2022', Math.Min(length, 20));
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='acard slim'><h2>Review password change</h2>");
        sb.Append("<p class='muted'>Please review the details below before saving. The change takes effect immediately.</p>");
        sb.Append("<div class='acct'><span>Account</span><b>" + AdminData.H(username) + "</b></div>");
        sb.Append("<div class='acct'><span>New password</span><b>" + masked + " (" + length + " characters)</b></div>");
        sb.Append("<p class='muted'>After saving, any other device signed in to this account is logged out and must sign in again.</p><br>");
        sb.Append("<form method='post' action='/admin/account/password/confirm' style='display:inline'>");
        sb.Append("<button class='btn'>Confirm change</button></form> ");
        sb.Append("<a class='btn gray' href='/admin/account'>Cancel</a></div>");
        return sb.ToString();
    }
}
