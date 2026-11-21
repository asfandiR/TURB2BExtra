const STORAGE_KEY = "TURB2BExtra.session";
const publicScreen = document.querySelector("#public-screen");
const loginScreen = document.querySelector("#login-screen");
const appScreen = document.querySelector("#app-screen");
const loginError = document.querySelector("#login-error");
const loginForm = document.querySelector("#login-form");
const toast = document.querySelector("#toast");
let session = readSession();
let activePickupMode = "hotel";
let cachedTrips = [];
let cachedTours = [];
let currencyRates = [];
let toastTimer;

const roleLabels = {
  AgencyGuide: "Гид-продавец",
  AgencyManager: "Менеджер агентства",
  AgencyAccountant: "Бухгалтер агентства",
  AgencyOwner: "Владелец агентства",
  IntermediaryDriver: "Водитель",
  IntermediaryDispatcher: "Диспетчер",
  IntermediaryOwner: "Владелец посредника",
  IntermediaryAccountant: "Бухгалтер посредника",
  IntermediaryStaffSales: "Продавец посредника"
};
const navIcons = { overview: "⌂", "new-booking": "＋", bookings: "▤", tours: "◇", "new-trip": "◷" };

function readSession() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
  } catch {
    return null;
  }
}

function decodeToken(token) {
  try {
    const payload = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(Array.from(atob(payload), char =>
      `%${char.charCodeAt(0).toString(16).padStart(2, "0")}`).join(""));
    return JSON.parse(json);
  } catch {
    return {};
  }
}

function currentProfile() {
  if (!session?.accessToken) return null;
  const claims = decodeToken(session.accessToken);
  return {
    ...session,
    userId: claims.sub || claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"],
    expiresAt: Number(claims.exp || 0)
  };
}

async function apiFetch(url, options = {}) {
  const headers = new Headers(options.headers || {});
  if (session?.accessToken) headers.set("Authorization", `Bearer ${session.accessToken}`);
  if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  const response = await fetch(url, { ...options, headers });
  if (response.status === 401 && session?.accessToken) {
    logout();
    throw new Error("Сессия завершилась. Войдите в систему ещё раз.");
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.detail || body?.title || body || `Ошибка запроса (${response.status})`);
  }
  if (response.status === 204) return null;
  return response.json();
}

function showToast(message, isError = false) {
  toast.textContent = message;
  toast.classList.toggle("toast-error", isError);
  toast.classList.remove("hidden");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.add("hidden"), 4200);
}

function showLogin() {
  publicScreen.classList.add("hidden");
  loginScreen.classList.remove("hidden");
  appScreen.classList.add("hidden");
}

