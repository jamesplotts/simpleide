Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.AI
Imports SimpleIDE.Managers

' MainWindow.WelcomeTab.vb
' Created: 2025-08-15 05:02:31
' Modified: Two-column layout with buttons on the right

Partial Public Class MainWindow

    ''' <summary>
    ''' Shows the welcome tab with a two-column layout
    ''' </summary>
    Private Sub ShowWelcomeTab()
        Try
            ' Don't show welcome tab if there are already open tabs
            If pNotebook.NPages > 0 Then
                Return
            End If
            
            ' Create a scrolled window to contain the welcome content
            ' This allows the content to scroll if the window is too small
            Dim lScrolledWindow As New ScrolledWindow()
            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create main horizontal container for two-column layout
            Dim lMainHBox As New Box(Orientation.Horizontal, 40)
            lMainHBox.Halign = Align.Center
            lMainHBox.Valign = Align.Start
            lMainHBox.MarginTop = 40
            lMainHBox.MarginStart = 40
            lMainHBox.MarginEnd = 40
            lMainHBox.MarginBottom = 40
            
            ' === LEFT COLUMN: Icon and Information ===
            Dim lLeftColumn As New Box(Orientation.Vertical, 20)
            lLeftColumn.Halign = Align.Center
            lLeftColumn.Valign = Align.Start
            
            ' Try to load and display the application icon
            Try
                Dim lIconStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                If lIconStream IsNot Nothing Then
                    Dim lPixbuf As Gdk.Pixbuf = New Gdk.Pixbuf(lIconStream)
                    ' Scale icon to reasonable size (128x128)
                    Dim lScaledPixbuf As Gdk.Pixbuf = lPixbuf.ScaleSimple(128, 128, Gdk.InterpType.Bilinear)
                    Dim lIconImage As New Image(lScaledPixbuf)
                    lLeftColumn.PackStart(lIconImage, False, False, 0)
                End If
            Catch ex As Exception
                Console.WriteLine($"Could not load welcome icon: {ex.Message}")
            End Try
            
            ' Welcome title
            Dim lWelcomeLabel As New Label()
            lWelcomeLabel.Markup = StringResources.Instance.GetString(StringResources.KEY_WELCOME_MESSAGE)
            lLeftColumn.PackStart(lWelcomeLabel, False, False, 0)
            
            ' Version info
            Dim lVersionLabel As New Label("Version 1.0")
            lVersionLabel.Markup = "<span size='small' foreground='#888888'>Version " + ApplicationVersion.VersionString + "</span>"
            lLeftColumn.PackStart(lVersionLabel, False, False, 0)
            
            ' Optional: Add description or tips
            Dim lDescriptionLabel As New Label()
            lDescriptionLabel.Markup = "<span size='medium'>A lightweight VB.NET IDE for Linux</span>"
            lDescriptionLabel.Wrap = True
            lDescriptionLabel.MaxWidthChars = 40
            lLeftColumn.PackStart(lDescriptionLabel, False, False, 10)
            
            ' === RIGHT COLUMN: Action Buttons ===
            Dim lRightColumn As New Box(Orientation.Vertical, 10)
            lRightColumn.Halign = Align.Start
            lRightColumn.Valign = Align.Start
            
            ' Instructions header
            Dim lInstructionLabel As New Label()
            lInstructionLabel.Markup = "<span size='large' weight='bold'>Get Started</span>"
            lRightColumn.PackStart(lInstructionLabel, False, False, 0)
            
            ' Add separator line
            Dim lSeparator As New Separator(Orientation.Horizontal)
            lRightColumn.PackStart(lSeparator, False, False, 5)
            
            ' Create button box for actions
            Dim lButtonBox As New Box(Orientation.Vertical, 10)
            lButtonBox.Halign = Align.Fill
            
            ' New Project button
            Dim lNewProjectButton As New Button("Create New Project")
            lNewProjectButton.WidthRequest = 250
            lNewProjectButton.HeightRequest = 40
            AddHandler lNewProjectButton.Clicked, AddressOf OnNewProject
            lButtonBox.PackStart(lNewProjectButton, False, False, 0)
            
            ' Open Project button  
            Dim lOpenProjectButton As New Button("Open Existing Project")
            lOpenProjectButton.WidthRequest = 250
            lOpenProjectButton.HeightRequest = 40
            AddHandler lOpenProjectButton.Clicked, AddressOf OnOpenProject
            lButtonBox.PackStart(lOpenProjectButton, False, False, 0)
            
            ' Recent Projects section (if any)
            Dim lRecentProjects As List(Of String) = pSettingsManager.RecentProjects
            If lRecentProjects.Count > 0 Then
                ' Add spacing before recent projects
                lButtonBox.PackStart(New Label(""), False, False, 10)
                
                ' Recent projects header
                Dim lRecentLabel As New Label()
                lRecentLabel.Markup = "<span size='medium' weight='bold'>Recent Projects</span>"
                lRecentLabel.Halign = Align.Start
                lButtonBox.PackStart(lRecentLabel, False, False, 0)
                
                ' Add small separator
                Dim lRecentSeparator As New Separator(Orientation.Horizontal)
                lButtonBox.PackStart(lRecentSeparator, False, False, 3)
                
                ' Create a frame for recent projects to visually group them
                Dim lRecentFrame As New Frame()
                lRecentFrame.ShadowType = ShadowType.In
                Dim lRecentBox As New Box(Orientation.Vertical, 5)
                lRecentBox.BorderWidth = 5
                
                ' Show up to 5 recent projects
                For i As Integer = 0 To Math.Min(4, lRecentProjects.Count - 1)
                    Dim lProjectPath As String = lRecentProjects(i)
                    Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(lProjectPath)
                    
                    ' Create button with project name
                    Dim lRecentButton As New Button()
                    
                    ' Create horizontal box for button content
                    Dim lButtonContent As New Box(Orientation.Horizontal, 5)
                    
                    ' Add project icon (optional)
                    Dim lProjectIcon As New Image()
                    lProjectIcon.SetFromIconName("document-open", IconSize.Button)
                    lButtonContent.PackStart(lProjectIcon, False, False, 0)
                    
                    ' Add project name label
                    Dim lNameLabel As New Label(lProjectName)
                    lNameLabel.Halign = Align.Start
                    lButtonContent.PackStart(lNameLabel, True, True, 0)
                    
                    lRecentButton.Add(lButtonContent)
                    lRecentButton.WidthRequest = 240
                    lRecentButton.HeightRequest = 35
                    lRecentButton.TooltipText = lProjectPath
                    
                    ' Capture the project path in a local variable for the lambda
                    Dim lPath As String = lProjectPath
                    AddHandler lRecentButton.Clicked, Sub() LoadProjectEnhanced(lPath)
                    
                    lRecentBox.PackStart(lRecentButton, False, False, 0)
                Next
                
                lRecentFrame.Add(lRecentBox)
                lButtonBox.PackStart(lRecentFrame, False, False, 0)
            End If
            
            ' Add buttons to right column
            lRightColumn.PackStart(lButtonBox, False, False, 0)
            
            ' === Assemble the two columns ===
            lMainHBox.PackStart(lLeftColumn, False, False, 0)
            
            ' Add a vertical separator between columns
            Dim lVerticalSeparator As New Separator(Orientation.Vertical)
            lMainHBox.PackStart(lVerticalSeparator, False, False, 0)
            
            lMainHBox.PackStart(lRightColumn, False, False, 0)
            
            ' Add the main container to the scrolled window
            lScrolledWindow.Add(lMainHBox)
            
            ' Show all widgets
            lScrolledWindow.ShowAll()
            
            ' Add to notebook
            pNotebook.AppendPage(lScrolledWindow, New Label("Welcome"))
            
            ' Make sure it's the current page
            pNotebook.CurrentPage = pNotebook.NPages - 1
            
        Catch ex As Exception
            Console.WriteLine($"ShowWelcomeTab error: {ex.Message}")
        End Try
    End Sub

End Class