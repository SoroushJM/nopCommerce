// Preserve nopCommerce's image selection/zoom handlers; add keyboard access to
// the native non-zoom thumbnails, which otherwise respond only to image clicks.
document.addEventListener('keydown', function (event) {
    const thumb = event.target.closest?.('.mishashop-shell .thumb-item[role="button"]');
    if (thumb && (event.key === 'Enter' || event.key === ' ')) {
        event.preventDefault();
        thumb.querySelector('img')?.click();
    }
});
document.addEventListener('click', function (event) {
    const thumb = event.target.closest?.('.mishashop-shell .thumb-item');
    if (!thumb) return;
    // A click in the padding of a non-zoom control selects the same native image.
    if (event.target === thumb && thumb.getAttribute('role') === 'button') {
        thumb.querySelector('img')?.click();
        return;
    }
    const gallery = thumb.closest('.picture-gallery');
    if (gallery) {
        gallery.querySelectorAll('.thumb-item').forEach(function (item) {
            item.setAttribute('aria-current', item === thumb ? 'true' : 'false');
        });
    }
});
