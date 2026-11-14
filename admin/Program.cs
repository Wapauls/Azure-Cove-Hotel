#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:include Admin/*.cs
// ============================================================================
// Azure Cove Hotel - working copy of the original Program.cs
//
// The ORIGINAL Program.cs (repo root) is left untouched; this copy hosts the
// guest website plus the separate admin console (Admin/*.cs files, wired up
// by the AdminConsole.Map hook below). Guest-side upgrades in this copy:
// check-in dates, seasonal rates, length-of-stay discounts, promo codes,
// taxes, blackout dates, cut-off hours and gateway-aware payment methods.
// ============================================================================
using System.Net;
using System.Text;
 
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseStaticFiles();

var rooms = new List<Room>
{
    new Room { Id = 1, Name = "Standard Room", Details = "Enjoy a comforting and convenience stay in our Standard Room, thoughtfully prepared and designed for a pleasant and relaxing stay. Featuring a comfortable bed, a private bathroom, and essential facilities, it is an excellent choice for solo vacations, couple getaway, and business trips. ",        Price = 1500, MaxGuests = 2, Image = "standard.png", IsAvailable = true, Units = 3, RoomStatus = "Clean" },
    new Room { Id = 2, Name = "Deluxe Room",   Details = "Treat yourself to a pleasant hotel experience in our Deluxe Bay View. With its spacious interior, premium bedding, and breathtaking views inside your room, this room combines relaxation and elegance for guests seeking extra comfort during their stay at our hotel. ",          Price = 2500, MaxGuests = 2, Image = "deluxe.png", IsAvailable = true, Units = 2, RoomStatus = "Clean" },
    new Room { Id = 3, Name = "Family Room",   Details = "Make a ever lasting memories with your loved ones in our Family Suite. Featuring a comfortable set of beds, a vast layout, and a cozy living area, this room provides plenty of space for families to enjoy their well deserved bond together. Perfect for family vacations and even group hang outs. ",       Price = 3500, MaxGuests = 4, Image = "family.png", IsAvailable = true, Units = 2, RoomStatus = "Clean" },
    new Room { Id = 4, Name = "Suite",         Details = "Experience our best suite. The Presidential Suite, featuring a well-furnitured living area, a spacious bedroom, a well lit private bathroom. Designed for guests who appreciates exclusivity, service and comfort, our Presidential Suite offers a truly unforgettable stay.",  Price = 5000, MaxGuests = 6, Image = "suite.png", IsAvailable = true, Units = 1, RoomStatus = "Clean" }
};

var paymentMethods = new List<string> { "Credit Card", "GCash", "PayPal", "Cash at hotel" };
var bookings = new List<Booking>();
var transactions = new List<Transaction>();
var tickets = new List<Ticket>();

// ============================================================================
// ADMIN CONSOLE HOOK - the admin console lives in Admin/*.cs (separate files).
// It shares the lists above with the guest site and adds /admin/* routes,
// JSON persistence, audit logs, accounts and more.
// ============================================================================
AdminConsole.Map(app, rooms, bookings, transactions, tickets, paymentMethods);

string css = """
*{box-sizing:border-box}
body{margin:0;font-family:'Poppins','Segoe UI',Arial,sans-serif;background:#f5f5f5;color:#1a1a1a}
a{color:#0071c2;text-decoration:none}
button,input,select,textarea{font-family:inherit}
.top{background:#003b95}
.bar{max-width:1100px;margin:auto;padding:16px 20px 12px;display:flex;flex-direction:column;align-items:center;gap:8px}
.nav a{color:#fff;margin:0 14px;font-family:'IBM Plex Sans Devanagari',sans-serif;font-size:24px;font-weight:600}
.logo{color:#fff;font-family:'Fleur De Leah',cursive;font-size:60px;font-weight:400;line-height:1}
.nav a{color:#fff;margin:0 18px;font-weight:600}
.hero{background:linear-gradient(rgba(0,20,60,0.55),rgba(0,20,60,0.55)),url('/photo/background  .png') center/cover;color:#fff;padding:45px 0 70px;text-align:center}
.hero h1{font-size:42px;margin:0 0 8px} 
.wrap{max-width:1100px;margin:auto;padding:0 20px}
.wrap h2{text-align:center}
.search{background:#febb02;padding:4px;border-radius:8px;display:flex;gap:4px;margin:28px auto 0;max-width:640px;text-align:left}
.search label{background:#fff;color:#555;flex:1;padding:8px 12px;border-radius:4px;font-size:13px;display:flex;flex-direction:column}
.search input{border:0;font-size:16px;outline:0;margin-top:4px;width:100%;text-align:center}
.btn,.search button{background:#0071c2;color:#fff;border:0;border-radius:4px;padding:12px 22px;font-size:16px;font-weight:600;cursor:pointer;display:inline-block}
.btn.red{background:#c0392b}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:18px;margin-bottom:40px}
.card{background:#fff;border:1px solid #ddd;border-radius:8px;overflow:hidden}
.pic{width:100%;height:160px;object-fit:cover;display:block}
.info{padding:14px}
.info h3{margin:0 0 4px}
.muted{color:#6b6b6b;font-size:14px}
.price{font-size:22px;font-weight:700;margin:10px 0 2px}
.tag{display:inline-block;background:#008009;color:#fff;border-radius:4px;padding:2px 8px;font-size:12px;margin-bottom:6px}
.tag.gray{background:#999}
.box{background:#fff;border:1px solid #ddd;border-radius:8px;padding:22px;margin:30px 0}
.cols{display:grid;grid-template-columns:2fr 1fr;gap:20px}
label.f{display:block;font-weight:600;margin:14px 0 4px}
input.t,select.t,textarea.t{width:100%;padding:10px;border:1px solid #bbb;border-radius:4px;font-size:15px;font-family:inherit}
.pay{display:flex;align-items:center;gap:10px;border:1px solid #ddd;border-radius:6px;padding:10px;margin-top:8px}
.ok{background:#e6f4ea;border:1px solid #008009;border-radius:6px;padding:14px;margin-bottom:16px}
.row{display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid #eee}
.foot{text-align:center;color:#777;padding:30px;font-size:13px}
@media(max-width:700px){.cols{grid-template-columns:1fr}.search{flex-direction:column}}
""";

