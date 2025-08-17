' Dialogs/GitCommitDialog.vb - Git commit message dialog
Imports Gtk
Imports System
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Dialogs
    
    Public Class GitCommitDialog
        Inherits Dialog
        
        ' Private fields
        Private pMessageTextView As TextView
        Private pMessageBuffer As TextBuffer
        Private pStagedFilesView As TreeView
        Private pStagedFilesStore As ListStore
        Private pGitManager As GitManager
        Private pCommitButton As Button
        Private pAmendCheck As CheckButton
        Private pSignOffCheck As CheckButton
        
        ' Properties
        Public ReadOnly Property CommitMessage As String
            Get
                Return pMessageBuffer.Text.Trim()
            End Get
        End Property
        
        Public ReadOnly Property AmendCommit As Boolean
            Get
                Return pAmendCheck.Active
            End Get
        End Property
        
        Public ReadOnly Property SignOff As Boolean
            Get
                Return pSignOffCheck.Active
            End Get
        End Property
        
        ' Constructor
        Public Sub New(vParent As Window, vGitManager As GitManager)
            MyBase.New("Commit Changes", vParent, DialogFlags.Modal)
            
            pGitManager = vGitManager
            
            Try
                SetupDialog()
                BuildUI()
                LoadStagedFiles()
                ConnectEvents()
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"GitCommitDialog constructor error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SetupDialog()
            Try
                SetDefaultSize(600, 500)
                SetPosition(WindowPosition.CenterOnParent)
                BorderWidth = 10
                
            Catch ex As Exception
                Console.WriteLine($"SetupDialog error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub BuildUI()
            Try
                Dim lMainBox As New Box(Orientation.Vertical, 10)
                
                ' Staged files section
                Dim lStagedFrame As New Frame("Staged Files")
                Dim lStagedBox As New Box(Orientation.Vertical, 5)
                lStagedBox.BorderWidth = 10
                
                Dim lScrolledWindow As New ScrolledWindow()
                lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolledWindow.HeightRequest = 150
                lScrolledWindow.ShadowType = ShadowType.In
                
                ' Create staged files list
                pStagedFilesStore = New ListStore(GetType(String), GetType(String))
                pStagedFilesView = New TreeView(pStagedFilesStore)
                
                ' Status column
                Dim lStatusColumn As New TreeViewColumn("Status", New CellRendererText(), "Text", 0)
                pStagedFilesView.AppendColumn(lStatusColumn)
                
                ' File column
                Dim lFileColumn As New TreeViewColumn("File", New CellRendererText(), "Text", 1)
                lFileColumn.Expand = True
                pStagedFilesView.AppendColumn(lFileColumn)
                
                lScrolledWindow.Add(pStagedFilesView)
                lStagedBox.PackStart(lScrolledWindow, True, True, 0)
                
                lStagedFrame.Add(lStagedBox)
                lMainBox.PackStart(lStagedFrame, True, True, 0)
                
                ' Commit message section
                Dim lMessageFrame As New Frame("Commit Message")
                Dim lMessageBox As New Box(Orientation.Vertical, 5)
                lMessageBox.BorderWidth = 10
                
                Dim lMessageScroll As New ScrolledWindow()
                lMessageScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lMessageScroll.HeightRequest = 150
                lMessageScroll.ShadowType = ShadowType.In
                
                pMessageTextView = New TextView()
                pMessageTextView.WrapMode = WrapMode.Word
                pMessageTextView.LeftMargin = 5
                pMessageTextView.RightMargin = 5
                pMessageBuffer = pMessageTextView.Buffer
                
                ' Set monospace font
                Dim lCss As String = "textview { font-family: Monospace; font-size: 10pt; }"
                Dim lCssProvider As New CssProvider()
                lCssProvider.LoadFromData(lCss)
                pMessageTextView.StyleContext.AddProvider(lCssProvider, StyleProviderPriority.Application)
                
                lMessageScroll.Add(pMessageTextView)
                lMessageBox.PackStart(lMessageScroll, True, True, 0)
                
                ' Character count label
                Dim lCharCountLabel As New Label("0 characters")
                lCharCountLabel.Halign = Align.End
                lMessageBox.PackStart(lCharCountLabel, False, False, 0)
                
                ' Update character count
                AddHandler pMessageBuffer.Changed, Sub()
                    Dim lCount As Integer = pMessageBuffer.Text.Length
                    lCharCountLabel.Text = $"{lCount} characters"
                    
                    ' Warn if first line is too long (convention is 50 chars)
                    Dim lFirstLine As String = pMessageBuffer.Text.Split({Environment.NewLine, vbLf}, StringSplitOptions.None)(0)
                    If lFirstLine.Length > 50 Then
                        lCharCountLabel.Markup = $"<span foreground=""orange"">{lCount} characters (first Line: {lFirstLine.Length}/50)</span>"
                    ElseIf lFirstLine.Length > 72 Then
                        lCharCountLabel.Markup = $"<span foreground=""red"">{lCount} characters (first Line too long: {lFirstLine.Length}/50)</span>"
                    End If
                End Sub
                
                lMessageFrame.Add(lMessageBox)
                lMainBox.PackStart(lMessageFrame, True, True, 0)
                
                ' Options
                Dim lOptionsBox As New Box(Orientation.Horizontal, 10)
                
                pAmendCheck = New CheckButton("Amend previous Commit")
                lOptionsBox.PackStart(pAmendCheck, False, False, 0)
                
                pSignOffCheck = New CheckButton("Add Signed-off-by Line")
                lOptionsBox.PackStart(pSignOffCheck, False, False, 0)
                
                lMainBox.PackStart(lOptionsBox, False, False, 0)
                
                ' Add main box to dialog
                ContentArea.PackStart(lMainBox, True, True, 0)
                
                ' Dialog buttons
                AddButton("Cancel", ResponseType.Cancel)
                pCommitButton = CType(AddButton("Commit", ResponseType.Ok), Button)
                pCommitButton.Sensitive = False
                
                ' Make Commit button suggested action
                pCommitButton.StyleContext.AddClass("suggested-action")
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadStagedFiles()
            Try
                ' Clear existing
                pStagedFilesStore.Clear()
                
                ' TODO: Get staged files from git
                ' For now, add placeholder
                pStagedFilesStore.AppendValues("M", "MainWindow.vb")
                pStagedFilesStore.AppendValues("A", "NewFile.vb")
                
            Catch ex As Exception
                Console.WriteLine($"LoadStagedFiles error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ConnectEvents()
            Try
                ' Enable/disable commit button based on message
                AddHandler pMessageBuffer.Changed, AddressOf OnMessageChanged
                
                ' Handle amend checkbox
                AddHandler pAmendCheck.Toggled, AddressOf OnAmendToggled
                
                ' Focus message area
                pMessageTextView.GrabFocus()
                
            Catch ex As Exception
                Console.WriteLine($"ConnectEvents error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnMessageChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' Enable commit button only if message is not empty
                pCommitButton.Sensitive = Not String.IsNullOrWhiteSpace(pMessageBuffer.Text)
                
            Catch ex As Exception
                Console.WriteLine($"OnMessageChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAmendToggled(vSender As Object, vArgs As EventArgs)
            Try
                If pAmendCheck.Active Then
                    ' TODO: Load previous commit message
                    ' For now, just add placeholder
                    pMessageBuffer.Text = "[Previous Commit Message would appear here]"
                Else
                    pMessageBuffer.Text = ""
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnAmendToggled error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace