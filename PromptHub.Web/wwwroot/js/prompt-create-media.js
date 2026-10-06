(() => {
    "use strict";

    const input =
        document.getElementById(
            "create-prompt-images");

    const preview =
        document.getElementById(
            "create-prompt-images-preview");

    const error =
        document.getElementById(
            "create-prompt-images-error");

    if (!(input instanceof HTMLInputElement) ||
        !(preview instanceof HTMLElement) ||
        !(error instanceof HTMLElement)) {
        return;
    }

    const maxFiles =
        Number.parseInt(
            input.dataset.maxFiles ?? "6",
            10);

    const maxFileSize =
        Number.parseInt(
            input.dataset.maxFileSize ?? "5242880",
            10);

    const allowedTypes =
        new Set([
            "image/jpeg",
            "image/png",
            "image/webp"
        ]);

    // -------------------------------------------------------------------------
    // EN: Files accumulated across multiple file-picker operations.
    // RU: Файлы, накопленные после нескольких открытий окна выбора.
    // -------------------------------------------------------------------------

    let selectedFiles =
        [];

    let objectUrls =
        [];

    // -------------------------------------------------------------------------
    // EN: File identity used to prevent accidental duplicates.
    // RU: Идентификатор файла для предотвращения случайных дубликатов.
    // -------------------------------------------------------------------------

    const getFileKey =
        file =>
            [
                file.name,
                file.size,
                file.type,
                file.lastModified
            ].join("|");

    // -------------------------------------------------------------------------
    // EN: Release browser preview resources.
    // RU: Освобождение ресурсов предпросмотра браузера.
    // -------------------------------------------------------------------------

    const clearObjectUrls =
        () => {
            objectUrls.forEach(url =>
                URL.revokeObjectURL(
                    url));

            objectUrls =
                [];
        };

    const showError =
        message => {
            error.textContent =
                message;

            error.classList.remove(
                "d-none");
        };

    const clearError =
        () => {
            error.textContent =
                "";

            error.classList.add(
                "d-none");
        };

    // -------------------------------------------------------------------------
    // EN: Synchronize the actual input FileList with the accumulated files.
    // RU: Синхронизация настоящего FileList с накопленными файлами.
    // -------------------------------------------------------------------------

    const synchronizeInputFiles =
        () => {
            const transfer =
                new DataTransfer();

            selectedFiles.forEach(file =>
                transfer.items.add(
                    file));

            input.files =
                transfer.files;
        };

    // -------------------------------------------------------------------------
    // EN: Draw all currently selected images.
    // RU: Отображение всех выбранных изображений.
    // -------------------------------------------------------------------------

    const renderPreview =
        () => {
            clearObjectUrls();

            preview.replaceChildren();

            selectedFiles.forEach(
                (file, index) => {
                    const item =
                        document.createElement(
                            "article");

                    item.className =
                        "prompt-create-media-preview-item";

                    const image =
                        document.createElement(
                            "img");

                    const objectUrl =
                        URL.createObjectURL(
                            file);

                    objectUrls.push(
                        objectUrl);

                    image.src =
                        objectUrl;

                    image.alt =
                        `Предпросмотр файла ${file.name}`;

                    const footer =
                        document.createElement(
                            "div");

                    footer.className =
                        "prompt-create-media-preview-footer";

                    const information =
                        document.createElement(
                            "div");

                    information.className =
                        "prompt-create-media-preview-information";

                    const name =
                        document.createElement(
                            "span");

                    name.className =
                        "prompt-create-media-preview-name";

                    name.title =
                        file.name;

                    name.textContent =
                        file.name;

                    information.append(
                        name);

                    if (index === 0) {
                        const cover =
                            document.createElement(
                                "span");

                        cover.className =
                            "notebook-tag notebook-tag-yellow";

                        cover.textContent =
                            "Обложка";

                        information.append(
                            cover);
                    }

                    const removeButton =
                        document.createElement(
                            "button");

                    removeButton.type =
                        "button";

                    removeButton.className =
                        "prompt-create-media-preview-remove";

                    removeButton.setAttribute(
                        "aria-label",
                        `Удалить изображение ${file.name}`);

                    removeButton.title =
                        "Удалить изображение";

                    removeButton.textContent =
                        "×";

                    removeButton.addEventListener(
                        "click",
                        () => {
                            selectedFiles.splice(
                                index,
                                1);

                            synchronizeInputFiles();
                            clearError();
                            renderPreview();
                        });

                    footer.append(
                        information,
                        removeButton);

                    item.append(
                        image,
                        footer);

                    preview.append(
                        item);
                });
        };

    // -------------------------------------------------------------------------
    // EN: Validate one newly selected file.
    // RU: Проверка одного нового выбранного файла.
    // -------------------------------------------------------------------------

    const validateFile =
        file => {
            if (!allowedTypes.has(
                file.type)) {
                return (
                    `Файл «${file.name}» имеет ` +
                    "неподдерживаемый формат."
                );
            }

            if (file.size >
                maxFileSize) {
                const maxMegabytes =
                    maxFileSize /
                    1024 /
                    1024;

                return (
                    `Файл «${file.name}» превышает ` +
                    `${maxMegabytes.toFixed(1)} МБ.`
                );
            }

            return null;
        };

    input.addEventListener(
        "change",
        () => {
            clearError();

            // -----------------------------------------------------------------
            // EN: At this point input.files contains only files selected during
            //     the latest file-picker operation.
            // RU: Сейчас input.files содержит только файлы, выбранные при
            //     последнем открытии окна выбора.
            // -----------------------------------------------------------------

            const newlySelectedFiles =
                Array.from(
                    input.files ?? []);

            if (newlySelectedFiles.length === 0) {
                synchronizeInputFiles();

                return;
            }

            for (const file of
                newlySelectedFiles) {
                const validationMessage =
                    validateFile(
                        file);

                if (validationMessage) {
                    synchronizeInputFiles();
                    showError(
                        validationMessage);

                    return;
                }
            }

            const existingKeys =
                new Set(
                    selectedFiles.map(
                        getFileKey));

            const uniqueNewFiles =
                newlySelectedFiles.filter(
                    file =>
                        !existingKeys.has(
                            getFileKey(
                                file)));

            const totalFileCount =
                selectedFiles.length +
                uniqueNewFiles.length;

            if (totalFileCount >
                maxFiles) {
                synchronizeInputFiles();

                showError(
                    `Можно выбрать не более ${maxFiles} изображений. ` +
                    `Сейчас уже выбрано: ${selectedFiles.length}.`);

                return;
            }

            selectedFiles.push(
                ...uniqueNewFiles);

            synchronizeInputFiles();
            renderPreview();
        });

    // -------------------------------------------------------------------------
    // EN: Keep the submitted FileList synchronized before form submission.
    // RU: Финальная синхронизация FileList перед отправкой формы.
    // -------------------------------------------------------------------------

    input.form?.addEventListener(
        "submit",
        () => {
            synchronizeInputFiles();
        });

    window.addEventListener(
        "beforeunload",
        clearObjectUrls);
})();