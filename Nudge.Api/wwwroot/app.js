// Nudge web client. Plain JavaScript: no build step, served by the API from wwwroot.
"use strict";

const TOKEN_KEY = "nudge.token";
const QUICK_EMOJIS = ["👋", "❤️", "🥰", "🤗", "😘", "😂", "🔥", "✨", "🌙", "☕", "🎉", "🙏"];

const state = {
  token: localStorage.getItem(TOKEN_KEY),
  me: null,
  friends: [],
  requests: { incoming: [], outgoing: [] },
  rooms: [],
  presets: [],
  openRoomId: null,
  nudgeTarget: null,      // { toUserId } or { roomId }
  pickedEmojis: [],
  connection: null
};

const $ = (sel) => document.querySelector(sel);
const el = (tag, props = {}, ...children) => {
  const node = Object.assign(document.createElement(tag), props);
  node.append(...children.filter((c) => c != null));
  return node;
};

// ---------- API helper ----------

class ApiError extends Error {}

async function api(method, path, body) {
  const headers = { "Content-Type": "application/json" };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;

  const res = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });

  if (res.status === 401 && state.token) {
    logout();
    throw new ApiError("Your session expired. Please log in again.");
  }
  if (!res.ok) {
    // The API returns ProblemDetails: { title, detail, status }.
    const problem = await res.json().catch(() => ({}));
    throw new ApiError(problem.detail || problem.title || `Request failed (${res.status})`);
  }
  return res.status === 204 ? null : res.json();
}

// ---------- Auth ----------

let authMode = "login";

function setAuthMode(mode) {
  authMode = mode;
  document.querySelectorAll("#auth-form .tab").forEach((t) => t.classList.toggle("active", t.dataset.mode === mode));
  document.querySelectorAll("[data-register-only]").forEach((n) => (n.hidden = mode !== "register"));
  $("#auth-submit").textContent = mode === "login" ? "Log in" : "Create account";
  $("#auth-form [name=password]").autocomplete = mode === "login" ? "current-password" : "new-password";
  $("#auth-error").textContent = "";
}

async function submitAuth(event) {
  event.preventDefault();
  const form = new FormData(event.target);
  const username = form.get("username").trim();
  const password = form.get("password");
  $("#auth-error").textContent = "";

  try {
    const result = authMode === "login"
      ? await api("POST", "/api/auth/login", { username, password })
      : await api("POST", "/api/auth/register", { username, displayName: form.get("displayName").trim() || username, password });
    state.token = result.token;
    localStorage.setItem(TOKEN_KEY, result.token);
    event.target.reset();
    await start();
  } catch (err) {
    $("#auth-error").textContent = err.message;
  }
}

function logout() {
  state.token = null;
  state.me = null;
  localStorage.removeItem(TOKEN_KEY);
  state.connection?.stop();
  state.connection = null;
  $("#app-view").hidden = true;
  $("#auth-view").hidden = false;
}

// ---------- Startup ----------

async function start() {
  if (!state.token) {
    $("#auth-view").hidden = false;
    return;
  }
  try {
    state.me = await api("GET", "/api/me");
  } catch {
    logout();
    return;
  }
  $("#auth-view").hidden = true;
  $("#app-view").hidden = false;
  renderMe();
  updateNotifyButton();

  state.presets = await api("GET", "/api/moods/presets");
  await Promise.all([loadFriends(), loadRequests(), loadRooms(), loadInbox()]);
  connectRealtime();
}

// ---------- Real-time (SignalR) ----------

