' Dialogs/GitBranchDialog.vb - Git branch management dialog: list, switch, create, delete
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets

Namespace Dialogs

    ''' <summary>
    ''' Lists local and remote branches and lets the user switch to, create, or delete a
    ''' local branch. Switching is deferred - the dialog just records the choice in
    ''' SelectedBranch and closes with ResponseType.Ok, so the caller can run its own
    ''' uncommitted-changes safety check (MainWindow.GitCheckout) before actually checking
    ''' out. Create/Delete are immediate, since they don't move the working tree.
    ''' </summary>
    Public Class GitBranchDialog
        Inherits Dialog

        ' ===== Private Fields =====
        Private pGitManager As GitManager
        Private pThemeManager As ThemeManager
        Private pBranchGrid As CustomDrawDataGrid
        Private pSwitchButton As CustomDrawButton
        Private pNewBranchButton As CustomDrawButton
        Private pDeleteButton As CustomDrawButton
        Private pStatusLabel As Label
        Private pSelectedBranch As String

        ' ===== Public Properties =====

        ''' <summary>
        ''' The branch name the user chose to switch to - only meaningful when Run()
        ''' returns ResponseType.Ok
        ''' </summary>
        Public ReadOnly Property SelectedBranch As String
            Get
                Return pSelectedBranch
            End Get
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Creates a new branch management dialog
        ''' </summary>
        ''' <param name="vParent">Owning window, for centering and modal ownership</param>
        ''' <param name="vGitManager">GitManager for the current repository</param>
        ''' <param name="vThemeManager">Optional ThemeManager for CustomDraw widget theming</param>
        Public Sub New(vParent As Window, vGitManager As GitManager, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New("Branches", vParent, DialogFlags.Modal)
            pGitManager = vGitManager
            pThemeManager = vThemeManager

            Try
                SetupDialog()
                BuildUI()
                ConnectEvents()
                ShowAll()
                RefreshBranches()

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog constructor error: {ex.Message}")
            End Try
        End Sub

        Private Sub SetupDialog()
            Try
                SetDefaultSize(500, 400)
                SetPosition(WindowPosition.CenterOnParent)
                BorderWidth = 10

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.SetupDialog error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI()
            Try
                Dim lMainBox As New Box(Orientation.Vertical, 10)

                pBranchGrid = CreateBranchGrid()
                lMainBox.PackStart(pBranchGrid, True, True, 0)

                pStatusLabel = New Label("")
                pStatusLabel.Halign = Align.Start
                lMainBox.PackStart(pStatusLabel, False, False, 0)

                ContentArea.PackStart(lMainBox, True, True, 0)

                ' Dialog buttons - custom-drawn, matching GitCommitDialog/InputDialog
                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                lButtonBox.BorderWidth = 6

                pNewBranchButton = New CustomDrawButton("New Branch...")
                pNewBranchButton.ThemeManager = pThemeManager
                AddHandler pNewBranchButton.Clicked, AddressOf OnNewBranch
                lButtonBox.PackStart(pNewBranchButton, False, False, 0)

                pDeleteButton = New CustomDrawButton("Delete")
                pDeleteButton.ThemeManager = pThemeManager
                pDeleteButton.Sensitive = False
                AddHandler pDeleteButton.Clicked, AddressOf OnDeleteBranch
                lButtonBox.PackStart(pDeleteButton, False, False, 0)

                pSwitchButton = New CustomDrawButton("Switch To")
                pSwitchButton.ThemeManager = pThemeManager
                pSwitchButton.Sensitive = False
                AddHandler pSwitchButton.Clicked, AddressOf OnSwitchBranch
                lButtonBox.PackStart(pSwitchButton, False, False, 0)

                Dim lCloseButton As New CustomDrawButton("Close")
                lCloseButton.ThemeManager = pThemeManager
                AddHandler lCloseButton.Clicked, Sub() Respond(ResponseType.Cancel)
                lButtonBox.PackStart(lCloseButton, False, False, 0)

                Dim lContentBox As Box = TryCast(ContentArea, Box)
                If lContentBox IsNot Nothing Then lContentBox.PackStart(lButtonBox, False, False, 0)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.BuildUI error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates the Name/Type branch list grid - a CustomDrawDataGrid rather than a
        ''' Gtk.TreeView, matching the rest of this session's Git panel work
        ''' </summary>
        Private Function CreateBranchGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lNameColumn As New DataGridColumn() With {
                    .Name = "Name",
                    .Title = "Branch",
                    .Width = 260,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lNameColumn)

                Dim lTypeColumn As New DataGridColumn() With {
                    .Name = "Type",
                    .Title = "Type",
                    .Width = 90,
                    .MinWidth = 60,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lTypeColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False
                lGrid.HeightRequest = 260

                If pThemeManager IsNot Nothing Then lGrid.SetThemeManager(pThemeManager)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.CreateBranchGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        Private Sub ConnectEvents()
            Try
                AddHandler pBranchGrid.SelectionChanged, AddressOf OnBranchSelectionChanged
                AddHandler pBranchGrid.RowDoubleClicked, AddressOf OnBranchRowDoubleClicked

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.ConnectEvents error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Reloads the branch list from GitManager on a background thread, then repopulates
        ''' the grid back on the GTK main thread
        ''' </summary>
        Private Sub RefreshBranches()
            Try
                pStatusLabel.Text = "Loading branches..."

                Task.Run(Async Function()
                    Try
                        Dim lBranches As List(Of GitManager.BranchInfo) = Await pGitManager.GetBranches()
                        Gtk.Application.Invoke(Sub()
                            PopulateBranchGrid(lBranches)
                            pStatusLabel.Text = $"{lBranches.Count} branch(es)"
                        End Sub)

                    Catch ex As Exception
                        Console.WriteLine($"GitBranchDialog.RefreshBranches background error: {ex.Message}")
                        Gtk.Application.Invoke(Sub() pStatusLabel.Text = "Failed to load branches")
                    End Try
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.RefreshBranches error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Populates the branch grid - each row's Tag is the exact BranchInfo it was built
        ''' from, so Switch/Delete never need to re-parse the display text
        ''' </summary>
        Private Sub PopulateBranchGrid(vBranches As List(Of GitManager.BranchInfo))
            Try
                pBranchGrid.ClearRows()
                If vBranches Is Nothing Then Return

                for each lBranch in vBranches
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lBranch
                    Dim lDisplayName As String = If(lBranch.IsCurrent, $"* {lBranch.Name}", lBranch.Name)
                    lRow.Cells.Add(New DataGridCell(lDisplayName))
                    lRow.Cells.Add(New DataGridCell(If(lBranch.IsRemote, "Remote", "Local")))
                    pBranchGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.PopulateBranchGrid error: {ex.Message}")
            End Try
        End Sub

        Private Function GetSelectedBranch() As GitManager.BranchInfo
            Try
                Dim lRows As List(Of DataGridRow) = pBranchGrid.GetSelectedRows()
                If lRows.Count = 0 Then Return Nothing
                Return TryCast(lRows(0).Tag, GitManager.BranchInfo)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.GetSelectedBranch error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Updates Switch/Delete sensitivity - both only make sense for a local branch
        ''' that isn't the one currently checked out
        ''' </summary>
        Private Sub OnBranchSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                Dim lBranch As GitManager.BranchInfo = TryCast(vRow?.Tag, GitManager.BranchInfo)
                Dim lCanActOn As Boolean = lBranch IsNot Nothing AndAlso lBranch.IsLocal AndAlso Not lBranch.IsCurrent
                pSwitchButton.Sensitive = lCanActOn
                pDeleteButton.Sensitive = lCanActOn

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.OnBranchSelectionChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Double-clicking a switchable branch is a shortcut for Switch To
        ''' </summary>
        Private Sub OnBranchRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                Dim lBranch As GitManager.BranchInfo = TryCast(vRow?.Tag, GitManager.BranchInfo)
                If lBranch Is Nothing OrElse Not lBranch.IsLocal OrElse lBranch.IsCurrent Then Return

                pSelectedBranch = lBranch.Name
                Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.OnBranchRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnSwitchBranch(vSender As Object, vArgs As EventArgs)
            Try
                Dim lBranch As GitManager.BranchInfo = GetSelectedBranch()
                If lBranch Is Nothing Then Return

                pSelectedBranch = lBranch.Name
                Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.OnSwitchBranch error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Prompts for a new branch name and creates it (without checking out - the user
        ''' stays on their current branch, matching how most git GUIs default this) then
        ''' refreshes the list
        ''' </summary>
        Private Sub OnNewBranch(vSender As Object, vArgs As EventArgs)
            Try
                Using lInput As New InputDialog(Me, "New Branch", "Branch Name:", "", pThemeManager)
                    If lInput.Run() = CInt(ResponseType.Ok) Then
                        Dim lName As String = lInput.Text.Trim()
                        If Not String.IsNullOrEmpty(lName) Then
                            pStatusLabel.Text = $"Creating branch '{lName}'..."

                            Task.Run(Async Function()
                                Dim lSuccess As Boolean = Await pGitManager.CreateBranch(lName, vCheckout:=False)
                                Gtk.Application.Invoke(Sub()
                                    If lSuccess Then
                                        RefreshBranches()
                                    Else
                                        pStatusLabel.Text = $"Failed to create branch '{lName}'"
                                        ShowError($"Could not create branch '{lName}'. See console for details.")
                                    End If
                                End Sub)
                                Return Nothing
                            End Function)
                        End If
                    End If
                    lInput.Destroy()
                End Using

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.OnNewBranch error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Deletes the selected local branch after confirmation - never offered for the
        ''' current branch or a remote branch (see OnBranchSelectionChanged)
        ''' </summary>
        Private Sub OnDeleteBranch(vSender As Object, vArgs As EventArgs)
            Try
                Dim lBranch As GitManager.BranchInfo = GetSelectedBranch()
                If lBranch Is Nothing OrElse Not lBranch.IsLocal OrElse lBranch.IsCurrent Then Return

                If Not ShowConfirmation($"Delete local branch '{lBranch.Name}'? This cannot be undone.") Then Return

                pStatusLabel.Text = $"Deleting branch '{lBranch.Name}'..."

                Task.Run(Async Function()
                    Dim lSuccess As Boolean = Await pGitManager.DeleteBranch(lBranch.Name)
                    Gtk.Application.Invoke(Sub()
                        If lSuccess Then
                            RefreshBranches()
                        Else
                            pStatusLabel.Text = $"Failed to delete branch '{lBranch.Name}'"
                            ShowError($"Could not delete branch '{lBranch.Name}'. It may not be fully merged. See console for details.")
                        End If
                    End Sub)
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"GitBranchDialog.OnDeleteBranch error: {ex.Message}")
            End Try
        End Sub

        ' ===== Message Helpers =====

        Private Function ShowConfirmation(vMessage As String) As Boolean
            Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, vMessage)
            Try
                Return lDialog.Run() = CInt(ResponseType.Yes)
            Finally
                lDialog.Destroy()
            End Try
        End Function

        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, vMessage)
            Try
                lDialog.Run()
            Finally
                lDialog.Destroy()
            End Try
        End Sub

    End Class

End Namespace
