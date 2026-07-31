' MainWindow.Navigation.vb - Navigation functionality for MainWindow
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax

Partial Public Class MainWindow
    
    ' ===== Navigation Functions =====
    
    ' Navigate to specific line in current editor
    Public Sub NavigateToLine(vLine As Integer)
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                lEditor.GoToLine(vLine)
                lEditor.GrabFocus()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"NavigateToLine error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Updates navigation dropdowns with classes and members from document structure
    ''' </summary>
    Private Sub UpdateNavigationDropdowns()
        Try
            ' Get current tab info
            Dim lTabInfo As TabInfo = GetCurrentTabInfo()
            If lTabInfo Is Nothing Then 
                Console.WriteLine("UpdateNavigationDropdowns: No current tab")
                Return
            End If
            
            If lTabInfo.NavigationDropdowns Is Nothing Then 
                Console.WriteLine("UpdateNavigationDropdowns: No navigation dropdowns in tab")
                Return
            End If
            
            ' Get the editor
            Dim lEditor As IEditor = lTabInfo.Editor
            If lEditor Is Nothing Then 
                Console.WriteLine("UpdateNavigationDropdowns: No editor in tab")
                Return
            End If
            
            Console.WriteLine($"UpdateNavigationDropdowns: Processing {lEditor.FilePath}")
            
            ' Get document structure
            Dim lRootNode As SyntaxNode = lEditor.GetDocumentStructure()
            If lRootNode Is Nothing Then
                Console.WriteLine("UpdateNavigationDropdowns: No document structure available")
                
                ' Try to trigger a parse if it's a CustomDrawingEditor
                If TypeOf lEditor Is CustomDrawingEditor Then
                    Dim lCustomEditor As CustomDrawingEditor = DirectCast(lEditor, CustomDrawingEditor)
                End If
                
                ' Clear navigation data
                lTabInfo.NavigationDropdowns.SetNavigationData(Nothing, Nothing)
                Return
            End If
            
            Console.WriteLine($"UpdateNavigationDropdowns: Found root node: {lRootNode.Name} with {lRootNode.Children.Count} children")
            
            ' Extract classes and members
            Dim lClasses As New List(Of CodeObject)()
            Dim lRootMembers As New List(Of CodeMember)()

            ' Almost every VB.NET file wraps its types in a single Namespace block (per this
            ' project's convention, only root-namespace files like Program.vb have none) - the
            ' actual Class/Module/etc. is a child of that Namespace node, not a direct child of
            ' the document root, so this has to recurse through Namespace nodes rather than
            ' only looking at lRootNode.Children directly
            CollectNavigationNodes(lRootNode, lClasses, lRootMembers)

            Console.WriteLine($"UpdateNavigationDropdowns: Found {lClasses.Count} classes and {lRootMembers.Count} root members")
            
            ' Update dropdowns
            lTabInfo.NavigationDropdowns.SetNavigationData(lClasses, lRootMembers)
            
            ' Update current position
            Dim lCurrentLine As Integer = lEditor.CurrentLine
            Console.WriteLine($"UpdateNavigationDropdowns: Updating position to line {lCurrentLine}")
            lTabInfo.NavigationDropdowns.UpdatePosition(lCurrentLine)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateNavigationDropdowns error: {ex.Message}")
            Console.WriteLine($"  Stack: {ex.StackTrace}")
        End Try
    End Sub

    ''' <summary>
    ''' Recursively walks a syntax tree collecting Class/Module/Interface/Structure/Enum
    ''' nodes (with their members) and any member-like nodes declared outside a type,
    ''' descending through Namespace (and Document) nodes since they aren't classes or
    ''' members themselves but the actual types live inside them
    ''' </summary>
    ''' <param name="vNode">The node whose children should be processed</param>
    ''' <param name="vClasses">Accumulates discovered classes/modules/etc.</param>
    ''' <param name="vRootMembers">Accumulates members found outside any type</param>
    Private Sub CollectNavigationNodes(vNode As SyntaxNode, vClasses As List(Of CodeObject), vRootMembers As List(Of CodeMember))
        Try
            For Each lNode In vNode.Children
                Console.WriteLine($"  Processing node: {lNode.Name} (Type: {lNode.NodeType})")

                Select Case lNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule,
                         CodeNodeType.eInterface, CodeNodeType.eStructure,
                         CodeNodeType.eEnum
                        Dim lClass As CodeObject = BuildClassObject(lNode)
                        Console.WriteLine($"    Found class/module: {lClass.Name} (Lines {lClass.StartLine}-{lClass.EndLine})")
                        vClasses.Add(lClass)

                        ' A Class/Module/Structure can itself declare nested types (e.g.
                        ' GitManager.vb nests GitFileInfo/CommitInfo/BranchInfo classes and a
                        ' FileStatus enum inside GitManager) - those need their own dropdown
                        ' entries too, not just as invisible content of the outer type
                        CollectNestedTypes(lNode, lClass, vClasses)

                    Case CodeNodeType.eNamespace, CodeNodeType.eDocument
                        ' Not a class and not a member - the real content is inside it
                        CollectNavigationNodes(lNode, vClasses, vRootMembers)

                    Case Else
                        ' Root-level members (not in a class)
                        If IsMemberNode(lNode.NodeType) Then
                            vRootMembers.Add(BuildMember(lNode))
                            Console.WriteLine($"    Found root member: {lNode.Name}")
                        End If
                End Select
            Next

        Catch ex As Exception
            Console.WriteLine($"CollectNavigationNodes error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Recursively finds Class/Module/Interface/Structure/Enum nodes nested inside a
    ''' type (at any depth) and adds each as its own dropdown entry, with Parent/
    ''' NestingLevel set so the dropdown can render proper tree indentation
    ''' </summary>
    ''' <param name="vTypeNode">The containing type node to search inside</param>
    ''' <param name="vParent">The CodeObject for vTypeNode, becomes each direct child's Parent</param>
    ''' <param name="vClasses">Accumulates discovered nested types</param>
    Private Sub CollectNestedTypes(vTypeNode As SyntaxNode, vParent As CodeObject, vClasses As List(Of CodeObject))
        Try
            For Each lChild In vTypeNode.Children
                Select Case lChild.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule,
                         CodeNodeType.eInterface, CodeNodeType.eStructure,
                         CodeNodeType.eEnum
                        Dim lNested As CodeObject = BuildClassObject(lChild)
                        lNested.Parent = vParent
                        lNested.NestingLevel = vParent.NestingLevel + 1
                        Console.WriteLine($"    Found nested type: {lNested.Name} (Lines {lNested.StartLine}-{lNested.EndLine}, Level {lNested.NestingLevel})")
                        vClasses.Add(lNested)
                        CollectNestedTypes(lChild, lNested, vClasses)
                End Select
            Next

        Catch ex As Exception
            Console.WriteLine($"CollectNestedTypes error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Builds a CodeObject for a Class/Module/Interface/Structure/Enum node, including
    ''' its own direct members (not nested types - see CollectNestedTypes for those)
    ''' </summary>
    Private Function BuildClassObject(vNode As SyntaxNode) As CodeObject
        ' CodeObject.StartLine/EndLine are documented as 1-based (Models/CodeTypes.vb),
        ' but SyntaxNode.StartLine/EndLine are 0-based - convert here so UpdatePosition's
        ' bounds check and the click-to-navigate "StartLine - 1" math elsewhere in that
        ' class both work correctly
        Dim lClass As New CodeObject()
        lClass.Name = vNode.Name
        lClass.ObjectType = ConvertNodeTypeToObjectType(vNode.NodeType)
        lClass.StartLine = vNode.StartLine + 1
        lClass.EndLine = If(vNode.EndLine > 0, vNode.EndLine + 1, lClass.StartLine + 1)

        For Each lChild In vNode.Children
            If IsMemberNode(lChild.NodeType) Then
                Dim lMember As CodeMember = BuildMember(lChild)
                lClass.Members.Add(lMember)
                Console.WriteLine($"      Added member: {lMember.Name} (Lines {lMember.StartLine}-{lMember.EndLine})")
            End If
        Next

        Return lClass
    End Function

    ''' <summary>
    ''' Builds a CodeMember for a method/function/property/field/event/constructor node
    ''' </summary>
    Private Function BuildMember(vNode As SyntaxNode) As CodeMember
        Dim lMember As New CodeMember()
        lMember.Name = vNode.Name
        lMember.MemberType = ConvertNodeTypeToMemberType(vNode.NodeType)
        lMember.StartLine = vNode.StartLine + 1
        lMember.EndLine = If(vNode.EndLine > 0, vNode.EndLine + 1, lMember.StartLine)
        lMember.LineNumber = lMember.StartLine
        Return lMember
    End Function

    ' Convert node type to object type
    Private Function ConvertNodeTypeToObjectType(vNodeType As CodeNodeType) As CodeObjectType
        Select Case vNodeType
            Case CodeNodeType.eClass
                Return CodeObjectType.eClass
            Case CodeNodeType.eModule
                Return CodeObjectType.eModule
            Case CodeNodeType.eInterface
                Return CodeObjectType.eInterface
            Case CodeNodeType.eStructure
                Return CodeObjectType.eStructure
            Case CodeNodeType.eEnum
                Return CodeObjectType.eEnum
            Case Else
                Return CodeObjectType.eUnspecified
        End Select
    End Function
    
    ' Convert node type to member type
    Private Function ConvertNodeTypeToMemberType(vNodeType As CodeNodeType) As CodeMemberType
        Select Case vNodeType
            Case CodeNodeType.eMethod, CodeNodeType.eConstructor
                Return CodeMemberType.eMethod
            Case CodeNodeType.eFunction
                Return CodeMemberType.eFunction
            Case CodeNodeType.eProperty
                Return CodeMemberType.eProperty
            Case CodeNodeType.eField
                Return CodeMemberType.eField
            Case CodeNodeType.eEvent
                Return CodeMemberType.eEvent
            Case Else
                Return CodeMemberType.eUnspecified
        End Select
    End Function
    
    ' Check if node is a member type
    Private Function IsMemberNode(vNodeType As CodeNodeType) As Boolean
        Select Case vNodeType
            Case CodeNodeType.eMethod, CodeNodeType.eFunction, 
                 CodeNodeType.eProperty, CodeNodeType.eField, 
                 CodeNodeType.eEvent, CodeNodeType.eConstructor
                Return True
            Case Else
                Return False
        End Select
    End Function
    
    ' Go to line dialog
    Public Sub ShowGoToLineDialog()
        Try
            Dim lDialog As New Dialog("Go To Line", Me, DialogFlags.Modal)
            lDialog.SetDefaultSize(300, 120)
            
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            Dim lLabel As New Label("Enter Line number:")
            lVBox.PackStart(lLabel, False, False, 0)
            
            Dim lEntry As New Entry()
            lEntry.ActivatesDefault = True
            lVBox.PackStart(lEntry, False, False, 0)
            
            lDialog.ContentArea.PackStart(lVBox, True, True, 0)
            
            lDialog.AddButton("Cancel", ResponseType.Cancel)
            Dim lGoButton As Widget = lDialog.AddButton("Go", ResponseType.Ok)
            lDialog.Default = lGoButton
            
            lDialog.ShowAll()
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lLineNumber As Integer
                If Integer.TryParse(lEntry.Text, lLineNumber) AndAlso lLineNumber > 0 Then
                    NavigateToLine(lLineNumber)
                End If
            End If
            
            lDialog.Destroy()

        Catch ex As Exception
            Console.WriteLine($"ShowGoToLineDialog error: {ex.Message}")
        End Try
    End Sub

    ' Handle the GoToLineRequested event from editors
    Private Sub OnEditorGoToLineRequested()
        Try
            ' Show the existing Go To Line dialog
            ShowGoToLineDialog()

        Catch ex As Exception
            Console.WriteLine($"OnEditorGoToLineRequested error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Switches to the next tab in the notebook with navigation dropdown support
    ''' </summary>
    ''' <remarks>
    ''' Enhanced version that ensures navigation dropdowns are updated when switching tabs programmatically
    ''' </remarks>
    Private Sub SwitchToNextTab()
        Try
            ' Check if notebook exists and has tabs
            If pNotebook Is Nothing OrElse pNotebook.NPages = 0 Then
                Console.WriteLine("SwitchToNextTab: No tabs available")
                Return
            End If
            
            ' Get current page index
            Dim lCurrentPage As Integer = pNotebook.CurrentPage
            
            ' Calculate next page index (wrap around if at end)
            Dim lNextPage As Integer = lCurrentPage + 1
            If lNextPage >= pNotebook.NPages Then
                lNextPage = 0  ' Wrap to first tab
            End If
            
            Console.WriteLine($"SwitchToNextTab: Switching from page {lCurrentPage} To {lNextPage}")
            
            ' Switch to next tab (this will trigger OnNotebookSwitchPage)
            pNotebook.CurrentPage = lNextPage
            
            ' Get the new tab info
            Dim lTabInfo As TabInfo = GetTabInfo(lNextPage)
            If lTabInfo IsNot Nothing Then
                ' Ensure the editor gets focus
                If lTabInfo.Editor IsNot Nothing Then
                    lTabInfo.Editor.GrabFocus()
                End If
                
                ' Update status bar with current file
                Dim lFileName As String = System.IO.Path.GetFileName(lTabInfo.FilePath)
                UpdateStatusBar($"Switched To {lFileName}")
                
                Console.WriteLine($"SwitchToNextTab: Successfully switched To {lFileName}")
            Else
                Console.WriteLine("SwitchToNextTab: Warning - could Not Get tab info for New page")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToNextTab error: {ex.Message}")
        End Try
    End Sub
            
    ''' <summary>
    ''' Switches to the previous tab in the notebook with navigation dropdown support
    ''' </summary>
    ''' <remarks>
    ''' Enhanced version that ensures navigation dropdowns are updated when switching tabs programmatically
    ''' </remarks>
    Private Sub SwitchToPreviousTab()
        Try
            ' Check if notebook exists and has tabs
            If pNotebook Is Nothing OrElse pNotebook.NPages = 0 Then
                Console.WriteLine("SwitchToPreviousTab: No tabs available")
                Return
            End If
            
            ' Get current page index
            Dim lCurrentPage As Integer = pNotebook.CurrentPage
            
            ' Calculate previous page index (wrap around if at beginning)
            Dim lPreviousPage As Integer = lCurrentPage - 1
            If lPreviousPage < 0 Then
                lPreviousPage = pNotebook.NPages - 1  ' Wrap to last tab
            End If
            
            Console.WriteLine($"SwitchToPreviousTab: Switching from page {lCurrentPage} To {lPreviousPage}")
            
            ' Switch to previous tab (this will trigger OnNotebookSwitchPage)
            pNotebook.CurrentPage = lPreviousPage
            
            ' Get the new tab info
            Dim lTabInfo As TabInfo = GetTabInfo(lPreviousPage)
            If lTabInfo IsNot Nothing Then
                ' Ensure the editor gets focus
                If lTabInfo.Editor IsNot Nothing Then
                    lTabInfo.Editor.GrabFocus()
                End If
                
                ' Update status bar with current file
                Dim lFileName As String = System.IO.Path.GetFileName(lTabInfo.FilePath)
                UpdateStatusBar($"Switched To {lFileName}")
                
                Console.WriteLine($"SwitchToPreviousTab: Successfully switched To {lFileName}")
            Else
                Console.WriteLine("SwitchToPreviousTab: Warning - could Not Get tab info for New page")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToPreviousTab error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles navigation requests from the navigation dropdowns
    ''' </summary>
    ''' <param name="vLine">0-based line number to navigate to</param>
    ''' <remarks>
    ''' This method is called when a user selects an item from the navigation dropdowns.
    ''' It navigates to the specified line and ensures the editor has focus.
    ''' </remarks>
    Private Sub OnNavigationRequested(vLine As Integer)
        Try
            Console.WriteLine($"OnNavigationRequested: Navigating To line {vLine}")
            
            ' Get current tab
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then
                Console.WriteLine("OnNavigationRequested: No active tab Or editor")
                Return
            End If
            
            ' Navigate to the line
            If TypeOf lCurrentTab.Editor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = DirectCast(lCurrentTab.Editor, CustomDrawingEditor)
                
                ' Set cursor to beginning of the specified line
                lCustomEditor.SetCursorPosition(vLine, 0)
                
                ' Ensure line is visible
                lCustomEditor.ScrollToLine(vLine)
                
                ' Give focus to the editor
                lCustomEditor.GrabFocus()

                Console.WriteLine($"OnNavigationRequested: Successfully navigated To line {vLine}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnNavigationRequested error: {ex.Message}")
        End Try
    End Sub  
    
End Class