function connectRealtime() {
  // The browser can't put an Authorization header on a WebSocket, so SignalR sends the
  // token as ?access_token=... and the server reads it only for /hubs paths.
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/nudge", { accessTokenFactory: () => state.token })
    .withAutomaticReconnect()
    .build();

  connection.on("NudgeReceived", onNudgeReceived);
  connection.on("MoodChanged", (user) => {
    const friend = state.friends.find((f) => f.id === user.id);
    if (friend) {
      friend.mood = user.mood;
      renderFriends();
    }
  });
  connection.on("FriendsChanged", () => Promise.all([loadFriends(), loadRequests()]));
  connection.on("RoomsChanged", async () => {
    await loadRooms();
    if (state.openRoomId) await openRoom(state.openRoomId, { quiet: true });
  });
  connection.onreconnected(() => Promise.all([loadFriends(), loadRequests(), loadRooms(), loadInbox()]));

  connection.start().catch((err) => console.warn("Real-time connection failed", err));
  state.connection = connection;
}

function onNudgeReceived(nudge) {
  const where = nudge.roomName ? ` in ${nudge.roomName}` : "";
  const title = `${nudge.from.displayName} nudged you${where}`;
  const body = [nudge.emojis, nudge.note].filter(Boolean).join(" ") || "👋";

  // 1. Vibrate. Works on Android Chrome over HTTPS; iPhones ignore it.
  if ("vibrate" in navigator) navigator.vibrate([200, 100, 200]);

  // 2. System notification, if the page is in the background and permission was granted.
  if ("Notification" in window && Notification.permission === "granted" && document.hidden) {
    new Notification(title, { body, tag: `nudge-${nudge.id}` });
  }

  // 3. Always show an in-app toast and give the screen a little shake.
  toast(title, nudge.emojis || "👋", nudge.note);
  document.body.classList.remove("shake");
  void document.body.offsetWidth; // restart the animation
  document.body.classList.add("shake");

  loadInbox();
  if (nudge.roomId && nudge.roomId === state.openRoomId) loadRoomFeed(nudge.roomId);
}

function toast(title, emojis, note) {
  const node = el("div", { className: "toast" },
    emojis ? el("div", { className: "emojis" }, emojis) : null,
    el("strong", {}, title),
    note ? el("div", {}, note) : null);
  $("#toasts").append(node);
  setTimeout(() => node.remove(), 5000);
}

function updateNotifyButton() {
  const button = $("#notify-button");
  button.hidden = !("Notification" in window) || Notification.permission !== "default";
}

// ---------- Me and moods ----------

function moodLabel(mood) {
  return mood ? `${mood.emoji} ${mood.text}`.trim() : "No mood set";
}

function renderMe() {
  $("#my-name").textContent = state.me.displayName;
  $("#my-mood-emoji").textContent = state.me.mood?.emoji ?? "🙂";
  $("#my-mood-text").textContent = state.me.mood?.text || (state.me.mood ? "" : "Set mood");
}

function openMoodDialog() {
  const grid = $("#mood-presets");
  grid.replaceChildren(...state.presets.map((preset) => {
    const button = el("button", { type: "button" }, el("span", {}, preset.emoji), el("span", {}, preset.text));
    if (state.me.mood?.emoji === preset.emoji && state.me.mood?.text === preset.text) button.classList.add("picked");
    button.onclick = () => saveMood(preset.emoji, preset.text);
    return button;
  }));
  $("#mood-emoji").value = state.me.mood?.emoji ?? "";
  $("#mood-text").value = state.me.mood?.text ?? "";
  $("#mood-error").textContent = "";
  $("#mood-dialog").showModal();
}

async function saveMood(emoji, text) {
  try {
    state.me = await api("PUT", "/api/me/mood", { emoji, text });
    renderMe();
    $("#mood-dialog").close();
  } catch (err) {
    $("#mood-error").textContent = err.message;
  }
}

// ---------- Friends ----------

async function loadFriends() {
  state.friends = await api("GET", "/api/friends");
  renderFriends();
}

function personRow(user, ...actions) {
  return el("li", { className: "person" },
    el("div", { className: "avatar", title: moodLabel(user.mood) }, user.mood?.emoji ?? "🙂"),
    el("div", { className: "info" },
      el("div", { className: "name" }, user.displayName),
      el("div", { className: "mood" }, `@${user.username} · ${moodLabel(user.mood)}`)),
    ...actions);
}

