' MainWindow.Project.vb - Project management implementation
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Utilities
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    
    ' ===== Project Operations =====
    Public Sub OnNewProject(vSender As Object, vArgs As EventArgs)
        Try
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then Return
            
            ' Show new project dialog
            Dim lDialog As New NewProjectDialog(Me)
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lProjectPath As String = lDialog.ProjectPath
                Dim lProjectName As String = lDialog.ProjectName
                Dim lProjectType As String = lDialog.ProjectType
                
                ' Create project
                If CreateNewProject(lProjectPath, lProjectName, lProjectType) Then
                    ' Load the new project
                    Dim lProjectFile As String = System.IO.Path.Combine(lProjectPath, $"{lProjectName}.vbproj")
                    LoadProjectEnhanced(lProjectFile)
                    
                    ' Add to recent projects
                    pSettingsManager.AddRecentProject(lProjectFile)
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnNewProject error: {ex.Message}")
            ShowError("New project error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnOpenProject(vSender As Object, vArgs As EventArgs)
        Try
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then Return
            
            Dim lDialog As FileChooserDialog = FileOperations.CreateOpenProjectDialog(Me)
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                LoadProjectEnhanced(lDialog.FileName)
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnOpenProject error: {ex.Message}")
            ShowError("Open project error", ex.Message)
        End Try
    End Sub
    
    Private Function CreateNewProject(vProjectPath As String, vProjectName As String, vProjectType As String) As Boolean
        Try
            ' Create project directory
            If Not Directory.Exists(vProjectPath) Then
                Directory.CreateDirectory(vProjectPath)
            End If
            
            ' Create project file
            Dim lProjectFile As String = System.IO.Path.Combine(vProjectPath, $"{vProjectName}.vbproj")
            
            ' FIXED: Create parameters for VbProjTemplate
            Dim lOutputType As String = ""
            Dim lPackageReferences As String = ""
            
            Select Case vProjectType
                Case "Console"
                    lOutputType = "Exe"
                Case "Library"
                    lOutputType = "Library"
                Case Else 
                    lOutputType = "Exe"
                    lPackageReferences = StringResources.Instance.GetString("GtkPackageReference")
            End Select
            
            Dim lProjectParams As New Dictionary(Of String, String) From {
                {"OutputType", lOutputType},
                {"RootNamespace", vProjectName},
                {"PackageReferences", lPackageReferences}
            }
            
            Dim lProjectContent As String = StringResources.Instance.GetTemplate(StringResources.KEY_VBPROJ_TEMPLATE, lProjectParams)  
          
            File.WriteAllText(lProjectFile, lProjectContent)
            
            ' Create My Project directory
            Dim lMyProjectDir As String = System.IO.Path.Combine(vProjectPath, "My project")
            Directory.CreateDirectory(lMyProjectDir)
            
            ' Create AssemblyInfo.vb
            Dim lAssemblyInfoFile As String = System.IO.Path.Combine(lMyProjectDir, "AssemblyInfo.vb")

            ' FIXED: Create parameters for AssemblyInfoTemplate
            Dim lAssemblyParams As New Dictionary(Of String, String) From {
                {"ProjectName", vProjectName},
                {"Description", $"{vProjectName} application"},
                {"Company", ""},
                {"Year", DateTime.Now.Year.ToString()}
            }

            Dim lAssemblyInfoContent As String = StringResources.Instance.GetTemplate(StringResources.KEY_ASSEMBLYINFO_TEMPLATE, lAssemblyParams)

            File.WriteAllText(lAssemblyInfoFile, lAssemblyInfoContent)
            
            ' Create main program file
            Dim lProgramFile As String = System.IO.Path.Combine(vProjectPath, "Program.vb")
            Dim lProgramContent As String = ""
            
            Select Case vProjectType
                Case "Console"
                    ' FIXED: Create parameters for ConsoleProjectTemplate
                    Dim lConsoleParams As New Dictionary(Of String, String) From {
                        {"ProjectName", vProjectName}
                    }
                    lProgramContent = StringResources.Instance.GetTemplate(StringResources.KEY_CONSOLE_PROJECT_TEMPLATE, lConsoleParams)                
                Case "Library"
                    ' FIXED: Create parameters for LibraryProjectTemplate
                    Dim lLibraryParams As New Dictionary(Of String, String) From {
                        {"ProjectName", vProjectName},
                        {"ClassName", "Class1"}
                    }
                    lProgramContent = StringResources.Instance.GetTemplate(StringResources.KEY_LIBRARY_PROJECT_TEMPLATE, lLibraryParams)                
                Case Else 
                    ' FIXED: Create parameters for GtkProjectTemplate
                    Dim lGtkParams As New Dictionary(Of String, String) From {
                        {"ProjectName", vProjectName}
                    }
                    lProgramContent = StringResources.Instance.GetTemplate(StringResources.KEY_GTK_PROJECT_TEMPLATE, lGtkParams)                    
            End Select
            
            File.WriteAllText(lProgramFile, lProgramContent)
            
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CreateNewProject error: {ex.Message}")
            ShowError("Create project error", ex.Message)
            Return False
        End Try
    End Function
    
    ' ===== Project Management =====
    Public Sub OnAddNewItem(vSender As Object, vArgs As EventArgs)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowInfo("No project", "Please open a project first.")
                Return
            End If
            
            ' Simple input dialog for new item name
            Dim lInputDialog As New InputDialog(Me, "Add New Item", "Enter item Name:", "NewClass.vb")
            
            If lInputDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lItemName As String = lInputDialog.Text.Trim()
                
                If String.IsNullOrEmpty(lItemName) Then
                    ShowError("Invalid Name", "Please enter a valid item Name.")
                    Return
                End If
                
                ' Ensure .vb extension
                If Not lItemName.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then
                    lItemName &= ".vb"
                End If
                
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
                Dim lItemPath As String = System.IO.Path.Combine(lProjectDir, lItemName)
                
                ' Check if file already exists
                If File.Exists(lItemPath) Then
                    ShowError("File Exists", "A file with that Name already exists.")
                    Return
                End If
                
                ' Create basic class template
                Dim lClassName As String = System.IO.Path.GetFileNameWithoutExtension(lItemName)
                Dim lContent As String = $"' {lClassName}" & Environment.NewLine &
                                       $"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" & Environment.NewLine &
                                       Environment.NewLine &
                                       $"Public Class {lClassName}" & Environment.NewLine &
                                       Environment.NewLine &
                                       "    ' Constructor" & Environment.NewLine &
                                       "    Public Sub New()" & Environment.NewLine &
                                       "        ' TODO: Initialize class" & Environment.NewLine &
                                       "    End Sub" & Environment.NewLine &
                                       Environment.NewLine &
                                       "End Class"
                
                ' Write the file
                File.WriteAllText(lItemPath, lContent)
                
                ' Refresh project explorer
                pProjectExplorer.RefreshProject()
                
                ' Open the new file in editor
                OpenFileInEditor(lItemPath)
                
                ShowInfo("Item Added", $"Added {lItemName} to project.")
            End If
            
            lInputDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnAddNewItem error: {ex.Message}")
            ShowError("Add item error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnSaveProject(vSender As Object, vArgs As EventArgs)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowInfo("No project", "Please open a project first.")
                Return
            End If
            
            ' Save all open files
            SaveAllFiles()
            
            ' Save project file if needed
            ' TODO: Implement project file saving
            
            ShowInfo("Project Saved", "Project and all files have been saved.")
            
        Catch ex As Exception
            Console.WriteLine($"OnSaveProject error: {ex.Message}")
            ShowError("Save project error", ex.Message)
        End Try
    End Sub
    
    Public Sub OnCloseProject(vSender As Object, vArgs As EventArgs)
        Try
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then
                Return
            End If
            
            ' Close project
            CloseCurrentProject()
            
            ShowInfo("Project Closed", "Project has been closed.")
            
        Catch ex As Exception
            Console.WriteLine($"OnCloseProject error: {ex.Message}")
            ShowError("Close project error", ex.Message)
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    ' Check for unsaved changes in all open documents
    Private Function CheckUnsavedChanges() As Boolean
        Try
            ' TODO: Implement check for unsaved changes
            ' For now, always return true
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CheckUnsavedChanges error: {ex.Message}")
            Return True
        End Try
    End Function
    
    ' Close the current project
    Private Sub CloseCurrentProject()
        Try
            ' Close all open documents
            CloseAllTabs()
            
            ' Clear project explorer
            pProjectExplorer.ClearProject()
            
            ' Clear current project
            pCurrentProject = ""
            
            ' Update window title
            UpdateWindowTitle()
            
        Catch ex As Exception
            Console.WriteLine($"CloseCurrentProject error: {ex.Message}")
        End Try
    End Sub
    
    ' Open a file in the editor
    Private Sub OpenFileInEditor(vFilePath As String)
        Try
            ' TODO: Implement file opening in editor
            Console.WriteLine($"Opening file: {vFilePath}")
            
        Catch ex As Exception
            Console.WriteLine($"OpenFileInEditor error: {ex.Message}")
        End Try
    End Sub
    
'    ' Load project with enhanced error handling
'    Private Sub LoadProjectEnhanced(vProjectFile As String)
'        Try
'            If Not File.Exists(vProjectFile) Then
'                ShowError("Project Not Found", $"Project file not found: {vProjectFile}")
'                Return
'            End If
'            
'            ' Set current project
'            pCurrentProject = vProjectFile
'            
'            ' Load project in project explorer
'            pProjectExplorer.LoadProject(vProjectFile)
'            
'            ' Update window title
'            UpdateWindowTitle()
'            
'            ' Add to recent projects
'            pSettingsManager.AddRecentProject(vProjectFile)
'            
'            ShowInfo("Project Loaded", $"Project loaded: {System.IO.Path.GetFileNameWithoutExtension(vProjectFile)}")
'            
'        Catch ex As Exception
'            Console.WriteLine($"LoadProjectEnhanced error: {ex.Message}")
'            ShowError("Load project error", ex.Message)
'        End Try
'    End Sub
    
    ' Update window title based on current project
    Private Sub UpdateWindowTitle()
        Try
            Dim lTitle As String = ApplicationVersion.Title
            
            ' Add version in debug builds or if configured
            If pSettingsManager.ShowVersionInTitle OrElse System.Diagnostics.Debugger.IsAttached Then
                lTitle &= $" v{ApplicationVersion.VersionString}"
            End If
            
            ' Add project name if loaded
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
                lTitle &= $" - {lProjectName}"
                
                ' Add project version if available
                Try
                    Dim lVersionManager As New AssemblyVersionManager(pCurrentProject)
                    Dim lProjectVersion As Version = lVersionManager.GetCurrentVersion()
                    If lProjectVersion IsNot Nothing AndAlso lProjectVersion.ToString() <> "1.0.0.0" Then
                        lTitle &= $" (v{lProjectVersion.Major}.{lProjectVersion.Minor}.{lProjectVersion.Build})"
                    End If
                Catch
                    ' Ignore project version errors
                End Try
            End If
            
            ' Add dirty indicator if needed
            If pProjectManager?.IsDirty = True Then
                lTitle = $"*{lTitle}"
            End If
            
            Title = lTitle
            
        Catch ex As Exception
            Console.WriteLine($"UpdateWindowTitle error: {ex.Message}")
            Title = "SimpleIDE"
        End Try
    End Sub

    ' Add version info to status bar methods
    Private Sub UpdateStatusBarWithVersion()
        Try
            ' Show application version in status occasionally
            If pBuildStatusLabel IsNot Nothing Then
                pBuildStatusLabel.Text = $"IDE v{ApplicationVersion.VersionString}"
                pBuildStatusLabel.Visible = True
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateStatusBarWithVersion error: {ex.Message}")
        End Try
    End Sub
    
    ' Method to refresh version displays throughout the UI
    Public Sub RefreshVersionDisplays()
        Try
            ' Update window title
            UpdateWindowTitle()
            
            ' Update status bar
            UpdateStatusBarWithVersion()
            
            ' Clear application version cache to get fresh data
            ApplicationVersion.ClearCache()
            
            Console.WriteLine($"Version displays refreshed - IDE: {ApplicationVersion.VersionString}")
            
        Catch ex As Exception
            Console.WriteLine($"RefreshVersionDisplays error: {ex.Message}")
        End Try
    End Sub

End Class
