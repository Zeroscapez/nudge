# Walkthrough: what changed and why

You asked me to build the full app. Your rule is that when I do work for you, I explain what was wrong before and why the new version is right. This file does that. Read it next to the code, one section at a time. Each section starts with what your code did, then explains what replaced it and why.

Your version was mid-way through step 1, so most of the "before" isn't really wrong. It's unfinished, or it was built for a smaller app. I say which is which as I go.

---

## 1. Accounts: from "anyone can be anyone" to real logins

**Before.** `POST /users` took a username and display name. There was no password. Every other route took the user id in the URL (`/users/{id}/friends`). Anyone could type `/users/2/friends` and act as user 2.

**After.** Users register with a password, log in, and get a **token**. Every other route reads *who you are* from that token, never from the URL. Look at `ClaimsPrincipalExtensions.GetUserId()` in `Infrastructure/`: endpoints call `http.User.GetUserId()`, so changing the URL can't make you someone else.

### "Encryption" vs. hashing

You asked for account creation "with encryption." What you actually want for passwords is **hashing**, which is different, and this difference comes up in interviews:

- **Encryption** can be reversed if you have the key. If we encrypted passwords, anyone who stole the key and the database would get every password back.
- **Hashing** is one-way. We store `hash(password)`. At login we hash what you typed and compare the two. Nobody, including us, can turn the hash back into the password.

`AuthService` uses ASP.NET Core's `PasswordHasher<User>`. It uses **PBKDF2** with a **random salt** per user and many thousands of rounds:

- The **salt** means two people with the password `password123` get different hashes. A precomputed table of common-password hashes is then useless.
- The **rounds** make each guess slow. That's no problem for one login, but it's a big problem for an attacker trying billions of guesses.

`PasswordHasher` also returns `SuccessRehashNeeded` when it's time to upgrade old hashes. `LoginAsync` handles that by quietly re-hashing the password.

Where encryption *does* happen is in transit: **HTTPS** encrypts everything between the browser and the server, including the password at login. That's why the README says to deploy behind HTTPS.

There's a test for this: `Passwords_are_stored_hashed_not_in_plain_text`.

### Tokens (JWT)

After login the API returns a **JSON Web Token**. It's a small signed note saying "this is user 7, valid until next Friday." The browser sends it on every request as `Authorization: Bearer <token>`.

- **Signed, not encrypted.** Anyone can read what's inside a JWT. Nobody can *change* it without the secret key, because the signature would no longer match. So we only put the user id and username in it, never anything private.
- **The key is a secret.** `Program.cs` refuses to start if `Jwt:Key` is missing or shorter than 32 characters. `appsettings.Development.json` has a dev-only key so it runs on your machine. In production you set `Jwt__Key` as an environment variable (the double underscore means "inside the `Jwt` section"). In step 4 this will come from a GitHub Actions secret. **Never commit a real key.**
- `MapInboundClaims = false` keeps the claim names as they are in the token (`sub`). Otherwise .NET renames them to long URLs like `http://schemas.xmlsoap.org/...`, which is a classic source of confusion.

### Small details that matter

- **Usernames are lowercased** before saving and searching, so `Zack` and `zack` can't both exist, and you can log in as either.
- **Wrong password and unknown username give the same error.** If they were different, an attacker could test which usernames exist. Test: `Wrong_password_and_unknown_user_give_the_same_401`.
- **Rate limiting.** Login and register are limited to 20 attempts per IP per minute, which slows down password guessing. Nudges are limited to 30 per user per minute, so nobody can spam a friend's phone. Both limits are in `appsettings.json`.

---

## 2. Friends: from instant add to requests

**Before.** The plan was `POST /users/{id}/friends/{friendId}`, which instantly made two people friends. In a real app, that means anyone could add you and start buzzing your phone.

**After.** There's a `FriendRequest` table:

1. Zack sends a request by **username**, not id, because people know usernames.
2. Ana sees it as incoming and can **accept** or **decline**. Zack can **cancel** it while it's pending.
3. Accepting creates the friendship and deletes the request.
4. If Ana had already sent Zack a request and Zack sends one back, they just become friends. Two pending requests between the same people would be silly. Test: `Asking_someone_who_already_asked_you_makes_you_friends`.

**Removing a friend** deletes both directions.

### Why two rows per friendship

`Friendship` stores **both** `Zack → Ana` and `Ana → Zack`. That looks like duplication, but it makes every question a simple one-direction query:

```csharp
db.Friendships.Where(f => f.UserId == me)                        // my friends
db.Friendships.AnyAsync(f => f.UserId == a && f.FriendId == b)   // are we friends?
```

With a single row you'd need `(UserId == a && FriendId == b) || (UserId == b && FriendId == a)` everywhere, and sooner or later someone forgets the second half. A **unique index** on `(UserId, FriendId)` stops duplicates at the database level, even if the code has a bug.

