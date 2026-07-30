' Replace: SimpleIDE.Widgets.NavigationDropdowns

' Widgets/NavigationDropdowns.vb - Enhanced navigation dropdowns with (General) and (Declarations) support
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Navigation dropdowns providing classic VB-style class and member navigation
    ''' </summary>
    Public Class NavigationDropdowns
        Inherits Box
        
        ' UI Components
        Private pClassCombo As ComboBoxText
        Private pMemberCombo As ComboBoxText
        Private pClassLabel As Label
        Private pMemberLabel As Label
        
        ' Data storage
        Private pClasses As New List(Of CodeObject)()
        Private pRootMembers As New List(Of CodeMember)()
        Private pCurrentMembers As New List(Of CodeMember)()
        Private pCurrentClass As String = ""
        Private pCurrentMember As String = ""
        Private pIsUpdating As Boolean = False
        
        ' Constants for special entries
        Private Const GENERAL_ITEM As String = "(General)"
        Private Const DECLARATIONS_ITEM As String = "(Declarations)"
        Private Const NO_CLASSES_ITEM As String = "(No classes)"
        Private Const NO_MEMBERS_ITEM As String = "(No members)"
        
        ' Editor reference
        Private pEditor As IEditor
        
        ' Events
        Public Event NavigationRequested(vLine As Integer)
        
        ''' <summary>
        ''' Initializes the navigation dropdowns widget
        ''' </summary>
        Public Sub New()
            MyBase.New(Orientation.Horizontal, 5)
            
            Try
                BuildUI()
                SetInitialState()
                
            Catch ex As Exception
                Console.WriteLine($"NavigationDropdowns initialization error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds the user interface components
        ''' </summary>
        Private Sub BuildUI()
            Try
                ' Class selection label and dropdown
                pClassLabel = New Label("Class:")
                pClassLabel.Halign = Align.Start
                PackStart(pClassLabel, False, False, 0)
                
                pClassCombo = New ComboBoxText()
                pClassCombo.WidthRequest = 200
                AddHandler pClassCombo.Changed, AddressOf OnClassChanged
                PackStart(pClassCombo, False, False, 0)
                
                ' Member selection label and dropdown
                pMemberLabel = New Label("Member:")
                pMemberLabel.MarginStart = 10
                pMemberLabel.Halign = Align.Start
                PackStart(pMemberLabel, False, False, 0)
                
                pMemberCombo = New ComboBoxText()
                pMemberCombo.WidthRequest = 250
                AddHandler pMemberCombo.Changed, AddressOf OnMemberChanged
                PackStart(pMemberCombo, False, False, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the initial state of the dropdowns
        ''' </summary>
        Private Sub SetInitialState()
            Try
                pIsUpdating = True
                
                ' Set default state
                pClassCombo.RemoveAll()
                pClassCombo.AppendText(NO_CLASSES_ITEM)
                pClassCombo.Active = 0
                pClassCombo.Sensitive = False
                
                pMemberCombo.RemoveAll()
                pMemberCombo.AppendText(NO_MEMBERS_ITEM)
                pMemberCombo.Active = 0
                pMemberCombo.Sensitive = False
                
            Catch ex As Exception
                Console.WriteLine($"SetInitialState error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the editor reference and connects to editor events
        ''' </summary>
        Public Sub SetEditor(vEditor As IEditor)
            Try
                ' Unhook from previous editor if any
                If pEditor IsNot Nothing Then
                    RemoveHandler pEditor.CursorPositionChanged, AddressOf OnEditorCursorChanged
                    RemoveHandler pEditor.TextChanged, AddressOf OnEditorTextChanged
                End If
                
                pEditor = vEditor
                
                ' Hook up to new editor events
                If pEditor IsNot Nothing Then
                    AddHandler pEditor.CursorPositionChanged, AddressOf OnEditorCursorChanged
                    AddHandler pEditor.TextChanged, AddressOf OnEditorTextChanged
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetEditor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates navigation data with classes and root-level members
        ''' </summary>
        Public Sub SetNavigationData(vClasses As List(Of CodeObject), vRootMembers As List(Of CodeMember))
            Try
                pIsUpdating = True
                
                ' Store current selections to restore them
                Dim lPreviousClass As String = pCurrentClass
                Dim lPreviousMember As String = pCurrentMember
                
                ' Update internal data
                pClasses.Clear()
                pRootMembers.Clear()
                
                If vClasses IsNot Nothing Then
                    pClasses.AddRange(vClasses)
                End If
                
                If vRootMembers IsNot Nothing Then
                    pRootMembers.AddRange(vRootMembers)
                End If
                
                ' Rebuild UI
                UpdateClassDropdown()
                
                ' Restore previous selection if possible
                RestoreSelection(lPreviousClass, lPreviousMember)
                
            Catch ex As Exception
                Console.WriteLine($"SetNavigationData error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the class dropdown with available classes
        ''' </summary>
        Private Sub UpdateClassDropdown()
            Try
                pClassCombo.RemoveAll()
                
                ' Always add (General) as first option
                pClassCombo.AppendText(GENERAL_ITEM)
                
                ' Add classes if any exist
                If pClasses.Count > 0 Then
                    for each lClass in pClasses
                        pClassCombo.AppendText(lClass.DisplayText)
                    Next
                    pClassCombo.Sensitive = True
                Else
                    pClassCombo.Sensitive = True ' Still sensitive for (General)
                End If
                
                ' Set initial selection to (General)
                pClassCombo.Active = 0
                pCurrentClass = GENERAL_ITEM
                
                ' Update members for (General)
                UpdateMemberDropdown()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateClassDropdown error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the member dropdown based on current class selection
        ''' </summary>
        Private Sub UpdateMemberDropdown()
            Try
                pMemberCombo.RemoveAll()
                pCurrentMembers.Clear()
                
                If pCurrentClass = GENERAL_ITEM Then
                    ' Show (Declarations) and root-level members
                    pMemberCombo.AppendText(DECLARATIONS_ITEM)
                    
                    ' Add root-level members
                    If pRootMembers.Count > 0 Then
                        for each lMember in pRootMembers
                            pMemberCombo.AppendText(lMember.DisplayText)
                            pCurrentMembers.Add(lMember)
                        Next
                        pMemberCombo.Sensitive = True
                    Else
                        pMemberCombo.Sensitive = True ' Still sensitive for (Declarations)
                    End If
                    
                    ' Select (Declarations) by default
                    pMemberCombo.Active = 0
                    pCurrentMember = DECLARATIONS_ITEM
                    
                Else
                    ' Show members of the selected class
                    Dim lSelectedClass As CodeObject = FindClassByDisplayText(pCurrentClass)
                    If lSelectedClass IsNot Nothing AndAlso lSelectedClass.members.Count > 0 Then
                        for each lMember in lSelectedClass.members
                            pMemberCombo.AppendText(lMember.DisplayText)
                            pCurrentMembers.Add(lMember)
                        Next
                        pMemberCombo.Sensitive = True
                        pMemberCombo.Active = 0
                    Else
                        pMemberCombo.AppendText(NO_MEMBERS_ITEM)
                        pMemberCombo.Active = 0
                        pMemberCombo.Sensitive = False
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateMemberDropdown error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles class dropdown selection changes
        ''' </summary>
        Private Sub OnClassChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdating OrElse pClassCombo.Active < 0 Then Return
                
                Dim lSelectedText As String = pClassCombo.ActiveText
                If String.IsNullOrEmpty(lSelectedText) Then Return
                
                pCurrentClass = lSelectedText
                UpdateMemberDropdown()
                
                ' Navigate to class if it's not (General)
                If pCurrentClass <> GENERAL_ITEM Then
                    Dim lClass As CodeObject = FindClassByDisplayText(pCurrentClass)
                    If lClass IsNot Nothing Then
                        RaiseEvent NavigationRequested(lClass.StartLine - 1)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnClassChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles member dropdown selection changes
        ''' </summary>
        Private Sub OnMemberChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdating OrElse pMemberCombo.Active < 0 Then Return
                
                Dim lSelectedText As String = pMemberCombo.ActiveText
                If String.IsNullOrEmpty(lSelectedText) Then Return
                
                pCurrentMember = lSelectedText
                
                ' Navigate to member if it's not a special item
                If pCurrentMember <> DECLARATIONS_ITEM AndAlso pCurrentMember <> NO_MEMBERS_ITEM Then
                    Dim lMember As CodeMember = FindMemberByDisplayText(pCurrentMember)
                    If lMember IsNot Nothing Then
                        RaiseEvent NavigationRequested(lMember.StartLine - 1)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnMemberChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates dropdown position based on cursor location
        ''' </summary>
        Public Sub UpdatePosition(vCurrentLine As Integer)
            Try
                If pIsUpdating Then Return
                
                pIsUpdating = True
                
                ' Convert to 1-based line number for comparison
                Dim lLine As Integer = vCurrentLine + 1
                
                ' Find containing class
                Dim lContainingClass As CodeObject = Nothing
                for each lClass in pClasses
                    If lLine >= lClass.StartLine AndAlso lLine <= lClass.EndLine Then
                        lContainingClass = lClass
                        Exit for
                    End If
                Next
                
                If lContainingClass IsNot Nothing Then
                    ' Update class selection
                    SelectClassByDisplayText(lContainingClass.DisplayText)
                    
                    ' Find containing member within class
                    Dim lContainingMember As CodeMember = Nothing
                    for each lMember in lContainingClass.members
                        If lLine >= lMember.StartLine AndAlso lLine <= lMember.EndLine Then
                            lContainingMember = lMember
                            Exit for
                        End If
                    Next
                    
                    If lContainingMember IsNot Nothing Then
                        SelectMemberByDisplayText(lContainingMember.DisplayText)
                    End If
                    
                Else
                    ' Not in any class - select (General)
                    SelectClassByDisplayText(GENERAL_ITEM)
                    
                    ' Check if in a root-level member
                    Dim lContainingRootMember As CodeMember = Nothing
                    for each lMember in pRootMembers
                        If lLine >= lMember.StartLine AndAlso lLine <= lMember.EndLine Then
                            lContainingRootMember = lMember
                            Exit for
                        End If
                    Next
                    
                    If lContainingRootMember IsNot Nothing Then
                        SelectMemberByDisplayText(lContainingRootMember.DisplayText)
                    Else
                        ' Select (Declarations)
                        SelectMemberByDisplayText(DECLARATIONS_ITEM)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdatePosition error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles editor cursor position changes
        ''' </summary>
        Private Sub OnEditorCursorChanged(vLine As Integer, vColumn As Integer)
            UpdatePosition(vLine)
        End Sub
        
        ''' <summary>
        ''' Handles editor text changes
        ''' </summary>
        Private Sub OnEditorTextChanged(vSender As Object, vArgs As EventArgs)
            ' Text changed - navigation data will be updated by the main window
        End Sub
        
        ''' <summary>
        ''' Attempts to restore previous class and member selection
        ''' </summary>
        Private Sub RestoreSelection(vPreviousClass As String, vPreviousMember As String)
            Try
                If Not String.IsNullOrEmpty(vPreviousClass) Then
                    SelectClassByDisplayText(vPreviousClass)
                End If
                
                If Not String.IsNullOrEmpty(vPreviousMember) Then
                    SelectMemberByDisplayText(vPreviousMember)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RestoreSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Selects a class by its display text
        ''' </summary>
        Private Sub SelectClassByDisplayText(vDisplayText As String)
            Try
                for i As Integer = 0 To pClassCombo.Model.IterNChildren() - 1
                    Dim lIter As TreeIter = Nothing
                    If pClassCombo.Model.IterNthChild(lIter, i) Then
                        Dim lText As String = pClassCombo.Model.GetValue(lIter, 0).ToString()
                        If lText = vDisplayText Then
                            pClassCombo.Active = i
                            pCurrentClass = vDisplayText
                            UpdateMemberDropdown()
                            Exit for
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SelectClassByDisplayText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Selects a member by its display text
        ''' </summary>
        Private Sub SelectMemberByDisplayText(vDisplayText As String)
            Try
                for i As Integer = 0 To pMemberCombo.Model.IterNChildren() - 1
                    Dim lIter As TreeIter = Nothing
                    If pMemberCombo.Model.IterNthChild(lIter, i) Then
                        Dim lText As String = pMemberCombo.Model.GetValue(lIter, 0).ToString()
                        If lText = vDisplayText Then
                            pMemberCombo.Active = i
                            pCurrentMember = vDisplayText
                            Exit for
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SelectMemberByDisplayText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Finds a class by its display text
        ''' </summary>
        Private Function FindClassByDisplayText(vDisplayText As String) As CodeObject
            for each lClass in pClasses
                If lClass.DisplayText = vDisplayText Then
                    Return lClass
                End If
            Next
            Return Nothing
        End Function
        
        ''' <summary>
        ''' Finds a member by its display text in current members list
        ''' </summary>
        Private Function FindMemberByDisplayText(vDisplayText As String) As CodeMember
            for each lMember in pCurrentMembers
                If lMember.DisplayText = vDisplayText Then
                    Return lMember
                End If
            Next
            Return Nothing
        End Function
        
        ''' <summary>
        ''' Clears all navigation data and resets to initial state
        ''' </summary>
        Public Sub Clear()
            Try
                pIsUpdating = True
                
                pClasses.Clear()
                pRootMembers.Clear()
                pCurrentMembers.Clear()
                pCurrentClass = ""
                pCurrentMember = ""
                
                SetInitialState()
                
            Catch ex As Exception
                Console.WriteLine($"Clear error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the currently selected class name
        ''' </summary>
        Public ReadOnly Property CurrentClass As String
            Get
                Return pCurrentClass
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the currently selected member name
        ''' </summary>
        Public ReadOnly Property CurrentMember As String
            Get
                Return pCurrentMember
            End Get
        End Property
        
    End Class
    
End Namespace