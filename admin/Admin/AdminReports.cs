// Admin/AdminReports.cs
// Reports: occupancy rate, ADR, RevPAR and revenue by source for a date range,
// plus a simple monthly revenue bar chart.
using System.Text;

public static class AdminReports
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/reports", (HttpContext ctx, string? from, string? to) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reports");
            if (deny != null) return deny;
            AdminUser user = u!;

            DateTime fromD = DateTime.TryParse(from, out DateTime f) ? f.Date : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime toD = DateTime.TryParse(to, out DateTime t) ? t.Date : DateTime.Today;
            if (toD < fromD) (fromD, toD) = (toD, fromD);
            int days = (toD - fromD).Days + 1;

            int capacity = 0;
            foreach (Room r in AdminData.Rooms) capacity += Math.Max(1, r.Units);
            capacity *= days;

            // nights each active booking overlaps the selected range
            int overlapNights = 0;
            var inRange = new List<Booking>();
            foreach (Booking b in AdminData.Bookings)
            {
                if (AdminData.StatusBlocks(b.Status)) continue;
                DateTime start = b.CheckIn > fromD ? b.CheckIn : fromD;
                DateTime end = b.CheckOut < toD.AddDays(1) ? b.CheckOut : toD.AddDays(1);
                int nights = (end - start).Days;
                if (nights > 0)
                {
                    overlapNights += nights;
                    if (b.CheckIn.Date >= fromD && b.CheckIn.Date <= toD) inRange.Add(b);
                }
            }

            int nightsSold = inRange.Sum(b => b.Nights);
            int roomRevenue = inRange.Sum(b => b.Subtotal);
            double occupancy = capacity > 0 ? 100.0 * overlapNights / capacity : 0;
            double adr = nightsSold > 0 ? (double)roomRevenue / nightsSold : 0;
            double revpar = capacity > 0 ? (double)roomRevenue / capacity : 0;

            var sb = new StringBuilder();

            sb.Append("<form class='acard filt' method='get' action='/admin/reports'>");
            sb.Append("<label>From<input class='t' type='date' name='from' value='" + AdminData.Ds(fromD) + "'></label>");
            sb.Append("<label>To<input class='t' type='date' name='to' value='" + AdminData.Ds(toD) + "'></label>");
            sb.Append("<button class='btn'>Run report</button></form>");

            sb.Append("<div class='stats'>");
            sb.Append("<div class='stat'><div class='lbl'>Occupancy rate</div><div class='val'>" + occupancy.ToString("F1") + "%</div><div class='sub'>" + overlapNights + " of " + capacity + " room-nights</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>ADR</div><div class='val'>" + AdminData.Money((int)Math.Round(adr)) + "</div><div class='sub'>average daily rate</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>RevPAR</div><div class='val'>" + AdminData.Money((int)Math.Round(revpar)) + "</div><div class='sub'>revenue per available room-night</div></div>");
            sb.Append("<div class='stat'><div class='lbl'>Room revenue</div><div class='val'>" + AdminData.Money(roomRevenue) + "</div><div class='sub'>bookings checking in " + AdminData.Ds(fromD) + " &rarr; " + AdminData.Ds(toD) + "</div></div>");
            sb.Append("</div>");

            // revenue by source
            sb.Append("<div class='acard'><h3>Revenue by source</h3><table class='atable'><tr><th>Source</th><th>Bookings</th><th>Collected</th><th>Refunded</th><th>Net</th></tr>");
            foreach (string source in AdminReservations.Sources)
            {
                var bookings = AdminData.Bookings.FindAll(b => b.Source == source);
                var ids = bookings.Select(b => b.Id).ToHashSet();
                int collected = 0, refunded = 0;
                foreach (Transaction tx in AdminData.Transactions)
                {
                    if (!ids.Contains(tx.BookingId)) continue;
                    if (tx.Type == "Payment" && tx.Status == "Completed") collected += tx.Amount;
                    if (tx.Type == "Refund" && tx.Status == "Completed") refunded += tx.Amount;
                }
                sb.Append("<tr><td>" + AdminData.StatusChip(source) + "</td><td>" + bookings.Count + "</td><td class='money'>" + AdminData.Money(collected) + "</td><td class='money'>-" + AdminData.Money(refunded) + "</td><td class='money'>" + AdminData.Money(collected - refunded) + "</td></tr>");
            }
            sb.Append("</table><p class='muted'>Direct = made on the guest website or by staff as Direct. OTA and Walk-in are chosen when staff create bookings.</p></div>");

            // monthly revenue bar chart (CSS only)
            var sb2 = new StringBuilder();
            int maxMonth = 1;
            var monthTotals = new List<(string label, int total)>();
            for (int i = 5; i >= 0; i--)
            {
                DateTime m = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                int sum = 0;
                foreach (Transaction tx in AdminData.Transactions)
                {
                    if (tx.Type == "Payment" && tx.Status == "Completed" && tx.Date.Year == m.Year && tx.Date.Month == m.Month) sum += tx.Amount;
                    if (tx.Type == "Refund" && tx.Status == "Completed" && tx.Date.Year == m.Year && tx.Date.Month == m.Month) sum -= tx.Amount;
                }
                if (sum > maxMonth) maxMonth = sum;
                monthTotals.Add((m.ToString("MMM yy"), sum));
            }
            foreach ((string label, int total) in monthTotals)
            {
                int h = total <= 0 ? 2 : (int)Math.Max(6, 120.0 * total / maxMonth);
                sb2.Append("<div style='text-align:center'><div style='height:130px;display:flex;align-items:end;justify-content:center'><div title='" + AdminData.Money(total) + "' style='width:46px;height:" + h + "px;background:#0071c2;border-radius:4px 4px 0 0'></div></div><div class='muted'>" + label + "</div><div style='font-size:12px;font-weight:600'>" + AdminData.Money(total) + "</div></div>");
            }
            sb.Append("<div class='acard'><h3>Revenue, last 6 months (payments minus refunds)</h3><div style='display:flex;gap:18px;align-items:end;overflow-x:auto'>");
            sb.Append(sb2.ToString());
            sb.Append("</div></div>");

            return AdminConsole.Page(user, "reports", "Reports", sb.ToString());
        });
    }
}
