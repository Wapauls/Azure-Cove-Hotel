// Admin/AdminDashboard.cs
// Overview of everything: key numbers, room status, recent activity.
using System.Text;

public static class AdminDashboard
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/dashboard", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "dashboard");
            if (deny != null) return deny;
            AdminUser user = u!;

            var active = AdminData.Bookings.FindAll(b => !AdminData.StatusBlocks(b.Status));
            int totalBookings = active.Count;
            int paidRevenue = 0, pending = 0;
            foreach (Transaction t in AdminData.Transactions)
            {
                if (t.Type == "Payment" && t.Status == "Completed") paidRevenue += t.Amount;
                if (t.Type == "Payment" && t.Status == "Due at check-in") pending += t.Amount;
            }
            int arrivalsToday = active.FindAll(b => b.CheckIn.Date == DateTime.Today).Count;
            int openTasks = AdminData.Tasks.FindAll(t => t.Status != "Done").Count;

            var sb = new StringBuilder();
            sb.Append("<div class='stats'>");
            sb.Append("<div class='stat'><div class='lbl'>Active bookings</div><div class='val'>" + totalBookings + "</div><div class='sub'>not cancelled / refunded</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Revenue collected</div><div class='val'>" + AdminData.Money(paidRevenue) + "</div><div class='sub'>completed payments</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Pending payments</div><div class='val'>" + AdminData.Money(pending) + "</div><div class='sub'>due at check-in</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Arrivals today</div><div class='val'>" + arrivalsToday + "</div><div class='sub'>check-in " + AdminData.H(AdminData.Settings.CheckInTime) + "</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Open tasks</div><div class='val'>" + openTasks + "</div><div class='sub'>housekeeping</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Support tickets</div><div class='val'>" + AdminData.Tickets.Count + "</div><div class='sub'>open</div></div>");
            sb.Append("</div>");

            // room status
            sb.Append("<div class='acard'><h3>Room status</h3><div class='cols3'>");
            foreach (Room r in AdminData.Rooms)
            {
                int booked = AdminData.BookedUnits(r, DateTime.Today);
                sb.Append("<div class='hkcard'>");
                sb.Append("<div class='rowflex' style='justify-content:space-between'><b>" + AdminData.H(r.Name) + "</b>" + AdminData.StatusChip(r.RoomStatus) + "</div>");
                sb.Append("<div class='muted' style='margin:6px 0'>" + AdminData.H(AdminData.AvailabilityLabel(r, DateTime.Today, 1)) + " &middot; booked today " + booked + "/" + r.Units + "</div>");
                sb.Append("<div class='rowflex'><a class='btn small gray' href='/admin/rooms'>Inventory</a><a class='btn small gray' href='/admin/calendar'>Calendar</a></div>");
                sb.Append("</div>");
            }
            sb.Append("</div></div>");

            // two columns: recent transactions + recent bookings
            sb.Append("<div class='cols'>");
            sb.Append("<div class='acard'><h3>Recent transactions</h3><table class='atable'><tr><th>#</th><th>Date</th><th>Guest</th><th>Type</th><th>Amount</th><th>Status</th></tr>");
            var txs = AdminData.Transactions.OrderByDescending(t => t.Date).Take(8).ToList();
            if (txs.Count == 0) sb.Append("<tr><td colspan='6' class='muted'>No transactions yet.</td></tr>");
            foreach (Transaction t in txs)
            {
                Booking? b = AdminData.Bookings.Find(x => x.Id == t.BookingId);
                sb.Append("<tr><td><a href='/admin/receipt/" + t.Id + "'>TRX-" + t.Id.ToString("D4") + "</a></td><td class='nowrap'>" + t.Date.ToString("MMM d, HH:mm") + "</td><td>" + AdminData.H(b?.GuestName ?? "-") + "</td><td>" + AdminData.H(t.Type) + "</td><td class='money'>" + AdminData.Money(t.Amount) + "</td><td>" + AdminData.StatusChip(t.Status) + "</td></tr>");
            }
            sb.Append("</table><br><a class='btn small' href='/admin/payments'>All payments</a></div>");

            sb.Append("<div class='acard'><h3>Recent bookings</h3><table class='atable'><tr><th>#</th><th>Guest</th><th>Check-in</th><th>Status</th></tr>");
            var recent = AdminData.Bookings.OrderByDescending(b => b.Id).Take(8).ToList();
            if (recent.Count == 0) sb.Append("<tr><td colspan='4' class='muted'>No bookings yet.</td></tr>");
            foreach (Booking b in recent)
            {
                sb.Append("<tr><td><a href='/admin/reservations/" + b.Id + "'>#" + b.Id + "</a></td><td>" + AdminData.H(b.GuestName) + "</td><td class='nowrap'>" + AdminData.D(b.CheckIn) + "</td><td>" + AdminData.StatusChip(b.Status) + "</td></tr>");
            }
            sb.Append("</table><br><a class='btn small' href='/admin/reservations'>All reservations</a></div>");
            sb.Append("</div>");

            // quick links + support tickets
            sb.Append("<div class='cols'>");
            sb.Append("<div class='acard'><h3>Support tickets</h3><table class='atable'><tr><th>#</th><th>From</th><th>Topic</th><th>Message</th></tr>");
            foreach (Ticket t in AdminData.Tickets.OrderByDescending(x => x.Id).Take(6))
                sb.Append("<tr><td>" + t.Id + "</td><td>" + AdminData.H(t.Name) + "<br><span class='muted'>" + AdminData.H(t.Email) + "</span></td><td>" + AdminData.H(t.Topic) + "</td><td>" + AdminData.H(t.Message) + "</td></tr>");
            if (AdminData.Tickets.Count == 0) sb.Append("<tr><td colspan='4' class='muted'>No tickets yet.</td></tr>");
            sb.Append("</table></div>");
            sb.Append("<div class='acard'><h3>Quick actions</h3><div class='rowflex'>");
            sb.Append("<a class='btn' href='/admin/reservations/new'>New booking</a>");
            sb.Append("<a class='btn gray' href='/admin/housekeeping'>Housekeeping board</a>");
            sb.Append("<a class='btn gray' href='/admin/rates'>Rates</a>");
            sb.Append("<a class='btn gray' href='/admin/reports'>Reports</a>");
            sb.Append("<a class='btn gray' href='/admin/settings'>Settings</a>");
            sb.Append("</div></div>");
            sb.Append("</div>");

            return AdminConsole.Page(user, "dashboard", "Dashboard", sb.ToString());
        });
    }
}
