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
    Private pCodeSenseManager As CodeSenseManager
    Private pFileSystemWatcher As Utilities.FileSystemWatcher
    Private pMemoryManifest As MemoryManifest
    Private pProjectManager As ProjectManager
    
    ' UI Components
    Private pMainVBox As Box
    Private pMenuBar As MenuBar
    Private pToolbar As Toolbar
    Private pMainHPaned As Paned
    Private pCenterVPaned As Paned
    Private pProjectExplorer As CustomDrawProjectExplorer
    Private pNotebook As CustomDrawNotebook
    Private pStatusBar As Statusbar
    
    ' Bottom panel manager
    Private pBottomPanelManager As BottomPanelManager

    ' Set when ShowBottomPanel wants the paned divider reset to the default bottom-panel
    ' height on the next real size-allocate pass - see OnCenterVPanedSizeAllocated
    Private pApplyDefaultBottomPanelPositionOnNextAllocate As Boolean = False
    
    ' State
    Private pCurrentProject As String = ""
    Private pOpenTabs As New Dictionary(Of String, TabInfo)()
    Private pLeftPanelVisible As Boolean = True
    Private pBottomPanelVisible As Boolean = False
    Private pIsBuilding As Boolean = False
    Private pIsFullScreen As Boolean = False
    Private pIsObjectExplorerNavigating As Boolean = False
    Private pPendingProjectFile As String = Nothing
    Private pTotalFilesToParse As Integer = 0
    Private pCurrentFileParsed As Integer = 0

    ''' <summary>
    ''' Set once OnAllFilesParseCompleted fires for the current project load, so the "Parsing N
    ''' files..." status text (written slightly later by LoadProjectEnhanced's own success
    ''' callback, since the two run as a race between independent background tasks) never
    ''' overwrites the real completion message once parsing has actually finished
    ''' </summary>
    Private pProjectFilesParsingComplete As Boolean = False
    Private pPanedSaveTimer As System.Threading.Timer
    
    ' ===== Constructor =====
    
    Public Sub New()
        MyBase.New(WINDOW_TITLE)
        
        Try
            ' Initialize settings FIRST
            pSettingsManager = New SettingsManager()
            ProjectFileScanner.Initialize(pSettingsManager)
            pProjectManager = New ProjectManager()
            pThemeManager = New ThemeManager(pSettingsManager)

            pMemoryManifest = New MemoryManifest(pSettingsManager)
            AddHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsChanged
            AddHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsManagerSettingsChanged_WelcomeTab

            InitializeThemeSystem()

            InitializeScratchpad()
            
            ' Setup window
            SetupWindow()
            
            ' Build UI (creates the structure but NOT ObjectExplorer)
            BuildUI()
            
            AddHandler Me.KeyPressEvent, AddressOf OnWindowKeyPress
            
            ' CRITICAL: Initialize left panel BEFORE project manager
            ' This creates the ObjectExplorer instance
            InitializeLeftPanel()            
            
            ' NOW safe to create project manager
            AddHandler pProjectManager.ProjectLoaded, AddressOf OnProjectManagerProjectLoaded
            AddHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
            AddHandler pProjectManager.ProjectModified, AddressOf OnProjectManagerProjectModified
            AddHandler pProjectManager.FileAdded, AddressOf OnProjectManagerFileAdded
            AddHandler pProjectManager.FileRemoved, AddressOf OnProjectManagerFileRemoved
            AddHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
            AddHandler pProjectManager.FileSaved, AddressOf OnProjectManagerFileSaved
            
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
            pCodeSenseManager = New CodeSenseManager(pCodeSenseEngine)
            
            ' Initialize build system
            InitializeBuildSystem()
            
            ' Wire up ProjectExplorer with ProjectManager
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.SetProjectManager(pProjectManager)
                
                ' Wire up ProjectManager events to refresh Project Explorer
                AddHandler pProjectManager.FileAdded, AddressOf OnProjectManagerFileAdded
                AddHandler pProjectManager.FileRemoved, AddressOf OnProjectManagerFileRemoved
                AddHandler pProjectManager.FileRenamed, AddressOf OnProjectManagerFileRenamed
                ' Note: OnProjectManagerProjectModified is already wired up elsewhere
                
                Console.WriteLine("ProjectExplorer integrated with ProjectManager")
            End If            

            ' Apply theme
            'ApplyTheme()
            
            ' Apply settings
            ApplySettings()
            
            ' Initialize panel visibility
            UpdatePanelVisibility()
            
            ' Setup file system watcher
            SetupFileSystemWatcher()
            
            
            InitializeObjectExplorer()
            InitializeCodeSense()
            InitializeCodeSenseWithProjectManager()
            InitializeProjectManagerReferences()

            ' Setup window state tracking
            ' TODO: SetupWindowStateTracking()
            
            ' ADD THIS LINE: Setup window focus handling
            ' TODO: SetupWindowFocusHandling()
            
            ' Show welcome tab on startup (only if no pending project)
            If String.IsNullOrEmpty(pPendingProjectFile) Then
                ShowWelcomeTab()
            End If

            ' Offer Linux desktop integration once the window is fully shown, rather than
            ' popping a dialog up mid-construction
            GLib.Idle.Add(Function()
                CheckAndOfferDesktopIntegration()
                Return False
            End Function)

            SetupKeyboardShortcuts()

            ' CRITICAL FIX: Show all BEFORE setting up panels
            ShowAll()
           
            ' CRITICAL FIX: Ensure left panel is properly initialized and visible
            ' Use idle handler to ensure proper initialization AFTER ShowAll()
            GLib.Idle.Add(Function()
                Try
                    ' Ensure left panel is visible with correct width
                    If pLeftNotebook IsNot Nothing Then
                        pLeftNotebook.ShowAll()
                        Console.WriteLine($"Left notebook shown with {pLeftNotebook.NPages} pages")
                    End If
                    
                    ' Get saved width or use default
                    Dim lSavedWidth As Integer = LEFT_PANEL_MINIMUM_WIDTH
                    If pSettingsManager IsNot Nothing Then
                        lSavedWidth = pSettingsManager.GetInteger("leftpanelwidth", LEFT_PANEL_MINIMUM_WIDTH)
                        If lSavedWidth < LEFT_PANEL_MINIMUM_WIDTH Then
                            lSavedWidth = LEFT_PANEL_MINIMUM_WIDTH
                        End If
                        If lSavedWidth > 500 Then lSavedWidth = 500
                    End If
                    
                    ' Set the position
                    If pMainHPaned IsNot Nothing Then
                        pMainHPaned.Position = lSavedWidth
                        Console.WriteLine($"Set left panel initial position to {lSavedWidth}")
                    End If
                    
                    ' Ensure left notebook is visible
                    If pLeftNotebook IsNot Nothing Then
                        pLeftNotebook.Visible = True
                        pLeftNotebook.ShowAll()
                        Console.WriteLine($"Left notebook visibility ensured")
                    End If
                    
                    ' Run auto-diagnosis to check and fix any issues
                    AutoDiagnoseOnStartup()
                    
                    ' Hide bottom panel on startup
                    pBottomPanelManager?.Hide()
                    HideBottomPanel()
                    UpdateToolbarButtons()
                    
                Catch ex As Exception
                    Console.WriteLine($"Error setting initial panel state: {ex.Message}")
                End Try
                Return False ' Remove idle handler
            End Function)   
            
            ' Add another idle handler as a safety check after a short delay
            GLib.Timeout.Add(500, Function()
                Try
                    ' Final check - if left panel still not visible, force it
                    If pLeftNotebook IsNot Nothing AndAlso Not pLeftNotebook.Visible Then
                        Console.WriteLine("WARNING: Left panel still not visible after 500ms, forcing visibility")
                        ForceShowLeftPanel()
                        EnsureLeftPanelWidth
                    ElseIf pMainHPaned IsNot Nothing AndAlso pMainHPaned.Position < 50 Then
                        Console.WriteLine("WARNING: Left panel position still too small after 500ms, forcing position")
                        pMainHPaned.Position = LEFT_PANEL_MINIMUM_WIDTH
                    End If
                Catch ex As Exception
                    Console.WriteLine($"500ms safety check error: {ex.Message}")
                End Try
                Return False ' Remove timeout
            End Function)   

            ' If we're not loading a project via the other constructor,
            ' check for auto-detect or recent projects after UI is shown
            If String.IsNullOrEmpty(pPendingProjectFile) Then
                AddHandler Me.Shown, AddressOf OnWindowShownNoProject
            End If

            Console.WriteLine("MainWindow initialized successfully")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow constructor error: {ex.Message}")
            ShowError("Initialization error", $"Failed to Initialize application: {ex.Message}")
        End Try
    End Sub

    ' ===== Overloaded Constructor for opening with project =====    

    ''' <summary>
    ''' Constructor for opening MainWindow with a project file (Alternative approach)
    ''' </summary>
    ''' <param name="vProjectFile">Path to the project file to load</param>
    Public Sub New(vProjectFile As String)
        ' Call the default constructor first
        Me.New()
        
        Try
            Console.WriteLine($"MainWindow(project) constructor: Starting with project: {vProjectFile}")
            
            ' Store the project file to load after UI is ready
            pPendingProjectFile = vProjectFile
            Console.WriteLine($"MainWindow(project) constructor: Set pPendingProjectFile = {pPendingProjectFile}")
            
            ' Use a timeout to load the project after the UI is ready
            ' This gives GTK time to fully initialize and show the window
            GLib.Timeout.Add(100, Function()
                Console.WriteLine($"MainWindow(project) Timeout: Checking if ready to load project")
                
                ' Check if window is realized and visible
                If Me.IsRealized AndAlso Me.Visible Then
                    Console.WriteLine($"MainWindow(project) Timeout: Window ready, loading project")
                    
                    ' Load the project (or solution) asynchronously
                    If Not String.IsNullOrEmpty(pPendingProjectFile) AndAlso
                       File.Exists(pPendingProjectFile) AndAlso
                       String.IsNullOrEmpty(pCurrentProject) Then

                        If System.IO.Path.GetExtension(pPendingProjectFile).Equals(".sln", StringComparison.OrdinalIgnoreCase) Then
                            LoadSolutionEnhanced(pPendingProjectFile)
                        Else
                            LoadProjectEnhanced(pPendingProjectFile)
                        End If
                        pPendingProjectFile = Nothing
                    End If
                    
                    Return False ' Remove timeout
                Else
                    Console.WriteLine($"MainWindow(project) Timeout: Window not ready yet, will retry")
                    Return True ' Keep trying
                End If
            End Function)
            
            Console.WriteLine("MainWindow(project) constructor: Complete")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow(project) constructor error: {ex.Message}")
            ShowError("Initialization Error", $"Failed To initialize with project: {ex.Message}")
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

            ' Wrap in a Gtk.EventBox - a Gtk.ToolItem (the wrapper GTK creates around each
            ' item added to a Toolbar, for its own drag-reorder support) realizes its own
            ' separate input window, so a plain ButtonPressEvent/MotionNotifyEvent handler on
            ' pToolbar itself only ever catches truly bare Toolbar canvas outside every
            ' ToolItem (e.g. the gap after the last item) - anything landing within a
            ' ToolItem's own bounds but not claimed by a deeper interactive child (button
            ' padding, a plain Label) never reaches it. EventBox exists specifically to give
            ' an area a real window so it can reliably claim events like this; wrapping the
            ' whole toolbar in one guarantees the swallow-handlers below actually see
            ' everything not already claimed by a button/control's own window, without
            ' interfering with those - GTK still delivers directly to the most specific
            ' windowed widget under the cursor first.
            '
            ' KNOWN LIMITATION (2026-08-06): this, together with the matching fixes in
            ' CustomDrawButton/CustomDrawComboBox (SimpleIDE.Widgets) and the identical
            ' EventBox wrap in CustomDrawProjectExplorer.Toolbar.vb/
            ' CustomDrawObjectExplorer.Toolbar.vb, DID fix double-click-on-toolbar-background
            ' toggling window maximize/restore. It did NOT fully fix click-and-drag on a
            ' toolbar still moving the window like a titlebar - confirmed still reproducible
            ' by James after this round of fixes. Believed to be a KDE/KWin (or GTK3
            ' interactive-move) behavior operating below the level these GTK signal handlers
            ' can intercept, rather than anything wrong in this app's own widget code. James
            ' has accepted this as a known desktop-environment quirk rather than something
            ' worth further investigation - don't re-attempt fixing the drag specifically
            ' without new information.
            Dim lToolbarEventBox As New EventBox()
            lToolbarEventBox.Events = Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.PointerMotionMask
            AddHandler lToolbarEventBox.ButtonPressEvent, AddressOf OnToolbarBackgroundButtonPress
            AddHandler lToolbarEventBox.MotionNotifyEvent, AddressOf OnToolbarBackgroundMotion
            lToolbarEventBox.Add(pToolbar)

            pMainVBox.PackStart(lToolbarEventBox, False, False, 0)
            
            ' Create main horizontal paned for project explorer and center
            pMainHPaned = New Paned(Orientation.Horizontal)
            
            ' Get saved width from settings
            Dim lSavedWidth As Integer = LEFT_PANEL_MINIMUM_WIDTH  ' Use the 310 constant
            If pSettingsManager IsNot Nothing Then
                lSavedWidth = pSettingsManager.GetInteger("leftpanelwidth", LEFT_PANEL_MINIMUM_WIDTH)
                
                ' Validate the position
                If lSavedWidth < LEFT_PANEL_MINIMUM_WIDTH Then 
                    lSavedWidth = LEFT_PANEL_MINIMUM_WIDTH
                End If
                If lSavedWidth > 500 Then lSavedWidth = 500
            End If      
                       
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
            pNotebook = New CustomDrawNotebook(pThemeManager)
            'DirectCast(pNotebook, CustomDrawNotebook).SetThemeManager(pThemeManager)
            DirectCast(pNotebook, CustomDrawNotebook).ShowHidePanelButton = False ' Main editor doesn't need hide button
            
            ' Wire up CustomDrawNotebook events
            AddHandler DirectCast(pNotebook, CustomDrawNotebook).CurrentTabChanged, AddressOf OnMainNotebookPageSwitched
            AddHandler DirectCast(pNotebook, CustomDrawNotebook).TabClosing, AddressOf OnNotebookTabClosing
            AddHandler DirectCast(pNotebook, CustomDrawNotebook).TabClosed, AddressOf OnCustomNotebookTabClosed
            AddHandler DirectCast(pNotebook, CustomDrawNotebook).TabModifiedChanged, AddressOf OnCustomNotebookTabModifiedChanged

            ' shrink:=True here for the same reason it's needed on Pack2 (see the bottom
            ' panel's Pack2 call below): GTK's Paned clamps any explicitly-set Position to
            ' never give a shrink:=False child less than its own reported natural/preferred
            ' size. The Welcome tab (a Fixed container whose default preferred-size
            ' computation is driven by its children's absolute positions - the Quick Start
            ' buttons and drawing area) can end up reporting a large natural height that has
            ' nothing to do with what space is actually available, which silently overrode
            ' every attempt to give the bottom panel more room, no matter what Position was
            ' set to. shrink:=True removes that floor - the editor/Welcome pane can be given
            ' less than its natural size and clip/scroll internally (both CustomDrawingEditor
            ' and WelcomeTabWidget already handle that), so Position is fully respected.
            pCenterVPaned.Pack1(pNotebook, True, True)
            
            ' Create bottom panel manager
            pBottomPanelManager = New BottomPanelManager(pSettingsManager, pThemeManager)
            
            ' Connect bottom panel events
            AddHandler pBottomPanelManager.FindResultSelected, AddressOf OnFindResultSelected
            AddHandler pBottomPanelManager.BuildErrorWarningSelected, AddressOf OnFindResultSelected
            AddHandler pBottomPanelManager.TodoSelected, AddressOf OnTodoSelected
            AddHandler pBottomPanelManager.TodoDoubleClicked, AddressOf OnTodoDoubleClicked
            AddHandler pBottomPanelManager.TodoNavigateToCode, AddressOf OnTodoNavigateToCode
            AddHandler pBottomPanelManager.ErrorDoubleClicked, AddressOf OnBuildErrorDoubleClicked
            AddHandler pBottomPanelManager.SendErrorsToAI, AddressOf OnSendBuildErrorsToAI
            AddHandler pBottomPanelManager.SendTodoToAI, AddressOf OnSendTodoToAI
            AddHandler pBottomPanelManager.PanelClosed, AddressOf OnBottomPanelClosed
            AddHandler pBottomPanelManager.BuildRequested, AddressOf OnBuildOutputPanelBuildRequested
            AddHandler pBottomPanelManager.RunRequested, AddressOf OnBuildOutputPanelRunRequested
            AddHandler pBottomPanelManager.StopRequested, AddressOf OnBuildOutputPanelStopRequested

            ' Hook up notebook fix after window is realized
            AddHandler Me.Realized, AddressOf OnWindowRealizedForNotebooks            
            

            ' Initialize find panel events
            InitializeFindPanelEvents()

            InitializeBottomPanel()
            
            ' Add bottom panel to center paned. shrink:=True is critical here - with
            ' shrink:=False (the original setting), GTK permanently reserves the bottom
            ' panel's full natural minimum height in the paned's own size requisition the
            ' moment the panel becomes visible, regardless of what pCenterVPaned.Position is
            ' set to afterward. If the window has no slack left (e.g. already filling the
            ' screen), GTK has no choice but to grow the toplevel window itself to satisfy
            ' that minimum - which is what was actually causing the window to grow taller
            ' than the screen and get pushed off the top by the window manager whenever the
            ' bottom panel was first shown. shrink:=True lets the paned allocate the panel
            ' less than its natural size (clipped/scrolled internally, same as every other
            ' dockable panel in a normal IDE), so showing it only takes space within the
            ' existing window instead of growing the window.
            pCenterVPaned.Pack2(pBottomPanelManager.GetWidget(), False, True)
            
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
                If pBottomPanelVisible Then
                    pBottomPanelManager.Show()
                Else
                    pBottomPanelManager.Hide()
                End If
                
                pBottomPanelManager.IsVisible = pBottomPanelVisible
                
                ' Only adjust position if showing the panel
                If pBottomPanelVisible AndAlso pCenterVPaned IsNot Nothing Then
                    ApplyDefaultBottomPanelPosition(pCenterVPaned.AllocatedHeight)
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
    
    Private ReadOnly Property pGitPanel As GitPanel
        Get
            Return pBottomPanelManager?.GitPanel
        End Get
    End Property
    
    Public Function GetSettingsManager() As SettingsManager
        Return pSettingsManager
    End Function

    Private Sub OnMainNotebookPageSwitched(vOldIndex As Integer, vNewIndex As Integer)
        Try
            ' Update UI for the new tab
            If vNewIndex >= 0 AndAlso vNewIndex < pNotebook.NPages Then
                Dim lPage As Widget = pNotebook.GetNthPage(vNewIndex)
                
                ' Find the tab info for this page
                for each lTabEntry in pOpenTabs
                    Dim lTabInfo As TabInfo = lTabEntry.Value
                    If lTabInfo.EditorContainer Is lPage OrElse 
                       (lTabInfo.Editor IsNot Nothing AndAlso lTabInfo.Editor.Widget Is lPage) Then
                        
                        ' Update window title
                        UpdateWindowTitle()
                        
                        ' Update status bar
                        If lTabInfo.Editor IsNot Nothing Then
                            Dim lFileName As String = System.IO.Path.GetFileName(lTabInfo.FilePath)
                            UpdateStatusBar($"Editing: {lFileName}")
                        End If
                        
                        ' Update Object Explorer if needed
                        UpdateObjectExplorerForActiveTab()
                        
                        ' Focus the editor
                        If lTabInfo.Editor IsNot Nothing Then
                            lTabInfo.Editor.GrabFocus()
                        End If
                        
                        Exit for
                    End If
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookPageSwitched error: {ex.Message}")
        End Try
    End Sub
    
    Public Function ShowQuestion(vTitle As String, vMessage As String) As Boolean
        Try
            Dim lResponse As ResponseType = ShowCustomButtonDialog(
                MessageType.Question, vMessage,
                New String() {"No", "Yes"},
                New ResponseType() {ResponseType.No, ResponseType.Yes},
                vTitle)

            Return lResponse = ResponseType.Yes

        Catch ex As Exception
            Console.WriteLine($"ShowQuestion error: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Shows a MessageDialog-style confirmation with custom-drawn bevel buttons instead of
    ''' native stock ones (MessageDialog's icon+message content area is left native - only
    ''' the action-area buttons are replaced)
    ''' </summary>
    ''' <param name="vMessageType">Icon/severity to show (Question, Warning, etc.)</param>
    ''' <param name="vMessage">The message body text</param>
    ''' <param name="vButtonTexts">Button labels, shown left-to-right in this order</param>
    ''' <param name="vButtonResponses">ResponseType for each button, matched by index to vButtonTexts</param>
    ''' <param name="vTitle">Optional dialog title</param>
    ''' <returns>The ResponseType of whichever button the user clicked</returns>
    Public Function ShowCustomButtonDialog(vMessageType As MessageType, vMessage As String,
                                            vButtonTexts() As String, vButtonResponses() As ResponseType,
                                            Optional vTitle As String = "") As ResponseType
        Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, vMessageType, ButtonsType.None, vMessage)
        Try
            If Not String.IsNullOrEmpty(vTitle) Then lDialog.Title = vTitle

            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.Halign = Align.End
            lButtonBox.BorderWidth = 6

            For i As Integer = 0 To vButtonTexts.Length - 1
                Dim lButton As New CustomDrawButton(vButtonTexts(i))
                lButton.ThemeManager = pThemeManager
                Dim lResponse As ResponseType = vButtonResponses(i)
                AddHandler lButton.Clicked, Sub() lDialog.Respond(lResponse)
                lButtonBox.PackStart(lButton, False, False, 0)
            Next

            Dim lContentBox As Box = TryCast(lDialog.ContentArea, Box)
            If lContentBox IsNot Nothing Then lContentBox.PackStart(lButtonBox, False, False, 0)

            lDialog.ShowAll()
            Return CType(lDialog.Run(), ResponseType)

        Catch ex As Exception
            Console.WriteLine($"ShowCustomButtonDialog error: {ex.Message}")
            Return ResponseType.Cancel
        Finally
            lDialog.Destroy()
        End Try
    End Function

    ''' <summary>
    ''' Closes all tabs (prompting to save as needed) and disposes application resources
    ''' </summary>
    ''' <returns>True if cleanup completed; False if the user cancelled closing a tab, in
    ''' which case resources are deliberately left undisposed since the app is staying open</returns>
    Private Function CleanUp() As Boolean
        Try
            ' Close all tabs first (may prompt to save/cancel) - check this before disposing
            ' anything else, so a cancelled close doesn't leave the app running with its
            ' file watcher/CodeSense engine already torn down
            If Not CloseAllTabs() Then
                Return False
            End If

            SaveAllScratchpads()
            pFileSystemWatcher?.Dispose()
            pCodeSenseEngine?.Dispose()

            Return True

        Catch ex As Exception
            Console.WriteLine($"CleanUp error: {ex.Message}")
            Return False
        End Try
    End Function

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

    Public ReadOnly Property OpenTabs() As Dictionary(Of String, TabInfo)
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
            If (lChangedMask and Gdk.WindowState.Focused) = Gdk.WindowState.Focused Then
                Dim lIsFocused As Boolean = (lNewState and Gdk.WindowState.Focused) = Gdk.WindowState.Focused
                
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

    ''' <summary>
    ''' Initializes Object Explorer integration in the main window
    ''' </summary>
    Private Sub InitializeObjectExplorer()
        Try
            ' Ensure Object Explorer is properly set up
            If pObjectExplorer Is Nothing Then
                Console.WriteLine("Warning: Object Explorer Not initialized")
                Return
            End If
            
            ' Set up initial Object Explorer state
            UpdateObjectExplorerForActiveTab()
            
            ' Hook up notebook events with Object Explorer integration
            If pNotebook IsNot Nothing Then
                ' Remove any existing handler to avoid duplicates
                RemoveHandler pNotebook.CurrentTabChanged, AddressOf OnMainNotebookPageSwitched
                AddHandler pNotebook.CurrentTabChanged, AddressOf OnMainNotebookPageSwitched
            End If
            
            ' Hook up left notebook page changes for Object Explorer activation
            If pLeftNotebook IsNot Nothing Then
                RemoveHandler pLeftNotebook.CurrentTabChanged, AddressOf OnLeftNotebookPageChanged
                AddHandler pLeftNotebook.CurrentTabChanged, AddressOf OnLeftNotebookPageChanged
            End If
            
            Console.WriteLine("Object Explorer integration initialized")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeObjectExplorer error: {ex.Message}")
        End Try
    End Sub    

    ''' <summary>
    ''' Handles the Shown event when a project needs to be loaded
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub OnWindowShownWithProject(sender As Object, e As EventArgs)
        Try
            Console.WriteLine($"OnWindowShownWithProject: Called with pending file: {pPendingProjectFile}")
            
            ' Unhook BOTH events so they don't fire again
            RemoveHandler Me.Shown, AddressOf OnWindowShownWithProject
            RemoveHandler Me.Realized, AddressOf OnWindowRealizedWithProject
            
            ' Check if we have a pending project to load
            If Not String.IsNullOrEmpty(pPendingProjectFile) AndAlso File.Exists(pPendingProjectFile) Then
                Console.WriteLine($"OnWindowShownWithProject: Scheduling project load for: {pPendingProjectFile}")
                
                ' Use idle handler to ensure UI is fully rendered
                GLib.Idle.Add(Function()
                    Console.WriteLine($"OnWindowShownWithProject (Idle): Loading project asynchronously")
                    ' Use the async loading method instead of the synchronous one
                    LoadProjectEnhanced(pPendingProjectFile)
                    pPendingProjectFile = Nothing ' Clear the pending file
                    Return False ' Remove idle handler
                End Function)
            Else
                If String.IsNullOrEmpty(pPendingProjectFile) Then
                    Console.WriteLine("OnWindowShownWithProject: No pending project file")
                ElseIf Not File.Exists(pPendingProjectFile) Then
                    Console.WriteLine($"OnWindowShownWithProject: File doesn't exist: {pPendingProjectFile}")
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowShownWithProject error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
            ShowError("Project Load error", $"Failed To load project: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the Realized event as a backup for loading projects
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub OnWindowRealizedWithProject(sender As Object, e As EventArgs)
        Try
            Console.WriteLine($"OnWindowRealizedWithProject: Called with pending file: {pPendingProjectFile}")
            
            ' Unhook the event so it doesn't fire again
            RemoveHandler Me.Realized, AddressOf OnWindowRealizedWithProject
            
            ' Check if we have a pending project to load AND it hasn't been loaded yet
            If Not String.IsNullOrEmpty(pPendingProjectFile) AndAlso 
               File.Exists(pPendingProjectFile) AndAlso 
               String.IsNullOrEmpty(pCurrentProject) Then
                
                Console.WriteLine($"OnWindowRealizedWithProject: Scheduling project load")
                
                ' Use idle handler to ensure UI is fully rendered
                GLib.Idle.Add(Function()
                    Console.WriteLine($"OnWindowRealizedWithProject (Idle): Loading project now")
                    LoadProjectEnhanced(pPendingProjectFile)
                    pPendingProjectFile = Nothing ' Clear the pending file
                    Return False ' Remove idle handler
                End Function)
            Else
                Console.WriteLine($"OnWindowRealizedWithProject: Not loading - already loaded Or no pending file")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowRealizedWithProject error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Initializes the paned position from settings or defaults
    ''' </summary>
    Private Sub InitializePanedPosition()
        Try
            ' Get saved position from settings, default to 250 pixels for left panel
            Dim lSavedPosition As Integer = LEFT_PANEL_MINIMUM_WIDTH
            If pSettingsManager IsNot Nothing Then
                lSavedPosition = pSettingsManager.GetInteger("LeftPanelWidth", LEFT_PANEL_MINIMUM_WIDTH)
                
                ' Validate the position (must be reasonable)
                If lSavedPosition < LEFT_PANEL_MINIMUM_WIDTH Then lSavedPosition = LEFT_PANEL_MINIMUM_WIDTH
                If lSavedPosition > 500 Then lSavedPosition = 500
            End If
            
            ' Set the paned position
            If pMainHPaned IsNot Nothing Then
                pMainHPaned.Position = lSavedPosition
                Console.WriteLine($"InitializePanedPosition: Set To {lSavedPosition}")
            End If
            
            ' Connect to notify event to save position when changed
            If pMainHPaned IsNot Nothing Then
                AddHandler pMainHPaned.MotionNotifyEvent, AddressOf OnPanedPositionChanged
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializePanedPosition error: {ex.Message}")
        End Try
    End Sub    

    ''' <summary>
    ''' Handles paned position changes to save the setting
    ''' </summary>
    Private Sub OnPanedPositionChanged(vSender As Object, vArgs As EventArgs)
        Try
            ' Use a timer to debounce the saves (only save after user stops dragging)
            If pPanedSaveTimer IsNot Nothing Then
                pPanedSaveTimer.Dispose()
            End If
            
            pPanedSaveTimer = New System.Threading.Timer(
                Sub()
                    Try
                        Application.Invoke(
                            Sub()
                                If pMainHPaned IsNot Nothing AndAlso pSettingsManager IsNot Nothing Then
                                    Dim lPosition As Integer = pMainHPaned.Position
                                    If lPosition > 0 Then ' Only save valid positions
                                        pSettingsManager.SetInteger("LeftPanelWidth", lPosition)
                                        Console.WriteLine($"Saved paned position: {lPosition}")
                                    End If
                                End If
                            End Sub)
                    Catch ex As Exception
                        Console.WriteLine($"error saving paned position: {ex.Message}")
                    End Try
                End Sub,
                Nothing, 500, System.Threading.Timeout.Infinite) ' Save after 500ms of no changes
                
        Catch ex As Exception
            Console.WriteLine($"OnPanedPositionChanged error: {ex.Message}")
        End Try
    End Sub
    
End Class
