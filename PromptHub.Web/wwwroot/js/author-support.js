(() => {
    const minimumAmount = 50;

    function initializeSupportDialog(dialog) {
        if (dialog.dataset.supportInitialized === "true") {
            return;
        }

        dialog.dataset.supportInitialized = "true";

        let selectedAmount = 100;

        const amountButtons =
            dialog.querySelectorAll(
                "[data-support-amount]");

        const customAmountInput =
            dialog.querySelector(
                "[data-support-custom-amount]");

        const amountError =
            dialog.querySelector(
                "[data-support-amount-error]");

        const cloudTipsLink =
            dialog.querySelector(
                "[data-support-cloudtips]");

        const yooMoneyForm =
            dialog.querySelector(
                "[data-support-yoomoney-form]");

        const yooMoneySum =
            dialog.querySelector(
                "[data-support-yoomoney-sum]");


        function clearError() {
            if (amountError) {
                amountError.hidden = true;
            }
        }


        function showError() {
            if (amountError) {
                amountError.hidden = false;
            }
        }


        function setAmount(amount) {
            if (!Number.isFinite(amount) ||
                amount < minimumAmount) {
                return false;
            }

            selectedAmount =
                Math.round(amount);

            clearError();

            if (yooMoneySum) {
                yooMoneySum.value =
                    String(selectedAmount);
            }

            if (cloudTipsLink) {
                const baseUrl =
                    cloudTipsLink.dataset.baseUrl;

                if (baseUrl) {
                    const url =
                        new URL(baseUrl);

                    url.searchParams.set(
                        "amount",
                        String(selectedAmount));

                    cloudTipsLink.href =
                        url.toString();
                }
            }

            return true;
        }


        function readAmount() {
            const customValue =
                customAmountInput?.value
                    .trim();

            if (customValue) {
                const value =
                    Number(customValue);

                if (!setAmount(value)) {
                    showError();

                    customAmountInput.focus();

                    return false;
                }
            }

            return setAmount(
                selectedAmount);
        }


        amountButtons.forEach(button => {
            button.addEventListener(
                "click",
                () => {
                    const value =
                        Number(
                            button.dataset
                                .supportAmount);

                    if (!setAmount(value)) {
                        return;
                    }

                    amountButtons.forEach(
                        item => {
                            item.classList.remove(
                                "is-selected");
                        });

                    button.classList.add(
                        "is-selected");

                    if (customAmountInput) {
                        customAmountInput.value =
                            "";
                    }
                });
        });


        customAmountInput?.addEventListener(
            "input",
            () => {
                amountButtons.forEach(
                    button => {
                        button.classList.remove(
                            "is-selected");
                    });

                clearError();

                const value =
                    Number(
                        customAmountInput.value);

                if (Number.isFinite(value) &&
                    value >= minimumAmount) {
                    setAmount(value);
                }
            });


        cloudTipsLink?.addEventListener(
            "click",
            event => {
                if (!readAmount()) {
                    event.preventDefault();
                }
            });


        yooMoneyForm?.addEventListener(
            "submit",
            event => {
                if (!readAmount()) {
                    event.preventDefault();
                }
            });


        dialog
            .querySelector(
                "[data-author-support-close]")
            ?.addEventListener(
                "click",
                () => {
                    dialog.close();
                });


        dialog.addEventListener(
            "click",
            event => {
                if (event.target === dialog) {
                    dialog.close();
                }
            });


        setAmount(
            selectedAmount);
    }


    document
        .querySelectorAll(
            "[data-author-support-dialog]")
        .forEach(dialog => {
            initializeSupportDialog(
                dialog);
        });


    document.addEventListener(
        "click",
        event => {
            const button =
                event.target.closest(
                    "[data-author-support-open]");

            if (!button) {
                return;
            }

            const dialog =
                document.querySelector(
                    "[data-author-support-dialog]");

            if (!(dialog instanceof
                HTMLDialogElement)) {
                return;
            }

            initializeSupportDialog(
                dialog);

            dialog.showModal();
        });
})();
