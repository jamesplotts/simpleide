' MainWindow.IdentifierSync.vb - Project-wide identifier case synchronization
Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports System.Text.RegularExpressions
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    
    ' ===== Private Fields for Identifier Sync =====
    Private pIsUpdatingIdentifiers As Boolean = False
    
    ' ===== Event Handlers =====
    
    ' Handle identifier case changed in editor
    Private Sub OnEditorIdentifierCaseChanged(vOldCase As String, vNewCase As String, vScope As CustomDrawingEditor.IdentifierScope)
        Try
            ' Skip if already updating to prevent recursion
            If pIsUpdatingIdentifiers Then Return
            
            ' Set flag
            pIsUpdatingIdentifiers = True
            
            ' Update all open editors
            UpdateIdentifierCaseInOpenEditors(vOldCase, vNewCase, vScope)
            
            ' Update project-wide identifier map through ProjectManager
            UpdateProjectIdentifierMap(vOldCase, vNewCase, vScope)
            
            ' Update all project files in background
            UpdateIdentifierCaseInProjectFiles(vOldCase, vNewCase, vScope)
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorIdentifierCaseChanged error: {ex.Message}")
        Finally
            pIsUpdatingIdentifiers = False
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    ' Update identifier case in all open editors
    Private Sub UpdateIdentifierCaseInOpenEditors(vOldCase As String, vNewCase As String, vScope As CustomDrawingEditor.IdentifierScope)
        Try
            ' Process each open tab
            For Each lTabEntry In pOpenTabs
                Dim lTab As TabInfo = lTabEntry.Value
                
                ' Skip if no editor
                If lTab.Editor Is Nothing Then Continue For
                
                ' Get CustomDrawingEditor
                Dim lEditor As CustomDrawingEditor = TryCast(lTab.Editor, CustomDrawingEditor)
                If lEditor Is Nothing Then Continue For
                
                ' Update identifier case map
                lEditor.UpdateIdentifierCaseMap(vOldCase, vNewCase)
                
                ' Force repaint
                lEditor.QueueDraw()
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateIdentifierCaseInOpenEditors error: {ex.Message}")
        End Try
    End Sub
    
    ' Update project-wide identifier map through ProjectManager
    Private Sub UpdateProjectIdentifierMap(vOldCase As String, vNewCase As String, vScope As CustomDrawingEditor.IdentifierScope)
        Try
            ' Skip if no project manager or project
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then Return
            
            ' Update identifier map
            pProjectManager.UpdateIdentifierCase(vOldCase, vNewCase)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateProjectIdentifierMap error: {ex.Message}")
        End Try
    End Sub
    
    ' Update identifier case in all project files
    Private Async Sub UpdateIdentifierCaseInProjectFiles(vOldCase As String, vNewCase As String, vScope As CustomDrawingEditor.IdentifierScope)
        Try
            ' Skip if no project manager or project
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then Return
            
            ' Get project directory
            Dim lProjectDir As String = pProjectManager.CurrentProjectDirectory
            If String.IsNullOrEmpty(lProjectDir) Then Return
            
            ' Get all VB files in project
            Dim lFiles As List(Of String) = pProjectManager.GetProjectInfo(pProjectManager.CurrentProjectPath)?.SourceFiles
            If lFiles Is Nothing OrElse lFiles.Count = 0 Then Return
            
            ' Update each file
            Dim lUpdatedFiles As New List(Of String)
            
            For Each lFile As String In lFiles
                ' Skip non-VB files
                If Not lFile.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then Continue For
                
                ' Get full path
                Dim lFilePath As String = System.IO.Path.Combine(lProjectDir, lFile)
                
                ' Skip if file doesn't exist
                If Not File.Exists(lFilePath) Then Continue For
                
                ' Skip if file is currently open (already updated)
                If IsFileOpen(lFilePath) Then Continue For
                
                ' Read file content
                Dim lContent As String = Await Task.Run(Function() File.ReadAllText(lFilePath))
                Dim lOriginalContent As String = lContent
                
                ' Update identifier case
                lContent = UpdateIdentifierCaseInContent(lContent, vOldCase, vNewCase)
                
                ' Save if changed
                If lContent <> lOriginalContent Then
                    Await Task.Run(Sub() File.WriteAllText(lFilePath, lContent))
                    lUpdatedFiles.Add(lFilePath)
                End If
            Next
            
            ' Show status if files were updated
            If lUpdatedFiles.Count > 0 Then
                Gtk.Application.Invoke(Sub()
                    UpdateStatusBar($"updated Identifier '{vNewCase}' in {lUpdatedFiles.Count} file(s)")
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateIdentifierCaseInProjectFiles error: {ex.Message}")
        End Try
    End Sub
    
    ' Update identifier case in text content
    Private Function UpdateIdentifierCaseInContent(vContent As String, vOldCase As String, vNewCase As String) As String
        Try
            ' Skip if cases are the same
            If vOldCase.Equals(vNewCase, StringComparison.Ordinal) Then
                Return vContent
            End If
            
            ' Create regex pattern for whole word matching
            Dim lPattern As String = $"\b{Regex.Escape(vOldCase)}\b"
            Dim lRegex As New Regex(lPattern, RegexOptions.IgnoreCase)
            
            ' Replace with custom evaluator to preserve context
            Return lRegex.Replace(vContent, Function(match As Match)
                ' Check if match is in a string or comment
                Dim lLineStart As Integer = vContent.LastIndexOf(Environment.NewLine, match.Index)
                If lLineStart < 0 Then lLineStart = 0
                
                Dim lLineEnd As Integer = vContent.IndexOf(Environment.NewLine, match.Index)
                If lLineEnd < 0 Then lLineEnd = vContent.Length
                
                Dim lLine As String = vContent.Substring(lLineStart, lLineEnd - lLineStart)
                Dim lPosInLine As Integer = match.Index - lLineStart
                
                ' Skip if in string
                If IsInsideString(lLine, lPosInLine) Then
                    Return match.Value
                End If
                
                ' Skip if in comment
                If IsInsideComment(lLine, lPosInLine) Then
                    Return match.Value
                End If
                
                ' Replace with new case
                Return vNewCase
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateIdentifierCaseInContent error: {ex.Message}")
            Return vContent
        End Try
    End Function
    
    ' Check if position is inside a string
    Private Function IsInsideString(vLine As String, vPosition As Integer) As Boolean
        Try
            Dim lInString As Boolean = False
            Dim lEscapeNext As Boolean = False
            
            For i As Integer = 0 To Math.Min(vPosition - 1, vLine.Length - 1)
                Dim lChar As Char = vLine(i)
                
                If lEscapeNext Then
                    lEscapeNext = False
                    Continue For
                End If
                
                If lChar = "\"c Then
                    lEscapeNext = True
                ElseIf lChar = """"c Then
                    lInString = Not lInString
                End If
            Next
            
            Return lInString
            
        Catch ex As Exception
            Return False
        End Try
    End Function
    
    ' Check if position is inside a comment
    Private Function IsInsideComment(vLine As String, vPosition As Integer) As Boolean
        Try
            ' Find comment start
            Dim lCommentPos As Integer = vLine.IndexOf("'"c)
            
            ' Check if position is after comment start
            Return lCommentPos >= 0 AndAlso vPosition >= lCommentPos
            
        Catch ex As Exception
            Return False
        End Try
    End Function
    
    ' Check if a file is currently open in editor
    Private Function IsFileOpen(vFilePath As String) As Boolean
        Try
            For Each lTabEntry In pOpenTabs
                Dim lTab As TabInfo = lTabEntry.Value
                If lTab?.FilePath IsNot Nothing AndAlso 
                   lTab.FilePath.Equals(vFilePath, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"IsFileOpen error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Initialize identifier case for a new editor
    Private Sub InitializeEditorIdentifierCase(vEditor As CustomDrawingEditor)
        Try
            ' Skip if no project manager or project
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then Return
            
            ' Get identifier map from project manager
            Dim lIdentifierMap As Dictionary(Of String, String) = pProjectManager.GetIdentifierCaseMap()
            
            ' Load identifier cases into editor
            For Each kvp In lIdentifierMap
                vEditor.UpdateIdentifierCaseMap(kvp.key, kvp.Value)
            Next
            
            ' Subscribe to case change events
            AddHandler vEditor.IdentifierCaseChanged, AddressOf OnEditorIdentifierCaseChanged
            
        Catch ex As Exception
            Console.WriteLine($"InitializeEditorIdentifierCase error: {ex.Message}")
        End Try
    End Sub
    
End Class
