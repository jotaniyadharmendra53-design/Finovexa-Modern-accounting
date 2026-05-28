/* ============================================================
   Finovexa — Global JavaScript
   site.js — loaded on every authenticated page via _Layout
   ============================================================ */

/* ── jQuery Ajax global defaults ────────────────────────────── */
$.ajaxSetup({
    headers: {
        'X-Requested-With': 'XMLHttpRequest',
        'RequestVerificationToken': $('meta[name="csrf-token"]').attr('content')
    }
});

/* ── CSRF helper (reads from meta tag in _Layout) ────────────── */
function getCsrfToken() {
    return $('meta[name="csrf-token"]').attr('content') || '';
}

/* ── SweetAlert2 Toast preset (top-right, auto-dismiss) ──────── */
const Toast = Swal.mixin({
    toast: true,
    position: 'top-end',
    showConfirmButton: false,
    timer: 3500,
    timerProgressBar: true,
    didOpen: (toast) => {
        toast.addEventListener('mouseenter', Swal.stopTimer);
        toast.addEventListener('mouseleave', Swal.resumeTimer);
    }
});

/* ── Global Swal default: never adjust scrollbar padding ──────── */
Swal.defaultOptions = { ...(Swal.defaultOptions || {}), scrollbarPadding: false };

function showToast(type, message) {
    Toast.fire({ icon: type, title: message });
}
function toastSuccess(msg) { showToast('success', msg); }
function toastError(msg) { showToast('error', msg); }
function toastInfo(msg) { showToast('info', msg); }
function toastWarning(msg) { showToast('warning', msg); }

/* ── SweetAlert2 confirm delete ─────────────────────────────── */
function confirmDelete(options) {
    return Swal.fire({
        icon: 'warning',
        title: options.title || 'Are you sure?',
        text: options.text || 'This action cannot be undone.',
        showCancelButton: true,
        confirmButtonText: options.confirm || 'Yes, delete it!',
        cancelButtonText: options.cancel || 'Cancel',
        confirmButtonColor: '#EF4444',
        cancelButtonColor: '#6B7280',
        reverseButtons: true,
        focusCancel: true
    });
}

/* ── Generic Ajax CRUD helpers ───────────────────────────────── */

/**
 * ajaxPost — wrapper around $.ajax POST with JSON body.
 * Returns a promise. Handles loading state on a button.
 */
function ajaxPost(url, data, $btn) {
    if ($btn) setLoading($btn, true);
    return $.ajax({
        url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(data),
        headers: { 'RequestVerificationToken': getCsrfToken() }
    }).always(function () {
        if ($btn) setLoading($btn, false);
    });
}

/**
 * ajaxDelete — sends DELETE request. Shows confirm dialog first.
 */
function ajaxDelete(url, opts = {}) {
    return confirmDelete(opts).then(result => {
        if (!result.isConfirmed) return Promise.reject('cancelled');
        return $.ajax({
            url, type: 'DELETE',
            headers: { 'RequestVerificationToken': getCsrfToken() }
        });
    });
}

/**
 * ajaxGet — GET request returning promise.
 */
function ajaxGet(url) {
    return $.ajax({ url, type: 'GET' });
}

/* ── Button loading state ────────────────────────────────────── */
function setLoading($btn, isLoading) {
    if (!$btn || !$btn.length) return;
    if (isLoading) {
        $btn.prop('disabled', true)
            .data('original-html', $btn.html())
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');
    } else {
        $btn.prop('disabled', false)
            .html($btn.data('original-html') || $btn.html());
    }
}

/* ── Form validation error renderer ─────────────────────────── */
function showFormErrors(errors, formSelector) {
    clearFormErrors(formSelector);
    if (!errors || !errors.length) return;
    if (typeof errors === 'string') errors = [errors];
    Swal.fire({
        icon: 'error',
        title: 'Validation Error',
        html: '<ul style="text-align:left;margin:0;padding-left:1.25rem;">'
            + errors.map(e => `<li style="margin:.25rem 0;font-size:.9rem;">${e}</li>`).join('')
            + '</ul>',
        confirmButtonColor: '#2563EB',
        customClass: { popup: 'swal-wide' }
    });
}

