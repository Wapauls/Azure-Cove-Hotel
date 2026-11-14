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

    // ---------- passwords ----------

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

    // ---------- sessions ----------

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

    // ---------- routes ----------

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
            return Results.Redirect(u.MustChangePassword ? "/admin/password" : "/admin/dashboard");
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

        app.MapGet("/admin/password", (HttpContext ctx) =>
        {
            AdminUser? u = UserOf(ctx);
            if (u == null) return Results.Redirect("/admin/login");
            return AdminConsole.Page(u, "dashboard", "Change password", ChangePasswordBody(""));
        });

        app.MapPost("/admin/password", async (HttpContext ctx) =>
        {
            AdminUser? u = UserOf(ctx);
            if (u == null) return Results.Redirect("/admin/login");
            var form = await ctx.Request.ReadFormAsync();
            string current = form["current"].ToString();
            string new1 = form["new1"].ToString();
            string new2 = form["new2"].ToString();

            if (!VerifyPassword(current, u.PasswordHash))
                return AdminConsole.Page(u, "dashboard", "Change password", ChangePasswordBody("Your current password is wrong."));
            if (new1.Length < 8)
                return AdminConsole.Page(u, "dashboard", "Change password", ChangePasswordBody("The new password must be at least 8 characters."));
            if (new1 != new2)
                return AdminConsole.Page(u, "dashboard", "Change password", ChangePasswordBody("The two new passwords do not match."));

            u.PasswordHash = HashPassword(new1);
            u.MustChangePassword = false;
            // sign out any other device/browser that is logged in as this user
            RevokeOtherSessions(u.Username, ctx.Request.Cookies[CookieName] ?? "");
            AdminData.Save();
            AdminData.Audit(ctx, "Password changed", "password changed for '" + u.Username + "'");
            return Results.Redirect("/admin/dashboard");
        });
    }

    private static string LoginBody(string error)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='loginwrap'><div class='logincard'>");
        sb.Append("<div class='llogo'>Azure Cove Hotel</div><div class='lsub'>Admin console</div>");
        if (error != "") sb.Append("<div class='alert bad'>" + AdminData.H(error) + "</div>");
        sb.Append("<form method='post' action='/admin/login'>");
        sb.Append("<label class='f'>Username</label><input class='t' name='username' required autofocus>");
        sb.Append("<label class='f'>Password</label><input class='t' type='password' name='password' required><br>");
        sb.Append("<button class='btn' style='width:100%'>Sign in</button></form>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string ChangePasswordBody(string error)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='acard slim'><h2>Change your password</h2>");
        if (error != "") sb.Append("<div class='alert bad'>" + AdminData.H(error) + "</div>");
        sb.Append("<form method='post' action='/admin/password'>");
        sb.Append("<label class='f'>Current password</label><input class='t' type='password' name='current' required>");
        sb.Append("<label class='f'>New password (8+ characters)</label><input class='t' type='password' name='new1' required>");
        sb.Append("<label class='f'>Repeat new password</label><input class='t' type='password' name='new2' required><br>");
        sb.Append("<button class='btn'>Save new password</button></form></div>");
        return sb.ToString();
    }
}
