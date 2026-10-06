const reduceMotionQuery =
    window.matchMedia(
        "(prefers-reduced-motion: reduce)");

const motionEnabled =
    !reduceMotionQuery.matches;

if (motionEnabled) {
    document.documentElement.classList.add(
        "motion-enabled");
}


function setupRevealMotion() {
    const selectors = [
        ".hero-section",
        ".home-feature",
        ".home-section-header",
        ".home-top-prompt-card",

        ".notebook-page-heading",

        ".identity-sheet",

        ".prompt-detail-header",
        ".prompt-paper-section",
        ".prompt-sidebar-note",

        ".account-header-sheet",
        ".account-nav-sheet",
        ".account-content-sheet",
        ".account-stat-note",

        ".prompt-form-header",
        ".prompt-form-section",
        ".prompt-form-sidebar-note",

        ".author-page-header",
        ".author-prompt-card",
        ".author-profile-header",
        ".author-profile-stat",

        ".notification-card",

        ".moderation-page-header",
        ".moderation-dashboard-stat-card",
        ".moderation-dashboard-panel",
        ".moderation-queue-card",
        ".moderation-review-header",
        ".moderation-review-sheet",

        ".admin-user-card",
        ".admin-category-card",
        ".admin-tag-card",

        ".prompt-comment-card",
        ".comment-report-card"
    ];

    const elements = [];

    selectors.forEach(selector => {
        document
            .querySelectorAll(selector)
            .forEach((element, index) => {

                if (element.classList.contains(
                    "motion-reveal")) {
                    return;
                }

                const currentOpacity =
                    window
                        .getComputedStyle(element)
                        .opacity;

                element.style.setProperty(
                    "--motion-opacity",
                    currentOpacity);

                element.style.setProperty(
                    "--motion-order",
                    String(
                        Math.min(
                            index,
                            5)));

                element.classList.add(
                    "motion-reveal");

                elements.push(element);
            });
    });

    if (!("IntersectionObserver" in window)) {
        elements.forEach(element => {
            element.classList.add(
                "is-visible");
        });

        return;
    }

    const observer =
        new IntersectionObserver(
            entries => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) {
                        return;
                    }

                    entry.target.classList.add(
                        "is-visible");

                    observer.unobserve(
                        entry.target);
                });
            },
            {
                threshold: 0.08,
                rootMargin:
                    "0px 0px -24px 0px"
            });

    elements.forEach(element => {
        observer.observe(element);
    });
}


function setupFavoriteMotion() {
    document
        .querySelectorAll(
            ".favorite-toggle-button")
        .forEach(button => {

            let previousState =
                button.classList.contains(
                    "is-favorite");

            const observer =
                new MutationObserver(() => {

                    const currentState =
                        button.classList.contains(
                            "is-favorite");

                    if (currentState ===
                        previousState) {
                        return;
                    }

                    previousState =
                        currentState;

                    if (!currentState) {
                        return;
                    }

                    button.classList.remove(
                        "is-favorite-pop");

                    void button.offsetWidth;

                    button.classList.add(
                        "is-favorite-pop");

                    window.setTimeout(
                        () => {
                            button.classList.remove(
                                "is-favorite-pop");
                        },
                        460);
                });

            observer.observe(
                button,
                {
                    attributes: true,
                    attributeFilter: [
                        "class"
                    ]
                });
        });
}


function setupCopyMotion() {
    document
        .querySelectorAll(
            ".copy-prompt-button")
        .forEach(button => {

            const observer =
                new MutationObserver(() => {

                    const text =
                        button.textContent
                            ?.trim() ?? "";

                    if (text.includes(
                        "Скопировано")) {

                        button.classList.remove(
                            "is-copy-error");

                        button.classList.add(
                            "is-copy-success");

                        window.setTimeout(
                            () => {
                                button.classList.remove(
                                    "is-copy-success");
                            },
                            600);

                        return;
                    }

                    if (text === "Ошибка") {
                        button.classList.remove(
                            "is-copy-success");

                        button.classList.add(
                            "is-copy-error");

                        window.setTimeout(
                            () => {
                                button.classList.remove(
                                    "is-copy-error");
                            },
                            380);
                    }
                });

            observer.observe(
                button,
                {
                    childList: true,
                    characterData: true,
                    subtree: true
                });
        });
}


function setupDynamicFieldMotion() {
    document
        .querySelectorAll(
            ".visual-fields-list")
        .forEach(list => {

            let previousCount =
                list.children.length;

            const observer =
                new MutationObserver(() => {

                    const currentCount =
                        list.children.length;

                    if (currentCount >
                        previousCount) {

                        const addedField =
                            list.querySelector(
                                ".visual-field-note:last-child");

                        if (addedField) {
                            addedField.classList.add(
                                "motion-added");

                            window.setTimeout(
                                () => {
                                    addedField.classList.remove(
                                        "motion-added");
                                },
                                420);
                        }
                    }

                    previousCount =
                        currentCount;
                });

            observer.observe(
                list,
                {
                    childList: true
                });
        });
}