function showPublic() {
  publicScreen.classList.remove("hidden");
  loginScreen.classList.add("hidden");
  appScreen.classList.add("hidden");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function showApp() {
  const profile = currentProfile();
  if (!profile || profile.expiresAt * 1000 <= Date.now()) {
    logout(false);
    return;
  }
  publicScreen.classList.add("hidden");
  loginScreen.classList.add("hidden");
  appScreen.classList.remove("hidden");
  const role = profile.role;
  document.querySelector("#top-user-name").textContent = session.login || "Пользователь";
  document.querySelector("#top-user-role").textContent = roleLabels[role] || role || "Участник";
  document.querySelector("#user-avatar").textContent = (session.login || "М").slice(0, 1).toUpperCase();

  document.querySelectorAll(".role-view").forEach(view => view.classList.add("hidden"));
  const accountType = session.accountType;
  if (accountType === "Agency") {
    document.querySelector("#agency-view").classList.remove("hidden");
    document.querySelector("#agency-overview").classList.remove("hidden");
    renderNav([
      ["overview", "Обзор", "agency-overview"],
      ["new-booking", "Новый билет", "agency-new-booking"],
      ["bookings", "Мои билеты", "agency-bookings"],
      ["staff", "Сотрудники", "agency-staff"],
      ["partners", "Посредники", "agency-partners"],
      ["warehouse", "Склад", "agency-warehouse"],
      ["notifications", "Уведомления", "agency-notifications"]
    ], "Агентство", roleLabels[role] || role);
    loadAgencyDashboard();
  } else if (role === "IntermediaryDriver") {
    document.querySelector("#driver-view").classList.remove("hidden");
    renderNav([["overview", "Мои выезды", "driver-trips"]], "Посредник", roleLabels[role]);
    loadDriverTrips();
  } else {
    document.querySelector("#intermediary-view").classList.remove("hidden");
    renderNav([
      ["overview", "Панель рейсов", "owner-overview"],
      ["new-trip", "Создать рейс", "intermediary-new-trip"],
      ["tours", "Экскурсии", "intermediary-tours"],
      ["staff", "Сотрудники", "intermediary-staff"],
      ["cash", "Касса продаж", "intermediary-cash"],
      ["warehouse", "Склад", "intermediary-warehouse"],
      ["notifications", "Уведомления", "intermediary-notifications"]
    ], "Посредник", roleLabels[role] || role);
    loadIntermediaryDashboard();
    loadTours();
    loadDrivers();
  }
  document.querySelector("#page-crumb").textContent =
    accountType === "Agency" ? "Обзор" : role === "IntermediaryDriver" ? "Мои выезды" : "Панель рейсов";
}

function renderNav(items, accountName, roleName) {
  document.querySelector("#account-badge").innerHTML =
    `<span class="account-symbol">${accountName === "Агентство" ? "⌂" : "◇"}</span><span class="account-info"><strong>${escapeHtml(accountName)}</strong><small>${escapeHtml(roleName)}</small></span>`;
  const nav = document.querySelector("#main-nav");
  nav.innerHTML = items.map(([view, label, target], index) =>
    `<button class="nav-button${index === 0 ? " active" : ""}" type="button" data-open-view="${view}" data-target="${target}"><span class="nav-symbol">${navIcons[view]}</span>${label}</button>`
  ).join("");
  nav.querySelectorAll("[data-open-view]").forEach(button => button.addEventListener("click", () => openView(button.dataset.openView)));
}

async function openView(view) {
  document.querySelectorAll(".subpage").forEach(page => page.classList.add("hidden"));
  const profile = currentProfile();
  let pageName = "Обзор";
  if (view === "new-booking") {
    document.querySelector("#agency-new-booking").classList.remove("hidden");
    pageName = "Новый билет";
    await populateBookingForm();
  } else if (view === "bookings") {
    document.querySelector("#agency-bookings").classList.remove("hidden");
    pageName = "Мои билеты";
    loadBookings();
  } else if (view === "tours") {
    document.querySelector("#intermediary-tours").classList.remove("hidden");
    pageName = "Экскурсии";
    loadTours();
  } else if (view === "new-trip") {
    document.querySelector("#intermediary-new-trip").classList.remove("hidden");
    pageName = "Создать рейс";
    populateTripForm();
  } else if (["staff", "partners", "warehouse", "cash", "notifications"].includes(view)) {
    pageName = {
      staff: "Сотрудники",
      partners: "Посредники",
      warehouse: "Склад",
      cash: "Касса продаж",
      notifications: "Уведомления"
    }[view];
    await renderWorkspaceWindow(view);
  } else if (profile?.role === "IntermediaryDriver") {
    pageName = "Мои выезды";
    document.querySelector("#driver-view").classList.remove("hidden");
  } else if (profile?.accountType === "Agency") {
    pageName = "Обзор";
    document.querySelector("#agency-view").classList.remove("hidden");
    document.querySelector("#agency-overview").classList.remove("hidden");
  } else {
    pageName = "Панель рейсов";
    document.querySelector("#intermediary-owner-view").classList.remove("hidden");
  }
  document.querySelector("#page-crumb").textContent = pageName;
  document.querySelectorAll(".nav-button").forEach(button =>
    button.classList.toggle("active", button.dataset.openView === view)
  );
  document.querySelector("#sidebar").classList.remove("sidebar-open");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

async function loadAgencyDashboard() {
  try {
    const [bookings, trips, ownerDashboard] = await Promise.all([
      apiFetch("/api/bookings/my"),
      apiFetch("/api/trips/available"),
      apiFetch("/api/dashboard/owner")
    ]);
    cachedTrips = trips;
    const tourists = bookings.reduce((total, item) => total + item.adults + item.children, 0);
    const commission = bookings.reduce((total, item) => total + Number(item.commissionAmount), 0);
    document.querySelector("#booking-count").textContent = bookings.length;
    document.querySelector("#tourist-count").textContent = ownerDashboard.touristCount || tourists;
    document.querySelector("#commission-total").textContent = formatMoney(commission, bookings[0]?.currency || "USD");
    document.querySelector("#available-count").textContent = trips.length;
    renderAvailableTrips(trips);
    renderBookings(document.querySelector("#agency-recent-bookings"), bookings.slice(0, 5));
    renderBookings(document.querySelector("#agency-all-bookings"), bookings);
  } catch (error) {
    showToast(error.message, true);
  }
}

function renderAvailableTrips(trips) {
  const container = document.querySelector("#agency-trip-cards");
  if (!trips.length) {
    container.innerHTML = emptyState("Пока нет доступных рейсов", "Как только посредник добавит рейс, он появится здесь.", "◷");
    return;
  }
  container.innerHTML = trips.slice(0, 6).map(trip => {
    const remaining = Math.max(0, trip.capacity - trip.bookedPeople);
    return `<article class="trip-card">
      <div class="trip-card-top"><h3>${escapeHtml(trip.tourTitle)}</h3><span class="trip-date">${formatDate(trip.departureDate)}</span></div>
      <div class="trip-card-meta"><span>◷ ${escapeHtml(trip.departureTime.slice(0, 5))} · ${escapeHtml(trip.vehicleNumber || "Машина уточняется")}</span><span>♙ ${remaining} свободных мест</span></div>
      <div class="trip-card-bottom"><strong>${formatMoney(trip.publicPrice, trip.currency)}<small>за человека</small></strong><button class="mini-action" type="button" data-book-trip="${trip.id}">Оформить билет</button></div>
    </article>`;
  }).join("");
  container.querySelectorAll("[data-book-trip]").forEach(button => {
    button.addEventListener("click", async () => {
      await openView("new-booking");
      document.querySelector("#booking-trip").value = button.dataset.bookTrip;
    });
  });
}

function renderBookings(container, bookings) {
  if (!bookings.length) {
    container.innerHTML = emptyState("Заявок пока нет", "Оформленные вами билеты будут отображаться здесь.", "▤");
    return;
  }
  container.innerHTML = `<table class="data-table"><thead><tr><th>Билет</th><th>Туристы</th><th>Продажа</th><th>Принято</th><th>Остаток</th><th>Комиссия</th><th>Статус</th></tr></thead><tbody>${bookings.map(item =>
    `<tr><td><strong>${escapeHtml(item.customPickupPoint || `Билет · ${item.id.slice(0, 8)}`)}</strong></td><td>${item.adults + item.children} чел.</td><td>${formatMoney(item.totalSaleAmount ?? item.cashAmount, item.currency)}</td><td>${formatMoney(item.paidAmount ?? item.cashAmount, item.currency)}</td><td>${formatMoney(item.remainingAmount, item.remainingCurrency || item.currency)}</td><td>${formatMoney(item.commissionAmount, item.currency)}</td><td><span class="status-pill">${escapeHtml(item.status)}</span></td></tr>`
  ).join("")}</tbody></table>`;
}

async function populateBookingForm() {
  try {
    const [trips, hotels, rates] = await Promise.all([
      apiFetch("/api/trips/available"),
      apiFetch("/api/hotels"),
      apiFetch("/api/currency-rates")
    ]);
    currencyRates = rates;
    cachedTrips = trips;
    const tripSelect = document.querySelector("#booking-trip");
    tripSelect.innerHTML = `<option value="">Выберите рейс</option>` + trips.map(trip =>
      `<option value="${trip.id}">${escapeHtml(trip.tourTitle)} · ${formatDate(trip.departureDate)}, ${trip.departureTime.slice(0, 5)} · ${formatMoney(trip.publicPrice, trip.currency)}</option>`
    ).join("");
    const hotelSelect = document.querySelector("#booking-hotel");
    hotelSelect.innerHTML = `<option value="">Без привязки к отелю</option>` + hotels.map(hotel =>
      `<option value="${hotel.id}">${escapeHtml(hotel.name)}${hotel.region ? ` · ${escapeHtml(hotel.region)}` : ""}</option>`
    ).join("");
  } catch (error) {
    showToast(error.message, true);
  }
}

async function loadBookings() {
  try {
    const bookings = await apiFetch("/api/bookings/my");
    renderBookings(document.querySelector("#agency-all-bookings"), bookings);
  } catch (error) {
    showToast(error.message, true);
  }
}

async function loadIntermediaryDashboard() {
  try {
    const [trips, ownerDashboard] = await Promise.all([
      apiFetch("/api/intermediary/trips"),
      apiFetch("/api/dashboard/owner")
    ]);
    const people = ownerDashboard.touristCount || trips.reduce((total, item) => total + item.bookedPeople, 0);
    const freeSeats = trips.reduce((total, item) => total + Math.max(0, item.capacity - item.bookedPeople), 0);
    const currencies = new Set(trips.map(item => item.currency));
    document.querySelector("#inter-trip-count").textContent = trips.length;
    document.querySelector("#inter-tourist-count").textContent = people;
    document.querySelector("#inter-capacity").textContent = freeSeats;
    document.querySelector("#inter-revenue").textContent = currencies.size > 1 ? "Разные" : formatMoney(trips.reduce((total, item) => total + Number(item.revenue), 0), trips[0]?.currency || "USD");
    renderIntermediaryTrips(trips);
  } catch (error) {
    showToast(error.message, true);
  }
}

function renderIntermediaryTrips(trips) {
  const container = document.querySelector("#intermediary-trips");
  if (!trips.length) {
    container.innerHTML = emptyState("Расписание пока пустое", "Создайте экскурсию, затем добавьте первый рейс.", "◷");
    return;
  }
  container.innerHTML = `<table class="data-table"><thead><tr><th>Экскурсия</th><th>Дата и время</th><th>Машина</th><th>Загрузка</th><th>Выручка</th><th></th></tr></thead><tbody>${trips.map(trip =>
    `<tr><td><strong>${escapeHtml(trip.tourTitle)}</strong></td><td>${formatDate(trip.departureDate)} · ${trip.departureTime.slice(0, 5)}</td><td>${escapeHtml(trip.vehicleNumber || "—")}</td><td>${trip.bookedPeople} / ${trip.capacity}</td><td>${formatMoney(trip.revenue, trip.currency)}</td><td><button class="mini-action" type="button" data-cost-trip="${trip.id}" data-title="${escapeHtml(trip.tourTitle)}">Расходы</button></td></tr>`
  ).join("")}</tbody></table>`;
  container.querySelectorAll("[data-cost-trip]").forEach(button => button.addEventListener("click", () => openCostPanel(button.dataset.costTrip, button.dataset.title)));
}

async function loadTours() {
  try {
    cachedTours = await apiFetch("/api/tours/my");
    const container = document.querySelector("#tour-list");
    container.innerHTML = cachedTours.length
      ? cachedTours.map(tour => `<div class="tour-row"><div><strong>${escapeHtml(tour.title)}</strong><small>Экскурсия · ${tour.currency}</small></div><span class="tour-price">${formatMoney(tour.publicPrice, tour.currency)}</span></div>`).join("")
      : emptyState("Каталог пуст", "Добавьте экскурсию, чтобы создать рейс.", "◇");
    populateTripForm();
  } catch (error) {
    showToast(error.message, true);
  }
}

async function loadDrivers() {
  try {
    const drivers = await apiFetch("/api/intermediary/drivers");
    const select = document.querySelector("#trip-driver");
    select.innerHTML = `<option value="">Пока не назначен</option>` + drivers.map(driver =>
      `<option value="${driver.id}">${escapeHtml(driver.fullName)}</option>`
    ).join("");
  } catch (error) {
    showToast(error.message, true);
  }
}

function populateTripForm() {
  const select = document.querySelector("#trip-tour");
  if (!select) return;
  select.innerHTML = cachedTours.length
    ? `<option value="">Выберите экскурсию</option>` + cachedTours.map(tour => `<option value="${tour.id}">${escapeHtml(tour.title)} · ${formatMoney(tour.publicPrice, tour.currency)}</option>`).join("")
    : `<option value="">Сначала создайте экскурсию</option>`;
}

async function loadDriverTrips() {
  try {
    const trips = await apiFetch("/api/trips/my");
    const container = document.querySelector("#driver-trips");
    if (!trips.length) {
      container.innerHTML = emptyState("На сегодня и завтра рейсов нет", "Когда диспетчер назначит вас на выезд, он появится здесь.", "◷");
      return;
    }
    container.innerHTML = trips.map(trip => {
      const departure = new Date(trip.departureDate);
      return `<article class="driver-trip-card"><div class="driver-trip-date"><strong>${departure.toLocaleDateString("ru-RU", { day: "2-digit" })}</strong><small>${departure.toLocaleDateString("ru-RU", { month: "long" })}</small></div><div class="driver-trip-info"><h3>${escapeHtml(trip.tourTitle)}</h3><div><span>◷ ${trip.departureTime.slice(0, 5)}</span><span>▣ ${escapeHtml(trip.vehicleNumber || "Машина не указана")}</span></div></div><div class="driver-trip-people">${trip.bookedPeople} туристов<small>из ${trip.capacity} мест</small></div></article>`;
    }).join("");
  } catch (error) {
    showToast(error.message, true);
  }
}

async function openCostPanel(tripId, title) {
  document.querySelector("#expense-trip-id").value = tripId;
  document.querySelector("#cost-trip-title").textContent = title;
  const panel = document.querySelector("#trip-cost-panel");
  panel.classList.remove("hidden");
  try {
    const cost = await apiFetch(`/api/trips/${tripId}/cost`);
    const currency = cachedTrips.find(trip => trip.id === tripId)?.currency || "USD";
    document.querySelector("#cost-summary").innerHTML = [
      ["Туристы", `${cost.peopleCount} чел.`],
      ["Расходы на рейс", formatMoney(cost.perTripCost, currency)],
      ["Расходы на людей", formatMoney(cost.perPersonCost, currency)],
      ["Итого себестоимость", formatMoney(cost.totalCost, currency)],
      ["Выручка", formatMoney(cost.revenue, currency)],
      ["Маржа", formatMoney(cost.margin, currency)]
    ].map(([label, value], index) => `<div class="cost-cell${index === 5 ? " highlight" : ""}"><small>${label}</small><strong>${value}</strong></div>`).join("");
    panel.scrollIntoView({ behavior: "smooth", block: "start" });
  } catch (error) {
    showToast(error.message, true);
  }
}

async function renderWorkspaceWindow(view) {
  const existing = document.querySelector(`#workspace-${view}`);
  if (existing) existing.remove();

  const section = document.createElement("section");
  section.id = `workspace-${view}`;
  section.className = "subpage";
  section.innerHTML = `<div class="back-row"><button class="text-button" data-open-view="overview">← К обзору</button></div>
    <div class="page-heading compact"><div><span class="eyebrow">РАБОЧЕЕ ОКНО</span><h1 id="workspace-title">${view === "cash" ? "Касса продаж" : view === "notifications" ? "Уведомления" : view === "warehouse" ? "Склад" : view === "partners" ? "Посредники" : "Сотрудники"}<span class="heading-period">.</span></h1></div></div>
    <div id="workspace-content" class="table-card"></div>`;
  document.querySelector(".content-area").prepend(section);
  section.querySelector("[data-open-view]").addEventListener("click", () => openView("overview"));

  const container = section.querySelector("#workspace-content");
  try {
    if (view === "notifications") {
      const items = await apiFetch("/api/notifications");
      container.innerHTML = items.length ? `<div class="tour-list">${items.map(item =>
        `<div class="tour-row"><div><strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.category)} · ${formatDate(item.createdAt)}</small></div><span class="status-pill">${item.isRead ? "Прочитано" : "Новое"}</span></div>`
      ).join("")}</div>` : emptyState("Уведомлений пока нет", "Новые события появятся здесь.", "◎");
    } else if (view === "warehouse") {
      const items = await apiFetch("/api/warehouse");
      container.innerHTML = items.length ? `<table class="data-table"><thead><tr><th>Позиция</th><th>Себестоимость</th><th>Цена продажи</th><th>Остаток</th></tr></thead><tbody>${items.map(item =>
        `<tr><td><strong>${escapeHtml(item.title)}</strong></td><td>${formatMoney(item.costPrice, item.currency)}</td><td>${formatMoney(item.salePrice, item.currency)}</td><td>${item.stockQty}</td></tr>`
      ).join("")}</tbody></table>` : emptyState("Склад пуст", "Добавьте первую продаваемую позицию.", "▦");
    } else if (view === "partners") {
      const items = await apiFetch("/api/partners");
      container.innerHTML = items.length ? `<table class="data-table"><thead><tr><th>Посредник</th><th>Hex-код</th><th>Статус</th></tr></thead><tbody>${items.map(item =>
        `<tr><td><strong>${escapeHtml(item.name)}</strong></td><td><code>${escapeHtml(item.hexCode)}</code></td><td><span class="status-pill">${escapeHtml(item.status)}</span></td></tr>`
      ).join("")}</tbody></table>` : emptyState("Нет подключений", "Подключите посредника по Hex-коду.", "◇");
    } else if (view === "cash") {
      const items = await apiFetch("/api/cash-register");
      container.innerHTML = items.length ? `<table class="data-table"><thead><tr><th>Продажа</th><th>Сумма</th><th>Дата</th></tr></thead><tbody>${items.map(item =>
        `<tr><td><strong>${escapeHtml(item.description)}</strong></td><td>${formatMoney(item.amount, item.currency)}</td><td>${formatDate(item.createdAt)}</td></tr>`
      ).join("")}</tbody></table>` : emptyState("Касса пуста", "Новые продажи появятся здесь.", "↗");
    } else {
      const items = await apiFetch("/api/staff");
      container.innerHTML = items.length ? `<table class="data-table"><thead><tr><th>Сотрудник</th><th>Роль</th><th>Оклад</th><th>Комиссия</th></tr></thead><tbody>${items.map(item =>
        `<tr><td><strong>${escapeHtml(item.fullName)}</strong><br><small>${escapeHtml(item.email || item.phone || "—")}</small></td><td>${escapeHtml(roleLabels[item.role] || item.role)}</td><td>${formatMoney(item.baseSalary, "USD")}</td><td>${item.commissionPercent}%</td></tr>`
      ).join("")}</tbody></table>` : emptyState("Сотрудников нет", "Список сотрудников этого контура пуст.", "♙");
    }
  } catch (error) {
    container.innerHTML = `<div class="alert alert-error">${escapeHtml(error.message)}</div>`;
  }
}