function clearFormErrors(formSelector) {
    if (formSelector) {
        $(formSelector + ' .is-invalid').removeClass('is-invalid');
        $(formSelector + ' .invalid-feedback').text('');
    }
}

/* ── Handle standard Ajax response ──────────────────────────── */
function handleResponse(res, opts = {}) {
    if (res.success) {
        if (opts.successMsg || res.message)
            toastSuccess(opts.successMsg || res.message);
        if (typeof opts.onSuccess === 'function')
            opts.onSuccess(res.data);
    } else {
        const errors = res.errors?.length ? res.errors : [res.message || 'An error occurred.'];
        if (opts.form) showFormErrors(errors, opts.form);
        else toastError(errors[0]);
        if (typeof opts.onError === 'function')
            opts.onError(res);
    }
}

/* ── Modal helpers ───────────────────────────────────────────── */
function openModal(id) { new bootstrap.Modal(document.getElementById(id)).show(); }

function closeModal(id) {
    const el = document.getElementById(id);
    const modal = bootstrap.Modal.getInstance(el);
    if (modal) modal.hide();
}

function loadModalContent(modalBodyId, url) {
    const $body = $('#' + modalBodyId);
    $body.html('<div class="text-center p-4"><span class="spinner-border text-primary"></span></div>');
    return ajaxGet(url).done(html => $body.html(html));
}

/* ── Sidebar toggle ──────────────────────────────────────────── */
$(function () {
    // Hamburger toggle (mobile)
    $('#sidebarToggle').on('click', function () {
        $('.sidebar').toggleClass('open');
        $('.sidebar-overlay').toggleClass('active');
    });

    // Close sidebar when overlay clicked
    $('.sidebar-overlay').on('click', function () {
        $('.sidebar').removeClass('open');
        $(this).removeClass('active');
    });

    // Close sidebar when a link is clicked on mobile
    $(document).on('click', '.sidebar-link', function () {
        if (window.innerWidth < 992) {
            $('.sidebar').removeClass('open');
            $('.sidebar-overlay').removeClass('active');
        }
    });

    // Auto-dismiss TempData toast notifications (set in BaseController)
    const successToast = $('#tempSuccessToast').val();
    const errorToast = $('#tempErrorToast').val();
    if (successToast) toastSuccess(successToast);
    if (errorToast) toastError(errorToast);

    // Auto-refresh access token 5 min before expiry (every 55 minutes)
    setInterval(function () {
        $.post('/Account/RefreshToken').fail(function (xhr) {
            if (xhr.status === 401) {
                Swal.fire({
                    icon: 'warning',
                    title: 'Session Expired',
                    text: 'Your session has expired. Please sign in again.',
                    confirmButtonText: 'Sign In',
                    confirmButtonColor: '#2563EB',
                    allowOutsideClick: false
                }).then(() => { window.location.href = '/Account/Login'; });
            }
        });
    }, 55 * 60 * 1000);
});

/* ── Format currency ─────────────────────────────────────────── */
function formatCurrency(amount, code) {
    code = code || 'USD';
    return new Intl.NumberFormat('en-US', {
        style: 'currency', currency: code, minimumFractionDigits: 2
    }).format(amount || 0);
}

/* ── Format date ─────────────────────────────────────────────── */
function formatDate(dateStr) {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('en-US', {
        year: 'numeric', month: 'short', day: 'numeric'
    });
}

/* ── Status badge builder ────────────────────────────────────── */
function statusBadge(statusName, badgeClass) {
    const map = {
        'Draft': 'draft',
        'Sent': 'sent',
        'Paid': 'paid',
        'Overdue': 'overdue',
        'Cancelled': 'cancelled',
        'PartiallyPaid': 'partial'
    };
    const cls = map[statusName] || 'draft';
    return `<span class="status-badge ${cls}">${statusName}</span>`;
}

