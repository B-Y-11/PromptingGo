"use strict";

document.addEventListener("DOMContentLoaded", () => {
    const moderationComment =
        document.getElementById("moderationComment");

    if (!moderationComment) {
        return;
    }

    const duplicateButtons =
        document.querySelectorAll(
            "[data-duplicate-rejection-title]");

    duplicateButtons.forEach(button => {
        button.addEventListener("click", () => {
            const duplicateTitle =
                button.dataset
                    .duplicateRejectionTitle
                    ?.trim();

            if (!duplicateTitle) {
                return;
            }

            moderationComment.value =
                `Промпт существенно совпадает с уже опубликованным материалом «${duplicateTitle}». ` +
                "Пожалуйста, переработайте содержание и добавьте самостоятельный подход.";

            moderationComment.focus();

            moderationComment.scrollIntoView({
                behavior: "smooth",
                block: "center"
            });
        });
    });
});
