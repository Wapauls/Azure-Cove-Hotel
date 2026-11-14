// Admin/AdminBilling.cs
// Guest folios (room charges, extras, taxes, payments) and printable invoices.
using System.Text;

public static class AdminBilling
{
    public static void Map(WebApplication app)
    {
        MapList(app);
        MapFolio(app);
        MapInvoice(app);
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/billing", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "billing");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard'><h2>Folios</h2><p class='muted'>Every booking gets an itemized folio: room charges per night, discounts, tax, extras and payments.</p>");
            sb.Append("<table class='atable'><tr><th>Invoice #</th><th>Booking</th><th>Guest</th><th>Charges</th><th>Paid</th><th>Balance</th><th></th></tr>");
            if (AdminData.Folios.Count == 0) sb.Append("<tr><td colspan='7' class='muted'>No folios yet. Folios are created automatically with each booking.</td></tr>");
            foreach (Folio f in AdminData.Folios.OrderByDescending(x => x.Id))
            {
                Booking? b = AdminData.Bookings.Find(x => x.Id == f.BookingId);
                int charges = f.Items.FindAll(i => i.Type == "charge" || i.Type == "tax" || i.Type == "extra").Sum(i => i.Amount);
                int paid = f.Items.FindAll(i => i.Type == "payment").Sum(i => i.Amount);
                int balance = AdminData.FolioBalance(f);
                sb.Append("<tr><td>INV-" + f.InvoiceNo.ToString("D4") + "</td><td><a href='/admin/reservations/" + f.BookingId + "'>#" + f.BookingId + "</a></td><td>" + AdminData.H(b?.GuestName ?? "-") + "</td>");
                sb.Append("<td class='money'>" + AdminData.Money(charges) + "</td><td class='money'>" + AdminData.Money(paid) + "</td><td class='money'>" + AdminData.Money(Math.Max(0, balance)) + " " + AdminData.StatusChip(balance <= 0 ? "Paid" : "Pending") + "</td>");
                sb.Append("<td class='rowflex'><a class='btn small' href='/admin/billing/" + f.BookingId + "'>Folio</a> <a class='btn small gray' href='/admin/invoice/" + f.BookingId + "'>Invoice</a></td></tr>");
            }
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "billing", "Billing / Invoicing", sb.ToString());
        });
    }

    private static void MapFolio(WebApplication app)
    {
        app.MapGet("/admin/billing/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "billing");
            if (deny != null) return deny;
            AdminUser user = u!;

            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            Folio? f = AdminData.FolioFor(id);
            if (b == null || f == null) return AdminConsole.Page(user, "billing", "Folio", "<div class='alert bad'>Folio not found.</div><a class='btn' href='/admin/billing'>Back</a>");
            Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);

            int balance = AdminData.FolioBalance(f);

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex no-print' style='margin-bottom:14px'>");
            sb.Append("<a class='btn' href='/admin/invoice/" + b.Id + "'>View invoice</a>");
            sb.Append("<a class='btn gray' href='/admin/reservations/" + b.Id + "'>Back to booking</a>");
            sb.Append("<span class='muted'>Balance: <b>" + AdminData.Money(Math.Max(0, balance)) + "</b></span></div>");

            sb.Append("<div class='acard'><h2>Folio - booking #" + b.Id + " (" + AdminData.H(b.GuestName) + ", " + AdminData.H(room?.Name ?? "-") + ")</h2>");
            sb.Append("<table class='atable'><tr><th>Date</th><th>Description</th><th>Type</th><th style='text-align:right'>Amount</th></tr>");
            foreach (FolioItem i in f.Items)
            {
                string sign = (i.Type == "charge" || i.Type == "tax" || i.Type == "extra") ? "" : "-";
                sb.Append("<tr><td class='nowrap'>" + AdminData.D(i.Date) + "</td><td>" + AdminData.H(i.Description) + "</td><td>" + AdminData.StatusChip(i.Type == "charge" ? "Room charge" : i.Type == "extra" ? "Extra" : i.Type) + "</td><td style='text-align:right'>" + sign + AdminData.Money(i.Amount) + "</td></tr>");
            }
            sb.Append("<tr><td colspan='3' style='text-align:right'><b>Balance due</b></td><td style='text-align:right' class='money'>" + AdminData.Money(Math.Max(0, balance)) + "</td></tr>");
            sb.Append("</table></div>");

            // add extra
            sb.Append("<div class='cols'>");
            sb.Append("<div class='acard'><h3>Add extra charge</h3><p class='muted'>Minibar, laundry, spa, room service...</p>");
            sb.Append("<form method='post' action='/admin/billing/extra/" + b.Id + "'>");
            sb.Append("<label class='f'>Description</label><input class='t' name='desc' required>");
            sb.Append("<label class='f'>Amount</label><input class='t' type='number' name='amount' min='1' required><br>");
            sb.Append("<button class='btn'>Add to folio</button></form></div>");

            // record payment
            sb.Append("<div class='acard'><h3>Record a payment</h3>");
            if (balance <= 0) sb.Append("<p class='muted'>This folio is fully settled.</p>");
            sb.Append("<form method='post' action='/admin/billing/pay/" + b.Id + "'>");
            sb.Append("<label class='f'>Method</label><select class='t' name='method'>");
            foreach (string m in AdminData.PaymentMethods) sb.Append("<option>" + AdminData.H(m) + "</option>");
            sb.Append("</select>");
            sb.Append("<label class='f'>Amount</label><input class='t' type='number' name='amount' min='1' value='" + Math.Max(1, balance) + "'><br>");
            sb.Append("<button class='btn'>Record payment</button></form></div>");
            sb.Append("</div>");

            return AdminConsole.Page(user, "billing", "Folio #" + f.InvoiceNo.ToString("D4"), sb.ToString());
        });

        app.MapPost("/admin/billing/extra/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "billing");
            if (deny != null) return deny;
            AdminUser user = u!;

            Folio? f = AdminData.FolioFor(id);
            if (f != null)
            {
                var form = await ctx.Request.ReadFormAsync();
                string desc = form["desc"].ToString().Trim();
                int.TryParse(form["amount"].ToString(), out int amount);
                if (desc != "" && amount > 0)
                {
                    f.Items.Add(new FolioItem { Id = AdminData.NextItemId(f), Date = DateTime.Now, Description = desc + " (added by " + user.Username + ")", Type = "extra", Amount = amount });
                    AdminData.Save();
                    AdminData.Audit(ctx, "Extra charge added", desc + " (" + AdminData.Money(amount) + ") added to folio of booking #" + id);
                }
            }
            return Results.Redirect("/admin/billing/" + id);
        });

        app.MapPost("/admin/billing/pay/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "billing");
            if (deny != null) return deny;
            AdminUser user = u!;

            Folio? f = AdminData.FolioFor(id);
            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            if (f != null && b != null)
            {
                var form = await ctx.Request.ReadFormAsync();
                string method = form["method"].ToString();
                int.TryParse(form["amount"].ToString(), out int amount);
                int balance = AdminData.FolioBalance(f);
                if (amount < 1) amount = 1;
                if (amount > balance) amount = balance;
                if (amount > 0 && AdminData.PaymentMethods.Contains(method))
                {
                    Transaction t = new()
                    {
                        Id = AdminData.NextId(AdminData.Transactions, x => x.Id),
                        BookingId = b.Id,
                        Type = "Payment",
                        Method = method,
                        Amount = amount,
                        Date = DateTime.Now,
                        Status = "Completed"
                    };
                    AdminData.Transactions.Add(t);
                    f.Items.Add(new FolioItem { Id = AdminData.NextItemId(f), Date = DateTime.Now, Description = "Payment received - " + method + " (TRX-" + t.Id.ToString("D4") + ")", Type = "payment", Amount = amount });
                    if (AdminData.FolioBalance(f) <= 0 && (b.Status == "Pay at hotel" || b.Status == "Checked-in"))
                        b.Status = "Paid";
                    AdminData.Save();
                    AdminData.Audit(ctx, "Payment recorded", AdminData.Money(amount) + " (" + method + ") recorded on folio of booking #" + id);
                }
            }
            return Results.Redirect("/admin/billing/" + id);
        });
    }

    private static void MapInvoice(WebApplication app)
    {
        app.MapGet("/admin/invoice/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "billing");
            if (deny != null) return deny;
            AdminUser user = u!;

            Booking? b = AdminData.Bookings.Find(x => x.Id == id);
            Folio? f = AdminData.FolioFor(id);
            if (b == null || f == null) return AdminConsole.Page(user, "billing", "Invoice", "<div class='alert bad'>Invoice not found.</div><a class='btn' href='/admin/billing'>Back</a>");
            Room? room = AdminData.Rooms.Find(r => r.Id == b.RoomId);

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex no-print' style='margin-bottom:14px'>");
            sb.Append("<button class='btn' onclick='window.print()'>Print invoice</button>");
            sb.Append("<a class='btn gray' href='/admin/billing/" + b.Id + "'>Back to folio</a></div>");

            sb.Append("<div class='paper'>");
            sb.Append("<div class='phead'><div><div class='pl'>" + AdminData.H(AdminData.Settings.HotelName) + "</div>");
            sb.Append("<div class='muted'>" + AdminData.H(AdminData.Settings.Address) + "<br>" + AdminData.H(AdminData.Settings.ContactEmail) + " &middot; " + AdminData.H(AdminData.Settings.ContactPhone) + "</div></div>");
            sb.Append("<div style='text-align:right'><b>INVOICE</b><br>INV-" + f.InvoiceNo.ToString("D4") + "<br>" + AdminData.D(DateTime.Now) + "</div></div>");

            sb.Append("<table class='atable'>");
            sb.Append("<tr><td>Billed to</td><td><b>" + AdminData.H(b.GuestName) + "</b><br>" + AdminData.H(b.Email) + "</td></tr>");
            sb.Append("<tr><td>Stay</td><td>" + AdminData.H(room?.Name ?? "-") + " &middot; booking #" + b.Id + "<br>" + AdminData.D(b.CheckIn) + " (" + AdminData.H(AdminData.Settings.CheckInTime) + ") to " + AdminData.D(b.CheckOut) + " (" + AdminData.H(AdminData.Settings.CheckOutTime) + ") &middot; " + b.Nights + " night(s)</td></tr>");
            sb.Append("</table><br>");

            int charges = 0, discounts = 0, tax = 0, paid = 0;
            sb.Append("<table class='atable'><tr><th>Description</th><th style='text-align:right'>Amount</th></tr>");
            foreach (FolioItem i in f.Items)
            {
                switch (i.Type)
                {
                    case "charge": case "extra": charges += i.Amount; break;
                    case "discount": discounts += i.Amount; break;
                    case "tax": tax += i.Amount; break;
                    case "payment": paid += i.Amount; break;
                }
                if (i.Type != "payment")
                    sb.Append("<tr><td>" + AdminData.H(i.Description) + "</td><td style='text-align:right'>" + (i.Type == "discount" ? "-" : "") + AdminData.Money(i.Amount) + "</td></tr>");
            }
            sb.Append("</table>");
            int total = charges - discounts + tax;
            sb.Append("<div class='ptotal'><span>Total</span><span>" + AdminData.Money(total) + "</span></div>");
            sb.Append("<table class='atable' style='margin-top:10px'>");
            sb.Append("<tr><td>Payments received</td><td style='text-align:right'>-" + AdminData.Money(paid) + "</td></tr>");
            sb.Append("<tr><td><b>Balance due</b></td><td style='text-align:right'><b>" + AdminData.Money(Math.Max(0, total - paid)) + "</b></td></tr>");
            sb.Append("</table>");
            sb.Append("<p class='muted' style='margin-top:20px'>" + AdminData.H(AdminData.Settings.CancellationPolicy) + "</p>");
            sb.Append("</div>");

            return AdminConsole.Page(user, "billing", "Invoice INV-" + f.InvoiceNo.ToString("D4"), sb.ToString());
        });
    }
}