string H(string text) { return WebUtility.HtmlEncode(text); }

string Money(int amount) { return "PHP " + amount.ToString("N0"); }

IResult Page(string title, string body)
{
    string html = "<!DOCTYPE html><html><head><meta charset='utf-8'>"
        + "<meta name='viewport' content='width=device-width,initial-scale=1'>"
        + "<link rel='icon' type='image/jpeg' href='/photo/azure-icon.jpeg'>"
        + "<link rel='preconnect' href='https://fonts.googleapis.com'><link href='https://fonts.googleapis.com/css2?family=Fleur+De+Leah&family=Poppins:wght@400;600;700&display=swap' rel='stylesheet'>"
        + "<title>" + title + " - Azure Cove Hotel</title><style>" + css + "</style></head><body>"
        + "<div class='top'><div class='bar'><a class='logo' href='/'>Azure Cove Hotel</a>"
        + "<div class='nav'><a href='/'>Stays</a><a href='/mybookings'>My bookings</a><a href='/support'>Customer support</a></div></div></div>"
        + body
        + "<div class='foot'>Azure Cove Hotel - all prices include " + H(AdminData.Settings.TaxName) + "</div></body></html>";

    return Results.Content(html, "text/html; charset=utf-8");
}

IResult Message(string title, string text)
{
    return Page(title, "<div class='wrap'><div class='box'><h2>" + title + "</h2><p>" + text + "</p><a class='btn' href='/'>Back to rooms</a></div></div>");
}

// Booking form with an auto-updating quote: changing the check-in date,
// nights or promo code re-renders the page with a fresh price - there is no
// separate "Update price" button. "Review booking" leads to a confirmation
// prompt before anything is created.
IResult BookingPage(Room room, DateTime checkin, int nights, string promo, Quote quote, string error, string name, string email, string method)
{
    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='cols'><div class='box'><h2>Your details</h2>");
    if (error != "") sb.Append("<div class='ok' style='background:#fdecec;border-color:#c0392b'><b>" + H(error) + "</b></div>");
    sb.Append("<form method='post' action='/book/" + room.Id + "' id='bookform'>");
    sb.Append("<label class='f'>Full name</label><input class='t' name='name' value='" + H(name) + "' required>");
    sb.Append("<label class='f'>Email</label><input class='t' type='email' name='email' value='" + H(email) + "' required>");
    sb.Append("<label class='f'>Check-in date</label><input class='t' type='date' name='checkin' min='" + DateTime.Today.ToString("yyyy-MM-dd") + "' value='" + checkin.ToString("yyyy-MM-dd") + "' required>");
    sb.Append("<label class='f'>Number of nights</label><input class='t' type='number' name='nights' min='1' max='30' value='" + nights + "'>");
    sb.Append("<label class='f'>Promo code (optional)</label><input class='t' name='promo' value='" + H(promo) + "' placeholder='Have a code? Enter it here'>");
    if (promo != "" && quote.OfferName == "") sb.Append("<div class='muted' style='margin-top:4px'>That promo code is not valid for this stay.</div>");
    sb.Append("<label class='f'>How would you like to pay?</label>");

    var methods = AdminData.OnlineMethods();
    if (methods.Count == 0)
    {
        sb.Append("<p class='muted'>No payment methods are currently available. Please check back later.</p>");
    }
    else
    {
        for (int i = 0; i < methods.Count; i++)
        {
            string m = methods[i];
            bool check = m == method || (i == 0 && method == "");
            sb.Append("<label class='pay'><input type='radio' name='method' value='" + H(m) + "'" + (check ? " checked" : "") + "> " + H(m) + "</label>");
        }
    }

    sb.Append("<br><button class='btn' name='action' value='review'>Review booking</button></form></div>");

    sb.Append("<div class='box'><h3>" + H(room.Name) + "</h3><div class='muted'>" + H(room.Details) + "</div><br>");
    sb.Append(BreakdownRows(checkin, nights, quote));
    sb.Append("<p class='muted'>The price updates automatically when you change the check-in date, nights or promo code.</p>");
    sb.Append("<p class='muted'>" + H(AdminData.Settings.CancellationPolicy) + "</p></div></div></div>");
    sb.Append("<script>(function(){var f=document.getElementById('bookform');if(!f)return;var t=null;function u(){var i=document.createElement('input');i.type='hidden';i.name='action';i.value='quote';f.appendChild(i);f.submit();}var fs=['checkin','nights'];fs.forEach(function(n){var e=f.elements[n];if(e)e.addEventListener('change',function(){clearTimeout(t);u();});});var p=f.elements['promo'];if(p)p.addEventListener('input',function(){clearTimeout(t);t=setTimeout(u,700);});f.addEventListener('submit',function(){clearTimeout(t);});})();</script>");

    return Page("Book " + room.Name, sb.ToString());
}

