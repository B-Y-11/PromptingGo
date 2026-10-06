document.addEventListener(
    "DOMContentLoaded",
    () => {
        const editorRoot =
            document.getElementById(
                "promptFormEditor");

        if (!editorRoot) {
            return;
        }

        const textType =
            Number(editorRoot.dataset.textType);

        const textAreaType =
            Number(editorRoot.dataset.textareaType);

        const numberType =
            Number(editorRoot.dataset.numberType);

        const selectType =
            Number(editorRoot.dataset.selectType);

        const checkboxType =
            Number(editorRoot.dataset.checkboxType);

        const constructorToggle =
            document.getElementById(
                "enableConstructor");

        const constructorSection =
            document.getElementById(
                "constructorSection");

        const templateTextarea =
            document.getElementById(
                "TemplateText");

        const fieldsJsonInput =
            document.getElementById(
                "constructorFieldsJson");

        const fieldsList =
            document.getElementById(
                "fieldsList");

        const fieldsEmptyState =
            document.getElementById(
                "fieldsEmptyState");

        const fieldEditor =
            document.getElementById(
                "fieldEditor");

        const fieldEditorTitle =
            document.getElementById(
                "fieldEditorTitle");

        const fieldEditorError =
            document.getElementById(
                "fieldEditorError");

        const openFieldEditorButton =
            document.getElementById(
                "openFieldEditorButton");

        const saveFieldButton =
            document.getElementById(
                "saveFieldButton");

        const cancelFieldButton =
            document.getElementById(
                "cancelFieldButton");

        const fieldLabel =
            document.getElementById(
                "fieldLabel");

        const fieldType =
            document.getElementById(
                "fieldType");

        const fieldRequired =
            document.getElementById(
                "fieldRequired");

        const fieldPlaceholder =
            document.getElementById(
                "fieldPlaceholder");

        const fieldHelpText =
            document.getElementById(
                "fieldHelpText");

        const fieldOptions =
            document.getElementById(
                "fieldOptions");

        const fieldOptionsGroup =
            document.getElementById(
                "fieldOptionsGroup");

        let editingFieldIndex = null;
        let fields = [];

        function createFieldId() {
            if (window.crypto?.randomUUID) {
                return window.crypto.randomUUID();
            }

            return `field-${Date.now()}-${Math.random()}`;
        }

        function normalizeField(rawField) {
            return {
                id:
                    rawField.id ??
                    rawField.Id ??
                    createFieldId(),

                label:
                    rawField.label ??
                    rawField.Label ??
                    "",

                variableType:
                    Number(
                        rawField.variableType ??
                        rawField.VariableType ??
                        textType),

                isRequired:
                    (rawField.isRequired ??
                        rawField.IsRequired) !== false,

                placeholder:
                    rawField.placeholder ??
                    rawField.Placeholder ??
                    "",

                helpText:
                    rawField.helpText ??
                    rawField.HelpText ??
                    "",

                options:
                    rawField.options ??
                    rawField.Options ??
                    []
            };
        }

        function loadFields() {
            try {
                const parsed = JSON.parse(
                    fieldsJsonInput?.value ||
                    "[]");

                fields = Array.isArray(parsed)
                    ? parsed.map(normalizeField)
                    : [];
            }
            catch {
                fields = [];
            }
        }

        function saveFieldsToHiddenInput() {
            if (!fieldsJsonInput) {
                return;
            }

            fieldsJsonInput.value =
                JSON.stringify(fields);
        }

        function escapeHtml(value) {
            const characterMap = {
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                "\"": "&quot;",
                "'": "&#039;"
            };

            return String(value).replace(
                /[&<>"']/g,
                character =>
                    characterMap[character]);
        }

        function getFieldTypeName(variableType) {
            switch (Number(variableType)) {
                case textAreaType:
                    return "Большой текст";

                case numberType:
                    return "Число";

                case selectType:
                    return "Выбор из списка";

                case checkboxType:
                    return "Да или нет";

                default:
                    return "Короткий текст";
            }
        }

        function getMarker(field) {
            return `[${field.label}]`;
        }

        function renderFields() {
            saveFieldsToHiddenInput();

            fieldsEmptyState?.classList.toggle(
                "d-none",
                fields.length > 0);

            if (!fieldsList) {
                return;
            }

            fieldsList.innerHTML =
                fields.map(
                    (field, index) => `
                        <div class="visual-field-note">
                            <div>
                                <div class="visual-field-title">
                                    ${escapeHtml(field.label)}
                                </div>

                                <div class="visual-field-meta">
                                    ${escapeHtml(
                        getFieldTypeName(
                            field.variableType))}
                                    ·
                                    ${field.isRequired
                            ? "Обязательное"
                            : "Необязательное"}
                                </div>

                                <code class="visual-field-marker">
                                    ${escapeHtml(
                                getMarker(field))}
                                </code>
                            </div>

                            <div class="visual-field-actions">
                                <button type="button"
                                        class="btn btn-outline-primary btn-sm"
                                        data-action="insert"
                                        data-index="${index}">
                                    Вставить
                                </button>

                                <button type="button"
                                        class="btn btn-outline-secondary btn-sm"
                                        data-action="edit"
                                        data-index="${index}">
                                    Изменить
                                </button>

                                <button type="button"
                                        class="btn btn-outline-danger btn-sm"
                                        data-action="delete"
                                        data-index="${index}">
                                    Удалить
                                </button>
                            </div>
                        </div>
                    `)
                    .join("");
        }

        function insertTextAtCursor(text) {
            if (!templateTextarea) {
                return;
            }

            const start =
                templateTextarea.selectionStart ??
                templateTextarea.value.length;

            const end =
                templateTextarea.selectionEnd ??
                start;

            const before =
                templateTextarea.value.slice(
                    0,
                    start);

            const after =
                templateTextarea.value.slice(
                    end);

            templateTextarea.value =
                before + text + after;

            const cursorPosition =
                start + text.length;

            templateTextarea.focus();

            templateTextarea.setSelectionRange(
                cursorPosition,
                cursorPosition);
        }

        function showEditorError(message) {
            if (!fieldEditorError) {
                return;
            }

            fieldEditorError.textContent =
                message;

            fieldEditorError.classList.remove(
                "d-none");
        }

        function clearEditorError() {
            if (!fieldEditorError) {
                return;
            }

            fieldEditorError.textContent = "";

            fieldEditorError.classList.add(
                "d-none");
        }

        function updateOptionsVisibility() {
            const isSelect =
                Number(fieldType?.value) ===
                selectType;

            fieldOptionsGroup?.classList.toggle(
                "d-none",
                !isSelect);
        }

        function openEditor(index = null) {
            clearEditorError();

            editingFieldIndex = index;

            if (index === null) {
                fieldEditorTitle.textContent =
                    "Новое поле";

                fieldLabel.value = "";
                fieldType.value =
                    String(textType);

                fieldRequired.checked = true;
                fieldPlaceholder.value = "";
                fieldHelpText.value = "";
                fieldOptions.value = "";

                saveFieldButton.textContent =
                    "Сохранить и вставить";
            }
            else {
                const field = fields[index];

                fieldEditorTitle.textContent =
                    "Изменить поле";

                fieldLabel.value =
                    field.label;

                fieldType.value =
                    String(field.variableType);

                fieldRequired.checked =
                    field.isRequired;

                fieldPlaceholder.value =
                    field.placeholder;

                fieldHelpText.value =
                    field.helpText;

                fieldOptions.value =
                    field.options.join("\n");

                saveFieldButton.textContent =
                    "Сохранить изменения";
            }

            updateOptionsVisibility();

            fieldEditor.classList.remove(
                "d-none");

            fieldLabel.focus();
        }

        function closeEditor() {
            clearEditorError();

            editingFieldIndex = null;

            fieldEditor?.classList.add(
                "d-none");
        }

        function readOptions() {
            return fieldOptions.value
                .split(/\r?\n/)
                .map(option =>
                    option.trim())
                .filter(option =>
                    option.length > 0)
                .filter(
                    (option, index, allOptions) =>
                        allOptions.findIndex(
                            candidate =>
                                candidate.toLocaleLowerCase() ===
                                option.toLocaleLowerCase()) ===
                        index);
        }

        function saveCurrentField() {
            clearEditorError();

            const label =
                fieldLabel.value.trim();

            if (!label) {
                showEditorError(
                    "Введите название поля.");

                return;
            }

            if (label.includes("[") ||
                label.includes("]")) {
                showEditorError(
                    "Название не должно содержать квадратные скобки.");

                return;
            }

            const duplicateIndex =
                fields.findIndex(
                    (field, index) =>
                        index !== editingFieldIndex &&
                        field.label.localeCompare(
                            label,
                            undefined,
                            {
                                sensitivity: "accent"
                            }) === 0);

            if (duplicateIndex >= 0) {
                showEditorError(
                    "Поле с таким названием уже существует.");

                return;
            }

            const variableType =
                Number(fieldType.value);

            const options =
                variableType === selectType
                    ? readOptions()
                    : [];

            if (variableType === selectType &&
                options.length < 2) {
                showEditorError(
                    "Для списка добавьте минимум два варианта.");

                return;
            }

            const newField = {
                id:
                    editingFieldIndex === null
                        ? createFieldId()
                        : fields[
                            editingFieldIndex].id,

                label,

                variableType,

                isRequired:
                    fieldRequired.checked,

                placeholder:
                    fieldPlaceholder.value.trim(),

                helpText:
                    fieldHelpText.value.trim(),

                options
            };

            if (editingFieldIndex === null) {
                fields.push(newField);

                insertTextAtCursor(
                    getMarker(newField));
            }
            else {
                const previousField =
                    fields[editingFieldIndex];

                const previousMarker =
                    getMarker(previousField);

                const newMarker =
                    getMarker(newField);

                templateTextarea.value =
                    templateTextarea.value
                        .split(previousMarker)
                        .join(newMarker);

                fields[editingFieldIndex] =
                    newField;
            }

            renderFields();
            closeEditor();
        }

        function deleteField(index) {
            const field = fields[index];

            if (!field) {
                return;
            }

            const confirmed =
                window.confirm(
                    `Удалить поле «${field.label}»?`);

            if (!confirmed) {
                return;
            }

            const marker =
                getMarker(field);

            templateTextarea.value =
                templateTextarea.value
                    .split(marker)
                    .join("");

            fields.splice(index, 1);

            renderFields();
            closeEditor();
        }

        function updateConstructorVisibility() {
            const enabled =
                constructorToggle?.checked === true;

            constructorSection?.classList.toggle(
                "d-none",
                !enabled);

            if (templateTextarea) {
                templateTextarea.disabled =
                    !enabled;
            }
        }

        openFieldEditorButton?.addEventListener(
            "click",
            () => openEditor());

        cancelFieldButton?.addEventListener(
            "click",
            closeEditor);

        saveFieldButton?.addEventListener(
            "click",
            saveCurrentField);

        fieldType?.addEventListener(
            "change",
            updateOptionsVisibility);

        fieldsList?.addEventListener(
            "click",
            event => {
                const button =
                    event.target.closest(
                        "button[data-action]");

                if (!button) {
                    return;
                }

                const index =
                    Number(button.dataset.index);

                const action =
                    button.dataset.action;

                if (action === "insert") {
                    const field =
                        fields[index];

                    if (field) {
                        insertTextAtCursor(
                            getMarker(field));
                    }

                    return;
                }

                if (action === "edit") {
                    openEditor(index);
                    return;
                }

                if (action === "delete") {
                    deleteField(index);
                }
            });

        constructorToggle?.addEventListener(
            "change",
            updateConstructorVisibility);

        loadFields();
        renderFields();
        updateConstructorVisibility();
        updateOptionsVisibility();
    });