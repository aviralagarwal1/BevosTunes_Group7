import openpyxl, re
from pathlib import Path
from datetime import datetime

SEEDER_DIR = Path(__file__).resolve().parent
REPO_ROOT = SEEDER_DIR.parent.parent
PROJECT_ROOT = REPO_ROOT / "BevosTunesMVC"
XLSX = SEEDER_DIR / "BevosTunes_Data.xlsx"
OUT = PROJECT_ROOT / "DAL" / "DbSeeder.cs"

wb = openpyxl.load_workbook(XLSX, data_only=True)

def get_data(sheet):
    ws = wb[sheet]
    all_rows = list(ws.iter_rows(values_only=True))
    data = [r for r in all_rows[1:] if any(v is not None for v in r)]
    return all_rows[0], data

def esc(v):
    if v is None: return ""
    s = str(v).strip()
    s = s.replace('\\', '\\\\').replace('"', '\\"')
    return s

def q(v):
    return '"' + esc(v) + '"'

def phone_str(v):
    if v is None: return '""'
    s = str(v).strip()
    if s.endswith('.0'): s = s[:-2]
    digits = re.sub(r'\D', '', s)
    if len(digits) == 10 and s == digits:
        return q(f"({digits[0:3]}) {digits[3:6]}-{digits[6:]}")
    return q(s)

def zip_str(v):
    if v is None: return '""'
    s = str(v).strip()
    if s.endswith('.0'): s = s[:-2]
    return q(s)

lines = []
W = lines.append

W("using BevosTunesMVC.Models;")
W("using Microsoft.EntityFrameworkCore;")
W("")
W("namespace BevosTunesMVC.DAL;")
W("")
W("public static class DbSeeder")
W("{")

def catch_block(table):
    W(f'        catch (Exception ex) {{ throw new InvalidOperationException(ex.Message + "  {table} added:" + intAdded + "; Error on " + strName); }}')
    W("    }")

# === GENRES ===
_, genres = get_data('Genre')
W("")
W("    public static void SeedGenres(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Genre> items = new List<Genre>();")
W("")
for i, r in enumerate(genres, 1):
    name = str(r[1]).strip() if r[1] else None
    if name:
        W(f"        Genre g{i} = new Genre() {{ Name = {q(name)} }};")
        W(f"        items.Add(g{i});")
W("")
W("        try")
W("        {")
W("            foreach (Genre toAdd in items)")
W("            {")
W("                strName = toAdd.Name;")
W("                Genre? dbItem = db.Genres.FirstOrDefault(x => x.Name == toAdd.Name);")
W("                if (dbItem == null) { db.Genres.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.Name = toAdd.Name; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Genres")

# === ARTISTS ===
_, artists = get_data('Artist')
W("")
W("    public static void SeedArtists(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Artist> items = new List<Artist>();")
W("")
for i, r in enumerate(artists, 1):
    name = str(r[1]).strip() if r[1] else None
    if name:
        W(f"        Artist a{i} = new Artist() {{ Name = {q(name)} }};")
        W(f"        items.Add(a{i});")
W("")
W("        try")
W("        {")
W("            foreach (Artist toAdd in items)")
W("            {")
W("                strName = toAdd.Name;")
W("                Artist? dbItem = db.Artists.FirstOrDefault(x => x.Name == toAdd.Name);")
W("                if (dbItem == null) { db.Artists.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.Name = toAdd.Name; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Artists")

