// Admin/AdminHousekeeping.cs
// Housekeeping: room status board (clean / dirty / maintenance / out of order)
// and task assignment to housekeeping staff.
using System.Text;

public static class AdminHousekeeping
{
    public static readonly string[] RoomStates = { "Clean", "Dirty", "Maintenance", "Out of Order" };
    public static readonly string[] TaskStates = { "Pending", "In progress", "Done" };

    public static void Map(WebApplication app)
    {
        MapBoard(app);
        MapTasks(app);
    }

    private static string Housekeepers()
    {
        var sb = new StringBuilder();
        foreach (AdminUser a in AdminData.Admins)
            if (a.Active && a.Role == "Housekeeping")
                sb.Append("<option>" + AdminData.H(a.Username) + "</option>");
        if (sb.Length == 0) sb.Append("<option value=''>-- no housekeeping staff yet --</option>");
        return sb.ToString();
    }

    private static void MapBoard(WebApplication app)
    {
        app.MapGet("/admin/housekeeping", (HttpContext ctx, string? filter) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "housekeeping");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();

            // room status board
            sb.Append("<div class='acard'><h3>Room status board</h3><div class='cols3'>");
            foreach (Room r in AdminData.Rooms)
            {
                int booked = AdminData.BookedUnits(r, DateTime.Today);
                sb.Append("<div class='hkcard'>");
                sb.Append("<div class='rowflex' style='justify-content:space-between'><b>" + AdminData.H(r.Name) + "</b>" + AdminData.StatusChip(r.RoomStatus) + "</div>");
                sb.Append("<div class='muted' style='margin:6px 0'>Booked today: " + booked + "/" + r.Units + (booked > 0 ? " - occupied" : " - vacant") + "</div>");
                sb.Append("<div class='rowflex'>");
                foreach (string s in RoomStates)
                {
                    if (s == r.RoomStatus) continue;
                    sb.Append("<form method='post' action='/admin/housekeeping/status/" + r.Id + "'><input type='hidden' name='value' value='" + s + "'><button class='btn small " + (s == "Clean" ? "" : s == "Dirty" ? "red" : "gray") + "'>" + s + "</button></form>");
                }
                sb.Append("</div></div>");
            }
            if (AdminData.Rooms.Count == 0) sb.Append("<p class='muted'>No rooms yet.</p>");
            sb.Append("</div></div>");

            // tasks
            sb.Append("<div class='acard'><h3>Tasks</h3>");
            sb.Append("<table class='atable'><tr><th>#</th><th>Room</th><th>Task</th><th>Assignee</th><th>Due</th><th>Status</th><th></th></tr>");
            var tasks = AdminData.Tasks.OrderBy(t => t.Status == "Done" ? 1 : 0).ThenBy(t => t.Due).ToList();
            if (filter == "mine")
            {
                tasks = tasks.FindAll(t => t.Assignee == user.Username && t.Status != "Done");
            }
            if (tasks.Count == 0) sb.Append("<tr><td colspan='7' class='muted'>No tasks" + (filter == "mine" ? " assigned to you" : " yet") + ".</td></tr>");
            foreach (HousekeepingTask t in tasks)
            {
                Room? r = AdminData.Rooms.Find(x => x.Id == t.RoomId);
                sb.Append("<tr><td>" + t.Id + "</td><td>" + AdminData.H(r?.Name ?? "-") + "</td><td>" + AdminData.H(t.Description) + "</td><td>" + AdminData.H(t.Assignee == "" ? "-" : t.Assignee) + "</td><td class='nowrap'>" + AdminData.Ds(t.Due) + "</td><td>" + AdminData.StatusChip(t.Status) + "</td>");
                sb.Append("<td class='rowflex'>");
                if (t.Status == "Pending") sb.Append("<form method='post' action='/admin/housekeeping/task/" + t.Id + "'><input type='hidden' name='status' value='In progress'><button class='btn small'>Start</button></form>");
                if (t.Status != "Done") sb.Append("<form method='post' action='/admin/housekeeping/task/" + t.Id + "'><input type='hidden' name='status' value='Done'><button class='btn small " + "'>Mark done</button></form>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table>");
            sb.Append("<div class='rowflex' style='margin-top:10px'><a class='btn small gray' href='/admin/housekeeping'>All tasks</a><a class='btn small gray' href='/admin/housekeeping?filter=mine'>My tasks</a></div>");
            sb.Append("</div>");

            // add task (housekeeping staff themselves only update status)
            if (AdminAuth.Can(user.Role, "reservations"))
            {
                sb.Append("<div class='acard slim'><h3>Assign a new task</h3>");
                sb.Append("<form method='post' action='/admin/housekeeping/task/add'>");
                sb.Append("<label class='f'>Room</label><select class='t' name='room'>");
                foreach (Room r in AdminData.Rooms) sb.Append("<option value='" + r.Id + "'>" + AdminData.H(r.Name) + "</option>");
                sb.Append("</select>");
                sb.Append("<label class='f'>Task</label><input class='t' name='desc' placeholder='e.g. Deep clean after checkout' required>");
                sb.Append("<label class='f'>Assign to</label><select class='t' name='assignee'>" + Housekeepers() + "</select>");
                sb.Append("<label class='f'>Due date</label><input class='t' type='date' name='due' value='" + AdminData.Ds(DateTime.Today) + "'>");
                sb.Append("<br><button class='btn'>Assign task</button></form></div>");
                if (AdminData.Admins.FindAll(a => a.Active && a.Role == "Housekeeping").Count == 0)
                    sb.Append("<p class='muted'>Tip: create staff accounts with the Housekeeping role in <a href='/admin/staff'>Staff Management</a> to assign tasks.</p>");
            }

            return AdminConsole.Page(user, "housekeeping", "Housekeeping", sb.ToString());
        });