/* ── DataTable defaults ──────────────────────────────────────── */
if (typeof $.fn.DataTable !== 'undefined') {
    $.extend(true, $.fn.dataTable.defaults, {
        language: {
            search: '',
            searchPlaceholder: 'Search...',
            lengthMenu: '_MENU_ per page',
            info: 'Showing _START_ to _END_ of _TOTAL_ entries',
            paginate: {
                previous: '<i class="bi bi-chevron-left"></i>',
                next: '<i class="bi bi-chevron-right"></i>'
            }
        },
        pageLength: 15,
        dom: "<'row align-items-center mb-3'<'col-sm-6'l><'col-sm-6 text-end'f>>" +
            "<'row'<'col-12'tr>>" +
            "<'row align-items-center mt-3'<'col-sm-6'i><'col-sm-6'p>>",
        drawCallback: function () {
            // Re-init Bootstrap tooltips after DataTable redraw
            $('[data-bs-toggle="tooltip"]').tooltip();
        }
    });
}

/* ── Bootstrap tooltip init ──────────────────────────────────── */
$(function () {
    $('[data-bs-toggle="tooltip"]').tooltip();
});

/* ── Numeric input formatter (auto strip non-numeric) ────────── */
$(document).on('input', '.numeric-input', function () {
    const val = $(this).val().replace(/[^0-9.]/g, '');
    $(this).val(val);
});

/* ── Number formatter (2 decimal places on blur) ────────────── */
$(document).on('blur', '.decimal-input', function () {
    const val = parseFloat($(this).val());
    if (!isNaN(val)) $(this).val(val.toFixed(2));
});

/* ── Theme Toggle (Dark Mode) ──────────────────────────────── */
$(function () {
    var toggle = document.getElementById('themeToggle');
    if (!toggle) return;

    // Set initial icon based on saved theme
    var saved = localStorage.getItem('theme') || 'light';
    var icon = toggle.querySelector('i');
    if (icon) icon.className = saved === 'dark' ? 'bi bi-sun' : 'bi bi-moon';

    toggle.addEventListener('click', function () {
        var html = document.documentElement;
        var next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-theme', next);
        localStorage.setItem('theme', next);
        if (icon) icon.className = next === 'dark' ? 'bi bi-sun' : 'bi bi-moon';
    });
});

/* ── TempData server toast ─────────────────────────────────── */
$(function () {
    var st = document.getElementById('serverToast');
    if (st) showToast(st.dataset.type, st.dataset.msg);

    // Global 401/403 handler
    $(document).ajaxError(function (e, xhr) {
        if (xhr.status === 401) window.location.href = '/Account/Login';
        else if (xhr.status === 403) showToast('error', 'Permission denied.');
    });
});

/* ── Chart.js Color Helpers ────────────────────────────────── */
window.ChartColors = {
    primary: '#2563EB',
    primaryRGB: '37,99,235',
    accent: '#F97316',
    accentRGB: '249,115,22',
    success: '#10B981',
    warning: '#F59E0B',
    danger: '#EF4444',
    info: '#3B82F6',
    muted: '#6B7280',
    grid: function () {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? '#374151' : '#F3F4F6';
    },
    text: function () {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? '#9CA3AF' : '#6B7280';
    }
};


///* ── PJAX — Sidebar navigation without full page reload ──────── */
//(function () {
//    const CONTENT = '.content-wrapper';
//    const NAV     = '.sidebar .nav-link';

//    // ── Load page via AJAX and swap content ───────────────────
//    function pjaxLoad(url, push) {
//        // Show subtle loading spinner in content area
//        $(CONTENT).css({ opacity: '0.4', pointerEvents: 'none' });

//        $.get(url)
//            .done(function (html) {
//                try {
//                    var doc = new DOMParser().parseFromString(html, 'text/html');

