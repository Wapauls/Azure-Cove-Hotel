// Admin/AdminRates.cs
// Rate management: per-room base price, seasonal rate adjustments,
// length-of-stay discounts and blackout dates.
using System.Text;

public static class AdminRates
{
    public static void Map(WebApplication app)
    {
        MapBase(app);
        MapSeasonal(app);
        MapLos(app);
        MapBlackouts(app);
    }

    private static void MapBase(WebApplication app)
    {
        app.MapGet("/admin/rates", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<p class='muted'>Pricing order: <b>seasonal rate</b> adjusts each night's price &rarr; <b>length-of-stay discount</b> &rarr; <b>best eligible offer</b> &rarr; <b>tax</b> (" + AdminData.Settings.TaxPercent + "% " + AdminData.H(AdminData.Settings.TaxName) + ").</p>");

            // base rates
            sb.Append("<div class='acard'><h3>Base nightly rates</h3><table class='atable'><tr><th>Room</th><th>Current base price</th><th>Set new base price</th></tr>");
            foreach (Room r in AdminData.Rooms)
            {
                sb.Append("<tr><td><b>" + AdminData.H(r.Name) + "</b> <span class='muted'>(id " + r.Id + ", " + r.Units + " unit(s))</span></td><td class='money'>" + AdminData.Money(r.Price) + "</td>");
                sb.Append("<td><form method='post' action='/admin/rates/base/" + r.Id + "' class='rowflex'><input class='t' style='width:130px' type='number' name='price' min='1' value='" + r.Price + "'><button class='btn small'>Save</button></form></td></tr>");
            }
            sb.Append("</table></div>");

            // seasonal rates
            sb.Append("<div class='cols'><div class='acard'><h3>Seasonal rates</h3>");
            sb.Append("<table class='atable'><tr><th>Scope</th><th>Period</th><th>Adjust</th><th></th></tr>");
            if (AdminData.SeasonalRates.Count == 0) sb.Append("<tr><td colspan='4' class='muted'>No seasonal rates. Base prices apply all year.</td></tr>");
            foreach (SeasonalRate s in AdminData.SeasonalRates)
            {
                Room? room = s.RoomId != 0 ? AdminData.Rooms.Find(r => r.Id == s.RoomId) : null;
                sb.Append("<tr><td>" + AdminData.H(room?.Name ?? "All rooms") + "</td><td class='nowrap'>" + AdminData.Ds(s.From) + " &rarr; " + AdminData.Ds(s.To) + "</td>");
                sb.Append("<td class='money'>" + (s.PercentAdjust >= 0 ? "+" : "") + s.PercentAdjust + "%</td>");
                sb.Append("<td><form method='post' action='/admin/rates/seasonal/delete/" + s.Id + "'><button class='btn small red'>Delete</button></form></td></tr>");
            }
            sb.Append("</table><br>");
            sb.Append("<form method='post' action='/admin/rates/seasonal' class='rowflex'>");
            sb.Append("<select class='t' name='room' style='width:auto'><option value='0'>All rooms</option>");
            foreach (Room r in AdminData.Rooms) sb.Append("<option value='" + r.Id + "'>" + AdminData.H(r.Name) + "</option>");
            sb.Append("</select>");
            sb.Append("<input class='t' style='width:140px' type='date' name='from' value='" + AdminData.Ds(DateTime.Today) + "' required>");
            sb.Append("<input class='t' style='width:140px' type='date' name='to' value='" + AdminData.Ds(DateTime.Today.AddDays(14)) + "' required>");
            sb.Append("<input class='t' style='width:90px' type='number' name='percent' value='20' required> <span class='muted'>%</span>");
            sb.Append("<button class='btn small'>Add period</button></form>");
            sb.Append("<p class='muted'>Percent adjust: +20 means 20% higher nightly price, -15 means 15% lower.</p></div>");

            // length of stay discounts
            sb.Append("<div class='acard'><h3>Length-of-stay discounts</h3>");
            sb.Append("<table class='atable'><tr><th>Stay length</th><th>Discount</th><th></th></tr>");
            if (AdminData.LosDiscounts.Count == 0) sb.Append("<tr><td colspan='3' class='muted'>No length-of-stay discounts.</td></tr>");
            foreach (LosDiscount d in AdminData.LosDiscounts.OrderBy(x => x.MinNights))
            {
                sb.Append("<tr><td>" + d.MinNights + "+ nights</td><td class='money'>" + d.PercentOff + "% off</td>");
                sb.Append("<td><form method='post' action='/admin/rates/los/delete/" + d.Id + "'><button class='btn small red'>Delete</button></form></td></tr>");
            }
            sb.Append("</table><br>");
            sb.Append("<form method='post' action='/admin/rates/los' class='rowflex'>");
            sb.Append("<input class='t' style='width:90px' type='number' name='minnights' min='1' value='3' required> <span class='muted'>+ nights</span>");
            sb.Append("<input class='t' style='width:90px' type='number' name='percent' min='1' max='90' value='10' required> <span class='muted'>% off</span>");
            sb.Append("<button class='btn small'>Add discount</button></form>");
            sb.Append("<p class='muted'>The biggest discount that matches the stay length is used.</p></div>");
            sb.Append("</div>");

            // blackout dates
            sb.Append("<div class='acard'><h3>Blackout dates</h3>");
            sb.Append("<table class='atable'><tr><th>Scope</th><th>Date</th><th></th></tr>");
            if (AdminData.Blackouts.Count == 0) sb.Append("<tr><td colspan='3' class='muted'>No blackout dates.</td></tr>");
            foreach (BlackoutDate bl in AdminData.Blackouts.OrderBy(x => x.Date))
            {
                Room? room = bl.RoomId != 0 ? AdminData.Rooms.Find(r => r.Id == bl.RoomId) : null;
                sb.Append("<tr><td>" + AdminData.H(room?.Name ?? "All rooms") + "</td><td class='nowrap'>" + AdminData.D(bl.Date) + "</td>");
                sb.Append("<td><form method='post' action='/admin/rates/blackout/delete/" + bl.Id + "'><button class='btn small red'>Delete</button></form></td></tr>");
            }
            sb.Append("</table><br>");
            sb.Append("<form method='post' action='/admin/rates/blackout' class='rowflex'>");
            sb.Append("<select class='t' name='room' style='width:auto'><option value='0'>All rooms</option>");
            foreach (Room r in AdminData.Rooms) sb.Append("<option value='" + r.Id + "'>" + AdminData.H(r.Name) + "</option>");
            sb.Append("</select>");
            sb.Append("<input class='t' style='width:150px' type='date' name='date' value='" + AdminData.Ds(DateTime.Today) + "' required>");
            sb.Append("<button class='btn small'>Block date</button></form>");
            sb.Append("<p class='muted'>Guests cannot check in, or stay over, a blocked date.</p></div>");

            return AdminConsole.Page(user, "rates", "Rate Management", sb.ToString());
        });

