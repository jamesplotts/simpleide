' Dialogs/NewProjectDialog.vb - Dialog for creating new VB.NET projects
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Utilities


Namespace Dialogs
    
    Public Class NewProjectDialog
        Inherits Dialog
        
        ' Private fields
        Private pProjectNameEntry As Entry
        Private pLocationEntry As Entry
        Private pBrowseButton As Button
        Private pProjectTypeCombo As ComboBoxText
        Private pCreateDirectoryCheck As CheckButton
        Private pInitializeGitCheck As CheckButton
        Private pSolutionNameEntry As Entry
        Private pCreateSolutionCheck As CheckButton
        
        ' Project templates
        Private Structure ProjectTemplate
            Public Name As String
            Public OutputType As String
            Public Description As String
            
            Public Sub New(vName As String, vOutputType As String, vDescription As String)
                Name = vName
                OutputType = vOutputType
                Description = vDescription
            End Sub
        End Structure
        
        Private pTemplates() As ProjectTemplate = {
            New ProjectTemplate("Console Application", "Exe", "A command-Line application"),
            New ProjectTemplate("Class Library", "Library", "A Library that can be used by other applications"),
            New ProjectTemplate("GTK# Application", "Exe", "A GTK# cross-Platform desktop application"),
            New ProjectTemplate("Web API", "Exe", "an ASP.NET Core Web API application"),
            New ProjectTemplate("Test project", "Library", "A unit Test project using NUnit")
        }
        
        ' Properties
        Public ReadOnly Property ProjectName As String
            Get
                Return pProjectNameEntry.Text.Trim()
            End Get
        End Property
        
        Public ReadOnly Property ProjectPath As String
            Get
                Dim lLocation As String = pLocationEntry.Text.Trim()
                If pCreateDirectoryCheck.Active Then
                    Return System.IO.Path.Combine(lLocation, ProjectName)
                Else
                    Return lLocation
                End If
            End Get
        End Property
        
        Public ReadOnly Property ProjectType As String
            Get
                If pProjectTypeCombo.Active >= 0 Then
                    Return pTemplates(pProjectTypeCombo.Active).OutputType
                End If
                Return "Exe"
            End Get
        End Property
        
        Public ReadOnly Property SolutionName As String
            Get
                Return pSolutionNameEntry.Text.Trim()
            End Get
        End Property
        
        Public ReadOnly Property CreateSolution As Boolean
            Get
                Return pCreateSolutionCheck.Active
            End Get
        End Property
        
        Public ReadOnly Property InitializeGit As Boolean
            Get
                Return pInitializeGitCheck.Active
            End Get
        End Property
        
        Public Sub New(vParent As Window)
            MyBase.New("New project", vParent, DialogFlags.Modal)
            
            ' Window setup
            SetDefaultSize(600, 450)
            SetPosition(WindowPosition.CenterOnParent)
            BorderWidth = 10
            
            ' Build UI
            BuildUI()
            
            ' Set defaults
            SetDefaults()
            
            ' Connect events
            ConnectEvents()
            
            ' Show all
            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Try
                ' Create main content area
                Dim lContentArea As Box = CType(ContentArea, Box)
                lContentArea.Spacing = 10
                
                ' Create notebook for tabbed interface
                Dim lNotebook As New Notebook()
                lContentArea.PackStart(lNotebook, True, True, 0)
                
                ' ===== Project tab =====
                Dim lProjectBox As New Box(Orientation.Vertical, 10)
                lProjectBox.BorderWidth = 10
                
                ' Project name
                Dim lNameFrame As New Frame("project Name")
                Dim lNameBox As New Box(Orientation.Vertical, 5)
                lNameBox.BorderWidth = 10
                
                pProjectNameEntry = New Entry()
                pProjectNameEntry.PlaceholderText = "Enter project Name"
                lNameBox.PackStart(pProjectNameEntry, False, False, 0)
                
                lNameFrame.Add(lNameBox)
                lProjectBox.PackStart(lNameFrame, False, False, 0)
                
                ' Location
                Dim lLocationFrame As New Frame("Location")
                Dim lLocationBox As New Box(Orientation.Vertical, 5)
                lLocationBox.BorderWidth = 10
                
                Dim lLocationHBox As New Box(Orientation.Horizontal, 5)
                pLocationEntry = New Entry()
                pLocationEntry.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                pBrowseButton = New Button("Browse...")
                
                lLocationHBox.PackStart(pLocationEntry, True, True, 0)
                lLocationHBox.PackStart(pBrowseButton, False, False, 0)
                lLocationBox.PackStart(lLocationHBox, False, False, 0)
                
                pCreateDirectoryCheck = New CheckButton("Create directory for solution")
                pCreateDirectoryCheck.Active = True
                lLocationBox.PackStart(pCreateDirectoryCheck, False, False, 0)
                
                lLocationFrame.Add(lLocationBox)
                lProjectBox.PackStart(lLocationFrame, False, False, 0)
                
                ' Project type
                Dim lTypeFrame As New Frame("project Template")
                Dim lTypeBox As New Box(Orientation.Vertical, 5)
                lTypeBox.BorderWidth = 10
                
                pProjectTypeCombo = New ComboBoxText()
                For Each lTemplate In pTemplates
                    pProjectTypeCombo.AppendText(lTemplate.Name)
                Next
                pProjectTypeCombo.Active = 0 ' Default to Console Application
                
                lTypeBox.PackStart(pProjectTypeCombo, False, False, 0)
                
                ' Template description
                Dim lDescLabel As New Label()
                lDescLabel.Markup = "<i>A command-Line application</i>"
                lDescLabel.Xalign = 0
                lTypeBox.PackStart(lDescLabel, False, False, 0)
                
                lTypeFrame.Add(lTypeBox)
                lProjectBox.PackStart(lTypeFrame, False, False, 0)
                
                lNotebook.AppendPage(lProjectBox, New Label("Project"))
                
                ' ===== Advanced tab =====
                Dim lAdvancedBox As New Box(Orientation.Vertical, 10)
                lAdvancedBox.BorderWidth = 10
                
                ' Solution settings
                Dim lSolutionFrame As New Frame("Solution")
                Dim lSolutionBox As New Box(Orientation.Vertical, 5)
                lSolutionBox.BorderWidth = 10
                
                pCreateSolutionCheck = New CheckButton("Create solution")
                pCreateSolutionCheck.Active = True
                lSolutionBox.PackStart(pCreateSolutionCheck, False, False, 0)
                
                Dim lSolutionHBox As New Box(Orientation.Horizontal, 5)
                Dim lSolutionLabel As New Label("Solution Name:")
                lSolutionLabel.SetSizeRequest(100, -1)
                pSolutionNameEntry = New Entry()
                
                lSolutionHBox.PackStart(lSolutionLabel, False, False, 0)
                lSolutionHBox.PackStart(pSolutionNameEntry, True, True, 0)
                lSolutionBox.PackStart(lSolutionHBox, False, False, 0)
                
                lSolutionFrame.Add(lSolutionBox)
                lAdvancedBox.PackStart(lSolutionFrame, False, False, 0)
                
                ' Git settings
                Dim lGitFrame As New Frame("Source Control")
                Dim lGitBox As New Box(Orientation.Vertical, 5)
                lGitBox.BorderWidth = 10
                
                pInitializeGitCheck = New CheckButton("Initialize git repository")
                pInitializeGitCheck.Active = True
                lGitBox.PackStart(pInitializeGitCheck, False, False, 0)
                
                Dim lGitNote As New Label()
                lGitNote.Markup = "<small><i>Creates a .gitignore file and initializes the repository</i></small>"
                lGitNote.Xalign = 0
                lGitBox.PackStart(lGitNote, False, False, 0)
                
                lGitFrame.Add(lGitBox)
                lAdvancedBox.PackStart(lGitFrame, False, False, 0)
                
                lNotebook.AppendPage(lAdvancedBox, New Label("Advanced"))
                
                ' Dialog buttons
                AddButton("Cancel", ResponseType.Cancel)
                AddButton("Create", ResponseType.Ok)
                
                ' Update template description when combo changes
                AddHandler pProjectTypeCombo.Changed, Sub()
                    If pProjectTypeCombo.Active >= 0 Then
                        lDescLabel.Markup = $"<i>{pTemplates(pProjectTypeCombo.Active).Description}</i>"
                    End If
                End Sub
                
            Catch ex As Exception
                Console.WriteLine($"NewProjectDialog.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SetDefaults()
            Try
                ' Set default project name
                pProjectNameEntry.Text = "MyProject"
                pSolutionNameEntry.Text = "MyProject"
                
                ' Focus project name entry
                pProjectNameEntry.GrabFocus()
                pProjectNameEntry.SelectRegion(0, -1)
                
            Catch ex As Exception
                Console.WriteLine($"NewProjectDialog.SetDefaults error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ConnectEvents()
            Try
                ' Browse button
                AddHandler pBrowseButton.Clicked, AddressOf OnBrowseClicked
                
                ' Project name changed - update solution name
                AddHandler pProjectNameEntry.Changed, Sub()
                    If pSolutionNameEntry.Text = "MyProject" OrElse String.IsNullOrEmpty(pSolutionNameEntry.Text) Then
                        pSolutionNameEntry.Text = pProjectNameEntry.Text
                    End If
                End Sub
                
                ' Create solution checkbox
                AddHandler pCreateSolutionCheck.Toggled, Sub()
                    pSolutionNameEntry.Sensitive = pCreateSolutionCheck.Active
                End Sub
                
                
            Catch ex As Exception
                Console.WriteLine($"NewProjectDialog.ConnectEvents error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnBrowseClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select project Location",
                    Me,
                    FileChooserAction.SelectFolder,
                    "Cancel", ResponseType.Cancel,
                    "Select", ResponseType.Accept
                )
                
                ' Set current folder
                If Not String.IsNullOrEmpty(pLocationEntry.Text) AndAlso Directory.Exists(pLocationEntry.Text) Then
                    lDialog.SetCurrentFolder(pLocationEntry.Text)
                End If
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    pLocationEntry.Text = lDialog.FileName
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnBrowseClicked error: {ex.Message}")
            End Try
        End Sub
        
        Protected Overrides Sub OnResponse(vResponseId As ResponseType)
            Try
                If vResponseId = ResponseType.Ok Then
                    ' Validate input
                    If String.IsNullOrWhiteSpace(ProjectName) Then
                        ShowError("project Name is required.")
                        Return ' Don't call base to prevent dialog from closing
                    End If
                    
                    If String.IsNullOrWhiteSpace(pLocationEntry.Text) Then
                        ShowError("Location is required.")
                        Return ' Don't call base to prevent dialog from closing
                    End If
                    
                    ' Check if directory already exists
                    If pCreateDirectoryCheck.Active Then
                        Dim lFullPath As String = ProjectPath
                        If Directory.Exists(lFullPath) Then
                            Dim lDialog As New MessageDialog(
                                Me,
                                DialogFlags.Modal,
                                MessageType.Question,
                                ButtonsType.YesNo,
                                $"Directory '{lFullPath}' already exists. Do you want to continue?"
                            )
                            
                            Dim lResult As Integer = lDialog.Run()
                            lDialog.Destroy()
                            
                            If lResult <> CInt(ResponseType.Yes) Then
                                Return ' Don't call base to prevent dialog from closing
                            End If
                        End If
                    End If
                    
                    ' Validate project name (no invalid characters)
                    If ProjectName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 Then
                        ShowError("project Name contains invalid characters.")
                        Return ' Don't call base to prevent dialog from closing
                    End If
                End If
                
                ' Only call base if validation passed or if canceling
                MyBase.OnResponse(vResponseId)
                
            Catch ex As Exception
                Console.WriteLine($"OnResponse error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ShowError(vMessage As String)
            Try
                Dim lDialog As New MessageDialog(
                    Me,
                    DialogFlags.Modal,
                    MessageType.Error,
                    ButtonsType.Ok,
                    vMessage
                )
                lDialog.Run()
                lDialog.Destroy()
            Catch ex As Exception
                Console.WriteLine($"ShowError error: {ex.Message}")
            End Try
        End Sub
        
    End Class

End Namespace