
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax


Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Helper Methods =====

        ''' <summary>
        ''' Refreshes syntax highlighting and visual state from the editor's own SourceFileInfo
        ''' </summary>
        ''' <remarks>
        ''' Used when this file's SyntaxTree/LineMetadata were updated outside of the normal
        ''' ProjectManager.ParseCompleted notification path - e.g. the initial project-wide
        ''' background parse (ProjectManager.ParseAllFilesAsync), which updates SourceFileInfo
        ''' directly without raising ParseCompleted for every file (that event also drives
        ''' expensive full-tree rebuilds in CodeSenseEngine/ObjectExplorer, which would be far
        ''' too costly to run once per file during a whole-project parse). Reuses
        ''' OnProjectManagerParseCompleted so there is a single source of truth for what
        ''' "the editor's data just got (re)parsed" means
        ''' </remarks>
        Public Sub RefreshFromParsedSourceFile()
            OnProjectManagerParseCompleted(pSourceFileInfo, pSourceFileInfo?.SyntaxTree)
        End Sub

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
                
                #If DEBUG Then
                Console.WriteLine($"CustomDrawingEditor: ParseCompleted received for {pFilePath}")
                #End If
                
                ' Update the root node from the parse result
                If vResult IsNot Nothing Then
                    pRootNode = vResult
                    #If DEBUG Then
                    Console.WriteLine($"CustomDrawingEditor: Updated pRootNode from parse result")
                    #End If
                    #If DEBUG Then
                    Console.WriteLine($"  Root node type: {pRootNode.NodeType}")
                    #End If
                    #If DEBUG Then
                    Console.WriteLine($"  Child count: {If(pRootNode.Children?.Count, 0)}")
                    #End If
                End If
                
                ' The SourceFileInfo should now have updated LineMetadata and CharacterColors
                ' Verify the updates
                If pSourceFileInfo.LineMetadata IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"CustomDrawingEditor: LineMetadata updated with {pSourceFileInfo.LineMetadata.Length} lines")
                    #End If
                    
                    ' Check if we have syntax tokens
                    Dim lTokenCount As Integer = 0
                    for each lMetadata in pSourceFileInfo.LineMetadata
                        If lMetadata?.SyntaxTokens IsNot Nothing Then
                            lTokenCount += lMetadata.SyntaxTokens.Count
                        End If
                    Next
                    #If DEBUG Then
                    Console.WriteLine($"  Total syntax tokens: {lTokenCount}")
                    #End If
                End If
                
                ' Notify that parsing is complete (raises DocumentParsed event for Object Explorer)
                NotifyParsingComplete()

                ' Restore folding state from persistence
                ApplyFoldingState()

                ' Rebuild visual line map with the new syntax tree - without this, fold nodes
                ' from before the edit (and their gutter icons) keep being used until something
                ' else happens to trigger a rebuild
                RebuildVisualLineMap()

                ' Queue redraw to show the updated syntax highlighting
                pDrawingArea?.QueueDraw()
                
                #If DEBUG Then
                Console.WriteLine($"CustomDrawingEditor: Redraw queued for {pFilePath}")
                #End If
                
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
                
                #If DEBUG Then
                Console.WriteLine("EnsureProjectManager: No ProjectManager provided")
                #End If
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"EnsureProjectManager error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Initializes the ProjectManager connection for both editor and SourceFileInfo
        ''' </summary>
        ''' <remarks>
        ''' Should be called during initialization to ensure proper syntax highlighting
        ''' </remarks>
        Private Sub InitializeProjectManagerConnection()
            Try
                ' First ensure the editor has ProjectManager
                If Not EnsureProjectManager() Then
                    #If DEBUG Then
                    Console.WriteLine("InitializeProjectManagerConnection: Failed to get ProjectManager for editor")
                    #End If
                    Return
                End If
                
                ' Now ensure SourceFileInfo also has it
                If pSourceFileInfo IsNot Nothing Then
                    ' Set the ProjectManager directly if we have it
                    If pProjectManager IsNot Nothing Then
                        pSourceFileInfo.ProjectManager = pProjectManager
                        #If DEBUG Then
                        Console.WriteLine("InitializeProjectManagerConnection: Connected SourceFileInfo to ProjectManager")
                        #End If
                    Else
                        ' Try through the event mechanism
                        If pSourceFileInfo.EnsureProjectManagerConnection() Then
                            #If DEBUG Then
                            Console.WriteLine("InitializeProjectManagerConnection: SourceFileInfo connected via event")
                            #End If
                        Else
                            #If DEBUG Then
                            Console.WriteLine("InitializeProjectManagerConnection: Failed to connect SourceFileInfo to ProjectManager")
                            #End If
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeProjectManagerConnection error: {ex.Message}")
            End Try
        End Sub


        
    End Class
    
End Namespace
