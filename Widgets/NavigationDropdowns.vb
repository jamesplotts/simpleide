' Replace: SimpleIDE.Widgets.NavigationDropdowns

' Widgets/NavigationDropdowns.vb - Custom-drawn class/member navigation dropdowns
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' Navigation dropdowns providing classic VB-style class and member navigation, using
    ''' custom-drawn popups (NavigationDropdownPopup) instead of GTK's native ComboBoxText
    ''' rendering - the Class popup shows an always-expanded indented tree (with nested
    ''' types), the Member popup shows a flat alphabetically-sorted list
    ''' </summary>
    Public Class NavigationDropdowns
        Inherits Box

        ' UI Components
        Private pClassTrigger As ToggleButton
        Private pClassTriggerLabel As Label
        Private pMemberTrigger As ToggleButton
        Private pMemberTriggerLabel As Label
        Private pClassLabel As Label
        Private pMemberLabel As Label
        Private pClassPopup As NavigationDropdownPopup
        Private pMemberPopup As NavigationDropdownPopup
        Private pThemeManager As ThemeManager

        ' Data storage
        Private pClasses As New List(Of CodeObject)()
        Private pRootMembers As New List(Of CodeMember)()
        Private pCurrentMembers As New List(Of CodeMember)()
        Private pCurrentClass As String = ""
        Private pCurrentMember As String = ""
        ''' <summary>The CodeObject behind pCurrentClass, or Nothing when it's (General)</summary>
        Private pCurrentClassTag As CodeObject = Nothing
        ''' <summary>The CodeMember behind pCurrentMember, or Nothing when it's (Declarations)</summary>
        Private pCurrentMemberTag As CodeMember = Nothing
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
                ' Class selection label and trigger
                pClassLabel = New Label("Class:")
                pClassLabel.Halign = Align.Start
                PackStart(pClassLabel, False, False, 0)

                pClassTrigger = CreateTriggerButton(200, pClassTriggerLabel)
                AddHandler pClassTrigger.Clicked, AddressOf OnClassTriggerClicked
                PackStart(pClassTrigger, False, False, 0)

                ' Member selection label and trigger
                pMemberLabel = New Label("Member:")
                pMemberLabel.MarginStart = 10
                pMemberLabel.Halign = Align.Start
                PackStart(pMemberLabel, False, False, 0)

                pMemberTrigger = CreateTriggerButton(250, pMemberTriggerLabel)
                AddHandler pMemberTrigger.Clicked, AddressOf OnMemberTriggerClicked
                PackStart(pMemberTrigger, False, False, 0)

                pClassPopup = New NavigationDropdownPopup()
                AddHandler pClassPopup.ItemSelected, AddressOf OnClassPopupItemSelected
                AddHandler pClassPopup.PopupCancelled, AddressOf OnClassPopupCancelled

                pMemberPopup = New NavigationDropdownPopup()
                AddHandler pMemberPopup.ItemSelected, AddressOf OnMemberPopupItemSelected
                AddHandler pMemberPopup.PopupCancelled, AddressOf OnMemberPopupCancelled

                ShowAll()

            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates a combo-box-like trigger button: an elided label plus a dropdown arrow
        ''' </summary>
        ''' <param name="vWidth">Fixed width for the trigger, matching the old ComboBoxText sizing</param>
        ''' <param name="vLabel">Receives the inner Label so callers can update its text later</param>
        Private Function CreateTriggerButton(vWidth As Integer, ByRef vLabel As Label) As ToggleButton
            Dim lButton As New ToggleButton()
            lButton.WidthRequest = vWidth

            Dim lBox As New Box(Orientation.Horizontal, 4)
            vLabel = New Label("")
            vLabel.Halign = Align.Start
            vLabel.Ellipsize = Pango.EllipsizeMode.End
            lBox.PackStart(vLabel, True, True, 2)

            Dim lArrow As New Label("▾") ' ▾
            lBox.PackStart(lArrow, False, False, 2)

            lButton.Add(lBox)
            Return lButton
        End Function

        ''' <summary>
        ''' Sets the theme manager used to color the popup lists
        ''' </summary>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                pClassPopup.SetThemeManager(vThemeManager)
                pMemberPopup.SetThemeManager(vThemeManager)

            Catch ex As Exception
                Console.WriteLine($"NavigationDropdowns.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the initial state of the dropdowns
        ''' </summary>
        Private Sub SetInitialState()
            Try
                pIsUpdating = True

                pClassTriggerLabel.Text = NO_CLASSES_ITEM
                pClassTrigger.Sensitive = False

                pMemberTriggerLabel.Text = NO_MEMBERS_ITEM
                pMemberTrigger.Sensitive = False

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
        ''' Resets the class trigger to (General) and refreshes the member list under it
        ''' </summary>
        Private Sub UpdateClassDropdown()
            Try
                pClassTrigger.Sensitive = True
                pCurrentClass = GENERAL_ITEM
                pCurrentClassTag = Nothing
                pClassTriggerLabel.Text = GENERAL_ITEM

                UpdateMemberDropdown()

            Catch ex As Exception
                Console.WriteLine($"UpdateClassDropdown error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Refreshes pCurrentMembers and the member trigger label for the current class
        ''' </summary>
        Private Sub UpdateMemberDropdown()
            Try
                pCurrentMembers.Clear()

                Dim lMembers As List(Of CodeMember) =
                    If(pCurrentClassTag Is Nothing, pRootMembers, pCurrentClassTag.Members)

                If lMembers IsNot Nothing Then pCurrentMembers.AddRange(lMembers)

                pMemberTrigger.Sensitive = True
                pCurrentMember = DECLARATIONS_ITEM
                pCurrentMemberTag = Nothing
                pMemberTriggerLabel.Text = DECLARATIONS_ITEM

            Catch ex As Exception
                Console.WriteLine($"UpdateMemberDropdown error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the Class popup's item list: (General) followed by every class in
        ''' pClasses (already in file/declaration order), indented per NestingLevel with
        ''' tree-connector info computed from each item's Parent chain
        ''' </summary>
        Private Function BuildClassPopupItems() As List(Of NavigationDropdownPopup.Item)
            Dim lItems As New List(Of NavigationDropdownPopup.Item)

            Dim lGeneralItem As New NavigationDropdownPopup.Item() With {
                .Text = GENERAL_ITEM,
                .IsClassItem = False,
                .MemberType = CodeMemberType.eUnspecified,
                .IndentLevel = 0,
                .IsLastSibling = (pClasses.Count = 0),
                .Tag = Nothing
            }
            lItems.Add(lGeneralItem)

            If pClasses.Count = 0 Then Return lItems

            ' Group by Parent to determine sibling order for both "is this the last child"
            ' and the ancestor "still has more siblings" checks the tree-connector rendering
            ' needs. Dictionary(Of CodeObject, ...) doesn't allow a Nothing key, but
            ' top-level classes have Parent = Nothing, so a non-null sentinel stands in for
            ' "no parent" everywhere a CodeObject would be used as a lookup key below
            Dim lTopLevelSentinel As New CodeObject()
            Dim lKeyFor As Func(Of CodeObject, CodeObject) = Function(vParent) If(vParent, lTopLevelSentinel)

            Dim lChildrenOf As New Dictionary(Of CodeObject, List(Of CodeObject))
            for each lClass in pClasses
                Dim lKey As CodeObject = lKeyFor(lClass.Parent)
                Dim lSiblings As List(Of CodeObject) = Nothing
                If Not lChildrenOf.TryGetValue(lKey, lSiblings) Then
                    lSiblings = New List(Of CodeObject)
                    lChildrenOf(lKey) = lSiblings
                End If
                lSiblings.Add(lClass)
            Next

            for each lClass in pClasses
                Dim lItem As New NavigationDropdownPopup.Item() With {
                    .Text = lClass.DisplayText,
                    .IsClassItem = True,
                    .ObjectType = lClass.ObjectType,
                    .IndentLevel = lClass.NestingLevel,
                    .Tag = lClass
                }

                Dim lOwnSiblings As List(Of CodeObject) = lChildrenOf(lKeyFor(lClass.Parent))
                lItem.IsLastSibling = (lOwnSiblings.IndexOf(lClass) = lOwnSiblings.Count - 1)

                Dim lAncestorChain As New List(Of CodeObject)
                Dim lCurrent As CodeObject = lClass.Parent
                While lCurrent IsNot Nothing
                    lAncestorChain.Insert(0, lCurrent)
                    lCurrent = lCurrent.Parent
                End While

                for each lAncestor in lAncestorChain
                    Dim lAncestorSiblings As List(Of CodeObject) = lChildrenOf(lKeyFor(lAncestor.Parent))
                    lItem.AncestorContinues.Add(lAncestorSiblings.IndexOf(lAncestor) < lAncestorSiblings.Count - 1)
                Next

                lItems.Add(lItem)
            Next

            Return lItems
        End Function

        ''' <summary>
        ''' Builds the Member popup's item list: (Declarations) pinned first, then the
        ''' current class's members sorted alphabetically by name
        ''' </summary>
        Private Function BuildMemberPopupItems() As List(Of NavigationDropdownPopup.Item)
            Dim lItems As New List(Of NavigationDropdownPopup.Item)
            lItems.Add(New NavigationDropdownPopup.Item() With {
                .Text = DECLARATIONS_ITEM,
                .IsClassItem = False,
                .MemberType = CodeMemberType.eUnspecified,
                .Tag = Nothing
            })

            Dim lSorted As New List(Of CodeMember)(pCurrentMembers)
            lSorted.Sort(Function(a, b) String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase))

            for each lMember in lSorted
                lItems.Add(New NavigationDropdownPopup.Item() With {
                    .Text = lMember.DisplayText,
                    .IsClassItem = False,
                    .MemberType = lMember.MemberType,
                    .Tag = lMember
                })
            Next

            Return lItems
        End Function

        ''' <summary>
        ''' Shows the Class popup below its trigger button, closing the Member popup first
        ''' if it happened to be open
        ''' </summary>
        Private Sub OpenClassPopup()
            If pMemberPopup.Visible Then
                pMemberPopup.HidePopup(False)
                pMemberTrigger.Active = False
            End If
            pClassPopup.ShowFor(pClassTrigger, BuildClassPopupItems(), pCurrentClassTag)
            pClassTrigger.Active = True
        End Sub

        ''' <summary>
        ''' Shows the Member popup below its trigger button, closing the Class popup first
        ''' if it happened to be open
        ''' </summary>
        Private Sub OpenMemberPopup()
            If pClassPopup.Visible Then
                pClassPopup.HidePopup(False)
                pClassTrigger.Active = False
            End If
            pMemberPopup.ShowFor(pMemberTrigger, BuildMemberPopupItems(), pCurrentMemberTag)
            pMemberTrigger.Active = True
        End Sub

        Private Sub OnClassTriggerClicked(vSender As Object, vArgs As EventArgs)
            Try
                If pClassTrigger.Active Then
                    OpenClassPopup()
                Else
                    pClassPopup.HidePopup(False)
                End If

            Catch ex As Exception
                Console.WriteLine($"OnClassTriggerClicked error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnMemberTriggerClicked(vSender As Object, vArgs As EventArgs)
            Try
                If pMemberTrigger.Active Then
                    OpenMemberPopup()
                Else
                    pMemberPopup.HidePopup(False)
                End If

            Catch ex As Exception
                Console.WriteLine($"OnMemberTriggerClicked error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnClassPopupCancelled()
            pClassTrigger.Active = False
        End Sub

        Private Sub OnMemberPopupCancelled()
            pMemberTrigger.Active = False
        End Sub

        ''' <summary>
        ''' Handles a commit (Enter or click) in the Class popup: selects the class,
        ''' navigates to its declaration, then - per James's requested keyboard flow -
        ''' shifts focus to the Member trigger and immediately opens its popup so a member
        ''' can be picked right away without an extra click. An Enum has no methods/
        ''' properties/etc. of its own to pick from (its values aren't tracked as
        ''' CodeMembers), so selecting one just navigates instead.
        ''' </summary>
        Private Sub OnClassPopupItemSelected(vTag As Object)
            Try
                pClassTrigger.Active = False

                Dim lClass As CodeObject = TryCast(vTag, CodeObject)
                pCurrentClassTag = lClass
                pCurrentClass = If(lClass IsNot Nothing, lClass.DisplayText, GENERAL_ITEM)
                pClassTriggerLabel.Text = pCurrentClass

                UpdateMemberDropdown()

                If lClass IsNot Nothing Then
                    RaiseEvent NavigationRequested(lClass.StartLine - 1)
                End If

                If lClass Is Nothing OrElse lClass.ObjectType <> CodeObjectType.eEnum Then
                    pMemberTrigger.GrabFocus()
                    OpenMemberPopup()
                End If

            Catch ex As Exception
                Console.WriteLine($"OnClassPopupItemSelected error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles a commit (Enter or click) in the Member popup: selects the member and
        ''' navigates to its declaration line
        ''' </summary>
        Private Sub OnMemberPopupItemSelected(vTag As Object)
            Try
                pMemberTrigger.Active = False

                Dim lMember As CodeMember = TryCast(vTag, CodeMember)
                pCurrentMemberTag = lMember
                pCurrentMember = If(lMember IsNot Nothing, lMember.DisplayText, DECLARATIONS_ITEM)
                pMemberTriggerLabel.Text = pCurrentMember

                If lMember IsNot Nothing Then
                    RaiseEvent NavigationRequested(lMember.StartLine - 1)
                End If

            Catch ex As Exception
                Console.WriteLine($"OnMemberPopupItemSelected error: {ex.Message}")
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

                ' Find containing class - pClasses is a flat list that includes nested types
                ' alongside their outer container (e.g. a Class nested inside another Class),
                ' so a line inside the nested type also falls within the outer type's wider
                ' range; pick the narrowest (most specific) matching range rather than the
                ' first one found, so nested types correctly take priority over their parent
                Dim lContainingClass As CodeObject = Nothing
                for each lClass in pClasses
                    If lLine >= lClass.StartLine AndAlso lLine <= lClass.EndLine Then
                        If lContainingClass Is Nothing OrElse
                           (lClass.EndLine - lClass.StartLine) < (lContainingClass.EndLine - lContainingClass.StartLine) Then
                            lContainingClass = lClass
                        End If
                    End If
                Next

                If lContainingClass IsNot Nothing Then
                    ' Update class selection
                    SelectClass(lContainingClass)

                    ' Find containing member within class
                    Dim lContainingMember As CodeMember = Nothing
                    for each lMember in lContainingClass.Members
                        If lLine >= lMember.StartLine AndAlso lLine <= lMember.EndLine Then
                            lContainingMember = lMember
                            Exit for
                        End If
                    Next

                    ' Cursor isn't inside any recognized member of this class (e.g. it's on
                    ' the class declaration line itself, inside a nested type's body, or on
                    ' a blank line between members) - fall back to (Declarations) rather
                    ' than leaving whatever member UpdateMemberDropdown() defaulted to
                    SelectMember(lContainingMember)

                Else
                    ' Not in any class - select (General)
                    SelectClass(Nothing)

                    ' Check if in a root-level member
                    Dim lContainingRootMember As CodeMember = Nothing
                    for each lMember in pRootMembers
                        If lLine >= lMember.StartLine AndAlso lLine <= lMember.EndLine Then
                            lContainingRootMember = lMember
                            Exit for
                        End If
                    Next

                    SelectMember(lContainingRootMember)
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
                    Dim lClass As CodeObject = If(vPreviousClass = GENERAL_ITEM, Nothing, FindClassByDisplayText(vPreviousClass))
                    SelectClass(lClass)
                End If

                If Not String.IsNullOrEmpty(vPreviousMember) Then
                    Dim lMember As CodeMember = If(vPreviousMember = DECLARATIONS_ITEM, Nothing, FindMemberByDisplayText(vPreviousMember))
                    SelectMember(lMember)
                End If

            Catch ex As Exception
                Console.WriteLine($"RestoreSelection error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects a class (Nothing for (General)), updating the trigger label and
        ''' rebuilding the member list under it
        ''' </summary>
        Private Sub SelectClass(vClass As CodeObject)
            pCurrentClassTag = vClass
            pCurrentClass = If(vClass IsNot Nothing, vClass.DisplayText, GENERAL_ITEM)
            pClassTriggerLabel.Text = pCurrentClass
            UpdateMemberDropdown()
        End Sub

        ''' <summary>
        ''' Selects a member (Nothing for (Declarations)), updating the trigger label
        ''' </summary>
        Private Sub SelectMember(vMember As CodeMember)
            pCurrentMemberTag = vMember
            pCurrentMember = If(vMember IsNot Nothing, vMember.DisplayText, DECLARATIONS_ITEM)
            pMemberTriggerLabel.Text = pCurrentMember
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
                pCurrentClassTag = Nothing
                pCurrentMemberTag = Nothing

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