function emptyState(title, text, icon) {
  return `<div class="empty-state"><span class="empty-icon">${icon}</span><strong>${escapeHtml(title)}</strong>${escapeHtml(text)}</div>`;
}

function formatDate(value) {
  return new Date(value).toLocaleDateString("ru-RU", { day: "numeric", month: "short" });
}

function formatMoney(value, currency) {
  try {
    return new Intl.NumberFormat("ru-RU", { style: "currency", currency, maximumFractionDigits: 2 }).format(Number(value || 0));
  } catch {
    return `${Number(value || 0).toFixed(2)} ${currency || ""}`;
  }
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, char => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  })[char]);
}

function logout(show = true) {
  session = null;
  localStorage.removeItem(STORAGE_KEY);
  showLogin();
  if (show) showToast("Вы вышли из аккаунта.");
}

loginForm.addEventListener("submit", async event => {
  event.preventDefault();
  loginError.classList.add("hidden");
  const button = loginForm.querySelector('button[type="submit"]');
  button.disabled = true;
  button.innerHTML = "<span>Входим...</span>";
  try {
    const login = document.querySelector("#login-input").value.trim();
    const password = document.querySelector("#password-input").value;
    const result = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ login, password })
    });
    if (!result.ok) throw new Error(result.status === 401 ? "Неверный логин или пароль." : "Не удалось выполнить вход.");
    session = { ...(await result.json()), login };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    showApp();
  } catch (error) {
    loginError.textContent = error.message;
    loginError.classList.remove("hidden");
  } finally {
    button.disabled = false;
    button.innerHTML = "<span>Войти в систему</span><span aria-hidden=\"true\">→</span>";
  }
});

