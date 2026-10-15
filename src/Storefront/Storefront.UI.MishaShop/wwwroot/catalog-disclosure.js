// Keep nopCommerce's filter nodes outside Blazor's render tree: jQuery owns the
// slider and its AJAX subscriptions. Blueprint owns the disclosure trigger.
const disclosures = new Map();
export function initialize(id) {
    const panel = document.getElementById(id);
    if (!panel || disclosures.has(id)) return;
    const desktop = window.matchMedia('(min-width: 62.5625rem)');
    const entry = { panel, desktop, open: false };
    entry.update = () => {
        const visible = desktop.matches || entry.open;
        panel.classList.toggle('hidden', !visible);
        panel.setAttribute('aria-hidden', String(!visible));
        panel.inert = !visible;
    };
    desktop.addEventListener('change', entry.update);
    disclosures.set(id, entry);
    entry.update();
}
export function setOpen(id, open) {
    const entry = disclosures.get(id);
    if (!entry) return;
    entry.open = open;
    entry.update();
}
export function dispose(id) {
    const entry = disclosures.get(id);
    if (!entry) return;
    entry.desktop.removeEventListener('change', entry.update);
    disclosures.delete(id);
}