function renderFriends() {
  $("#friend-list").replaceChildren(...state.friends.map((friend) => {
    const nudge = el("button", { className: "primary", textContent: "Nudge 👋" });
    nudge.onclick = () => openNudgeDialog({ toUserId: friend.id }, friend.displayName, friend.mood);
    const remove = el("button", { className: "ghost danger", textContent: "Remove", title: `Remove ${friend.displayName}` });
    remove.onclick = async () => {
      if (!confirm(`Remove ${friend.displayName} from your friends?`)) return;
      await run(() => api("DELETE", `/api/friends/${friend.id}`));
      await loadFriends();
    };
    return personRow(friend, nudge, remove);
  }));
  $("#no-friends").hidden = state.friends.length > 0;
}

async function loadRequests() {
  state.requests = await api("GET", "/api/friends/requests");
  const { incoming, outgoing } = state.requests;

  const rows = [
    ...incoming.map((r) => {
      const accept = el("button", { className: "primary", textContent: "Accept" });
      accept.onclick = () => run(async () => {
        await api("POST", `/api/friends/requests/${r.id}/accept`);
        await Promise.all([loadFriends(), loadRequests()]);
      });
      const decline = el("button", { className: "ghost", textContent: "Decline" });
      decline.onclick = () => run(async () => {
        await api("DELETE", `/api/friends/requests/${r.id}`);
        await loadRequests();
      });
      return personRow(r.from, accept, decline);
    }),
    ...outgoing.map((r) => {
      const cancel = el("button", { className: "ghost", textContent: "Cancel" });
      cancel.onclick = () => run(async () => {
        await api("DELETE", `/api/friends/requests/${r.id}`);
        await loadRequests();
      });
      return personRow(r.to, el("span", { className: "muted small" }, "Pending"), cancel);
    })
  ];

  const box = $("#requests");
  box.replaceChildren();
  if (rows.length) box.append(el("h2", {}, "Friend requests"), el("ul", { className: "list" }, ...rows));

  const badge = $("#request-badge");
  badge.hidden = incoming.length === 0;
  badge.textContent = incoming.length;
}

async function addFriend(event) {
  event.preventDefault();
  const input = event.target.username;
  await run(async () => {
    const result = await api("POST", "/api/friends/requests", { username: input.value.trim() });
    toast(result.becameFriends ? "You're now friends! 🎉" : "Friend request sent");
    input.value = "";
    await Promise.all([loadFriends(), loadRequests()]);
  });
}

// ---------- Nudge dialog ----------

function openNudgeDialog(target, name, mood) {
  state.nudgeTarget = target;
  state.pickedEmojis = [];
  $("#nudge-title").textContent = `Nudge ${name}`;

  const banner = $("#nudge-mood");
  banner.hidden = !!target.roomId;
  banner.textContent = mood ? `${name} is feeling ${moodLabel(mood)}` : `${name} hasn't set a mood`;

  $("#emoji-picks").replaceChildren(...QUICK_EMOJIS.map((emoji) => {
    const button = el("button", { type: "button", textContent: emoji });
    button.onclick = () => toggleEmoji(emoji, button);
    return button;
  }));
  $("#emoji-custom").value = "";
  $("#nudge-note").value = "";
  $("#note-count").textContent = "0 / 140";
  $("#nudge-error").textContent = "";
  $("#nudge-dialog").showModal();
}

function toggleEmoji(emoji, button) {
  const i = state.pickedEmojis.indexOf(emoji);
  if (i >= 0) state.pickedEmojis.splice(i, 1);
  else if (state.pickedEmojis.length < 2) state.pickedEmojis.push(emoji);
  else return; // the server allows at most 2
  button.classList.toggle("picked", i < 0);
}

