// The native select remains the form/script contract. Only its visible control is BB.
const entries = new Map();
let observer;
let nextKey = 0;
let scheduled = false;
let running = false;
let magnificFocusBridge;
let dropdownEscape = false;
// Keep a measurable native control for jQuery validation's :visible contract.
// Important utilities override native theme select rules without custom CSS.
const mirrorUtilities = "absolute! w-[.0625rem]! h-[.0625rem]! min-w-0! min-h-0! -m-[.0625rem]! p-0! border-[0rem]! [clip-path:inset(50%)]! overflow-hidden! whitespace-nowrap!".split(" ");
const hostUtilities = "inline-block align-middle min-w-[min(8rem,100%)] max-w-full [&[hidden]]:hidden [.inputs_&]:w-full [.date-picker-wrapper_&]:flex-1 [.date-picker-wrapper_&]:min-w-20 [.date-picker-wrapper_&]:w-auto [&.qty-dropdown]:min-w-20";
const dateLayouts = new Map();
const dateUtilities = "flex gap-2 flex-wrap".split(" ");

function dateLayoutFor(entry) {
  const parent = entry.select.closest(".date-picker-wrapper");
  if (parent === entry.dateParent) return;
  releaseDateLayout(entry);
  if (!parent) return;
  let layout = dateLayouts.get(parent);
  if (!layout) {
    layout = { count: 0, added: dateUtilities.filter(name => !parent.classList.contains(name)) };
    parent.classList.add(...layout.added);
    dateLayouts.set(parent, layout);
  }
  layout.count++;
  entry.dateParent = parent;
}

function releaseDateLayout(entry) {
  const parent = entry.dateParent;
  const layout = dateLayouts.get(parent);
  if (layout && --layout.count === 0) {
    parent.classList.remove(...layout.added);
    dateLayouts.delete(parent);
  }
  entry.dateParent = null;
}

function schedule() {
  if (!running || scheduled) return;
  scheduled = true;
  queueMicrotask(() => {
    scheduled = false;
    if (running) refresh();
  });
}

function labelFor(select) {
  const labelText = label => {
    const copy = label.cloneNode(true);
    copy.querySelectorAll("select, .sf-native-select").forEach(control => control.remove());
    return copy.textContent.trim();
  };
  const labelled = (select.getAttribute("aria-labelledby") ?? "").split(/\s+/)
    .map(id => document.getElementById(id)?.textContent?.trim()).filter(Boolean).join(" ");
  return select.getAttribute("aria-label") || labelled
    || Array.from(select.labels ?? []).map(labelText).filter(Boolean).join(" ")
    || select.getAttribute("title") || select.name || select.id;
}

function snapshot(entry) {
  const select = entry.select;
  let parent;
  let groupIndex = -1;
  const options = Array.from(select.options, (option, index) => {
    if (parent !== option.parentElement) { parent = option.parentElement; groupIndex++; }
    return {
      index, value: option.value, text: option.text,
      disabled: option.disabled || (parent instanceof HTMLOptGroupElement && parent.disabled),
      groupIndex, groupText: parent instanceof HTMLOptGroupElement ? parent.label : "",
    };
  });
  return JSON.stringify({
    id: select.id || entry.key, label: labelFor(select),
    describedBy: select.getAttribute("aria-describedby"),
    value: select.value, values: Array.from(select.selectedOptions, option => option.value),
    rtl: getComputedStyle(select).direction === "rtl", multiple: select.multiple,
    searchable: select.matches('.estimate-shipping-address-control[data-trigger="country-select"]'),
    required: select.required, disabled: select.matches(":disabled"),
    invalid: select.getAttribute("aria-invalid") === "true"
      || select.classList.contains("input-validation-error") || select.classList.contains("error"),
    options,
  });
}

// jQuery.val() can change option.selected without dispatching an event. Watch only
// these element instances, leaving native prototypes and third-party APIs intact.
function watchProperty(entry, element, property, prototype) {
  if (Object.hasOwn(element, property)) return;
  const descriptor = Object.getOwnPropertyDescriptor(prototype, property);
  if (!descriptor?.get || !descriptor?.set) return;
  Object.defineProperty(element, property, {
    configurable: true, enumerable: descriptor.enumerable,
    get() { return descriptor.get.call(this); },
    set(value) { descriptor.set.call(this, value); schedule(); },
  });
  entry.restorers.push(() => { delete element[property]; });
}

function watchOptions(entry) {
  for (const option of entry.select.options) {
    if (entry.options.has(option)) continue;
    entry.options.add(option);
    watchProperty(entry, option, "selected", HTMLOptionElement.prototype);
  }
}

