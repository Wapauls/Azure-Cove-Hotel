// Admin/AdminSettings.cs
// System settings: hotel identity, taxes, currency, booking cut-off hours,
// cancellation policy and check-in / check-out times.
using System.Text;

public static class AdminSettings
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/settings", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "settings");
            if (deny != null) return deny;
            AdminUser user = u!;
            SystemSettings s = AdminData.Settings;

            var sb = new StringBuilder();
            sb.Append("<div class='acard'><h2>Hotel &amp; booking settings</h2><form method='post' action='/admin/settings'>");

            sb.Append("<div class='cols'><div>");
            sb.Append("<h3>Identity (used on invoices, receipts and e-mails)</h3>");
            sb.Append("<label class='f'>Hotel name</label><input class='t' name='hotelname' value='" + AdminData.H(s.HotelName) + "' required>");
            sb.Append("<label class='f'>Address</label><input class='t' name='address' value='" + AdminData.H(s.Address) + "'>");
            sb.Append("<label class='f'>Contact e-mail</label><input class='t' name='contactemail' value='" + AdminData.H(s.ContactEmail) + "'>");
            sb.Append("<label class='f'>Contact phone</label><input class='t' name='contactphone' value='" + AdminData.H(s.ContactPhone) + "'>");

            sb.Append("<h3>Money &amp; tax</h3>");
            sb.Append("<label class='f'>Currency code (reports)</label><input class='t' name='currencycode' value='" + AdminData.H(s.CurrencyCode) + "'>");
            sb.Append("<label class='f'>Currency symbol (prices, e.g. 'PHP ')</label><input class='t' name='currencysymbol' value='" + AdminData.H(s.CurrencySymbol) + "'>");
            sb.Append("<label class='f'>Tax name</label><input class='t' name='taxname' value='" + AdminData.H(s.TaxName) + "'>");
            sb.Append("<label class='f'>Tax percent (0-50)</label><input class='t' type='number' name='taxpercent' min='0' max='50' value='" + s.TaxPercent + "'>");
            sb.Append("</div><div>");

            sb.Append("<h3>Booking rules</h3>");
            sb.Append("<label class='f'>Booking cut-off hour (0-23; same-day bookings blocked after this hour)</label><input class='t' type='number' name='cutoff' min='0' max='23' value='" + s.BookingCutoffHour + "'>");
            sb.Append("<label class='f'>Check-in time</label><input class='t' name='checkintime' value='" + AdminData.H(s.CheckInTime) + "'>");
            sb.Append("<label class='f'>Check-out time</label><input class='t' name='checkouttime' value='" + AdminData.H(s.CheckOutTime) + "'>");
            sb.Append("<label class='f'>Free cancellation window (hours before check-in)</label><input class='t' type='number' name='freecancel' min='0' max='720' value='" + s.FreeCancelHours + "'>");
            sb.Append("<label class='f'>Cancellation fee percent when cancelling late (0-100)</label><input class='t' type='number' name='cancelfee' min='0' max='100' value='" + s.CancelFeePercent + "'>");
            sb.Append("<label class='f'>Cancellation policy text (shown on booking page, invoices, receipts)</label><textarea class='t' name='policy' rows='4'>" + AdminData.H(s.CancellationPolicy) + "</textarea>");
            sb.Append("</div></div>");

            sb.Append("<br><button class='btn'>Save settings</button></form></div>");
            sb.Append("<p class='muted'>Changes apply immediately to the guest site: pricing and tax on the booking form, the cut-off rule, the policy text and payment method availability (via <a href='/admin/payments'>gateway status</a>).</p>");

            return AdminConsole.Page(user, "settings", "System Settings", sb.ToString());
        });

        app.MapPost("/admin/settings", async (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "settings");
            if (deny != null) return deny;
            AdminUser user = u!;
            SystemSettings s = AdminData.Settings;

            var form = await ctx.Request.ReadFormAsync();
            string hotelName = form["hotelname"].ToString().Trim();
            int.TryParse(form["taxpercent"].ToString(), out int taxPercent);
            int.TryParse(form["cutoff"].ToString(), out int cutoff);
            int.TryParse(form["freecancel"].ToString(), out int freecancel);
            int.TryParse(form["cancelfee"].ToString(), out int cancelfee);

            if (hotelName != "")
            {
                s.HotelName = hotelName;
                s.Address = form["address"].ToString().Trim();
                s.ContactEmail = form["contactemail"].ToString().Trim();
                s.ContactPhone = form["contactphone"].ToString().Trim();
                s.CurrencyCode = form["currencycode"].ToString().Trim();
                s.CurrencySymbol = form["currencysymbol"].ToString();
                s.TaxName = form["taxname"].ToString().Trim();
                s.TaxPercent = Math.Clamp(taxPercent, 0, 50);
                s.BookingCutoffHour = Math.Clamp(cutoff, 0, 23);
                s.CheckInTime = form["checkintime"].ToString().Trim();
                s.CheckOutTime = form["checkouttime"].ToString().Trim();
                s.FreeCancelHours = Math.Clamp(freecancel, 0, 720);
                s.CancelFeePercent = Math.Clamp(cancelfee, 0, 100);
                s.CancellationPolicy = form["policy"].ToString().Trim();
                AdminData.Save();
                AdminData.Audit(ctx, "Settings saved", "system settings updated by " + user.Username);
            }
            return Results.Redirect("/admin/settings");
        });
    }
}