---

## 3. Moods

`User` has three new columns: `MoodEmoji`, `MoodText` and `MoodUpdatedAt`. There's no separate "custom mood" feature, because a preset is just a ready-made `{ emoji, text }` pair. **Custom moods use exactly the same endpoint** (`PUT /api/me/mood`). One code path means one place to validate and one place to have bugs.

Validation: exactly one emoji and up to 30 characters of text.

When you change your mood, `MoodService` tells your friends over SignalR (`MoodChanged`), so their friend list updates without a refresh. The nudge dialog shows "Ana is feeling 🎮 Grinding ranked" before you send.

### Counting emojis is harder than it looks

"Max 2 emojis" sounds like `emojis.Length <= 2`. It isn't:

| You see | `string.Length` | Why |
|---|---|---|
| 😀 | 2 | Emojis are outside the basic range, so C# stores each one as two `char`s (a "surrogate pair"). |
| 👍🏽 | 4 | A thumbs-up plus a skin-tone modifier. |
| ❤️ | 2 | A heart plus an invisible "show as emoji" marker. |
| 👨‍👩‍👧 | 8 | Three people glued together with invisible joiners. |

`EmojiValidator` uses `StringInfo`, which counts **text elements**: what a human would call "one character." So 👍🏽❤️ counts as 2 and is allowed. Test: `A_nudge_with_a_note_and_two_emojis_lands_in_the_inbox`.

---

## 4. Nudges

**Before.** `NudgeMessage` had `Message` with `[MaxLength(140)]`, and `NudgeService.SendAsync` threw `NotImplementedException`.

**After.**

- `Message` became **`Note`**, plus a separate **`Emojis`** field (up to 2). Keeping them apart means the client can show the emojis big and the note small, and we can validate each one properly. Words in the emoji field get a 400 with "Put words in the note."
- **`[MaxLength(140)]` only limits the database column.** Your attribute was right, but on its own it gives the user a database error instead of a friendly message. `NudgeService` checks the length itself and returns a 400 with a clear reason. The constant `NudgeMessage.MaxNoteLength` is used in both places so they can't drift apart.
- A nudge goes to **either** one friend (`ToUserId`) **or** a room (`RoomId`), never both and never neither. The service checks this, and so does a database **check constraint** (`CK_Nudge_OneTarget` in `NudgeDbContext`). That's the "belt and braces" approach: code catches it with a nice message, and the database guarantees it even if the code is wrong.
- Nudging a non-friend returns **403 Forbidden**. Your plan said 400, but 403 is more precise: the request is well-formed, you're just not allowed to do it.

### Getting it to the other person: SignalR

Saving a nudge in the database isn't enough; Ana needs to *feel* it. SignalR keeps a live connection (usually a WebSocket) open between each browser and the server.

- `Hubs/NudgeHub.cs` is almost empty. Browsers connect to it, but the server does all the sending.
- `[Authorize]` on the hub means you need a token to connect.
- `SubUserIdProvider` tells SignalR "a connection's user id is the `sub` claim in its token." Then the server can say `Clients.User("7").NudgeReceived(...)`, and it reaches every tab and device user 7 has open.
- Browsers can't put an `Authorization` header on a WebSocket, so the SignalR client sends the token as `?access_token=...` in the URL. `Program.cs` accepts that **only for `/hubs` paths**, so tokens don't end up in URLs and logs for the rest of the API.

### Why there's an `INudgeNotifier` interface

Services don't call SignalR directly. They call `INudgeNotifier`, and `SignalRNudgeNotifier` is the real implementation. This is the reason for interfaces we talked about in lesson 1:

- **Tests** could swap in a fake notifier.
- Later you could add **real push notifications**, which work even when the browser is closed, by writing a second implementation, without touching `NudgeService` at all.

### Vibration and notifications in the browser

In `wwwroot/app.js`, `onNudgeReceived` does three things:

1. `navigator.vibrate([200, 100, 200])`: buzz, pause, buzz. This works in Android Chrome. **iPhones don't support it at all.** Nothing we can do about that from a web page.
2. A system **notification**, if you've clicked "Allow alerts" and the tab is in the background.
3. An in-app **toast** and a little screen shake, which always works.

Browsers only allow vibration and notifications on **HTTPS** pages (or `localhost`). So to try it on your phone you'll need step 4's deployment, or a tunnel like `ngrok`.

---

## 5. Private rooms

New tables: `Room` and `RoomMember`. `RoomMember` uses the pair `(RoomId, UserId)` as its key, so the same person can't be in a room twice.

The rules, all in `RoomService`:

- You can only put **your friends** in a room.
- Any member can add **their own** friends.
- Only the **owner** can remove other people. Anyone can leave.
- If the owner leaves, the longest-standing member becomes owner. If the last person leaves, the room is deleted. Test: `When_the_owner_leaves_the_next_member_takes_over`.
- A room nudge goes to everyone in the room except the sender. It shows up in the room's feed, not in anyone's inbox, which stays for direct nudges.

