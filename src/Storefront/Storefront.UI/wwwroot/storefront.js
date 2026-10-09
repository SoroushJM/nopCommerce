export async function request(url, method = "GET", data = null) {
  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]',
  )?.value;
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
  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]',
  )?.value;
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