// Parses the shared booking fields (booking form, confirm and pay steps).
(string name, string email, string method, string promo, int nights, DateTime checkin) ParseBookingForm(IFormCollection form)
{
    string name = form["name"].ToString().Trim();
    string email = form["email"].ToString().Trim().ToLower();
    string method = form["method"].ToString();
    string promo = form["promo"].ToString().Trim();
    int.TryParse(form["nights"].ToString(), out int nights);
    if (nights < 1) nights = 1;
    DateTime.TryParse(form["checkin"].ToString(), out DateTime checkin);
    if (checkin == DateTime.MinValue) checkin = DateTime.Today;
    checkin = checkin.Date;
    return (name, email, method, promo, nights, checkin);
}

// Hidden fields that carry a pending booking through the confirm / pay steps.
string BookingHidden(string name, string email, DateTime checkin, int nights, string promo, string method)
{
    return "<input type='hidden' name='name' value='" + H(name) + "'>"
        + "<input type='hidden' name='email' value='" + H(email) + "'>"
        + "<input type='hidden' name='checkin' value='" + checkin.ToString("yyyy-MM-dd") + "'>"
        + "<input type='hidden' name='nights' value='" + nights + "'>"
        + "<input type='hidden' name='promo' value='" + H(promo) + "'>"
        + "<input type='hidden' name='method' value='" + H(method) + "'>";
}

// The price lines shared by the booking form, review and payment pages.
string BreakdownRows(DateTime checkin, int nights, Quote quote)
{
    var sb = new StringBuilder();
    sb.Append("<div class='row'><span>Check-in</span><b>" + AdminData.D(checkin) + " (" + H(AdminData.Settings.CheckInTime) + ")</b></div>");
    sb.Append("<div class='row'><span>Check-out</span><b>" + AdminData.D(checkin.AddDays(nights)) + " (" + H(AdminData.Settings.CheckOutTime) + ")</b></div>");
    foreach (QuoteLine line in quote.Lines)
        sb.Append("<div class='row'><span>" + AdminData.D(line.Date) + "</span><b>" + Money(line.Rate) + "</b></div>");
    sb.Append("<div class='row'><span>Room charges</span><b>" + Money(quote.Subtotal) + "</b></div>");
    if (quote.LosAmount > 0) sb.Append("<div class='row'><span>Length-of-stay discount (" + quote.LosPercent + "%)</span><b>-" + Money(quote.LosAmount) + "</b></div>");
    if (quote.OfferAmount > 0) sb.Append("<div class='row'><span>Promo: " + H(quote.OfferName) + "</span><b>-" + Money(quote.OfferAmount) + "</b></div>");
    sb.Append("<div class='row'><span>" + H(AdminData.Settings.TaxName) + " (" + AdminData.Settings.TaxPercent + "%)</span><b>" + Money(quote.Tax) + "</b></div>");
    sb.Append("<div class='row'><span>Total</span><b>" + Money(quote.Total) + "</b></div>");
    return sb.ToString();
}

// Validates a booking attempt. Returns an error message, or null when all is good.
string? ValidateBooking(Room room, string name, string email, DateTime checkin, int nights, string method, string promo)
{
    if (name == "" || !email.Contains("@")) return "Please fill in your name and a valid e-mail address.";
    if (nights < 1 || nights > 30) return "The number of nights must be between 1 and 30.";
    if (AdminData.OnlineMethods().Find(m => m == method) == null) return "Please choose one of the available payment methods.";
    if (checkin < DateTime.Today) return "The check-in date cannot be in the past.";
    if (checkin == DateTime.Today && DateTime.Now.Hour >= AdminData.Settings.BookingCutoffHour)
        return "Same-day bookings close at " + AdminData.Settings.BookingCutoffHour.ToString("D2") + ":00. Please pick a later check-in date.";
    if (!room.IsAvailable) return "Sorry, the " + room.Name + " is not bookable right now.";
    if (!AdminData.RoomFree(room, checkin, nights)) return "The " + room.Name + " is fully booked or blocked for the chosen dates. Try different dates or another room.";
    if (promo != "" && AdminData.Quote(room, checkin, nights, promo).OfferName == "") return "That promo code is not valid for this stay, or it has expired.";
    return null;
}

// Creates booking + transaction + folio, queues the confirmation e-mail and
// redirects to the guest confirmation page.
IResult FinalizeBooking(Room room, string name, string email, DateTime checkin, int nights, string promo, string method, bool paidNow)
{
    Quote quote = AdminData.Quote(room, checkin, nights, promo);
    Booking booking = AdminReservations.MakeBooking(room, name, email, checkin, nights, "Direct", method, quote, promo);
    Transaction transaction = AdminReservations.MakeTransaction(booking, method, paidNow ? "Completed" : "Due at check-in");
    booking.TransactionId = transaction.Id;
    AdminData.CreateFolio(booking, room, quote);
    AdminData.QueueEmail("booking_confirmation", booking);
    AdminData.Save();
    return Results.Redirect("/confirmation/" + booking.Id);
}