document.querySelectorAll("[data-demo-login]").forEach(button => {
  button.addEventListener("click", () => {
    document.querySelector("#login-input").value = button.dataset.demoLogin;
    document.querySelector("#password-input").value = button.dataset.demoPassword;
  });
});

document.querySelector("#toggle-password").addEventListener("click", event => {
  const password = document.querySelector("#password-input");
  password.type = password.type === "password" ? "text" : "password";
  event.currentTarget.textContent = password.type === "password" ? "Показать" : "Скрыть";
});

document.querySelector("#logout-button").addEventListener("click", () => logout());
document.querySelector("#menu-toggle").addEventListener("click", () => document.querySelector("#sidebar").classList.toggle("sidebar-open"));
document.querySelector("#refresh-driver").addEventListener("click", loadDriverTrips);
document.querySelector("#close-cost-panel").addEventListener("click", () => document.querySelector("#trip-cost-panel").classList.add("hidden"));

document.querySelectorAll("[data-open-view]").forEach(button => {
  if (!button.closest("#main-nav")) button.addEventListener("click", () => openView(button.dataset.openView));
});

document.querySelectorAll("[data-open-login]").forEach(button => {
  button.addEventListener("click", showLogin);
});

document.querySelectorAll("[data-pickup]").forEach(button => button.addEventListener("click", () => {
  activePickupMode = button.dataset.pickup;
  document.querySelectorAll("[data-pickup]").forEach(item => item.classList.toggle("active", item === button));
  document.querySelector("#hotel-field").classList.toggle("hidden", activePickupMode !== "hotel");
  document.querySelector("#custom-pickup-field").classList.toggle("hidden", activePickupMode !== "custom");
  document.querySelector("#custom-pickup").required = activePickupMode === "custom";
}));

