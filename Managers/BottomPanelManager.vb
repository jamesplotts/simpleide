' Managers/BottomPanelManager.vb - Manages the bottom panel tabs
Imports Gtk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Managers
    Public Class BottomPanelManager
        
        ' Private fields
        Private pNotebook As CustomDrawNotebook
        Private pCurrentHeight As Integer = 200
        Private pSettingsManager As SettingsManager
        Private pIsVisible As Boolean = False
        Private pProjectRoot As String
        Private pThemeManager As ThemeManager
        
        ' Tab panels
        Private pBuildOutputPanel As BuildOutputPanel
        Private pFindPanel As FindReplacePanel
        Private pTodoPanel As TodoPanel
        Private pAIAssistantPanel As AIAssistantPanel
        Private pHelpViewerPanel As HelpViewerPanel
        Private pGitPanel As GitPanel
        Private pConsoleTextView As TextView
        
        ' Events
        Public Event TabChanged(vTabIndex As Integer)
        Public Event PanelClosed()
        Public Event FindResultSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event TodoSelected(vTodo As TODOItem)
        Public Event BuildErrorWarningSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event SendErrorsToAI(vErrorsText As String)
        Public Event ErrorDoubleClicked(vError As BuildError)
        Public Event HelpTitleChanged(vTitle As String)


        
        ' Properties

        ''' <summary>
        ''' Gets the errors data grid from the build output panel
        ''' </summary>
        ''' <remarks>
        ''' Updated to return CustomDrawDataGrid instead of TreeView
        ''' </remarks>
        Public ReadOnly Property ErrorListView() As CustomDrawDataGrid
            Get
                Return pBuildOutputPanel?.ErrorsDataGrid
            End Get
        End Property

        Public ReadOnly Property BuildOutputPanel As BuildOutputPanel
            Get
                Return pBuildOutputPanel
            End Get
        End Property
        
        Public ReadOnly Property FindPanel As FindReplacePanel
            Get
                Return pFindPanel
            End Get
        End Property
        
        Public ReadOnly Property TodoPanel As TodoPanel
            Get
                Return pTodoPanel
            End Get
        End Property
        
        Public ReadOnly Property AIAssistantPanel As AIAssistantPanel
            Get
                Return pAIAssistantPanel
            End Get
        End Property
        
        Public ReadOnly Property HelpViewerPanel As HelpViewerPanel
            Get
                Return pHelpViewerPanel
            End Get
        End Property
        
        Public ReadOnly Property GitPanel As GitPanel
            Get
                Return pGitPanel
            End Get
        End Property
        
        Public ReadOnly Property ConsoleTextView As TextView
            Get
                Return pConsoleTextView
            End Get
        End Property
        
        Public Property CurrentHeight As Integer
            Get
                Return pCurrentHeight
            End Get
            Set(Value As Integer)
                pCurrentHeight = Value
            End Set
        End Property
        
        Public Property IsVisible As Boolean
            Get
                Return pIsVisible
            End Get
            Set(Value As Boolean)
                pIsVisible = Value
                If Value Then
                    Show()
                Else
                    Hide()
                End If
            End Set
        End Property        
        
        ' Constructor
        Public Sub New(vSettingsManager As SettingsManager)
            pSettingsManager = vSettingsManager
            Initialize()
        End Sub
        
        ''' <summary>
        ''' Initialize the bottom panel with CustomDrawNotebook
        ''' </summary>
        Public Sub Initialize()
            Try
                Console.WriteLine("BottomPanelManager.Initialize: Starting")
                
                ' Create CustomDrawNotebook instead of regular Notebook
                pNotebook = New CustomDrawNotebook()
                
                ' Configure the CustomDrawNotebook
                Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                lCustomNotebook.ShowHidePanelButton = True ' Bottom panel needs hide button
                lCustomNotebook.ShowDropdownButton = False   ' No dropdown needed - all tabs fit
                lCustomNotebook.ShowScrollButtons = False    ' No scroll buttons needed - all tabs fit
                lCustomNotebook.ShowTabCloseButtons = True   ' CHANGED: Show close buttons on tabs
        
                ' Set theme if available
                If pThemeManager IsNot Nothing Then
                    lCustomNotebook.SetThemeManager(pThemeManager)
                End If
                
                ' Wire up CustomDrawNotebook specific events
                AddHandler lCustomNotebook.CurrentTabChanged, AddressOf OnTabSwitched
                AddHandler lCustomNotebook.HidePanelRequested, AddressOf OnHidePanelRequested
                AddHandler lCustomNotebook.TabClosing, AddressOf OnTabClosing
                
                ' Create tabs using CustomDrawNotebook's AppendPage with icons
                Console.WriteLine("  Creating Build Output tab")
                CreateBuildOutputTab()
                Console.WriteLine("  Creating Find Results tab")
                CreateFindResultsTab()
                Console.WriteLine("  Creating Todo List tab")
                CreateTodoListTab()
                Console.WriteLine("  Creating AI Assistant tab")
                CreateAIAssistantTab()
                Console.WriteLine("  Creating Help Viewer tab")
                CreateHelpViewerTab()
                Console.WriteLine("  Creating Git tab")
                CreateGitTab()
                Console.WriteLine("  Creating Console tab")
                CreateConsoleTab()
                
                InitializeEscapeKeyHandling()
        
                ' CRITICAL: Show all tabs to ensure they're visible
                lCustomNotebook.ShowAll()
                
                ' Set the first tab as active
                If lCustomNotebook.NPages > 0 Then
                    Console.WriteLine($"  Setting tab 0 as current")
                    lCustomNotebook.CurrentPage = 0
                End If
                
                ' Initially hide the panel (will be shown when needed)
                pNotebook.Visible = False
                pIsVisible = False
                
                Console.WriteLine($"BottomPanelManager.Initialize: Completed with {lCustomNotebook.NPages} tabs")
                
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.Initialize error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initialize ESC key handling for the panel and all child widgets
        ''' </summary>
        ''' <remarks>
        ''' Sets up key press event handlers on all child panels to hide the bottom panel on ESC
        ''' </remarks>
        Private Sub InitializeEscapeKeyHandling()
            Try
                ' Connect ESC handler to the main notebook
                If pNotebook IsNot Nothing Then
                    AddHandler pNotebook.KeyPressEvent, AddressOf OnPanelKeyPress
                End If
                
                ' Connect to Build Output Panel
                If pBuildOutputPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pBuildOutputPanel)
                End If
                
                ' Connect to Find Panel
                If pFindPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pFindPanel)
                End If
                
                ' Connect to TODO Panel
                If pTodoPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pTodoPanel)
                End If
                
                ' Connect to AI Assistant Panel
                If pAIAssistantPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pAIAssistantPanel)
                End If
                
                ' Connect to Help Viewer Panel
                If pHelpViewerPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pHelpViewerPanel)
                End If
                
                ' Connect to Git Panel
                If pGitPanel IsNot Nothing Then
                    ConnectEscapeHandlerToWidget(pGitPanel)
                End If
                
                ' Connect to Console TextView
                If pConsoleTextView IsNot Nothing Then
                    AddHandler pConsoleTextView.KeyPressEvent, AddressOf OnPanelKeyPress
                End If
                
                Console.WriteLine("BottomPanelManager: ESC key handling initialized")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeEscapeKeyHandling error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Managers.BottomPanelManager.CreateBuildOutputTab
        ''' <summary>
        ''' Creates the Build Output tab with the BuildOutputPanel
        ''' </summary>
        Private Sub CreateBuildOutputTab()
            Try
                Console.WriteLine("CreateBuildOutputTab: Starting")
                
                ' Create the BuildOutputPanel
                pBuildOutputPanel = New BuildOutputPanel(pThemeManager)
                
                ' Wire up events
                AddHandler pBuildOutputPanel.ErrorSelected, AddressOf OnErrorWarningSelected
                AddHandler pBuildOutputPanel.WarningSelected, AddressOf OnErrorWarningSelected
                AddHandler pBuildOutputPanel.SendErrorsToAI, AddressOf OnSendErrorsToAI
                
                ' Set theme if available
                If pThemeManager IsNot Nothing Then
                    pBuildOutputPanel.SetThemeManager(pThemeManager)
                End If
                
                ' Add to notebook using CustomDrawNotebook's AppendPage with icon
                If TypeOf pNotebook Is CustomDrawNotebook Then
                    Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                    Dim lIndex As Integer = lCustomNotebook.AppendPage(pBuildOutputPanel, "Build Output", "build")
                    Console.WriteLine($"  Build Output tab added at index {lIndex}")
                    
                    ' Ensure the panel is visible
                    pBuildOutputPanel.ShowAll()
                Else
                    pNotebook.AppendPage(pBuildOutputPanel, "Build Output")
                End If
                
                Console.WriteLine("CreateBuildOutputTab: Completed")
                
            Catch ex As Exception
                Console.WriteLine($"CreateBuildOutputTab error: {ex.Message}")
            End Try
        End Sub 

        Private Sub OnErrorWarningSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
            RaiseEvent BuildErrorWarningSelected(vFilePath, vLine, vColumn)
        End Sub

        Private Sub OnSendErrorsToAI(vErrorsText As String)
            RaiseEvent SendErrorsToAI(vErrorsText)
        End Sub
        
        Private Function CreateTabLabel(vLabelText As String, vModified As Boolean) As Widget
            Try
                Dim lBox As New Box(Orientation.Horizontal, 5)
                
                ' File name label
                Dim lLabel As New Label()
                lLabel.Text = vLabelText
                lBox.PackStart(lLabel, True, True, 0)
                
                lBox.ShowAll()
                Return lBox
                
            Catch ex As Exception
                Console.WriteLine($"CreateTabLabel error: {ex.Message}")
                Return New Label(vLabelText)
            End Try
        End Function

        ''' <summary>
        ''' Recursively connects ESC handler to a widget and all its children
        ''' </summary>
        ''' <param name="vWidget">Widget to connect handler to</param>
        Private Sub ConnectEscapeHandlerToWidget(vWidget As Widget)
            Try
                If vWidget Is Nothing Then Return
                
                ' Connect to this widget
                AddHandler vWidget.KeyPressEvent, AddressOf OnPanelKeyPress
                
                ' If it's a container, connect to all children
                If TypeOf vWidget Is Container Then
                    Dim lContainer As Container = CType(vWidget, Container)
                    for each lChild As Widget in lContainer.Children
                        ConnectEscapeHandlerToWidget(lChild)
                    Next
                End If
                
                ' Special handling for certain widget types
                If TypeOf vWidget Is TextView Then
                    ' TextViews need special handling as they consume key events
                    Dim lTextView As TextView = CType(vWidget, TextView)
                    AddHandler lTextView.KeyPressEvent, AddressOf OnTextViewKeyPress
                ElseIf TypeOf vWidget Is TreeView Then
                    ' TreeViews also need special handling
                    Dim lTreeView As TreeView = CType(vWidget, TreeView)
                    AddHandler lTreeView.KeyPressEvent, AddressOf OnTreeViewKeyPress
                ElseIf TypeOf vWidget Is Entry Then
                    ' Entry widgets need special handling
                    Dim lEntry As Entry = CType(vWidget, Entry)
                    AddHandler lEntry.KeyPressEvent, AddressOf OnEntryKeyPress
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ConnectEscapeHandlerToWidget error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Main key press handler for panel widgets
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Key press event arguments</param>
        Private Sub OnPanelKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                ' Check if ESC was pressed
                If vArgs.Event.Key = Gdk.Key.Escape Then
                    ' Hide the panel
                    HidePanel()
                    
                    ' Mark event as handled
                    vArgs.RetVal = True
                    
                    Console.WriteLine("BottomPanelManager: ESC pressed - hiding panel")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnPanelKeyPress error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Special handler for TextView widgets to handle ESC
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Key press event arguments</param>
        Private Sub OnTextViewKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                ' Check if ESC was pressed
                If vArgs.Event.Key = Gdk.Key.Escape Then
                    Dim lTextView As TextView = TryCast(vSender, TextView)
                    
                    ' Check if there's a selection - if so, clear it first
                    If lTextView IsNot Nothing Then
                        Dim lBuffer As TextBuffer = lTextView.Buffer
                        If lBuffer.HasSelection Then
                            ' Clear selection
                            Dim lInsert As TextIter = lBuffer.GetIterAtMark(lBuffer.InsertMark)
                            lBuffer.PlaceCursor(lInsert)
                            vArgs.RetVal = True
                            Return
                        End If
                    End If
                    
                    ' No selection or not a TextView - hide panel
                    HidePanel()
                    vArgs.RetVal = True
                    
                    Console.WriteLine("BottomPanelManager: ESC pressed in TextView - hiding panel")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTextViewKeyPress error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Special handler for TreeView widgets to handle ESC
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Key press event arguments</param>
        Private Sub OnTreeViewKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                ' Check if ESC was pressed
                If vArgs.Event.Key = Gdk.Key.Escape Then
                    ' Hide the panel
                    HidePanel()
                    vArgs.RetVal = True
                    
                    Console.WriteLine("BottomPanelManager: ESC pressed in TreeView - hiding panel")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTreeViewKeyPress error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Special handler for Entry widgets to handle ESC
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Key press event arguments</param>
        Private Sub OnEntryKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                ' Check if ESC was pressed
                If vArgs.Event.Key = Gdk.Key.Escape Then
                    Dim lEntry As Entry = TryCast(vSender, Entry)
                    
                    ' Check if entry has text selected
                    If lEntry IsNot Nothing Then
                        Dim lBounds As Integer() = {0, 0}
                        If lEntry.GetSelectionBounds(lBounds(0), lBounds(1)) Then
                            ' Clear selection
                            lEntry.SelectRegion(0, 0)
                            vArgs.RetVal = True
                            Return
                        End If
                    End If
                    
                    ' No selection - hide panel
                    HidePanel()
                    vArgs.RetVal = True
                    
                    Console.WriteLine("BottomPanelManager: ESC pressed in Entry - hiding panel")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnEntryKeyPress error: {ex.Message}")
            End Try
        End Sub
        
        ' Update build output - PUBLIC METHOD
        Public Sub UpdateBuildOutput(vText As String, vIsError As Boolean)
            Try
                If pBuildOutputPanel IsNot Nothing Then
                    ' FIXED: Use Function delegate instead of Action
                    GLib.Idle.Add(Function()
                        ' FIXED: Use AppendOutput method that exists in BuildOutputPanel
                        pBuildOutputPanel.AppendOutput(vText, If(vIsError, "error", "normal"))
                        Return False
                    End Function)
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateBuildOutput error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the TODO list tab with icon
        ''' </summary>
        Private Sub CreateTodoListTab()
            Try
                pTodoPanel = New TodoPanel()
                
                AddHandler pTodoPanel.TodoSelected, 
                    Sub(vTodo As TODOItem)
                        RaiseEvent TodoSelected(vTodo)
                    End Sub
                
                ' Add to notebook with icon
                pNotebook.AppendPage(pTodoPanel, "TODO List", "list-checks")
                
            Catch ex As Exception
                Console.WriteLine($"CreateTodoListTab error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the AI Assistant tab with icon
        ''' </summary>
        Private Sub CreateAIAssistantTab()
            Try
                Dim lApiKey As String = If(pSettingsManager IsNot Nothing, 
                    pSettingsManager.GetString("AI.ApiKey", ""), "")
                    
                pAIAssistantPanel = New AIAssistantPanel(lApiKey)    
                            
                ' Add to notebook with icon
                pNotebook.AppendPage(pAIAssistantPanel, "AI Assistant", "bot")
                
            Catch ex As Exception
                Console.WriteLine($"CreateAIAssistantTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Update selected tab - PUBLIC METHOD
        Public Sub UpdateSelectedTab(vText As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pNotebook IsNot Nothing AndAlso pNotebook.CurrentPage >= 0 Then
                        ' Update the current tab based on its type
                        Select Case pNotebook.CurrentPage
                            Case 0 ' Build output
                                If pBuildOutputPanel IsNot Nothing Then
                                    ' FIXED: Use AppendOutput method
                                    pBuildOutputPanel.AppendOutput(vText, "normal")
                                End If
                            Case 6 ' Console
                                If pConsoleTextView IsNot Nothing Then
                                    AppendToConsole(vText)
                                End If
                        End Select
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateSelectedTab error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Help Viewer tab with icon
        ''' </summary>
        Private Sub CreateHelpViewerTab()
            Try
                pHelpViewerPanel = New HelpViewerPanel()
                
                AddHandler pHelpViewerPanel.TitleChanged,
                    Sub(vTitle As String)
                        OnHelpTitleChanged(vTitle)
                    End Sub
                
                ' Add to notebook with icon
                pNotebook.AppendPage(pHelpViewerPanel, "Help", "help-circle")
                
            Catch ex As Exception
                Console.WriteLine($"CreateHelpViewerTab error: {ex.Message}")
            End Try
        End Sub      
          
        ' Update help title - PUBLIC METHOD
        Public Sub UpdateHelpTitle(vTitle As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pHelpViewerPanel IsNot Nothing Then
                        ' Update the help viewer title
                        RaiseEvent HelpTitleChanged(vTitle)
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateHelpTitle error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Git tab with icon
        ''' </summary>
        Private Sub CreateGitTab()
            Try
                pGitPanel = New GitPanel()
                
                ' Add to notebook with icon
                pNotebook.AppendPage(pGitPanel, "Git", "git-branch")
                
            Catch ex As Exception
                Console.WriteLine($"CreateGitTab error: {ex.Message}")
            End Try
        End Sub

        ' Set project root
        Public Sub SetProjectRoot(vProjectRoot As String)
            Try
                If Not String.IsNullOrEmpty(vProjectRoot) Then
                    pProjectRoot = vProjectRoot
                    pGitPanel.ProjectRoot = System.IO.Path.GetDirectoryName(vProjectRoot)
                End If
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.SetProjectRoot error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Console tab with icon
        ''' </summary>
        Private Sub CreateConsoleTab()
            Try
                Dim lScrolledWindow As New ScrolledWindow()
                lScrolledWindow.VscrollbarPolicy = PolicyType.Automatic
                lScrolledWindow.HscrollbarPolicy = PolicyType.Never
                
                pConsoleTextView = New TextView()
                pConsoleTextView.Editable = False
                pConsoleTextView.CursorVisible = False
                pConsoleTextView.WrapMode = WrapMode.Word
                
                ' Apply monospace font
                Dim lFontDesc As Pango.FontDescription = Pango.FontDescription.FromString("Monospace 10")
                pConsoleTextView.OverrideFont(lFontDesc)
                
                lScrolledWindow.Add(pConsoleTextView)
                
                ' Add to notebook with icon
                pNotebook.AppendPage(lScrolledWindow, "Console", "terminal")
                
            Catch ex As Exception
                Console.WriteLine($"CreateConsoleTab error: {ex.Message}")
            End Try
        End Sub
        
        ' ADDITIONAL PUBLIC METHODS that might be causing errors
        
        ' Process build complete
        Public Sub ProcessBuildComplete(vSuccess As Boolean)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pBuildOutputPanel IsNot Nothing Then
                        Dim lMessage As String = If(vSuccess, "Build completed successfully", "Build failed")
                        ' FIXED: Use AppendOutput method
                        pBuildOutputPanel.AppendOutput(lMessage & Environment.NewLine, If(vSuccess, "Success", "error"))
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"ProcessBuildComplete error: {ex.Message}")
            End Try
        End Sub
        
        ' Update find results
        Public Sub UpdateFindResults(vResults As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pFindPanel IsNot Nothing Then
                        ' Update find panel with results
                        ' This would need to be implemented in FindReplacePanel
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateFindResults error: {ex.Message}")
            End Try
        End Sub
        
        ' Update git status
        Public Sub UpdateGitStatus(vStatus As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pGitPanel IsNot Nothing Then
                        ' Update git panel status
                        ' This would need to be implemented in GitPanel
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateGitStatus error: {ex.Message}")
            End Try
        End Sub
        


        Public Sub ShowConsole()
            ShowTab(6)
        End Sub
        
        ''' <summary>
        ''' Show specific tab by index
        ''' </summary>
        ''' <param name="vTabIndex">Index of the tab to show</param>
        Public Sub ShowTab(vTabIndex As Integer)
            Try
                Console.WriteLine($"ShowTab called with index: {vTabIndex}")
                
                If vTabIndex >= 0 AndAlso vTabIndex < pNotebook.NPages Then
                    ' First make sure the panel is visible
                    If Not pIsVisible Then
                        Show()
                    End If
                    
                    ' Set the current page
                    pNotebook.CurrentPage = vTabIndex
                    
                    ' For CustomDrawNotebook, set the tab without triggering scroll
                    If TypeOf pNotebook Is CustomDrawNotebook Then
                        Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                        
                        ' Use False for vEnsureVisible to prevent scrolling
                        ' The tabs should already be at scroll position 0 from UpdateTabBounds
                        lCustomNotebook.SetCurrentTab(vTabIndex, False)
                    End If
                    
                    ' Make sure the panel is visible
                    IsVisible = True
                    
                    Console.WriteLine($"ShowTab: Switched to tab {vTabIndex}")
                Else
                    Console.WriteLine($"ShowTab: Invalid tab index {vTabIndex} (NPages={pNotebook.NPages})")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ShowTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Show tab by type
        Public Sub ShowTabByType(vTabType As BottomPanelTab)
            Try
                Dim lIndex As Integer = CInt(vTabType)
                ShowTab(lIndex)
            Catch ex As Exception
                Console.WriteLine($"ShowTabByType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Hide the panel and notify listeners
        ''' </summary>
        ''' <remarks>
        ''' Enhanced to properly handle focus return when hiding via ESC key
        ''' </remarks>
        Public Sub HidePanel()
            Try
                IsVisible = False
                RaiseEvent PanelClosed()
            Catch ex As Exception
                Console.WriteLine($"HidePanel error: {ex.Message}")
            End Try
        End Sub

        Public Sub ToggleVisible()
            If IsVisible Then 
                HidePanel()
            Else
                ShowTab(pNotebook.CurrentPage)
            End If
        End Sub
        
        ' Clear console
        Public Sub ClearConsole()
            Try
                If pConsoleTextView IsNot Nothing Then
                    pConsoleTextView.Buffer.Clear()
                End If
            Catch ex As Exception
                Console.WriteLine($"ClearConsole error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Append text to console with thread-safe TextIter handling
        ''' </summary>
        ''' <param name="vText">Text to append to the console</param>
        Public Sub AppendToConsole(vText As String)
            Try
                If pConsoleTextView IsNot Nothing AndAlso pConsoleTextView.Buffer IsNot Nothing Then
                    ' Use GLib.Idle to ensure we're on the UI thread
                    GLib.Idle.Add(Function()
                        Try
                            Dim lBuffer As TextBuffer = pConsoleTextView.Buffer
                            
                            ' Get end iter, place cursor, and insert in one operation
                            Dim lEndIter As TextIter = lBuffer.EndIter
                            lBuffer.PlaceCursor(lEndIter)
                            lBuffer.InsertAtCursor(vText)
                            
                            ' Create a mark at the end for scrolling
                            ' Marks persist across buffer modifications unlike iterators
                            Dim lEndMark As TextMark = lBuffer.CreateMark(Nothing, lBuffer.EndIter, False)
                            pConsoleTextView.ScrollToMark(lEndMark, 0.0, False, 0.0, 0.0)
                            lBuffer.DeleteMark(lEndMark)
                            
                        Catch ex As Exception
                            Console.WriteLine($"AppendToConsole inner error: {ex.Message}")
                        End Try
                        Return False ' Remove from idle queue
                    End Function)
                End If
            Catch ex As Exception
                Console.WriteLine($"AppendToConsole error: {ex.Message}")
            End Try
        End Sub
        
        ' Update project root for panels that need it
        Public Sub UpdateProjectRoot(vProjectRoot As String)
            Try
                pFindPanel?.SetProjectRoot(vProjectRoot)
                pTodoPanel?.SetProjectRoot(vProjectRoot)
                pGitPanel?.SetProjectRoot(vProjectRoot)
            Catch ex As Exception
                Console.WriteLine($"UpdateProjectRoot error: {ex.Message}")
            End Try
        End Sub
        
        ' Event handler for tab switching
        Private Sub OnTabSwitched(vSender As Object, vArgs As SwitchPageArgs)
            Try
                RaiseEvent TabChanged(CInt(vArgs.PageNum))
                
                ' Focus appropriate widget based on tab
                Select Case vArgs.PageNum
                    Case 0 ' Build output
                        pBuildOutputPanel?.GrabFocus()
                    Case 1 ' Find Results
                        pFindPanel?.GrabFocus()
                    Case 2 ' TODO List
                        pTodoPanel?.GrabFocus()
                    Case 3 ' AI Assistant
                        pAIAssistantPanel?.GrabFocus()
                    Case 4 ' Help Viewer
                        pHelpViewerPanel?.GrabFocus()
                    Case 5 ' git
                        pGitPanel?.GrabFocus()
                    Case 6 ' Console
                        pConsoleTextView?.GrabFocus()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnTabSwitched error: {ex.Message}")
            End Try
        End Sub
        
        ' Copy errors to clipboard
        Public Sub CopyErrorsToClipboard()
            If pBuildOutputPanel IsNot Nothing Then
                pBuildOutputPanel.CopyErrorsToClipboard()
            End If
        End Sub
        
        ' Enum for tab types
        Public Enum BottomPanelTab
            eBuildOutput = 0
            eFindResults = 1
            eTodoList = 2
            eAIAssistant = 3
            eHelpViewer = 4
            eGit = 5
            eConsole = 6
        End Enum

        Private Sub OnHelpTitleChanged(vTitle As String)
            Try
                ' Update window title or status bar with help title
                RaiseEvent HelpTitleChanged(vTitle)
                Console.WriteLine($"Help Title changed: {vTitle}")
            Catch ex As Exception
                Console.WriteLine($"OnHelpTitleChanged error: {ex.Message}")
            End Try
        End Sub

        ' Get the widget to add to the UI (for packing into paned)
        Public Function GetWidget() As Widget
            Return pNotebook
        End Function
        
        ' Set NoShowAll property for initialization
        Public Sub SetNoShowAll(vValue As Boolean)
            If pNotebook IsNot Nothing Then
                pNotebook.NoShowAll = vValue
            End If
        End Sub
        
        ' Show the notebook widget
        Public Sub Show()
            If pNotebook IsNot Nothing Then
                pNotebook.NoShowAll = False
                pNotebook.Show()
            End If
        End Sub
        
        ' Hide the notebook widget
        Public Sub Hide()
            If pNotebook IsNot Nothing Then
                pNotebook.Hide()
                pNotebook.NoShowAll = True
            End If
        End Sub
        
        ' Find tab index for a specific panel widget
        Public Function GetTabIndexForPanel(vPanel As Widget) As Integer
            Try
                If pNotebook IsNot Nothing AndAlso vPanel IsNot Nothing Then
                    for i As Integer = 0 To pNotebook.NPages - 1
                        If pNotebook.GetNthPage(i) Is vPanel Then
                            Return i
                        End If
                    Next
                End If
                Return -1
            Catch ex As Exception
                Console.WriteLine($"GetTabIndexForPanel error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ' Show tab containing specific panel
        Public Sub ShowTabForPanel(vPanel As Widget)
            Try
                Dim lIndex As Integer = GetTabIndexForPanel(vPanel)
                If lIndex >= 0 Then
                    ShowTab(lIndex)
                End If
            Catch ex As Exception
                Console.WriteLine($"ShowTabForPanel error: {ex.Message}")
            End Try
        End Sub
        
        ' Update tab label text
        Public Sub SetTabLabelText(vPanel As Widget, vText As String)
            Try
                If pNotebook IsNot Nothing AndAlso vPanel IsNot Nothing Then
                    Dim lPageNum As Integer = pNotebook.PageNum(vPanel)
                    If lPageNum >= 0 Then
                        pNotebook.SetTabLabelText(vPanel, vText)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"SetTabLabelText error: {ex.Message}")
            End Try
        End Sub
        
        ' Get current page index
        Public Function GetCurrentPageIndex() As Integer
            If pNotebook IsNot Nothing Then
                Return pNotebook.CurrentPage
            End If
            Return -1
        End Function
        

        ' Toggle output panel visibility
        Public Sub ToggleOutputPanel()
            Try
                If pIsVisible Then
                    If pNotebook.CurrentPage = CInt(BottomPanelTab.eBuildOutput) Then
                        HidePanel()
                    Else
                        ShowTab(BottomPanelTab.eBuildOutput)
                    End If
                Else
                    Show()
                    ShowTab(BottomPanelTab.eBuildOutput)
                End If
            Catch ex As Exception
                Console.WriteLine($"ToggleOutputPanel error: {ex.Message}")
            End Try
        End Sub

        ' Toggle error list panel visibility
        Public Sub ToggleErrorListPanel()
            Try
                If pIsVisible Then
                    If pNotebook.CurrentPage = CInt(BottomPanelTab.eBuildOutput) Then
                        HidePanel()
                    Else
                        ShowTab(BottomPanelTab.eBuildOutput)
                    End If
                Else
                    Show()
                    ShowTab(BottomPanelTab.eBuildOutput)
                End If
            Catch ex As Exception
                Console.WriteLine($"ToggleErrorListPanel error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates the find results tab with icon
        ''' </summary>
        Private Sub CreateFindResultsTab()
            Try
                pFindPanel = New FindReplacePanel()
                
                AddHandler pFindPanel.ResultSelected, 
                    Sub(vPath As String, vLine As Integer, vCol As Integer)
                        RaiseEvent FindResultSelected(vPath, vLine, vCol)
                    End Sub
                
                AddHandler pFindPanel.CloseRequested, 
                    Sub()
                        HidePanel()
                    End Sub
                
                ' Add to notebook with icon
                Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                lCustomNotebook.AppendPage(pFindPanel, "Find/Replace", "search")
                
            Catch ex As Exception
                Console.WriteLine($"CreateFindResultsTab error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if any child widget of the bottom panel has focus
        ''' </summary>
        ''' <returns>True if a child widget has focus, False otherwise</returns>
        Public Function IsChildWidgetFocused() As Boolean
            Try
                ' Get the currently focused widget
                Dim lFocusedWidget As Widget = Window.ListToplevels()(0)?.Focus
                If lFocusedWidget Is Nothing Then Return False
                
                ' Check if the focused widget is a descendant of our notebook
                Dim lParent As Widget = lFocusedWidget
                While lParent IsNot Nothing
                    If lParent Is pNotebook Then
                        Return True
                    End If
                    lParent = lParent.Parent
                End While
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsChildWidgetFocused error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Sets the ThemeManager for all panels that need theme support
        ''' </summary>
        ''' <param name="vThemeManager">The ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                
                ' Pass to the CustomDrawNotebook
                If pNotebook IsNot Nothing AndAlso TypeOf pNotebook Is CustomDrawNotebook Then
                    DirectCast(pNotebook, CustomDrawNotebook).SetThemeManager(vThemeManager)
                End If
                
                ' Pass to BuildOutputPanel if it exists
                If pBuildOutputPanel IsNot Nothing Then
                    pBuildOutputPanel.SetThemeManager(vThemeManager)
                End If
                
                If pGitPanel IsNot Nothing Then
                    pGitPanel.SetThemeManager(vThemeManager)
                End If
                
                ' Pass to other panels that might need it
                ' (Future: other panels can get theme support too)
                
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.SetThemeManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the hide panel request from CustomDrawNotebook
        ''' </summary>
        Private Sub OnHidePanelRequested()
            Try
                HidePanel()
                Console.WriteLine("Bottom panel hide requested from CustomDrawNotebook")
            Catch ex As Exception
                Console.WriteLine($"OnHidePanelRequested error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles tab switching in CustomDrawNotebook
        ''' </summary>
        ''' <param name="vOldIndex">Previous tab index</param>
        ''' <param name="vNewIndex">New tab index</param>
        Private Sub OnTabSwitched(vOldIndex As Integer, vNewIndex As Integer)
            Try
                Console.WriteLine($"Bottom panel tab switched from {vOldIndex} to {vNewIndex}")
                RaiseEvent TabChanged(vNewIndex)
                
                ' Special handling for specific tabs
                Select Case vNewIndex
                    Case 0 ' Build Output
                        ' Focus on the output text view if available
                        If pBuildOutputPanel IsNot Nothing Then
                            ' Switch to output sub-tab within BuildOutputPanel
                            pBuildOutputPanel.SwitchToOutputTab()
                        End If
                        
                    Case 6 ' Console
                        ' Auto-scroll to bottom when switching to console
                        If pConsoleTextView IsNot Nothing Then
                            Dim lBuffer As TextBuffer = pConsoleTextView.Buffer
                            Dim lEndIter As TextIter = lBuffer.EndIter
                            pConsoleTextView.ScrollToIter(lEndIter, 0, False, 0, 0)
                        End If
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnTabSwitched error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles tab closing requests from CustomDrawNotebook
        ''' </summary>
        ''' <param name="vSender">The CustomDrawNotebook raising the event</param>
        ''' <param name="vArgs">Event arguments containing tab information</param>
        Private Sub OnTabClosing(vSender As Object, vArgs As TabClosingEventArgs)
            Try
                Console.WriteLine($"BottomPanelManager.OnTabClosing: Tab '{vArgs.TabLabel}' at index {vArgs.TabIndex} close button clicked")
                
                ' For the bottom panel, we want to hide the entire panel instead of removing individual tabs
                ' So we mark the event as handled and hide the panel
                vArgs.Handled = True  ' Tell CustomDrawNotebook we handled it - don't remove the tab
                vArgs.Cancel = False  ' We're not canceling, just handling it differently
                
                ' Hide the entire bottom panel
                HidePanel()
                
                ' Raise the PanelClosed event so MainWindow knows the panel was closed
                RaiseEvent PanelClosed()
                
                Console.WriteLine("BottomPanelManager: Hiding panel via tab close button")
                
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.OnTabClosing error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the underlying notebook widget
        ''' </summary>
        ''' <returns>The CustomDrawNotebook instance</returns>
        Public Function GetNotebook() As CustomDrawNotebook
            Return pNotebook
        End Function

    End Class

End Namespace
