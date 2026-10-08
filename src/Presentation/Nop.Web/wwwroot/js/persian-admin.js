/* Admin presentation only. Native inputs remain the Gregorian/ASCII contract.
 * Calendar arithmetic delegates to the browser's ICU Persian calendar (Intl).
 */
(function () {
    'use strict';
    const enabled = /^fa(?:-|$)/i.test(document.documentElement.lang);
    const digits = '۰۱۲۳۴۵۶۷۸۹';
    let activePickerInput;
    const latin = value => String(value).replace(/[۰-۹٠-٩]/g, c => String(c.charCodeAt(0) - (c >= '۰' ? 1776 : 1632))).replace(/٫/g, '.').replace(/٬/g, '');
    const persian = value => String(value).replace(/[0-9]/g, c => digits[+c]);
    const calendar = new Intl.DateTimeFormat('en-US-u-ca-persian-nu-latn', { timeZone: 'UTC', year: 'numeric', month: '2-digit', day: '2-digit' });
    const pad = value => String(value).padStart(2, '0');
    function parts(date) {
        const result = {};
        calendar.formatToParts(date).forEach(p => { if (p.type !== 'literal') result[p.type] = +p.value; });
        return result;
    }
    function fromJalali(year, month, day) {
        if (year < 1 || month < 1 || month > 12 || day < 1 || day > 31) return null;
        // Search UTC dates near the corresponding Gregorian year. A strict round trip
        // rejects invalid leap days rather than silently rolling them into next month.
        let low = Math.floor(Date.UTC(year + 621, 0, 1) / 86400000);
        let high = Math.floor(Date.UTC(year + 622, 11, 31) / 86400000);
        const key = year * 10000 + month * 100 + day;
        while (low <= high) {
            const mid = Math.floor((low + high) / 2), date = new Date(mid * 86400000), p = parts(date);
            const actual = p.year * 10000 + p.month * 100 + p.day;
            if (actual === key) return date.toISOString().slice(0, 10);
            if (actual < key) low = mid + 1; else high = mid - 1;
        }
        return null;
    }
    function toJalali(value) {
        if (!value) return '';
        const date = new Date(value.slice(0, 10) + 'T12:00:00Z');
        if (isNaN(date)) return '';
        const p = parts(date);
        return `${p.year}/${pad(p.month)}/${pad(p.day)}` + (value.includes('T') ? ' ' + value.split('T')[1] : '');
    }
    function parse(value, withTime) {
        const match = latin(value).trim().match(/^(\d{1,4})[\/\-](\d{1,2})[\/\-](\d{1,2})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?$/);
        if (!match || (withTime && !match[4]) || (!withTime && match[4])) return null;
        const date = fromJalali(+match[1], +match[2], +match[3]);
        if (!date || (match[4] && (+match[4] > 23 || +match[5] > 59 || +(match[6] || 0) > 59))) return null;
        return date + (withTime ? `T${pad(match[4])}:${match[5]}${match[6] ? ':' + match[6] : ''}` : '');
    }
    function adapt(input) {
        if (input.dataset.persianAdapted || input.closest('[data-admin-latin]')) return;
        if (input.hidden || input.style.display === 'none' || input.classList.contains('d-none')) return;
        const isDate = input.type === 'date' || input.type === 'datetime-local';
        if (!isDate && input.type !== 'number') return;
        input.dataset.persianAdapted = 'true';
        const visible = document.createElement('input');
        visible.type = 'text'; visible.className = input.className + ' admin-persian-input';
        visible.id = input.id + '_Persian'; visible.disabled = input.disabled; visible.readOnly = input.readOnly;
        visible.required = input.required; input.required = false; visible.dir = 'ltr';
        visible.inputMode = isDate ? 'text' : 'decimal';
        visible.setAttribute('aria-label', input.labels?.[0]?.textContent.trim() || input.name);
        if (isDate) visible.placeholder = input.type === 'date' ? '۱۴۰۵/۰۱/۰۱' : '۱۴۰۵/۰۱/۰۱ ۱۴:۳۰';
        const sync = () => { if (document.activeElement !== visible) visible.value = persian(isDate ? toJalali(input.value) : input.value); visible.disabled = input.disabled; visible.readOnly = input.readOnly; const button = visible.nextElementSibling; if (button?.classList.contains('admin-calendar-button')) button.disabled = input.disabled || input.readOnly; };
        input.adminPersianSync = sync;
        const save = () => {
            const raw = visible.value.trim();
            const value = raw ? (isDate ? parse(raw, input.type === 'datetime-local') : latin(raw)) : '';
            if (value === null || (!isDate && value && !/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(value))) {
                input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); input.dispatchEvent(new Event('change', { bubbles: true }));
                visible.setCustomValidity(isDate ? 'تاریخ شمسی معتبر وارد کنید؛ مانند ۱۴۰۵/۰۱/۰۱.' : 'عدد معتبر وارد کنید.');
                return;
            }
            visible.setCustomValidity(''); input.value = value;
            // Preserve native min/max/step constraints although the backing control is hidden.
            if (!input.checkValidity()) { visible.setCustomValidity('مقدار واردشده خارج از محدودهٔ مجاز است.'); input.value = ''; }
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        };
        sync(); input.classList.add('admin-persian-backing'); input.insertAdjacentElement('afterend', visible);
        input.labels?.forEach(label => label.htmlFor = visible.id);
        visible.addEventListener('input', save);
        visible.addEventListener('blur', () => { if (visible.checkValidity()) sync(); });
        input.addEventListener('change', () => { if (document.activeElement !== visible) sync(); });
        input.form?.addEventListener('reset', () => setTimeout(sync, 0));
        if (isDate) attachCalendar(input, visible, save);
    }
    function attachCalendar(input, visible, save) {
        // Official JalaliDatePicker 1.0.0 owns the picker, month/year navigation,
        // today/clear controls and date/time selection. This adapter owns binding.
        visible.setAttribute('data-jdp', '');
        visible.setAttribute('data-jdp-has-second', 'false');
        if (input.type === 'date') visible.setAttribute('data-jdp-only-date', '');
        if (input.min) visible.setAttribute('data-jdp-min-date', toJalali(input.min).split(' ')[0]);
        if (input.max) visible.setAttribute('data-jdp-max-date', toJalali(input.max).split(' ')[0]);
        visible.addEventListener('jdp:change', () => { save(); visible.value = persian(visible.value); });
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'btn btn-outline-secondary admin-calendar-button';
        button.textContent = 'انتخاب تاریخ شمسی'; button.disabled = input.disabled || input.readOnly;
        button.addEventListener('click', event => {
            event.stopPropagation();
            if (window.jalaliDatepicker) {
                activePickerInput = visible;
                visible.value = latin(visible.value); window.jalaliDatepicker.show(visible); preparePicker(); visible.value = persian(visible.value);
                document.querySelector('jdp-container .jdp-day.selected, jdp-container .jdp-day:not(.disabled-day)')?.focus();
            }
        });
        visible.insertAdjacentElement('afterend', button);
    }
    function preparePicker() {
        const picker = document.querySelector('jdp-container'); if (!picker) return;
        picker.setAttribute('role', 'dialog'); picker.setAttribute('aria-label', 'انتخاب تاریخ شمسی');
        picker.querySelectorAll('.jdp-day:not(.disabled-day), .jdp-icon-plus, .jdp-icon-minus, .jdp-btn-today:not(.disabled-btn), .jdp-btn-empty, .jdp-btn-close').forEach(node => {
            node.tabIndex = 0; node.setAttribute('role', 'button');
            if (node.day) node.setAttribute('aria-label', persian(`${node.year}/${node.month}/${node.day}`));
            else if (node.classList.contains('jdp-icon-plus')) node.setAttribute('aria-label', 'بعدی');
            else if (node.classList.contains('jdp-icon-minus')) node.setAttribute('aria-label', 'قبلی');
        });
        picker.querySelectorAll('select, input').forEach(node => node.tabIndex = 0);
    }
    function refresh(root) {
        (root || document).querySelectorAll('input[type="date"], input[type="datetime-local"], input[type="number"]').forEach(adapt);
        (root || document).querySelectorAll('[data-admin-date]').forEach(element => {
            const value = element.dataset.adminDate;
            if (value) element.textContent = formatDate(value, element.dataset.adminDateFormat || 'L LT');
        });
        (root || document).querySelectorAll('.badge, .small-box h3, .pagination a, .pagination span, td, td span, .form-text-row, .dt-info, .dt-length option, .configuration-step-link h5, [data-admin-number]').forEach(element => {
            if (element.closest('code, pre, [data-admin-latin]') || /sku|code|url|email|guid|ipaddress/i.test(element.dataset.columnname || element.id || '')) return;
            Array.from(element.childNodes).filter(node => node.nodeType === 3).forEach(node => {
                // Only standalone quantities/currency; names, brands and embedded codes remain untouched.
                if ((element.matches('.dt-info, .dt-length option, .configuration-step-link h5') ||
                    /^[\s\d.,%+−\-$€£۰-۹٬٫]+(?:ریال|تومان)?\s*$/.test(node.textContent)) && /\d/.test(node.textContent))
                    node.textContent = persian(node.textContent);
            });
        });
    }
    function formatDate(value, format) {
        if (!enabled) return null;
        // moment preserves the existing grid's timezone interpretation.
        const m = window.moment(value);
        if (!m.isValid()) return value;
        const p = parts(new Date(Date.UTC(m.year(), m.month(), m.date(), 12)));
        let result = `${p.year}/${pad(p.month)}/${pad(p.day)}`;
        if (/[Hhms]|LTS|LT/.test(format || '')) result += ` ${pad(m.hours())}:${pad(m.minutes())}`;
        return persian(result);
    }
    window.nopAdminPersian = { enabled, latin, digits: persian, toJalali, parse, fromJalali, formatDate, refresh };
    if (!enabled) return;
    const start = () => {
        if (window.jalaliDatepicker) window.jalaliDatepicker.startWatch({ time: true, hasSecond: "attr", minDate: "attr", maxDate: "attr", autoShow: false, persianDigits: true, zIndex: 2100 });
        refresh();
        document.addEventListener('click', event => {
            if (!event.target.closest('.btn-search, [id^="search-"]')) return;
            const invalid = Array.from(document.querySelectorAll('.admin-persian-input:not(:disabled)')).find(input => input.validity.customError);
            if (invalid) { event.preventDefault(); event.stopImmediatePropagation(); invalid.reportValidity(); }
        }, true);
        document.addEventListener('keydown', event => {
            const picker = event.target.closest('jdp-container'); if (!picker) return;
            if (event.key === 'Escape') { event.preventDefault(); event.stopImmediatePropagation(); window.jalaliDatepicker.hide(); activePickerInput?.focus(); return; }
            if ((event.key === 'Enter' || event.key === ' ') && event.target.getAttribute('role') === 'button') { event.preventDefault(); event.target.click(); }
            const delta = { ArrowLeft: 1, ArrowRight: -1, ArrowDown: 7, ArrowUp: -7 }[event.key];
            if (delta && event.target.classList.contains('jdp-day')) {
                const days = Array.from(picker.querySelectorAll('.jdp-day:not(.disabled-day)'));
                const next = days[days.indexOf(event.target) + delta]; if (next) { event.preventDefault(); next.focus(); }
            }
        }, true);
        new MutationObserver(records => {
            records.forEach(r => {
                if (r.type === 'attributes') { r.target.adminPersianSync?.(); return; }
                r.addedNodes.forEach(n => { if (n.nodeType === 1) { if (n.matches('input')) adapt(n); refresh(n); } });
            });
            preparePicker();
        }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['disabled', 'readonly'] });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
