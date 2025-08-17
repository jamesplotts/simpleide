 
' Dialogs/BuildConfigurationDialog.vb - Build configuration dialog
Imports Gtk
Imports System
Imports System.IO

Namespace Dialogs

    Public Class BuildConfigurationDialog
        Inherits Dialog
        
        ' Private fields
        Private pConfigurationCombo As ComboBoxText
        Private pPlatformCombo As ComboBoxText
        Private pVerbosityCombo As ComboBoxText
        Private pOutputPathEntry As Entry
        Private pAdditionalArgsEntry As Entry
        Private pRestorePackagesCheck As CheckButton
        Private pCleanBeforeBuildCheck As CheckButton
        Private pBuildConfiguration As Models.BuildConfiguration
        
        Public ReadOnly Property BuildConfiguration As Models.BuildConfiguration
            Get
                Return pBuildConfiguration
            End Get
        End Property
        
        Public Sub New(vParent As Window, vCurrentConfig As Models.BuildConfiguration)
            MyBase.New("Build Configuration", vParent, DialogFlags.Modal)
            
            ' Clone the configuration to avoid modifying the original
            pBuildConfiguration = New Models.BuildConfiguration()
            CopyConfiguration(vCurrentConfig, pBuildConfiguration)
            
            ' Window setup
            SetDefaultSize(500, 400)
            SetPosition(WindowPosition.CenterOnParent)
            BorderWidth = 10
            
            ' Build UI
            BuildUI()
            
            ' Load current settings
            LoadSettings()
            
            ' Add buttons
            AddButton("_Cancel", ResponseType.Cancel)
            AddButton("_OK", ResponseType.Ok)
            
            ' Set default button
            DefaultResponse = ResponseType.Ok
            
            ' Connect response handler
            AddHandler Me.Response, AddressOf OnResponse
            
            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Dim lVBox As New Box(Orientation.Vertical, 10)
            
            ' Build Settings section
            Dim lBuildFrame As New Frame("Build Settings")
            Dim lBuildGrid As New Grid()
            lBuildGrid.RowSpacing = 6
            lBuildGrid.ColumnSpacing = 12
            lBuildGrid.BorderWidth = 12
            
            ' Configuration
            Dim lConfigLabel As New Label("Configuration:")
            lConfigLabel.Halign = Align.Start
            lBuildGrid.Attach(lConfigLabel, 0, 0, 1, 1)
            
            pConfigurationCombo = New ComboBoxText()
            pConfigurationCombo.AppendText("Debug")
            pConfigurationCombo.AppendText("Release")
            pConfigurationCombo.AppendText("Test")
            lBuildGrid.Attach(pConfigurationCombo, 1, 0, 1, 1)
            
            ' Platform
            Dim lPlatformLabel As New Label("Platform:")
            lPlatformLabel.Halign = Align.Start
            lBuildGrid.Attach(lPlatformLabel, 0, 1, 1, 1)
            
            pPlatformCombo = New ComboBoxText()
            pPlatformCombo.AppendText("any CPU")
            pPlatformCombo.AppendText("x86")
            pPlatformCombo.AppendText("x64")
            pPlatformCombo.AppendText("ARM")
            pPlatformCombo.AppendText("ARM64")
            lBuildGrid.Attach(pPlatformCombo, 1, 1, 1, 1)
            
            ' Verbosity
            Dim lVerbosityLabel As New Label("Verbosity:")
            lVerbosityLabel.Halign = Align.Start
            lBuildGrid.Attach(lVerbosityLabel, 0, 2, 1, 1)
            
            pVerbosityCombo = New ComboBoxText()
            pVerbosityCombo.AppendText("Quiet")
            pVerbosityCombo.AppendText("Minimal")
            pVerbosityCombo.AppendText("Normal")
            pVerbosityCombo.AppendText("Detailed")
            pVerbosityCombo.AppendText("Diagnostic")
            lBuildGrid.Attach(pVerbosityCombo, 1, 2, 1, 1)
            
            ' Output Path
            Dim lOutputLabel As New Label("output Path:")
            lOutputLabel.Halign = Align.Start
            lBuildGrid.Attach(lOutputLabel, 0, 3, 1, 1)
            
            Dim lOutputHBox As New Box(Orientation.Horizontal, 5)
            pOutputPathEntry = New Entry()
            pOutputPathEntry.PlaceholderText = "Leave empty for default"
            lOutputHBox.PackStart(pOutputPathEntry, True, True, 0)
            
            Dim lBrowseButton As New Button("Browse...")
            AddHandler lBrowseButton.Clicked, AddressOf OnBrowseOutputPath
            lOutputHBox.PackStart(lBrowseButton, False, False, 0)
            
            lBuildGrid.Attach(lOutputHBox, 1, 3, 1, 1)
            
            ' Additional Arguments
            Dim lArgsLabel As New Label("Additional Arguments:")
            lArgsLabel.Halign = Align.Start
            lBuildGrid.Attach(lArgsLabel, 0, 4, 1, 1)
            
            pAdditionalArgsEntry = New Entry()
            pAdditionalArgsEntry.PlaceholderText = "e.g., --no-restore --force"
            lBuildGrid.Attach(pAdditionalArgsEntry, 1, 4, 1, 1)
            
            lBuildFrame.Add(lBuildGrid)
            lVBox.PackStart(lBuildFrame, False, False, 0)
            
            ' Options section
            Dim lOptionsFrame As New Frame("Options")
            Dim lOptionsVBox As New Box(Orientation.Vertical, 6)
            lOptionsVBox.BorderWidth = 12
            
            ' Restore packages
            pRestorePackagesCheck = New CheckButton("Restore NuGet Packages before build")
            pRestorePackagesCheck.TooltipText = "Run 'dotnet restore' before building"
            lOptionsVBox.PackStart(pRestorePackagesCheck, False, False, 0)
            
            ' Clean before build
            pCleanBeforeBuildCheck = New CheckButton("Clean before build (Rebuild)")
            pCleanBeforeBuildCheck.TooltipText = "Run 'dotnet clean' before building"
            lOptionsVBox.PackStart(pCleanBeforeBuildCheck, False, False, 0)
            
            lOptionsFrame.Add(lOptionsVBox)
            lVBox.PackStart(lOptionsFrame, False, False, 0)
            
            ' Command Preview section
            Dim lPreviewFrame As New Frame("Command Preview")
            Dim lPreviewVBox As New Box(Orientation.Vertical, 6)
            lPreviewVBox.BorderWidth = 12
            
            Dim lPreviewLabel As New Label("Generated dotnet command:")
            lPreviewLabel.Halign = Align.Start
            lPreviewVBox.PackStart(lPreviewLabel, False, False, 0)
            
            Dim lPreviewScrolled As New ScrolledWindow()
            lPreviewScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lPreviewScrolled.SetSizeRequest(-1, 60)
            
            Dim lPreviewTextView As New TextView()
            lPreviewTextView.Editable = False
            lPreviewTextView.WrapMode = WrapMode.Word
            lPreviewTextView.Buffer.Text = "dotnet build --Configuration Debug"
            lPreviewScrolled.Add(lPreviewTextView)
            
            lPreviewVBox.PackStart(lPreviewScrolled, True, True, 0)
            lPreviewFrame.Add(lPreviewVBox)
            lVBox.PackStart(lPreviewFrame, True, True, 0)
            
            ' Update preview when settings change
            AddHandler pConfigurationCombo.Changed, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pPlatformCombo.Changed, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pVerbosityCombo.Changed, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pOutputPathEntry.Changed, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pAdditionalArgsEntry.Changed, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pRestorePackagesCheck.Toggled, Sub() UpdateCommandPreview(lPreviewTextView)
            AddHandler pCleanBeforeBuildCheck.Toggled, Sub() UpdateCommandPreview(lPreviewTextView)
            
            ContentArea.PackStart(lVBox, True, True, 0)
        End Sub
        
        Private Sub LoadSettings()
            Try
                ' Load configuration
                Select Case pBuildConfiguration.Configuration.ToLower()
                    Case "debug"
                        pConfigurationCombo.Active = 0
                    Case "release"
                        pConfigurationCombo.Active = 1
                    Case "Test"
                        pConfigurationCombo.Active = 2
                    Case Else
                        pConfigurationCombo.Active = 0
                End Select
                
                ' Load platform
                Select Case pBuildConfiguration.Platform.ToLower()
                    Case "any cpu"
                        pPlatformCombo.Active = 0
                    Case "x86"
                        pPlatformCombo.Active = 1
                    Case "x64"
                        pPlatformCombo.Active = 2
                    Case "arm"
                        pPlatformCombo.Active = 3
                    Case "arm64"
                        pPlatformCombo.Active = 4
                    Case Else
                        pPlatformCombo.Active = 0
                End Select
                
                ' Load verbosity
                pVerbosityCombo.Active = CInt(pBuildConfiguration.Verbosity)
                
                ' Load other settings
                pOutputPathEntry.Text = pBuildConfiguration.OutputPath
                pAdditionalArgsEntry.Text = pBuildConfiguration.AdditionalArguments
                pRestorePackagesCheck.Active = pBuildConfiguration.RestorePackages
                pCleanBeforeBuildCheck.Active = pBuildConfiguration.CleanBeforeBuild
                
            Catch ex As Exception
                Console.WriteLine($"error loading build Configuration settings: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveSettings()
            Try
                ' Save configuration
                Select Case pConfigurationCombo.Active
                    Case 0
                        pBuildConfiguration.Configuration = "Debug"
                    Case 1
                        pBuildConfiguration.Configuration = "Release"
                    Case 2
                        pBuildConfiguration.Configuration = "Test"
                End Select
                
                ' Save platform
                Select Case pPlatformCombo.Active
                    Case 0
                        pBuildConfiguration.Platform = "any CPU"
                    Case 1
                        pBuildConfiguration.Platform = "x86"
                    Case 2
                        pBuildConfiguration.Platform = "x64"
                    Case 3
                        pBuildConfiguration.Platform = "ARM"
                    Case 4
                        pBuildConfiguration.Platform = "ARM64"
                End Select
                
                ' Save verbosity
                pBuildConfiguration.Verbosity = CType(pVerbosityCombo.Active, Models.BuildVerbosity)
                
                ' Save other settings
                pBuildConfiguration.OutputPath = pOutputPathEntry.Text.Trim()
                pBuildConfiguration.AdditionalArguments = pAdditionalArgsEntry.Text.Trim()
                pBuildConfiguration.RestorePackages = pRestorePackagesCheck.Active
                pBuildConfiguration.CleanBeforeBuild = pCleanBeforeBuildCheck.Active
                
            Catch ex As Exception
                Console.WriteLine($"error saving build Configuration settings: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateCommandPreview(vTextView As TextView)
            Try
                ' Create a temporary configuration with current UI values
                Dim lTempConfig As New Models.BuildConfiguration()
                
                ' Get values from UI
                Select Case pConfigurationCombo.Active
                    Case 0
                        lTempConfig.Configuration = "Debug"
                    Case 1
                        lTempConfig.Configuration = "Release"
                    Case 2
                        lTempConfig.Configuration = "Test"
                End Select
                
                Select Case pPlatformCombo.Active
                    Case 0
                        lTempConfig.Platform = "any CPU"
                    Case 1
                        lTempConfig.Platform = "x86"
                    Case 2
                        lTempConfig.Platform = "x64"
                    Case 3
                        lTempConfig.Platform = "ARM"
                    Case 4
                        lTempConfig.Platform = "ARM64"
                End Select
                
                lTempConfig.Verbosity = CType(pVerbosityCombo.Active, Models.BuildVerbosity)
                lTempConfig.OutputPath = pOutputPathEntry.Text.Trim()
                lTempConfig.AdditionalArguments = pAdditionalArgsEntry.Text.Trim()
                lTempConfig.RestorePackages = pRestorePackagesCheck.Active
                lTempConfig.CleanBeforeBuild = pCleanBeforeBuildCheck.Active
                
                ' Generate command preview
                Dim lCommand As String = "dotnet " & lTempConfig.GetDotNetArguments() & " [ProjectFile]"
                
                ' Add restore and clean commands if enabled
                Dim lFullCommand As String = ""
                If lTempConfig.CleanBeforeBuild Then
                    lFullCommand &= "dotnet clean [ProjectFile]" & Environment.NewLine
                End If
                If lTempConfig.RestorePackages Then
                    lFullCommand &= "dotnet restore [ProjectFile]" & Environment.NewLine
                End If
                lFullCommand &= lCommand
                
                ' Update preview
                vTextView.Buffer.Text = lFullCommand
                
            Catch ex As Exception
                Console.WriteLine($"error updating command preview: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnBrowseOutputPath(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select output Directory",
                    Me,
                    FileChooserAction.SelectFolder,
                    "Cancel", ResponseType.Cancel,
                    "Select", ResponseType.Accept
                )
                
                ' Set current directory if path is set
                If Not String.IsNullOrEmpty(pOutputPathEntry.Text) AndAlso Directory.Exists(pOutputPathEntry.Text) Then
                    lDialog.SetCurrentFolder(pOutputPathEntry.Text)
                End If
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    pOutputPathEntry.Text = lDialog.FileName
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error browsing for output Path: {ex.Message}")
            End Try
        End Sub
        
        Private Shadows Sub OnResponse(vSender As Object, vE As ResponseArgs)
            Select Case vE.ResponseId
                Case ResponseType.Ok
                    SaveSettings()
                    Me.Hide()
                Case ResponseType.Cancel
                    Me.Hide()
            End Select
        End Sub
        
        Private Sub CopyConfiguration(vSource As Models.BuildConfiguration, vDestination As Models.BuildConfiguration)
            If vSource Is Nothing Or vDestination Is Nothing Then Return
            
            vDestination.Configuration = vSource.Configuration
            vDestination.Platform = vSource.Platform
            vDestination.Verbosity = vSource.Verbosity
            vDestination.OutputPath = vSource.OutputPath
            vDestination.AdditionalArguments = vSource.AdditionalArguments
            vDestination.RestorePackages = vSource.RestorePackages
            vDestination.CleanBeforeBuild = vSource.CleanBeforeBuild
        End Sub
    End Class


End Namespace