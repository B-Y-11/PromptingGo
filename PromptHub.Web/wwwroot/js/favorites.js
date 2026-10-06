document.addEventListener(
    "DOMContentLoaded",
    () => {
        const forms =
            document.querySelectorAll(
                ".favorite-toggle-form");

        forms.forEach(form => {
            form.addEventListener(
                "submit",
                async event => {
                    event.preventDefault();

                    const button =
                        form.querySelector(
                            ".favorite-toggle-button");

                    if (!button ||
                        button.disabled) {
                        return;
                    }

                    const previousText =
                        button.textContent;

                    button.disabled = true;

                    try {
                        const response =
                            await fetch(
                                form.action,
                                {
                                    method: "POST",

                                    body:
                                        new FormData(
                                            form),

                                    headers: {
                                        "X-Requested-With":
                                            "XMLHttpRequest"
                                    }
                                });

                        if (!response.ok) {
                            throw new Error(
                                "Не удалось изменить избранное.");
                        }

                        const result =
                            await response.json();

                        updateFavoriteButton(
                            button,
                            result.isFavorite);

                        const shouldRemoveCard =
                            form.dataset
                                .removeCard ===
                            "true";

                        if (!result.isFavorite &&
                            shouldRemoveCard) {
                            removeFavoriteCard(
                                form);
                        }
                    }
                    catch (error) {
                        console.error(error);

                        button.textContent =
                            "Не удалось";

                        window.setTimeout(
                            () => {
                                button.textContent =
                                    previousText;
                            },
                            1400);
                    }
                    finally {
                        button.disabled = false;
                    }
                });
        });

        function updateFavoriteButton(
            button,
            isFavorite) {
            const addLabel =
                button.dataset.addLabel ??
                "В избранное";

            const removeLabel =
                button.dataset.removeLabel ??
                "В избранном";

            button.textContent =
                isFavorite
                    ? removeLabel
                    : addLabel;

            button.setAttribute(
                "aria-pressed",
                String(isFavorite));

            button.classList.toggle(
                "btn-primary",
                isFavorite);

            button.classList.toggle(
                "btn-outline-primary",
                !isFavorite);

            button.classList.toggle(
                "is-favorite",
                isFavorite);
        }

        function removeFavoriteCard(form) {
            const card =
                form.closest(
                    "[data-favorite-card]");

            if (card) {
                card.remove();
            }

            const countElement =
                document.querySelector(
                    "[data-favorites-count]");

            let count = 0;

            if (countElement) {
                const currentCount =
                    Number(
                        countElement.textContent);

                count =
                    Math.max(
                        0,
                        currentCount - 1);

                countElement.textContent =
                    String(count);
            }

            const remainingCards =
                document.querySelectorAll(
                    "[data-favorite-card]");

            if (remainingCards.length > 0) {
                return;
            }

            const grid =
                document.querySelector(
                    "[data-favorites-grid]");

            const emptyState =
                document.querySelector(
                    "[data-favorites-empty]");

            grid?.classList.add(
                "d-none");

            emptyState?.classList.remove(
                "d-none");
        }
    });