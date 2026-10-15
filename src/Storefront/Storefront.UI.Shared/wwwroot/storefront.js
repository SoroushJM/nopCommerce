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
  // Native add responses carry header/flyout HTML and the store's notification message.
  // Attribute-change responses have no section keys and retain their inline validation.
  if (hasNativeSections(result)) applyNativeResponse(result);
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
const nativeSuccessOriginal = Symbol.for("Storefront.AjaxCart.successOriginal");

export function getNativeWishlistQuantity(fallback) {
  // HeaderLinks remains the native AjaxCart update target, including localized digits.
  return document.querySelector(".sf-native-header-links .wishlist-qty")?.textContent?.trim()
    || fallback;
}

function hasNativeSections(response) {
  return !!(response?.updatetopcartsectionhtml
    || response?.updatetopwishlistsectionhtml
    || response?.updateflyoutcartsectionhtml);
}

function installNativeCartBridge() {
  const native = window.AjaxCart;
  if (typeof native?.success_process !== "function") return;
  if (native.success_process[nativeSuccessOriginal]) return;
  const original = native.success_process;
  const wrapped = function (response) {
    const result = original.apply(this, arguments);
    // Wishlist-only changes do not change the shopping cart. Never fetch from a GET sync.
    if (response?.updatetopcartsectionhtml || response?.updateflyoutcartsectionhtml)
      notifyCartChanged(false);
    return result;
  };
  Object.defineProperty(wrapped, nativeSuccessOriginal, { value: original });
  native.success_process = wrapped;
}

function applyNativeResponse(response) {
  installNativeCartBridge();
  const native = window.AjaxCart;
  if (typeof native?.success_process !== "function") return;
  // Shared callers already own their cart event. Skip the wrapper to avoid a second refresh.
  const original = native.success_process[nativeSuccessOriginal] ?? native.success_process;
  original.call(native, response);
}

export function syncNativeCart(cart) {
  const sections = cart.NativeSections ?? cart.nativeSections;
  if (!sections || !hasNativeSections(sections)) return;
  // Server-rendered sections provide localized counts and all native flyout features.
  // Omitting message/redirect makes a read or quantity update silent and cannot loop.
  applyNativeResponse({
    updatetopcartsectionhtml: sections.updatetopcartsectionhtml,
    updatetopwishlistsectionhtml: sections.updatetopwishlistsectionhtml,
    updateflyoutcartsectionhtml: sections.updateflyoutcartsectionhtml,
  });
}

export function listenForCart(reference) {
  installNativeCartBridge();
  const listener = (event) =>
    reference.invokeMethodAsync("CartChanged", !!event.detail?.open);
  const id = ++nextCartListener;
  cartListeners.set(id, listener);
  window.addEventListener("storefront:cart-changed", listener);
  return id;
}
export function stopListeningForCart(id) {
  const listener = cartListeners.get(id);
  if (listener) window.removeEventListener("storefront:cart-changed", listener);
  cartListeners.delete(id);
}
export function notifyCartChanged(open = false) {
  window.dispatchEvent(
    new CustomEvent("storefront:cart-changed", { detail: { open } }),
  );
}

export function notifyProductChanged(productId, attributes, quantity) {
  if (!window.jQuery) return;
  if (attributes) {
    const event = window.jQuery.Event("product_attributes_changed");
    event.changedData = { ...attributes, productId };
    window.jQuery(document).trigger(event);
  }
  if (quantity !== null) {
    const event = window.jQuery.Event("product_quantity_changed");
    event.changedData = { productId, quantity };
    window.jQuery(document).trigger(event);
  }
}

export function focusProductZoomTrigger(gallery) {
  // Restore after BB removes the overlay and releases its focus trap.
  requestAnimationFrame(() => {
    gallery.querySelector(".sf-product-zoom")?.focus({ preventScroll: true });
  });
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
  syncNativeCart(cart);
  return cart;
}

export async function getNativeCart(url) {
  return readNativeCart(await fetch(url, {
    credentials: "same-origin",
    cache: "no-store",
    headers: { "X-Storefront-Cart": "1" },
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
    headers: { "X-Storefront-Cart": "1" },
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

const purchaseListeners = new Map();
let nextPurchaseListener = 0;

export function listenForPurchase(reference) {
  const id = ++nextPurchaseListener;
  const open = (url) => {
    if (window.MadadrangPurchase) window.MadadrangPurchase.pending = null;
    return reference.invokeMethodAsync("OpenPurchase", url);
  };
  const listener = (event) => open(event.detail.url);
  purchaseListeners.set(id, listener);
  window.addEventListener("storefront:purchase", listener);
  const pending = window.MadadrangPurchase?.pending;
  if (pending) queueMicrotask(() => open(pending));
  return id;
}

export function stopListeningForPurchase(id) {
  const listener = purchaseListeners.get(id);
  if (listener) window.removeEventListener("storefront:purchase", listener);
  purchaseListeners.delete(id);
}

export async function getPurchaseProduct(url) {
  const target = new URL(url, location.href);
  if (target.origin !== location.origin) throw new Error("آدرس محصول معتبر نیست.");
  const response = await fetch(target.href, {
    credentials: "same-origin",
    cache: "no-store",
    headers: { "X-Storefront-Purchase": "1" },
  });
  if (!response.headers.get("Content-Type")?.includes("application/json"))
    throw new Error("گزینه‌های محصول دریافت نشد. دوباره تلاش کنید.");
  const product = await response.json();
  if (!response.ok) throw new Error(product.message || "گزینه‌های محصول دریافت نشد. دوباره تلاش کنید.");
  return product;
}

export function reloadAfterSignIn(force) {
  if (force || document.querySelector(".sf-native-cart")) {
    location.reload();
    return true;
  }
  return false;
}
