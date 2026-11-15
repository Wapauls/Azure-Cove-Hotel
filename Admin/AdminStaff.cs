// Admin/AdminStaff.cs
// Staff management (add / edit staff accounts, roles, password resets)
// plus the security-focused "Accounts Database" view (admin only).
using System.Text;

public static class AdminStaff
{
    public static readonly string[] Roles = { "Admin", "Manager", "Front Desk", "Housekeeping" };

    public static void Map(WebApplication app)
    {
        MapList(app);
        MapNew(app);
        MapEdit(app);
        MapReset(app);
        MapToggle(app);
        MapAccounts(app);
    }

    // a manager may not administer Admin accounts
    private static bool MayTouch(AdminUser actor, AdminUser target) => actor.Role != "Manager" || target.Role != "Admin";

    private static string RoleOptions(string current, bool includeAdmin)
    {
        var sb = new StringBuilder();
        foreach (string r in Roles)
        {
            if (r == "Admin" && !includeAdmin) continue;
            sb.Append("<option" + (r == current ? " selected" : "") + ">" + r + "</option>");
        }
        return sb.ToString();
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/staff", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex' style='margin-bottom:14px'><a class='btn' href='/admin/staff/new'>+ Add staff</a>");
            if (user.Role == "Admin") sb.Append("<a class='btn gray' href='/admin/accounts'>Accounts Database view</a>");
            sb.Append("</div>");

