' MainWindow.Menu.vb - Menu system implementation with GTK#3 compliant menu items
Imports Gtk
Imports System
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models

Partial Public Class MainWindow
    
    ' Menu references for dynamic updates
    Private pThemeMenu As Menu
    Private pThemeRadioGroup As RadioMenuItem
    Private pProjectExplorerMenuItem As CheckMenuItem
    
    ' ===== Menu Creation =====
    Private Sub CreateMenuBar()
        Try
            pMenuBar = New MenuBar()
            
            ' File menu
            CreateFileMenu()
            
            ' Edit menu
            CreateEditMenu()
            
            ' View menu
            CreateViewMenu()
            
            ' Project menu
            CreateProjectMenu()
            
            ' Build menu
            CreateBuildMenu()
            
            ' Git menu
            CreateGitMenu()
            
            ' AI menu
            CreateAIMenu()
            
        #If DEBUG Then
            ' Debug menu (NEW - for diagnostic options)
            CreateDebugMenu()
            AddObjectExplorerDebugMenuItem
        #End If

            ' Help menu
            CreateHelpMenu()
            
        Catch ex As Exception
            Console.WriteLine($"CreateMenuBar error: {ex.Message}")
        End Try
    End Sub
    
#If DEBUG Then    
    ''' <summary>
    ''' Creates the Debug menu with diagnostic options
    ''' </summary>
    Private Sub CreateDebugMenu()
        Try
            Dim lDebugMenu As New Menu()
            Dim lDebugMenuItem As New MenuItem("_Debug")
            lDebugMenuItem.Submenu = lDebugMenu
            
            ' Diagnose Left Panel
            Dim lDiagnoseLeftPanel As New MenuItem("Diagnose Left Panel")
            AddHandler lDiagnoseLeftPanel.Activated, Sub() DiagnoseLeftPanelVisibility()
            lDebugMenu.Append(lDiagnoseLeftPanel)
            
            ' Force Show Left Panel
            Dim lForceShowLeft As New MenuItem("Force Show Left Panel")
            AddHandler lForceShowLeft.Activated, Sub() ForceShowLeftPanel()
            lDebugMenu.Append(lForceShowLeft)
            
            ' Separator
            lDebugMenu.Append(New SeparatorMenuItem())
            
'            ' Refresh Explorers
'            Dim lRefreshExplorers As New MenuItem("Refresh Explorers")
'            AddHandler lRefreshExplorers.Activated, Sub() RefreshExplorers()
'            lDebugMenu.Append(lRefreshExplorers)
'            
'            ' Fix Empty Left Panel
'            Dim lFixEmptyPanel As New MenuItem("Fix Empty Left Panel")
'            AddHandler lFixEmptyPanel.Activated, Sub() FixEmptyLeftPanel()
'            lDebugMenu.Append(lFixEmptyPanel)
            
            ' Separator
            lDebugMenu.Append(New SeparatorMenuItem())
            
            ' Ensure Notebooks Ready
            Dim lEnsureNotebooks As New MenuItem("Ensure Notebooks Ready")
            AddHandler lEnsureNotebooks.Activated, Sub() EnsureNotebooksReady()
            lDebugMenu.Append(lEnsureNotebooks)
            
            Dim lDiagnosticItem As New MenuItem("Export Syntax Tree Diagnostic")
            AddHandler lDiagnosticItem.Activated, AddressOf OnExportSyntaxTreeDiagnostic            
            lDebugMenu.Append(lDiagnosticItem)
            
             ' Add to menu bar (before Help menu)
            pMenuBar.Append(lDebugMenuItem)
            
            Console.WriteLine("Debug menu created with diagnostic options")
            
        Catch ex As Exception
            Console.WriteLine($"CreateDebugMenu error: {ex.Message}")
        End Try
    End Sub  
#End If      
    
    ' Helper method to create a menu item with icon
    Private Function CreateMenuItemWithIcon(vLabel As String, vIconName As String) As MenuItem
        Try
            Dim lMenuItem As New MenuItem()
            
            ' Create box to hold icon and label
            Dim lBox As New Box(Orientation.Horizontal, 6)
            
            ' Create icon
            Dim lIcon As New Image()
            lIcon.SetFromIconName(vIconName, IconSize.Menu)
            
            ' Create label with mnemonic support
            Dim lLabel As New Label(vLabel)
            lLabel.UseUnderline = True
            lLabel.Xalign = 0.0F
            
            ' Pack widgets
            lBox.PackStart(lIcon, False, False, 0)
            lBox.PackStart(lLabel, True, True, 0)
            
            ' Add box to menu item
            lMenuItem.Add(lBox)
            lMenuItem.ShowAll()
            
            Return lMenuItem
            
        Catch ex As Exception
            Console.WriteLine($"CreateMenuItemWithIcon error: {ex.Message}")
            Return New MenuItem(vLabel)
        End Try
    End Function
    
    Private Sub CreateFileMenu()
        Try
            Dim lFileMenu As New Menu()
            Dim lFileMenuItem As New MenuItem("_File")
            lFileMenuItem.Submenu = lFileMenu
            pMenuBar.Append(lFileMenuItem)
            
            ' New Project
            Dim lNewProject As MenuItem = CreateMenuItemWithIcon("New _Project...", "document-New")
            AddHandler lNewProject.Activated, AddressOf OnNewProject
            lFileMenu.Append(lNewProject)
            
            ' Open Project
            Dim lOpenProject As MenuItem = CreateMenuItemWithIcon("_Open project...", "document-open")
            AddHandler lOpenProject.Activated, AddressOf OnOpenProject
            lFileMenu.Append(lOpenProject)
            
            ' Recent Projects submenu
            Dim lRecentProjects As New MenuItem("_Recent Projects")
            Dim lRecentMenu As New Menu()
            lRecentProjects.Submenu = lRecentMenu
            UpdateRecentProjectsMenu(lRecentMenu)
            lFileMenu.Append(lRecentProjects)
            
            lFileMenu.Append(New SeparatorMenuItem())
            
            ' New File
            Dim lNewFile As MenuItem = CreateMenuItemWithIcon("New _File...", "document-New")
            AddHandler lNewFile.Activated, AddressOf OnNewFile
            lFileMenu.Append(lNewFile)
            
            ' Open File
            Dim lOpenFile As MenuItem = CreateMenuItemWithIcon("Open F_ile...", "document-open")
            AddHandler lOpenFile.Activated, AddressOf OnOpenFile
            lFileMenu.Append(lOpenFile)
            
            lFileMenu.Append(New SeparatorMenuItem())
            
            ' Save
            Dim lSave As MenuItem = CreateMenuItemWithIcon("_Save", "document-Save")
            AddHandler lSave.Activated, AddressOf OnSave
            lFileMenu.Append(lSave)
            
            ' Save As
            Dim lSaveAs As MenuItem = CreateMenuItemWithIcon("Save _As...", "document-Save-as")
            AddHandler lSaveAs.Activated, AddressOf OnSaveAs
            lFileMenu.Append(lSaveAs)
            
            ' Save All
            Dim lSaveAll As New MenuItem("Save A_ll")
            AddHandler lSaveAll.Activated, AddressOf OnSaveAll
            lFileMenu.Append(lSaveAll)
            
            lFileMenu.Append(New SeparatorMenuItem())
            
            ' Close File
            Dim lClose As New MenuItem("_Close")
            AddHandler lClose.Activated, AddressOf OnCloseFile
            lFileMenu.Append(lClose)
            
            ' Close All
            Dim lCloseAll As New MenuItem("Close _All")
            ' TODO: AddHandler lCloseAll.Activated, AddressOf OnCloseAll
            lFileMenu.Append(lCloseAll)
            
            lFileMenu.Append(New SeparatorMenuItem())
            
            ' Exit
            Dim lExit As MenuItem = CreateMenuItemWithIcon("E_xit", "application-exit")
            AddHandler lExit.Activated, AddressOf OnQuit
            lFileMenu.Append(lExit)
            
        Catch ex As Exception
            Console.WriteLine($"CreateFileMenu error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CreateEditMenu()
        Try
            Dim lEditMenu As New Menu()
            Dim lEditMenuItem As New MenuItem("_Edit")
            lEditMenuItem.Submenu = lEditMenu
            pMenuBar.Append(lEditMenuItem)
            
            ' Undo
            Dim lUndo As MenuItem = CreateMenuItemWithIcon("_Undo", "edit-Undo")
            AddHandler lUndo.Activated, AddressOf OnUndo
            lEditMenu.Append(lUndo)
            
            ' Redo
            Dim lRedo As MenuItem = CreateMenuItemWithIcon("_Redo", "edit-Redo")
            AddHandler lRedo.Activated, AddressOf OnRedo
            lEditMenu.Append(lRedo)
            
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Cut
            Dim lCut As MenuItem = CreateMenuItemWithIcon("Cu_t", "edit-Cut")
            AddHandler lCut.Activated, AddressOf OnCut
            lEditMenu.Append(lCut)
            
            ' Copy
            Dim lCopy As MenuItem = CreateMenuItemWithIcon("_Copy", "edit-Copy")
            AddHandler lCopy.Activated, AddressOf OnCopy
            lEditMenu.Append(lCopy)
            
            ' Paste
            Dim lPaste As MenuItem = CreateMenuItemWithIcon("_Paste", "edit-Paste")
            AddHandler lPaste.Activated, AddressOf OnPaste
            lEditMenu.Append(lPaste)
            
           
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Select All
            Dim lSelectAll As New MenuItem("Select _All")
            AddHandler lSelectAll.Activated, AddressOf OnSelectAll
            lEditMenu.Append(lSelectAll)
            
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Find
            Dim lFind As MenuItem = CreateMenuItemWithIcon("_Find...", "edit-Find")
            AddHandler lFind.Activated, AddressOf ShowFindPanel
            lEditMenu.Append(lFind)
            
            ' Replace
            Dim lReplace As MenuItem = CreateMenuItemWithIcon("R_eplace...", "edit-Find-Replace")
            ' TODO: AddHandler lReplace.Activated, AddressOf OnReplace
            lEditMenu.Append(lReplace)
            
           
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Go to Line
            Dim lGoToLine As New MenuItem("_Go to Line...")
            ' TODO: AddHandler lGoToLine.Activated, AddressOf OnGoToLine
            lEditMenu.Append(lGoToLine)
            
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Indent
            Dim lIndent As New MenuItem("_Indent")
            AddHandler lIndent.Activated, AddressOf OnIndent
            lEditMenu.Append(lIndent)
            
            ' Outdent
            Dim lOutdent As New MenuItem("_Outdent")
            AddHandler lOutdent.Activated, AddressOf OnOutdent
            lEditMenu.Append(lOutdent)
            
            ' Format Document
            Dim lFormatDoc As New MenuItem("Format _Document")
            ' TODO: AddHandler lFormatDoc.Activated, AddressOf OnFormatDocument
            lEditMenu.Append(lFormatDoc)
            
            lEditMenu.Append(New SeparatorMenuItem())
            
            ' Preferences
            Dim lPreferences As MenuItem = CreateMenuItemWithIcon("Pre_ferences...", "preferences-system")
            AddHandler lPreferences.Activated, AddressOf OnEditPreferences
            lEditMenu.Append(lPreferences)
            
        Catch ex As Exception
            Console.WriteLine($"CreateEditMenu error: {ex.Message}")
        End Try
    End Sub

    Private Sub UpdateMenuStates()
        ' TODO: Implement UpdateMenuStates
    End Sub
    
    Private Sub CreateViewMenu()
        Try
            Dim lViewMenu As New Menu()
            Dim lViewMenuItem As New MenuItem("_View")
            lViewMenuItem.Submenu = lViewMenu
            pMenuBar.Append(lViewMenuItem)
            
            ' Project Explorer
            Dim lProjectExp As New CheckMenuItem("_Project Explorer")
            lProjectExp.Active = True
            AddHandler lProjectExp.Toggled, AddressOf OnToggleProjectExplorer
            lViewMenu.Append(lProjectExp)
            
            ' Output
            Dim lOutput As New CheckMenuItem("_Output")
            AddHandler lOutput.Toggled, AddressOf OnToggleOutput
            lViewMenu.Append(lOutput)
            
            ' Error List
            Dim lErrorList As New CheckMenuItem("_Error List")
            AddHandler lErrorList.Toggled, AddressOf OnToggleErrorList
            lViewMenu.Append(lErrorList)
            
            ' TODO List
            Dim lTodoList As New CheckMenuItem("_TODO List")
            ' TODO: AddHandler lTodoList.Toggled, AddressOf ToggleTodoList
            lViewMenu.Append(lTodoList)
            
            ' AI Assistant
            Dim lAIAssistant As New CheckMenuItem("_AI Assistant")
            ' TODO: AddHandler lAIAssistant.Toggled, AddressOf OnToggleAIAssistant
            lViewMenu.Append(lAIAssistant)
    
            AddScratchpadMenuItem(lViewMenu)
    
    
            lViewMenu.Append(New SeparatorMenuItem())
            
            ' Toolbar submenu
            Dim lToolbar As New MenuItem("_Toolbar")
            Dim lToolbarMenu As New Menu()
            lToolbar.Submenu = lToolbarMenu
            lViewMenu.Append(lToolbar)
            
            ' Show/Hide Toolbar
            Dim lShowToolbar As New CheckMenuItem("_Show Toolbar")
            lShowToolbar.Active = pSettingsManager.ShowToolbar
            AddHandler lShowToolbar.Toggled, AddressOf OnToggleToolbar
            lToolbarMenu.Append(lShowToolbar)
            
            lToolbarMenu.Append(New SeparatorMenuItem())
            
            ' Show Labels
            Dim lShowLabels As New CheckMenuItem("Show _Labels")
            lShowLabels.Active = pSettingsManager.ToolbarShowLabels
            AddHandler lShowLabels.Toggled, AddressOf OnToggleToolbarLabels
            lToolbarMenu.Append(lShowLabels)
            
            ' Button Size submenu
            Dim lButtonSize As New MenuItem("Button _Size")
            Dim lButtonSizeMenu As New Menu()
            lButtonSize.Submenu = lButtonSizeMenu
            lToolbarMenu.Append(lButtonSize)
            
            ' Large buttons radio
            Dim lLargeButtons As New RadioMenuItem("_Large")
            lLargeButtons.Active = pSettingsManager.ToolbarLargeIcons
            AddHandler lLargeButtons.Toggled, AddressOf OnToolbarLargeButtons
            lButtonSizeMenu.Append(lLargeButtons)
            
            ' Small buttons radio (use same group as large)
            Dim lSmallButtons As New RadioMenuItem(lLargeButtons, "_Small")
            lSmallButtons.Active = Not pSettingsManager.ToolbarLargeIcons
            AddHandler lSmallButtons.Toggled, AddressOf OnToolbarSmallButtons
            lButtonSizeMenu.Append(lSmallButtons)
            
            lViewMenu.Append(New SeparatorMenuItem())
            
            ' Full Screen
            Dim lFullScreen As New CheckMenuItem("_Full Screen")
            AddHandler lFullScreen.Toggled, AddressOf OnToggleFullScreen
            lViewMenu.Append(lFullScreen)
            
            lViewMenu.Append(New SeparatorMenuItem())
            
            ' Theme submenu
            Dim lTheme As New MenuItem("T_hemes")
            pThemeMenu = New Menu()
            lTheme.Submenu = pThemeMenu
            UpdateThemeMenu()
            lViewMenu.Append(lTheme)
            
            lViewMenu.Append(New SeparatorMenuItem())
            
            ' Zoom In
            Dim lZoomIn As MenuItem = CreateMenuItemWithIcon("Zoom _In", "zoom-in")
            ' TODO: AddHandler lZoomIn.Activated, AddressOf OnZoomIn
            lViewMenu.Append(lZoomIn)
            
            ' Zoom Out
            Dim lZoomOut As MenuItem = CreateMenuItemWithIcon("Zoom _Out", "zoom-out")
            ' TODO: AddHandler lZoomOut.Activated, AddressOf OnZoomOut
            lViewMenu.Append(lZoomOut)
            
            ' Reset Zoom
            Dim lZoomReset As New MenuItem("_Reset Zoom")
            ' TODO: AddHandler lZoomReset.Activated, AddressOf OnZoomReset
            lViewMenu.Append(lZoomReset)
            
        Catch ex As Exception
            Console.WriteLine($"CreateViewMenu error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CreateProjectMenu()
        Try
            Dim lProjectMenu As New Menu()
            Dim lProjectMenuItem As New MenuItem("_Project")
            lProjectMenuItem.Submenu = lProjectMenu
            pMenuBar.Append(lProjectMenuItem)
            
            ' Add Reference
            Dim lAddReference As New MenuItem("Add _Reference...")
            AddHandler lAddReference.Activated, Sub() OnManageReferences(Nothing, Nothing, 0)
            lProjectMenu.Append(lAddReference)
            
            ' Add Existing Item
            Dim lAddExisting As New MenuItem("Add _Existing Item...")
            AddHandler lAddExisting.Activated, AddressOf OnAddExistingItem
            lProjectMenu.Append(lAddExisting)
            
            ' Add New Item
            Dim lAddNew As New MenuItem("Add _New Item...")
            AddHandler lAddNew.Activated, AddressOf OnAddNewItem
            lProjectMenu.Append(lAddNew)
            
            lProjectMenu.Append(New SeparatorMenuItem())
            
            ' Project Properties
            Dim lProperties As New MenuItem("_Properties...")
            AddHandler lProperties.Activated, AddressOf OnProjectProperties
            lProjectMenu.Append(lProperties)
            
        Catch ex As Exception
            Console.WriteLine($"CreateProjectMenu error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Creates the Build menu with proper F5/F6 accelerator labels
    ''' </summary>
    Private Sub CreateBuildMenu()
        Try
            Dim lBuildMenu As New Menu()
            Dim lBuildMenuItem As New MenuItem("_Build")
            lBuildMenuItem.Submenu = lBuildMenu
            pMenuBar.Append(lBuildMenuItem)
            
            ' Build Project (F6)
            Dim lBuild As MenuItem = CreateMenuItemWithIcon("_Build Project", "system-run")
            lBuild.UseUnderline = True
            ' Add F6 label to show the shortcut
            lBuild.Label = "_Build Project" & vbTab & "F6"
            AddHandler lBuild.Activated, AddressOf OnBuildProject
            lBuildMenu.Append(lBuild)
            
            ' Build and Run (F5)
            Dim lBuildRun As MenuItem = CreateMenuItemWithIcon("Build and _Run", "media-playback-start")
            lBuildRun.UseUnderline = True
            ' Add F5 label to show the shortcut
            lBuildRun.Label = "Build and _Run" & vbTab & "F5"
            AddHandler lBuildRun.Activated, AddressOf OnBuildAndRun
            lBuildMenu.Append(lBuildRun)
            
            ' Rebuild Project
            Dim lRebuild As New MenuItem("_Rebuild Project")
            AddHandler lRebuild.Activated, AddressOf OnRebuildProject
            lBuildMenu.Append(lRebuild)
            
            ' Clean Project
            Dim lClean As New MenuItem("_Clean Project")
            AddHandler lClean.Activated, AddressOf OnCleanProject
            lBuildMenu.Append(lClean)
            
            lBuildMenu.Append(New SeparatorMenuItem())
            
            ' Run Without Building (Ctrl+F5)
            Dim lRun As MenuItem = CreateMenuItemWithIcon("R_un Without Building", "media-playback-start")
            lRun.UseUnderline = True
            lRun.Label = "R_un Without Building" & vbTab & "Ctrl+F5"
            AddHandler lRun.Activated, AddressOf OnRunWithoutBuilding
            lBuildMenu.Append(lRun)
            
            ' Stop (Shift+F5)
            Dim lStop As MenuItem = CreateMenuItemWithIcon("_Stop", "process-stop")
            lStop.UseUnderline = True
            lStop.Label = "_Stop" & vbTab & "Shift+F5"
            AddHandler lStop.Activated, AddressOf OnStopDebugging
            lBuildMenu.Append(lStop)
            
            lBuildMenu.Append(New SeparatorMenuItem())
            
            ' Configuration submenu
            Dim lConfig As New MenuItem("C_onfiguration")
            Dim lConfigMenu As New Menu()
            lConfig.Submenu = lConfigMenu
            
            Dim lDebugConfig As New RadioMenuItem("_Debug")
            lDebugConfig.Active = True
            AddHandler lDebugConfig.Toggled, Sub() 
                If lDebugConfig.Active Then 
                    OnConfigurationChanged("Debug")
                End If
            End Sub
            lConfigMenu.Append(lDebugConfig)
            
            Dim lReleaseConfig As New RadioMenuItem(lDebugConfig.Group, "_Release")
            AddHandler lReleaseConfig.Toggled, Sub() 
                If lReleaseConfig.Active Then 
                    OnConfigurationChanged("Release")
                End If
            End Sub
            lConfigMenu.Append(lReleaseConfig)
            
            lBuildMenu.Append(lConfig)
            
            ' Build Configuration Dialog
            lBuildMenu.Append(New SeparatorMenuItem())
            Dim lBuildConfig As New MenuItem("Build _Settings...")
            AddHandler lBuildConfig.Activated, AddressOf OnBuildConfiguration
            lBuildMenu.Append(lBuildConfig)
            
        Catch ex As Exception
            Console.WriteLine($"CreateBuildMenu error: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.OnBuildConfiguration
    ''' <summary>
    ''' Shows the build configuration dialog
    ''' </summary>
    Private Sub OnBuildConfiguration(vSender As Object, vArgs As EventArgs)
        ConfigureBuild()
    End Sub
    
    ' Add: SimpleIDE.MainWindow.OnRunWithoutBuilding  
    ''' <summary>
    ''' Runs the project without building (Ctrl+F5)
    ''' </summary>
    Private Sub OnRunWithoutBuilding(vSender As Object, vArgs As EventArgs)
        Task.Run(Async Function()
            Await RunProject()
            Return Nothing
        End Function)
    End Sub
    
    
    ''' <summary>
    ''' Handles build configuration change (Debug/Release)
    ''' </summary>
    Private Sub OnConfigurationChanged(vConfiguration As String)
        Try
            If pBuildConfiguration IsNot Nothing Then
                pBuildConfiguration.Configuration = vConfiguration
                SaveBuildConfiguration()
                UpdateStatusBar($"Build configuration: {vConfiguration}")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnConfigurationChanged error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CreateGitMenu()
        Try
            Dim lGitMenu As New Menu()
            Dim lGitMenuItem As New MenuItem("_Git")
            lGitMenuItem.Submenu = lGitMenu
            pMenuBar.Append(lGitMenuItem)
            
            ' Initialize Repository
            Dim lInitRepo As New MenuItem("_Initialize Repository")
            ' TODO: AddHandler lInitRepo.Activated, AddressOf OnGitInitialize
            lGitMenu.Append(lInitRepo)
            
            lGitMenu.Append(New SeparatorMenuItem())
            
            ' Changes
            Dim lChanges As New MenuItem("View _Changes")
            ' TODO: AddHandler lChanges.Activated, AddressOf OnGitChanges
            lGitMenu.Append(lChanges)
            
            ' Commit
            Dim lCommit As New MenuItem("_Commit...")
            AddHandler lCommit.Activated, AddressOf OnGitCommit
            lGitMenu.Append(lCommit)
            
            ' Push
            Dim lPush As New MenuItem("_Push")
            AddHandler lPush.Activated, AddressOf OnGitPush
            lGitMenu.Append(lPush)
            
            ' Pull
            Dim lPull As New MenuItem("P_ull")
            AddHandler lPull.Activated, AddressOf OnGitPull
            lGitMenu.Append(lPull)
            
            lGitMenu.Append(New SeparatorMenuItem())
            
            ' Branch
            Dim lBranch As New MenuItem("_Branch")
            AddHandler lBranch.Activated, AddressOf ShowGitBranchDialog
            lGitMenu.Append(lBranch)
            
            ' History
            Dim lHistory As New MenuItem("_History")
            AddHandler lHistory.Activated, AddressOf ShowGitHistory
            lGitMenu.Append(lHistory)
            
            lGitMenu.Append(New SeparatorMenuItem())
            
            ' Settings
            Dim lSettings As New MenuItem("_Settings...")
            AddHandler lSettings.Activated, AddressOf ShowGitSettings
            lGitMenu.Append(lSettings)
            
        Catch ex As Exception
            Console.WriteLine($"CreateGitMenu error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CreateAIMenu()
        Try
            Dim lAIMenu As New Menu()
            Dim lAIMenuItem As New MenuItem("A_I")
            lAIMenuItem.Submenu = lAIMenu
            pMenuBar.Append(lAIMenuItem)
            
            ' Update Project Knowledge
            Dim lUpdateKnowledge As New MenuItem("_Update project Knowledge")
            ' TODO: AddHandler lUpdateKnowledge.Activated, AddressOf OnUpdateProjectKnowledge
            lAIMenu.Append(lUpdateKnowledge)
            
            lAIMenu.Append(New SeparatorMenuItem())
            
            ' Ask AI Assistant
            Dim lAskAI As New MenuItem("_Ask AI Assistant...")
            ' TODO: AddHandler lAskAI.Activated, AddressOf OnAskAIAssistant
            lAIMenu.Append(lAskAI)
            
            ' Explain Code
            Dim lExplainCode As New MenuItem("_Explain Selected code")
            ' TODO: AddHandler lExplainCode.Activated, AddressOf OnExplainCode
            lAIMenu.Append(lExplainCode)
            
            ' Fix Build Errors
            Dim lFixErrors As New MenuItem("_Fix Build Errors")
            ' TODO: AddHandler lFixErrors.Activated, AddressOf OnFixBuildErrors
            lAIMenu.Append(lFixErrors)
            
            ' Generate Code
            Dim lGenerateCode As New MenuItem("_Generate code...")
            ' TODO: AddHandler lGenerateCode.Activated, AddressOf OnGenerateCode
            lAIMenu.Append(lGenerateCode)
            
            lAIMenu.Append(New SeparatorMenuItem())
            
            ' AI Settings
            Dim lAISettings As New MenuItem("AI _Settings...")
            AddHandler lAISettings.Activated, AddressOf OnAISettings
            lAIMenu.Append(lAISettings)
            
        Catch ex As Exception
            Console.WriteLine($"CreateAIMenu error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CreateHelpMenu()
        Try
            Dim lHelpMenu As New Menu()
            Dim lHelpMenuItem As New MenuItem("_Help")
            lHelpMenuItem.Submenu = lHelpMenu
            pMenuBar.Append(lHelpMenuItem)
            
            ' View Help
            Dim lViewHelp As MenuItem = CreateMenuItemWithIcon("_View Help", "help-browser")
            ' TODO: AddHandler lViewHelp.Activated, AddressOf OnViewHelp
            lHelpMenu.Append(lViewHelp)
            
            ' API Documentation
            Dim lApiDocs As New MenuItem("_API documentation")
            ' TODO: AddHandler lApiDocs.Activated, AddressOf OnApiDocumentation
            lHelpMenu.Append(lApiDocs)
            
            lHelpMenu.Append(New SeparatorMenuItem())
            
            ' GTK# Help submenu
            Dim lGtkHelp As New MenuItem("GTK# _Help")
            Dim lGtkHelpMenu As New Menu()
            lGtkHelp.Submenu = lGtkHelpMenu
            PopulateGtkHelpMenu(lGtkHelpMenu)
            lHelpMenu.Append(lGtkHelp)
            
            ' .NET Help submenu
            Dim lDotNetHelp As New MenuItem(".NET _Documentation")
            Dim lDotNetHelpMenu As New Menu()
            lDotNetHelp.Submenu = lDotNetHelpMenu
            PopulateDotNetHelpMenu(lDotNetHelpMenu)
            lHelpMenu.Append(lDotNetHelp)
            
            lHelpMenu.Append(New SeparatorMenuItem())
            
            ' About
            Dim lAbout As MenuItem = CreateMenuItemWithIcon("_About SimpleIDE", "help-about")
            AddHandler lAbout.Activated, AddressOf OnAbout
            lHelpMenu.Append(lAbout)
            
        Catch ex As Exception
            Console.WriteLine($"CreateHelpMenu error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    Private Sub UpdateRecentProjectsMenu(vMenu As Menu)
        Try
            ' Clear existing items
            for each lChild in vMenu.Children
                vMenu.Remove(lChild)
            Next
            
            ' Get recent projects from settings
            Dim lRecentProjects As List(Of String) = pSettingsManager.GetRecentProjects()
            
            If lRecentProjects.Count = 0 Then
                Dim lNoRecent As New MenuItem("(No recent projects)")
                lNoRecent.Sensitive = False
                vMenu.Append(lNoRecent)
            Else
                for i As Integer = 0 To lRecentProjects.Count - 1
                    Dim lProject As String = lRecentProjects(i)
                    Dim lMenuItem As New MenuItem($"{i + 1}. {System.IO.Path.GetFileName(lProject)}")
                    lMenuItem.TooltipText = lProject
                    AddHandler lMenuItem.Activated, Sub() LoadProjectEnhanced(lProject)
                    vMenu.Append(lMenuItem)
                Next
            End If
            
            vMenu.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"UpdateRecentProjectsMenu error: {ex.Message}")
        End Try
    End Sub
    
    ' Update theme menu with all available themes
    Private Sub UpdateThemeMenu()
        Try
            If pThemeMenu Is Nothing Then Return
            
            ' Clear existing items
            for each lChild in pThemeMenu.Children
                pThemeMenu.Remove(lChild)
            Next
            
            ' Get current theme
            Dim lCurrentTheme As String = pThemeManager.GetCurrentTheme()
            
            ' Add all available themes as radio menu items
            pThemeRadioGroup = Nothing
            for each lThemeName in pThemeManager.GetAvailableThemes()
                Dim lThemeItem As RadioMenuItem
                
                If pThemeRadioGroup Is Nothing Then
                    lThemeItem = New RadioMenuItem(lThemeName)
                    pThemeRadioGroup = lThemeItem
                Else
                    lThemeItem = New RadioMenuItem(pThemeRadioGroup.Group, lThemeName)
                End If
                
                ' Set active if current theme
                If lThemeName = lCurrentTheme Then
                    lThemeItem.Active = True
                End If
                
                ' Add handler - capture theme name in closure
                Dim lCapturedThemeName As String = lThemeName
                AddHandler lThemeItem.Toggled, Sub()
                    If lThemeItem.Active Then
                        ApplyTheme(lCapturedThemeName)
                    End If
                End Sub
                
                pThemeMenu.Append(lThemeItem)
            Next
            
            pThemeMenu.Append(New SeparatorMenuItem())
            
            ' Theme Editor menu item
            Dim lThemeEditor As New MenuItem("Theme _Editor...")
            AddHandler lThemeEditor.Activated, AddressOf OnThemeEditor
            pThemeMenu.Append(lThemeEditor)
            
            pThemeMenu.ShowAll()
            
            ' Subscribe to theme list changes
            AddHandler pThemeManager.ThemeListChanged, AddressOf OnThemeListChanged
            
        Catch ex As Exception
            Console.WriteLine($"UpdateThemeMenu error: {ex.Message}")
        End Try
    End Sub
    
    ' Handler for theme list changes
    Private Sub OnThemeListChanged()
        Try
            ' Update the theme menu when themes are added/removed
            UpdateThemeMenu()
        Catch ex As Exception
            Console.WriteLine($"OnThemeListChanged error: {ex.Message}")
        End Try
    End Sub
    
    ' Open theme editor
    Private Sub OnThemeEditor(vSender As Object, vArgs As EventArgs)
        Try
            ' Show theme editor in a new tab
            ShowThemeEditor()
        Catch ex As Exception
            Console.WriteLine($"OnThemeEditor error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub PopulateGtkHelpMenu(vMenu As Menu)
        Try
            Dim lLinks As New Dictionary(Of String, String) From {
                {"GTK# documentation", "https://docs.gtk.org/gtk3/"},
                {"Widget Gallery", "https://docs.gtk.org/gtk3/gallery.html"},
                {"GTK# on GitHub", "https://github.com/GtkSharp/GtkSharp"},
                {"GTK# API Reference", "https://docs.gtk.org/gtk3/"}
            }
            
            for each kvp in lLinks
                Dim lMenuItem As New MenuItem(kvp.key)
                Dim lUrl As String = kvp.Value
                AddHandler lMenuItem.Activated, Sub() OpenUrl(lUrl)
                vMenu.Append(lMenuItem)
            Next
            
        Catch ex As Exception
            Console.WriteLine($"PopulateGtkHelpMenu error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub PopulateDotNetHelpMenu(vMenu As Menu)
        Try
            Dim lLinks As New Dictionary(Of String, String) From {
                {"VB.NET documentation", "https://docs.microsoft.com/en-us/dotnet/Visual-basic/"},
                {".NET API Browser", "https://docs.microsoft.com/en-us/dotnet/api/"},
                {"VB.NET Language Reference", "https://docs.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/"},
                {"VB.NET Programming Guide", "https://docs.microsoft.com/en-us/dotnet/Visual-basic/programming-guide/"}
            }
            
            for each kvp in lLinks
                Dim lMenuItem As New MenuItem(kvp.key)
                Dim lUrl As String = kvp.Value
                AddHandler lMenuItem.Activated, Sub() OpenUrl(lUrl)
                vMenu.Append(lMenuItem)
            Next
            
        Catch ex As Exception
            Console.WriteLine($"PopulateDotNetHelpMenu error: {ex.Message}")
        End Try
    End Sub
    
    ' Apply theme by name (overload for menu usage)
    Private Sub ApplyTheme(vThemeName As String)
        Try
            UpdateStatusBar("Applying Theme " + vThemeName)
            pThemeManager.SetTheme(vThemeName)
            
            ' Update all open editors
            for each lTabInfo in pOpenTabs.Values
                If lTabInfo.Editor IsNot Nothing Then
                    Dim lEditor As CustomDrawingEditor = TryCast(lTabInfo.Editor, CustomDrawingEditor)
                    If lEditor IsNot Nothing Then
                        lEditor.ApplyTheme()
                    End If
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"ApplyTheme(String) error: {ex.Message}")
        End Try
    End Sub

#If DEBUG Then
    ' Add: SimpleIDE.MainWindow.AddObjectExplorerDebugMenuItem
    ' To: MainWindow.MenuBar.vb
    ''' <summary>
    ''' Adds a debug menu item for Object Explorer diagnostics
    ''' </summary>
    Private Sub AddObjectExplorerDebugMenuItem()
        Try
            ' Find or create Debug menu
            Dim lDebugMenu As Menu = Nothing
            Dim lMenuBar As MenuBar = pMenuBar
            
            ' Look for existing Debug menu
            for each lItem in lMenuBar.Children
                If TypeOf lItem Is MenuItem Then
                    Dim lMenuItem As MenuItem = DirectCast(lItem, MenuItem)
                    If lMenuItem.Label = "_Debug" Then
                        lDebugMenu = DirectCast(lMenuItem.Submenu, Menu)
                        Exit for
                    End If
                End If
            Next
            
            ' Create Debug menu if it doesn't exist
            If lDebugMenu Is Nothing Then
                Dim lDebugMenuItem As New MenuItem("_Debug")
                lDebugMenu = New Menu()
                lDebugMenuItem.Submenu = lDebugMenu
                lMenuBar.Add(lDebugMenuItem)
            End If
            
            ' Add separator if menu has items
            If lDebugMenu.Children.Length > 0 Then
                lDebugMenu.Add(New SeparatorMenuItem())
            End If
            
            ' Add Object Explorer debug items
            Dim lOEDebugItem As New MenuItem("Object Explorer - Debug Parser Output")
            AddHandler lOEDebugItem.Activated, Sub()
                Try
                    If pObjectExplorer IsNot Nothing Then
                        If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                            Dim lExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                            lExplorer.DebugProjectParserOutput()
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"Debug Parser Output error: {ex.Message}")
                End Try
            End Sub
            lDebugMenu.Add(lOEDebugItem)
            
            Dim lOETreeDebugItem As New MenuItem("Object Explorer - Debug Visual Tree")
            AddHandler lOETreeDebugItem.Activated, Sub()
                Try
                    If pObjectExplorer IsNot Nothing Then
                        If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                            Dim lExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                            lExplorer.DiagnoseTreeViewStatus()
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"Debug Visual Tree error: {ex.Message}")
                End Try
            End Sub
            lDebugMenu.Add(lOETreeDebugItem)
            
            Dim lOERebuildItem As New MenuItem("Object Explorer - Force Rebuild")
            AddHandler lOERebuildItem.Activated, Sub()
                Try
                    If pObjectExplorer IsNot Nothing Then
                        If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                            Dim lExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                            lExplorer.RebuildVisualTree()
                            lExplorer.ForceCompleteRefresh()
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"Force Rebuild error: {ex.Message}")
                End Try
            End Sub
            lDebugMenu.Add(lOERebuildItem)
            
            Dim lOEExpandNamespacesItem As New MenuItem("Object Explorer - Force Expand Namespaces")
            AddHandler lOEExpandNamespacesItem.Activated, Sub()
                Try
                    If pObjectExplorer IsNot Nothing Then
                        If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                            Dim lExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                            lExplorer.ForceExpandNamespaces()
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"Force Expand Namespaces error: {ex.Message}")
                End Try
            End Sub
            lDebugMenu.Add(lOEExpandNamespacesItem)

            Dim lOEDebugBuildItem As New MenuItem("Object Explorer - Debug Build Process")
            AddHandler lOEDebugBuildItem.Activated, Sub()
                Try
                    If pObjectExplorer IsNot Nothing Then
                        If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                            Dim lExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                            lExplorer.DebugBuildVisualNodes()
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"Debug Build Process error: {ex.Message}")
                End Try
            End Sub
            lDebugMenu.Add(lOEDebugBuildItem)

            ' Show all menu items
            lMenuBar.ShowAll()
            
            Console.WriteLine("Object Explorer debug menu items added")
            
        Catch ex As Exception
            Console.WriteLine($"AddObjectExplorerDebugMenuItem error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the Revert to Saved menu action
    ''' </summary>
    Private Sub OnRevertToSaved(vSender As Object, vArgs As EventArgs)
        Try
            ' Get current tab
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then
                ShowInfo("Revert to Saved", "No file is currently open.")
                Return
            End If
            
            ' Check if file has a saved version
            If String.IsNullOrEmpty(lCurrentTab.FilePath) OrElse lCurrentTab.FilePath.StartsWith("Untitled") Then
                ShowInfo("Revert to Saved", "This file has never been saved.")
                Return
            End If
            
            ' Check if file exists on disk
            If Not System.IO.File.Exists(lCurrentTab.FilePath) Then
                ShowError("Revert to Saved", "The file no longer exists on disk.")
                Return
            End If
            
            ' Check if there are actually changes to revert
            If Not lCurrentTab.Modified Then
                ShowInfo("Revert to Saved", "No changes to revert.")
                Return
            End If
            
            ' Confirm with user
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Warning,
                ButtonsType.None,
                $"Are you sure you want to revert '{System.IO.Path.GetFileName(lCurrentTab.FilePath)}' to the last saved version?{Environment.NewLine}{Environment.NewLine}All unsaved changes will be lost."
            )
            
            lDialog.AddButton("Cancel", ResponseType.Cancel)
            lDialog.AddButton("Revert", ResponseType.Yes)
            
            Dim lResponse As Integer = lDialog.Run()
            lDialog.Destroy()
            
            If lResponse <> CInt(ResponseType.Yes) Then
                Return
            End If
            
            ' Get SourceFileInfo and reload it
            If pProjectManager IsNot Nothing Then
                Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(lCurrentTab.FilePath)
                If lSourceFileInfo IsNot Nothing Then
                    Console.WriteLine($"Reverting {lCurrentTab.FilePath} To saved version")
                    
                    ' Use ReloadFile to reload from disk
                    If lSourceFileInfo.LoadContent() Then
                        ' Update editor with reloaded content
                        If TypeOf lCurrentTab.Editor Is CustomDrawingEditor Then
                            
                            ' Update tab label to remove the asterisk
                            UpdateTabLabel(lCurrentTab)
                            
                            ' Update status bar
                            UpdateStatusBar($"Reverted: {System.IO.Path.GetFileName(lCurrentTab.FilePath)}")
                            
                            ' Request re-parsing if needed
                            If lSourceFileInfo.NeedsParsing Then
                                pProjectManager.ParseFile(lSourceFileInfo)
                            End If
                        End If
                    Else
                        ShowError("Revert Failed", "Failed To reload the file from disk.")
                    End If
                Else
                    ShowError("Revert Failed", "Could Not find the file information.")
                End If
            Else
                ShowError("Revert Failed", "Project manager Is Not available.")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnRevertToSaved error: {ex.Message}")
            ShowError("Revert error", $"An error occurred While reverting: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Adds the Revert to Saved menu item to the File menu
    ''' </summary>
    ''' <param name="vFileMenu">The File menu to add the item to</param>
    ''' <remarks>
    ''' Call this in CreateFileMenu after the Save All menu item
    ''' </remarks>
    Private Sub AddRevertToSavedMenuItem(vFileMenu As Menu)
        Try
            ' Add separator if needed
            vFileMenu.Append(New SeparatorMenuItem())
            
            ' Create Revert to Saved menu item
            Dim lRevertToSaved As MenuItem = CreateMenuItemWithIcon("_Revert To Saved", "document-revert")
            AddHandler lRevertToSaved.Activated, AddressOf OnRevertToSaved
            vFileMenu.Append(lRevertToSaved)
            
        Catch ex As Exception
            Console.WriteLine($"AddRevertToSavedMenuItem error: {ex.Message}")
        End Try
    End Sub
#End If

    
End Class
