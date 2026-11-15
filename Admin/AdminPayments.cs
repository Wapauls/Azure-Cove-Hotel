// Admin/AdminPayments.cs
// Payment logs, gateway status, refund tracking and duplicate detection.
using System.Text;

public static class AdminPayments
{
    public static void Map(WebApplication app)
    {
        MapLedger(app);
        MapGateway(app);
        MapRefund(app);
        MapDuplicates(app);
    }

    private static void MapLedger(WebApplication app)
    {
        app.MapGet("/admin/payments", (HttpContext ctx, string? type, string? method, string? status) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "payments");
            if (deny != null) return deny;
            AdminUser user = u!;

            string st = type ?? "", sm = method ?? "", ss = status ?? "";

            var list = AdminData.Transactions.FindAll(t =>
                (st == "" || t.Type == st) && (sm == "" || t.Method == sm) && (ss == "" || t.Status == ss))
                .OrderByDescending(t => t.Date).Take(300).ToList();

            var sb = new StringBuilder();

            // gateway status
            sb.Append("<div class='acard'><h3>Gateway status</h3><p class='muted'>Offline methods are hidden from guest checkout. Cash at hotel is not processed by a gateway but follows the same switch.</p><div class='rowflex'>");
            foreach (string m in AdminData.PaymentMethods)
            {
                bool online = AdminData.IsGatewayOnline(m);
                sb.Append("<form method='post' action='/admin/payments/gateway' class='rowflex'>");
                sb.Append("<input type='hidden' name='method' value='" + AdminData.H(m) + "'>");
                sb.Append("<span class='chip " + (online ? "ok" : "bad") + "'>" + AdminData.H(m) + (online ? " - online" : " - offline") + "</span>");
                sb.Append("<button class='btn small gray'>" + (online ? "Set offline" : "Set online") + "</button></form>");
            }
            sb.Append("</div></div>");

            // duplicate detection
            var dupes = AdminData.FlaggedDuplicates();
            sb.Append("<div class='acard'><h3>Duplicate detection</h3>");
            if (dupes.Count == 0) sb.Append("<p class='muted'>No duplicate payments detected.</p>");
            else
            {
                sb.Append("<p>These payments look like possible duplicates (same booking within 10 minutes, or same guest, amount and method on the same day). Review them, then dismiss or void.</p>");
                sb.Append("<table class='atable'><tr><th>#</th><th>Date</th><th>Booking</th><th>Type</th><th>Method</th><th>Amount</th><th></th></tr>");
                foreach (Transaction t in dupes)
                {
                    sb.Append("<tr><td>TRX-" + t.Id.ToString("D4") + "</td><td class='nowrap'>" + t.Date.ToString("MMM d, HH:mm") + "</td><td><a href='/admin/reservations/" + t.BookingId + "'>#" + t.BookingId + "</a></td><td>" + AdminData.H(t.Type) + "</td><td>" + AdminData.H(t.Method) + "</td><td class='money'>" + AdminData.Money(t.Amount) + "</td>");
                    sb.Append("<td class='rowflex'><form method='post' action='/admin/payments/dupe/dismiss/" + t.Id + "'><button class='btn small gray'>Dismiss</button></form>");
                    sb.Append("<form method='post' action='/admin/payments/dupe/void/" + t.Id + "'><button class='btn small red'>Mark void</button></form></td></tr>");
                }
                sb.Append("</table>");
            }
            sb.Append("</div>");

            // refunds overview
            sb.Append("<div class='acard'><h3>Refund tracking</h3><table class='atable'><tr><th>#</th><th>Date</th><th>Booking</th><th>Method</th><th>Amount</th><th>Status</th><th>Original payment</th></tr>");
            var refunds = AdminData.Transactions.FindAll(t => t.Type == "Refund").OrderByDescending(t => t.Date).ToList();
            if (refunds.Count == 0) sb.Append("<tr><td colspan='7' class='muted'>No refunds yet.</td></tr>");
            foreach (Transaction t in refunds)
            {
                Transaction? orig = t.RelatedId != 0 ? AdminData.Transactions.Find(x => x.Id == t.RelatedId) : null;
                sb.Append("<tr><td>TRX-" + t.Id.ToString("D4") + "</td><td class='nowrap'>" + t.Date.ToString("MMM d, HH:mm") + "</td><td><a href='/admin/reservations/" + t.BookingId + "'>#" + t.BookingId + "</a></td><td>" + AdminData.H(t.Method) + "</td><td class='money'>" + AdminData.Money(t.Amount) + "</td><td>" + AdminData.StatusChip(t.Status) + "</td><td class='muted'>" + (orig != null ? "TRX-" + orig.Id.ToString("D4") : "-") + "</td></tr>");
            }
            sb.Append("</table></div>");

            // ledger
            sb.Append("<div class='acard'><h3>Payment log</h3>");
            sb.Append("<form class='filt' method='get' action='/admin/payments'>");
            sb.Append("<label>Type<select class='t' name='type'><option value=''>any</option><option" + (st == "Payment" ? " selected" : "") + ">Payment</option><option" + (st == "Refund" ? " selected" : "") + ">Refund</option></select></label>");
            sb.Append("<label>Method<select class='t' name='method'><option value=''>any</option>");
            foreach (string m in AdminData.PaymentMethods) sb.Append("<option" + (sm == m ? " selected" : "") + ">" + AdminData.H(m) + "</option>");
            sb.Append("</select></label>");
            sb.Append("<label>Status<select class='t' name='status'><option value=''>any</option>");
            foreach (string s in new[] { "Completed", "Due at check-in", "Voided" }) sb.Append("<option" + (ss == s ? " selected" : "") + ">" + s + "</option>");
            sb.Append("</select></label><button class='btn'>Filter</button></form><br>");

            sb.Append("<table class='atable'><tr><th>#</th><th>Date</th><th>Booking</th><th>Guest</th><th>Type</th><th>Method</th><th>Amount</th><th>Status</th><th></th></tr>");
            if (list.Count == 0) sb.Append("<tr><td colspan='9' class='muted'>No transactions match.</td></tr>");
            foreach (Transaction t in list)
            {
                Booking? b = AdminData.Bookings.Find(x => x.Id == t.BookingId);
                sb.Append("<tr><td><a href='/admin/receipt/" + t.Id + "'>TRX-" + t.Id.ToString("D4") + "</a></td><td class='nowrap'>" + t.Date.ToString("MMM d, HH:mm") + "</td><td><a href='/admin/reservations/" + t.BookingId + "'>#" + t.BookingId + "</a></td><td>" + AdminData.H(b?.GuestName ?? "-") + "</td><td>" + AdminData.H(t.Type) + "</td><td>" + AdminData.H(t.Method) + "</td><td class='money'>" + AdminData.Money(t.Amount) + "</td><td>" + AdminData.StatusChip(t.Status) + "</td>");
                sb.Append("<td class='rowflex'><a class='btn small gray' href='/admin/receipt/" + t.Id + "'>Receipt</a>");
                if (t.Type == "Payment" && t.Status == "Completed")
                    sb.Append("<form method='post' action='/admin/payments/refund/" + t.Id + "' class='rowflex'><input class='t' style='width:110px' type='number' name='amount' min='1' max='" + t.Amount + "' value='" + t.Amount + "'><button class='btn small red'>Refund</button></form>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "payments", "Payments", sb.ToString());
        });
    }

