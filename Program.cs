using System.Net;
using System.Text;
 
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseStaticFiles();

var rooms = new List<Room>
{
    new Room { Id = 1, Name = "Standard Room", Details = "Enjoy a comforting and convenience stay in our Standard Room, thoughtfully prepared and designed for a pleasant and relaxing stay. Featuring a comfortable bed, a private bathroom, and essential facilities, it is an excellent choice for solo vacations, couple getaway, and business trips. ",        Price = 1500, MaxGuests = 2, Image = "standard.png", IsAvailable = true },
    new Room { Id = 2, Name = "Deluxe Room",   Details = "Treat yourself to a pleasant hotel experience in our Deluxe Bay View. With its spacious interior, premium bedding, and breathtaking views inside your room, this room combines relaxation and elegance for guests seeking extra comfort during their stay at our hotel. ",          Price = 2500, MaxGuests = 2, Image = "deluxe.png", IsAvailable = true },
    new Room { Id = 3, Name = "Family Room",   Details = "Make a ever lasting memories with your loved ones in our Family Suite. Featuring a comfortable set of beds, a vast layout, and a cozy living area, this room provides plenty of space for families to enjoy their well deserved bond together. Perfect for family vacations and even group hang outs. ",       Price = 3500, MaxGuests = 4, Image = "family.png", IsAvailable = true },
    new Room { Id = 4, Name = "Suite",         Details = "Experience our best suite. The Presidential Suite, featuring a well-furnitured living area, a spacious bedroom, a well lit private bathroom. Designed for guests who appreciates exclusivity, service and comfort, our Presidential Suite offers a truly unforgettable stay.",  Price = 5000, MaxGuests = 6, Image = "suite.png", IsAvailable = true }
};

var paymentMethods = new List<string> { "Credit Card", "GCash", "PayPal", "Cash at hotel" };
var bookings = new List<Booking>();
var transactions = new List<Transaction>();
var tickets = new List<Ticket>();

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
        + "<link rel='icon' type='image/png' href='/photo/logo.png'>"
        + "<link rel='preconnect' href='https://fonts.googleapis.com'><link href='https://fonts.googleapis.com/css2?family=Fleur+De+Leah&family=Poppins:wght@400;600;700&display=swap' rel='stylesheet'>"
        + "<title>" + title + " - Azure Cove Hotel</title><style>" + css + "</style></head><body>"
        + "<div class='top'><div class='bar'><a class='logo' href='/'>Azure Cove Hotel</a>"
        + "<div class='nav'><a href='/'>Stays</a><a href='/mybookings'>My bookings</a><a href='/support'>Customer support</a></div></div></div>"
        + body
        + "<div class='foot'>Azure Cove Hotel - all prices in Philippine pesos (PHP)</div></body></html>";

    return Results.Content(html, "text/html; charset=utf-8");
}

