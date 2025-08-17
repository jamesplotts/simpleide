' Widgets/NavigationDropdowns.vb - Fixed navigation dropdowns with proper imports
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ' Navigation dropdowns for class and member selection
    Public Class NavigationDropdowns
        Inherits Box
        
        ' UI Components
        Private pClassCombo As ComboBoxText
        Private pMemberCombo As ComboBoxText
        Private pClassLabel As Label
        Private pMemberLabel As Label
        
        ' Data
        Private pClasses As New List(Of CodeObject)()
        Private pMembers As New List(Of CodeMember)()
        Private pCurrentClass As String = ""
        Private pCurrentMember As String = ""
        Private pIsUpdating As Boolean = False
        
        ' Editor reference
        Private pEditor As IEditor
        
        ' Events
        Public Event NavigationRequested(vLine As Integer)
        
        ' Constructor
        Public Sub New()
            MyBase.New(Orientation.Horizontal, 5)
            
            Try
                ' Build UI
                BuildUI()
                
                ' Set initial state
                UpdateUI()
                
            Catch ex As Exception
                Console.WriteLine($"NavigationDropdowns initialization error: {ex.Message}")
            End Try
        End Sub
        
        ' Build UI
        Private Sub BuildUI()
            ' Class selection
            pClassLabel = New Label("Class:")
            PackStart(pClassLabel, False, False, 0)
            
            pClassCombo = New ComboBoxText()
            pClassCombo.WidthRequest = 200
            AddHandler pClassCombo.Changed, AddressOf OnClassChanged
            PackStart(pClassCombo, False, False, 0)
            
            ' Member selection
            pMemberLabel = New Label("member:")
            pMemberLabel.MarginStart = 10
            PackStart(pMemberLabel, False, False, 0)
            
            pMemberCombo = New ComboBoxText()
            pMemberCombo.WidthRequest = 250
            AddHandler pMemberCombo.Changed, AddressOf OnMemberChanged
            PackStart(pMemberCombo, False, False, 0)
            
            ShowAll()
        End Sub
        
        ' Set editor reference
        Public Sub SetEditor(vEditor As IEditor)
            pEditor = vEditor
            
            ' Subscribe to editor events
            If pEditor IsNot Nothing Then
                AddHandler pEditor.CursorPositionChanged, AddressOf OnEditorCursorChanged
                AddHandler pEditor.TextChanged, AddressOf OnEditorTextChanged
            End If
        End Sub
        
        ' Update navigation data
        Public Sub UpdateNavigationData(vClasses As List(Of CodeObject))
            Try
                pIsUpdating = True
                
                ' Store current selections
                Dim lPreviousClass As String = pCurrentClass
                Dim lPreviousMember As String = pCurrentMember
                
                ' Update classes
                pClasses.Clear()
                pClasses.AddRange(vClasses)
                
                ' Update UI
                UpdateClassCombo()
                
                ' Try to restore selection
                If Not String.IsNullOrEmpty(lPreviousClass) Then
                    For i As Integer = 0 To pClassCombo.Model.IterNChildren() - 1
                        Dim lIter As TreeIter = Nothing
                        If pClassCombo.Model.IterNthChild(lIter, i) Then
                            If pClassCombo.Model.GetValue(lIter, 0).ToString() = lPreviousClass Then
                                pClassCombo.Active = i
                                Exit For
                            End If
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateNavigationData error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ' Update dropdowns based on cursor position
        Public Sub UpdateFromCursorPosition(vLine As Integer, vColumn As Integer)
            Try
                If pIsUpdating Then Return
                
                ' Find containing class
                Dim lContainingClass As CodeObject = Nothing
                For Each lClass In pClasses
                    If vLine >= lClass.StartLine - 1 AndAlso vLine <= lClass.EndLine - 1 Then
                        lContainingClass = lClass
                        Exit For
                    End If
                Next
                
                If lContainingClass IsNot Nothing Then
                    ' Update class selection
                    If lContainingClass.Name <> pCurrentClass Then
                        pIsUpdating = True
                        SelectClassByName(lContainingClass.Name)
                        pIsUpdating = False
                    End If
                    
                    ' Find containing member
                    Dim lContainingMember As CodeMember = Nothing
                    For Each lMember In lContainingClass.members
                        If vLine >= lMember.StartLine - 1 AndAlso vLine <= lMember.EndLine - 1 Then
                            lContainingMember = lMember
                            Exit For
                        End If
                    Next
                    
                    If lContainingMember IsNot Nothing Then
                        ' Update member selection
                        If lContainingMember.Name <> pCurrentMember Then
                            pIsUpdating = True
                            SelectMemberByName(lContainingMember.Name)
                            pIsUpdating = False
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromCursorPosition error: {ex.Message}")
            End Try
        End Sub
        
        ' Handle class selection change
        Private Sub OnClassChanged(vSender As Object, vE As EventArgs)
            Try
                If pIsUpdating Then Return
                
                If pClassCombo.Active >= 0 Then
                    pCurrentClass = pClassCombo.ActiveText
                    
                    ' Update members for selected class
                    UpdateMembersForClass(pCurrentClass)
                    
                    ' Navigate to class
                    Dim lSelectedClass As CodeObject = FindClassByName(pCurrentClass)
                    If lSelectedClass IsNot Nothing Then
                        RaiseEvent NavigationRequested(lSelectedClass.StartLine - 1)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnClassChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' Handle member selection change
        Private Sub OnMemberChanged(vSender As Object, vE As EventArgs)
            Try
                If pIsUpdating Then Return
                
                If pMemberCombo.Active >= 0 Then
                    pCurrentMember = GetMemberNameFromDisplay(pMemberCombo.ActiveText)
                    
                    ' Navigate to member
                    Dim lSelectedMember As CodeMember = FindMemberByName(pCurrentMember)
                    If lSelectedMember IsNot Nothing Then
                        RaiseEvent NavigationRequested(lSelectedMember.StartLine - 1)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnMemberChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' Handle editor cursor change
        Private Sub OnEditorCursorChanged(vLine As Integer, vColumn As Integer)
            UpdateFromCursorPosition(vLine, vColumn)
        End Sub
        
        ' Handle editor text change
        Private Sub OnEditorTextChanged()
            ' Text changed - navigation data will be updated by the editor
        End Sub
        
        ' Update class combo
        Private Sub UpdateClassCombo()
            Try
                pClassCombo.RemoveAll()
                
                If pClasses.Count = 0 Then
                    pClassCombo.AppendText("(No classes)")
                    pClassCombo.Active = 0
                    pClassCombo.Sensitive = False
                Else
                    pClassCombo.Sensitive = True
                    
                    ' Add classes
                    For Each lClass In pClasses
                        pClassCombo.AppendText(lClass.DisplayText)
                    Next
                    
                    ' Select first if nothing selected
                    If pClassCombo.Active < 0 AndAlso pClasses.Count > 0 Then
                        pClassCombo.Active = 0
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateClassCombo error: {ex.Message}")
            End Try
        End Sub
        
        ' Update members for selected class
        Private Sub UpdateMembersForClass(vClassName As String)
            Try
                pIsUpdating = True
                pMemberCombo.RemoveAll()
                pMembers.Clear()
                
                ' Find the class
                Dim lClass As CodeObject = FindClassByDisplayText(vClassName)
                If lClass IsNot Nothing Then
                    pMembers.AddRange(lClass.members)
                    
                    If pMembers.Count = 0 Then
                        pMemberCombo.AppendText("(No members)")
                        pMemberCombo.Active = 0
                        pMemberCombo.Sensitive = False
                    Else
                        pMemberCombo.Sensitive = True
                        
                        ' Add members
                        For Each lMember In pMembers
                            pMemberCombo.AppendText(lMember.DisplayText)
                        Next
                        
                        ' Select first if nothing selected
                        If pMemberCombo.Active < 0 Then
                            pMemberCombo.Active = 0
                        End If
                    End If
                Else
                    pMemberCombo.AppendText("(No members)")
                    pMemberCombo.Active = 0
                    pMemberCombo.Sensitive = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateMembersForClass error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ' Helper methods
        Private Function FindClassByName(vName As String) As CodeObject
            For Each lClass In pClasses
                If lClass.Name = vName Then
                    Return lClass
                End If
            Next
            Return Nothing
        End Function
        
        Private Function FindClassByDisplayText(vDisplayText As String) As CodeObject
            For Each lClass In pClasses
                If lClass.DisplayText = vDisplayText Then
                    Return lClass
                End If
            Next
            Return Nothing
        End Function
        
        Private Function FindMemberByName(vName As String) As CodeMember
            For Each lMember In pMembers
                If lMember.Name = vName Then
                    Return lMember
                End If
            Next
            Return Nothing
        End Function
        
        Private Sub SelectClassByName(vName As String)
            For i As Integer = 0 To pClasses.Count - 1
                If pClasses(i).Name = vName Then
                    pClassCombo.Active = i
                    Exit For
                End If
            Next
        End Sub
        
        Private Sub SelectMemberByName(vName As String)
            For i As Integer = 0 To pMembers.Count - 1
                If pMembers(i).Name = vName Then
                    pMemberCombo.Active = i
                    Exit For
                End If
            Next
        End Sub
        
        Private Function GetMemberNameFromDisplay(vDisplayText As String) As String
            ' Extract member name from display text
            ' For example: "Sub MyMethod(param As String)" -> "MyMethod"
            For Each lMember In pMembers
                If lMember.DisplayText = vDisplayText Then
                    Return lMember.Name
                End If
            Next
            Return vDisplayText
        End Function
        
        ' Clear navigation data
        Public Sub Clear()
            pIsUpdating = True
            pClasses.Clear()
            pMembers.Clear()
            pCurrentClass = ""
            pCurrentMember = ""
            UpdateUI()
            pIsUpdating = False
        End Sub
        
        ' Update UI state
        Private Sub UpdateUI()
            If pClasses.Count = 0 Then
                pClassCombo.RemoveAll()
                pClassCombo.AppendText("(No classes)")
                pClassCombo.Active = 0
                pClassCombo.Sensitive = False
                
                pMemberCombo.RemoveAll()
                pMemberCombo.AppendText("(No members)")
                pMemberCombo.Active = 0
                pMemberCombo.Sensitive = False
            End If
        End Sub
        
        ' Get current selections
        Public ReadOnly Property CurrentClass As String
            Get
                Return pCurrentClass
            End Get
        End Property
        
        Public ReadOnly Property CurrentMember As String
            Get
                Return pCurrentMember
            End Get
        End Property

        ' Set navigation data with both classes and root members
        Public Sub SetNavigationData(vClasses As List(Of CodeObject), vRootMembers As List(Of CodeMember))
            Try
                pIsUpdating = True
                
                ' Update classes
                pClasses.Clear()
                pClasses.AddRange(vClasses)
                
                ' Update root members (store them but don't display in class combo)
                pMembers.Clear()
                If vRootMembers IsNot Nothing AndAlso vRootMembers.Count > 0 Then
                    pMembers.AddRange(vRootMembers)
                End If
                
                ' Update UI
                UpdateClassCombo()
                
                ' If we have root members but no classes, show them in member combo
                If pClasses.Count = 0 AndAlso pMembers.Count > 0 Then
                    UpdateMembersCombo()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetNavigationData error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ' Update position based on current line
        Public Sub UpdatePosition(vCurrentLine As Integer)
            Try
                ' Convert to 1-based line number for comparison
                Dim lLine As Integer = vCurrentLine + 1
                
                ' Update dropdowns based on line position
                UpdateFromCursorPosition(vCurrentLine, 0)
                
            Catch ex As Exception
                Console.WriteLine($"UpdatePosition error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateMembersCombo()
            Try
                pMemberCombo.RemoveAll()
                
                If pMembers.Count = 0 Then
                    pMemberCombo.AppendText("(No members)")
                    pMemberCombo.Active = 0
                    pMemberCombo.Sensitive = False
                Else
                    pMemberCombo.Sensitive = True
                    
                    ' Add members
                    For Each lMember In pMembers
                        pMemberCombo.AppendText(lMember.DisplayText)
                    Next
                    
                    ' Select first if nothing selected
                    If pMemberCombo.Active < 0 AndAlso pMembers.Count > 0 Then
                        pMemberCombo.Active = 0
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateMembersCombo error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