async function update(entry) {
  if (!entry.component || entry.updating) { entry.dirty = true; return; }
  const json = snapshot(entry);
  const hidden = entry.select.hidden || entry.select.style.display === "none";
  if (entry.host.hidden !== hidden) entry.host.hidden = hidden;
  const className = "sf-native-select " + hostUtilities + " " + Array.from(entry.select.classList)
    .filter(name => name !== "sf-select-mirror" && !mirrorUtilities.includes(name)).join(" ");
  // Do not write unchanged observed attributes: that would retrigger the observer.
  if (entry.host.className !== className) entry.host.className = className;
  if (json === entry.json) return;
  entry.updating = true;
  try {
    await entry.component.setParameters({ bridgeKey: entry.key, stateJson: json });
    entry.json = json;
    // MultiSelect forwards extra attributes to its wrapper. Keep validation
    // semantics on the actual focusable trigger as well as on the native field.
    const trigger = entry.host.querySelector('button[role="combobox"]');
    const invalid = JSON.parse(json).invalid;
    if (trigger) {
      if (invalid && trigger.getAttribute("aria-invalid") !== "true") trigger.setAttribute("aria-invalid", "true");
      if (!invalid && trigger.hasAttribute("aria-invalid")) trigger.removeAttribute("aria-invalid");
    }
  } catch (error) {
    if (entry.select.isConnected && entry.host.isConnected) console.error("Storefront select update failed", error);
  } finally {
    entry.updating = false;
    if (entry.dirty) { entry.dirty = false; schedule(); }
  }
}

async function mount(select) {
  const key = `sf-native-select-${++nextKey}`;
  const host = document.createElement("span");
  host.className = "sf-native-select " + hostUtilities;
  host.dataset.nativeSelect = key;
  host.hidden = true;
  const entry = {
    key, select, host, component: null, json: "", updating: false, dirty: false,
    options: new WeakSet(), restorers: [], ariaHidden: select.getAttribute("aria-hidden"),
    tabIndex: select.getAttribute("tabindex"), mirrorAdded: [],
  };
  entries.set(key, entry);
  select.dataset.bbSelect = key;
  select.after(host);
  watchProperty(entry, select, "value", HTMLSelectElement.prototype);
  watchProperty(entry, select, "selectedIndex", HTMLSelectElement.prototype);
  watchOptions(entry);
  try {
    entry.json = snapshot(entry);
    entry.component = await window.Blazor.rootComponents.add(host, "storefront-native-select", {
      bridgeKey: key, stateJson: entry.json,
    });
    if (entry.released || !running || !select.isConnected || !host.isConnected) {
      await entry.component.dispose();
      entry.component = null;
      if (!entry.released) release(entry);
      return;
    }
    select.classList.add("sf-select-mirror");
    entry.mirrorAdded = mirrorUtilities.filter(name => !select.classList.contains(name));
    select.classList.add(...entry.mirrorAdded);
    dateLayoutFor(entry);
    select.setAttribute("aria-hidden", "true");
    select.tabIndex = -1;
    host.hidden = select.hidden || select.style.display === "none";
    // Include native width/layout classes without modifying its IDs or form name.
    entry.json = "";
    await update(entry);
  } catch (error) {
    release(entry);
    console.error("Storefront BB select could not start; native control retained", error);
  }
}

function release(entry) {
  if (entry.released) return;
  entry.released = true;
  entries.delete(entry.key);
  if (entry.component) Promise.resolve(entry.component.dispose()).catch(() => {});
  entry.restorers.forEach(restore => restore());
  entry.select.classList.remove("sf-select-mirror");
  entry.select.classList.remove(...entry.mirrorAdded);
  releaseDateLayout(entry);
  delete entry.select.dataset.bbSelect;
  for (const [name, value] of [["aria-hidden", entry.ariaHidden], ["tabindex", entry.tabIndex]]) {
    if (value === null) entry.select.removeAttribute(name);
    else entry.select.setAttribute(name, value);
  }
  entry.host.remove();
}

function refresh() {
  configureShippingPopupFocus();
  for (const entry of entries.values()) {
    if (!entry.select.isConnected || !entry.host.isConnected) { release(entry); continue; }
    if (entry.select.nextElementSibling !== entry.host) entry.select.after(entry.host);
    dateLayoutFor(entry);
    watchOptions(entry);
    update(entry);
  }
  // Includes native fallback views, tag-helper date fields, plugin controls and
  // replacement checkout/country-state HTML, even when their popup is portalled.
  if (!document.querySelector(".storefront")) return;
  document.querySelectorAll("select:not([data-bb-select])").forEach(select => {
    if (!select.closest("[data-storefront-select-skip]")) mount(select);
  });
}

function ownedShippingDropdown(target) {
  if (!(target instanceof Element)) return false;
  const popup = window.jQuery?.magnificPopup?.instance;
  if (!popup?.isOpen || !popup.wrap?.[0]) return false;
  for (const entry of entries.values()) {
    if (!entry.select.closest(".estimate-shipping-popup") || !popup.wrap[0].contains(entry.select)) continue;
    const trigger = entry.host.querySelector('button[role="combobox"]');
    const contentId = trigger?.getAttribute("aria-controls");
    const content = contentId && document.getElementById(contentId);
    if (content?.contains(target)) return true;
  }
  return false;
}