function calculateBookingRemaining() {
  const total = Number(document.querySelector("#booking-total-sale").value || 0);
  const paid = Number(document.querySelector("#booking-paid").value || 0);
  const saleCurrency = document.querySelector("#booking-sale-currency").value;
  const paidCurrency = document.querySelector("#booking-paid-currency").value;
  const trip = cachedTrips.find(item => item.id === document.querySelector("#booking-trip").value);
  const priceCurrency = trip?.currency || "USD";
  const rate = code => currencyRates.find(item => item.code === code)?.rateToUsd || (code === "USD" ? 1 : 0);
  const saleUsd = total * rate(saleCurrency);
  const paidUsd = paid * rate(paidCurrency);
  const remainingUsd = Math.max(0, saleUsd - paidUsd);
  const remaining = rate(priceCurrency) ? remainingUsd / rate(priceCurrency) : remainingUsd;
  document.querySelector("#booking-rest-amount").textContent =
    `${remaining.toFixed(2)} ${priceCurrency}`;
}

["booking-total-sale", "booking-paid", "booking-sale-currency", "booking-paid-currency", "booking-trip"].forEach(id => {
  document.querySelector(`#${id}`).addEventListener("input", calculateBookingRemaining);
  document.querySelector(`#${id}`).addEventListener("change", calculateBookingRemaining);
});

