' Editors/CustomDrawingEditor.EnumParameterHint.vb - Auto-popup of an Enum's values when the
' cursor sits on a method/function/constructor call argument whose declared parameter type is
' a project-defined Enum, so the value can be picked from a list instead of typed/remembered.
' Reuses the CodeSense popup (CustomDrawingEditor.CodeSensePopup.vb) for display/selection and
' the call/parameter resolution already built for CustomDrawingEditor.ParameterHint.vb.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' True while the cursor sits on a call argument whose parameter type is a
        ''' project-defined Enum and the CodeSense popup is showing that Enum's values.
        ''' CheckCodeSenseTrigger/HandleBackspaceForCodeSense read this to avoid clobbering
        ''' the Enum-values popup with an unrelated generic identifier search - UpdateParameterHint
        ''' (which runs first on every cursor move) is solely responsible for keeping it current.
        ''' </summary>
        Private pCursorInEnumParameterSlot As Boolean = False

        ''' <summary>
        ''' If vParameterType names a project-defined Enum, shows/refreshes a CodeSense popup of
        ''' "EnumName.MemberName" suggestions for it, filtered by whatever's already typed at the
        ''' cursor. Returns True if it did so (caller should skip its own tooltip/suggestions for
        ''' this cycle); False if vParameterType isn't a known Enum.
        ''' </summary>
        Private Function TryShowEnumParameterSuggestions(vParameterType As String) As Boolean
            Try
                Dim lEnumNode As SyntaxNode = FindEnumNodeByName(vParameterType)
                If lEnumNode Is Nothing OrElse lEnumNode.Children Is Nothing Then Return False

                Dim lPrefix As String = GetCurrentWord()
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()

                for each lMember As SyntaxNode in lEnumNode.Children
                    If lMember.NodeType <> CodeNodeType.eEnumValue Then Continue For
                    If Not String.IsNullOrEmpty(lPrefix) AndAlso
                       Not lMember.Name.StartsWith(lPrefix, StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim lSuggestion As New CodeSenseSuggestion()
                    lSuggestion.Text = $"{lEnumNode.Name}.{lMember.Name}"
                    lSuggestion.Description = $"{lEnumNode.Name} enum value"
                    lSuggestion.SuggestionType = CodeSenseSuggestionType.eField
                    lSuggestions.Add(lSuggestion)
                Next

                If lSuggestions.Count = 0 Then
                    pCursorInEnumParameterSlot = False
                    CancelCodeSense()
                    Return False
                End If

                pCursorInEnumParameterSlot = True

                Dim lContext As New CodeSenseContext()
                lContext.TriggerReason = CodeSenseTriggerReason.eManual
                lContext.TriggerKind = CodeSenseTriggerKind.eManual
                lContext.TriggerPosition = New EditorPosition(pCursorLine, pCursorColumn)
                lContext.Prefix = lPrefix

                ShowCodeSenseSuggestions(lSuggestions, lContext)
                Return True

            Catch ex As Exception
                Console.WriteLine($"TryShowEnumParameterSuggestions error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Dismisses the Enum-values popup (if it's the one currently showing) when the
        ''' cursor has left the Enum-typed parameter slot that opened it
        ''' </summary>
        Private Sub ExitEnumParameterSlotIfNeeded()
            If pCursorInEnumParameterSlot Then
                pCursorInEnumParameterSlot = False
                CancelCodeSense()
            End If
        End Sub

        ''' <summary>
        ''' Recursively finds the first Enum node in the project tree named vName
        ''' </summary>
        Private Function FindEnumNodeByName(vName As String) As SyntaxNode
            If String.IsNullOrEmpty(vName) OrElse pProjectManager Is Nothing Then Return Nothing
            Dim lTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
            If lTree Is Nothing Then Return Nothing
            Return FindEnumNodeRecursive(lTree, vName.Trim())
        End Function

        Private Function FindEnumNodeRecursive(vNode As SyntaxNode, vName As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                If vNode.NodeType = CodeNodeType.eEnum AndAlso
                   String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindEnumNodeRecursive(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindEnumNodeRecursive error: {ex.Message}")
            End Try
            Return Nothing
        End Function

    End Class

End Namespace
