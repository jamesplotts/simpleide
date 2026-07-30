' Widgets/GitPanel.vb - Git integration panel for bottom pane
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports SimpleIDE.Managers


Namespace Widgets
    Public Class GitPanel
        Inherits Box
        
        ' Private fields
        Private pNotebook As CustomDrawNotebook
        Private pThemeManager As ThemeManager
        Private pProjectRoot As String
        Private pStatusTreeView As TreeView
        Private pStatusStore As ListStore
        Private pHistoryTreeView As TreeView
        Private pHistoryStore As ListStore
        Private pDiffTextView As TextView
        Private pDiffBuffer As TextBuffer
        Private pCommitMessageEntry As TextView
        Private pCommitButton As Button
        Private pPushButton As ToolButton
        Private pPullButton As ToolButton
        Private pRefreshButton As ToolButton
        Private pBranchLabel As Label
        Private pStageAllButton As Button
        Private pUnstageAllButton As Button
        Private pSelectedFile As String = ""
        
        ' Git file status
        Private Enum GitFileStatus
            eUntracked
            eModified
            eAdded
            eDeleted
            eRenamed
            eStaged
            eConflicted
        End Enum
        
        ' Git file info class
        Private Class GitFileInfo
            Public Property FilePath As String
            Public Property Status As GitFileStatus
            Public Property IsStaged As Boolean
            Public Property OldPath As String ' for renames
            
            Public ReadOnly Property DisplayPath As String
                Get
                    If Status = GitFileStatus.eRenamed AndAlso Not String.IsNullOrEmpty(OldPath) Then
                        Return $"{OldPath} → {FilePath}"
                    Else
                        Return FilePath
                    End If
                End Get
            End Property
            
            Public ReadOnly Property StatusText As String
                Get
                    Select Case Status
                        Case GitFileStatus.eUntracked : Return "Untracked"
                        Case GitFileStatus.eModified : Return "Modified"
                        Case GitFileStatus.eAdded : Return "Added"
                        Case GitFileStatus.eDeleted : Return "Deleted"
                        Case GitFileStatus.eRenamed : Return "Renamed"
                        Case GitFileStatus.eStaged : Return "Staged"
                        Case GitFileStatus.eConflicted : Return "Conflicted"
                        Case Else : Return "Unknown"
                    End Select
                End Get
            End Property
            
            Public ReadOnly Property StatusIcon As String
                Get
                    Select Case Status
                        Case GitFileStatus.eUntracked : Return "?"
                        Case GitFileStatus.eModified : Return "M"
                        Case GitFileStatus.eAdded : Return "A"
                        Case GitFileStatus.eDeleted : Return "D"
                        Case GitFileStatus.eRenamed : Return "r"
                        Case GitFileStatus.eStaged : Return "S"
                        Case GitFileStatus.eConflicted : Return "C"
                        Case Else : Return " "
                    End Select
                End Get
            End Property
        End Class
        
        ' Events
        Public Event FileSelected(vFilePath As String)
        Public Event RefreshRequested()
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            
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
        
        Private Function CreateToolbar() As Widget
            Dim lToolbar As New Toolbar()
            lToolbar.ToolbarStyle = ToolbarStyle.Icons
            lToolbar.IconSize = IconSize.SmallToolbar
            
            ' Refresh button
            pRefreshButton = New ToolButton(Nothing, "Refresh")
            pRefreshButton.IconWidget = Image.NewFromIconName("view-Refresh", IconSize.SmallToolbar)
            pRefreshButton.TooltipText = "Refresh git Status"
            lToolbar.Insert(pRefreshButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Pull button
            pPullButton = New ToolButton(Nothing, "Pull")
            pPullButton.IconWidget = Image.NewFromIconName("go-down", IconSize.SmallToolbar)
            pPullButton.TooltipText = "Pull from remote"
            lToolbar.Insert(pPullButton, -1)
            
            ' Push button
            pPushButton = New ToolButton(Nothing, "Push")
            pPushButton.IconWidget = Image.NewFromIconName("go-up", IconSize.SmallToolbar)
            pPushButton.TooltipText = "Push to remote"
            lToolbar.Insert(pPushButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Branch label
            Dim lBranchItem As New ToolItem()
            Dim lBranchBox As New Box(Orientation.Horizontal, 6)
            lBranchBox.PackStart(New Label("Branch:"), False, False, 0)
            pBranchLabel = New Label("master")
            pBranchLabel.Markup = "<b>master</b>"
            lBranchBox.PackStart(pBranchLabel, False, False, 0)
            lBranchItem.Add(lBranchBox)
            lToolbar.Insert(lBranchItem, -1)
            
            Return lToolbar
        End Function
        
         Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                pNotebook.SetThemeManager(vThemeManager)
            Catch
            End Try
        End Sub
        
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
            
            pStageAllButton = New Button("Stage All")
            lButtonBox.PackStart(pStageAllButton, False, False, 0)
            
            pUnstageAllButton = New Button("Unstage All")
            lButtonBox.PackStart(pUnstageAllButton, False, False, 0)
            
            lTopBox.PackStart(lButtonBox, False, False, 0)
            
            ' File list
            Dim lScrolledWindow As New ScrolledWindow()
            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolledWindow.HeightRequest = 100
            
            ' Create tree view for file status
            ' Columns: Staged (checkbox), Status, File
            pStatusStore = New ListStore(GetType(Boolean), GetType(String), GetType(String), GetType(String))
            pStatusTreeView = New TreeView(pStatusStore)
            
            ' Staged column (checkbox)
            Dim lStagedRenderer As New CellRendererToggle()
            lStagedRenderer.Activatable = True
            AddHandler lStagedRenderer.Toggled, AddressOf OnStagedToggled
            Dim lStagedColumn As New TreeViewColumn("", lStagedRenderer, "active", 0)
            pStatusTreeView.AppendColumn(lStagedColumn)
            
            ' Status column
            Dim lStatusColumn As New TreeViewColumn("Status", New CellRendererText(), "text", 1)
            pStatusTreeView.AppendColumn(lStatusColumn)
            
            ' File column
            Dim lFileColumn As New TreeViewColumn("File", New CellRendererText(), "text", 2)
            lFileColumn.Resizable = True
            pStatusTreeView.AppendColumn(lFileColumn)
            
            ' Hidden full path column
            Dim lPathColumn As New TreeViewColumn("Path", New CellRendererText(), "text", 3)
            lPathColumn.Visible = False
            pStatusTreeView.AppendColumn(lPathColumn)
            
            lScrolledWindow.Add(pStatusTreeView)
            lTopBox.PackStart(lScrolledWindow, True, True, 0)
            
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
            pCommitButton = New Button("Commit")
            pCommitButton.Sensitive = False
            lCommitButtonBox.PackEnd(pCommitButton, False, False, 0)
            lCommitBox.PackStart(lCommitButtonBox, False, False, 0)
            
            lVPaned.Pack2(lCommitBox, False, False)
            
            Return lVPaned
        End Function
        
        Private Function CreateHistoryPage() As Widget
            Dim lScrolledWindow As New ScrolledWindow()
            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create tree view for commit history
            ' Columns: Hash, Author, Date, Message
            pHistoryStore = New ListStore(GetType(String), GetType(String), GetType(String), GetType(String))
            pHistoryTreeView = New TreeView(pHistoryStore)
            
            Dim lHashColumn As New TreeViewColumn("Commit", New CellRendererText(), "text", 0)
            lHashColumn.Resizable = True
            pHistoryTreeView.AppendColumn(lHashColumn)
            
            Dim lAuthorColumn As New TreeViewColumn("Author", New CellRendererText(), "text", 1)
            lAuthorColumn.Resizable = True
            pHistoryTreeView.AppendColumn(lAuthorColumn)
            
            Dim lDateColumn As New TreeViewColumn("Date", New CellRendererText(), "text", 2)
            lDateColumn.Resizable = True
            pHistoryTreeView.AppendColumn(lDateColumn)
            
            Dim lMessageColumn As New TreeViewColumn("Message", New CellRendererText(), "text", 3)
            lMessageColumn.Resizable = True
            pHistoryTreeView.AppendColumn(lMessageColumn)
            
            lScrolledWindow.Add(pHistoryTreeView)
            
            Return lScrolledWindow
        End Function
        
        Private Function CreateDiffPage() As Widget
            Dim lScrolledWindow As New ScrolledWindow()
            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            pDiffTextView = New TextView()
            pDiffTextView.Editable = False
            pDiffTextView.CursorVisible = False
            Dim lCss As String = "textview { font-family: Monospace; font-size: 9pt; }"
            Dim lCssProvider As New CssProvider()
            lCssProvider.LoadFromData(lCss)
            pDiffTextView.StyleContext.AddProvider(lCssProvider, StyleProviderPriority.Application)            
            pDiffBuffer = pDiffTextView.Buffer
            
            ' Create tags for diff highlighting
            CreateDiffTags()
            
            lScrolledWindow.Add(pDiffTextView)
            
            Return lScrolledWindow
        End Function
        
        Private Sub CreateDiffTags()
            ' Added lines (green)
            Dim lAddTag As New TextTag("diff_add")
            lAddTag.Background = "#E6FFED"
            lAddTag.Foreground = "#22863A"
            pDiffBuffer.TagTable.Add(lAddTag)
            
            ' Removed lines (red)
            Dim lRemoveTag As New TextTag("diff_remove")
            lRemoveTag.Background = "#FFEEF0"
            lRemoveTag.Foreground = "#CB2431"
            pDiffBuffer.TagTable.Add(lRemoveTag)
            
            ' Header lines (blue)
            Dim lHeaderTag As New TextTag("diff_header")
            lHeaderTag.Background = "#F1F8FF"
            lHeaderTag.Foreground = "#032F62"
            lHeaderTag.Weight = Pango.Weight.Bold
            pDiffBuffer.TagTable.Add(lHeaderTag)
            
            ' Line numbers (gray)
            Dim lLineNumTag As New TextTag("diff_linenum")
            lLineNumTag.Foreground = "#6A737D"
            pDiffBuffer.TagTable.Add(lLineNumTag)
        End Sub
        
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
            
            ' File selection
            AddHandler pStatusTreeView.CursorChanged, AddressOf OnFileSelectionChanged
            AddHandler pStatusTreeView.RowActivated, AddressOf OnFileDoubleClicked
            
            ' History selection
            AddHandler pHistoryTreeView.CursorChanged, AddressOf OnCommitSelectionChanged
        End Sub
        
        Private Sub OnStagedToggled(vSender As Object, vArgs As ToggledArgs)
            Try
                Dim lPath As New TreePath(vArgs.Path)
                Dim lIter As TreeIter = Nothing
                
                If pStatusStore.GetIter(lIter, lPath) Then
                    Dim lIsStaged As Boolean = CBool(pStatusStore.GetValue(lIter, 0))
                    Dim lFilePath As String = CStr(pStatusStore.GetValue(lIter, 3))
                    
                    ' Toggle staging
                    If lIsStaged Then
                        UnstageFile(lFilePath)
                    Else
                        StageFile(lFilePath)
                    End If
                    
                    ' Update the checkbox
                    pStatusStore.SetValue(lIter, 0, Not lIsStaged)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error toggling stage: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pStatusTreeView.Selection
                Dim lIter As TreeIter = Nothing
                
                If lSelection.GetSelected(lIter) Then
                    pSelectedFile = CStr(pStatusStore.GetValue(lIter, 3))
                    ShowDiff(pSelectedFile)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error on file selection: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileDoubleClicked(vSender As Object, vArgs As RowActivatedArgs)
            Try
                Dim lIter As TreeIter = Nothing
                If pStatusStore.GetIter(lIter, vArgs.Path) Then
                    Dim lFilePath As String = CStr(pStatusStore.GetValue(lIter, 3))
                    RaiseEvent FileSelected(lFilePath)
                End If
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
            ExecuteGitCommand("Pull", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    If String.IsNullOrEmpty(Errors) Then
                        ShowMessage("Pull completed successfully")
                        RefreshGitStatus()
                    Else
                        ShowError($"Pull failed: {Errors}")
                    End If
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub OnPush(vSender As Object, vE As EventArgs)
            ExecuteGitCommand("Push", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    If String.IsNullOrEmpty(Errors) Then
                        ShowMessage("Push completed successfully")
                    Else
                        ShowError($"Push failed: {Errors}")
                    End If
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub OnStageAll(vSender As Object, vE As EventArgs)
            ExecuteGitCommand("add -A", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    RefreshGitStatus()
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub OnUnstageAll(vSender As Object, vE As EventArgs)
            ExecuteGitCommand("reset HEAD", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    RefreshGitStatus()
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub OnCommit(vSender As Object, vE As EventArgs)
            Dim lMessage As String = pCommitMessageEntry.Buffer.Text.Trim()
            If String.IsNullOrEmpty(lMessage) Then Return
            
            ' Escape the commit message for shell
            lMessage = lMessage.Replace("""", "\""")
            
            ExecuteGitCommand($"Commit -m ""{lMessage}""", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    If String.IsNullOrEmpty(Errors) Then
                        ShowMessage("Commit successful")
                        pCommitMessageEntry.Buffer.Text = ""
                        RefreshGitStatus()
                        RefreshHistory()
                    Else
                        ShowError($"Commit failed: {Errors}")
                    End If
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub OnCommitSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pHistoryTreeView.Selection
                Dim lIter As TreeIter = Nothing
                
                If lSelection.GetSelected(lIter) Then
                    Dim lCommitHash As String = CStr(pHistoryStore.GetValue(lIter, 0))
                    ShowCommitDiff(lCommitHash)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error on Commit selection: {ex.Message}")
            End Try
        End Sub
        
        ' Git operations
        Public Sub RefreshGitStatus()
            If String.IsNullOrEmpty(pProjectRoot) Then Return
            
            ' Clear current status
            pStatusStore.Clear()
            
            ' Get current branch
            ExecuteGitCommand("branch --Show-current", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    If Not String.IsNullOrEmpty(output) Then
                        pBranchLabel.Markup = $"<b>{output.Trim()}</b>"
                    End If
                    Return False
                End Function)
            End Sub)
            
            ' Get file status
            ExecuteGitCommand("Status --porcelain", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    ParseGitStatus(output)
                    Return False
                End Function)
            End Sub)
            
            ' Refresh history
            RefreshHistory()
        End Sub
        
        Private Sub RefreshHistory()
            If String.IsNullOrEmpty(pProjectRoot) Then Return
            
            pHistoryStore.Clear()
            
            ' Get commit history
            ExecuteGitCommand("log --pretty=format:""%h|%an|%ad|%s"" --date=short -20", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    ParseGitHistory(output)
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub ParseGitStatus(vOutput As String)
            If String.IsNullOrEmpty(vOutput) Then Return
            
            Dim lLines() As String = vOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            
            for each lLine in lLines
                If lLine.Length < 3 Then Continue for
                
                Dim lStatus As String = lLine.Substring(0, 2)
                Dim lFilePath As String = lLine.Substring(3).Trim()
                
                Dim lFileInfo As New GitFileInfo()
                lFileInfo.FilePath = lFilePath
                
                ' Parse status
                Select Case lStatus
                    Case "??"
                        lFileInfo.Status = GitFileStatus.eUntracked
                        lFileInfo.IsStaged = False
                    Case " M", "M "
                        lFileInfo.Status = GitFileStatus.eModified
                        lFileInfo.IsStaged = (lStatus(0) = "M"c)
                    Case "A ", "AM"
                        lFileInfo.Status = GitFileStatus.eAdded
                        lFileInfo.IsStaged = True
                    Case " D", "D "
                        lFileInfo.Status = GitFileStatus.eDeleted
                        lFileInfo.IsStaged = (lStatus(0) = "D"c)
                    Case "r "
                        lFileInfo.Status = GitFileStatus.eRenamed
                        lFileInfo.IsStaged = True
                    Case Else
                        lFileInfo.Status = GitFileStatus.eModified
                        lFileInfo.IsStaged = (lStatus(0) <> " "c AndAlso lStatus(0) <> "?"c)
                End Select
                
                ' Add to store
                pStatusStore.AppendValues(
                    lFileInfo.IsStaged,
                    lFileInfo.StatusIcon,
                    lFileInfo.DisplayPath,
                    lFileInfo.FilePath
                )
            Next
        End Sub
        
        Private Sub ParseGitHistory(vOutput As String)
            If String.IsNullOrEmpty(vOutput) Then Return
            
            Dim lLines() As String = vOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            
            for each lLine in lLines
                Dim lParts() As String = lLine.Split("|"c)
                If lParts.Length >= 4 Then
                    pHistoryStore.AppendValues(
                        lParts(0), ' Hash
                        lParts(1), ' Author
                        lParts(2), ' Date
                        lParts(3)  ' Message
                    )
                End If
            Next
        End Sub
        
        Private Sub ShowDiff(vFilePath As String)
            If String.IsNullOrEmpty(vFilePath) Then Return
            
            ExecuteGitCommand($"diff HEAD -- ""{vFilePath}""", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    DisplayDiff(output)
                    pNotebook.CurrentPage = 2 ' Switch to diff tab
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub ShowCommitDiff(vCommitHash As String)
            ExecuteGitCommand($"Show {vCommitHash}", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    DisplayDiff(output)
                    pNotebook.CurrentPage = 2 ' Switch to diff tab
                    Return False
                End Function)
            End Sub)
        End Sub
        
        ''' <summary>
        ''' Displays diff text with proper syntax highlighting using stable offsets
        ''' </summary>
        ''' <param name="vDiffText">The diff text to display</param>
        Private Sub DisplayDiff(vDiffText As String)
            Try
                pDiffBuffer.Text = ""
                
                If String.IsNullOrEmpty(vDiffText) Then
                    pDiffBuffer.Text = "No Changes to display"
                    Return
                End If
                
                Dim lLines() As String = vDiffText.Split({Environment.NewLine, vbLf}, StringSplitOptions.None)
                
                for each lLine in lLines
                    ' Store the offset before inserting the line
                    Dim lStartOffset As Integer = pDiffBuffer.CharCount
                    
                    ' Insert the line
                    pDiffBuffer.PlaceCursor(pDiffBuffer.EndIter)
                    pDiffBuffer.InsertAtCursor(lLine & Environment.NewLine)
                    
                    ' Get the end offset after insertion
                    Dim lEndOffset As Integer = pDiffBuffer.CharCount
                    
                    ' Determine which tag to apply based on line content
                    Dim lTagName As String = Nothing
                    If lLine.StartsWith("+") AndAlso Not lLine.StartsWith("+++") Then
                        lTagName = "diff_add"
                    ElseIf lLine.StartsWith("-") AndAlso Not lLine.StartsWith("---") Then
                        lTagName = "diff_remove"
                    ElseIf lLine.StartsWith("@@") Then
                        lTagName = "diff_linenum"
                    ElseIf lLine.StartsWith("diff ") OrElse lLine.StartsWith("index ") OrElse _
                           lLine.StartsWith("commit ") OrElse lLine.StartsWith("Author:") OrElse _
                           lLine.StartsWith("Date:") Then
                        lTagName = "diff_header"
                    End If
                    
                    ' Apply the tag using stable offsets
                    If Not String.IsNullOrEmpty(lTagName) Then
                        Dim lStartIter As TextIter = pDiffBuffer.GetIterAtOffset(lStartOffset)
                        Dim lEndIter As TextIter = pDiffBuffer.GetIterAtOffset(lEndOffset)
                        pDiffBuffer.ApplyTag(lTagName, lStartIter, lEndIter)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DisplayDiff error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub StageFile(vFilePath As String)
            ExecuteGitCommand($"add ""{vFilePath}""", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    RefreshGitStatus()
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub UnstageFile(vFilePath As String)
            ExecuteGitCommand($"reset HEAD ""{vFilePath}""", Sub(output, Errors)
                GLib.Idle.Add(Function()
                    RefreshGitStatus()
                    Return False
                End Function)
            End Sub)
        End Sub
        
        Private Sub ExecuteGitCommand(vCommand As String, vCallback As Action(Of String, String))
            If String.IsNullOrEmpty(pProjectRoot) Then Return
            
            Task.Run(Sub()
                Try
                    Dim lProcess As New Process()
                    lProcess.StartInfo.FileName = "git"
                    lProcess.StartInfo.Arguments = vCommand
                    lProcess.StartInfo.WorkingDirectory = pProjectRoot
                    lProcess.StartInfo.UseShellExecute = False
                    lProcess.StartInfo.RedirectStandardOutput = True
                    lProcess.StartInfo.RedirectStandardError = True
                    lProcess.StartInfo.CreateNoWindow = True
                    
                    lProcess.Start()
                    
                    Dim lOutput As String = lProcess.StandardOutput.ReadToEnd()
                    Dim lErrors As String = lProcess.StandardError.ReadToEnd()
                    
                    lProcess.WaitForExit()
                    
                    vCallback(lOutput, lErrors)
                    
                Catch ex As Exception
                    vCallback("", ex.Message)
                End Try
            End Sub)
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
                If Not String.IsNullOrEmpty(Value) AndAlso Directory.Exists(System.IO.Path.Combine(Value, ".git")) Then
                    RefreshGitStatus()
                Else
                    pStatusStore.Clear()
                    pHistoryStore.Clear()
                    pDiffBuffer.Text = "Not a git repository"
                End If
            End Set
        End Property
        
        Public Sub Refresh()
            RefreshGitStatus()
        End Sub
    End Class
End Namespace
 