// Confirmation prompt shown before a booking is created.
IResult ReviewPage(Room room, string name, string email, DateTime checkin, int nights, string promo, string method, Quote quote)
{
    string hidden = BookingHidden(name, email, checkin, nights, promo, method);
    string note = method switch
    {
        "Credit Card" => "After you confirm, you will enter your card details on the payment page. Your booking is completed as soon as the card payment is approved.",
        "PayPal" => "After you confirm, you will sign in to PayPal to approve the payment. Your booking is completed as soon as PayPal approves it.",
        "GCash" => "After you confirm, we will e-mail you a GCash payment request for " + Money(quote.Total) + ". Approve it in your GCash app - your booking is completed the moment you do.",
        _ => "No online payment needed. Your booking is confirmed as 'Pay at hotel' - pay at the front desk when you check in."
    };

    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='cols'><div class='box'><h2>Confirm your booking</h2>");
    sb.Append("<p class='muted'>Please review everything below. Nothing is booked yet - that happens when you press the button.</p>");
    sb.Append("<div class='row'><span>Guest</span><b>" + H(name) + "</b></div>");
    sb.Append("<div class='row'><span>Email</span><b>" + H(email) + "</b></div>");
    sb.Append("<div class='row'><span>Room</span><b>" + H(room.Name) + "</b></div>");
    sb.Append(BreakdownRows(checkin, nights, quote));
    sb.Append("<div class='row'><span>Payment method</span><b>" + H(method) + "</b></div>");
    sb.Append("<p class='muted'>" + H(AdminData.Settings.CancellationPolicy) + "</p>");
    sb.Append("<form method='post' action='/book/" + room.Id + "/confirm'>" + hidden + "<button class='btn'>Confirm booking</button></form>");
    sb.Append("<form method='post' action='/book/" + room.Id + "' style='margin-top:8px'>" + hidden + "<input type='hidden' name='action' value='quote'><button class='btn red'>Edit details</button></form>");
    sb.Append("</div><div class='box'><h3>What happens next?</h3><p>" + H(note) + "</p>");
    sb.Append("<p class='muted'>Every booking, payment and cancellation on this site asks for your confirmation first.</p></div></div></div>");
    return Page("Confirm your booking", sb.ToString());
}

// Payment step: card / PayPal credentials, or the GCash e-mail prompt.
IResult PaymentPage(Room room, string name, string email, DateTime checkin, int nights, string promo, string method, Quote quote, string error)
{
    string hidden = BookingHidden(name, email, checkin, nights, promo, method);

    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='cols'><div class='box'>");

    if (method == "GCash")
    {
        sb.Append("<h2>Approve the GCash payment</h2>");
        if (error != "") sb.Append("<div class='ok' style='background:#fdecec;border-color:#c0392b'><b>" + H(error) + "</b></div>");
        sb.Append("<div class='ok'>We have e-mailed a GCash payment request for <b>" + Money(quote.Total) + "</b> to <b>" + H(email) + "</b>.</div>");
        sb.Append("<p>Please check your e-mail, open the payment request and approve it in your GCash app.</p>");
        sb.Append("<p class='muted'>Demo mode: nothing is charged for real - the button below stands for approving the payment request.</p>");
        sb.Append("<form method='post' action='/book/" + room.Id + "/pay'>" + hidden + "<button class='btn'>I have approved the payment</button></form>");
        sb.Append("<form method='post' action='/book/" + room.Id + "' style='margin-top:8px'>" + hidden + "<input type='hidden' name='action' value='quote'><button class='btn red'>Cancel this payment</button></form>");
    }
    else if (method == "Credit Card")
    {
        sb.Append("<h2>Card payment</h2>");
        if (error != "") sb.Append("<div class='ok' style='background:#fdecec;border-color:#c0392b'><b>" + H(error) + "</b></div>");
        sb.Append("<p class='muted'>Demo gateway - no real charge is made and card details are never stored. Amount to pay: <b>" + Money(quote.Total) + "</b></p>");
        sb.Append("<form method='post' action='/book/" + room.Id + "/pay'>" + hidden);
        sb.Append("<label class='f'>Name on card</label><input class='t' name='cardname' required>");
        sb.Append("<label class='f'>Card number</label><input class='t' name='cardnumber' inputmode='numeric' placeholder='4242 4242 4242 4242' required>");
        sb.Append("<label class='f'>Expiry (MM/YY)</label><input class='t' name='cardexp' placeholder='12/28' required>");
        sb.Append("<label class='f'>CVC</label><input class='t' name='cardcvc' inputmode='numeric' placeholder='123' required>");
        sb.Append("<br><button class='btn'>Pay " + Money(quote.Total) + " now</button></form>");
        sb.Append("<form method='post' action='/book/" + room.Id + "' style='margin-top:8px'>" + hidden + "<input type='hidden' name='action' value='quote'><button class='btn red'>Cancel this payment</button></form>");
    }
    else
    {
        sb.Append("<h2>PayPal sign-in</h2>");
        if (error != "") sb.Append("<div class='ok' style='background:#fdecec;border-color:#c0392b'><b>" + H(error) + "</b></div>");
        sb.Append("<p class='muted'>Demo gateway - no real charge is made and credentials are never stored. Amount to pay: <b>" + Money(quote.Total) + "</b></p>");
        sb.Append("<form method='post' action='/book/" + room.Id + "/pay'>" + hidden);
        sb.Append("<label class='f'>PayPal e-mail</label><input class='t' type='email' name='ppemail' required>");
        sb.Append("<label class='f'>PayPal password</label><input class='t' type='password' name='pppass' required>");
        sb.Append("<br><button class='btn'>Pay " + Money(quote.Total) + " with PayPal</button></form>");
        sb.Append("<form method='post' action='/book/" + room.Id + "' style='margin-top:8px'>" + hidden + "<input type='hidden' name='action' value='quote'><button class='btn red'>Cancel this payment</button></form>");
    }

    sb.Append("</div><div class='box'><h3>" + H(room.Name) + "</h3>");
    sb.Append("<div class='row'><span>Guest</span><b>" + H(name) + "</b></div>");
    sb.Append("<div class='row'><span>Email</span><b>" + H(email) + "</b></div>");
    sb.Append(BreakdownRows(checkin, nights, quote));
    sb.Append("<div class='row'><span>Payment method</span><b>" + H(method) + "</b></div>");
    sb.Append("<p class='muted'>" + H(AdminData.Settings.CancellationPolicy) + "</p></div></div></div>");

    return Page("Payment - " + method, sb.ToString());
}

