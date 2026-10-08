export async function request(url, method = 'GET', data = null) {
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  const response = await fetch(url, { method, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', ...(token ? { 'RequestVerificationToken': token } : {}) }, body: data ? JSON.stringify(data) : undefined });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.message || 'ارتباط با فروشگاه برقرار نشد. دوباره تلاش کنید.');
  }
  return await response.json();
}
export function lockScroll(locked) { document.body.style.overflow = locked ? 'hidden' : ''; }
export function focusDialog(id) { document.getElementById(id)?.focus(); }