            sb.Append("<div class='acard'><table class='atable'><tr><th>Username</th><th>Role</th><th>Status</th><th>Created</th><th>Last login</th><th></th></tr>");
            foreach (AdminUser a in AdminData.Admins.OrderBy(x => x.Role).ThenBy(x => x.Username))
            {
                sb.Append("<tr><td><b>" + AdminData.H(a.Username) + "</b>" + (a.Id == user.Id ? " <span class='chip mut'>you</span>" : "") + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(a.Role == "Admin" ? "Paid" : a.Role == "Manager" ? "Completed" : "Pending") + " " + AdminData.H(a.Role) + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(a.Active ? "Available" : "Sold out").Replace("Available", "Active").Replace("Sold out", "Inactive") + "</td>");
                sb.Append("<td class='nowrap muted'>" + AdminData.Ds(a.CreatedAt) + " by " + AdminData.H(a.CreatedBy) + "</td>");
                sb.Append("<td class='nowrap muted'>" + (a.LastLoginAt != null ? ((DateTime)a.LastLoginAt).ToString("MMM d, HH:mm") + " - " + AdminData.H(a.LastLoginIp) : "never") + "</td>");
                sb.Append("<td class='rowflex'>");
                if (MayTouch(user, a) && a.Id != user.Id)
                {
                    sb.Append("<a class='btn small gray' href='/admin/staff/edit/" + a.Id + "'>Edit</a>");
                    sb.Append("<a class='btn small gray' href='/admin/staff/reset/" + a.Id + "'>Reset password</a>");
                    sb.Append("<form method='post' action='/admin/staff/toggle/" + a.Id + "'><button class='btn small " + (a.Active ? "red" : "") + "'>" + (a.Active ? "Deactivate" : "Reactivate") + "</button></form>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");

            sb.Append("<div class='acard'><h3>Role permissions</h3><table class='atable'><tr><th>Role</th><th>Access</th></tr>");
            sb.Append("<tr><td><b>Admin</b></td><td>Everything, including Accounts Database, Audit Logs and System Settings.</td></tr>");
            sb.Append("<tr><td><b>Manager</b></td><td>Everything except Accounts Database. Manages staff (except Admin accounts), rates, offers and reports.</td></tr>");
            sb.Append("<tr><td><b>Front Desk</b></td><td>Reservations, walk-in bookings, payments, billing, rooms, calendar, guests and housekeeping.</td></tr>");
            sb.Append("<tr><td><b>Housekeeping</b></td><td>Dashboard, housekeeping board and tasks only.</td></tr>");
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "staff", "Staff Management", sb.ToString());
        });
    }

    private static void MapNew(WebApplication app)
    {
        app.MapGet("/admin/staff/new", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Add staff</h2>");
            sb.Append("<form method='post' action='/admin/staff/new'>");
            sb.Append("<label class='f'>Username</label><input class='t' name='username' required>");
            sb.Append("<label class='f'>Password (8+ characters, shared with the staff member)</label><input class='t' type='password' name='password' required>");
            sb.Append("<label class='f'>Role</label><select class='t' name='role'>" + RoleOptions("Front Desk", user.Role == "Admin") + "</select>");
            sb.Append("<p class='muted'>The staff member will be asked to change this password at first sign-in.</p>");
            sb.Append("<br><button class='btn'>Create account</button> <a class='btn gray' href='/admin/staff'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "staff", "Add staff", sb.ToString());
        });

        app.MapPost("/admin/staff/new", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string username = form["username"].ToString().Trim();
            string password = form["password"].ToString();
            string role = form["role"].ToString();

            if (username == "" || password.Length < 8 || !Roles.Contains(role) || (role == "Admin" && user.Role != "Admin"))
                return AdminConsole.Page(user, "staff", "Add staff", "<div class='alert bad'>Check the fields: username required, password 8+ characters, valid role.</div><a class='btn' href='/admin/staff/new'>Back</a>");
            if (AdminData.Admins.Find(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) != null)
                return AdminConsole.Page(user, "staff", "Add staff", "<div class='alert bad'>That username already exists.</div><a class='btn' href='/admin/staff/new'>Back</a>");

            AdminData.Admins.Add(new AdminUser
            {
                Id = AdminData.NextId(AdminData.Admins, x => x.Id),
                Username = username,
                PasswordHash = AdminAuth.HashPassword(password),
                Role = role,
                Active = true,
                CreatedAt = DateTime.Now,
                CreatedBy = user.Username,
                MustChangePassword = true
            });
            AdminData.Save();
            AdminData.Audit(ctx, "Staff added", username + " (" + role + ") created by " + user.Username);
            return Results.Redirect("/admin/staff");
        });
    }

    private static void MapEdit(WebApplication app)
    {
        app.MapGet("/admin/staff/edit/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            AdminUser? a = AdminData.Admins.Find(x => x.Id == id);
            if (a == null || !MayTouch(user, a)) return AdminConsole.Page(user, "staff", "Edit staff", "<div class='alert bad'>Account not found or not editable by your role.</div><a class='btn' href='/admin/staff'>Back</a>");

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Edit " + AdminData.H(a.Username) + "</h2>");
            sb.Append("<form method='post' action='/admin/staff/edit/" + a.Id + "'>");
            sb.Append("<label class='f'>Role</label><select class='t' name='role'>" + RoleOptions(a.Role, user.Role == "Admin") + "</select>");
            sb.Append("<br><button class='btn'>Save</button> <a class='btn gray' href='/admin/staff'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "staff", "Edit staff", sb.ToString());
        });

        app.MapPost("/admin/staff/edit/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            AdminUser? a = AdminData.Admins.Find(x => x.Id == id);
            if (a != null && MayTouch(user, a))
            {
                var form = await ctx.Request.ReadFormAsync();
                string role = form["role"].ToString();
                if (Roles.Contains(role) && !(role == "Admin" && user.Role != "Admin"))
                {
                    string old = a.Role;
                    a.Role = role;
                    // a demoted manager/admin keeps access consistent with the new role
                    AdminData.Save();
                    AdminData.Audit(ctx, "Staff edited", a.Username + " role changed from " + old + " to " + role);
                }
            }
            return Results.Redirect("/admin/staff");
        });
    }

    private static void MapReset(WebApplication app)
    {
        app.MapGet("/admin/staff/reset/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            AdminUser? a = AdminData.Admins.Find(x => x.Id == id);
            if (a == null || !MayTouch(user, a)) return AdminConsole.Page(user, "staff", "Reset password", "<div class='alert bad'>Account not found or not editable by your role.</div><a class='btn' href='/admin/staff'>Back</a>");

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Reset password for " + AdminData.H(a.Username) + "</h2>");
            sb.Append("<form method='post' action='/admin/staff/reset/" + a.Id + "'>");
            sb.Append("<label class='f'>New temporary password (8+ characters)</label><input class='t' type='password' name='password' required>");
            sb.Append("<p class='muted'>The account will be flagged to change this password at next sign-in.</p>");
            sb.Append("<br><button class='btn'>Reset password</button> <a class='btn gray' href='/admin/staff'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "staff", "Reset password", sb.ToString());
        });

        app.MapPost("/admin/staff/reset/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            AdminUser? a = AdminData.Admins.Find(x => x.Id == id);
            if (a != null && MayTouch(user, a))
            {
                var form = await ctx.Request.ReadFormAsync();
                string password = form["password"].ToString();
                if (password.Length >= 8)
                {
                    a.PasswordHash = AdminAuth.HashPassword(password);
                    a.MustChangePassword = true;
                    a.FailedLogins = 0;
                    a.LockoutUntil = null;
                    AdminData.Save();
                    AdminData.Audit(ctx, "Password reset", "password of '" + a.Username + "' reset by " + user.Username);
                }
            }
            return Results.Redirect("/admin/staff");
        });
    }

    private static void MapToggle(WebApplication app)
    {
        app.MapPost("/admin/staff/toggle/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "staff");
            if (deny != null) return deny;
            AdminUser user = u!;

            AdminUser? a = AdminData.Admins.Find(x => x.Id == id);
            if (a != null && a.Id != user.Id && MayTouch(user, a))
            {
                // never deactivate the last active admin
                if (a.Role == "Admin" && a.Active && AdminData.Admins.FindAll(x => x.Active && x.Role == "Admin").Count <= 1)
                    return Results.Redirect("/admin/staff");
                a.Active = !a.Active;
                if (!a.Active) a.FailedLogins = 0;
                AdminData.Save();
                AdminData.Audit(ctx, "Staff " + (a.Active ? "reactivated" : "deactivated"), a.Username + " (" + a.Role + ") by " + user.Username);
            }
            return Results.Redirect("/admin/staff");
        });
    }

    private static void MapAccounts(WebApplication app)
    {
        app.MapGet("/admin/accounts", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "accounts");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard'><h2>Accounts Database</h2><p class='muted'>Security view of every console account. Passwords are stored only as salted PBKDF2 hashes - never in plain text. All account activity is written to the Audit Logs.</p>");
            sb.Append("<table class='atable'><tr><th>Id</th><th>Username</th><th>Role</th><th>Status</th><th>Created</th><th>Last login</th><th>Failed logins</th><th>Password</th></tr>");
            foreach (AdminUser a in AdminData.Admins.OrderBy(x => x.Id))
            {
                bool locked = a.LockoutUntil != null && a.LockoutUntil > DateTime.Now;
                sb.Append("<tr><td>" + a.Id + "</td><td><b>" + AdminData.H(a.Username) + "</b></td><td>" + AdminData.H(a.Role) + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(a.Active ? "Available" : "Sold out").Replace("Available", "Active").Replace("Sold out", "Inactive") + (locked ? " " + AdminData.StatusChip("Sold out").Replace("Sold out", "Locked") : "") + "</td>");
                sb.Append("<td class='nowrap muted'>" + AdminData.Ds(a.CreatedAt) + " by " + AdminData.H(a.CreatedBy) + "</td>");
                sb.Append("<td class='nowrap muted'>" + (a.LastLoginAt != null ? ((DateTime)a.LastLoginAt).ToString("MMM d, HH:mm") + " - " + AdminData.H(a.LastLoginIp) : "never") + "</td>");
                sb.Append("<td>" + a.FailedLogins + (locked ? " <span class='muted'>(locked until " + ((DateTime)a.LockoutUntil!).ToString("HH:mm") + ")</span>" : "") + "</td>");
                sb.Append("<td>" + (a.MustChangePassword ? AdminData.StatusChip("Pending").Replace("Pending", "Must change") : "OK") + "</td></tr>");
            }
            sb.Append("</table></div>");

            sb.Append("<div class='acard'><h3>Security notes</h3>");
            sb.Append("<p class='muted'>- Sessions expire after 8 hours of inactivity.<br>- Accounts lock for 15 minutes after 5 failed sign-in attempts.<br>- Deactivating an account ends nothing in progress but blocks future sign-ins.<br>- Manage accounts in <a href='/admin/staff'>Staff Management</a>.</p></div>");

            return AdminConsole.Page(user, "accounts", "Accounts Database", sb.ToString());
        });
    }
}