app.MapGet("/", (int? nights, int? guests) =>
{
    int n = nights ?? 1;
    int g = guests ?? 1;
    if (n < 1) n = 1;
    if (g < 1) g = 1;

    var sb = new StringBuilder();

    sb.Append("<div class='hero'><div class='wrap'><h1>Find your next stay</h1>");
    sb.Append("<div>Search deals on rooms at Azure Cove Hotel</div>");
    sb.Append("<form class='search' action='/' method='get'>");
    sb.Append("<label>Nights<input type='number' name='nights' min='1' max='30' value='" + n + "'></label>");
    sb.Append("<label>Guests<input type='number' name='guests' min='1' max='6' value='" + g + "'></label>");
    sb.Append("<button>Search</button></form></div></div>");

    sb.Append("<div class='wrap'><h2>Rooms for " + g + " guest(s), " + n + " night(s)</h2><div class='grid'>");

    int shown = 0;
    foreach (Room room in rooms)
    {
        if (room.MaxGuests < g) continue;
        shown++;

        sb.Append("<div class='card'><img class='pic' src='" + room.Image + "' alt='" + room.Name + "'><div class='info'>");
        sb.Append("<h3>" + room.Name + "</h3><div class='muted'>" + room.Details + "</div>");
        sb.Append("<div class='muted'>Sleeps up to " + room.MaxGuests + "</div>");
        sb.Append("<div class='price'>" + Money(AdminData.Quote(room, DateTime.Today, n, null).Total) + "</div>");
        sb.Append("<div class='muted'>for " + n + " night(s), " + H(AdminData.Settings.TaxName) + " included</div><br>");

        string label = AdminData.AvailabilityLabel(room, DateTime.Today, n);
        if (label == "Available")
        {
            sb.Append("<span class='tag'>Available</span><br>");
            sb.Append("<a class='btn' href='/book/" + room.Id + "?nights=" + n + "'>Reserve</a>");
        }
        else
        {
            sb.Append("<span class='tag gray'>" + H(label) + "</span>");
        }

        sb.Append("</div></div>");
    }

    if (shown == 0)
    {
        sb.Append("<p>No rooms fit that many guests. Try fewer guests.</p>");
    }

    sb.Append("</div></div>");
    return Page("Find your next stay", sb.ToString());
});


app.MapGet("/book/{id}", (int id, int? nights, DateTime? checkin, string? promo) =>
{
    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");
    if (!room.IsAvailable) return Message("Sold out", "Sorry, the " + room.Name + " is already booked.");

    int n = nights ?? 1;
    if (n < 1) n = 1;
    DateTime ci = (checkin ?? DateTime.Today).Date;

    Quote quote = AdminData.Quote(room, ci, n, promo ?? "");
    return BookingPage(room, ci, n, promo ?? "", quote, "", "", "", "");
});


app.MapPost("/book/{id}", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");

    var f = ParseBookingForm(form);
    string action = form["action"].ToString();

    // auto price update: the page script re-submits with action=quote whenever
    // the check-in date, nights or promo code change - no separate button
    if (action == "quote")
    {
        Quote q = AdminData.Quote(room, f.checkin, f.nights, f.promo);
        return BookingPage(room, f.checkin, f.nights, f.promo, q, "", f.name, f.email, f.method);
    }

    // review: validate, then show the confirmation prompt
    string? err = ValidateBooking(room, f.name, f.email, f.checkin, f.nights, f.method, f.promo);
    if (err != null)
    {
        Quote q = AdminData.Quote(room, f.checkin, f.nights, f.promo);
        return BookingPage(room, f.checkin, f.nights, f.promo, q, err, f.name, f.email, f.method);
    }

    Quote quote = AdminData.Quote(room, f.checkin, f.nights, f.promo);
    return ReviewPage(room, f.name, f.email, f.checkin, f.nights, f.promo, f.method, quote);
});

