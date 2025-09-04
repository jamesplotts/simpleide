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
                lEditor.Widget.GrabFocus()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"NavigateToLine error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub NavigateToDefinition()
        Try
            Dim lEditor As CustomDrawingEditor = TryCast(GetCurrentEditor(), CustomDrawingEditor)
            If lEditor Is Nothing Then Return
            
            ' Get word at cursor
            Dim lWord As String = lEditor.GetWordAtCursor()
            If String.IsNullOrEmpty(lWord) Then Return
            
            ' Find definition in current file
            Dim lDefinitionLine As Integer = FindDefinitionInFile(lEditor, lWord)
            
            If lDefinitionLine >= 0 Then
                NavigateToLine(lDefinitionLine + 1)
            Else
                ' Search in other open files
                Dim lDefinitionLineInOtherFiles As Integer = FindDefinitionInOpenFiles(lWord)
                If lDefinitionLineInOtherFiles >= 0 Then
                    ' Definition found in another file, it should have switched to that file
                    Return
                End If
                
                ' Search in project files
                If Not String.IsNullOrEmpty(pCurrentProject) Then
                    FindDefinitionInProject(lWord)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"NavigateToDefinition error: {ex.Message}")
        End Try
    End Sub    

    ' Find definition in current file
    Private Function FindDefinitionInFile(vEditor As CustomDrawingEditor, vSymbol As String) As Integer
        Try
            ' Get parsed nodes from editor
            Dim lNodes As List(Of DocumentNode) = vEditor.GetAllNodes()
            
            for each lNode in lNodes
                If lNode.Name = vSymbol Then
                    ' Consider it a definition if it's a class, method, property, etc.
                    Select Case lNode.NodeType
                        Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface,
                             CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eProperty,
                             CodeNodeType.eField, CodeNodeType.eEvent
                            Return lNode.StartLine
                    End Select
                End If
            Next
            
            Return -1
            
        Catch ex As Exception
            Console.WriteLine($"FindDefinitionInFile error: {ex.Message}")
            Return -1
        End Try
    End Function
    
    ' Find definition in open files
    Private Function FindDefinitionInOpenFiles(vSymbol As String) As Integer
        Try
            for each lTabInfo in pOpenTabs.Values
                If lTabInfo.Editor Is Nothing Then Continue for
                
                Dim lCustomEditor As CustomDrawingEditor = TryCast(lTabInfo.Editor, CustomDrawingEditor)
                If lCustomEditor Is Nothing Then Continue for
                
                Dim lLine As Integer = FindDefinitionInFile(lCustomEditor, vSymbol)
                If lLine >= 0 Then
                    ' Switch to this tab
                    for i As Integer = 0 To pNotebook.NPages - 1
                        If pNotebook.GetNthPage(i) Is lTabInfo.EditorContainer Then
                            pNotebook.CurrentPage = i
                            NavigateToLine(lLine + 1)
                            Return lLine
                        End If
                    Next
                End If
            Next
            
            Return -1
            
        Catch ex As Exception
            Console.WriteLine($"FindDefinitionInOpenFiles error: {ex.Message}")
            Return -1
        End Try
    End Function
    
    ' Find definition in project files
    Private Sub FindDefinitionInProject(vSymbol As String)
        Try
            ' TODO: Implement project-wide symbol search
            ' This would involve:
            ' 1. Getting list of all VB files in project
            ' 2. Parsing each file to find symbol
            ' 3. Opening file and navigating to definition
            
            UpdateStatusBar($"definition of '{vSymbol}' not found in open files")
            
        Catch ex As Exception
            Console.WriteLine($"FindDefinitionInProject error: {ex.Message}")
        End Try
    End Sub
    
    ' Navigate forward in navigation history
    Public Sub NavigateForward()
        Try
            ' TODO: Implement navigation history
            UpdateStatusBar("Navigate forward Not yet implemented")
            
        Catch ex As Exception
            Console.WriteLine($"NavigateForward error: {ex.Message}")
        End Try
    End Sub
    
    ' Navigate backward in navigation history  
    Public Sub NavigateBackward()
        Try
            ' TODO: Implement navigation history
            UpdateStatusBar("Navigate backward Not yet implemented")
            
        Catch ex As Exception
            Console.WriteLine($"NavigateBackward error: {ex.Message}")
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
            
            ' Process top-level nodes
            For Each lNode In lRootNode.Children
                Console.WriteLine($"  Processing node: {lNode.Name} (Type: {lNode.NodeType})")
                
                Select Case lNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule, 
                         CodeNodeType.eInterface, CodeNodeType.eStructure
                        ' Create class object
                        Dim lClass As New CodeObject()
                        lClass.Name = lNode.Name
                        lClass.ObjectType = ConvertNodeTypeToObjectType(lNode.NodeType)
                        lClass.StartLine = lNode.StartLine
                        lClass.EndLine = If(lNode.EndLine > 0, lNode.EndLine, lNode.StartLine + 1)
                        
                        Console.WriteLine($"    Found class/module: {lClass.Name} (Lines {lClass.StartLine}-{lClass.EndLine})")
                        
                        ' Add members
                        For Each lChild In lNode.Children
                            If IsMemberNode(lChild.NodeType) Then
                                Dim lMember As New CodeMember()
                                lMember.Name = lChild.Name
                                lMember.MemberType = ConvertNodeTypeToMemberType(lChild.NodeType)
                                lMember.StartLine = lChild.StartLine
                                lMember.EndLine = If(lChild.EndLine > 0, lChild.EndLine, lChild.StartLine)
                                lMember.LineNumber = lChild.StartLine + 1 ' 1-based for display
                                lClass.Members.Add(lMember)
                                Console.WriteLine($"      Added member: {lMember.Name} (Lines {lMember.StartLine}-{lMember.EndLine})")
                            End If
                        Next
                        
                        lClasses.Add(lClass)
                        
                    Case Else
                        ' Root-level members (not in a class)
                        If IsMemberNode(lNode.NodeType) Then
                            Dim lMember As New CodeMember()
                            lMember.Name = lNode.Name
                            lMember.MemberType = ConvertNodeTypeToMemberType(lNode.NodeType)
                            lMember.StartLine = lNode.StartLine
                            lMember.EndLine = If(lNode.EndLine > 0, lNode.EndLine, lNode.StartLine)
                            lMember.LineNumber = lNode.StartLine + 1 ' 1-based for display
                            lRootMembers.Add(lMember)
                            Console.WriteLine($"    Found root member: {lMember.Name} (Lines {lMember.StartLine}-{lMember.EndLine})")
                        End If
                End Select
            Next
            
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
                If lTabInfo.Editor?.Widget IsNot Nothing Then
                    lTabInfo.Editor.Widget.GrabFocus()
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
                If lTabInfo.Editor?.Widget IsNot Nothing Then
                    lTabInfo.Editor.Widget.GrabFocus()
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
                lCustomEditor.Widget.GrabFocus()
                
                Console.WriteLine($"OnNavigationRequested: Successfully navigated To line {vLine}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnNavigationRequested error: {ex.Message}")
        End Try
    End Sub  
    
End Class
