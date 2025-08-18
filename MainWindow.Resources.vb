' MainWindow.Resources.vb - Resource management for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Reflection
Imports SimpleIDE.Editors
Imports SimpleIDE.Utilities

Partial Public Class MainWindow
    
    ' ===== Resource Management =====
    
    ' Handle resource file operations from project explorer
    Private Sub OnResourceFileSelected(vFilePath As String)
        Try
            If String.IsNullOrEmpty(vFilePath) Then Return
            
            ' Determine resource type by extension
            Dim lExtension As String = System.IO.Path.GetExtension(vFilePath).ToLower()
            
            Select Case lExtension
                Case ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico"
                    OpenImageResource(vFilePath)
                    
                Case ".resx"
                    OpenResxFile(vFilePath)
                    
                Case ".txt", ".xml", ".json", ".config"
                    OpenTextResource(vFilePath)
                    
                Case Else
                    ' Try to open as text
                    OpenTextResource(vFilePath)
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"OnResourceFileSelected error: {ex.Message}")
            ShowError("Resource error", $"Failed to open resource: {ex.Message}")
        End Try
    End Sub
    
    ' Open image resource in PNG editor
    Private Sub OpenImageResource(vFilePath As String)
        Try
            ' Check if already open
            If pOpenTabs.ContainsKey(vFilePath) Then
                ' Switch to existing tab
                SwitchToTab(vFilePath)
                Return
            End If
            
            ' Create PNG editor
            'Dim lEditor As New PngEditor(vFilePath, pSettingsManager)
            
            ' Create tab
            CreateNewTab(System.IO.Path.GetFileName(vFilePath), pProjectManager.GetSourceInfo(vFilePath), True)
            
            ' Update status
            UpdateStatusBar($"Opened image resource: {System.IO.Path.GetFileName(vFilePath)}")
            
        Catch ex As Exception
            Console.WriteLine($"OpenImageResource error: {ex.Message}")
            ShowError("Image Resource error", $"Failed to open image: {ex.Message}")
        End Try
    End Sub
    
    ' Open RESX file
    Private Sub OpenResxFile(vFilePath As String)
        Try
            ' For now, open as XML text file
            ' TODO: Create a proper RESX editor with grid view
            OpenFile(vFilePath)
            
        Catch ex As Exception
            Console.WriteLine($"OpenResxFile error: {ex.Message}")
            ShowError("RESX error", $"Failed to open .resx file: {ex.Message}")
        End Try
    End Sub
    
    ' Open text-based resource
    Private Sub OpenTextResource(vFilePath As String)
        Try
            OpenFile(vFilePath)
        Catch ex As Exception
            Console.WriteLine($"OpenTextResource error: {ex.Message}")
            ShowError("Resource error", $"Failed to open resource file: {ex.Message}")
        End Try
    End Sub
    
    ' Add new resource to project
    Public Sub AddResourceToProject(vResourceType As String)
        Try
            Select Case vResourceType.ToLower()
                Case "image"
                    AddImageResource()
                Case "IcOn"
                    AddIconResource()
                Case "Text"
                    AddTextResource()
                Case "resx"
                    AddResxResource()
                Case Else
                    ShowError("Unknown Resource", $"Unknown resource Type: {vResourceType}")
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"AddResourceToProject error: {ex.Message}")
            ShowError("Add Resource error", ex.Message)
        End Try
    End Sub
    
    ' Add image resource
    Private Sub AddImageResource()
        Try
            Dim lDialog As New FileChooserDialog(
                "Select Image",
                Me,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Add", ResponseType.Accept
            )
            
            ' Add filters
            Dim lImageFilter As New FileFilter()
            lImageFilter.Name = "Image Files"
            lImageFilter.AddPattern("*.png")
            lImageFilter.AddPattern("*.jpg")
            lImageFilter.AddPattern("*.jpeg")
            lImageFilter.AddPattern("*.gif")
            lImageFilter.AddPattern("*.bmp")
            lDialog.AddFilter(lImageFilter)
            
            Dim lAllFilter As New FileFilter()
            lAllFilter.Name = "All Files"
            lAllFilter.AddPattern("*")
            lDialog.AddFilter(lAllFilter)
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                Dim lSourcePath As String = lDialog.FileName
                CopyResourceToProject(lSourcePath, "Resources")
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"AddImageResource error: {ex.Message}")
            ShowError("Add Image error", ex.Message)
        End Try
    End Sub
    
    ' Add icon resource
    Private Sub AddIconResource()
        Try
            Dim lDialog As New FileChooserDialog(
                "Select Icon",
                Me,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Add", ResponseType.Accept
            )
            
            ' Add filters
            Dim lIconFilter As New FileFilter()
            lIconFilter.Name = "IcOn Files"
            lIconFilter.AddPattern("*.ico")
            lIconFilter.AddPattern("*.png")
            lDialog.AddFilter(lIconFilter)
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                Dim lSourcePath As String = lDialog.FileName
                CopyResourceToProject(lSourcePath, "Resources")
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"AddIconResource error: {ex.Message}")
            ShowError("Add Icon error", ex.Message)
        End Try
    End Sub
    
    ' Add text resource
    Private Sub AddTextResource()
        Try
            ' Create new text file
            Dim lDialog As New Dialog("New Text Resource", Me, DialogFlags.Modal)
            lDialog.SetDefaultSize(400, 150)
            
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            Dim lLabel As New Label("Enter resource file Name:")
            lVBox.PackStart(lLabel, False, False, 0)
            
            Dim lEntry As New Entry()
            lEntry.Text = "NewResource.txt"
            lEntry.ActivatesDefault = True
            lVBox.PackStart(lEntry, False, False, 0)
            
            lDialog.ContentArea.PackStart(lVBox, True, True, 0)
            
            lDialog.AddButton("Cancel", ResponseType.Cancel)
            Dim lCreateButton As Widget = lDialog.AddButton("Create", ResponseType.Ok)
            lDialog.Default = lCreateButton
            
            lDialog.ShowAll()
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lFileName As String = lEntry.Text
                If Not String.IsNullOrEmpty(lFileName) Then
                    CreateTextResource(lFileName)
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"AddTextResource error: {ex.Message}")
            ShowError("Add Text Resource error", ex.Message)
        End Try
    End Sub
    
    ' Add RESX resource
    Private Sub AddResxResource()
        Try
            ' Create new .resx file
            Dim lDialog As New Dialog("New RESX Resource", Me, DialogFlags.Modal)
            lDialog.SetDefaultSize(400, 150)
            
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            Dim lLabel As New Label("Enter resource file Name (without .resx):")
            lVBox.PackStart(lLabel, False, False, 0)
            
            Dim lEntry As New Entry()
            lEntry.Text = "Resources"
            lEntry.ActivatesDefault = True
            lVBox.PackStart(lEntry, False, False, 0)
            
            lDialog.ContentArea.PackStart(lVBox, True, True, 0)
            
            lDialog.AddButton("Cancel", ResponseType.Cancel)
            Dim lCreateButton As Widget = lDialog.AddButton("Create", ResponseType.Ok)
            lDialog.Default = lCreateButton
            
            lDialog.ShowAll()
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lFileName As String = lEntry.Text
                If Not String.IsNullOrEmpty(lFileName) Then
                    If Not lFileName.EndsWith(".resx", StringComparison.OrdinalIgnoreCase) Then
                        lFileName &= ".resx"
                    End If
                    CreateResxResource(lFileName)
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"AddResxResource error: {ex.Message}")
            ShowError("Add RESX Resource error", ex.Message)
        End Try
    End Sub
    
    ' Copy resource file to project
    Private Sub CopyResourceToProject(vSourcePath As String, vTargetFolder As String)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lResourceDir As String = System.IO.Path.Combine(lProjectDir, vTargetFolder)
            
            ' Create Resources directory if it doesn't exist
            If Not Directory.Exists(lResourceDir) Then
                Directory.CreateDirectory(lResourceDir)
            End If
            
            ' Copy file
            Dim lFileName As String = System.IO.Path.GetFileName(vSourcePath)
            Dim lTargetPath As String = System.IO.Path.Combine(lResourceDir, lFileName)
            
            ' Check if file already exists
            If File.Exists(lTargetPath) Then
                Dim lResponse As Integer = ShowQuestion(
                    "File Exists",
                    $"the file '{lFileName}' already exists. Do you want to Replace it?"
                )
                
                If lResponse <> CInt(ResponseType.Yes) Then
                    Return
                End If
            End If
            
            File.Copy(vSourcePath, lTargetPath, True)
            
            ' Add to project file
            AddResourceToProjectFile(lTargetPath, GetResourceBuildAction(lFileName))
            
            ' Refresh project explorer
            RefreshProjectExplorer()
            
            ' Open the resource
            OnResourceFileSelected(lTargetPath)
            
        Catch ex As Exception
            Console.WriteLine($"CopyResourceToProject error: {ex.Message}")
            ShowError("Copy Resource error", ex.Message)
        End Try
    End Sub
    
    ' Create text resource
    Private Sub CreateTextResource(vFileName As String)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lResourceDir As String = System.IO.Path.Combine(lProjectDir, "Resources")
            
            ' Create Resources directory if it doesn't exist
            If Not Directory.Exists(lResourceDir) Then
                Directory.CreateDirectory(lResourceDir)
            End If
            
            Dim lFilePath As String = System.IO.Path.Combine(lResourceDir, vFileName)
            
            ' Create empty file
            File.WriteAllText(lFilePath, "")
            
            ' Add to project file
            AddResourceToProjectFile(lFilePath, "Content")
            
            ' Refresh project explorer
            RefreshProjectExplorer()
            
            ' Open the file
            OpenFile(lFilePath)
            
        Catch ex As Exception
            Console.WriteLine($"CreateTextResource error: {ex.Message}")
            ShowError("Create Text Resource error", ex.Message)
        End Try
    End Sub
    
    ' Create RESX resource
    Private Sub CreateResxResource(vFileName As String)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lFilePath As String = System.IO.Path.Combine(lProjectDir, vFileName)
            
            ' Create basic .resx file
            Dim lResxContent As String = StringResources.Instance.GetString(
                StringResources.KEY_RESX_TEMPLATE)
            
            File.WriteAllText(lFilePath, lResxContent)
            
            ' Add to project file
            AddResourceToProjectFile(lFilePath, "EmbeddedResource")
            
            ' Refresh project explorer
            RefreshProjectExplorer()
            
            ' Open the file
            OpenFile(lFilePath)
            
        Catch ex As Exception
            Console.WriteLine($"CreateResxResource error: {ex.Message}")
            ShowError("Create RESX Resource error", ex.Message)
        End Try
    End Sub
    
    ' Get build action for resource
    Private Function GetResourceBuildAction(vFileName As String) As String
        Dim lExtension As String = System.IO.Path.GetExtension(vFileName).ToLower()
        
        Select Case lExtension
            Case ".resx"
                Return "EmbeddedResource"
            Case ".ico"
                Return "Resource"
            Case Else
                Return "Content"
        End Select
    End Function
    
    ' Add resource to project file
    Private Sub AddResourceToProjectFile(vResourcePath As String, vBuildAction As String)
        Try
            ' TODO: Implement project file modification
            ' This would involve:
            ' 1. Loading the .vbproj XML
            ' 2. Adding appropriate item group entry
            ' 3. Saving the project file
            
            UpdateStatusBar($"Added resource to project: {System.IO.Path.GetFileName(vResourcePath)}")
            
        Catch ex As Exception
            Console.WriteLine($"AddResourceToProjectFile error: {ex.Message}")
        End Try
    End Sub
    
    ' Load embedded resources
    Private Function LoadEmbeddedResource(vResourceName As String) As Stream
        Try
            Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
            Return lAssembly.GetManifestResourceStream($"SimpleIDE.{vResourceName}")
            
        Catch ex As Exception
            Console.WriteLine($"LoadEmbeddedResource error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ' Get embedded resource as string
    Private Function GetEmbeddedResourceString(vResourceName As String) As String
        Try
            Using lStream As Stream = LoadEmbeddedResource(vResourceName)
                If lStream IsNot Nothing Then
                    Using lReader As New StreamReader(lStream)
                        Return lReader.ReadToEnd()
                    End Using
                End If
            End Using
            
            Return ""
            
        Catch ex As Exception
            Console.WriteLine($"GetEmbeddedResourceString error: {ex.Message}")
            Return ""
        End Try
    End Function

    ' Refresh the project explorer to show current project state
    Public Sub RefreshProjectExplorer()
        Try
            ' Only refresh if we have a project explorer and current project
            If pProjectExplorer Is Nothing OrElse String.IsNullOrEmpty(pCurrentProject) Then
                Return
            End If
            
            ' Reload the project in the explorer
            Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from MainWindow.RefreshProjectExplorer")
            pProjectExplorer.LoadProjectFromManager
            
            ' Update status bar
            Dim lStatusContext As UInteger = pStatusBar.GetContextId("Main")
            pStatusBar.Pop(lStatusContext)
            pStatusBar.Push(lStatusContext, "project explorer refreshed")
            
        Catch ex As Exception
            Console.WriteLine($"RefreshProjectExplorer error: {ex.Message}")
            ShowError("Refresh error", $"Failed to Refresh project explorer: {ex.Message}")
        End Try
    End Sub
    
End Class