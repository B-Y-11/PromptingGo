document.addEventListener(
    "DOMContentLoaded",
    () => {
        const forms =
            document.querySelectorAll(
                ".prompt-rating-form");

        forms.forEach(form => {
            form.addEventListener(
                "submit",
                async event => {
                    event.preventDefault();

                    const submitter =
                        event.submitter;

                    const score =
                        submitter?.dataset.score;

                    if (!score) {
                        return;
                    }

                    const formData =
                        new FormData(form);

                    formData.set(
                        "score",
                        score);

                    await sendRatingRequest(
                        form,
                        form.action,
                        formData);
                });

            const clearButton =
                form.querySelector(
                    ".prompt-rating-clear");

            clearButton?.addEventListener(
                "click",
                async () => {
                    const clearAction =
                        form.dataset.clearAction;

                    if (!clearAction) {
                        return;
                    }

                    const formData =
                        new FormData(form);

                    await sendRatingRequest(
                        form,
                        clearAction,
                        formData);
                });
        });

        async function sendRatingRequest(
            form,
            action,
            formData) {
            const buttons =
                form.querySelectorAll(
                    "button");

            buttons.forEach(button =>
                button.disabled = true);

            try {
                const response =
                    await fetch(
                        action,
                        {
                            method: "POST",
                            body: formData,
                            headers: {
                                "X-Requested-With":
                                    "XMLHttpRequest"
                            }
                        });

                if (!response.ok) {
                    throw new Error(
                        "Не удалось сохранить оценку.");
                }

                const result =
                    await response.json();

                updateRatingPanel(
                    form.closest(
                        "[data-rating-panel]"),
                    result);
            }
            catch (error) {
                console.error(error);
            }
            finally {
                buttons.forEach(button =>
                    button.disabled = false);
            }
        }

        function updateRatingPanel(
            panel,
            result) {
            if (!panel) {
                return;
            }

            const averageElement =
                panel.querySelector(
                    "[data-rating-average]");

            const countElement =
                panel.querySelector(
                    "[data-rating-count]");

            const userRatingElement =
                panel.querySelector(
                    "[data-user-rating]");

            const buttons =
                panel.querySelectorAll(
                    ".prompt-rating-star");

            const currentRating =
                result.currentUserRating;

            if (averageElement) {
                averageElement.textContent =
                    result.ratingCount > 0
                        ? Number(
                            result.averageRating)
                            .toLocaleString(
                                "ru-RU",
                                {
                                    minimumFractionDigits: 1,
                                    maximumFractionDigits: 1
                                })
                        : "—";
            }

            if (countElement) {
                countElement.textContent =
                    String(result.ratingCount);
            }

            if (userRatingElement) {
                userRatingElement.textContent =
                    currentRating
                        ? `Ваша оценка: ${currentRating}`
                        : "Вы ещё не оценивали";
            }

            buttons.forEach(button => {
                const score =
                    Number(button.dataset.score);

                const isActive =
                    currentRating &&
                    score <= currentRating;

                button.classList.toggle(
                    "active",
                    Boolean(isActive));

                button.setAttribute(
                    "aria-pressed",
                    String(score === currentRating));
            });
        }
    });