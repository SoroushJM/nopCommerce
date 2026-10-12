import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import test from 'node:test';

const source = await readFile(new URL('../../Storefront/Storefront.UI.Shared/wwwroot/storefront.js', import.meta.url), 'utf8');
const api = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

test('cart subscribers receive the shared event and stop receiving it after cleanup', () => {
  globalThis.window = new EventTarget();
  const notifications = [];
  const subscription = api.listenForCart({
    invokeMethodAsync: (name, open) => notifications.push({ name, open }),
  });
  api.notifyCartChanged(true);
  assert.deepEqual(notifications, [{ name: 'CartChanged', open: true }]);
  api.stopListeningForCart(subscription);
  api.notifyCartChanged(false);
  assert.equal(notifications.length, 1);
});

test('mini cart mutations retain repeated checkout fields and refreshed antiforgery tokens', async () => {
  const hiddenToken = { name: '__RequestVerificationToken', value: 'old-token' };
  globalThis.document = {
    querySelector: () => hiddenToken,
    querySelectorAll: () => [hiddenToken],
    getElementById: () => null,
  };
  const calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options });
    return {
      ok: true, redirected: false,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ Token: 'new-token', TokenField: '__RequestVerificationToken', Lines: [{ Id: 7, Quantity: 3 }] }),
    };
  };
  await api.getNativeCart('/shop/cart');
  assert.equal(hiddenToken.value, 'new-token');
  assert.equal(calls[0].options.headers['X-Storefront-Cart'], '1');
  assert.equal(calls[0].options.credentials, 'same-origin');
  assert.equal(calls[0].options.cache, 'no-store');

  const cart = {
    cartUrl: '/shop/cart', tokenField: '__RequestVerificationToken', token: 'new-token',
    checkoutFields: [{ name: 'checkout_attribute_8', values: ['S', 'Blue'] }],
    lines: [{ id: 7, quantity: 2 }],
  };
  await api.updateNativeCart(cart, 7, 3);
  assert.equal(calls[1].url, '/shop/cart');
  assert.deepEqual(calls[1].options.body.getAll('checkout_attribute_8'), ['S', 'Blue']);
  assert.equal(calls[1].options.body.get('itemquantity7'), '3');
  assert.equal(calls[1].options.body.get('__RequestVerificationToken'), 'new-token');
  await api.updateNativeCart(cart, 7, 0);
  assert.equal(calls[2].options.body.get('removefromcart'), '7');
  assert.equal(calls[2].options.body.has('itemquantity7'), false);
  await api.request('/shop/other', 'POST', { x: 1 });
  assert.equal(calls[3].options.headers.RequestVerificationToken, 'new-token');
});

test('a checkout login redirect opens the native page instead of parsing it as cart data', async () => {
  const navigations = [];
  globalThis.location = { assign: url => navigations.push(url) };
  globalThis.fetch = async () => ({ redirected: true, url: 'https://example.test/shop/login' });
  await assert.rejects(() => api.getNativeCart('/shop/cart'));
  assert.deepEqual(navigations, ['https://example.test/shop/login']);
});
