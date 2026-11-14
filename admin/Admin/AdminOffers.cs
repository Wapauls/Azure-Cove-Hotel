// Admin/AdminOffers.cs
// Special offers, discounts and promo codes. Offers with a code must be
// entered by the guest; offers without a code apply automatically when eligible.
using System.Text;

public static class AdminOffers
{
    public static void Map(WebApplication app)
    {
        MapList(app);
        MapAdd(app);
        MapActions(app);
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/offers", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "offers");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex' style='margin-bottom:14px'><a class='btn' href='/admin/offers/new'>+ Create offer</a></div>");

            sb.Append("<div class='acard'><table class='atable'><tr><th>Offer</th><th>Discount</th><th>Scope</th><th>Code</th><th>Valid</th><th>Min nights</th><th>Status</th><th></th></tr>");
            if (AdminData.Offers.Count == 0) sb.Append("<tr><td colspan='8' class='muted'>No offers yet.</td></tr>");
            foreach (Offer o in AdminData.Offers.OrderByDescending(x => x.Id))
            {
                Room? room = o.RoomId != 0 ? AdminData.Rooms.Find(r => r.Id == o.RoomId) : null;
                sb.Append("<tr><td><b>" + AdminData.H(o.Name) + "</b></td>");
                sb.Append("<td class='money'>" + (o.Type == "percent" ? o.Value + "% off" : AdminData.Money(o.Value) + " off") + "</td>");
                sb.Append("<td>" + AdminData.H(room?.Name ?? "All rooms") + "</td>");
                sb.Append("<td>" + (o.Code == "" ? "<span class='chip info'>auto-applied</span>" : "<span class='chip mut'>" + AdminData.H(o.Code) + "</span>") + "</td>");
                sb.Append("<td class='nowrap'>" + AdminData.Ds(o.ValidFrom) + " &rarr; " + AdminData.Ds(o.ValidTo) + "</td><td>" + o.MinNights + "+</td>");
                sb.Append("<td>" + AdminData.StatusChip(o.Enabled ? "Available" : "Sold out").Replace("Available", "Enabled").Replace("Sold out", "Disabled") + "</td>");
                sb.Append("<td class='rowflex'>");
                sb.Append("<form method='post' action='/admin/offers/toggle/" + o.Id + "'><button class='btn small " + (o.Enabled ? "red" : "") + "'>" + (o.Enabled ? "Disable" : "Enable") + "</button></form>");
                sb.Append("<form method='post' action='/admin/offers/delete/" + o.Id + "'><button class='btn small red'>Delete</button></form>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");

            sb.Append("<p class='muted'>How discounts stack: seasonal rates set the nightly price, then the length-of-stay discount applies, then the best eligible offer, then tax. One offer is applied per booking.</p>");

            return AdminConsole.Page(user, "offers", "Offers / Promotions", sb.ToString());
        });
    }

    private static void MapAdd(WebApplication app)
    {
        app.MapGet("/admin/offers/new", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "offers");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>Create offer</h2>");
            sb.Append("<form method='post' action='/admin/offers/new'>");
            sb.Append("<label class='f'>Name</label><input class='t' name='name' placeholder='e.g. Rainy season deal' required>");
            sb.Append("<label class='f'>Discount type</label><select class='t' name='type'><option value='percent'>Percent off</option><option value='fixed'>Fixed amount off</option></select>");
            sb.Append("<label class='f'>Value (percent 1-100 or amount)</label><input class='t' type='number' name='value' min='1' required>");
            sb.Append("<label class='f'>Applies to</label><select class='t' name='room'><option value='0'>All rooms</option>");
            foreach (Room r in AdminData.Rooms) sb.Append("<option value='" + r.Id + "'>" + AdminData.H(r.Name) + "</option>");
            sb.Append("</select>");
            sb.Append("<label class='f'>Promo code (leave empty to auto-apply)</label><input class='t' name='code' placeholder='e.g. RAINY10'>");
            sb.Append("<label class='f'>Valid from</label><input class='t' type='date' name='from' value='" + AdminData.Ds(DateTime.Today) + "' required>");
            sb.Append("<label class='f'>Valid to</label><input class='t' type='date' name='to' value='" + AdminData.Ds(DateTime.Today.AddMonths(3)) + "' required>");
            sb.Append("<label class='f'>Minimum nights</label><input class='t' type='number' name='minnights' min='1' value='1'>");
            sb.Append("<br><button class='btn'>Create offer</button> <a class='btn gray' href='/admin/offers'>Cancel</a></form></div>");

            return AdminConsole.Page(user, "offers", "Create offer", sb.ToString());
        });

        app.MapPost("/admin/offers/new", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "offers");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string name = form["name"].ToString().Trim();
            string type = form["type"].ToString();
            int.TryParse(form["value"].ToString(), out int value);
            int.TryParse(form["room"].ToString(), out int roomId);
            int.TryParse(form["minnights"].ToString(), out int minNights);
            string code = form["code"].ToString().Trim();
            DateTime.TryParse(form["from"].ToString(), out DateTime from);
            DateTime.TryParse(form["to"].ToString(), out DateTime to);

            if (name == "" || value < 1 || from == DateTime.MinValue || to == DateTime.MinValue || to < from)
                return AdminConsole.Page(user, "offers", "Create offer", "<div class='alert bad'>Check the fields: name, value and a valid date range are required.</div><a class='btn' href='/admin/offers/new'>Back</a>");

            Offer o = new()
            {
                Id = AdminData.NextId(AdminData.Offers, x => x.Id),
                Name = name,
                Type = type == "fixed" ? "fixed" : "percent",
                Value = type == "percent" ? Math.Min(value, 100) : value,
                RoomId = roomId,
                Code = code,
                ValidFrom = from,
                ValidTo = to,
                MinNights = minNights < 1 ? 1 : minNights,
                Enabled = true
            };
            AdminData.Offers.Add(o);
            AdminData.Save();
            AdminData.Audit(ctx, "Offer created", name + " (" + (o.Type == "percent" ? o.Value + "%" : AdminData.Money(o.Value) + " off") + (code == "" ? ", auto" : ", code " + code) + ")");
            return Results.Redirect("/admin/offers");
        });
    }

    private static void MapActions(WebApplication app)
    {
        app.MapPost("/admin/offers/toggle/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "offers");
            if (deny != null) return deny;
            Offer? o = AdminData.Offers.Find(x => x.Id == id);
            if (o != null)
            {
                o.Enabled = !o.Enabled;
                AdminData.Save();
                AdminData.Audit(ctx, "Offer " + (o.Enabled ? "enabled" : "disabled"), o.Name);
            }
            return Results.Redirect("/admin/offers");
        });

        app.MapPost("/admin/offers/delete/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "offers");
            if (deny != null) return deny;
            AdminUser user = u!;
            Offer? o = AdminData.Offers.Find(x => x.Id == id);
            if (o != null)
            {
                AdminData.Offers.Remove(o);
                AdminData.Save();
                AdminData.Audit(ctx, "Offer deleted", o.Name + " by " + user.Username);
            }
            return Results.Redirect("/admin/offers");
        });
    }
}
