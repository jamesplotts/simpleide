' MainWindow.FileManagement.vb - File management operations for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Partial Public Class MainWindow
    
    ' ===== File Management Operations =====
    
    ' Note: ReloadFile is already implemented in MainWindow.Editor.vb as an Async Sub
    
    ' Show notification when external file change is detected
    Private Sub ShowFileChangedNotification(vFilePath As String)
        Try
            If Not pOpenTabs.ContainsKey(vFilePath) Then Return

            ' Create info bar for notification
            Dim lInfoBar As New InfoBar()
            lInfoBar.MessageType = MessageType.Warning
            
            ' Add message
            Dim lContentArea As Box = CType(lInfoBar.ContentArea, Box)
            Dim lLabel As New Label($"the file '{System.IO.Path.GetFileName(vFilePath)}' has been Modified outside the Editor.")
            lContentArea.PackStart(lLabel, False, False, 0)
            
            ' Add buttons
            lInfoBar.AddButton("Reload", ResponseType.Yes)
            lInfoBar.AddButton("Ignore", ResponseType.No)
            
'            ' Handle response
'            AddHandler lInfoBar.Response, AddressOf HandleResponse
            
            ' Add to UI (assuming we have a notification area or we'll add to status bar area)
            ' For now, show as dialog
            Dim lDialog As New Dialog("File Changed", Me, DialogFlags.Modal)
            lDialog.ContentArea.Add(lInfoBar)
            lDialog.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"ShowFileChangedNotification error: {ex.Message}")
        End Try
    End Sub


    
    ' Show notification when external file is deleted
    Private Sub ShowFileDeletedNotification(vFilePath As String)
        Try
            If Not pOpenTabs.ContainsKey(vFilePath) Then Return
            
            Dim lTabInfo As TabInfo = pOpenTabs(vFilePath)
            
            ' Show warning dialog
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Warning,
                ButtonsType.None,
                $"the file '{System.IO.Path.GetFileName(vFilePath)}' has been deleted outside the Editor."
            )
            
            lDialog.AddButton("Keep in Editor", ResponseType.Yes)
            lDialog.AddButton("Close Tab", ResponseType.No)
            
            If lDialog.Run() = CInt(ResponseType.No) Then
                CloseTab(lTabInfo)
            Else
                ' Mark as modified since it no longer exists on disk
                lTabInfo.Modified = True
                UpdateTabLabel(lTabInfo)
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowFileDeletedNotification error: {ex.Message}")
        End Try
    End Sub
    
    ' Rename a tab when file is renamed externally
    Private Sub RenameTab(vOldPath As String, vNewPath As String)
        Try
            If Not pOpenTabs.ContainsKey(vOldPath) Then Return
            
            Dim lTabInfo As TabInfo = pOpenTabs(vOldPath)
            
            ' Update tab info
            lTabInfo.FilePath = vNewPath
            
            ' Update the tab label using the existing UpdateTabLabel method
            UpdateTabLabel(lTabInfo)
            
            ' Update dictionary
            pOpenTabs.Remove(vOldPath)
            pOpenTabs(vNewPath) = lTabInfo
            
            ' Update UI
            UpdateWindowTitle()
            
            ' Update file watcher
            If pFileSystemWatcher IsNot Nothing Then
                ' FileSystemWatcher doesn't have RemoveWatch/AddWatch methods
                ' The watcher will automatically track the renamed file
            End If
            
            ' Show notification
            Dim lStatusContext As UInteger = pStatusBar.GetContextId("rename")
            pStatusBar.Pop(lStatusContext)
            pStatusBar.Push(lStatusContext, $"File renamed: {System.IO.Path.GetFileName(vOldPath)} → {System.IO.Path.GetFileName(vNewPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"RenameTab error: {ex.Message}")
        End Try
    End Sub
    
    ' Get tab info for a file path
    Public Function GetTabInfoForFile(vFilePath As String) As TabInfo
        If pOpenTabs.ContainsKey(vFilePath) Then
            Return pOpenTabs(vFilePath)
        End If
        Return Nothing
    End Function
    
    ' Update LastSaved timestamp when saving files
    Private Sub UpdateLastSavedTimestamp(vTabInfo As TabInfo)
        Try
            If Not String.IsNullOrEmpty(vTabInfo.FilePath) AndAlso File.Exists(vTabInfo.FilePath) Then
                vTabInfo.LastSaved = File.GetLastWriteTime(vTabInfo.FilePath)
            Else
                vTabInfo.LastSaved = DateTime.Now
            End If
        Catch ex As Exception
            Console.WriteLine($"UpdateLastSavedTimestamp error: {ex.Message}")
            vTabInfo.LastSaved = DateTime.Now
        End Try
    End Sub

    
    Private Sub SetupFileSystemWatcher()
        Try
            pFileSystemWatcher = New Utilities.FileSystemWatcher(pSettingsManager)
            AddHandler pFileSystemWatcher.FileChanged, AddressOf OnExternalFileChanged
            AddHandler pFileSystemWatcher.FileDeleted, AddressOf OnExternalFileDeleted
            AddHandler pFileSystemWatcher.FileRenamed, AddressOf OnExternalFileRenamed
            
        Catch ex As Exception
            Console.WriteLine($"SetupFileSystemWatcher error: {ex.Message}")
        End Try
    End Sub
    
    
    Private Sub OnExternalFileChanged(vFilePath As String)
        Try
            ' Handle external file changes
            If pOpenTabs.ContainsKey(vFilePath) Then
                ' Show file changed notification
                Application.Invoke(Sub()
                    ShowFileChangedNotification(vFilePath)
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnExternalFileChanged error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnExternalFileDeleted(vFilePath As String)
        Try
            ' Handle external file deletion
            If pOpenTabs.ContainsKey(vFilePath) Then
                Application.Invoke(Sub()
                    ShowFileDeletedNotification(vFilePath)
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnExternalFileDeleted error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnExternalFileRenamed(vOldPath As String, vNewPath As String)
        Try
            ' Handle external file rename
            If pOpenTabs.ContainsKey(vOldPath) Then
                Application.Invoke(Sub()
                    RenameTab(vOldPath, vNewPath)
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnExternalFileRenamed error: {ex.Message}")
        End Try
    End Sub

    ' Save current file
    Private Sub OnSave(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then SaveFile(lCurrentTab)
        Catch ex As Exception
            Console.WriteLine($"OnSave error: {ex.Message}")
        End Try
    End Sub
    
    ' Quit application
    Private Sub OnQuit(vSender As Object, vArgs As EventArgs)
        Try
            ' Check for unsaved changes
            If CheckForUnsavedChanges() Then
                Application.Quit()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnQuit error: {ex.Message}")
        End Try
    End Sub

    ' Navigate to next build error
    Private Sub OnNavigateToNextError(vSender As Object, vArgs As EventArgs)
        Try
            NavigateToNextError()
        Catch ex As Exception
            Console.WriteLine($"OnNavigateToNextError error: {ex.Message}")
        End Try
    End Sub

    ' Navigate to previous build error
    Private Sub OnNavigateToPreviousError(vSender As Object, vArgs As EventArgs)
        Try
            NavigateToPreviousError()
        Catch ex As Exception
            Console.WriteLine($"NavigateToPreviousError error: {ex.Message}")
        End Try
    End Sub

    Private Sub NavigateToNextError()
        Try
            If pBuildOutputPanel IsNot Nothing Then
                ' Get current error position
                Dim lErrors = pBuildOutputPanel.GetErrors()
                If lErrors IsNot Nothing AndAlso lErrors.Count > 0 Then
                    ' Navigate to next error
                    ' This would need implementation in BuildOutputPanel
                    Console.WriteLine("Navigate to next error")
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"NavigateToNextError error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub NavigateToPreviousError()
        Try
            If pBuildOutputPanel IsNot Nothing Then
                ' Get current error position
                Dim lErrors = pBuildOutputPanel.GetErrors()
                If lErrors IsNot Nothing AndAlso lErrors.Count > 0 Then
                    ' Navigate to previous error
                    ' This would need implementation in BuildOutputPanel
                    Console.WriteLine("Navigate to previous error")
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"NavigateToPreviousError error: {ex.Message}")
        End Try
    End Sub
    
    ' Open specific file at line/column
    Private Sub OpenSpecificFile(vFilePath As String, vLine As Integer, vColumn As Integer)
        Try
            If String.IsNullOrEmpty(vFilePath) Then Return
            
            ' Open the file
            OpenFile(vFilePath)
            
            ' Navigate to line/column
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing AndAlso lEditor.FilePath = vFilePath Then
                lEditor.GoToPosition(New EditorPosition(vLine, vColumn))
                lEditor.GrabFocus()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OpenSpecificFile error: {ex.Message}")
        End Try
    End Sub

    Private Function CheckForUnsavedChanges() As Boolean
        Try
            ' Check all open tabs for unsaved changes
            For Each lTabEntry In pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                If lTabInfo.Modified Then
                    Dim lResponse As Integer = ShowQuestion(
                        "Unsaved Changes",
                        $"You have unsaved changes in '{System.IO.Path.GetFileName(lTabInfo.FilePath)}'. Do you want to save them?"
                    )
                    
                    If lResponse = CInt(ResponseType.Yes) Then
                        If Not SaveFile(lTabInfo) Then
                            Return False ' Cancel if save fails
                        End If
                    ElseIf lResponse = CInt(ResponseType.Cancel) Then
                        Return False ' Cancel the operation
                    End If
                End If
            Next
            
            Return True ' All files handled, okay to proceed
            
        Catch ex As Exception
            Console.WriteLine($"CheckForUnsavedChanges error: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Save a file through ProjectManager/SourceFileInfo system
    ''' </summary>
    Private Function SaveFile(vTabInfo As TabInfo) As Boolean
        Try
            ' Check if we need to save as
            If String.IsNullOrEmpty(vTabInfo.FilePath) OrElse vTabInfo.FilePath.StartsWith("Untitled") Then
                Return SaveFileAs(vTabInfo)
            End If
            
            ' Update content in SourceFileInfo from editor
            If vTabInfo.Editor IsNot Nothing Then
                ' Get or create SourceFileInfo
                Dim lSourceFileInfo As SourceFileInfo = Nothing
                
                If pProjectManager IsNot Nothing Then
                    lSourceFileInfo = pProjectManager.GetSourceFileInfo(vTabInfo.FilePath)
                End If
                
                If lSourceFileInfo Is Nothing Then
                    ' Create new SourceFileInfo if not in ProjectManager
                    Dim lProjectDir As String = ""
                    If pProjectManager?.CurrentProjectInfo IsNot Nothing Then
                        lProjectDir = System.IO.Path.GetDirectoryName(pProjectManager.CurrentProjectInfo.ProjectPath)
                    Else
                        lProjectDir = System.IO.Path.GetDirectoryName(vTabInfo.FilePath)
                    End If
                    
                    lSourceFileInfo = New SourceFileInfo(vTabInfo.FilePath, lProjectDir)
                    lSourceFileInfo.Content = vTabInfo.Editor.Text
                    lSourceFileInfo.TextLines = New List(Of String)(lSourceFileInfo.Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                    If lSourceFileInfo.TextLines.Count = 0 Then
                        lSourceFileInfo.TextLines.Add("")
                    End If
                    lSourceFileInfo.IsLoaded = True
                    
                    ' Register with ProjectManager
                    If pProjectManager IsNot Nothing Then
                        pProjectManager.RegisterSourceFileInfo(vTabInfo.FilePath, lSourceFileInfo)
                    End If
                Else
                    ' Update existing SourceFileInfo
                    lSourceFileInfo.Content = vTabInfo.Editor.Text
                    lSourceFileInfo.TextLines = New List(Of String)(lSourceFileInfo.Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                    If lSourceFileInfo.TextLines.Count = 0 Then
                        lSourceFileInfo.TextLines.Add("")
                    End If
                End If
                
                ' Save through SourceFileInfo
                If lSourceFileInfo.SaveContent() Then
                    ' Update tab state
                    vTabInfo.Modified = False
                    vTabInfo.Editor.IsModified = False
                    UpdateTabLabel(vTabInfo)
                    
                    ' Update LastSaved timestamp
                    UpdateLastSavedTimestamp(vTabInfo)
                    
                    ' Update UI
                    UpdateWindowTitle()
                    UpdateStatusBar($"Saved: {System.IO.Path.GetFileName(vTabInfo.FilePath)}")
                    
                    ' Mark project as dirty if it's a project file
                    If vTabInfo.IsProjectFile AndAlso pProjectManager IsNot Nothing Then
                        pProjectManager.MarkDirty()
                    End If
                    
                    Console.WriteLine($"Saved file: {vTabInfo.FilePath}")
                    Return True
                End If
            End If
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"SaveFile error: {ex.Message}")
            ShowError("Save File Error", $"Failed to save file: {ex.Message}")
            Return False
        End Try
    End Function

End Class
     