function setupLightboxClosingMotion() {
    const closeWithAnimation =
        dialog => {

            if (!(dialog instanceof
                HTMLDialogElement) ||
                !dialog.open ||
                dialog.classList.contains(
                    "is-closing")) {
                return;
            }

            dialog.classList.add(
                "is-closing");

            window.setTimeout(
                () => {

                    if (dialog.open) {
                        dialog.close();
                    }

                    dialog.classList.remove(
                        "is-closing");
                },
                175);
        };


    document.addEventListener(
        "click",
        event => {

            if (!(event.target instanceof
                Element)) {
                return;
            }

            const closeButton =
                event.target.closest(
                    ".prompt-media-lightbox-close");

            if (closeButton) {
                const dialog =
                    closeButton.closest(
                        ".prompt-media-lightbox");

                if (dialog instanceof
                    HTMLDialogElement) {

                    event.preventDefault();
                    event.stopPropagation();

                    closeWithAnimation(
                        dialog);
                }

                return;
            }

            if (event.target instanceof
                HTMLDialogElement &&
                event.target.classList.contains(
                    "prompt-media-lightbox")) {

                event.preventDefault();
                event.stopPropagation();

                closeWithAnimation(
                    event.target);
            }
        },
        true);


    document
        .querySelectorAll(
            ".prompt-media-lightbox")
        .forEach(dialog => {

            if (!(dialog instanceof
                HTMLDialogElement)) {
                return;
            }

            dialog.addEventListener(
                "cancel",
                event => {

                    event.preventDefault();

                    closeWithAnimation(
                        dialog);
                });
        });
}


function formatLocalDates() {
    const locale =
        document.documentElement.lang ||
        navigator.language ||
        "ru";

    const dateFormatter =
        new Intl.DateTimeFormat(
            locale,
            {
                day: "2-digit",
                month: "2-digit",
                year: "numeric"
            });

    const dateTimeFormatter =
        new Intl.DateTimeFormat(
            locale,
            {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });

    const fullDateTimeFormatter =
        new Intl.DateTimeFormat(
            locale,
            {
                dateStyle: "full",
                timeStyle: "long"
            });

    document
        .querySelectorAll(
            "time[data-local-date], " +
            "time[data-local-datetime]")
        .forEach(element => {

            const rawValue =
                element.getAttribute(
                    "datetime");

            if (!rawValue) {
                return;
            }

            const date =
                new Date(rawValue);

            if (Number.isNaN(
                date.getTime())) {
                return;
            }

            const showTime =
                element.hasAttribute(
                    "data-local-datetime");

            element.textContent =
                showTime
                    ? dateTimeFormatter
                        .format(date)
                    : dateFormatter
                        .format(date);

            element.title =
                fullDateTimeFormatter
                    .format(date);
        });
}


if (motionEnabled) {
    setupRevealMotion();
    setupFavoriteMotion();
    setupCopyMotion();
    setupDynamicFieldMotion();
    setupLightboxClosingMotion();
}


document.addEventListener(
    "DOMContentLoaded",
    () => {

        formatLocalDates();
        setupCatalogNavigation();

        if (!motionEnabled) {
            return;
        }

        requestAnimationFrame(
            () => {
                requestAnimationFrame(
                    () => {
                        document
                            .documentElement
                            .classList
                            .add(
                                "motion-ready");
                    });
            });
    });


function setupCatalogNavigation() {
    const items =
        Array.from(
            document.querySelectorAll(
                ".catalog-nav-item"));

    if (items.length === 0) {
        return;
    }

    const hoverMedia =
        window.matchMedia(
            "(hover: hover) and (pointer: fine)");

    const openDelayMs = 180;
    const closeDelayMs = 240;

    items.forEach(item => {
        const panel =
            item.querySelector(
                ".catalog-nav-mega");

        const mainLink =
            item.querySelector(
                ".catalog-nav-main-link");

        if (!(panel instanceof HTMLElement)) {
            return;
        }

        let openTimer = null;
        let closeTimer = null;

        const clearOpenTimer = () => {
            if (openTimer === null) {
                return;
            }

            window.clearTimeout(openTimer);
            openTimer = null;
        };

        const clearCloseTimer = () => {
            if (closeTimer === null) {
                return;
            }

            window.clearTimeout(closeTimer);
            closeTimer = null;
        };

        const open = () => {
            clearOpenTimer();
            clearCloseTimer();

            item.classList.add("is-open");
            panel.setAttribute("aria-hidden", "false");
        };

        const close = () => {
            clearOpenTimer();
            clearCloseTimer();

            item.classList.remove("is-open");
            panel.setAttribute("aria-hidden", "true");
        };

        const scheduleOpen = () => {
            if (!hoverMedia.matches) {
                return;
            }

            clearCloseTimer();

            if (item.classList.contains("is-open")) {
                return;
            }

            clearOpenTimer();
            openTimer = window.setTimeout(
                open,
                openDelayMs);
        };

        const scheduleClose = () => {
            clearOpenTimer();
            clearCloseTimer();

            closeTimer = window.setTimeout(
                close,
                closeDelayMs);
        };

        panel.setAttribute("aria-hidden", "true");

        item.addEventListener(
            "mouseenter",
            scheduleOpen);

        item.addEventListener(
            "mouseleave",
            scheduleClose);

        panel.addEventListener(
            "mouseenter",
            () => {
                if (!hoverMedia.matches) {
                    return;
                }

                clearCloseTimer();
                open();
            });

        panel.addEventListener(
            "mouseleave",
            scheduleClose);

        item.addEventListener(
            "focusin",
            () => {
                if (hoverMedia.matches) {
                    open();
                }
            });

        item.addEventListener(
            "focusout",
            event => {
                const nextTarget = event.relatedTarget;

                if (nextTarget instanceof Node &&
                    item.contains(nextTarget)) {
                    return;
                }

                scheduleClose();
            });

        item.addEventListener(
            "keydown",
            event => {
                if (event.key !== "Escape") {
                    return;
                }

                close();

                if (mainLink instanceof HTMLElement) {
                    mainLink.focus();
                }
            });
    });
}
