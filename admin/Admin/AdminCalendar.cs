// Admin/AdminCalendar.cs
// Availability calendar: one row per room, one column per day of the month.
// Shows booked (full/partial), blackout dates and free days.
using System.Text;

public static class AdminCalendar
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/calendar", (HttpContext ctx, int? year, int? month, int? room) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "calendar");
            if (deny != null) return deny;
            AdminUser user = u!;

            DateTime today = DateTime.Today;
            int y = year ?? today.Year;
            int mo = month ?? today.Month;
            if (mo < 1) { mo = 12; y--; }
            if (mo > 12) { mo = 1; y++; }
            DateTime first = new(y, mo, 1);
            int days = DateTime.DaysInMonth(y, mo);

            var rooms = AdminData.Rooms;
            if (room != null && room > 0)
            {
                Room? sel = AdminData.Rooms.Find(r => r.Id == room);
                if (sel != null) rooms = new List<Room> { sel };
            }

            var sb = new StringBuilder();

            sb.Append("<div class='rowflex' style='margin-bottom:14px'>");
            sb.Append("<a class='btn small gray' href='/admin/calendar?year=" + (mo == 1 ? y - 1 : y) + "&month=" + (mo == 1 ? 12 : mo - 1) + (room != null ? "&room=" + room : "") + "'>&larr; Previous</a>");
            sb.Append("<b style='padding:0 10px'>" + first.ToString("MMMM yyyy") + "</b>");
            sb.Append("<a class='btn small gray' href='/admin/calendar?year=" + (mo == 12 ? y + 1 : y) + "&month=" + (mo == 12 ? 1 : mo + 1) + (room != null ? "&room=" + room : "") + "'>Next &rarr;</a>");
            sb.Append("<form method='get' action='/admin/calendar' class='rowflex'><input type='hidden' name='year' value='" + y + "'><input type='hidden' name='month' value='" + mo + "'>");
            sb.Append("<select class='t' name='room'><option value=''>All rooms</option>");
            foreach (Room r in AdminData.Rooms)
                sb.Append("<option value='" + r.Id + "'" + (room == r.Id ? " selected" : "") + ">" + AdminData.H(r.Name) + "</option>");
            sb.Append("</select><button class='btn small'>Show</button></form>");
            sb.Append("</div>");

            sb.Append("<div class='acard calwrap'><table class='cal'>");
            sb.Append("<tr><th style='min-width:120px'>Room</th>");
            for (int day = 1; day <= days; day++)
            {
                DateTime d = new(y, mo, day);
                bool isToday = d == today;
                sb.Append("<th" + (isToday ? " style='background:#003b95;color:#fff'" : "") + ">" + day + "<br>" + d.ToString("ddd")[..2] + "</th>");
            }
            sb.Append("</tr>");

            foreach (Room r in rooms)
            {
                sb.Append("<tr><td><b>" + AdminData.H(r.Name) + "</b><br><span class='muted'>" + r.Units + " unit(s)" + (r.IsAvailable ? "" : " - sold out") + "</span></td>");
                for (int day = 1; day <= days; day++)
                {
                    DateTime d = new(y, mo, day);
                    int booked = AdminData.BookedUnits(r, d);
                    bool black = AdminData.IsBlackout(r.Id, d);
                    string cls = "c-free";
                    string note = "free";
                    if (d < today) { cls = "c-past"; note = "past"; }
                    if (black) { cls = "c-blackout"; note = "blackout date"; }
                    if (booked >= Math.Max(1, r.Units)) { cls = "c-full"; note = "fully booked (" + booked + "/" + r.Units + ")"; }
                    else if (booked > 0) { cls = "c-partial"; note = "partially booked (" + booked + "/" + r.Units + ")"; }
                    if (!r.IsAvailable) note += " - room sold out";
                    sb.Append("<td class='" + cls + "' title='" + r.Name + " - " + AdminData.Ds(d) + ": " + note + "'>" + (booked > 0 ? booked : "") + "</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table></div>");

            sb.Append("<div class='legend'><span><i class='lg lg-free'></i> free</span><span><i class='lg lg-partial'></i> partially booked</span><span><i class='lg lg-full'></i> fully booked</span><span><i class='lg lg-blackout'></i> blackout date</span><span><i class='lg lg-past'></i> past</span></div>");
            sb.Append("<p class='muted'>Bookings arrive from the guest site and staff bookings. Blackout dates are managed in <a href='/admin/rates'>Rate Management</a>.</p>");

            return AdminConsole.Page(user, "calendar", "Availability Calendar", sb.ToString());
        });
    }
}