### Why outsiders get 404, not 403

If you ask for a room you're not in, you get **404 Not Found**, not 403 Forbidden. A 403 would confirm "that room exists, you just can't see it," and someone could count rooms by trying ids. A 404 reveals nothing. "Private" should mean private.

---

## 6. Errors: from `null` checks to exceptions with meaning

**Before.**

```csharp
await users.CreateAsync(request) is { } user ? Results.Created(...) : Results.BadRequest("Username taken")
```

The endpoint guessed *why* `null` came back. With one failure reason that works. With five (bad username, short password, taken, …) it doesn't.

**After.** Services throw specific exceptions from `Infrastructure/AppExceptions.cs`:

| Exception | Status |
|---|---|
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |

One `AppExceptionHandler` turns them into a standard **ProblemDetails** response:

```json
{ "status": 409, "title": "Conflict", "detail": "That username is taken." }
```

So the endpoints have no `try`/`catch` and no guessing. They're one line each, which is the "thin endpoints" rule from lesson 1. The web client just shows `detail` to the user.

Any *other* exception (a real bug) becomes a plain 500. The handler only sends back the details of our own `AppException`s, so internal errors never leak to the user.

---

## 7. Smaller fixes worth knowing

- **Interfaces return DTOs, not entities.** `IUserService` returned `User`. Once `User` has `PasswordHash`, returning it would send the hash to the browser. Every response is now a record from `Dtos/`. `UserResponse` has no password field, so it's impossible to leak by accident.
- **`TimeProvider` instead of `DateTime.UtcNow`.** Services ask DI for a `TimeProvider`. In tests you could freeze time. It also makes "now" consistent within a request.
- **Dates come back in UTC.** SQLite doesn't remember that a date was UTC, so dates came back without the `Z` and browsers treated them as local time. `NudgeDbContext.ConfigureConventions` marks every `DateTime` as UTC on the way out.
- **`Select` straight into DTOs.** The inbox and room feed queries project into `NudgeResponse` inside the query, so EF only fetches the columns the response needs instead of whole rows.
- **`using` cleanup.** `using Nudge.Api.Data;` in the interface files wasn't used. An interface shouldn't know about the database at all.
- **`UserService` had no `namespace`**, so it sat in the global namespace. Everything now lives under `Nudge.Api.*`.

---

## 8. The database was reset (do this on your machine)

The data model changed a lot: new tables, `Message` renamed to `Note`, new columns. Rather than ship a pile of migrations on top of an unfinished schema, I deleted the old migrations and made one clean `InitialCreate`.

**Before you run the new code, delete your local `nudge.db`** (and any `nudge.db-shm`/`nudge.db-wal`). It was made by the old migrations and won't match. The app creates a fresh one on startup, because `Program.cs` calls `Database.Migrate()`.

`nudge.db` was also committed to git before. A database file is data, not code, so it's now in `.gitignore` (along with its `-shm`/`-wal` side files).

Going forward: **never edit or delete a migration that's already been applied somewhere that matters**, like production. Add a new one. Resetting was only OK here because nothing was live yet.

---

## 9. Tests (preview of step 3)

`Nudge.Api.Tests` has 28 **integration tests**. They start the real API in memory with `WebApplicationFactory<Program>` and make real HTTP calls:

- `NudgeApiFactory` gives each test class its own temporary SQLite file, a test JWT key, and high rate limits. Tests never touch your `nudge.db`.
- `TestClient` is a tiny helper: register a user, befriend someone, send a nudge. It keeps the tests readable, like a story.
- `RealtimeTests` connects a real SignalR client as Ana and checks that Zack's nudge arrives.
- `public partial class Program;` at the bottom of `Program.cs` is what lets the test project see `Program`.

Run them with `dotnet test`. In step 3, GitHub Actions will run this same command on every push.

---

## How to get this onto your machine

Your folder hasn't been touched. Everything is on the branch `claude/full-nudge-app`.

```bash
cd F:\Coding\Projects\Learning\Step1BackEnd\Nudge
git fetch origin
git switch claude/full-nudge-app
del Nudge.Api\nudge.db
cd Nudge.Api
dotnet run
```

Then open http://localhost:5173 in two browser windows (one normal, one private) and register two accounts.

### Suggested reading order

1. `Models/` and `Data/NudgeDbContext.cs`, to see the shape of the data.
2. `Services/AuthService.cs`, then `FriendService.cs`, then `NudgeService.cs`, where the rules live.
3. `Endpoints/`. Notice how small they are.
4. `Program.cs`, which wires it all together.
5. `wwwroot/app.js`, starting from `connectRealtime` and `onNudgeReceived`.
6. `Nudge.Api.Tests/`, then try breaking a rule in a service and watch a test go red.