    private static void MapGateway(WebApplication app)
    {
        app.MapPost("/admin/payments/gateway", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "payments");
            if (deny != null) return deny;
            AdminUser user = u!;

            var form = await ctx.Request.ReadFormAsync();
            string method = form["method"].ToString();
            if (AdminData.PaymentMethods.Contains(method))
            {
                bool online = !AdminData.IsGatewayOnline(method); // button flips current state
                AdminData.Gateways[method] = online;
                AdminData.Save();
                AdminData.Audit(ctx, "Gateway status", method + " set " + (online ? "online" : "offline") + " by " + user.Username);
            }
            return Results.Redirect("/admin/payments");
        });
    }

    private static void MapRefund(WebApplication app)
    {
        app.MapPost("/admin/payments/refund/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "payments");
            if (deny != null) return deny;
            AdminUser user = u!;

            Transaction? t = AdminData.Transactions.Find(x => x.Id == id);
            if (t == null || t.Type != "Payment" || t.Status != "Completed") return Results.Redirect("/admin/payments");

            var form = await ctx.Request.ReadFormAsync();
            int.TryParse(form["amount"].ToString(), out int amount);
            if (amount < 1) amount = t.Amount;
            if (amount > t.Amount) amount = t.Amount;

            Transaction refund = new()
            {
                Id = AdminData.NextId(AdminData.Transactions, x => x.Id),
                BookingId = t.BookingId,
                Type = "Refund",
                Method = t.Method,
                Amount = amount,
                Date = DateTime.Now,
                Status = "Completed",
                RelatedId = t.Id
            };
            AdminData.Transactions.Add(refund);

            Folio? folio = AdminData.FolioFor(t.BookingId);
            if (folio != null)
                folio.Items.Add(new FolioItem { Id = AdminData.NextItemId(folio), Date = DateTime.Now, Description = amount == t.Amount ? "Refund (full)" : "Refund (partial, " + AdminData.Money(amount) + " of " + AdminData.Money(t.Amount) + ")", Type = "refund", Amount = amount });

            Booking? b = AdminData.Bookings.Find(x => x.Id == t.BookingId);
            if (b != null) b.Status = amount == t.Amount ? "Refunded" : "Partially refunded";

            AdminData.Save();
            AdminData.Audit(ctx, "Refund issued", AdminData.Money(amount) + " refunded on booking #" + t.BookingId + " (" + b?.GuestName + ") via TRX-" + t.Id.ToString("D4"));
            return Results.Redirect("/admin/payments");
        });
    }

    private static void MapDuplicates(WebApplication app)
    {
        app.MapPost("/admin/payments/dupe/dismiss/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "payments");
            if (deny != null) return deny;
            if (!AdminData.DismissedDupes.Contains(id)) AdminData.DismissedDupes.Add(id);
            AdminData.Save();
            AdminData.Audit(ctx, "Duplicate dismissed", "TRX-" + id.ToString("D4") + " marked as not a duplicate");
            return Results.Redirect("/admin/payments");
        });

        app.MapPost("/admin/payments/dupe/void/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "payments");
            if (deny != null) return deny;
            Transaction? t = AdminData.Transactions.Find(x => x.Id == id);
            if (t != null)
            {
                t.Status = "Voided";
                AdminData.Save();
                AdminData.Audit(ctx, "Transaction voided", "TRX-" + id.ToString("D4") + " (" + AdminData.Money(t.Amount) + ") marked void as duplicate");
            }
            return Results.Redirect("/admin/payments");
        });
    }
}
