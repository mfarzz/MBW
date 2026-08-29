# 🎯 STEP 5: Simple HTML Editor (MBW.App)

## Understanding
Implement email template editor UI in MBW.App using ContentEditable div (via WebView2) for WYSIWYG editing with formatting toolbar. Integrate with STEP 3 (Excel variables) and STEP 4 (template rendering). Support variable insertion (via typing or dropdown), auto-highlighting of variables, and preview rendering.

## Assumptions
- Use WebView2 (already in project) for ContentEditable HTML editor
- Toolbar commands via JavaScript injection into WebView2
- Variables extracted from loaded Excel via ExcelImporter (STEP 3)
- On-demand save only (no auto-save)
- Variable insertion: Both typing {Var} and dropdown menu
- Variable highlighting: Auto-detect and style {Variable} patterns
- Preview shows first recipient from Excel data
- Keep XAML/C# clean; minimize code-behind via MVVM Toolkit

## Approach
Create MVVM-based email editor page in MBW.App:

**UI Components:**
- EmailEditorPage.xaml: Main layout with subject, toolbar, editor area, variables panel
- Toolbar buttons: Undo/Redo, B/I/U, Font, Size, Alignment, Lists, Link, Insert Variable
- Variables sidebar panel: List of available variables with Insert buttons
- ContentEditable area via WebView2: HTML rich text editor
- Preview dialog: Render email for first recipient using RenderForRecipient() from STEP 4

**ViewModel:**
- EmailEditorViewModel.cs: MVVM Community Toolkit with ObservableProperty, RelayCommand
- Properties: Subject, HtmlBody, AvailableVariables (ObservableCollection), CurrentWorkspace
- Commands: SaveCommand, PreviewCommand, InsertVariableCommand, ToolbarCommands (Bold, Italic, etc)
- Integration: Load workspace & Excel data; render preview

**JavaScript/WebView2:**
- setup-editor.js: Initialize ContentEditable, handle commands, sync to ViewModel
- highlighting.js: Auto-detect {Variable} patterns, apply CSS styling
- toolbar.js: Execute formatting commands (document.execCommand)

**Integration Points:**
- Load ExcelImporter.GetHeadersAsync() → populate AvailableVariables
- Load WorkspaceService.OpenAsync() → load existing template
- Preview: Use EmailTemplate.RenderForRecipient() (STEP 4)
- Save: Use WorkspaceService.SaveAsync() (STEP 2)

## Key Files
- MBW.App/Views/EmailEditorPage.xaml & .xaml.cs
- MBW.App/ViewModels/EmailEditorViewModel.cs
- MBW.App/Assets/js/setup-editor.js
- MBW.App/Assets/js/highlighting.js
- MBW.App/Assets/css/editor-styles.css

## Risks & Open Questions
- WebView2 sandbox: Limited access to Windows APIs (use JSON messaging)
- Variable highlighting performance: Needs debouncing for large templates
- Copy-paste handling: May lose formatting; requires sanitization
- Excel data loading: Must load headers before showing variables panel

**Progress**: 37% [███░░░░░░░]

**Last Updated**: 2026-08-29 03:57:07

## 📝 Plan Steps
- ✅ **Create EmailEditorPage UI (XAML)**
- ✅ **Create EmailEditorViewModel**
- ✅ **Implement WebView2 JavaScript integration**
- 🔄 **Implement Preview Dialog**
-  **Wire up toolbar commands**
-  **Integration tests**
-  **Build and test**
-  **Document in STEPS/STEP-05-HTML-EDITOR.md**

