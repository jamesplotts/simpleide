' Editors/CustomDrawingEditor.IdentifierCaseSync.vb - Project-wide identifier case synchronization
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Events for Project-Wide Updates =====
        
        ' Raised when an identifier's case changes in a declaration
        Public Event IdentifierCaseChanged(vOldName As String, vNewName As String, vScope As IdentifierScope)
        
        ' ===== Identifier Case Synchronization =====
        
        ' Process a line and detect declaration changes
        Private Sub ProcessLineFormattingWithDeclarationTracking(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pLineCount Then Return
                
                Dim lOriginalText As String = pTextLines(vLineIndex)
                If String.IsNullOrEmpty(lOriginalText.Trim()) Then Return
                
                ' Extract declarations before formatting
                Dim lOriginalDeclarations As List(Of IdentifierDeclaration) = ExtractDeclarations(lOriginalText)
                
                ' Apply formatting
                Dim lFormattedText As String = FormatLine(lOriginalText)
                
                ' Extract declarations after formatting
                Dim lNewDeclarations As List(Of IdentifierDeclaration) = ExtractDeclarations(lFormattedText)
                
                ' Compare and detect case changes
                DetectDeclarationCaseChanges(lOriginalDeclarations, lNewDeclarations)
                
                ' Update the line if it changed
                If lFormattedText <> lOriginalText Then
                    pTextLines(vLineIndex) = lFormattedText
                    IsModified = True
                    pLineMetadata(vLineIndex).MarkChanged()
                End If
                
                ' Apply syntax highlighting
                ApplySyntaxHighlightingToLine(vLineIndex)
                
                ' Schedule redraw
                InvalidateLine(vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessLineFormattingWithDeclarationTracking error: {ex.Message}")
            End Try
        End Sub
        
        ' Extract declarations from a line of code
        Private Function ExtractDeclarations(vLine As String) As List(Of IdentifierDeclaration)
            Dim lDeclarations As New List(Of IdentifierDeclaration)
            
            Try
                ' Patterns for various declarations
                Dim lPatterns As New List(Of DeclarationPattern) 
                With lPatterns
                    ' Variable declarations
                    .Add(New DeclarationPattern( "^\s*(?:Public|Private|Protected|Friend|Dim)\s+(?:Shared\s+)?(?:ReadOnly\s+)?(\w+)(?:\s*,\s*(\w+))*\s+As\s+", IdentifierScope.eVariable ))
                    ' Function/Sub declarations
                    .Add(New DeclarationPattern( "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:Overrides\s+)?(?:Function|Sub)\s+(\w+)\s*\(", IdentifierScope.eMethod ))
                    ' Property declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:ReadOnly\s+|WriteOnly\s+)?Property\s+(\w+)",
                        IdentifierScope.eProperty ) )
                    ' Class/Module/Structure declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Partial\s+)?(?:Class|Module|Structure|Interface)\s+(\w+)",
                        IdentifierScope.eType ) )
                    ' Event declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*Event\s+(\w+)",
                        IdentifierScope.eEvent ) )
                    ' Const declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*Const\s+(\w+)\s*=",
                        IdentifierScope.eConstant ) )
                    ' For loop variables
                    .Add(New DeclarationPattern(
                        "\bFor\s+(?:each\s+)?(\w+)\s+",
                        IdentifierScope.eLocal ) )
                    ' Parameter declarations in method signature
                    .Add(New DeclarationPattern(
                        "(?:ByVal|ByRef)?\s*(\w+)\s+As\s+",
                        IdentifierScope.eParameter ) )
                End With
                
                ' Check each pattern
                For Each lPattern In lPatterns
                    Dim lRegex As New Regex(lPattern.Pattern, RegexOptions.IgnoreCase)
                    Dim lMatch As Match = lRegex.Match(vLine)
                    
                    If lMatch.Success Then
                        ' Extract all captured identifiers
                        For i As Integer = 1 To lMatch.Groups.Count - 1
                            If lMatch.Groups(i).Success AndAlso Not String.IsNullOrWhiteSpace(lMatch.Groups(i).Value) Then
                                lDeclarations.Add(New IdentifierDeclaration(
                                    lMatch.Groups(i).Value,
                                    lPattern.Scope,
                                    pCursorLine ' current Line number
                                ))
                            End If
                        Next
                    End If
                Next
                
                ' Handle multiple variable declarations on same line (Dim x, y, z As Integer)
                Dim lMultiVarPattern As New Regex("^\s*(?:Dim|Private|Public)\s+((?:\w+\s*,\s*)*\w+)\s+As\s+", RegexOptions.IgnoreCase)
                Dim lMultiMatch As Match = lMultiVarPattern.Match(vLine)
                If lMultiMatch.Success Then
                    Dim lVarList As String = lMultiMatch.Groups(1).Value
                    Dim lVars() As String = lVarList.Split(","c)
                    For Each lVar As String In lVars
                        lVar = lVar.Trim()
                        If Not String.IsNullOrWhiteSpace(lVar) Then
                            lDeclarations.Add(New IdentifierDeclaration(lVar, IdentifierScope.eVariable, pCursorLine))
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExtractDeclarations error: {ex.Message}")
            End Try
            
            Return lDeclarations
        End Function
        
        ' Detect case changes in declarations
        Private Sub DetectDeclarationCaseChanges(vOldDecls As List(Of IdentifierDeclaration), vNewDecls As List(Of IdentifierDeclaration))
            Try
                ' Build lookup dictionary for old declarations
                Dim lOldDeclMap As New Dictionary(Of String, IdentifierDeclaration)(StringComparer.OrdinalIgnoreCase)
                For Each lOldDecl In vOldDecls
                    lOldDeclMap(lOldDecl.Name) = lOldDecl
                Next
                
                ' Check each new declaration for case changes
                For Each lNewDecl In vNewDecls
                    Dim lOldDecl As IdentifierDeclaration = Nothing
                    If lOldDeclMap.TryGetValue(lNewDecl.Name, lOldDecl) Then
                        ' Found matching declaration - check if case changed
                        If Not lOldDecl.Name.Equals(lNewDecl.Name, StringComparison.Ordinal) Then
                            ' Case changed! Update project-wide
                            UpdateIdentifierCaseProjectWide(lOldDecl.Name, lNewDecl.Name, lNewDecl.Scope)
                        End If
                    Else
                        ' New declaration - add to case map
                        UpdateIdentifierCaseMap(lNewDecl.Name, lNewDecl.Name)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DetectDeclarationCaseChanges error: {ex.Message}")
            End Try
        End Sub
        
        ' Update identifier case project-wide
        Private Sub UpdateIdentifierCaseProjectWide(vOldCase As String, vNewCase As String, vScope As IdentifierScope)
            Try
                ' Update our local case map
                UpdateIdentifierCaseMap(vOldCase, vNewCase)
                
                ' Raise event for MainWindow to handle project-wide update
                RaiseEvent IdentifierCaseChanged(vOldCase, vNewCase, vScope)
                
                ' Update all occurrences in current document
                UpdateIdentifierCaseInDocument(vOldCase, vNewCase)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseProjectWide error: {ex.Message}")
            End Try
        End Sub
        
        ' Update all occurrences of an identifier in current document
        Private Sub UpdateIdentifierCaseInDocument(vOldCase As String, vNewCase As String)
            Try
                ' Update all lines except the one being edited
                For i As Integer = 0 To pLineCount - 1
                    ' Skip the line being edited
                    If i = pEditingLine Then Continue For
                    
                    Dim lLine As String = pTextLines(i)
                    Dim lUpdatedLine As String = UpdateIdentifierCaseInLine(lLine, vOldCase, vNewCase, i)  ' FIXED: Pass line index
                    
                    If Not lLine.Equals(lUpdatedLine, StringComparison.Ordinal) Then
                        pTextLines(i) = lUpdatedLine
                        pLineMetadata(i).MarkChanged()
                        
                        ' Re-apply syntax highlighting for this line
                        ApplySyntaxHighlightingToLine(i)
                        InvalidateLine(i)
                    End If
                Next
                
                ' Mark document as modified
                IsModified = True
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseInDocument error: {ex.Message}")
            End Try
        End Sub
        
        ' Update identifier case in a single line
        Private Function UpdateIdentifierCaseInLine(vLine As String, vOldCase As String, vNewCase As String, vLineIndex As Integer) As String
            Try
                ' Tokenize the line WITH LINE INDEX
                Dim lTokens As List(Of LineToken) = TokenizeLine(vLine, vLineIndex)
                Dim lResult As New System.Text.StringBuilder()
                
                For Each lToken In lTokens
                    If lToken.Type = LineTokenType.eIdentifier Then
                        ' Check if this identifier matches (case-insensitive)
                        If lToken.Text.Equals(vOldCase, StringComparison.OrdinalIgnoreCase) Then
                            ' Replace with new case
                            lResult.Append(vNewCase)
                        Else
                            lResult.Append(lToken.Text)
                        End If
                    Else
                        ' Keep other tokens unchanged
                        lResult.Append(lToken.Text)
                    End If
                Next
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseInLine error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        ' ===== Helper Classes =====
        
        Private Class DeclarationPattern
            Public Property Pattern As String
            Public Property Scope As IdentifierScope
            
            Public Sub New(vPattern As String, vScope As IdentifierScope)
                Pattern = vPattern
                Scope = vScope
            End Sub
        End Class
        
        Private Class IdentifierDeclaration
            Public Property Name As String
            Public Property Scope As IdentifierScope
            Public Property Line As Integer
            
            Public Sub New(vName As String, vScope As IdentifierScope, vLine As Integer)
                Name = vName
                Scope = vScope
                Line = vLine
            End Sub
        End Class
        
'        Public Enum IdentifierScope
'            Local
'            Parameter
'            Variable
'            Method
'            [Property]
'            Type
'            [Event]
'            Constant
'        End Enum
        
    End Class
    
End Namespace
