' MainWindow.vb - Main window implementation with BottomPanelManager
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

' Main window class - uses partial classes to organize functionality
Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Constants =====
    Private Const WINDOW_TITLE As String = "SimpleIDE"
    Private Const LEFT_PANEL_WIDTH As Integer = 250
    Private Const BOTTOM_PANEL_HEIGHT As Integer = 200
    
    ' ===== Private Fields =====
    Private pSettingsManager As SettingsManager
    Private pThemeManager As ThemeManager
    Private pIntelliSenseEngine As IntelliSenseEngine
    Private pFileSystemWatcher As Utilities.FileSystemWatcher
    Private pMemoryManifest As MemoryManifest
    Private pProjectManager As ProjectManager
    Private pEditorFactory As EditorFactory
    
    ' UI Components
    Private pMainVBox As Box
    Private pMenuBar As MenuBar
    Private pToolbar As Toolbar
    Private pMainHPaned As Paned
    Private pCenterVPaned As Paned
    Private pProjectExplorer As ProjectExplorer
    Private pNotebook As Notebook
    Private pStatusBar As Statusbar
    
    ' Bottom panel manager
    Private pBottomPanelManager As BottomPanelManager
    
    ' State
    Private pCurrentProject As String = ""
    Private pOpenTabs As New Dictionary(Of String, TabInfo)()
    Private pLeftPanelVisible As Boolean = True
    Private pBottomPanelVisible As Boolean = False
    Private pIsBuilding As Boolean = False
    Private pIsFullScreen As Boolean = False
    
    ' Events
    Public Event ProjectChanged(vProjectPath As String)
    Public Event FileOpened(vFilePath As String)
    Public Event FileClosed(vFilePath As String)
    Public Event FileSaved(vFilePath As String)
    
    ' ===== Constructor =====
    
    Public Sub New()
        MyBase.New(WINDOW_TITLE)
        
        Try
            ' Initialize settings
            pSettingsManager = New SettingsManager()
            pThemeManager = New ThemeManager(pSettingsManager)
            pMemoryManifest = New MemoryManifest(pSettingsManager)
            AddHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsChanged
            InitializeScratchpad()
            
            ' Initialize project manager
            pProjectManager = New ProjectManager()
            AddHandler pProjectManager.ProjectLoaded, AddressOf OnProjectManagerProjectLoaded
            AddHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
            AddHandler pProjectManager.ProjectModified, AddressOf OnProjectManagerProjectModified
            AddHandler pProjectManager.FileAdded, AddressOf OnProjectManagerFileAdded
            AddHandler pProjectManager.FileRemoved, AddressOf OnProjectManagerFileRemoved
            AddHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
            
            ' Setup window
            SetupWindow()
            
            ' Build UI
            BuildUI()
            AddHandler Me.KeyPressEvent, AddressOf OnWindowKeyPress

        ' After creating pObjectExplorer and pProjectManager
        CompleteObjectExplorerSetup()
            
            ' Initialize IntelliSense engine
            pIntelliSenseEngine = New IntelliSenseEngine()
    
            ' Initialize build system
            InitializeBuildSystem()
            
            ' Apply theme
            ApplyTheme()
            
            ' Apply settings
            ApplySettings()
            
            ' DO NOT call RestoreWindowSize here - Program.vb will handle it after ShowAll()
            ' DO NOT call ShowAll here - let Program.vb handle it
            
            ' Initialize panel visibility
            UpdatePanelVisibility()
            
            ' Setup file system watcher
            SetupFileSystemWatcher()

            pEditorFactory = New EditorFactory()
            EditorFactory.Initialize(pSyntaxColorSet, pSettingsManager, pProjectManager)

            InitializeWithObjectExplorerIntegration()

            InitializeCapitalizationManager()
            InitializeLeftPanel
            pObjectExplorer.InitializeWithProjectManager(pProjectManager)
            ' Show welcome tab on startup
            ShowWelcomeTab()

            
            ' Hide bottom panel on startup - use idle handler to ensure proper initialization
            GLib.Idle.Add(Function()
                pBottomPanelManager?.Hide()
                HideBottomPanel()
                UpdateToolbarButtons()
                Return False ' Remove idle handler
            End Function)
            
            Console.WriteLine("MainWindow initialized successfully")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow constructor error: {ex.Message}")
            ShowError("Initialization error", $"Failed to Initialize application: {ex.Message}")
        End Try
    End Sub

    ' ===== Overloaded Constructor for opening with project =====    
    Public Sub New(vProjectFile As String)
        ' Call the default constructor first
        Me.New()
        
        Try
            ' Load pending project file if any (after UI is ready)
            If Not String.IsNullOrEmpty(vProjectFile) AndAlso File.Exists(vProjectFile) Then
                GLib.Idle.Add(Function()
                    LoadProjectEnhanced(vProjectFile)
                    Return False ' Remove idle handler
                End Function)
            End If
        Catch ex As Exception
            Console.WriteLine($"MainWindow constructor with project error: {ex.Message}")
            ShowError("project Load error", $"Failed to load project '{vProjectFile}': {ex.Message}")
        End Try
    End Sub
    
    Private Sub BuildUI()
        Try
            ' Create main vertical box
            pMainVBox = New Box(Orientation.Vertical, 0)
            
            ' Create and add menu bar
            CreateMenuBar()
            pMainVBox.PackStart(pMenuBar, False, False, 0)
            
            ' Create and add toolbar
            CreateToolbar()

            ' Apply initial toolbar settings
            ApplyToolbarSettings()

            pMainVBox.PackStart(pToolbar, False, False, 0)
            
            ' Create main horizontal paned for project explorer and center
            pMainHPaned = New Paned(Orientation.Horizontal)
            pMainHPaned.Position = LEFT_PANEL_WIDTH
            
            ' Create project explorer
            pProjectExplorer = New ProjectExplorer(pSettingsManager)
            AddHandler pProjectExplorer.FileSelected, AddressOf OnProjectFileSelected
            AddHandler pProjectExplorer.ProjectFileSelected, AddressOf OnProjectFileDoubleClicked
            AddHandler pProjectExplorer.ProjectModified, AddressOf OnProjectModified
            AddHandler pProjectExplorer.CloseRequested, AddressOf OnProjectExplorerCloseRequested
            AddHandler pProjectExplorer.ReferencesChanged, AddressOf OnReferencesChanged
            AddHandler pProjectExplorer.ManifestSelected, AddressOf OnManifestSelected
            
            ' Create the notebook for the left panel
            pLeftNotebook = New Notebook()
            pLeftNotebook.TabPos = PositionType.Top
            pLeftNotebook.Scrollable = False
            
            ' Add Project Explorer tab
            Dim lProjectLabel As New Label("Project")
            pLeftNotebook.AppendPage(pProjectExplorer, lProjectLabel)
            
            ' Create and add Object Explorer tab
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager)
            AddHandler pObjectExplorer.NodeDoubleClicked, AddressOf OnObjectExplorerNodeDoubleClicked
            AddHandler pObjectExplorer.CloseRequested, AddressOf OnObjectExplorerCloseRequested


            
            Dim lObjectLabel As New Label("Objects")
            pLeftNotebook.AppendPage(pObjectExplorer, lObjectLabel)
            
            ' Pack the notebook into the left pane
            pMainHPaned.Pack1(pLeftNotebook, False, False)
            
            ' Set the initial tab based on user preference
            Try
                Dim lStartupTab As Integer = pSettingsManager.LeftPanelStartupTab
                ' Ensure the tab index is valid
                If lStartupTab >= 0 AndAlso lStartupTab < pLeftNotebook.NPages Then
                    pLeftNotebook.CurrentPage = lStartupTab
                Else
                    pLeftNotebook.CurrentPage = 0 ' Default to Project tab
                End If
                
                ' Ensure the selected tab's content is visible
                Dim lCurrentPage As Widget = pLeftNotebook.GetNthPage(pLeftNotebook.CurrentPage)
                If lCurrentPage IsNot Nothing Then
                    lCurrentPage.ShowAll()
                End If
                
                Console.WriteLine($"Left panel initialized with tab {pLeftNotebook.CurrentPage}")
            Catch ex As Exception
                Console.WriteLine($"Error setting startup tab: {ex.Message}")
                pLeftNotebook.CurrentPage = 0 ' Fallback to Project tab
            End Try

            ' Create center vertical paned for editor and bottom panel
            pCenterVPaned = New Paned(Orientation.Vertical)
            
            ' Create editor notebook
            pNotebook = New Notebook()
            pNotebook.Scrollable = True
            pNotebook.EnablePopup = True
            AddHandler pNotebook.SwitchPage, AddressOf OnNotebookPageSwitched
            'AddHandler pNotebook.PageRemoved, AddressOf OnNotebookPageRemoved
            
            pCenterVPaned.Pack1(pNotebook, True, False)
            
            ' Create bottom panel manager
            pBottomPanelManager = New BottomPanelManager(pSettingsManager)
            pBottomPanelManager.Initialize()
            'pBottomNotebook = pBottomPanelManager.Notebook
            
            ' Connect bottom panel events
            AddHandler pBottomPanelManager.FindResultSelected, AddressOf OnFindResultSelected
            AddHandler pBottomPanelManager.TodoSelected, AddressOf OnTodoSelected
            AddHandler pBottomPanelManager.ErrorDoubleClicked, AddressOf OnBuildErrorDoubleClicked
            AddHandler pBottomPanelManager.SendErrorsToAI, AddressOf OnSendBuildErrorsToAI

            
            

            ' Initialize find panel events
            InitializeFindPanelEvents()
            
            ' Add bottom panel to center paned
            pCenterVPaned.Pack2(pBottomPanelManager.GetWidget(), False, False)
            
            ' Add center paned to main paned
            pMainHPaned.Pack2(pCenterVPaned, True, False)
            
            ' Add main paned to vbox
            pMainVBox.PackStart(pMainHPaned, True, True, 0)
            
            ' Create and add status bar
            CreateStatusBar()
            pMainVBox.PackStart(pStatusBar, False, False, 0)
            
            ' Add main vbox to window
            Add(pMainVBox)
            
        Catch ex As Exception
            Console.WriteLine($"BuildUI error: {ex.Message}")
            Throw
        End Try
    End Sub

    
    Private Sub ApplyTheme()
        Try
            ' Apply CSS theme
            Dim lCssProvider As New CssProvider()
            Dim lTheme As String = pThemeManager.GetCurrentTheme()
            
            ' Get the CSS from ThemeManager using GetThemeCss method
            Dim lThemeCss As String = pThemeManager.GetThemeCss(lTheme)
            
            If Not String.IsNullOrEmpty(lThemeCss) Then
                lCssProvider.LoadFromData(lThemeCss)
            Else
                Console.WriteLine($"Warning: Theme CSS is empty for theme: {lTheme}")
            End If
            
            ' Apply to all screens
            Dim lScreen As Gdk.Screen = Gdk.Screen.Default
            If lScreen IsNot Nothing Then
                StyleContext.AddProviderForScreen(lScreen, lCssProvider, CUInt(StyleProviderPriority.User))
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ApplyTheme error: {ex.Message}")
        End Try
    End Sub
    
    
    Private Sub UpdatePanelVisibility()
        Try
            ' Update project explorer visibility
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.Visible = pLeftPanelVisible
            End If
            
            ' Update bottom panel visibility using BottomPanelManager
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.IsVisible = pBottomPanelVisible
                
                ' Only adjust position if showing the panel
                If pBottomPanelVisible AndAlso pCenterVPaned IsNot Nothing Then
                    ' Restore saved height
                    Dim lHeight As Integer = pSettingsManager.BottomPanelHeight
                    If lHeight < 50 Then lHeight = BOTTOM_PANEL_HEIGHT
                    
                    ' Ensure we don't exceed allocated height
                    If pCenterVPaned.AllocatedHeight > 0 Then
                        Dim lMaxPosition As Integer = pCenterVPaned.AllocatedHeight - 50
                        pCenterVPaned.Position = Math.Max(50, Math.Min(lMaxPosition, pCenterVPaned.AllocatedHeight - lHeight))
                    End If
                End If
            End If
            
            ' Update menu items
            UpdateMenuStates()
            
        Catch ex As Exception
            Console.WriteLine($"UpdatePanelVisibility error: {ex.Message}")
        End Try
    End Sub
    
    ' Property accessors for panels (using BottomPanelManager)
    Private ReadOnly Property pFindPanel As FindReplacePanel
        Get
            Return pBottomPanelManager?.FindPanel
        End Get
    End Property
    
    Private ReadOnly Property pTodoPanel As TodoPanel
        Get
            Return pBottomPanelManager?.TodoPanel
        End Get
    End Property
    
    Private ReadOnly Property pAIAssistantPanel As AIAssistantPanel
        Get
            Return pBottomPanelManager?.AIAssistantPanel
        End Get
    End Property

    
    Private ReadOnly Property pBuildOutputPanel As BuildOutputPanel
        Get
            Return pBottomPanelManager?.BuildOutputPanel
        End Get
    End Property
    
    Private ReadOnly Property pHelpViewerPanel As HelpViewerPanel
        Get
            Return pBottomPanelManager?.HelpViewerPanel
        End Get
    End Property
    
    Private ReadOnly Property pGitPanel As GitPanel
        Get
            Return pBottomPanelManager?.GitPanel
        End Get
    End Property
    
    Public Function GetSettingsManager() As SettingsManager
        Return pSettingsManager
    End Function
    
    ' Update project root when project changes
    Private Sub UpdateProjectRootForPanels()
        Try
            If pBottomPanelManager IsNot Nothing AndAlso Not String.IsNullOrEmpty(pCurrentProject) Then
                Dim lProjectRoot As String = System.IO.Path.GetDirectoryName(pCurrentProject)
                pBottomPanelManager.UpdateProjectRoot(lProjectRoot)
            End If
        Catch ex As Exception
            Console.WriteLine($"UpdateProjectRootForPanels error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnNotebookPageSwitched(vSender As Object, vArgs As SwitchPageArgs)
        Try
            UpdateStatusBar("")
            UpdateWindowTitle()
            UpdateToolbarButtons()
        Catch ex As Exception
            Console.WriteLine($"OnNotebookPageSwitched error: {ex.Message}")
        End Try
    End Sub

    
    Public Function ShowQuestion(vTitle As String, vMessage As String) As Boolean
        Try
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Question,
                ButtonsType.YesNo,
                vMessage
            )
            lDialog.Title = vTitle
            Dim lResponse As ResponseType = CType(lDialog.Run(), ResponseType)
            lDialog.Destroy()
            
            Return lResponse = ResponseType.Yes
            
        Catch ex As Exception
            Console.WriteLine($"ShowQuestion error: {ex.Message}")
            Return False
        End Try
    End Function

    Private Sub CleanUp()
        Try
            ' Dispose of resources
            SaveAllScratchpads()
            pFileSystemWatcher?.Dispose()
            pIntelliSenseEngine?.Dispose()
            
            ' Close all tabs
            CloseAllTabs()
            
        Catch ex As Exception
            Console.WriteLine($"CleanUp error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnSettingsChanged(vSettingName As String, vOldValue As Object, vNewValue As Object)
        Try
            Select Case vSettingName
                Case "ShowToolbar", "ToolbarShowLabels", "ToolbarLargeIcons"
                    ' Apply toolbar settings when they change
                    ApplyToolbarSettings()
                    
                Case "ShowStatusBar"
                    ' Handle status bar visibility
                    Dim lShow As Boolean = CBool(vNewValue)
                    If lShow Then
                        pStatusBar?.Show()
                    Else
                        pStatusBar?.Hide()
                    End If
                    
                Case "ShowProjectExplorer"
                    ' Handle project explorer visibility
                    Dim lShow As Boolean = CBool(vNewValue)
                    If lShow Then
                        pProjectExplorer?.Show()
                    Else
                        pProjectExplorer?.Hide()
                    End If
                    
                ' Add other settings as needed
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"OnSettingsChanged error: {ex.Message}")
        End Try
    End Sub

    Public ReadOnly Property OpenTabs() as Dictionary(Of String, TabInfo)
        Get
            Return pOpenTabs
        End Get
    End Property

