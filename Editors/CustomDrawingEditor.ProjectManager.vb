
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Events for ProjectManager Access =====
        
        ''' <summary>
        ''' Raised when the editor needs access to the ProjectManager
        ''' </summary>
        Public Event ProjectManagerRequested(sender As Object, e As ProjectManagerRequestEventArgs)
        
        ' ===== Helper Methods =====

        Private Function ProjectManager As ProjectManager
            Return GetProjectManager()
        End Function        


        ''' <summary>
        ''' Gets the ProjectManager instance via event
        ''' </summary>
        ''' <returns>The ProjectManager if available, Nothing otherwise</returns>
        Private Function GetProjectManager() As ProjectManager
            Try
                Dim lEventArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                
                If lEventArgs.HasProjectManager Then
                    Return lEventArgs.ProjectManager
                End If
                
                Console.WriteLine("GetProjectManager: No ProjectManager provided via event")
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectManager error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Subscribe to ProjectManager parse events using event-based access
        ''' </summary>
        Private Sub SubscribeToProjectParserViaEvent()
            Try
                Dim lProjectManager As ProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Console.WriteLine("SubscribeToProjectParserViaEvent: ProjectManager not available")
                    Return
                End If
                
                ' Subscribe to parse completed event
                RemoveHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                AddHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                
                Console.WriteLine("CustomDrawingEditor subscribed to ProjectManager.ParseCompleted via event")
                
            Catch ex As Exception
                Console.WriteLine($"SubscribeToProjectParserViaEvent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Request a parse from the centralized ProjectManager using event-based access
        ''' </summary>
        Private Sub RequestParseFromProjectManagerViaEvent()
            Try
                Dim lProjectManager As ProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Console.WriteLine("RequestParseFromProjectManagerViaEvent: ProjectManager not available")
                    Return
                End If
                
                ' Update SourceFileInfo with current content
                If pSourceFileInfo IsNot Nothing Then
                    pSourceFileInfo.Content = GetAllText()
                    pSourceFileInfo.TextLines.Clear()
                    for i As Integer = 0 To pLineCount - 1
                        pSourceFileInfo.TextLines.Add(GetLineText(i))
                    Next
                    
                    ' Request parse through ProjectManager
                    Console.WriteLine($"Requesting parse for {pSourceFileInfo.FileName}")
                    lProjectManager.ParseFile(pSourceFileInfo)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RequestParseFromProjectManagerViaEvent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Request a parse of the current document
        ''' Called by external components (like ObjectExplorer) to trigger parsing
        ''' </summary>
        ''' <remarks>
        ''' Uses centralized ProjectManager.Parser instead of local parsing
        ''' </remarks>
        Public Sub RequestParseViaEvent() Implements IEditor.RequestParse
            Try
                Console.WriteLine("RequestParseViaEvent called - using event-based ProjectManager access")
                
                Dim lProjectManager As ProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Console.WriteLine("RequestParseViaEvent: ProjectManager not available")
                    Return
                End If
                
                ' Ensure SourceFileInfo is current
                If pSourceFileInfo IsNot Nothing Then
                    ' Update content in SourceFileInfo
                    pSourceFileInfo.Content = GetAllText()
                    pSourceFileInfo.TextLines.Clear()
                    for i As Integer = 0 To pLineCount - 1
                        pSourceFileInfo.TextLines.Add(GetLineText(i))
                    Next
                    
                    ' Request parse through ProjectManager
                    Console.WriteLine($"Requesting parse for {pSourceFileInfo.FileName} via event")
                    lProjectManager.ParseFile(pSourceFileInfo)
                Else
                    Console.WriteLine("RequestParseViaEvent: No SourceFileInfo available")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RequestParseViaEvent error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
