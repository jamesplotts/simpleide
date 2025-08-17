' MainWindow.Indent.vb - Indentation and code formatting functionality
Imports Gtk
Imports System
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Partial Public Class MainWindow
    
    ' ===== Indentation Constants =====
    Private Const DEFAULT_INDENT_SIZE As Integer = 4
    Private Const MAX_INDENT_LEVEL As Integer = 20
    
    ' ===== Indentation Event Handlers =====
    
    ' Handle Indent menu item
    Public Sub OnIndent(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.IndentSelection()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnIndent error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle Outdent menu item
    Public Sub OnOutdent(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.OutdentSelection()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnOutdent error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Smart Indentation =====
    
    ' Apply smart indentation based on VB.NET language rules
    Public Sub ApplySmartIndentation()
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor Is Nothing Then Return
            
            Dim lCursorPos As EditorPosition = lCurrentEditor.GetCursorPosition()
            If lCursorPos.Line <= 0 Then Return
            
            ' Get current and previous line
            Dim lCurrentLine As String = lCurrentEditor.GetLineText(lCursorPos.Line)
            Dim lPreviousLine As String = lCurrentEditor.GetLineText(lCursorPos.Line - 1)
            
            ' Calculate appropriate indentation
            Dim lNewIndent As String = CalculateSmartIndent(lPreviousLine, lCurrentLine, lCursorPos.Line, lCurrentEditor)
            
            ' Apply indentation if needed
            If Not String.IsNullOrEmpty(lNewIndent) Then
                ApplyLineIndentation(lCurrentEditor, lCursorPos.Line, lNewIndent)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ApplySmartIndentation error: {ex.Message}")
        End Try
    End Sub
    
    ' Calculate smart indentation based on VB.NET syntax
    Private Function CalculateSmartIndent(vPreviousLine As String, vCurrentLine As String, vCurrentLineIndex As Integer, vEditor As IEditor) As String
        Try
            If String.IsNullOrEmpty(vPreviousLine) Then Return ""
            
            ' Get base indentation from previous line
            Dim lBaseIndent As String = GetLineIndentation(vPreviousLine)
            Dim lTrimmedPrevious As String = vPreviousLine.Trim().ToLower()
            Dim lTrimmedCurrent As String = vCurrentLine.Trim().ToLower()
            
            ' Check if we need to increase indentation
            If ShouldIncreaseIndentation(lTrimmedPrevious) Then
                ' Don't indent if current line is a closing statement
                If Not IsClosingStatement(lTrimmedCurrent) Then
                    Return lBaseIndent & GetIndentString()
                End If
            End If
            
            ' Check if we need to decrease indentation
            If ShouldDecreaseIndentation(lTrimmedCurrent) Then
                Return DecreaseIndentation(lBaseIndent)
            End If
            
            ' For continuation lines, check if previous line ends with underscore
            If lTrimmedPrevious.EndsWith("_") Then
                Return lBaseIndent & GetIndentString()
            End If
            
            ' For case statements, maintain case indentation
            If IsCaseStatement(lTrimmedCurrent) Then
                Return CalculateCaseIndentation(vEditor, vCurrentLineIndex)
            End If
            
            ' Default: use same indentation as previous line
            Return lBaseIndent
            
        Catch ex As Exception
            Console.WriteLine($"CalculateSmartIndent error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' Check if line should increase indentation
    Private Function ShouldIncreaseIndentation(vTrimmedLine As String) As Boolean
        Try
            ' VB.NET block start keywords
            Dim lBlockStarters As String() = {
                "if ", "then", "else", "elseif ",
                "for ", "for each ", "while ", "do ",
                "select case ", "case ",
                "try", "catch ", "finally",
                "with ", "using ",
                "sub ", "function ", "property ",
                "class ", "module ", "interface ", "structure ", "enum ",
                "namespace ",
                "get", "set",
                "#if ", "#region"
            }
            
            For Each lStarter In lBlockStarters
                If vTrimmedLine.StartsWith(lStarter) OrElse 
                   vTrimmedLine.Contains(" " & lStarter) OrElse
                   vTrimmedLine.EndsWith(" " & lStarter.Trim()) Then
                    Return True
                End If
            Next
            
            ' Check for multiline statements
            If vTrimmedLine.EndsWith(" then") OrElse 
               vTrimmedLine.EndsWith(" _") Then
                Return True
            End If
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"ShouldIncreaseIndentation error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Check if line should decrease indentation
    Private Function ShouldDecreaseIndentation(vTrimmedLine As String) As Boolean
        Try
            Return IsClosingStatement(vTrimmedLine)
            
        Catch ex As Exception
            Console.WriteLine($"ShouldDecreaseIndentation error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Check if line is a closing statement
    Private Function IsClosingStatement(vTrimmedLine As String) As Boolean
        Try
            Dim lClosingStatements As String() = {
                "end if", "end select", "end try", "end with", "end using",
                "end sub", "end function", "end property", "end get", "end set",
                "end class", "end module", "end interface", "end structure", "end enum",
                "end namespace",
                "next", "loop", "wend",
                "catch ", "finally", "else", "elseif ",
                "#end if", "#end region"
            }
            
            For Each lClosing In lClosingStatements
                If vTrimmedLine.StartsWith(lClosing) Then
                    Return True
                End If
            Next
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"IsClosingStatement error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Check if line is a case statement
    Private Function IsCaseStatement(vTrimmedLine As String) As Boolean
        Try
            Return vTrimmedLine.StartsWith("case ") OrElse vTrimmedLine = "case else"
            
        Catch ex As Exception
            Console.WriteLine($"IsCaseStatement error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Calculate proper indentation for case statements
    Private Function CalculateCaseIndentation(vEditor As IEditor, vCurrentLineIndex As Integer) As String
        Try
            ' Find the Select Case statement
            For i As Integer = vCurrentLineIndex - 1 To Math.Max(0, vCurrentLineIndex - 50) Step -1
                Dim lLine As String = vEditor.GetLineText(i).Trim().ToLower()
                If lLine.StartsWith("select case ") Then
                    Dim lSelectIndent As String = GetLineIndentation(vEditor.GetLineText(i))
                    Return lSelectIndent & GetIndentString()
                End If
            Next
            
            ' Fallback: use current indentation
            Return GetLineIndentation(vEditor.GetLineText(vCurrentLineIndex))
            
        Catch ex As Exception
            Console.WriteLine($"CalculateCaseIndentation error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' ===== Format Document =====
    
    ' Format entire document
    Public Sub FormatDocument()
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor Is Nothing Then Return
            
            ' Begin bulk update
            lCurrentEditor.BeginUpdate()
            
            Try
                ' Format all lines
                Dim lLineCount As Integer = lCurrentEditor.LineCount
                For i As Integer = 0 To lLineCount - 1
                    FormatLine(lCurrentEditor, i)
                Next
                
            Finally
                ' End bulk update
                lCurrentEditor.EndUpdate()
            End Try
            
        Catch ex As Exception
            Console.WriteLine($"FormatDocument error: {ex.Message}")
        End Try
    End Sub
    
    ' Format selection
    Private Sub FormatSelection()
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor Is Nothing OrElse Not lCurrentEditor.HasSelection Then Return
            
            ' Get selection bounds using existing interface methods
            Dim lStartPos As EditorPosition = lCurrentEditor.SelectionStart
            Dim lEndPos As EditorPosition = lCurrentEditor.SelectionEnd
            
            ' Begin bulk update
            lCurrentEditor.BeginUpdate()
            
            Try
                ' Format selected lines
                For i As Integer = lStartPos.Line To lEndPos.Line
                    FormatLine(lCurrentEditor, i)
                Next
                
            Finally
                ' End bulk update
                lCurrentEditor.EndUpdate()
            End Try
            
        Catch ex As Exception
            Console.WriteLine($"FormatSelection error: {ex.Message}")
        End Try
    End Sub
    
    ' Format a single line
    Private Sub FormatLine(vEditor As IEditor, vLineIndex As Integer)
        Try
            Dim lOriginalLine As String = vEditor.GetLineText(vLineIndex)
            Dim lTrimmedLine As String = lOriginalLine.Trim()
            
            If String.IsNullOrEmpty(lTrimmedLine) Then Return
            
            ' Calculate proper indentation
            Dim lNewIndent As String = ""
            If vLineIndex > 0 Then
                Dim lPreviousLine As String = vEditor.GetLineText(vLineIndex - 1)
                lNewIndent = CalculateSmartIndent(lPreviousLine, lOriginalLine, vLineIndex, vEditor)
            End If
            
            ' Apply formatting
            Dim lFormattedLine As String = lNewIndent & lTrimmedLine
            
            If lFormattedLine <> lOriginalLine Then
                ' Replace the line
                vEditor.ReplaceText(vLineIndex, 0, vLineIndex, lOriginalLine.Length, lFormattedLine)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"FormatLine error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    ' Get indentation string based on settings
    Private Function GetIndentString() As String
        Try
            If pSettingsManager.UseTabs Then
                Return vbTab
            Else
                Return New String(" "c, pSettingsManager.TabWidth)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"GetIndentString error: {ex.Message}")
            Return "    " ' Default to 4 spaces
        End Try
    End Function
    
    ' Get indentation from a line
    Private Function GetLineIndentation(vLine As String) As String
        Try
            Dim lIndent As New System.Text.StringBuilder()
            
            For Each lChar In vLine
                If lChar = " "c OrElse lChar = vbTab Then
                    lIndent.Append(lChar)
                Else
                    Exit For
                End If
            Next
            
            Return lIndent.ToString()
            
        Catch ex As Exception
            Console.WriteLine($"GetLineIndentation error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' Decrease indentation level
    Private Function DecreaseIndentation(vCurrentIndent As String) As String
        Try
            If String.IsNullOrEmpty(vCurrentIndent) Then Return ""
            
            Dim lIndentString As String = GetIndentString()
            
            ' If using tabs, remove one tab
            If pSettingsManager.UseTabs AndAlso vCurrentIndent.EndsWith(vbTab) Then
                Return vCurrentIndent.Substring(0, vCurrentIndent.Length - 1)
            End If
            
            ' If using spaces, remove one indent level
            If vCurrentIndent.Length >= lIndentString.Length AndAlso 
               vCurrentIndent.EndsWith(lIndentString) Then
                Return vCurrentIndent.Substring(0, vCurrentIndent.Length - lIndentString.Length)
            End If
            
            ' Fallback: remove last character if it's whitespace
            If vCurrentIndent.Length > 0 AndAlso 
               (vCurrentIndent.EndsWith(" ") OrElse vCurrentIndent.EndsWith(vbTab)) Then
                Return vCurrentIndent.Substring(0, vCurrentIndent.Length - 1)
            End If
            
            Return vCurrentIndent
            
        Catch ex As Exception
            Console.WriteLine($"DecreaseIndentation error: {ex.Message}")
            Return vCurrentIndent
        End Try
    End Function
    
    ' Apply indentation to a specific line
    Private Sub ApplyLineIndentation(vEditor As IEditor, vLineIndex As Integer, vNewIndent As String)
        Try
            Dim lCurrentLine As String = vEditor.GetLineText(vLineIndex)
            Dim lCurrentIndent As String = GetLineIndentation(lCurrentLine)
            Dim lTrimmedLine As String = lCurrentLine.Substring(lCurrentIndent.Length)
            
            ' Only apply if indentation is different
            If lCurrentIndent <> vNewIndent Then
                Dim lNewLine As String = vNewIndent & lTrimmedLine
                vEditor.ReplaceText(vLineIndex, 0, vLineIndex, lCurrentLine.Length, lNewLine)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ApplyLineIndentation error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Auto-Indentation on Enter =====
    
    ' Handle automatic indentation when Enter is pressed
    Public Sub OnAutoIndentNewLine(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            Dim lCursorPos As EditorPosition = vEditor.GetCursorPosition()
            If lCursorPos.Line <= 0 Then Return
            
            ' Get previous line
            Dim lPreviousLine As String = vEditor.GetLineText(lCursorPos.Line - 1)
            Dim lCurrentLine As String = vEditor.GetLineText(lCursorPos.Line)
            
            ' Calculate indentation
            Dim lNewIndent As String = CalculateSmartIndent(lPreviousLine, lCurrentLine, lCursorPos.Line, vEditor)
            
            ' Apply indentation to current line
            If Not String.IsNullOrEmpty(lNewIndent) Then
                ApplyLineIndentation(vEditor, lCursorPos.Line, lNewIndent)
                
                ' Move cursor to end of indentation
                vEditor.GoToPosition(lCursorPos.Line, lNewIndent.Length)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnAutoIndentNewLine error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Menu State Updates =====
    
    ' Update indent/outdent menu states
    Public Sub UpdateIndentMenuStates()
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            Dim lHasEditor As Boolean = lCurrentEditor IsNot Nothing
            
            ' Enable/disable indent/outdent menu items based on editor availability
            ' This would be called from the menu update logic
            
        Catch ex As Exception
            Console.WriteLine($"UpdateIndentMenuStates error: {ex.Message}")
        End Try
    End Sub
    
End Class