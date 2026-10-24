# Nudge 👋

Send a friend a nudge: their phone buzzes or their PC pops a notification, with an optional note and an emoji or two. Set a mood so your friends know how you're doing before they nudge you.

Built with ASP.NET Core (.NET 10), EF Core + SQLite, SignalR and a plain JavaScript web client.

## Features

- **Accounts.** Sign up and log in. Passwords are hashed with PBKDF2 and a random salt, and the API issues a JWT for each session.
- **Friends.** Send a friend request by username, then accept, decline or cancel it. You can also remove a friend.
- **Moods.** Pick a preset mood (😴 Sleepy, 🎮 Gaming…) or make your own with any emoji and up to 30 characters. Your friends see your mood next to your name and in the nudge dialog.
- **Nudges.** Each nudge can carry a note of up to 140 characters and up to 2 emojis. It arrives in real time. On Android it vibrates; on a PC it shows a system notification. It also shows a toast inside the app.
- **Private rooms.** Make a room with any of your friends and nudge everyone in it at once. People outside the room can't see it at all.

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd Nudge.Api
dotnet run
```

Then open http://localhost:5173. To try it with two people, open a second browser profile or a private window and register a second account.

The database (`nudge.db`) is created and migrated automatically on startup.

### Run the tests

```bash
dotnet test
```

The tests start the whole API in memory, each test class with its own throwaway database.

### Configuration

| Setting | What it is | Default |
|---|---|---|
| `ConnectionStrings:Nudge` | SQLite connection string | `Data Source=nudge.db` |
| `Jwt:Key` | Secret for signing login tokens. **At least 32 characters.** | Development only: a dev key in `appsettings.Development.json` |
| `Jwt:ExpiryHours` | How long a login lasts | 168 (7 days) |
| `RateLimits:AuthPerMinute` | Login/register attempts per IP per minute | 20 |
| `RateLimits:NudgesPerMinute` | Nudges per user per minute | 30 |

Outside Development, set the key with an environment variable such as `Jwt__Key=...`. The app refuses to start without it. Never commit a real key.

### Phones

Browsers only allow vibration and notifications on **HTTPS** pages (or `localhost`). To test on your phone, deploy behind HTTPS, or use a tunnel such as `ngrok http 5173`. iPhones don't support web vibration at all, so they get the notification and the toast only.

## API

All routes except register, login, health and mood presets need `Authorization: Bearer <token>`. Errors come back as [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) JSON with a readable `detail`.

| Method | Route | Does |
|---|---|---|
| POST | `/api/auth/register` | Create an account and get a token |
| POST | `/api/auth/login` | Log in and get a token |
| GET | `/api/me` | Your profile |
| GET | `/api/moods/presets` | Preset moods |
| PUT / DELETE | `/api/me/mood` | Set or clear your mood |
| GET | `/api/friends` | Your friends, with their moods |
| DELETE | `/api/friends/{friendId}` | Remove a friend |
| GET / POST | `/api/friends/requests` | List your requests, or send one by username |
| POST | `/api/friends/requests/{id}/accept` | Accept a request |
| DELETE | `/api/friends/requests/{id}` | Decline or cancel a request |
| GET / POST | `/api/rooms` | Your rooms, or create one |
| GET | `/api/rooms/{id}` | One room |
| POST | `/api/rooms/{id}/members` | Add a friend to a room |
| DELETE | `/api/rooms/{id}/members/{userId}` | Leave (your own id) or remove someone (owner only) |
| GET | `/api/rooms/{id}/nudges` | The room's recent nudges |
| POST | `/api/nudges` | Send a nudge to a friend (`toUserId`) or a room (`roomId`) |
| GET | `/api/nudges/inbox` | Nudges sent to you directly |
| POST | `/api/nudges/{id}/read` | Mark a nudge as read |

Real-time events come over the SignalR hub at `/hubs/nudge`: `NudgeReceived`, `MoodChanged`, `FriendsChanged` and `RoomsChanged`.

`Nudge.Api/Nudge.Api.http` has a request for each of these, in order, ready to click through in VS Code (REST Client extension) or Visual Studio.

## Project layout

```
Nudge.Api/
  Models/          Database entities (User, Friendship, FriendRequest, Room, NudgeMessage)
  Data/            NudgeDbContext: tables, indexes, rules
  Migrations/      EF Core migrations
  Dtos/            What goes over HTTP (never the entities themselves)
  Services/        All the business rules
  Endpoints/       Thin HTTP routes that call services
  Hubs/            SignalR hub for real-time events
  Infrastructure/  Errors, JWT options, emoji counting
  wwwroot/         The web client
Nudge.Api.Tests/   Integration tests
docs/WALKTHROUGH.md  Why the code is built this way
```
