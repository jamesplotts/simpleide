' MainWindow.Events.vb - Event handlers for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    
    ' ===== Window Events =====
    Public Shadows Sub OnDeleteEvent(vSender As Object, vArgs As DeleteEventArgs)
        Try
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then
                vArgs.RetVal = True ' Cancel close
                Return
            End If
            
            ' Save window state
            SaveWindowState()
            
            ' Close application
            Application.Quit()
            
        Catch ex As Exception
            Console.WriteLine($"OnDeleteEvent error: {ex.Message}")
            Application.Quit()
        End Try
    End Sub

    ' Handle TODO selection from BottomPanelManager
    Private Sub OnTodoSelected(vTodo As TODOItem)
        Try
            If vTodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(vTodo.SourceFile) Then
                OpenSpecificFile(vTodo.SourceFile, vTodo.SourceLine, vTodo.SourceColumn)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnTodoSelected error: {ex.Message}")
        End Try
    End Sub
    

    
    Public Sub OnSaveAll(vSender As Object, vArgs As EventArgs)
        Try
            SaveAllFiles()
            ShowInfo("Save All", "All files have been saved.")
        Catch ex As Exception
            Console.WriteLine($"OnSaveAll error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnRecentFile(vSender As Object, vArgs As EventArgs)
        Try
            If TypeOf vSender Is MenuItem Then
                Dim lMenuItem As MenuItem = DirectCast(vSender, MenuItem)
                If lMenuItem.Child IsNot Nothing AndAlso TypeOf lMenuItem.Child Is Label Then
                    Dim lLabel As Label = DirectCast(lMenuItem.Child, Label)
                    Dim lFilePath As String = lLabel.Text
                    
                    If File.Exists(lFilePath) Then
                        If lFilePath.EndsWith(".vbproj") Then
                            LoadProjectEnhanced(lFilePath)
                        Else
                            OpenSpecificFile(lFilePath, 1, 1)
                        End If
                    Else
                        ShowError("File Not Found", $"The file '{lFilePath}' no longer exists.")
                    End If
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"OnRecentFile error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnExit(vSender As Object, vArgs As EventArgs)
        Try
            OnDeleteEvent(vSender, New DeleteEventArgs())
        Catch ex As Exception
            Console.WriteLine($"OnExit error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Edit Menu Events =====
    Public Sub OnUndo(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Undo()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnUndo error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnRedo(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Redo()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnRedo error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnCut(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Cut()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnCut error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnCopy(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Copy()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnCopy error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnPaste(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Paste()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnPaste error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnSelectAll(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.SelectAll()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnSelectAll error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnFind(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.FindReplace.vb
            ShowFindPanel()
        Catch ex As Exception
            Console.WriteLine($"OnFind error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnReplace(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.FindReplace.vb
            ShowReplacePanel()
        Catch ex As Exception
            Console.WriteLine($"OnReplace error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGoToLine(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Navigation.vb
            ShowGoToLineDialog()
        Catch ex As Exception
            Console.WriteLine($"OnGoToLine error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== View Menu Events =====
    Public Sub OnToggleProjectExplorer(vSender As Object, vArgs As EventArgs)
        Try
            pLeftPanelVisible = Not pLeftPanelVisible
            pSettingsManager.ShowProjectExplorer = pLeftPanelVisible
            UpdatePanelVisibility()
        Catch ex As Exception
            Console.WriteLine($"OnToggleProjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnToggleBottomPanel(vSender As Object, vArgs As EventArgs)
        Try
            pBottomPanelVisible = Not pBottomPanelVisible
            pSettingsManager.ShowBottomPanel = pBottomPanelVisible
            UpdatePanelVisibility()
        Catch ex As Exception
            Console.WriteLine($"OnToggleBottomPanel error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnZoomIn(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomIn()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomIn error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnZoomOut(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomOut()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomOut error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnZoomReset(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomReset()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomReset error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Build Menu Events =====
    
    
    
    Public Sub OnRunProject(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Build.vb
            Task.Run(Async Function()
                Await RunProject()
                Return Nothing
            End Function)
        Catch ex As Exception
            Console.WriteLine($"OnRunProject error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnBuildAndRun(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Build.vb
            BuildAndRun()
        Catch ex As Exception
            Console.WriteLine($"OnBuildAndRun error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnConfigureBuild(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Build.vb
            ConfigureBuild()
        Catch ex As Exception
            Console.WriteLine($"OnConfigureBuild error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Tools Menu Events =====
    Public Sub OnExternalTools(vSender As Object, vArgs As EventArgs)
        Try
            ' TODO: Implement external tools
            ShowInfo("External Tools", "External tools not yet implemented.")
        Catch ex As Exception
            Console.WriteLine($"OnExternalTools error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnTerminal(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Terminal.vb
            ShowTerminalPanel()
        Catch ex As Exception
            Console.WriteLine($"OnTerminal error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGitCommit(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Git.vb
            ShowGitCommitDialog()
        Catch ex As Exception
            Console.WriteLine($"OnGitCommit error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGitStatus(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Git.vb
            ShowGitStatus()
        Catch ex As Exception
            Console.WriteLine($"OnGitStatus error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnUpdateProjectKnowledge(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            UpdateProjectKnowledge()
        Catch ex As Exception
            Console.WriteLine($"OnUpdateProjectKnowledge error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGenerateCode(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            ShowGenerateCodeDialog()
        Catch ex As Exception
            Console.WriteLine($"OnGenerateCode error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnAISettings(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            ShowAISettings()
        Catch ex As Exception
            Console.WriteLine($"OnAISettings error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Help Menu Events =====
    Public Sub OnViewHelp(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Help.vb
            ShowHelpPanel()  ' Changed from ShowHelpBrowser()
        Catch ex As Exception
            Console.WriteLine($"OnViewHelp error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnApiDocumentation(vSender As Object, vArgs As EventArgs)
        Try
            HelpSystem.ShowHelp("vb-Reference")
        Catch ex As Exception
            Console.WriteLine($"OnApiDocumentation error: {ex.Message}")
        End Try
    End Sub
   
    Public Sub OnAbout(vSender As Object, vArgs As EventArgs)
        Try
            Dim lAboutDialog As New AboutDialog()
            
            ' FIXED: Use actual application version instead of hardcoded
            lAboutDialog.ProgramName = ApplicationVersion.Title
            lAboutDialog.Version = ApplicationVersion.VersionString  ' Shows Major.Minor.Build from actual AssemblyInfo
            lAboutDialog.Copyright = ApplicationVersion.Copyright
            lAboutDialog.Comments = StringResources.Instance.GetString(StringResources.KEY_ABOUT_TEXT)
            lAboutDialog.Website = "https://github.com/jamesplotts/simpleide"
            lAboutDialog.WebsiteLabel = "Project Website"
            lAboutDialog.License = "GPL-3.0 license"
            lAboutDialog.Authors = {"James Duane Plotts"}
            
            ' Set icon
            Try
                Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                    If lStream IsNot Nothing Then
                        lAboutDialog.Logo = New Gdk.Pixbuf(lStream)
                    End If
                End Using
            Catch ex As Exception
                Console.WriteLine($"error loading about dialog logo: {ex.Message}")
            End Try
            
            lAboutDialog.TransientFor = Me
            lAboutDialog.Run()
            lAboutDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnAbout error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnToggleOutput(vSender As Object, vArgs As EventArgs)
        Try
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ToggleOutputPanel()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnToggleOutput error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnToggleErrorList(vSender As Object, vArgs As EventArgs)
        Try
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ToggleErrorListPanel()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnToggleErrorList error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnAddExistingItem(vSender As Object, vArgs As EventArgs)
        Try
            Using lDialog As New FileChooserDialog("Add Existing Item", Me, 
                                                  FileChooserAction.Open,
                                                  "Cancel", ResponseType.Cancel,
                                                  "Add", ResponseType.Accept)
                
                ' Add file filters
                Dim lVbFilter As New FileFilter()
                lVbFilter.Name = "VB.NET Files"
                lVbFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVbFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    ' Add file to project
                    If pProjectManager IsNot Nothing Then
                        pProjectManager.AddFileToProject(lDialog.Filename)
                        ' Refresh project explorer
                        Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from MainWindow.OnAddExistingItem")
                        pProjectExplorer?.LoadProjectFromManager
                    End If
                End If
                
                lDialog.Destroy()
            End Using
        Catch ex As Exception
            Console.WriteLine($"OnAddExistingItem error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnProjectProperties(vSender As Object, vArgs As EventArgs)
        Try
            ' TODO: Implement project properties dialog
            ShowInfo("Project Properties", "Project properties dialog not yet implemented.")
        Catch ex As Exception
            Console.WriteLine($"OnProjectProperties error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnToggleFullScreen(vSender As Object, vArgs As EventArgs)
        Try
            If pIsFullScreen Then
                Unfullscreen()
                pIsFullScreen = False
            Else
                Fullscreen()
                pIsFullScreen = True
            End If
        Catch ex As Exception
            Console.WriteLine($"OnToggleFullScreen error: {ex.Message}")
        End Try
    End Sub

    
End Class