# === ARTIST GENRES ===
W("")
W("    public static void SeedArtistGenres(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in artists:
    aname = str(r[1]).strip() if r[1] else None
    if not aname: continue
    for col in r[2:]:
        gname = str(col).strip() if col else None
        if not gname: continue
        W(f"            strName = {q(aname + ' - ' + gname)};")
        W(f"            {{ var _a = db.Artists.FirstOrDefault(x => x.Name == {q(aname)});")
        W(f"              var _g = db.Genres.FirstOrDefault(x => x.Name == {q(gname)});")
        W(f"              if (_a != null && _g != null && !db.ArtistGenres.Any(x => x.ArtistID == _a.ArtistID && x.GenreID == _g.GenreID))")
        W(f"              {{ db.ArtistGenres.Add(new ArtistGenre {{ ArtistID = _a.ArtistID, GenreID = _g.GenreID }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("ArtistGenres")

# === ALBUMS ===
alb_headers, albums = get_data('Albums')
# Find cover column
cover_col = None
for j, h in enumerate(alb_headers):
    if h and ('cover' in str(h).lower() or 'url' in str(h).lower()):
        cover_col = j
# Find genre start col
genre_start = None
for j, h in enumerate(alb_headers):
    if h and 'genre' in str(h).lower() and j > 2:
        genre_start = j
        break
genre_end = (cover_col - 1) if cover_col else len(alb_headers) - 1
art_start = 3
art_end = (genre_start - 1) if genre_start else 3

W("")
W("    public static void SeedAlbums(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Album> items = new List<Album>();")
W("")
for i, r in enumerate(albums, 1):
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    price = str(r[2]).strip() if r[2] is not None else "0"
    if price.endswith('.0'): price = price[:-2]
    cover = str(r[cover_col]).strip() if cover_col is not None and r[cover_col] else None
    W(f"        Album al{i} = new Album()")
    W("        {")
    W(f"            Title = {q(title)},")
    W(f"            Price = {price}m,")
    W(f"            IsActive = true,")
    if cover:
        W(f"            AlbumCoverURL = {q(cover)}")
    else:
        W(f"            AlbumCoverURL = null")
    W("        };")
    W(f"        items.Add(al{i});")
W("")
W("        try")
W("        {")
W("            foreach (Album toAdd in items)")
W("            {")
W("                strName = toAdd.Title;")
W("                Album? dbItem = db.Albums.FirstOrDefault(x => x.Title == toAdd.Title);")
W("                if (dbItem == null) { db.Albums.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.Title = toAdd.Title; dbItem.Price = toAdd.Price; dbItem.AlbumCoverURL = toAdd.AlbumCoverURL; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Albums")

# === ALBUM ARTISTS ===
W("")
W("    public static void SeedAlbumArtists(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in albums:
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    for j in range(art_start, art_end + 1):
        aname = str(r[j]).strip() if r[j] else None
        if not aname: continue
        W(f"            strName = {q(title + ' - ' + aname)};")
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(title)});")
        W(f"              var _a = db.Artists.FirstOrDefault(x => x.Name == {q(aname)});")
        W(f"              if (_al != null && _a != null && !db.AlbumArtists.Any(x => x.AlbumID == _al.AlbumID && x.ArtistID == _a.ArtistID))")
        W(f"              {{ db.AlbumArtists.Add(new AlbumArtist {{ AlbumID = _al.AlbumID, ArtistID = _a.ArtistID }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("AlbumArtists")

# === ALBUM GENRES ===
W("")
W("    public static void SeedAlbumGenres(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in albums:
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    for j in range(genre_start, genre_end + 1):
        gname = str(r[j]).strip() if r[j] else None
        if not gname: continue
        W(f"            strName = {q(title + ' - ' + gname)};")
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(title)});")
        W(f"              var _g = db.Genres.FirstOrDefault(x => x.Name == {q(gname)});")
        W(f"              if (_al != null && _g != null && !db.AlbumGenres.Any(x => x.AlbumID == _al.AlbumID && x.GenreID == _g.GenreID))")
        W(f"              {{ db.AlbumGenres.Add(new AlbumGenre {{ AlbumID = _al.AlbumID, GenreID = _g.GenreID }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("AlbumGenres")

# === SONGS ===
# Songs: A=SongID(skip), B=Name, C=Price, D=Album1, E=Album2, F=Album3, G=Artist, H=Genre1, I=Genre2, J=Genre3
_, songs = get_data('Songs')
W("")
W("    public static void SeedSongs(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Song> items = new List<Song>();")
W("")
for i, r in enumerate(songs, 1):
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    price = str(r[2]).strip() if r[2] is not None else "1"
    if price.endswith('.0'): price = price[:-2]
    artist = str(r[6]).strip() if r[6] else None
    W(f"        Song s{i} = new Song()")
    W("        {")
    W(f"            Title = {q(title)},")
    W(f"            Price = {price}m,")
    W(f"            IsActive = true")
    W("        };")
    if artist:
        W(f"        s{i}.Artist = db.Artists.FirstOrDefault(x => x.Name == {q(artist)}) ?? db.Artists.First();")
    W(f"        items.Add(s{i});")
W("")
W("        try")
W("        {")
W("            foreach (Song toAdd in items)")
W("            {")
W("                strName = toAdd.Title;")
W("                Song? dbItem = db.Songs.FirstOrDefault(x => x.Title == toAdd.Title);")
W("                if (dbItem == null) { db.Songs.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.Title = toAdd.Title; dbItem.Price = toAdd.Price; dbItem.Artist = toAdd.Artist; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Songs")

# === SONG GENRES ===
W("")
W("    public static void SeedSongGenres(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in songs:
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    for col in r[7:10]:
        gname = str(col).strip() if col else None
        if not gname: continue
        W(f"            strName = {q(title + ' - ' + gname)};")
        W(f"            {{ var _s = db.Songs.FirstOrDefault(x => x.Title == {q(title)});")
        W(f"              var _g = db.Genres.FirstOrDefault(x => x.Name == {q(gname)});")
        W(f"              if (_s != null && _g != null && !db.SongGenres.Any(x => x.SongID == _s.SongID && x.GenreID == _g.GenreID))")
        W(f"              {{ db.SongGenres.Add(new SongGenre {{ SongID = _s.SongID, GenreID = _g.GenreID }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("SongGenres")

# === ALBUM SONGS ===
W("")
W("    public static void SeedAlbumSongs(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in songs:
    title = str(r[1]).strip() if r[1] else None
    if not title: continue
    for col in r[3:6]:
        aname = str(col).strip() if col else None
        if not aname: continue
        W(f"            strName = {q(aname + ' - ' + title)};")
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(aname)});")
        W(f"              var _s = db.Songs.FirstOrDefault(x => x.Title == {q(title)});")
        W(f"              if (_al != null && _s != null && !db.AlbumSongs.Any(x => x.AlbumID == _al.AlbumID && x.SongID == _s.SongID))")
        W(f"              {{ db.AlbumSongs.Add(new AlbumSong {{ AlbumID = _al.AlbumID, SongID = _s.SongID }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("AlbumSongs")

# === CUSTOMERS ===
_, customers = get_data('Customers')
W("")
W("    public static void SeedCustomers(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Customer> items = new List<Customer>();")
W("")
for i, r in enumerate(customers, 1):
    email = str(r[3]).strip() if r[3] else None
    if not email: continue
    fname  = str(r[1]).strip() if r[1] else ""
    lname  = str(r[2]).strip() if r[2] else ""
    pw     = str(r[4]).strip() if r[4] else ""
    phone  = phone_str(r[5])
    street = str(r[6]).strip() if r[6] else ""
    zipcd  = zip_str(r[7])
    W(f"        Customer c{i} = new Customer()")
    W("        {")
    W(f"            FirstName = {q(fname)},")
    W(f"            LastName = {q(lname)},")
    W(f"            Email = {q(email)},")
    W(f"            Password = {q(pw)},")
    W(f"            PhoneNumber = {phone},")
    W(f"            Street = {q(street)},")
    W(f'            City = "Austin",')
    W(f'            State = "TX",')
    W(f"            ZipCode = {zipcd},")
    W(f"            IsActive = true")
    W("        };")
    W(f"        items.Add(c{i});")
W("")
W("        try")
W("        {")
W("            foreach (Customer toAdd in items)")
W("            {")
W("                strName = toAdd.Email;")
W("                Customer? dbItem = db.Customers.FirstOrDefault(x => x.Email == toAdd.Email);")
W("                if (dbItem == null) { db.Customers.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.FirstName = toAdd.FirstName; dbItem.LastName = toAdd.LastName; dbItem.PhoneNumber = toAdd.PhoneNumber; dbItem.Street = toAdd.Street; dbItem.ZipCode = toAdd.ZipCode; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Customers")

# === EMPLOYEES ===
_, employees = get_data('Employees')
_, admins    = get_data('Admins')
W("")
W("    public static void SeedEmployees(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<Employee> items = new List<Employee>();")
W("")
W("        // --- Regular Employees ---")
n = 1
for r in employees:
    email = str(r[2]).strip() if r[2] else None
    if not email: continue
    fname  = str(r[0]).strip() if r[0] else ""
    lname  = str(r[1]).strip() if r[1] else ""
    pw     = str(r[3]).strip() if r[3] else ""
    phone  = phone_str(r[4])
    street = str(r[5]).strip() if r[5] else ""
    zipcd  = zip_str(r[6])
    W(f"        Employee e{n} = new Employee()")
    W("        {")
    W(f"            FirstName = {q(fname)},")
    W(f"            LastName = {q(lname)},")
    W(f"            Email = {q(email)},")
    W(f"            Password = {q(pw)},")
    W(f"            PhoneNumber = {phone},")
    W(f"            Street = {q(street)},")
    W(f'            City = "Austin",')
    W(f'            State = "TX",')
    W(f"            ZipCode = {zipcd},")
    W(f"            IsActive = true,")
    W(f"            IsManager = false")
    W("        };")
    W(f"        items.Add(e{n});")
    n += 1
W("")
W("        // --- Managers (Admins sheet) ---")
# Admins: A=LastName, B=FirstName, C=PSW, D=Address, E=Zip, F=Phone, G=Email
for r in admins:
    email = str(r[6]).strip() if r[6] else None
    if not email: continue
    fname  = str(r[1]).strip() if r[1] else ""
    lname  = str(r[0]).strip() if r[0] else ""
    pw     = str(r[2]).strip() if r[2] else ""
    phone  = phone_str(r[5])
    street = str(r[3]).strip() if r[3] else ""
    zipcd  = zip_str(r[4])
    W(f"        Employee e{n} = new Employee()")
    W("        {")
    W(f"            FirstName = {q(fname)},")
    W(f"            LastName = {q(lname)},")
    W(f"            Email = {q(email)},")
    W(f"            Password = {q(pw)},")
    W(f"            PhoneNumber = {phone},")
    W(f"            Street = {q(street)},")
    W(f'            City = "Austin",')
    W(f'            State = "TX",')
    W(f"            ZipCode = {zipcd},")
    W(f"            IsActive = true,")
    W(f"            IsManager = true")
    W("        };")
    W(f"        items.Add(e{n});")
    n += 1
W("")
W("        try")
W("        {")
W("            foreach (Employee toAdd in items)")
W("            {")
W("                strName = toAdd.Email;")
W("                Employee? dbItem = db.Employees.FirstOrDefault(x => x.Email == toAdd.Email);")
W("                if (dbItem == null) { db.Employees.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.FirstName = toAdd.FirstName; dbItem.LastName = toAdd.LastName; dbItem.Password = toAdd.Password; dbItem.PhoneNumber = toAdd.PhoneNumber; dbItem.Street = toAdd.Street; dbItem.ZipCode = toAdd.ZipCode; dbItem.IsManager = toAdd.IsManager; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("Employees")

# === CREDIT CARDS (explicit ID) ===
_, cards = get_data('Cards')
W("")
W("    public static void SeedCreditCards(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        List<CreditCard> items = new List<CreditCard>();")
W("")
for i, r in enumerate(cards, 1):
    cnum = str(r[2]).strip() if r[2] else None
    if not cnum: continue
    card_id = str(r[0]).strip()
    if card_id.endswith('.0'): card_id = card_id[:-2]
    cust_full = str(r[1]).strip() if r[1] else ""
    parts = cust_full.split(' ', 1)
    cfirst = parts[0] if len(parts) > 0 else ""
    clast  = parts[1] if len(parts) > 1 else ""
    ctype  = str(r[3]).strip() if r[3] else ""
    if cnum.endswith('.0'): cnum = cnum[:-2]
    W(f"        CreditCard cc{i} = new CreditCard()")
    W("        {")
    W(f"            CreditCardID = {card_id},")
    W(f"            CardNumber = {q(cnum)},")
    W(f"            CardType = {q(ctype)},")
    W(f"            IsActive = true")
    W("        };")
    W(f"        cc{i}.Customer = db.Customers.FirstOrDefault(c => c.FirstName == {q(cfirst)} && c.LastName == {q(clast)});")
    W(f"        items.Add(cc{i});")
W("")
W("        try")
W("        {")
W("            foreach (CreditCard toAdd in items)")
W("            {")
W("                strName = toAdd.CardNumber;")
W("                CreditCard? dbItem = db.CreditCards.FirstOrDefault(x => x.CreditCardID == toAdd.CreditCardID);")
W("                if (dbItem == null) { db.CreditCards.Add(toAdd); db.SaveChanges(); intAdded++; }")
W("                else { dbItem.CardNumber = toAdd.CardNumber; dbItem.CardType = toAdd.CardType; dbItem.Customer = toAdd.Customer; db.Update(dbItem); db.SaveChanges(); intAdded++; }")
W("            }")
W("        }")
catch_block("CreditCards")

# === ORDERS + ORDER ITEMS (explicit OrderID) ===
_, order_rows = get_data('Orders')
W("")
W("    public static void SeedOrders(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")

seen_orders = set()
status_map = {'incart': 'OrderStatus.InCart', 'ordered': 'OrderStatus.Ordered', 'refunded': 'OrderStatus.Refunded'}

for r in order_rows:
    if r[0] is None: continue
    oid       = str(r[0]).strip()
    if oid.endswith('.0'): oid = oid[:-2]
    if not oid.isdigit(): continue
    card_id   = str(r[1]).strip() if r[1] else ""
    if card_id.endswith('.0'): card_id = card_id[:-2]
    cust_full = str(r[2]).strip() if r[2] else ""
    parts     = cust_full.split(' ', 1)
    cfirst    = parts[0] if len(parts) > 0 else ""
    clast     = parts[1] if len(parts) > 1 else ""
    odate     = r[3]
    status_raw = str(r[4]).strip().lower().replace('_', '') if r[4] else ""
    song      = str(r[5]).strip() if r[5] else None
    album     = str(r[6]).strip() if r[6] else None
    price     = str(r[7]).strip() if r[7] is not None else "0"
    if price.endswith('.0'): price = price[:-2]
    gift      = str(r[8]).strip() if r[8] else None
    status_enum = status_map.get(status_raw, 'OrderStatus.Ordered')
    is_refunded = 'true' if status_enum == 'OrderStatus.Refunded' else 'false'

    if oid not in seen_orders:
        seen_orders.add(oid)
        W("")
        W(f"            // Order {oid}")
        W(f"            strName = \"{oid}\";")
        W(f"            if (!db.Orders.Any(x => x.OrderID == {oid}))")
        W(f"            {{")
        W(f"                Order o_{oid} = new Order()")
        W(f"                {{")
        W(f"                    OrderID = {oid},")
        W(f"                    Status = {status_enum},")
        if odate and isinstance(odate, datetime):
            W(f"                    OrderDate = new DateTime({odate.year}, {odate.month}, {odate.day}),")
        else:
            W(f"                    OrderDate = null,")
        if gift:
            W(f"                    IsGift = true,")
            W(f"                    GiftRecipientEmail = {q(gift)},")
        else:
            W(f"                    IsGift = false,")
        W(f"                    IsRefunded = {is_refunded}")
        W(f"                }};")
        W(f"                o_{oid}.Customer = db.Customers.FirstOrDefault(c => c.FirstName == {q(cfirst)} && c.LastName == {q(clast)});")
        if card_id:
            W(f"                o_{oid}.CreditCard = db.CreditCards.FirstOrDefault(cc => cc.CreditCardID == {card_id});")
        W(f"                db.Orders.Add(o_{oid}); db.SaveChanges(); intAdded++;")
        W(f"            }}")

    if song:
        W(f"            {{ var _s = db.Songs.FirstOrDefault(x => x.Title == {q(song)});")
        W(f"              if (_s != null && !db.OrderItems.Any(x => x.OrderID == {oid} && x.SongID == _s.SongID))")
        W(f"              {{ db.OrderItems.Add(new OrderItem {{ OrderID = {oid}, SongID = _s.SongID, Price = {price}m }}); db.SaveChanges(); }} }}")
    elif album:
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(album)});")
        W(f"              if (_al != null && !db.OrderItems.Any(x => x.OrderID == {oid} && x.AlbumID == _al.AlbumID))")
        W(f"              {{ db.OrderItems.Add(new OrderItem {{ OrderID = {oid}, AlbumID = _al.AlbumID, Price = {price}m }}); db.SaveChanges(); }} }}")

W("        }")
catch_block("Orders")

# === CART ITEMS (from InCart orders) ===
W("")
W("    public static void SeedCartItems(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")

seen_cart_customers: dict[str, set] = {}
for r in order_rows:
    if r[0] is None: continue
    status_raw_ci = str(r[4]).strip().lower().replace('_', '') if r[4] else ""
    if status_raw_ci != 'incart': continue
    cust_full_ci = str(r[2]).strip() if r[2] else ""
    parts_ci = cust_full_ci.split(' ', 1)
    cfirst_ci = parts_ci[0] if len(parts_ci) > 0 else ""
    clast_ci  = parts_ci[1] if len(parts_ci) > 1 else ""
    song_ci  = str(r[5]).strip() if r[5] else None
    album_ci = str(r[6]).strip() if r[6] else None
    item_name_ci = song_ci or album_ci or "?"
    W(f"            strName = {q(item_name_ci)};")
    if song_ci:
        W(f"            {{ var _c = db.Customers.FirstOrDefault(c => c.FirstName == {q(cfirst_ci)} && c.LastName == {q(clast_ci)});")
        W(f"              var _s = db.Songs.FirstOrDefault(x => x.Title == {q(song_ci)});")
        W(f"              if (_c != null && _s != null && !db.CartItems.Any(ci => ci.CustomerID == _c.CustomerID && ci.SongID == _s.SongID))")
        W(f"              {{ db.CartItems.Add(new CartItem {{ CustomerID = _c.CustomerID, SongID = _s.SongID, DateAdded = DateTime.Now }}); db.SaveChanges(); intAdded++; }} }}")
    elif album_ci:
        W(f"            {{ var _c = db.Customers.FirstOrDefault(c => c.FirstName == {q(cfirst_ci)} && c.LastName == {q(clast_ci)});")
        W(f"              var _al = db.Albums.FirstOrDefault(x => x.Title == {q(album_ci)});")
        W(f"              if (_c != null && _al != null && !db.CartItems.Any(ci => ci.CustomerID == _c.CustomerID && ci.AlbumID == _al.AlbumID))")
        W(f"              {{ db.CartItems.Add(new CartItem {{ CustomerID = _c.CustomerID, AlbumID = _al.AlbumID, DateAdded = DateTime.Now }}); db.SaveChanges(); intAdded++; }} }}")

W("        }")
catch_block("CartItems")

# === REVIEWS (Song + Album, try song first then album) ===
_, reviews = get_data('Reviews')
W("")
W("    public static void SeedSongReviews(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in reviews:
    if not r[0] or not r[1]: continue
    reviewer = str(r[0]).strip()
    item     = str(r[1]).strip()
    approver = str(r[2]).strip() if r[2] else None
    rating   = int(r[3]) if r[3] else 3
    text_raw = str(r[4]).strip() if r[4] else None
    # Sanitize text: replace non-ASCII smart quotes etc.
    if text_raw:
        text_raw = text_raw.encode('ascii', 'replace').decode('ascii').replace('?', "'")
    disp     = str(r[5]).strip().lower() if r[5] else ""
    if disp == 'approve':  status = "ReviewStatus.Approved"
    elif disp == 'reject': status = "ReviewStatus.Rejected"
    else:                  status = "ReviewStatus.Pending"

    rparts = reviewer.split(' ', 1)
    rfirst = rparts[0]; rlast = rparts[1] if len(rparts) > 1 else ""

    W(f"            strName = {q(reviewer + ' - ' + item)};")
    W(f"            {{ var _cust = db.Customers.FirstOrDefault(x => x.FirstName == {q(rfirst)} && x.LastName == {q(rlast)});")
    W(f"              var _song = db.Songs.FirstOrDefault(x => x.Title == {q(item)});")
    W(f"              var _alb  = db.Albums.FirstOrDefault(x => x.Title == {q(item)});")
    if approver:
        aparts = approver.split(' ', 1)
        afirst = aparts[0]; alast = aparts[1] if len(aparts) > 1 else ""
        W(f"              var _emp  = db.Employees.FirstOrDefault(x => x.FirstName == {q(afirst)} && x.LastName == {q(alast)});")
        emp_field = ", ApprovingEmployeeID = _emp != null ? _emp.EmployeeID : (int?)null"
    else:
        emp_field = ""

    text_val = q(text_raw) if text_raw else "null"
    W(f"              if (_song != null && _cust != null && !db.SongReviews.Any(x => x.CustomerID == _cust.CustomerID && x.SongID == _song.SongID))")
    W(f"              {{ db.SongReviews.Add(new SongReview {{ CustomerID = _cust.CustomerID, SongID = _song.SongID, Rating = {rating}, ReviewText = {text_val}, Status = {status}{emp_field} }}); db.SaveChanges(); intAdded++; }}")
    W(f"              else if (_alb != null && _cust != null && !db.AlbumReviews.Any(x => x.CustomerID == _cust.CustomerID && x.AlbumID == _alb.AlbumID))")
    W(f"              {{ db.AlbumReviews.Add(new AlbumReview {{ CustomerID = _cust.CustomerID, AlbumID = _alb.AlbumID, Rating = {rating}, ReviewText = {text_val}, Status = {status}{emp_field} }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("Reviews")

# === FEATURED ITEMS ===
_, promos = get_data('Promotion')
featured  = [r for r in promos if r[3] and str(r[3]).strip().lower() == 'featured']
discounts = [r for r in promos if r[3] and str(r[3]).strip().lower() == 'discount']

W("")
W("    public static void SeedFeaturedItems(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in featured:
    artist = str(r[0]).strip() if r[0] else None
    song   = str(r[1]).strip() if r[1] else None
    album  = str(r[2]).strip() if r[2] else None
    active = str(r[5]).strip().lower() == 'active' if r[5] else False
    label  = artist or song or album or "?"
    W(f"            strName = {q(label)};")
    if artist:
        W(f"            {{ var _a = db.Artists.FirstOrDefault(x => x.Name == {q(artist)});")
        W(f"              if (_a != null && !db.FeaturedItems.Any(x => x.ArtistID == _a.ArtistID))")
        W(f"              {{ db.FeaturedItems.Add(new FeaturedItem {{ ArtistID = _a.ArtistID, IsActive = {str(active).lower()} }}); db.SaveChanges(); intAdded++; }} }}")
    elif song:
        W(f"            {{ var _s = db.Songs.FirstOrDefault(x => x.Title == {q(song)});")
        W(f"              if (_s != null && !db.FeaturedItems.Any(x => x.SongID == _s.SongID))")
        W(f"              {{ db.FeaturedItems.Add(new FeaturedItem {{ SongID = _s.SongID, IsActive = {str(active).lower()} }}); db.SaveChanges(); intAdded++; }} }}")
    elif album:
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(album)});")
        W(f"              if (_al != null && !db.FeaturedItems.Any(x => x.AlbumID == _al.AlbumID))")
        W(f"              {{ db.FeaturedItems.Add(new FeaturedItem {{ AlbumID = _al.AlbumID, IsActive = {str(active).lower()} }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("FeaturedItems")

# === DISCOUNTS ===
W("")
W("    public static void SeedDiscounts(AppDbContext db)")
W("    {")
W("        Int32 intAdded = 0;")
W('        String strName = "Begin";')
W("        try")
W("        {")
for r in discounts:
    song   = str(r[1]).strip() if r[1] else None
    album  = str(r[2]).strip() if r[2] else None
    amt    = str(r[4]).strip() if r[4] is not None else "0"
    if amt.endswith('.0'): amt = amt[:-2]
    active = str(r[5]).strip().lower() == 'active' if r[5] else False
    label  = song or album or "?"
    W(f"            strName = {q(label)};")
    if song:
        W(f"            {{ var _s = db.Songs.FirstOrDefault(x => x.Title == {q(song)});")
        W(f"              if (_s != null && !db.Discounts.Any(x => x.SongID == _s.SongID))")
        W(f"              {{ db.Discounts.Add(new Discount {{ SongID = _s.SongID, DiscountAmount = {amt}m, IsActive = {str(active).lower()} }}); db.SaveChanges(); intAdded++; }} }}")
    elif album:
        W(f"            {{ var _al = db.Albums.FirstOrDefault(x => x.Title == {q(album)});")
        W(f"              if (_al != null && !db.Discounts.Any(x => x.AlbumID == _al.AlbumID))")
        W(f"              {{ db.Discounts.Add(new Discount {{ AlbumID = _al.AlbumID, DiscountAmount = {amt}m, IsActive = {str(active).lower()} }}); db.SaveChanges(); intAdded++; }} }}")
W("        }")
catch_block("Discounts")

W("}")

content = "\n".join(lines)
with open(OUT, 'w', encoding='utf-8') as f:
    f.write(content)
print(f"SUCCESS: Written {len(lines)} lines to {OUT}")