IResult Message(string title, string text)
{
    return Page(title, "<div class='wrap'><div class='box'><h2>" + title + "</h2><p>" + text + "</p><a class='btn' href='/'>Back to rooms</a></div></div>");
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
        sb.Append("<div class='price'>" + Money(room.Price * n) + "</div>");
        sb.Append("<div class='muted'>" + Money(room.Price) + " per night</div><br>");

        if (room.IsAvailable)
        {
            sb.Append("<span class='tag'>Available</span><br>");
            sb.Append("<a class='btn' href='/book/" + room.Id + "?nights=" + n + "'>Reserve</a>");
        }
        else
        {
            sb.Append("<span class='tag gray'>Sold out</span>");
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


app.MapGet("/book/{id}", (int id, int? nights) =>
{
    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");
    if (!room.IsAvailable) return Message("Sold out", "Sorry, the " + room.Name + " is already booked.");

    int n = nights ?? 1;
    if (n < 1) n = 1;

    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='cols'><div class='box'><h2>Your details</h2>");
    sb.Append("<form method='post' action='/book/" + id + "'>");
    sb.Append("<label class='f'>Full name</label><input class='t' name='name' required>");
    sb.Append("<label class='f'>Email</label><input class='t' type='email' name='email' required>");
    sb.Append("<label class='f'>Number of nights</label><input class='t' type='number' name='nights' min='1' max='30' value='" + n + "'>");
    sb.Append("<label class='f'>How would you like to pay?</label>");

    bool first = true;
    foreach (string method in paymentMethods)
    {
        sb.Append("<label class='pay'><input type='radio' name='method' value='" + method + "'" + (first ? " checked" : "") + "> " + method + "</label>");
        first = false;
    }

    sb.Append("<br><button class='btn'>Complete booking</button></form></div>");
    sb.Append("<div class='box'><h3>" + room.Name + "</h3><div class='muted'>" + room.Details + "</div><br>");
    sb.Append("<div class='row'><span>Price per night</span><b>" + Money(room.Price) + "</b></div>");
    sb.Append("<div class='row'><span>Nights</span><b>" + n + "</b></div>");
    sb.Append("<div class='row'><span>Total</span><b>" + Money(room.Price * n) + "</b></div>");
    sb.Append("<p class='muted'>Free cancellation. Refunds go back to your payment method.</p></div></div></div>");

    return Page("Book " + room.Name, sb.ToString());
});


app.MapPost("/book/{id}", async (int id, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    Room? room = rooms.Find(r => r.Id == id);
    if (room == null) return Message("Room not found", "That room does not exist.");

    string name = form["name"].ToString().Trim();
    string email = form["email"].ToString().Trim().ToLower();
    string method = form["method"].ToString();
    int.TryParse(form["nights"].ToString(), out int nights);

    string? chosenMethod = paymentMethods.Find(m => m == method);

    if (name == "" || !email.Contains("@") || nights < 1 || nights > 30 || chosenMethod == null)
    {
        return Message("Check your details", "Please fill in every field correctly and try again.");
    }

    if (!room.IsAvailable)
    {
        return Message("Sold out", "Sorry, the " + room.Name + " was just booked by someone else.");
    }

    Booking booking = new Booking();
    booking.Id = bookings.Count + 1;
    booking.GuestName = name;
    booking.Email = email;
    booking.RoomId = room.Id;
    booking.Nights = nights;
    booking.Total = room.Price * nights;
    booking.PaymentMethod = chosenMethod;
    booking.Status = chosenMethod == "Cash at hotel" ? "Pay at hotel" : "Paid";

    Transaction transaction = new Transaction();
    transaction.Id = transactions.Count + 1;
    transaction.BookingId = booking.Id;
    transaction.Type = "Payment";
    transaction.Method = chosenMethod;
    transaction.Amount = booking.Total;
    transaction.Date = DateTime.Now;
    transaction.Status = booking.Status == "Paid" ? "Completed" : "Due at check-in";

    booking.TransactionId = transaction.Id;

    bookings.Add(booking);
    transactions.Add(transaction);
    room.IsAvailable = false;

    return Results.Redirect("/confirmation/" + booking.Id);
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
        + "<div class='row'><span>Nights</span><b>" + b.Nights + "</b></div>"
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

    if (b.Status == "Paid")
    {
        Transaction refund = new Transaction();
        refund.Id = transactions.Count + 1;
        refund.BookingId = b.Id;
        refund.Type = "Refund";
        refund.Method = b.PaymentMethod;
        refund.Amount = b.Total;
        refund.Date = DateTime.Now;
        refund.Status = "Completed";
        transactions.Add(refund);

        b.Status = "Refunded";
    }
    else if (b.Status == "Pay at hotel")
    {
        b.Status = "Cancelled";
    }
    else
    {
        return Message("Cannot cancel", "This booking is already " + b.Status + ".");
    }

    Room? room = rooms.Find(r => r.Id == b.RoomId);
    if (room != null) room.IsAvailable = true;

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
    sb.Append("<p><b>How do I get a refund?</b><br><span class='muted'>Go to My bookings and press Cancel and refund. You get the full amount back on the same payment method.</span></p>");
    sb.Append("<p><b>Which payments do you accept?</b><br><span class='muted'>Credit Card, GCash, PayPal and Cash at hotel.</span></p></div>");
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

    return Message("Message received", "Thank you, " + H(ticket.Name) + ". Your support ticket is #" + ticket.Id + ". We will reply to " + H(ticket.Email) + " soon.");
});

app.MapGet("/admin", () =>
{
    var sb = new StringBuilder();
    sb.Append("<div class='wrap'><div class='box'><h2>Support tickets</h2>");

    foreach (Ticket t in tickets)
    {
        sb.Append("<div class='row'><span>#" + t.Id + " " + H(t.Name) + " (" + t.Topic + ")</span><span>" + H(t.Message) + "</span></div>");
    }

    if (tickets.Count == 0) sb.Append("<p>No tickets yet.</p>");

    sb.Append("</div><div class='box'><h2>Transactions</h2>");

    foreach (Transaction t in transactions)
    {
        sb.Append("<div class='row'><span>#" + t.Id + " " + t.Type + " - booking " + t.BookingId + " - " + t.Method + "</span><span>" + Money(t.Amount) + " (" + t.Status + ")</span></div>");
    }

    if (transactions.Count == 0) sb.Append("<p>No transactions yet.</p>");

    sb.Append("</div></div>");
    return Page("Staff", sb.ToString());
});


app.MapGet("/photo/{name}", (string name) =>
{
    string file = Path.GetFileName(name);

    string[] folders =
    {
        Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
        app.Environment.ContentRootPath,
        AppContext.BaseDirectory
    };

    foreach (string folder in folders)
    {
        string path = Path.Combine(folder, file);
        if (File.Exists(path))
        {
            return Results.File(path, "image/png");
        }
    }

    return Results.Text("Photo not found: " + file + "\nThe site looked in:\n" + string.Join("\n", folders), "text/plain");
});

app.Run();


class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Details { get; set; } = "";
    public string Image { get; set; } = "";
    public int Price { get; set; }
    public int MaxGuests { get; set; }
    public bool IsAvailable { get; set; }
}

class Booking
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
}

class Transaction
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string Type { get; set; } = "";
    public string Method { get; set; } = "";
    public int Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTime Date { get; set; }
}

class Ticket
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Message { get; set; } = "";
}