//                    // 1. Swap content
//                    var newContent = doc.querySelector(CONTENT);
//                    if (!newContent) { window.location.href = url; return; }
//                    $(CONTENT).html(newContent.innerHTML)
//                              .css({ opacity: '1', pointerEvents: '' });

//                    // 2. Update page title
//                    document.title = doc.title || 'Finovexa';

//                    // 3. Update navbar title
//                    var navTitle = doc.querySelector('.navbar-title');
//                    if (navTitle) $('.navbar-title').text(navTitle.textContent);

//                    // 4. Update sidebar active link
//                    updateActiveNav(url, doc);

//                    // 5. Push browser history
//                    if (push !== false)
//                        history.pushState({ pjax: url }, document.title, url);

//                    // 6. Execute page-specific inline scripts
//                    doc.querySelectorAll('body script:not([src])').forEach(function (s) {
//                        try { (new Function(s.innerHTML))(); }
//                        catch (e) { console.warn('[PJAX] Script error:', e); }
//                    });

//                    // 7. Re-init Bootstrap tooltips
//                    document.querySelectorAll('[data-bs-toggle="tooltip"]')
//                        .forEach(function (el) {
//                            bootstrap.Tooltip.getOrCreateInstance(el);
//                        });

//                    // 8. TempData server toasts
//                    var st = document.getElementById('serverToast');
//                    if (st) showToast(st.dataset.type, st.dataset.msg);

//                    // 9. Scroll top + close mobile sidebar
//                    window.scrollTo(0, 0);
//                    $('.sidebar').removeClass('open');
//                    $('.sidebar-overlay').removeClass('active');

//                } catch (err) {
//                    console.warn('[PJAX] Parse error, falling back:', err);
//                    window.location.href = url;
//                }
//            })
//            .fail(function () {
//                // Network error or server error — fall back to normal navigation
//                window.location.href = url;
//            });
//    }

//    // ── Sync sidebar active state with loaded page ─────────────
//    function updateActiveNav(url, doc) {
//        // Read active menu from loaded page's body data or nav-link class
//        var loadedActive = doc.querySelector('.nav-link.active');
//        var activeHref   = loadedActive ? loadedActive.getAttribute('href') : null;

//        $(NAV).removeClass('active');

//        if (activeHref) {
//            $(NAV + '[href="' + activeHref + '"]').addClass('active');
//        } else {
//            // Fallback: match by URL prefix
//            var path = url.split('?')[0];
//            $(NAV).each(function () {
//                var href = $(this).attr('href') || '';
//                if (href !== '/' && path.startsWith(href)) {
//                    $(this).addClass('active');
//                    return false; // break
//                }
//            });
//        }
//    }

//    // ── Intercept sidebar link clicks ─────────────────────────
//    //$(document).on('click', NAV, function (e) {
//    //    var href = $(this).attr('href');

//    //    // Skip: no href, external, anchor-only, javascript:
//    //    if (!href || href === '#' || href.startsWith('http')
//    //              || href.startsWith('javascript') || href.startsWith('mailto'))
//    //        return;

//    //    // Skip: same page
//    //    if (href === window.location.pathname + window.location.search)
//    //        return;

//    //    e.preventDefault();
//    //    pjaxLoad(href, true);
//    //});

//    $(document).on('click', NAV, function (e) {

//        // 🚨 IMPORTANT: skip PJAX
//        if ($(this).hasClass('no-pjax')) return;

//        var href = $(this).attr('href');

//        if (!href || href.startsWith('http')) return;

//        e.preventDefault();
//        pjaxLoad(href, true);
//    });


//    // ── Browser Back / Forward support ────────────────────────
//    window.addEventListener('popstate', function (e) {
//        if (e.state && e.state.pjax) {
//            pjaxLoad(e.state.pjax, false);
//        } else {
//            // No PJAX state — just reload normally
//            window.location.reload();
//        }
//    });

//    // ── Mark initial page state ───────────────────────────────
//    history.replaceState(
//        { pjax: window.location.href },
//        document.title,
//        window.location.href
//    );

//})();