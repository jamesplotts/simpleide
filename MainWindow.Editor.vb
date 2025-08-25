' MainWindow.Editor.vb - Editor tab management for SimpleIDE
' Uses TabInfo from Models/TabInfo.vb - NO NESTED CLASS

Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Widgets

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields =====
    ' Note: pOpenTabs is already declared in MainWindow.vb as Dictionary(Of String, TabInfo)
    Private pNextUntitledNumber As Integer = 1
    Private pSyntaxColorSet As SyntaxColorSet
    
    ' ===== Public Methods =====
    
    Public Sub OpenFile(vFilePath As String)
        Try
            ' Check if file is already open
            If pOpenTabs.ContainsKey(vFilePath) Then
                SwitchToTab(vFilePath)
                Return
            End If
            
            ' Read file content
            Dim lContent As String = File.ReadAllText(vFilePath)
            
            ' Create SourceFileInfo with proper initialization
            Dim lSourceFileInfo As New SourceFileInfo(vFilePath, "")
            
            ' CRITICAL FIX: Properly initialize the content and TextLines
            lSourceFileInfo.Content = lContent
            lSourceFileInfo.TextLines = New List(Of String)(lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
            If lSourceFileInfo.TextLines.Count = 0 Then
                lSourceFileInfo.TextLines.Add("")
            End If
            lSourceFileInfo.IsLoaded = True
            
            ' Create new tab
            CreateNewTab(vFilePath, lSourceFileInfo, True)
            
        Catch ex As Exception
            Console.WriteLine($"OpenFile error: {ex.Message}")
            ShowError("Open File Error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnNewFile(vSender As Object, vArgs As EventArgs)
        Try
            Dim lFileName As String = GetNextUntitledFileName()
            Dim lSourceFileInfo As New SourceFileInfo(lFileName, "")
            CreateNewTab(lFileName, lSourceFileInfo, False)
            
        Catch ex As Exception
            Console.WriteLine($"OnNewFile error: {ex.Message}")
            ShowError("New File Error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnOpenFile(vSender As Object, vArgs As EventArgs)
        Try
            Dim lDialog As FileChooserDialog = FileOperations.CreateOpenFileDialog(Me)
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                OpenFile(lDialog.Filename)
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnOpenFile error: {ex.Message}")
            ShowError("Open File Error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnSaveFile(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                SaveFile(lCurrentTab)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnSaveFile error: {ex.Message}")
            ShowError("Save File Error", ex.Message)
        End Try
    End Sub

    Public Sub OnFindPrevious(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                lCurrentTab.Editor.FindPrevious()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnFindPrevious error: {ex.Message}")
            ShowError("OnFindPrevious Error", ex.Message)
        End Try
    End Sub

    Public Sub OnFindNext(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                lCurrentTab.Editor.FindNext()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnFindNext error: {ex.Message}")
            ShowError("OnFindNext Error", ex.Message)
        End Try
    End Sub

    Public Sub OnSaveAs()
        Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
        If lCurrentTab IsNot Nothing Then
            SaveFileAs(lCurrentTab)
        End If
    End Sub 

    Public Sub OnSaveFileAs(vSender As Object, vArgs As EventArgs)
        Try
            OnSaveAs()
        Catch ex As Exception
            Console.WriteLine($"OnSaveFileAs error: {ex.Message}")
            ShowError("Save File As Error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnSaveAllFiles(vSender As Object, vArgs As EventArgs)
        Try
            SaveAllFiles()
        Catch ex As Exception
            Console.WriteLine($"OnSaveAllFiles error: {ex.Message}")
            ShowError("Save All Files Error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnCloseFile(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                CloseTab(lCurrentTab)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCloseFile error: {ex.Message}")
            ShowError("Close File Error", ex.Message)
        End Try
    End Sub
    
    ' Get current tab info
    Public Function GetCurrentTabInfo() As TabInfo
        Try
            If pNotebook.CurrentPage >= 0 Then
                Dim lCurrentPage As Widget = pNotebook.GetNthPage(pNotebook.CurrentPage)
                
                for each lTabEntry in pOpenTabs
                    If lTabEntry.Value.EditorContainer Is lCurrentPage Then
                        Return lTabEntry.Value
                    End If
                Next
            End If
            
            Return Nothing
            
        Catch ex As Exception
            Console.WriteLine($"GetCurrentTabInfo error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ' Get current editor interface
    Public Function GetCurrentEditor() As IEditor
        Try
            Dim lTabInfo As TabInfo = GetCurrentTabInfo()
            Return lTabInfo?.Editor
            
        Catch ex As Exception
            Console.WriteLine($"GetCurrentEditor error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ' Switch to specific tab by file path
    Public Sub SwitchToTab(vFilePath As String)
        Try
            If Not pOpenTabs.ContainsKey(vFilePath) Then
                Console.WriteLine($"SwitchToTab: Tab not found for: {vFilePath}")
                Return
            End If
            
            Dim lTabInfo As TabInfo = pOpenTabs(vFilePath)
            
            ' Find the page index
            for i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.CurrentPage = i
                    
                    ' Focus editor
                    If lTabInfo.Editor?.Widget IsNot Nothing Then
                        lTabInfo.Editor.Widget.GrabFocus()
                    End If
                    
                    Exit for
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToTab error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Tab Management Methods =====
    
    Private Sub CreateNewTab(vFilePath As String, vSourceFileInfo As SourceFileInfo, vIsExistingFile As Boolean)
        Try
            ' Close welcome tab if it exists
            CloseWelcomeTab()
            
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = vFilePath
            lTabInfo.Modified = Not vIsExistingFile
            lTabInfo.IsProjectFile = IsProjectFile(vFilePath)
            lTabInfo.IsPngFile = System.IO.Path.GetExtension(vFilePath).ToLower() = ".png"
            
            ' Set LastSaved timestamp
            If vIsExistingFile AndAlso File.Exists(vFilePath) Then
                lTabInfo.LastSaved = File.GetLastWriteTime(vFilePath)
            Else
                lTabInfo.LastSaved = DateTime.Now
            End If
            
            ' Create editor based on file type
            If lTabInfo.IsPngFile Then
                ' Create simple label for PNG files - PngEditor is too complex for now
                Dim lImageLabel As New Label($"PNG Image: {System.IO.Path.GetFileName(vFilePath)}")
                lImageLabel.Halign = Align.Center
                lImageLabel.Valign = Align.Center
                lTabInfo.Editor = Nothing ' No IEditor for images
                lTabInfo.EditorContainer = lImageLabel
            Else
                ' Create code editor - CustomDrawingEditor requires SourceFileInfo parameter
                Dim lEditor As New CustomDrawingEditor(vSourceFileInfo)

                ' CRITICAL: Set the theme manager to avoid null reference errors
                If pThemeManager IsNot Nothing Then
                    lEditor.SetThemeManager(pThemeManager)
                End If

                ' CRITICAL FIX: Call RefreshFromSourceFileInfo to ensure editor displays content
                lEditor.RefreshFromSourceFileInfo()

                ' CRITICAL: Show the editor widget to ensure it's visible
                lEditor.ShowAll()
                
                ' Connect editor events
                AddHandler lEditor.TextChanged, AddressOf OnEditorTextChanged

                AddHandler lEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
                AddHandler lEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
                
                lTabInfo.Editor = lEditor
                lTabInfo.EditorContainer = lEditor
            End If
            
            ' Create tab label
            lTabInfo.TabLabel = CreateTabLabel(vFilePath, lTabInfo.Modified)
            
            ' Add to notebook
            pNotebook.AppendPage(lTabInfo.EditorContainer, lTabInfo.TabLabel)
            pNotebook.ShowAll()
            
            ' Add to open tabs dictionary
            pOpenTabs(vFilePath) = lTabInfo
            
            ' Switch to new tab
            pNotebook.CurrentPage = pNotebook.NPages - 1
            
            ' Focus editor
            If lTabInfo.Editor?.Widget IsNot Nothing Then
                lTabInfo.Editor.Widget.GrabFocus()
            End If
            
            ' Update UI
            UpdateWindowTitle()
            UpdateStatusBar("")
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"CreateNewTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Close a specific tab with proper cleanup and SourceFileInfo reload
    ''' </summary>
    Private Sub CloseTab(vTabInfo As TabInfo)
        Try
            If vTabInfo Is Nothing Then Return
            
            ' Check for unsaved changes
            If vTabInfo.Modified Then
                Dim lDialog As New MessageDialog(
                    Me,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.None,
                    $"The file '{vTabInfo.Editor.DisplayName}' has unsaved changes.{Environment.NewLine}Do you want to save before closing?"
                )
                
                lDialog.AddButton("Save", ResponseType.Yes)
                lDialog.AddButton("Don't Save", ResponseType.No)
                lDialog.AddButton("Cancel", ResponseType.Cancel)
                
                Dim lResponse As Integer = lDialog.Run()
                lDialog.Destroy()
                
                Select Case lResponse
                    Case CInt(ResponseType.Yes)
                        ' Save the file
                        If Not SaveFile(vTabInfo) Then
                            Return ' Cancel close if save fails
                        End If
                        
                    Case CInt(ResponseType.No)
                        ' CRITICAL FIX: Reload SourceFileInfo from disk when discarding changes
                        ' This prevents the in-memory SourceFileInfo from retaining modified content
                        If pProjectManager IsNot Nothing AndAlso Not String.IsNullOrEmpty(vTabInfo.FilePath) Then
                            Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(vTabInfo.FilePath)
                            If lSourceFileInfo IsNot Nothing Then
                                Console.WriteLine($"Discarding changes - reloading {vTabInfo.FilePath} from disk")
                                
                                ' Reload content from disk to discard all in-memory changes
                                If System.IO.File.Exists(vTabInfo.FilePath) Then
                                    lSourceFileInfo.LoadContent()
                                    ' Mark as needing re-parsing since we reloaded
                                    lSourceFileInfo.NeedsParsing = True
                                    lSourceFileInfo.IsParsed = False
                                End If
                            End If
                        End If
                        
                    Case CInt(ResponseType.Cancel)
                        Return ' Cancel the close operation
                End Select
            End If
    
            ' When closing a tab
            UnhookEditorEventsForObjectExplorer(vTabInfo.Editor)
            UpdateObjectExplorerForActiveTab()
            
            ' Find and remove the page
            for i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is vTabInfo.EditorContainer Then
                    pNotebook.RemovePage(i)
                    Exit for
                End If
            Next
            
            ' Remove from dictionary
            pOpenTabs.Remove(vTabInfo.FilePath)
            
            ' Dispose tab
            vTabInfo.Dispose()
    
            ' Update UI
            UpdateWindowTitle()
            UpdateStatusBar("")
            
            ' Show welcome screen if no tabs left
            If pNotebook.NPages = 0 Then
                ShowWelcomeTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CloseTab error: {ex.Message}")
        End Try
    End Sub

    
    Private Sub CloseAllTabs()
        Try
            ' Create a copy of the collection to avoid modification during iteration
            Dim lTabsToClose As New List(Of TabInfo)(pOpenTabs.Values)
            
            for each lTabInfo in lTabsToClose
                CloseTab(lTabInfo)
            Next
            
        Catch ex As Exception
            Console.WriteLine($"CloseAllTabs error: {ex.Message}")
        End Try
    End Sub

    
    ' ===== File Operations =====
    
'    Private Function SaveFile(vTabInfo As TabInfo) As Boolean
'        Try
'            ' Check if we need to save as
'            If String.IsNullOrEmpty(vTabInfo.FilePath) OrElse vTabInfo.FilePath.StartsWith("Untitled") Then
'                Return SaveFileAs(vTabInfo)
'            End If
'            
'            ' Save to file
'            If vTabInfo.Editor IsNot Nothing Then
'                ' Get the current text content
'                Dim lContent As String = vTabInfo.Editor.Text
'                
'                ' Save to file
'                File.WriteAllText(vTabInfo.FilePath, lContent)
'                
'                ' Update state
'                vTabInfo.Modified = False
'                vTabInfo.LastSaved = DateTime.Now
'                UpdateTabLabel(vTabInfo)
'                
'                ' Update UI
'                UpdateStatusBar("")
'                UpdateWindowTitle()
'                
'                ' Add to recent files
'                If pSettingsManager IsNot Nothing Then
'                    pSettingsManager.AddRecentFile(vTabInfo.FilePath)
'                End If
'                
'                Console.WriteLine($"Saved file: {vTabInfo.FilePath}")
'                Return True
'            End If
'            
'            Return False
'            
'        Catch ex As Exception
'            Console.WriteLine($"SaveFile error: {ex.Message}")
'            ShowError("Save error", ex.Message)
'            Return False
'        End Try
'    End Function
    
    Private Function SaveFileAs(vTabInfo As TabInfo) As Boolean
        Try
            ' Use the existing method from FileOperations
            Dim lDialog As FileChooserDialog = FileOperations.CreateSaveAsDialog(Me)
            
            ' Set initial filename
            If Not String.IsNullOrEmpty(vTabInfo.FilePath) Then
                lDialog.CurrentName = System.IO.Path.GetFileName(vTabInfo.FilePath)
            End If
            
            Dim lResult As Boolean = False
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                ' Store old path
                Dim lOldPath As String = vTabInfo.FilePath
                
                ' Update file path
                vTabInfo.FilePath = lDialog.Filename
                
                ' Save file
                If SaveFile(vTabInfo) Then
                    ' Update open tabs dictionary
                    If lOldPath <> vTabInfo.FilePath Then
                        pOpenTabs.Remove(lOldPath)
                        pOpenTabs(vTabInfo.FilePath) = vTabInfo
                    End If
                    
                    ' Update UI
                    UpdateWindowTitle()
                    
                    lResult = True
                Else
                    ' Restore old path on failure
                    vTabInfo.FilePath = lOldPath
                End If
            End If
            
            lDialog.Destroy()
            Return lResult
            
        Catch ex As Exception
            Console.WriteLine($"SaveFileAs error: {ex.Message}")
            ShowError("Save As error", ex.Message)
            Return False
        End Try
    End Function
    
    Private Function SaveAllFiles() As Boolean
        Try
            Dim lSuccess As Boolean = True
            
            for each lKvp in pOpenTabs
                Dim lTabInfo As TabInfo = lKvp.Value
                If lTabInfo.Modified AndAlso lTabInfo.Editor IsNot Nothing Then
                    If Not SaveFile(lTabInfo) Then
                        lSuccess = False
                    End If
                End If
            Next
            
            Return lSuccess
            
        Catch ex As Exception
            Console.WriteLine($"SaveAllFiles error: {ex.Message}")
            ShowError("Save All error", ex.Message)
            Return False
        End Try
    End Function
    
    Public Async Sub ReLoadFile(vFilePath As String)
        Try
            If Not pOpenTabs.ContainsKey(vFilePath) Then Return
            
            Dim lTabInfo As TabInfo = pOpenTabs(vFilePath)
            If lTabInfo?.Editor Is Nothing Then Return
            
            ' Read file content
            Dim lContent As String = Await Task.Run(Function() File.ReadAllText(vFilePath))
            
            ' Update editor
            lTabInfo.Editor.Text = lContent
            lTabInfo.Modified = False
            UpdateTabLabel(lTabInfo)
            
        Catch ex As Exception
            Console.WriteLine($"ReLoadFile error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    Private Function GetNextUntitledFileName() As String
        Dim lIndex As Integer = 1
        Dim lFileName As String
        
        Do
            lFileName = $"Untitled{lIndex}.vb"
            If Not pOpenTabs.ContainsKey(lFileName) Then
                Return lFileName
            End If
            lIndex += 1
        Loop
    End Function
    
    Private Function CreateTabLabel(vFilePath As String, vModified As Boolean) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' File name label
            Dim lLabel As New Label()
            Dim lFileName As String = System.IO.Path.GetFileName(vFilePath)
            If vModified Then
                lLabel.Text = $"*{lFileName}"
            Else
                lLabel.Text = lFileName
            End If
            lBox.PackStart(lLabel, True, True, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.FocusOnClick = False
            
            Dim lCloseIcon As New Image()
            lCloseIcon.IconName = "window-close"
            lCloseIcon.IconSize = CInt(IconSize.Menu)
            lCloseButton.Add(lCloseIcon)
            
            ' Connect close event
            Dim lFilePath As String = vFilePath
            AddHandler lCloseButton.Clicked, Sub(sender, e)
                ' Close the tab for this specific file
                If pOpenTabs.ContainsKey(lFilePath) Then
                    CloseTab(pOpenTabs(lFilePath))
                End If
            End Sub
            
            lBox.PackStart(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateTabLabel error: {ex.Message}")
            Return New Label(System.IO.Path.GetFileName(vFilePath))
        End Try
    End Function
    
    Private Function IsProjectFile(vFilePath As String) As Boolean
        If String.IsNullOrEmpty(pCurrentProject) Then Return False
        
        Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
        Return vFilePath.StartsWith(lProjectDir)
    End Function
    
    Private Function IsWelcomeTab(vPageIndex As Integer) As Boolean
        Try
            If vPageIndex < 0 OrElse vPageIndex >= pNotebook.NPages Then
                Return False
            End If
            
            Dim lTabLabel As Widget = pNotebook.GetTabLabel(pNotebook.GetNthPage(vPageIndex))
            If TypeOf lTabLabel Is Label Then
                Return DirectCast(lTabLabel, Label).Text = "Welcome"
            End If
            
            Return False
        Catch ex As Exception
            Console.WriteLine($"IsWelcomeTab error: {ex.Message}")
            Return False
        End Try
    End Function
    
    Private Sub CloseWelcomeTab()
        Try
            ' Find and close welcome tab
            for i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    pNotebook.RemovePage(i)
                    Exit for
                End If
            Next
        Catch ex As Exception
            Console.WriteLine($"CloseWelcomeTab error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub MarkTabModified(vEditor As IEditor)
        Try
            ' Find tab containing this editor
            for each lTabEntry in pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                If lTabInfo.Editor Is vEditor AndAlso Not lTabInfo.Modified Then
                    lTabInfo.Modified = True
                    UpdateTabLabel(lTabInfo)
                    UpdateWindowTitle()
                    Exit for
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"MarkTabModified error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub UpdateTabLabel(vTabInfo As TabInfo)
        Try
            ' Find the label in the tab
            If vTabInfo.TabLabel.GetType() Is GetType(Box) Then
                Dim lBox As Box = CType(vTabInfo.TabLabel, Box)
                for each lChild As Widget in lBox.Children
                    If lChild.GetType() Is GetType(Label) Then
                        Dim lLabel As Label = CType(lChild, Label)
                        Dim lFileName As String = System.IO.Path.GetFileName(vTabInfo.FilePath)
                        If vTabInfo.Modified Then
                            lLabel.Text = $"*{lFileName}"
                        Else
                            lLabel.Text = lFileName
                        End If
                        Exit for
                    End If
                Next
            ElseIf vTabInfo.TabLabel.GetType() Is GetType(Label) Then
                ' Fallback for simple label tabs
                Dim lLabel As Label = CType(vTabInfo.TabLabel, Label)
                Dim lFileName As String = System.IO.Path.GetFileName(vTabInfo.FilePath)
                If vTabInfo.Modified Then
                    lLabel.Text = $"*{lFileName}"
                Else
                    lLabel.Text = lFileName
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateTabLabel error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Get TabInfo for a specific notebook page by index
    ''' </summary>
    Private Function GetTabInfo(vPageIndex As Integer) As TabInfo
        Try
            ' Validate index
            If vPageIndex < 0 OrElse vPageIndex >= pNotebook.NPages Then
                Return Nothing
            End If
            
            ' Get the widget at the specified index
            Dim lPage As Widget = pNotebook.GetNthPage(vPageIndex)
            If lPage Is Nothing Then Return Nothing
            
            ' Find the TabInfo that matches this page
            for each lTabEntry in pOpenTabs
                If lTabEntry.Value.EditorContainer Is lPage Then
                    Return lTabEntry.Value
                End If
            Next
            
            ' No matching TabInfo found
            Return Nothing
            
        Catch ex As Exception
            Console.WriteLine($"GetTabInfo error: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ' Method to unhook events when closing tabs
    Private Sub UnhookEditorEventsForObjectExplorer(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Unhook standard events
            RemoveHandler vEditor.Modified, AddressOf OnEditorModified
            RemoveHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            RemoveHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            RemoveHandler vEditor.TextChanged, AddressOf OnEditorTextChangedEnhanced
            RemoveHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Unhook Object Explorer events
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
        Catch ex As Exception
            Console.WriteLine($"UnhookEditorEventsForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Editor Event Handlers =====
    
    Private Sub OnEditorTextChanged(vSender As Object, vArgs As EventArgs)
        Try
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor IsNot Nothing Then
                MarkTabModified(lEditor)
                OnEditorTextChangedEnhanced(vSender, vArgs)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnEditorTextChanged error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnEditorUndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        Try
            ' Update toolbar buttons
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorUndoRedoStateChanged error: {ex.Message}")
        End Try
    End Sub

    
    Private Sub OnEditorCursorPositionChanged(vLine As Integer, vColumn As Integer)
        Try
            UpdateStatusBar("")
        Catch ex As Exception
            Console.WriteLine($"OnEditorCursorPositionChanged error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnEditorSelectionChanged(vHasSelection As Boolean)
        Try
            ' Update toolbar buttons when selection changes
            UpdateToolbarButtons()
        Catch ex As Exception
            Console.WriteLine($"OnEditorSelectionChanged error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnNotebookSwitchPage(vSender As Object, vArgs As SwitchPageArgs)
        Try
            ' Update UI for the newly selected tab
            UpdateWindowTitle()
            UpdateStatusBar("")
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPage error: {ex.Message}")
        End Try
    End Sub

    ' Missing event handlers that may be referenced
    Private Sub OnEditorModified(vIsModified As Boolean)
        Try
            ' Find the editor that sent this event and mark tab as modified
            UpdateStatusBar("")
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorModified error: {ex.Message}")
        End Try
    End Sub

    ' Enhanced text changed handler with Object Explorer considerations
    Public Sub OnEditorTextChangedEnhanced(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the editor that changed
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Update modified state in UI
            UpdateTabModifiedState(lEditor)
            
            ' Update status bar if this is the current editor
            If lEditor Is GetCurrentEditor() Then
                UpdateStatusBar()
            End If
            
            ' Mark project as dirty if needed
            If pProjectManager IsNot Nothing Then
                pProjectManager.MarkDirty()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorTextChanged error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Settings and Initialization =====
    
    Private Sub ApplySettings()
        Try
            ' Apply settings to all open editors
            for each lTabEntry in pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                If TypeOf lTabInfo.Editor Is CustomDrawingEditor Then
                    Dim lEditor As CustomDrawingEditor = DirectCast(lTabInfo.Editor, CustomDrawingEditor)
                    ' CustomDrawingEditor doesn't have UpdateSettings method
                    ' Would need to update individual settings if they existed
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"ApplySettings error: {ex.Message}")
        End Try
    End Sub
        
    ''' <summary>
    ''' Handles the Cut Line command (Ctrl+Y) - Traditional VB.NET behavior
    ''' </summary>
    ''' <param name="vSender">Event sender (unused)</param>
    ''' <param name="vArgs">Event arguments (unused)</param>
    Public Sub OnCutLine(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the current editor
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                Console.WriteLine("OnCutLine: No active editor")
                Return
            End If
            
            ' Check if it's a CustomDrawingEditor (which has the CutLine method)
            Dim lCustomEditor As CustomDrawingEditor = TryCast(lEditor, CustomDrawingEditor)
            If lCustomEditor IsNot Nothing Then
                ' Call the CutLine method on the editor
                lCustomEditor.CutLine()
                
                ' Update toolbar button states
                UpdateToolbarButtons()
                
                ' Show status
                UpdateStatusBar("Line cut to clipboard")
            Else
                ' For other editor types, try to implement basic cut line functionality
                ' using the IEditor interface methods
                
                ' Get current line position
                Dim lPosition As EditorPosition = lEditor.GetCursorPosition()
                Dim lCurrentLine As Integer = lPosition.Line  ' 0-based
                
                ' Select the entire line
                lEditor.SelectLine(lCurrentLine + 1)  ' SelectLine expects 1-based
                
                ' Cut the selected text
                lEditor.Cut()
                
                ' Update toolbar and status
                UpdateToolbarButtons()
                UpdateStatusBar("Line cut to clipboard")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCutLine error: {ex.Message}")
            ShowError("Cut Line Error", $"Failed to cut line: {ex.Message}")
        End Try
    End Sub
       
         
        
End Class
