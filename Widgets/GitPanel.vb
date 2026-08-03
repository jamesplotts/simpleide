' Widgets/GitPanel.vb - Git integration panel for bottom pane
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities


Namespace Widgets
    Public Class GitPanel
        Inherits Box

        ' Private fields
        Private pNotebook As CustomDrawNotebook
        Private pThemeManager As ThemeManager
        Private pGitManager As GitManager
        Private pProjectRoot As String
        Private pStatusGrid As CustomDrawDataGrid
        Private pHistoryGrid As CustomDrawDataGrid
        Private pDiffView As CustomDrawTextOutput
        Private pCommitMessageEntry As TextView
        Private pCommitButton As CustomDrawButton
        Private pPushButton As CustomDrawButton
        Private pPullButton As CustomDrawButton
        Private pRefreshButton As CustomDrawButton
        Private pBranchLabel As Label
        Private pStageAllButton As CustomDrawButton
        Private pUnstageAllButton As CustomDrawButton
        Private pSelectedFile As String = ""

        ' pCommitMessageEntry is the one editable widget in this panel (a real Gtk.TextView -
        ' there's no multi-line editable CustomDraw widget in the project yet; CustomDrawTextBox
        ' only wraps a single-line Gtk.Entry). It also isn't fought by ThemeManager's
        ' screen-wide CSS cascade the way treeview was (GenerateThemeCss has no hardcoded
        ' textview rule), so its existing per-widget CSS override is kept as-is.
        Private pCommitMessageEntryCssProvider As CssProvider

        ' Events
        Public Event FileSelected(vFilePath As String)
        Public Event RefreshRequested()

        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)

            pGitManager = New GitManager()
            BuildUI()
            ConnectEvents()
        End Sub

        Private Sub BuildUI()
            ' Create toolbar
            Dim lToolbar As Widget = CreateToolbar()
            PackStart(lToolbar, False, False, 0)

            ' Create notebook for tabs
            pNotebook = New CustomDrawNotebook(pThemeManager)
            'pNotebook.TabPos = PositionType.Top

            ' Create Changes tab
            Dim lChangesPage As Widget = CreateChangesPage()
            pNotebook.AppendPage(lChangesPage, "Changes")

            ' Create History tab
            Dim lHistoryPage As Widget = CreateHistoryPage()
            pNotebook.AppendPage(lHistoryPage, "History")

            ' Create Diff tab
            Dim lDiffPage As Widget = CreateDiffPage()
            pNotebook.AppendPage(lDiffPage, "Diff")

            PackStart(pNotebook, True, True, 0)

            ShowAll()
        End Sub

        ''' <summary>
        ''' Switches to the Changes tab - used by callers (e.g. the Git > View Changes menu
        ''' command) that want the panel to land on the file-status/commit view
        ''' </summary>
        Public Sub ShowChangesTab()
            Try
                pNotebook.CurrentPage = 0
            Catch ex As Exception
                Console.WriteLine($"GitPanel.ShowChangesTab error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Switches to the History tab - used by the Git > History menu command
        ''' </summary>
        Public Sub ShowHistoryTab()
            Try
                pNotebook.CurrentPage = 1
            Catch ex As Exception
                Console.WriteLine($"GitPanel.ShowHistoryTab error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates the panel's top action row - CustomDrawButtons (bevel style, matching
        ''' every other button in this panel) instead of a native Gtk.Toolbar, which rendered
        ''' flat and, under ToolbarStyle.Icons, showed no label text at all if the system
        ''' icon theme happened to lack "view-refresh"/"go-down"/"go-up" - leaving the
        ''' buttons entirely blank
        ''' </summary>
        Private Function CreateToolbar() As Widget
            Dim lToolbarBox As New Box(Orientation.Horizontal, 6)
            lToolbarBox.MarginTop = 4
            lToolbarBox.MarginBottom = 4
            lToolbarBox.MarginStart = 6
            lToolbarBox.MarginEnd = 6

            ' Refresh button
            pRefreshButton = New CustomDrawButton("Refresh", LoadGitIconPixbuf("view-refresh"))
            pRefreshButton.TooltipText = "Refresh git Status"
            lToolbarBox.PackStart(pRefreshButton, False, False, 0)

            ' Pull button
            pPullButton = New CustomDrawButton("Pull", LoadGitIconPixbuf("go-down"))
            pPullButton.TooltipText = "Pull from remote"
            lToolbarBox.PackStart(pPullButton, False, False, 0)

            ' Push button
            pPushButton = New CustomDrawButton("Push", LoadGitIconPixbuf("go-up"))
            pPushButton.TooltipText = "Push to remote"
            lToolbarBox.PackStart(pPushButton, False, False, 0)

            ' Branch label
            Dim lBranchBox As New Box(Orientation.Horizontal, 6)
            lBranchBox.PackStart(New Label("Branch:"), False, False, 0)
            pBranchLabel = New Label("master")
            pBranchLabel.Markup = "<b>master</b>"
            lBranchBox.PackStart(pBranchLabel, False, False, 0)
            lToolbarBox.PackEnd(lBranchBox, False, False, 0)

            Return lToolbarBox
        End Function

        ''' <summary>
        ''' Loads a 16px icon-theme icon for a panel button - CustomDrawButton's own
        ''' IconContrastHelper auto-inverts it for dark/light contrast, so no separate
        ''' dark-variant asset is needed the way BuildOutputPanel's hand-authored PNGs are
        ''' </summary>
        ''' <param name="vIconName">Icon-theme name to look up</param>
        Private Function LoadGitIconPixbuf(vIconName As String) As Gdk.Pixbuf
            Try
                Return Gtk.IconTheme.Default.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"GitPanel.LoadGitIconPixbuf error ({vIconName}): {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Applies the app's color theme to this panel's CustomDraw controls (status/history
        ''' grids, diff view) - each self-themes via its own SetThemeManager - and to the
        ''' commit-message TextView's background/foreground colors via CSS, since that's the
        ''' one remaining native GTK widget in this panel
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                pThemeManager = vThemeManager
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If

                pNotebook.SetThemeManager(vThemeManager)
                If pRefreshButton IsNot Nothing Then pRefreshButton.ThemeManager = vThemeManager
                If pPullButton IsNot Nothing Then pPullButton.ThemeManager = vThemeManager
                If pPushButton IsNot Nothing Then pPushButton.ThemeManager = vThemeManager
                If pStageAllButton IsNot Nothing Then pStageAllButton.ThemeManager = vThemeManager
                If pUnstageAllButton IsNot Nothing Then pUnstageAllButton.ThemeManager = vThemeManager
                If pCommitButton IsNot Nothing Then pCommitButton.ThemeManager = vThemeManager
                If pStatusGrid IsNot Nothing Then pStatusGrid.SetThemeManager(vThemeManager)
                If pHistoryGrid IsNot Nothing Then pHistoryGrid.SetThemeManager(vThemeManager)
                If pDiffView IsNot Nothing Then pDiffView.SetThemeManager(vThemeManager)

                ApplyCurrentTheme()

            Catch ex As Exception
                Console.WriteLine($"GitPanel.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles live theme changes - the CustomDraw controls redraw themselves via their
        ''' own ThemeChanged subscriptions, this only needs to refresh the commit-message
        ''' TextView's CSS
        ''' </summary>
        Private Sub OnThemeChanged(vTheme As EditorTheme)
            ApplyCurrentTheme()
        End Sub

        ''' <summary>
        ''' Applies the current theme's colors to the commit-message TextView via a CSS
        ''' override, since it's not a CustomDraw control with its own theme handling
        ''' </summary>
        Private Sub ApplyCurrentTheme()
            Try
                If pThemeManager Is Nothing Then Return

                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lTheme Is Nothing Then Return

                Dim lBackground As String = lTheme.GetColor(EditorTheme.Tags.eEditorBackgroundColor)
                Dim lForeground As String = lTheme.GetColor(EditorTheme.Tags.eForegroundColor)

                Dim lTextViewCss As String = String.Format(
                    "textview {{ background-color: {0}; color: {1}; }}", lBackground, lForeground)

                ApplyWidgetCss(pCommitMessageEntry, lTextViewCss, pCommitMessageEntryCssProvider)

            Catch ex As Exception
                Console.WriteLine($"GitPanel.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces vWidget's tracked CSS provider with a new one built from vCss, removing
        ''' the old provider first so repeated theme changes don't stack providers
        ''' </summary>
        Private Sub ApplyWidgetCss(vWidget As Widget, vCss As String, ByRef vProvider As CssProvider)
            If vWidget Is Nothing Then Return
            If vProvider IsNot Nothing Then
                vWidget.StyleContext.RemoveProvider(vProvider)
            End If
            vProvider = New CssProvider()
            vProvider.LoadFromData(vCss)
            vWidget.StyleContext.AddProvider(vProvider, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        End Sub

        ''' <summary>
        ''' Creates the Changes tab's file-status grid - Staged (checkbox) / Status (code) /
        ''' File columns. Rows carry the originating GitManager.GitFileInfo directly (Tag),
        ''' so selection/double-click never needs to re-look-up a file by display text.
        ''' </summary>
        Private Function CreateStatusGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lStagedColumn As New DataGridColumn() With {
                    .Name = "Staged",
                    .Title = "",
                    .Width = 30,
                    .MinWidth = 24,
                    .MaxWidth = 40,
                    .Resizable = False,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eBoolean,
                    .Alignment = ColumnAlignment.eCenter
                }
                lGrid.Columns.Add(lStagedColumn)

                Dim lStatusColumn As New DataGridColumn() With {
                    .Name = "Status",
                    .Title = "Status",
                    .Width = 50,
                    .MinWidth = 40,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Alignment = ColumnAlignment.eCenter
                }
                lGrid.Columns.Add(lStatusColumn)

                Dim lFileColumn As New DataGridColumn() With {
                    .Name = "File",
                    .Title = "File",
                    .Width = 200,
                    .MinWidth = 80,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lFileColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False

            Catch ex As Exception
                Console.WriteLine($"CreateStatusGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ''' <summary>
        ''' Creates the History tab's commit-log grid - Commit / Author / Date / Message
        ''' columns. Rows carry the originating GitManager.CommitInfo directly (Tag)
        ''' </summary>
        Private Function CreateHistoryGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lHashColumn As New DataGridColumn() With {
                    .Name = "Commit",
                    .Title = "Commit",
                    .Width = 70,
                    .MinWidth = 50,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lHashColumn)

                Dim lAuthorColumn As New DataGridColumn() With {
                    .Name = "Author",
                    .Title = "Author",
                    .Width = 120,
                    .MinWidth = 60,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True
                }
                lGrid.Columns.Add(lAuthorColumn)

                Dim lDateColumn As New DataGridColumn() With {
                    .Name = "Date",
                    .Title = "Date",
                    .Width = 90,
                    .MinWidth = 70,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lDateColumn)

                Dim lMessageColumn As New DataGridColumn() With {
                    .Name = "Message",
                    .Title = "Message",
                    .Width = 300,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lMessageColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False

            Catch ex As Exception
                Console.WriteLine($"CreateHistoryGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        Private Function CreateChangesPage() As Widget
            Dim lVPaned As New Paned(Orientation.Vertical)

            ' Top: File list
            Dim lTopBox As New Box(Orientation.Vertical, 0)

            ' Stage/Unstage buttons
            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.MarginTop = 6
            lButtonBox.MarginBottom = 6
            lButtonBox.MarginStart = 6
            lButtonBox.MarginEnd = 6

            pStageAllButton = New CustomDrawButton("Stage All", LoadGitIconPixbuf("list-add"))
            lButtonBox.PackStart(pStageAllButton, False, False, 0)

            pUnstageAllButton = New CustomDrawButton("Unstage All", LoadGitIconPixbuf("list-remove"))
            lButtonBox.PackStart(pUnstageAllButton, False, False, 0)

            lTopBox.PackStart(lButtonBox, False, False, 0)

            ' File status grid - self-contained (own scrollbar), no ScrolledWindow needed
            pStatusGrid = CreateStatusGrid()
            pStatusGrid.HeightRequest = 100
            lTopBox.PackStart(pStatusGrid, True, True, 0)

            lVPaned.Pack1(lTopBox, True, False)

            ' Bottom: Commit area
            Dim lCommitBox As New Box(Orientation.Vertical, 6)
            lCommitBox.MarginTop = 6
            lCommitBox.MarginBottom = 6
            lCommitBox.MarginStart = 6
            lCommitBox.MarginEnd = 6

            Dim lCommitLabel As New Label("Commit Message:")
            lCommitLabel.Halign = Align.Start
            lCommitBox.PackStart(lCommitLabel, False, False, 0)

            ' Commit message text view
            Dim lCommitScroll As New ScrolledWindow()
            lCommitScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lCommitScroll.HeightRequest = 80
            lCommitScroll.ShadowType = ShadowType.in

            pCommitMessageEntry = New TextView()
            pCommitMessageEntry.WrapMode = WrapMode.Word
            lCommitScroll.Add(pCommitMessageEntry)
            lCommitBox.PackStart(lCommitScroll, True, True, 0)

            ' Commit button
            Dim lCommitButtonBox As New Box(Orientation.Horizontal, 6)
            pCommitButton = New CustomDrawButton("Commit", LoadGitIconPixbuf("document-save"))
            pCommitButton.Sensitive = False
            lCommitButtonBox.PackEnd(pCommitButton, False, False, 0)
            lCommitBox.PackStart(lCommitButtonBox, False, False, 0)

            lVPaned.Pack2(lCommitBox, False, False)

            Return lVPaned
        End Function

        Private Function CreateHistoryPage() As Widget
            pHistoryGrid = CreateHistoryGrid()
            Return pHistoryGrid
        End Function

        Private Function CreateDiffPage() As Widget
            pDiffView = New CustomDrawTextOutput()
            Return pDiffView
        End Function

        Private Sub ConnectEvents()
            ' Toolbar buttons
            AddHandler pRefreshButton.Clicked, AddressOf OnRefresh
            AddHandler pPullButton.Clicked, AddressOf OnPull
            AddHandler pPushButton.Clicked, AddressOf OnPush

            ' Stage/Unstage buttons
            AddHandler pStageAllButton.Clicked, AddressOf OnStageAll
            AddHandler pUnstageAllButton.Clicked, AddressOf OnUnstageAll

            ' Commit
            AddHandler pCommitButton.Clicked, AddressOf OnCommit
            AddHandler pCommitMessageEntry.Buffer.Changed, AddressOf OnCommitMessageChanged

            ' File selection (single-click selects + shows diff; double-click toggles staged)
            AddHandler pStatusGrid.SelectionChanged, AddressOf OnFileSelectionChanged
            AddHandler pStatusGrid.RowDoubleClicked, AddressOf OnFileDoubleClicked

            ' History selection
            AddHandler pHistoryGrid.SelectionChanged, AddressOf OnCommitSelectionChanged
        End Sub

        ''' <summary>
        ''' Toggles a file's staged state via GitManager, then refreshes status once the
        ''' command completes
        ''' </summary>
        Private Sub ToggleFileStaged(vFile As GitManager.GitFileInfo)
            Try
                If vFile Is Nothing Then Return

                Task.Run(Async Function()
                    Try
                        If vFile.IsStaged Then
                            Await pGitManager.UnstageFile(vFile.Path)
                        Else
                            Await pGitManager.StageFile(vFile.Path)
                        End If
                        Gtk.Application.Invoke(Sub() RefreshGitStatus())

                    Catch ex As Exception
                        Console.WriteLine($"ToggleFileStaged background error: {ex.Message}")
                    End Try
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"ToggleFileStaged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects a file in the status grid and shows its diff against HEAD - the row's
        ''' Tag is the exact GitManager.GitFileInfo it was built from
        ''' </summary>
        Private Sub OnFileSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                Dim lFile As GitManager.GitFileInfo = TryCast(vRow?.Tag, GitManager.GitFileInfo)
                If lFile Is Nothing Then Return

                pSelectedFile = lFile.Path
                ShowDiff(lFile.Path)

            Catch ex As Exception
                Console.WriteLine($"error on file selection: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Double-clicking a file row toggles its staged state (mirrors common git GUI
        ''' conventions) and still raises FileSelected for any future consumer
        ''' </summary>
        Private Sub OnFileDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                Dim lFile As GitManager.GitFileInfo = TryCast(vRow?.Tag, GitManager.GitFileInfo)
                If lFile Is Nothing Then Return

                RaiseEvent FileSelected(lFile.Path)
                ToggleFileStaged(lFile)

            Catch ex As Exception
                Console.WriteLine($"error on file double-click: {ex.Message}")
            End Try
        End Sub

        Private Sub OnCommitMessageChanged(vSender As Object, vE As EventArgs)
            pCommitButton.Sensitive = Not String.IsNullOrWhiteSpace(pCommitMessageEntry.Buffer.Text)
        End Sub

        Private Sub OnRefresh(vSender As Object, vE As EventArgs)
            RefreshGitStatus()
        End Sub

        Private Sub OnPull(vSender As Object, vE As EventArgs)
            Try
                Task.Run(Async Function()
                    Dim lSuccess As Boolean = Await pGitManager.Pull()
                    Gtk.Application.Invoke(Sub()
                        If lSuccess Then
                            ShowMessage("Pull completed successfully")
                            RefreshGitStatus()
                        Else
                            ShowError("Pull failed - see console for details")
                        End If
                    End Sub)
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"OnPull error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnPush(vSender As Object, vE As EventArgs)
            Try
                Task.Run(Async Function()
                    Dim lSuccess As Boolean = Await pGitManager.Push()
                    Gtk.Application.Invoke(Sub()
                        If lSuccess Then
                            ShowMessage("Push completed successfully")
                        Else
                            ShowError("Push failed - see console for details")
                        End If
                    End Sub)
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"OnPush error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnStageAll(vSender As Object, vE As EventArgs)
            Try
                Task.Run(Async Function()
                    Await pGitManager.StageAll()
                    Gtk.Application.Invoke(Sub() RefreshGitStatus())
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"OnStageAll error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnUnstageAll(vSender As Object, vE As EventArgs)
            Try
                Task.Run(Async Function()
                    Await pGitManager.UnstageAll()
                    Gtk.Application.Invoke(Sub() RefreshGitStatus())
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"OnUnstageAll error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnCommit(vSender As Object, vE As EventArgs)
            Try
                Dim lMessage As String = pCommitMessageEntry.Buffer.Text.Trim()
                If String.IsNullOrEmpty(lMessage) Then Return

                Task.Run(Async Function()
                    Dim lSuccess As Boolean = Await pGitManager.Commit(lMessage)
                    Gtk.Application.Invoke(Sub()
                        If lSuccess Then
                            ShowMessage("Commit successful")
                            pCommitMessageEntry.Buffer.Text = ""
                            RefreshGitStatus()
                        Else
                            ShowError("Commit failed - see console for details")
                        End If
                    End Sub)
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"OnCommit error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects a commit in the history grid and shows its diff - the row's Tag is the
        ''' exact GitManager.CommitInfo it was built from
        ''' </summary>
        Private Sub OnCommitSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                Dim lCommit As GitManager.CommitInfo = TryCast(vRow?.Tag, GitManager.CommitInfo)
                If lCommit Is Nothing Then Return

                ShowCommitDiff(lCommit.Hash)

            Catch ex As Exception
                Console.WriteLine($"error on Commit selection: {ex.Message}")
            End Try
        End Sub

        ' ===== Git operations (consolidated through GitManager) =====

        Public Sub RefreshGitStatus()
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then Return

                pStatusGrid.ClearRows()

                Task.Run(Async Function()
                    Try
                        Dim lBranch As String = Await pGitManager.GetCurrentBranch()
                        Dim lFiles As List(Of GitManager.GitFileInfo) = Await pGitManager.GetFileStatus()

                        Gtk.Application.Invoke(Sub()
                            If Not String.IsNullOrEmpty(lBranch) Then
                                pBranchLabel.Markup = $"<b>{lBranch}</b>"
                            End If
                            PopulateStatusGrid(lFiles)
                        End Sub)

                    Catch ex As Exception
                        Console.WriteLine($"RefreshGitStatus background error: {ex.Message}")
                    End Try
                    Return Nothing
                End Function)

                RefreshHistory()

            Catch ex As Exception
                Console.WriteLine($"RefreshGitStatus error: {ex.Message}")
            End Try
        End Sub

        Private Sub RefreshHistory()
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then Return

                pHistoryGrid.ClearRows()

                Task.Run(Async Function()
                    Try
                        Dim lCommits As List(Of GitManager.CommitInfo) = Await pGitManager.GetCommitHistory(20)
                        Gtk.Application.Invoke(Sub() PopulateHistoryGrid(lCommits))

                    Catch ex As Exception
                        Console.WriteLine($"RefreshHistory background error: {ex.Message}")
                    End Try
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"RefreshHistory error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Populates the status grid, one row per GitManager.GitFileInfo, with the file
        ''' itself stashed in each row's Tag
        ''' </summary>
        Private Sub PopulateStatusGrid(vFiles As List(Of GitManager.GitFileInfo))
            Try
                pStatusGrid.ClearRows()
                If vFiles Is Nothing Then Return

                for each lFile in vFiles
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lFile
                    lRow.Cells.Add(New DataGridCell(lFile.IsStaged))
                    lRow.Cells.Add(New DataGridCell(GetStatusIcon(lFile.Status)))
                    lRow.Cells.Add(New DataGridCell(GetDisplayPath(lFile)))
                    pStatusGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"PopulateStatusGrid error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Populates the history grid, one row per GitManager.CommitInfo, with the commit
        ''' itself stashed in each row's Tag
        ''' </summary>
        Private Sub PopulateHistoryGrid(vCommits As List(Of GitManager.CommitInfo))
            Try
                pHistoryGrid.ClearRows()
                If vCommits Is Nothing Then Return

                for each lCommit in vCommits
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lCommit
                    Dim lShortHash As String = If(Not String.IsNullOrEmpty(lCommit.Hash) AndAlso lCommit.Hash.Length > 7,
                                                   lCommit.Hash.Substring(0, 7), lCommit.Hash)
                    lRow.Cells.Add(New DataGridCell(lShortHash))
                    lRow.Cells.Add(New DataGridCell(lCommit.Author))
                    lRow.Cells.Add(New DataGridCell(lCommit.CommitDate.ToString("yyyy-MM-dd")))
                    lRow.Cells.Add(New DataGridCell(lCommit.Message))
                    pHistoryGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"PopulateHistoryGrid error: {ex.Message}")
            End Try
        End Sub

        Private Function GetStatusIcon(vStatus As GitManager.FileStatus) As String
            Select Case vStatus
                Case GitManager.FileStatus.eUntracked : Return "?"
                Case GitManager.FileStatus.eModified : Return "M"
                Case GitManager.FileStatus.eAdded : Return "A"
                Case GitManager.FileStatus.eDeleted : Return "D"
                Case GitManager.FileStatus.eRenamed : Return "R"
                Case GitManager.FileStatus.eCopied : Return "C"
                Case GitManager.FileStatus.eConflicted : Return "!"
                Case GitManager.FileStatus.eIgnored : Return "I"
                Case Else : Return " "
            End Select
        End Function

        Private Function GetDisplayPath(vFile As GitManager.GitFileInfo) As String
            If vFile.Status = GitManager.FileStatus.eRenamed AndAlso Not String.IsNullOrEmpty(vFile.OldPath) Then
                Return $"{vFile.OldPath} → {vFile.Path}"
            Else
                Return vFile.Path
            End If
        End Function

        Private Sub ShowDiff(vFilePath As String)
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return

                Task.Run(Async Function()
                    Try
                        Dim lDiffText As String = Await pGitManager.GetFileDiffFromHead(vFilePath)
                        Gtk.Application.Invoke(Sub()
                            DisplayDiff(lDiffText)
                            pNotebook.CurrentPage = 2 ' Switch to diff tab
                        End Sub)

                    Catch ex As Exception
                        Console.WriteLine($"ShowDiff background error: {ex.Message}")
                    End Try
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"ShowDiff error: {ex.Message}")
            End Try
        End Sub

        Private Sub ShowCommitDiff(vCommitHash As String)
            Try
                Task.Run(Async Function()
                    Try
                        Dim lDiffText As String = Await pGitManager.GetCommitDiff(vCommitHash)
                        Gtk.Application.Invoke(Sub()
                            DisplayDiff(lDiffText)
                            pNotebook.CurrentPage = 2 ' Switch to diff tab
                        End Sub)

                    Catch ex As Exception
                        Console.WriteLine($"ShowCommitDiff background error: {ex.Message}")
                    End Try
                    Return Nothing
                End Function)

            Catch ex As Exception
                Console.WriteLine($"ShowCommitDiff error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Displays diff text in the diff view, color-coding added/removed/header/hunk
        ''' lines via CustomDrawTextOutput's diff line styles
        ''' </summary>
        ''' <param name="vDiffText">The diff text to display</param>
        Private Sub DisplayDiff(vDiffText As String)
            Try
                pDiffView.Clear()

                If String.IsNullOrEmpty(vDiffText) Then
                    pDiffView.AppendOutput("No Changes to display")
                    Return
                End If

                Dim lLines() As String = vDiffText.Split({Environment.NewLine, vbLf}, StringSplitOptions.None)

                for each lLine in lLines
                    Dim lStyle As CustomDrawTextOutput.eOutputLineStyle = CustomDrawTextOutput.eOutputLineStyle.eNormal

                    If lLine.StartsWith("+") AndAlso Not lLine.StartsWith("+++") Then
                        lStyle = CustomDrawTextOutput.eOutputLineStyle.eDiffAdded
                    ElseIf lLine.StartsWith("-") AndAlso Not lLine.StartsWith("---") Then
                        lStyle = CustomDrawTextOutput.eOutputLineStyle.eDiffRemoved
                    ElseIf lLine.StartsWith("@@") Then
                        lStyle = CustomDrawTextOutput.eOutputLineStyle.eDiffLineNumber
                    ElseIf lLine.StartsWith("diff ") OrElse lLine.StartsWith("index ") OrElse _
                           lLine.StartsWith("commit ") OrElse lLine.StartsWith("Author:") OrElse _
                           lLine.StartsWith("Date:") Then
                        lStyle = CustomDrawTextOutput.eOutputLineStyle.eDiffHeader
                    End If

                    pDiffView.AppendOutput(lLine, lStyle)
                Next

            Catch ex As Exception
                Console.WriteLine($"DisplayDiff error: {ex.Message}")
            End Try
        End Sub

        Private Sub ShowMessage(vMessage As String)
            Console.WriteLine($"git: {vMessage}")
            ' TODO: Connect to status bar
        End Sub

        Private Sub ShowError(vMessage As String)
            Console.WriteLine($"git error: {vMessage}")
            ' TODO: Show error dialog
        End Sub

        ' Public properties and methods
        Public Property ProjectRoot As String
            Get
                Return pProjectRoot
            End Get
            Set(Value As String)
                pProjectRoot = Value
                pGitManager.RepositoryPath = Value
                If Not String.IsNullOrEmpty(Value) AndAlso Directory.Exists(System.IO.Path.Combine(Value, ".git")) Then
                    RefreshGitStatus()
                Else
                    pStatusGrid.ClearRows()
                    pHistoryGrid.ClearRows()
                    pDiffView.Clear()
                    pDiffView.AppendOutput("Not a git repository")
                End If
            End Set
        End Property

        Public Sub Refresh()
            RefreshGitStatus()
        End Sub
    End Class
End Namespace
