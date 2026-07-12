' Editors/CustomDrawingEditor.Folding.vb - Code folding implementation
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Syntax
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Private Fields =====
        Private pVisualLineMap As New List(Of Integer)
        Private pIsLineHidden() As Boolean
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Rebuilds the map between visual lines and source lines based on folding state
        ''' </summary>
        Public Sub RebuildVisualLineMap()
            Try
                If pSourceFileInfo Is Nothing OrElse pSourceFileInfo.LineCount = 0 Then
                    pVisualLineMap.Clear()
                    pVisualLineMap.Add(0)
                    Return
                End If
                
                Dim lLineCount As Integer = pSourceFileInfo.LineCount
                ReDim pIsLineHidden(lLineCount - 1)
                
                ' Reset hidden state
                Array.Clear(pIsLineHidden, 0, lLineCount)
                
                ' Mark hidden lines based on syntax tree
                If pRootNode IsNot Nothing Then
                    MarkHiddenLinesRecursive(pRootNode)
                End If
                
                ' Build the map
                pVisualLineMap.Clear()
                For i As Integer = 0 To lLineCount - 1
                    If Not pIsLineHidden(i) Then
                        pVisualLineMap.Add(i)
                    End If
                Next
                
                ' Ensure at least one line is visible
                If pVisualLineMap.Count = 0 Then
                    pVisualLineMap.Add(0)
                End If
                
                ' Safety check: If we have lines but map is effectively empty (only 0), and we shouldn't be hidden
                ' This prevents the "single line map" bug
                If pVisualLineMap.Count = 1 AndAlso lLineCount > 1 Then
                     ' Check if we really should be hidden (e.g. root is collapsed?)
                     ' If not, force full map
                     Dim lShouldBeHidden As Boolean = False
                     If pIsLineHidden IsNot Nothing AndAlso pIsLineHidden.Length > 1 Then
                         If pIsLineHidden(1) Then lShouldBeHidden = True
                     End If
                     
                     If Not lShouldBeHidden Then
                        pVisualLineMap.Clear()
                        For i As Integer = 0 To lLineCount - 1
                            pVisualLineMap.Add(i)
                        Next
                     End If
                End If
                
                ' Update scrollbars since visual line count changed
                UpdateScrollbars()
                
                ' Queue redraw
                If pDrawingArea IsNot Nothing Then pDrawingArea.QueueDraw()
                If pLineNumberWidget IsNot Nothing Then pLineNumberWidget.QueueDraw()
                
                ' DEBUG LOGGING
                Try
                    Using writer As New System.IO.StreamWriter("/home/jamesp/.gemini/debug_folding.log", True)
                        writer.WriteLine($"[{DateTime.Now}] RebuildVisualLineMap: Count={pVisualLineMap.Count}")
                        For i As Integer = 0 To Math.Min(10, pVisualLineMap.Count - 1)
                            writer.WriteLine($"  Map[{i}] = {pVisualLineMap(i)}")
                        Next
                    End Using
                Catch
                End Try
                
            Catch ex As Exception
                Console.WriteLine($"RebuildVisualLineMap error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Converts a visual line index to a source line index
        ''' </summary>
        Public Function VisualToSourceLine(vVisualLine As Integer) As Integer
            If pVisualLineMap Is Nothing OrElse pVisualLineMap.Count = 0 Then Return vVisualLine
            
            If vVisualLine < 0 Then Return 0
            If vVisualLine >= pVisualLineMap.Count Then Return pVisualLineMap(pVisualLineMap.Count - 1)
            
            Return pVisualLineMap(vVisualLine)
        End Function
        
        ''' <summary>
        ''' Converts a source line index to a visual line index
        ''' </summary>
        ''' <returns>Visual line index, or -1 if the line is hidden</returns>
        Public Function SourceToVisualLine(vSourceLine As Integer) As Integer
            If pVisualLineMap Is Nothing OrElse pVisualLineMap.Count = 0 Then Return vSourceLine
            
            ' If hidden, return -1
            If pIsLineHidden IsNot Nothing AndAlso vSourceLine < pIsLineHidden.Length AndAlso pIsLineHidden(vSourceLine) Then
                Return -1
            End If
            
            ' Binary search for the line
            Dim lIndex As Integer = pVisualLineMap.BinarySearch(vSourceLine)
            If lIndex >= 0 Then
                Return lIndex
            Else
                ' Should not happen if line is not hidden
                Return -1
            End If
        End Function
        
        ''' <summary>
        ''' Gets the total number of visible lines
        ''' </summary>
        Public Function GetVisualLineCount() As Integer
            If pVisualLineMap Is Nothing Then Return 0
            Return pVisualLineMap.Count
        End Function
        
        ''' <summary>
        ''' Toggles the fold state of a node
        ''' </summary>
        Public Sub ToggleFold(vNode As SyntaxNode)
            If vNode IsNot Nothing AndAlso vNode.IsFoldable Then
                vNode.IsExpanded = Not vNode.IsExpanded
                
                ' Update persistence state
                If pSourceFileInfo IsNot Nothing Then
                    Dim lPath As String = GetNodePath(vNode)
                    If pSourceFileInfo.FoldingState.ContainsKey(lPath) Then
                        pSourceFileInfo.FoldingState(lPath) = vNode.IsExpanded
                    Else
                        pSourceFileInfo.FoldingState.Add(lPath, vNode.IsExpanded)
                    End If
                End If
                
                RebuildVisualLineMap()
            End If
        End Sub
        
        ''' <summary>
        ''' Finds the foldable node at the specified line
        ''' </summary>
        Public Function GetFoldableNodeAtLine(vLine As Integer) As SyntaxNode
            If pRootNode Is Nothing Then Return Nothing
            Return FindFoldableNodeRecursive(pRootNode, vLine)
        End Function
        
        ' ===== Private Helper Methods =====
        
        Private Sub MarkHiddenLinesRecursive(vNode As SyntaxNode)
            If vNode Is Nothing Then Return
            
            ' If this node is collapsed, hide its content
            If vNode.IsFoldable AndAlso Not vNode.IsExpanded Then
                Dim lStart As Integer = vNode.StartLine + 1 ' Keep definition line visible
                Dim lEnd As Integer = vNode.EndLine
                
                If lStart <= lEnd Then
                    For i As Integer = lStart To lEnd
                        If i < pIsLineHidden.Length Then
                            pIsLineHidden(i) = True
                        End If
                    Next
                End If
                
                ' Don't recurse into children of collapsed nodes (they are already hidden)
                Return
            End If
            
            ' Recurse for children
            If vNode.Children IsNot Nothing Then
                For Each lChild As SyntaxNode In vNode.Children
                    MarkHiddenLinesRecursive(lChild)
                Next
            End If
        End Sub
        
        Private Function FindFoldableNodeRecursive(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            
            ' Check if this node starts on the requested line and is foldable
            If vNode.StartLine = vLine AndAlso vNode.IsFoldable Then
                Return vNode
            End If
            
            ' Check children
            If vNode.Children IsNot Nothing Then
                For Each lChild As SyntaxNode In vNode.Children
                    ' Optimization: Only check children if the line is within the child's range (or close to it)
                    ' But since we are looking for StartLine match, we can just check all children or use binary search if sorted
                    ' For now, simple iteration
                    Dim lResult As SyntaxNode = FindFoldableNodeRecursive(lChild, vLine)
                    If lResult IsNot Nothing Then Return lResult
                Next
            End If
            
            Return Nothing
        End Function
        
        ''' <summary>
        ''' Applies saved folding state to the current syntax tree
        ''' </summary>
        Public Sub ApplyFoldingState()
            If pRootNode Is Nothing OrElse pSourceFileInfo Is Nothing Then Return
            RestoreFoldingStateRecursive(pRootNode)
        End Sub
        
        Private Sub RestoreFoldingStateRecursive(vNode As SyntaxNode)
            If vNode Is Nothing Then Return
            
            ' Apply state if exists
            If vNode.IsFoldable Then
                Dim lPath As String = GetNodePath(vNode)
                If pSourceFileInfo.FoldingState.ContainsKey(lPath) Then
                    vNode.IsExpanded = pSourceFileInfo.FoldingState(lPath)
                End If
            End If
            
            ' Recurse
            If vNode.Children IsNot Nothing Then
                For Each lChild As SyntaxNode In vNode.Children
                    RestoreFoldingStateRecursive(lChild)
                Next
            End If
        End Sub
        
        ''' <summary>
        ''' Generates a unique path for a node to persist its state
        ''' </summary>
        Private Function GetNodePath(vNode As SyntaxNode) As String
            If vNode Is Nothing Then Return ""
            
            Dim lPath As String = $"{vNode.NodeType}:{vNode.Name}"
            
            Dim lParent As SyntaxNode = vNode.Parent
            While lParent IsNot Nothing AndAlso lParent.NodeType <> CodeNodeType.eFile
                lPath = $"{lParent.NodeType}:{lParent.Name}/{lPath}"
                lParent = lParent.Parent
            End While
            
            Return lPath
        End Function
        
    End Class
End Namespace
