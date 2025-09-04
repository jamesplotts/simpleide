' MainWindow.FindPanel.vb - Integration with FindReplacePanel
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Partial Public Class MainWindow
    
    ' ===== Find Panel Integration =====
    

    ''' <summary>
    ''' Enhanced initialization that connects FindReplacePanel with ProjectManager
    ''' </summary>
    Private Sub InitializeFindPanelEvents()
        Try
            If pBottomPanelManager?.FindPanel IsNot Nothing Then
                Dim lFindPanel As FindReplacePanel = pBottomPanelManager.FindPanel
                
                ' Existing event handlers
                AddHandler lFindPanel.OnRequestCurrentTab, AddressOf OnFindPanelRequestCurrentTab
                AddHandler lFindPanel.OnRequestOpenTabs, AddressOf OnFindPanelRequestOpenTabs
                AddHandler lFindPanel.OpenFileRequested, AddressOf OnFindPanelOpenFileRequested
                AddHandler lFindPanel.CloseRequested, AddressOf OnFindPanelCloseRequested
                
                ' NEW: Handle ProjectManager request
                AddHandler lFindPanel.OnRequestProjectManager, AddressOf OnFindPanelRequestProjectManager
                
                ' NEW: Set ProjectManager if already available
                If pProjectManager IsNot Nothing Then
                    lFindPanel.SetProjectManager(pProjectManager)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeFindPanelOptimized error: {ex.Message}")
        End Try
    End Sub

    Public Sub OnFindPanelRequestCurrentTab(vTabInfoEventArgs As FindReplacePanel.TabInfoEventArgs)
        vTabInfoEventArgs.TabInfo = GetCurrentTabInfo()
    End Sub

    ''' <summary>
    ''' Handles FindReplacePanel's request for ProjectManager
    ''' </summary>
    Private Sub OnFindPanelRequestProjectManager(vSender As Object, vArgs As FindReplacePanel.ProjectManagerEventArgs)
        Try
            vArgs.ProjectManager = pProjectManager
            Console.WriteLine($"MainWindow: Provided ProjectManager to FindReplacePanel")
            
        Catch ex As Exception
            Console.WriteLine($"OnFindPanelRequestProjectManager error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows the find panel and executes search if text is selected (for Ctrl+F)
    ''' </summary>
    Public Sub ShowFindPanel()
        Try
            ' Show bottom panel if hidden
            If Not pBottomPanelVisible Then
                ToggleBottomPanel()
            End If
            
            ' Switch to Find Results tab
            If pBottomPanelManager IsNot Nothing AndAlso pFindPanel IsNot Nothing Then
                pBottomPanelManager.ShowTabForPanel(pFindPanel)
            End If
            
            ' Set project root if available
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                pFindPanel.SetProjectRoot(System.IO.Path.GetDirectoryName(pCurrentProject))
            End If
            
            Dim lHasSelection As Boolean = False
            Dim lWordAtCursor As String = ""
            
            ' Pre-populate with selected text if any
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing AndAlso lEditor.HasSelection Then
                Dim lSelectedText As String = lEditor.SelectedText
                
                ' Only use if it's a single line
                If Not String.IsNullOrEmpty(lSelectedText) AndAlso 
                   Not lSelectedText.Contains(vbLf) AndAlso 
                   Not lSelectedText.Contains(vbCr) Then
                    
                    lHasSelection = True
                    pFindPanel.SetSearchText(lSelectedText)
                    
                    ' Execute the find with current options
                    pFindPanel.OnFind(Nothing, Nothing)
                End If
            ElseIf lEditor IsNot Nothing Then
                ' No selection - get word at cursor
                lWordAtCursor = lEditor.GetWordAtCursor()
                
                ' If there's a word at cursor, use it as search text
                If Not String.IsNullOrEmpty(lWordAtCursor) Then
                    pFindPanel.SetSearchText(lWordAtCursor)
                    ' Don't execute search yet - let user confirm
                End If
            End If
            
            ' Focus search entry based on context
            If String.IsNullOrEmpty(lWordAtCursor) AndAlso Not lHasSelection Then
                ' No word at cursor and no selection - select all existing text
                pFindPanel.FocusSearchEntry() ' This selects all via SelectRegion(0, -1)
            Else
                ' Has word at cursor or selection - focus without selecting
                pFindPanel.FocusSearchEntryNoSelect()
                pFindPanel.OnFind(Nothing, Nothing)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowFindPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' Show find panel with specific text
    Public Sub ShowFindPanelWithText(vSearchText As String)
        Try
            ShowFindPanel()
            pFindPanel.SetSearchText(vSearchText)
            
        Catch ex As Exception
            Console.WriteLine($"ShowFindPanelWithText error: {ex.Message}")
        End Try
    End Sub
    
    ' Show find panel for replace
    Public Sub ShowFindPanelForReplace()
        Try
            ShowFindPanel()
            
            ' Pre-populate with selected text if any
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing AndAlso lEditor.HasSelection Then
                Dim lSelectedText As String = lEditor.SelectedText
                ' Only use if it's a single line
                If Not lSelectedText.Contains(vbLf) AndAlso Not lSelectedText.Contains(vbCr) Then
                    pFindPanel.SetSearchText(lSelectedText)
                End If
            End If
            
            ' Focus replace entry
            pFindPanel.FocusReplaceEntry()
            
        Catch ex As Exception
            Console.WriteLine($"ShowFindPanelForReplace error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Event Handlers =====
    
    Private Sub OnFindPanelRequestCurrentTab(vSender As Object, vArgs As FindReplacePanel.TabInfoEventArgs)
        Try
            vArgs.TabInfo = GetCurrentTabInfo()
            
        Catch ex As Exception
            Console.WriteLine($"OnFindPanelRequestCurrentTab error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnFindPanelRequestOpenTabs(vSender As Object, vArgs As FindReplacePanel.OpenTabsEventArgs)
        Try
            vArgs.OpenTabs = New List(Of TabInfo)(pOpenTabs.Values)
            
        Catch ex As Exception
            Console.WriteLine($"OnFindPanelRequestOpenTabs error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles navigation to a find result when selected in the Find panel
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file containing the match</param>
    ''' <param name="vLineNumber">Line number of the match (1-based)</param>
    ''' <param name="vColumnNumber">Column number of the match (1-based)</param>
    Private Sub OnFindResultSelected(vFilePath As String, vLineNumber As Integer, vColumnNumber As Integer)
        Try
            ' Open file if not already open
            If Not pOpenTabs.ContainsKey(vFilePath) Then
                OpenFile(vFilePath)
            End If
            
            ' Switch to the file tab
            SwitchToTab(vFilePath)
            
            ' Navigate to the location
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' FIXED: Don't convert to 0-based since GoToLine expects 1-based
                ' Use GoToLine for navigation to avoid the off-by-one error
                lEditor.GoToLine(vLineNumber)
                
                ' Now set the column position if needed
                If vColumnNumber > 1 Then
                    Dim lPosition As New EditorPosition(vLineNumber, vColumnNumber)
                    lEditor.GoToPosition(lPosition)
                End If
                
                ' Ensure the editor has focus
                lEditor.Widget.GrabFocus()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnFindResultSelected error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnFindPanelOpenFileRequested(vFilePath As String)
        Try
            ' Check if file is already open
            If pOpenTabs.ContainsKey(vFilePath) Then
                ' Reload the file
                Dim lTab As TabInfo = pOpenTabs(vFilePath)
                If lTab?.Editor IsNot Nothing Then
                    ' Reload content
                    lTab.Editor.SourceFileInfo.ReloadFile()
'                    ReloadFile(lTab.FilePath)
                End If
            Else
                ' Open the file
                OpenFile(vFilePath)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnFindPanelOpenFileRequested error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnFindPanelCloseRequested()
        Try
            ' Hide bottom panel
            If pBottomPanelVisible Then
                ToggleBottomPanel()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnFindPanelCloseRequested error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Quick Find Methods (Ctrl+F, Ctrl+H) =====
    
    Public Sub QuickFind()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                ShowError("Find", "No file is currently open.")
                Return
            End If
            
            ShowFindPanel()
            
        Catch ex As Exception
            Console.WriteLine($"QuickFind error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub QuickReplace()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                ShowError("Replace", "No file is currently open.")
                Return
            End If
            
            ShowFindPanelForReplace()
            
        Catch ex As Exception
            Console.WriteLine($"QuickReplace error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Find Next/Previous (F3, Shift+F3) =====
    
    Public Sub FindNextOccurrence()
        Try
            ' If find panel is visible and has search text, use it
            If pBottomPanelVisible AndAlso pFindPanel.Parent IsNot Nothing AndAlso pFindPanel.HasSearchText() Then
                ' Trigger find next in panel
                pFindPanel.FindNext()
            Else
                ' Use editor's FindNext
                Dim lEditor As IEditor = GetCurrentEditor()
                If lEditor IsNot Nothing Then
                    lEditor.FindNext()
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"FindNextOccurrence error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub FindPreviousOccurrence()
        Try
            ' If find panel is visible and has search text, use it
            If pBottomPanelVisible AndAlso pFindPanel.Parent IsNot Nothing AndAlso pFindPanel.HasSearchText() Then
                ' Trigger find previous in panel
                pFindPanel.FindPrevious()
            Else
                ' Use editor's FindPrevious
                Dim lEditor As IEditor = GetCurrentEditor()
                If lEditor IsNot Nothing Then
                    lEditor.FindPrevious()
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"FindPreviousOccurrence error: {ex.Message}")
        End Try
    End Sub

    Private Sub HideFindPanel()
        Try
            If pBottomPanelManager IsNot Nothing AndAlso pFindPanel IsNot Nothing Then
                ' Hide the find panel
                If pFindPanel.Visible Then
                    pBottomPanelManager.HidePanel()
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"HideFindPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Update Menu States =====
    
    Public Sub UpdateFindMenuStates()
        Try
            Dim lHasEditor As Boolean = GetCurrentEditor() IsNot Nothing
            
            ' Update menu items if they exist
            ' This would be called from UpdateMenuStates in MainWindow.Menu.vb
            
        Catch ex As Exception
            Console.WriteLine($"UpdateFindMenuStates error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updates FindReplacePanel when project is loaded
    ''' </summary>
    Private Sub UpdateFindPanelAfterProjectLoad()
        Try
            If pBottomPanelManager?.FindPanel IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                ' Set the ProjectManager in FindReplacePanel
                pBottomPanelManager.FindPanel.SetProjectManager(pProjectManager)
                
                ' Set the project root path
                If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    pBottomPanelManager.FindPanel.SetProjectRoot(pProjectManager.CurrentProjectInfo.ProjectDirectory)
                End If
                
                Console.WriteLine("FindReplacePanel updated with ProjectManager after project load")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateFindPanelAfterProjectLoad error: {ex.Message}")
        End Try
    End Sub
    
End Class
