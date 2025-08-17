' MainWindow.AssemblySettings.vb - Assembly settings and manifest management
Imports Gtk
Imports System
Imports System.IO
Imports System.Xml
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities

Partial Public Class MainWindow
    
    ' ===== Assembly Settings Management =====
    
    ' Show Assembly Settings dialog
    Public Sub ShowAssemblySettings()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowInfo("No project", "Please open a project first.")
                Return
            End If
            
            Dim lDialog As New PreferencesDialog(Me, pSettingsManager, pThemeManager)
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                ' Refresh project explorer to update manifest status
                pProjectExplorer.RefreshManifestNode()
                
                ' Update any open editors that might be affected
                RefreshAssemblyRelatedEditors()
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowAssemblySettings error: {ex.Message}")
            ShowError("Assembly Settings error", ex.Message)
        End Try
    End Sub


    
    ' Handle Assembly Settings menu item
    Public Sub OnAssemblySettings(vSender As Object, vArgs As EventArgs)
        Try
            ShowAssemblySettings()
            
        Catch ex As Exception
            Console.WriteLine($"OnAssemblySettings error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Assembly Information Management =====
    
    ' Get AssemblyInfo.vb path for current project
    Public Function GetAssemblyInfoPath() As String
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                Return ""
            End If
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lMyProjectDir As String = System.IO.Path.Combine(lProjectDir, "My project")
            Dim lAssemblyInfoPath As String = System.IO.Path.Combine(lMyProjectDir, "AssemblyInfo.vb")
            
            Return lAssemblyInfoPath
            
        Catch ex As Exception
            Console.WriteLine($"GetAssemblyInfoPath error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' Check if AssemblyInfo.vb exists
    Public Function AssemblyInfoExists() As Boolean
        Try
            Dim lPath As String = GetAssemblyInfoPath()
            Return Not String.IsNullOrEmpty(lPath) AndAlso File.Exists(lPath)
            
        Catch ex As Exception
            Console.WriteLine($"AssemblyInfoExists error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Create default AssemblyInfo.vb file
    Public Function CreateDefaultAssemblyInfo() As Boolean
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                Return False
            End If
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
            Dim lMyProjectDir As String = System.IO.Path.Combine(lProjectDir, "My project")
            Dim lAssemblyInfoPath As String = System.IO.Path.Combine(lMyProjectDir, "AssemblyInfo.vb")
            
            ' Create My Project directory if it doesn't exist
            If Not Directory.Exists(lMyProjectDir) Then
                Directory.CreateDirectory(lMyProjectDir)
            End If
            
            ' Generate default AssemblyInfo content
            Dim lContent As String = GenerateDefaultAssemblyInfo(lProjectName)
            
            ' Write the file
            File.WriteAllText(lAssemblyInfoPath, lContent)
            
            Console.WriteLine($"Created AssemblyInfo.vb at: {lAssemblyInfoPath}")
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CreateDefaultAssemblyInfo error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Generate default AssemblyInfo.vb content
    Private Function GenerateDefaultAssemblyInfo(vProjectName As String) As String
        Try
            ' FIXED: Create the missing lParams dictionary
            Dim lParams As New Dictionary(Of String, String) From {
                {"ProjectName", vProjectName},
                {"Description", $"{vProjectName} application"},
                {"Company", ""},
                {"Year", DateTime.Now.Year.ToString()}
            }

            Dim lContent As String = StringResources.Instance.GetTemplate(StringResources.KEY_ASSEMBLYINFO_TEMPLATE, lParams)
            
            Return lContent
            
        Catch ex As Exception
            Console.WriteLine($"GenerateDefaultAssemblyInfo error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' ===== Manifest Management =====
    
    ' Get app.manifest path for current project
    Public Function GetManifestPath() As String
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                Return ""
            End If
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lManifestPath As String = System.IO.Path.Combine(lProjectDir, "app.manifest")
            
            Return lManifestPath
            
        Catch ex As Exception
            Console.WriteLine($"GetManifestPath error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    
    ' Create default app.manifest
    Public Function CreateDefaultManifest() As Boolean
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                Return False
            End If
            
            Dim lManifestPath As String = GetManifestPath()
            Dim lManifestContent As String = GetDefaultManifestContent()
            
            File.WriteAllText(lManifestPath, lManifestContent)
            
            Console.WriteLine($"Created app.manifest at: {lManifestPath}")
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CreateDefaultManifest error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Get default manifest content
    Private Function GetDefaultManifestContent() As String
        Return "<?xml version=""1.0"" encoding=""utf-8""?>" & Environment.NewLine &
               "<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">" & Environment.NewLine &
               "  <assemblyIdentity version=""1.0.0.0"" name=""MyApplication.app""/>" & Environment.NewLine &
               "  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">" & Environment.NewLine &
               "    <security>" & Environment.NewLine &
               "      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">" & Environment.NewLine &
               "        <requestedExecutionLevel level=""asInvoker"" uiAccess=""false"" />" & Environment.NewLine &
               "      </requestedPrivileges>" & Environment.NewLine &
               "    </security>" & Environment.NewLine &
               "  </trustInfo>" & Environment.NewLine &
               "</assembly>"
    End Function
    
    ' Refresh editors that might be showing assembly-related files
    Private Sub RefreshAssemblyRelatedEditors()
        Try
            ' This would refresh any open AssemblyInfo.vb or manifest files
            ' Implementation depends on editor management system
            
        Catch ex As Exception
            Console.WriteLine($"RefreshAssemblyRelatedEditors error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Version Management =====
    
    ' Get assembly version from AssemblyInfo.vb
    Public Function GetAssemblyVersion() As String
        Try
            Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
            If Not File.Exists(lAssemblyInfoPath) Then
                Return "1.0.0.0"
            End If
            
            Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
            
            ' Look for AssemblyVersion attribute
            Dim lVersionMatch As System.Text.RegularExpressions.Match = 
                System.Text.RegularExpressions.Regex.Match(lContent, 
                    "<Assembly:\s*AssemblyVersion\s*\(\s*""([^""]+)""\s*\)>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            
            If lVersionMatch.Success Then
                Return lVersionMatch.Groups(1).Value
            End If
            
            Return "1.0.0.0"
            
        Catch ex As Exception
            Console.WriteLine($"GetAssemblyVersion error: {ex.Message}")
            Return "1.0.0.0"
        End Try
    End Function
    
    ' Get assembly title from AssemblyInfo.vb
    Public Function GetAssemblyTitle() As String
        Try
            Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
            If Not File.Exists(lAssemblyInfoPath) Then
                Return System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
            End If
            
            Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
            
            ' Look for AssemblyTitle attribute
            Dim lTitleMatch As System.Text.RegularExpressions.Match = 
                System.Text.RegularExpressions.Regex.Match(lContent, 
                    "<Assembly:\s*AssemblyTitle\s*\(\s*""([^""]+)""\s*\)>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            
            If lTitleMatch.Success Then
                Return lTitleMatch.Groups(1).Value
            End If
            
            Return System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
            
        Catch ex As Exception
            Console.WriteLine($"GetAssemblyTitle error: {ex.Message}")
            Return System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
        End Try
    End Function
    
End Class