        app.MapPost("/admin/rates/base/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            AdminUser user = u!;

            Room? r = AdminData.Rooms.Find(x => x.Id == id);
            if (r != null)
            {
                var form = await ctx.Request.ReadFormAsync();
                int.TryParse(form["price"].ToString(), out int price);
                if (price > 0)
                {
                    int old = r.Price;
                    r.Price = price;
                    AdminData.Save();
                    AdminData.Audit(ctx, "Base rate changed", r.Name + " from " + AdminData.Money(old) + " to " + AdminData.Money(price) + " per night");
                }
            }
            return Results.Redirect("/admin/rates");
        });
    }

    private static void MapSeasonal(WebApplication app)
    {
        app.MapPost("/admin/rates/seasonal", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["room"].ToString(), out int roomId);
            int.TryParse(form["percent"].ToString(), out int percent);
            DateTime.TryParse(form["from"].ToString(), out DateTime from);
            DateTime.TryParse(form["to"].ToString(), out DateTime to);
            if (from != DateTime.MinValue && to != DateTime.MinValue && to >= from)
            {
                SeasonalRate s = new()
                {
                    Id = AdminData.NextId(AdminData.SeasonalRates, x => x.Id),
                    RoomId = roomId,
                    From = from,
                    To = to,
                    PercentAdjust = percent
                };
                AdminData.SeasonalRates.Add(s);
                AdminData.Save();
                AdminData.Audit(ctx, "Seasonal rate added", (roomId == 0 ? "All rooms" : "room id " + roomId) + " " + AdminData.Ds(from) + " to " + AdminData.Ds(to) + " (" + (percent >= 0 ? "+" : "") + percent + "%)");
            }
            return Results.Redirect("/admin/rates");
        });

        app.MapPost("/admin/rates/seasonal/delete/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            SeasonalRate? s = AdminData.SeasonalRates.Find(x => x.Id == id);
            if (s != null)
            {
                AdminData.SeasonalRates.Remove(s);
                AdminData.Save();
                AdminData.Audit(ctx, "Seasonal rate deleted", "id " + id);
            }
            return Results.Redirect("/admin/rates");
        });
    }

    private static void MapLos(WebApplication app)
    {
        app.MapPost("/admin/rates/los", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["minnights"].ToString(), out int min);
            int.TryParse(form["percent"].ToString(), out int percent);
            if (min >= 1 && percent >= 1 && percent <= 90)
            {
                AdminData.LosDiscounts.Add(new LosDiscount { Id = AdminData.NextId(AdminData.LosDiscounts, x => x.Id), MinNights = min, PercentOff = percent });
                AdminData.Save();
                AdminData.Audit(ctx, "LOS discount added", min + "+ nights get " + percent + "% off");
            }
            return Results.Redirect("/admin/rates");
        });

        // list rows are re-rendered with delete forms once entries exist;
        // support deleting the newest matching row by id pattern used above
        app.MapPost("/admin/rates/los/delete/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            LosDiscount? d = AdminData.LosDiscounts.Find(x => x.Id == id);
            if (d != null)
            {
                AdminData.LosDiscounts.Remove(d);
                AdminData.Save();
                AdminData.Audit(ctx, "LOS discount deleted", "id " + id);
            }
            return Results.Redirect("/admin/rates");
        });
    }

    private static void MapBlackouts(WebApplication app)
    {
        app.MapPost("/admin/rates/blackout", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["room"].ToString(), out int roomId);
            DateTime.TryParse(form["date"].ToString(), out DateTime date);
            if (date != DateTime.MinValue)
            {
                AdminData.Blackouts.Add(new BlackoutDate { Id = AdminData.NextId(AdminData.Blackouts, x => x.Id), RoomId = roomId, Date = date });
                AdminData.Save();
                AdminData.Audit(ctx, "Blackout date added", (roomId == 0 ? "All rooms" : "room id " + roomId) + " on " + AdminData.Ds(date));
            }
            return Results.Redirect("/admin/rates");
        });

        app.MapPost("/admin/rates/blackout/delete/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "rates");
            if (deny != null) return deny;
            BlackoutDate? bl = AdminData.Blackouts.Find(x => x.Id == id);
            if (bl != null)
            {
                AdminData.Blackouts.Remove(bl);
                AdminData.Save();
                AdminData.Audit(ctx, "Blackout date deleted", "id " + id);
            }
            return Results.Redirect("/admin/rates");
        });
    }
}
