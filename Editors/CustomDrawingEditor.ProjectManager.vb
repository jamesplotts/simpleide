
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax


Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Handles parse completion notification from ProjectManager
        ''' </summary>
        ''' <param name="vFile">The source file that was parsed</param>
        ''' <param name="vResult">The root SyntaxNode from the parse</param>
        ''' <remarks>
        ''' The ProjectManager passes a SyntaxNode directly, not a generic Object.
        ''' The SourceFileInfo will have updated LineMetadata and CharacterColors arrays.
        ''' </remarks>
        Private Sub OnProjectManagerParseCompleted(vFile As SourceFileInfo, vResult As SyntaxNode)
            Try
                ' Verify this is for our file
                If vFile Is Nothing OrElse vFile IsNot pSourceFileInfo Then
                    Return
                End If
                
                Console.WriteLine($"CustomDrawingEditor: ParseCompleted received for {pFilePath}")
                
                ' Update the root node from the parse result
                If vResult IsNot Nothing Then
                    pRootNode = vResult
                    Console.WriteLine($"CustomDrawingEditor: Updated pRootNode from parse result")
                    Console.WriteLine($"  Root node type: {pRootNode.NodeType}")
                    Console.WriteLine($"  Child count: {If(pRootNode.Children?.Count, 0)}")
                End If
                
                ' The SourceFileInfo should now have updated LineMetadata and CharacterColors
                ' Verify the updates
                If pSourceFileInfo.LineMetadata IsNot Nothing Then
                    Console.WriteLine($"CustomDrawingEditor: LineMetadata updated with {pSourceFileInfo.LineMetadata.Length} lines")
                    
                    ' Check if we have syntax tokens
                    Dim lTokenCount As Integer = 0
                    for each lMetadata in pSourceFileInfo.LineMetadata
                        If lMetadata?.SyntaxTokens IsNot Nothing Then
                            lTokenCount += lMetadata.SyntaxTokens.Count
                        End If
                    Next
                    Console.WriteLine($"  Total syntax tokens: {lTokenCount}")
                End If
                
                If pSourceFileInfo.CharacterColors IsNot Nothing Then
                    Console.WriteLine($"CustomDrawingEditor: CharacterColors updated with {pSourceFileInfo.CharacterColors.Length} lines")
                End If
                
                ' Notify that parsing is complete (raises DocumentParsed event for Object Explorer)
                NotifyParsingComplete()
                
                ' Queue redraw to show the updated syntax highlighting
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"CustomDrawingEditor: Redraw queued for {pFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectManagerParseCompleted error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Ensures ProjectManager is available by requesting it via event
        ''' </summary>
        ''' <returns>True if ProjectManager is available, False otherwise</returns>
        ''' <remarks>
        ''' Used during initialization to get ProjectManager reference from MainWindow
        ''' </remarks>
        Private Function EnsureProjectManager() As Boolean
            Try
                ' If we already have it, we're good
                If pProjectManager IsNot Nothing Then Return True
                
                ' Request it via event from MainWindow
                Dim lEventArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                
                If lEventArgs.HasProjectManager Then
                    ' Use the property setter to properly subscribe to events
                    ProjectManager = lEventArgs.ProjectManager
                    Return True
                End If
                
                Console.WriteLine("EnsureProjectManager: No ProjectManager provided")
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"EnsureProjectManager error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Add: SimpleIDE.Editors.CustomDrawingEditor.ForceRecolorization
        ' To: CustomDrawingEditor.ProjectManager.vb
        ''' <summary>
        ''' Forces re-colorization of the current file from parsed tokens
        ''' </summary>
        ''' <remarks>
        ''' This method requests the ProjectManager to re-apply theme colors
        ''' to the existing parsed tokens, updating the CharacterColors array.
        ''' Useful when the editor is first shown or theme changes.
        ''' </remarks>
        Public Sub ForceRecolorization()
            Try
                ' Only proceed if we have a source file and project manager
                If pSourceFileInfo Is Nothing OrElse pProjectManager Is Nothing Then
                    Console.WriteLine($"ForceRecolorization: Missing SourceFileInfo or ProjectManager")
                    Return
                End If
                
                ' Check if the file has been parsed (has LineMetadata with tokens)
                If pSourceFileInfo.LineMetadata Is Nothing Then
                    Console.WriteLine($"ForceRecolorization: No LineMetadata - requesting parse")
                    ' No metadata yet, request a parse which will include colorization
                    pSourceFileInfo.RequestAsyncParse()
                    Return
                End If
                
                ' Check if we have any tokens to colorize
                Dim lHasTokens As Boolean = False
                for each lMetadata in pSourceFileInfo.LineMetadata
                    If lMetadata?.SyntaxTokens IsNot Nothing AndAlso lMetadata.SyntaxTokens.Count > 0 Then
                        lHasTokens = True
                        Exit for
                    End If
                Next
                
                If Not lHasTokens Then
                    Console.WriteLine($"ForceRecolorization: No syntax tokens found - requesting parse")
                    ' No tokens, need to parse first
                    pSourceFileInfo.RequestAsyncParse()
                    Return
                End If
                
                Console.WriteLine($"ForceRecolorization: Requesting color update for {pFilePath}")
                
                ' We have tokens, just need to reapply colors
                ' Request the ProjectManager to update colors for this file
                pProjectManager.UpdateFileColors(pSourceFileInfo)
                
                ' Queue a redraw to show the new colors
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ForceRecolorization: Color update requested and redraw queued")
                
            Catch ex As Exception
                Console.WriteLine($"ForceRecolorization error: {ex.Message}")
            End Try
        End Sub


        
    End Class
    
End Namespace