' Step 1: Add this to your MainWindow constructor or initialization
Private Sub DebugF5Issue()
    Try
        Console.WriteLine("=== F5 DEBUG SETUP ===")
        
        ' Test 1: Verify AccelGroup is working
        Console.WriteLine("Test 1: Setting up simple F12 test")
        Dim lTestAccelGroup As New AccelGroup()
        AddAccelGroup(lTestAccelGroup)
        
        ' F12 should show a message - this tests if accelerators work at all
        Dim lF12Keyval As UInteger = gdk.Keyval.FromName("F12")
        If lF12Keyval <> 0 Then
            AddHandler lTestAccelGroup.AccelActivate, Sub()
                Console.WriteLine("F12 ACCELERATOR WORKED!")
                Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Accelerator Test")
                lDialog.SecondaryText = "F12 accelerator is working! Now testing F5..."
                lDialog.Run()
                lDialog.Destroy()
            End Sub
            lTestAccelGroup.Connect(lF12Keyval, gdk.ModifierType.None, AccelFlags.Visible, Nothing)
            Console.WriteLine("F12 test accelerator connected")
        End If
        
        ' Test 2: Try F5 with simple handler
        Console.WriteLine("Test 2: Setting up F5 test")
        Dim lF5Keyval As UInteger = gdk.Keyval.FromName("F5")
        If lF5Keyval <> 0 Then
            AddHandler lTestAccelGroup.AccelActivate, Sub()
                Console.WriteLine("F5 ACCELERATOR WORKED!")
                Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "F5 Test")
                lDialog.SecondaryText = "F5 accelerator is working!"
                lDialog.Run()
                lDialog.Destroy()
            End Sub
            lTestAccelGroup.Connect(lF5Keyval, Gdk.ModifierType.None, AccelFlags.Visible, Nothing)
            Console.WriteLine("F5 test accelerator connected")
        End If
        
        ' Test 3: Check window focus properties
        Console.WriteLine($"Window CanFocus: {CanFocus}")
        Console.WriteLine($"Window HasFocus: {HasFocus}")
        
        ' Test 4: Add key press handler to window itself
        AddHandler KeyPressEvent, AddressOf OnDebugKeyPress
        
        Console.WriteLine("=== F5 DEBUG SETUP COMPLETE ===")
        
    Catch ex As Exception
        Console.WriteLine($"DebugF5Issue error: {ex.Message}")
    End Try
End Sub

' Add this key press handler to catch all key events
Private Function OnDebugKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
    Try
        Dim lKey As Gdk.Key = vArgs.Event.Key
        Console.WriteLine($"DEBUG: Key pressed: {lKey} (KeyVal: {vArgs.Event.KeyValue})")
        
        ' Specifically watch for F5
        If lKey = Gdk.Key.F5 Then
            Console.WriteLine("DEBUG: F5 detected in window key press handler!")
           BuildProject
            ' Don't consume it - let it bubble to accelerators
            Return False
        End If
        
        Return False ' Don't consume any keys
        
    Catch ex As Exception
        Console.WriteLine($"OnDebugKeyPress error: {ex.Message}")
        Return False
    End Try
End Function    
    
End Class
