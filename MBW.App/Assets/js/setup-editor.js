// MBW Email Editor - WebView2 contenteditable engine

(function () {
    const editor = document.getElementById('editor');
    if (!editor) {
        console.error('Editor element not found');
        return;
    }

    editor.contentEditable = 'true';
    editor.spellcheck = false;
    editor.setAttribute('role', 'textbox');
    editor.setAttribute('aria-multiline', 'true');

    const variablePattern = /\{[a-zA-Z_][a-zA-Z0-9_]*\}/g;
    let pendingFontSize = null;

    function postMessage(payload) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(payload);
        }
    }

    function postContentChanged() {
        postMessage({ type: 'editorContent', content: editor.innerHTML });
    }

    function focusEditor() {
        editor.focus();
    }

    function getRange() {
        const selection = window.getSelection();
        if (selection && selection.rangeCount > 0) {
            return selection.getRangeAt(0);
        }
        const range = document.createRange();
        range.selectNodeContents(editor);
        range.collapse(false);
        return range;
    }

    function restoreSelection(range) {
        const selection = window.getSelection();
        if (!selection || !range) return;
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function wrapSelection(tagName, styles) {
        focusEditor();
        const range = getRange();
        if (range.collapsed) return;

        const wrapper = document.createElement(tagName);
        Object.assign(wrapper.style, styles || {});
        try {
            range.surroundContents(wrapper);
        } catch {
            const fragment = range.extractContents();
            wrapper.appendChild(fragment);
            range.insertNode(wrapper);
        }
        postContentChanged();
    }

    function applyBlockStyle(property, value) {
        focusEditor();
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) return;

        let node = selection.anchorNode;
        if (node && node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        while (node && node !== editor && !/^(P|DIV|LI|H1|H2|H3|H4|H5|H6)$/i.test(node.tagName)) {
            node = node.parentElement;
        }

        const block = node && node !== editor ? node : editor;
        if (block === editor) {
            document.execCommand('formatBlock', false, 'p');
            return applyBlockStyle(property, value);
        }

        block.style[property] = value;
        postContentChanged();
    }

    window.execEditorCommand = function (command, value) {
        focusEditor();
        document.execCommand(command, false, value ?? null);
        postContentChanged();
        notifySelectionFormatAfterCommand();
    };

    function notifySelectionFormatAfterCommand() {
        // queryCommandState is unreliable in the same turn as execCommand (notably for shortcuts).
        postSelectionFormat();
        setTimeout(postSelectionFormat, 0);
        setTimeout(postSelectionFormat, 30);
        setTimeout(postSelectionFormat, 100);
        requestAnimationFrame(postSelectionFormat);
    }

    window.setFontFamily = function (family) {
        window.execEditorCommand('fontName', family);
    };

    function expandRangeToWord(range) {
        const node = range.startContainer;
        if (node.nodeType !== Node.TEXT_NODE) {
            return null;
        }

        const text = node.textContent || '';
        const offset = range.startOffset;
        let start = offset;
        let end = offset;

        while (start > 0 && !/\s/.test(text.charAt(start - 1))) {
            start--;
        }

        while (end < text.length && !/\s/.test(text.charAt(end))) {
            end++;
        }

        if (start === end) {
            return null;
        }

        const wordRange = document.createRange();
        wordRange.setStart(node, start);
        wordRange.setEnd(node, end);
        return wordRange;
    }

    function stripLegacyFontSizeElements(root) {
        root.querySelectorAll('font[size]').forEach((fontEl) => {
            const span = document.createElement('span');
            span.style.fontSize = fontEl.style.fontSize || '';
            while (fontEl.firstChild) {
                span.appendChild(fontEl.firstChild);
            }
            fontEl.replaceWith(span);
        });
    }

    function applyFontSizeToRange(range, size) {
        const span = document.createElement('span');
        span.style.fontSize = `${size}px`;
        span.style.lineHeight = 'inherit';
        span.style.verticalAlign = 'baseline';
        span.style.display = 'inline';

        try {
            range.surroundContents(span);
        } catch {
            const fragment = range.extractContents();
            span.appendChild(fragment);
            range.insertNode(span);
        }

        const selection = window.getSelection();
        const newRange = document.createRange();
        newRange.selectNodeContents(span);
        selection.removeAllRanges();
        selection.addRange(newRange);
    }

    window.setFontSize = function (px) {
        focusEditor();
        const size = parseInt(px, 10);
        if (Number.isNaN(size) || size < 1 || size > 400) {
            return;
        }

        document.execCommand('styleWithCSS', false, true);

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return;
        }

        const range = selection.getRangeAt(0);
        pendingFontSize = null;

        if (range.collapsed) {
            const wordRange = expandRangeToWord(range);
            if (wordRange) {
                applyFontSizeToRange(wordRange, size);
            } else {
                pendingFontSize = size;
            }
        } else {
            applyFontSizeToRange(range, size);
        }

        stripLegacyFontSizeElements(editor);
        postContentChanged();
        notifySelectionFormatAfterCommand();
    };

    window.setForeColor = function (color) {
        window.execEditorCommand('foreColor', color);
    };

    window.setBackColor = function (color) {
        window.execEditorCommand('backColor', color);
    };

    window.setLineHeight = function (value) {
        applyBlockStyle('lineHeight', value);
    };

    function normalizeLink(anchor) {
        const href = anchor.getAttribute('href');
        if (!href) {
            return;
        }

        anchor.setAttribute('title', `${href}\nCtrl+Click to open link`);
        anchor.removeAttribute('target');
    }

    function normalizeAllLinks(root) {
        root.querySelectorAll('a[href]').forEach(normalizeLink);
    }

    function findLinkFromEventTarget(target) {
        if (!target || !(target instanceof Element)) {
            return null;
        }

        const anchor = target.closest('a[href]');
        if (!anchor || !editor.contains(anchor)) {
            return null;
        }

        return anchor;
    }

    function getLinkAtSelection() {
        const selection = window.getSelection();
        if (!selection?.anchorNode) {
            return null;
        }

        let node = selection.anchorNode;
        if (node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        if (!node || !editor.contains(node)) {
            return null;
        }

        const anchor = node.closest ? node.closest('a[href]') : null;
        if (!anchor || !editor.contains(anchor)) {
            return null;
        }

        return anchor;
    }

    function selectionIsInLink() {
        return getLinkAtSelection() !== null;
    }

    function updateCtrlHeldState() {
        if (window.__mbwCtrlHeld) {
            editor.classList.add('ctrl-held');
        } else {
            editor.classList.remove('ctrl-held');
        }
    }

    window.insertLink = function (url, text) {
        focusEditor();
        const selection = window.getSelection();
        const displayText = text || url || '';
        if (!selection || selection.rangeCount === 0) return;

        const range = selection.getRangeAt(0);
        if (range.collapsed) {
            const anchor = document.createElement('a');
            anchor.href = url;
            anchor.textContent = displayText;
            normalizeLink(anchor);
            range.insertNode(anchor);
        } else {
            document.execCommand('createLink', false, url);
            normalizeAllLinks(editor);
        }
        postContentChanged();
    };

    window.removeLink = function () {
        window.execEditorCommand('unlink');
    };

    let activeImage = null;
    let resizeFrame = null;
    let isResizingImage = false;
    let imagePointerDown = null;

    function prepareImages(root) {
        root.querySelectorAll('img').forEach((img) => {
            img.draggable = false;
        });
    }

    function deselectImage() {
        if (resizeFrame) {
            resizeFrame.remove();
            resizeFrame = null;
        }

        if (activeImage) {
            activeImage.classList.remove('mbw-image-object-active');
        }

        activeImage = null;
        imagePointerDown = null;
    }

    function beginImagePointer(img, clientX, clientY) {
        imagePointerDown = {
            img,
            x: clientX,
            y: clientY,
            dragged: false
        };
    }

    function finishImagePointer(event) {
        if (!imagePointerDown || event.button !== 0) {
            return;
        }

        const pending = imagePointerDown;
        imagePointerDown = null;

        if (!pending.dragged && pending.img.isConnected) {
            event.preventDefault();
            selectImageObject(pending.img);
        }
    }

    function handleImagePointerMove(event) {
        if (!imagePointerDown || (event.buttons & 1) !== 1) {
            return;
        }

        const dx = Math.abs(event.clientX - imagePointerDown.x);
        const dy = Math.abs(event.clientY - imagePointerDown.y);
        if (dx > 4 || dy > 4) {
            imagePointerDown.dragged = true;
        }
    }

    function isCaretAdjacentToImage(img, selection) {
        if (!selection || selection.rangeCount === 0 || !selection.isCollapsed) {
            return false;
        }

        const range = selection.getRangeAt(0);
        const container = range.startContainer;

        if (container === img) {
            return true;
        }

        if (container.nodeType !== Node.ELEMENT_NODE) {
            return false;
        }

        const children = container.childNodes;
        for (let i = 0; i < children.length; i++) {
            if (children[i] === img) {
                return range.startOffset === i || range.startOffset === i + 1;
            }
        }

        return false;
    }

    function placeCaretAfterImage(img) {
        const selection = window.getSelection();
        if (!selection) {
            return;
        }

        const range = document.createRange();
        range.setStartAfter(img);
        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function updateResizeFramePosition() {
        if (!activeImage || !resizeFrame) {
            return;
        }

        const rect = activeImage.getBoundingClientRect();
        resizeFrame.style.left = `${rect.left}px`;
        resizeFrame.style.top = `${rect.top}px`;
        resizeFrame.style.width = `${rect.width}px`;
        resizeFrame.style.height = `${rect.height}px`;
    }

    function startImageResize(event, corner) {
        if (!activeImage) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        isResizingImage = true;

        const startX = event.clientX;
        const startY = event.clientY;
        const rect = activeImage.getBoundingClientRect();
        const startWidth = rect.width;
        const startHeight = rect.height;
        const aspect = startWidth / Math.max(startHeight, 1);
        const maxWidth = editor.clientWidth - 16;

        function applySize(width) {
            const clampedWidth = Math.max(40, Math.min(width, maxWidth));
            activeImage.style.width = `${Math.round(clampedWidth)}px`;
            activeImage.style.height = `${Math.round(clampedWidth / aspect)}px`;
            activeImage.style.maxWidth = '100%';
            updateResizeFramePosition();
        }

        function onMove(moveEvent) {
            let nextWidth = startWidth;
            if (corner.includes('e')) {
                nextWidth = startWidth + (moveEvent.clientX - startX);
            } else if (corner.includes('w')) {
                nextWidth = startWidth - (moveEvent.clientX - startX);
            }

            if (corner.includes('n') || corner.includes('s')) {
                let nextHeight = startHeight;
                if (corner.includes('s')) {
                    nextHeight = startHeight + (moveEvent.clientY - startY);
                } else {
                    nextHeight = startHeight - (moveEvent.clientY - startY);
                }
                nextWidth = nextHeight * aspect;
            }

            applySize(nextWidth);
        }

        function onUp() {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            isResizingImage = false;
            postContentChanged();
            scheduleSelectionFormat();
        }

        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    }

    function createResizeFrame() {
        if (!activeImage) {
            return;
        }

        if (resizeFrame) {
            resizeFrame.remove();
            resizeFrame = null;
        }

        resizeFrame = document.createElement('div');
        resizeFrame.className = 'mbw-image-resize-frame';

        const moveSurface = document.createElement('div');
        moveSurface.className = 'mbw-image-move-surface';
        moveSurface.addEventListener('mousedown', (event) => {
            if (!activeImage || event.button !== 0) {
                return;
            }

            event.preventDefault();
            beginImagePointer(activeImage, event.clientX, event.clientY);
        });
        moveSurface.addEventListener('mousemove', handleImagePointerMove);
        moveSurface.addEventListener('mouseup', finishImagePointer);
        resizeFrame.appendChild(moveSurface);

        ['nw', 'ne', 'sw', 'se'].forEach((corner) => {
            const handle = document.createElement('div');
            handle.className = `mbw-image-resize-handle mbw-handle-${corner}`;
            handle.addEventListener('mousedown', (event) => startImageResize(event, corner));
            resizeFrame.appendChild(handle);
        });

        document.body.appendChild(resizeFrame);
        updateResizeFramePosition();
    }

    function selectImageObject(img) {
        if (!img || !editor.contains(img)) {
            return;
        }

        if (activeImage === img && resizeFrame) {
            updateResizeFramePosition();
            placeCaretAfterImage(img);
            img.classList.add('mbw-image-object-active');
            return;
        }

        if (activeImage) {
            activeImage.classList.remove('mbw-image-object-active');
        }

        activeImage = img;
        activeImage.classList.add('mbw-image-object-active');
        createResizeFrame();
        placeCaretAfterImage(img);
    }

    window.insertImage = function (src, alt) {
        focusEditor();
        const img = document.createElement('img');
        img.src = src;
        img.alt = alt || '';
        img.draggable = false;
        img.style.maxWidth = '100%';
        img.style.height = 'auto';
        const range = getRange();
        range.insertNode(img);
        range.setStartAfter(img);
        range.collapse(true);
        restoreSelection(range);
        prepareImages(editor);
        selectImageObject(img);
        postContentChanged();
    };

    window.insertHorizontalRule = function () {
        focusEditor();
        document.execCommand('insertHorizontalRule', false, null);
        postContentChanged();
    };

    window.insertTable = function (rows, cols) {
        focusEditor();

        const rowCount = Math.max(1, Math.min(20, parseInt(rows, 10) || 1));
        const colCount = Math.max(1, Math.min(20, parseInt(cols, 10) || 1));
        const cellStyle = 'border:1px solid #bfbfbf;padding:6px 8px;vertical-align:top;min-width:40px;';
        const tableStyle = 'border-collapse:collapse;width:100%;margin:0 0 10px;table-layout:fixed;';

        let html = `<table cellpadding="0" cellspacing="0" style="${tableStyle}"><tbody>`;
        for (let row = 0; row < rowCount; row++) {
            html += '<tr>';
            for (let col = 0; col < colCount; col++) {
                html += `<td style="${cellStyle}"><br></td>`;
            }
            html += '</tr>';
        }
        html += '</tbody></table>';

        document.execCommand('insertHTML', false, html);
        postContentChanged();
        notifySelectionFormatAfterCommand();
    };

    window.insertVariable = function (variableName) {
        focusEditor();
        const range = getRange();
        const selection = window.getSelection();
        const span = document.createElement('span');
        span.className = 'variable';
        span.textContent = variableName;
        span.contentEditable = 'false';
        range.insertNode(span);
        range.setStartAfter(span);
        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);
        postContentChanged();
    };

    window.clearFormatting = function () {
        window.execEditorCommand('removeFormat');
    };

    window.clearEditor = function () {
        editor.innerHTML = '';
        postContentChanged();
        focusEditor();
    };

    window.pastePlainText = function (text) {
        focusEditor();
        document.execCommand('insertText', false, text);
        postContentChanged();
    };

    window.insertHtmlAtSelection = function (html) {
        focusEditor();
        document.execCommand('insertHTML', false, html || '');
        normalizeAllLinks(editor);
        prepareImages(editor);
        postContentChanged();
        notifySelectionFormatAfterCommand();
    };

    window.getEditorContent = function () {
        return editor.innerHTML;
    };

    window.setEditorContent = function (html) {
        editor.innerHTML = html || '';
        highlightVariables(false);
        normalizeAllLinks(editor);
        prepareImages(editor);
        deselectImage();
        postContentChanged();
    };

    function highlightVariables(notify) {
        const html = editor.innerHTML;
        const temp = document.createElement('div');
        temp.innerHTML = html;

        walkDOM(temp, (node) => {
            if (node.nodeType !== Node.TEXT_NODE || !node.textContent) return;
            if (node.parentElement && node.parentElement.classList.contains('variable')) return;

                const text = node.textContent;
                const matches = [...text.matchAll(variablePattern)];
            if (matches.length === 0) return;

                    const fragment = document.createDocumentFragment();
                    let lastIndex = 0;
            matches.forEach((match) => {
                        if (match.index > lastIndex) {
                    fragment.appendChild(document.createTextNode(text.substring(lastIndex, match.index)));
                        }
                        const span = document.createElement('span');
                        span.className = 'variable';
                        span.textContent = match[0];
                span.contentEditable = 'false';
                        fragment.appendChild(span);
                        lastIndex = match.index + match[0].length;
                    });
                    if (lastIndex < text.length) {
                fragment.appendChild(document.createTextNode(text.substring(lastIndex)));
            }
            node.parentNode.replaceChild(fragment, node);
        });

        editor.innerHTML = temp.innerHTML;
        if (notify) postContentChanged();
    }

    function walkDOM(node, callback) {
        callback(node);
        let child = node.firstChild;
        while (child) {
            walkDOM(child, callback);
            child = child.nextSibling;
        }
    }

    let notifyTimeout;
    let selectionFormatTimeout;
    let commandFormatTimeout;

    function normalizeFontFamily(value) {
        if (!value) {
            return null;
        }

        return value.replace(/['"]/g, '').split(',')[0].trim();
    }

    function getCurrentFontSizePx() {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return null;
        }

        let node = selection.anchorNode;
        if (node && node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        if (!node || !editor.contains(node)) {
            return null;
        }

        return parseInt(window.getComputedStyle(node).fontSize, 10) || null;
    }

    function getCurrentLineHeight() {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return '1.15';
        }

        let node = selection.anchorNode;
        if (node && node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        while (node && node !== editor && !/^(P|DIV|LI|H1|H2|H3|H4|H5|H6)$/i.test(node.tagName)) {
            node = node.parentElement;
        }

        if (!node || node === editor) {
            return '1.15';
        }

        const inlineHeight = node.style.lineHeight;
        if (inlineHeight && inlineHeight !== 'normal') {
            return inlineHeight;
        }

        const computed = window.getComputedStyle(node).lineHeight;
        if (!computed || computed === 'normal') {
            return '1.15';
        }

        if (computed.endsWith('px')) {
            const fontSize = parseFloat(window.getComputedStyle(node).fontSize) || 14;
            const ratio = parseFloat(computed) / fontSize;
            return ratio.toFixed(2).replace(/\.?0+$/, '');
        }

        return computed;
    }

    function getAlignment() {
        if (document.queryCommandState('justifyFull')) {
            return 'justify';
        }
        if (document.queryCommandState('justifyCenter')) {
            return 'center';
        }
        if (document.queryCommandState('justifyRight')) {
            return 'right';
        }
        return 'left';
    }

    function postSelectionFormat() {
        postMessage({
            type: 'selectionFormat',
            state: {
                bold: document.queryCommandState('bold'),
                italic: document.queryCommandState('italic'),
                underline: document.queryCommandState('underline'),
                strikeThrough: document.queryCommandState('strikeThrough'),
                superscript: document.queryCommandState('superscript'),
                subscript: document.queryCommandState('subscript'),
                unorderedList: document.queryCommandState('insertUnorderedList'),
                orderedList: document.queryCommandState('insertOrderedList'),
                fontFamily: normalizeFontFamily(document.queryCommandValue('fontName')),
                fontSize: getCurrentFontSizePx(),
                lineHeight: getCurrentLineHeight(),
                alignment: getAlignment(),
                inLink: selectionIsInLink(),
                linkUrl: getLinkAtSelection()?.getAttribute('href') || null,
                imageSelected: activeImage !== null
            }
        });
    }

    function scheduleSelectionFormat() {
        clearTimeout(selectionFormatTimeout);
        selectionFormatTimeout = setTimeout(postSelectionFormat, 16);
    }

    window.postSelectionFormat = postSelectionFormat;

    editor.addEventListener('input', () => {
        clearTimeout(notifyTimeout);
        notifyTimeout = setTimeout(postContentChanged, 120);
    });

    editor.addEventListener('beforeinput', (event) => {
        if (!pendingFontSize || event.inputType !== 'insertText' || !event.data) {
            return;
        }

        event.preventDefault();
        focusEditor();

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return;
        }

        const range = selection.getRangeAt(0);
        const span = document.createElement('span');
        span.style.fontSize = `${pendingFontSize}px`;
        span.style.lineHeight = 'inherit';
        span.style.verticalAlign = 'baseline';
        span.textContent = event.data;
        range.insertNode(span);

        const caret = document.createRange();
        caret.setStartAfter(span);
        caret.collapse(true);
        selection.removeAllRanges();
        selection.addRange(caret);

        pendingFontSize = null;
        postContentChanged();
    });

    editor.addEventListener('keyup', (event) => {
        if (event.ctrlKey || event.metaKey) {
            notifySelectionFormatAfterCommand();
            return;
        }

        scheduleSelectionFormat();
    });
    editor.addEventListener('mouseup', scheduleSelectionFormat);

    document.addEventListener('selectionchange', () => {
        const selection = window.getSelection();
        if (selection?.anchorNode && editor.contains(selection.anchorNode)) {
            scheduleSelectionFormat();
        }

        if (!activeImage || isResizingImage) {
            return;
        }

        if (!selection || selection.rangeCount === 0) {
            return;
        }

        if (!selection.isCollapsed) {
            deselectImage();
            return;
        }

        if (!isCaretAdjacentToImage(activeImage, selection)) {
            deselectImage();
        }
    });

    editor.addEventListener('mousedown', (event) => {
        if (event.target instanceof HTMLImageElement && editor.contains(event.target)) {
            if (event.button !== 0) {
                return;
            }

            event.preventDefault();
            beginImagePointer(event.target, event.clientX, event.clientY);
            return;
        }

        if (!event.target.closest('.mbw-image-resize-frame')) {
            deselectImage();
        }
    });

    editor.addEventListener('mousemove', handleImagePointerMove);

    editor.addEventListener('mouseup', finishImagePointer);

    const canvasScroll = document.getElementById('canvas-scroll');
    canvasScroll?.addEventListener('scroll', () => {
        if (activeImage) {
            updateResizeFramePosition();
        }
    }, { passive: true });
    window.addEventListener('resize', () => {
        if (activeImage) {
            updateResizeFramePosition();
        }
    });

    editor.addEventListener('blur', postContentChanged);

    editor.addEventListener('paste', (event) => {
        if (!event.clipboardData) return;
        const plain = event.clipboardData.getData('text/plain');
        if (event.shiftKey && plain) {
            event.preventDefault();
            window.pastePlainText(plain);
            return;
        }

        setTimeout(() => {
            normalizeAllLinks(editor);
            prepareImages(editor);
        }, 0);
    });

    editor.addEventListener('click', (event) => {
        const anchor = findLinkFromEventTarget(event.target);
        if (!anchor) {
            return;
        }

        const url = anchor.getAttribute('href');
        if (!url) {
            return;
        }

        if (event.ctrlKey || event.metaKey) {
            event.preventDefault();
            event.stopPropagation();
            postMessage({ type: 'openLink', url });
            return;
        }

        event.preventDefault();
    }, true);

    window.addEventListener('keydown', (event) => {
        if (event.key === 'Control' || event.key === 'Meta') {
            window.__mbwCtrlHeld = true;
            updateCtrlHeldState();
        }
    });

    window.addEventListener('keyup', (event) => {
        if (event.key === 'Control' || event.key === 'Meta') {
            window.__mbwCtrlHeld = false;
            updateCtrlHeldState();
        }
    });

    window.addEventListener('blur', () => {
        window.__mbwCtrlHeld = false;
        updateCtrlHeldState();
    });

    editor.addEventListener('keydown', (event) => {
        if (!event.ctrlKey && !event.metaKey) return;

        const key = event.key.toLowerCase();
        switch (key) {
            case 'b':
                event.preventDefault();
                window.execEditorCommand('bold');
                break;
            case 'i':
                event.preventDefault();
                window.execEditorCommand('italic');
                break;
            case 'u':
                event.preventDefault();
                window.execEditorCommand('underline');
                break;
            case 'z':
                event.preventDefault();
                window.execEditorCommand(event.shiftKey ? 'redo' : 'undo');
                break;
            case 'y':
                event.preventDefault();
                window.execEditorCommand('redo');
                break;
            case 'l':
                event.preventDefault();
                window.execEditorCommand('justifyLeft');
                break;
            case 'e':
                event.preventDefault();
                window.execEditorCommand('justifyCenter');
                break;
            case 'r':
                event.preventDefault();
                window.execEditorCommand('justifyRight');
                break;
            case 'j':
                event.preventDefault();
                window.execEditorCommand('justifyFull');
                break;
            case 'k':
                event.preventDefault();
                postMessage({ type: 'requestLink' });
                break;
            case 's':
                event.preventDefault();
                postMessage({ type: 'requestSave' });
                break;
            default:
                break;
        }

        if (event.shiftKey && key === 'v') {
            // handled in paste event
        }
    });

    if (window.initialContent) {
        editor.innerHTML = window.initialContent;
        highlightVariables(false);
        normalizeAllLinks(editor);
        prepareImages(editor);
    }

    postContentChanged();
    scheduleSelectionFormat();
    focusEditor();

    window.__mbwEditorApi = {
        editor,
        focusEditor,
        execEditorCommand: window.execEditorCommand,
        setFontFamily: window.setFontFamily,
        setFontSize: window.setFontSize,
        setForeColor: window.setForeColor,
        setBackColor: window.setBackColor,
        pastePlainText: window.pastePlainText,
        removeLink: window.removeLink,
        clearFormatting: window.clearFormatting,
        postMessage,
        postContentChanged,
        normalizeAllLinks,
        getCurrentFontSizePx,
        normalizeFontFamily,
        getActiveImage: () => activeImage,
        selectImageObject
    };
})();