// Confirmation prompt: nothing is created until the guest confirms here.
app.MapPost("/book/{id}/confirm", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");

    var f = ParseBookingForm(form);
    string? err = ValidateBooking(room, f.name, f.email, f.checkin, f.nights, f.method, f.promo);
    if (err != null) return Message("Check your details", err);

    Quote quote = AdminData.Quote(room, f.checkin, f.nights, f.promo);

    if (f.method == "Cash at hotel")
        return FinalizeBooking(room, f.name, f.email, f.checkin, f.nights, f.promo, f.method, paidNow: false);

    if (f.method == "GCash" || f.method == "Credit Card" || f.method == "PayPal")
        return PaymentPage(room, f.name, f.email, f.checkin, f.nights, f.promo, f.method, quote, "");

    return Message("Payment method", "That payment method is not available.");
});

// Final step: Credit Card / PayPal credentials, or the GCash approval.
app.MapPost("/book/{id}/pay", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");

    var f = ParseBookingForm(form);
    string? err = ValidateBooking(room, f.name, f.email, f.checkin, f.nights, f.method, f.promo);
    if (err != null) return Message("Check your details", err);

    if (f.method != "GCash" && f.method != "Credit Card" && f.method != "PayPal")
        return Message("Payment method", "That payment method is not available.");

    string payError = "";
    if (f.method == "Credit Card")
    {
        string cardName = form["cardname"].ToString().Trim();
        string digits = new string(form["cardnumber"].ToString().Where(char.IsDigit).ToArray());
        string cvc = new string(form["cardcvc"].ToString().Where(char.IsDigit).ToArray());
        string exp = form["cardexp"].ToString().Trim();
        bool expOk = false;
        string[] expParts = exp.Split('/');
        if (expParts.Length == 2 && int.TryParse(expParts[0], out int mm) && int.TryParse(expParts[1], out int yy)
            && mm >= 1 && mm <= 12 && yy >= 0 && yy <= 99)
        {
            DateTime e = new DateTime(2000 + yy, mm, 1);
            expOk = e >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }

        if (cardName == "") payError = "Enter the name on the card.";
        else if (digits.Length < 13 || digits.Length > 19) payError = "Enter a valid card number (13-19 digits).";
        else if (!expOk) payError = "Enter a valid future expiry date as MM/YY.";
        else if (cvc.Length < 3 || cvc.Length > 4) payError = "Enter a valid CVC (3 or 4 digits).";
    }
    else if (f.method == "PayPal")
    {
        string ppEmail = form["ppemail"].ToString().Trim();
        string ppPass = form["pppass"].ToString();
        if (!ppEmail.Contains("@")) payError = "Enter your PayPal e-mail address.";
        else if (ppPass.Length < 6) payError = "Enter your PayPal password (at least 6 characters).";
    }

    if (payError != "")
    {
        Quote q = AdminData.Quote(room, f.checkin, f.nights, f.promo);
        return PaymentPage(room, f.name, f.email, f.checkin, f.nights, f.promo, f.method, q, payError);
    }

    // card / PayPal approved, or the guest pressed "I have approved the
    // payment" on the GCash prompt - the payment is done
    return FinalizeBooking(room, f.name, f.email, f.checkin, f.nights, f.promo, f.method, paidNow: true);
});

app.MapGet("/confirmation/{id}", (int id) =>
{
    Booking? b = bookings.Find(x => x.Id == id);
    if (b == null) return Message("Not found", "We could not find that booking.");

    Room room = rooms.Find(r => r.Id == b.RoomId)!;

    string body = "<div class='wrap'><div class='box'>"
        + "<div class='ok'><b>Your booking is confirmed, " + H(b.GuestName) + "!</b><br>Status: " + b.Status + "</div>"
        + "<div class='row'><span>Booking number</span><b>#" + b.Id + "</b></div>"
        + "<div class='row'><span>Room</span><b>" + room.Name + "</b></div>"
        + "<div class='row'><span>Check-in</span><b>" + AdminData.D(b.CheckIn) + " (" + H(AdminData.Settings.CheckInTime) + ")</b></div>"
        + "<div class='row'><span>Check-out</span><b>" + AdminData.D(b.CheckOut) + " (" + H(AdminData.Settings.CheckOutTime) + ")</b></div>"
        + "<div class='row'><span>Nights</span><b>" + b.Nights + "</b></div>"
        + "<div class='row'><span>Room charges</span><b>" + Money(b.Subtotal) + "</b></div>"
        + (b.Discount > 0 ? "<div class='row'><span>Discounts</span><b>-" + Money(b.Discount) + "</b></div>" : "")
        + "<div class='row'><span>" + H(AdminData.Settings.TaxName) + " (" + AdminData.Settings.TaxPercent + "%)</span><b>" + Money(b.Tax) + "</b></div>"
        + "<div class='row'><span>Payment method</span><b>" + b.PaymentMethod + "</b></div>"
        + "<div class='row'><span>Transaction number</span><b>#" + b.TransactionId + "</b></div>"
        + "<div class='row'><span>Total</span><b>" + Money(b.Total) + "</b></div><br>"
        + "<a class='btn' href='/mybookings?email=" + WebUtility.UrlEncode(b.Email) + "'>View my bookings</a></div></div>";

    return Page("Booking confirmed", body);
});


