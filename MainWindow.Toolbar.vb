' MainWindow.Toolbar.vb - Toolbar creation and management for MainWindow
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow

    ' Toolbar buttons - all bevel-styled CustomDrawButton (see AddToolbarButton), matching
    ' the retro 3D look already used for the notebook nav buttons and the Explorer toolbars
    Private pNewButton As CustomDrawButton
    Private pOpenButton As CustomDrawButton
    Private pSaveButton As CustomDrawButton
    Private pSaveAllButton As CustomDrawButton
    Private pUndoButton As CustomDrawButton
    Private pRedoButton As CustomDrawButton
    Private pCutButton As CustomDrawButton
    Private pCopyButton As CustomDrawButton
    Private pPasteButton As CustomDrawButton
    Private pFindButton As CustomDrawButton
    Private pBuildButton As CustomDrawButton
    Private pRunButton As CustomDrawButton
    Private pStopButton As CustomDrawButton
    Private pGitButton As CustomDrawButton
    Private pAIButton As CustomDrawButton
    Private pHelpButton As CustomDrawButton
    Private pOutdentToolButton As CustomDrawButton
    Private pIndentToolButton As CustomDrawButton
    Private pToggleCommentButton As CustomDrawButton
    Private pOutputPanelToggleButton As CustomDrawButton
    Private pQuickFindClipboardButton As CustomDrawButton

    ''' <summary>
    ''' Records how one toolbar button's icon was loaded, so RefreshToolbarAppearance can
    ''' reload every button's icon (at a new pixel size and/or dark/light variant) and
    ''' re-apply the current show-labels setting without duplicating each button's load
    ''' logic a second time, the way CreateToolbar/UpdateToolbarIcons used to
    ''' </summary>
    Private Class ToolbarIconSpec
        Public Button As CustomDrawButton
        Public ResourceBaseName As String
        Public UsesDarkVariant As Boolean
        Public FallbackIconName As String
        Public LabelText As String
    End Class

    Private pToolbarIconSpecs As New List(Of ToolbarIconSpec)

    ' ===== Toolbar Creation =====

    Private Sub CreateToolbar()
        Try
            pToolbar = New Toolbar()

            ' A click that lands on the toolbar's own background (not on any button/control -
            ' e.g. the gap after the last item) is otherwise never claimed by any child widget,
            ' so GTK delivers it to the nearest ancestor that owns an input window - which ends
            ' up being this window itself. A double-click there is then apparently interpreted
            ' as equivalent to double-clicking the titlebar (toggling maximize/restore) by this
            ' desktop's window manager. Explicitly marking the event handled here stops it from
            ' ever reaching that point - individual toolbar buttons/controls have their own
            ' input windows and are unaffected, since GTK only delivers here when nothing more
            ' specific already claimed the click.
            AddHandler pToolbar.ButtonPressEvent, AddressOf OnToolbarBackgroundButtonPress

            ' Swallowing the press alone stopped the double-click-to-maximize behavior, but
            ' James found click-and-drag on the same background still moves the window like
            ' a titlebar - that's very likely the window manager's own X11-level drag
            ' tracking off the raw pointer motion (independent of whether GTK marked the
            ' press "handled"), so also swallow motion here in case it IS GTK-level. Widgets
            ' don't receive motion events by default unless they request the mask.
            pToolbar.Events = pToolbar.Events Or Gdk.EventMask.PointerMotionMask
            AddHandler pToolbar.MotionNotifyEvent, AddressOf OnToolbarBackgroundMotion

            pToolbarIconSpecs = New List(Of ToolbarIconSpec)

            Dim lPixelSize As Integer = GetToolbarIconPixelSize()
            Dim lShowLabels As Boolean = pSettingsManager.ToolbarShowLabels

            ' File operations
            pNewButton = AddToolbarButton("new", False, "document-new", "New", "New File (Ctrl+N)", lPixelSize, lShowLabels)
            AddHandler pNewButton.Clicked, AddressOf OnNewFile

            pOpenButton = AddToolbarButton("open", False, "document-open", "Open", "Open File (Ctrl+O)", lPixelSize, lShowLabels)
            AddHandler pOpenButton.Clicked, AddressOf OnOpenFile

            pSaveButton = AddToolbarButton("disc", False, "document-save", "Save", "Save File (Ctrl+S)", lPixelSize, lShowLabels)
            AddHandler pSaveButton.Clicked, AddressOf OnSaveFile

            pSaveAllButton = AddToolbarButton("saveall", False, "document-saveall", "Save All", "Save All Files (Ctrl+Shift+S)", lPixelSize, lShowLabels)
            AddHandler pSaveAllButton.Clicked, AddressOf OnSaveAll

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Edit operations
            pUndoButton = AddToolbarButton("undo", True, "edit-undo", "Undo", "Undo (Ctrl+Z)", lPixelSize, lShowLabels)
            AddHandler pUndoButton.Clicked, AddressOf OnUndo

            pRedoButton = AddToolbarButton("redo", True, "edit-redo", "Redo", "Redo (Ctrl+y)", lPixelSize, lShowLabels)
            AddHandler pRedoButton.Clicked, AddressOf OnRedo

            pToolbar.Insert(New SeparatorToolItem(), -1)

            pCutButton = AddToolbarButton("cut", True, "edit-cut", "Cut", "Cut (Ctrl+x)", lPixelSize, lShowLabels)
            AddHandler pCutButton.Clicked, AddressOf OnCut

            pCopyButton = AddToolbarButton("copy", True, "edit-copy", "Copy", "Copy (Ctrl+C)", lPixelSize, lShowLabels)
            AddHandler pCopyButton.Clicked, AddressOf OnCopy

            pPasteButton = AddToolbarButton("paste", True, "edit-paste", "Paste", "Paste (Ctrl+V)", lPixelSize, lShowLabels)
            AddHandler pPasteButton.Clicked, AddressOf OnPaste

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Find
            pFindButton = AddToolbarButton("find", True, "edit-find", "Find", "Find (Ctrl+F)", lPixelSize, lShowLabels)
            AddHandler pFindButton.Clicked, AddressOf OnShowFindPanel

            CreateQuickFindFromClipboardButton(lPixelSize, lShowLabels)

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Outdent button
            pOutdentToolButton = AddToolbarButton("outdent", True, "format-indent-less", "Outdent", "Outdent (Ctrl+[ or Shift+Tab)", lPixelSize, lShowLabels)
            AddHandler pOutdentToolButton.Clicked, AddressOf OnOutdent

            ' Indent button
            pIndentToolButton = AddToolbarButton("indent", True, "format-indent-more", "Indent", "Indent (Ctrl+] or Tab when Text is selected)", lPixelSize, lShowLabels)
            AddHandler pIndentToolButton.Clicked, AddressOf OnIndent

            ' Toggle Comment button
            pToggleCommentButton = AddToolbarButton("comment", True, "format-text-bold", "Comment", "Toggle Comment Block (Ctrl+')", lPixelSize, lShowLabels)
            AddHandler pToggleCommentButton.Clicked, AddressOf OnToggleComment

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Build operations
            pBuildButton = AddToolbarButton("build_start", True, "media-eject", "Build", "Build project (F6)", lPixelSize, lShowLabels)
            AddHandler pBuildButton.Clicked, AddressOf OnBuildProject

            pRunButton = AddToolbarButton("run", True, "media-playback-start", "Run", "Run project (builds If needed) (Shift+F5)", lPixelSize, lShowLabels)
            AddHandler pRunButton.Clicked, AddressOf OnRunProject

            pStopButton = AddToolbarButton("build_stop", True, "media-playback-stop", "Stop", "Stop Debugging", lPixelSize, lShowLabels)
            AddHandler pStopButton.Clicked, AddressOf OnStopDebugging

            pOutputPanelToggleButton = AddToolbarButton("bottom", False, "view-paged", "Output", "Toggle Output Panel", lPixelSize, lShowLabels)
            AddHandler pOutputPanelToggleButton.Clicked, AddressOf ToggleBottomPanel

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Git
            pGitButton = AddToolbarButton("git", False, "git", "git", "git Status", lPixelSize, lShowLabels)
            AddHandler pGitButton.Clicked, AddressOf OnShowGitPanel

            ' AI Assistant - no embedded PNG for this one, always the "chat" themed icon
            pAIButton = AddToolbarButton("", False, "chat", "AI", "AI Assistant", lPixelSize, lShowLabels)
            AddHandler pAIButton.Clicked, AddressOf OnShowAIAssistant

            ' Help - no embedded PNG for this one, always the "help-browser" themed icon
            pHelpButton = AddToolbarButton("", False, "help-browser", "Help", "Help (F1)", lPixelSize, lShowLabels)
            AddHandler pHelpButton.Clicked, AddressOf OnShowHelpPanel

            CreateScratchpadToolbarButton(lPixelSize, lShowLabels)

            'CreateDiagnosticToolbarButton(lPixelSize, lShowLabels)

            pToolbar.ShowAll()

        Catch ex As Exception
            Console.WriteLine($"CreateToolbar error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Creates one bevel-styled toolbar button, wires it into pToolbarIconSpecs so
    ''' RefreshToolbarAppearance can reload it later, and inserts it into pToolbar
    ''' </summary>
    ''' <param name="vResourceBaseName">Embedded PNG base name with no "SimpleIDE." prefix, no
    ''' "dark" suffix, and no ".png" extension (e.g. "undo") - pass "" to skip straight to the
    ''' themed fallback icon (used by buttons with no embedded PNG asset at all)</param>
    ''' <param name="vUsesDarkVariant">Whether a hand-authored "{name}dark.png" embedded
    ''' variant exists and should be preferred while the active theme is dark</param>
    ''' <param name="vFallbackIconName">Themed icon-theme name used if the embedded resource
    ''' can't be loaded</param>
    ''' <param name="vLabelText">Label shown under/beside the icon when labels are enabled</param>
    ''' <param name="vTooltip">Tooltip text</param>
    ''' <param name="vPixelSize">Icon pixel size to load/scale to</param>
    ''' <param name="vShowLabel">Whether to show vLabelText now (Toolbar > Show Labels setting)</param>
    Private Function AddToolbarButton(vResourceBaseName As String, vUsesDarkVariant As Boolean,
                                       vFallbackIconName As String, vLabelText As String, vTooltip As String,
                                       vPixelSize As Integer, vShowLabel As Boolean) As CustomDrawButton
        Dim lButton As New CustomDrawButton()
        Try
            lButton.IconPixbuf = LoadToolbarIconPixbuf(vResourceBaseName, vUsesDarkVariant, vFallbackIconName, vPixelSize)
            lButton.Label = If(vShowLabel, vLabelText, "")
            lButton.TooltipText = vTooltip
            lButton.Style = CustomDrawButton.eButtonStyle.eBevel
            lButton.ThemeManager = pThemeManager

            pToolbarIconSpecs.Add(New ToolbarIconSpec() With {
                .Button = lButton,
                .ResourceBaseName = vResourceBaseName,
                .UsesDarkVariant = vUsesDarkVariant,
                .FallbackIconName = vFallbackIconName,
                .LabelText = vLabelText
            })

            Dim lItem As New ToolItem()
            lItem.Add(lButton)
            pToolbar.Insert(lItem, -1)

        Catch ex As Exception
            Console.WriteLine($"AddToolbarButton error: {ex.Message}")
        End Try
        Return lButton
    End Function

    ''' <summary>
    ''' Loads an icon pixbuf for a main-toolbar button at the given pixel size - tries the
    ''' embedded "SimpleIDE.{name}[dark].png" resource first, falling back to a themed
    ''' icon-theme lookup if the resource is missing or vResourceBaseName is blank
    ''' </summary>
    Private Function LoadToolbarIconPixbuf(vResourceBaseName As String, vUsesDarkVariant As Boolean,
                                            vFallbackIconName As String, vPixelSize As Integer) As Gdk.Pixbuf
        Try
            If Not String.IsNullOrEmpty(vResourceBaseName) Then
                Dim lDarkSuffix As String = ""
                If vUsesDarkVariant AndAlso pThemeManager IsNot Nothing AndAlso pThemeManager.GetCurrentThemeObject.IsDarkTheme Then
                    lDarkSuffix = "dark"
                End If
                Dim lResourceName As String = $"SimpleIDE.{vResourceBaseName}{lDarkSuffix}.png"

                Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream(lResourceName)
                    If lStream IsNot Nothing Then
                        Dim lPixbuf As New Gdk.Pixbuf(lStream)
                        If lPixbuf.Width <> vPixelSize OrElse lPixbuf.Height <> vPixelSize Then
                            lPixbuf = lPixbuf.ScaleSimple(vPixelSize, vPixelSize, Gdk.InterpType.Bilinear)
                        End If
                        Return lPixbuf
                    End If
                End Using
            End If
        Catch ex As Exception
            Console.WriteLine($"LoadToolbarIconPixbuf embedded resource error ({vResourceBaseName}): {ex.Message}")
        End Try

        Try
            Return Gtk.IconTheme.Default.LoadIcon(vFallbackIconName, vPixelSize, IconLookupFlags.UseBuiltin)
        Catch ex As Exception
            Console.WriteLine($"LoadToolbarIconPixbuf fallback icon load error ({vFallbackIconName}): {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Resolves the current Toolbar Button Size setting to an actual icon pixel dimension
    ''' </summary>
    Private Function GetToolbarIconPixelSize() As Integer
        Try
            Dim lIconSize As IconSize = If(pSettingsManager.ToolbarLargeIcons, IconSize.LargeToolbar, IconSize.SmallToolbar)
            Dim lWidth As Integer = 0
            Dim lHeight As Integer = 0
            If Gtk.Icon.SizeLookup(lIconSize, lWidth, lHeight) AndAlso lWidth > 0 Then
                Return lWidth
            End If
        Catch ex As Exception
            Console.WriteLine($"GetToolbarIconPixelSize error: {ex.Message}")
        End Try
        Return If(pSettingsManager.ToolbarLargeIcons, 24, 16)
    End Function

    ' Toolbar event handlers

    ''' <summary>
    ''' Handles the Find toolbar button click - shows find panel and executes search if text is selected
    ''' </summary>
    ''' <param name="vSender">The sender of the event</param>
    ''' <param name="vArgs">Event arguments</param>
    Private Sub OnShowFindPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Show bottom panel with Find tab
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eFindResults)
            Else
                ' Fallback to old method
                ShowBottomPanel(1) ' Find Results tab
            End If

            ' Get the current editor
            Dim lEditor As IEditor = GetCurrentEditor()
            Dim lHasSelection As Boolean = False
            Dim lWordAtCursor As String = ""

            ' Check if there's selected text
            If lEditor IsNot Nothing AndAlso lEditor.HasSelection Then
                Dim lSelectedText As String = lEditor.SelectedText

                ' Only use if it's a single line
                If Not String.IsNullOrEmpty(lSelectedText) AndAlso
                   Not lSelectedText.Contains(vbLf) AndAlso
                   Not lSelectedText.Contains(vbCr) Then

                    lHasSelection = True

                    ' Set the search text in the find panel
                    pBottomPanelManager?.FindPanel?.SetSearchText(lSelectedText)

                    ' Execute the find with current options
                    pBottomPanelManager?.FindPanel?.OnFind(Nothing, Nothing)
                End If
            ElseIf lEditor IsNot Nothing Then
                ' No selection - get word at cursor
                lWordAtCursor = lEditor.GetWordAtCursor()

                ' If there's a word at cursor, use it as search text
                If Not String.IsNullOrEmpty(lWordAtCursor) Then
                    pBottomPanelManager?.FindPanel?.SetSearchText(lWordAtCursor)
                    pBottomPanelManager?.FindPanel?.OnFind(Nothing, Nothing)
                End If
            End If

            ' Focus search entry based on context
            If String.IsNullOrEmpty(lWordAtCursor) AndAlso Not lHasSelection Then
                ' No word at cursor and no selection - select all existing text
                pBottomPanelManager?.FindPanel?.FocusSearchEntry() ' Selects all
            Else
                ' Has word at cursor or selection - don't select text
                pBottomPanelManager?.FindPanel?.FocusSearchEntryNoSelect()
            End If

        Catch ex As Exception
            Console.WriteLine($"OnShowFindPanel error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles help toolbar button click - opens help in a center tab
    ''' </summary>
    Private Sub OnShowHelpPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Open help in a new tab instead of bottom panel
            Console.WriteLine($"OnShowHelpPanel Called")
            OpenHelpTab()

        Catch ex As Exception
            Console.WriteLine($"OnShowHelpPanel error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnShowGitPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Show bottom panel with Git tab
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eGit)

                ' Refresh git status
                pBottomPanelManager.GitPanel?.RefreshStatus()
            Else
                ' Fallback to old method
                ShowBottomPanel(5) ' git tab
            End If

        Catch ex As Exception
            Console.WriteLine($"OnShowGitPanel error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnShowAIAssistant(vSender As Object, vArgs As EventArgs)
        Try
            ' Show bottom panel with AI Assistant tab
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eAIAssistant)
            Else
                ' Fallback to old method
                ShowBottomPanel(3) ' AI Assistant tab
            End If

        Catch ex As Exception
            Console.WriteLine($"OnShowAIAssistant error: {ex.Message}")
        End Try
    End Sub

    ' Update toolbar button states based on current context
    Private Sub UpdateToolbarButtons()
        Try
            Dim lHasCurrentTab As Boolean = GetCurrentTabInfo() IsNot Nothing
            Dim lHasCurrentEditor As Boolean = GetCurrentEditor() IsNot Nothing

            ' File operations - New and Open should always be enabled
            pNewButton.Sensitive = True
            pOpenButton.Sensitive = True
            pSaveButton.Sensitive = lHasCurrentTab
            pSaveAllButton.Sensitive = lHasCurrentTab

            ' Edit operations
            pUndoButton.Sensitive = lHasCurrentEditor
            pRedoButton.Sensitive = lHasCurrentEditor
            pCutButton.Sensitive = lHasCurrentEditor
            pCopyButton.Sensitive = lHasCurrentEditor
            pPasteButton.Sensitive = lHasCurrentEditor
            pOutdentToolButton.Sensitive = lHasCurrentEditor
            pIndentToolButton.Sensitive = lHasCurrentEditor
            pToggleCommentButton.Sensitive = lHasCurrentEditor

            ' Find
            pFindButton.Sensitive = True ' Always enabled

            ' Build operations
            Dim lHasProject As Boolean = Not String.IsNullOrEmpty(pCurrentProject)
            pBuildButton.Sensitive = lHasProject
            pRunButton.Sensitive = lHasProject
            pStopButton.Sensitive = pIsDebugging
            pBuildOutputPanel?.SetProjectLoaded(lHasProject, pIsDebugging)

            ' Git, AI, Help - always enabled
            pGitButton.Sensitive = True
            pAIButton.Sensitive = True
            pHelpButton.Sensitive = True

        Catch ex As Exception
            Console.WriteLine($"UpdateToolbarButtons error: {ex.Message}")
        End Try
    End Sub

    ' ===== Toolbar Settings Application =====

    Private Sub ApplyToolbarSettings()
        Try
            If pToolbar Is Nothing Then Return

            ' Apply visibility
            If pSettingsManager.ShowToolbar Then
                pToolbar.Show()
            Else
                pToolbar.Hide()
                Return ' Don't need to apply other settings if hidden
            End If

            RefreshToolbarAppearance()

            ' Force redraw
            pToolbar.ShowAll()

        Catch ex As Exception
            Console.WriteLine($"ApplyToolbarSettings error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Reloads every toolbar button's icon (at the current Button Size/theme) and re-applies
    ''' the current Show Labels setting - called after a toolbar display setting changes, and
    ''' after a color theme change so hand-authored dark/light icon variants stay in sync
    ''' (the icon's own contrast-inversion, for buttons with no hand-authored variant, keeps
    ''' itself in sync automatically via CustomDrawButton's own ThemeManager subscription)
    ''' </summary>
    Private Sub RefreshToolbarAppearance()
        Try
            If pToolbarIconSpecs Is Nothing Then Return

            Dim lPixelSize As Integer = GetToolbarIconPixelSize()
            Dim lShowLabels As Boolean = pSettingsManager.ToolbarShowLabels

            For Each lSpec As ToolbarIconSpec In pToolbarIconSpecs
                lSpec.Button.IconPixbuf = LoadToolbarIconPixbuf(lSpec.ResourceBaseName, lSpec.UsesDarkVariant, lSpec.FallbackIconName, lPixelSize)
                lSpec.Button.Label = If(lShowLabels, lSpec.LabelText, "")
            Next

        Catch ex As Exception
            Console.WriteLine($"RefreshToolbarAppearance error: {ex.Message}")
        End Try
    End Sub


    Private Sub OnToggleToolbar(vSender As Object, vArgs As EventArgs)
        Try
            Dim lMenuItem As CheckMenuItem = DirectCast(vSender, CheckMenuItem)
            pSettingsManager.ShowToolbar = lMenuItem.Active

            ' Apply toolbar visibility
            ApplyToolbarSettings()

        Catch ex As Exception
            Console.WriteLine($"OnToggleToolbar error: {ex.Message}")
            ShowError("Toggle Toolbar failed", ex.Message)
        End Try
    End Sub

    Private Sub OnToggleToolbarLabels(vSender As Object, vArgs As EventArgs)
        Try
            Dim lMenuItem As CheckMenuItem = DirectCast(vSender, CheckMenuItem)
            pSettingsManager.ToolbarShowLabels = lMenuItem.Active

            ' Apply toolbar style
            ApplyToolbarSettings()

        Catch ex As Exception
            Console.WriteLine($"OnToggleToolbarLabels error: {ex.Message}")
            ShowError("Toggle Toolbar Labels failed", ex.Message)
        End Try
    End Sub

    Private Sub OnToolbarLargeButtons(vSender As Object, vArgs As EventArgs)
        Try
            Dim lMenuItem As RadioMenuItem = DirectCast(vSender, RadioMenuItem)
            If lMenuItem.Active Then
                pSettingsManager.ToolbarLargeIcons = True
                ApplyToolbarSettings()
            End If

        Catch ex As Exception
            Console.WriteLine($"OnToolbarLargeButtons error: {ex.Message}")
            ShowError("Set Large Toolbar Buttons failed", ex.Message)
        End Try
    End Sub

    Private Sub OnToolbarSmallButtons(vSender As Object, vArgs As EventArgs)
        Try
            Dim lMenuItem As RadioMenuItem = DirectCast(vSender, RadioMenuItem)
            If lMenuItem.Active Then
                pSettingsManager.ToolbarLargeIcons = False
                ApplyToolbarSettings()
            End If

        Catch ex As Exception
            Console.WriteLine($"OnToolbarSmallButtons error: {ex.Message}")
            ShowError("Set Small Toolbar Buttons failed", ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Creates a diagnostic toolbar button for running merge diagnostics
    ''' </summary>
    ''' <remarks>Not currently called from CreateToolbar (see the commented-out call) - kept
    ''' converted to the current CustomDrawButton pattern for consistency if re-enabled</remarks>
    Private Sub CreateDiagnosticToolbarButton(vPixelSize As Integer, vShowLabel As Boolean)
        Try
            pToolbar.Insert(New SeparatorToolItem(), -1)

            Dim lDiagnosticButton As CustomDrawButton = AddToolbarButton("", False, "dialog-warning", "Diagnostics",
                "Run Merge Diagnostics (Debug Partial Class merging)", vPixelSize, vShowLabel)
            ' AddHandler lDiagnosticButton.Clicked, AddressOf OnRunDiagnostics

            Console.WriteLine("Diagnostic toolbar button added successfully")

        Catch ex As Exception
            Console.WriteLine($"CreateDiagnosticToolbarButton error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Creates a toolbar button for quick find using clipboard content with F2 shortcut
    ''' </summary>
    ''' <summary>
    ''' Swallows a button press that lands on the toolbar's own background (not on any
    ''' button/control) so it can never propagate further - see the AddHandler site in
    ''' CreateToolbar for why this exists
    ''' </summary>
    Private Sub OnToolbarBackgroundButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
        Try
            vArgs.RetVal = True
        Catch ex As Exception
            Console.WriteLine($"OnToolbarBackgroundButtonPress error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Swallows pointer motion over the toolbar's own background - see the AddHandler site
    ''' in CreateToolbar for why this exists
    ''' </summary>
    Private Sub OnToolbarBackgroundMotion(vSender As Object, vArgs As MotionNotifyEventArgs)
        Try
            vArgs.RetVal = True
        Catch ex As Exception
            Console.WriteLine($"OnToolbarBackgroundMotion error: {ex.Message}")
        End Try
    End Sub

    Private Sub CreateQuickFindFromClipboardButton(vPixelSize As Integer, vShowLabel As Boolean)
        Try
            pToolbar.Insert(New SeparatorToolItem(), -1)

            pQuickFindClipboardButton = AddToolbarButton("magnifier", True, "edit-find-replace", "Quick Find",
                "Quick Find from Clipboard (F2) - Searches for clipboard text in entire project", vPixelSize, vShowLabel)
            AddHandler pQuickFindClipboardButton.Clicked, AddressOf OnQuickFindFromClipboard

            Console.WriteLine("Quick Find from Clipboard button added successfully")

        Catch ex As Exception
            Console.WriteLine($"CreateQuickFindFromClipboardButton error: {ex.Message}")
        End Try
    End Sub

End Class
