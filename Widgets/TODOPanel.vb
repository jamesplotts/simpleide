' Widgets/TODOPanel.vb - Enhanced TODO panel with filtering, priorities, and AI integration
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets
    Public Class TodoPanel
        Inherits Box
        
        ' Private fields - UI
        Private pToolbar As Box
        Private pFilterToolbar As Box
        Private pListBox As CustomDrawListBox
        Private pContextMenu As Menu
        Private pStatusBar As Label
        Private pThemeManager As ThemeManager


        ' Filter controls
        Private pSearchEntry As CustomDrawTextBox
        Private pPriorityCombo As CustomDrawComboBox
        Private pCategoryCombo As CustomDrawComboBox
        Private pStatusCombo As CustomDrawComboBox
        Private pOverdueToggle As CustomDrawToggleButton
        Private pRefreshButton As CustomDrawButton
        Private pAddButton As CustomDrawButton

        ' Comment-tag visibility toggles (TODO/FIXED/NOTE/Manual)
        Private pShowTodoToggle As CustomDrawToggleButton
        Private pShowFixedToggle As CustomDrawToggleButton
        Private pShowNoteToggle As CustomDrawToggleButton
        Private pShowManualToggle As CustomDrawToggleButton

        ' Whole Project / Current File Only scope toggle
        Private pScopeToggle As CustomDrawToggleButton

        ' Private fields - Data
        Private pTODOManager As TODOManager = Nothing
        Private pProjectRoot As String = ""
        Private pCurrentFilePath As String = ""
        Private pAllTODOs As New List(Of TODOItem)
        Private pFilteredTODOs As New List(Of TODOItem)
        Private pSelectedTODO As TODOItem
        
        ' Events
        Public Event TodoSelected(vTODO As TODOItem)
        Public Event TODODoubleClicked(vTODO As TODOItem)
        Public Event SendToAI(vTODO As TODOItem)
        Public Event NavigateToCode(vFilePath As String, vLine As Integer, vColumn As Integer)
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            Initialize()
        End Sub
        
        Private Sub Initialize()
            Try
                BuildUI()
                CreateContextMenu()
                ConnectEvents()
                
                ' Show empty state initially
                UpdateStatusBar()
                ShowEmptyState()
                ShowAll()
            #If DEBUG Then
            Console.WriteLine($"TODO Intialized")
            #End If
            Catch ex As Exception
                Console.WriteLine($"error initializing TodoPanel: {ex.Message}")
            End Try
        End Sub
        
        Private Sub BuildUI()
            #If DEBUG Then
            Console.WriteLine($"TodoPanel.vb - BuildUI()")
            #End If
            ' Main toolbar row - a plain Box holding CustomDrawButtons rather than a
            ' Gtk.Toolbar, since CustomDrawButton isn't a ToolItem
            pToolbar = New Box(Orientation.Horizontal, 4)
            pToolbar.MarginStart = 4
            pToolbar.MarginTop = 3
            pToolbar.MarginBottom = 3

            pAddButton = New CustomDrawButton("Add", LoadIconByName("list-add-symbolic"))
            pAddButton.TooltipText = "Add New TODO item"
            pToolbar.PackStart(pAddButton, False, False, 0)

            pRefreshButton = New CustomDrawButton("Refresh", LoadIconByName("view-refresh-symbolic"))
            pRefreshButton.TooltipText = "Refresh TODO list and scan codebase"
            pToolbar.PackStart(pRefreshButton, False, False, 0)

            pToolbar.PackStart(New Separator(Orientation.Vertical), False, False, 4)

            ' Search entry
            pSearchEntry = New CustomDrawTextBox("Search TODOs...")
            pSearchEntry.WidthRequest = 150
            pToolbar.PackStart(pSearchEntry, False, False, 0)

            ' Priority filter
            pPriorityCombo = New CustomDrawComboBox()
            pPriorityCombo.AppendText("All Priorities")
            pPriorityCombo.AppendText("Critical")
            pPriorityCombo.AppendText("High")
            pPriorityCombo.AppendText("Medium")
            pPriorityCombo.AppendText("Low")
            pPriorityCombo.Active = 0
            pPriorityCombo.WidthRequest = 120
            pToolbar.PackStart(pPriorityCombo, False, False, 0)

            ' Category filter
            pCategoryCombo = New CustomDrawComboBox()
            pCategoryCombo.AppendText("All Categories")
            pCategoryCombo.AppendText("Bug")
            pCategoryCombo.AppendText("Feature")
            pCategoryCombo.AppendText("documentation")
            pCategoryCombo.AppendText("Refactor")
            pCategoryCombo.AppendText("Testing")
            pCategoryCombo.AppendText("Performance")
            pCategoryCombo.AppendText("Security")
            pCategoryCombo.AppendText("UI/UX")
            pCategoryCombo.AppendText("Other")
            pCategoryCombo.Active = 0
            pCategoryCombo.WidthRequest = 120
            pToolbar.PackStart(pCategoryCombo, False, False, 0)

            ' Status filter
            pStatusCombo = New CustomDrawComboBox()
            pStatusCombo.AppendText("All Status")
            pStatusCombo.AppendText("Pending")
            pStatusCombo.AppendText("in Progress")
            pStatusCombo.AppendText("Completed")
            pStatusCombo.AppendText("Cancelled")
            pStatusCombo.Active = 0
            pStatusCombo.WidthRequest = 120
            pToolbar.PackStart(pStatusCombo, False, False, 0)

            ' Overdue toggle
            pOverdueToggle = New CustomDrawToggleButton("", LoadIconByName("alarm-symbolic"))
            pOverdueToggle.TooltipText = "Show only overdue items"
            pToolbar.PackStart(pOverdueToggle, False, False, 0)

            ' Second toolbar row: comment-tag visibility toggles and view scope
            pFilterToolbar = New Box(Orientation.Horizontal, 4)
            pFilterToolbar.MarginStart = 4
            pFilterToolbar.MarginBottom = 3

            pShowTodoToggle = New CustomDrawToggleButton("TODO")
            pShowTodoToggle.Active = True
            pShowTodoToggle.TooltipText = "Show items from ' TODO:' comments"
            pFilterToolbar.PackStart(pShowTodoToggle, False, False, 0)

            pShowFixedToggle = New CustomDrawToggleButton("FIXED")
            pShowFixedToggle.Active = True
            pShowFixedToggle.TooltipText = "Show items from ' FIXED:' comments"
            pFilterToolbar.PackStart(pShowFixedToggle, False, False, 0)

            pShowNoteToggle = New CustomDrawToggleButton("NOTE")
            pShowNoteToggle.Active = True
            pShowNoteToggle.TooltipText = "Show items from ' NOTE:' comments"
            pFilterToolbar.PackStart(pShowNoteToggle, False, False, 0)

            pShowManualToggle = New CustomDrawToggleButton("Manual")
            pShowManualToggle.Active = True
            pShowManualToggle.TooltipText = "Show manually-added tasks"
            pFilterToolbar.PackStart(pShowManualToggle, False, False, 0)

            pFilterToolbar.PackStart(New Separator(Orientation.Vertical), False, False, 4)

            pScopeToggle = New CustomDrawToggleButton("Current File Only")
            pScopeToggle.Active = False
            pScopeToggle.TooltipText = "Show only TODOs from the currently active file"
            pFilterToolbar.PackStart(pScopeToggle, False, False, 0)

            ' Custom-drawn list (owns its own DrawingArea + Scrollbar internally)
            CreateListBox()

            ' Status bar
            pStatusBar = New Label("Ready")
            pStatusBar.Halign = Align.Start
            pStatusBar.MarginStart = 6
            pStatusBar.MarginEnd = 6
            pStatusBar.MarginTop = 3
            pStatusBar.MarginBottom = 3

            ' Pack components
            PackStart(pToolbar, False, False, 0)
            PackStart(pFilterToolbar, False, False, 0)
            PackStart(New Separator(Orientation.Horizontal), False, False, 0)
            PackStart(pListBox, True, True, 0)
            PackStart(pStatusBar, False, False, 0)

            'ShowAll()
        End Sub

        
        ''' <summary>
        ''' Creates the custom-drawn list that replaces the old GTK TreeView. Each
        ''' ListBoxItem's Data holds the TODOItem's Id for real rows, or Nothing for file/
        ''' "Manual Tasks" group header rows (IsGroupHeader = True), which
        ''' CustomDrawListBox already excludes from selection/activation/context-menu
        ''' events on its own.
        ''' </summary>
        Private Sub CreateListBox()
            #If DEBUG Then
            Console.WriteLine($"TodoPanel.vb - CreateListBox()")
            #End If
            pListBox = New CustomDrawListBox()

            AddHandler pListBox.SelectionChanged, AddressOf OnListBoxSelectionChanged
            AddHandler pListBox.ItemDoubleClicked, AddressOf OnListBoxItemDoubleClicked
            AddHandler pListBox.ContextMenuRequested, AddressOf OnListBoxContextMenuRequested
        End Sub
        
        Private Sub CreateContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Edit (for manual TODOs)
                Dim lEditItem As New MenuItem("Edit TODO")
                AddHandler lEditItem.Activated, AddressOf OnEditTODO
                pContextMenu.Append(lEditItem)
                
                ' Mark as completed (for manual TODOs)
                Dim lCompleteItem As New MenuItem("Mark as Completed")
                AddHandler lCompleteItem.Activated, AddressOf OnMarkCompleted
                pContextMenu.Append(lCompleteItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Send to AI
                Dim lSendToAIItem As New MenuItem("Send to AI Assistant")
                AddHandler lSendToAIItem.Activated, AddressOf OnSendToAI
                pContextMenu.Append(lSendToAIItem)
                
                ' Navigate to source (for code TODOs)
                Dim lNavigateItem As New MenuItem("Go to Source")
                AddHandler lNavigateItem.Activated, AddressOf OnNavigateToSource
                pContextMenu.Append(lNavigateItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Delete (for manual TODOs)
                Dim lDeleteItem As New MenuItem("Delete TODO")
                AddHandler lDeleteItem.Activated, AddressOf OnDeleteTODO
                pContextMenu.Append(lDeleteItem)
                
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error creating Context menu: {ex.Message}")
            End Try
        End Sub

        Protected Overrides Sub OnShown()
            MyBase.OnShown()
            #If DEBUG Then
            Console.WriteLine($"TODO OnShown")
            #End If
        End Sub
        
        Private Sub ConnectEvents()
            Try
                ' Connect toolbar events
                AddHandler pAddButton.Clicked, AddressOf OnAddTODO
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshTODOs
                
                ' Connect filter events
                AddHandler pSearchEntry.Changed, AddressOf OnFilterChanged
                AddHandler pPriorityCombo.Changed, AddressOf OnFilterChanged
                AddHandler pCategoryCombo.Changed, AddressOf OnFilterChanged
                AddHandler pStatusCombo.Changed, AddressOf OnFilterChanged
                AddHandler pOverdueToggle.Toggled, AddressOf OnFilterChanged
                AddHandler pShowTodoToggle.Toggled, AddressOf OnFilterChanged
                AddHandler pShowFixedToggle.Toggled, AddressOf OnFilterChanged
                AddHandler pShowNoteToggle.Toggled, AddressOf OnFilterChanged
                AddHandler pShowManualToggle.Toggled, AddressOf OnFilterChanged
                AddHandler pScopeToggle.Toggled, AddressOf OnFilterChanged

            Catch ex As Exception
                Console.WriteLine($"error connecting events: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles double-click on a list row - group header rows never reach here since
        ''' CustomDrawListBox toggles their expand state instead of raising this event
        ''' </summary>
        Private Sub OnListBoxItemDoubleClicked(vIndex As Integer, vItem As ListBoxItem)
            Try
                Dim lId As String = TryCast(vItem?.Data, String)
                If String.IsNullOrEmpty(lId) Then Return

                Dim lTODO As TODOItem = pAllTODOs.FirstOrDefault(Function(t) t.Id = lId)
                If lTODO IsNot Nothing Then
                    RaiseEvent TODODoubleClicked(lTODO)
                End If

            Catch ex As Exception
                Console.WriteLine($"error handling item activation: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles a right-click context menu request - CustomDrawListBox only raises this
        ''' for real item rows (already selected via SelectedIndex by the time this fires),
        ''' never for group header rows
        ''' </summary>
        Private Sub OnListBoxContextMenuRequested(vIndex As Integer, vItem As ListBoxItem, vEvent As Gdk.EventButton)
            Try
                If pSelectedTODO Is Nothing Then Return

                UpdateContextMenuForTODO()
                pContextMenu.ShowAll()
                pContextMenu.PopupAtPointer(vEvent)

            Catch ex As Exception
                Console.WriteLine($"error handling context menu request: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateContextMenuForTODO()
            Try
                If pSelectedTODO Is Nothing Then Return
                
                ' Get menu items (assuming order matches creation)
                Dim lItems As Widget() = pContextMenu.Children
                If lItems.Length >= 7 Then
                    ' Edit - only for manual TODOs
                    lItems(0).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual)
                    
                    ' Mark as completed - only for manual TODOs that aren't already completed
                    lItems(1).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual AndAlso 
                                         pSelectedTODO.Status <> TODOItem.eStatus.eCompleted)
                    
                    ' Navigate to source - only for code TODOs
                    lItems(4).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eCodeComment)
                    
                    ' Delete - only for manual TODOs
                    lItems(6).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Context menu: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles list selection changes - a Nothing/non-matching Data means the
        ''' selection landed on a group header row (reachable via keyboard navigation,
        ''' since arrow keys don't skip header rows) rather than a real TODO item
        ''' </summary>
        Private Sub OnListBoxSelectionChanged(vIndex As Integer, vItem As ListBoxItem)
            Try
                Dim lId As String = TryCast(vItem?.Data, String)

                If Not String.IsNullOrEmpty(lId) Then
                    pSelectedTODO = pAllTODOs.FirstOrDefault(Function(t) t.Id = lId)
                    If pSelectedTODO IsNot Nothing Then
                        RaiseEvent TodoSelected(pSelectedTODO)
                    End If
                Else
                    pSelectedTODO = Nothing
                End If

            Catch ex As Exception
                Console.WriteLine($"error handling selection change: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAddTODO(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New TODOEditDialog(Nothing, "Add TODO", pThemeManager)
                lDialog.TransientFor = CType(Me.Toplevel, Window)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.AddTODO(lDialog.TODOTitle, lDialog.TODODescription, lDialog.TODOPriority, lDialog.TODOCategory)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error adding TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnEditTODO(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                Dim lDialog As New TODOEditDialog(pSelectedTODO, "Edit TODO", pThemeManager)
                lDialog.TransientFor = CType(Me.Toplevel, Window)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    ' Update the TODO
                    pSelectedTODO.Title = lDialog.TODOTitle
                    pSelectedTODO.Description = lDialog.TODODescription
                    pSelectedTODO.Priority = lDialog.TODOPriority
                    pSelectedTODO.Category = lDialog.TODOCategory
                    pSelectedTODO.Status = lDialog.TODOStatus
                    pSelectedTODO.DueDate = lDialog.TODODueDate
                    pSelectedTODO.Progress = lDialog.TODOProgress
                    
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.UpdateTODO(pSelectedTODO)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error editing TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnMarkCompleted(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                pSelectedTODO.Status = TODOItem.eStatus.eCompleted
                pSelectedTODO.CompletedDate = DateTime.Now
                pSelectedTODO.Progress = 100
                
                If pTODOManager IsNot Nothing Then
                    pTODOManager.UpdateTODO(pSelectedTODO)
                    RefreshTODOs()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error marking TODO completed: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSendToAI(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO IsNot Nothing Then
                    RaiseEvent SendToAI(pSelectedTODO)
                End If
            Catch ex As Exception
                Console.WriteLine($"error sending TODO to AI: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnNavigateToSource(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO IsNot Nothing AndAlso pSelectedTODO.SourceType = TODOItem.eSourceType.eCodeComment Then
                    RaiseEvent NavigateToCode(pSelectedTODO.SourceFile, pSelectedTODO.SourceLine, pSelectedTODO.SourceColumn)
                End If
            Catch ex As Exception
                Console.WriteLine($"error navigating to source: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDeleteTODO(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                Dim lDialog As New MessageDialog(
                    CType(Me.Toplevel, Window),
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want to Delete the TODO '{pSelectedTODO.Title}'?"
                )
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.DeleteTODO(pSelectedTODO.Id)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error deleting TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRefreshTODOs(vSender As Object, vE As EventArgs)
            RefreshTODOs()
        End Sub
        
        Private Sub OnFilterChanged(vSender As Object, vE As EventArgs)
            ApplyFilters()
        End Sub

        ''' <summary>
        ''' Applies the current editor theme's colors to the custom-drawn list
        ''' </summary>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager

                If pListBox IsNot Nothing Then
                    pListBox.ThemeManager = vThemeManager
                End If

                If pSearchEntry IsNot Nothing Then
                    pSearchEntry.ThemeManager = vThemeManager
                End If

                For Each lButton As CustomDrawButton In New CustomDrawButton() {
                    pAddButton, pRefreshButton, pOverdueToggle,
                    pShowTodoToggle, pShowFixedToggle, pShowNoteToggle, pShowManualToggle, pScopeToggle
                }
                    If lButton IsNot Nothing Then
                        lButton.ThemeManager = vThemeManager
                    End If
                Next

                For Each lCombo As CustomDrawComboBox In New CustomDrawComboBox() {pPriorityCombo, pCategoryCombo, pStatusCombo}
                    If lCombo IsNot Nothing Then
                        lCombo.ThemeManager = vThemeManager
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"error setting theme manager: {ex.Message}")
            End Try
        End Sub

        ' Then update the SetProjectRoot method:
        Public Sub SetProjectRoot(vProjectRoot As String)
            Try
                ' Create TODO manager for this project
                If Not String.IsNullOrEmpty(vProjectRoot) Then
                    pProjectRoot = vProjectRoot
                    pTODOManager = New TODOManager(vProjectRoot)
                    #If DEBUG Then
                    Console.WriteLine("TODOManager New()")
                    #End If
                    AddHandler pTODOManager.TODOsChanged, AddressOf OnTODOsChanged

                    ' IMPORTANT: Refresh TODOs after setting project root
                    RefreshTODOs()
                End If

            Catch ex As Exception
                Console.WriteLine($"error setting project root: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates which file is considered "current" for the Current File Only scope
        ''' toggle, and re-applies filters immediately if that toggle is active
        ''' </summary>
        ''' <param name="vFilePath">Full path of the newly active editor's file</param>
        Public Sub SetCurrentFile(vFilePath As String)
            Try
                pCurrentFilePath = vFilePath
                If pScopeToggle IsNot Nothing AndAlso pScopeToggle.Active Then
                    ApplyFilters()
                End If
            Catch ex As Exception
                Console.WriteLine($"error setting current file: {ex.Message}")
            End Try
        End Sub

        Private Sub OnTODOsChanged()
            GLib.Idle.Add(Function()
                RefreshTODOs()
                Return False
            End Function)
        End Sub

        Friend Sub RefreshTODOs()
            Try
                #If DEBUG Then
                Console.WriteLine($"RefreshTODOs Started")
                #End If
                If pTODOManager Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("TODO Manager Not initialized")
                    #End If
                    Return
                End If

                ' Load TODOs
                Dim lTODOs As List(Of TODOItem) = pTODOManager.LoadTODOs()
                #If DEBUG Then
                Console.WriteLine($"loaded {lTODOs.Count} TODO items")
                #End If

                ' Store all TODOs, then run them back through the active filters/toggles
                ' (previously this bypassed the current filter state entirely on refresh)
                pAllTODOs = lTODOs
                ApplyFilters()

                #If DEBUG Then
                Console.WriteLine($"RefreshTODOs Finished")
                #End If

            Catch ex As Exception
                Console.WriteLine($"error refreshing TODOs: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Whether a TODO item should be visible given the TODO/FIXED/NOTE/Manual toggles
        ''' </summary>
        Private Function IsTagVisible(vTODO As TODOItem) As Boolean
            Select Case vTODO.CommentTag
                Case TODOItem.eCommentTag.eTodo
                    Return pShowTodoToggle.Active
                Case TODOItem.eCommentTag.eFixed
                    Return pShowFixedToggle.Active
                Case TODOItem.eCommentTag.eNote
                    Return pShowNoteToggle.Active
                Case Else ' eManual (and eUnspecified, which shouldn't occur)
                    Return pShowManualToggle.Active
            End Select
        End Function

        Private Sub ApplyFilters()
            Try
                If pTODOManager Is Nothing Then
                    Return
                End If

                Dim lSearchText As String = pSearchEntry.Text
                Dim lPriorityFilter As TODOItem.ePriority? = GetSelectedPriority()
                Dim lCategoryFilter As TODOItem.eCategory? = GetSelectedCategory()
                Dim lStatusFilter As TODOItem.eStatus? = GetSelectedStatus()
                Dim lShowOverdueOnly As Boolean = pOverdueToggle.Active

                Dim lFiltered As List(Of TODOItem) = pTODOManager.FilterTODOs(pAllTODOs, lSearchText, lPriorityFilter, lCategoryFilter, lStatusFilter, lShowOverdueOnly)

                ' Comment-tag visibility toggles (TODO/FIXED/NOTE/Manual)
                lFiltered = lFiltered.Where(Function(t) IsTagVisible(t)).ToList()

                ' Current File Only scope
                If pScopeToggle.Active Then
                    If String.IsNullOrEmpty(pCurrentFilePath) Then
                        lFiltered = New List(Of TODOItem)
                    Else
                        lFiltered = lFiltered.Where(Function(t) t.SourceType = TODOItem.eSourceType.eCodeComment AndAlso
                                                                 String.Equals(t.SourceFile, pCurrentFilePath, StringComparison.OrdinalIgnoreCase)).ToList()
                    End If
                End If

                pFilteredTODOs = lFiltered

                UpdateListBox()
                UpdateStatusBar()

            Catch ex As Exception
                Console.WriteLine($"error applying filters: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetSelectedPriority() As TODOItem.ePriority?
            Select Case pPriorityCombo.Active
                Case 1
                    Return TODOItem.ePriority.eCritical
                Case 2
                    Return TODOItem.ePriority.eHigh
                Case 3
                    Return TODOItem.ePriority.eMedium
                Case 4
                    Return TODOItem.ePriority.eLow
                Case Else
                    Return Nothing
            End Select
        End Function
        
        Private Function GetSelectedCategory() As TODOItem.eCategory?
            Select Case pCategoryCombo.Active
                Case 1
                    Return TODOItem.eCategory.eBug
                Case 2
                    Return TODOItem.eCategory.eFeature
                Case 3
                    Return TODOItem.eCategory.eDocumentation
                Case 4
                    Return TODOItem.eCategory.eRefactor
                Case 5
                    Return TODOItem.eCategory.eTesting
                Case 6
                    Return TODOItem.eCategory.ePerformance
                Case 7
                    Return TODOItem.eCategory.eSecurity
                Case 8
                    Return TODOItem.eCategory.eUI
                Case 9
                    Return TODOItem.eCategory.eOther
                Case Else
                    Return Nothing
            End Select
        End Function
        
        Private Function GetSelectedStatus() As TODOItem.eStatus?
            Select Case pStatusCombo.Active
                Case 1
                    Return TODOItem.eStatus.ePending
                Case 2
                    Return TODOItem.eStatus.eInProgress
                Case 3
                    Return TODOItem.eStatus.eCompleted
                Case 4
                    Return TODOItem.eStatus.eCancelled
                Case Else
                    Return Nothing
            End Select
        End Function
        
        ''' <summary>
        ''' Rebuilds the tree grouped by source file (sorted alphabetically, items
        ''' sequential by line number within each file), with manually-added tasks in
        ''' their own trailing "Manual Tasks" group
        ''' </summary>
        Private Sub UpdateListBox()
            Try
                pListBox.Clear()

                ' Show empty state if no TODOs or no manager
                If pTODOManager Is Nothing OrElse pFilteredTODOs.Count = 0 Then
                    ShowEmptyState()
                    Return
                End If

                Dim lFileGroups = pFilteredTODOs.
                    Where(Function(t) t.SourceType = TODOItem.eSourceType.eCodeComment AndAlso Not String.IsNullOrEmpty(t.SourceFile)).
                    GroupBy(Function(t) t.SourceFile).
                    OrderBy(Function(g) GetRelativeDisplayPath(g.Key), StringComparer.OrdinalIgnoreCase)

                Dim lFolderIcon As Gdk.Pixbuf = LoadIconByName("folder-symbolic")

                For Each lGroup In lFileGroups
                    Dim lItems As List(Of TODOItem) = lGroup.OrderBy(Function(t) t.SourceLine).ToList()
                    Dim lRelativePath As String = GetRelativeDisplayPath(lGroup.Key)
                    Dim lGroupItem As New ListBoxItem($"{lRelativePath}  ({lItems.Count})") With {
                        .IsGroupHeader = True,
                        .IndentLevel = 0
                    }
                    If lFolderIcon IsNot Nothing Then lGroupItem.Icons.Add(lFolderIcon)
                    pListBox.AddItem(lGroupItem)

                    For Each lTODO In lItems
                        AppendItemRow(lTODO)
                    Next
                Next

                Dim lManualItems As List(Of TODOItem) = pFilteredTODOs.
                    Where(Function(t) t.SourceType <> TODOItem.eSourceType.eCodeComment OrElse String.IsNullOrEmpty(t.SourceFile)).
                    OrderByDescending(Function(t) t.Priority).
                    ThenBy(Function(t) t.Title, StringComparer.OrdinalIgnoreCase).
                    ToList()

                If lManualItems.Count > 0 Then
                    Dim lManualIcon As Gdk.Pixbuf = LoadIconByName("view-list-symbolic")
                    Dim lManualGroupItem As New ListBoxItem($"Manual Tasks  ({lManualItems.Count})") With {
                        .IsGroupHeader = True,
                        .IndentLevel = 0
                    }
                    If lManualIcon IsNot Nothing Then lManualGroupItem.Icons.Add(lManualIcon)
                    pListBox.AddItem(lManualGroupItem)

                    For Each lTODO In lManualItems
                        AppendItemRow(lTODO)
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"error updating tree view: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Appends a single TODO item as a child row under whichever file/"Manual Tasks"
        ''' group header was most recently added
        ''' </summary>
        Private Sub AppendItemRow(vTODO As TODOItem)
            Try
                Dim lPriorityIcon As Gdk.Pixbuf = CreatePriorityIcon(vTODO.Priority)
                Dim lCategoryIcon As Gdk.Pixbuf = CreateCategoryIcon(vTODO.Category)
                Dim lDueDateText As String = If(vTODO.DueDate.HasValue, vTODO.DueDate.Value.ToString("yyyy-MM-dd"), "")

                Dim lSecondaryText As String = vTODO.GetStatusDisplayText()
                If Not String.IsNullOrEmpty(lDueDateText) Then
                    lSecondaryText &= $"  ({lDueDateText})"
                End If

                Dim lItem As New ListBoxItem(vTODO.GetDisplayTitle(), vTODO.Id) With {
                    .IndentLevel = 1,
                    .SecondaryText = lSecondaryText
                }
                If lPriorityIcon IsNot Nothing Then lItem.Icons.Add(lPriorityIcon)
                If lCategoryIcon IsNot Nothing Then lItem.Icons.Add(lCategoryIcon)

                pListBox.AddItem(lItem)
            Catch ex As Exception
                Console.WriteLine($"error appending TODO item row: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Formats a source file's full path relative to the project root for display
        ''' as a group header
        ''' </summary>
        Private Function GetRelativeDisplayPath(vFilePath As String) As String
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then Return vFilePath
                Return System.IO.Path.GetRelativePath(pProjectRoot, vFilePath)
            Catch ex As Exception
                Return vFilePath
            End Try
        End Function

        Private Function LoadIconByName(vIconName As String) As Gdk.Pixbuf
            Try
                Dim lIconTheme As Gtk.IconTheme = Gtk.IconTheme.Default
                Return lIconTheme.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"error creating group Icon: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function CreatePriorityIcon(vPriority As TODOItem.ePriority) As Gdk.Pixbuf
            Try
                Dim lIconName As String = "dialog-information-symbolic"
                
                Select Case vPriority
                    Case TODOItem.ePriority.eLow
                        lIconName = "dialog-information-symbolic"
                    Case TODOItem.ePriority.eMedium
                        lIconName = "dialog-warning-symbolic"
                    Case TODOItem.ePriority.eHigh
                        lIconName = "dialog-error-symbolic"
                    Case TODOItem.ePriority.eCritical
                        lIconName = "security-high-symbolic"
                End Select
                
                Dim lIconTheme As Gtk.IconTheme = Gtk.IconTheme.Default
                Return lIconTheme.LoadIcon(lIconName, 16, IconLookupFlags.UseBuiltin)
                
            Catch ex As Exception
                Console.WriteLine($"error creating Priority Icon: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function CreateCategoryIcon(vCategory As TODOItem.eCategory) As Gdk.Pixbuf
            Try
                Dim lIconName As String = GetCategoryIconName(vCategory)
                Dim lIconTheme As Gtk.IconTheme = Gtk.IconTheme.Default
                Return lIconTheme.LoadIcon(lIconName, 16, IconLookupFlags.UseBuiltin)
                
            Catch ex As Exception
                Console.WriteLine($"error creating Category Icon: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function GetCategoryIconName(vCategory As TODOItem.eCategory) As String
            Select Case vCategory
                Case TODOItem.eCategory.eBug
                    Return "bug-symbolic"
                Case TODOItem.eCategory.eFeature
                    Return "starred-symbolic"
                Case TODOItem.eCategory.eDocumentation
                    Return "help-browser-symbolic"
                Case TODOItem.eCategory.eRefactor
                    Return "preferences-system-symbolic"
                Case TODOItem.eCategory.eTesting
                    Return "checkbox-symbolic"
                Case TODOItem.eCategory.ePerformance
                    Return "applications-utilities-symbolic"
                Case TODOItem.eCategory.eSecurity
                    Return "security-high-symbolic"
                Case TODOItem.eCategory.eUI
                    Return "applications-graphics-symbolic"
                Case Else
                    Return "text-x-generic-symbolic"
            End Select
        End Function
        
        Private Sub UpdateStatusBar()
            Try
                If pStatusBar Is Nothing Then pStatusBar = New Label

                If pAllTODOs Is Nothing OrElse pFilteredTODOs Is Nothing Then
                    pStatusBar.Text = "Ready"
                    Return
                End If
                
                If pTODOManager IsNot Nothing Then
                    Dim lStats = pTODOManager.GetTODOStatistics(pAllTODOs)
                    Dim lFilteredCount = pFilteredTODOs.Count
                    Dim lTotalCount = lStats("Total")
                    
                    Dim lStatusText As String = $"Showing {lFilteredCount} Of {lTotalCount} TODOs"
                    
                    If lStats("Overdue") > 0 Then
                        lStatusText &= $" | {lStats("Overdue")} Overdue"
                    End If
                    
                    If lStats("Critical") > 0 Then
                        lStatusText &= $" | {lStats("Critical")} Critical"
                    End If
                    
                    pStatusBar.Text = lStatusText
                Else
                    #If DEBUG Then
                    Console.WriteLine($"No project loaded in TodoPanel.UpdateStatusBar")
                    #End If
                    pStatusBar.Text = "No project loaded"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Status bar: {ex.Message}")
                pStatusBar.Text = "error"
            End Try
        End Sub
        
        Private Sub ShowEmptyState()
            Try
                pListBox.Clear()

                Dim lMessage As String = If(pTODOManager Is Nothing,
                    "No project loaded - Open a project To view TODOs",
                    "No TODOs found - Click Add To create a New TODO")

                pListBox.AddItem(lMessage)

            Catch ex As Exception
                Console.WriteLine($"error showing empty state: {ex.Message}")
            End Try
        End Sub

    End Class
    
    ' TODO Edit Dialog
    Public Class TODOEditDialog
        Inherits Dialog
        
        Private pTitleEntry As CustomDrawTextBox
        Private pDescriptionTextView As TextView
        Private pPriorityCombo As CustomDrawComboBox
        Private pCategoryCombo As CustomDrawComboBox
        Private pStatusCombo As CustomDrawComboBox
        Private pDueDateCalendar As Calendar
        Private pDueDateCheckButton As CheckButton
        Private pProgressScale As Scale
        Private pTagsEntry As CustomDrawTextBox
        Private pThemeManager As ThemeManager
        
        Public ReadOnly Property TODOTitle As String
            Get
                Return pTitleEntry.Text
            End Get
        End Property
        
        Public ReadOnly Property TODODescription As String
            Get
                Return pDescriptionTextView.Buffer.Text
            End Get
        End Property
        
        Public ReadOnly Property TODOPriority As TODOItem.ePriority
            Get
                Select Case pPriorityCombo.Active
                    Case 0
                        Return TODOItem.ePriority.eLow
                    Case 1
                        Return TODOItem.ePriority.eMedium
                    Case 2
                        Return TODOItem.ePriority.eHigh
                    Case 3
                        Return TODOItem.ePriority.eCritical
                    Case Else
                        Return TODOItem.ePriority.eMedium
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODOCategory As TODOItem.eCategory
            Get
                Select Case pCategoryCombo.Active
                    Case 0
                        Return TODOItem.eCategory.eBug
                    Case 1
                        Return TODOItem.eCategory.eFeature
                    Case 2
                        Return TODOItem.eCategory.eDocumentation
                    Case 3
                        Return TODOItem.eCategory.eRefactor
                    Case 4
                        Return TODOItem.eCategory.eTesting
                    Case 5
                        Return TODOItem.eCategory.ePerformance
                    Case 6
                        Return TODOItem.eCategory.eSecurity
                    Case 7
                        Return TODOItem.eCategory.eUI
                    Case 8
                        Return TODOItem.eCategory.eOther
                    Case Else
                        Return TODOItem.eCategory.eOther
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODOStatus As TODOItem.eStatus
            Get
                Select Case pStatusCombo.Active
                    Case 0
                        Return TODOItem.eStatus.ePending
                    Case 1
                        Return TODOItem.eStatus.eInProgress
                    Case 2
                        Return TODOItem.eStatus.eCompleted
                    Case 3
                        Return TODOItem.eStatus.eCancelled
                    Case Else
                        Return TODOItem.eStatus.ePending
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODODueDate As DateTime?
            Get
                If pDueDateCheckButton.Active Then
                    Dim lYear As UInteger
                    Dim lMonth As UInteger
                    Dim lDay As UInteger
                    pDueDateCalendar.GetDate(lYear, lMonth, lDay)
                    Return New DateTime(CInt(lYear), CInt(lMonth) + 1, CInt(lDay))
                Else
                    Return Nothing
                End If
            End Get
        End Property
        
        Public ReadOnly Property TODOProgress As Integer
            Get
                Return CInt(pProgressScale.Value)
            End Get
        End Property
        
        Public ReadOnly Property TODOTags As List(Of String)
            Get
                Dim lTags As New List(Of String)
                If Not String.IsNullOrEmpty(pTagsEntry.Text) Then
                    lTags.AddRange(pTagsEntry.Text.Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrEmpty(t)))
                End If
                Return lTags
            End Get
        End Property
        
        Public Sub New(vTODO As TODOItem, vTitle As String, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New(vTitle, Nothing, DialogFlags.Modal)

            pThemeManager = vThemeManager

            SetDefaultSize(500, 600)
            BorderWidth = 10

            BuildUI()

            ' Populate if editing
            If vTODO IsNot Nothing Then
                PopulateFields(vTODO)
            End If

            ' Add buttons - custom-drawn, wired directly to Respond()
            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.Halign = Align.End
            lButtonBox.BorderWidth = 6

            Dim lCancelButton As New CustomDrawButton("Cancel")
            lCancelButton.ThemeManager = pThemeManager
            AddHandler lCancelButton.Clicked, Sub() Respond(ResponseType.Cancel)
            lButtonBox.PackStart(lCancelButton, False, False, 0)

            Dim lOkButton As New CustomDrawButton("OK")
            lOkButton.ThemeManager = pThemeManager
            AddHandler lOkButton.Clicked, Sub() Respond(ResponseType.Ok)
            lButtonBox.PackStart(lOkButton, False, False, 0)

            Dim lContentBox As Box = TryCast(ContentArea, Box)
            If lContentBox IsNot Nothing Then
                lContentBox.PackStart(lButtonBox, False, False, 0)
            End If

            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Try
                Dim lVBox As New Box(Orientation.Vertical, 10)
                
                ' Title
                Dim lTitleFrame As New Frame("Title")
                pTitleEntry = New CustomDrawTextBox()
                pTitleEntry.ThemeManager = pThemeManager
                lTitleFrame.Add(pTitleEntry)
                lVBox.PackStart(lTitleFrame, False, False, 0)
                
                ' Description
                Dim lDescFrame As New Frame("Description")
                pDescriptionTextView = New TextView()
                pDescriptionTextView.WrapMode = WrapMode.Word
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolled.SetSizeRequest(-1, 150)
                lScrolled.Add(pDescriptionTextView)
                lDescFrame.Add(lScrolled)
                lVBox.PackStart(lDescFrame, True, True, 0)
                
                ' Priority and Category
                Dim lPriCatBox As New Box(Orientation.Horizontal, 10)
                
                Dim lPriorityFrame As New Frame("Priority")
                pPriorityCombo = New CustomDrawComboBox()
                pPriorityCombo.ThemeManager = pThemeManager
                pPriorityCombo.AppendText("Low")
                pPriorityCombo.AppendText("Medium")
                pPriorityCombo.AppendText("High")
                pPriorityCombo.AppendText("Critical")
                pPriorityCombo.Active = 1 ' Default to Medium
                lPriorityFrame.Add(pPriorityCombo)
                lPriCatBox.PackStart(lPriorityFrame, True, True, 0)
                
                Dim lCategoryFrame As New Frame("Category")
                pCategoryCombo = New CustomDrawComboBox()
                pCategoryCombo.ThemeManager = pThemeManager
                pCategoryCombo.AppendText("Bug")
                pCategoryCombo.AppendText("Feature")
                pCategoryCombo.AppendText("documentation")
                pCategoryCombo.AppendText("Refactor")
                pCategoryCombo.AppendText("Testing")
                pCategoryCombo.AppendText("Performance")
                pCategoryCombo.AppendText("Security")
                pCategoryCombo.AppendText("UI/UX")
                pCategoryCombo.AppendText("Other")
                pCategoryCombo.Active = 8 ' Default to Other
                lCategoryFrame.Add(pCategoryCombo)
                lPriCatBox.PackStart(lCategoryFrame, True, True, 0)
                
                lVBox.PackStart(lPriCatBox, False, False, 0)
                
                ' Status and Progress
                Dim lStatusProgressBox As New Box(Orientation.Horizontal, 10)
                
                Dim lStatusFrame As New Frame("Status")
                pStatusCombo = New CustomDrawComboBox()
                pStatusCombo.ThemeManager = pThemeManager
                pStatusCombo.AppendText("Pending")
                pStatusCombo.AppendText("in Progress")
                pStatusCombo.AppendText("Completed")
                pStatusCombo.AppendText("Cancelled")
                pStatusCombo.Active = 0 ' Default to Pending
                lStatusFrame.Add(pStatusCombo)
                lStatusProgressBox.PackStart(lStatusFrame, True, True, 0)
                
                Dim lProgressFrame As New Frame("Progress %")
                pProgressScale = New Scale(Orientation.Horizontal, 0, 100, 5)
                pProgressScale.DrawValue = True
                pProgressScale.ValuePos = PositionType.Right
                lProgressFrame.Add(pProgressScale)
                lStatusProgressBox.PackStart(lProgressFrame, True, True, 0)
                
                lVBox.PackStart(lStatusProgressBox, False, False, 0)
                
                ' Due Date
                Dim lDueDateFrame As New Frame("Due Date")
                Dim lDueDateBox As New Box(Orientation.Vertical, 5)
                pDueDateCheckButton = New CheckButton("Set due Date")
                AddHandler pDueDateCheckButton.Toggled, AddressOf OnDueDateToggled
                lDueDateBox.PackStart(pDueDateCheckButton, False, False, 0)
                
                pDueDateCalendar = New Calendar()
                pDueDateCalendar.Sensitive = False
                lDueDateBox.PackStart(pDueDateCalendar, False, False, 0)
                lDueDateFrame.Add(lDueDateBox)
                lVBox.PackStart(lDueDateFrame, False, False, 0)
                
                ' Tags
                Dim lTagsFrame As New Frame("Tags (comma-separated)")
                pTagsEntry = New CustomDrawTextBox("e.g. urgent, client, backend")
                pTagsEntry.ThemeManager = pThemeManager
                lTagsFrame.Add(pTagsEntry)
                lVBox.PackStart(lTagsFrame, False, False, 0)
                
                ' Add to dialog content area
                ContentArea.Add(lVBox)
                
            Catch ex As Exception
                Console.WriteLine($"error building TODO dialog UI: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDueDateToggled(vSender As Object, vE As EventArgs)
            pDueDateCalendar.Sensitive = pDueDateCheckButton.Active
        End Sub
        
        Private Sub PopulateFields(vTODO As TODOItem)
            Try
                pTitleEntry.Text = vTODO.Title
                pDescriptionTextView.Buffer.Text = vTODO.Description
                
                ' Set priority
                Select Case vTODO.Priority
                    Case TODOItem.ePriority.eLow
                        pPriorityCombo.Active = 0
                    Case TODOItem.ePriority.eMedium
                        pPriorityCombo.Active = 1
                    Case TODOItem.ePriority.eHigh
                        pPriorityCombo.Active = 2
                    Case TODOItem.ePriority.eCritical
                        pPriorityCombo.Active = 3
                End Select
                
                ' Set category
                Select Case vTODO.Category
                    Case TODOItem.eCategory.eBug
                        pCategoryCombo.Active = 0
                    Case TODOItem.eCategory.eFeature
                        pCategoryCombo.Active = 1
                    Case TODOItem.eCategory.eDocumentation
                        pCategoryCombo.Active = 2
                    Case TODOItem.eCategory.eRefactor
                        pCategoryCombo.Active = 3
                    Case TODOItem.eCategory.eTesting
                        pCategoryCombo.Active = 4
                    Case TODOItem.eCategory.ePerformance
                        pCategoryCombo.Active = 5
                    Case TODOItem.eCategory.eSecurity
                        pCategoryCombo.Active = 6
                    Case TODOItem.eCategory.eUI
                        pCategoryCombo.Active = 7
                    Case TODOItem.eCategory.eOther
                        pCategoryCombo.Active = 8
                End Select
                
                ' Set status
                Select Case vTODO.Status
                    Case TODOItem.eStatus.ePending
                        pStatusCombo.Active = 0
                    Case TODOItem.eStatus.eInProgress
                        pStatusCombo.Active = 1
                    Case TODOItem.eStatus.eCompleted
                        pStatusCombo.Active = 2
                    Case TODOItem.eStatus.eCancelled
                        pStatusCombo.Active = 3
                End Select
                
                ' Set progress
                pProgressScale.Value = vTODO.Progress
                
                ' Set due date
                If vTODO.DueDate.HasValue Then
                    pDueDateCheckButton.Active = True
                    pDueDateCalendar.Sensitive = True
                    pDueDateCalendar.SelectMonth(CUInt(vTODO.DueDate.Value.Month - 1), CUInt(vTODO.DueDate.Value.Year))
                    pDueDateCalendar.SelectDay(CUInt(vTODO.DueDate.Value.Day))
                End If
                
                ' Set tags
                If vTODO.Tags.Count > 0 Then
                    pTagsEntry.Text = String.Join(", ", vTODO.Tags)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error populating TODO fields: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
