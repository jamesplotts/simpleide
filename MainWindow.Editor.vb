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
    
    ''' <summary>
    ''' Opens a file with full project and Object Explorer integrationas
    ' Replace: SimpleIDE.MainWindow.OpenFile
    Public Sub OpenFile(vFilePath As String)
        Try
            Console.WriteLine($"===== OpenFile called with: {vFilePath} =====")
            Console.WriteLine($"  pOpenTabs.Count = {pOpenTabs.Count}")
            
            ' Debug: List all open tabs
            for each lKey in pOpenTabs.Keys
                Console.WriteLine($"  Open tab: {lKey}")
            Next
            
            ' Check if file is already open
            If pOpenTabs.ContainsKey(vFilePath) Then
                Console.WriteLine($"  File IS in pOpenTabs, switching to existing tab")
                SwitchToTab(vFilePath)
                Return
            End If
            
            Console.WriteLine($"  File NOT in pOpenTabs, creating new tab")
            Console.WriteLine($"OpenFile: Opening {vFilePath}")
            
            ' Get or create SourceFileInfo through ProjectManager
            Dim lSourceFileInfo As SourceFileInfo = Nothing
            Dim lIsNewFile As Boolean = False
            
            If pProjectManager IsNot Nothing Then
                lSourceFileInfo = pProjectManager.GetSourceInfo(vFilePath)
                If lSourceFileInfo Is Nothing Then
                    Console.WriteLine($"OpenFile: Creating new SourceFileInfo for {vFilePath}")
                    lSourceFileInfo = pProjectManager.CreateEmptyFile(vFilePath)
                    lIsNewFile = True
                Else
                    Console.WriteLine($"OpenFile: Found existing SourceFileInfo for {vFilePath}")
                End If
            Else
                Console.WriteLine($"OpenFile: WARNING - No ProjectManager, creating standalone SourceFileInfo")
                lSourceFileInfo = New SourceFileInfo(vFilePath, "")
                lIsNewFile = True
            End If
            
            ' Ensure content is loaded
            If Not lSourceFileInfo.IsLoaded Then
                Console.WriteLine($"OpenFile: Loading content for {vFilePath}")
                lSourceFileInfo.LoadContent()
            End If
            
            ' Update status
            UpdateStatusBar($"Loading: {System.IO.Path.GetFileName(vFilePath)}")
            
            ' Create new tab - this will handle all integrations
            CreateNewTab(vFilePath, lSourceFileInfo, Not lIsNewFile)
            
            ' Update Object Explorer if project is open
            If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                ' Get the entire project syntax tree
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    ' Update with project-aware structure
                    Console.WriteLine($"OpenFile: Updating Object Explorer with project structure")
                    UpdateObjectExplorerForActiveTab()
                End If
            End If            
    
            Console.WriteLine($"OpenFile: Completed for {vFilePath}")
            Console.WriteLine($"===== OpenFile finished =====")
            
        Catch ex As Exception
            Console.WriteLine($"OpenFile error: {ex.Message}")
            Console.WriteLine($"Stack: {ex.StackTrace}")
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
    
    ''' <summary>
    ''' Gets the TabInfo for the currently active tab (including help tabs)
    ''' </summary>
    ''' <returns>TabInfo for current tab or Nothing</returns>
    Private Function GetCurrentTabInfo() As TabInfo
        Try
            If pNotebook IsNot Nothing AndAlso pNotebook.CurrentPage >= 0 Then
                Dim lCurrentPage As Widget = pNotebook.GetNthPage(pNotebook.CurrentPage)
                
                ' Check regular editor tabs
                for each lTabEntry in pOpenTabs
                    If lTabEntry.Value.EditorContainer Is lCurrentPage Then
                        Return lTabEntry.Value
                    End If
                Next
                
                ' Check help tabs
                for each lHelpEntry in pHelpTabs
                    If lHelpEntry.Value.EditorContainer Is lCurrentPage Then
                        Return lHelpEntry.Value
                    End If
                Next
                
                ' Check other special tabs (AI artifacts, comparisons, etc.)
                If pAIArtifactTabs IsNot Nothing Then
                    for each lArtifactEntry in pAIArtifactTabs
                        If lArtifactEntry.Value.EditorContainer Is lCurrentPage Then
                            Return lArtifactEntry.Value
                        End If
                    Next
                End If
                
                If pComparisonTabs IsNot Nothing Then
                    for each lCompEntry in pComparisonTabs
                        If lCompEntry.Value.EditorContainer Is lCurrentPage Then
                            Return lCompEntry.Value
                        End If
                    Next
                End If
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
    
    ''' <summary>
    ''' Creates a new editor tab with full Object Explorer integration
    ''' </summary>
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
                ' Create simple label for PNG files
                Dim lImageLabel As New Label($"PNG Image: {System.IO.Path.GetFileName(vFilePath)}")
                lImageLabel.Halign = Align.Center
                lImageLabel.Valign = Align.Center
                lTabInfo.Editor = Nothing ' No IEditor for images
                lTabInfo.EditorContainer = lImageLabel
                lTabInfo.NavigationDropdowns = Nothing ' No navigation for images
            Else
                ' Create code editor
                Dim lEditor As New CustomDrawingEditor(vSourceFileInfo, pThemeManager, pSettingsManager)
            
                ' Set the theme manager
                If pThemeManager IsNot Nothing Then
                    lEditor.SetThemeManager(pThemeManager)
                End If
                
                ' Set the ProjectManager BEFORE any other initialization
                If pProjectManager IsNot Nothing Then
                    Console.WriteLine($"CreateNewTab: Setting ProjectManager on editor for {vFilePath}")
                    lEditor.ProjectManager = pProjectManager
                Else
                    Console.WriteLine($"CreateNewTab: WARNING - No ProjectManager available for {vFilePath}")
                End If
            
                ' Show the editor widget
                lEditor.ShowAll()
                
                ' Hook up ALL editor events (standard + Object Explorer)
                HookupAllEditorEvents(lEditor)
                
                ' Set up Object Explorer integration
                SetupObjectExplorerForEditor(lEditor)
                
                ' Create navigation dropdowns for code files
                Dim lNavigationDropdowns As New NavigationDropdowns()
                lNavigationDropdowns.SetEditor(lEditor)
                
                ' Hook up navigation event
                AddHandler lNavigationDropdowns.NavigationRequested, AddressOf OnNavigationRequested
                
                ' Create container to hold both navigation and editor
                Dim lContainer As New Box(Orientation.Vertical, 0)
                lContainer.PackStart(lNavigationDropdowns, False, False, 2)
                lContainer.PackStart(lEditor, True, True, 0)
                lContainer.ShowAll()
                
                lTabInfo.Editor = lEditor
                lTabInfo.EditorContainer = lContainer ' Container holds both navigation and editor
                lTabInfo.NavigationDropdowns = lNavigationDropdowns
                HookupNavigationEvents(lTabInfo)
            End If
            
            ' Create tab label
            lTabInfo.TabLabel = CreateTabLabel(vFilePath, lTabInfo.Modified)
            
            ' Add to notebook
            pNotebook.AppendPage(lTabInfo.EditorContainer, System.IO.Path.GetFileName(vFilePath))
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
            
            ' Update Object Explorer for the new tab
            UpdateObjectExplorerForActiveTab()
            
        Catch ex As Exception
            Console.WriteLine($"CreateNewTab error: {ex.Message}")
        End Try
    End Sub

    
    ''' <summary>
    ''' Hooks up all editor events including Object Explorer integration and navigation updates
    ''' </summary>
    ''' <param name="vEditor">The editor to hook events for</param>
    ''' <remarks>
    ''' This method connects all editor events including the NavigationUpdateRequested event
    ''' for CustomDrawingEditor instances
    ''' </remarks>
    Private Sub HookupAllEditorEvents(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Standard editor events
            AddHandler vEditor.TextChanged, AddressOf OnEditorTextChanged
            AddHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            AddHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            AddHandler vEditor.Modified, AddressOf OnEditorModified
            AddHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Object Explorer specific events
            AddHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
            ' ProjectManager request event
            AddHandler vEditor.ProjectManagerRequested, AddressOf OnEditorProjectManagerRequested
            AddHandler vEditor.RequestGotoDefinition, AddressOf OnRequestGotoDefinition
            
            ' Navigation update event for CustomDrawingEditor
            If TypeOf vEditor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = DirectCast(vEditor, CustomDrawingEditor)
                AddHandler lCustomEditor.NavigationUpdateRequested, AddressOf OnEditorNavigationUpdateRequested
            End If
            
            Console.WriteLine($"Hooked up all events for editor")
            
        Catch ex As Exception
            Console.WriteLine($"HookupAllEditorEvents error: {ex.Message}")
        End Try
    End Sub

    
    ''' <summary>
    ''' Unhooks all editor events including Object Explorer integration and navigation dropdowns
    ''' </summary>
    ''' <param name="vEditor">The editor to unhook events from</param>
    ''' <remarks>
    ''' This method removes all event handlers including NavigationUpdateRequested
    ''' for CustomDrawingEditor instances
    ''' </remarks>
    Private Sub UnhookAllEditorEvents(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            Console.WriteLine($"UnhookAllEditorEvents: Cleaning up events for {vEditor.DisplayName}")
            
            ' Find the tab info for this editor to clean up navigation
            Dim lTabInfo As TabInfo = Nothing
            for each lTabEntry in pOpenTabs
                If lTabEntry.Value.Editor Is vEditor Then
                    lTabInfo = lTabEntry.Value
                    Exit for
                End If
            Next
            
            ' Clean up navigation events first
            If lTabInfo IsNot Nothing Then
                UnhookNavigationEvents(lTabInfo)
            End If
            
            ' Unhook standard editor events
            RemoveHandler vEditor.Modified, AddressOf OnEditorModified
            RemoveHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            RemoveHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            RemoveHandler vEditor.TextChanged, AddressOf OnEditorTextChanged
            RemoveHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Unhook Object Explorer specific events
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
            ' Unhook ProjectManager request event (if applicable)
            RemoveHandler vEditor.ProjectManagerRequested, AddressOf OnEditorProjectManagerRequested
            
            RemoveHandler vEditor.RequestGotoDefinition, AddressOf OnRequestGotoDefinition
            
            ' Unhook navigation update event for CustomDrawingEditor
            If TypeOf vEditor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = DirectCast(vEditor, CustomDrawingEditor)
                RemoveHandler lCustomEditor.NavigationUpdateRequested, AddressOf OnEditorNavigationUpdateRequested
            End If
            
            Console.WriteLine($"Unhooked all events for editor: {vEditor.DisplayName}")
            
        Catch ex As Exception
            Console.WriteLine($"UnhookAllEditorEvents error: {ex.Message}")
        End Try
    End Sub
    
    
    
    ' Replace: SimpleIDE.MainWindow.CloseTab
    Private Sub CloseTab(vTabInfo As TabInfo)
        Try
            If vTabInfo Is Nothing Then Return
            
            Console.WriteLine($"===== CloseTab called for: {vTabInfo.FilePath} =====")
            Console.WriteLine($"  pOpenTabs.Count before = {pOpenTabs.Count}")
            
            ' Check for unsaved changes
            If vTabInfo.Modified Then
                Dim lDialog As New MessageDialog(
                    Me,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.None,
                    $"Save changes to {System.IO.Path.GetFileName(vTabInfo.FilePath)} before closing?"
                )
                lDialog.AddButton("Don't Save", ResponseType.No)
                lDialog.AddButton("Cancel", ResponseType.Cancel)
                lDialog.AddButton("Save", ResponseType.Yes)
                lDialog.DefaultResponse = ResponseType.Yes
                
                Dim lResponse As ResponseType = CType(lDialog.Run(), ResponseType)
                lDialog.Destroy()
                
                Select Case lResponse
                    Case ResponseType.Yes
                        ' Save the file
                        If Not SaveFile(vTabInfo) Then
                            ' Save failed, abort close
                            Console.WriteLine("  Save failed, aborting close")
                            Return
                        End If
                    Case ResponseType.Cancel
                        ' User cancelled, abort close
                        Console.WriteLine("  User cancelled, aborting close")
                        Return
                    Case ResponseType.No
                        ' Continue without saving
                        Console.WriteLine("  Closing without saving")
                End Select
            End If
            
            ' Remove from notebook
            Console.WriteLine($"  Removing from notebook...")
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is vTabInfo.EditorContainer Then
                    Console.WriteLine($"    Found at index {i}, removing")
                    pNotebook.RemovePage(i)
                    Exit For
                End If
            Next
            
            ' Remove from open tabs dictionary
            Console.WriteLine($"  Removing from pOpenTabs dictionary...")
            If pOpenTabs.ContainsKey(vTabInfo.FilePath) Then
                pOpenTabs.Remove(vTabInfo.FilePath)
                Console.WriteLine($"    Removed {vTabInfo.FilePath}")
            Else
                Console.WriteLine($"    WARNING: {vTabInfo.FilePath} was Not in pOpenTabs!")
            End If
            
            Console.WriteLine($"  pOpenTabs.Count after = {pOpenTabs.Count}")
            
            ' Debug: List remaining open tabs
            For Each lKey In pOpenTabs.Keys
                Console.WriteLine($"    Still open: {lKey}")
            Next
            
            ' CRITICAL: Dispose the TabInfo which will dispose the editor
            Console.WriteLine($"  Disposing TabInfo...")
            vTabInfo.Dispose()
            
            ' Update UI elements
            UpdateWindowTitle()
            UpdateStatusBar("")
            UpdateToolbarButtons()
            
            ' CRITICAL FIX: Check tab count AFTER removing from notebook
            ' For CustomDrawNotebook, we need to check the actual page count
            Dim lRemainingTabs As Integer = pNotebook.NPages
            Console.WriteLine($"  Remaining tabs in notebook: {lRemainingTabs}")
            
            ' Also check if there are any non-Welcome tabs remaining
            Dim lHasNonWelcomeTabs As Boolean = False
            For i As Integer = 0 To pNotebook.NPages - 1
                If Not IsWelcomeTab(i) Then
                    lHasNonWelcomeTabs = True
                    Exit For
                End If
            Next
            
            ' Show welcome screen if no tabs left or only Welcome tab remains
            If lRemainingTabs = 0 OrElse Not lHasNonWelcomeTabs Then
                Console.WriteLine("  No tabs left, showing Welcome tab")
                ' Close any existing Welcome tab first to avoid duplicates
                CloseWelcomeTab()
                ShowWelcomeTab()
                
                ' Clear Object Explorer when no tabs are open
                If pObjectExplorer IsNot Nothing Then
                    pObjectExplorer.ClearStructure()
                End If
            End If
            
            Console.WriteLine($"===== CloseTab finished =====")
            
        Catch ex As Exception
            Console.WriteLine($"CloseTab error: {ex.Message}")
            Console.WriteLine($"Stack: {ex.StackTrace}")
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

    
' Replace: SimpleIDE.MainWindow.SaveFileAs
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
            
            ' Update file path in TabInfo
            vTabInfo.FilePath = lDialog.Filename
            
            ' CRITICAL: Update the SourceFileInfo's file path too
            If vTabInfo.Editor.SourceFileInfo IsNot Nothing Then
                vTabInfo.Editor.SourceFileInfo.FilePath = lDialog.Filename
                Console.WriteLine($"SaveFileAs: Updated SourceFileInfo path To {lDialog.Filename}")
            End If
            
            ' Save file (this will now save to the new path and sync states)
            If SaveFile(vTabInfo) Then
                ' Update open tabs dictionary
                If lOldPath <> vTabInfo.FilePath Then
                    pOpenTabs.Remove(lOldPath)
                    pOpenTabs(vTabInfo.FilePath) = vTabInfo
                End If
                
                ' Update tab label to show new filename
                UpdateTabLabel(vTabInfo)
                
                ' Update UI
                UpdateWindowTitle()
                
                lResult = True
            Else
                ' Restore old paths on failure
                vTabInfo.FilePath = lOldPath
                If vTabInfo.Editor.SourceFileInfo IsNot Nothing Then
                    vTabInfo.Editor.SourceFileInfo.FilePath = lOldPath
                End If
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
'     
'     Public Async Sub ReLoadFile(vFilePath As String)
'         Try
'             If Not pOpenTabs.ContainsKey(vFilePath) Then Return
'             
'             Dim lTabInfo As TabInfo = pOpenTabs(vFilePath)
'             If lTabInfo?.Editor Is Nothing Then Return
'             
'             ' Read file content
'             Dim lContent As String = Await Task.Run(Function() File.ReadAllText(vFilePath))
'             
'             ' Update editor
'             lTabInfo.Editor.Text = lContent
'             lTabInfo.Modified = False
'             UpdateTabLabel(lTabInfo)
'             
'         Catch ex As Exception
'             Console.WriteLine($"ReLoadFile error: {ex.Message}")
'         End Try
'     End Sub
    
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
            
            Dim lTabLabel As String = pNotebook.GetTabLabel(vPageIndex)
            Return (lTabLabel = "Welcome")
            
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
    
    ''' <summary>
    ''' Updates the tab label text and CustomDrawNotebook's red dot indicator
    ''' </summary>
    ''' <param name="vTabInfo">The TabInfo for the tab to update</param>
    Private Sub UpdateTabLabel(vTabInfo As TabInfo)
        Try
            ' Update the label text (asterisk before filename)
            If vTabInfo.TabLabel.GetType() Is GetType(Box) Then
                Dim lBox As Box = CType(vTabInfo.TabLabel, Box)
                For Each lChild As Widget In lBox.Children
                    If lChild.GetType() Is GetType(Label) Then
                        Dim lLabel As Label = CType(lChild, Label)
                        Dim lFileName As String = System.IO.Path.GetFileName(vTabInfo.FilePath)
                        If vTabInfo.Modified Then
                            lLabel.Text = $"*{lFileName}"
                        Else
                            lLabel.Text = lFileName
                        End If
                        Exit For
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
            
            ' FIXED: Update the CustomDrawNotebook's red dot indicator (visual modified indicator)
            ' This is separate from the asterisk in the text label
            If TypeOf pNotebook Is CustomDrawNotebook Then
                Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                
                ' Find the tab index by comparing the EditorContainer widget
                For i As Integer = 0 To lCustomNotebook.NPages - 1
                    Dim lPage As Widget = lCustomNotebook.GetNthPage(i)
                    If lPage Is vTabInfo.EditorContainer Then
                        ' Update the red dot indicator in the CustomDrawNotebook
                        lCustomNotebook.SetTabModified(i, vTabInfo.Modified)
                        Console.WriteLine($"UpdateTabLabel: Updated red dot for tab {i} To {vTabInfo.Modified} for {vTabInfo.FilePath}")
                        Exit For
                    End If
                Next
            End If
            
            ' FIXED: Update project dirty state whenever a tab's modified state changes
            UpdateProjectDirtyState()
            
            ' Update window title to reflect both project and file states
            UpdateWindowTitle()
            
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
            RemoveHandler vEditor.TextChanged, AddressOf OnEditorTextChanged
            RemoveHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Unhook Object Explorer events
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
        Catch ex As Exception
            Console.WriteLine($"UnhookEditorEventsForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Editor Event Handlers =====
    
    ''' <summary>
    ''' Unified text changed handler with all functionality
    ''' </summary>
    Private Sub OnEditorTextChanged(vSender As Object, vArgs As EventArgs)
        Try
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Mark tab as modified
            MarkTabModified(lEditor)
            
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
    
    Private Sub OnEditorUndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        Try
            ' Update toolbar buttons
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorUndoRedoStateChanged error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced cursor position changed handler with navigation dropdown support
    ''' </summary>
    Private Sub OnEditorCursorPositionChanged(vLine As Integer, vColumn As Integer)
        Try
            ' Update status bar (existing functionality)
            UpdateStatusBar("")
            
            ' Update navigation dropdowns for current tab (new functionality)
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.NavigationDropdowns IsNot Nothing Then
                ' Update navigation dropdowns position
                lCurrentTab.NavigationDropdowns.UpdatePosition(vLine)
            End If
            
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

    ''' <summary>
    ''' Comprehensive notebook page switching handler with all integrations
    ''' </summary>
    ''' <remarks>
    ''' This method provides complete integration for:
    ''' 1. Basic UI updates (window title, status bar, toolbar)
    ''' 2. Object Explorer integration and updates
    ''' 3. Project structure synchronization
    ''' 4. Syntax highlighting refresh
    ''' 5. Navigation dropdowns update (Step 5)
    ''' 6. Editor focus management
    ''' </remarks>
    Private Sub OnLeftPanelNotebookSwitchPage(vOldIndex As Integer, vNewIndex As Integer)
        Try
            Console.WriteLine($"OnNotebookSwitchPage: Switching To page {vNewIndex}")
            
            ' ===== 1. Basic UI Updates =====
            UpdateWindowTitle()
            UpdateStatusBar("")
            UpdateToolbarButtons()
            
            ' ===== 2. Get Current Tab Info =====
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then
                Console.WriteLine("OnNotebookSwitchPage: No current tab Or editor found")
                Return
            End If
            
            Console.WriteLine($"OnNotebookSwitchPage: Switching To {lCurrentTab.FilePath}")
            
            ' ===== 3. Object Explorer Integration =====
            ' Update Object Explorer for new active editor
            UpdateObjectExplorerForActiveTab()
            
            ' Update Object Explorer toolbar state
            UpdateObjectExplorerToolbarState()
            
            ' Update current editor in Object Explorer
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.SetCurrentEditor(lCurrentTab.Editor)
            End If
            
            ' ===== 4. Project Integration =====
            ' If project is open, check if we have structure for this file
            If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen AndAlso 
               Not String.IsNullOrEmpty(lCurrentTab.FilePath) Then
                
                Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(lCurrentTab.FilePath)
                
                If lSourceFileInfo IsNot Nothing AndAlso lSourceFileInfo.SyntaxTree IsNot Nothing Then
                    ' Update Object Explorer with project structure for this file
                    If pObjectExplorer IsNot Nothing Then
                        ' Show the whole project structure
                        Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                        If lProjectTree IsNot Nothing Then
                            pObjectExplorer.UpdateStructure(lProjectTree)
                        End If
                    End If
                End If
            End If
            
            
            ' ===== 6. UPDATE NAVIGATION DROPDOWNS (STEP 5) =====
            ' Update navigation dropdowns for the newly active tab
            Console.WriteLine("OnNotebookSwitchPage: Updating navigation dropdowns for newly active tab")
            UpdateNavigationDropdowns()
            
            ' ===== 7. Ensure Editor Focus =====
            ' Give focus to the editor widget for keyboard input
            If lCurrentTab.Editor.Widget IsNot Nothing Then
                lCurrentTab.Editor.Widget.GrabFocus()
            End If
            
            ' ===== 8. Update Status Bar with File Info =====
            Dim lFileName As String = System.IO.Path.GetFileName(lCurrentTab.FilePath)
            UpdateStatusBar($"Ready - {lFileName}")
            
            Console.WriteLine($"OnNotebookSwitchPage: Successfully completed tab switch To {lFileName}")
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPage error: {ex.Message}")
            Console.WriteLine($"  Stack trace: {ex.StackTrace}")
            
            ' Try to provide minimal functionality even if something fails
            Try
                UpdateStatusBar("Tab switch completed with errors - see console")
            Catch
                ' Ignore secondary errors
            End Try
        End Try
    End Sub


    ''' <summary>
    ''' Handles the Modified event from an editor
    ''' </summary>
    ''' <param name="vIsModified">True if the editor content has been modified</param>
    Private Sub OnEditorModified(sender as Object, vIsModified As Boolean)
        Try
            ' Find the editor that raised the event
            Dim lSender As IEditor = TryCast(sender, IEditor)
            If lSender Is Nothing Then Return
            
            ' Find the corresponding TabInfo
            For Each lTabEntry In pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                If lTabInfo.Editor Is lSender Then
                    ' Update the TabInfo's modified state
                    lTabInfo.Modified = vIsModified
                    
                    ' Update the tab label (which now also updates CustomDrawNotebook)
                    UpdateTabLabel(lTabInfo)
                    
                    ' FIXED: Update the project's dirty state based on all files
                    UpdateProjectDirtyState()
                    
                    ' Update window title (now shows both project and file states)
                    UpdateWindowTitle()
                    
                    Console.WriteLine($"OnEditorModified: Updated tab for {lTabInfo.FilePath}, Modified = {vIsModified}")
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorModified error: {ex.Message}")
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
                UpdateStatusBar("Line cut To clipboard")
            Else
                ' For other editor types, try to implement basic cut line functionality
                ' using the IEditor interface methods
                
                ' Get current line position
                Dim lPosition As EditorPosition = lEditor.GetCursorPosition()
                Dim lCurrentLine As Integer = lPosition.Line  ' 0-based
                
                ' Select the entire line
                lEditor.SelectLine(lCurrentLine + 1)  ' SelectLine expects 0-based
                
                ' Cut the selected text
                lEditor.Cut()
                
                ' Update toolbar and status
                UpdateToolbarButtons()
                UpdateStatusBar("Line cut To clipboard")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCutLine error: {ex.Message}")
            ShowError("Cut Line error", $"Failed To cut line: {ex.Message}")
        End Try
    End Sub
       
    
    ''' <summary>
    ''' Hooks up navigation dropdown events for an editor
    ''' </summary>
    ''' <param name="vTabInfo">The tab info containing the editor and navigation dropdowns</param>
    ''' <remarks>
    ''' This method connects editor events to the navigation dropdowns for automatic updates.
    ''' Call this after both the editor and navigation dropdowns have been created and configured.
    ''' Integrates with existing event handling system.
    ''' </remarks>
    Private Sub HookupNavigationEvents(vTabInfo As TabInfo)
        Try
            If vTabInfo?.Editor Is Nothing OrElse vTabInfo.NavigationDropdowns Is Nothing Then
                Console.WriteLine("HookupNavigationEvents: Missing editor Or navigation dropdowns")
                Return
            End If
            
            Console.WriteLine($"HookupNavigationEvents: Setting up navigation events for {vTabInfo.FilePath}")
            
            ' Store reference to tab info for use in event handlers
            ' We'll use the existing event handlers and add navigation updates to them
            
            ' Note: The actual cursor position and document parsed events are already
            ' connected via HookupAllEditorEvents, so we don't duplicate them here.
            ' Instead, we rely on the existing OnEditorCursorPositionChanged and
            ' OnEditorDocumentParsed methods which will call UpdateNavigationDropdowns
            ' when the current tab matches this tab.
            
            ' Initialize navigation dropdowns with current editor state if available
            If TypeOf vTabInfo.Editor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = DirectCast(vTabInfo.Editor, CustomDrawingEditor)
                If lCustomEditor.LineCount > 0 Then
                    Console.WriteLine("HookupNavigationEvents: Editor has content, scheduling initial navigation update")
                    
                    ' Schedule initial update after UI is fully loaded
                    GLib.Idle.Add(Function()
                        Try
                            ' Only update if this tab is currently active
                            If GetCurrentTabInfo() Is vTabInfo Then
                                UpdateNavigationDropdowns()
                                Console.WriteLine("Initial navigation dropdowns populated")
                            End If
                        Catch ex As Exception
                            Console.WriteLine($"Initial navigation update error: {ex.Message}")
                        End Try
                        Return False ' Run once only
                    End Function)
                End If
            End If
            
            Console.WriteLine("Navigation events integration completed")
            
        Catch ex As Exception
            Console.WriteLine($"HookupNavigationEvents error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Unhooks navigation dropdown events when closing a tab
    ''' </summary>
    ''' <param name="vTabInfo">The tab info containing the editor and navigation dropdowns</param>
    ''' <remarks>
    ''' This method should be called before closing a tab to prevent memory leaks.
    ''' Since navigation events are integrated into the main event handlers,
    ''' this mainly handles cleanup of the navigation dropdowns themselves.
    ''' </remarks>
    Private Sub UnhookNavigationEvents(vTabInfo As TabInfo)
        Try
            If vTabInfo Is Nothing Then Return
            
            Console.WriteLine($"UnhookNavigationEvents: Cleaning up navigation for {vTabInfo.FilePath}")
            
            ' Clear navigation dropdowns
            If vTabInfo.NavigationDropdowns IsNot Nothing Then
                vTabInfo.NavigationDropdowns.Clear()
                Console.WriteLine("Navigation dropdowns cleared")
            End If
            
            ' Note: We don't need to manually unhook the cursor position and document parsed
            ' events here because they are handled by the main UnhookAllEditorEvents method
            ' and the navigation updates are integrated into those existing handlers.
            
            Console.WriteLine("Navigation events cleanup completed")
            
        Catch ex As Exception
            Console.WriteLine($"UnhookNavigationEvents error: {ex.Message}")
        End Try
    End Sub

    
    ''' <summary>
    ''' Handles the NavigationUpdateRequested event from CustomDrawingEditor
    ''' </summary>
    ''' <param name="vSender">The editor that raised the event</param>
    ''' <param name="vArgs">Event arguments</param>
    ''' <remarks>
    ''' This handler is called when the cursor moves and the navigation dropdowns
    ''' need to check for context changes (different class/method)
    ''' </remarks>
    Private Sub OnEditorNavigationUpdateRequested(vSender As Object, vArgs As EventArgs)
        Try
            ' Check if the sender is the current active editor
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing Then Return
            
            ' Verify this is the active editor raising the event
            If lCurrentTab.Editor IsNot vSender Then Return
            
            ' Call UpdateNavigationDropdowns to update the dropdowns
            ' based on the current cursor position and document structure
            UpdateNavigationDropdowns()
            
            Console.WriteLine("OnEditorNavigationUpdateRequested: Navigation dropdowns updated")
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorNavigationUpdateRequested error: {ex.Message}")
        End Try
    End Sub
    
    ' Replace: SimpleIDE.MainWindow.OnCustomNotebookTabClosed
    ''' <summary>
    ''' Handles the TabClosed event from CustomDrawNotebook
    ''' </summary>
    ''' <param name="vIndex">Index of the closed tab</param>
    Private Sub OnCustomNotebookTabClosed(vIndex As Integer)
        Try
            Console.WriteLine($"OnCustomNotebookTabClosed: Tab at index {vIndex} was closed")
            
            ' CRITICAL FIX: We need to find and remove the TabInfo from pOpenTabs
            ' The CustomDrawNotebook has already removed the widget from the notebook,
            ' but we need to clean up our tracking dictionaries
            
            ' Find the TabInfo that was at this index
            Dim lTabInfoToRemove As TabInfo = Nothing
            Dim lKeyToRemove As String = Nothing
            
            ' Search through all open tabs (including special ones like scratchpad, help, etc.)
            For Each lKvp In pOpenTabs
                Dim lKey As String = lKvp.Key
                Dim lTabInfo As TabInfo = lKvp.Value
                
                ' Check if this TabInfo's widget is still in the notebook
                Dim lFoundInNotebook As Boolean = False
                For i As Integer = 0 To pNotebook.NPages - 1
                    Dim lPage As Widget = pNotebook.GetNthPage(i)
                    If lPage Is lTabInfo.EditorContainer Then
                        lFoundInNotebook = True
                        Exit For
                    End If
                Next
                
                ' If not found in notebook, this is the one that was closed
                If Not lFoundInNotebook Then
                    lTabInfoToRemove = lTabInfo
                    lKeyToRemove = lKey
                    Console.WriteLine($"  Found closed tab: {lKey}")
                    Exit For
                End If
            Next
            
            ' Handle special cleanup based on tab type
            If lTabInfoToRemove IsNot Nothing AndAlso lKeyToRemove IsNot Nothing Then
                ' Check if it's a scratchpad tab
                If lKeyToRemove.StartsWith("scratchpad:") Then
                    ' Extract the tab ID
                    Dim lTabId As String = lKeyToRemove.Replace("scratchpad:", "")
                    
                    ' Force save and remove from scratchpad panels
                    If pScratchpadPanels.ContainsKey(lTabId) Then
                        pScratchpadPanels(lTabId).ForceSave()
                        pScratchpadPanels.Remove(lTabId)
                        Console.WriteLine($"  Saved and removed scratchpad panel: {lTabId}")
                    End If
                    
                    ' Update scratchpad button tooltip
                    UpdateScratchpadButtonTooltip(False)
                End If
                
                ' Check if it's a help tab
                If lKeyToRemove.StartsWith("help:") Then
                    ' Extract the help tab ID
                    Dim lHelpTabId As String = lKeyToRemove.Replace("help:", "")
                    
                    ' Remove from help tabs dictionary if it exists
                    If pHelpTabs IsNot Nothing AndAlso pHelpTabs.ContainsKey(lHelpTabId) Then
                        pHelpTabs.Remove(lHelpTabId)
                        Console.WriteLine($"  Removed help tab: {lHelpTabId}")
                    End If
                End If
                
                ' Check if it's an AI artifact tab
                If lKeyToRemove.StartsWith("ai-artifact:") Then
                    ' Extract the artifact ID
                    Dim lArtifactId As String = lKeyToRemove.Replace("ai-artifact:", "")
                    
                    ' Remove from AI artifact tabs dictionary if it exists
                    If pAIArtifactTabs IsNot Nothing AndAlso pAIArtifactTabs.ContainsKey(lArtifactId) Then
                        pAIArtifactTabs.Remove(lArtifactId)
                        Console.WriteLine($"  Removed AI artifact tab: {lArtifactId}")
                    End If
                End If
                
                ' Remove from pOpenTabs dictionary
                pOpenTabs.Remove(lKeyToRemove)
                Console.WriteLine($"  Removed {lKeyToRemove} from pOpenTabs")
                Console.WriteLine($"  pOpenTabs.Count = {pOpenTabs.Count}")
                
                ' Unhook all editor events (if it has an editor)
                If lTabInfoToRemove.Editor IsNot Nothing Then
                    UnhookAllEditorEvents(lTabInfoToRemove.Editor)
                End If
                
                ' Dispose the TabInfo
                lTabInfoToRemove.Dispose()
                
                ' Update UI
                UpdateWindowTitle()
                UpdateStatusBar("")
                UpdateToolbarButtons()
                
                ' Update Object Explorer for regular file tabs
                If Not lKeyToRemove.Contains(":") Then ' Regular file path doesn't contain ":"
                    UpdateObjectExplorerForActiveTab()
                End If
            Else
                Console.WriteLine("  WARNING: Could Not find TabInfo for closed tab!")
            End If
            
            ' Check if all tabs are now closed
            If TypeOf pNotebook Is CustomDrawNotebook Then
                Dim lNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                
                ' Check if notebook is now empty
                If lNotebook.NPages = 0 Then
                    Console.WriteLine("CustomDrawNotebook Is now empty - showing Welcome tab")
                    ShowWelcomeTab()
                    
                    ' Clear Object Explorer
                    If pObjectExplorer IsNot Nothing Then
                        pObjectExplorer.ClearStructure()
                    End If
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCustomNotebookTabClosed error: {ex.Message}")
            Console.WriteLine($"Stack: {ex.StackTrace}")
        End Try
    End Sub    
    
    ''' <summary>
    ''' Wire up CustomDrawNotebook events
    ''' </summary>
    Private Sub WireCustomNotebookEvents()
        Try
            If TypeOf pNotebook Is CustomDrawNotebook Then
                Dim lNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                
                ' Wire up the TabClosed event
                AddHandler lNotebook.TabClosed, AddressOf OnCustomNotebookTabClosed
                
                ' Wire up other events as needed
                AddHandler lNotebook.TabModifiedChanged, AddressOf OnCustomNotebookTabModifiedChanged
            End If
            
        Catch ex As Exception
            Console.WriteLine($"WireCustomNotebookEvents error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles tab modified state changes from CustomDrawNotebook
    ''' </summary>
    Private Sub OnCustomNotebookTabModifiedChanged(vIndex As Integer, vModified As Boolean)
        Try
            ' Update window title if this is the current tab
            If vIndex = pNotebook.CurrentPage Then
                UpdateWindowTitle()
            End If
            
            ' Update toolbar buttons
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnCustomNotebookTabModifiedChanged error: {ex.Message}")
        End Try
    End Sub    

    ''' <summary>
    ''' Handles the RequestGotoDefinition event from CustomDrawingEditor instances
    ''' </summary>
    ''' <param name="vSender">The CustomDrawingEditor that raised the event</param>
    ''' <param name="vArgs">Event arguments containing word and location information</param>
    ''' <remarks>
    ''' This method searches for the definition of the word across all project files,
    ''' opens the file containing the definition, and navigates to the exact location
    ''' </remarks>
    Private Sub OnRequestGotoDefinition(vSender As Object, vArgs As GoToDefinitionEventArgs)
        Try
            Console.WriteLine($"OnRequestGotoDefinition: Word='{vArgs.Word}' at {vArgs.FilePath}:{vArgs.LineNumber}:{vArgs.ColumnNumber}")
            
            ' Validate input
            If String.IsNullOrWhiteSpace(vArgs.Word) Then
                Console.WriteLine("OnRequestGotoDefinition: No word specified")
                UpdateStatusBar("No symbol selected for Go to Definition")
                Return
            End If
            
            ' Check if we have a project manager
            If pProjectManager Is Nothing Then
                Console.WriteLine("OnRequestGotoDefinition: No ProjectManager available")
                UpdateStatusBar($"Go to Definition: No project open")
                Return
            End If
            
            ' Call the ProjectManager to find the definition
            Dim lDefinitionInfo As DefinitionInfo = pProjectManager.FindDefinition(vArgs.Word, vArgs.FilePath, vArgs.LineNumber, vArgs.ColumnNumber)
            
            If lDefinitionInfo Is Nothing Then
                Console.WriteLine($"OnRequestGotoDefinition: Definition not found for '{vArgs.Word}'")
                UpdateStatusBar($"Definition Of '{vArgs.Word}' not found")
                Return
            End If
            
            Console.WriteLine($"OnRequestGotoDefinition: Found definition in {lDefinitionInfo.FilePath} at line {lDefinitionInfo.Line}")
            
            ' Check if the file is already open
            Dim lTargetFilePath As String = lDefinitionInfo.FilePath
            Dim lIsAlreadyOpen As Boolean = pOpenTabs.ContainsKey(lTargetFilePath)
            
            If lIsAlreadyOpen Then
                Console.WriteLine($"OnRequestGotoDefinition: File already open, switching to tab")
                ' Switch to the existing tab
                SwitchToTab(lTargetFilePath)
            Else
                Console.WriteLine($"OnRequestGotoDefinition: Opening file {lTargetFilePath}")
                ' Open the file
                OpenFile(lTargetFilePath)
            End If
            
            ' Navigate to the definition location
            Dim lTabInfo As TabInfo = Nothing
            If pOpenTabs.TryGetValue(lTargetFilePath, lTabInfo) Then
                If lTabInfo.Editor IsNot Nothing Then
                    ' Create EditorPosition for navigation
                    Dim lPosition As New EditorPosition(lDefinitionInfo.Line, lDefinitionInfo.Column)
                    
                    Console.WriteLine($"OnRequestGotoDefinition: Navigating to position {lPosition}")
                    
                    ' Navigate to the position
                    lTabInfo.Editor.GoToPosition(lPosition)
                    
                    ' Focus the editor
                    lTabInfo.Editor.Widget.GrabFocus()
                    
                    ' Update status bar
                    Dim lFileName As String = System.IO.Path.GetFileName(lTargetFilePath)
                    UpdateStatusBar($"Navigated to definition of '{vArgs.Word}' in {lFileName} at line {lDefinitionInfo.Line + 1}")
                Else
                    Console.WriteLine("OnRequestGotoDefinition: Editor Not found in tab")
                End If
            Else
                Console.WriteLine($"OnRequestGotoDefinition: Could Not find tab for {lTargetFilePath}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnRequestGotoDefinition error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
            UpdateStatusBar($"error navigating To definition: {ex.Message}")
        End Try
    End Sub

        
End Class
