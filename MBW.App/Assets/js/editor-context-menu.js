// Word-like mini toolbar + context menu for the email editor

(function () {
    const api = window.__mbwEditorApi;
    if (!api) {
        return;
    }

    const FONT_FAMILIES = [
        'Segoe UI', 'Arial', 'Calibri', 'Times New Roman', 'Verdana', 'Georgia', 'Courier New'
    ];
    const FONT_SIZE_LADDER = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72];

    const ICON = {
        bold: '\uE8DD',
        italic: '\uE8DB',
        underline: '\uE8DC',
        bullets: '\uE8FD',
        clearFormat: '\uE8E6',
        fontIncrease: '\uE8FC',
        fontDecrease: '\uE8FB',
        link: '\uE71B',
        cut: '\uE8C6',
        copy: '\uE8C8',
        paste: '\uE77F'
    };

    const SVG = {
        highlight: 'M3,13.5 H5.5 L12.5,3.5 L10,1 L3,11 Z M10.2,1.8 L11.2,2.8 L10.5,3.5 L9.5,2.5 Z',
        numberedList: `
            <svg class="mbw-mini-svg" viewBox="0 0 16 16" aria-hidden="true">
                <text x="0" y="4.5" font-size="5" font-family="Segoe UI, sans-serif" font-weight="600">1.</text>
                <rect x="6" y="2.5" width="9" height="1" fill="currentColor"/>
                <text x="0" y="9" font-size="5" font-family="Segoe UI, sans-serif" font-weight="600">2.</text>
                <rect x="6" y="7" width="9" height="1" fill="currentColor"/>
                <text x="0" y="13.5" font-size="5" font-family="Segoe UI, sans-serif" font-weight="600">3.</text>
                <rect x="6" y="11.5" width="9" height="1" fill="currentColor"/>
            </svg>`
    };

    let savedRange = null;
    let root = null;
    let boldBtn;
    let italicBtn;
    let underlineBtn;
    let bulletBtn;
    let numberedBtn;
    let fontSelect;
    let sizeSelect;
    let cutItem;
    let copyItem;
    let linkItem;
    let linkItemInLink = false;
    let fontColorBar;
    let contextImageTarget = null;
    let miniToolbar;

    function isOpen() {
        return root?.classList.contains('is-open');
    }

    function saveSelection() {
        const selection = window.getSelection();
        if (selection && selection.rangeCount > 0) {
            savedRange = selection.getRangeAt(0).cloneRange();
        }
    }

    function restoreSelection() {
        if (!savedRange) {
            return;
        }

        api.focusEditor();
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(savedRange);
    }

    function hasTextSelection() {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0 || selection.isCollapsed) {
            return false;
        }

        return selection.toString().length > 0;
    }

    function selectionIsInLink() {
        const selection = window.getSelection();
        if (!selection?.anchorNode) {
            return false;
        }

        let node = selection.anchorNode;
        if (node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        return !!(node && node.closest && node.closest('a[href]') && api.editor.contains(node));
    }

    function runCommand(command, value) {
        restoreSelection();
        api.execEditorCommand(command, value);
        saveSelection();
        updateMenuState();
    }

    function stepFontSize(direction) {
        restoreSelection();
        const current = api.getCurrentFontSizePx() || 14;
        let index = FONT_SIZE_LADDER.findIndex((size) => size >= current);
        if (index < 0) {
            index = FONT_SIZE_LADDER.length - 1;
        } else if (FONT_SIZE_LADDER[index] !== current) {
            index = Math.max(0, index);
        }

        const nextIndex = direction > 0
            ? Math.min(FONT_SIZE_LADDER.length - 1, index + 1)
            : Math.max(0, index - 1);
        api.setFontSize(FONT_SIZE_LADDER[nextIndex]);
        saveSelection();
        updateMenuState();
    }

    function iconHtml(glyph) {
        return `<span class="mbw-icon" aria-hidden="true">${glyph}</span>`;
    }

    function svgIconHtml(path) {
        return `<svg class="mbw-mini-svg" viewBox="0 0 16 16" aria-hidden="true"><path d="${path}"/></svg>`;
    }

    function createMiniButton(content, title, onClick) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'mbw-mini-btn';
        button.title = title;
        button.innerHTML = content;
        button.addEventListener('mousedown', (event) => event.preventDefault());
        button.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            onClick();
        });
        return button;
    }

    function createSeparator() {
        const separator = document.createElement('span');
        separator.className = 'mbw-mini-sep';
        separator.setAttribute('aria-hidden', 'true');
        return separator;
    }

    function createMiniRow() {
        const row = document.createElement('div');
        row.className = 'mbw-context-mini-row';
        return row;
    }

    function createFontSelect() {
        const select = document.createElement('select');
        select.className = 'mbw-mini-select mbw-mini-font';
        select.title = 'Font';
        FONT_FAMILIES.forEach((family) => {
            const option = document.createElement('option');
            option.value = family;
            option.textContent = family;
            select.appendChild(option);
        });
        select.addEventListener('mousedown', (event) => event.stopPropagation());
        select.addEventListener('change', () => {
            restoreSelection();
            api.setFontFamily(select.value);
            saveSelection();
            updateMenuState();
        });
        return select;
    }

    function createSizeSelect() {
        const select = document.createElement('select');
        select.className = 'mbw-mini-select mbw-mini-size';
        select.title = 'Font size';
        FONT_SIZE_LADDER.forEach((size) => {
            const option = document.createElement('option');
            option.value = String(size);
            option.textContent = String(size);
            select.appendChild(option);
        });
        select.addEventListener('mousedown', (event) => event.stopPropagation());
        select.addEventListener('change', () => {
            restoreSelection();
            api.setFontSize(select.value);
            saveSelection();
            updateMenuState();
        });
        return select;
    }

    function createFontColorButton() {
        const wrap = document.createElement('button');
        wrap.type = 'button';
        wrap.className = 'mbw-mini-btn';
        wrap.title = 'Font color';

        const icon = document.createElement('span');
        icon.className = 'mbw-font-color-icon';
        icon.innerHTML = '<span class="mbw-font-color-a">A</span>';
        fontColorBar = document.createElement('span');
        fontColorBar.className = 'mbw-font-color-bar';
        fontColorBar.style.backgroundColor = '#000000';
        icon.appendChild(fontColorBar);
        wrap.appendChild(icon);

        const input = document.createElement('input');
        input.type = 'color';
        input.value = '#000000';
        input.className = 'mbw-color-input';
        wrap.appendChild(input);

        wrap.addEventListener('mousedown', (event) => event.preventDefault());
        wrap.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            restoreSelection();
            input.click();
        });
        input.addEventListener('input', () => {
            restoreSelection();
            fontColorBar.style.backgroundColor = input.value;
            api.setForeColor(input.value);
            saveSelection();
            updateMenuState();
        });

        return wrap;
    }

    function createHighlightButton() {
        const wrap = document.createElement('button');
        wrap.type = 'button';
        wrap.className = 'mbw-mini-btn';
        wrap.title = 'Text highlight color';
        wrap.innerHTML = svgIconHtml(SVG.highlight);

        const input = document.createElement('input');
        input.type = 'color';
        input.value = '#ffff00';
        input.className = 'mbw-color-input';
        wrap.appendChild(input);

        wrap.addEventListener('mousedown', (event) => event.preventDefault());
        wrap.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            restoreSelection();
            input.click();
        });
        input.addEventListener('input', () => {
            restoreSelection();
            api.setBackColor(input.value);
            saveSelection();
            updateMenuState();
        });

        return wrap;
    }

    function createMenuItem(label, shortcut, onClick, iconGlyph) {
        const item = document.createElement('div');
        item.className = 'mbw-context-menu-item';
        const labelHtml = iconGlyph
            ? `<span class="mbw-context-menu-item-with-icon">${iconHtml(iconGlyph)}<span>${label}</span></span>`
            : `<span>${label}</span>`;
        item.innerHTML = `${labelHtml}${shortcut ? `<span class="mbw-context-shortcut">${shortcut}</span>` : ''}`;
        item.addEventListener('mousedown', (event) => event.preventDefault());
        item.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            if (item.classList.contains('disabled')) {
                return;
            }

            onClick();
        });
        return item;
    }

    function updateLinkMenuItem() {
        if (!linkItem) {
            return;
        }

        linkItemInLink = selectionIsInLink();
        const label = linkItemInLink ? 'Remove Link' : 'Link';
        const shortcut = linkItemInLink ? '' : 'Ctrl+K';
        linkItem.innerHTML = `<span class="mbw-context-menu-item-with-icon">${iconHtml(ICON.link)}<span>${label}</span></span>${shortcut ? `<span class="mbw-context-shortcut">${shortcut}</span>` : ''}`;
        linkItem.classList.remove('disabled');
    }

    function handleLinkMenuAction() {
        if (linkItemInLink) {
            restoreSelection();
            api.removeLink();
        } else {
            api.postMessage({ type: 'requestLink' });
        }

        hideContextMenu();
    }

    function setImageContextMode(isImage) {
        if (miniToolbar) {
            miniToolbar.style.display = isImage ? 'none' : '';
        }
    }

    function createMenuSeparator() {
        const separator = document.createElement('div');
        separator.className = 'mbw-context-separator';
        return separator;
    }

    function buildUi() {
        root = document.createElement('div');
        root.id = 'mbw-context-root';
        root.className = 'mbw-context-root';

        miniToolbar = document.createElement('div');
        miniToolbar.className = 'mbw-context-mini';

        const mini = miniToolbar;

        const row1 = createMiniRow();
        const row2 = createMiniRow();

        fontSelect = createFontSelect();
        sizeSelect = createSizeSelect();
        row1.appendChild(fontSelect);
        row1.appendChild(sizeSelect);
        row1.appendChild(createMiniButton(iconHtml(ICON.fontIncrease), 'Increase font size', () => stepFontSize(1)));
        row1.appendChild(createMiniButton(iconHtml(ICON.fontDecrease), 'Decrease font size', () => stepFontSize(-1)));
        row1.appendChild(createMiniButton(iconHtml(ICON.clearFormat), 'Clear formatting', () => {
            restoreSelection();
            api.clearFormatting();
            saveSelection();
            updateMenuState();
        }));

        boldBtn = createMiniButton(iconHtml(ICON.bold), 'Bold (Ctrl+B)', () => runCommand('bold'));
        italicBtn = createMiniButton(iconHtml(ICON.italic), 'Italic (Ctrl+I)', () => runCommand('italic'));
        underlineBtn = createMiniButton(iconHtml(ICON.underline), 'Underline (Ctrl+U)', () => runCommand('underline'));
        row2.appendChild(boldBtn);
        row2.appendChild(italicBtn);
        row2.appendChild(underlineBtn);
        row2.appendChild(createFontColorButton());
        row2.appendChild(createHighlightButton());
        row2.appendChild(createSeparator());
        bulletBtn = createMiniButton(iconHtml(ICON.bullets), 'Bullets', () => runCommand('insertUnorderedList'));
        numberedBtn = createMiniButton(SVG.numberedList, 'Numbering', () => runCommand('insertOrderedList'));
        row2.appendChild(bulletBtn);
        row2.appendChild(numberedBtn);

        mini.appendChild(row1);
        mini.appendChild(row2);

        const menu = document.createElement('div');
        menu.className = 'mbw-context-menu';

        cutItem = createMenuItem('Cut', 'Ctrl+X', () => {
            restoreSelection();
            document.execCommand('cut');
            api.postContentChanged();
            hideContextMenu();
        }, ICON.cut);
        copyItem = createMenuItem('Copy', 'Ctrl+C', () => {
            restoreSelection();
            document.execCommand('copy');
            hideContextMenu();
        }, ICON.copy);
        menu.appendChild(cutItem);
        menu.appendChild(copyItem);
        menu.appendChild(createMenuItem('Paste', 'Ctrl+V', () => pasteFormatted(), ICON.paste));
        menu.appendChild(createMenuItem('Paste Text Only', 'Ctrl+Shift+V', () => pastePlain()));
        menu.appendChild(createMenuSeparator());
        linkItem = createMenuItem('Link', 'Ctrl+K', () => handleLinkMenuAction(), ICON.link);
        menu.appendChild(linkItem);
        menu.appendChild(createMenuSeparator());
        menu.appendChild(createMenuItem('Clear Formatting', '', () => {
            restoreSelection();
            api.clearFormatting();
            hideContextMenu();
        }, ICON.clearFormat));

        root.appendChild(mini);
        root.appendChild(menu);
        document.body.appendChild(root);

        root.addEventListener('contextmenu', (event) => event.preventDefault());
    }

    function resolveContextImage(target) {
        if (!target || !(target instanceof Element)) {
            return null;
        }

        const img = target instanceof HTMLImageElement ? target : target.closest('img');
        if (!img || !api.editor.contains(img)) {
            return null;
        }

        return img;
    }

    function pasteFormatted() {
        restoreSelection();
        api.focusEditor();
        const pasted = document.execCommand('paste');
        if (!pasted) {
            api.postMessage({ type: 'requestPaste' });
        } else {
            api.normalizeAllLinks(api.editor);
            api.postContentChanged();
        }
        hideContextMenu();
    }

    function pastePlain() {
        restoreSelection();
        api.postMessage({ type: 'requestPastePlain' });
        hideContextMenu();
    }

    function updateMenuState() {
        if (!root) {
            return;
        }

        const hasSelection = hasTextSelection();
        cutItem.classList.toggle('disabled', !hasSelection);
        copyItem.classList.toggle('disabled', !hasSelection);
        updateLinkMenuItem();

        boldBtn.classList.toggle('active', document.queryCommandState('bold'));
        italicBtn.classList.toggle('active', document.queryCommandState('italic'));
        underlineBtn.classList.toggle('active', document.queryCommandState('underline'));
        bulletBtn.classList.toggle('active', document.queryCommandState('insertUnorderedList'));
        numberedBtn.classList.toggle('active', document.queryCommandState('insertOrderedList'));

        const family = api.normalizeFontFamily(document.queryCommandValue('fontName')) || 'Segoe UI';
        if ([...fontSelect.options].some((option) => option.value === family)) {
            fontSelect.value = family;
        }

        const size = api.getCurrentFontSizePx();
        if (size && [...sizeSelect.options].some((option) => option.value === String(size))) {
            sizeSelect.value = String(size);
        }
    }

    function hideContextMenu() {
        if (!root) {
            return;
        }

        root.classList.remove('is-open');
        contextImageTarget = null;
        setImageContextMode(false);
    }

    function showContextMenu(event) {
        if (!root) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        saveSelection();

        contextImageTarget = resolveContextImage(event.target);
        if (contextImageTarget) {
            api.selectImageObject(contextImageTarget);
            setImageContextMode(true);
        } else {
            setImageContextMode(false);
        }

        updateMenuState();
        root.classList.add('is-open');

        const x = event.clientX;
        const y = event.clientY;
        root.style.left = `${x}px`;
        root.style.top = `${y}px`;

        requestAnimationFrame(() => {
            const rect = root.getBoundingClientRect();
            let left = x;
            let top = y;

            if (left + rect.width > window.innerWidth - 8) {
                left = Math.max(8, window.innerWidth - rect.width - 8);
            }
            if (top + rect.height > window.innerHeight - 8) {
                top = Math.max(8, window.innerHeight - rect.height - 8);
            }

            root.style.left = `${left}px`;
            root.style.top = `${top}px`;
        });
    }

    function handleOutsideInteraction(event) {
        if (!isOpen()) {
            return;
        }

        if (event.type === 'mousedown' && event.button === 2) {
            return;
        }

        if (root.contains(event.target)) {
            return;
        }

        hideContextMenu();
    }

    function init() {
        buildUi();

        api.editor.addEventListener('contextmenu', showContextMenu);

        const canvasScroll = document.getElementById('canvas-scroll');
        canvasScroll?.addEventListener('contextmenu', (event) => {
            if (api.editor.contains(event.target)) {
                return;
            }

            hideContextMenu();
        });

        const dismissTargets = [document, api.editor, canvasScroll, document.getElementById('editor-page')];
        dismissTargets.forEach((target) => {
            if (!target) {
                return;
            }

            target.addEventListener('pointerdown', handleOutsideInteraction, true);
            target.addEventListener('mousedown', handleOutsideInteraction, true);
        });

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                hideContextMenu();
            }
        });

        document.getElementById('canvas-scroll')?.addEventListener('scroll', hideContextMenu, { passive: true });
        window.addEventListener('resize', hideContextMenu);
        window.addEventListener('blur', hideContextMenu);
    }

    init();
})();