app.MapGet("/mybookings", (string? email) =>
{
    string mail = (email ?? "").Trim().ToLower();

    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='box'><h2>My bookings</h2>");
    sb.Append("<form method='get' action='/mybookings'><label class='f'>Enter your email</label>");
    sb.Append("<input class='t' type='email' name='email' value='" + H(mail) + "' required><br><br>");
    sb.Append("<button class='btn'>Find my bookings</button></form></div>");

    if (mail != "")
    {
        int found = 0;

        foreach (Booking b in bookings)
        {
            if (b.Email != mail) continue;
            found++;

            Room room = rooms.Find(r => r.Id == b.RoomId)!;

            sb.Append("<div class='box'><h3>#" + b.Id + " - " + room.Name + "</h3>");
            sb.Append("<div class='row'><span>Check-in</span><b>" + AdminData.D(b.CheckIn) + "</b></div>");
            sb.Append("<div class='row'><span>Check-out</span><b>" + AdminData.D(b.CheckOut) + "</b></div>");
            sb.Append("<div class='row'><span>Nights</span><b>" + b.Nights + "</b></div>");
            sb.Append("<div class='row'><span>Total</span><b>" + Money(b.Total) + "</b></div>");
            sb.Append("<div class='row'><span>Paid with</span><b>" + b.PaymentMethod + "</b></div>");
            sb.Append("<div class='row'><span>Status</span><b>" + b.Status + "</b></div><br>");

            if (b.Status == "Paid" || b.Status == "Pay at hotel")
            {
                string label = b.Status == "Paid" ? "Cancel and refund" : "Cancel booking";
                sb.Append("<form method='post' action='/cancel/" + b.Id + "'>");
                sb.Append("<input type='hidden' name='email' value='" + H(mail) + "'>");
                sb.Append("<button class='btn red'>" + label + "</button></form>");
            }

            sb.Append("</div>");
        }

        if (found == 0)
        {
            sb.Append("<div class='box'>No bookings found for that email.</div>");
        }
    }

    sb.Append("</div>");
    return Page("My bookings", sb.ToString());
});

app.MapPost("/cancel/{id}", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string mail = form["email"].ToString().Trim().ToLower();

    Booking? b = bookings.Find(x => x.Id == id && x.Email == mail);
    if (b == null) return Message("Not found", "We could not find that booking.");

    if (b.Status != "Paid" && b.Status != "Pay at hotel")
        return Message("Cannot cancel", "This booking is already " + b.Status + ".");

    Room room = rooms.Find(r => r.Id == b.RoomId)!;

    // confirmation prompt before the cancellation (and any refund) happens
    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='box'><h2>Cancel booking #" + b.Id + "?</h2>");
    sb.Append("<p class='muted'>Please confirm - this cannot be undone.</p>");
    sb.Append("<div class='row'><span>Room</span><b>" + H(room.Name) + "</b></div>");
    sb.Append("<div class='row'><span>Check-in</span><b>" + AdminData.D(b.CheckIn) + "</b></div>");
    sb.Append("<div class='row'><span>Check-out</span><b>" + AdminData.D(b.CheckOut) + "</b></div>");
    sb.Append("<div class='row'><span>Nights</span><b>" + b.Nights + "</b></div>");
    sb.Append("<div class='row'><span>Total</span><b>" + Money(b.Total) + "</b></div>");
    sb.Append("<div class='row'><span>Paid with</span><b>" + H(b.PaymentMethod) + "</b></div>");

    if (b.Status == "Paid")
    {
        (int refundAmount, bool full) = AdminData.RefundableFor(b);
        if (refundAmount <= 0)
            sb.Append("<div class='ok' style='background:#fdecec;border-color:#c0392b'><b>No refund:</b> the free cancellation window has passed and the policy fee covers the whole amount. The booking will simply be cancelled.</div>");
        else if (full)
            sb.Append("<div class='ok'><b>Full refund:</b> " + Money(refundAmount) + " goes back to your " + H(b.PaymentMethod) + ".</div>");
        else
            sb.Append("<div class='ok' style='background:#fff3d6;border-color:#946200'><b>Refund:</b> " + Money(refundAmount) + " of " + Money(b.Total) + " goes back to your " + H(b.PaymentMethod) + " (cancellation policy applies).</div>");
    }
    else
    {
        sb.Append("<div class='ok'>No payment has been taken - the booking will simply be cancelled.</div>");
    }

    sb.Append("<form method='post' action='/cancel/" + b.Id + "/confirm'><input type='hidden' name='email' value='" + H(mail) + "'><button class='btn red'>Yes, cancel my booking</button></form>");
    sb.Append("<br><a class='btn' href='/mybookings?email=" + WebUtility.UrlEncode(mail) + "'>No, keep my booking</a>");
    sb.Append("</div></div>");
    return Page("Cancel booking", sb.ToString());
});