        app.MapPost("/admin/housekeeping/status/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "housekeeping");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string value = form["value"].ToString();
            Room? r = AdminData.Rooms.Find(x => x.Id == id);
            if (r != null && RoomStates.Contains(value))
            {
                string old = r.RoomStatus;
                r.RoomStatus = value;
                AdminData.Save();
                AdminData.Audit(ctx, "Room status", r.Name + " set from " + old + " to " + value + " by " + user.Username);
            }
            return Results.Redirect("/admin/housekeeping");
        });
    }

    private static void MapTasks(WebApplication app)
    {
        app.MapPost("/admin/housekeeping/task/add", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "housekeeping");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["room"].ToString(), out int roomId);
            string desc = form["desc"].ToString().Trim();
            string assignee = form["assignee"].ToString().Trim();
            DateTime.TryParse(form["due"].ToString(), out DateTime due);
            if (due == DateTime.MinValue) due = DateTime.Today;
            if (desc != "" && AdminData.Rooms.Find(r => r.Id == roomId) != null)
            {
                HousekeepingTask t = new()
                {
                    Id = AdminData.NextId(AdminData.Tasks, x => x.Id),
                    RoomId = roomId,
                    Description = desc,
                    Assignee = assignee,
                    Due = due,
                    Status = "Pending"
                };
                AdminData.Tasks.Add(t);
                AdminData.Save();
                AdminData.Audit(ctx, "Task assigned", "'" + desc + "' for room id " + roomId + " assigned to " + (assignee == "" ? "nobody" : assignee));
            }
            return Results.Redirect("/admin/housekeeping");
        });

        app.MapPost("/admin/housekeeping/task/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "housekeeping");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string status = form["status"].ToString();
            HousekeepingTask? t = AdminData.Tasks.Find(x => x.Id == id);
            if (t != null && TaskStates.Contains(status))
            {
                string old = t.Status;
                t.Status = status;
                AdminData.Save();
                AdminData.Audit(ctx, "Task updated", "task '" + t.Description + "' moved from " + old + " to " + status + " by " + user.Username);
            }
            return Results.Redirect("/admin/housekeeping");
        });
    }
}
