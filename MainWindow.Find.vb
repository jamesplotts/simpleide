' MainWindow.FindPanel.vb - Integration with FindReplacePanel
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    
    ' ===== Find Panel Integration =====
    
    ' Initialize find panel event handlers (now using BottomPanelManager)
    Private Sub InitializeFindPanelEvents()
        Try
            If pBottomPanelManager?.FindPanel IsNot Nothing Then
                Dim lFindPanel As FindReplacePanel = pBottomPanelManager.FindPanel
                
                ' Handle request for current tab
                AddHandler lFindPanel.OnRequestCurrentTab, AddressOf OnFindPanelRequestCurrentTab
                
                ' Handle request for open tabs
                AddHandler lFindPanel.OnRequestOpenTabs, AddressOf OnFindPanelRequestOpenTabs
                
                ' Handle file open request
                AddHandler lFindPanel.OpenFileRequested, AddressOf OnFindPanelOpenFileRequested
                
                ' Handle close request
                AddHandler lFindPanel.CloseRequested, AddressOf OnFindPanelCloseRequested
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeFindPanelEvents error: {ex.Message}")
        End Try
    End Sub



    Public Sub OnFindPanelRequestCurrentTab(vTabInfoEventArgs As FindReplacePanel.TabInfoEventArgs)
        vTabInfoEventArgs.TabInfo = GetCurrentTabInfo()
    End Sub
    
    ' Show the find panel
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
            
            ' Focus search entry
            pFindPanel.FocusSearchEntry()
            
            ' Pre-populate with selected text if any
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing AndAlso lEditor.HasSelection Then
                Dim lSelectedText As String = lEditor.SelectedText
                ' Only use if it's a single line
                If Not lSelectedText.Contains(vbLf) AndAlso Not lSelectedText.Contains(vbCr) Then
                    pFindPanel.SetSearchText(lSelectedText)
                End If
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
                ' Convert to 0-based indices
                lEditor.GoToPosition(vLineNumber - 1, vColumnNumber - 1)
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
                    ReLoadFile(lTab.FilePath)
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
    
End Class
