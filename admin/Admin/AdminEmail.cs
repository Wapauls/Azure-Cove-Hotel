// Admin/AdminEmail.cs
// E-mail templates (booking confirmation, cancellation, check-in reminder)
// with placeholders and live preview, plus the demo outbox.
// Note: sending is simulated (queued to the outbox); hooking up SMTP is a later step.
using System.Text;

public static class AdminEmail
{
    public static void Map(WebApplication app)
    {
        MapList(app);
        MapEdit(app);
        MapPreview(app);
    }

    private static void MapList(WebApplication app)
    {
        app.MapGet("/admin/emails", (HttpContext ctx) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "emails");
            if (deny != null) return deny;
            AdminUser user = u!;

            var sb = new StringBuilder();
            sb.Append("<div class='acard'><h2>Templates</h2>");
            sb.Append("<table class='atable'><tr><th>Template</th><th>Subject</th><th></th></tr>");
            foreach (EmailTemplate t in AdminData.Templates)
            {
                sb.Append("<tr><td><b>" + AdminData.H(t.Name) + "</b><br><span class='muted'>key: " + AdminData.H(t.Key) + "</span></td><td>" + AdminData.H(t.Subject) + "</td>");
                sb.Append("<td class='rowflex'><a class='btn small gray' href='/admin/emails/edit/" + t.Id + "'>Edit</a><a class='btn small gray' href='/admin/emails/preview/" + t.Id + "'>Preview</a></td></tr>");
            }
            sb.Append("</table></div>");

            sb.Append("<div class='acard'><h3>Placeholders</h3><p class='muted'>{{hotel_name}}, {{guest_name}}, {{booking_id}}, {{room}}, {{checkin}}, {{checkout}}, {{checkin_time}}, {{checkout_time}}, {{nights}}, {{total}}, {{status}}, {{contact}}</p></div>");

            sb.Append("<div class='acard'><h3>Outbox (demo mode)</h3><p class='muted'>Sending is simulated: e-mails are rendered and stored here when guests book or cancel, or when staff send a reminder. Real sending needs SMTP credentials.</p>");
            sb.Append("<table class='atable'><tr><th>#</th><th>Created</th><th>Template</th><th>To</th><th>Subject</th><th></th></tr>");
            foreach (OutboxEmail e in AdminData.Outbox.OrderByDescending(x => x.Id).Take(30))
            {
                sb.Append("<tr><td>" + e.Id + "</td><td class='nowrap'>" + e.Created.ToString("MMM d, HH:mm") + "</td><td>" + AdminData.H(e.Template) + "</td><td>" + AdminData.H(e.To) + "</td><td>" + AdminData.H(e.Subject) + "</td>");
                sb.Append("<td><details><summary class='btn small gray'>Body</summary><pre style='white-space:pre-wrap;background:var(--thbg);border:1px solid var(--border);color:var(--text);border-radius:6px;padding:10px;margin-top:8px'>" + AdminData.H(e.Body) + "</pre></details></td></tr>");
            }
            if (AdminData.Outbox.Count == 0) sb.Append("<tr><td colspan='6' class='muted'>Outbox is empty.</td></tr>");
            sb.Append("</table></div>");

            return AdminConsole.Page(user, "emails", "Email Templates", sb.ToString());
        });
    }

    private static void MapEdit(WebApplication app)
    {
        app.MapGet("/admin/emails/edit/{id}", (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "emails");
            if (deny != null) return deny;
            AdminUser user = u!;

            EmailTemplate? t = AdminData.Templates.Find(x => x.Id == id);
            if (t == null) return AdminConsole.Page(user, "emails", "Template", "<div class='alert bad'>Template not found.</div><a class='btn' href='/admin/emails'>Back</a>");

            var sb = new StringBuilder();
            sb.Append("<div class='acard'><h2>Edit template: " + AdminData.H(t.Name) + "</h2>");
            sb.Append("<form method='post' action='/admin/emails/edit/" + t.Id + "'>");
            sb.Append("<label class='f'>Subject</label><input class='t' name='subject' value='" + AdminData.H(t.Subject) + "' required>");
            sb.Append("<label class='f'>Body (plain text)</label><textarea class='t' name='body' rows='12'>" + AdminData.H(t.Body) + "</textarea>");
            sb.Append("<br><button class='btn'>Save template</button> <a class='btn gray' href='/admin/emails'>Cancel</a> <a class='btn gray' href='/admin/emails/preview/" + t.Id + "'>Preview</a></form></div>");

            return AdminConsole.Page(user, "emails", "Edit template", sb.ToString());
        });

        app.MapPost("/admin/emails/edit/{id}", async (HttpContext ctx, int id) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "emails");
            if (deny != null) return deny;
            AdminUser user = u!;

            EmailTemplate? t = AdminData.Templates.Find(x => x.Id == id);
            if (t != null)
            {
                var form = await ctx.Request.ReadFormAsync();
                t.Subject = form["subject"].ToString().Trim();
                t.Body = form["body"].ToString();
                AdminData.Save();
                AdminData.Audit(ctx, "Email template saved", t.Name + " by " + user.Username);
            }
            return Results.Redirect("/admin/emails");
        });
    }

    private static void MapPreview(WebApplication app)
    {
        app.MapGet("/admin/emails/preview/{id}", (HttpContext ctx, int id, int? booking) =>
        {
            var (u, deny) = AdminAuth.Check(ctx, "emails");
            if (deny != null) return deny;
            AdminUser user = u!;

            EmailTemplate? t = AdminData.Templates.Find(x => x.Id == id);
            if (t == null) return AdminConsole.Page(user, "emails", "Preview", "<div class='alert bad'>Template not found.</div><a class='btn' href='/admin/emails'>Back</a>");

            // pick a booking to render against (newest by default, or ?booking=id)
            Booking? b = booking != null && booking > 0
                ? AdminData.Bookings.Find(x => x.Id == booking)
                : AdminData.Bookings.Count == 0 ? null : AdminData.Bookings[AdminData.Bookings.Count - 1];

            var sb = new StringBuilder();
            sb.Append("<div class='rowflex no-print' style='margin-bottom:14px'><a class='btn gray' href='/admin/emails'>Back to templates</a></div>");

            if (b == null)
            {
                sb.Append("<div class='alert bad'>No bookings exist yet - make a booking on the guest site first, then preview against it.</div>");
            }
            else
            {
                sb.Append("<div class='acard'><h3>Rendered for booking #" + b.Id + " (" + AdminData.H(b.GuestName) + ")</h3>");
                sb.Append("<form method='get' action='/admin/emails/preview/" + t.Id + "' class='rowflex'><select class='t' name='booking' style='width:auto'>");
                foreach (Booking o in AdminData.Bookings.OrderByDescending(x => x.Id).Take(30))
                    sb.Append("<option value='" + o.Id + "'" + (o.Id == b.Id ? " selected" : "") + ">#" + o.Id + " - " + AdminData.H(o.GuestName) + "</option>");
                sb.Append("</select><button class='btn small'>Switch booking</button></form></div>");

                sb.Append("<div class='paper'><b>Subject:</b> " + AdminData.H(AdminData.Fill(t.Subject, b)) + "<br><br><hr style='border:0;border-top:1px solid #e3e8f0'><br>");
                sb.Append("<pre style='white-space:pre-wrap;font-family:inherit'>" + AdminData.H(AdminData.Fill(t.Body, b)) + "</pre></div>");
            }

            return AdminConsole.Page(user, "emails", "Preview: " + t.Name, sb.ToString());
        });
    }
}