document.querySelector("#booking-form").addEventListener("submit", async event => {
  event.preventDefault();
  const profile = currentProfile();
  const tripId = document.querySelector("#booking-trip").value;
  if (!profile?.userId || !tripId) return showToast("Выберите рейс.", true);
  const request = {
    tripId,
    guideUserId: profile.userId,
    hotelId: activePickupMode === "hotel" ? document.querySelector("#booking-hotel").value || null : null,
    customPickupPoint: activePickupMode === "custom" ? document.querySelector("#custom-pickup").value.trim() : null,
    adults: Number(document.querySelector("#booking-adults").value),
    children: Number(document.querySelector("#booking-children").value || 0),
    commissionAmount: Number(document.querySelector("#booking-commission").value || 0),
    totalSaleAmount: Number(document.querySelector("#booking-total-sale").value || 0),
    saleCurrency: document.querySelector("#booking-sale-currency").value,
    paidAmount: Number(document.querySelector("#booking-paid").value || 0),
    paidCurrency: document.querySelector("#booking-paid-currency").value
  };
  try {
    await apiFetch("/api/bookings", { method: "POST", body: JSON.stringify(request) });
    event.currentTarget.reset();
    document.querySelector("#booking-adults").value = 1;
    document.querySelector("#booking-children").value = 0;
    showToast("Билет оформлен. Он сохранён в вашем списке.");
    openView("overview");
    await loadAgencyDashboard();
  } catch (error) {
    showToast(error.message, true);
  }
});

