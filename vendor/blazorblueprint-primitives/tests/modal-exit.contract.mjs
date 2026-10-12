import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const modulePath = process.argv[2];
if (!modulePath) {
  throw new Error('Usage: node modal-exit.contract.mjs <extracted-package-staticwebassets/modal-exit.js>');
}

const callbacks = [];
const dotnet = { invokeMethodAsync: async (_name, generation) => callbacks.push(generation) };
globalThis.requestAnimationFrame = callback => setTimeout(callback, 0);
globalThis.CSS = { escape: value => value };
let reduced = false;
globalThis.matchMedia = () => ({ matches: reduced });
let root = null;
globalThis.document = { querySelector: () => root };
const { waitForExit, cancel } = await import(pathToFileURL(resolve(modulePath)).href);

await waitForExit('missing', 1, dotnet);
assert.deepEqual(callbacks, [1], 'a missing portal must release retained content');
callbacks.length = 0;

reduced = true;
await waitForExit('reduced', 2, dotnet);
assert.deepEqual(callbacks, [2], 'reduced motion must complete without an animation');
callbacks.length = 0;
reduced = false;

let finish;
root = { querySelectorAll: () => [{ getAnimations: () => [{ playState: 'running', effect: { getTiming: () => ({ iterations: 1 }) }, finished: new Promise(resolve => { finish = resolve; }) }] }] };
const pending = waitForExit('reopen', 3, dotnet);
await new Promise(resolve => setTimeout(resolve, 5));
cancel('reopen');
finish();
await pending;
assert.deepEqual(callbacks, [], 'a cancelled close must not complete after reopening');

const part = { getAnimations: () => [{ playState: 'running', effect: { getTiming: () => ({ iterations: 1 }) }, finished: Promise.resolve() }] };
root = {
  getAnimations: () => { throw new Error('root subtree animations must not be inspected'); },
  querySelectorAll: selector => {
    assert.equal(selector, '[data-bb-modal-exit-part][data-state="closed"]');
    return [part];
  }
};
await waitForExit('parts-only', 4, dotnet);
assert.deepEqual(callbacks, [4], 'only explicit modal parts may delay teardown');
console.log('modal-exit contract checks passed');
