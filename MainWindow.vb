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
    Private pCodeSenseEngine As CodeSenseEngine
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
    Private pProjectExplorer As CustomDrawProjectExplorer
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
            ' Initialize settings FIRST
            pSettingsManager = New SettingsManager()
            pThemeManager = New ThemeManager(pSettingsManager)
            pMemoryManifest = New MemoryManifest(pSettingsManager)
            AddHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsChanged
            InitializeScratchpad()
            
            ' Setup window
            SetupWindow()
            
            ' Build UI (creates the structure but NOT ObjectExplorer)
            BuildUI()
            AddHandler Me.KeyPressEvent, AddressOf OnWindowKeyPress
            
            ' CRITICAL: Initialize left panel BEFORE project manager
            ' This creates the ObjectExplorer instance
            InitializeLeftPanel()

            InitializeLeftPanelWidth()
            
            ' NOW safe to create project manager
            pProjectManager = New ProjectManager()
            AddHandler pProjectManager.ProjectLoaded, AddressOf OnProjectManagerProjectLoaded
            AddHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
            AddHandler pProjectManager.ProjectModified, AddressOf OnProjectManagerProjectModified
            AddHandler pProjectManager.FileAdded, AddressOf OnProjectManagerFileAdded
            AddHandler pProjectManager.FileRemoved, AddressOf OnProjectManagerFileRemoved
            AddHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
            
            ' FIX: Now set the ProjectManager in ProjectExplorer since it was created before ProjectManager existed
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.SetProjectManager(pProjectManager)
                Console.WriteLine("ProjectManager set in ProjectExplorer after creation")
            End If

            ' Now initialize ObjectExplorer with ProjectManager
            If pObjectExplorer IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                pObjectExplorer.InitializeWithProjectManager(pProjectManager)
                AddHandler pObjectExplorer.GetThemeManager, AddressOf OnGetThemeManager
                ' Single-click selection
                AddHandler pObjectExplorer.NodeSelected, AddressOf OnObjectExplorerNodeSelected
                
                ' Navigation (handles double-click and Enter key)
                AddHandler pObjectExplorer.NavigateToFile, AddressOf OnObjectExplorerNavigateToFile
            End If
            
            ' Complete Object Explorer setup
            CompleteObjectExplorerSetup()
            
            ' Initialize CodeSense engine
            pCodeSenseEngine = New CodeSenseEngine()
            
            ' Initialize build system
            InitializeBuildSystem()
            
            ' Wire up ProjectExplorer with ProjectManager
            InitializeProjectExplorerWithManager()
            
            ' Apply theme
            ApplyTheme()
            
            ' Apply settings
            ApplySettings()
            
            ' Initialize panel visibility
            UpdatePanelVisibility()
            
            ' Setup file system watcher
            SetupFileSystemWatcher()
            
            pEditorFactory = New EditorFactory()
            EditorFactory.Initialize(pSyntaxColorSet, pSettingsManager, pProjectManager)
            
            InitializeWithObjectExplorerIntegration()
            InitializeCapitalizationManager()
            InitializeCodeSense()
            InitializeProjectManagerReferences()

            ' Setup window state tracking
            ' TODO: SetupWindowStateTracking()
            
            ' ADD THIS LINE: Setup window focus handling
            ' TODO: SetupWindowFocusHandling()
            
            ' Show welcome tab on startup
            ShowWelcomeTab()

            SetupKeyboardShortcuts()
            
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
            pProjectExplorer = New CustomDrawProjectExplorer(pSettingsManager, pProjectManager, pThemeManager)
            AddHandler pProjectExplorer.FileSelected, AddressOf OnProjectFileSelected
            AddHandler pProjectExplorer.ProjectFileSelected, AddressOf OnProjectFileDoubleClicked
            AddHandler pProjectExplorer.ProjectModified, AddressOf OnProjectModified
            AddHandler pProjectExplorer.CloseRequested, AddressOf OnProjectExplorerCloseRequested
            AddHandler pProjectExplorer.ReferencesChanged, AddressOf OnReferencesChanged
            AddHandler pProjectExplorer.ManifestSelected, AddressOf OnManifestSelected
            
            ' Create center vertical paned for editor and bottom panel
            pCenterVPaned = New Paned(Orientation.Vertical)

            ' Setup paned constraints and handling
            SetupPanedHandling()
             
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
            AddHandler pBottomPanelManager.PanelClosed, AddressOf OnBottomPanelClosed

            
            

            ' Initialize find panel events
            InitializeFindPanelEvents()

            InitializeBottomPanel()
            
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
                Console.WriteLine($"Warning: Theme CSS Is empty for theme: {lTheme}")
            End If
            
            ' Apply to all screens
            Dim lScreen As Gdk.Screen = Gdk.Screen.Default
            If lScreen IsNot Nothing Then
                StyleContext.AddProviderForScreen(lScreen, lCssProvider, CUInt(StyleProviderPriority.User))
            End If

            If pBuildOutputPanel IsNot Nothing AndAlso pThemeManager IsNot Nothing Then
                pBuildOutputPanel.SetThemeManager(pThemeManager)
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
            pCodeSenseEngine?.Dispose()
            
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

    ''' <summary>
    ''' Sets up window focus event handling to ensure editor gets focus
    ''' </summary>
    ''' <remarks>
    ''' Ensures the editor receives focus when window is activated
    ''' </remarks>
    Private Sub SetupWindowFocusHandling()
        Try
            ' Connect window focus events
            AddHandler Me.FocusInEvent, AddressOf OnWindowFocusIn
            AddHandler Me.FocusOutEvent, AddressOf OnWindowFocusOut
            
            ' Also handle window activation (when clicking on title bar)
            AddHandler Me.WindowStateEvent, AddressOf OnWindowStateEventForFocus
            
            Console.WriteLine("Window focus handling initialized")
            
        Catch ex As Exception
            Console.WriteLine($"SetupWindowFocusHandling error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles window focus in event
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Focus event arguments</param>
    ''' <remarks>
    ''' When window gains focus, ensure current editor also gets focus
    ''' </remarks>
    Private Sub OnWindowFocusIn(vSender As Object, vArgs As FocusInEventArgs)
        Try
            Console.WriteLine("Window gained focus")
            
            ' Schedule editor focus on idle to ensure window is fully activated
            GLib.Idle.Add(Function()
                ' Check if we should focus the editor
                If ShouldFocusEditor() Then
                    Dim lEditor As IEditor = GetCurrentEditor()
                    If lEditor IsNot Nothing Then
                        lEditor.GrabFocus()
                        Console.WriteLine("Focus returned To editor On window activation")
                    End If
                End If
                Return False ' Run once
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowFocusIn error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles window focus out event
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Focus event arguments</param>
    ''' <remarks>
    ''' Tracks when window loses focus for proper handling on return
    ''' </remarks>
    Private Sub OnWindowFocusOut(vSender As Object, vArgs As FocusOutEventArgs)
        Try
            Console.WriteLine("Window lost focus")
            ' Could store state here if needed
        Catch ex As Exception
            Console.WriteLine($"OnWindowFocusOut error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles window state changes for focus management
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Window state event arguments</param>
    ''' <remarks>
    ''' Ensures editor gets focus when window becomes active/focused
    ''' </remarks>
    Private Sub OnWindowStateEventForFocus(vSender As Object, vArgs As WindowStateEventArgs)
        Try
            Dim lNewState As Gdk.WindowState = vArgs.Event.NewWindowState
            Dim lChangedMask As Gdk.WindowState = vArgs.Event.ChangedMask
            
            ' Check if focused state changed
            If (lChangedMask And Gdk.WindowState.Focused) = Gdk.WindowState.Focused Then
                Dim lIsFocused As Boolean = (lNewState And Gdk.WindowState.Focused) = Gdk.WindowState.Focused
                
                If lIsFocused Then
                    Console.WriteLine("Window became focused via state change")
                    
                    ' Schedule editor focus on idle
                    GLib.Idle.Add(Function()
                        If ShouldFocusEditor() Then
                            Dim lEditor As IEditor = GetCurrentEditor()
                            If lEditor IsNot Nothing Then
                                lEditor.GrabFocus()
                                Console.WriteLine("Focus returned To editor On window state change")
                            End If
                        End If
                        Return False ' Run once
                    End Function)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowStateEventForFocus error: {ex.Message}")
        End Try
    End Sub

    
    
End Class
