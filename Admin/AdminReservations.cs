// Admin/AdminReservations.cs
// Reservation list with filters, booking details, walk-in / OTA bookings
// created by staff, digital receipts and status actions (check-in / out / cancel).
using System.Text;

public static class AdminReservations
{
    public static readonly string[] Statuses = { "Paid", "Pay at hotel", "Checked-in", "Checked-out", "Cancelled", "Refunded" };
    public static readonly string[] Sources = { "Direct", "OTA", "Walk-in" };

    public static void Map(WebApplication app)
    {
        MapList(app);
        MapNew(app);
        MapDetail(app);
        MapActions(app);
        MapReceipt(app);
    }

    // ---------- list ----------

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/reservations", (HttpContext ctx, string? q, string? status, string? source) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;

            string sq = (q ?? "").Trim();
            string sst = status ?? "";
            string ssrc = source ?? "";

            var list = AdminData.Bookings.FindAll(b =>
            {
                if (sq != "" && !b.GuestName.Contains(sq, StringComparison.OrdinalIgnoreCase)
                    && !b.Email.Contains(sq, StringComparison.OrdinalIgnoreCase)
                    && b.Id.ToString() != sq) return false;
                if (sst != "" && b.Status != sst) return false;
                if (ssrc != "" && b.Source != ssrc) return false;
                return true;
            }).OrderByDescending(b => b.CheckIn).ThenByDescending(b => b.Id).ToList();

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex' style='margin-bottom:14px'>");
            sb.Append("<a class='btn' href='/admin/reservations/new'>+ New booking</a>");
            sb.Append("<span class='muted'>" + list.Count + " booking(s)</span></div>");

            sb.Append("<form class='acard filt' method='get' action='/admin/reservations'>");
            sb.Append("<label>Search<input class='t' name='q' value='" + AdminData.H(sq) + "' placeholder='name, e-mail or #id'></label>");
            sb.Append("<label>Status<select class='t' name='status'><option value=''>any</option>");
            foreach (string s in Statuses) sb.Append("<option" + (sst == s ? " selected" : "") + ">" + s + "</option>");
            sb.Append("</select></label>");
            sb.Append("<label>Source<select class='t' name='source'><option value=''>any</option>");
            foreach (string s in Sources) sb.Append("<option" + (ssrc == s ? " selected" : "") + ">" + s + "</option>");
            sb.Append("</select></label><button class='btn'>Filter</button></form>");