document.querySelector("#tour-form").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    await apiFetch("/api/tours", {
      method: "POST",
      body: JSON.stringify({
        title: document.querySelector("#tour-title").value.trim(),
        publicPrice: Number(document.querySelector("#tour-price").value),
        currency: document.querySelector("#tour-currency").value
      })
    });
    event.currentTarget.reset();
    document.querySelector("#tour-price").value = 0;
    showToast("Экскурсия добавлена в каталог.");
    await loadTours();
  } catch (error) {
    showToast(error.message, true);
  }
});

document.querySelector("#trip-form").addEventListener("submit", async event => {
  event.preventDefault();
  const date = document.querySelector("#trip-date").value;
  const time = document.querySelector("#trip-time").value;
  try {
    await apiFetch("/api/trips", {
      method: "POST",
      body: JSON.stringify({
        tourId: document.querySelector("#trip-tour").value,
        departureDate: new Date(`${date}T${time}:00`).toISOString(),
        departureTime: `${time}:00`,
        vehicleNumber: document.querySelector("#trip-vehicle").value.trim(),
        driverUserId: document.querySelector("#trip-driver").value || null,
        capacity: Number(document.querySelector("#trip-capacity-input").value)
      })
    });
    event.currentTarget.reset();
    document.querySelector("#trip-capacity-input").value = 20;
    showToast("Рейс добавлен в расписание.");
    openView("overview");
    await loadIntermediaryDashboard();
  } catch (error) {
    showToast(error.message, true);
  }
});

document.querySelector("#expense-form").addEventListener("submit", async event => {
  event.preventDefault();
  const tripId = document.querySelector("#expense-trip-id").value;
  try {
    await apiFetch(`/api/trips/${tripId}/expenses`, {
      method: "POST",
      body: JSON.stringify({
        title: document.querySelector("#expense-title").value.trim(),
        brickType: document.querySelector("#expense-type").value,
        amount: Number(document.querySelector("#expense-amount").value),
        currency: document.querySelector("#expense-currency").value,
        isPenaltyOrExtra: document.querySelector("#expense-extra").checked
      })
    });
    event.currentTarget.reset();
    showToast("Расход добавлен.");
    const trip = document.querySelector("#intermediary-trips [data-cost-trip]");
    await openCostPanel(tripId, trip?.dataset.title || "Расходы рейса");
  } catch (error) {
    showToast(error.message, true);
  }
});

if (session?.accessToken) showApp();
else showPublic();