async function sendNudge(event) {
  event.preventDefault();
  const emojis = (state.pickedEmojis.join("") + $("#emoji-custom").value.trim()) || null;
  const note = $("#nudge-note").value.trim() || null;
  try {
    await api("POST", "/api/nudges", { ...state.nudgeTarget, note, emojis });
    $("#nudge-dialog").close();
    toast("Nudge sent", emojis);
    if (state.nudgeTarget.roomId) await loadRoomFeed(state.nudgeTarget.roomId);
  } catch (err) {
    $("#nudge-error").textContent = err.message;
  }
}

// ---------- Rooms ----------

async function loadRooms() {
  state.rooms = await api("GET", "/api/rooms");
  $("#room-list").replaceChildren(...state.rooms.map((room) => {
    const row = el("li", { className: "person clickable" },
      el("div", { className: "avatar" }, "🏠"),
      el("div", { className: "info" },
        el("div", { className: "name" }, room.name),
        el("div", { className: "mood" }, room.members.map((m) => `${m.mood?.emoji ?? "🙂"} ${m.displayName}`).join(", "))));
    row.onclick = () => openRoom(room.id);
    return row;
  }));
  $("#no-rooms").hidden = state.rooms.length > 0;

  if (state.openRoomId && !state.rooms.some((r) => r.id === state.openRoomId)) closeRoom();
}

async function openRoom(id, { quiet = false } = {}) {
  let room;
  try {
    room = await api("GET", `/api/rooms/${id}`);
  } catch (err) {
    if (!quiet) toast(err.message);
    closeRoom();
    return;
  }
  state.openRoomId = id;
  $("#room-detail").hidden = false;
  $("#room-title").textContent = room.name;

  const isOwner = room.ownerId === state.me.id;
  $("#room-members").replaceChildren(...room.members.map((m) => {
    const chip = el("span", { className: "chip", title: moodLabel(m.mood) },
      `${m.mood?.emoji ?? "🙂"} ${m.displayName}${m.id === room.ownerId ? " 👑" : ""}`);
    if (isOwner && m.id !== state.me.id) {
      const kick = el("button", { className: "ghost small", textContent: "✕", title: `Remove ${m.displayName}` });
      kick.onclick = () => run(async () => {
        await api("DELETE", `/api/rooms/${id}/members/${m.id}`);
        await openRoom(id);
      });
      chip.append(kick);
    }
    return chip;
  }));

  const select = $("#room-add-select");
  const candidates = state.friends.filter((f) => !room.members.some((m) => m.id === f.id));
  select.replaceChildren(el("option", { value: "" }, candidates.length ? "Add a friend…" : "All your friends are here"),
    ...candidates.map((f) => el("option", { value: f.id }, f.displayName)));
  select.disabled = candidates.length === 0;

  $("#room-nudge").onclick = () => openNudgeDialog({ roomId: id }, room.name, null);
  await loadRoomFeed(id);
}

function closeRoom() {
  state.openRoomId = null;
  $("#room-detail").hidden = true;
}

async function loadRoomFeed(id) {
  const nudges = await api("GET", `/api/rooms/${id}/nudges`);
  $("#room-feed").replaceChildren(...nudges.map((n) => feedItem(n)));
  if (!nudges.length) $("#room-feed").append(el("li", { className: "muted" }, "No nudges in this room yet."));
}

function openRoomDialog() {
  $("#room-name").value = "";
  $("#room-error").textContent = "";
  $("#room-friend-picks").replaceChildren(...state.friends.map((f) =>
    el("label", { className: "row" }, el("input", { type: "checkbox", value: f.id, style: "width:auto" }), `${f.mood?.emoji ?? "🙂"} ${f.displayName}`)));
  if (!state.friends.length) $("#room-friend-picks").append(el("p", { className: "muted small" }, "Add some friends first, or make a room just for you."));
  $("#room-dialog").showModal();
}