            sb.Append("<div class='acard'><table class='atable'><tr><th>#</th><th>Guest</th><th>Room</th><th>Stay</th><th>Total</th><th>Method</th><th>Source</th><th>Status</th><th></th></tr>");
            if (list.Count == 0) sb.Append("<tr><td colspan='9' class='muted'>No bookings match.</td></tr>");
            foreach (Booking b in list)
            {
                Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);
                sb.Append("<tr><td><a href='/admin/reservations/" + b.Id + "'>#" + b.Id + "</a></td>");
                sb.Append("<td>" + AdminData.H(b.GuestName) + "<br><span class='muted'>" + AdminData.H(b.Email) + "</span></td>");
                sb.Append("<td>" + AdminData.H(room?.Name ?? "-") + "</td>");
                sb.Append("<td class='nowrap'>" + AdminData.D(b.CheckIn) + "<br><span class='muted'>to " + AdminData.D(b.CheckOut) + " (" + b.Nights + "n)</span></td>");
                sb.Append("<td class='money'>" + AdminData.Money(b.Total) + "</td><td>" + AdminData.H(b.PaymentMethod) + "</td>");
                sb.Append("<td>" + AdminData.StatusChip(b.Source) + "</td><td>" + AdminData.StatusChip(b.Status) + "</td>");
                sb.Append("<td><a class='btn small gray' href='/admin/reservations/" + b.Id + "'>View</a></td></tr>");
            }
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "reservations", "Reservations", sb.ToString());
        });
    }

    // ---------- staff-created booking (walk-in / OTA) ----------

    private static void MapNew(WebApplication app)
    {
        app.MapGet("/admin/reservations/new", (HttpContext ctx, int? room, string? date, int? nights) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;

            DateTime checkin = DateTime.TryParse(date, out DateTime d) ? d.Date : DateTime.Today;
            int n = nights != null && nights > 0 ? nights.Value : 1;

            var sb = new StringBuilder();
            sb.Append("<div class='acard slim'><h2>New booking (staff)</h2><p class='muted'>Creates the booking immediately, including transaction and folio.</p>");
            sb.Append("<form method='post' action='/admin/reservations/new'>");
            sb.Append("<label class='f'>Room</label><select class='t' name='room'>");
            foreach (Room r in AdminData.Rooms)
                sb.Append("<option value='" + r.Id + "'" + (room == r.Id ? " selected" : "") + ">" + AdminData.H(r.Name) + " - " + AdminData.Money(r.Price) + "/night</option>");
            sb.Append("</select>");
            sb.Append("<label class='f'>Guest name</label><input class='t' name='name' required>");
            sb.Append("<label class='f'>Guest e-mail</label><input class='t' type='email' name='email' required>");
            sb.Append("<label class='f'>Check-in date</label><input class='t' type='date' name='date' value='" + AdminData.Ds(checkin) + "' min='" + AdminData.Ds(DateTime.Today) + "' required>");
            sb.Append("<label class='f'>Nights</label><input class='t' type='number' name='nights' min='1' max='30' value='" + n + "'>");
            sb.Append("<label class='f'>Source</label><select class='t' name='source'>");
            foreach (string s in Sources) sb.Append("<option>" + s + "</option>");
            sb.Append("</select>");
            sb.Append("<label class='f'>Payment method</label><select class='t' name='method'>");
            foreach (string m in AdminData.PaymentMethods) sb.Append("<option>" + AdminData.H(m) + "</option>");
            sb.Append("</select>");
            sb.Append("<label class='f'>Promo code (optional)</label><input class='t' name='promo'>");
            sb.Append("<br><button class='btn'>Create booking</button></form></div>");

            return AdminConsole.Page(user, "reservations", "New booking", sb.ToString());
        });

        app.MapPost("/admin/reservations/new", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["room"], out int roomId);
            int.TryParse(form["nights"], out int nights);
            DateTime.TryParse(form["date"], out DateTime checkin);
            string name = form["name"].ToString().Trim();
            string email = form["email"].ToString().Trim().ToLower();
            string source = form["source"].ToString();
            string method = form["method"].ToString();
            string promo = form["promo"].ToString().Trim();

            Room? room = AdminData.Rooms.Find(r => r.Id == roomId);
            if (room == null || name == "" || !email.Contains("@") || nights < 1 || nights > 30 || checkin.Date < DateTime.Today
                || !Sources.Contains(source) || !AdminData.PaymentMethods.Contains(method))
                return AdminConsole.Page(user, "reservations", "New booking", "<div class='alert bad'>Check the details and try again.</div>" + "<a class='btn' href='/admin/reservations/new'>Back</a>");

            if (!AdminData.RoomFree(room, checkin, nights))
                return AdminConsole.Page(user, "reservations", "New booking", "<div class='alert bad'>The " + AdminData.H(room.Name) + " is not free for those dates (booked, blocked or sold out).</div><a class='btn' href='/admin/reservations/new'>Back</a>");

            Quote quote = AdminData.Quote(room, checkin, nights, promo);
            Booking booking = MakeBooking(room, name, email, checkin, nights, source, method, quote, null);
            booking.Status = method == "Cash at hotel" ? "Pay at hotel" : "Paid";
            Transaction tx = MakeTransaction(booking, method, booking.Status == "Paid" ? "Completed" : "Due at check-in");
            booking.TransactionId = tx.Id;
            AdminData.CreateFolio(booking, room, quote);
            AdminData.QueueEmail("booking_confirmation", booking);
            AdminData.Save();
            AdminData.Audit(ctx, "Booking created", "booking #" + booking.Id + " for " + booking.GuestName + " (" + source + ", " + AdminData.Money(booking.Total) + ")");
            return Results.Redirect("/admin/reservations/" + booking.Id);
        });
    }

    // ---------- detail ----------

    private static void MapDetail(WebApplication app)
    {
        app.MapGet("/admin/reservations/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;

            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (b == null) return AdminConsole.Page(user, "reservations", "Reservation", "<div class='alert bad'>Booking not found.</div><a class='btn' href='/admin/reservations'>Back</a>");
            Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);
            Folio? folio = AdminData.FolioFor(b.Id);

            var sb = new StringBuilder();
            sb.Append("<div class='cols'><div class='acard'><h2>Booking #" + b.Id + "</h2>");
            sb.Append("<table class='atable'>");
            sb.Append("<tr><td>Guest</td><td><b>" + AdminData.H(b.GuestName) + "</b><br><span class='muted'>" + AdminData.H(b.Email) + "</span></td></tr>");
            sb.Append("<tr><td>Room</td><td>" + AdminData.H(room?.Name ?? "-") + "</td></tr>");
            sb.Append("<tr><td>Check-in</td><td>" + AdminData.D(b.CheckIn) + " (" + AdminData.H(AdminData.Settings.CheckInTime) + ")</td></tr>");
            sb.Append("<tr><td>Check-out</td><td>" + AdminData.D(b.CheckOut) + " (" + AdminData.H(AdminData.Settings.CheckOutTime) + ")</td></tr>");
            sb.Append("<tr><td>Nights</td><td>" + b.Nights + "</td></tr>");
            sb.Append("<tr><td>Room charges</td><td class='money'>" + AdminData.Money(b.Subtotal) + "</td></tr>");
            if (b.Discount > 0) sb.Append("<tr><td>Discounts</td><td class='money'>-" + AdminData.Money(b.Discount) + "</td></tr>");
            sb.Append("<tr><td>" + AdminData.H(AdminData.Settings.TaxName) + " (" + AdminData.Settings.TaxPercent + "%)</td><td class='money'>" + AdminData.Money(b.Tax) + "</td></tr>");
            sb.Append("<tr><td><b>Total</b></td><td class='money'>" + AdminData.Money(b.Total) + "</td></tr>");
            sb.Append("<tr><td>Payment method</td><td>" + AdminData.H(b.PaymentMethod) + "</td></tr>");
            sb.Append("<tr><td>Source</td><td>" + AdminData.StatusChip(b.Source) + "</td></tr>");
            sb.Append("<tr><td>Status</td><td>" + AdminData.StatusChip(b.Status) + "</td></tr>");
            if (b.PromoCode != "") sb.Append("<tr><td>Promo code</td><td>" + AdminData.H(b.PromoCode) + "</td></tr>");
            sb.Append("</table><br>");

            sb.Append("<div class='rowflex'>");
            if (b.Status == "Paid" || b.Status == "Pay at hotel")
            {
                sb.Append("<form method='post' action='/admin/reservations/checkin/" + b.Id + "'><button class='btn'>Check in</button></form>");
                sb.Append("<form method='post' action='/admin/reservations/cancel/" + b.Id + "'><button class='btn red'>Cancel booking</button></form>");
                sb.Append("<form method='post' action='/admin/reservations/remind/" + b.Id + "'><button class='btn gray'>Send check-in reminder</button></form>");
            }
            else if (b.Status == "Checked-in")
            {
                sb.Append("<form method='post' action='/admin/reservations/checkout/" + b.Id + "'><button class='btn'>Check out</button></form>");
            }
            if (folio != null) sb.Append("<a class='btn gray' href='/admin/billing/" + b.Id + "'>Folio / invoice</a>");
            sb.Append("<a class='btn gray' href='/admin/guests/" + AdminData.H(b.Email) + "'>Guest profile</a>");
            sb.Append("</div></div>");

            sb.Append("<div class='acard'><h3>Transaction history</h3><table class='atable'><tr><th>#</th><th>Date</th><th>Type</th><th>Method</th><th>Amount</th><th>Status</th><th></th></tr>");
            var txs = AdminData.Transactions.FindAll(t => t.BookingId == b.Id).OrderBy(t => t.Date).ToList();
            if (txs.Count == 0) sb.Append("<tr><td colspan='7' class='muted'>No transactions.</td></tr>");
            foreach (Transaction t in txs)
                sb.Append("<tr><td><a href='/admin/receipt/" + t.Id + "'>TRX-" + t.Id.ToString("D4") + "</a></td><td class='nowrap'>" + t.Date.ToString("MMM d, HH:mm") + "</td><td>" + AdminData.H(t.Type) + "</td><td>" + AdminData.H(t.Method) + "</td><td class='money'>" + AdminData.Money(t.Amount) + "</td><td>" + AdminData.StatusChip(t.Status) + "</td><td><a class='btn small gray' href='/admin/receipt/" + t.Id + "'>Receipt</a></td></tr>");
            sb.Append("</table></div>");
            sb.Append("</div>");

            return AdminConsole.Page(user, "reservations", "Reservation #" + b.Id, sb.ToString());
        });
    }

    // ---------- status actions ----------

    private static void MapActions(WebApplication app)
    {
        app.MapPost("/admin/reservations/checkin/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (b != null && (b.Status == "Paid" || b.Status == "Pay at hotel"))
            {
                b.Status = "Checked-in";
                AdminData.Save();
                AdminData.Audit(ctx, "Check-in", "booking #" + b.Id + " (" + b.GuestName + ") checked in");
            }
            return Results.Redirect("/admin/reservations/" + id);
        });

        app.MapPost("/admin/reservations/checkout/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (b != null && b.Status == "Checked-in")
            {
                b.Status = "Checked-out";
                Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);
                if (room != null) room.RoomStatus = "Dirty"; // needs cleaning after departure
                AdminData.Save();
                AdminData.Audit(ctx, "Check-out", "booking #" + b.Id + " (" + b.GuestName + ") checked out, room marked Dirty");
            }
            return Results.Redirect("/admin/reservations/" + id);
        });

        app.MapPost("/admin/reservations/cancel/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;
            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (b == null || (b.Status != "Paid" && b.Status != "Pay at hotel"))
                return Results.Redirect("/admin/reservations/" + id);

            (int refundAmount, bool full) = AdminData.RefundableFor(b);
            if (b.Status == "Paid" && refundAmount > 0)
            {
                Transaction? original = AdminData.Transactions.Find(t => t.Id == b.TransactionId);
                Transaction refund = new()
                {
                    Id = AdminData.NextId(AdminData.Transactions, x => x.Id),
                    BookingId = b.Id,
                    Type = "Refund",
                    Method = b.PaymentMethod,
                    Amount = refundAmount,
                    Date = DateTime.Now,
                    Status = "Completed",
                    RelatedId = original?.Id ?? 0
                };
                AdminData.Transactions.Add(refund);
                Folio? folio = AdminData.FolioFor(b.Id);
                if (folio != null)
                    folio.Items.Add(new FolioItem { Id = AdminData.NextItemId(folio), Date = DateTime.Now, Description = full ? "Refund (full, cancellation by staff)" : "Partial refund (cancellation fee applied)", Type = "refund", Amount = refundAmount });
                b.Status = "Refunded";
            }
            else
            {
                b.Status = "Cancelled";
            }

            AdminData.QueueEmail("cancellation", b);
            AdminData.Save();
            AdminData.Audit(ctx, "Booking cancelled", "booking #" + b.Id + " (" + b.GuestName + ") cancelled by " + user.Username + (b.Status == "Refunded" ? ", refund " + AdminData.Money(refundAmount) : ", no refund"));
            return Results.Redirect("/admin/reservations/" + id);
        });

        app.MapPost("/admin/reservations/remind/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (b != null)
            {
                AdminData.QueueEmail("reminder", b);
                AdminData.Save();
                AdminData.Audit(ctx, "Reminder queued", "check-in reminder for booking #" + b.Id + " to " + b.Email);
            }
            return Results.Redirect("/admin/reservations/" + id);
        });
    }

    // ---------- digital receipt ----------

    private static void MapReceipt(WebApplication app)
    {
        app.MapGet("/admin/receipt/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "reservations");
            if (deny != null) return deny;
            AdminUser user = u!;

            Transaction? t = AdminData.Transactions.Find(x => x.Id == id);
            if (t == null) return AdminConsole.Page(user, "reservations", "Receipt", "<div class='alert bad'>Transaction not found.</div><a class='btn' href='/admin/payments'>Back</a>");
            Booking? b = AdminData.Bookings.Find(x => x.Id == t.BookingId);
            Room? room = b != null ? AdminData.Rooms.Find(r => r.Id == b.RoomId) : null;
            Folio? folio = AdminData.FolioFor(t.BookingId);

            string stampClass = t.Type == "Refund" ? "stamp refund" : t.Status == "Completed" ? "stamp" : t.Status == "Voided" ? "stamp void" : "stamp due";
            string stampText = t.Status == "Voided" ? "VOIDED" : t.Status == "Completed" ? (t.Type == "Refund" ? "REFUNDED" : "PAID") : "DUE";

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex no-print' style='margin-bottom:14px'>");
            sb.Append("<button class='btn' onclick='window.print()'>Print receipt</button>");
            sb.Append("<a class='btn gray' href='/admin/reservations/" + t.BookingId + "'>Back to booking</a></div>");

            sb.Append("<div class='paper'>");
            sb.Append("<div class='phead'><div><div class='pl'>" + AdminData.H(AdminData.Settings.HotelName) + "</div>");
            sb.Append("<div class='muted'>" + AdminData.H(AdminData.Settings.Address) + "<br>" + AdminData.H(AdminData.Settings.ContactEmail) + " &middot; " + AdminData.H(AdminData.Settings.ContactPhone) + "</div></div>");
            sb.Append("<div style='text-align:right'><b>" + (t.Type == "Refund" ? "REFUND SLIP" : "OFFICIAL RECEIPT") + "</b><br>TRX-" + t.Id.ToString("D4") + "<br>" + AdminData.D(t.Date) + "</div></div>");

            sb.Append("<table class='atable'>");
            sb.Append("<tr><td>Received from</td><td><b>" + AdminData.H(b?.GuestName ?? "-") + "</b> (" + AdminData.H(b?.Email ?? "-") + ")</td></tr>");
            sb.Append("<tr><td>For</td><td>" + AdminData.H(room?.Name ?? "-") + " &middot; booking #" + t.BookingId + " &middot; " + (b != null ? AdminData.D(b.CheckIn) + " to " + AdminData.D(b.CheckOut) : "-") + "</td></tr>");
            sb.Append("<tr><td>Payment method</td><td>" + AdminData.H(t.Method) + "</td></tr>");
            sb.Append("</table><br>");

            sb.Append("<table class='atable'><tr><th>Description</th><th style='text-align:right'>Amount</th></tr>");
            if (folio != null)
            {
                foreach (FolioItem i in folio.Items)
                {
                    if (i.Type == "charge" || i.Type == "discount" || i.Type == "tax")
                        sb.Append("<tr><td>" + AdminData.H(i.Description) + "</td><td style='text-align:right'>" + (i.Type == "discount" ? "-" : "") + AdminData.Money(i.Amount) + "</td></tr>");
                }
            }
            sb.Append("<tr><td><b>" + (t.Type == "Refund" ? "Refund issued" : "Amount " + (t.Status == "Completed" ? "paid" : "due")) + " - " + AdminData.H(t.Method) + "</b></td><td style='text-align:right'><b>" + AdminData.Money(t.Amount) + "</b></td></tr>");
            sb.Append("</table>");

            sb.Append("<div style='text-align:right;margin-top:24px'><span class='" + stampClass + "'>" + stampText + "</span></div>");
            sb.Append("<p class='muted' style='margin-top:20px'>" + AdminData.H(AdminData.Settings.CancellationPolicy) + "</p>");
            sb.Append("</div>");

            return AdminConsole.Page(user, "reservations", "Receipt TRX-" + t.Id.ToString("D4"), sb.ToString());
        });
    }

    // shared helpers used by the "new booking" flow

    public static Booking MakeBooking(Room room, string name, string email, DateTime checkin, int nights, string source, string method, Quote quote, string? promo)
    {
        Booking b = new()
        {
            Id = AdminData.Bookings.Count == 0 ? 1 : AdminData.Bookings.Max(x => x.Id) + 1,
            GuestName = name,
            Email = email,
            RoomId = room.Id,
            Nights = nights,
            CheckIn = checkin.Date,
            CheckOut = checkin.Date.AddDays(nights),
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Tax = quote.Tax,
            Total = quote.Total,
            PaymentMethod = method,
            Status = method == "Cash at hotel" ? "Pay at hotel" : "Paid",
            PromoCode = promo ?? "",
            Source = source
        };
        AdminData.Bookings.Add(b);
        return b;
    }

    public static Transaction MakeTransaction(Booking b, string method, string status)
    {
        Transaction t = new()
        {
            Id = AdminData.Transactions.Count == 0 ? 1 : AdminData.Transactions.Max(x => x.Id) + 1,
            BookingId = b.Id,
            Type = "Payment",
            Method = method,
            Amount = b.Total,
            Date = DateTime.Now,
            Status = status
        };
        AdminData.Transactions.Add(t);
        return t;
    }
}
