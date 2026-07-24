' CustomDrawingEditor.ImplementInterface.vb - "Implement Interface Members" quick action:
' for each interface a class Implements, generates a Public stub (with an
' "Implements IFoo.Member" clause and a "' TODO:" marker in the body, so it surfaces
' automatically in the existing TODO panel) for every member not yet implemented.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Returns the interface members (across every interface the containing class
        ''' Implements) that don't already have a matching "Implements IFoo.Member" clause
        ''' somewhere in the class, or an empty list if the cursor isn't inside a class that
        ''' implements any interfaces (used both for context menu visibility and generation)
        ''' </summary>
        Private Function GetUnimplementedInterfaceMembers() As List(Of SyntaxNode)
            Dim lResult As New List(Of SyntaxNode)()
            Try
                Dim lClassNode As SyntaxNode = FindContainingClassOrModuleNode(pRootNode, pCursorLine)
                If lClassNode Is Nothing OrElse lClassNode.ImplementsList Is Nothing OrElse lClassNode.ImplementsList.Count = 0 Then
                    Return lResult
                End If
                If pProjectManager Is Nothing Then Return lResult

                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree Is Nothing Then Return lResult

                for each lImplementsEntry As String in lClassNode.ImplementsList
                    Dim lInterfaceName As String = lImplementsEntry.Split("."c).Last()
                    Dim lInterfaceNode As SyntaxNode = FindInterfaceNodeByName(lProjectTree, lInterfaceName)
                    If lInterfaceNode Is Nothing OrElse lInterfaceNode.Children Is Nothing Then Continue for

                    for each lMember As SyntaxNode in lInterfaceNode.Children
                        Select Case lMember.NodeType
                            Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eProperty, CodeNodeType.eEvent
                                If Not IsInterfaceMemberImplemented(lClassNode, lInterfaceName, lMember.Name) Then
                                    lResult.Add(lMember)
                                End If
                        End Select
                    Next
                Next

            Catch ex As Exception
                Console.WriteLine($"GetUnimplementedInterfaceMembers error: {ex.Message}")
            End Try
            Return lResult
        End Function

        ''' <summary>
        ''' Generates stubs for every currently-unimplemented interface member, inserted as a
        ''' contiguous block right before the class's End Class/End Module line
        ''' </summary>
        Private Sub ImplementInterfaceMembers()
            Try
                Dim lClassNode As SyntaxNode = FindContainingClassOrModuleNode(pRootNode, pCursorLine)
                If lClassNode Is Nothing Then Return

                Dim lMissing As List(Of SyntaxNode) = GetUnimplementedInterfaceMembers()
                If lMissing.Count = 0 Then Return

                Dim lMemberIndent As String = GetLineIndentation(lClassNode.StartLine) & GetTabIndentString()
                Dim lBodyIndent As String = lMemberIndent & GetTabIndentString()

                Dim lStubLines As New List(Of String)()
                for each lMember As SyntaxNode in lMissing
                    If lStubLines.Count > 0 Then lStubLines.Add("")
                    lStubLines.AddRange(BuildInterfaceStub(lMember, lMemberIndent, lBodyIndent))
                Next

                InsertLinesBefore(lClassNode.EndLine, String.Join(Environment.NewLine, lStubLines) & Environment.NewLine)

                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"ImplementInterfaceMembers error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the stub line(s) for a single interface member: Sub/Function get a
        ''' "' TODO:" marker plus "Throw New NotImplementedException()"; Property gets the
        ''' same in whichever of Get/Set the interface member's ReadOnly/WriteOnly modifiers
        ''' call for; Event needs no body at all (a bare Implements declaration is complete)
        ''' </summary>
        Private Function BuildInterfaceStub(vMember As SyntaxNode, vIndent As String, vBodyIndent As String) As List(Of String)
            Dim lLines As New List(Of String)()
            Dim lInterfaceName As String = If(vMember.Parent IsNot Nothing, vMember.Parent.Name, "")
            Dim lParamText As String = BuildParameterListText(vMember.Parameters)
            Dim lTodoText As String = vBodyIndent & $"' TODO: Implement {lInterfaceName}.{vMember.Name}"
            Dim lThrowText As String = vBodyIndent & "Throw New NotImplementedException()"

            Select Case vMember.NodeType
                Case CodeNodeType.eMethod
                    lLines.Add(vIndent & $"Public Sub {vMember.Name}({lParamText}) Implements {lInterfaceName}.{vMember.Name}")
                    lLines.Add(lTodoText)
                    lLines.Add(lThrowText)
                    lLines.Add(vIndent & "End Sub")

                Case CodeNodeType.eFunction
                    Dim lReturnType As String = If(String.IsNullOrEmpty(vMember.ReturnType), "Object", vMember.ReturnType)
                    lLines.Add(vIndent & $"Public Function {vMember.Name}({lParamText}) As {lReturnType} Implements {lInterfaceName}.{vMember.Name}")
                    lLines.Add(lTodoText)
                    lLines.Add(lThrowText)
                    lLines.Add(vIndent & "End Function")

                Case CodeNodeType.eProperty
                    Dim lType As String = If(Not String.IsNullOrEmpty(vMember.DataType), vMember.DataType,
                                              If(Not String.IsNullOrEmpty(vMember.ReturnType), vMember.ReturnType, "Object"))
                    Dim lPropParamText As String = If(String.IsNullOrEmpty(lParamText), "", $"({lParamText})")
                    Dim lModifierPrefix As String = If(vMember.IsReadOnly, "ReadOnly ", If(vMember.IsWriteOnly, "WriteOnly ", ""))

                    lLines.Add(vIndent & $"Public {lModifierPrefix}Property {vMember.Name}{lPropParamText} As {lType} Implements {lInterfaceName}.{vMember.Name}")

                    If Not vMember.IsWriteOnly Then
                        lLines.Add(vBodyIndent & "Get")
                        lLines.Add(lTodoText)
                        lLines.Add(lThrowText)
                        lLines.Add(vBodyIndent & "End Get")
                    End If
                    If Not vMember.IsReadOnly Then
                        lLines.Add(vBodyIndent & $"Set(value As {lType})")
                        lLines.Add(lTodoText)
                        lLines.Add(lThrowText)
                        lLines.Add(vBodyIndent & "End Set")
                    End If
                    lLines.Add(vIndent & "End Property")

                Case CodeNodeType.eEvent
                    lLines.Add(vIndent & $"Public Event {vMember.Name}({lParamText}) Implements {lInterfaceName}.{vMember.Name}")
            End Select

            Return lLines
        End Function

        ''' <summary>
        ''' Formats a list of parameters for a stub's signature, reusing the same
        ''' Optional/ByRef/ParamArray-aware formatter the parameter-hint popup uses
        ''' </summary>
        Private Function BuildParameterListText(vParameters As List(Of ParameterInfo)) As String
            If vParameters Is Nothing OrElse vParameters.Count = 0 Then Return ""
            Dim lParts As New List(Of String)()
            for each lParam As ParameterInfo in vParameters
                lParts.Add(FormatParameterDeclaration(lParam))
            Next
            Return String.Join(", ", lParts)
        End Function

        ''' <summary>
        ''' Best-effort check for whether vClassNode already has a member carrying
        ''' "Implements InterfaceName.MemberName" somewhere in its own line range
        ''' </summary>
        Private Function IsInterfaceMemberImplemented(vClassNode As SyntaxNode, vInterfaceName As String, vMemberName As String) As Boolean
            Try
                Dim lTarget As String = vInterfaceName & "." & vMemberName
                Dim lEndLine As Integer = Math.Min(vClassNode.EndLine, pLineCount - 1)
                for lLine As Integer = vClassNode.StartLine To lEndLine
                    Dim lCode As String = StripLineComment(TextLines(lLine))
                    Dim lImplementsIdx As Integer = lCode.IndexOf("Implements ", StringComparison.OrdinalIgnoreCase)
                    If lImplementsIdx < 0 Then Continue for

                    Dim lClausePart As String = lCode.Substring(lImplementsIdx + 11)
                    for each lClause As String in lClausePart.Split(","c)
                        If String.Equals(lClause.Trim(), lTarget, StringComparison.OrdinalIgnoreCase) Then Return True
                    Next
                Next
            Catch ex As Exception
                Console.WriteLine($"IsInterfaceMemberImplemented error: {ex.Message}")
            End Try
            Return False
        End Function

        ''' <summary>
        ''' Recursively finds the innermost class/module/structure node whose line range
        ''' contains vLine
        ''' </summary>
        Private Function FindContainingClassOrModuleNode(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                If (vNode.NodeType = CodeNodeType.eClass OrElse vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eStructure) AndAlso
                   vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then

                    If vNode.Children IsNot Nothing Then
                        for each lChild As SyntaxNode in vNode.Children
                            Dim lNested As SyntaxNode = FindContainingClassOrModuleNode(lChild, vLine)
                            If lNested IsNot Nothing Then Return lNested
                        Next
                    End If
                    Return vNode
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindContainingClassOrModuleNode(lChild, vLine)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindContainingClassOrModuleNode error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Recursively finds the first Interface node named vName
        ''' </summary>
        Private Function FindInterfaceNodeByName(vNode As SyntaxNode, vName As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                If vNode.NodeType = CodeNodeType.eInterface AndAlso String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If
                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindInterfaceNodeByName(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If
            Catch ex As Exception
                Console.WriteLine($"FindInterfaceNodeByName error: {ex.Message}")
            End Try
            Return Nothing
        End Function

    End Class

End Namespace
