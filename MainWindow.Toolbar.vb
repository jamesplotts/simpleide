' MainWindow.Toolbar.vb - Toolbar creation and management for MainWindow
Imports Gtk
Imports System
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    
    ' Toolbar buttons
    Private pNewButton As ToolButton
    Private pOpenButton As ToolButton
    Private pSaveButton As ToolButton
    Private pSaveAllButton As ToolButton
    Private pUndoButton As ToolButton
    Private pRedoButton As ToolButton
    Private pCutButton As ToolButton
    Private pCopyButton As ToolButton
    Private pPasteButton As ToolButton
    Private pFindButton As ToolButton
    Private pBuildButton As ToolButton
    Private pRunButton As ToolButton
    Private pStopButton As ToolButton
    Private pGitButton As ToolButton
    Private pAIButton As ToolButton
    Private pHelpButton As ToolButton
    Private pReferencesButton As ToolButton
    Private pOutdentToolButton As ToolButton
    Private pIndentToolButton As ToolButton
    Private pToggleCommentButton As ToolButton
    Private pOutputPanelToggleButton As ToolButton

    
    ' ===== Toolbar Creation =====
    
    Private Sub CreateToolbar()
        Try
            pToolbar = New Toolbar()
    
            ' Set initial values from settings
            If pSettingsManager.ToolbarShowLabels Then
                pToolbar.ToolbarStyle = ToolbarStyle.Both
            Else
                pToolbar.ToolbarStyle = ToolbarStyle.Icons
            End If
            
            If pSettingsManager.ToolbarLargeIcons Then
                pToolbar.IconSize = IconSize.LargeToolbar
            Else
                pToolbar.IconSize = IconSize.SmallToolbar
            End If   

            ' Create toolbar buttons with proper icon size
            Dim lIconSize As IconSize = pToolbar.IconSize
         
            ' File operations
            pNewButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.new.png", lIconSize)
                lImg.Show()
                pNewButton.IconWidget = lImg
            Catch ex As Exception
                pNewButton.IconWidget = Image.NewFromIconName("document-New", lIconSize)
            End Try
            pNewButton.Label = "New"
            pNewButton.TooltipText = "New File (Ctrl+N)"
            AddHandler pNewButton.Clicked, AddressOf OnNewFile
            pToolbar.Insert(pNewButton, -1)
            
            pOpenButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.open.png", lIconSize)
                lImg.Show()
                pOpenButton.IconWidget = lImg
            Catch ex As Exception
                pOpenButton.IconWidget = Image.NewFromIconName("document-open", lIconSize)
            End Try
            pOpenButton.Label = "Open"
            pOpenButton.TooltipText = "Open File (Ctrl+O)"
            AddHandler pOpenButton.Clicked, AddressOf OnOpenFile
            pToolbar.Insert(pOpenButton, -1)
            
            pSaveButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.disc.png", lIconSize)
                lImg.Show()
                pSaveButton.IconWidget = lImg
            Catch ex As Exception
                pSaveButton.IconWidget = Image.NewFromIconName("document-Save", lIconSize)
            End Try
            pSaveButton.Label = "Save"
            pSaveButton.TooltipText = "Save File (Ctrl+S)"
            AddHandler pSaveButton.Clicked, AddressOf OnSaveFile
            pToolbar.Insert(pSaveButton, -1)
            
            pSaveAllButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.saveall.png", lIconSize)
                lImg.Show()
                pSaveAllButton.IconWidget = lImg
            Catch ex As Exception
                pSaveAllButton.IconWidget = Image.NewFromIconName("document-saveall", lIconSize)
            End Try
            pSaveAllButton.Label = "Save All"
            pSaveAllButton.TooltipText = "Save All Files (Ctrl+Shift+S)"
            AddHandler pSaveAllButton.Clicked, AddressOf OnSaveAll
            pToolbar.Insert(pSaveAllButton, -1)
            
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Edit operations
            pUndoButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.undo.png", lIconSize)
                lImg.Show()
                pUndoButton.IconWidget = lImg
            Catch ex As Exception
                pUndoButton.IconWidget = Image.NewFromIconName("edit-undo", lIconSize)
            End Try
            pUndoButton.Label = "Undo"
            pUndoButton.TooltipText = "Undo (Ctrl+Z)"
            AddHandler pUndoButton.Clicked, AddressOf OnUndo
            pToolbar.Insert(pUndoButton, -1)
            
            pRedoButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.redo.png", lIconSize)
                lImg.Show()
                pRedoButton.IconWidget = lImg
            Catch ex As Exception
                pRedoButton.IconWidget = Image.NewFromIconName("edit-redo", lIconSize)
            End Try
            pRedoButton.Label = "Redo"
            pRedoButton.TooltipText = "Redo (Ctrl+y)"
            AddHandler pRedoButton.Clicked, AddressOf OnRedo
            pToolbar.Insert(pRedoButton, -1)
            
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            pCutButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.cut.png", lIconSize)
                lImg.Show()
                pCutButton.IconWidget = lImg
            Catch ex As Exception
                pCutButton.IconWidget = Image.NewFromIconName("edit-cut", lIconSize)
            End Try
            pCutButton.Label = "Cut"
            pCutButton.TooltipText = "Cut (Ctrl+x)"
            AddHandler pCutButton.Clicked, AddressOf OnCut
            pToolbar.Insert(pCutButton, -1)
            
            pCopyButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.copy.png", lIconSize)
                lImg.Show()
                pCopyButton.IconWidget = lImg
            Catch ex As Exception
                pCopyButton.IconWidget = Image.NewFromIconName("edit-copy", lIconSize)
            End Try
            pCopyButton.Label = "Copy"
            pCopyButton.TooltipText = "Copy (Ctrl+C)"
            AddHandler pCopyButton.Clicked, AddressOf OnCopy
            pToolbar.Insert(pCopyButton, -1)
            
            pPasteButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.paste.png", lIconSize)
                lImg.Show()
                pPasteButton.IconWidget = lImg
            Catch ex As Exception
                pPasteButton.IconWidget = Image.NewFromIconName("edit-paste", lIconSize)
            End Try
            pPasteButton.Label = "Paste"
            pPasteButton.TooltipText = "Paste (Ctrl+V)"
            AddHandler pPasteButton.Clicked, AddressOf OnPaste
            pToolbar.Insert(pPasteButton, -1)
            
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Find
            pFindButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.find.png", lIconSize)
                lImg.Show()
                pFindButton.IconWidget = lImg
            Catch ex As Exception
                pFindButton.IconWidget = Image.NewFromIconName("edit-find", lIconSize)
            End Try
            pFindButton.Label = "Find"
            pFindButton.TooltipText = "Find (Ctrl+F)"
            AddHandler pFindButton.Clicked, AddressOf OnShowFindPanel
            pToolbar.Insert(pFindButton, -1)
            
            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' outdent button
            pOutdentToolButton = New ToolButton(Nothing, "Outdent")
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.outdent.png", lIconSize)
                lImg.Show()
                pOutdentToolButton.IconWidget = lImg
            Catch ex As Exception
                pOutdentToolButton.IconWidget = Image.NewFromIconName("format-indent-less", lIconSize)
            End Try
            pOutdentToolButton.TooltipText = "Outdent (Ctrl+[ or Shift+Tab)"
            AddHandler pOutdentToolButton.Clicked, AddressOf OnOutdent
            pOutdentToolButton.Sensitive = True
            pToolbar.Insert(pOutdentToolButton, -1)
            
            ' Indent button
            pIndentToolButton = New ToolButton(Nothing, "Indent")
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.indent.png", lIconSize)
                lImg.Show()
                pIndentToolButton.IconWidget = lImg
            Catch ex As Exception
                pIndentToolButton.IconWidget = Image.NewFromIconName("format-indent-more", lIconSize)
            End Try
            pIndentToolButton.TooltipText = "Indent (Ctrl+] or Tab when Text is selected)"
            AddHandler pIndentToolButton.Clicked, AddressOf OnIndent
            pIndentToolButton.Sensitive = True
            pToolbar.Insert(pIndentToolButton, -1)
    
            ' Toggle Comment button
            pToggleCommentButton = New ToolButton(Nothing, "Toggle Comment")
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.comment.png", lIconSize)
                lImg.Show()
                pToggleCommentButton.IconWidget = lImg
            Catch ex As Exception
                pToggleCommentButton.IconWidget = Image.NewFromIconName("format-text-bold", lIconSize)
            End Try
            pToggleCommentButton.TooltipText = "Toggle Comment Block (Ctrl+')"
            AddHandler pToggleCommentButton.Clicked, AddressOf OnToggleComment
            pToolbar.Insert(pToggleCommentButton, -1)

            pToolbar.Insert(New SeparatorToolItem(), -1)

            ' Build operations
            pBuildButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build_start.png", lIconSize)
                lImg.Show()
                pBuildButton.IconWidget = lImg
            Catch ex As Exception
                pBuildButton.IconWidget = Image.NewFromIconName("media-eject", lIconSize)
            End Try            
            pBuildButton.Label = "Build"
            pBuildButton.TooltipText = "Build project (F6)"
            AddHandler pBuildButton.Clicked, AddressOf OnBuildProject
            pToolbar.Insert(pBuildButton, -1)
            
            pRunButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build-run.png", lIconSize)
                lImg.Show()
                pRunButton.IconWidget = lImg
            Catch ex As Exception
                pRunButton.IconWidget = Image.NewFromIconName("media-playback-start", lIconSize)
            End Try            
            pRunButton.Label = "Run"
            pRunButton.TooltipText = "Run project (F5)"
            AddHandler pRunButton.Clicked, AddressOf OnRunProject
            pToolbar.Insert(pRunButton, -1)
            
            pStopButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build-stop.png", lIconSize)
                lImg.Show()
                pStopButton.IconWidget = lImg
            Catch ex As Exception
                pStopButton.IconWidget = Image.NewFromIconName("media-playback-stop", lIconSize)
            End Try            
            pStopButton.Label = "Stop"
            pStopButton.TooltipText = "Stop Debugging"
            AddHandler pStopButton.Clicked, AddressOf OnStopDebugging
            pToolbar.Insert(pStopButton, -1)
            

            pOutputPanelToggleButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.bottom.png", lIconSize)
                lImg.Show()
                pOutputPanelToggleButton.IconWidget = lImg
            Catch ex As Exception
                pOutputPanelToggleButton.IconWidget = Image.NewFromIconName("media-playback-stop", lIconSize)
            End Try            
            pOutputPanelToggleButton.Label = "Stop"
            pOutputPanelToggleButton.TooltipText = "Stop Debugging"
            AddHandler pOutputPanelToggleButton.Clicked, AddressOf ToggleBottomPanel
            pToolbar.Insert(pOutputPanelToggleButton, -1)

            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Git
            pGitButton = New ToolButton(Nothing, Nothing)
            Try
                Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.git.png", lIconSize)
                lImg.Show()
                pGitButton.IconWidget = lImg
            Catch ex As Exception
                pGitButton.IconWidget = Image.NewFromIconName("git", lIconSize)
            End Try            
            pGitButton.Label = "git"
            pGitButton.TooltipText = "git Status"
            AddHandler pGitButton.Clicked, AddressOf OnShowGitPanel
            pToolbar.Insert(pGitButton, -1)
            
            ' AI Assistant
            pAIButton = New ToolButton(Nothing, Nothing)
            Dim lAIIcon As New Image()
            lAIIcon.SetFromIconName("starred-symbolic", lIconSize)
            lAIIcon.Show()
            pAIButton.IconWidget = lAIIcon
            pAIButton.Label = "AI"
            pAIButton.TooltipText = "AI Assistant"
            AddHandler pAIButton.Clicked, AddressOf OnShowAIAssistant
            pToolbar.Insert(pAIButton, -1)
            
            ' Help
            pHelpButton = New ToolButton(Nothing, Nothing)
            Dim lHelpIcon As New Image()
            lHelpIcon.SetFromIconName("help-browser", lIconSize)
            lHelpIcon.Show()
            pHelpButton.IconWidget = lHelpIcon
            pHelpButton.Label = "Help"
            pHelpButton.TooltipText = "Help (F1)"
            AddHandler pHelpButton.Clicked, AddressOf OnShowHelpPanel
            pToolbar.Insert(pHelpButton, -1)

            CreateScratchpadToolbarButton()

            ' Force show all toolbar items
            For Each lItem As Widget In pToolbar.Children
                lItem.Show()
            Next
    
            pToolbar.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"CreateToolbar error: {ex.Message}")
        End Try
    End Sub

    Public Function GetEmbeddedIcon(vResourceName As String, vIconSize As IconSize) As Gtk.Image
        Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream(vResourceName)
            If lStream IsNot Nothing Then
                Dim lPB As New Gdk.Pixbuf(lStream)
                Dim lImg As New Gtk.Image(lPB, vIconSize)
                lImg.Show()
                Return lImg
            Else
                Throw New Exception("")
            End If
        End Using
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
    
    Private Sub OnShowHelpPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Show bottom panel with Help tab
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eHelpViewer)
            Else
                ' Fallback to old method
                ShowBottomPanel(4) ' Help tab
            End If
            
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
            
            ' Find
            pFindButton.Sensitive = True ' Always enabled
            
            ' Build operations
            Dim lHasProject As Boolean = Not String.IsNullOrEmpty(pCurrentProject)
            pBuildButton.Sensitive = lHasProject
            pRunButton.Sensitive = lHasProject
            pStopButton.Sensitive = pIsDebugging
            
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
            
            ' Apply icon size and style
            Dim lIconSize As IconSize
            Dim lToolbarStyle As ToolbarStyle
            
            If pSettingsManager.ToolbarLargeIcons Then
                lIconSize = IconSize.LargeToolbar
            Else
                lIconSize = IconSize.SmallToolbar
            End If
            
            If pSettingsManager.ToolbarShowLabels Then
                lToolbarStyle = ToolbarStyle.Both
            Else
                lToolbarStyle = ToolbarStyle.Icons
            End If
            
            ' Apply settings to toolbar
            pToolbar.IconSize = lIconSize
            pToolbar.ToolbarStyle = lToolbarStyle
            
            ' Update all icon widgets with new size
            UpdateToolbarIcons(lIconSize)
            
            ' Force redraw
            pToolbar.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"ApplyToolbarSettings error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub UpdateToolbarIcons(vIconSize As IconSize)
        Try
            ' Update each button's icon with the new size
            If pNewButton IsNot Nothing AndAlso pNewButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.New.png", vIconSize)
                    lImg.Show()
                    pNewButton.IconWidget = lImg
                Catch ex As Exception
                    pNewButton.IconWidget = Image.NewFromIconName("document-New", vIconSize)
                End Try
            End If
            
            If pOpenButton IsNot Nothing AndAlso pOpenButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.open.png", vIconSize)
                    lImg.Show()
                    pOpenButton.IconWidget = lImg
                Catch ex As Exception
                    pOpenButton.IconWidget = Image.NewFromIconName("document-open", vIconSize)
                End Try
            End If
            
            If pSaveButton IsNot Nothing AndAlso pSaveButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.disc.png", vIconSize)
                    lImg.Show()
                    pSaveButton.IconWidget = lImg
                Catch ex As Exception
                    pSaveButton.IconWidget = Image.NewFromIconName("document-Save", vIconSize)
                End Try
            End If
            
            If pSaveAllButton IsNot Nothing AndAlso pSaveAllButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.saveall.png", vIconSize)
                    lImg.Show()
                    pSaveAllButton.IconWidget = lImg
                Catch ex As Exception
                    pSaveAllButton.IconWidget = Image.NewFromIconName("document-saveall", vIconSize)
                End Try
            End If
            
            If pUndoButton IsNot Nothing AndAlso pUndoButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Undo.png", vIconSize)
                    lImg.Show()
                    pUndoButton.IconWidget = lImg
                Catch ex As Exception
                    pUndoButton.IconWidget = Image.NewFromIconName("edit-Undo", vIconSize)
                End Try
            End If
            
            If pRedoButton IsNot Nothing AndAlso pRedoButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Redo.png", vIconSize)
                    lImg.Show()
                    pRedoButton.IconWidget = lImg
                Catch ex As Exception
                    pRedoButton.IconWidget = Image.NewFromIconName("edit-Redo", vIconSize)
                End Try
            End If
            
            If pCutButton IsNot Nothing AndAlso pCutButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Cut.png", vIconSize)
                    lImg.Show()
                    pCutButton.IconWidget = lImg
                Catch ex As Exception
                    pCutButton.IconWidget = Image.NewFromIconName("edit-Cut", vIconSize)
                End Try
            End If
            
            If pCopyButton IsNot Nothing AndAlso pCopyButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Copy.png", vIconSize)
                    lImg.Show()
                    pCopyButton.IconWidget = lImg
                Catch ex As Exception
                    pCopyButton.IconWidget = Image.NewFromIconName("edit-Copy", vIconSize)
                End Try
            End If
            
            If pPasteButton IsNot Nothing AndAlso pPasteButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Paste.png", vIconSize)
                    lImg.Show()
                    pPasteButton.IconWidget = lImg
                Catch ex As Exception
                    pPasteButton.IconWidget = Image.NewFromIconName("edit-Paste", vIconSize)
                End Try
            End If

            If pToggleCommentButton IsNot Nothing AndAlso pToggleCommentButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.comment.png", vIconSize)
                    lImg.Show()
                    pToggleCommentButton.IconWidget = lImg
                Catch ex As Exception
                    pToggleCommentButton.IconWidget = Image.NewFromIconName("format-text-bold", vIconSize)
                End Try
            End If

            If pOutdentToolButton IsNot Nothing AndAlso pOutdentToolButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.outdent.png", vIconSize)
                    lImg.Show()
                    pOutdentToolButton.IconWidget = lImg
                Catch ex As Exception
                    pOutdentToolButton.IconWidget = Image.NewFromIconName("format-indent-less", vIconSize)
                End Try
            End If

            If pIndentToolButton IsNot Nothing AndAlso pIndentToolButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.indent.png", vIconSize)
                    lImg.Show()
                    pIndentToolButton.IconWidget = lImg
                Catch ex As Exception
                    pIndentToolButton.IconWidget = Image.NewFromIconName("format-indent-more", vIconSize)
                End Try
            End If

            If pToggleCommentButton IsNot Nothing AndAlso pToggleCommentButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.comment.png", vIconSize)
                    lImg.Show()
                    pToggleCommentButton.IconWidget = lImg
                Catch ex As Exception
                    pToggleCommentButton.IconWidget = Image.NewFromIconName("format-text-bold", vIconSize)
                End Try
            End If

            If pFindButton IsNot Nothing AndAlso pFindButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.Find.png", vIconSize)
                    lImg.Show()
                    pFindButton.IconWidget = lImg
                Catch ex As Exception
                    pFindButton.IconWidget = Image.NewFromIconName("edit-Find", vIconSize)
                End Try
            End If
            
            If pBuildButton IsNot Nothing AndAlso pBuildButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build.png", vIconSize)
                    lImg.Show()
                    pBuildButton.IconWidget = lImg
                Catch ex As Exception
                    pBuildButton.IconWidget = Image.NewFromIconName("system-run", vIconSize)
                End Try            
            End If
            
            If pRunButton IsNot Nothing AndAlso pRunButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build-run.png", vIconSize)
                    lImg.Show()
                    pRunButton.IconWidget = lImg
                Catch ex As Exception
                    pRunButton.IconWidget = Image.NewFromIconName("media-playback-start", vIconSize)
                End Try            
            End If
            
            If pStopButton IsNot Nothing AndAlso pStopButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.build-stop.png", vIconSize)
                    lImg.Show()
                    pStopButton.IconWidget = lImg
                Catch ex As Exception
                    pStopButton.IconWidget = Image.NewFromIconName("media-playback-stop", vIconSize)
                End Try            
            End If

            If pOutputPanelToggleButton IsNot Nothing AndAlso pOutputPanelToggleButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.bottom.png", vIconSize)
                    lImg.Show()
                    pOutputPanelToggleButton.IconWidget = lImg
                Catch ex As Exception
                    pOutputPanelToggleButton.IconWidget = Image.NewFromIconName("view-paged", vIconSize)
                End Try            
            End If

            
            If pGitButton IsNot Nothing AndAlso pGitButton.IconWidget IsNot Nothing Then
                Try
                    Dim lImg As Gtk.Image = GetEmbeddedIcon( "SimpleIDE.git.png", vIconSize)
                    lImg.Show()
                    pGitButton.IconWidget = lImg
                Catch ex As Exception
                    pGitButton.IconWidget = Image.NewFromIconName("git", vIconSize)
                End Try            
            End If
            
            If pAIButton IsNot Nothing AndAlso pAIButton.IconWidget IsNot Nothing Then
                DirectCast(pAIButton.IconWidget, Image).SetFromIconName("starred-symbolic", vIconSize)
            End If
            
            If pHelpButton IsNot Nothing AndAlso pHelpButton.IconWidget IsNot Nothing Then
                DirectCast(pHelpButton.IconWidget, Image).SetFromIconName("help-browser", vIconSize)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateToolbarIcons error: {ex.Message}")
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
        End Try
    End Sub

End Class