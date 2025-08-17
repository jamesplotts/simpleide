' CustomDrawingEditor.Capitalization.vb  Enhanced CustomDrawingEditor Events and Methods
Imports System


Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== New Events for Classic VB Capitalization =====
        
        Public Event IdentifierTyped(sender As Object, e As IdentifierTypedEventArgs)
        Public Event DeclarationDetected(sender As Object, e As DeclarationDetectedEventArgs)
        
        ' ===== Event Args Classes =====
        
        Public Class IdentifierTypedEventArgs
            Inherits EventArgs
            
            Public Property Identifier As String
            Public Property Line As Integer
            Public Property Column As Integer
            Public Property StartColumn As Integer
            Public Property EndColumn As Integer
            
            Public Sub New(vIdentifier As String, vLine As Integer, vColumn As Integer, vStartCol As Integer, vEndCol As Integer)
                Identifier = vIdentifier
                Line = vLine
                Column = vColumn
                StartColumn = vStartCol
                EndColumn = vEndCol
            End Sub
        End Class
        
        Public Class DeclarationDetectedEventArgs
            Inherits EventArgs
            
            Public Property Identifier As String
            Public Property Line As Integer
            Public Property DeclarationType As String ' "Sub", "Function", "Dim", "Class", etc.
            
            Public Sub New(vIdentifier As String, vLine As Integer, vType As String)
                Identifier = vIdentifier
                Line = vLine
                DeclarationType = vType
            End Sub
        End Class
        
        ' ===== Enhanced InsertCharacter Method =====
        
        Private Sub InsertCharacter(vChar As Char)
            Try
                If pIsReadOnly Then Return
                
                ' Ensure we have a valid line
                If pCursorLine >= pLineCount Then
                    While pLineCount <= pCursorLine
                        AddNewLine("")
                    End While
                End If
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordInsertChar(pCursorLine, pCursorColumn, vChar, pCursorLine, pCursorColumn + 1)
                End If
                
                ' Insert character
                Dim lLine As String = pTextLines(pCursorLine)
                If pCursorColumn <= lLine.Length Then
                    pTextLines(pCursorLine) = lLine.Insert(pCursorColumn, vChar)
                Else
                    pTextLines(pCursorLine) = lLine.PadRight(pCursorColumn) & vChar
                End If
                
                pLineMetadata(pCursorLine).MarkChanged()
                
                ' Move cursor
                SetCursorPosition(pCursorLine, pCursorColumn + 1)
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' ===== NEW: Check for identifier completion =====
                CheckForIdentifierCompletion(vChar)
                
                ' ===== NEW: Check for declaration =====
                CheckForDeclaration()
                
                ' Update display
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacter error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== New Helper Methods =====
        
        ''' <summary>
        ''' Check if we just completed typing an identifier
        ''' </summary>
        Private Sub CheckForIdentifierCompletion(vChar As Char)
            Try
                ' Check if character ends an identifier (space, punctuation, etc.)
                If Not IsIdentifierBoundary(vChar) Then Return
                
                ' Get the word we just finished typing
                Dim lIdentifierInfo = GetIdentifierAtPosition(pCursorLine, pCursorColumn - 1)
                If lIdentifierInfo.Identifier IsNot Nothing AndAlso lIdentifierInfo.Identifier.Length > 1 Then
                    ' Fire event for capitalization manager
                    RaiseEvent IdentifierTyped(Me, New IdentifierTypedEventArgs(
                        lIdentifierInfo.Identifier, pCursorLine, pCursorColumn - 1,
                        lIdentifierInfo.StartColumn, lIdentifierInfo.EndColumn))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CheckForIdentifierCompletion error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Check if current line contains a declaration
        ''' </summary>
        Private Sub CheckForDeclaration()
            Try
                Dim lLine As String = pTextLines(pCursorLine).Trim()
                
                ' Simple declaration patterns
                Dim lDeclarationPatterns As String() = {
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Sub\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Function\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Property\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Class\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Module\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Interface\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Structure\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Enum\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*(?:ReadOnly)?\s*Dim\s+(\w+)"
                }
                
                For Each lPattern In lDeclarationPatterns
                    Dim lMatch As System.Text.RegularExpressions.Match = 
                        System.Text.RegularExpressions.Regex.Match(lLine, lPattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    
                    If lMatch.Success Then
                        Dim lIdentifier As String = lMatch.Groups(1).Value
                        Dim lType As String = ExtractDeclarationType(lPattern)
                        
                        ' Fire declaration event
                        RaiseEvent DeclarationDetected(Me, New DeclarationDetectedEventArgs(lIdentifier, pCursorLine, lType))
                        Exit For
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CheckForDeclaration error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Replace identifier at specific position with corrected case
        ''' </summary>
        Public Sub ReplaceIdentifierAt(vLine As Integer, vColumn As Integer, vOldIdentifier As String, vNewIdentifier As String)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                Dim lIdentifierInfo = GetIdentifierAtPosition(vLine, vColumn)
                
                If lIdentifierInfo.Identifier = vOldIdentifier Then
                    ' Replace with correct case
                    Dim lNewLine As String = lLine.Remove(lIdentifierInfo.StartColumn, vOldIdentifier.Length)
                    lNewLine = lNewLine.Insert(lIdentifierInfo.StartColumn, vNewIdentifier)
                    
                    pTextLines(vLine) = lNewLine
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Adjust cursor if needed
                    If pCursorLine = vLine AndAlso pCursorColumn > lIdentifierInfo.StartColumn Then
                        Dim lDiff As Integer = vNewIdentifier.Length - vOldIdentifier.Length
                        SetCursorPosition(pCursorLine, pCursorColumn + lDiff)
                    End If
                    
                    ' Mark as modified and redraw
                    IsModified = True
                    pDrawingArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceIdentifierAt error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Update all instances of an identifier in the current document
        ''' </summary>
        Public Sub UpdateIdentifierCase(vCanonicalCase As String)
            Try
                For i As Integer = 0 To pLineCount - 1
                    Dim lLine As String = pTextLines(i)
                    Dim lUpdated As Boolean = False
                    
                    ' Use regex to find all identifier instances (word boundaries)
                    Dim lPattern As String = "\b" & System.Text.RegularExpressions.Regex.Escape(vCanonicalCase) & "\b"
                    Dim lMatches As System.Text.RegularExpressions.MatchCollection = 
                        System.Text.RegularExpressions.Regex.Matches(lLine, lPattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    
                    ' Replace from right to left to avoid position shifts
                    For j As Integer = lMatches.Count - 1 To 0 Step -1
                        Dim lMatch As System.Text.RegularExpressions.Match = lMatches(j)
                        If lMatch.Value <> vCanonicalCase Then
                            lLine = lLine.Remove(lMatch.Index, lMatch.Length)
                            lLine = lLine.Insert(lMatch.Index, vCanonicalCase)
                            lUpdated = True
                        End If
                    Next
                    
                    If lUpdated Then
                        pTextLines(i) = lLine
                        pLineMetadata(i).MarkChanged()
                        IsModified = True
                    End If
                Next
                
                If IsModified Then
                    pDrawingArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCase error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get identifier at specific position with boundaries
        ''' </summary>
        Private Function GetIdentifierAtPosition(vLine As Integer, vColumn As Integer) As (Identifier As String, StartColumn As Integer, EndColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then 
                    Return (Nothing, 0, 0)
                End If
                
                Dim lLine As String = pTextLines(vLine)
                If vColumn < 0 OrElse vColumn >= lLine.Length Then 
                    Return (Nothing, 0, 0)
                End If
                
                ' Find start of identifier
                Dim lStart As Integer = vColumn
                While lStart > 0 AndAlso IsIdentifierChar(lLine(lStart - 1))
                    lStart -= 1
                End While
                
                ' Find end of identifier
                Dim lEnd As Integer = vColumn
                While lEnd < lLine.Length AndAlso IsIdentifierChar(lLine(lEnd))
                    lEnd += 1
                End While
                
                If lEnd > lStart Then
                    Dim lIdentifier As String = lLine.Substring(lStart, lEnd - lStart)
                    Return (lIdentifier, lStart, lEnd)
                End If
                
                Return (Nothing, 0, 0)
                
            Catch ex As Exception
                Console.WriteLine($"GetIdentifierAtPosition error: {ex.Message}")
                Return (Nothing, 0, 0)
            End Try
        End Function
        
        ''' <summary>
        ''' Check if character is part of an identifier
        ''' </summary>
        Private Function IsIdentifierChar(vChar As Char) As Boolean
            Return Char.IsLetterOrDigit(vChar) OrElse vChar = "_"c
        End Function
        
        ''' <summary>
        ''' Check if character marks an identifier boundary
        ''' </summary>
        Private Function IsIdentifierBoundary(vChar As Char) As Boolean
            Return Not IsIdentifierChar(vChar)
        End Function
        
        ''' <summary>
        ''' Extract declaration type from regex pattern
        ''' </summary>
        Private Function ExtractDeclarationType(vPattern As String) As String
            If vPattern.Contains("Sub\s+") Then Return "Sub"
            If vPattern.Contains("Function\s+") Then Return "Function"
            If vPattern.Contains("Property\s+") Then Return "Property"
            If vPattern.Contains("Class\s+") Then Return "Class"
            If vPattern.Contains("Module\s+") Then Return "Module"
            If vPattern.Contains("Interface\s+") Then Return "Interface"
            If vPattern.Contains("Structure\s+") Then Return "Structure"
            If vPattern.Contains("Enum\s+") Then Return "Enum"
            If vPattern.Contains("Dim\s+") Then Return "Dim"
            Return "Unknown"
        End Function
        
    End Class
    
End Namespace

