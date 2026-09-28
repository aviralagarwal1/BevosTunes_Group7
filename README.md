# Bevo's Tunes

A role-based music storefront built with ASP.NET Core MVC. Customers browse, buy, gift, and review songs and albums. Employees and managers get back-office tools for customer support, catalog management, promotions, and sales reporting.

<img src=".github/assets/screenshot.png" alt="Bevo's Tunes home page" width="900" />

## Project specification and results

Read the [full original project specification](course/final-project-spec.txt) for the detailed requirements this app was built against: customer, employee, and manager workflows; search rules; checkout and gifting; review eligibility; promotions; reporting; and data-preservation constraints.

**Course evaluation:** passed 98% of 200 live-graded specification test cases, ranked 1st among Spring 2026 MIS 333K teams, and received the $2,000 first-place award.

The specification is the instructor-provided assignment, preserved in full for reference. Some of its logistics (hosting, submission, grading) applied only to the course; everything you need to run the app yourself is below.

## Features

**Customers**

- Browse and search songs, albums, and artists (by keyword, genre, and rating) without logging in
- Keep a persistent cart that flags songs you'd be buying twice through an album
- Check out with a saved or new card, for yourself or as a gift to another customer
- Get itemized order, gift, and refund emails
- View order history, a gift-aware *My Music* library, and a *My Reviews* page

**Employees**

- Create, edit, disable, and re-enable customer accounts
- Approve, reject, and edit reviews
- Place orders on a customer's behalf

**Managers**

- Everything employees can do, plus hire, edit, fire, rehire, and promote employees
- Manage songs, albums, artists, genres, featured items, and discounts
- View sales reports for songs, albums, and top bands by genre

## Tech stack

- ASP.NET Core MVC on .NET 10, Razor views, Bootstrap 5
- Entity Framework Core + SQL Server
- Gmail SMTP for transactional email; [Zippopotam](https://zippopotam.us) for ZIP → city/state lookup
- No custom JavaScript or front-end framework. MIS 333K is a server-side C#/MVC course, so every interaction is a server-rendered page or form post.

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) and an empty SQL Server database (LocalDB, SQL Server Express, or Azure SQL all work).

1. Clone the repo.
2. Copy `BevosTunesMVC/appsettings.Development.json.example` to `BevosTunesMVC/appsettings.Development.json`. This file is gitignored.
3. Check `ConnectionStrings:DefaultConnection`. The example points at Windows LocalDB (installed with Visual Studio), so on Windows it usually works as-is. On macOS or Linux, point it at any SQL Server you can reach, for example the [SQL Server Docker image](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker).
4. Optional: fill in the `Email` section with a Gmail address and [app password](https://support.google.com/accounts/answer/185833) to send real emails. Leave `FromAddress` and `AppPassword` empty to skip email delivery.
5. Run:
   ```bash
   dotnet run --project BevosTunesMVC/BevosTunesMVC.csproj
   ```
6. Open `http://localhost:5150`.

On first run, the app applies migrations and seeds a full demo dataset: catalog, customers, staff, orders, reviews, discounts, and featured items. Seeding only runs when the database is empty, so to reset, point the app at a fresh database and run it again.

`dotnet build` works without any configuration; running the app needs a reachable database.

### Demo accounts

| Role | Email | Password |
|---|---|---|
| Customer | `cbaker@example.com` | `musiclover` |
| Customer | `banker@longhorn.net` | `potato` |
| Employee | `j.smith@bevotunes.com` | `Password1` |
| Manager | `c.baker@bevotunes.com` | `dewey4` |

Seeded customer emails are placeholders. To test email delivery, register a new account with an inbox you control.

## Configuration

Local settings go in `appsettings.Development.json`. For hosted environments, use environment variables. ASP.NET maps `__` to `:`.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string (required) |
| `Email__SmtpHost`, `Email__SmtpPort` | SMTP server (defaults to Gmail on 587) |
| `Email__FromAddress`, `Email__AppPassword` | SMTP sender account |
| `Email__SubjectPrefix` | Prefix for email subject lines |
| `Email__GradingInbox` | Optional comma-separated addresses BCC'd on every outgoing email, useful for watching mail sent to the seeded placeholder accounts |

## Project structure

```
BevosTunesMVC/          ASP.NET Core MVC app
  Controllers/          Request handling by role and feature
  Models/               EF Core entities and view models
  DAL/                  DbContext and generated seed data
  Services/             Email delivery
  Views/                Razor views
  Migrations/           EF Core schema history
course/                Original project specification and course seeding guide
tools/seeder/           Python generator that turns the seed spreadsheet into DAL/DbSeeder.cs
.github/workflows/      Build CI
```

`DAL/DbSeeder.cs` is generated from `tools/seeder/BevosTunes_Data.xlsx`, so don't edit it by hand. To change the seed data, edit the spreadsheet, then run:

```bash
pip install openpyxl
python tools/seeder/gen_seeder.py
```

The new data loads the next time the app starts against an empty database. The original [course VBA seeding guide](course/seeding-with-vba.txt) is included for historical context; this repo uses the Python generator above.

## Acknowledgements

Bevo's Tunes was built by Group 7 as the final project for MIS 333K at the University of Texas at Austin in Spring 2026. The course provided the requirements and seed data. Special thanks to Professor Jawad and the MIS 333K TAs for their help throughout the semester.

**Group 7:** Aviral Agarwal, Samhith Dharani, Shriya Punreddy, Raya Bhattacharyya

## Contributing

Issues and pull requests are welcome. Please keep the app free of custom JavaScript, run `dotnet build` before opening a PR, and change seed data through the spreadsheet rather than editing `DbSeeder.cs`.

## License

Project code: [MIT](LICENSE). The instructor-provided materials in `course/` are included as attributed references and are not covered by the project's MIT license.