function configureShippingPopupFocus() {
  const jquery = window.jQuery;
  const popup = jquery?.magnificPopup?.instance;
  if (!popup || magnificFocusBridge?.popup === popup || typeof popup._onFocusIn !== "function") return;
  // The pinned native popup traps focus inside its wrap. BB dropdowns are
  // portalled: permit only content owned by a native select in that same popup.
  const original = popup._onFocusIn;
  const wrapped = function (event) {
    if (ownedShippingDropdown(event.target)) return;
    return original.apply(this, arguments);
  };
  popup._onFocusIn = wrapped;
  if (popup.isOpen) jquery(document).off("focusin.mfp", original).on("focusin.mfp", wrapped);
  magnificFocusBridge = { popup, original, wrapped };
}

function dropdownKeyDown(event) {
  if (event.key === "Escape") dropdownEscape = ownedShippingDropdown(event.target);
}

function dropdownKeyUp(event) {
  if (event.key !== "Escape" || !dropdownEscape) return;
  dropdownEscape = false;
  // BB closes its own dropdown on keydown. Do not also close the shipping
  // popup when Magnific handles the subsequent keyup.
  event.stopPropagation();
}

function changed(event) {
  if (event.target instanceof HTMLSelectElement) schedule();
}

function focusBridge(select) {
  const entry = entries.get(select.dataset.bbSelect);
  entry?.host.querySelector('button[role="combobox"]')?.focus();
}

function invalid(event) {
  if (!(event.target instanceof HTMLSelectElement) || !event.target.dataset.bbSelect) return;
  event.preventDefault();
  event.target.setAttribute("aria-invalid", "true");
  focusBridge(event.target);
  schedule();
}

function clickedLabel(event) {
  if (event.target.closest?.(".sf-native-select")) return;
  const label = event.target.closest?.("label");
  if (label?.control instanceof HTMLSelectElement && label.control.dataset.bbSelect) {
    event.preventDefault();
    focusBridge(label.control);
  }
}

function focused(event) {
  if (event.target instanceof HTMLSelectElement && event.target.dataset.bbSelect) focusBridge(event.target);
}

function reset() { setTimeout(schedule, 0); }

export function startNativeSelects() {
  if (running) return;
  running = true;
  observer = new MutationObserver(schedule);
  observer.observe(document.body, {
    subtree: true, childList: true, characterData: true, attributes: true,
    attributeFilter: ["selected", "disabled", "required", "multiple", "value", "class", "style", "hidden", "aria-invalid", "aria-label", "aria-labelledby", "aria-describedby"],
  });
  document.addEventListener("change", changed, true);
  document.addEventListener("input", changed, true);
  document.addEventListener("reset", reset, true);
  document.addEventListener("invalid", invalid, true);
  document.addEventListener("click", clickedLabel, true);
  document.addEventListener("focus", focused, true);
  document.addEventListener("keydown", dropdownKeyDown, true);
  document.addEventListener("keyup", dropdownKeyUp, true);
  schedule();
}

export function writeNativeValues(key, values) {
  const entry = entries.get(key);
  if (!entry || !entry.select.isConnected || entry.select.matches(":disabled")) return;
  const select = entry.select;
  const allowed = new Set(Array.from(select.options).filter(option => !option.disabled
    && !(option.parentElement instanceof HTMLOptGroupElement && option.parentElement.disabled)).map(option => option.value));
  const selected = values.filter(value => allowed.has(value));
  if (select.multiple) {
    for (const option of select.options) option.selected = selected.includes(option.value);
  } else {
    if (!selected.length) return;
    select.value = selected[0];
  }
  if (select.validity.valid) select.removeAttribute("aria-invalid");
  select.dispatchEvent(new Event("input", { bubbles: true }));
  select.dispatchEvent(new Event("change", { bubbles: true }));
  schedule();
}

export function stopNativeSelects() {
  running = false;
  observer?.disconnect();
  document.removeEventListener("change", changed, true);
  document.removeEventListener("input", changed, true);
  document.removeEventListener("reset", reset, true);
  document.removeEventListener("invalid", invalid, true);
  document.removeEventListener("click", clickedLabel, true);
  document.removeEventListener("focus", focused, true);
  document.removeEventListener("keydown", dropdownKeyDown, true);
  document.removeEventListener("keyup", dropdownKeyUp, true);
  if (magnificFocusBridge) {
    const { popup, original, wrapped } = magnificFocusBridge;
    if (popup._onFocusIn === wrapped) popup._onFocusIn = original;
    if (popup.isOpen) window.jQuery(document).off("focusin.mfp", wrapped).on("focusin.mfp", original);
    magnificFocusBridge = null;
  }
  dropdownEscape = false;
  for (const entry of entries.values()) release(entry);
}
