(() => {
    "use strict";

    // -------------------------------------------------------------------------
    // EN: Prompt media lightbox initialization
    // RU: Инициализация просмотра медиа промпта
    // -------------------------------------------------------------------------

    const dialog =
        document.getElementById(
            "promptMediaLightbox");

    const items =
        Array.from(
            document.querySelectorAll(
                "[data-prompt-lightbox-item]"));

    if (!(dialog instanceof HTMLDialogElement) ||
        items.length === 0) {
        return;
    }

    const image =
        dialog.querySelector(
            "[data-lightbox-image]");

    const caption =
        dialog.querySelector(
            "[data-lightbox-caption]");

    const position =
        dialog.querySelector(
            "[data-lightbox-position]");

    const status =
        dialog.querySelector(
            "[data-lightbox-status]");

    const closeButton =
        dialog.querySelector(
            "[data-lightbox-close]");

    const previousButton =
        dialog.querySelector(
            "[data-lightbox-previous]");

    const nextButton =
        dialog.querySelector(
            "[data-lightbox-next]");

    if (!(image instanceof HTMLImageElement) ||
        !(caption instanceof HTMLElement) ||
        !(position instanceof HTMLElement) ||
        !(status instanceof HTMLElement) ||
        !(closeButton instanceof HTMLButtonElement) ||
        !(previousButton instanceof HTMLButtonElement) ||
        !(nextButton instanceof HTMLButtonElement)) {
        return;
    }

    let activeIndex =
        0;

    let lastTrigger =
        null;

    let imageRequestId =
        0;



    // -------------------------------------------------------------------------
    // EN: Image zoom
    // RU: Увеличение изображения
    // -------------------------------------------------------------------------

    const resetImageZoom = () => {
        image.classList.remove(
            "is-zoomed");

        image.style.removeProperty(
            "--prompt-media-zoom-x");

        image.style.removeProperty(
            "--prompt-media-zoom-y");
    };

    image.addEventListener(
        "click",
        event => {
            if (image.classList.contains("is-loading")) {
                return;
            }

            if (image.classList.contains("is-zoomed")) {
                resetImageZoom();
                return;
            }

            const rect =
                image.getBoundingClientRect();

            const x =
                ((event.clientX - rect.left) /
                    rect.width) * 100;

            const y =
                ((event.clientY - rect.top) /
                    rect.height) * 100;

            image.style.setProperty(
                "--prompt-media-zoom-x",
                `${x}%`);

            image.style.setProperty(
                "--prompt-media-zoom-y",
                `${y}%`);

            image.classList.add(
                "is-zoomed");
        });

    // -------------------------------------------------------------------------
    // EN: Active image rendering
    // RU: Отображение активного изображения
    // -------------------------------------------------------------------------

    const renderActiveItem = () => {
        resetImageZoom();

        const item =
            items[activeIndex];

        const fullUrl =
            item.dataset.fullUrl ??
            item.getAttribute("href") ??
            "";

        const previewUrl =
            item.dataset.previewUrl ??
            fullUrl;

        const altText =
            item.dataset.alt ??
            "Пример результата промпта";

        const captionText =
            item.dataset.caption?.trim() ??
            "";

        imageRequestId += 1;

        const currentRequestId =
            imageRequestId;

        image.alt =
            altText;

        image.src =
            previewUrl;

        image.classList.add(
            "is-loading");

        status.hidden =
            false;

        status.textContent =
            "Загрузка оригинала…";

        position.textContent =
            `${activeIndex + 1} из ${items.length}`;

        caption.textContent =
            captionText;

        caption.hidden =
            captionText.length === 0;

        const fullImage =
            new Image();

        fullImage.onload = () => {
            if (currentRequestId !==
                imageRequestId) {
                return;
            }

            image.src =
                fullUrl;

            image.classList.remove(
                "is-loading");

            status.hidden =
                true;
        };

        fullImage.onerror = () => {
            if (currentRequestId !==
                imageRequestId) {
                return;
            }

            image.classList.remove(
                "is-loading");

            status.hidden =
                false;

            status.textContent =
                "Не удалось загрузить оригинал. Показана миниатюра.";
        };

        fullImage.src =
            fullUrl;

        const hasMultipleItems =
            items.length > 1;

        previousButton.hidden =
            !hasMultipleItems;

        nextButton.hidden =
            !hasMultipleItems;
    };

    // -------------------------------------------------------------------------
    // EN: Lightbox opening and closing
    // RU: Открытие и закрытие окна просмотра
    // -------------------------------------------------------------------------

    const openLightbox = (
        index,
        trigger) => {
        activeIndex =
            index;

        lastTrigger =
            trigger;

        renderActiveItem();

        if (!dialog.open) {
            dialog.showModal();
        }

        closeButton.focus();
    };

    const closeLightbox = () => {
        if (dialog.open) {
            dialog.close();
        }
    };

    const showPrevious = () => {
        activeIndex =
            activeIndex === 0
                ? items.length - 1
                : activeIndex - 1;

        renderActiveItem();
    };

    const showNext = () => {
        activeIndex =
            activeIndex ===
                items.length - 1
                ? 0
                : activeIndex + 1;

        renderActiveItem();
    };

    // -------------------------------------------------------------------------
    // EN: Gallery item activation
    // RU: Активация изображения галереи
    // -------------------------------------------------------------------------

    items.forEach(
        (item, index) => {
            item.addEventListener(
                "click",
                event => {
                    if (!(event instanceof MouseEvent)) {
                        return;
                    }

                    // EN: Preserve opening in a new tab with modifier keys.
                    // RU: Сохраняем открытие в новой вкладке с модификаторами.

                    if (event.button !== 0 ||
                        event.ctrlKey ||
                        event.metaKey ||
                        event.shiftKey ||
                        event.altKey) {
                        return;
                    }

                    event.preventDefault();

                    openLightbox(
                        index,
                        item);
                });
        });

    closeButton.addEventListener(
        "click",
        closeLightbox);

    previousButton.addEventListener(
        "click",
        showPrevious);

    nextButton.addEventListener(
        "click",
        showNext);

    // -------------------------------------------------------------------------
    // EN: Backdrop click and keyboard navigation
    // RU: Закрытие по фону и управление клавиатурой
    // -------------------------------------------------------------------------

    dialog.addEventListener(
        "click",
        event => {
            if (event.target === dialog) {
                closeLightbox();
            }
        });

    dialog.addEventListener(
        "keydown",
        event => {
            if (event.key === "ArrowLeft") {
                event.preventDefault();
                showPrevious();
            }

            if (event.key === "ArrowRight") {
                event.preventDefault();
                showNext();
            }
        });

    dialog.addEventListener(
        "close",
        () => {
            imageRequestId += 1;

            resetImageZoom();

            image.removeAttribute(
                "src");

            status.hidden =
                true;

            if (lastTrigger instanceof HTMLElement) {
                lastTrigger.focus();
            }
        });
})();
