(() => {
    "use strict";

    // -------------------------------------------------------------------------
    // EN: Prompt media ordering initialization
    // RU: Инициализация сортировки медиа промпта
    // -------------------------------------------------------------------------

    const component =
        document.querySelector(
            "[data-media-order-component]");

    if (!(component instanceof HTMLElement)) {
        return;
    }

    const list =
        component.querySelector(
            "[data-media-order-list]");

    const form =
        component.querySelector(
            "[data-media-order-form]");

    const inputsContainer =
        component.querySelector(
            "[data-media-order-inputs]");

    const saveButton =
        component.querySelector(
            "[data-media-order-save]");

    const resetButton =
        component.querySelector(
            "[data-media-order-reset]");

    const status =
        component.querySelector(
            "[data-media-order-status]");

    if (!(list instanceof HTMLElement) ||
        !(form instanceof HTMLFormElement) ||
        !(inputsContainer instanceof HTMLElement) ||
        !(saveButton instanceof HTMLButtonElement) ||
        !(resetButton instanceof HTMLButtonElement) ||
        !(status instanceof HTMLElement)) {
        return;
    }

    const getItems = () =>
        Array.from(
            list.querySelectorAll(
                "[data-media-order-item]"))
            .filter(item =>
                item instanceof HTMLElement);

    const getOrder = () =>
        getItems()
            .map(item =>
                item.dataset.mediaId ?? "")
            .filter(mediaId =>
                mediaId.length > 0);

    const initialOrder =
        getOrder();

    let draggedItem =
        null;

    // -------------------------------------------------------------------------
    // EN: Hidden form values and visual position synchronization
    // RU: Синхронизация скрытых полей и отображаемых позиций
    // -------------------------------------------------------------------------

    const synchronize = () => {
        const items =
            getItems();

        inputsContainer.replaceChildren();

        items.forEach(
            (item, index) => {
                const mediaId =
                    item.dataset.mediaId;

                if (!mediaId) {
                    return;
                }

                const input =
                    document.createElement(
                        "input");

                input.type =
                    "hidden";

                input.name =
                    "OrderedMediaIds";

                input.value =
                    mediaId;

                inputsContainer.append(
                    input);

                const position =
                    item.querySelector(
                        "[data-media-order-position]");

                if (position instanceof HTMLElement) {
                    position.textContent =
                        String(index + 1);
                }
            });

        const currentOrder =
            getOrder();

        const hasChanges =
            currentOrder.length ===
            initialOrder.length &&
            currentOrder.some(
                (mediaId, index) =>
                    mediaId !==
                    initialOrder[index]);

        saveButton.disabled =
            !hasChanges;

        resetButton.disabled =
            !hasChanges;

        status.textContent =
            hasChanges
                ? "Порядок изменён"
                : "Изменений нет";

        component.classList.toggle(
            "has-unsaved-order",
            hasChanges);
    };

    // -------------------------------------------------------------------------
    // EN: Drag-and-drop interaction
    // RU: Перетаскивание изображений
    // -------------------------------------------------------------------------

    getItems().forEach(
        item => {
            item.addEventListener(
                "dragstart",
                event => {
                    draggedItem =
                        item;

                    item.classList.add(
                        "is-dragging");

                    if (event.dataTransfer) {
                        event.dataTransfer.effectAllowed =
                            "move";

                        event.dataTransfer.setData(
                            "text/plain",
                            item.dataset.mediaId ?? "");
                    }
                });

            item.addEventListener(
                "dragend",
                () => {
                    item.classList.remove(
                        "is-dragging");

                    draggedItem =
                        null;

                    getItems().forEach(
                        currentItem =>
                            currentItem.classList.remove(
                                "is-drag-target"));

                    synchronize();
                });

            item.addEventListener(
                "dragenter",
                () => {
                    if (draggedItem !== item) {
                        item.classList.add(
                            "is-drag-target");
                    }
                });

            item.addEventListener(
                "dragleave",
                event => {
                    if (!item.contains(
                        event.relatedTarget)) {
                        item.classList.remove(
                            "is-drag-target");
                    }
                });
        });

    list.addEventListener(
        "dragover",
        event => {
            if (!(draggedItem instanceof HTMLElement)) {
                return;
            }

            event.preventDefault();

            if (event.dataTransfer) {
                event.dataTransfer.dropEffect =
                    "move";
            }

            const target =
                event.target instanceof Element
                    ? event.target.closest(
                        "[data-media-order-item]")
                    : null;

            if (!(target instanceof HTMLElement) ||
                target === draggedItem) {
                return;
            }

            const targetRectangle =
                target.getBoundingClientRect();

            const targetMiddleY =
                targetRectangle.top +
                targetRectangle.height / 2;

            const targetMiddleX =
                targetRectangle.left +
                targetRectangle.width / 2;

            const verticalDistance =
                Math.abs(
                    event.clientY -
                    targetMiddleY);

            const useVerticalPosition =
                verticalDistance >
                targetRectangle.height * 0.25;

            const insertBeforeTarget =
                useVerticalPosition
                    ? event.clientY <
                    targetMiddleY
                    : event.clientX <
                    targetMiddleX;

            if (insertBeforeTarget) {
                list.insertBefore(
                    draggedItem,
                    target);
            } else {
                list.insertBefore(
                    draggedItem,
                    target.nextSibling);
            }
        });

    list.addEventListener(
        "drop",
        event => {
            event.preventDefault();

            getItems().forEach(
                item =>
                    item.classList.remove(
                        "is-drag-target"));

            synchronize();
        });

    // -------------------------------------------------------------------------
    // EN: Reset and submission state
    // RU: Сброс порядка и состояние отправки
    // -------------------------------------------------------------------------

    resetButton.addEventListener(
        "click",
        () => {
            const itemsById =
                new Map(
                    getItems().map(
                        item => [
                            item.dataset.mediaId,
                            item
                        ]));

            initialOrder.forEach(
                mediaId => {
                    const item =
                        itemsById.get(
                            mediaId);

                    if (item) {
                        list.append(
                            item);
                    }
                });

            synchronize();
        });

    form.addEventListener(
        "submit",
        () => {
            saveButton.disabled =
                true;

            resetButton.disabled =
                true;

            status.textContent =
                "Сохраняем порядок…";
        });

    synchronize();
})();