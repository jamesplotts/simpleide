' MainWindow.Navigation.vb - Navigation functionality for MainWindow
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors

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
    
    ' Update navigation dropdowns for current editor
    Public Sub UpdateNavigationDropdowns()
        Try
            Dim lTabInfo As TabInfo = GetCurrentTabInfo()
            If lTabInfo Is Nothing OrElse lTabInfo.NavigationDropdowns Is Nothing Then
                Return
            End If
            
            Dim lEditor As CustomDrawingEditor = TryCast(lTabInfo.Editor, CustomDrawingEditor)
            If lEditor Is Nothing Then Return
            
            ' Get all nodes from editor
            Dim lNodes As List(Of DocumentNode) = lEditor.GetAllNodes()
            
            ' Convert to navigation format
            Dim lClasses As New List(Of CodeObject)
            Dim lRootMembers As New List(Of CodeMember)
            
            For Each lNode In lNodes
                Select Case lNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule, 
                         CodeNodeType.eInterface, CodeNodeType.eStructure
                        Dim lClass As New CodeObject()
                        lClass.Name = lNode.Name
                        lClass.ObjectType = ConvertNodeTypeToObjectType(lNode.NodeType)
                        lClass.StartLine = lNode.StartLine + 1
                        
                        ' Add members
                        For Each lChild In lNode.Children
                            If IsMemberNode(lChild.NodeType) Then
                                Dim lMember As New CodeMember()
                                lMember.Name = lChild.Name
                                lMember.MemberType = ConvertNodeTypeToMemberType(lChild.NodeType)
                                lMember.LineNumber = lChild.StartLine + 1
                                ' DisplayText is a ReadOnly property, so we don't set it
                                lClass.members.Add(lMember)
                            End If
                        Next
                        
                        lClasses.Add(lClass)
                        
                    Case Else
                        ' Root-level members (not in a class)
                        If IsMemberNode(lNode.NodeType) Then
                            Dim lMember As New CodeMember()
                            lMember.Name = lNode.Name
                            lMember.MemberType = ConvertNodeTypeToMemberType(lNode.NodeType)
                            lMember.LineNumber = lNode.StartLine + 1
                            ' DisplayText is a ReadOnly property, so we don't set it
                            lRootMembers.Add(lMember)
                        End If
                End Select
            Next
            
            ' Update dropdowns
            lTabInfo.NavigationDropdowns.SetNavigationData(lClasses, lRootMembers)
            
            ' Update current position - GetCursorLine is in NodeInfo class, not CustomDrawingEditor
            ' We need to use the pCursorLine property directly via reflection or a public method
            Dim lCurrentLine As Integer = lEditor.CurrentLine
            lTabInfo.NavigationDropdowns.UpdatePosition(lCurrentLine)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateNavigationDropdowns error: {ex.Message}")
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
    
    ' Handle navigation dropdown request
    Private Sub OnNavigationRequested(vLine As Integer)
        Try
            NavigateToLine(vLine)
        Catch ex As Exception
            Console.WriteLine($"OnNavigationRequested error: {ex.Message}")
        End Try
    End Sub
    
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
    ''' Switches to the next tab in the notebook (Ctrl+Tab functionality)
    ''' </summary>
    Private Sub SwitchToNextTab()
        Try
            ' Check if notebook exists and has tabs
            If pNotebook Is Nothing OrElse pNotebook.NPages = 0 Then
                Return
            End If
            
            ' Get current page index
            Dim lCurrentPage As Integer = pNotebook.CurrentPage
            
            ' Calculate next page index (wrap around if at end)
            Dim lNextPage As Integer = lCurrentPage + 1
            If lNextPage >= pNotebook.NPages Then
                lNextPage = 0  ' Wrap to first tab
            End If
            
            ' Switch to next tab
            pNotebook.CurrentPage = lNextPage
            
            ' Ensure the editor gets focus
            Dim lTabInfo As TabInfo = GetTabInfo(lNextPage)
            If lTabInfo IsNot Nothing AndAlso lTabInfo.Editor IsNot Nothing Then
                ' Give focus to the editor widget
                lTabInfo.Editor.Widget.GrabFocus()
                
                ' Update status bar with current file
                Dim lFileName As String = System.IO.Path.GetFileName(lTabInfo.FilePath)
                UpdateStatusBar($"Switched To {lFileName}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToNextTab error: {ex.Message}")
        End Try
    End Sub        
            
    ''' <summary>
    ''' Switches to the previous tab in the notebook (Ctrl+Shift+Tab functionality)
    ''' </summary>
    Private Sub SwitchToPreviousTab()
        Try
            ' Check if notebook exists and has tabs
            If pNotebook Is Nothing OrElse pNotebook.NPages = 0 Then
                Return
            End If
            
            ' Get current page index
            Dim lCurrentPage As Integer = pNotebook.CurrentPage
            
            ' Calculate previous page index (wrap around if at beginning)
            Dim lPreviousPage As Integer = lCurrentPage - 1
            If lPreviousPage < 0 Then
                lPreviousPage = pNotebook.NPages - 1  ' Wrap to last tab
            End If
            
            ' Switch to previous tab
            pNotebook.CurrentPage = lPreviousPage
            
            ' Ensure the editor gets focus
            Dim lTabInfo As TabInfo = GetTabInfo(lPreviousPage)
            If lTabInfo IsNot Nothing AndAlso lTabInfo.Editor IsNot Nothing Then
                ' Give focus to the editor widget
                lTabInfo.Editor.Widget.GrabFocus()
                
                ' Update status bar with current file
                Dim lFileName As String = System.IO.Path.GetFileName(lTabInfo.FilePath)
                UpdateStatusBar($"Switched To {lFileName}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToPreviousTab error: {ex.Message}")
        End Try
    End Sub        
    
End Class
