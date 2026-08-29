// setup-editor.js - Initialize ContentEditable editor in WebView2

(function() {
    const editor = document.getElementById('editor');

    if (!editor) {
        console.error('Editor element not found');
        return;
    }

    // Make editor contenteditable
    editor.contentEditable = true;
    editor.spellcheck = true;
    editor.style.padding = '16px';
    editor.style.minHeight = '300px';
    editor.style.fontFamily = '"Segoe UI", "Helvetica Neue", sans-serif';
    editor.style.fontSize = '14px';
    editor.style.lineHeight = '1.6';
    editor.style.color = '#333';
    editor.style.outline = 'none';

    // Insert variable at cursor position
    window.insertVariable = function(variableName) {
        const selection = window.getSelection();
        const range = selection.getRangeAt(0);

        // Create span for variable
        const variableSpan = document.createElement('span');
        variableSpan.className = 'variable';
        variableSpan.textContent = variableName;
        variableSpan.style.color = '#0078D4';
        variableSpan.style.fontWeight = '500';
        variableSpan.style.backgroundColor = '#E7F3FF';
        variableSpan.style.padding = '2px 4px';
        variableSpan.style.borderRadius = '2px';
        variableSpan.style.cursor = 'default';

        range.insertNode(variableSpan);
        range.setStartAfter(variableSpan);
        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);

        editor.focus();
    };

    // Get HTML content
    window.getEditorContent = function() {
        return editor.innerHTML;
    };

    // Set HTML content
    window.setEditorContent = function(html) {
        editor.innerHTML = html;
        highlightVariables();
    };

    // Clear content
    window.clearEditor = function() {
        editor.innerHTML = '';
        editor.focus();
    };

    // Highlight variables with auto-detect
    function highlightVariables() {
        const html = editor.innerHTML;
        const variablePattern = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g;

        // Create temporary container
        const temp = document.createElement('div');
        temp.innerHTML = html;

        // Find and replace variable text nodes
        walkDOM(temp, (node) => {
            if (node.nodeType === Node.TEXT_NODE) {
                const text = node.textContent;
                const matches = [...text.matchAll(variablePattern)];

                if (matches.length > 0) {
                    const fragment = document.createDocumentFragment();
                    let lastIndex = 0;

                    matches.forEach(match => {
                        // Add text before variable
                        if (match.index > lastIndex) {
                            fragment.appendChild(
                                document.createTextNode(text.substring(lastIndex, match.index))
                            );
                        }

                        // Create variable span
                        const span = document.createElement('span');
                        span.className = 'variable';
                        span.textContent = match[0];
                        span.style.color = '#0078D4';
                        span.style.fontWeight = '500';
                        span.style.backgroundColor = '#E7F3FF';
                        span.style.padding = '2px 4px';
                        span.style.borderRadius = '2px';
                        span.style.cursor = 'default';
                        span.contentEditable = 'false'; // Prevent editing

                        fragment.appendChild(span);
                        lastIndex = match.index + match[0].length;
                    });

                    // Add remaining text
                    if (lastIndex < text.length) {
                        fragment.appendChild(
                            document.createTextNode(text.substring(lastIndex))
                        );
                    }

                    node.parentNode.replaceChild(fragment, node);
                }
            }
        });

        editor.innerHTML = temp.innerHTML;
    }

    // Walk DOM tree
    function walkDOM(node, callback) {
        callback(node);
        node = node.firstChild;
        while (node) {
            walkDOM(node, callback);
            node = node.nextSibling;
        }
    }

    // Track changes and update highlighting (debounced)
    let highlightTimeout;
    editor.addEventListener('input', () => {
        clearTimeout(highlightTimeout);
        highlightTimeout = setTimeout(() => {
            highlightVariables();
        }, 500); // Debounce 500ms
    });

    editor.addEventListener('keyup', () => {
        clearTimeout(highlightTimeout);
        highlightTimeout = setTimeout(() => {
            highlightVariables();
        }, 500);
    });

    // Sync content on blur
    editor.addEventListener('blur', () => {
        // Send content back to C# if needed
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({
                type: 'editorContent',
                content: editor.innerHTML
            });
        }
    });

    // Initialize with optional content
    if (window.initialContent) {
        editor.innerHTML = window.initialContent;
        highlightVariables();
    }

    editor.focus();
})();
