// Admin/AdminRooms.cs
// Rooms & inventory: view, edit, add rooms, availability toggle.
// Base prices are managed in Rate Management; this page shows them read-only.
using System.Text;

public static class AdminRooms
{
    public static void Map(WebApplication app)
    {
        MapList(app);
        MapEdit(app);
        MapAdd(app);
        MapToggle(app);
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/rooms", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex' style='margin-bottom:14px'><a class='btn' href='/admin/rooms/new'>+ Add room</a></div>");
            sb.Append("<div class='acard'><table class='atable'><tr><th>Photo</th><th>Room</th><th>Base price</th><th>Units</th><th>Sleeps</th><th>Booking status</th><th>Housekeeping</th><th></th></tr>");
            if (AdminData.Rooms.Count == 0) sb.Append("<tr><td colspan='8' class='muted'>No rooms yet.</td></tr>");
            foreach (Room r in AdminData.Rooms)
            {
                sb.Append("<tr><td><img src='/photo/" + AdminData.H(r.Image) + "' style='width:74px;height:46px;object-fit:cover;border-radius:6px' onerror=\"this.style.display='none'\"></td>");
                sb.Append("<td><b>" + AdminData.H(r.Name) + "</b><br><span class='muted'>" + AdminData.H(r.Details.Length > 80 ? r.Details[..80] + "..." : r.Details) + "</span></td>");
                sb.Append("<td class='money'>" + AdminData.Money(r.Price) + "<br><a class='muted' href='/admin/rates'>change in Rates</a></td>");
                sb.Append("<td>" + r.Units + "</td><td>" + r.MaxGuests + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(r.IsAvailable ? "Available" : "Sold out") + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(r.RoomStatus) + "</td>");
                sb.Append("<td class='rowflex'>");
                sb.Append("<a class='btn small gray' href='/admin/rooms/edit/" + r.Id + "'>Edit</a>");
                sb.Append("<form method='post' action='/admin/rooms/toggle/" + r.Id + "'><button class='btn small " + (r.IsAvailable ? "red" : "") + "'>" + (r.IsAvailable ? "Mark sold out" : "Reopen") + "</button></form>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");
            sb.Append("<p class='muted'>Units = how many rooms of this type the hotel owns. Availability for specific dates is checked per night against bookings and blackout dates (see the Availability Calendar).</p>");

            return AdminConsole.Page(user, "rooms", "Rooms & Inventory", sb.ToString());
        });
    }

    private static void MapEdit(WebApplication app)
    {
        app.MapGet("/admin/rooms/edit/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            Room? r = AdminData.Rooms.Find(x => x.Id == id);
            if (r == null) return AdminConsole.Page(user, "rooms", "Edit room", "<div class='alert bad'>Room not found.</div><a class='btn' href='/admin/rooms'>Back</a>");

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Edit " + AdminData.H(r.Name) + "</h2>");
            sb.Append("<form method='post' action='/admin/rooms/edit/" + r.Id + "'>");
            sb.Append("<label class='f'>Name</label><input class='t' name='name' value='" + AdminData.H(r.Name) + "' required>");
            sb.Append("<label class='f'>Details</label><textarea class='t' name='details' rows='5'>" + AdminData.H(r.Details) + "</textarea>");
            sb.Append("<label class='f'>Image file name</label><input class='t' name='image' value='" + AdminData.H(r.Image) + "'>");
            sb.Append("<label class='f'>Sleeps up to (max guests)</label><input class='t' type='number' name='maxguests' min='1' max='12' value='" + r.MaxGuests + "'>");
            sb.Append("<label class='f'>Units (inventory count)</label><input class='t' type='number' name='units' min='1' max='50' value='" + r.Units + "'>");
            sb.Append("<br><button class='btn'>Save changes</button> <a class='btn gray' href='/admin/rooms'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "rooms", "Edit room", sb.ToString());
        });

        app.MapPost("/admin/rooms/edit/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            Room? r = AdminData.Rooms.Find(x => x.Id == id);
            if (r != null)
            {
                var form = await ctx.Request.ReadFormAsync();
                string name = form["name"].ToString().Trim();
                int.TryParse(form["maxguests"].ToString(), out int maxg);
                int.TryParse(form["units"].ToString(), out int units);
                if (name != "" && maxg >= 1)
                {
                    r.Name = name;
                    r.Details = form["details"].ToString().Trim();
                    r.Image = form["image"].ToString().Trim();
                    r.MaxGuests = maxg;
                    if (units >= 1) r.Units = units;
                    AdminData.Save();
                    AdminData.Audit(ctx, "Room edited", r.Name + " (id " + r.Id + ") updated");
                }
            }
            return Results.Redirect("/admin/rooms");
        });
    }

    private static void MapAdd(WebApplication app)
    {
        app.MapGet("/admin/rooms/new", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Add a room</h2>");
            sb.Append("<form method='post' action='/admin/rooms/new'>");
            sb.Append("<label class='f'>Name</label><input class='t' name='name' required>");
            sb.Append("<label class='f'>Details</label><textarea class='t' name='details' rows='5'></textarea>");
            sb.Append("<label class='f'>Base price per night</label><input class='t' type='number' name='price' min='1' required>");
            sb.Append("<label class='f'>Image file name</label><input class='t' name='image' placeholder='e.g. standard.png'>");
            sb.Append("<label class='f'>Sleeps up to (max guests)</label><input class='t' type='number' name='maxguests' min='1' max='12' value='2' required>");
            sb.Append("<label class='f'>Units (inventory count)</label><input class='t' type='number' name='units' min='1' max='50' value='1' required>");
            sb.Append("<br><button class='btn'>Add room</button> <a class='btn gray' href='/admin/rooms'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "rooms", "Add room", sb.ToString());
        });

        app.MapPost("/admin/rooms/new", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string name = form["name"].ToString().Trim();
            string details = form["details"].ToString().Trim();
            string image = form["image"].ToString().Trim();
            int.TryParse(form["price"].ToString(), out int price);
            int.TryParse(form["maxguests"].ToString(), out int maxg);
            int.TryParse(form["units"].ToString(), out int units);
            if (name != "" && price > 0 && maxg >= 1)
            {
                Room r = new()
                {
                    Id = AdminData.NextId(AdminData.Rooms, x => x.Id),
                    Name = name,
                    Details = details,
                    Image = image,
                    Price = price,
                    MaxGuests = maxg,
                    Units = units < 1 ? 1 : units,
                    IsAvailable = true,
                    RoomStatus = "Clean"
                };
                AdminData.Rooms.Add(r);
                AdminData.Save();
                AdminData.Audit(ctx, "Room added", r.Name + " (id " + r.Id + ", " + AdminData.Money(r.Price) + ", " + r.Units + " unit(s))");
            }
            return Results.Redirect("/admin/rooms");
        });
    }

    private static void MapToggle(WebApplication app)
    {
        app.MapPost("/admin/rooms/toggle/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rooms");
            if (deny != null) return deny;
            AdminUser user = u!;

            Room? r = AdminData.Rooms.Find(x => x.Id == id);
            if (r != null)
            {
                r.IsAvailable = !r.IsAvailable;
                AdminData.Save();
                AdminData.Audit(ctx, "Room availability", r.Name + " marked " + (r.IsAvailable ? "available" : "sold out"));
            }
            return Results.Redirect("/admin/rooms");
        });
    }
}
