' Editors/CustomDrawingEditor.EnumParameterHint.vb - Auto-popup of an Enum's values when the
' cursor sits on a method/function/constructor call argument whose declared parameter type is
' an Enum - either project-defined or a system/framework one (BCL, GTK#, etc.) - so the value
' can be picked from a list instead of typed/remembered.
' Reuses the CodeSense popup (CustomDrawingEditor.CodeSensePopup.vb) for display/selection and
' the call/parameter resolution already built for CustomDrawingEditor.ParameterHint.vb.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Common namespaces to try a bare Enum name under (e.g. "Orientation" as
        ''' "Gtk.Orientation") when an exact/full-name lookup misses - mirrors
        ''' ReflectionHelper.GetTypeInfo's own fallback list, since parameter type strings are
        ''' whatever was literally written in source and may rely on an Imports statement
        ''' rather than being fully qualified
        ''' </summary>
        Private Shared ReadOnly EnumParameterCommonNamespaces As String() = {
            "System", "System.IO", "System.Collections.Generic", "System.Linq", "System.Text",
            "System.Threading.Tasks", "Gtk", "Gdk", "GLib", "Pango", "Cairo",
            "Microsoft.VisualBasic"
        }

        ''' <summary>
        ''' True while the cursor sits on a call argument whose parameter type is an Enum
        ''' (project-defined or system/framework) and the CodeSense popup is showing that
        ''' Enum's values. CheckCodeSenseTrigger/HandleBackspaceForCodeSense read this to avoid
        ''' clobbering the Enum-values popup with an unrelated generic identifier search -
        ''' UpdateParameterHint (which runs first on every cursor move) is solely responsible
        ''' for keeping it current.
        ''' </summary>
        Private pCursorInEnumParameterSlot As Boolean = False

        ''' <summary>
        ''' If vParameterType names an Enum - checking the project's own parsed source first,
        ''' then falling back to any Enum type loaded from the BCL, GTK#, or any other
        ''' referenced assembly - shows/refreshes a CodeSense popup of "EnumName.MemberName"
        ''' suggestions for it, filtered by whatever's already typed at the cursor.
        ''' </summary>
        ''' <returns>True if it did so (caller should skip its own tooltip/suggestions for this
        ''' cycle); False if vParameterType isn't a known Enum of either kind</returns>
        Private Function TryShowEnumParameterSuggestions(vParameterType As String) As Boolean
            Try
                Dim lPrefix As String = GetCurrentWord()
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()

                Dim lEnumNode As SyntaxNode = FindEnumNodeByName(vParameterType)
                If lEnumNode IsNot Nothing AndAlso lEnumNode.Children IsNot Nothing Then
                    for each lMember As SyntaxNode in lEnumNode.Children
                        If lMember.NodeType <> CodeNodeType.eEnumValue Then Continue For
                        If Not String.IsNullOrEmpty(lPrefix) AndAlso
                           Not lMember.Name.StartsWith(lPrefix, StringComparison.OrdinalIgnoreCase) Then Continue For
                        AddEnumSuggestion(lSuggestions, lEnumNode.Name, lMember.Name)
                    Next
                Else
                    Dim lSystemType As Type = FindSystemEnumType(vParameterType)
                    If lSystemType IsNot Nothing Then
                        for each lMemberName As String in System.Enum.GetNames(lSystemType)
                            If Not String.IsNullOrEmpty(lPrefix) AndAlso
                               Not lMemberName.StartsWith(lPrefix, StringComparison.OrdinalIgnoreCase) Then Continue For
                            AddEnumSuggestion(lSuggestions, lSystemType.Name, lMemberName)
                        Next
                    End If
                End If

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
        ''' Builds and appends one "EnumName.MemberName" suggestion
        ''' </summary>
        Private Sub AddEnumSuggestion(vSuggestions As List(Of CodeSenseSuggestion), vEnumName As String, vMemberName As String)
            Dim lSuggestion As New CodeSenseSuggestion()
            lSuggestion.Text = $"{vEnumName}.{vMemberName}"
            lSuggestion.Description = $"{vEnumName} enum value"
            lSuggestion.SuggestionType = CodeSenseSuggestionType.eField
            vSuggestions.Add(lSuggestion)
        End Sub

        ''' <summary>
        ''' Resolves vName to a loaded .NET Enum type - tried as-written first (handles
        ''' explicitly-qualified names like "Gtk.Orientation"), then under each namespace this
        ''' file actually imports (the real Imports statements, not a guess), then finally
        ''' under each of EnumParameterCommonNamespaces as a last-resort safety net for
        ''' whatever the real-Imports pass doesn't cover
        ''' </summary>
        Private Function FindSystemEnumType(vName As String) As Type
            If String.IsNullOrEmpty(vName) Then Return Nothing
            Try
                Dim lName As String = vName.Trim()

                Dim lType As Type = ReflectionHelper.FindTypeByName(lName)
                If lType IsNot Nothing AndAlso lType.IsEnum Then Return lType

                ' Already dotted (e.g. "Gtk.Orientation") and still not found - trying it
                ' again under a namespace prefix below would only produce nonsense
                If lName.Contains("."c) Then Return Nothing

                If pProjectManager IsNot Nothing Then
                    for each lCandidate As String in pProjectManager.GetImportsDerivedCandidates(pFilePath, lName)
                        lType = ReflectionHelper.FindTypeByName(lCandidate)
                        If lType IsNot Nothing AndAlso lType.IsEnum Then Return lType
                    Next
                End If

                for each lNamespace As String in EnumParameterCommonNamespaces
                    lType = ReflectionHelper.FindTypeByName($"{lNamespace}.{lName}")
                    If lType IsNot Nothing AndAlso lType.IsEnum Then Return lType
                Next

            Catch ex As Exception
                Console.WriteLine($"FindSystemEnumType error: {ex.Message}")
            End Try
            Return Nothing
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
