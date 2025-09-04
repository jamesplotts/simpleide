' Editors/CustomDrawingEditor.ScheduleParse.vb - Line-based parsing and formatting
Imports Gtk
Imports System
Imports System.Text
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' Track the line currently being edited
        Private pEditingLine As Integer = -1
        Private pLastEditedLine As Integer = -1

        ' Track if we're in a multi-line string for the current file
        Private pInMultiLineString As Boolean = False
        Private pMultiLineStringStartLine As Integer = -1

        ' Track deferred formatting state
        Private pDeferredFormattingEnabled As Boolean = False
        Private pFormattedLines As New HashSet(Of Integer)()
        
        ' Case correction dictionary for known identifiers
        Private pIdentifierCaseMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        
        ''' <summary>
        ''' Validates that the editor is in a consistent state
        ''' </summary>
        ''' <returns>True if the editor state is valid</returns>
        Private Function ValidateEditorState() As Boolean
            Try
                If pSourceFileInfo Is Nothing Then
                    Console.WriteLine("ValidateEditorState: pSourceFileInfo is Nothing")
                    Return False
                End If
                
                If pSourceFileInfo.TextLines Is Nothing Then
                    Console.WriteLine("ValidateEditorState: TextLines is Nothing")
                    Return False
                End If
                
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ValidateEditorState error: {ex.Message}")
                Return False
            End Try
        End Function


        ''' <summary>
        ''' Updates the identifier case map with a specific identifier
        ''' </summary>
        ''' <param name="vOldCase">The old casing of the identifier</param>
        ''' <param name="vNewCase">The new casing of the identifier</param>
        Public Sub UpdateIdentifierCaseMap(vOldCase As String, vNewCase As String)
            Try
                ' Update the dictionary
                pIdentifierCaseMap(vOldCase) = vNewCase
                
                Console.WriteLine($"Updated identifier case: {vOldCase} -> {vNewCase}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub  
        
        ''' <summary>
        ''' Handle scroll events to trigger deferred formatting
        ''' </summary>
        Private Sub OnScrollChanged()
            Try
                If Not pDeferredFormattingEnabled Then Return
                
                ' Calculate newly visible lines
                Dim lVisibleRect As Gdk.Rectangle = pDrawingArea.Allocation
                Dim lStartLine As Integer = Math.Max(0, CInt(Math.Floor(pScrollY / pLineHeight)) - 5)
                Dim lEndLine As Integer = Math.Min(pLineCount - 1, CInt(Math.Ceiling((pScrollY + lVisibleRect.Height) / pLineHeight)) + 5)
                
                
            Catch ex As Exception
                Console.WriteLine($"OnScrollChanged error: {ex.Message}")
            End Try
        End Sub

        ' ===== Helper to update the parsing completion =====

        ''' <summary>
        ''' Notifies that parsing is complete and raises the DocumentParsed event
        ''' </summary>
        ''' <remarks>
        ''' This method is called after parse results are received from ProjectManager.
        ''' It ensures the pRootNode is properly set from SourceFileInfo and raises
        ''' the DocumentParsed event for listeners like ObjectExplorer.
        ''' </remarks>
        Private Sub NotifyParsingComplete()
            Try
                ' Get the root node from SourceFileInfo if available
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.SyntaxTree IsNot Nothing Then
                    ' Update our local reference to the root node
                    pRootNode = pSourceFileInfo.SyntaxTree
                    
                    Console.WriteLine($"NotifyParsingComplete: Updated pRootNode from SourceFileInfo for {pFilePath}")
                ElseIf pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.ParseResult IsNot Nothing Then
                    ' Alternative: Get from ParseResult if SyntaxTree not set
                    pRootNode = pSourceFileInfo.ParseResult
                    
                    Console.WriteLine($"NotifyParsingComplete: Updated pRootNode from ParseResult for {pFilePath}")
                End If
                
                ' Only raise event if we have a valid root node
                If pRootNode IsNot Nothing Then
                    ' Notify listeners that parsing is complete
                    RaiseEvent DocumentParsed(pRootNode)
                    
                    Console.WriteLine($"NotifyParsingComplete: Raised DocumentParsed event for {pFilePath}")
                Else
                    Console.WriteLine($"NotifyParsingComplete: No root node available for {pFilePath}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NotifyParsingComplete error: {ex.Message}")
            End Try
        End Sub

        
    End Class
    
    ' ===== Helper Classes =====
    
    ' Token type enumeration
    Friend Enum LineTokenType
        eKeyword
        eIdentifier
        eStringLiteral
        eComment
        eOperator
        eWhitespace
        eOther
    End Enum
    
    ' Token class for line parsing
    Friend Class LineToken
        Public Property Type As LineTokenType
        Public Property Text As String
        Public Property PreserveSpacing As Boolean
        
        Public Sub New(vType As LineTokenType, vText As String, vPreserveSpacing As Boolean)
            Type = vType
            Text = vText
            PreserveSpacing = vPreserveSpacing
        End Sub
    End Class
        
    
End Namespace