import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';

const source = await readFile(new URL('../../Storefront/Storefront.UI.Shared/wwwroot/storefront.js', import.meta.url), 'utf8');
const api = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const nativeSource = await readFile(new URL('../../Presentation/Nop.Web/wwwroot/js/public.ajaxcart.js', import.meta.url), 'utf8');

function nativeFixture(popup = false) {
  globalThis.window = new EventTarget();
  const dom = new Map();
  const notifications = [];
  const nativeContext = {
    $: selector => ({ html: value => dom.set(selector, value), replaceWith: value => dom.set(selector, value) }),
    displayPopupNotification: (...args) => notifications.push(['popup', ...args]),
    displayBarNotification: (...args) => notifications.push(['bar', ...args]),
    location: {},
  };
  vm.runInNewContext(nativeSource, nativeContext);
  const native = nativeContext.AjaxCart;
  native.init(popup, '.cart-qty', '.wishlist-qty', '#flyout-cart', { AjaxCartFailure: 'Failure' });
  window.AjaxCart = native;
  const events = [];
  const subscription = api.listenForCart({ invokeMethodAsync: (name, open) => events.push({ name, open }) });
  return { native, nativeContext, dom, notifications, events, cleanup: () => api.stopListeningForCart(subscription) };
}

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

test('native success processing keeps its return and popup preferences and emits one Shared refresh', () => {
  const fixture = nativeFixture(true);
  const wrapped = fixture.native.success_process;
  const second = api.listenForCart({ invokeMethodAsync: () => {} });
  assert.equal(fixture.native.success_process, wrapped, 'Installing another listener must not nest the wrapper.');
  assert.equal(wrapped.call(fixture.native, {
    success: true, message: ['Added'], updatetopcartsectionhtml: '(3)',
    updateflyoutcartsectionhtml: '<div id="flyout-cart">native features</div>',
  }), false);
  assert.equal(fixture.dom.get('.cart-qty'), '(3)');
  assert.match(fixture.dom.get('#flyout-cart'), /native features/);
  assert.deepEqual(fixture.notifications, [['popup', ['Added'], 'success', true]]);
  assert.deepEqual(fixture.events, [{ name: 'CartChanged', open: false }]);
  assert.equal(wrapped.call(fixture.native, { redirect: '/wishlist' }), true);
  assert.equal(fixture.nativeContext.location.href, '/wishlist');
  assert.equal(fixture.events.length, 1);
  wrapped.call(fixture.native, { success: true, message: ['Wished'], updatetopwishlistsectionhtml: '(2)' });
  assert.equal(fixture.dom.get('.wishlist-qty'), '(2)');
  assert.equal(fixture.events.length, 1, 'Wishlist-only changes must not reload the cart.');
  api.stopListeningForCart(second);
  fixture.cleanup();
});

test('Shared product add applies actual native sections and one configured notification with no duplicate refresh', async () => {
  const fixture = nativeFixture(false);
  globalThis.document = { querySelector: () => null };
  globalThis.fetch = async () => ({ ok: true, json: async () => ({
    success: true, message: ['Added'], updatetopcartsectionhtml: '(4)', updatetopwishlistsectionhtml: '(1)',
    updateflyoutcartsectionhtml: '<div id="flyout-cart">four items</div>',
  }) });
  await api.postForm('/native-add', { 'addtocart_7.EnteredQuantity': '2' });
  assert.equal(fixture.dom.get('.cart-qty'), '(4)');
  assert.equal(fixture.dom.get('.wishlist-qty'), '(1)');
  assert.match(fixture.dom.get('#flyout-cart'), /four items/);
  assert.deepEqual(fixture.notifications, [['bar', ['Added'], 'success', 3500]]);
  assert.equal(fixture.events.length, 0);
  api.notifyCartChanged(true); // Existing NativeProductPage owns this event.
  assert.deepEqual(fixture.events, [{ name: 'CartChanged', open: true }]);
  fixture.cleanup();
});

test('native cart JSON sync updates count/flyout without notifications, requests or event loops', async () => {
  const fixture = nativeFixture(true);
  globalThis.document = { querySelectorAll: () => [], getElementById: () => null };
  let requests = 0;
  globalThis.fetch = async () => {
    requests++;
    return {
      ok: true, redirected: false, headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ Token: 'csrf', TokenField: '__RequestVerificationToken', Lines: [{ Id: 7, Quantity: 5 }],
        NativeSections: { updatetopcartsectionhtml: '(5)', updateflyoutcartsectionhtml: '<div id="flyout-cart">five items</div>' } }),
    };
  };
  await api.getNativeCart('/shop/cart');
  assert.equal(fixture.dom.get('.cart-qty'), '(5)');
  assert.match(fixture.dom.get('#flyout-cart'), /five items/);
  await api.updateNativeCart({ cartUrl: '/shop/cart', tokenField: '__RequestVerificationToken', token: 'csrf', checkoutFields: [], lines: [{ id: 7, quantity: 2 }] }, 7, 5);
  assert.equal(requests, 2, 'A native GET and mini quantity POST must be the only requests.');
  assert.deepEqual(fixture.events, []);
  assert.deepEqual(fixture.notifications, []);
  api.syncNativeCart({ nativeSections: { updatetopcartsectionhtml: '(0)', updateflyoutcartsectionhtml: '<div id="flyout-cart">empty</div>', message: ['must not notify'], redirect: '/must-not-redirect' } });
  assert.equal(fixture.dom.get('.cart-qty'), '(0)');
  assert.match(fixture.dom.get('#flyout-cart'), /empty/);
  assert.equal(fixture.nativeContext.location.href, undefined);
  assert.deepEqual(fixture.events, []);
  assert.deepEqual(fixture.notifications, []);
  fixture.cleanup();
});

test('attribute change messages remain inline and do not enter native AjaxCart notifications', async () => {
  const fixture = nativeFixture(true);
  globalThis.document = { querySelector: () => null };
  globalThis.fetch = async () => ({ ok: true, json: async () => ({ price: 'PRICE', message: ['Selection validation'] }) });
  const result = await api.postForm('/attribute-change', { product_attribute_8: '12' });
  assert.deepEqual(result.message, ['Selection validation']);
  assert.deepEqual(fixture.notifications, []);
  assert.deepEqual(fixture.events, []);
  fixture.cleanup();
});
