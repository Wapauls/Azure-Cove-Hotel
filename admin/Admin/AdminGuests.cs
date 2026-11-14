// Admin/AdminGuests.cs
// Guest directory aggregated from bookings, plus per-guest history.
using System.Text;

public static class AdminGuests
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/guests", (HttpContext ctx, string? q) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "guests");
            if (deny != null) return deny;
            AdminUser user = u!;

            string sq = (q ?? "").Trim();

            var guests = AdminData.Bookings
                .GroupBy(b => b.Email)
                .Select(g => new GuestRow
                {
                    Email = g.Key,
                    Name = g.First().GuestName,
                    Count = g.Count(),
                    Spend = g.Sum(x => x.Total),
                    Last = g.Max(x => x.CheckIn)
                })
                .Where(g => sq == "" || g.Email.Contains(sq, StringComparison.OrdinalIgnoreCase) || g.Name.Contains(sq, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.Spend)
                .ToList();

            var sb = new StringBuilder();
            sb.Append("<form class='acard filt' method='get' action='/admin/guests'>");
            sb.Append("<label>Search<input class='t' name='q' value='" + AdminData.H(sq) + "' placeholder='name or e-mail'></label><button class='btn'>Search</button></form>");

            sb.Append("<div class='acard'><table class='atable'><tr><th>Guest</th><th>E-mail</th><th>Bookings</th><th>Total spend</th><th>Latest stay</th><th></th></tr>");
            if (guests.Count == 0) sb.Append("<tr><td colspan='6' class='muted'>No guests found. Guests appear here automatically after the first booking.</td></tr>");
            foreach (GuestRow g in guests)
            {
                sb.Append("<tr><td><b>" + AdminData.H(g.Name) + "</b></td><td>" + AdminData.H(g.Email) + "</td><td>" + g.Count + "</td><td class='money'>" + AdminData.Money(g.Spend) + "</td><td class='nowrap'>" + AdminData.D(g.Last) + "</td>");
                sb.Append("<td><a class='btn small gray' href='/admin/guests/" + AdminData.H(g.Email) + "'>History</a></td></tr>");
            }
            sb.Append("</table></div>");
            sb.Append("<p class='muted'>Guests are built from booking records - the guest site has no separate registration.</p>");

            return AdminConsole.Page(user, "guests", "Guests / Users", sb.ToString());
        });

        app.MapGet("/admin/guests/{email}", (HttpContext ctx, string email) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "guests");
            if (deny != null) return deny;
            AdminUser user = u!;

            var bookings = AdminData.Bookings.FindAll(b => b.Email == email).OrderByDescending(b => b.CheckIn).ToList();
            if (bookings.Count == 0) return AdminConsole.Page(user, "guests", "Guest", "<div class='alert bad'>Guest not found.</div><a class='btn' href='/admin/guests'>Back</a>");

            int spend = bookings.Sum(b => b.Total);

            var sb = new StringBuilder();
            sb.Append("<div class='stats'>");
            sb.Append("<div class='stat'><div class='lbl'>Guest</div><div class='val' style='font-size:20px'>" + AdminData.H(bookings[0].GuestName) + "</div><div class='sub'>" + AdminData.H(email) + "</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Bookings</div><div class='val'>" + bookings.Count + "</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Lifetime spend</div><div class='val'>" + AdminData.Money(spend) + "</div></div>");
            sb.Append("</div>");

            sb.Append("<div class='acard'><h3>Booking history</h3><table class='atable'><tr><th>#</th><th>Room</th><th>Stay</th><th>Total</th><th>Status</th><th></th></tr>");
            foreach (Booking b in bookings)
            {
                Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);
                sb.Append("<tr><td><a href='/admin/reservations/" + b.Id + "'>#" + b.Id + "</a></td><td>" + AdminData.H(room?.Name ?? "-") + "</td><td class='nowrap'>" + AdminData.D(b.CheckIn) + " &rarr; " + AdminData.D(b.CheckOut) + "</td><td class='money'>" + AdminData.Money(b.Total) + "</td><td>" + AdminData.StatusChip(b.Status) + "</td>");
                sb.Append("<td class='rowflex'><a class='btn small gray' href='/admin/reservations/" + b.Id + "'>View</a>");
                Folio? folio = AdminData.FolioFor(b.Id);
                if (folio != null) sb.Append("<a class='btn small gray' href='/admin/invoice/" + b.Id + "'>Invoice</a>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "guests", "Guest: " + bookings[0].GuestName, sb.ToString());
        });
    }

    private class GuestRow
    {
        public string Email = "";
        public string Name = "";
        public int Count;
        public int Spend;
        public DateTime Last;
    }
}