async function createRoom(event) {
  event.preventDefault();
  const memberIds = [...document.querySelectorAll("#room-friend-picks input:checked")].map((i) => Number(i.value));
  try {
    const room = await api("POST", "/api/rooms", { name: $("#room-name").value.trim(), memberIds });
    $("#room-dialog").close();
    await loadRooms();
    await openRoom(room.id);
  } catch (err) {
    $("#room-error").textContent = err.message;
  }
}

// ---------- Inbox ----------

function feedItem(n, { markable = false } = {}) {
  const when = new Date(n.sentAt).toLocaleString();
  const where = n.roomName ? ` in ${n.roomName}` : "";
  const item = el("li", { className: markable && !n.readAt ? "unread" : "" },
    el("div", { className: "emojis" }, n.emojis || "👋"),
    n.note ? el("div", {}, n.note) : null,
    el("div", { className: "meta" }, `${n.from.mood?.emoji ?? ""} ${n.from.displayName}${where} · ${when}`));
  if (markable && !n.readAt) {
    item.style.cursor = "pointer";
    item.title = "Mark as read";
    item.onclick = () => run(async () => {
      await api("POST", `/api/nudges/${n.id}/read`);
      await loadInbox();
    });
  }
  return item;
}

async function loadInbox() {
  const nudges = await api("GET", "/api/nudges/inbox");
  $("#inbox-list").replaceChildren(...nudges.map((n) => feedItem(n, { markable: true })));
  $("#no-inbox").hidden = nudges.length > 0;
  const unread = nudges.filter((n) => !n.readAt).length;
  $("#inbox-badge").hidden = unread === 0;
  $("#inbox-badge").textContent = unread;
}

// ---------- Helpers and wiring ----------

async function run(action) {
  try {
    await action();
  } catch (err) {
    toast(err.message);
  }
}

function showTab(name) {
  document.querySelectorAll(".main-tabs .tab").forEach((t) => t.classList.toggle("active", t.dataset.tab === name));
  for (const tab of ["friends", "rooms", "inbox"]) $(`#tab-${tab}`).hidden = tab !== name;
}

document.querySelectorAll("#auth-form .tab").forEach((t) => (t.onclick = () => setAuthMode(t.dataset.mode)));
$("#auth-form").addEventListener("submit", submitAuth);
$("#logout-button").onclick = logout;
$("#notify-button").onclick = async () => {
  await Notification.requestPermission();
  updateNotifyButton();
};
document.querySelectorAll(".main-tabs .tab").forEach((t) => (t.onclick = () => showTab(t.dataset.tab)));

document.querySelectorAll("[data-close]").forEach((b) => (b.onclick = () => b.closest("dialog").close()));
$("#add-friend-form").addEventListener("submit", addFriend);

$("#mood-button").onclick = openMoodDialog;
$("#mood-form").addEventListener("submit", (event) => {
  event.preventDefault();
  saveMood($("#mood-emoji").value.trim(), $("#mood-text").value.trim());
});
$("#mood-clear").onclick = () => run(async () => {
  state.me = await api("DELETE", "/api/me/mood");
  renderMe();
  $("#mood-dialog").close();
});

$("#nudge-form").addEventListener("submit", sendNudge);
$("#nudge-note").oninput = (e) => ($("#note-count").textContent = `${e.target.value.length} / 140`);

$("#new-room-button").onclick = openRoomDialog;
$("#room-form").addEventListener("submit", createRoom);
$("#room-close").onclick = closeRoom;
$("#room-leave").onclick = () => run(async () => {
  if (!confirm("Leave this room?")) return;
  await api("DELETE", `/api/rooms/${state.openRoomId}/members/${state.me.id}`);
  closeRoom();
  await loadRooms();
});
$("#room-add-select").onchange = (e) => run(async () => {
  if (!e.target.value) return;
  await api("POST", `/api/rooms/${state.openRoomId}/members`, { userId: Number(e.target.value) });
  await openRoom(state.openRoomId);
});

start();