// The actual cancellation (and refund), only after the guest confirmed above.
app.MapPost("/cancel/{id}/confirm", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string mail = form["email"].ToString().Trim().ToLower();

    Booking? b = bookings.Find(x => x.Id == id && x.Email == mail);
    if (b == null) return Message("Not found", "We could not find that booking.");

    if (b.Status == "Paid")
    {
        (int refundAmount, bool full) = AdminData.RefundableFor(b);
        if (refundAmount > 0)
        {
            Transaction refund = new Transaction();
            refund.Id = transactions.Count == 0 ? 1 : transactions.Max(x => x.Id) + 1;
            refund.BookingId = b.Id;
            refund.Type = "Refund";
            refund.Method = b.PaymentMethod;
            refund.Amount = refundAmount;
            refund.Date = DateTime.Now;
            refund.Status = "Completed";
            refund.RelatedId = b.TransactionId;
            transactions.Add(refund);

            Folio? folio = AdminData.FolioFor(b.Id);
            if (folio != null)
                folio.Items.Add(new FolioItem { Id = AdminData.NextItemId(folio), Date = DateTime.Now, Description = full ? "Refund (guest cancelled, free window)" : "Partial refund (cancellation fee applied)", Type = "refund", Amount = refundAmount });

            b.Status = "Refunded";
        }
        else
        {
            b.Status = "Cancelled";
        }

        AdminData.QueueEmail("cancellation", b);
        AdminData.Save();
    }
    else if (b.Status == "Pay at hotel")
    {
        b.Status = "Cancelled";
        AdminData.QueueEmail("cancellation", b);
        AdminData.Save();
    }
    else
    {
        return Message("Cannot cancel", "This booking is already " + b.Status + ".");
    }

    return Results.Redirect("/mybookings?email=" + WebUtility.UrlEncode(mail));
});


app.MapGet("/support", () =>
{
    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='cols'><div class='box'><h2>How can we help?</h2>");
    sb.Append("<form method='post' action='/support'>");
    sb.Append("<label class='f'>Your name</label><input class='t' name='name' required>");
    sb.Append("<label class='f'>Email</label><input class='t' type='email' name='email' required>");
    sb.Append("<label class='f'>Topic</label><select class='t' name='topic'>");
    sb.Append("<option>Booking question</option><option>Payment problem</option><option>Refund</option><option>Other</option></select>");
    sb.Append("<label class='f'>Message</label><textarea class='t' name='message' rows='5' required></textarea><br><br>");
    sb.Append("<button class='btn'>Send message</button></form></div>");
    sb.Append("<div class='box'><h3>Quick answers</h3>");
    sb.Append("<p><b>How do I get a refund?</b><br><span class='muted'>Go to My bookings and press Cancel and refund. " + H(AdminData.Settings.CancellationPolicy) + "</span></p>");
    sb.Append("<p><b>Which payments do you accept?</b><br><span class='muted'>" + H(string.Join(", ", AdminData.OnlineMethods())) + ".</span></p></div>");
    sb.Append("</div></div>");
    return Page("Customer support", sb.ToString());
});

app.MapPost("/support", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    Ticket ticket = new Ticket();
    ticket.Id = tickets.Count + 1;
    ticket.Name = form["name"].ToString().Trim();
    ticket.Email = form["email"].ToString().Trim();
    ticket.Topic = form["topic"].ToString();
    ticket.Message = form["message"].ToString().Trim();
    tickets.Add(ticket);

    AdminData.Save();

    return Message("Message received", "Thank you, " + H(ticket.Name) + ". Your support ticket is #" + ticket.Id + ". We will reply to " + H(ticket.Email) + " soon.");
});

// NOTE: the old inline staff "/admin" page from the original Program.cs was
// replaced by the separate admin console (Admin/*.cs). See /admin.

app.MapGet("/photo/{name}", (string name) =>
{
    string file = Path.GetFileName(name);

    string root = app.Environment.ContentRootPath;
    string[] folders =
    {
        Path.Combine(root, "wwwroot"),
        root,
        Path.GetFullPath(Path.Combine(root, "..")), // repo root, photos next to the original Program.cs
        AppContext.BaseDirectory
    };

    foreach (string folder in folders)
    {
        string path = Path.Combine(folder, file);
        if (File.Exists(path))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            string mime = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "image/png";
            return Results.File(path, mime);
        }
    }

    return Results.Text("Photo not found: " + file + "\nThe site looked in:\n" + string.Join("\n", folders), "text/plain");
});

app.Run();


public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Details { get; set; } = "";
    public string Image { get; set; } = "";
    public int Price { get; set; }
    public int MaxGuests { get; set; }
    public bool IsAvailable { get; set; }
    public int Units { get; set; } = 1;              // inventory count for this room type
    public string RoomStatus { get; set; } = "Clean"; // Clean | Dirty | Maintenance | Out of Order
}

public class Booking
{
    public int Id { get; set; }
    public string GuestName { get; set; } = "";
    public string Email { get; set; } = "";
    public int RoomId { get; set; }
    public int Nights { get; set; }
    public int Total { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public int TransactionId { get; set; }
    public DateTime CheckIn { get; set; }            // set at booking time
    public DateTime CheckOut { get; set; }
    public int Subtotal { get; set; }                // room charges before discounts/tax
    public int Discount { get; set; }                // LOS + promo
    public int Tax { get; set; }                     // tax included in Total
    public string Source { get; set; } = "Direct";   // Direct | OTA | Walk-in
    public string PromoCode { get; set; } = "";
}

public class Transaction
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string Type { get; set; } = "";
    public string Method { get; set; } = "";
    public int Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTime Date { get; set; }
    public int RelatedId { get; set; }               // refunds point at the original payment
}

public class Ticket
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Message { get; set; } = "";
}
