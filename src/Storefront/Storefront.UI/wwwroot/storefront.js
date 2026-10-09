let currentAntiforgeryToken = null;

function antiforgeryToken() {
  return currentAntiforgeryToken ?? document.querySelector(
    'input[name="__RequestVerificationToken"]',
  )?.value;
}

export async function request(url, method = "GET", data = null) {
  const token = antiforgeryToken();
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { RequestVerificationToken: token } : {}),
    },
    body: data ? JSON.stringify(data) : undefined,
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(
      error?.message || "ارتباط با فروشگاه برقرار نشد. دوباره تلاش کنید.",
    );
  }
  return await response.json();
}
export function lockScroll(locked) {
  document.body.style.overflow = locked ? "hidden" : "";
}
export function focusDialog(id) {
  document.getElementById(id)?.focus();
}

export async function postForm(url, fields) {
  const token = antiforgeryToken();
  const body = new URLSearchParams(fields);
  if (token) body.set("__RequestVerificationToken", token);
  const response = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    body,
  });
  if (!response.ok)
    throw new Error("درخواست فروشگاه انجام نشد. دوباره تلاش کنید.");
  const result = await response.json();
  if (typeof result.message === "string") {
    result.message = [
      new DOMParser().parseFromString(result.message, "text/html").body
        .textContent,
    ];
  }
  return result;
}

const cartListeners = new Map();
let nextCartListener = 0;
export function listenForCart(reference) {
  const listener = (event) =>
    reference.invokeMethodAsync("CartChanged", !!event.detail?.open);
  const id = ++nextCartListener;
  cartListeners.set(id, listener);
  window.addEventListener("madadrang:cart-changed", listener);
  return id;
}
export function stopListeningForCart(id) {
  const listener = cartListeners.get(id);
  if (listener) window.removeEventListener("madadrang:cart-changed", listener);
  cartListeners.delete(id);
}
export function notifyCartChanged(open = false) {
  window.dispatchEvent(
    new CustomEvent("madadrang:cart-changed", { detail: { open } }),
  );
}

export function submitNativeSelect(input, value, submitName) {
  input.value = value;
  const form = input.form;
  if (!form || !submitName) return;
  const submitter = document.createElement("button");
  submitter.type = "submit";
  submitter.name = submitName;
  submitter.value = "1";
  submitter.hidden = true;
  form.appendChild(submitter);
  form.requestSubmit(submitter);
  submitter.remove();
}

function refreshAntiforgery(cart) {
  currentAntiforgeryToken = cart.Token;
  document.querySelectorAll('input[type="hidden"]').forEach((input) => {
    if (input.name === cart.TokenField) input.value = cart.Token;
  });
}

async function readNativeCart(response) {
  if (response.redirected) {
    location.assign(response.url);
    throw new Error("در حال باز کردن صفحهٔ فروشگاه.");
  }
  if (!response.ok || !response.headers.get("Content-Type")?.includes("application/json"))
    throw new Error("درخواست سبد خرید انجام نشد. دوباره تلاش کنید.");
  const cart = await response.json();
  refreshAntiforgery(cart);
  return cart;
}

export async function getNativeCart(url) {
  return readNativeCart(await fetch(url, {
    credentials: "same-origin",
    cache: "no-store",
    headers: { "X-Madadrang-Cart": "1" },
  }));
}

export async function updateNativeCart(cart, itemId, quantity) {
  const body = new URLSearchParams();
  const form = document.getElementById("shopping-cart-form");
  if (form) {
    for (const [name, value] of new FormData(form)) {
      if (typeof value === "string" && !/^itemquantity\d+$/.test(name))
        body.append(name, value);
    }
  } else {
    // Blazor JS interop arguments use camelCase; native HTTP results use PascalCase.
    for (const field of cart.checkoutFields) {
      for (const value of field.values) body.append(field.name, value);
    }
  }
  body.set("updatecart", "1");
  body.set(cart.tokenField, cart.token);
  if (quantity === 0) body.append("removefromcart", String(itemId));
  else body.set(`itemquantity${itemId}`, String(quantity));
  const result = await readNativeCart(await fetch(cart.cartUrl, {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: { "X-Madadrang-Cart": "1" },
    body,
  }));
  const before = cart.lines.find((line) => line.id === itemId);
  const after = result.Lines.find((line) => line.Id === itemId);
  const changed = quantity === 0 ? before && !after
    : after?.Quantity === quantity && before?.quantity !== quantity;
  if (form && changed) location.reload();
  return result;
}

export function navigate(url) {
  location.assign(url);
}

export function reloadAfterSignIn(force) {
  if (force || document.querySelector(".sf-native-cart")) {
    location.reload();
    return true;
  }
  return false;
}
