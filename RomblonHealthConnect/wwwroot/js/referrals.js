/* ==========================================================================
   Romblon HealthConnect — Referral module shared behaviour
   Notification centre, toasts, filter bar, and formatting helpers.
   Loaded on every referral page; the wizard and real-time layers build on it.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};
    var Referrals = RHC.referrals = RHC.referrals || {};

    /* ----------------------------------------------------------------------
       1. Helpers
       ---------------------------------------------------------------------- */

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    /** Short relative label such as "4 minutes ago". */
    function timeAgo(isoString) {
        var then = new Date(isoString);
        if (isNaN(then.getTime())) { return ''; }

        var minutes = Math.floor((Date.now() - then.getTime()) / 60000);

        if (minutes < 1) { return 'just now'; }
        if (minutes < 60) { return minutes + (minutes === 1 ? ' minute ago' : ' minutes ago'); }

        var hours = Math.floor(minutes / 60);
        if (hours < 24) { return hours + (hours === 1 ? ' hour ago' : ' hours ago'); }

        var days = Math.floor(hours / 24);
        return days + (days === 1 ? ' day ago' : ' days ago');
    }

    /** Reads the anti-forgery token rendered by any form on the page. */
    function antiForgeryToken() {
        var field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : null;
    }

    async function postJson(url) {
        var token = antiForgeryToken();
        var headers = {};
        if (token) { headers.RequestVerificationToken = token; }

        return fetch(url, { method: 'POST', headers: headers, credentials: 'same-origin' });
    }

    async function getJson(url) {
        var response = await fetch(url, {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        });

        if (!response.ok) {
            throw new Error('Request failed with status ' + response.status);
        }

        return response.json();
    }

    /* ----------------------------------------------------------------------
       2. Toasts
       ---------------------------------------------------------------------- */

    function toastStack() {
        var stack = document.getElementById('toastStack');

        if (!stack) {
            stack = document.createElement('div');
            stack.id = 'toastStack';
            stack.className = 'toast-stack';
            stack.setAttribute('role', 'status');
            stack.setAttribute('aria-live', 'polite');
            document.body.appendChild(stack);
        }

        return stack;
    }

    function showToast(title, message, icon) {
        var stack = toastStack();

        var toast = document.createElement('div');
        toast.className = 'toast';
        toast.innerHTML =
            '<span class="notification-icon"><i class="fa-solid ' + escapeHtml(icon || 'fa-bell') + '"></i></span>' +
            '<span class="notification-body">' +
                '<span class="toast-title">' + escapeHtml(title) + '</span>' +
                '<span class="toast-message">' + escapeHtml(message) + '</span>' +
            '</span>';

        stack.appendChild(toast);

        window.setTimeout(function () {
            toast.remove();
        }, 6000);
    }

    /* ----------------------------------------------------------------------
       3. Notification centre
       ---------------------------------------------------------------------- */

    var notifications = {
        panel: null,
        list: null,
        badge: null,
        button: null,
        loaded: false
    };

    function renderNotifications(payload) {
        if (!notifications.list) { return; }

        if (!payload.items || payload.items.length === 0) {
            notifications.list.innerHTML =
                '<li class="empty-state">' +
                    '<i class="fa-regular fa-bell-slash empty-state-icon" aria-hidden="true"></i>' +
                    '<span class="empty-state-title">No notifications</span>' +
                '</li>';
        } else {
            notifications.list.innerHTML = payload.items.map(function (item) {
                var href = item.referralId ? '/Referrals/Details/' + item.referralId : null;

                var body =
                    '<span class="notification-icon"><i class="fa-solid ' + escapeHtml(item.icon) + '"></i></span>' +
                    '<span class="notification-body">' +
                        '<span class="notification-item-title">' + escapeHtml(item.title) + '</span>' +
                        '<span class="notification-message">' + escapeHtml(item.message) + '</span>' +
                        '<span class="notification-time">' + escapeHtml(timeAgo(item.createdUtc)) + '</span>' +
                    '</span>';

                return '<li class="notification-item' + (item.isRead ? '' : ' is-unread') + '">' +
                    (href ? '<a class="notification-link" href="' + href + '">' + body + '</a>' : body) +
                '</li>';
            }).join('');
        }

        updateBadge(payload.unreadCount);
    }

    function updateBadge(count) {
        if (!notifications.badge) { return; }

        if (count > 0) {
            notifications.badge.textContent = count > 99 ? '99+' : String(count);
            notifications.badge.hidden = false;
        } else {
            notifications.badge.hidden = true;
        }

        if (notifications.button) {
            notifications.button.setAttribute('aria-label',
                count > 0 ? 'Notifications, ' + count + ' unread' : 'Notifications');
        }
    }

    async function loadNotifications() {
        try {
            var payload = await getJson('/Referrals/Notifications');
            renderNotifications(payload);
            notifications.loaded = true;
        } catch (error) {
            window.console.warn('[referrals] Could not load notifications:', error);
        }
    }

    function initNotifications() {
        notifications.panel = document.getElementById('notificationPanel');
        notifications.list = document.getElementById('notificationList');
        notifications.button = document.getElementById('notificationsButton');
        notifications.badge = document.getElementById('notificationBadge');

        if (!notifications.button || !notifications.panel) { return; }

        notifications.button.addEventListener('click', function (event) {
            event.stopPropagation();

            var isOpen = notifications.panel.classList.toggle('is-open');
            notifications.button.setAttribute('aria-expanded', String(isOpen));

            if (isOpen && !notifications.loaded) {
                loadNotifications();
            }
        });

        // Dismiss when clicking away or pressing Escape.
        document.addEventListener('click', function (event) {
            if (!notifications.panel.contains(event.target) && event.target !== notifications.button) {
                notifications.panel.classList.remove('is-open');
                notifications.button.setAttribute('aria-expanded', 'false');
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                notifications.panel.classList.remove('is-open');
                notifications.button.setAttribute('aria-expanded', 'false');
            }
        });

        var markRead = document.getElementById('markAllRead');
        if (markRead) {
            markRead.addEventListener('click', async function () {
                await postJson('/Referrals/MarkNotificationsRead');
                await loadNotifications();
            });
        }

        loadNotifications();
    }

    /* ----------------------------------------------------------------------
       4. Filter bar
       ---------------------------------------------------------------------- */

    function initFilters() {
        var form = document.getElementById('referralFilters');
        if (!form) { return; }

        // Selects apply immediately; free text waits for submit.
        form.querySelectorAll('select').forEach(function (select) {
            select.addEventListener('change', function () {
                form.submit();
            });
        });

        var reset = document.getElementById('resetFilters');
        if (reset) {
            reset.addEventListener('click', function () {
                window.location.href = form.getAttribute('action') || window.location.pathname;
            });
        }
    }

    /* ----------------------------------------------------------------------
       5. Confirmation prompts on destructive actions
       ---------------------------------------------------------------------- */

    function initConfirmations() {
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (!window.confirm(form.getAttribute('data-confirm'))) {
                    event.preventDefault();
                }
            });
        });
    }

    /* ----------------------------------------------------------------------
       6. Public API
       ---------------------------------------------------------------------- */

    Referrals.escapeHtml = escapeHtml;
    Referrals.timeAgo = timeAgo;
    Referrals.getJson = getJson;
    Referrals.postJson = postJson;
    Referrals.showToast = showToast;
    Referrals.reloadNotifications = loadNotifications;
    Referrals.updateNotificationBadge = updateBadge;

    /* ----------------------------------------------------------------------
       7. Bootstrap
       ---------------------------------------------------------------------- */

    function init() {
        initNotifications();
        initFilters();
        initConfirmations();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
